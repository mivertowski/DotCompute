// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Represents an accelerator context for managing device state.
/// This is a value type for AOT compatibility and zero allocations.
/// </summary>
public readonly struct AcceleratorContext : IEquatable<AcceleratorContext>
{
    /// <summary>
    /// Gets the context handle.
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Gets the device ID associated with this context.
    /// </summary>
    public int DeviceId { get; }

    /// <summary>
    /// Gets whether this is a valid context.
    /// </summary>
    public bool IsValid => Handle != IntPtr.Zero;

    /// <summary>
    /// Creates a new accelerator context.
    /// </summary>
    /// <param name="handle">The context handle.</param>
    /// <param name="deviceId">The device ID.</param>
    public AcceleratorContext(IntPtr handle, int deviceId)
    {
        Handle = handle;
        DeviceId = deviceId;
    }

    /// <summary>
    /// Creates an invalid context.
    /// </summary>
    public static AcceleratorContext Invalid => new(IntPtr.Zero, -1);

    public bool Equals(AcceleratorContext other) => Handle == other.Handle && DeviceId == other.DeviceId;

    public override bool Equals(object? obj) => obj is AcceleratorContext other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Handle, DeviceId);

    public static bool operator ==(AcceleratorContext left, AcceleratorContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AcceleratorContext left, AcceleratorContext right)
    {
        return !left.Equals(right);
    }

    public override string ToString() => IsValid ? $"AcceleratorContext(Device={DeviceId}, Handle={Handle:X})" : "AcceleratorContext(Invalid)";
}
