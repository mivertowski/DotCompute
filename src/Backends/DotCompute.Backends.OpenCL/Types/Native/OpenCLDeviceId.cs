// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL device ID handle.
/// </summary>
public readonly struct OpenCLDeviceId
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLDeviceId"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLDeviceId(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a device ID to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLDeviceId deviceId) => deviceId.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a device ID.
    /// </summary>
    public static implicit operator OpenCLDeviceId(nint handle) => new(handle);

    /// <summary>
    /// Converts a device ID to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLDeviceId deviceId) => deviceId.Handle;

    /// <summary>
    /// Creates a device ID from an IntPtr.
    /// </summary>
    public static OpenCLDeviceId FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the device ID.
    /// </summary>
    public override string ToString() => $"Device[{Handle:X}]";
}
