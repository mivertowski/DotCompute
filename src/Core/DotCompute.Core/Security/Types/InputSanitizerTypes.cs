// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Types;

/// <summary>
/// Configuration for input sanitization behavior and limits.
/// </summary>
/// <remarks>
/// <para>
/// Defines global limits, timeouts, and feature flags for the input sanitization system.
/// Use <see cref="Default"/> for recommended production settings.
/// </para>
/// </remarks>
public sealed class InputSanitizationConfiguration
{
    /// <summary>
    /// Gets the default sanitization configuration with recommended production settings.
    /// </summary>
    public static InputSanitizationConfiguration Default => new()
    {
        MaxInputLength = 10_000,
        MaxHighSeverityThreats = 0,
        MaxGlobalWorkSize = 1_000_000_000L,
        EnableDetailedLogging = false,
        CacheValidationResults = true
    };

    /// <summary>
    /// Gets or sets the maximum input length in characters.
    /// </summary>
    /// <remarks>
    /// Inputs exceeding this limit are rejected to prevent resource exhaustion.
    /// </remarks>
    public int MaxInputLength { get; init; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of high/critical severity threats allowed.
    /// </summary>
    /// <remarks>
    /// Default: 0 (zero tolerance for high-severity threats).
    /// </remarks>
    public int MaxHighSeverityThreats { get; init; }

    /// <summary>
    /// Gets or sets the maximum global work size for GPU kernels.
    /// </summary>
    /// <remarks>
    /// Default: 1 billion work items. Prevents excessive GPU resource allocation.
    /// </remarks>
    public long MaxGlobalWorkSize { get; init; } = 1_000_000_000L;

    /// <summary>
    /// Gets or sets whether detailed validation logging is enabled.
    /// </summary>
    public bool EnableDetailedLogging { get; init; }

    /// <summary>
    /// Gets or sets whether validation results should be cached for performance.
    /// </summary>
    public bool CacheValidationResults { get; init; } = true;

    /// <summary>
    /// Gets or sets the validation timeout duration.
    /// </summary>
    public TimeSpan ValidationTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public override string ToString()
        => $"MaxLength={MaxInputLength}, MaxThreats={MaxHighSeverityThreats}, DetailedLogging={EnableDetailedLogging}";
}

/// <summary>
/// Represents a detected security threat in input data.
/// </summary>
public sealed class InputThreat
{
    /// <summary>
    /// Gets the type of threat detected.
    /// </summary>
    public required ThreatType ThreatType { get; init; }

    /// <summary>
    /// Gets the severity level of this threat.
    /// </summary>
    public required ThreatSeverity Severity { get; init; }

    /// <summary>
    /// Gets a human-readable description of the threat.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the location where the threat was detected (e.g., character offset, field name).
    /// </summary>
    public required string Location { get; init; }

    /// <summary>
    /// Gets an optional recommendation for remediation.
    /// </summary>
    public string? Recommendation { get; init; }
}

/// <summary>
/// Result of input sanitization operation.
/// </summary>
public sealed class SanitizationResult
{
    /// <summary>
    /// Gets the original unsanitized input.
    /// </summary>
    public required string OriginalInput { get; init; }

    /// <summary>
    /// Gets the sanitized input (null if sanitization failed or input was rejected).
    /// </summary>
    public string? SanitizedInput { get; set; }

    /// <summary>
    /// Gets the context or purpose of this input validation.
    /// </summary>
    public required string Context { get; init; }

    /// <summary>
    /// Gets the sanitization strategy applied.
    /// </summary>
    public required SanitizationType SanitizationType { get; init; }

    /// <summary>
    /// Gets when processing started.
    /// </summary>
    public DateTimeOffset ProcessingStartTime { get; init; }

    /// <summary>
    /// Gets or sets when processing completed.
    /// </summary>
    public DateTimeOffset ProcessingEndTime { get; set; }

    /// <summary>
    /// Gets the total processing duration.
    /// </summary>
    public TimeSpan ProcessingDuration => ProcessingEndTime - ProcessingStartTime;

