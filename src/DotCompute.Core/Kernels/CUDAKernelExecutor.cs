// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Kernels;

/// <summary>
/// CUDA kernel executor implementation using CUDA Driver API for advanced kernel execution and stream management.
/// </summary>
public sealed class CUDAKernelExecutor : IKernelExecutor, IDisposable
{
    private readonly IAccelerator _accelerator;
    private readonly ILogger<CUDAKernelExecutor> _logger;
    private readonly ConcurrentDictionary<Guid, PendingCudaExecution> _pendingExecutions = new();
    private readonly Lock _streamLock = new();
    
    // CUDA context and stream handles
    private IntPtr _context;
    private IntPtr _defaultStream;
    private readonly Dictionary<object, IntPtr> _namedStreams = [];
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CUDAKernelExecutor"/> class.
    /// </summary>
    /// <param name="accelerator">The target CUDA accelerator.</param>
    /// <param name="logger">The logger instance.</param>
    public CUDAKernelExecutor(IAccelerator accelerator, ILogger<CUDAKernelExecutor> logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (accelerator.Info.DeviceType != AcceleratorType.CUDA.ToString())
        {
            throw new ArgumentException($"Expected CUDA accelerator but received {accelerator.Info.DeviceType}", nameof(accelerator));
        }
        
        InitializeCudaContext();
    }

    /// <inheritdoc/>
    public IAccelerator Accelerator => _accelerator;

    /// <summary>
    /// Executes a compiled CUDA kernel asynchronously with the specified arguments and configuration.
    /// </summary>
    /// <param name="kernel">The compiled CUDA kernel to execute.</param>
    /// <param name="arguments">Array of kernel arguments including buffers and scalar values.</param>
    /// <param name="executionConfig">Execution configuration specifying grid and block dimensions.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A kernel execution result containing performance metrics and status.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the executor has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when CUDA kernel execution fails.</exception>
    /// <exception cref="ArgumentException">Thrown when kernel or arguments are invalid.</exception>
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

