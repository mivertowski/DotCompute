// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Loaders;

namespace DotCompute.Plugins.Security;

/// <summary>
/// Security context for plugin loading and execution with comprehensive restrictions.
/// </summary>
public class PluginSecurityContext
{
    /// <summary>
    /// Gets or sets the path to the assembly being loaded.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets when the plugin was loaded.
    /// </summary>
    public DateTimeOffset LoadTime { get; set; }

    /// <summary>
    /// Gets or sets the security policy applied to this plugin.
    /// </summary>
    public SecurityPolicy? SecurityPolicy { get; set; }

    /// <summary>
    /// Gets or sets the allowed permissions for this plugin.
    /// </summary>
    public HashSet<string> AllowedPermissions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the plugin is running in a restricted environment.
    /// </summary>
    public bool RestrictedEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory usage allowed for this plugin in bytes.
    /// </summary>
    public long MaxMemoryUsage { get; set; } = 100 * 1024 * 1024; // 100MB default

    /// <summary>
    /// Gets or sets the maximum execution time allowed for this plugin.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the isolation level for this plugin.
    /// </summary>
    public PluginIsolationLevel IsolationLevel { get; set; } = PluginIsolationLevel.High;

    /// <summary>
    /// Gets or sets the security violations detected for this plugin.
    /// </summary>
    public List<SecurityViolation> Violations { get; set; } = new();

    /// <summary>
    /// Gets whether this plugin has any security violations.
    /// </summary>
    public bool HasViolations => Violations.Count > 0;

    /// <summary>
    /// Gets whether this plugin has critical security violations.
    /// </summary>
    public bool HasCriticalViolations => Violations.Any(v => v.Severity == ViolationSeverity.Critical);

    /// <summary>
    /// Records a security violation for this plugin.
    /// </summary>
    public void RecordViolation(SecurityViolation violation)
    {
        ArgumentNullException.ThrowIfNull(violation);
        violation.Timestamp = DateTimeOffset.UtcNow;
        Violations.Add(violation);
    }

    /// <summary>
    /// Records a security violation with the specified details.
    /// </summary>
    public void RecordViolation(string description, ViolationSeverity severity, string? details = null)
    {
        var violation = new SecurityViolation
        {
            Description = description,
            Severity = severity,
            Details = details,
            Timestamp = DateTimeOffset.UtcNow
        };
        
        Violations.Add(violation);
    }

    /// <summary>
    /// Gets violations of a specific severity level.
    /// </summary>
    public IEnumerable<SecurityViolation> GetViolationsBySeverity(ViolationSeverity severity)
    {
        return Violations.Where(v => v.Severity == severity);
    }

    /// <summary>
    /// Clears all recorded violations.
    /// </summary>
    public void ClearViolations()
    {
        Violations.Clear();
    }
}

/// <summary>
/// Isolation levels for plugin execution.
/// </summary>
public enum PluginIsolationLevel
{
    /// <summary>
    /// No isolation - plugin runs with full permissions.
    /// </summary>
    None = 0,

    /// <summary>
    /// Low isolation - basic restrictions applied.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium isolation - moderate restrictions with sandboxing.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High isolation - strict restrictions with comprehensive sandboxing.
    /// </summary>
    High = 3,

    /// <summary>
    /// Maximum isolation - plugin runs in completely isolated environment.
    /// </summary>
    Maximum = 4
}

/// <summary>
/// Represents a security violation detected during plugin execution.
/// </summary>
public class SecurityViolation
{
    /// <summary>
    /// Gets or sets the description of the violation.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the severity of the violation.
    /// </summary>
    public ViolationSeverity Severity { get; set; }

    /// <summary>
    /// Gets or sets additional details about the violation.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets when the violation was detected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the source of the violation (e.g., method name, code location).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the stack trace when the violation occurred.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets whether this violation requires immediate termination.
    /// </summary>
    public bool RequiresTermination { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the violation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Severity levels for security violations.
/// </summary>
public enum ViolationSeverity
{
    /// <summary>
    /// Informational violation - no immediate action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Low severity violation - monitoring recommended.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Medium severity violation - investigation recommended.
    /// </summary>
    Medium = 2,

    /// <summary>
    /// High severity violation - immediate attention required.
    /// </summary>
    High = 3,

    /// <summary>
    /// Critical violation - immediate termination required.
    /// </summary>
    Critical = 4
}