    /// <summary>
    /// Gets or sets whether the input is secure (no high/critical threats).
    /// </summary>
    public bool IsSecure { get; set; }

    /// <summary>
    /// Gets or sets whether the input is valid and usable.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of detected security threats.
    /// </summary>
    public IList<InputThreat> SecurityThreats { get; init; } = [];
}

/// <summary>
/// Result of kernel parameter validation.
/// </summary>
public sealed class ParameterValidationResult
{
    /// <summary>
    /// Gets the kernel name being validated.
    /// </summary>
    public required string KernelName { get; init; }

    /// <summary>
    /// Gets or sets whether all parameters are valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of parameter validation errors.
    /// </summary>
    public IList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Gets the dictionary of parameter validation details (parameter name → validation message).
    /// </summary>
    public Dictionary<string, string> ParameterDetails { get; init; } = [];

    /// <summary>
    /// Gets the dictionary of parameter validation results (parameter name → result).
    /// </summary>
    public Dictionary<string, ParameterResult> ParameterResults { get; init; } = [];

    /// <summary>
    /// Gets or sets the total number of parameters validated.
    /// </summary>
    public int TotalParameters { get; set; }

    /// <summary>
    /// Gets or sets the number of parameters validated.
    /// </summary>
    public int ParameterCount { get; set; }

    /// <summary>
    /// Gets or sets the number of invalid parameters (count).
    /// </summary>
    public int InvalidParameterCount { get; set; }

    /// <summary>
    /// Gets the list of invalid parameter names.
    /// </summary>
    public IList<string> InvalidParameters { get; init; } = [];

    /// <summary>
    /// Gets or sets whether there are invalid parameters.
    /// </summary>
    public bool HasInvalidParameters { get; set; }

    /// <summary>
    /// Gets the list of security threats detected.
    /// </summary>
    public IList<InputThreat> SecurityThreats { get; init; } = [];

    /// <summary>
    /// Gets or sets the validation start time.
    /// </summary>
    public DateTimeOffset ValidationStartTime { get; set; }

    /// <summary>
    /// Gets or sets the validation end time.
    /// </summary>
    public DateTimeOffset ValidationEndTime { get; set; }
}

/// <summary>
/// Result of a single parameter validation.
/// </summary>
public sealed class ParameterResult
{
    /// <summary>Gets the parameter name.</summary>
    public required string ParameterName { get; init; }

    /// <summary>Gets or sets whether the parameter is valid.</summary>
    public bool IsValid { get; set; }

    /// <summary>Gets or sets whether the parameter is secure (no security threats).</summary>
    public bool IsSecure { get; set; }

    /// <summary>Gets or sets the validation message.</summary>
    public string? Message { get; set; }

    /// <summary>Gets the list of security threats detected.</summary>
    public IList<InputThreat> SecurityThreats { get; init; } = [];
}

/// <summary>
/// Result of file path validation.
/// </summary>
public sealed class PathValidationResult
{
    /// <summary>
    /// Gets the original path being validated.
    /// </summary>
    public required string OriginalPath { get; init; }

    /// <summary>
    /// Gets or sets whether the path is valid and safe.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the normalized safe path (null if validation failed).
    /// </summary>
    public string? NormalizedPath { get; set; }

    /// <summary>
    /// Gets or sets the sanitized path (alias for NormalizedPath).
    /// </summary>
    public string? SanitizedPath { get; set; }

    /// <summary>
    /// Gets or sets the base directory for path resolution.
    /// </summary>
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// Gets the list of detected path security threats.
    /// </summary>
    public IList<InputThreat> Threats { get; init; } = [];

    /// <summary>
    /// Gets the list of security threats (alias for Threats).
    /// </summary>
    public IList<InputThreat> SecurityThreats { get; init; } = [];

