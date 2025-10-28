// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL command queue handle.
/// </summary>
public readonly struct OpenCLCommandQueue
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLCommandQueue"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLCommandQueue(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a command queue to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLCommandQueue queue) => queue.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a command queue.
    /// </summary>
    public static implicit operator OpenCLCommandQueue(nint handle) => new(handle);

    /// <summary>
    /// Converts a command queue to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLCommandQueue queue) => queue.Handle;

    /// <summary>
    /// Creates a command queue from an IntPtr.
    /// </summary>
    public static OpenCLCommandQueue FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the command queue.
    /// </summary>
    public override string ToString() => $"Queue[{Handle:X}]";
}
