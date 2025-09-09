// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Configuration.Settings.Validation;

/// <summary>
/// Validation settings for generated code, controlling what runtime checks
/// and validations are included to ensure correctness and safety.
/// </summary>
/// <remarks>
/// This class provides configuration options for various validation mechanisms
/// that can be embedded in generated code. While validation checks improve
/// safety and help catch errors at runtime, they also add overhead and may
/// impact performance. The settings allow developers to choose the appropriate
/// balance between safety and performance for their specific use cases.
/// </remarks>
public class ValidationSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether to generate null reference checks.
    /// </summary>
    /// <value>
    /// <c>true</c> if null checks should be inserted before dereferencing
    /// objects and arrays; otherwise, <c>false</c>. Null checks help prevent
    /// <see cref="System.NullReferenceException"/> at runtime.
    /// </value>
    /// <remarks>
    /// Null checks add safety by validating that objects and arrays are not
    /// null before access. While this prevents null reference exceptions,
    /// it adds runtime overhead. In performance-critical scenarios where
    /// null values are guaranteed not to occur, these checks can be disabled.
    /// </remarks>
    public bool GenerateNullChecks { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to generate array bounds checks.
    /// </summary>
    /// <value>
    /// <c>true</c> if bounds checks should be inserted before array and
    /// collection access; otherwise, <c>false</c>. Bounds checks help prevent
    /// <see cref="System.IndexOutOfRangeException"/> at runtime.
    /// </value>
    /// <remarks>
    /// Bounds checking validates that array and collection indices are within
    /// valid ranges before access. This prevents buffer overruns and data
    /// corruption but adds performance overhead. In scenarios where indices
    /// are guaranteed to be valid, these checks can be disabled for better performance.
    /// </remarks>
    public bool GenerateBoundsChecks { get; set; } = true;


    /// <summary>
    /// Gets or sets a value indicating whether to validate memory alignment for SIMD operations.
    /// </summary>
    /// <value>
    /// <c>true</c> if memory alignment should be validated before SIMD operations;
    /// otherwise, <c>false</c>. Alignment validation ensures data is properly
    /// aligned for vectorized operations, preventing performance degradation or crashes.
    /// </value>
    /// <remarks>
    /// Memory alignment is critical for optimal SIMD performance and correctness.
    /// Misaligned data can cause significant performance penalties or exceptions
    /// on some processors. However, alignment checks add overhead and are often
    /// unnecessary when data alignment is guaranteed by the application design.
    /// </remarks>
    public bool ValidateAlignment { get; set; }


    /// <summary>
    /// Gets or sets a value indicating whether to check for NaN (Not a Number) and Infinity values.
    /// </summary>
    /// <value>
    /// <c>true</c> if floating-point operations should be validated for special
    /// values like NaN and Infinity; otherwise, <c>false</c>. These checks help
    /// detect and handle numerical instability in mathematical computations.
    /// </value>
    /// <remarks>
    /// NaN and Infinity checks help detect numerical errors and instabilities
    /// in floating-point calculations. These special values can propagate through
    /// calculations and cause unexpected results. While useful for debugging
    /// numerical algorithms, these checks add significant overhead to mathematical
    /// operations and are typically disabled in production code.
    /// </remarks>
    public bool CheckForNaNInfinity { get; set; } = false;
}