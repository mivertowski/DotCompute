// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Types;

/// <summary>
/// Represents 3D dimensions for kernel configuration.
/// This is a legacy type for backward compatibility with tests.
/// </summary>
public readonly struct Dimensions3D : IEquatable<Dimensions3D>
{
    /// <summary>
    /// Gets the X dimension.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the Y dimension.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the Z dimension.
    /// </summary>
    public int Z { get; }

    /// <summary>
    /// Initializes a new instance of the Dimensions3D struct.
    /// </summary>
    /// <param name="x">The X dimension.</param>
    /// <param name="y">The Y dimension.</param>
    /// <param name="z">The Z dimension.</param>
    public Dimensions3D(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Implicit conversion from integer to Dimensions3D.
    /// </summary>
    /// <param name="x">The single dimension value to use for all dimensions.</param>
    /// <returns>A new Dimensions3D instance.</returns>
    public static implicit operator Dimensions3D(int x) => new(x, 1, 1);

    /// <summary>
    /// Implicit conversion from tuple to Dimensions3D.
    /// </summary>
    /// <param name="tuple">The tuple to convert.</param>
    /// <returns>A new Dimensions3D instance.</returns>
    public static implicit operator Dimensions3D((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);

    /// <summary>
    /// Implicit conversion to Dim3 for compatibility.
    /// </summary>
    /// <param name="dimensions">The Dimensions3D to convert.</param>
    /// <returns>A new Dim3 instance.</returns>
    public static implicit operator Dim3(Dimensions3D dimensions) => new(dimensions.X, dimensions.Y, dimensions.Z);

    /// <summary>
    /// Implicit conversion from Dim3 for compatibility.
    /// </summary>
    /// <param name="dim3">The Dim3 to convert.</param>
    /// <returns>A new Dimensions3D instance.</returns>
    public static implicit operator Dimensions3D(Dim3 dim3) => new(dim3.X, dim3.Y, dim3.Z);

    /// <summary>
    /// Determines whether this instance is equal to another Dimensions3D.
    /// </summary>
    /// <param name="other">The Dimensions3D to compare with this instance.</param>
    /// <returns>True if the specified Dimensions3D is equal to this instance; otherwise, false.</returns>
    public bool Equals(Dimensions3D other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare with this instance.</param>
    /// <returns>True if the specified object is equal to this instance; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is Dimensions3D other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>The hash code for this instance.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Determines whether two Dimensions3D instances are equal.
    /// </summary>
    /// <param name="left">The first Dimensions3D to compare.</param>
    /// <param name="right">The second Dimensions3D to compare.</param>
    /// <returns>True if the instances are equal; otherwise, false.</returns>
    public static bool operator ==(Dimensions3D left, Dimensions3D right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two Dimensions3D instances are not equal.
    /// </summary>
    /// <param name="left">The first Dimensions3D to compare.</param>
    /// <param name="right">The second Dimensions3D to compare.</param>
    /// <returns>True if the instances are not equal; otherwise, false.</returns>
    public static bool operator !=(Dimensions3D left, Dimensions3D right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Returns a string representation of the dimensions.
    /// </summary>
    /// <returns>A string representation of the dimensions.</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";
}
