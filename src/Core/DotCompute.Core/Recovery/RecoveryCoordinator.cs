// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions;
using DotCompute.Core.Recovery.Models;
using CoreRecoveryMetrics = DotCompute.Core.Recovery.Models.RecoveryMetrics;
using DotCompute.Core.Recovery.Statistics;
using DotCompute.Core.Recovery.Types;
using CompilationRecoveryContext = DotCompute.Core.Recovery.Compilation.CompilationRecoveryContext;
using CompilationFallbackResult = DotCompute.Core.Recovery.Compilation.CompilationFallbackResult;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Recovery;

/// <summary>
/// Central coordinator for all error recovery operations in DotCompute.
/// Orchestrates GPU, Memory, Compilation, Network, and Plugin recovery strategies.
/// </summary>
public sealed partial class RecoveryCoordinator : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 13301, Level = MsLogLevel.Warning, Message = "Error reporting recovery metrics")]
    private static partial void LogMetricsReportingError(ILogger logger, Exception ex);

    #endregion

    private readonly ILogger<RecoveryCoordinator> _logger;
    private readonly ConcurrentDictionary<Type, IRecoveryStrategy<object>> _strategies;
    private readonly RecoveryCoordinatorConfiguration _config;
    private readonly CoreRecoveryMetrics _globalMetrics;
    private readonly Timer _metricsReportTimer;
    private readonly SemaphoreSlim _recoveryLock;

    // Individual recovery managers

    private readonly GpuRecoveryManager _gpuRecovery;
    private readonly MemoryRecoveryStrategy _memoryRecovery;
    private readonly CompilationFallback _compilationFallback;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly PluginRecoveryManager _pluginRecovery;


    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the RecoveryCoordinator class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The config.</param>
    /// <param name="loggerFactory">The logger factory.</param>

    public RecoveryCoordinator(
        ILogger<RecoveryCoordinator> logger,
        RecoveryCoordinatorConfiguration? config = null,
        ILoggerFactory? loggerFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? RecoveryCoordinatorConfiguration.Default;
        _strategies = new ConcurrentDictionary<Type, IRecoveryStrategy<object>>();
        _globalMetrics = new CoreRecoveryMetrics();
        _recoveryLock = new SemaphoreSlim(1, 1);


        var factory = loggerFactory ?? LoggerFactory.Create(builder => builder.AddConsole());

        // Initialize recovery managers

        _gpuRecovery = new GpuRecoveryManager(factory.CreateLogger<GpuRecoveryManager>(), _config.GpuRecoveryConfig);
        _memoryRecovery = new MemoryRecoveryStrategy(factory.CreateLogger<MemoryRecoveryStrategy>(), _config.MemoryRecoveryConfig);
        _compilationFallback = new CompilationFallback(factory.CreateLogger<CompilationFallback>(), _config.CompilationFallbackConfig);
        _circuitBreaker = new CircuitBreaker(factory.CreateLogger<CircuitBreaker>(), _config.CircuitBreakerConfig);
        _pluginRecovery = new PluginRecoveryManager(factory.CreateLogger<PluginRecoveryManager>(), _config.PluginRecoveryConfig);

        // Register strategies

        RegisterDefaultStrategies();

        // Start metrics reporting

        _metricsReportTimer = new Timer(ReportMetrics, null,
            _config.MetricsReportInterval, _config.MetricsReportInterval);


        _logger.LogInfoMessage("Recovery Coordinator initialized with {_strategies.Count} recovery strategies");
    }

    /// <summary>
    /// Central recovery method that routes errors to appropriate recovery strategies
    /// </summary>
    public async Task<RecoveryResult> RecoverAsync<TContext>(
        Exception error,
        TContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default) where TContext : class
    {
        var stopwatch = Stopwatch.StartNew();
        var recoveryOptions = options ?? new RecoveryOptions();
        var contextType = typeof(TContext);


        _logger.LogWarningMessage("Recovery requested for {ContextType}: {contextType.Name, error.Message}");

        await _recoveryLock.WaitAsync(cancellationToken);


        try
        {
            // Find appropriate recovery strategy
            var strategy = FindRecoveryStrategy(error, context);
            if (strategy == null)
            {
                var result = new RecoveryResult
                {
                    Success = false,
                    Message = $"No recovery strategy found for error type {error.GetType().Name}",
                    Duration = stopwatch.Elapsed,
                    Strategy = "None"
                };


                _globalMetrics.RecordFailure(result.Duration, error);
                return result;
            }

            _logger.LogInfoMessage($"Using recovery strategy {strategy.GetType().Name} for {contextType.Name}");

            // Execute recovery with circuit breaker protection

            var recoveryResult = await _circuitBreaker.ExecuteAsync(
                $"Recovery_{strategy.GetType().Name}",
                async ct => await ((dynamic)strategy).RecoverAsync(error, context!, recoveryOptions, ct),
                cancellationToken) ?? new RecoveryResult { Success = false, Message = "Circuit breaker returned null", Strategy = strategy.GetType().Name, Duration = stopwatch.Elapsed };


            stopwatch.Stop();
            recoveryResult.Duration = stopwatch.Elapsed;


            if (recoveryResult.Success)
            {
                _globalMetrics.RecordSuccess(recoveryResult.Duration);
                _logger.LogInfoMessage($"Recovery successful using {strategy.GetType().Name} in {stopwatch.ElapsedMilliseconds}ms");
            }
            else
            {
                _globalMetrics.RecordFailure(recoveryResult.Duration, error);
                var strategyName = strategy.GetType().Name;
                _logger.LogErrorMessage($"Recovery failed using {strategyName}: {recoveryResult.Message}");
            }


            return recoveryResult!;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorMessage(ex, $"Exception during recovery coordination for {contextType.Name}");


            var result = new RecoveryResult
            {
                Success = false,
                Message = $"Recovery coordination failed: {ex.Message}",
                Exception = ex,
                Duration = stopwatch.Elapsed,
                Strategy = "CoordinatorException"
            };


            _globalMetrics.RecordFailure(result.Duration, ex);
            return result;
        }
        finally
        {
            _ = _recoveryLock.Release();
        }
    }

    /// <summary>
    /// GPU-specific recovery operations
    /// </summary>
    public async Task<RecoveryResult> RecoverGpuErrorAsync(
        Exception error,
        string deviceId,
        string? operation = null,
        CancellationToken cancellationToken = default) => await _gpuRecovery.HandleGpuErrorAsync(error, deviceId, operation, cancellationToken);

    /// <summary>
    /// Memory-specific recovery operations
    /// </summary>
    public async Task<RecoveryResult> RecoverMemoryErrorAsync(
        Exception error,
        MemoryRecoveryContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default) => await _memoryRecovery.RecoverAsync(error, context, options ?? new RecoveryOptions(), cancellationToken);

    /// <summary>
    /// Compilation-specific recovery operations
    /// </summary>
    public async Task<RecoveryResult> RecoverCompilationErrorAsync(
        Exception error,
        CompilationRecoveryContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default) => await _compilationFallback.RecoverAsync(error, context, options ?? new RecoveryOptions(), cancellationToken);

    /// <summary>
    /// Plugin-specific recovery operations
    /// </summary>
    public async Task<RecoveryResult> RecoverPluginErrorAsync(
        Exception error,
        PluginRecoveryContext context,
        RecoveryOptions? options = null,
        CancellationToken cancellationToken = default) => await _pluginRecovery.RecoverAsync(error, context, options ?? new RecoveryOptions(), cancellationToken);

    /// <summary>
    /// Network operation with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteWithCircuitBreakerAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) => await _circuitBreaker.ExecuteAsync(serviceName, operation, cancellationToken);

    /// <summary>
    /// Memory allocation with retry and recovery
    /// </summary>
    public async Task<T?> AllocateWithRecoveryAsync<T>(
        Func<T> allocateFunc,
        int maxRetries = 3,
        TimeSpan? baseDelay = null,
        CancellationToken cancellationToken = default) where T : class => await _memoryRecovery.AllocateWithRetryAsync(allocateFunc, maxRetries, baseDelay, cancellationToken);

    /// <summary>
    /// Compilation with progressive fallback
    /// </summary>
    public async Task<CompilationFallbackResult> CompileWithFallbackAsync(
        string kernelName,
        string sourceCode,
        CompilationOptions originalOptions,
        CancellationToken cancellationToken = default)
    {
        return await _compilationFallback.CompileWithProgressiveFallbackAsync(
            kernelName, sourceCode, originalOptions, cancellationToken);
    }

    /// <summary>
    /// Gets comprehensive recovery statistics
    /// </summary>
    public Statistics.RecoveryStatistics GetRecoveryStatistics()
    {
        var gpuReport = _gpuRecovery.GetDeviceHealthReport();
        var memoryPressure = _memoryRecovery.MemoryPressureInfo;
        var compilationStats = _compilationFallback.GetCompilationStatistics();
        var circuitStats = _circuitBreaker.GetStatistics();
        var pluginHealthResult = _pluginRecovery.GetHealthReport();
        var pluginHealth = new PluginHealthReport
        {
            PluginId = pluginHealthResult.Component,
            Status = pluginHealthResult.IsHealthy ? PluginHealthStatus.Healthy : PluginHealthStatus.Warning,
            Timestamp = DateTimeOffset.UtcNow
        };


        return new Statistics.RecoveryStatistics
        {
            Timestamp = DateTimeOffset.UtcNow,
            GlobalMetrics = _globalMetrics,
            GpuHealthReport = gpuReport,
            MemoryPressureInfo = memoryPressure,
            CompilationStatistics = compilationStats,
            CircuitBreakerStatistics = circuitStats,
            PluginHealthReport = pluginHealth,
            RegisteredStrategies = _strategies.Count,
            OverallSystemHealth = CalculateOverallSystemHealth(gpuReport, memoryPressure, pluginHealth)
        };
    }

    /// <summary>
    /// Performs system-wide health check and recovery
    /// </summary>
    public Task<SystemHealthResult> PerformSystemHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInfoMessage("Performing comprehensive system health check");
        var stopwatch = Stopwatch.StartNew();


        var healthResults = new List<ComponentHealthResult>();


        try
        {
            // Check GPU health
            var gpuHealth = _gpuRecovery.GetDeviceHealthReport();
            healthResults.Add(new ComponentHealthResult
            {
                Component = "GPU",
                IsHealthy = gpuHealth.OverallHealth > 0.7,
                Health = gpuHealth.OverallHealth,
                Message = $"{gpuHealth.DeviceHealth.Count} devices, {gpuHealth.ActiveKernels} active kernels"
            });

            // Check memory health

            var memoryHealth = _memoryRecovery.MemoryPressureInfo;
            healthResults.Add(new ComponentHealthResult
            {
                Component = "Memory",
                IsHealthy = memoryHealth.Level <= MemoryPressureLevel.Medium,
                Health = 1.0 - memoryHealth.PressureRatio,
                Message = $"Pressure: {memoryHealth.Level}, {memoryHealth.AvailableMemory / 1024 / 1024}MB available"
            });

            // Check compilation health

            var compilationHealth = _compilationFallback.GetCompilationStatistics();
            healthResults.Add(new ComponentHealthResult
            {
                Component = "Compilation",
                IsHealthy = compilationHealth.SuccessRate > 0.8,
                Health = compilationHealth.SuccessRate,
                Message = $"Success rate: {compilationHealth.SuccessRate:P1}, {compilationHealth.CacheSize} cached"
            });

            // Check circuit breaker health

            var circuitHealth = _circuitBreaker.GetStatistics();
            healthResults.Add(new ComponentHealthResult
            {
                Component = "Network",
                IsHealthy = circuitHealth.GlobalState == CircuitState.Closed,
                Health = circuitHealth.GlobalState == CircuitState.Closed ? 1.0 : 0.5,
                Message = $"State: {circuitHealth.GlobalState}, {circuitHealth.ActiveServices} services"
            });

            // Check plugin health

            var pluginHealth = _pluginRecovery.GetHealthReport();
            healthResults.Add(new ComponentHealthResult
            {
                Component = "Plugins",
                IsHealthy = pluginHealth.OverallHealth > 0.8,
                Health = pluginHealth.OverallHealth,
                Message = $"{pluginHealth.HealthyPlugins}/{pluginHealth.TotalPlugins} healthy"
            });


            stopwatch.Stop();


            var overallHealth = healthResults.Average(r => r.Health);
            var allHealthy = healthResults.All(r => r.IsHealthy);


            _logger.LogInfoMessage($"System health check completed in {stopwatch.ElapsedMilliseconds}ms. Overall health: {overallHealth}");


            return Task.FromResult(new SystemHealthResult
            {
                IsHealthy = allHealthy,
                OverallHealth = overallHealth,
                Duration = stopwatch.Elapsed,
                Timestamp = DateTimeOffset.UtcNow,
                ComponentResults = healthResults
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogErrorMessage(ex, "Error during system health check");


            return Task.FromResult(new SystemHealthResult
            {
                IsHealthy = false,
                OverallHealth = 0.0,
                Duration = stopwatch.Elapsed,
                Timestamp = DateTimeOffset.UtcNow,
                Error = ex.Message,
                ComponentResults = healthResults
            });
        }
    }

    /// <summary>
    /// Registers a custom recovery strategy
    /// </summary>
    public void RegisterRecoveryStrategy<TContext>(IRecoveryStrategy<TContext> strategy)
        where TContext : class
    {
        var contextType = typeof(TContext);
        _ = _strategies.TryAdd(contextType, (IRecoveryStrategy<object>)strategy);


        _logger.LogInfoMessage($"Registered recovery strategy {strategy.GetType().Name} for context {contextType.Name}");
    }

    private void RegisterDefaultStrategies()
        // Strategies are directly used rather than registered generically
        // due to their specific implementations and contexts




        => _logger.LogDebugMessage("Default recovery strategies registered");

    private IRecoveryStrategy<object>? FindRecoveryStrategy<TContext>(Exception error, TContext context)
    {
        var contextType = typeof(TContext);

        // Try exact type match first

        if (_strategies.TryGetValue(contextType, out var strategy) && strategy.CanHandle(error, context ?? new object()))
        {
            return strategy;
        }

        // Try base types and interfaces

        foreach (var kvp in _strategies)
        {
            if (kvp.Key.IsAssignableFrom(contextType) && kvp.Value.CanHandle(error, context ?? new object()))
            {
                return kvp.Value;
            }
        }


        return null;
    }

    private static double CalculateOverallSystemHealth(
        DeviceHealthReport gpuReport,
        Memory.MemoryPressureInfo memoryInfo,
        PluginHealthReport pluginReport)
    {
        var healthFactors = new List<double>
        {
            gpuReport.OverallHealth,
            memoryInfo.Level switch
            {
                MemoryPressureLevel.Low => 1.0,
                MemoryPressureLevel.Medium => 0.8,
                MemoryPressureLevel.High => 0.5,
                MemoryPressureLevel.Critical => 0.2,
                _ => 0.5
            },
            pluginReport.OverallHealth
        };


        return healthFactors.Average();
    }

    private void ReportMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var stats = GetRecoveryStatistics();


            _logger.LogInfoMessage($"Recovery Metrics - Success Rate: {stats.GlobalMetrics.SuccessRate:P1}, Avg Recovery Time: {stats.GlobalMetrics.AverageRecoveryTime.TotalMilliseconds:F1}ms");

            // Log component-specific metrics

            _logger.LogDebugMessage($"Component Health - Overall GPU Health: {stats.GpuHealthReport?.OverallHealth ?? 0:P1}");
        }
        catch (Exception ex)
        {
            LogMetricsReportingError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _metricsReportTimer?.Dispose();
            _recoveryLock?.Dispose();


            _gpuRecovery?.Dispose();
            _memoryRecovery?.Dispose();
            _compilationFallback?.Dispose();
            _circuitBreaker?.Dispose();
            _pluginRecovery?.Dispose();


            _disposed = true;
            _logger.LogInfoMessage("Recovery Coordinator disposed");
        }
    }
}



// Supporting configuration and result types would continue...
