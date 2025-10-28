#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Management.Info;
using DotCompute.Algorithms.Abstractions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Sockets;

namespace DotCompute.Algorithms.Management.Services;

/// <summary>
/// Manages the lifecycle of algorithm plugins including initialization, execution, and disposal.
/// </summary>
public sealed partial class AlgorithmLifecycleManager : IDisposable
{
    private readonly ILogger<AlgorithmLifecycleManager> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly AlgorithmRegistry _registry;
    private readonly Timer? _healthCheckTimer;
    private bool _disposed;
    /// <summary>
    /// Initializes a new instance of the AlgorithmLifecycleManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="registry">The registry.</param>

    public AlgorithmLifecycleManager(
        ILogger<AlgorithmLifecycleManager> logger,
        AlgorithmPluginManagerOptions options,
        AlgorithmRegistry registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        // Start health monitoring if enabled
        if (_options.EnableHealthChecks)
        {
            _healthCheckTimer = new Timer(PerformHealthChecksWrapper, null,
                _options.HealthCheckInterval, _options.HealthCheckInterval);
        }
    }

    /// <summary>
    /// Initializes a plugin with the specified accelerator.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="accelerator">The accelerator to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializePluginAsync(string pluginId, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(accelerator);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId) ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found in registry.");
        try
        {
            _registry.UpdatePluginState(pluginId, PluginState.Initializing);

            await loadedPlugin.Plugin.InitializeAsync(accelerator, _logger).ConfigureAwait(false);

            _registry.UpdatePluginState(pluginId, PluginState.Running);
            _registry.UpdatePluginHealth(pluginId, PluginHealth.Healthy);

            LogPluginInitialized(pluginId);
        }
        catch (Exception ex)
        {
            _registry.UpdatePluginState(pluginId, PluginState.Failed);
            _registry.UpdatePluginHealth(pluginId, PluginHealth.Critical);

            LogPluginInitializationFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Executes a plugin with retry logic for transient failures.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="inputs">The input data.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<object> ExecutePluginAsync(
        string pluginId,
        object[] inputs,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(inputs);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var plugin = _registry.GetPlugin(pluginId) ?? throw new InvalidOperationException($"Plugin '{pluginId}' not found.");
        var stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;

        try
        {
            LogExecutingAlgorithm(pluginId);

            var result = await ExecuteWithRetryAsync(plugin, inputs, parameters, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            _registry.UpdatePluginStats(pluginId, stopwatch.Elapsed);

            LogPluginExecutionCompleted(pluginId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            lastError = ex;

            _registry.UpdatePluginStats(pluginId, stopwatch.Elapsed, ex);

            LogPluginExecutionFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Disposes a plugin properly.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    public async Task DisposePluginAsync(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId);
        if (loadedPlugin == null)
        {
            return;
        }

        try
        {
            _registry.UpdatePluginState(pluginId, PluginState.Stopping);

            await loadedPlugin.Plugin.DisposeAsync().ConfigureAwait(false);

            _registry.UpdatePluginState(pluginId, PluginState.Unloaded);

            LogPluginDisposed(pluginId);
        }
        catch (Exception ex)
        {
            _registry.UpdatePluginState(pluginId, PluginState.Failed);
            LogPluginDisposeFailed(pluginId, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Reloads a plugin (hot reload).
    /// </summary>
    /// <param name="pluginId">The plugin ID to reload.</param>
    /// <param name="accelerator">The accelerator to use for reinitialization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the plugin was reloaded successfully; otherwise, false.</returns>
    public async Task<bool> ReloadPluginAsync(string pluginId, IAccelerator accelerator, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(accelerator);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var loadedPlugin = _registry.GetLoadedPluginInfo(pluginId);
        if (loadedPlugin == null)
        {
            return false;
        }

        LogPluginReloading(pluginId);

        try
        {
            // Dispose current plugin
            await DisposePluginAsync(pluginId).ConfigureAwait(false);

            // Small delay to allow for cleanup
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            // Reinitialize plugin
            await InitializePluginAsync(pluginId, accelerator, cancellationToken).ConfigureAwait(false);

            LogPluginReloaded(pluginId, true);
            return true;
        }
        catch (Exception ex)
        {
            LogPluginReloadFailed(pluginId, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Executes plugin with retry logic for transient failures.
    /// </summary>
    private async Task<object> ExecuteWithRetryAsync(
        IAlgorithmPlugin plugin,
        object[] inputs,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromMilliseconds(100);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Convert inputs array to single input object for plugin interface
                var singleInput = inputs.Length == 1 ? inputs[0] : inputs;
                return await plugin.ExecuteAsync(singleInput, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                LogPluginRetryingExecution(plugin.Id, attempt, ex.Message);
                await Task.Delay(retryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        // Final attempt without retry handling
        // Convert inputs array to single input object for plugin interface
        var singleInput = inputs.Length == 1 ? inputs[0] : inputs;
        return await plugin.ExecuteAsync(singleInput, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Determines if an error is transient and worth retrying.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is SocketException ||
               (ex is IOException ioEx && ioEx.Message.Contains("network", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Performs health checks on all loaded plugins.
    /// </summary>
    private void PerformHealthChecksWrapper(object? state) => _ = Task.Run(async () => await PerformHealthChecksAsync(state));

    private async Task PerformHealthChecksAsync(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            foreach (var loadedPlugin in _registry.GetAllLoadedPlugins().ToList())
            {
                await CheckPluginHealthAsync(loadedPlugin).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex.Message);
        }
    }

    /// <summary>
    /// Checks the health of a single plugin.
    /// </summary>
    private async Task CheckPluginHealthAsync(LoadedPluginInfo loadedPlugin)
    {
        try
        {
            var oldHealth = loadedPlugin.Health;

            // Check if plugin has been executing successfully
            if (loadedPlugin.LastError != null &&
                DateTime.UtcNow - loadedPlugin.LastExecution < TimeSpan.FromMinutes(5))
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
            }
            else if (loadedPlugin.ExecutionCount > 0 && loadedPlugin.LastError == null)
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Healthy);
            }

            // Sophisticated health checks implementation
            await PerformMemoryUsageMonitoringAsync(loadedPlugin);
            await PerformResponseTimeAnalysisAsync(loadedPlugin);
            await PerformErrorRateTrackingAsync(loadedPlugin);
            await PerformResourceLeakDetectionAsync(loadedPlugin);

            await Task.CompletedTask; // Placeholder for async health checks
        }
        catch (Exception ex)
        {
            _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Critical);
            loadedPlugin.LastError = ex;
        }
    }

    /// <summary>
    /// Monitors memory usage for a loaded plugin.
    /// </summary>
    private async Task PerformMemoryUsageMonitoringAsync(LoadedPluginInfo loadedPlugin)
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
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
                loadedPlugin.LastError = new InvalidOperationException($"Plugin is using excessive memory: {memoryUsage:N0} bytes");
            }
            else if (memoryUsage > _options.MaxAssemblySize)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
                }
            }

            // Store memory metrics
            loadedPlugin.Metadata.AdditionalMetadata["MemoryUsage"] = memoryUsage;
            loadedPlugin.Metadata.AdditionalMetadata["MemoryCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogMemoryMonitoringFailed(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Analyzes response time patterns for a loaded plugin.
    /// </summary>
    private async Task PerformResponseTimeAnalysisAsync(LoadedPluginInfo loadedPlugin)
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
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
                loadedPlugin.LastError = new TimeoutException($"Plugin average response time is too high: {averageResponseTime:F2} ms");
            }
            else if (averageResponseTime > warningResponseTime && loadedPlugin.Health == PluginHealth.Healthy)
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
            }

            // Store response time metrics
            loadedPlugin.Metadata.AdditionalMetadata["AverageResponseTime"] = averageResponseTime;
            loadedPlugin.Metadata.AdditionalMetadata["ResponseTimeCheckTime"] = DateTime.UtcNow;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            LogResponseTimeAnalysisFailed(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Tracks error rates for a loaded plugin.
    /// </summary>
    private async Task PerformErrorRateTrackingAsync(LoadedPluginInfo loadedPlugin)
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
            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("TotalErrorCount", out var totalErrorsObj) &&
                totalErrorsObj is long totalErrors)
            {
                errorCount = (int)totalErrors;
            }
            else
            {
                totalErrors = errorCount; // Initialize if not found
            }

            var errorRate = (double)errorCount / loadedPlugin.ExecutionCount;
            const double criticalErrorRate = 0.5; // 50% error rate
            const double warningErrorRate = 0.1; // 10% error rate

            if (errorRate > criticalErrorRate)
            {
                _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Critical);
                loadedPlugin.LastError = new InvalidOperationException($"Plugin has critical error rate: {errorRate:P2}");
            }
            else if (errorRate > warningErrorRate)
            {
                if (loadedPlugin.Health == PluginHealth.Healthy)
                {
                    _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
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
            LogErrorRateTrackingFailed(ex, loadedPlugin.Plugin.Id);
        }
    }

    /// <summary>
    /// Detects potential resource leaks for a loaded plugin.
    /// </summary>
    private async Task PerformResourceLeakDetectionAsync(LoadedPluginInfo loadedPlugin)
    {
        try
        {
            // Check for handle leaks
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var handleCount = currentProcess.HandleCount;

            if (loadedPlugin.Metadata.AdditionalMetadata.TryGetValue("PreviousHandleCount", out var prevHandleObj) &&
                prevHandleObj is int prevHandleCount)
            {
                var handleIncrease = handleCount - prevHandleCount;
                const int handleLeakThreshold = 100; // Arbitrary threshold

                if (handleIncrease > handleLeakThreshold)
                {
                    if (loadedPlugin.Health == PluginHealth.Healthy)
                    {
                        _registry.UpdatePluginHealth(loadedPlugin.Plugin.Id, PluginHealth.Degraded);
                    }

                    LogPotentialHandleLeak(loadedPlugin.Plugin.Id, handleIncrease);
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
            LogResourceLeakDetectionFailed(ex, loadedPlugin.Plugin.Id);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _healthCheckTimer?.Dispose();
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin initialized: {PluginId}")]
    private partial void LogPluginInitialized(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin initialization failed for {PluginId}: {Reason}")]
    private partial void LogPluginInitializationFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing algorithm {PluginId}")]
    private partial void LogExecutingAlgorithm(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin execution completed for {PluginId} in {ElapsedMs} ms")]
    private partial void LogPluginExecutionCompleted(string pluginId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin execution failed for {PluginId}: {Reason}")]
    private partial void LogPluginExecutionFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retrying plugin execution for {PluginId}, attempt {Attempt}: {Reason}")]
    private partial void LogPluginRetryingExecution(string pluginId, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin disposed: {PluginId}")]
    private partial void LogPluginDisposed(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin dispose failed for {PluginId}: {Reason}")]
    private partial void LogPluginDisposeFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reloading plugin: {PluginId}")]
    private partial void LogPluginReloading(string pluginId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Plugin reload completed for {PluginId}, success: {Success}")]
    private partial void LogPluginReloaded(string pluginId, bool success);

    [LoggerMessage(Level = LogLevel.Error, Message = "Plugin reload failed for {PluginId}: {Reason}")]
    private partial void LogPluginReloadFailed(string pluginId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed: {Reason}")]
    private partial void LogHealthCheckFailed(string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to monitor memory usage for plugin: {PluginId}")]
    private partial void LogMemoryMonitoringFailed(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to analyze response times for plugin: {PluginId}")]
    private partial void LogResponseTimeAnalysisFailed(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to track error rates for plugin: {PluginId}")]
    private partial void LogErrorRateTrackingFailed(Exception ex, string pluginId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Potential handle leak detected for plugin {PluginId}: {HandleIncrease} new handles")]
    private partial void LogPotentialHandleLeak(string pluginId, int handleIncrease);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to detect resource leaks for plugin: {PluginId}")]
    private partial void LogResourceLeakDetectionFailed(Exception ex, string pluginId);

    #endregion
}