// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Execution;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Continuous health monitoring for Metal backend with anomaly detection
/// </summary>
public sealed class MetalHealthMonitor : IDisposable
{
    private readonly ILogger<MetalHealthMonitor> _logger;
    private readonly MetalHealthMonitorOptions _options;
    private readonly ConcurrentQueue<HealthEvent> _healthEvents;
    private readonly ConcurrentDictionary<string, ComponentHealth> _componentHealth;
    private readonly Timer? _healthCheckTimer;
    private readonly Timer? _anomalyDetectionTimer;
    private volatile bool _disposed;

    private readonly CircuitBreaker _memoryCircuitBreaker;
    private readonly CircuitBreaker _deviceCircuitBreaker;
    private readonly CircuitBreaker _kernelCircuitBreaker;

    public MetalHealthMonitor(
        ILogger<MetalHealthMonitor> logger,
        MetalHealthMonitorOptions options)
    {
        _logger = logger;
        _options = options;
        _healthEvents = new ConcurrentQueue<HealthEvent>();
        _componentHealth = new ConcurrentDictionary<string, ComponentHealth>();

        // Initialize circuit breakers
        _memoryCircuitBreaker = new CircuitBreaker("Memory", _options.CircuitBreakerThreshold, _options.CircuitBreakerTimeout);
        _deviceCircuitBreaker = new CircuitBreaker("Device", _options.CircuitBreakerThreshold, _options.CircuitBreakerTimeout);
        _kernelCircuitBreaker = new CircuitBreaker("Kernel", _options.CircuitBreakerThreshold, _options.CircuitBreakerTimeout);

        // Initialize component health
        InitializeComponentHealth();

        // Start monitoring timers
        if (_options.HealthCheckInterval > TimeSpan.Zero)
        {
            _healthCheckTimer = new Timer(PerformHealthCheck, null, _options.HealthCheckInterval, _options.HealthCheckInterval);
        }

        if (_options.AnomalyDetectionInterval > TimeSpan.Zero)
        {
            _anomalyDetectionTimer = new Timer(DetectAnomalies, null, _options.AnomalyDetectionInterval, _options.AnomalyDetectionInterval);
        }

        _logger.LogInformation("Metal health monitor initialized with health check interval: {HealthCheck}, anomaly detection: {AnomalyDetection}",
            _options.HealthCheckInterval, _options.AnomalyDetectionInterval);
    }

    /// <summary>
    /// Reports an error to the health monitoring system
    /// </summary>
    public void ReportError(MetalError error, string context)
    {
        if (_disposed)
        {
            return;
        }


        var healthEvent = new HealthEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = HealthEventType.Error,
            Component = GetErrorComponent(error),
            Severity = GetErrorSeverity(error),
            Message = $"Metal error: {error} in context: {context}"
        };

        // Add data to the collection property
        healthEvent.Data["error_code"] = error;
        healthEvent.Data["context"] = context;

        RecordHealthEvent(healthEvent);

        // Update circuit breakers
        switch (GetErrorComponent(error))
        {
            case "Memory":
                _memoryCircuitBreaker.RecordFailure();
                break;
            case "Device":
                _deviceCircuitBreaker.RecordFailure();
                break;
            case "Kernel":
                _kernelCircuitBreaker.RecordFailure();
                break;
        }

        // Update component health
        var componentName = GetErrorComponent(error);
        _ = _componentHealth.AddOrUpdate(componentName,
            new ComponentHealth(componentName) { Status = HealthStatus.Degraded },
            (_, existing) =>
            {
                existing.RecordError(error, context);
                return existing;
            });

