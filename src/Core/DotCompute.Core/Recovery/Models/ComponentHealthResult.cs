// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents the health status result of a system component
/// </summary>
public class ComponentHealthResult
{
    /// <summary>
    /// Gets or sets the component name
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall health status
    /// </summary>
    public HealthStatus Status { get; set; } = HealthStatus.Unknown;

    /// <summary>
    /// Gets or sets the health score (0-100)
    /// </summary>
    public double HealthScore { get; set; }

    /// <summary>
    /// Gets or sets the list of detected issues
    /// </summary>
    public List<HealthIssue> Issues { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of health metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the health check
    /// </summary>
    public DateTimeOffset CheckTime { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the duration of the health check
    /// </summary>
    public TimeSpan CheckDuration { get; set; }

    /// <summary>
    /// Gets or sets recommended actions to improve health
    /// </summary>
    public List<string> RecommendedActions { get; set; } = new();

    /// <summary>
    /// Gets or sets additional diagnostic information
    /// </summary>
    public Dictionary<string, string> DiagnosticInfo { get; set; } = new();
}

/// <summary>
/// Represents the health status of a component
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Health status is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Component is healthy and functioning normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Component has minor issues but is still functional
    /// </summary>
    Warning,

    /// <summary>
    /// Component has significant issues affecting functionality
    /// </summary>
    Unhealthy,

    /// <summary>
    /// Component is critically unhealthy and may fail
    /// </summary>
    Critical,

    /// <summary>
    /// Component has failed and is not functional
    /// </summary>
    Failed
}

/// <summary>
/// Represents a specific health issue detected in a component
/// </summary>
public class HealthIssue
{
    /// <summary>
    /// Gets or sets the issue identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issue description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the severity of the issue
    /// </summary>
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;

    /// <summary>
    /// Gets or sets the category of the issue
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the detection timestamp
    /// </summary>
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the issue can be automatically resolved
    /// </summary>
    public bool CanAutoResolve { get; set; }

    /// <summary>
    /// Gets or sets suggested resolution steps
    /// </summary>
    public List<string> SuggestedResolution { get; set; } = new();
}

/// <summary>
/// Represents the severity level of a health issue
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// Low severity issue
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity issue
    /// </summary>
    Medium,

    /// <summary>
    /// High severity issue
    /// </summary>
    High,

    /// <summary>
    /// Critical severity issue
    /// </summary>
    Critical
}