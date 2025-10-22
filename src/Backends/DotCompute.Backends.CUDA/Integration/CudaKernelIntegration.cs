// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Execution;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;
using DotCompute.Abstractions.Kernels;
using AbstractionsKernelArgument = DotCompute.Abstractions.Kernels.KernelArgument;
using InterfacesKernelArgument = DotCompute.Abstractions.Interfaces.Kernels.KernelArgument;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Integrates CUDA kernel compilation and execution with optimization
/// </summary>
public sealed partial class CudaKernelIntegration : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaKernelCompiler _compiler;
    private readonly CudaKernelExecutor _executor;
    private readonly CudaKernelCache _cache;
    private readonly Dictionary<string, KernelExecutionStats> _executionStats;
    private readonly Timer _optimizationTimer;
    private readonly object _statsLock = new();
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaKernelIntegration class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

    public CudaKernelIntegration(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));


        _compiler = new CudaKernelCompiler(context, logger);
        // Create logger with correct generic type for CudaKernelExecutor
        var executorLogger = logger is ILogger<CudaKernelExecutor> typedLogger
            ? typedLogger
            : (ILogger<CudaKernelExecutor>)(object)logger;
        _executor = new CudaKernelExecutor(context.ToIAccelerator(), context, null!, null!, executorLogger);
        _cache = new CudaKernelCache(logger);
        _executionStats = [];

        // Set up periodic optimization
        _optimizationTimer = new Timer(PerformOptimization, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10));

        LogIntegrationInitialized(context.DeviceId);
    }

    /// <summary>
    /// Gets the kernel compiler
    /// </summary>
    public CudaKernelCompiler Compiler => _compiler;

    /// <summary>
    /// Gets the kernel executor
    /// </summary>
    public CudaKernelExecutor Executor => _executor;

    /// <summary>
    /// Gets the kernel cache
    /// </summary>
    public CudaKernelCache Cache => _cache;

    /// <summary>
    /// Compiles a kernel with caching and optimization
    /// </summary>
    public async Task<CudaCompiledKernel> CompileOptimizedKernelAsync(
        string kernelSource,
        string kernelName,
        CudaCompilationOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Check cache first
            var cacheKey = GenerateCacheKey(kernelSource, kernelName, options);
            if (_cache.TryGetKernel(cacheKey, out var cachedKernel))
            {
                LogKernelCacheHit(kernelName);
                return (cachedKernel as CudaCompiledKernel)!; // Non-null when TryGetKernel returns true, cast to specific type
            }

            // Compile with optimizations
            var optimizedOptions = OptimizeCompilationOptions(options);
            // Create KernelDefinition for compiler
            var kernelDef = new KernelDefinition { Name = kernelName };
            var compiled = await _compiler.CompileKernelAsync(kernelDef, optimizedOptions, cancellationToken);
            var compiledKernel = compiled as CudaCompiledKernel ?? throw new InvalidOperationException($"Expected CudaCompiledKernel but got {compiled?.GetType().Name}");

            // Cache the result
            _cache.CacheKernel(cacheKey, compiledKernel!); // Non-null after successful compilation and cast


            LogKernelCompiledAndCached(kernelName);


            return compiledKernel!; // Non-null after successful compilation
        }
        catch (Exception ex)
        {
            LogCompileOptimizedFailed(ex, kernelName);
            throw;
        }
    }

    /// <summary>
    /// Executes a kernel with performance tracking
    /// </summary>
    public async Task<KernelExecutionResult> ExecuteKernelAsync(
        CudaCompiledKernel kernel,
        AbstractionsKernelArgument[] arguments,
        KernelExecutionConfig config,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var startTime = DateTimeOffset.UtcNow;


        try
        {
            // Convert arguments
            var convertedArguments = ConvertKernelArguments(arguments);

            // Execute kernel

            var result = await _executor.ExecuteAndWaitAsync(
                kernel.ToCompiledKernel(), convertedArguments, config, cancellationToken);

            var endTime = DateTimeOffset.UtcNow;

            // Record execution statistics

            RecordExecutionStats(kernel.Name, endTime - startTime, result.Success);


            LogKernelExecuted(kernel.Name, result.Success, endTime - startTime);


            return result;
        }
        catch (Exception ex)
        {
            var endTime = DateTimeOffset.UtcNow;
            RecordExecutionStats(kernel.Name, endTime - startTime, false);


            LogKernelExecutionFailed(ex, kernel.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets optimal execution configuration for a kernel
    /// </summary>
    public KernelExecutionConfig GetOptimalExecutionConfig(
        CudaCompiledKernel kernel,
        int[] problemSize)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Use executor's optimization logic
            return _executor.GetOptimalExecutionConfig(kernel.ToCompiledKernel(), problemSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get optimal execution config for kernel '{KernelName}'", kernel.Name);

            // Return default configuration

            return new KernelExecutionConfig
            {
                GlobalWorkSize = problemSize,
                LocalWorkSize = [Math.Min(256, problemSize[0])],
                DynamicSharedMemorySize = 0,
                Stream = null,
                Flags = 0, // KernelLaunchFlags.None
                CaptureTimings = false
            };
        }
    }

    /// <summary>
    /// Gets kernel execution statistics
    /// </summary>
    public IReadOnlyDictionary<string, KernelExecutionStats> GetExecutionStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_statsLock)
        {
            return new Dictionary<string, KernelExecutionStats>(_executionStats);
        }
    }

    /// <summary>
    /// Gets kernel health status
    /// </summary>
    public double GetKernelHealth()
    {
        if (_disposed)
        {
            return 0.0;
        }

        try
        {
            lock (_statsLock)
            {
                if (_executionStats.Count == 0)
                {
                    return 1.0; // No kernels = healthy
                }

                var totalExecutions = _executionStats.Values.Sum(s => s.TotalExecutions);
                var successfulExecutions = _executionStats.Values.Sum(s => s.SuccessfulExecutions);


                if (totalExecutions == 0)
                {
                    return 1.0;
                }

                var successRate = (double)successfulExecutions / totalExecutions;

                // Health is based on success rate and recent performance

                return successRate;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating kernel health");
            return 0.0;
        }
    }

    /// <summary>
    /// Optimizes kernels for the given workload
    /// </summary>
    public async Task OptimizeKernelsAsync(CudaWorkloadProfile profile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            try
            {
                // Optimize cache based on workload
                _cache.OptimizeForWorkload(profile);

                // Update compilation options for future kernels

                UpdateOptimizationStrategies(profile);


                LogOptimizationCompleted();
            }
            catch (Exception ex)
            {
                LogOptimizationFailed(ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Performs maintenance on kernel resources
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Clean up old cache entries
            _cache.Cleanup();

            // Reset old statistics

            lock (_statsLock)
            {
                var oldEntries = _executionStats
                    .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.LastExecution > TimeSpan.FromHours(1))
                    .ToList();

                foreach (var (key, _) in oldEntries)
                {
                    _ = _executionStats.Remove(key);
                }

                if (oldEntries.Count > 0)
                {
                    LogCleanedUpOldStats(oldEntries.Count);
                }
            }


            LogMaintenanceCompleted();
        }
        catch (Exception ex)
        {
            LogMaintenanceError(ex);
        }
    }

    private static string GenerateCacheKey(
        string kernelSource,
        string kernelName,
        CudaCompilationOptions options)
    {
        // Generate a cache key based on source, name, and options
        var sourceHash = kernelSource.GetHashCode();
        var optionsHash = options.GetHashCode();


        return $"{kernelName}_{sourceHash:X8}_{optionsHash:X8}";
    }

    private static CudaCompilationOptions OptimizeCompilationOptions(CudaCompilationOptions options)
    {
        // Create optimized compilation options
        return new CudaCompilationOptions
        {
            OptimizationLevel = options.OptimizationLevel, // Use as-is
            GenerateDebugInfo = options.GenerateDebugInfo,
            UseRestrictedPointers = true, // Enable restrict for better optimization
            EnableFastMath = options.EnableFastMath,
            MaxRegistersPerThread = options.MaxRegistersPerThread,
            AdditionalOptions = options.AdditionalOptions
        };
    }

    private static InterfacesKernelArgument[] ConvertKernelArguments(AbstractionsKernelArgument[] arguments)
    {
        return [.. arguments.Select(arg => new InterfacesKernelArgument
        {
            Name = arg.Name,
            Value = arg.Value ?? new object(),
            Type = arg.Type,
            IsDeviceMemory = arg.IsDeviceMemory,
            MemoryBuffer = arg.MemoryBuffer as IUnifiedMemoryBuffer,
            SizeInBytes = arg.SizeInBytes,
            IsOutput = arg.IsOutput
        })];
    }

    private void RecordExecutionStats(string kernelName, TimeSpan duration, bool success)
    {
        try
        {
            lock (_statsLock)
            {
                if (!_executionStats.TryGetValue(kernelName, out var stats))
                {
                    stats = new KernelExecutionStats
                    {
                        KernelName = kernelName,
                        FirstExecution = DateTimeOffset.UtcNow
                    };
                    _executionStats[kernelName] = stats;
                }

                stats.TotalExecutions++;
                if (success)
                {
                    stats.SuccessfulExecutions++;
                }


                stats.TotalExecutionTime += duration;
                stats.LastExecution = DateTimeOffset.UtcNow;


                if (duration < stats.FastestExecution || stats.FastestExecution == TimeSpan.Zero)
                {
                    stats.FastestExecution = duration;
                }


                if (duration > stats.SlowestExecution)
                {
                    stats.SlowestExecution = duration;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record execution statistics for kernel '{KernelName}'", kernelName);
        }
    }

    private void UpdateOptimizationStrategies(CudaWorkloadProfile profile)
    {
        try
        {
            // Update compiler optimization strategies based on workload
            // This could involve adjusting default compilation options
            LogStrategiesUpdated();
        }
        catch (Exception ex)
        {
            LogOptimizationFailed(ex);
        }
    }

    private void PerformOptimization(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Periodic optimization based on execution statistics
            lock (_statsLock)
            {
                var poorPerformingKernels = _executionStats.Values
                    .Where(s => s.TotalExecutions > 10 && (double)s.SuccessfulExecutions / s.TotalExecutions < 0.95)
                    .ToList();

                foreach (var stats in poorPerformingKernels)
                {
                    _logger.LogWarning("Kernel '{KernelName}' has poor performance: {SuccessRate:P2} success rate",
                        stats.KernelName, (double)stats.SuccessfulExecutions / stats.TotalExecutions);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during periodic optimization");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _optimizationTimer?.Dispose();

            // Clean up statistics

            lock (_statsLock)
            {
                _executionStats.Clear();
            }


            _cache?.Dispose();
            _executor?.Dispose();
            _compiler?.Dispose();


            _disposed = true;


            LogIntegrationDisposed();
        }
    }
}

/// <summary>
/// Kernel execution statistics
/// </summary>
public sealed class KernelExecutionStats
{
    /// <summary>
    /// Gets or sets the kernel name.
    /// </summary>
    /// <value>The kernel name.</value>
    public string KernelName { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public int TotalExecutions { get; set; }
    /// <summary>
    /// Gets or sets the successful executions.
    /// </summary>
    /// <value>The successful executions.</value>
    public int SuccessfulExecutions { get; set; }
    /// <summary>
    /// Gets or sets the total execution time.
    /// </summary>
    /// <value>The total execution time.</value>
    public TimeSpan TotalExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the fastest execution.
    /// </summary>
    /// <value>The fastest execution.</value>
    public TimeSpan FastestExecution { get; set; }
    /// <summary>
    /// Gets or sets the slowest execution.
    /// </summary>
    /// <value>The slowest execution.</value>
    public TimeSpan SlowestExecution { get; set; }
    /// <summary>
    /// Gets or sets the first execution.
    /// </summary>
    /// <value>The first execution.</value>
    public DateTimeOffset FirstExecution { get; init; }
    /// <summary>
    /// Gets or sets the last execution.
    /// </summary>
    /// <value>The last execution.</value>
    public DateTimeOffset LastExecution { get; set; }
    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    /// <value>The average execution time.</value>


    public TimeSpan AverageExecutionTime

        => TotalExecutions > 0 ? TimeSpan.FromTicks(TotalExecutionTime.Ticks / TotalExecutions) : TimeSpan.Zero;
    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>


    public double SuccessRate

        => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0.0;
}
