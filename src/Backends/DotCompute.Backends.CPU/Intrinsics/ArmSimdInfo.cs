// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using global::System.Runtime.Intrinsics.Arm;

namespace DotCompute.Backends.CPU.Intrinsics;

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