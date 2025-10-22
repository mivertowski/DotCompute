// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.Metal.Execution;

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Threshold monitoring and alerting system for Metal backend
/// </summary>
public sealed class MetalAlertsManager : IDisposable
{
    private readonly ILogger<MetalAlertsManager> _logger;
    private readonly MetalAlertsOptions _options;
    private readonly ConcurrentDictionary<string, Alert> _activeAlerts;
    private readonly ConcurrentDictionary<string, AlertRule> _alertRules;
    private readonly ConcurrentDictionary<string, AlertHistory> _alertHistory;
    private readonly Timer? _evaluationTimer;
    private readonly Timer? _cleanupTimer;
    private volatile bool _disposed;

    public MetalAlertsManager(
        ILogger<MetalAlertsManager> logger,
        MetalAlertsOptions options)
    {
        _logger = logger;
        _options = options;
        _activeAlerts = new ConcurrentDictionary<string, Alert>();
        _alertRules = new ConcurrentDictionary<string, AlertRule>();
        _alertHistory = new ConcurrentDictionary<string, AlertHistory>();

        InitializeDefaultAlertRules();

        if (_options.EvaluationInterval > TimeSpan.Zero)
        {
            _evaluationTimer = new Timer(EvaluateAlerts, null, _options.EvaluationInterval, _options.EvaluationInterval);
        }

        if (_options.CleanupInterval > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(CleanupAlerts, null, _options.CleanupInterval, _options.CleanupInterval);
        }

        _logger.LogInformation("Metal alerts manager initialized with {RuleCount} alert rules, evaluation interval: {EvaluationInterval}",
            _alertRules.Count, _options.EvaluationInterval);
    }

