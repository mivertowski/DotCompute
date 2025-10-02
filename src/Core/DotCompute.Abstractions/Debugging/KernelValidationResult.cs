// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Result of executing a kernel on a specific backend.
/// </summary>
public record KernelExecutionResult
{
    public string KernelName { get; init; } = string.Empty;
    public string BackendType { get; init; } = string.Empty;
    public object? Result { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsed { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = [];
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a validation issue found during kernel testing.
/// </summary>
public class DebugValidationIssue
{
    public ValidationSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public string BackendAffected { get; init; } = string.Empty;
    public string? Suggestion { get; init; }
    public Dictionary<string, object> Details { get; init; } = [];
    public string? Context { get; init; }

    /// <summary>
    /// Creates a new debug validation issue.
    /// </summary>
    public DebugValidationIssue(ValidationSeverity severity, string message, string backendAffected = "", string? suggestion = null)
    {
        Severity = severity;
        Message = message;
        BackendAffected = backendAffected;
        Suggestion = suggestion;
    }

    /// <summary>
    /// Parameterless constructor for object initialization.
    /// </summary>
    public DebugValidationIssue() { }
}

/// <summary>
/// Severity levels for comparison issues.
/// </summary>
public enum ComparisonSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Represents an issue found during result comparison.
/// </summary>
public class ComparisonIssue
{
    public ComparisonSeverity Severity { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Represents a difference between execution results.
/// </summary>
public class ResultDifference
{
    public string Location { get; init; } = string.Empty;
    public object ExpectedValue { get; init; } = new();
    public object ActualValue { get; init; } = new();
    public float Difference { get; init; }
    public string[] BackendsInvolved { get; init; } = Array.Empty<string>();

    /// <summary>
    /// First backend involved in the comparison.
    /// </summary>
    public string Backend1 { get; init; } = string.Empty;

    /// <summary>
    /// Second backend involved in the comparison.
    /// </summary>
    public string Backend2 { get; init; } = string.Empty;
}