        if (healthEvent.Severity >= HealthSeverity.High)
        {
            _logger.LogWarning("High severity health event recorded: {Component} - {Message}",

                healthEvent.Component, healthEvent.Message);
        }
    }

    /// <summary>
    /// Reports memory pressure to the health monitoring system
    /// </summary>
    public void ReportMemoryPressure(MemoryPressureLevel level, double percentage)
    {
        if (_disposed)
        {
            return;
        }


        var severity = level switch
        {
            MemoryPressureLevel.Low => HealthSeverity.Low,
            MemoryPressureLevel.Medium => HealthSeverity.Medium,
            MemoryPressureLevel.High => HealthSeverity.High,
            MemoryPressureLevel.Critical => HealthSeverity.Critical,
            _ => HealthSeverity.Low
        };

        var healthEvent = new HealthEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = HealthEventType.MemoryPressure,
            Component = "Memory",
            Severity = severity,
            Message = $"Memory pressure: {level} at {percentage:F1}%"
        };

        // Add data to the collection property
        healthEvent.Data["pressure_level"] = level;
        healthEvent.Data["percentage"] = percentage;

        RecordHealthEvent(healthEvent);

        // Update memory component health
        _ = _componentHealth.AddOrUpdate("Memory",
            new ComponentHealth("Memory"),
            (_, existing) =>
            {
                existing.RecordMemoryPressure(level, percentage);
                return existing;
            });

        if (level >= MemoryPressureLevel.High)
        {
            _memoryCircuitBreaker.RecordFailure();
        }
        else
        {
            _memoryCircuitBreaker.RecordSuccess();
        }
    }

    /// <summary>
    /// Reports successful operation to improve health status
    /// </summary>
    public void ReportSuccess(string component, string operation, TimeSpan duration)
    {
        if (_disposed)
        {
            return;
        }


        var healthEvent = new HealthEvent
        {
            Timestamp = DateTimeOffset.UtcNow,
            EventType = HealthEventType.Success,
            Component = component,
            Severity = HealthSeverity.Info,
            Message = $"Successful {operation} completed in {duration.TotalMilliseconds:F2}ms"
        };

        // Add data to the collection property
        healthEvent.Data["operation"] = operation;
        healthEvent.Data["duration_ms"] = duration.TotalMilliseconds;

        RecordHealthEvent(healthEvent);

        // Update component health
        _ = _componentHealth.AddOrUpdate(component,
            new ComponentHealth(component),
            (_, existing) =>
            {
                existing.RecordSuccess(operation, duration);
                return existing;
            });

        // Record success in circuit breakers
        switch (component.ToLowerInvariant())
        {
            case "memory":
                _memoryCircuitBreaker.RecordSuccess();
                break;
            case "device":
                _deviceCircuitBreaker.RecordSuccess();
                break;
            case "kernel":
                _kernelCircuitBreaker.RecordSuccess();
                break;
        }
    }

    /// <summary>
    /// Gets current overall health status
    /// </summary>
    public HealthStatus GetCurrentHealth()
    {
        if (_disposed)
        {
            return HealthStatus.Unknown;
        }

        var componentStatuses = _componentHealth.Values.Select(c => c.Status).ToList();


        if (componentStatuses.Any(s => s == HealthStatus.Critical))
        {
            return HealthStatus.Critical;
        }

        if (componentStatuses.Any(s => s == HealthStatus.Degraded))
        {
            return HealthStatus.Degraded;
        }

        if (componentStatuses.All(s => s == HealthStatus.Healthy))
        {
            return HealthStatus.Healthy;
        }


        return HealthStatus.Unknown;
    }

    /// <summary>
    /// Gets detailed health information for all components
    /// </summary>
    public MetalHealthReport GetDetailedHealthReport()
    {
        if (_disposed)
        {
            return new MetalHealthReport { OverallHealth = HealthStatus.Unknown };
        }


        var recentEvents = _healthEvents.ToArray()
            .Where(e => e.Timestamp > DateTimeOffset.UtcNow.Subtract(_options.EventRetentionPeriod))
            .OrderByDescending(e => e.Timestamp)
            .Take(100)
            .ToList();

        var report = new MetalHealthReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            OverallHealth = GetCurrentHealth()
        };

        // Add items to collection properties
        foreach (var kvp in _componentHealth)
        {
            report.ComponentHealthMap[kvp.Key] = kvp.Value;
        }

        foreach (var evt in recentEvents)
        {
            report.RecentEvents.Add(evt);
        }

        report.CircuitBreakerStates["Memory"] = _memoryCircuitBreaker.GetState();
        report.CircuitBreakerStates["Device"] = _deviceCircuitBreaker.GetState();
        report.CircuitBreakerStates["Kernel"] = _kernelCircuitBreaker.GetState();

        var systemMetrics = GetSystemHealthMetrics();
        foreach (var kvp in systemMetrics)
        {
            report.SystemMetrics[kvp.Key] = kvp.Value;
        }

        var recommendations = GenerateHealthRecommendations(recentEvents);
        foreach (var recommendation in recommendations)
        {
            report.Recommendations.Add(recommendation);
        }

        return report;
    }

    /// <summary>
    /// Analyzes health trends and patterns
    /// </summary>
    public MetalHealthAnalysis AnalyzeHealth()
    {
        if (_disposed)
        {
            return new MetalHealthAnalysis();
        }


        var recentEvents = _healthEvents.ToArray()
            .Where(e => e.Timestamp > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(24)))
            .ToList();

        var analysis = new MetalHealthAnalysis
        {
            Timestamp = DateTimeOffset.UtcNow,
            AnalysisPeriod = TimeSpan.FromHours(24),
            TotalEvents = recentEvents.Count
        };

        // Analyze error patterns
        var errorPatterns = AnalyzeErrorPatterns(recentEvents);
        foreach (var kvp in errorPatterns)
        {
            analysis.ErrorPatterns[kvp.Key] = kvp.Value;
        }

        // Analyze performance degradation
        var performanceDegradation = AnalyzePerformanceDegradation(recentEvents);
        foreach (var kvp in performanceDegradation)
        {
            analysis.PerformanceDegradation[kvp.Key] = kvp.Value;
        }

        // Analyze resource pressure trends
        var resourcePressure = AnalyzeResourcePressure(recentEvents);
        foreach (var kvp in resourcePressure)
        {
            analysis.ResourcePressureTrends[kvp.Key] = kvp.Value;
        }

        // Calculate health score
        analysis.HealthScore = CalculateHealthScore(recentEvents);

        // Predict potential issues
        var predictedIssues = PredictPotentialIssues(recentEvents);
        foreach (var issue in predictedIssues)
        {
            analysis.PredictedIssues.Add(issue);
        }

        return analysis;
    }

    /// <summary>
    /// Performs cleanup of old health events
    /// </summary>
    public void PerformCleanup(DateTimeOffset cutoffTime)
    {
        if (_disposed)
        {
            return;
        }


        var eventsToRemove = new List<HealthEvent>();
        var eventArray = _healthEvents.ToArray();

        foreach (var healthEvent in eventArray)
        {
            if (healthEvent.Timestamp < cutoffTime)
            {
                eventsToRemove.Add(healthEvent);
            }
        }

        // Note: ConcurrentQueue doesn't support selective removal
        // In a production implementation, you'd use a different data structure
        // or implement a more sophisticated cleanup mechanism

        _logger.LogDebug("Health cleanup would remove {Count} old events", eventsToRemove.Count);
    }

    private void InitializeComponentHealth()
    {
        var components = new[] { "Memory", "Device", "Kernel", "Compiler", "CommandQueue", "Pipeline" };


        foreach (var component in components)
        {
            _componentHealth[component] = new ComponentHealth(component);
        }
    }

    private void RecordHealthEvent(HealthEvent healthEvent)
    {
        _healthEvents.Enqueue(healthEvent);

        // Maintain event queue size
        while (_healthEvents.Count > _options.MaxHealthEvents)
        {
            _ = _healthEvents.TryDequeue(out _);
        }

        // Log significant health events
        if (healthEvent.Severity >= HealthSeverity.Medium)
        {
            var logLevel = healthEvent.Severity switch
            {
                HealthSeverity.Critical => LogLevel.Critical,
                HealthSeverity.High => LogLevel.Error,
                HealthSeverity.Medium => LogLevel.Warning,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, "Health event: {Component} - {Message}", healthEvent.Component, healthEvent.Message);
        }
    }

    private void PerformHealthCheck(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Check Metal device availability
            CheckDeviceHealth();

            // Check memory health

            CheckMemoryHealth();

            // Check system resources

            CheckSystemResourceHealth();

            // Update overall health status

            var overallHealth = GetCurrentHealth();


            if (overallHealth != HealthStatus.Healthy)
            {
                _logger.LogWarning("Health check completed with status: {Health}", overallHealth);
            }
            else
            {
                _logger.LogTrace("Health check completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            ReportError(MetalError.InternalError, "health_check_failed");
        }
    }

    private void DetectAnomalies(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var recentEvents = _healthEvents.ToArray()
                .Where(e => e.Timestamp > DateTimeOffset.UtcNow.Subtract(_options.AnomalyDetectionWindow))
                .ToList();

            // Detect error rate anomalies
            DetectErrorRateAnomalies(recentEvents);

            // Detect performance anomalies

            DetectPerformanceAnomalies(recentEvents);

            // Detect resource usage anomalies

            DetectResourceAnomalies(recentEvents);

            _logger.LogTrace("Anomaly detection completed for {EventCount} recent events", recentEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during anomaly detection");
        }
    }

    private void CheckDeviceHealth()
    {
        try
        {
            var deviceCount = MetalNative.GetDeviceCount();


            if (deviceCount == 0)
            {
                ReportError(MetalError.DeviceUnavailable, "no_devices_available");
                return;
            }

            for (var i = 0; i < Math.Min(deviceCount, 4); i++) // Check up to 4 devices
            {
                var device = MetalNative.CreateDeviceAtIndex(i);
                if (device != IntPtr.Zero)
                {
                    // Device is accessible
                    ReportSuccess("Device", $"device_{i}_accessible", TimeSpan.Zero);
                    MetalNative.ReleaseDevice(device);
                }
                else
                {
                    ReportError(MetalError.DeviceUnavailable, $"device_{i}_inaccessible");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check device health");
            ReportError(MetalError.DeviceUnavailable, "device_health_check_failed");
        }
    }

    private void CheckMemoryHealth()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = Environment.WorkingSet;


            var memoryPressure = (double)totalMemory / workingSet;


            if (memoryPressure > 0.9) // > 90% memory usage
            {
                ReportMemoryPressure(MemoryPressureLevel.High, memoryPressure * 100);
            }
            else if (memoryPressure > 0.7) // > 70% memory usage
            {
                ReportMemoryPressure(MemoryPressureLevel.Medium, memoryPressure * 100);
            }
            else
            {
                ReportMemoryPressure(MemoryPressureLevel.Low, memoryPressure * 100);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check memory health");
        }
    }

    private void CheckSystemResourceHealth()
    {
        try
        {
            // Check available disk space (for shader caches, logs, etc.)
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();


            foreach (var drive in drives.Take(2)) // Check system drives
            {
                var freePercentage = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;


                if (freePercentage < 10) // < 10% free space
                {
                    var healthEvent = new HealthEvent
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        EventType = HealthEventType.ResourcePressure,
                        Component = "Storage",
                        Severity = HealthSeverity.High,
                        Message = $"Low disk space on {drive.Name}: {freePercentage:F1}% free",
                        Data = new Dictionary<string, object>
                        {
                            ["drive_name"] = drive.Name,
                            ["free_percentage"] = freePercentage,
                            ["available_bytes"] = drive.AvailableFreeSpace,
                            ["total_bytes"] = drive.TotalSize
                        }
                    };


                    RecordHealthEvent(healthEvent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check system resource health");
        }
    }

    private void DetectErrorRateAnomalies(IReadOnlyList<HealthEvent> recentEvents)
    {
        var errorEvents = recentEvents.Where(e => e.EventType == HealthEventType.Error).ToList();
        var timeWindows = GetTimeWindows(recentEvents, TimeSpan.FromMinutes(5));

        foreach (var window in timeWindows)
        {
            var windowErrors = errorEvents.Where(e => IsInTimeWindow(e, window)).Count();
            var windowTotal = recentEvents.Where(e => IsInTimeWindow(e, window)).Count();


            if (windowTotal > 0)
            {
                var errorRate = (double)windowErrors / windowTotal;


                if (errorRate > _options.AnomalyErrorRateThreshold)
                {
                    var anomalyEvent = new HealthEvent
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        EventType = HealthEventType.Anomaly,
                        Component = "System",
                        Severity = HealthSeverity.High,
                        Message = $"Error rate anomaly detected: {errorRate:P2} in {window.Duration.TotalMinutes:F1} minute window",
                        Data = new Dictionary<string, object>
                        {
                            ["anomaly_type"] = "error_rate",
                            ["error_rate"] = errorRate,
                            ["window_start"] = window.Start,
                            ["window_end"] = window.End,
                            ["error_count"] = windowErrors,
                            ["total_count"] = windowTotal
                        }
                    };


                    RecordHealthEvent(anomalyEvent);
                }
            }
        }
    }

    private void DetectPerformanceAnomalies(IReadOnlyList<HealthEvent> recentEvents)
    {
        var successEvents = recentEvents.Where(e => e.EventType == HealthEventType.Success).ToList();


        if (successEvents.Count < 10)
        {
            return; // Need sufficient data
        }


        var durations = successEvents
            .Where(e => e.Data.ContainsKey("duration_ms"))
            .Select(e => (double)e.Data["duration_ms"])
            .ToList();


        if (durations.Count < 10)
        {
            return;
        }


        var mean = durations.Average();
        var stdDev = Math.Sqrt(durations.Select(d => Math.Pow(d - mean, 2)).Average());
        var threshold = mean + (2 * stdDev); // 2 standard deviations


        var recentAnomalies = durations.TakeLast(10).Count(d => d > threshold);


        if (recentAnomalies > 3) // More than 30% of recent operations are anomalous
        {
            var anomalyEvent = new HealthEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = HealthEventType.Anomaly,
                Component = "Performance",
                Severity = HealthSeverity.Medium,
                Message = $"Performance anomaly detected: {recentAnomalies}/10 recent operations exceed normal duration",
                Data = new Dictionary<string, object>
                {
                    ["anomaly_type"] = "performance_degradation",
                    ["mean_duration_ms"] = mean,
                    ["std_dev_ms"] = stdDev,
                    ["threshold_ms"] = threshold,
                    ["anomalous_operations"] = recentAnomalies
                }
            };


            RecordHealthEvent(anomalyEvent);
        }
    }

    private void DetectResourceAnomalies(IReadOnlyList<HealthEvent> recentEvents)
    {
        var memoryEvents = recentEvents.Where(e => e.EventType == HealthEventType.MemoryPressure).ToList();

        // Detect sustained high memory pressure

        var recentMemoryEvents = memoryEvents.TakeLast(10).ToList();
        var highPressureEvents = recentMemoryEvents.Count(e =>

            e.Data.ContainsKey("pressure_level") &&

            Enum.Parse<MemoryPressureLevel>(e.Data["pressure_level"]?.ToString() ?? "Low") >= MemoryPressureLevel.High);


        if (highPressureEvents > 7) // 70% of recent memory events are high pressure
        {
            var anomalyEvent = new HealthEvent
            {
                Timestamp = DateTimeOffset.UtcNow,
                EventType = HealthEventType.Anomaly,
                Component = "Memory",
                Severity = HealthSeverity.High,
                Message = $"Sustained high memory pressure detected: {highPressureEvents}/10 recent checks show high pressure",
                Data = new Dictionary<string, object>
                {
                    ["anomaly_type"] = "sustained_memory_pressure",
                    ["high_pressure_events"] = highPressureEvents,
                    ["total_recent_events"] = recentMemoryEvents.Count
                }
            };


            RecordHealthEvent(anomalyEvent);
        }
    }

    private static string GetErrorComponent(MetalError error)
    {
        return error switch
        {
            MetalError.OutOfMemory or MetalError.ResourceLimitExceeded => "Memory",
            MetalError.DeviceUnavailable or MetalError.DeviceLost => "Device",
            MetalError.InvalidOperation or MetalError.InvalidArgument => "Kernel",
            MetalError.CompilationError => "Compiler",
            MetalError.InternalError => "System",
            _ => "Unknown"
        };
    }

    private static HealthSeverity GetErrorSeverity(MetalError error)
    {
        return error switch
        {
            MetalError.DeviceLost => HealthSeverity.Critical,
            MetalError.OutOfMemory or MetalError.DeviceUnavailable => HealthSeverity.High,
            MetalError.ResourceLimitExceeded or MetalError.CompilationError => HealthSeverity.Medium,
            _ => HealthSeverity.Low
        };
    }

    private static List<TimeWindow> GetTimeWindows(IReadOnlyList<HealthEvent> events, TimeSpan windowSize)
    {
        if (events.Count == 0)
        {
            return [];
        }


        var windows = new List<TimeWindow>();
        var startTime = events.Min(e => e.Timestamp);
        var endTime = events.Max(e => e.Timestamp);


        var currentStart = startTime;
        while (currentStart < endTime)
        {
            var currentEnd = currentStart.Add(windowSize);
            windows.Add(new TimeWindow { Start = currentStart, End = currentEnd, Duration = windowSize });
            currentStart = currentEnd;
        }


        return windows;
    }

    private static bool IsInTimeWindow(HealthEvent healthEvent, TimeWindow window) => healthEvent.Timestamp >= window.Start && healthEvent.Timestamp < window.End;

    private static Dictionary<string, object> GetSystemHealthMetrics()
    {
        var metrics = new Dictionary<string, object>();

        try
        {
            metrics["total_memory_bytes"] = GC.GetTotalMemory(false);
            metrics["working_set_bytes"] = Environment.WorkingSet;
            metrics["processor_count"] = Environment.ProcessorCount;
            metrics["uptime_ticks"] = Environment.TickCount64;

            // Add Metal-specific metrics

            metrics["metal_device_count"] = MetalNative.GetDeviceCount();
        }
        catch
        {
            // Ignore errors getting system metrics
        }

        return metrics;
    }

    private static List<string> GenerateHealthRecommendations(IReadOnlyList<HealthEvent> recentEvents)
    {
        var recommendations = new List<string>();

        // Analyze error patterns
        var errorsByComponent = recentEvents
            .Where(e => e.EventType == HealthEventType.Error)
            .GroupBy(e => e.Component)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var kvp in errorsByComponent)
        {
            if (kvp.Value > 5) // More than 5 errors from a component
            {
                recommendations.Add($"High error rate in {kvp.Key} component ({kvp.Value} recent errors). Consider investigating root cause and implementing additional error handling.");
            }
        }

        // Analyze memory pressure
        var memoryPressureEvents = recentEvents
            .Where(e => e.EventType == HealthEventType.MemoryPressure)
            .Count();

        if (memoryPressureEvents > 10)
        {
            recommendations.Add("Frequent memory pressure events detected. Consider implementing memory pooling, reducing allocation sizes, or increasing available memory.");
        }

        return recommendations;
    }

    private static Dictionary<string, object> AnalyzeErrorPatterns(IReadOnlyList<HealthEvent> events)
    {
        var errorEvents = events.Where(e => e.EventType == HealthEventType.Error).ToList();


        return new Dictionary<string, object>
        {
            ["total_errors"] = errorEvents.Count,
            ["errors_by_component"] = errorEvents.GroupBy(e => e.Component).ToDictionary(g => g.Key, g => g.Count()),
            ["errors_by_severity"] = errorEvents.GroupBy(e => e.Severity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ["most_frequent_error"] = errorEvents.GroupBy(e => e.Message).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "none"
        };
    }

    private static Dictionary<string, object> AnalyzePerformanceDegradation(IReadOnlyList<HealthEvent> events)
    {
        var successEvents = events.Where(e => e.EventType == HealthEventType.Success).ToList();


        var durations = successEvents
            .Where(e => e.Data.ContainsKey("duration_ms"))
            .Select(e => (double)e.Data["duration_ms"])
            .ToList();

        if (durations.Count == 0)
        {
            return new Dictionary<string, object> { ["status"] = "insufficient_data" };
        }

        return new Dictionary<string, object>
        {
            ["average_duration_ms"] = durations.Average(),
            ["median_duration_ms"] = durations.OrderBy(d => d).ElementAt(durations.Count / 2),
            ["max_duration_ms"] = durations.Max(),
            ["min_duration_ms"] = durations.Min(),
            ["performance_trend"] = CalculatePerformanceTrend(durations)
        };
    }

    private static Dictionary<string, object> AnalyzeResourcePressure(IReadOnlyList<HealthEvent> events)
    {
        var pressureEvents = events.Where(e => e.EventType == HealthEventType.MemoryPressure).ToList();


        if (pressureEvents.Count == 0)
        {
            return new Dictionary<string, object> { ["status"] = "no_pressure_events" };
        }

        var pressureLevels = pressureEvents
            .Where(e => e.Data.ContainsKey("pressure_level"))
            .Select(e => Enum.Parse<MemoryPressureLevel>(e.Data["pressure_level"]?.ToString() ?? "Low"))
            .ToList();

        return new Dictionary<string, object>
        {
            ["total_pressure_events"] = pressureEvents.Count,
            ["high_pressure_events"] = pressureLevels.Count(p => p >= MemoryPressureLevel.High),
            ["critical_pressure_events"] = pressureLevels.Count(p => p == MemoryPressureLevel.Critical),
            ["average_pressure_level"] = pressureLevels.Average(p => (int)p)
        };
    }

    private static double CalculateHealthScore(IReadOnlyList<HealthEvent> events)
    {
        if (events.Count == 0)
        {
            return 100.0;
        }


        var score = 100.0;

        // Penalize errors

        var errorCount = events.Count(e => e.EventType == HealthEventType.Error);
        score -= errorCount * 5; // -5 points per error

        // Penalize high severity events

        var criticalEvents = events.Count(e => e.Severity == HealthSeverity.Critical);
        var highSeverityEvents = events.Count(e => e.Severity == HealthSeverity.High);


        score -= criticalEvents * 20; // -20 points per critical event
        score -= highSeverityEvents * 10; // -10 points per high severity event

        // Bonus for successful operations

        var successCount = events.Count(e => e.EventType == HealthEventType.Success);
        score += Math.Min(20, successCount * 0.1); // Up to 20 bonus points


        return Math.Max(0, Math.Min(100, score));
    }

    private static List<string> PredictPotentialIssues(IReadOnlyList<HealthEvent> events)
    {
        var predictions = new List<string>();

        // Predict based on error trends
        var recentErrors = events
            .Where(e => e.EventType == HealthEventType.Error && e.Timestamp > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)))
            .ToList();

        if (recentErrors.Count > 10)
        {
            predictions.Add("High recent error rate may lead to system instability within the next hour.");
        }

        // Predict based on memory pressure trends
        var recentMemoryEvents = events
            .Where(e => e.EventType == HealthEventType.MemoryPressure && e.Timestamp > DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30)))
            .ToList();

        var highPressureEvents = recentMemoryEvents.Count(e =>

            e.Data.ContainsKey("pressure_level") &&

            Enum.Parse<MemoryPressureLevel>(e.Data["pressure_level"]?.ToString() ?? "Low") >= MemoryPressureLevel.High);

        if (highPressureEvents > recentMemoryEvents.Count * 0.7)
        {
            predictions.Add("Sustained high memory pressure may lead to allocation failures or system slowdown.");
        }

        return predictions;
    }

    private static string CalculatePerformanceTrend(IReadOnlyList<double> durations)
    {
        if (durations.Count < 2)
        {
            return "insufficient_data";
        }


        var firstHalf = durations.Take(durations.Count / 2).Average();
        var secondHalf = durations.Skip(durations.Count / 2).Average();

        var change = (secondHalf - firstHalf) / firstHalf;

        return change switch
        {
            > 0.1 => "degrading",
            < -0.1 => "improving",
            _ => "stable"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Generate final health report
            try
            {
                var finalReport = GetDetailedHealthReport();
                _logger.LogInformation("Metal health monitor disposed - Final health status: {Health}, Total events: {Events}",
                    finalReport.OverallHealth, finalReport.RecentEvents.Count);
            }
            catch
            {
                // Suppress exceptions during disposal
            }

            _healthCheckTimer?.Dispose();
            _anomalyDetectionTimer?.Dispose();

            _logger.LogDebug("Metal health monitor disposed");
        }
    }
}