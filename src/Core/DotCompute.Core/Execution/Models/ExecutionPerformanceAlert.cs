// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution.Models;

/// <summary>
/// Represents a performance alert for execution monitoring.
/// </summary>
public class ExecutionPerformanceAlert
{
    /// <summary>
    /// Gets or sets the unique alert identifier.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the alert type.
    /// </summary>
    public required ExecutionPerformanceAlertType Type { get; set; }

    /// <summary>
    /// Gets or sets the alert severity (0.0 to 1.0).
    /// </summary>
    public required double Severity { get; set; }

    /// <summary>
    /// Gets or sets the alert message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the detailed description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets when this alert was triggered.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the execution ID that triggered this alert.
    /// </summary>
    public Guid? ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the device ID associated with this alert.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the kernel name associated with this alert.
    /// </summary>
    public string? KernelName { get; set; }

    /// <summary>
    /// Gets or sets additional alert metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets recommended actions to address this alert.
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this alert has been acknowledged.
    /// </summary>
    public bool IsAcknowledged { get; set; }

    /// <summary>
    /// Gets or sets when this alert was acknowledged.
    /// </summary>
    public DateTimeOffset? AcknowledgedAt { get; set; }

    /// <summary>
    /// Gets or sets who acknowledged this alert.
    /// </summary>
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// Gets the alert priority based on severity.
    /// </summary>
    public AlertPriority Priority => Severity switch
    {
        >= 0.8 => AlertPriority.Critical,
        >= 0.6 => AlertPriority.High,
        >= 0.4 => AlertPriority.Medium,
        >= 0.2 => AlertPriority.Low,
        _ => AlertPriority.Info
    };

    /// <summary>
    /// Acknowledges this alert.
    /// </summary>
    /// <param name="acknowledgedBy">Who acknowledged the alert.</param>
    public void Acknowledge(string acknowledgedBy)
    {
        IsAcknowledged = true;
        AcknowledgedAt = DateTimeOffset.UtcNow;
        AcknowledgedBy = acknowledgedBy;
    }
}

/// <summary>
/// Defines alert priority levels.
/// </summary>
public enum AlertPriority
{
    /// <summary>
    /// Informational alert.
    /// </summary>
    Info,

    /// <summary>
    /// Low priority alert.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority alert.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority alert.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority alert.
    /// </summary>
    Critical
}