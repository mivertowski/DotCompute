// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents a validation profile that defines validation behavior for kernels.
/// </summary>
public sealed class ValidationProfile
{
    /// <summary>
    /// Gets the validation level.
    /// </summary>
    public ValidationLevel Level { get; init; }

    /// <summary>
    /// Gets whether to perform determinism testing.
    /// </summary>
    public bool EnableDeterminismTesting { get; init; }

    /// <summary>
    /// Gets whether to perform memory pattern analysis.
    /// </summary>
    public bool EnableMemoryAnalysis { get; init; }

    /// <summary>
    /// Gets whether to perform performance analysis.
    /// </summary>
    public bool EnablePerformanceAnalysis { get; init; }

    /// <summary>
    /// Gets whether to perform cross-backend validation.
    /// </summary>
    public bool EnableCrossBackendValidation { get; init; }

    /// <summary>
    /// Gets the maximum execution timeout for validation.
    /// </summary>
    public TimeSpan MaxExecutionTime { get; init; }

    /// <summary>
    /// Gets the number of iterations for determinism testing.
    /// </summary>
    public int DeterminismIterations { get; init; }

    /// <summary>
    /// Gets the default validation profile for development.
    /// </summary>
    public static ValidationProfile Default => new()
    {
        Level = ValidationLevel.Basic,
        EnableDeterminismTesting = true,
        EnableMemoryAnalysis = true,
        EnablePerformanceAnalysis = false,
        EnableCrossBackendValidation = false,
        MaxExecutionTime = TimeSpan.FromSeconds(30),
        DeterminismIterations = 3
    };

    /// <summary>
    /// Gets a comprehensive validation profile for production.
    /// </summary>
    public static ValidationProfile Comprehensive => new()
    {
        Level = ValidationLevel.Comprehensive,
        EnableDeterminismTesting = true,
        EnableMemoryAnalysis = true,
        EnablePerformanceAnalysis = true,
        EnableCrossBackendValidation = true,
        MaxExecutionTime = TimeSpan.FromMinutes(5),
        DeterminismIterations = 10
    };

    /// <summary>
    /// Gets a minimal validation profile for quick checks.
    /// </summary>
    public static ValidationProfile Minimal => new()
    {
        Level = ValidationLevel.Minimal,
        EnableDeterminismTesting = false,
        EnableMemoryAnalysis = false,
        EnablePerformanceAnalysis = false,
        EnableCrossBackendValidation = false,
        MaxExecutionTime = TimeSpan.FromSeconds(5),
        DeterminismIterations = 1
    };
}

/// <summary>
/// Validation levels that define the depth of validation performed.
/// </summary>
public enum ValidationLevel
{
    /// <summary>
    /// Minimal validation - basic structure checks only.
    /// </summary>
    Minimal,

    /// <summary>
    /// Basic validation - structure and simple pattern checks.
    /// </summary>
    Basic,

    /// <summary>
    /// Standard validation - includes memory and determinism checks.
    /// </summary>
    Standard,

    /// <summary>
    /// Comprehensive validation - all checks including cross-backend validation.
    /// </summary>
    Comprehensive
}
