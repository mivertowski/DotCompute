
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Logging;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Core;

/// <summary>
/// Service responsible for monitoring plugin health and performance.
/// </summary>
public sealed partial class HealthMonitor : IHealthMonitor, IDisposable
{
    private readonly ILogger<HealthMonitor> _logger;
    private readonly PluginLifecycleManager _lifecycleManager;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="lifecycleManager">The plugin lifecycle manager.</param>
    /// <param name="options">Configuration options.</param>
    public HealthMonitor(
        ILogger<HealthMonitor> logger,
        PluginLifecycleManager lifecycleManager,
        AlgorithmPluginManagerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        // Initialize health check timer if enabled
        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(OnHealthCheckTimerWrapper, null,
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
    }

    /// <inheritdoc/>
    public void StartHealthMonitoring()
    {
        if (_options.EnableHealthChecks && _healthCheckTimer != null)
        {
            _ = _healthCheckTimer.Change(_options.HealthCheckInterval, _options.HealthCheckInterval);
            _logger.LogInfoMessage("Health monitoring started with interval: {_options.HealthCheckInterval}");
        }
    }

    /// <inheritdoc/>
    public void StopHealthMonitoring()
    {
        _ = (_healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite));
        _logger.LogInfoMessage("Health monitoring stopped");
    }

