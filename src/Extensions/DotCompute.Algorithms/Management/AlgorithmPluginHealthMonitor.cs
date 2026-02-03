// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions.Health;
using DotCompute.Algorithms.Abstractions;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Types.Enums;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Handles health monitoring for algorithm plugins including memory usage,
/// response time analysis, error rate tracking, and resource leak detection.
/// </summary>
public partial class AlgorithmPluginHealthMonitor(ILogger<AlgorithmPluginHealthMonitor> logger, AlgorithmPluginManagerOptions options) : IDisposable
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


#pragma warning disable VSTHRD101 // Avoid unsupported async delegates - Timer callbacks fire-and-forget by design
        _healthCheckTimer = new Timer(
            async _ => await PerformHealthChecksAsync(healthCheckCallback),
            null,
            _options.HealthCheckInterval,
            _options.HealthCheckInterval);
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

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

            // Use IHealthCheckable interface if available
            if (plugin is IHealthCheckable healthCheckable)
            {
                var result = await healthCheckable.CheckHealthAsync(cancellationToken);
                var health = result.Status switch
                {
                    HealthStatus.Healthy or HealthStatus.Optimal => PluginHealth.Healthy,
                    HealthStatus.Degraded => PluginHealth.Degraded,
                    HealthStatus.Critical => PluginHealth.Critical,
                    _ => PluginHealth.Unknown
                };

                if (health == PluginHealth.Healthy)
                {
                    LogPluginPassedHealthCheck(plugin.Id);
                }
                else
                {
                    LogPluginFailedHealthCheck(plugin.Id);
                }

                return health;
            }

            // Fallback: perform basic validation as health check
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

        // Use IMemoryMonitorable interface if available
        if (plugin is IMemoryMonitorable memoryMonitorable)
        {
            var result = await memoryMonitorable.GetMemoryUsageAsync(cancellationToken);
            return new MemoryUsageInfo
            {
                TotalMemoryBytes = result.CurrentBytes,
                DeltaMemoryBytes = result.CurrentBytes - result.PeakBytes,
                PeakMemoryBytes = result.PeakBytes,
                Timestamp = DateTime.UtcNow
            };
        }

        // Fallback: use basic memory measurement
        var currentMemory = GC.GetTotalMemory(false);

        return new MemoryUsageInfo
        {
            TotalMemoryBytes = currentMemory,
            DeltaMemoryBytes = 0,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Performs response time analysis for a plugin.
    /// </summary>
    public async Task<ResponseTimeInfo> PerformResponseTimeAnalysisAsync(IAlgorithmPlugin plugin, CancellationToken cancellationToken = default)
    {
        LogAnalyzingResponseTime(plugin.Id);

        // Use IPerformanceMonitorable interface if available
        if (plugin is IPerformanceMonitorable performanceMonitorable)
        {
            var metrics = await performanceMonitorable.GetPerformanceMetricsAsync(cancellationToken);
            return new ResponseTimeInfo
            {
                AverageResponseTimeMs = metrics.AverageDurationMs,
                P95ResponseTimeMs = metrics.P95DurationMs,
                P99ResponseTimeMs = metrics.P99DurationMs,
                MinResponseTimeMs = metrics.MinDurationMs,
                MaxResponseTimeMs = metrics.MaxDurationMs,
                TotalOperations = metrics.TotalOperations,
                Timestamp = DateTime.UtcNow
            };
        }

        // Fallback: return default values (no historical data available)
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

        // Use IErrorMonitorable interface if available
        if (plugin is IErrorMonitorable errorMonitorable)
        {
            var stats = await errorMonitorable.GetErrorStatisticsAsync(cancellationToken);
            return new ErrorRateInfo
            {
                TotalExecutions = stats.TotalOperations,
                TotalErrors = stats.TotalErrors,
                ErrorRate = stats.ErrorRate,
                LastError = stats.LastError,
                Timestamp = DateTime.UtcNow
            };
        }

        // Fallback: calculate based on provided parameters
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

        // Use IResourceMonitorable interface if available
        if (plugin is IResourceMonitorable resourceMonitorable)
        {
            var result = await resourceMonitorable.CheckForLeaksAsync(cancellationToken);
            return new ResourceLeakInfo
            {
                HasLeaks = result.HasPotentialLeaks,
                LeakDescription = result.LeakDescriptions is { Count: > 0 } descriptions ? descriptions[0] : null,
                LeakCount = result.LeakedResourceCount,
                TotalAllocated = result.TotalAllocated,
                TotalReleased = result.TotalReleased,
                Timestamp = DateTime.UtcNow
            };
        }

        // Fallback: return no leaks detected (cannot detect without interface)
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by the health monitor.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            StopHealthMonitoring();
        }

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
    public long TotalMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the delta memory bytes.
    /// </summary>
    public long DeltaMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the peak memory bytes.
    /// </summary>
    public long PeakMemoryBytes { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
/// <summary>
/// A class that represents response time info.
/// </summary>

public record ResponseTimeInfo
{
    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the 95th percentile response time in milliseconds.
    /// </summary>
    public double P95ResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the 99th percentile response time in milliseconds.
    /// </summary>
    public double P99ResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the minimum response time in milliseconds.
    /// </summary>
    public double MinResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the maximum response time in milliseconds.
    /// </summary>
    public double MaxResponseTimeMs { get; init; }
    /// <summary>
    /// Gets or sets the total number of operations.
    /// </summary>
    public long TotalOperations { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
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
    /// Gets or sets a value indicating whether potential leaks were detected.
    /// </summary>
    public bool HasLeaks { get; init; }
    /// <summary>
    /// Gets or sets the leak description.
    /// </summary>
    public string? LeakDescription { get; init; }
    /// <summary>
    /// Gets or sets the number of potentially leaked resources.
    /// </summary>
    public int LeakCount { get; init; }
    /// <summary>
    /// Gets or sets the total number of allocated resources.
    /// </summary>
    public int TotalAllocated { get; init; }
    /// <summary>
    /// Gets or sets the total number of released resources.
    /// </summary>
    public int TotalReleased { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