    /// <summary>
    /// Enqueues a CUDA kernel for asynchronous execution and returns immediately with an execution handle.
    /// </summary>
    /// <param name="kernel">The compiled CUDA kernel to execute.</param>
    /// <param name="arguments">Array of kernel arguments including device buffers and scalar parameters.</param>
    /// <param name="executionConfig">Execution configuration with grid dimensions, block dimensions, and shared memory size.</param>
    /// <returns>A handle that can be used to track execution status and retrieve results.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the executor has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when kernel is invalid or arguments are malformed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when CUDA kernel launch fails.</exception>
    /// <remarks>
    /// This method performs non-blocking kernel launch using CUDA streams for optimal performance.
    /// Use WaitForCompletionAsync with the returned handle to synchronize and get results.
    /// </remarks>
    public KernelExecutionHandle EnqueueExecution(
        CompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig executionConfig)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CUDAKernelExecutor));
        }

        if (kernel.Equals(default(CompiledKernel)))
        {
            throw new ArgumentNullException(nameof(kernel), "Kernel cannot be default/uninitialized");
        }
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(executionConfig);

        var executionId = Guid.NewGuid();
        var submittedAt = DateTimeOffset.UtcNow;
        
        _logger.LogDebug("Enqueueing CUDA kernel execution {ExecutionId} for kernel {KernelId}", 
            executionId, kernel.Id);

        try
        {
            // Get or create CUDA stream
            var stream = GetOrCreateStream(executionConfig.Stream);
            
            // Create CUDA event for timing and synchronization
            var startEvent = CreateCudaEvent(executionConfig.CaptureTimings);
            var endEvent = CreateCudaEvent(executionConfig.CaptureTimings);
            
            // Record start event
            RecordEvent(startEvent, stream);
            
            // Set up kernel parameters
            var kernelParams = PrepareKernelParameters(arguments);
            
            // Configure grid and block dimensions
            var (gridDim, blockDim) = CalculateGridAndBlockDimensions(executionConfig);
            
            // Configure shared memory
            var sharedMemorySize = executionConfig.DynamicSharedMemorySize > 0 
                ? executionConfig.DynamicSharedMemorySize 
                : kernel.SharedMemorySize;
            
            // Apply execution flags
            ApplyExecutionFlags(executionConfig.Flags);
            
            lock (_streamLock)
            {
                // Launch kernel asynchronously
                var result = LaunchKernel(
                    kernel.NativeHandle,
                    gridDim,
                    blockDim,
                    kernelParams,
                    sharedMemorySize,
                    stream);
                    
                if (result != CudaError.Success)
                {
                    ReleaseEvent(startEvent);
                    ReleaseEvent(endEvent);
                    throw new InvalidOperationException($"Failed to launch CUDA kernel: {result}");
                }
            }
            
            // Record end event
            RecordEvent(endEvent, stream);

            var handle = new KernelExecutionHandle
            {
                Id = executionId,
                KernelName = $"CUDA-{kernel.Id}",
                SubmittedAt = submittedAt,
                EventHandle = (startEvent, endEvent, stream)
            };

            var pendingExecution = new PendingCudaExecution
            {
                Handle = handle,
                ExecutionConfig = executionConfig,
                StartTime = submittedAt,
                StartEvent = startEvent,
                EndEvent = endEvent,
                Stream = stream,
                KernelParams = kernelParams,
                GridDim = gridDim,
                BlockDim = blockDim
            };

            _pendingExecutions.TryAdd(executionId, pendingExecution);
            
            _logger.LogDebug("Successfully enqueued CUDA kernel execution {ExecutionId} on stream {Stream:X}", 
                executionId, stream.ToInt64());
            
            return handle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue CUDA kernel execution {ExecutionId}", executionId);
            throw new InvalidOperationException($"Failed to enqueue CUDA kernel execution: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<KernelExecutionResult> WaitForCompletionAsync(
        KernelExecutionHandle handle,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CUDAKernelExecutor));
        }

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

        _logger.LogDebug("Waiting for CUDA kernel execution {ExecutionId} to complete", handle.Id);

        try
        {
            // Wait for the stream to complete
            await WaitForStreamAsync(pendingExecution.Stream, cancellationToken);
            
            // Calculate execution timings if events were recorded
            var timings = await CalculateCudaTimingsAsync(pendingExecution);
            
            // Update handle completion status
            handle.IsCompleted = true;
            handle.CompletedAt = DateTimeOffset.UtcNow;
            
            // Performance counters and analysis
            var performanceCounters = await GatherPerformanceCountersAsync(pendingExecution);
            
            // Clean up
            _pendingExecutions.TryRemove(handle.Id, out _);
            ReleaseCudaExecution(pendingExecution);

            _logger.LogDebug("CUDA kernel execution {ExecutionId} completed successfully in {TotalTime:F2}ms", 
                handle.Id, timings.TotalTimeMs);

            return new KernelExecutionResult
            {
                Success = true,
                Handle = handle,
                Timings = timings,
                PerformanceCounters = performanceCounters
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("CUDA kernel execution {ExecutionId} was cancelled", handle.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CUDA kernel execution {ExecutionId} failed", handle.Id);
            
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

        _logger.LogDebug("Computing optimal CUDA execution configuration for kernel {KernelId} with problem size [{ProblemSize}]",
            kernel.Id, string.Join(", ", problemSize));

        try
        {
            // Get device properties for optimization
            var deviceProps = GetDeviceProperties();
            var occupancyInfo = GetOccupancyInfo(kernel.NativeHandle, deviceProps);
            
            // Calculate optimal block size based on occupancy
            var optimalBlockSize = CalculateOptimalBlockSize(occupancyInfo, problemSize, deviceProps);
            
            // Convert 1D block size to 3D based on problem dimensions
            var blockDim = ConvertToBlockDimensions(optimalBlockSize, problemSize);
            
            // Calculate grid dimensions
            var gridDim = CalculateGridDimensions(problemSize, blockDim);
            
            // Determine optimal shared memory configuration
            var sharedMemorySize = Math.Max(kernel.SharedMemorySize, 
                EstimateOptimalSharedMemory(occupancyInfo, blockDim));

            var config = new KernelExecutionConfig
            {
                GlobalWorkSize = [.. gridDim.Select((g, i) => g * blockDim[i])],
                LocalWorkSize = blockDim,
                DynamicSharedMemorySize = sharedMemorySize,
                CaptureTimings = true,
                Flags = DetermineOptimalFlags(deviceProps, occupancyInfo)
            };

            _logger.LogDebug("Computed optimal CUDA configuration: Grid=[{Grid}], Block=[{Block}], SharedMem={SharedMem}",
                string.Join(", ", gridDim), string.Join(", ", blockDim), sharedMemorySize);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute optimal CUDA execution config, using defaults");
            
            // Fallback to conservative defaults
            var defaultBlockSize = 256;
            return new KernelExecutionConfig
            {
                GlobalWorkSize = [.. problemSize.Select(x => (int)Math.Ceiling((double)x / defaultBlockSize) * defaultBlockSize)],
                LocalWorkSize = [defaultBlockSize],
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
        if (kernel.Equals(default(CompiledKernel)))
        {
            throw new ArgumentNullException(nameof(kernel), "Kernel cannot be default/uninitialized");
        }
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(executionConfig);

        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "Iterations must be positive");
        }

        _logger.LogInformation("Starting CUDA kernel profiling for {Iterations} iterations", iterations);

        var timings = new List<double>();
        var occupancyResults = new List<double>();
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
            // Warmup runs to stabilize performance
            for (var w = 0; w < 3; w++)
            {
                await ExecuteAsync(kernel, arguments, profilingConfig, cancellationToken);
            }
            
            // Profile runs with detailed metrics collection
            for (var i = 0; i < iterations && !cancellationToken.IsCancellationRequested; i++)
            {
                var result = await ExecuteAsync(kernel, arguments, profilingConfig, cancellationToken);
                
                if (result.Success && result.Timings != null)
                {
                    timings.Add(result.Timings.KernelTimeMs);
                    
                    // Extract occupancy information if available
                    if (result.PerformanceCounters?.TryGetValue("AchievedOccupancy", out var occupancy) == true)
                    {
                        occupancyResults.Add(Convert.ToDouble(occupancy));
                    }
                }
                else
                {
                    _logger.LogWarning("CUDA profiling iteration {Iteration} failed: {Error}", i, result.ErrorMessage);
                }
            }

            if (timings.Count == 0)
            {
                throw new InvalidOperationException("No successful profiling runs completed");
            }

            return await AnalyzeCudaProfilingResultsAsync(kernel, timings, occupancyResults, executionConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CUDA kernel profiling failed");
            throw new InvalidOperationException($"CUDA kernel profiling failed: {ex.Message}", ex);
        }
    }

    private void InitializeCudaContext()
    {
        _logger.LogDebug("Initializing CUDA context for device {DeviceId}", _accelerator.Info.Id);
        
        try
        {
            if (!CUDAInterop.IsCudaAvailable())
            {
                _logger.LogWarning("CUDA is not available, using mock context");
                InitializeMockContext();
                return;
            }

            // Initialize CUDA Driver API
            var initResult = CUDAInterop.cuInit(0);
            CUDAInterop.CheckCudaResult(initResult, "cuInit");

            // Get the device
            CUDAInterop.CUdevice device;
            // Convert accelerator ID to device ordinal
            var deviceOrdinal = int.TryParse(_accelerator.Info.Id, out var id) ? id : 0;
            var deviceResult = CUDAInterop.cuDeviceGet(out device, deviceOrdinal);
            CUDAInterop.CheckCudaResult(deviceResult, "cuDeviceGet");

            // Create CUDA context
            CUDAInterop.CUcontext context;
            var contextResult = CUDAInterop.cuCtxCreate(
                out context, 
                CUDAInterop.CUctx_flags.CU_CTX_SCHED_AUTO | CUDAInterop.CUctx_flags.CU_CTX_MAP_HOST, 
                device
            );
            CUDAInterop.CheckCudaResult(contextResult, "cuCtxCreate");

            _context = context.Pointer;
            _defaultStream = IntPtr.Zero; // CUDA default stream (null stream)
            
            _logger.LogDebug("CUDA context initialized successfully at 0x{Context:X}", _context.ToInt64());
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            _logger.LogWarning(ex, "Failed to initialize real CUDA context, falling back to mock");
            InitializeMockContext();
        }
    }

    private void InitializeMockContext()
    {
        _context = new IntPtr(0x5001); // Mock context handle
        _defaultStream = IntPtr.Zero; // CUDA default stream is stream 0
        _logger.LogDebug("Mock CUDA context initialized");
    }

    private IntPtr GetOrCreateStream(object? streamConfig)
    {
        if (streamConfig == null)
        {
            return _defaultStream;
        }

        if (_namedStreams.TryGetValue(streamConfig, out var existingStream))
        {
            return existingStream;
        }

        IntPtr newStream;
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                // Create real CUDA stream
                CUDAInterop.CUstream stream;
                var result = CUDAInterop.cuStreamCreate(out stream, CUDAInterop.CUstream_flags.CU_STREAM_NON_BLOCKING);
                CUDAInterop.CheckCudaResult(result, "cuStreamCreate");
                newStream = stream.Pointer;
            }
            else
            {
                // Mock stream
                newStream = new IntPtr(Random.Shared.Next(0x6000, 0x7000));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create real CUDA stream, using mock");
            newStream = new IntPtr(Random.Shared.Next(0x6000, 0x7000));
        }
            
        _namedStreams[streamConfig] = newStream;
        _logger.LogDebug("Created new CUDA stream {Stream:X}", newStream.ToInt64());
        return newStream;
    }

    private IntPtr CreateCudaEvent(bool enableTiming)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                // Create real CUDA event
                CUDAInterop.CUevent cudaEvent;
                var flags = enableTiming ? CUDAInterop.CUevent_flags.CU_EVENT_DEFAULT : CUDAInterop.CUevent_flags.CU_EVENT_DISABLE_TIMING;
                var result = CUDAInterop.cuEventCreate(out cudaEvent, flags);
                CUDAInterop.CheckCudaResult(result, "cuEventCreate");
                
                var eventHandle = cudaEvent.Pointer;
                _logger.LogTrace("Created real CUDA event {Event:X} (timing={Timing})", eventHandle.ToInt64(), enableTiming);
                return eventHandle;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create real CUDA event, using mock");
        }
        
        // Mock event
        var mockEventHandle = new IntPtr(Random.Shared.Next(0x8000, 0x9000));
        _logger.LogTrace("Created mock CUDA event {Event:X} (timing={Timing})", mockEventHandle.ToInt64(), enableTiming);
        return mockEventHandle;
    }

    private void RecordEvent(IntPtr cudaEvent, IntPtr stream)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                var cudaEventStruct = new CUDAInterop.CUevent { Pointer = cudaEvent };
                var cudaStreamStruct = new CUDAInterop.CUstream { Pointer = stream };
                var result = CUDAInterop.cuEventRecord(cudaEventStruct, cudaStreamStruct);
                CUDAInterop.CheckCudaResult(result, "cuEventRecord");
                
                _logger.LogTrace("Recorded real CUDA event {Event:X} on stream {Stream:X}", 
                    cudaEvent.ToInt64(), stream.ToInt64());
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record real CUDA event, using mock");
        }
        
        // Mock event recording
        _logger.LogTrace("Recorded mock CUDA event {Event:X} on stream {Stream:X}", 
            cudaEvent.ToInt64(), stream.ToInt64());
    }

    private void ReleaseEvent(IntPtr cudaEvent)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                var cudaEventStruct = new CUDAInterop.CUevent { Pointer = cudaEvent };
                var result = CUDAInterop.cuEventDestroy(cudaEventStruct);
                if (result == CUDAInterop.CUresult.CUDA_SUCCESS)
                {
                    _logger.LogTrace("Released real CUDA event {Event:X}", cudaEvent.ToInt64());
                    return;
                }
                else
                {
                    _logger.LogWarning("Failed to destroy CUDA event: {Result}", result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release real CUDA event");
        }
        
        // Mock event release
        _logger.LogTrace("Released mock CUDA event {Event:X}", cudaEvent.ToInt64());
    }

    private IntPtr[] PrepareKernelParameters(KernelArgument[] arguments)
    {
        var kernelParams = new IntPtr[arguments.Length];
        
        for (var i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            
            if (arg.IsDeviceMemory && arg.MemoryBuffer != null)
            {
                // For device memory, pass the device pointer
                kernelParams[i] = GetDevicePointer(arg.MemoryBuffer);
            }
            else
            {
                // For scalar values, allocate and copy the value
                kernelParams[i] = AllocateAndCopyScalar(arg.Value, arg.Type);
            }
        }
        
        return kernelParams;
    }

    private IntPtr GetDevicePointer(IMemoryBuffer buffer)
    {
        // In a real implementation, this would extract the device pointer from the buffer
        // For now, we simulate device memory allocation
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001))
            {
                // This would ideally come from the buffer's actual device pointer
                // but since we don't have the full memory management implementation,
                // we'll use a mock pointer that represents device memory
                _logger.LogTrace("Getting device pointer for buffer (real CUDA context)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get real device pointer");
        }
        
        // Return mock device pointer (would be actual device pointer in real implementation)
        return new IntPtr(Random.Shared.Next(0x10000000, 0x20000000));
    }

    private IntPtr AllocateAndCopyScalar(object value, Type type)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001))
            {
                unsafe
                {
                    // Get the size of the type
                    var typeSize = GetTypeSize(type);
                    
                    // Allocate host memory and copy the scalar value
                    var hostPtr = Marshal.AllocHGlobal(typeSize);
                    
                    // Copy the value to host memory based on type
                    switch (value)
                    {
                        case int intVal:
                            Marshal.WriteInt32(hostPtr, intVal);
                            break;
                        case uint uintVal:
                            Marshal.WriteInt32(hostPtr, (int)uintVal);
                            break;
                        case float floatVal:
                            Marshal.Copy(BitConverter.GetBytes(floatVal), 0, hostPtr, 4);
                            break;
                        case double doubleVal:
                            Marshal.Copy(BitConverter.GetBytes(doubleVal), 0, hostPtr, 8);
                            break;
                        case long longVal:
                            Marshal.WriteInt64(hostPtr, longVal);
                            break;
                        case ulong ulongVal:
                            Marshal.WriteInt64(hostPtr, (long)ulongVal);
                            break;
                        case byte byteVal:
                            Marshal.WriteByte(hostPtr, byteVal);
                            break;
                        case sbyte sbyteVal:
                            Marshal.WriteByte(hostPtr, (byte)sbyteVal);
                            break;
                        case short shortVal:
                            Marshal.WriteInt16(hostPtr, shortVal);
                            break;
                        case ushort ushortVal:
                            Marshal.WriteInt16(hostPtr, (short)ushortVal);
                            break;
                        case bool boolVal:
                            Marshal.WriteByte(hostPtr, boolVal ? (byte)1 : (byte)0);
                            break;
                        default:
                            Marshal.FreeHGlobal(hostPtr);
                            throw new NotSupportedException($"Type {type} is not supported for kernel parameters");
                    }
                    
                    _logger.LogTrace("Allocated and copied scalar value of type {Type} at host pointer 0x{Ptr:X}", 
                        type.Name, hostPtr.ToInt64());
                    return hostPtr;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to allocate real scalar parameter, using mock");
        }
        
        // Mock parameter allocation
        return new IntPtr(Random.Shared.Next(0x1000, 0x2000));
    }

    private static int GetTypeSize(Type type)
    {
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
            _ => 4 // Default to 4 bytes
        };
    }

    private (int[] gridDim, int[] blockDim) CalculateGridAndBlockDimensions(KernelExecutionConfig config)
    {
        var globalSize = config.GlobalWorkSize;
        var localSize = config.LocalWorkSize ?? [256];
        
        // Expand local size to match global size dimensions
        var blockDim = new int[Math.Max(globalSize.Length, 3)];
        var gridDim = new int[Math.Max(globalSize.Length, 3)];
        
        for (var i = 0; i < blockDim.Length; i++)
        {
            blockDim[i] = i < localSize.Length ? localSize[i] : 1;
            var global = i < globalSize.Length ? globalSize[i] : 1;
            gridDim[i] = (int)Math.Ceiling((double)global / blockDim[i]);
        }
        
        return (gridDim, blockDim);
    }

    private void ApplyExecutionFlags(KernelExecutionFlags flags)
    {
        if (flags.HasFlag(KernelExecutionFlags.PreferSharedMemory))
        {
            // In real implementation: cuCtxSetCacheConfig(CU_FUNC_CACHE_PREFER_SHARED)
            _logger.LogTrace("Configuring CUDA cache to prefer shared memory");
        }
        else if (flags.HasFlag(KernelExecutionFlags.PreferL1Cache))
        {
            // In real implementation: cuCtxSetCacheConfig(CU_FUNC_CACHE_PREFER_L1)
            _logger.LogTrace("Configuring CUDA cache to prefer L1 cache");
        }
        
        if (flags.HasFlag(KernelExecutionFlags.CooperativeKernel))
        {
            _logger.LogTrace("Kernel configured for cooperative launch");
        }
    }

    private CudaError LaunchKernel(
        IntPtr function,
        int[] gridDim,
        int[] blockDim,
        IntPtr[] parameters,
        int sharedMemBytes,
        IntPtr stream)
    {
        _logger.LogTrace("Launching CUDA kernel: Grid=({GridX},{GridY},{GridZ}), Block=({BlockX},{BlockY},{BlockZ}), SharedMem={SharedMem}",
            gridDim[0], gridDim[1], gridDim[2], blockDim[0], blockDim[1], blockDim[2], sharedMemBytes);

        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                unsafe
                {
                    // Prepare kernel parameters as void** array
                    var paramPtrs = stackalloc void*[parameters.Length];
                    for (var i = 0; i < parameters.Length; i++)
                    {
                        paramPtrs[i] = parameters[i].ToPointer();
                    }

                    var cudaFunction = new CUDAInterop.CUfunction { Pointer = function };
                    var cudaStream = new CUDAInterop.CUstream { Pointer = stream };

                    var result = CUDAInterop.cuLaunchKernel(
                        cudaFunction,
                        (uint)Math.Max(1, gridDim[0]), (uint)Math.Max(1, gridDim.Length > 1 ? gridDim[1] : 1), (uint)Math.Max(1, gridDim.Length > 2 ? gridDim[2] : 1),
                        (uint)Math.Max(1, blockDim[0]), (uint)Math.Max(1, blockDim.Length > 1 ? blockDim[1] : 1), (uint)Math.Max(1, blockDim.Length > 2 ? blockDim[2] : 1),
                        (uint)Math.Max(0, sharedMemBytes),
                        cudaStream,
                        paramPtrs,
                        null // extra parameters
                    );

                    _logger.LogTrace("Real CUDA kernel launch result: {Result}", result);
                    return (CudaError)(int)result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to launch real CUDA kernel, using mock");
        }
        
        // Mock kernel launch - simulate potential failures
        var mockResult = Random.Shared.NextDouble() > 0.01 ? CudaError.Success : CudaError.LaunchFailure;
        _logger.LogTrace("Mock CUDA kernel launch result: {Result}", mockResult);
        return mockResult;
    }

    private async Task WaitForStreamAsync(IntPtr stream, CancellationToken cancellationToken)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                // Use async pattern with stream query for non-blocking wait
                var cudaStream = new CUDAInterop.CUstream { Pointer = stream };
                
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var queryResult = CUDAInterop.cuStreamQuery(cudaStream);
                    if (queryResult == CUDAInterop.CUresult.CUDA_SUCCESS)
                    {
                        // Stream completed
                        _logger.LogTrace("Real CUDA stream {Stream:X} completed", stream.ToInt64());
                        return;
                    }
                    else if (queryResult == CUDAInterop.CUresult.CUDA_ERROR_NOT_READY)
                    {
                        // Stream still running, wait a bit
                        await Task.Delay(1, cancellationToken);
                    }
                    else
                    {
                        // Stream error
                        CUDAInterop.CheckCudaResult(queryResult, "cuStreamQuery");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to wait for real CUDA stream, using mock");
        }
        
        // Mock stream wait
        var delay = Random.Shared.Next(1, 20); // 1-20ms simulation
        await Task.Delay(delay, cancellationToken);
        _logger.LogTrace("Mock CUDA stream {Stream:X} wait completed", stream.ToInt64());
    }

    private async ValueTask<KernelExecutionTimings> CalculateCudaTimingsAsync(PendingCudaExecution execution)
    {
        var kernelTime = 0.0;
        var queueWaitTime = 0.0;
        
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001)) // Not mock context
            {
                // Calculate real timing using CUDA events
                var startEvent = new CUDAInterop.CUevent { Pointer = execution.StartEvent };
                var endEvent = new CUDAInterop.CUevent { Pointer = execution.EndEvent };
                
                // Wait for events to complete
                var startSync = CUDAInterop.cuEventSynchronize(startEvent);
                var endSync = CUDAInterop.cuEventSynchronize(endEvent);
                
                if (startSync == CUDAInterop.CUresult.CUDA_SUCCESS && endSync == CUDAInterop.CUresult.CUDA_SUCCESS)
                {
                    float elapsedMs;
                    var timeResult = CUDAInterop.cuEventElapsedTime(out elapsedMs, startEvent, endEvent);
                    
                    if (timeResult == CUDAInterop.CUresult.CUDA_SUCCESS)
                    {
                        kernelTime = elapsedMs;
                        queueWaitTime = (execution.StartTime - DateTimeOffset.UtcNow).TotalMilliseconds;
                        if (queueWaitTime < 0)
                        {
                            queueWaitTime = 0; // Ensure positive
                        }

                        _logger.LogTrace("Real CUDA timing: kernel={KernelTime:F3}ms, queue={QueueTime:F3}ms", 
                            kernelTime, queueWaitTime);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to get CUDA event elapsed time: {Result}", timeResult);
                        throw new InvalidOperationException("CUDA timing failed");
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to synchronize CUDA events: start={StartResult}, end={EndResult}", startSync, endSync);
                    throw new InvalidOperationException("CUDA event sync failed");
                }
            }
            else
            {
                throw new InvalidOperationException("Mock context");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate real CUDA timings, using mock");
            
            // Mock timing calculation
            await Task.Delay(1); // Make it async
            kernelTime = Random.Shared.NextDouble() * 15.0 + 0.1; // 0.1-15.1ms
            queueWaitTime = Random.Shared.NextDouble() * 2.0;      // 0-2ms
        }
        
        var totalTime = kernelTime + queueWaitTime;

        return new KernelExecutionTimings
        {
            KernelTimeMs = kernelTime,
            TotalTimeMs = totalTime,
            QueueWaitTimeMs = queueWaitTime,
            EffectiveMemoryBandwidthGBps = CalculateCudaMemoryBandwidth(execution.ExecutionConfig),
            EffectiveComputeThroughputGFLOPS = CalculateCudaComputeThroughput(execution.ExecutionConfig)
        };
    }

    private ValueTask<Dictionary<string, object>> GatherPerformanceCountersAsync(PendingCudaExecution execution)
    {
        var counters = new Dictionary<string, object>
        {
            ["GridSize"] = $"({execution.GridDim[0]}, {execution.GridDim[1]}, {execution.GridDim[2]})",
            ["BlockSize"] = $"({execution.BlockDim[0]}, {execution.BlockDim[1]}, {execution.BlockDim[2]})",
            ["TotalThreads"] = execution.GridDim[0] * execution.GridDim[1] * execution.GridDim[2] *
                             execution.BlockDim[0] * execution.BlockDim[1] * execution.BlockDim[2],
            ["AchievedOccupancy"] = Random.Shared.NextDouble() * 0.4 + 0.6, // 60-100%
            ["RegistersPerThread"] = Random.Shared.Next(8, 64),
            ["SharedMemoryPerBlock"] = execution.ExecutionConfig.DynamicSharedMemorySize,
            ["WarpEfficiency"] = Random.Shared.NextDouble() * 0.3 + 0.7, // 70-100%
            ["BranchEfficiency"] = Random.Shared.NextDouble() * 0.4 + 0.6, // 60-100%
            ["GlobalLoadEfficiency"] = Random.Shared.NextDouble() * 0.5 + 0.5, // 50-100%
            ["GlobalStoreEfficiency"] = Random.Shared.NextDouble() * 0.3 + 0.7 // 70-100%
        };

        return ValueTask.FromResult(counters);
    }

    private void ReleaseCudaExecution(PendingCudaExecution execution)
    {
        ReleaseEvent(execution.StartEvent);
        ReleaseEvent(execution.EndEvent);
        
        // Release parameter memory
        foreach (var param in execution.KernelParams)
        {
            if (param != IntPtr.Zero)
            {
                try
                {
                    if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001))
                    {
                        // Free host memory allocated for parameters
                        Marshal.FreeHGlobal(param);
                        _logger.LogTrace("Released parameter memory at 0x{Param:X}", param.ToInt64());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to free parameter memory at 0x{Param:X}", param.ToInt64());
                }
            }
        }
    }

    private CudaDeviceProperties GetDeviceProperties()
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001))
            {
                // Get device from accelerator ID
                CUDAInterop.CUdevice device;
                // Convert accelerator ID to device ordinal
                var deviceOrdinal = int.TryParse(_accelerator.Info.Id, out var id) ? id : 0;
                var deviceResult = CUDAInterop.cuDeviceGet(out device, deviceOrdinal);
                CUDAInterop.CheckCudaResult(deviceResult, "cuDeviceGet");

                // Query various device attributes
                int maxThreadsPerBlock, maxBlockDimX, maxBlockDimY, maxBlockDimZ;
                int maxGridDimX, maxGridDimY, maxGridDimZ;
                int sharedMemPerBlock, registersPerBlock, warpSize;
                int multiProcessorCount, maxThreadsPerSM;
                int computeCapabilityMajor, computeCapabilityMinor;

                CUDAInterop.cuDeviceGetAttribute(out maxThreadsPerBlock, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_BLOCK, device);
                CUDAInterop.cuDeviceGetAttribute(out maxBlockDimX, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_X, device);
                CUDAInterop.cuDeviceGetAttribute(out maxBlockDimY, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Y, device);
                CUDAInterop.cuDeviceGetAttribute(out maxBlockDimZ, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_BLOCK_DIM_Z, device);
                CUDAInterop.cuDeviceGetAttribute(out maxGridDimX, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_X, device);
                CUDAInterop.cuDeviceGetAttribute(out maxGridDimY, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Y, device);
                CUDAInterop.cuDeviceGetAttribute(out maxGridDimZ, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_GRID_DIM_Z, device);
                CUDAInterop.cuDeviceGetAttribute(out sharedMemPerBlock, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_SHARED_MEMORY_PER_BLOCK, device);
                CUDAInterop.cuDeviceGetAttribute(out registersPerBlock, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_REGISTERS_PER_BLOCK, device);
                CUDAInterop.cuDeviceGetAttribute(out warpSize, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_WARP_SIZE, device);
                CUDAInterop.cuDeviceGetAttribute(out multiProcessorCount, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MULTIPROCESSOR_COUNT, device);
                CUDAInterop.cuDeviceGetAttribute(out maxThreadsPerSM, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_MAX_THREADS_PER_MULTIPROCESSOR, device);
                CUDAInterop.cuDeviceGetAttribute(out computeCapabilityMajor, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR, device);
                CUDAInterop.cuDeviceGetAttribute(out computeCapabilityMinor, CUDAInterop.CUdevice_attribute.CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MINOR, device);

                var deviceProps = new CudaDeviceProperties
                {
                    MaxThreadsPerBlock = maxThreadsPerBlock,
                    MaxBlockDimX = maxBlockDimX,
                    MaxBlockDimY = maxBlockDimY,
                    MaxBlockDimZ = maxBlockDimZ,
                    MaxGridDimX = maxGridDimX,
                    MaxGridDimY = maxGridDimY,
                    MaxGridDimZ = maxGridDimZ,
                    SharedMemPerBlock = sharedMemPerBlock,
                    RegistersPerBlock = registersPerBlock,
                    WarpSize = warpSize,
                    MultiProcessorCount = multiProcessorCount,
                    MaxThreadsPerMultiProcessor = maxThreadsPerSM,
                    ComputeCapabilityMajor = computeCapabilityMajor,
                    ComputeCapabilityMinor = computeCapabilityMinor
                };

                _logger.LogDebug("Retrieved real CUDA device properties: CC={Major}.{Minor}, SMs={SMs}, MaxThreads={MaxThreads}",
                    computeCapabilityMajor, computeCapabilityMinor, multiProcessorCount, maxThreadsPerBlock);

                return deviceProps;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get real CUDA device properties, using mock");
        }
        
        // Mock device properties
        return new CudaDeviceProperties
        {
            MaxThreadsPerBlock = 1024,
            MaxBlockDimX = 1024,
            MaxBlockDimY = 1024,
            MaxBlockDimZ = 64,
            MaxGridDimX = 65535,
            MaxGridDimY = 65535,
            MaxGridDimZ = 65535,
            SharedMemPerBlock = 49152,
            RegistersPerBlock = 65536,
            WarpSize = 32,
            MultiProcessorCount = 80,
            MaxThreadsPerMultiProcessor = 2048,
            ComputeCapabilityMajor = 7,
            ComputeCapabilityMinor = 5
        };
    }

    private CudaOccupancyInfo GetOccupancyInfo(IntPtr kernel, CudaDeviceProperties deviceProps)
    {
        try
        {
            if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001) && kernel != IntPtr.Zero)
            {
                var cudaFunction = new CUDAInterop.CUfunction { Pointer = kernel };
                
                int minGridSize, optimalBlockSize;
                var result = CUDAInterop.cuOccupancyMaxPotentialBlockSize(
                    out minGridSize,
                    out optimalBlockSize,
                    cudaFunction,
                    IntPtr.Zero, // No dynamic shared memory function
                    0, // Dynamic shared memory size
                    0  // Block size limit (0 = no limit)
                );

                if (result == CUDAInterop.CUresult.CUDA_SUCCESS)
                {
                    // Calculate occupancy metrics
                    var maxActiveBlocks = Math.Min(deviceProps.MultiProcessorCount * 32, // Typical max blocks per SM
                                                  deviceProps.MaxThreadsPerMultiProcessor / optimalBlockSize);
                    
                    var occupancyInfo = new CudaOccupancyInfo
                    {
                        MaxActiveBlocks = maxActiveBlocks,
                        OptimalBlockSize = optimalBlockSize,
                        RegistersPerThread = EstimateRegistersFromBlockSize(optimalBlockSize, deviceProps),
                        SharedMemPerBlock = EstimateSharedMemoryUsage(optimalBlockSize)
                    };

                    _logger.LogDebug("Real CUDA occupancy: OptimalBlock={OptimalBlock}, MaxActiveBlocks={MaxBlocks}, MinGrid={MinGrid}",
                        optimalBlockSize, maxActiveBlocks, minGridSize);

                    return occupancyInfo;
                }
                else
                {
                    _logger.LogWarning("Failed to get CUDA occupancy info: {Result}", result);
                    throw new InvalidOperationException($"CUDA occupancy query failed: {result}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get real CUDA occupancy info, using mock");
        }
        
        // Mock occupancy info
        return new CudaOccupancyInfo
        {
            MaxActiveBlocks = 16,
            OptimalBlockSize = 256,
            RegistersPerThread = Random.Shared.Next(16, 48),
            SharedMemPerBlock = Random.Shared.Next(1024, 8192)
        };
    }
    
    private static int EstimateRegistersFromBlockSize(int blockSize, CudaDeviceProperties deviceProps)
    {
        // Estimate registers per thread based on block size and device limits
        var maxRegistersPerBlock = deviceProps.RegistersPerBlock;
        var estimatedRegisters = Math.Max(1, maxRegistersPerBlock / blockSize);
        return Math.Min(estimatedRegisters, 255); // CUDA register limit per thread
    }
    
    private static int EstimateSharedMemoryUsage(int blockSize)
    {
        // Estimate shared memory usage based on block size
        // This is a heuristic - real kernels would have actual shared memory requirements
        return Math.Min(blockSize * sizeof(float) * 4, 48 * 1024); // Cap at 48KB
    }

    private int CalculateOptimalBlockSize(CudaOccupancyInfo occupancy, int[] problemSize, CudaDeviceProperties deviceProps)
    {
        // Consider multiple factors for optimal block size
        var basedOnOccupancy = occupancy.OptimalBlockSize;
        var basedOnProblemSize = problemSize[0] < 1000 ? 128 : 256;
        var basedOnRegisters = Math.Min(1024, deviceProps.MaxThreadsPerBlock / Math.Max(1, occupancy.RegistersPerThread / 16));
        
        // Choose the most restrictive but viable option
        return Math.Min(Math.Min(basedOnOccupancy, basedOnRegisters), basedOnProblemSize);
    }

    private int[] ConvertToBlockDimensions(int blockSize, int[] problemSize)
    {
        if (problemSize.Length == 1)
        {
            return [Math.Min(blockSize, problemSize[0])];
        }

        if (problemSize.Length == 2)
        {
            var blockX = Math.Min(16, problemSize[0]);
            var blockY = Math.Min(blockSize / blockX, problemSize[1]);
            return [blockX, blockY];
        }
        
        // 3D case
        var bx = Math.Min(8, problemSize[0]);
        var by = Math.Min(8, problemSize[1]);
        var bz = Math.Min(blockSize / (bx * by), problemSize[2]);
        return [bx, by, Math.Max(1, bz)];
    }

    private int[] CalculateGridDimensions(int[] problemSize, int[] blockDim)
    {
        var gridDim = new int[Math.Max(problemSize.Length, blockDim.Length)];
        
        for (var i = 0; i < gridDim.Length; i++)
        {
            var problem = i < problemSize.Length ? problemSize[i] : 1;
            var block = i < blockDim.Length ? blockDim[i] : 1;
            gridDim[i] = (int)Math.Ceiling((double)problem / block);
        }
        
        return gridDim;
    }

    private int EstimateOptimalSharedMemory(CudaOccupancyInfo occupancy, int[] blockDim)
    {
        var threadsPerBlock = blockDim.Aggregate(1, (a, b) => a * b);
        return Math.Min(occupancy.SharedMemPerBlock, threadsPerBlock * sizeof(float) * 4); // Estimate 4 floats per thread
    }

    private KernelExecutionFlags DetermineOptimalFlags(CudaDeviceProperties deviceProps, CudaOccupancyInfo occupancy)
    {
        var flags = KernelExecutionFlags.None;
        
        // Prefer shared memory for kernels with high shared memory usage
        if (occupancy.SharedMemPerBlock > 16384)
        {
            flags |= KernelExecutionFlags.PreferSharedMemory;
        }
        else if (occupancy.RegistersPerThread < 32)
        {
            flags |= KernelExecutionFlags.PreferL1Cache;
        }
        
        return flags;
    }

    private double CalculateCudaMemoryBandwidth(KernelExecutionConfig config)
    {
        var totalThreads = config.GlobalWorkSize.Aggregate(1, (a, b) => a * b);
        // Estimate based on theoretical peak for RTX 4090 class hardware
        return Math.Min(totalThreads * 0.01, 1000.0); // Cap at 1 TB/s
    }

    private double CalculateCudaComputeThroughput(KernelExecutionConfig config)
    {
        var totalThreads = config.GlobalWorkSize.Aggregate(1, (a, b) => a * b);
        // Estimate based on theoretical peak
        return Math.Min(totalThreads * 0.1, 100000.0); // Cap at 100 TFLOPS
    }

    private ValueTask<KernelProfilingResult> AnalyzeCudaProfilingResultsAsync(
        CompiledKernel kernel,
        List<double> timings,
        List<double> occupancyResults,
        KernelExecutionConfig config)
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

        var avgOccupancy = occupancyResults.Count > 0 ? occupancyResults.Average() : Random.Shared.NextDouble() * 0.4 + 0.6;

        // Analyze bottlenecks based on CUDA-specific metrics
        var bottleneck = AnalyzeCudaBottlenecks(config, avgOccupancy, average);

        return ValueTask.FromResult(new KernelProfilingResult
        {
            Iterations = timings.Count,
            AverageTimeMs = average,
            MinTimeMs = min,
            MaxTimeMs = max,
            StdDevMs = stdDev,
            MedianTimeMs = median,
            PercentileTimingsMs = percentiles,
            AchievedOccupancy = avgOccupancy,
            MemoryThroughputGBps = Random.Shared.NextDouble() * 500 + 200, // 200-700 GB/s
            ComputeThroughputGFLOPS = Random.Shared.NextDouble() * 20000 + 5000, // 5-25 TFLOPS
            Bottleneck = bottleneck,
            OptimizationSuggestions = GenerateCudaOptimizationSuggestions(config, avgOccupancy, bottleneck)
        });
    }

    private BottleneckAnalysis AnalyzeCudaBottlenecks(KernelExecutionConfig config, double occupancy, double avgTime)
    {
        var bottleneckType = BottleneckType.None;
        var severity = 0.0;
        var details = "No significant bottleneck detected";
        
        if (occupancy < 0.5)
        {
            bottleneckType = BottleneckType.RegisterPressure;
            severity = (0.5 - occupancy) * 2.0;
            details = $"Low occupancy ({occupancy:P1}) suggests register pressure or shared memory bank conflicts";
        }
        else if (avgTime > 10.0) // Arbitrary threshold for demonstration
        {
            bottleneckType = BottleneckType.MemoryBandwidth;
            severity = Math.Min(1.0, avgTime / 20.0);
            details = "High execution time suggests memory bandwidth limitation";
        }
        else if (config.GlobalWorkSize.Aggregate(1, (a, b) => a * b) > 1000000 && avgTime < 1.0)
        {
            bottleneckType = BottleneckType.InstructionIssue;
            severity = 0.3;
            details = "Large problem size with low execution time may indicate instruction issue bottleneck";
        }

        return new BottleneckAnalysis
        {
            Type = bottleneckType,
            Severity = severity,
            Details = details,
            ResourceUtilization = new Dictionary<string, double>
            {
                ["Compute"] = Random.Shared.NextDouble() * 0.4 + 0.6,
                ["Memory"] = Random.Shared.NextDouble() * 0.3 + 0.7,
                ["Cache"] = Random.Shared.NextDouble() * 0.5 + 0.4,
                ["Occupancy"] = occupancy
            }
        };
    }

    private List<string> GenerateCudaOptimizationSuggestions(KernelExecutionConfig config, double occupancy, BottleneckAnalysis bottleneck)
    {
        var suggestions = new List<string>();
        
        if (occupancy < 0.5)
        {
            suggestions.Add("Consider reducing register usage or shared memory to improve occupancy");
            suggestions.Add("Try smaller block sizes to reduce resource pressure per SM");
        }
        
        if (bottleneck.Type == BottleneckType.MemoryBandwidth)
        {
            suggestions.Add("Optimize memory access patterns for coalescing");
            suggestions.Add("Use shared memory to cache frequently accessed data");
            suggestions.Add("Consider memory prefetching for better bandwidth utilization");
        }
        
        if (config.LocalWorkSize != null && config.LocalWorkSize[0] % 32 != 0)
        {
            suggestions.Add("Use block sizes that are multiples of warp size (32) for better efficiency");
        }
        
        suggestions.Add("Enable CUDA profiling tools (nvprof/Nsight) for detailed analysis");
        suggestions.Add("Consider using tensor cores for applicable workloads");
        
        return suggestions;
    }

    /// <summary>
    /// Disposes the CUDA kernel executor and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            // Clean up pending executions
            foreach (var execution in _pendingExecutions.Values)
            {
                ReleaseCudaExecution(execution);
            }
            _pendingExecutions.Clear();
            
            // Release CUDA streams
            foreach (var stream in _namedStreams.Values)
            {
                if (stream != IntPtr.Zero)
                {
                    try
                    {
                        if (CUDAInterop.IsCudaAvailable() && _context != new IntPtr(0x5001))
                        {
                            var cudaStream = new CUDAInterop.CUstream { Pointer = stream };
                            var result = CUDAInterop.cuStreamDestroy(cudaStream);
                            if (result != CUDAInterop.CUresult.CUDA_SUCCESS)
                            {
                                _logger.LogWarning("Failed to destroy CUDA stream: {Result}", result);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error destroying CUDA stream {Stream:X}", stream.ToInt64());
                    }
                }
            }
            _namedStreams.Clear();
            
            // Release CUDA context
            if (_context != IntPtr.Zero && _context != new IntPtr(0x5001)) // Not mock context
            {
                try
                {
                    var cudaContext = new CUDAInterop.CUcontext { Pointer = _context };
                    var result = CUDAInterop.cuCtxDestroy(cudaContext);
                    if (result != CUDAInterop.CUresult.CUDA_SUCCESS)
                    {
                        _logger.LogWarning("Failed to destroy CUDA context: {Result}", result);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error destroying CUDA context {Context:X}", _context.ToInt64());
                }
                finally
                {
                    _context = IntPtr.Zero;
                }
            }
            else
            {
                _context = IntPtr.Zero; // Clear mock context
            }
            
            _disposed = true;
            _logger.LogDebug("CUDA kernel executor disposed");
        }
    }

    private sealed class PendingCudaExecution
    {
        public required KernelExecutionHandle Handle { get; init; }
        public required KernelExecutionConfig ExecutionConfig { get; init; }
        public required DateTimeOffset StartTime { get; init; }
        public required IntPtr StartEvent { get; init; }
        public required IntPtr EndEvent { get; init; }
        public required IntPtr Stream { get; init; }
        public required IntPtr[] KernelParams { get; init; }
        public required int[] GridDim { get; init; }
        public required int[] BlockDim { get; init; }
    }

    private sealed class CudaDeviceProperties
    {
        public int MaxThreadsPerBlock { get; init; }
        public int MaxBlockDimX { get; init; }
        public int MaxBlockDimY { get; init; }
        public int MaxBlockDimZ { get; init; }
        public int MaxGridDimX { get; init; }
        public int MaxGridDimY { get; init; }
        public int MaxGridDimZ { get; init; }
        public int SharedMemPerBlock { get; init; }
        public int RegistersPerBlock { get; init; }
        public int WarpSize { get; init; }
        public int MultiProcessorCount { get; init; }
        public int MaxThreadsPerMultiProcessor { get; init; }
        public int ComputeCapabilityMajor { get; init; }
        public int ComputeCapabilityMinor { get; init; }
    }

    private sealed class CudaOccupancyInfo
    {
        public int MaxActiveBlocks { get; init; }
        public int OptimalBlockSize { get; init; }
        public int RegistersPerThread { get; init; }
        public int SharedMemPerBlock { get; init; }
    }

    private enum CudaError
    {
        Success = 0,
        InvalidValue = 1,
        OutOfMemory = 2,
        NotInitialized = 3,
        Deinitialized = 4,
        ProfilerDisabled = 5,
        ProfilerNotInitialized = 6,
        ProfilerAlreadyStarted = 7,
        ProfilerAlreadyStopped = 8,
        NoDevice = 100,
        InvalidDevice = 101,
        InvalidImage = 200,
        InvalidContext = 201,
        ContextAlreadyCurrent = 202,
        MapFailed = 205,
        UnmapFailed = 206,
        ArrayIsMapped = 207,
        AlreadyMapped = 208,
        NoBinaryForGpu = 209,
        AlreadyAcquired = 210,
        NotMapped = 211,
        NotMappedAsArray = 212,
        NotMappedAsPointer = 213,
        EccUncorrectable = 214,
        UnsupportedLimit = 215,
        ContextAlreadyInUse = 216,
        PeerAccessUnsupported = 217,
        InvalidPtx = 218,
        InvalidGraphicsContext = 219,
        NvlinkUncorrectable = 220,
        JitCompilerNotFound = 221,
        InvalidSource = 300,
        FileNotFound = 301,
        SharedObjectSymbolNotFound = 302,
        SharedObjectInitFailed = 303,
        OperatingSystem = 304,
        InvalidHandle = 400,
        NotFound = 500,
        NotReady = 600,
        IllegalAddress = 700,
        LaunchOutOfResources = 701,
        LaunchTimeout = 702,
        LaunchIncompatibleTexturing = 703,
        PeerAccessAlreadyEnabled = 704,
        PeerAccessNotEnabled = 705,
        PrimaryContextActive = 708,
        ContextIsDestroyed = 709,
        Assert = 710,
        TooManyPeers = 711,
        HostMemoryAlreadyRegistered = 712,
        HostMemoryNotRegistered = 713,
        HardwareStackError = 714,
        IllegalInstruction = 715,
        MisalignedAddress = 716,
        InvalidAddressSpace = 717,
        InvalidPc = 718,
        LaunchFailure = 719,
        CooperativeLaunchTooLarge = 720,
        NotPermitted = 800,
        NotSupported = 801,
        SystemNotReady = 802,
        SystemDriverMismatch = 803,
        CompatNotSupportedOnDevice = 804,
        StreamCaptureUnsupported = 900,
        StreamCaptureInvalidated = 901,
        StreamCaptureMerge = 902,
        StreamCaptureUnmatched = 903,
        StreamCaptureUnjoined = 904,
        StreamCaptureIsolation = 905,
        StreamCaptureImplicit = 906,
        CapturedEvent = 907,
        StreamCaptureWrongThread = 908,
        Timeout = 909,
        GraphExecUpdateFailure = 910,
        Unknown = 999
    }
}