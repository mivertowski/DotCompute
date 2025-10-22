// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Core.Telemetry;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Central telemetry coordination for Metal backend with production-grade monitoring.
/// Consolidated using BaseTelemetryProvider to eliminate duplicate patterns.
/// </summary>
public sealed class MetalTelemetryManager : BaseTelemetryProvider
{
    private readonly ILogger<MetalTelemetryManager> _logger;
    private readonly MetalTelemetryOptions _options;
    private readonly MetalPerformanceCounters _performanceCounters;
    private readonly MetalHealthMonitor _healthMonitor;
    private readonly MetalProductionLogger _productionLogger;
    private readonly MetalMetricsExporter _metricsExporter;
    private readonly MetalAlertsManager _alertsManager;


    private readonly ConcurrentDictionary<string, MetalOperationMetrics> _operationMetrics;
    private readonly ConcurrentDictionary<string, MetalResourceMetrics> _resourceMetrics;
    private readonly Timer? _reportingTimer;
    private readonly Timer? _cleanupTimer;


    private readonly Meter _meter;
    private readonly Counter<long> _operationCounter;
    private readonly Counter<long> _errorCounter;
    private readonly Histogram<double> _operationDuration;
    private readonly Gauge<long> _memoryUsage;
    private readonly Gauge<double> _gpuUtilization;


    private volatile bool _disposed;
    private long _totalOperations;
    private long _totalErrors;

    public MetalTelemetryManager(
        IOptions<MetalTelemetryOptions> options,
        ILogger<MetalTelemetryManager> logger,
        ILoggerFactory loggerFactory) : base(logger, new Abstractions.Telemetry.TelemetryConfiguration(), "Metal", "1.0.0")
    {
        _options = options.Value;
        _logger = logger;

        // Initialize telemetry components

        _performanceCounters = new MetalPerformanceCounters(
            loggerFactory.CreateLogger<MetalPerformanceCounters>(),
            _options.PerformanceCountersOptions);


        _healthMonitor = new MetalHealthMonitor(
            loggerFactory.CreateLogger<MetalHealthMonitor>(),
            _options.HealthMonitorOptions);


        _productionLogger = new MetalProductionLogger(
            loggerFactory.CreateLogger<MetalProductionLogger>(),
            _options.LoggingOptions);


        _metricsExporter = new MetalMetricsExporter(
            loggerFactory.CreateLogger<MetalMetricsExporter>(),
            _options.ExportOptions);


        _alertsManager = new MetalAlertsManager(
            loggerFactory.CreateLogger<MetalAlertsManager>(),
            _options.AlertsOptions);

        // Initialize collections

        _operationMetrics = new ConcurrentDictionary<string, MetalOperationMetrics>();
        _resourceMetrics = new ConcurrentDictionary<string, MetalResourceMetrics>();

        // Initialize OpenTelemetry metrics

        _meter = new Meter("DotCompute.Backends.Metal", "1.0.0");
        _operationCounter = _meter.CreateCounter<long>("metal_operations_total", "count", "Total number of Metal operations");
        _errorCounter = _meter.CreateCounter<long>("metal_errors_total", "count", "Total number of Metal errors");
        _operationDuration = _meter.CreateHistogram<double>("metal_operation_duration_ms", "ms", "Duration of Metal operations in milliseconds");
        _memoryUsage = _meter.CreateGauge<long>("metal_memory_usage_bytes", "bytes", "Current Metal memory usage");
        _gpuUtilization = _meter.CreateGauge<double>("metal_gpu_utilization_percent", "%", "Current GPU utilization percentage");

        // Start periodic tasks

        if (_options.ReportingInterval > TimeSpan.Zero)
        {
            _reportingTimer = new Timer(GeneratePeriodicReport, null, _options.ReportingInterval, _options.ReportingInterval);
        }


        if (_options.CleanupInterval > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(PerformCleanup, null, _options.CleanupInterval, _options.CleanupInterval);
        }


        _logger.LogInformation("Metal telemetry manager initialized with reporting interval: {ReportingInterval}, cleanup interval: {CleanupInterval}",
            _options.ReportingInterval, _options.CleanupInterval);
    }

