// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal alerts
/// </summary>
public sealed class MetalAlertsOptions
{
    /// <summary>
    /// Gets or sets the alert evaluation interval
    /// </summary>
    public TimeSpan EvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the cleanup interval
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the alert retention period
    /// </summary>
    public TimeSpan AlertRetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets whether to enable notifications
    /// </summary>
    public bool EnableNotifications { get; set; }


    /// <summary>
    /// Gets or sets the notification endpoints
    /// </summary>
    public IList<string> NotificationEndpoints { get; } = [];

    /// <summary>
    /// Gets or sets the memory allocation failure threshold
    /// </summary>
    public ThresholdConfiguration MemoryAllocationFailureThreshold { get; set; } = new() { MaxFailuresPerWindow = 3 };

    /// <summary>
    /// Gets or sets the kernel execution failure threshold
    /// </summary>
    public ThresholdConfiguration KernelExecutionFailureThreshold { get; set; } = new() { MaxFailuresPerWindow = 5 };

    /// <summary>
    /// Gets or sets the slow operation alert threshold
    /// </summary>
    public int SlowOperationAlertThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the slow operation threshold in milliseconds
    /// </summary>
    public double SlowOperationThresholdMs { get; set; } = 100.0;

    /// <summary>
    /// Gets or sets the high GPU utilization threshold percentage
    /// </summary>
    public double HighGpuUtilizationThreshold { get; set; } = 90.0;

    /// <summary>
    /// Gets or sets the high memory utilization threshold percentage
    /// </summary>
    public double HighMemoryUtilizationThreshold { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the high resource utilization threshold percentage
    /// </summary>
    public double HighResourceUtilizationThreshold { get; set; } = 85.0;

    /// <summary>
    /// Gets or sets the error rate alert threshold
    /// </summary>
    public int ErrorRateAlertThreshold { get; set; } = 10;

    /// <summary>
    /// Gets or sets the error rate window in minutes
    /// </summary>
    public int ErrorRateWindowMinutes { get; set; } = 10;
}