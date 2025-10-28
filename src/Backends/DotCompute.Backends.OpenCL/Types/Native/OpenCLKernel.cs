// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL kernel handle.
/// </summary>
public readonly struct OpenCLKernel
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLKernel"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLKernel(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a kernel to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLKernel kernel) => kernel.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a kernel.
    /// </summary>
    public static implicit operator OpenCLKernel(nint handle) => new(handle);

    /// <summary>
    /// Converts a kernel to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLKernel kernel) => kernel.Handle;

    /// <summary>
    /// Creates a kernel from an IntPtr.
    /// </summary>
    public static OpenCLKernel FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the kernel.
    /// </summary>
    public override string ToString() => $"Kernel[{Handle:X}]";
}