    /// <inheritdoc/>
    public async Task PerformHealthChecksAsync()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var loadedPlugins = _lifecycleManager.GetAllLoadedPlugins();
            foreach (var lifecyclePlugin in loadedPlugins)
            {
                // Convert PluginLifecycleManager.LoadedPlugin to AlgorithmPluginRegistry.LoadedPlugin
                var registryPlugin = new AlgorithmPluginRegistry.LoadedPlugin
                {
                    Plugin = lifecyclePlugin.Plugin,
                    LoadContext = lifecyclePlugin.LoadContext,
                    Assembly = lifecyclePlugin.Assembly,
                    Metadata = lifecyclePlugin.Metadata,
                    LoadTime = lifecyclePlugin.LoadTime,
                    State = lifecyclePlugin.State,
                    Health = lifecyclePlugin.Health,
                    ExecutionCount = lifecyclePlugin.ExecutionCount,
                    LastExecution = lifecyclePlugin.LastExecution,
                    TotalExecutionTime = lifecyclePlugin.TotalExecutionTime,
                    LastError = lifecyclePlugin.LastError
                };
                await CheckPluginHealthInternalAsync(registryPlugin).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task CheckPluginHealthAsync(string pluginId)
    {
        var pluginInfo = _lifecycleManager.GetLoadedPluginInfo(pluginId);
        if (pluginInfo == null)
        {
            _logger.LogWarningMessage("Plugin {pluginId} not found for health check");
            return;
        }

        var loadedPlugins = _lifecycleManager.GetAllLoadedPlugins();
        var lifecyclePlugin = loadedPlugins.FirstOrDefault(lp => lp.Plugin.Id == pluginId);
        if (lifecyclePlugin != null)
        {
            // Convert PluginLifecycleManager.LoadedPlugin to AlgorithmPluginRegistry.LoadedPlugin
            var registryPlugin = new AlgorithmPluginRegistry.LoadedPlugin
            {
                Plugin = lifecyclePlugin.Plugin,
                LoadContext = lifecyclePlugin.LoadContext,
                Assembly = lifecyclePlugin.Assembly,
                Metadata = lifecyclePlugin.Metadata,
                LoadTime = lifecyclePlugin.LoadTime,
                State = lifecyclePlugin.State,
                Health = lifecyclePlugin.Health,
                ExecutionCount = lifecyclePlugin.ExecutionCount,
                LastExecution = lifecyclePlugin.LastExecution,
                TotalExecutionTime = lifecyclePlugin.TotalExecutionTime,
                LastError = lifecyclePlugin.LastError
            };
            await CheckPluginHealthInternalAsync(registryPlugin).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Timer callback wrapper for periodic health checks.
    /// </summary>
    private void OnHealthCheckTimerWrapper(object? state) => _ = Task.Run(async () => await OnHealthCheckTimerAsync(state));

    /// <summary>
    /// Timer callback for periodic health checks.
    /// </summary>
    private async Task OnHealthCheckTimerAsync(object? state) => await PerformHealthChecksAsync().ConfigureAwait(false);

    /// <summary>
    /// Checks the health of a single plugin.
    /// </summary>
    private async Task CheckPluginHealthInternalAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            var oldHealth = loadedPlugin.Health;

            // Check if plugin has been executing successfully
            if (loadedPlugin.LastError != null &&
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }
            else if (loadedPlugin.ExecutionCount > 0 && loadedPlugin.LastError == null)
            {
                loadedPlugin.Health = PluginHealth.Healthy;
            }

            // Perform sophisticated health checks
            await PerformMemoryUsageMonitoringAsync(loadedPlugin);
            await PerformResponseTimeAnalysisAsync(loadedPlugin);
            await PerformErrorRateTrackingAsync(loadedPlugin);
            await PerformResourceLeakDetectionAsync(loadedPlugin);

            var newHealth = loadedPlugin.Health;
            if (oldHealth != newHealth)
            {
                LogPluginHealthChanged(loadedPlugin.Plugin.Id, oldHealth, newHealth);
            }
        }
        catch (Exception ex)
        {
            loadedPlugin.Health = PluginHealth.Critical;
            loadedPlugin.LastError = ex;
        }
    }

    /// <summary>
    /// Monitors memory usage for a loaded plugin.
    /// </summary>
    private async Task PerformMemoryUsageMonitoringAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            // Get memory usage for the plugin's load context
            var memoryBefore = GC.GetTotalMemory(false);

            // Force garbage collection to get more accurate reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsage = memoryAfter - memoryBefore;

            // Check if memory usage is excessive
            if (memoryUsage > _options.MaxAssemblySize * 2) // More than 2x the assembly size
            {
                loadedPlugin.Health = PluginHealth.Degraded;
                loadedPlugin.LastError = new InvalidOperationException($"Plugin is using excessive memory: {memoryUsage:N0} bytes");
            }
            else if (memoryUsage > _options.MaxAssemblySize)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    loadedPlugin.Health = PluginHealth.Degraded;
                }
            }

            // Store memory metrics
            loadedPlugin.Metadata.AdditionalMetadata["MemoryUsage"] = memoryUsage;
            loadedPlugin.Metadata.AdditionalMetadata["MemoryCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogFailedToMonitorMemoryUsage(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Analyzes response time patterns for a loaded plugin.
    /// </summary>
    private async Task PerformResponseTimeAnalysisAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return; // No executions to analyze
            }

            var averageResponseTime = loadedPlugin.TotalExecutionTime.TotalMilliseconds / loadedPlugin.ExecutionCount;
            const double maxAcceptableResponseTime = 30000; // 30 seconds
            const double warningResponseTime = 10000; // 10 seconds

            if (averageResponseTime > maxAcceptableResponseTime)
            {
                loadedPlugin.Health = PluginHealth.Degraded;
                loadedPlugin.LastError = new TimeoutException($"Plugin average response time is too high: {averageResponseTime:F2} ms");
            }
            else if (averageResponseTime > warningResponseTime && loadedPlugin.Health == PluginHealth.Healthy)
            {
                loadedPlugin.Health = PluginHealth.Degraded;
            }

            // Check for response time degradation over time
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousAverageResponseTime", out var prevTimeObj))
            {
                if (prevTimeObj is double prevTime)
                {
                    var degradationThreshold = 1.5; // 50% increase
                    if (averageResponseTime > prevTime * degradationThreshold)
                    {
                        if (loadedPlugin.Health == PluginHealth.Healthy)
                        {
                            loadedPlugin.Health = PluginHealth.Degraded;
                        }
                    }
                }
            }

            // Store response time metrics
            loadedPlugin.Metadata.AdditionalMetadata["AverageResponseTime"] = averageResponseTime;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousAverageResponseTime"] = averageResponseTime;
            loadedPlugin.Metadata.AdditionalMetadata["ResponseTimeCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogFailedToAnalyzeResponseTimes(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Tracks error rates for a loaded plugin.
    /// </summary>
    private async Task PerformErrorRateTrackingAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            if (loadedPlugin.ExecutionCount == 0)
            {
                return; // No executions to track
            }

            // Calculate error rate based on recent errors
            var errorCount = loadedPlugin.LastError != null ? 1 : 0;

            // Get historical error count if available
            var totalErrors = 0L;
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("TotalErrorCount", out var totalErrorsObj))
            {
                if (totalErrorsObj is long totalErrorsValue)
                {
                    totalErrors = totalErrorsValue;
                    errorCount = (int)totalErrors;
                }
            }

            var errorRate = (double)totalErrors / loadedPlugin.ExecutionCount;
            const double criticalErrorRate = 0.5; // 50% error rate
            const double warningErrorRate = 0.1; // 10% error rate

            if (errorRate > criticalErrorRate)
            {
                loadedPlugin.Health = PluginHealth.Critical;
                loadedPlugin.LastError = new InvalidOperationException($"Plugin has critical error rate: {errorRate:P2}");
            }
            else if (errorRate > warningErrorRate)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    loadedPlugin.Health = PluginHealth.Degraded;
                }
            }

            // Store error rate metrics
            loadedPlugin.Metadata.AdditionalMetadata["ErrorRate"] = errorRate;
            loadedPlugin.Metadata.AdditionalMetadata["TotalErrorCount"] = errorCount;
            loadedPlugin.Metadata.AdditionalMetadata["ErrorRateCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogFailedToTrackErrorRates(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Detects potential resource leaks for a loaded plugin.
    /// </summary>
    private async Task PerformResourceLeakDetectionAsync(AlgorithmPluginRegistry.LoadedPlugin loadedPlugin)
    {
        try
        {
            // Check for handle leaks
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var handleCount = currentProcess.HandleCount;

            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousHandleCount", out var prevHandleObj))
            {
                if (prevHandleObj is int prevHandleCount)
                {
                    var handleIncrease = handleCount - prevHandleCount;
                    const int handleLeakThreshold = 100; // Arbitrary threshold

                    if (handleIncrease > handleLeakThreshold)
                    {
                        if (loadedPlugin.Health == PluginHealth.Healthy)
                        {
                            loadedPlugin.Health = PluginHealth.Degraded;
                        }

                        _logger.LogWarningMessage($"Potential handle leak detected for plugin {loadedPlugin.Plugin.Id}: {handleIncrease} new handles");
                    }
                }
            }

            // Store resource metrics
            loadedPlugin.Metadata.AdditionalMetadata["CurrentHandleCount"] = handleCount;
            loadedPlugin.Metadata.AdditionalMetadata["PreviousHandleCount"] = handleCount;
            loadedPlugin.Metadata.AdditionalMetadata["ResourceLeakCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogFailedToDetectResourceLeaks(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Disposes of managed resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed: {Reason}")]
    private partial void LogHealthCheckFailed(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin health changed for {PluginId}: {OldHealth} -> {NewHealth}")]
    private partial void LogPluginHealthChanged(string pluginId, PluginHealth oldHealth, PluginHealth newHealth);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to monitor memory usage for plugin: {PluginId}")]
    private partial void LogFailedToMonitorMemoryUsage(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to analyze response times for plugin: {PluginId}")]
    private partial void LogFailedToAnalyzeResponseTimes(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to track error rates for plugin: {PluginId}")]
    private partial void LogFailedToTrackErrorRates(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to detect resource leaks for plugin: {PluginId}")]
    private partial void LogFailedToDetectResourceLeaks(Exception ex, string pluginId);

    #endregion
}
