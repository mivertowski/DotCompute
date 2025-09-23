// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using DotCompute.Abstractions.Types;
using DotCompute.Abstractions.Validation;
using ValidationIssue = DotCompute.Abstractions.ValidationIssue;

namespace DotCompute.Abstractions.Pipelines.Models;

/// <summary>
/// Result of pipeline stage validation operations.
/// Provides detailed information about validation success, errors, and warnings.
/// </summary>
public sealed class StageValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the overall validation severity.
    /// </summary>
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Info;

    /// <summary>
    /// Gets or sets the name of the stage that was validated.
    /// </summary>
    public string? StageName { get; set; }

    /// <summary>
    /// Gets or sets the type of the stage that was validated.
    /// </summary>
    public string? StageType { get; set; }

    /// <summary>
    /// Gets or sets validation errors that prevent execution.
    /// </summary>
    public IList<ValidationIssue> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets validation warnings that may affect performance or correctness.
    /// </summary>
    public IList<ValidationIssue> Warnings { get; set; } = [];

    /// <summary>
    /// Gets or sets informational messages about the validation.
    /// </summary>
    public IList<ValidationIssue> Information { get; set; } = [];

    /// <summary>
    /// Gets or sets validation suggestions for improvements.
    /// </summary>
    public IList<ValidationSuggestion> Suggestions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time when validation was performed.
    /// </summary>
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the duration of the validation process.
    /// </summary>
    public TimeSpan ValidationDuration { get; set; }

    /// <summary>
    /// Gets or sets the version of the validator used.
    /// </summary>
    public string? ValidatorVersion { get; set; }

    /// <summary>
    /// Gets or sets estimated resource requirements for the stage.
    /// </summary>
    public ResourceEstimate? ResourceEstimate { get; set; }

    /// <summary>
    /// Gets or sets estimated performance characteristics.
    /// </summary>
    public PerformanceEstimate? PerformanceEstimate { get; set; }

    /// <summary>
    /// Gets or sets compatibility information with different backends.
    /// </summary>
    public IDictionary<string, BackendCompatibility> BackendCompatibility { get; set; } =
        new Dictionary<string, BackendCompatibility>();

    /// <summary>
    /// Gets or sets additional metadata associated with the validation.
    /// </summary>
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="code">Error code</param>
    /// <param name="location">Location where the error occurred</param>
    public void AddError(string message, string? code = null, string? location = null)
    {
        Errors.Add(new ValidationIssue(
            code ?? "ERROR",
            message,
            ValidationSeverity.Error));

        if (Severity < ErrorSeverity.Error)
        {
            Severity = ErrorSeverity.Error;
        }

        IsValid = false;
    }

    /// <summary>
    /// Adds a validation warning.
    /// </summary>
    /// <param name="message">Warning message</param>
    /// <param name="code">Warning code</param>
    /// <param name="location">Location where the warning occurred</param>
    public void AddWarning(string message, string? code = null, string? location = null)
    {
        Warnings.Add(new ValidationIssue(
            code ?? "WARNING",
            message,
            ValidationSeverity.Warning));

        if (Severity < ErrorSeverity.Warning && Severity != ErrorSeverity.Error)
        {
            Severity = ErrorSeverity.Warning;
        }
    }

    /// <summary>
    /// Adds validation information.
    /// </summary>
    /// <param name="message">Information message</param>
    /// <param name="code">Information code</param>
    /// <param name="location">Location where the information applies</param>
    public void AddInformation(string message, string? code = null, string? location = null)
    {
        Information.Add(new ValidationIssue(
            code ?? "INFO",
            message,
            ValidationSeverity.Info));
    }

    /// <summary>
    /// Adds a validation suggestion.
    /// </summary>
    /// <param name="message">Suggestion message</param>
    /// <param name="category">Category of the suggestion</param>
    /// <param name="priority">Priority of the suggestion</param>
    public void AddSuggestion(string message, SuggestionCategory category = SuggestionCategory.Performance,
                             SuggestionPriority priority = SuggestionPriority.Medium)
    {
        Suggestions.Add(new ValidationSuggestion
        {
            Message = message,
            Category = category,
            Priority = priority
        });
    }

    /// <summary>
    /// Gets the total number of issues (errors + warnings).
    /// </summary>
    /// <returns>Total issue count</returns>
    public int GetTotalIssueCount()
    {
        return Errors.Count + Warnings.Count;
    }

    /// <summary>
    /// Gets whether the validation has any critical issues.
    /// </summary>
    /// <returns>True if there are critical errors</returns>
    public bool HasCriticalIssues()
    {
        return Errors.Any(e => e.Severity == ValidationSeverity.Error) ||
               Warnings.Any(w => w.Severity >= ValidationSeverity.Warning);
    }

    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    /// <param name="other">The other validation result to merge</param>
    public void Merge(StageValidationResult other)
    {
        foreach (var error in other.Errors)
        {
            Errors.Add(error);
        }


        foreach (var warning in other.Warnings)
        {
            Warnings.Add(warning);
        }


        foreach (var info in other.Information)
        {
            Information.Add(info);
        }

        foreach (var suggestion in other.Suggestions)
        {
            Suggestions.Add(suggestion);
        }

        // Update validity and severity
        if (!other.IsValid)
        {
            IsValid = false;
        }

        if (other.Severity > Severity)
        {
            Severity = other.Severity;
        }

        // Merge metadata
        foreach (var kvp in other.Metadata)
        {
            Metadata[kvp.Key] = kvp.Value;
        }

        // Merge backend compatibility
        foreach (var kvp in other.BackendCompatibility)
        {
            BackendCompatibility[kvp.Key] = kvp.Value;
        }

    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="stageName">Name of the validated stage</param>
    /// <returns>A successful validation result</returns>
    public static StageValidationResult Success(string stageName)
    {
        return new StageValidationResult
        {
            IsValid = true,
            StageName = stageName,
            Severity = ErrorSeverity.Info
        };
    }

    /// <summary>
    /// Creates a failed validation result with an error.
    /// </summary>
    /// <param name="stageName">Name of the validated stage</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="errorCode">Error code</param>
    /// <returns>A failed validation result</returns>
    public static StageValidationResult Failure(string stageName, string errorMessage, string? errorCode = null)
    {
        var result = new StageValidationResult
        {
            IsValid = false,
            StageName = stageName,
            Severity = ErrorSeverity.Error
        };

        result.AddError(errorMessage, errorCode);
        return result;
    }
}


