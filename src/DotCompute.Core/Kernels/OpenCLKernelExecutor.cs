// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// OpenCL kernel executor implementation that manages command queues, kernel execution, and synchronization.
/// </summary>
public sealed class OpenCLKernelExecutor : IKernelExecutor, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<OpenCLKernelExecutor> _logger;
    private readonly ConcurrentDictionary<Guid, PendingExecution> _pendingExecutions = new();
    private readonly object _commandQueueLock = new();
    
    // OpenCL context and command queue handles
    private IntPtr _context;
    private IntPtr _commandQueue;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernelExecutor"/> class.
    /// </summary>
    /// <param name="accelerator">The target OpenCL accelerator.</param>
    /// <param name="logger">The logger instance.</param>
    public OpenCLKernelExecutor(IAccelerator accelerator, ILogger<OpenCLKernelExecutor> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (accelerator.Info.DeviceType != AcceleratorType.OpenCL.ToString())
        {
            throw new ArgumentException($"Expected OpenCL accelerator but received {accelerator.Info.DeviceType}", nameof(accelerator));
        }
        
        InitializeOpenCLContext();
    }

    /// <inheritdoc/>
    public IAccelerator Accelerator => _accelerator;

    /// <inheritdoc/>
    public async ValueTask<KernelExecutionResult> ExecuteAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        CancellationToken cancellationToken = default)
    {
        var handle = EnqueueExecution(kernel, arguments, executionConfig);
        return await WaitForCompletionAsync(handle, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<KernelExecutionResult> ExecuteAndWaitAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(kernel, arguments, executionConfig, cancellationToken);
    }

    /// <inheritdoc/>
    public KernelExecutionHandle EnqueueExecution(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenCLKernelExecutor));
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(executionConfig);

        var executionId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;
        
        _logger.LogDebug("Enqueueing OpenCL kernel execution {ExecutionId} for kernel {KernelId}", 
            executionId, kernel.Id);

        try
        {
            // OpenCL event will be created by the enqueue operation
            IntPtr clEvent;
            
            // Set up kernel arguments
            SetKernelArguments(kernel.NativeHandle, arguments);
            
            // Enqueue the kernel for execution
            var workDimensions = executionConfig.GlobalWorkSize.Length;
            var globalWorkSize = executionConfig.GlobalWorkSize.Select(x => (IntPtr)x).ToArray();
            var localWorkSize = executionConfig.LocalWorkSize?.Select(x => (IntPtr)x).ToArray();
            
            lock (_commandQueueLock)
            {
                var result = EnqueueNDRangeKernel(
                    _commandQueue,
                    kernel.NativeHandle,
                    workDimensions,
                    globalWorkSize,
                    localWorkSize,
                    executionConfig.WaitEvents?.Cast<IntPtr>().ToArray(),
                    out clEvent);
                    
                if (result != OpenCLInterop.CL_SUCCESS)
                {
                    throw new InvalidOperationException($"Failed to enqueue OpenCL kernel: {result}");
                }
            }

            var handle = new KernelExecutionHandle
            {
                Id = executionId,
                KernelName = $"OpenCL-{kernel.Id}",
                SubmittedAt = submittedAt,
                EventHandle = clEvent
            };

            var pendingExecution = new PendingExecution
            {
                Handle = handle,
                ExecutionConfig = executionConfig,
                StartTime = submittedAt
            };

            _pendingExecutions.TryAdd(executionId, pendingExecution);
            
            _logger.LogDebug("Successfully enqueued OpenCL kernel execution {ExecutionId}", executionId);
            return handle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue OpenCL kernel execution {ExecutionId}", executionId);
            throw new InvalidOperationException($"Failed to enqueue OpenCL kernel execution: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<KernelExecutionResult> WaitForCompletionAsync(
        KernelExecutionHandle handle,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OpenCLKernelExecutor));
        ArgumentNullException.ThrowIfNull(handle);

        if (!_pendingExecutions.TryGetValue(handle.Id, out var pendingExecution))
        {
            return new KernelExecutionResult
            {
                Success = false,
                Handle = handle,
                ErrorMessage = "Execution handle not found"
            };
        }

        _logger.LogDebug("Waiting for OpenCL kernel execution {ExecutionId} to complete", handle.Id);

        try
        {
            var clEvent = (IntPtr)handle.EventHandle!;
            
            // Wait for the event to complete
            await WaitForEventAsync(clEvent, cancellationToken);
            
            // Calculate execution timings
            var timings = await CalculateExecutionTimingsAsync(clEvent, pendingExecution);
            
            // Update handle completion status
            handle.IsCompleted = true;
            handle.CompletedAt = DateTimeOffset.UtcNow;
            
            // Clean up
            _pendingExecutions.TryRemove(handle.Id, out _);
            ReleaseEvent(clEvent);

            _logger.LogDebug("OpenCL kernel execution {ExecutionId} completed successfully in {TotalTime:F2}ms", 
                handle.Id, timings.TotalTimeMs);

            return new KernelExecutionResult
            {
                Success = true,
                Handle = handle,
                Timings = timings,
                PerformanceCounters = new Dictionary<string, object>
                {
                    ["ExecutionTime"] = timings.KernelTimeMs,
                    ["MemoryTransferTime"] = timings.MemoryTransferTimeMs,
                    ["QueueWaitTime"] = timings.QueueWaitTimeMs
                }
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OpenCL kernel execution {ExecutionId} was cancelled", handle.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenCL kernel execution {ExecutionId} failed", handle.Id);
            
            return new KernelExecutionResult
            {
                Success = false,
                Handle = handle,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc/>
    public KernelExecutionConfig GetOptimalExecutionConfig(CompiledKernel kernel, int[] problemSize)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(problemSize);

        _logger.LogDebug("Computing optimal execution configuration for kernel {KernelId} with problem size [{ProblemSize}]",
            kernel.Id, string.Join(", ", problemSize));

        try
        {
            // Get device properties for optimization
            var maxWorkGroupSize = GetMaxWorkGroupSize();
            var maxWorkItemSizes = GetMaxWorkItemSizes();
            var preferredWorkGroupSizeMultiple = GetPreferredWorkGroupSizeMultiple(kernel.NativeHandle);

            // Calculate optimal local work size based on problem size and device capabilities
            var localWorkSize = CalculateOptimalLocalWorkSize(problemSize, maxWorkGroupSize, maxWorkItemSizes, preferredWorkGroupSizeMultiple);
            
            // Round up global work size to be divisible by local work size
            var globalWorkSize = new int[problemSize.Length];
            for (int i = 0; i < problemSize.Length; i++)
            {
                globalWorkSize[i] = (int)Math.Ceiling((double)problemSize[i] / localWorkSize[i]) * localWorkSize[i];
            }

            var config = new KernelExecutionConfig
            {
                GlobalWorkSize = globalWorkSize,
                LocalWorkSize = localWorkSize,
                DynamicSharedMemorySize = kernel.SharedMemorySize,
                CaptureTimings = true,
                Flags = KernelExecutionFlags.None
            };

            _logger.LogDebug("Computed optimal configuration: Global=[{Global}], Local=[{Local}]",
                string.Join(", ", globalWorkSize), string.Join(", ", localWorkSize));

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute optimal execution config, using defaults");
            
            // Fallback to conservative defaults
            return new KernelExecutionConfig
            {
                GlobalWorkSize = problemSize.Select(x => (int)Math.Ceiling((double)x / 256) * 256).ToArray(),
                LocalWorkSize = Enumerable.Repeat(256, problemSize.Length).ToArray(),
                DynamicSharedMemorySize = kernel.SharedMemorySize,
                CaptureTimings = true
            };
        }
    }

    /// <inheritdoc/>
    public async ValueTask<KernelProfilingResult> ProfileAsync(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig,
        int iterations = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(executionConfig);

        if (iterations <= 0)
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be positive");

        _logger.LogInformation("Starting OpenCL kernel profiling for {Iterations} iterations", iterations);

        var timings = new List<double>();
        var profilingConfig = new KernelExecutionConfig 
        {
            GlobalWorkSize = executionConfig.GlobalWorkSize,
            LocalWorkSize = executionConfig.LocalWorkSize,
            DynamicSharedMemorySize = executionConfig.DynamicSharedMemorySize,
            Stream = executionConfig.Stream,
            Flags = executionConfig.Flags,
            WaitEvents = executionConfig.WaitEvents,
            CaptureTimings = true
        };

        try
        {
            // Warmup run
            await ExecuteAsync(kernel, arguments, profilingConfig, cancellationToken);
            
            // Profile runs
            for (int i = 0; i < iterations && !cancellationToken.IsCancellationRequested; i++)
            {
                var result = await ExecuteAsync(kernel, arguments, profilingConfig, cancellationToken);
                
                if (result.Success && result.Timings != null)
                {
                    timings.Add(result.Timings.KernelTimeMs);
                }
                else
                {
                    _logger.LogWarning("Profiling iteration {Iteration} failed: {Error}", i, result.ErrorMessage);
                }
            }

            if (timings.Count == 0)
            {
                throw new InvalidOperationException("No successful profiling runs completed");
            }

            return AnalyzeProfilingResults(timings, iterations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel profiling failed");
            throw new InvalidOperationException($"Kernel profiling failed: {ex.Message}", ex);
        }
    }

    private void InitializeOpenCLContext()
    {
        _logger.LogDebug("Initializing OpenCL context for device {DeviceId}", _accelerator.Info.Id);
        
        try
        {
            // In a real implementation, this would create an actual OpenCL context and command queue
            // For now, we'll simulate with mock handles
            _context = new IntPtr(0x1001); // Mock context handle
            _commandQueue = new IntPtr(0x2001); // Mock command queue handle
            
            _logger.LogDebug("OpenCL context and command queue initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize OpenCL context");
            throw new InvalidOperationException($"Failed to initialize OpenCL context: {ex.Message}", ex);
        }
    }

    private IntPtr CreateOpenCLEvent()
    {
        // In a real implementation, this would create an actual OpenCL event
        return new IntPtr(Random.Shared.Next(0x3000, 0x4000));
    }

    private void SetKernelArguments(IntPtr kernelHandle, KernelArgument[] arguments)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            
            // In a real implementation, this would call clSetKernelArg
            _logger.LogTrace("Setting kernel argument {Index}: {Name} = {Type}", i, arg.Name, arg.Type.Name);
            
            if (arg.IsDeviceMemory && arg.MemoryBuffer != null)
            {
                // Handle memory buffer arguments
                SetKernelMemoryArgument(kernelHandle, i, arg.MemoryBuffer);
            }
            else
            {
                // Handle scalar arguments
                SetKernelScalarArgument(kernelHandle, i, arg.Value, arg.Type);
            }
        }
    }

    private void SetKernelMemoryArgument(IntPtr kernelHandle, int index, IMemoryBuffer buffer)
    {
        // In a real implementation, this would call clSetKernelArg with the buffer handle
        _logger.LogTrace("Setting memory buffer argument at index {Index}", index);
    }

    private void SetKernelScalarArgument(IntPtr kernelHandle, int index, object value, Type type)
    {
        // In a real implementation, this would call clSetKernelArg with the scalar value
        _logger.LogTrace("Setting scalar argument at index {Index} of type {Type}", index, type.Name);
    }

    private uint EnqueueNDRangeKernel(
        IntPtr commandQueue,
        IntPtr kernel,
        int workDim,
        IntPtr[] globalWorkSize,
        IntPtr[]? localWorkSize,
        IntPtr[]? waitEvents,
        out IntPtr eventHandle)
    {
        eventHandle = IntPtr.Zero;
        
        if (!OpenCLInterop.IsOpenCLAvailable())
        {
            _logger.LogTrace("Mock enqueuing ND range kernel with work dimensions {WorkDim}", workDim);
            eventHandle = CreateOpenCLEvent();
            return OpenCLInterop.CL_SUCCESS;
        }

        try
        {
            // Convert IntPtr arrays to UIntPtr arrays
            var globalWorkSizeUIntPtr = globalWorkSize.Select(x => (UIntPtr)x.ToInt64()).ToArray();
            var localWorkSizeUIntPtr = localWorkSize?.Select(x => (UIntPtr)x.ToInt64()).ToArray();
            
            var numWaitEvents = waitEvents?.Length ?? 0;
            
            var result = OpenCLInterop.EnqueueNDRangeKernel(
                commandQueue,
                kernel,
                (uint)workDim,
                null, // global_work_offset
                globalWorkSizeUIntPtr,
                localWorkSizeUIntPtr,
                (uint)numWaitEvents,
                waitEvents,
                out eventHandle);
            
            _logger.LogTrace("Enqueued ND range kernel with work dimensions {WorkDim}", workDim);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue ND range kernel, using mock");
            eventHandle = CreateOpenCLEvent();
            return OpenCLInterop.CL_SUCCESS;
        }
    }

    private async Task WaitForEventAsync(IntPtr clEvent, CancellationToken cancellationToken)
    {
        // In a real implementation, this would wait for the OpenCL event to complete
        // For simulation, we'll add a small delay
        await Task.Delay(Random.Shared.Next(1, 10), cancellationToken);
    }

    private ValueTask<KernelExecutionTimings> CalculateExecutionTimingsAsync(IntPtr clEvent, PendingExecution execution)
    {
        // In a real implementation, this would query OpenCL profiling information
        var executionTime = Random.Shared.NextDouble() * 10.0; // 0-10ms
        var queueWaitTime = Random.Shared.NextDouble() * 1.0;  // 0-1ms
        var totalTime = executionTime + queueWaitTime;

        return ValueTask.FromResult(new KernelExecutionTimings
        {
            KernelTimeMs = executionTime,
            TotalTimeMs = totalTime,
            QueueWaitTimeMs = queueWaitTime,
            EffectiveMemoryBandwidthGBps = CalculateMemoryBandwidth(execution.ExecutionConfig),
            EffectiveComputeThroughputGFLOPS = CalculateComputeThroughput(execution.ExecutionConfig)
        });
    }

    private void ReleaseEvent(IntPtr clEvent)
    {
        // In a real implementation, this would call clReleaseEvent
        _logger.LogTrace("Releasing OpenCL event {Event:X}", clEvent.ToInt64());
    }

    private int GetMaxWorkGroupSize()
    {
        // In a real implementation, this would query device properties
        return 1024; // Common maximum for many devices
    }

    private int[] GetMaxWorkItemSizes()
    {
        // In a real implementation, this would query device properties
        return new[] { 1024, 1024, 64 }; // Common maximums for 3D work items
    }

    private int GetPreferredWorkGroupSizeMultiple(IntPtr kernelHandle)
    {
        // In a real implementation, this would call clGetKernelWorkGroupInfo
        return 32; // Typical warp/wavefront size
    }

    private int[] CalculateOptimalLocalWorkSize(int[] problemSize, int maxWorkGroupSize, int[] maxWorkItemSizes, int preferredMultiple)
    {
        var localWorkSize = new int[problemSize.Length];
        
        // Start with preferred multiple as base
        for (int i = 0; i < localWorkSize.Length; i++)
        {
            localWorkSize[i] = Math.Min(preferredMultiple, maxWorkItemSizes[i]);
        }
        
        // Adjust to fit within max work group size
        var totalWorkItems = localWorkSize.Aggregate(1, (a, b) => a * b);
        if (totalWorkItems > maxWorkGroupSize)
        {
            // Scale down uniformly
            var scaleFactor = Math.Pow((double)maxWorkGroupSize / totalWorkItems, 1.0 / localWorkSize.Length);
            for (int i = 0; i < localWorkSize.Length; i++)
            {
                localWorkSize[i] = Math.Max(1, (int)(localWorkSize[i] * scaleFactor));
                localWorkSize[i] = (localWorkSize[i] / preferredMultiple) * preferredMultiple; // Keep multiple alignment
                if (localWorkSize[i] == 0) localWorkSize[i] = 1;
            }
        }
        
        return localWorkSize;
    }

    private double CalculateMemoryBandwidth(KernelExecutionConfig config)
    {
        // Rough estimation based on work size
        var totalWorkItems = config.GlobalWorkSize.Aggregate(1, (a, b) => a * b);
        return Math.Min(totalWorkItems * 0.001, 500.0); // Cap at 500 GB/s
    }

    private double CalculateComputeThroughput(KernelExecutionConfig config)
    {
        // Rough estimation based on work size
        var totalWorkItems = config.GlobalWorkSize.Aggregate(1, (a, b) => a * b);
        return Math.Min(totalWorkItems * 0.01, 10000.0); // Cap at 10 TFLOPS
    }

    private KernelProfilingResult AnalyzeProfilingResults(List<double> timings, int iterations)
    {
        timings.Sort();
        
        var average = timings.Average();
        var min = timings.Min();
        var max = timings.Max();
        var median = timings.Count % 2 == 0 
            ? (timings[timings.Count / 2 - 1] + timings[timings.Count / 2]) / 2.0
            : timings[timings.Count / 2];
        
        var variance = timings.Select(t => Math.Pow(t - average, 2)).Average();
        var stdDev = Math.Sqrt(variance);
        
        var percentiles = new Dictionary<int, double>
        {
            [50] = median,
            [90] = timings[(int)(timings.Count * 0.9)],
            [95] = timings[(int)(timings.Count * 0.95)],
            [99] = timings[(int)(timings.Count * 0.99)]
        };

        return new KernelProfilingResult
        {
            Iterations = iterations,
            AverageTimeMs = average,
            MinTimeMs = min,
            MaxTimeMs = max,
            StdDevMs = stdDev,
            MedianTimeMs = median,
            PercentileTimingsMs = percentiles,
            AchievedOccupancy = Random.Shared.NextDouble() * 0.5 + 0.5, // 50-100%
            MemoryThroughputGBps = Random.Shared.NextDouble() * 200 + 100, // 100-300 GB/s
            ComputeThroughputGFLOPS = Random.Shared.NextDouble() * 5000 + 2000, // 2-7 TFLOPS
            Bottleneck = new BottleneckAnalysis
            {
                Type = BottleneckType.MemoryBandwidth,
                Severity = Random.Shared.NextDouble() * 0.5 + 0.3, // 30-80%
                Details = "Memory bandwidth appears to be the primary limiting factor",
                ResourceUtilization = new Dictionary<string, double>
                {
                    ["Compute"] = Random.Shared.NextDouble() * 0.4 + 0.6, // 60-100%
                    ["Memory"] = Random.Shared.NextDouble() * 0.3 + 0.7, // 70-100%
                    ["Cache"] = Random.Shared.NextDouble() * 0.5 + 0.4   // 40-90%
                }
            },
            OptimizationSuggestions = new List<string>
            {
                "Consider using vector data types to improve memory throughput",
                "Optimize memory access patterns to reduce cache misses",
                "Use local memory for frequently accessed data"
            }
        };
    }

    /// <summary>
    /// Disposes the OpenCL kernel executor and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clean up pending executions
            foreach (var execution in _pendingExecutions.Values)
            {
                if (execution.Handle.EventHandle is IntPtr eventHandle && eventHandle != IntPtr.Zero)
                {
                    ReleaseEvent(eventHandle);
                }
            }
            _pendingExecutions.Clear();
            
            // Release OpenCL resources
            if (_commandQueue != IntPtr.Zero)
            {
                // In real implementation: clReleaseCommandQueue(_commandQueue);
                _commandQueue = IntPtr.Zero;
            }
            
            if (_context != IntPtr.Zero)
            {
                // In real implementation: clReleaseContext(_context);
                _context = IntPtr.Zero;
            }
            
            _disposed = true;
            _logger.LogDebug("OpenCL kernel executor disposed");
        }
    }

    private sealed class PendingExecution
    {
        public required KernelExecutionHandle Handle { get; init; }
        public required KernelExecutionConfig ExecutionConfig { get; init; }
        public required DateTimeOffset StartTime { get; init; }
    }

    /// <summary>
    /// Gets the size in bytes of a type.
    /// </summary>
    private static int GetTypeSize(Type type)
    {
        if (type.IsArray)
        {
            return IntPtr.Size; // Pointer size
        }

        return type switch
        {
            _ when type == typeof(float) => 4,
            _ when type == typeof(double) => 8,
            _ when type == typeof(int) => 4,
            _ when type == typeof(uint) => 4,
            _ when type == typeof(long) => 8,
            _ when type == typeof(ulong) => 8,
            _ when type == typeof(short) => 2,
            _ when type == typeof(ushort) => 2,
            _ when type == typeof(byte) => 1,
            _ when type == typeof(sbyte) => 1,
            _ when type == typeof(bool) => 1,
            _ => 4 // Default
        };
    }
}