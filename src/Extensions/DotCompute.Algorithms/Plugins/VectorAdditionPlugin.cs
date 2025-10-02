// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Core.Extensions;
using DotCompute.Core.Kernels;
using ManagedCompiledKernel = DotCompute.Core.Kernels.ManagedCompiledKernel;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using KernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Algorithms.Plugins
{

/// <summary>
/// Adapter to use ILogger VectorAdditionPlugin as ILogger KernelManager.
/// </summary>
internal class KernelManagerLoggerWrapper(ILogger innerLogger) : ILogger<KernelManager>
{
    private readonly ILogger _innerLogger = innerLogger;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _innerLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _innerLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _innerLogger.Log(logLevel, eventId, state, exception, formatter);
}

    /// <summary>
    /// Algorithm plugin for element-wise vector addition with GPU acceleration.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="VectorAdditionPlugin"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled in OnDisposeAsync
    public sealed partial class VectorAdditionPlugin(ILogger<VectorAdditionPlugin> logger) : AlgorithmPluginBase(logger)
#pragma warning restore CA1001
{
    private KernelManager? _kernelManager;
    private MemoryAllocator? _memoryAllocator;
    private ManagedCompiledKernel? _cachedKernel;
    private readonly Lock _kernelLock = new();

        /// <inheritdoc/>
        public override string Id => "com.dotcompute.algorithms.vector.add";

    /// <inheritdoc/>
    public override string Name => "Vector Addition";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "Performs element-wise addition of two vectors of equal length with GPU acceleration.";

    /// <inheritdoc/>
    public override AcceleratorType[] SupportedAcceleratorTypes => [
        AcceleratorType.CPU,
        AcceleratorType.CUDA,
        AcceleratorType.ROCm,
        AcceleratorType.OpenCL,
        AcceleratorType.OneAPI
    ];

    /// <inheritdoc/>
    public override IReadOnlyList<string> SupportedOperations => [
        "VectorAddition",
        "ElementwiseOperation",
        "ParallelComputation"
    ];

    /// <inheritdoc/>
    public override Type[] InputTypes => [typeof(float[]), typeof(float[])];

    /// <inheritdoc/>
    public override Type OutputType => typeof(float[]);

    /// <inheritdoc/>
    protected override Task OnInitializeAsync(IAccelerator accelerator, CancellationToken cancellationToken)
    {
        _memoryAllocator = new MemoryAllocator();
        
        // Create kernel manager for GPU execution
        if (Enum.Parse<AcceleratorType>(accelerator.Info.DeviceType) != AcceleratorType.CPU)
        {
            // Create logger wrapper for KernelManager
            var kernelLogger = new KernelManagerLoggerWrapper(Logger);
            _kernelManager = new KernelManager(kernelLogger);
        }
        
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async Task<object> OnExecuteAsync(object[] inputs, Dictionary<string, object>? parameters, CancellationToken cancellationToken)
    {
        var a = (float[])inputs[0];
        var b = (float[])inputs[1];

        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length.");
        }

        var length = a.Length;

        // Use GPU if available and vectors are large enough
        if (_kernelManager != null && 
            Enum.Parse<AcceleratorType>(Accelerator.Info.DeviceType) != AcceleratorType.CPU && 
            length >= 1024) // GPU is beneficial for larger vectors
        {
            return await ExecuteOnGPUAsync(a, b, length, cancellationToken).ConfigureAwait(false);
        }
        
        // CPU implementation with SIMD when possible
        return await ExecuteOnCPUAsync(a, b, length, cancellationToken).ConfigureAwait(false);
    }

    private async Task<float[]> ExecuteOnGPUAsync(float[] a, float[] b, int length, CancellationToken cancellationToken)
    {
        try
        {
            // Get or compile kernel
            ManagedCompiledKernel kernel;
            if (_cachedKernel == null)
            {
                var compiled = await CompileVectorAddKernelAsync(cancellationToken).ConfigureAwait(false);
                lock (_kernelLock)
                {
                    _cachedKernel ??= compiled;
                }
            }
            kernel = _cachedKernel;

            // Allocate device memory
            var sizeInBytes = length * sizeof(float);
            var bufferA = await Accelerator.Memory.AllocateAsync<float>(length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferB = await Accelerator.Memory.AllocateAsync<float>(length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);
            var bufferResult = await Accelerator.Memory.AllocateAsync<float>(length, DotCompute.Abstractions.Memory.MemoryOptions.None, cancellationToken).ConfigureAwait(false);

            try
            {
                // Copy data to device
                await bufferA.WriteAsync(a, 0, cancellationToken).ConfigureAwait(false);
                await bufferB.WriteAsync(b, 0, cancellationToken).ConfigureAwait(false);

                // Prepare kernel arguments
                var arguments = new[]
                {
                    new KernelArgument
                    {
                        Name = "a",
                        Value = bufferA,
                        Type = typeof(float[]),
                        IsDeviceMemory = true,
                        MemoryBuffer = bufferA,
                        SizeInBytes = sizeInBytes
                    },
                    new KernelArgument
                    {
                        Name = "b",
                        Value = bufferB,
                        Type = typeof(float[]),
                        IsDeviceMemory = true,
                        MemoryBuffer = bufferB,
                        SizeInBytes = sizeInBytes
                    },
                    new KernelArgument
                    {
                        Name = "result",
                        Value = bufferResult,
                        Type = typeof(float[]),
                        IsDeviceMemory = true,
                        MemoryBuffer = bufferResult,
                        SizeInBytes = sizeInBytes
                    },
                    new KernelArgument
                    {
                        Name = "n",
                        Value = length,
                        Type = typeof(int),
                        IsDeviceMemory = false
                    }
                };

                // Configure execution
                var workGroupSize = Math.Min(256, Accelerator.Info.MaxThreadsPerBlock);
                var globalSize = ((length + workGroupSize - 1) / workGroupSize) * workGroupSize;
                
                var config = new KernelExecutionConfig
                {
                    GlobalWorkSize = [globalSize],
                    LocalWorkSize = [workGroupSize],
                    CaptureTimings = true
                };

                // Execute kernel
                var result = await _kernelManager!.ExecuteKernelAsync(
                    kernel,
                    arguments,
                    Accelerator,
                    config,
                    cancellationToken).ConfigureAwait(false);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"GPU execution failed: {result.ErrorMessage}");
                }

                // Read results
                var output = new float[length];
                await bufferResult.ReadAsync(output, 0, cancellationToken).ConfigureAwait(false);

                LogGPUExecutionCompleted(length, result.Timings?.KernelTimeMs ?? 0);

                return output;
            }
            finally
            {
                // Cleanup device memory
                await bufferA.DisposeAsync().ConfigureAwait(false);
                await bufferB.DisposeAsync().ConfigureAwait(false);
                await bufferResult.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogGPUExecutionFailed(ex.Message);
            // Fallback to CPU
            return await ExecuteOnCPUAsync(a, b, length, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ManagedCompiledKernel> CompileVectorAddKernelAsync(CancellationToken cancellationToken)
    {
        var context = new KernelGenerationContext
        {
            DeviceInfo = Accelerator.Info,
            UseSharedMemory = false, // Not needed for simple vector addition
            UseVectorTypes = true,
            Precision = PrecisionMode.Single,
            WorkGroupDimensions = [256]
        };

        return await _kernelManager!.GetOrCompileOperationKernelAsync(
            "VectorAdd",
            [typeof(float[]), typeof(float[])],
            typeof(float[]),
            Accelerator,
            context,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<float[]> ExecuteOnCPUAsync(float[] a, float[] b, int length, CancellationToken cancellationToken)
    {
        var result = new float[length];
        
        await Task.Run(() =>
        {
            // Use parallel processing for large vectors
            if (length > 10000)
            {
                const int batchSize = 1000;
                _ = Parallel.For(0, (length + batchSize - 1) / batchSize, batch =>
                {
                    var start = batch * batchSize;
                    var end = Math.Min(start + batchSize, length);

                    for (var i = start; i < end; i++)
                    {
                        result[i] = a[i] + b[i];
                    }
                });
            }
            else
            {
                // Simple loop for small vectors
                for (var i = 0; i < length; i++)
                {
                    result[i] = a[i] + b[i];
                }
            }
        }, cancellationToken).ConfigureAwait(false);
        
        return result;
    }

    /// <inheritdoc/>
    public override bool ValidateInputs(object[] inputs)
    {
        if (!base.ValidateInputs(inputs))
        {
            return false;
        }

        if (inputs.Length != 2)
        {
            return false;
        }

        if (inputs[0] is not float[] a || inputs[1] is not float[] b)
        {
            return false;
        }

        return a.Length == b.Length && a.Length > 0;
    }

    /// <inheritdoc/>
    public override long EstimateMemoryRequirement(int[] inputSizes)
    {
        if (inputSizes.Length != 2)
        {
            throw new ArgumentException("Expected 2 input sizes.");
        }

        var vectorLength = inputSizes[0];
        
        if (Accelerator != null && Enum.Parse<AcceleratorType>(Accelerator.Info.DeviceType) != AcceleratorType.CPU)
        {
            // GPU memory: input A + input B + result + kernel overhead
            return vectorLength * sizeof(float) * 3 + 4096;
        }
        else
        {
            // CPU memory: input A + input B + result + workspace
            return vectorLength * sizeof(float) * 3 + 1024;
        }
    }

    /// <inheritdoc/>
    public override AlgorithmPerformanceProfile GetPerformanceProfile()
    {
        var profile = new AlgorithmPerformanceProfile
        {
            Complexity = "O(n)",
            IsParallelizable = true,
            OptimalParallelism = Accelerator != null && Enum.Parse<AcceleratorType>(Accelerator.Info.DeviceType) == AcceleratorType.CPU 
                ? Environment.ProcessorCount * 2 
                : Accelerator?.Info.ComputeUnits ?? 32,
            IsMemoryBound = true,
            IsComputeBound = false,
            EstimatedFlops = 1, // One addition per element
            Metadata = []
        };

        if (Accelerator != null && Enum.Parse<AcceleratorType>(Accelerator.Info.DeviceType) == AcceleratorType.CPU)
        {
            profile.Metadata["VectorizationSupported"] = true;
            profile.Metadata["SimdInstructions"] = new[] { "SSE", "AVX", "AVX2", "AVX512" };
            profile.Metadata["MinVectorLength"] = 16;
            profile.Metadata["OptimalVectorLength"] = 1024;
        }
        else
        {
            profile.Metadata["GPUAccelerated"] = true;
            profile.Metadata["MinVectorLength"] = 1024;
            profile.Metadata["OptimalVectorLength"] = 1048576; // 1M elements
            profile.Metadata["MaxThreadsPerBlock"] = Accelerator?.Info.MaxThreadsPerBlock ?? 256;
        }

        return profile;
    }

    /// <inheritdoc/>
    protected override ValueTask OnDisposeAsync()
    {
        _cachedKernel?.Dispose();
        _cachedKernel = null;
        _kernelManager?.Dispose();
        _kernelManager = null;
        _memoryAllocator?.Dispose();
        _memoryAllocator = null;
        return ValueTask.CompletedTask;
    }

    #region Logging

    private partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "GPU execution completed: {VectorLength} elements in {TimeMs:F2} ms")]
        public static partial void GPUExecutionCompleted(ILogger logger, int vectorLength, double timeMs);

        [LoggerMessage(2, LogLevel.Warning, "GPU execution failed: {Reason}. Falling back to CPU.")]
        public static partial void GPUExecutionFailed(ILogger logger, string reason);
    }

    private void LogGPUExecutionCompleted(int vectorLength, double timeMs) => Log.GPUExecutionCompleted(Logger, vectorLength, timeMs);
    
    private void LogGPUExecutionFailed(string reason) => Log.GPUExecutionFailed(Logger, reason);

    #endregion
}}
