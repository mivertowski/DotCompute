
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Selection.Enums;

/// <summary>
/// FFT (Fast Fourier Transform) algorithm strategies.
/// Different strategies are optimal for different input sizes and characteristics.
/// </summary>
public enum FFTStrategy
{
    /// <summary>
    /// No computation needed for trivial cases (size ≤ 1).
    /// </summary>
    Trivial,

    /// <summary>
    /// Direct DFT computation for very small sizes (typically ≤ 16).
    /// </summary>
    DirectDFT,

    /// <summary>
    /// Standard Cooley-Tukey FFT for power-of-2 sizes.
    /// </summary>
    CooleyTukey,

    /// <summary>
    /// Mixed-radix FFT for composite sizes with small prime factors.
    /// </summary>
    MixedRadix,

    /// <summary>
    /// SIMD-optimized complex FFT using vectorized instructions.
    /// </summary>
    SimdComplex,

    /// <summary>
    /// SIMD-optimized real FFT for real-valued input data.
    /// </summary>
    SimdReal,

    /// <summary>
    /// Cache-friendly four-step FFT for very large transforms.
    /// </summary>
    CacheFriendly,

    /// <summary>
    /// Bluestein's algorithm for arbitrary sizes.
    /// </summary>
    Bluestein
}
