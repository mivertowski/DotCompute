// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL memory object handle.
/// </summary>
public readonly struct OpenCLMemObject
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMemObject"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLMemObject(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a memory object to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLMemObject memObject) => memObject.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a memory object.
    /// </summary>
    public static implicit operator OpenCLMemObject(nint handle) => new(handle);

    /// <summary>
    /// Converts a memory object to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLMemObject memObject) => memObject.Handle;

    /// <summary>
    /// Creates a memory object from an IntPtr.
    /// </summary>
    public static OpenCLMemObject FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the memory object.
    /// </summary>
    public override string ToString() => $"Buffer[{Handle:X}]";
}
