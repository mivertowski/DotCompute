// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.Types;
using DotCompute.Algorithms.Management.Configuration;
using DotCompute.Algorithms.Management.Core;
using DotCompute.Algorithms.Types.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Management.Services;

/// <summary>
/// Provides comprehensive performance monitoring and metrics collection for algorithm plugins.
/// Tracks execution statistics, performance trends, and resource usage patterns.
/// </summary>
public sealed class AlgorithmPluginMetrics : IDisposable
{
    private readonly ILogger<AlgorithmPluginMetrics> _logger;
    private readonly AlgorithmPluginManagerOptions _options;
    private readonly AlgorithmPluginRegistry _registry;
    private readonly ConcurrentDictionary<string, PluginMetricsData> _metricsData = new();
    private readonly Timer _metricsCollectionTimer;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private bool _disposed;

    /// <summary>
    /// Represents comprehensive metrics data for a plugin.
    /// </summary>
    private sealed class PluginMetricsData
    {
        public string PluginId { get; init; } = string.Empty;
        public long TotalExecutions { get; set; }
        public long SuccessfulExecutions { get; set; }
        public long FailedExecutions { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public TimeSpan MinExecutionTime { get; set; } = TimeSpan.MaxValue;
        public TimeSpan MaxExecutionTime { get; set; }
        public Queue<ExecutionRecord> RecentExecutions { get; } = new();
        public Dictionary<string, long> ErrorCounts { get; } = new();
        public long TotalMemoryAllocated { get; set; }
        public double AverageCpuUsage { get; set; }
        public DateTime FirstExecution { get; set; }
        public DateTime LastExecution { get; set; }
        public Queue<PerformanceSnapshot> PerformanceHistory { get; } = new();
    }

    /// <summary>
    /// Represents a single execution record.
    /// </summary>
    private sealed class ExecutionRecord
    {
        public DateTime Timestamp { get; init; }
        public TimeSpan Duration { get; init; }
        public bool Success { get; init; }
        public string? ErrorType { get; init; }
        public long MemoryUsed { get; init; }
        public double CpuUsage { get; init; }
    }

