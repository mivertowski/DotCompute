// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL platform ID handle.
/// </summary>
public readonly struct OpenCLPlatformId
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLPlatformId"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLPlatformId(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a platform ID to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLPlatformId platformId) => platformId.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a platform ID.
    /// </summary>
    public static implicit operator OpenCLPlatformId(nint handle) => new(handle);

    /// <summary>
    /// Converts a platform ID to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLPlatformId platformId) => platformId.Handle;

    /// <summary>
    /// Creates a platform ID from an IntPtr.
    /// </summary>
    public static OpenCLPlatformId FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the platform ID.
    /// </summary>
    public override string ToString() => $"Platform[{Handle:X}]";
}