/// <summary>
/// Represents a validation suggestion for improvement.
/// </summary>
public sealed class ValidationSuggestion
{
    /// <summary>
    /// Gets or sets the suggestion message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the category of the suggestion.
    /// </summary>
    public SuggestionCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the priority of the suggestion.
    /// </summary>
    public SuggestionPriority Priority { get; set; }

    /// <summary>
    /// Gets or sets the estimated impact of implementing the suggestion.
    /// </summary>
    public string? EstimatedImpact { get; set; }

    /// <summary>
    /// Gets or sets the effort required to implement the suggestion.
    /// </summary>
    public ImplementationEffort Effort { get; set; } = ImplementationEffort.Medium;
}

/// <summary>
/// Categories of validation suggestions.
/// </summary>
public enum SuggestionCategory
{
    /// <summary>
    /// Performance optimization suggestions.
    /// </summary>
    Performance,

    /// <summary>
    /// Memory usage optimization suggestions.
    /// </summary>
    Memory,

    /// <summary>
    /// Code quality improvement suggestions.
    /// </summary>
    Quality,

    /// <summary>
    /// Security enhancement suggestions.
    /// </summary>
    Security,

    /// <summary>
    /// Maintainability improvement suggestions.
    /// </summary>
    Maintainability,

    /// <summary>
    /// Compatibility enhancement suggestions.
    /// </summary>
    Compatibility,

    /// <summary>
    /// Configuration optimization suggestions.
    /// </summary>
    Configuration
}

/// <summary>
/// Priority levels for validation suggestions.
/// </summary>
public enum SuggestionPriority
{
    /// <summary>
    /// Low priority suggestion.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority suggestion.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority suggestion.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority suggestion.
    /// </summary>
    Critical
}

/// <summary>
/// Implementation effort levels for suggestions.
/// </summary>
public enum ImplementationEffort
{
    /// <summary>
    /// Low effort to implement.
    /// </summary>
    Low,

    /// <summary>
    /// Medium effort to implement.
    /// </summary>
    Medium,

    /// <summary>
    /// High effort to implement.
    /// </summary>
    High,

    /// <summary>
    /// Very high effort to implement.
    /// </summary>
    VeryHigh
}

/// <summary>
/// Resource usage estimates for a pipeline stage.
/// </summary>
public sealed class ResourceEstimate
{
    /// <summary>
    /// Gets or sets the estimated memory usage in bytes.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the estimated computation time.
    /// </summary>
    public TimeSpan EstimatedExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated CPU utilization percentage.
    /// </summary>
    public double EstimatedCpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the estimated GPU utilization percentage.
    /// </summary>
    public double EstimatedGpuUtilization { get; set; }

    /// <summary>
    /// Gets or sets the confidence level of the estimates (0-1).
    /// </summary>
    public double ConfidenceLevel { get; set; } = 0.5;
}

/// <summary>
/// Performance estimates for a pipeline stage.
/// </summary>
public sealed class PerformanceEstimate
{
    /// <summary>
    /// Gets or sets the estimated throughput in operations per second.
    /// </summary>
    public double EstimatedThroughput { get; set; }

    /// <summary>
    /// Gets or sets the estimated latency in milliseconds.
    /// </summary>
    public double EstimatedLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the estimated scalability factor.
    /// </summary>
    public double EstimatedScalability { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the confidence level of the estimates (0-1).
    /// </summary>
    public double ConfidenceLevel { get; set; } = 0.5;
}

/// <summary>
/// Backend compatibility information.
/// </summary>
public sealed class BackendCompatibility
{
    /// <summary>
    /// Gets or sets whether the stage is compatible with the backend.
    /// </summary>
    public bool IsCompatible { get; set; }

    /// <summary>
    /// Gets or sets the compatibility level.
    /// </summary>
    public CompatibilityLevel Level { get; set; }

    /// <summary>
    /// Gets or sets compatibility limitations or issues.
    /// </summary>
    public IList<string> Limitations { get; set; } = [];

    /// <summary>
    /// Gets or sets the estimated performance on this backend.
    /// </summary>
    public PerformanceEstimate? PerformanceEstimate { get; set; }
}

/// <summary>
/// Levels of backend compatibility.
/// </summary>
public enum CompatibilityLevel
{
    /// <summary>
    /// Not compatible.
    /// </summary>
    None,

    /// <summary>
    /// Basic compatibility with limitations.
    /// </summary>
    Basic,

    /// <summary>
    /// Good compatibility with minor limitations.
    /// </summary>
    Good,

    /// <summary>
    /// Full compatibility with optimal performance.
    /// </summary>
    Full
}