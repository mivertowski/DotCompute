// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.Arm;
using global::System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Intrinsics;


/// <summary>
/// Provides information about SIMD capabilities of the current CPU.
/// </summary>
public static class SimdCapabilities
{
    /// <summary>
    /// Gets whether any form of SIMD is supported.
    /// </summary>
    public static bool IsSupported { get; } = Vector.IsHardwareAccelerated;

    /// <summary>
    /// Gets the preferred vector width in bits.
    /// </summary>
    public static int PreferredVectorWidth { get; } = Vector256.IsHardwareAccelerated ? 256 :
                                                      Vector128.IsHardwareAccelerated ? 128 :
                                                      Vector.IsHardwareAccelerated ? Vector<byte>.Count * 8 : 64;

    /// <summary>
    /// Gets information about x86/x64 SIMD support.
    /// </summary>
    public static X86SimdInfo X86 { get; } = new();

    /// <summary>
    /// Gets information about ARM SIMD support.
    /// </summary>
    public static ArmSimdInfo Arm { get; } = new();

    /// <summary>
    /// Gets a summary of all supported SIMD instruction sets.
    /// </summary>
    public static SimdSummary GetSummary() => new()
    {
        IsHardwareAccelerated = IsSupported,
        PreferredVectorWidth = PreferredVectorWidth,
        SupportedInstructionSets = GetSupportedInstructionSets()
    };

    private static HashSet<string> GetSupportedInstructionSets()
    {
        var sets = new HashSet<string>();

        // x86/x64 instruction sets
        if (Sse.IsSupported)
        {
            _ = sets.Add("SSE");
        }

        if (Sse2.IsSupported)
        {
            _ = sets.Add("SSE2");
        }

        if (Sse3.IsSupported)
        {
            _ = sets.Add("SSE3");
        }

        if (Ssse3.IsSupported)
        {
            _ = sets.Add("SSSE3");
        }

        if (Sse41.IsSupported)
        {
            _ = sets.Add("SSE4.1");
        }

        if (Sse42.IsSupported)
        {
            _ = sets.Add("SSE4.2");
        }

        if (Avx.IsSupported)
        {
            _ = sets.Add("AVX");
        }

        if (Avx2.IsSupported)
        {
            _ = sets.Add("AVX2");
        }

        if (Avx512F.IsSupported)
        {
            _ = sets.Add("AVX512F");
        }

        if (Avx512BW.IsSupported)
        {
            _ = sets.Add("AVX512BW");
        }

        if (Avx512CD.IsSupported)
        {
            _ = sets.Add("AVX512CD");
        }

        if (Avx512DQ.IsSupported)
        {
            _ = sets.Add("AVX512DQ");
        }

        if (Avx512Vbmi.IsSupported)
        {
            _ = sets.Add("AVX512VBMI");
        }

        if (Fma.IsSupported)
        {
            _ = sets.Add("FMA");
        }

        if (Bmi1.IsSupported)
        {
            _ = sets.Add("BMI1");
        }

        if (Bmi2.IsSupported)
        {
            _ = sets.Add("BMI2");
        }

        if (Popcnt.IsSupported)
        {
            _ = sets.Add("POPCNT");
        }

        if (Lzcnt.IsSupported)
        {
            _ = sets.Add("LZCNT");
        }

        // ARM instruction sets
        if (AdvSimd.IsSupported)
        {
            _ = sets.Add("NEON");
        }

        if (AdvSimd.Arm64.IsSupported)
        {
            _ = sets.Add("NEON-ARM64");
        }

        if (ArmBase.IsSupported)
        {
            _ = sets.Add("ARM-BASE");
        }

        if (ArmBase.Arm64.IsSupported)
        {
            _ = sets.Add("ARM64-BASE");
        }

        if (Crc32.IsSupported)
        {
            _ = sets.Add("CRC32");
        }

        if (Crc32.Arm64.IsSupported)
        {
            _ = sets.Add("CRC32-ARM64");
        }

        if (Dp.IsSupported)
        {
            _ = sets.Add("DP");
        }

        if (Rdm.IsSupported)
        {
            _ = sets.Add("RDM");
        }

        if (Sha1.IsSupported)
        {
            _ = sets.Add("SHA1");
        }

        if (Sha256.IsSupported)
        {
            _ = sets.Add("SHA256");
        }

        return sets;
    }
}

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

/// <summary>
/// Contains ARM SIMD capability information.
/// </summary>
public sealed class ArmSimdInfo
{
    /// <summary>
    /// Gets a value indicating whether this instance has neon.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has neon; otherwise, <c>false</c>.
    /// </value>
    public static bool HasNeon => AdvSimd.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has neon arm64.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has neon arm64; otherwise, <c>false</c>.
    /// </value>
    public static bool HasNeonArm64 => AdvSimd.Arm64.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has CRC32.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has CRC32; otherwise, <c>false</c>.
    /// </value>
    public static bool HasCrc32 => Crc32.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has aes.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has aes; otherwise, <c>false</c>.
    /// </value>
    public static bool HasAes => global::System.Runtime.Intrinsics.Arm.Aes.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sha1.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sha1; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSha1 => Sha1.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has sha256.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has sha256; otherwise, <c>false</c>.
    /// </value>
    public static bool HasSha256 => Sha256.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has dp.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has dp; otherwise, <c>false</c>.
    /// </value>
    public static bool HasDp => Dp.IsSupported;

    /// <summary>
    /// Gets a value indicating whether this instance has RDM.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance has RDM; otherwise, <c>false</c>.
    /// </value>
    public static bool HasRdm => Rdm.IsSupported;

    /// <summary>
    /// Gets the maximum width of the vector.
    /// </summary>
    /// <value>
    /// The maximum width of the vector.
    /// </value>
    public static int MaxVectorWidth => HasNeon ? 128 : 0;
}

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