    /// <summary>
    /// Checks for memory allocation failure and potentially triggers an alert
    /// </summary>
    public void CheckMemoryAllocationFailure(long sizeBytes)
    {
        if (_disposed)
        {
            return;
        }


        var alertKey = "memory_allocation_failure";
        var threshold = _options.MemoryAllocationFailureThreshold;

        // Increment failure counter
        var history = _alertHistory.GetOrAdd(alertKey, _ => new AlertHistory(alertKey));
        history.RecordEvent(DateTimeOffset.UtcNow, new Dictionary<string, object>
        {
            ["size_bytes"] = sizeBytes,
            ["size_mb"] = sizeBytes / (1024.0 * 1024.0)
        });

        // Check if we've exceeded the threshold
        var recentFailures = history.GetEventsInWindow(TimeSpan.FromMinutes(5));


        if (recentFailures.Count >= threshold.MaxFailuresPerWindow)
        {
            TriggerAlert(new Alert
            {
                Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                RuleId = alertKey,
                Severity = AlertSeverity.High,
                Title = "High Memory Allocation Failure Rate",
                Description = $"{recentFailures.Count} memory allocation failures in the last 5 minutes. Last failure: {FormatBytes(sizeBytes)}",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["failure_count"] = recentFailures.Count,
                    ["window_minutes"] = 5,
                    ["last_failure_size_bytes"] = sizeBytes,
                    ["total_failed_bytes"] = recentFailures.Sum(e => (long)(e.Properties?.GetValueOrDefault("size_bytes") ?? 0L))
                },
                RecommendedActions =
                [
                    "Check available system memory",
                    "Reduce allocation sizes",
                    "Implement memory pooling",
                    "Check for memory leaks"
                ]
            });
        }
    }

    /// <summary>
    /// Checks for kernel execution failure and potentially triggers an alert
    /// </summary>
    public void CheckKernelExecutionFailure(string kernelName, TimeSpan duration)
    {
        if (_disposed)
        {
            return;
        }


        var alertKey = $"kernel_execution_failure_{kernelName}";
        var history = _alertHistory.GetOrAdd(alertKey, _ => new AlertHistory(alertKey));


        history.RecordEvent(DateTimeOffset.UtcNow, new Dictionary<string, object>
        {
            ["kernel_name"] = kernelName,
            ["duration_ms"] = duration.TotalMilliseconds
        });

        var recentFailures = history.GetEventsInWindow(TimeSpan.FromMinutes(10));
        var threshold = _options.KernelExecutionFailureThreshold;


        if (recentFailures.Count >= threshold.MaxFailuresPerWindow)
        {
            TriggerAlert(new Alert
            {
                Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                RuleId = alertKey,
                Severity = AlertSeverity.High,
                Title = $"High Kernel Execution Failure Rate: {kernelName}",
                Description = $"Kernel '{kernelName}' has failed {recentFailures.Count} times in the last 10 minutes",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["kernel_name"] = kernelName,
                    ["failure_count"] = recentFailures.Count,
                    ["window_minutes"] = 10,
                    ["average_duration_ms"] = recentFailures.Average(e => (double)(e.Properties?.GetValueOrDefault("duration_ms") ?? 0.0))
                },
                RecommendedActions =
                [
                    $"Review kernel '{kernelName}' implementation",
                    "Check kernel compilation errors",
                    "Verify input data validity",
                    "Monitor GPU device health"
                ]
            });
        }
    }

    /// <summary>
    /// Checks for slow operations and potentially triggers an alert
    /// </summary>
    public void CheckSlowOperation(string operationName, TimeSpan duration)
    {
        if (_disposed)
        {
            return;
        }


        if (duration.TotalMilliseconds <= _options.SlowOperationThresholdMs)
        {
            return;
        }


        var alertKey = $"slow_operation_{operationName}";
        var history = _alertHistory.GetOrAdd(alertKey, _ => new AlertHistory(alertKey));


        history.RecordEvent(DateTimeOffset.UtcNow, new Dictionary<string, object>
        {
            ["operation_name"] = operationName,
            ["duration_ms"] = duration.TotalMilliseconds,
            ["threshold_ms"] = _options.SlowOperationThresholdMs
        });

        var recentSlowOps = history.GetEventsInWindow(TimeSpan.FromMinutes(15));


        if (recentSlowOps.Count >= _options.SlowOperationAlertThreshold)
        {
            var avgDuration = recentSlowOps.Average(e => (double)(e.Properties?.GetValueOrDefault("duration_ms") ?? 0.0));


            TriggerAlert(new Alert
            {
                Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                RuleId = alertKey,
                Severity = AlertSeverity.Medium,
                Title = $"Performance Degradation: {operationName}",
                Description = $"Operation '{operationName}' has been consistently slow. {recentSlowOps.Count} slow executions in 15 minutes (avg: {avgDuration:F1}ms)",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["operation_name"] = operationName,
                    ["slow_operation_count"] = recentSlowOps.Count,
                    ["average_duration_ms"] = avgDuration,
                    ["threshold_ms"] = _options.SlowOperationThresholdMs,
                    ["window_minutes"] = 15
                },
                RecommendedActions =
                [
                    "Profile the operation for bottlenecks",
                    "Check system resource utilization",
                    "Consider operation optimization",
                    "Verify GPU is not thermal throttling"
                ]
            });
        }
    }

    /// <summary>
    /// Checks for high GPU utilization and potentially triggers an alert
    /// </summary>
    public void CheckHighGpuUtilization(double utilizationPercentage)
    {
        if (_disposed)
        {
            return;
        }


        if (utilizationPercentage <= _options.HighGpuUtilizationThreshold)
        {
            return;
        }


        var alertKey = "high_gpu_utilization";
        var history = _alertHistory.GetOrAdd(alertKey, _ => new AlertHistory(alertKey));


        history.RecordEvent(DateTimeOffset.UtcNow, new Dictionary<string, object>
        {
            ["utilization_percentage"] = utilizationPercentage
        });

        var recentHighUtil = history.GetEventsInWindow(TimeSpan.FromMinutes(5));


        if (recentHighUtil.Count >= 10) // Sustained high utilization
        {
            var avgUtilization = recentHighUtil.Average(e => (double)(e.Properties?.GetValueOrDefault("utilization_percentage") ?? 0.0));


            var severity = avgUtilization > 95 ? AlertSeverity.High : AlertSeverity.Medium;


            TriggerAlert(new Alert
            {
                Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                RuleId = alertKey,
                Severity = severity,
                Title = "Sustained High GPU Utilization",
                Description = $"GPU utilization has been consistently high (avg: {avgUtilization:F1}%) for 5 minutes",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["average_utilization_percentage"] = avgUtilization,
                    ["current_utilization_percentage"] = utilizationPercentage,
                    ["threshold_percentage"] = _options.HighGpuUtilizationThreshold,
                    ["duration_minutes"] = 5
                },
                RecommendedActions =
                [
                    "Monitor for thermal throttling",
                    "Check for resource contention",
                    "Consider workload distribution",
                    "Verify adequate cooling"
                ]
            });
        }
    }

    /// <summary>
    /// Checks for high memory utilization and potentially triggers an alert
    /// </summary>
    public void CheckHighMemoryUtilization(double utilizationPercentage)
    {
        if (_disposed)
        {
            return;
        }


        if (utilizationPercentage <= _options.HighMemoryUtilizationThreshold)
        {
            return;
        }


        var alertKey = "high_memory_utilization";


        var severity = utilizationPercentage switch
        {
            > 95 => AlertSeverity.Critical,
            > 90 => AlertSeverity.High,
            _ => AlertSeverity.Medium
        };

        TriggerAlert(new Alert
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = alertKey,
            Severity = severity,
            Title = "High Memory Utilization",
            Description = $"Memory utilization is at {utilizationPercentage:F1}%",
            Timestamp = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                ["utilization_percentage"] = utilizationPercentage,
                ["threshold_percentage"] = _options.HighMemoryUtilizationThreshold
            },
            RecommendedActions =
            [
                "Free unused memory",
                "Implement memory pooling",
                "Reduce allocation sizes",
                "Check for memory leaks"
            ]
        });
    }

    /// <summary>
    /// Checks for high resource utilization and potentially triggers an alert
    /// </summary>
    public void CheckHighResourceUtilization(ResourceType resourceType, double utilizationPercentage)
    {
        if (_disposed)
        {
            return;
        }


        if (utilizationPercentage <= _options.HighResourceUtilizationThreshold)
        {
            return;
        }


        var alertKey = $"high_resource_utilization_{resourceType}";


        var severity = utilizationPercentage switch
        {
            > 95 => AlertSeverity.Critical,
            > 85 => AlertSeverity.High,
            _ => AlertSeverity.Medium
        };

        TriggerAlert(new Alert
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = alertKey,
            Severity = severity,
            Title = $"High {resourceType} Utilization",
            Description = $"{resourceType} utilization is at {utilizationPercentage:F1}%",
            Timestamp = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                ["resource_type"] = resourceType.ToString(),
                ["utilization_percentage"] = utilizationPercentage,
                ["threshold_percentage"] = _options.HighResourceUtilizationThreshold
            },
            RecommendedActions = GetResourceUtilizationRecommendations(resourceType)
        });
    }

    /// <summary>
    /// Checks for high memory pressure and potentially triggers an alert
    /// </summary>
    public void CheckHighMemoryPressure(MemoryPressureLevel level, double percentage)
    {
        if (_disposed)
        {
            return;
        }


        var alertKey = "high_memory_pressure";


        var severity = level switch
        {
            MemoryPressureLevel.Critical => AlertSeverity.Critical,
            MemoryPressureLevel.High => AlertSeverity.High,
            _ => AlertSeverity.Medium
        };

        TriggerAlert(new Alert
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = alertKey,
            Severity = severity,
            Title = $"High Memory Pressure: {level}",
            Description = $"System is experiencing {level.ToString().ToLowerInvariant()} memory pressure ({percentage:F1}%)",
            Timestamp = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, object>
            {
                ["pressure_level"] = level.ToString(),
                ["pressure_percentage"] = percentage
            },
            RecommendedActions =
            [
                "Trigger garbage collection",
                "Free cached resources",
                "Reduce active allocations",
                "Monitor for memory leaks",
                "Consider increasing available memory"
            ]
        });
    }

    /// <summary>
    /// Checks for high error rates and potentially triggers an alert
    /// </summary>
    public void CheckErrorRate(MetalError error)
    {
        if (_disposed)
        {
            return;
        }


        var alertKey = $"error_rate_{error}";
        var history = _alertHistory.GetOrAdd(alertKey, _ => new AlertHistory(alertKey));


        history.RecordEvent(DateTimeOffset.UtcNow, new Dictionary<string, object>
        {
            ["error_code"] = error.ToString()
        });

        var recentErrors = history.GetEventsInWindow(TimeSpan.FromMinutes(_options.ErrorRateWindowMinutes));


        if (recentErrors.Count >= _options.ErrorRateAlertThreshold)
        {
            var severity = error switch
            {
                MetalError.DeviceLost => AlertSeverity.Critical,
                MetalError.OutOfMemory or MetalError.DeviceUnavailable => AlertSeverity.High,
                _ => AlertSeverity.Medium
            };

            TriggerAlert(new Alert
            {
                Id = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                RuleId = alertKey,
                Severity = severity,
                Title = $"High Error Rate: {error}",
                Description = $"Error '{error}' has occurred {recentErrors.Count} times in {_options.ErrorRateWindowMinutes} minutes",
                Timestamp = DateTimeOffset.UtcNow,
                Properties = new Dictionary<string, object>
                {
                    ["error_code"] = error.ToString(),
                    ["error_count"] = recentErrors.Count,
                    ["window_minutes"] = _options.ErrorRateWindowMinutes,
                    ["threshold"] = _options.ErrorRateAlertThreshold
                },
                RecommendedActions = GetErrorRecommendations(error)
            });
        }
    }

    /// <summary>
    /// Gets all currently active alerts
    /// </summary>
    public List<Alert> GetActivAlerts() => [.. _activeAlerts.Values];

    /// <summary>
    /// Evaluates active alerts against current telemetry data
    /// </summary>
    public void EvaluateActiveAlerts(MetalTelemetrySnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }


        foreach (var alert in _activeAlerts.Values.ToList())
        {
            try
            {
                // Check if alert conditions are still met
                var shouldResolve = ShouldResolveAlert(alert, snapshot);


                if (shouldResolve)
                {
                    ResolveAlert(alert.Id, "Conditions no longer met");
                }
                else
                {
                    // Update alert with current data
                    UpdateAlert(alert, snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating alert: {AlertId}", alert.Id);
            }
        }
    }

    private void InitializeDefaultAlertRules()
    {
        var defaultRules = new[]
        {
            new AlertRule
            {
                Id = "memory_allocation_failure",
                Name = "Memory Allocation Failures",
                Description = "Triggers when memory allocation failure rate is high",
                Severity = AlertSeverity.High,
                Enabled = true
            },
            new AlertRule
            {
                Id = "high_gpu_utilization",
                Name = "High GPU Utilization",
                Description = "Triggers when GPU utilization is consistently high",
                Severity = AlertSeverity.Medium,
                Enabled = true
            },
            new AlertRule
            {
                Id = "high_memory_utilization",
                Name = "High Memory Utilization",

                Description = "Triggers when memory utilization exceeds threshold",
                Severity = AlertSeverity.High,
                Enabled = true
            },
            new AlertRule
            {
                Id = "high_error_rate",
                Name = "High Error Rate",
                Description = "Triggers when error rate exceeds acceptable threshold",
                Severity = AlertSeverity.High,
                Enabled = true
            }
        };

        foreach (var rule in defaultRules)
        {
            _alertRules[rule.Id] = rule;
        }
    }

    private void TriggerAlert(Alert alert)
    {
        // Check if we already have an active alert for this rule
        var existingAlert = _activeAlerts.Values.FirstOrDefault(a => a.RuleId == alert.RuleId);


        if (existingAlert != null)
        {
            // Update existing alert
            existingAlert.LastOccurrence = alert.Timestamp;
            existingAlert.OccurrenceCount++;


            if (existingAlert.Properties != null && alert.Properties != null)
            {
                foreach (var property in alert.Properties)
                {
                    existingAlert.Properties[property.Key] = property.Value;
                }
            }


            _logger.LogWarning("Alert updated: {Title} (#{OccurrenceCount})", alert.Title, existingAlert.OccurrenceCount);
        }
        else
        {
            // Create new alert
            alert.OccurrenceCount = 1;
            alert.LastOccurrence = alert.Timestamp;
            _activeAlerts[alert.Id] = alert;


            _logger.LogWarning("Alert triggered: {Title} - {Description}", alert.Title, alert.Description);

            // Send notifications if configured

            if (_options.EnableNotifications)
            {
                _ = Task.Run(() => SendNotificationAsync(alert));
            }
        }
    }

    private void ResolveAlert(string alertId, string resolution)
    {
        if (_activeAlerts.TryRemove(alertId, out var alert))
        {
            alert.ResolvedAt = DateTimeOffset.UtcNow;
            alert.Resolution = resolution;


            _logger.LogInformation("Alert resolved: {Title} - {Resolution}", alert.Title, resolution);

            // Archive resolved alert
            _ = $"{alert.RuleId}_resolved_{DateTimeOffset.UtcNow:yyyyMMdd}";
            // In production, you'd store this in persistent storage
        }
    }

    private static void UpdateAlert(Alert alert, MetalTelemetrySnapshot snapshot)
    {
        // Update alert with current telemetry data
        alert.LastOccurrence = DateTimeOffset.UtcNow;

        // Add current snapshot data to alert properties

        if (alert.Properties == null)
        {
            alert.Properties = [];
        }


        alert.Properties["last_update"] = DateTimeOffset.UtcNow;
        alert.Properties["total_operations"] = snapshot.TotalOperations;
        alert.Properties["error_rate"] = snapshot.ErrorRate;
    }

    private bool ShouldResolveAlert(Alert alert, MetalTelemetrySnapshot snapshot)
    {
        // Simple resolution logic - would be more sophisticated in production
        return alert.RuleId switch
        {
            "high_memory_utilization" => snapshot.ResourceMetrics.Values
                .Where(r => r.ResourceName.Contains("memory", StringComparison.OrdinalIgnoreCase))
                .All(r => r.UtilizationPercentage < _options.HighMemoryUtilizationThreshold * 0.8),
            "high_error_rate" => snapshot.ErrorRate < _options.ErrorRateAlertThreshold * 0.5,
            _ => false
        };
    }

    private async Task SendNotificationAsync(Alert alert)
    {
        try
        {
            foreach (var endpoint in _options.NotificationEndpoints)
            {
                await SendNotificationToEndpointAsync(endpoint, alert);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification for alert: {AlertId}", alert.Id);
        }
    }

    private async Task SendNotificationToEndpointAsync(string endpoint, Alert alert)
    {
        // Placeholder for notification sending
        // Would implement integrations with:
        // - Slack
        // - Microsoft Teams
        // - Email
        // - PagerDuty
        // - Custom webhooks


        await Task.Delay(100); // Simulate async notification
        _logger.LogDebug("Notification sent to {Endpoint} for alert: {AlertTitle}", endpoint, alert.Title);
    }

    private static string[] GetResourceUtilizationRecommendations(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Memory =>
            [
                "Implement memory pooling",
                "Reduce allocation sizes",

                "Free unused resources",
                "Check for memory leaks"
            ],
            ResourceType.GPU =>
            [
                "Optimize kernel execution",
                "Reduce parallel workload",
                "Check for thermal throttling",
                "Balance workload distribution"
            ],
            _ =>
            [
                "Monitor resource usage patterns",
                "Optimize resource allocation",
                "Consider scaling resources"
            ]
        };
    }

    private static string[] GetErrorRecommendations(MetalError error)
    {
        return error switch
        {
            MetalError.OutOfMemory =>
            [
                "Reduce memory allocation sizes",
                "Implement memory pooling",
                "Free unused resources",
                "Check available system memory"
            ],
            MetalError.DeviceLost =>
            [
                "Check GPU driver status",
                "Monitor system stability",
                "Verify hardware health",
                "Restart application if necessary"
            ],
            MetalError.CompilationError =>
            [
                "Review shader source code",
                "Check Metal language version compatibility",
                "Verify function signatures",
                "Update Metal development tools"
            ],
            _ =>
            [
                "Review operation parameters",
                "Check system logs for details",
                "Monitor for patterns",
                "Consider retry logic"
            ]
        };
    }

    private void EvaluateAlerts(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Periodic alert evaluation
            var activeAlertCount = _activeAlerts.Count;
            var ruleCount = _alertRules.Count;


            _logger.LogTrace("Alert evaluation completed: {ActiveAlerts} active alerts, {Rules} rules configured",
                activeAlertCount, ruleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during alert evaluation");
        }
    }

    private void CleanupAlerts(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            var cutoffTime = DateTimeOffset.UtcNow.Subtract(_options.AlertRetentionPeriod);

            // Clean up old alert history

            var expiredHistoryKeys = _alertHistory
                .Where(kvp => kvp.Value.GetEventsInWindow(TimeSpan.MaxValue).All(e => e.Timestamp < cutoffTime))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredHistoryKeys)
            {
                _ = _alertHistory.TryRemove(key, out _);
            }

            if (expiredHistoryKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired alert history entries", expiredHistoryKeys.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during alert cleanup");
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            _evaluationTimer?.Dispose();
            _cleanupTimer?.Dispose();

            var activeAlertCount = _activeAlerts.Count;


            _logger.LogInformation("Metal alerts manager disposed with {ActiveAlerts} active alerts", activeAlertCount);
        }
    }
}