    /// <summary>
    /// Represents a performance snapshot at a point in time.
    /// </summary>
    private sealed class PerformanceSnapshot
    {
        public DateTime Timestamp { get; init; }
        public double ThroughputPerSecond { get; init; }
        public TimeSpan AverageExecutionTime { get; init; }
        public double ErrorRate { get; init; }
        public long MemoryUsage { get; init; }
        public double CpuUsage { get; init; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmPluginMetrics"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">Configuration options.</param>
    /// <param name="registry">The plugin registry.</param>
    public AlgorithmPluginMetrics(
        ILogger<AlgorithmPluginMetrics> logger,
        AlgorithmPluginManagerOptions options,
        AlgorithmPluginRegistry registry)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        // Initialize performance counters (platform-specific)
        try
        {
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize performance counters");
        }

        // Setup metrics collection timer - collect every minute
        _metricsCollectionTimer = new Timer(CollectMetrics, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Records the execution of a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="duration">The execution duration.</param>
    /// <param name="success">Whether the execution was successful.</param>
    /// <param name="error">Optional error that occurred.</param>
    /// <param name="memoryUsed">Memory used during execution.</param>
    public void RecordExecution(string pluginId, TimeSpan duration, bool success, Exception? error = null, long memoryUsed = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var metricsData = _metricsData.GetOrAdd(pluginId, _ => new PluginMetricsData { PluginId = pluginId });
        var now = DateTime.UtcNow;
        var cpuUsage = GetCurrentCpuUsage();

        lock (metricsData)
        {
            // Update basic counters
            metricsData.TotalExecutions++;
            if (success)
            {
                metricsData.SuccessfulExecutions++;
            }
            else
            {
                metricsData.FailedExecutions++;
                
                // Track error types
                var errorType = error?.GetType().Name ?? "Unknown";
                metricsData.ErrorCounts[errorType] = metricsData.ErrorCounts.GetValueOrDefault(errorType) + 1;
            }

            // Update timing statistics
            metricsData.TotalExecutionTime += duration;
            if (duration < metricsData.MinExecutionTime)
            {
                metricsData.MinExecutionTime = duration;
            }
            if (duration > metricsData.MaxExecutionTime)
            {
                metricsData.MaxExecutionTime = duration;
            }

            // Update timestamps
            if (metricsData.FirstExecution == default)
            {
                metricsData.FirstExecution = now;
            }
            metricsData.LastExecution = now;

            // Update resource usage
            metricsData.TotalMemoryAllocated += memoryUsed;
            metricsData.AverageCpuUsage = (metricsData.AverageCpuUsage + cpuUsage) / 2;

            // Add to recent executions (keep last 100)
            var executionRecord = new ExecutionRecord
            {
                Timestamp = now,
                Duration = duration,
                Success = success,
                ErrorType = error?.GetType().Name,
                MemoryUsed = memoryUsed,
                CpuUsage = cpuUsage
            };

            metricsData.RecentExecutions.Enqueue(executionRecord);
            while (metricsData.RecentExecutions.Count > 100)
            {
                metricsData.RecentExecutions.Dequeue();
            }
        }

        _logger.LogDebug("Recorded execution for plugin {PluginId}: Duration={Duration}ms, Success={Success}",
            pluginId, duration.TotalMilliseconds, success);
    }

    /// <summary>
    /// Gets comprehensive metrics for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin metrics if available; otherwise, null.</returns>
    public PluginMetrics? GetPluginMetrics(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (!_metricsData.TryGetValue(pluginId, out var metricsData))
        {
            return null;
        }

        lock (metricsData)
        {
            return new PluginMetrics
            {
                PluginId = pluginId,
                TotalExecutions = metricsData.TotalExecutions,
                SuccessfulExecutions = metricsData.SuccessfulExecutions,
                FailedExecutions = metricsData.FailedExecutions,
                SuccessRate = metricsData.TotalExecutions > 0 
                    ? (double)metricsData.SuccessfulExecutions / metricsData.TotalExecutions 
                    : 0,
                AverageExecutionTime = metricsData.TotalExecutions > 0 
                    ? TimeSpan.FromTicks(metricsData.TotalExecutionTime.Ticks / metricsData.TotalExecutions)
                    : TimeSpan.Zero,
                MinExecutionTime = metricsData.MinExecutionTime == TimeSpan.MaxValue 
                    ? TimeSpan.Zero 
                    : metricsData.MinExecutionTime,
                MaxExecutionTime = metricsData.MaxExecutionTime,
                TotalExecutionTime = metricsData.TotalExecutionTime,
                FirstExecution = metricsData.FirstExecution,
                LastExecution = metricsData.LastExecution,
                ErrorCounts = new Dictionary<string, long>(metricsData.ErrorCounts),
                TotalMemoryAllocated = metricsData.TotalMemoryAllocated,
                AverageCpuUsage = metricsData.AverageCpuUsage,
                ThroughputPerSecond = CalculateThroughput(metricsData),
                RecentErrorRate = CalculateRecentErrorRate(metricsData),
                PerformanceTrend = CalculatePerformanceTrend(metricsData)
            };
        }
    }

    /// <summary>
    /// Gets metrics for all plugins.
    /// </summary>
    /// <returns>Collection of plugin metrics.</returns>
    public IEnumerable<PluginMetrics> GetAllPluginMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return _metricsData.Keys
            .Select(GetPluginMetrics)
            .Where(metrics => metrics != null)
            .Cast<PluginMetrics>();
    }

    /// <summary>
    /// Gets system-wide metrics summary.
    /// </summary>
    /// <returns>System metrics summary.</returns>
    public SystemMetrics GetSystemMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var allMetrics = GetAllPluginMetrics().ToList();
        
        return new SystemMetrics
        {
            TotalPlugins = _registry.PluginCount,
            ActivePlugins = allMetrics.Count,
            TotalExecutions = allMetrics.Sum(m => m.TotalExecutions),
            TotalSuccessfulExecutions = allMetrics.Sum(m => m.SuccessfulExecutions),
            TotalFailedExecutions = allMetrics.Sum(m => m.FailedExecutions),
            OverallSuccessRate = allMetrics.Sum(m => m.TotalExecutions) > 0
                ? allMetrics.Sum(m => m.SuccessfulExecutions) / (double)allMetrics.Sum(m => m.TotalExecutions)
                : 0,
            AverageExecutionTime = allMetrics.Any() 
                ? TimeSpan.FromTicks((long)allMetrics.Average(m => m.AverageExecutionTime.Ticks))
                : TimeSpan.Zero,
            TotalMemoryAllocated = allMetrics.Sum(m => m.TotalMemoryAllocated),
            SystemCpuUsage = GetCurrentCpuUsage(),
            SystemMemoryUsage = GetCurrentMemoryUsage(),
            UptimeHours = (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalHours
        };
    }

    /// <summary>
    /// Gets performance trend analysis for a plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="timeRange">The time range to analyze.</param>
    /// <returns>Performance trend analysis.</returns>
    public PluginPerformanceTrend GetPerformanceTrend(string pluginId, TimeSpan timeRange)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (!_metricsData.TryGetValue(pluginId, out var metricsData))
        {
            return new PluginPerformanceTrend { PluginId = pluginId, TrendDirection = TrendDirection.Unknown };
        }

        lock (metricsData)
        {
            var cutoffTime = DateTime.UtcNow - timeRange;
            var recentExecutions = metricsData.RecentExecutions
                .Where(e => e.Timestamp >= cutoffTime)
                .ToList();

            if (!recentExecutions.Any())
            {
                return new PluginPerformanceTrend { PluginId = pluginId, TrendDirection = TrendDirection.Unknown };
            }

            // Analyze performance trends
            var firstHalf = recentExecutions.Take(recentExecutions.Count / 2).ToList();
            var secondHalf = recentExecutions.Skip(recentExecutions.Count / 2).ToList();

            if (!firstHalf.Any() || !secondHalf.Any())
            {
                return new PluginPerformanceTrend { PluginId = pluginId, TrendDirection = TrendDirection.Stable };
            }

            var firstHalfAvg = firstHalf.Average(e => e.Duration.TotalMilliseconds);
            var secondHalfAvg = secondHalf.Average(e => e.Duration.TotalMilliseconds);
            var percentChange = (secondHalfAvg - firstHalfAvg) / firstHalfAvg * 100;

            var trendDirection = percentChange switch
            {
                > 10 => TrendDirection.Degrading,
                < -10 => TrendDirection.Improving,
                _ => TrendDirection.Stable
            };

            return new PluginPerformanceTrend
            {
                PluginId = pluginId,
                TrendDirection = trendDirection,
                PercentChange = percentChange,
                SampleSize = recentExecutions.Count,
                TimeRange = timeRange
            };
        }
    }

