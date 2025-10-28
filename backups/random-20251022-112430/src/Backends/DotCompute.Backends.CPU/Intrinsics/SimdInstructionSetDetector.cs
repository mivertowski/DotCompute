// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace DotCompute.Backends.CPU.Intrinsics;

/// <summary>
/// Provides instruction set detection utilities for SIMD capabilities.
/// </summary>
internal static class SimdInstructionSetDetector
{
    /// <summary>
    /// Gets all supported instruction sets on the current platform.
    /// </summary>
    /// <returns>A set of supported instruction set names.</returns>
    public static HashSet<string> GetSupportedInstructionSets()
    {
        var sets = new HashSet<string>();

        // x86/x64 instruction sets
        AddX86InstructionSets(sets);

        // ARM instruction sets
        AddArmInstructionSets(sets);

        return sets;
    }

    private static void AddX86InstructionSets(HashSet<string> sets)
    {
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
    }

    private static void AddArmInstructionSets(HashSet<string> sets)
    {
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
    }
}