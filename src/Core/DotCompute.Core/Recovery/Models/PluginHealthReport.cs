// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Models;

/// <summary>
/// Represents a comprehensive health report for the plugin system.
/// </summary>
public class PluginHealthReport
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall plugin health status.
    /// </summary>
    public PluginHealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the overall health score (0.0 to 1.0).
    /// </summary>
    public double OverallHealth { get; set; }

    /// <summary>
    /// Gets or sets the total number of loaded plugins.
    /// </summary>
    public int TotalPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of healthy plugins.
    /// </summary>
    public int HealthyPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of plugins with warnings.
    /// </summary>
    public int PluginsWithWarnings { get; set; }

    /// <summary>
    /// Gets or sets the number of failed plugins.
    /// </summary>
    public int FailedPlugins { get; set; }

    /// <summary>
    /// Gets or sets the number of disabled plugins.
    /// </summary>
    public int DisabledPlugins { get; set; }

    /// <summary>
    /// Gets or sets the health status of individual plugins.
    /// </summary>
    public Dictionary<string, PluginHealth> PluginStatuses { get; init; } = [];

    /// <summary>
    /// Gets or sets the timestamp when this report was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets any critical issues detected.
    /// </summary>
    public IList<string> CriticalIssues { get; init; } = [];

    /// <summary>
    /// Gets or sets warning messages.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets or sets the component identifier for health tracking.
    /// </summary>
    public string Component { get; set; } = "Plugins";
}

/// <summary>
/// Represents the health status of an individual plugin.
/// </summary>
public class PluginHealth
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin name.
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plugin status.
    /// </summary>
    public PluginHealthStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the health score (0.0 to 1.0).
    /// </summary>
    public double HealthScore { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin is loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Gets or sets whether the plugin is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the last activity timestamp.
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }

    /// <summary>
    /// Gets or sets the plugin initialization time.
    /// </summary>
    public TimeSpan InitializationTime { get; set; }

    /// <summary>
    /// Gets or sets the memory usage in bytes.
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the number of active operations.
    /// </summary>
    public int ActiveOperations { get; set; }

    /// <summary>
    /// Gets or sets plugin-specific metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Represents the health status of a plugin.
/// </summary>
public enum PluginHealthStatus
{
    /// <summary>
    /// Plugin is healthy and operational.
    /// </summary>
    Healthy,

    /// <summary>
    /// Plugin has warnings but is operational.
    /// </summary>
    Warning,

    /// <summary>
    /// Plugin has critical issues.
    /// </summary>
    Critical,

    /// <summary>
    /// Plugin has failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Plugin is disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Plugin status is unknown.
    /// </summary>
    Unknown
}
