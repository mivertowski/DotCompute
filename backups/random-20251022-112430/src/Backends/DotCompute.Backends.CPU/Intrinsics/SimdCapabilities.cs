// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using System.Runtime.Intrinsics;

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
        SupportedInstructionSets = SimdInstructionSetDetector.GetSupportedInstructionSets()
    };
}
