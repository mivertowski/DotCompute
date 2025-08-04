// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

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
            sets.Add("SSE");
        if (Sse2.IsSupported)
            sets.Add("SSE2");
        if (Sse3.IsSupported)
            sets.Add("SSE3");
        if (Ssse3.IsSupported)
            sets.Add("SSSE3");
        if (Sse41.IsSupported)
            sets.Add("SSE4.1");
        if (Sse42.IsSupported)
            sets.Add("SSE4.2");
        if (Avx.IsSupported)
            sets.Add("AVX");
        if (Avx2.IsSupported)
            sets.Add("AVX2");
        if (Avx512F.IsSupported)
            sets.Add("AVX512F");
        if (Avx512BW.IsSupported)
            sets.Add("AVX512BW");
        if (Avx512CD.IsSupported)
            sets.Add("AVX512CD");
        if (Avx512DQ.IsSupported)
            sets.Add("AVX512DQ");
        if (Avx512Vbmi.IsSupported)
            sets.Add("AVX512VBMI");
        if (Fma.IsSupported)
            sets.Add("FMA");
        if (Bmi1.IsSupported)
            sets.Add("BMI1");
        if (Bmi2.IsSupported)
            sets.Add("BMI2");
        if (Popcnt.IsSupported)
            sets.Add("POPCNT");
        if (Lzcnt.IsSupported)
            sets.Add("LZCNT");

        // ARM instruction sets
        if (AdvSimd.IsSupported)
            sets.Add("NEON");
        if (AdvSimd.Arm64.IsSupported)
            sets.Add("NEON-ARM64");
        if (ArmBase.IsSupported)
            sets.Add("ARM-BASE");
        if (ArmBase.Arm64.IsSupported)
            sets.Add("ARM64-BASE");
        if (Crc32.IsSupported)
            sets.Add("CRC32");
        if (Crc32.Arm64.IsSupported)
            sets.Add("CRC32-ARM64");
        if (Dp.IsSupported)
            sets.Add("DP");
        if (Rdm.IsSupported)
            sets.Add("RDM");
        if (Sha1.IsSupported)
            sets.Add("SHA1");
        if (Sha256.IsSupported)
            sets.Add("SHA256");

        return sets;
    }
}

/// <summary>
/// Contains x86/x64 SIMD capability information.
/// </summary>
public sealed class X86SimdInfo
{
    public bool HasSse => Sse.IsSupported;
    public bool HasSse2 => Sse2.IsSupported;
    public bool HasSse3 => Sse3.IsSupported;
    public bool HasSsse3 => Ssse3.IsSupported;
    public bool HasSse41 => Sse41.IsSupported;
    public bool HasSse42 => Sse42.IsSupported;
    public bool HasAvx => Avx.IsSupported;
    public bool HasAvx2 => Avx2.IsSupported;
    public bool HasAvx512F => Avx512F.IsSupported;
    public bool HasAvx512BW => Avx512BW.IsSupported;
    public bool HasFma => Fma.IsSupported;
    public bool HasBmi1 => Bmi1.IsSupported;
    public bool HasBmi2 => Bmi2.IsSupported;
    public bool HasPopcnt => Popcnt.IsSupported;
    public bool HasLzcnt => Lzcnt.IsSupported;

    public int MaxVectorWidth => HasAvx512F ? 512 : HasAvx ? 256 : HasSse ? 128 : 0;
}

/// <summary>
/// Contains ARM SIMD capability information.
/// </summary>
public sealed class ArmSimdInfo
{
    public bool HasNeon => AdvSimd.IsSupported;
    public bool HasNeonArm64 => AdvSimd.Arm64.IsSupported;
    public bool HasCrc32 => Crc32.IsSupported;
    public bool HasAes => System.Runtime.Intrinsics.Arm.Aes.IsSupported;
    public bool HasSha1 => Sha1.IsSupported;
    public bool HasSha256 => Sha256.IsSupported;
    public bool HasDp => Dp.IsSupported;
    public bool HasRdm => Rdm.IsSupported;

    public int MaxVectorWidth => HasNeon ? 128 : 0;
}

/// <summary>
/// Summary of SIMD capabilities.
/// </summary>
public sealed class SimdSummary
{
    public required bool IsHardwareAccelerated { get; init; }
    public required int PreferredVectorWidth { get; init; }
    public required IReadOnlySet<string> SupportedInstructionSets { get; init; }

    // Helper properties for quick checks
    public bool SupportsSse2 => SupportedInstructionSets.Contains("SSE2");
    public bool SupportsAvx2 => SupportedInstructionSets.Contains("AVX2");
    public bool SupportsAvx512 => SupportedInstructionSets.Contains("AVX512F");
    public bool SupportsAdvSimd => SupportedInstructionSets.Contains("NEON");

    public override string ToString()
    {
        if (!IsHardwareAccelerated)
            return "No SIMD support detected";

        return $"SIMD: {PreferredVectorWidth}-bit vectors, Instructions: {string.Join(", ", SupportedInstructionSets)}";
    }
}
