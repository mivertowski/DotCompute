// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CPU.Intrinsics;

/// <summary>
/// Summary of SIMD capabilities.
/// </summary>
public sealed class SimdSummary
{
    /// <summary>
    /// Gets a value indicating whether this instance is hardware accelerated.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is hardware accelerated; otherwise, <c>false</c>.
    /// </value>
    public required bool IsHardwareAccelerated { get; init; }

    /// <summary>
    /// Gets the width of the preferred vector.
    /// </summary>
    /// <value>
    /// The width of the preferred vector.
    /// </value>
    public required int PreferredVectorWidth { get; init; }

    /// <summary>
    /// Gets the supported instruction sets.
    /// </summary>
    /// <value>
    /// The supported instruction sets.
    /// </value>
    public required IReadOnlySet<string> SupportedInstructionSets { get; init; }

    // Helper properties for quick checks
    /// <summary>
    /// Gets a value indicating whether [supports sse2].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [supports sse2]; otherwise, <c>false</c>.
    /// </value>
    public bool SupportsSse2 => SupportedInstructionSets.Contains("SSE2");

    /// <summary>
    /// Gets a value indicating whether [supports avx2].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [supports avx2]; otherwise, <c>false</c>.
    /// </value>
    public bool SupportsAvx2 => SupportedInstructionSets.Contains("AVX2");

    /// <summary>
    /// Gets a value indicating whether [supports avx512].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [supports avx512]; otherwise, <c>false</c>.
    /// </value>
    public bool SupportsAvx512 => SupportedInstructionSets.Contains("AVX512F");

    /// <summary>
    /// Gets a value indicating whether [supports adv simd].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [supports adv simd]; otherwise, <c>false</c>.
    /// </value>
    public bool SupportsAdvSimd => SupportedInstructionSets.Contains("NEON");

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        if (!IsHardwareAccelerated)
        {
            return "No SIMD support detected";
        }

        return $"SIMD: {PreferredVectorWidth}-bit vectors, Instructions: {string.Join(", ", SupportedInstructionSets)}";
    }
}