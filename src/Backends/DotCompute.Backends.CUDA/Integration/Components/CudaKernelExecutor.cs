// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Execution;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Core.Kernels;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// CUDA kernel execution orchestrator that provides high-level kernel execution
/// services with optimization, configuration management, and performance monitoring.
/// </summary>
public sealed class CudaKernelExecutor : IDisposable
{
    private readonly ILogger<CudaKernelExecutor> _logger;
    private readonly CudaContext _context;
    private readonly CudaStreamManager _streamManager;
    private readonly CudaEventManager _eventManager;
    private readonly CudaKernelExecutor _internalExecutor;
    private readonly KernelConfigurationOptimizer _configurationOptimizer;
    private readonly Dictionary<string, ManagedCompiledKernel> _kernelCache;
    private volatile bool _disposed;

    public CudaKernelExecutor(
        IAccelerator accelerator,
        CudaContext context,
        CudaStreamManager streamManager,
        CudaEventManager eventManager,
        ILogger<CudaKernelExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
        _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));

        _internalExecutor = new CudaKernelExecutor(accelerator, context, streamManager, eventManager, logger);
        _configurationOptimizer = new KernelConfigurationOptimizer(context, logger);
        _kernelCache = [];

        _logger.LogDebug("CUDA kernel execution orchestrator initialized for device {DeviceId}", context.DeviceId);
    }

    /// <summary>
    /// Executes a kernel with full optimization and monitoring.
    /// </summary>
    /// <param name="kernel">Compiled kernel to execute.</param>
    /// <param name="arguments">Kernel arguments.</param>
    /// <param name="config">Execution configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result with performance metrics.</returns>
    public async Task<KernelExecutionResult> ExecuteAndWaitAsync(
        ICompiledKernel kernel,
        KernelArgument[] arguments,
        KernelExecutionConfig config,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(config);

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("Executing kernel {KernelName} with {ArgumentCount} arguments",
                kernel.Name, arguments.Length);

            // Validate arguments
            ValidateKernelArguments(arguments);

            // Get optimal configuration if not specified
            var executionConfig = config.GlobalWorkSize == null ?
                await GetOptimalConfigurationAsync(kernel, arguments, cancellationToken).ConfigureAwait(false) :
                config;

            // Execute through internal executor
            var result = await _internalExecutor.ExecuteAndWaitAsync(
                kernel, arguments, executionConfig, cancellationToken).ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;
            var executionTime = endTime - startTime;

            _logger.LogDebug("Kernel {KernelName} executed in {ExecutionTime:F2}ms with success: {Success}",
                kernel.Name, executionTime.TotalMilliseconds, result.Success);

            // Enhance result with additional metrics
            return new KernelExecutionResult
            {
                Success = result.Success,
                ExecutionTime = executionTime,
                ErrorMessage = result.ErrorMessage,
                PerformanceMetrics = result.PerformanceMetrics,
                OptimizationsApplied = true,
                ConfigurationUsed = executionConfig
            };
        }
        catch (Exception ex)
        {
            var endTime = DateTimeOffset.UtcNow;
            var executionTime = endTime - startTime;

            _logger.LogError(ex, "Failed to execute kernel {KernelName}", kernel.Name);

            return new KernelExecutionResult
            {
                Success = false,
                ExecutionTime = executionTime,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets optimal execution configuration for a kernel.
    /// </summary>
    /// <param name="kernel">Kernel to optimize for.</param>
    /// <param name="problemSize">Problem size dimensions.</param>
    /// <returns>Optimal execution configuration.</returns>
    public KernelExecutionConfig GetOptimalExecutionConfig(ICompiledKernel kernel, int[] problemSize)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(problemSize);

        try
        {
            return _configurationOptimizer.OptimizeConfiguration(kernel, problemSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize configuration for kernel {KernelName}, using fallback", kernel.Name);
            return CreateFallbackConfiguration(problemSize);
        }
    }

    /// <summary>
    /// Compiles and caches a kernel for efficient reuse.
    /// </summary>
    /// <param name="definition">Kernel definition.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Compiled and cached kernel.</returns>
    public async Task<ManagedCompiledKernel> CompileAndCacheKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(definition);

        var kernelKey = GenerateKernelKey(definition, options);

        // Check cache first
        if (_kernelCache.TryGetValue(kernelKey, out var cachedKernel))
        {
            _logger.LogDebug("Using cached kernel {KernelName}", definition.Name);
            return cachedKernel;
        }

        try
        {
            _logger.LogDebug("Compiling kernel {KernelName}", definition.Name);

            // Compile kernel using the accelerator
            var accelerator = _internalExecutor.Accelerator;
            var compiledKernel = await accelerator.CompileKernelAsync(definition, options, cancellationToken)
                .ConfigureAwait(false);

            // Wrap in managed kernel
            var managedKernel = new ManagedCompiledKernel(compiledKernel, definition);

            // Cache for reuse
            _kernelCache[kernelKey] = managedKernel;

            _logger.LogDebug("Kernel {KernelName} compiled and cached successfully", definition.Name);
            return managedKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile kernel {KernelName}", definition.Name);
            throw;
        }
    }

    /// <summary>
    /// Executes multiple kernels in a batch with optimized scheduling.
    /// </summary>
    /// <param name="kernelBatch">Batch of kernels to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of execution results.</returns>
    public async Task<KernelExecutionResult[]> ExecuteBatchAsync(
        KernelBatchExecution[] kernelBatch,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernelBatch);

        if (kernelBatch.Length == 0)
        {
            return [];
        }

        _logger.LogDebug("Executing batch of {KernelCount} kernels", kernelBatch.Length);

        var results = new KernelExecutionResult[kernelBatch.Length];
        var tasks = new Task[kernelBatch.Length];

        // Execute kernels concurrently using different streams
        for (var i = 0; i < kernelBatch.Length; i++)
        {
            var index = i;
            var batch = kernelBatch[i];

            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    // Use different streams for parallel execution
                    var stream = index % 2 == 0 ?
                        _streamManager.DefaultStream :
                        _streamManager.HighPriorityStream;

                    var config = batch.ExecutionConfig with { Stream = stream };

                    results[index] = await ExecuteAndWaitAsync(
                        batch.Kernel, batch.Arguments, config, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute kernel {Index} in batch", index);
                    results[index] = new KernelExecutionResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message
                    };
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        var successCount = results.Count(r => r.Success);
        _logger.LogDebug("Batch execution completed: {SuccessCount}/{TotalCount} kernels succeeded",
            successCount, kernelBatch.Length);

        return results;
    }

    /// <summary>
    /// Gets execution statistics for performance monitoring.
    /// </summary>
    /// <returns>Execution statistics.</returns>
    public KernelExecutorStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new KernelExecutorStatistics
        {
            TotalExecutions = _internalExecutor.Statistics.TotalExecutions,
            SuccessfulExecutions = _internalExecutor.Statistics.SuccessfulExecutions,
            FailedExecutions = _internalExecutor.Statistics.FailedExecutions,
            AverageExecutionTime = _internalExecutor.Statistics.AverageExecutionTime,
            CachedKernels = _kernelCache.Count,
            LastExecutionTime = _internalExecutor.Statistics.LastExecutionTime
        };
    }

    #region Private Methods

    private async Task<KernelExecutionConfig> GetOptimalConfigurationAsync(
        ICompiledKernel kernel,
        KernelArgument[] arguments,
        CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken).ConfigureAwait(false);

        // Estimate problem size from arguments
        var problemSize = EstimateProblemSizeFromArguments(arguments);

        // Get optimal configuration
        return GetOptimalExecutionConfig(kernel, problemSize);
    }

    private static int[] EstimateProblemSizeFromArguments(KernelArgument[] arguments)
    {
        // Simple heuristic to estimate problem size from arguments
        foreach (var arg in arguments)
        {
            if (arg.Value is int size && size > 0)
            {
                return [size];
            }
            if (arg.Value is int[] dimensions)
            {
                return dimensions;
            }
            if (arg.MemoryBuffer != null)
            {
                // Try to get buffer size through reflection
                var sizeProperty = arg.MemoryBuffer.GetType().GetProperty("Length") ??
                                  arg.MemoryBuffer.GetType().GetProperty("Count") ??
                                  arg.MemoryBuffer.GetType().GetProperty("Size");

                if (sizeProperty?.GetValue(arg.MemoryBuffer) is int bufferSize && bufferSize > 0)
                {
                    return [bufferSize];
                }
            }
        }

        return [1]; // Default size
    }

    private static void ValidateKernelArguments(KernelArgument[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        for (var i = 0; i < arguments.Length; i++)
        {
            var arg = arguments[i];
            if (arg == null)
            {
                throw new ArgumentException($"Kernel argument at index {i} is null", nameof(arguments));
            }

            if (string.IsNullOrEmpty(arg.Name))
            {
                throw new ArgumentException($"Kernel argument at index {i} has no name", nameof(arguments));
            }
        }
    }

    private static string GenerateKernelKey(KernelDefinition definition, CompilationOptions? options)
    {
        var optionsHash = options?.GetHashCode().ToString() ?? "default";
        return $"{definition.Name}_{definition.GetHashCode()}_{optionsHash}";
    }

    private KernelExecutionConfig CreateFallbackConfiguration(int[] problemSize)
    {
        var totalSize = problemSize.Aggregate(1, (a, b) => a * b);

        return new KernelExecutionConfig
        {
            GlobalWorkSize = [Math.Min(totalSize, 65536)], // Max grid size fallback
            LocalWorkSize = [Math.Min(totalSize, 256)],    // Max block size fallback
            Stream = _streamManager.DefaultStream,
            CaptureTimings = true
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaKernelExecutor));
        }
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Dispose cached kernels
            foreach (var kernel in _kernelCache.Values)
            {
                try
                {
                    kernel.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing cached kernel");
                }
            }
            _kernelCache.Clear();

            _configurationOptimizer?.Dispose();
            _internalExecutor?.Dispose();

            _logger.LogDebug("CUDA kernel execution orchestrator disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Kernel batch execution item.
/// </summary>
public readonly record struct KernelBatchExecution
{
    public ICompiledKernel Kernel { get; init; }
    public KernelArgument[] Arguments { get; init; }
    public KernelExecutionConfig ExecutionConfig { get; init; }
}

/// <summary>
/// Enhanced kernel execution result with optimization information.
/// </summary>
public sealed class KernelExecutionResult
{
    public bool Success { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public string? ErrorMessage { get; init; }
    public object? PerformanceMetrics { get; init; }
    public bool OptimizationsApplied { get; init; }
    public KernelExecutionConfig? ConfigurationUsed { get; init; }
}

/// <summary>
/// Kernel executor performance statistics.
/// </summary>
public readonly record struct KernelExecutorStatistics
{
    public long TotalExecutions { get; init; }
    public long SuccessfulExecutions { get; init; }
    public long FailedExecutions { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public int CachedKernels { get; init; }
    public DateTimeOffset LastExecutionTime { get; init; }
}

/// <summary>
/// Managed compiled kernel wrapper with metadata.
/// </summary>
public sealed class ManagedCompiledKernel : IDisposable
{
    private readonly ICompiledKernel _kernel;
    private readonly KernelDefinition _definition;
    private volatile bool _disposed;

    public ManagedCompiledKernel(ICompiledKernel kernel, KernelDefinition definition)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Name => _kernel.Name;
    public Guid Id => _kernel.Id;
    public KernelDefinition Definition => _definition;
    public bool IsDisposed => _disposed;

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ManagedCompiledKernel));
        }

        return _kernel.ExecuteAsync(arguments, cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _kernel?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        return _kernel?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}

/// <summary>
/// Kernel configuration optimizer for CUDA execution.
/// </summary>
internal sealed class KernelConfigurationOptimizer : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private volatile bool _disposed;

    public KernelConfigurationOptimizer(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public KernelExecutionConfig OptimizeConfiguration(ICompiledKernel kernel, int[] problemSize)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(KernelConfigurationOptimizer));
        }

        try
        {
            var totalElements = problemSize.Aggregate(1, (a, b) => a * b);

            // Simple optimization based on problem size
            var blockSize = CalculateOptimalBlockSize(totalElements);
            var gridSize = CalculateOptimalGridSize(totalElements, blockSize);

            return new KernelExecutionConfig
            {
                GlobalWorkSize = [gridSize],
                LocalWorkSize = [blockSize],
                CaptureTimings = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize kernel configuration");
            throw;
        }
    }

    private static int CalculateOptimalBlockSize(int totalElements)
    {
        // CUDA-specific optimization
        if (totalElements < 128)
        {
            return Math.Max(32, totalElements); // Minimum warp size
        }
        else if (totalElements < 1024)
        {
            return 128;
        }
        else
        {
            return 256; // Common optimal block size
        }
    }

    private static int CalculateOptimalGridSize(int totalElements, int blockSize)
    {
        return (totalElements + blockSize - 1) / blockSize;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // No explicit cleanup needed
        }
    }
}

#endregion