    /// <summary>
    /// Exports metrics data to a structured format.
    /// </summary>
    /// <param name="format">The export format.</param>
    /// <returns>Exported metrics data.</returns>
    public string ExportMetrics(MetricsExportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return format switch
        {
            MetricsExportFormat.Json => ExportAsJson(),
            MetricsExportFormat.Csv => ExportAsCsv(),
            MetricsExportFormat.Xml => ExportAsXml(),
            _ => throw new ArgumentException($"Unsupported export format: {format}")
        };
    }

    /// <summary>
    /// Clears metrics data for a specific plugin.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>True if metrics were cleared; otherwise, false.</returns>
    public bool ClearPluginMetrics(string pluginId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_metricsData.TryRemove(pluginId, out _))
        {
            _logger.LogInformation("Cleared metrics for plugin {PluginId}", pluginId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clears all metrics data.
    /// </summary>
    public void ClearAllMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var count = _metricsData.Count;
        _metricsData.Clear();
        
        _logger.LogInformation("Cleared metrics for {Count} plugins", count);
    }

    /// <summary>
    /// Calculates throughput for a plugin.
    /// </summary>
    private static double CalculateThroughput(PluginMetricsData metricsData)
    {
        if (metricsData.FirstExecution == default || metricsData.LastExecution == default)
        {
            return 0;
        }

        var timeSpan = metricsData.LastExecution - metricsData.FirstExecution;
        if (timeSpan.TotalSeconds <= 0)
        {
            return metricsData.TotalExecutions;
        }

        return metricsData.TotalExecutions / timeSpan.TotalSeconds;
    }

    /// <summary>
    /// Calculates recent error rate for a plugin.
    /// </summary>
    private static double CalculateRecentErrorRate(PluginMetricsData metricsData)
    {
        var recentExecutions = metricsData.RecentExecutions.TakeLast(50).ToList();
        if (!recentExecutions.Any())
        {
            return 0;
        }

        var errors = recentExecutions.Count(e => !e.Success);
        return (double)errors / recentExecutions.Count;
    }

    /// <summary>
    /// Calculates performance trend for a plugin.
    /// </summary>
    private static TrendDirection CalculatePerformanceTrend(PluginMetricsData metricsData)
    {
        var recentExecutions = metricsData.RecentExecutions.TakeLast(20).ToList();
        if (recentExecutions.Count < 10)
        {
            return TrendDirection.Unknown;
        }

        var firstHalf = recentExecutions.Take(10).Average(e => e.Duration.TotalMilliseconds);
        var secondHalf = recentExecutions.Skip(10).Average(e => e.Duration.TotalMilliseconds);
        
        var percentChange = (secondHalf - firstHalf) / firstHalf * 100;

        return percentChange switch
        {
            > 15 => TrendDirection.Degrading,
            < -15 => TrendDirection.Improving,
            _ => TrendDirection.Stable
        };
    }

    /// <summary>
    /// Gets current CPU usage percentage.
    /// </summary>
    private double GetCurrentCpuUsage()
    {
        try
        {
            return _cpuCounter?.NextValue() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets current memory usage in bytes.
    /// </summary>
    private static long GetCurrentMemoryUsage()
    {
        try
        {
            return GC.GetTotalMemory(false);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Timer callback for collecting periodic metrics.
    /// </summary>
    private void CollectMetrics(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var systemCpu = GetCurrentCpuUsage();
            var systemMemory = GetCurrentMemoryUsage();

            foreach (var kvp in _metricsData)
            {
                lock (kvp.Value)
                {
                    // Create performance snapshot
                    var snapshot = new PerformanceSnapshot
                    {
                        Timestamp = now,
                        ThroughputPerSecond = CalculateThroughput(kvp.Value),
                        AverageExecutionTime = kvp.Value.TotalExecutions > 0
                            ? TimeSpan.FromTicks(kvp.Value.TotalExecutionTime.Ticks / kvp.Value.TotalExecutions)
                            : TimeSpan.Zero,
                        ErrorRate = CalculateRecentErrorRate(kvp.Value),
                        MemoryUsage = systemMemory,
                        CpuUsage = systemCpu
                    };

                    kvp.Value.PerformanceHistory.Enqueue(snapshot);
                    
                    // Keep only last 24 hours of snapshots (1440 minutes)
                    while (kvp.Value.PerformanceHistory.Count > 1440)
                    {
                        kvp.Value.PerformanceHistory.Dequeue();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during metrics collection");
        }
    }

    /// <summary>
    /// Exports metrics as JSON.
    /// </summary>
    private string ExportAsJson()
    {
        var allMetrics = GetAllPluginMetrics();
        return System.Text.Json.JsonSerializer.Serialize(allMetrics, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Exports metrics as CSV.
    /// </summary>
    private string ExportAsCsv()
    {
        var allMetrics = GetAllPluginMetrics().ToList();
        var csv = new System.Text.StringBuilder();
        
        // Header
        csv.AppendLine("PluginId,TotalExecutions,SuccessfulExecutions,FailedExecutions,SuccessRate,AverageExecutionTime,MinExecutionTime,MaxExecutionTime");
        
        // Data
        foreach (var metrics in allMetrics)
        {
            csv.AppendLine($"{metrics.PluginId},{metrics.TotalExecutions},{metrics.SuccessfulExecutions},{metrics.FailedExecutions},{metrics.SuccessRate:F4},{metrics.AverageExecutionTime.TotalMilliseconds},{metrics.MinExecutionTime.TotalMilliseconds},{metrics.MaxExecutionTime.TotalMilliseconds}");
        }
        
        return csv.ToString();
    }

    /// <summary>
    /// Exports metrics as XML.
    /// </summary>
    private string ExportAsXml()
    {
        var allMetrics = GetAllPluginMetrics();
        var xml = new System.Text.StringBuilder();
        
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine("<PluginMetrics>");
        
        foreach (var metrics in allMetrics)
        {
            xml.AppendLine($"  <Plugin Id=\"{metrics.PluginId}\">");
            xml.AppendLine($"    <TotalExecutions>{metrics.TotalExecutions}</TotalExecutions>");
            xml.AppendLine($"    <SuccessfulExecutions>{metrics.SuccessfulExecutions}</SuccessfulExecutions>");
            xml.AppendLine($"    <FailedExecutions>{metrics.FailedExecutions}</FailedExecutions>");
            xml.AppendLine($"    <SuccessRate>{metrics.SuccessRate:F4}</SuccessRate>");
            xml.AppendLine($"    <AverageExecutionTime>{metrics.AverageExecutionTime.TotalMilliseconds}</AverageExecutionTime>");
            xml.AppendLine("  </Plugin>");
        }
        
        xml.AppendLine("</PluginMetrics>");
        return xml.ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _metricsCollectionTimer.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            
            _logger.LogInformation("Plugin metrics service disposed");
        }
    }
}

/// <summary>
/// Represents comprehensive metrics for a plugin.
/// </summary>
public sealed class PluginMetrics
{
    public required string PluginId { get; init; }
    public long TotalExecutions { get; init; }
    public long SuccessfulExecutions { get; init; }
    public long FailedExecutions { get; init; }
    public double SuccessRate { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MinExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
    public TimeSpan TotalExecutionTime { get; init; }
    public DateTime FirstExecution { get; init; }
    public DateTime LastExecution { get; init; }
    public Dictionary<string, long> ErrorCounts { get; init; } = new();
    public long TotalMemoryAllocated { get; init; }
    public double AverageCpuUsage { get; init; }
    public double ThroughputPerSecond { get; init; }
    public double RecentErrorRate { get; init; }
    public TrendDirection PerformanceTrend { get; init; }
}

/// <summary>
/// Represents system-wide metrics.
/// </summary>
public sealed class SystemMetrics
{
    public int TotalPlugins { get; init; }
    public int ActivePlugins { get; init; }
    public long TotalExecutions { get; init; }
    public long TotalSuccessfulExecutions { get; init; }
    public long TotalFailedExecutions { get; init; }
    public double OverallSuccessRate { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public long TotalMemoryAllocated { get; init; }
    public double SystemCpuUsage { get; init; }
    public long SystemMemoryUsage { get; init; }
    public double UptimeHours { get; init; }
}

/// <summary>
/// Represents plugin-specific performance trend analysis.
/// </summary>
public sealed class PluginPerformanceTrend
{
    public required string PluginId { get; init; }
    public TrendDirection TrendDirection { get; init; }
    public double PercentChange { get; init; }
    public int SampleSize { get; init; }
    public TimeSpan TimeRange { get; init; }
}

/// <summary>
/// Represents trend direction.
/// </summary>
public enum TrendDirection
{
    Unknown,
    Improving,
    Stable,
    Degrading
}

/// <summary>
/// Represents metrics export formats.
/// </summary>
public enum MetricsExportFormat
{
    Json,
    Csv,
    Xml
}