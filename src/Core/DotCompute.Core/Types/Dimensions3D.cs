// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Types;

/// <summary>
/// Represents 3D dimensions for kernel configuration.
/// This is a legacy type for backward compatibility with tests.
/// </summary>
public readonly struct Dimensions3D(int x, int y = 1, int z = 1) : IEquatable<Dimensions3D>
{
    /// <summary>
    /// Gets the X dimension.
    /// </summary>
    public int X { get; } = x;

    /// <summary>
    /// Gets the Y dimension.
    /// </summary>
    public int Y { get; } = y;

    /// <summary>
    /// Gets the Z dimension.
    /// </summary>
    public int Z { get; } = z;

    /// <summary>
    /// Implicit conversion from integer to Dimensions3D.
    /// </summary>
    public static implicit operator Dimensions3D(int value) => new(value);

    /// <summary>
    /// Implicit conversion from tuple to Dimensions3D.
    /// </summary>
    public static implicit operator Dimensions3D((int x, int y) value) => new(value.x, value.y);

    /// <summary>
    /// Implicit conversion from tuple to Dimensions3D.
    /// </summary>
    public static implicit operator Dimensions3D((int x, int y, int z) value) => new(value.x, value.y, value.z);

    /// <summary>
    /// Implicit conversion to Dim3 for compatibility.
    /// </summary>
    public static implicit operator DotCompute.Abstractions.Dim3(Dimensions3D dims) => new(dims.X, dims.Y, dims.Z);

    /// <summary>
    /// Implicit conversion from Dim3 for compatibility.
    /// </summary>
    public static implicit operator Dimensions3D(DotCompute.Abstractions.Dim3 dims) => new(dims.X, dims.Y, dims.Z);

    /// <summary>
    /// Determines whether this instance is equal to another Dimensions3D.
    /// </summary>
    public readonly bool Equals(Dimensions3D other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    public override readonly bool Equals(object? obj) => obj is Dimensions3D other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Determines whether two Dimensions3D instances are equal.
    /// </summary>
    public static bool operator ==(Dimensions3D left, Dimensions3D right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two Dimensions3D instances are not equal.
    /// </summary>
    public static bool operator !=(Dimensions3D left, Dimensions3D right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns a string representation of the dimensions.
    /// </summary>
    public override string ToString() => $"({X}, {Y}, {Z})";
}