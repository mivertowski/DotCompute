// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Numerics;
using global::System.Runtime.Intrinsics;
using global::System.Runtime.Intrinsics.X86;
using global::System.Runtime.Intrinsics.Arm;

namespace DotCompute.Algorithms.Optimized.Simd;

/// <summary>
/// Hardware capability detection and vector size constants for SIMD operations.
/// </summary>
internal static class SimdCapabilities
{
    // Hardware capability detection
    public static readonly bool HasAvx512 = Avx512F.IsSupported;
    public static readonly bool HasAvx2 = Avx2.IsSupported;
    public static readonly bool HasFma = Fma.IsSupported;
    public static readonly bool HasSse42 = Sse42.IsSupported;
    public static readonly bool HasNeon = AdvSimd.IsSupported;

    // Vector sizes for different architectures
    public static readonly int Vector512Size = HasAvx512 ? Vector512<float>.Count : 0;
    public static readonly int Vector256Size = HasAvx2 ? Vector256<float>.Count : 0;
    public static readonly int Vector128Size = Vector128<float>.Count;
    public static readonly int VectorSize = Vector<float>.Count;

    // Optimal vector size for current architecture
    public static readonly int OptimalVectorSize = HasAvx512 ? Vector512Size :
                                                    HasAvx2 ? Vector256Size :
                                                    Vector128Size;
}