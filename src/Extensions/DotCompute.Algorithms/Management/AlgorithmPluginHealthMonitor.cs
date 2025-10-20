// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Handles health monitoring for algorithm plugins including memory usage,
/// response time analysis, error rate tracking, and resource leak detection.
/// </summary>
public partial class AlgorithmPluginHealthMonitor(ILogger<AlgorithmPluginHealthMonitor> logger, AlgorithmPluginManagerOptions options)
{
    private readonly ILogger<AlgorithmPluginHealthMonitor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly AlgorithmPluginManagerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private Timer? _healthCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Starts periodic health monitoring for all loaded plugins.
    /// </summary>
    public void StartHealthMonitoring(Func<Task> healthCheckCallback)
    {
        if (!_options.EnableHealthChecks)
        {
            return;
        }


        _healthCheckTimer = new Timer(
            async _ => await PerformHealthChecksAsync(healthCheckCallback),
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);

        LogStartedHealthMonitoring(_options.HealthCheckInterval);
    }

    /// <summary>
    /// Stops health monitoring.
    /// </summary>
    public void StopHealthMonitoring()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        LogStoppedHealthMonitoring();
    }

    /// <summary>
    /// Performs health checks on a specific plugin.
    /// </summary>
    public async Task<PluginHealth> CheckPluginHealthAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        try
        {
            LogPerformingHealthCheck(plugin.Id);

            // TODO: Implement IHealthCheckable interface when available
            // For now, perform basic validation as health check
            await Task.CompletedTask; // Keep async for future compatibility

            // Simple health check - if plugin can validate basic input, it's healthy
            var testInput = new object[] { new() };
            if (plugin.ValidateInputs(testInput))
            {
                LogPluginPassedHealthCheck(plugin.Id);
                return PluginHealth.Healthy;
            }

            LogPluginFailedHealthCheck(plugin.Id);
            return PluginHealth.Degraded;
        }
        catch (Exception ex)
        {
            LogHealthCheckError(ex, plugin.Id);
            return PluginHealth.Critical;
        }
    }

    /// <summary>
    /// Performs memory usage monitoring for a plugin.
    /// </summary>
    public async Task<MemoryUsageInfo> PerformMemoryUsageMonitoringAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        LogMonitoringMemoryUsage(plugin.Id);

        var initialMemory = GC.GetTotalMemory(false);

        // TODO: Implement IMemoryMonitorable interface when available
        // For now, use basic memory measurement
        await Task.CompletedTask; // Keep async for future compatibility

        var currentMemory = GC.GetTotalMemory(false);
        var memoryDelta = currentMemory - initialMemory;

        return new MemoryUsageInfo
        {
            TotalMemoryBytes = currentMemory,
            DeltaMemoryBytes = memoryDelta,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Performs response time analysis for a plugin.
    /// </summary>
    public async Task<ResponseTimeInfo> PerformResponseTimeAnalysisAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        LogAnalyzingResponseTime(plugin.Id);

        // TODO: Implement IPerformanceMonitorable interface when available
        // For now, return default values
        await Task.CompletedTask; // Keep async for future compatibility

        return new ResponseTimeInfo
        {
            AverageResponseTimeMs = 0,
            P95ResponseTimeMs = 0,
            P99ResponseTimeMs = 0,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Performs error rate tracking for a plugin.
    /// </summary>
    public async Task<ErrorRateInfo> PerformErrorRateTrackingAsync(IAlgorithmPlugin plugin, long executionCount, Exception? lastError, CancellationToken cancellationToken = default)
    {
        LogTrackingErrorRate(plugin.Id);

        // TODO: Implement IErrorMonitorable interface when available
        // For now, calculate based on provided parameters
        await Task.CompletedTask; // Keep async for future compatibility

        return new ErrorRateInfo
        {
            TotalExecutions = executionCount,
            TotalErrors = lastError != null ? 1 : 0,
            ErrorRate = lastError != null && executionCount > 0 ? 1.0 / executionCount : 0,
            LastError = lastError,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Performs resource leak detection for a plugin.
    /// </summary>
    public async Task<ResourceLeakInfo> PerformResourceLeakDetectionAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        LogDetectingResourceLeaks(plugin.Id);

        // TODO: Implement IResourceMonitorable interface when available
        // For now, return no leaks detected
        await Task.CompletedTask; // Keep async for future compatibility

        return new ResourceLeakInfo
        {
            HasLeaks = false,
            LeakDescription = null,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task PerformHealthChecksAsync(Func<Task> healthCheckCallback)
    {
        try
        {
            await healthCheckCallback();
        }
        catch (Exception ex)
        {
            LogPeriodicHealthCheckError(ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        StopHealthMonitoring();
        _disposed = true;
    }

    #region LoggerMessage Delegates

    [LoggerMessage(Level = LogLevel.Information, Message = "Started plugin health monitoring with interval: {Interval}")]
    private partial void LogStartedHealthMonitoring(TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped plugin health monitoring")]
    private partial void LogStoppedHealthMonitoring();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Performing health check for plugin: {PluginId}")]
    private partial void LogPerformingHealthCheck(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin {PluginId} passed basic health check")]
    private partial void LogPluginPassedHealthCheck(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Plugin {PluginId} failed basic health check")]
    private partial void LogPluginFailedHealthCheck(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error performing health check for plugin: {PluginId}")]
    private partial void LogHealthCheckError(Exception exception, string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Monitoring memory usage for plugin: {PluginId}")]
    private partial void LogMonitoringMemoryUsage(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Analyzing response time for plugin: {PluginId}")]
    private partial void LogAnalyzingResponseTime(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tracking error rate for plugin: {PluginId}")]
    private partial void LogTrackingErrorRate(string pluginId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Detecting resource leaks for plugin: {PluginId}")]
    private partial void LogDetectingResourceLeaks(string pluginId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error during periodic health check")]
    private partial void LogPeriodicHealthCheckError(Exception exception);

    #endregion
}
/// <summary>
/// A class that represents memory usage info.
/// </summary>

// Health monitoring data structures
public record MemoryUsageInfo
{
    /// <summary>
    /// Gets or sets the total memory bytes.
    /// </summary>
    /// <value>The total memory bytes.</value>
    public long TotalMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the delta memory bytes.
    /// </summary>
    /// <value>The delta memory bytes.</value>
    public long DeltaMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; init; }
}
/// <summary>
/// A class that represents response time info.
/// </summary>

public record ResponseTimeInfo
{
    /// <summary>
    /// Gets or sets the average response time ms.
    /// </summary>
    /// <value>The average response time ms.</value>
    public double AverageResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the p95 response time ms.
    /// </summary>
    /// <value>The p95 response time ms.</value>
    public double P95ResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the p99 response time ms.
    /// </summary>
    /// <value>The p99 response time ms.</value>
    public double P99ResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; init; }
}
/// <summary>
/// A class that represents error rate info.
/// </summary>

public record ErrorRateInfo
{
    /// <summary>
    /// Gets or sets the total executions.
    /// </summary>
    /// <value>The total executions.</value>
    public long TotalExecutions { get; init; }
    /// <summary>
    /// Gets or sets the total errors.
    /// </summary>
    /// <value>The total errors.</value>
    public long TotalErrors { get; init; }
    /// <summary>
    /// Gets or sets the error rate.
    /// </summary>
    /// <value>The error rate.</value>
    public double ErrorRate { get; init; }
    /// <summary>
    /// Gets or sets the last error.
    /// </summary>
    /// <value>The last error.</value>
    public Exception? LastError { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; init; }
}
/// <summary>
/// A class that represents resource leak info.
/// </summary>

public record ResourceLeakInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether leaks.
    /// </summary>
    /// <value>The has leaks.</value>
    public bool HasLeaks { get; init; }
    /// <summary>
    /// Gets or sets the leak description.
    /// </summary>
    /// <value>The leak description.</value>
    public string? LeakDescription { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTime Timestamp { get; init; }
}