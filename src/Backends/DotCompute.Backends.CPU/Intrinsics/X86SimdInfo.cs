// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Intrinsics;

/// <summary>
/// Contains x86/x64 SIMD capability information.
/// </summary>
public sealed class X86SimdInfo
{
    /// <summary>
    /// Gets a value indicating whether this instance has sse.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sse; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSse => Sse.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sse2.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sse2; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSse2 => Sse2.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sse3.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sse3; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSse3 => Sse3.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has ssse3.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has ssse3; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSsse3 => Ssse3.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sse41.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sse41; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSse41 => Sse41.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sse42.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sse42; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSse42 => Sse42.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has avx.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has avx; otherwise, <c>false</c>.
    /// </value>
    public static bool HasAvx => Avx.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has avx2.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has avx2; otherwise, <c>false</c>.
    /// </value>
    public static bool HasAvx2 => Avx2.IsSupported;


    /// <summary>
    /// Gets a value indicating whether this instance has avx512 f.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has avx512 f; otherwise, <c>false</c>.
    /// </value>
    public static bool HasAvx512F => Avx512F.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has avx512 bw.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has avx512 bw; otherwise, <c>false</c>.
    /// </value>
    public static bool HasAvx512BW => Avx512BW.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has fma.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has fma; otherwise, <c>false</c>.
    /// </value>
    public static bool HasFma => Fma.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has bmi1.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has bmi1; otherwise, <c>false</c>.
    /// </value>
    public static bool HasBmi1 => Bmi1.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has bmi2.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has bmi2; otherwise, <c>false</c>.
    /// </value>
    public static bool HasBmi2 => Bmi2.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has popcnt.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has popcnt; otherwise, <c>false</c>.
    /// </value>
    public static bool HasPopcnt => Popcnt.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has LZCNT.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has LZCNT; otherwise, <c>false</c>.
    /// </value>
    public static bool HasLzcnt => Lzcnt.IsSupported;

    /// <summary>
    /// Gets the maximum width of the vector.
    /// </summary>
    /// <value>
    /// The maximum width of the vector.
    /// </value>
    public static int MaxVectorWidth => HasAvx512F ? 512 : HasAvx ? 256 : HasSse ? 128 : 0;
}
