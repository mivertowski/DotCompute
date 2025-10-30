// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL program handle.
/// </summary>
public readonly struct OpenCLProgram
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLProgram"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLProgram(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts a program to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLProgram program) => program.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to a program.
    /// </summary>
    public static implicit operator OpenCLProgram(nint handle) => new(handle);

    /// <summary>
    /// Converts a program to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLProgram program) => program.Handle;

    /// <summary>
    /// Creates a program from an IntPtr.
    /// </summary>
    public static OpenCLProgram FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the program.
    /// </summary>
    public override string ToString() => $"Program[{Handle:X}]";
}
