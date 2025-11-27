// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// Comprehensive P2P validation result with detailed diagnostics.
/// </summary>
/// <remarks>
/// <para>
/// Contains overall validation status and a collection of individual validation
/// checks performed during P2P capability or transfer validation.
/// </para>
/// <para>
/// Used by <see cref="P2PValidator"/> to report validation outcomes with
/// actionable error messages and diagnostic details.
/// </para>
/// </remarks>
public sealed class P2PValidationResult
{
    /// <summary>
    /// Gets or sets the unique validation identifier.
    /// </summary>
    public required string ValidationId { get; init; }

    /// <summary>
    /// Gets or sets when the validation was performed.
    /// </summary>
    public required DateTimeOffset ValidationTime { get; init; }

    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    /// <remarks>
    /// True only if all individual validation details passed.
    /// </remarks>
    public required bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the overall error message if validation failed.
    /// </summary>
    /// <remarks>
    /// Summary of the most critical validation failure. Check <see cref="ValidationDetails"/>
    /// for complete diagnostic information.
    /// </remarks>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the collection of individual validation check results.
    /// </summary>
    public required IList<P2PValidationDetail> ValidationDetails { get; init; }
}

/// <summary>
/// Individual validation detail result for a specific check.
/// </summary>
/// <remarks>
/// Represents the outcome of a single validation rule (e.g., "P2P support check",
/// "bandwidth verification", "topology compatibility").
/// </remarks>
public sealed class P2PValidationDetail
{
    /// <summary>
    /// Gets or sets the type of validation performed.
    /// </summary>
    /// <remarks>
    /// Examples: "P2PSupport", "BandwidthTest", "LatencyCheck", "TopologyValidation".
    /// </remarks>
    public required string ValidationType { get; init; }

    /// <summary>
    /// Gets or sets whether this specific validation check passed.
    /// </summary>
    public required bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if this check failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional diagnostic details about the validation.
    /// </summary>
    /// <remarks>
    /// May include technical information like actual vs expected values,
    /// hardware capabilities, or suggested remediation steps.
    /// </remarks>
    public string? Details { get; set; }
}

/// <summary>
/// P2P validation statistics tracking validation operations over time.
/// </summary>
/// <remarks>
/// <para>
/// Maintains cumulative metrics for all validation operations performed by
/// the P2P validation subsystem.
/// </para>
/// <para>
/// Useful for monitoring validation patterns, identifying problematic device
/// pairs, and capacity planning.
/// </para>
/// </remarks>
public sealed class P2PValidationStatistics
{
    /// <summary>
    /// Gets or sets the total number of validations performed.
    /// </summary>
    public long TotalValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of successful validations.
    /// </summary>
    public long SuccessfulValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed validations.
    /// </summary>
    public long FailedValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of data integrity checks performed.
    /// </summary>
    /// <remarks>
    /// Counts validations that verified transferred data correctness.
    /// </remarks>
    public long IntegrityChecks { get; set; }

    /// <summary>
    /// Gets or sets the number of performance validations performed.
    /// </summary>
    /// <remarks>
    /// Counts validations that measured transfer throughput and latency.
    /// </remarks>
    public long PerformanceValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of capability validations performed.
    /// </summary>
    /// <remarks>
    /// Counts validations that checked P2P hardware support.
    /// </remarks>
    public long CapabilityValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of benchmarks executed.
    /// </summary>
    public long BenchmarksExecuted { get; set; }

    /// <summary>
    /// Gets or sets the total time spent on validations.
    /// </summary>
    public TimeSpan TotalValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the average time per validation.
    /// </summary>
    public TimeSpan AverageValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the number of cached benchmark hits.
    /// </summary>
    /// <remarks>
    /// Tracks how often cached benchmark results were reused instead of re-running.
    /// </remarks>
    public long CachedBenchmarkHits { get; set; }

    /// <summary>
    /// Gets or sets the validation success rate (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Ratio of successful validations to total validations.
    /// </remarks>
    public double ValidationSuccessRate { get; set; }
}

/// <summary>
/// Represents a device pair for P2P operations.
/// </summary>
public sealed class P2PDevicePair
{
    /// <summary>
    /// Gets or sets the first device in the pair.
    /// </summary>
    public required string Device1 { get; init; }

    /// <summary>
    /// Gets or sets the second device in the pair.
    /// </summary>
    public required string Device2 { get; init; }

    /// <summary>
    /// Gets or sets the P2P capability for this device pair.
    /// </summary>
    public P2PConnectionCapability? Capability { get; set; }

    /// <summary>
    /// Gets or sets whether P2P is enabled for this device pair.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Result of P2P initialization operation.
/// </summary>
public sealed class P2PInitializationResult
{
    private readonly List<P2PDevicePair> _devicePairs = [];

    /// <summary>
    /// Gets or sets whether the initialization succeeded.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the count of successful connections.
    /// </summary>
    public int SuccessfulConnections { get; set; }

    /// <summary>
    /// Gets or sets the count of failed connections.
    /// </summary>
    public int FailedConnections { get; set; }

    /// <summary>
    /// Gets or sets the total number of devices.
    /// </summary>
    public int TotalDevices { get; set; }

    /// <summary>
    /// Gets all device pairs.
    /// </summary>
    public IList<P2PDevicePair> DevicePairs => _devicePairs;

    /// <summary>
    /// Gets or sets the error message if initialization failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