    /// <summary>
    /// Records memory allocation metrics
    /// </summary>
    public void RecordMemoryAllocation(long sizeBytes, TimeSpan duration, bool success = true)
    {
        if (_disposed)
        {
            return;
        }


        var tags = new Dictionary<string, object?>
        {
            ["operation"] = "memory_allocation",
            ["success"] = success,
            ["size_bytes"] = sizeBytes,
            ["size_category"] = GetSizeCategory(sizeBytes)
        };

        _operationCounter.Add(1, [.. tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))]);
        _operationDuration.Record(duration.TotalMilliseconds, [.. tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))]);

        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "memory_allocation"));
            _alertsManager.CheckMemoryAllocationFailure(sizeBytes);
        }

        _performanceCounters.RecordMemoryAllocation(sizeBytes, duration, success);
        _productionLogger.LogMemoryAllocation(sizeBytes, duration, success);

        _ = Interlocked.Increment(ref _totalOperations);
        if (!success)
        {
            _ = Interlocked.Increment(ref _totalErrors);
        }
    }

    /// <summary>
    /// Records kernel execution metrics
    /// </summary>
    public void RecordKernelExecution(string kernelName, TimeSpan duration, long dataSize, bool success = true, Dictionary<string, object>? additionalProperties = null)
    {
        if (_disposed)
        {
            return;
        }


        var correlationId = MetalProductionLogger.GenerateCorrelationId();


        var tags = new Dictionary<string, object?>
        {
            ["operation"] = "kernel_execution",
            ["kernel_name"] = kernelName,
            ["success"] = success,
            ["data_size"] = dataSize,
            ["correlation_id"] = correlationId
        };

        _operationCounter.Add(1, [.. tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))]);
        _operationDuration.Record(duration.TotalMilliseconds, [.. tags.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))]);

        if (!success)
        {
            _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "kernel_execution"));
            _alertsManager.CheckKernelExecutionFailure(kernelName, duration);
        }

        // Update operation metrics
        var operationKey = $"kernel_{kernelName}";
        _ = _operationMetrics.AddOrUpdate(operationKey,
            new MetalOperationMetrics(operationKey, duration, success),
            (_, existing) =>
            {
                existing.UpdateMetrics(duration, success);
                return existing;
            });

        _performanceCounters.RecordKernelExecution(kernelName, duration, dataSize, success);
        _productionLogger.LogKernelExecution(correlationId, kernelName, duration, dataSize, success, additionalProperties);

        // Check performance thresholds
        if (duration.TotalMilliseconds > _options.SlowOperationThresholdMs)
        {
            _alertsManager.CheckSlowOperation(operationKey, duration);
        }

        _ = Interlocked.Increment(ref _totalOperations);
        if (!success)
        {
            _ = Interlocked.Increment(ref _totalErrors);
        }
    }

    /// <summary>
    /// Records device utilization metrics
    /// </summary>
    public void RecordDeviceUtilization(double gpuUtilization, double memoryUtilization, long totalMemory, long usedMemory)
    {
        if (_disposed)
        {
            return;
        }


        _gpuUtilization.Record(gpuUtilization);
        _memoryUsage.Record(usedMemory);

        var utilizationMetrics = new Dictionary<string, object>
        {
            ["gpu_utilization"] = gpuUtilization,
            ["memory_utilization"] = memoryUtilization,
            ["total_memory"] = totalMemory,
            ["used_memory"] = usedMemory,
            ["available_memory"] = totalMemory - usedMemory
        };

        _performanceCounters.RecordDeviceUtilization(gpuUtilization, memoryUtilization);
        _productionLogger.LogDeviceUtilization(utilizationMetrics);

        // Check utilization thresholds
        if (gpuUtilization > _options.HighGpuUtilizationThreshold)
        {
            _alertsManager.CheckHighGpuUtilization(gpuUtilization);
        }

        if (memoryUtilization > _options.HighMemoryUtilizationThreshold)
        {
            _alertsManager.CheckHighMemoryUtilization(memoryUtilization);
        }

        // Update resource metrics
        var resourceKey = "gpu_device";
        _ = _resourceMetrics.AddOrUpdate(resourceKey,
            new MetalResourceMetrics(resourceKey, usedMemory, totalMemory),
            (_, existing) =>
            {
                existing.UpdateUtilization(gpuUtilization, memoryUtilization, usedMemory);
                return existing;
            });
    }

    /// <summary>
    /// Records error events with context
    /// </summary>
    public void RecordErrorEvent(MetalError error, string context, Dictionary<string, object>? additionalContext = null)
    {
        if (_disposed)
        {
            return;
        }


        var correlationId = MetalProductionLogger.GenerateCorrelationId();


        _errorCounter.Add(1, new KeyValuePair<string, object?>("error_code", error.ToString()));

        _performanceCounters.RecordError(error, context);
        _productionLogger.LogError(correlationId, error, context, additionalContext);
        _alertsManager.CheckErrorRate(error);


        _healthMonitor.ReportError(error, context);

        _ = Interlocked.Increment(ref _totalErrors);
    }

    /// <summary>
    /// Records memory pressure levels
    /// </summary>
    public void RecordMemoryPressure(MemoryPressureLevel level, double percentage)
    {
        if (_disposed)
        {
            return;
        }


        _performanceCounters.RecordMemoryPressure(level, percentage);
        _productionLogger.LogMemoryPressure(level, percentage);

        if (level >= MemoryPressureLevel.High)
        {
            _alertsManager.CheckHighMemoryPressure(level, percentage);
        }

        _healthMonitor.ReportMemoryPressure(level, percentage);
    }

    /// <summary>
    /// Records resource usage metrics
    /// </summary>
    public void RecordResourceUsage(ResourceType type, long currentUsage, long peakUsage, long limit)
    {
        if (_disposed)
        {
            return;
        }


        var utilizationPercentage = limit > 0 ? (double)currentUsage / limit * 100.0 : 0.0;


        _performanceCounters.RecordResourceUsage(type, currentUsage, peakUsage, limit);
        _productionLogger.LogResourceUsage(type, currentUsage, peakUsage, limit, utilizationPercentage);

        // Update resource metrics
        var resourceKey = $"resource_{type}";
        _ = _resourceMetrics.AddOrUpdate(resourceKey,
            new MetalResourceMetrics(resourceKey, currentUsage, limit),
            (_, existing) =>
            {
                existing.UpdateUsage(currentUsage, peakUsage, limit);
                return existing;
            });

        if (utilizationPercentage > _options.HighResourceUtilizationThreshold)
        {
            _alertsManager.CheckHighResourceUtilization(type, utilizationPercentage);
        }
    }

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    public MetalTelemetrySnapshot GetCurrentSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var snapshot = new MetalTelemetrySnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            TotalOperations = Interlocked.Read(ref _totalOperations),
            TotalErrors = Interlocked.Read(ref _totalErrors),
            ErrorRate = CalculateErrorRate(),
            HealthStatus = _healthMonitor.GetCurrentHealth(),
            SystemInfo = GetSystemInfo()
        };

        // Add items to collection properties
        foreach (var kvp in _operationMetrics)
        {
            snapshot.OperationMetrics[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in _resourceMetrics)
        {
            snapshot.ResourceMetrics[kvp.Key] = kvp.Value;
        }

        var performanceCounters = _performanceCounters.GetCurrentCounters();
        foreach (var kvp in performanceCounters)
        {
            snapshot.PerformanceCounters[kvp.Key] = kvp.Value;
        }

        return snapshot;
    }

    /// <summary>
    /// Generates comprehensive production report
    /// </summary>
    public MetalProductionReport GenerateProductionReport()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var snapshot = GetCurrentSnapshot();


        var report = new MetalProductionReport
        {
            Snapshot = snapshot,
            PerformanceAnalysis = _performanceCounters.AnalyzePerformance(),
            HealthAnalysis = _healthMonitor.AnalyzeHealth()
        };

        // Add items to collection properties
        var alerts = _alertsManager.GetActivAlerts();
        foreach (var alert in alerts)
        {
            report.AlertsSummary.Add(alert);
        }

        var recommendations = GenerateRecommendations(snapshot);
        foreach (var recommendation in recommendations)
        {
            report.Recommendations.Add(recommendation);
        }

        var exportedMetrics = _metricsExporter.GetExportableMetrics();
        foreach (var kvp in exportedMetrics)
        {
            report.ExportedMetrics[kvp.Key] = kvp.Value;
        }

        return report;
    }

    /// <summary>
    /// Exports metrics to configured monitoring systems
    /// </summary>
    public async Task ExportMetricsAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var snapshot = GetCurrentSnapshot();
            await _metricsExporter.ExportAsync(snapshot, cancellationToken);


            _logger.LogDebug("Successfully exported metrics to monitoring systems");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export metrics to monitoring systems");
            RecordErrorEvent(MetalError.InternalError, "metrics_export_failed");
        }
    }

    private void GeneratePeriodicReport(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var report = GenerateProductionReport();


            _logger.LogInformation("Metal telemetry report - Operations: {Operations}, Errors: {Errors}, Error Rate: {ErrorRate:P2}, Health: {Health}",
                report.Snapshot.TotalOperations, report.Snapshot.TotalErrors, report.Snapshot.ErrorRate, report.Snapshot.HealthStatus);

            // Export metrics if configured
            if (_options.AutoExportMetrics)
            {
                _ = Task.Run(async () => await ExportMetricsAsync());
            }

            // Trigger alerts if needed
            _alertsManager.EvaluateActiveAlerts(report.Snapshot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating periodic telemetry report");
        }
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var now = DateTimeOffset.UtcNow;
            var cutoffTime = now.Subtract(_options.MetricsRetentionPeriod);

            // Clean up old operation metrics
            var oldOperations = _operationMetrics
                .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldOperations)
            {
                _ = _operationMetrics.TryRemove(key, out _);
            }

            // Clean up old resource metrics
            var oldResources = _resourceMetrics
                .Where(kvp => kvp.Value.LastUpdated < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldResources)
            {
                _ = _resourceMetrics.TryRemove(key, out _);
            }

            _performanceCounters.PerformCleanup(cutoffTime);
            _healthMonitor.PerformCleanup(cutoffTime);
            _productionLogger.PerformCleanup(cutoffTime);

            if (oldOperations.Count > 0 || oldResources.Count > 0)
            {
                _logger.LogDebug("Telemetry cleanup completed - removed {Operations} operation metrics, {Resources} resource metrics",
                    oldOperations.Count, oldResources.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during telemetry cleanup");
        }
    }

    private double CalculateErrorRate()
    {
        var totalOps = Interlocked.Read(ref _totalOperations);
        var totalErrors = Interlocked.Read(ref _totalErrors);


        return totalOps > 0 ? (double)totalErrors / totalOps : 0.0;
    }

    private static string GetSizeCategory(long sizeBytes)
    {
        return sizeBytes switch
        {
            < 1024 => "tiny",
            < 1024 * 1024 => "small",
            < 1024 * 1024 * 1024 => "medium",
            _ => "large"
        };
    }

    private static MetalSystemInfo GetSystemInfo()
    {
        try
        {
            return new MetalSystemInfo
            {
                DeviceCount = MetalNative.GetDeviceCount(),
                TotalSystemMemory = GC.GetTotalMemory(false),
                AvailableSystemMemory = GC.GetTotalMemory(false), // Simplified
                ProcessorCount = Environment.ProcessorCount,
                OSVersion = Environment.OSVersion.VersionString,
                RuntimeVersion = Environment.Version.ToString()
            };
        }
        catch
        {
            return new MetalSystemInfo
            {
                DeviceCount = 0,
                TotalSystemMemory = 0,
                AvailableSystemMemory = 0,
                ProcessorCount = Environment.ProcessorCount,
                OSVersion = Environment.OSVersion.VersionString,
                RuntimeVersion = Environment.Version.ToString()
            };
        }
    }

    private static List<string> GenerateRecommendations(MetalTelemetrySnapshot snapshot)
    {
        var recommendations = new List<string>();

        // Error rate recommendations
        if (snapshot.ErrorRate > 0.05) // > 5% error rate
        {
            recommendations.Add($"High error rate detected ({snapshot.ErrorRate:P2}). Consider investigating recent failures and implementing additional error handling.");
        }

        // Memory recommendations
        var memoryMetrics = snapshot.ResourceMetrics.Values.FirstOrDefault(m => m.ResourceName.Contains("memory", StringComparison.OrdinalIgnoreCase));
        if (memoryMetrics != null && memoryMetrics.UtilizationPercentage > 85)
        {
            recommendations.Add($"High memory utilization ({memoryMetrics.UtilizationPercentage:F1}%). Consider implementing memory pooling or reducing allocation sizes.");
        }

        // Performance recommendations
        var slowOperations = snapshot.OperationMetrics.Values.Where(m => m.AverageExecutionTime.TotalMilliseconds > 100).ToList();
        if (slowOperations.Count > 0)
        {
            recommendations.Add($"{slowOperations.Count} operations have high average execution time. Consider optimizing: {string.Join(", ", slowOperations.Select(o => o.OperationName).Take(3))}");
        }

        return recommendations;
    }

    protected override string GetBackendType() => "Metal";

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;

            // Generate final report
            try
            {
                var finalReport = GenerateProductionReport();
                _logger.LogInformation("Metal telemetry manager disposed - Final report: Operations: {Operations}, Errors: {Errors}, Uptime: {Uptime}",
                    finalReport.Snapshot.TotalOperations, finalReport.Snapshot.TotalErrors, DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime);
            }
            catch
            {
                // Suppress exceptions during disposal
            }

            // Dispose timers
            _reportingTimer?.Dispose();
            _cleanupTimer?.Dispose();

            // Dispose components
            _performanceCounters?.Dispose();
            _healthMonitor?.Dispose();
            _productionLogger?.Dispose();
            _metricsExporter?.Dispose();
            _alertsManager?.Dispose();

            // Dispose OpenTelemetry
            _meter?.Dispose();
        }

        base.Dispose(disposing);
    }
}