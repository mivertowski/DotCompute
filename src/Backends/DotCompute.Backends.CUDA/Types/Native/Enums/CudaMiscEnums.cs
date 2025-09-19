// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// CUDA driver entry point query result enumeration
    /// </summary>
    public enum CudaDriverEntryPointQueryResult : uint
    {
        Success = 0,
        SymbolNotFound = 1,
        VersionNotSufficent = 2
    }

    /// <summary>
    /// CUDA array format kind enumeration
    /// </summary>
    public enum CudaArrayFormatKind : uint
    {
        Unsigned = 0,
        Signed = 1,
        Float = 2,
        None = 3,
        Nv12 = 4,
        UnsignedNormalized8X1 = 0x10,
        UnsignedNormalized8X2 = 0x11,
        UnsignedNormalized8X4 = 0x12,
        UnsignedNormalized16X1 = 0x13,
        UnsignedNormalized16X2 = 0x14,
        UnsignedNormalized16X4 = 0x15,
        SignedNormalized8X1 = 0x16,
        SignedNormalized8X2 = 0x17,
        SignedNormalized8X4 = 0x18,
        SignedNormalized16X1 = 0x19,
        SignedNormalized16X2 = 0x1a,
        SignedNormalized16X4 = 0x1b,
        Unsigned64 = 0x1c,
        Signed64 = 0x1d,
        Half = 0x1e,
        Half2 = 0x1f,
        Half4 = 0x20,
        Half8 = 0x21,
        Half16 = 0x22,
        Bfloat16 = 0x23,
        Bfloat162 = 0x24,
        Bfloat164 = 0x25
    }
}