    /// <summary>
    /// Gets the list of validation error messages.
    /// </summary>
    public IList<string> ValidationErrors { get; init; } = [];
}

/// <summary>
/// Result of GPU work group size validation.
/// </summary>
public sealed class WorkGroupValidationResult
{
    /// <summary>
    /// Gets or sets whether the work group configuration is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the list of validation errors (alias for Errors).
    /// </summary>
    public IList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets the list of validation warnings (alias for Warnings).
    /// </summary>
    public IList<string> ValidationWarnings { get; init; } = [];

    /// <summary>
    /// Gets or sets the requested global work size (total work items).
    /// </summary>
    public long RequestedGlobalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the requested local work group size.
    /// </summary>
    public long RequestedLocalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the recommended local work group size (power of 2, warp-aligned).
    /// </summary>
    public long? RecommendedLocalWorkSize { get; set; }

    /// <summary>
    /// Gets or sets the work group size dimensions.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public int[]? WorkGroupSize { get; set; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets the global size dimensions.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays
    public int[]? GlobalSize { get; set; }
#pragma warning restore CA1819

    /// <summary>
    /// Gets or sets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the total work group size (product of all dimensions).
    /// </summary>
    public int TotalWorkGroupSize { get; set; }

    /// <summary>
    /// Gets or sets the total global size (product of all dimensions).
    /// </summary>
    public long TotalGlobalSize { get; set; }
}

/// <summary>
/// Custom validation rule for context-specific input validation.
/// </summary>
public sealed class ValidationRule
{
    /// <summary>
    /// Gets the rule name/identifier.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Gets the rule name/identifier (alias for RuleName).
    /// </summary>
    public string Name => RuleName;

    /// <summary>
    /// Gets the validation function that returns true if input is valid.
    /// </summary>
    public required Func<string, bool> Validator { get; init; }

    /// <summary>
    /// Gets the error message to display when validation fails.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// Gets the threat type to report when this rule fails.
    /// </summary>
    public ThreatType AssociatedThreatType { get; init; } = ThreatType.GeneralMalicious;

    /// <summary>
    /// Gets the threat severity when this rule fails.
    /// </summary>
    public ThreatSeverity ThreatSeverity { get; init; } = ThreatSeverity.Medium;
}

/// <summary>
/// Cumulative validation statistics for monitoring and analysis.
/// </summary>
public sealed class ValidationStatistics
{
    /// <summary>
    /// Gets or sets the total number of validations performed.
    /// </summary>
    public long TotalValidations { get; set; }

    /// <summary>
    /// Gets or sets the total number of threats detected.
    /// </summary>
    public long TotalThreats { get; set; }

    /// <summary>
    /// Gets or sets the total number of threats detected (alias).
    /// </summary>
    public long TotalThreatsDetected { get; set; }

    /// <summary>
    /// Gets or sets the total number of validation violations (rejections).
    /// </summary>
    public long TotalViolations { get; set; }

    /// <summary>
    /// Gets or sets the total number of security violations.
    /// </summary>
    public long TotalSecurityViolations { get; set; }

    /// <summary>
    /// Gets or sets the number of critical severity threats detected.
    /// </summary>
    public long CriticalThreats { get; set; }

    /// <summary>
    /// Gets or sets the number of high severity threats detected.
    /// </summary>
    public long HighThreats { get; set; }

    /// <summary>
    /// Gets or sets the total sanitization processing time.
    /// </summary>
    public TimeSpan TotalProcessingTime { get; set; }

    /// <summary>
    /// Gets the average processing time per validation.
    /// </summary>
    public TimeSpan AverageProcessingTime => TotalValidations > 0 ? TimeSpan.FromTicks(TotalProcessingTime.Ticks / TotalValidations) : TimeSpan.Zero;

    /// <summary>
    /// Gets the threat detection rate (threats per validation).
    /// </summary>
    public double ThreatDetectionRate => TotalValidations > 0 ? (double)TotalThreats / TotalValidations : 0.0;

    /// <summary>
    /// Gets or sets the number of validations by type.
    /// </summary>
    public Dictionary<string, long> ValidationsByType { get; init; } = new();

    /// <summary>
    /// Gets or sets the number of threats by type.
    /// </summary>
    public Dictionary<string, long> ThreatsByType { get; init; } = new();
}
