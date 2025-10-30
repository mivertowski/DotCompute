// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils.Analysis;

/// <summary>
/// Configuration for SIMD type mapping.
/// </summary>
public sealed class SimdConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to prefer AVX2 instructions.
    /// </summary>
    public bool PreferAvx2 { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to prefer AVX-512 instructions.
    /// </summary>
    public bool PreferAvx512 { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to fallback to SSE instructions.
    /// </summary>
    public bool FallbackToSse { get; set; } = true;

    /// <summary>
    /// Gets or sets the default vector bit width (default is 256 for AVX2).
    /// </summary>
    public int DefaultVectorBitWidth { get; set; } = 256;
}
