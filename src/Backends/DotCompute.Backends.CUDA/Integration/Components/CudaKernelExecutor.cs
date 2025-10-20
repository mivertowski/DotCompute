// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Execution;
using Microsoft.Extensions.Logging;
using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using KernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

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
    private readonly IKernelExecutor _kernelExecutor;
    private readonly KernelConfigurationOptimizer _configurationOptimizer;
    private readonly Dictionary<string, ManagedCompiledKernel> _kernelCache;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaKernelExecutor class.
    /// </summary>
    /// <param name="accelerator">The accelerator.</param>
    /// <param name="context">The context.</param>
    /// <param name="streamManager">The stream manager.</param>
    /// <param name="eventManager">The event manager.</param>
    /// <param name="logger">The logger.</param>

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

        // Create logger with correct generic type for Execution.CudaKernelExecutor
        var executorLogger = logger is ILogger<Execution.CudaKernelExecutor> typedLogger
            ? typedLogger
            : (ILogger<Execution.CudaKernelExecutor>)(object)logger;
        _kernelExecutor = new Execution.CudaKernelExecutor(accelerator, context, streamManager, eventManager, executorLogger);
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

            // Execute through kernel executor
            var compiledKernel = kernel as CompiledKernel ?? throw new InvalidOperationException("Kernel must be a CompiledKernel");
            var result = await _kernelExecutor.ExecuteAndWaitAsync(
                compiledKernel, arguments, executionConfig, cancellationToken).ConfigureAwait(false);

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
                // PerformanceMetrics handled by base result
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
            var accelerator = _kernelExecutor.Accelerator;
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

        // Note: Statistics would need to be implemented in IKernelExecutor
        return new KernelExecutorStatistics
        {
            TotalExecutions = 0, // TODO: Implement statistics in IKernelExecutor
            SuccessfulExecutions = 0,
            FailedExecutions = 0,
            AverageExecutionTime = TimeSpan.Zero,
            CachedKernels = _kernelCache.Count,
            LastExecutionTime = DateTimeOffset.MinValue
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
            var arg = arguments[i] ?? throw new ArgumentException($"Kernel argument at index {i} is null", nameof(arguments));
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
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
            (_kernelExecutor as IDisposable)?.Dispose();

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
    /// <summary>
    /// Gets or sets the kernel.
    /// </summary>
    /// <value>The kernel.</value>
    public ICompiledKernel Kernel { get; init; }
    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    /// <value>The arguments.</value>
    public KernelArgument[] Arguments { get; init; }
    /// <summary>
    /// Gets or sets the execution config.
    /// </summary>
    /// <value>The execution config.</value>
    public KernelExecutionConfig ExecutionConfig { get; init; }
}

/// <summary>
/// Enhanced kernel execution result with optimization information.
/// </summary>
public sealed class KernelExecutionResult
{
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; init; }
    /// <summary>
    /// Gets or sets the execution time.
    /// </summary>
    /// <value>The execution time.</value>
    public TimeSpan ExecutionTime { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    /// <value>The performance metrics.</value>
    public object? PerformanceMetrics { get; init; }
    /// <summary>
    /// Gets or sets the optimizations applied.
    /// </summary>
    /// <value>The optimizations applied.</value>
    public bool OptimizationsApplied { get; init; }
    /// <summary>
    /// Gets or sets the configuration used.
    /// </summary>
    /// <value>The configuration used.</value>
    public KernelExecutionConfig? ConfigurationUsed { get; init; }
}

/// <summary>
/// Kernel executor performance statistics.
/// </summary>
public readonly record struct KernelExecutorStatistics
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; init; }
    /// <summary>
    /// Gets or sets the successful executions.
    /// </summary>
    /// <value>The successful executions.</value>
    public long SuccessfulExecutions { get; init; }
    /// <summary>
    /// Gets or sets the failed executions.
    /// </summary>
    /// <value>The failed executions.</value>
    public long FailedExecutions { get; init; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>
    public TimeSpan AverageExecutionTime { get; init; }
    /// <summary>
    /// Gets or sets the cached kernels.
    /// </summary>
    /// <value>The cached kernels.</value>
    public int CachedKernels { get; init; }
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; init; }
}

/// <summary>
/// Managed compiled kernel wrapper with metadata.
/// </summary>
public sealed class ManagedCompiledKernel(ICompiledKernel kernel, KernelDefinition definition) : IDisposable
{
    private readonly ICompiledKernel _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    private readonly KernelDefinition _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    private volatile bool _disposed;
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>

    public string Name => _kernel.Name;
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id => _kernel.Id;
    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    /// <value>The definition.</value>
    public KernelDefinition Definition => _definition;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _disposed;
    /// <summary>
    /// Gets execute asynchronously.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ManagedCompiledKernel));
        }

        return _kernel.ExecuteAsync(arguments, cancellationToken);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _kernel?.Dispose();
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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
internal sealed class KernelConfigurationOptimizer(CudaContext context, ILogger logger) : IDisposable
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private volatile bool _disposed;
    /// <summary>
    /// Gets optimize configuration.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="problemSize">The problem size.</param>
    /// <returns>The result of the operation.</returns>

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

    private static int CalculateOptimalGridSize(int totalElements, int blockSize) => (totalElements + blockSize - 1) / blockSize;
    /// <summary>
    /// Performs dispose.
    /// </summary>

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