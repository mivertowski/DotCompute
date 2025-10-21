// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Types.Native.Enums
{
    /// <summary>
    /// An cuda driver entry point query result enumeration.
    /// </summary>
    /// <summary>
    /// CUDA driver entry point query result enumeration
    /// </summary>
    public enum CudaDriverEntryPointQueryResult : uint
    {
        /// <summary>
        /// The driver entry point was found successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The requested driver symbol was not found in the driver library.
        /// </summary>
        SymbolNotFound = 1,

        /// <summary>
        /// The driver version is not sufficient to support the requested entry point.
        /// </summary>
        VersionNotSufficent = 2
    }
    /// <summary>
    /// An cuda array format kind enumeration.
    /// </summary>

    /// <summary>
    /// CUDA array format kind enumeration
    /// </summary>
    public enum CudaArrayFormatKind : uint
    {
        /// <summary>
        /// Unsigned integer format.
        /// </summary>
        Unsigned = 0,

        /// <summary>
        /// Signed integer format.
        /// </summary>
        Signed = 1,

        /// <summary>
        /// Floating-point format.
        /// </summary>
        Float = 2,

        /// <summary>
        /// No specific format (uninitialized or unsupported).
        /// </summary>
        None = 3,

        /// <summary>
        /// NV12 format for video processing (Y plane followed by interleaved UV).
        /// </summary>
        Nv12 = 4,

        /// <summary>
        /// Unsigned normalized 8-bit single channel format.
        /// </summary>
        UnsignedNormalized8X1 = 0x10,

        /// <summary>
        /// Unsigned normalized 8-bit two channel format.
        /// </summary>
        UnsignedNormalized8X2 = 0x11,

        /// <summary>
        /// Unsigned normalized 8-bit four channel format.
        /// </summary>
        UnsignedNormalized8X4 = 0x12,

        /// <summary>
        /// Unsigned normalized 16-bit single channel format.
        /// </summary>
        UnsignedNormalized16X1 = 0x13,

        /// <summary>
        /// Unsigned normalized 16-bit two channel format.
        /// </summary>
        UnsignedNormalized16X2 = 0x14,

        /// <summary>
        /// Unsigned normalized 16-bit four channel format.
        /// </summary>
        UnsignedNormalized16X4 = 0x15,

        /// <summary>
        /// Signed normalized 8-bit single channel format.
        /// </summary>
        SignedNormalized8X1 = 0x16,

        /// <summary>
        /// Signed normalized 8-bit two channel format.
        /// </summary>
        SignedNormalized8X2 = 0x17,

        /// <summary>
        /// Signed normalized 8-bit four channel format.
        /// </summary>
        SignedNormalized8X4 = 0x18,

        /// <summary>
        /// Signed normalized 16-bit single channel format.
        /// </summary>
        SignedNormalized16X1 = 0x19,

        /// <summary>
        /// Signed normalized 16-bit two channel format.
        /// </summary>
        SignedNormalized16X2 = 0x1a,

        /// <summary>
        /// Signed normalized 16-bit four channel format.
        /// </summary>
        SignedNormalized16X4 = 0x1b,

        /// <summary>
        /// Unsigned 64-bit integer format.
        /// </summary>
        Unsigned64 = 0x1c,

        /// <summary>
        /// Signed 64-bit integer format.
        /// </summary>
        Signed64 = 0x1d,

        /// <summary>
        /// 16-bit half-precision floating-point format.
        /// </summary>
        Half = 0x1e,

        /// <summary>
        /// Two-component 16-bit half-precision floating-point format.
        /// </summary>
        Half2 = 0x1f,

        /// <summary>
        /// Four-component 16-bit half-precision floating-point format.
        /// </summary>
        Half4 = 0x20,

        /// <summary>
        /// Eight-component 16-bit half-precision floating-point format.
        /// </summary>
        Half8 = 0x21,

        /// <summary>
        /// Sixteen-component 16-bit half-precision floating-point format.
        /// </summary>
        Half16 = 0x22,

        /// <summary>
        /// 16-bit brain floating-point format (BF16).
        /// </summary>
        Bfloat16 = 0x23,

        /// <summary>
        /// Two-component 16-bit brain floating-point format.
        /// </summary>
        Bfloat162 = 0x24,

        /// <summary>
        /// Four-component 16-bit brain floating-point format.
        /// </summary>
        Bfloat164 = 0x25
    }
}