// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.OpenCL.Types.Native;

/// <summary>
/// OpenCL event handle.
/// </summary>
public readonly struct OpenCLEventHandle : IEquatable<OpenCLEventHandle>
{
    /// <summary>
    /// Gets the underlying native handle.
    /// </summary>
    public readonly nint Handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLEventHandle"/> struct.
    /// </summary>
    /// <param name="handle">The native handle value.</param>
    public OpenCLEventHandle(nint handle) => Handle = handle;

    /// <summary>
    /// Implicitly converts an event handle to an IntPtr.
    /// </summary>
    public static implicit operator nint(OpenCLEventHandle evt) => evt.Handle;

    /// <summary>
    /// Implicitly converts an IntPtr to an event handle.
    /// </summary>
    public static implicit operator OpenCLEventHandle(nint handle) => new(handle);

    /// <summary>
    /// Converts an event handle to an IntPtr.
    /// </summary>
    public static nint ToIntPtr(OpenCLEventHandle evt) => evt.Handle;

    /// <summary>
    /// Creates an event handle from an IntPtr.
    /// </summary>
    public static OpenCLEventHandle FromIntPtr(nint handle) => new(handle);

    /// <summary>
    /// Returns a string representation of the event handle.
    /// </summary>
    public override string ToString() => $"Event[{Handle:X}]";

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
    public bool Equals(OpenCLEventHandle other) => Handle == other.Handle;

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is OpenCLEventHandle other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => Handle.GetHashCode();

    /// <summary>
    /// Indicates whether two instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(OpenCLEventHandle left, OpenCLEventHandle right) => left.Equals(right);

    /// <summary>
    /// Indicates whether two instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns>true if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(OpenCLEventHandle left, OpenCLEventHandle right) => !left.Equals(right);
}
