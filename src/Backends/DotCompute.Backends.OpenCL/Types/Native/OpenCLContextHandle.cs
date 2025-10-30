// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL context handle.
/// </summary>
public readonly struct OpenCLContextHandle
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLContextHandle"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLContextHandle(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a context handle to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLContextHandle context) => context.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a context handle.
    /// </summary>
    public static implicit operator OpenCLContextHandle(nint handle) => new(handle);

    /// <summary>
    /// Converts a context handle to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLContextHandle context) => context.Handle;

    /// <summary>
    /// Creates a context handle from an IntPtr.
    /// </summary>
    public static OpenCLContextHandle FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the context handle.
    /// </summary>
    public override string ToString() => $"Context[{Handle:X}]";
}
