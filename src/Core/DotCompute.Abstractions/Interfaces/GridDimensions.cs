// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces;

/// <summary>
/// Represents 3D grid or block dimensions for kernel execution.
/// </summary>
/// <remarks>
/// Grid dimensions specify how many thread blocks to launch (CUDA) or threadgroups to dispatch (Metal).
/// Block dimensions specify how many threads per block/threadgroup.
/// Supports 1D, 2D, and 3D configurations for flexible workload mapping.
/// </remarks>
public readonly struct GridDimensions : IEquatable<GridDimensions>
{
    /// <summary>
    /// Gets the X dimension (width).
    /// </summary>
    public int X { get; init; }

    /// <summary>
    /// Gets the Y dimension (height). Defaults to 1 for 1D grids.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    /// Gets the Z dimension (depth). Defaults to 1 for 1D/2D grids.
    /// </summary>
    public int Z { get; init; }

    /// <summary>
    /// Gets the total number of elements (X × Y × Z).
    /// </summary>
    public int TotalElements => X * Y * Z;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridDimensions"/> struct.
    /// </summary>
    /// <param name="x">The X dimension (width).</param>
    /// <param name="y">The Y dimension (height). Defaults to 1.</param>
    /// <param name="z">The Z dimension (depth). Defaults to 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any dimension is less than or equal to zero.
    /// </exception>
    public GridDimensions(int x, int y = 1, int z = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(x, 0, nameof(x));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(y, 0, nameof(y));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(z, 0, nameof(z));

        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// Creates a 1D grid dimension.
    /// </summary>
    /// <param name="size">The size of the 1D grid.</param>
    /// <returns>A <see cref="GridDimensions"/> with the specified X dimension and Y=Z=1.</returns>
    public static GridDimensions OneDimensional(int size) => new(size, 1, 1);

    /// <summary>
    /// Creates a 2D grid dimension.
    /// </summary>
    /// <param name="width">The width (X dimension).</param>
    /// <param name="height">The height (Y dimension).</param>
    /// <returns>A <see cref="GridDimensions"/> with the specified X and Y dimensions and Z=1.</returns>
    public static GridDimensions TwoDimensional(int width, int height) => new(width, height, 1);

    /// <summary>
    /// Creates a 3D grid dimension.
    /// </summary>
    /// <param name="width">The width (X dimension).</param>
    /// <param name="height">The height (Y dimension).</param>
    /// <param name="depth">The depth (Z dimension).</param>
    /// <returns>A <see cref="GridDimensions"/> with all three dimensions specified.</returns>
    public static GridDimensions ThreeDimensional(int width, int height, int depth) => new(width, height, depth);

    /// <summary>
    /// Creates a <see cref="GridDimensions"/> from a value tuple (alternate for implicit operator).
    /// </summary>
    /// <param name="tuple">A tuple containing (x, y, z) dimensions.</param>
    /// <returns>A <see cref="GridDimensions"/> with the specified dimensions.</returns>
    public static GridDimensions FromValueTuple((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);

    /// <summary>
    /// Creates a 1D <see cref="GridDimensions"/> from an integer (alternate for implicit operator).
    /// </summary>
    /// <param name="size">The size of the 1D grid.</param>
    /// <returns>A <see cref="GridDimensions"/> with X=size and Y=Z=1.</returns>
    public static GridDimensions FromInt32(int size) => new(size, 1, 1);

    /// <summary>
    /// Implicitly converts a tuple to <see cref="GridDimensions"/>.
    /// </summary>
    /// <param name="tuple">A tuple containing (x, y, z) dimensions.</param>
    public static implicit operator GridDimensions((int x, int y, int z) tuple) => FromValueTuple(tuple);

    /// <summary>
    /// Implicitly converts a single integer to a 1D <see cref="GridDimensions"/>.
    /// </summary>
    /// <param name="size">The size of the 1D grid.</param>
    public static implicit operator GridDimensions(int size) => FromInt32(size);

    /// <summary>
    /// Determines whether this instance is equal to another <see cref="GridDimensions"/>.
    /// </summary>
    /// <param name="other">The other <see cref="GridDimensions"/> to compare.</param>
    /// <returns><c>true</c> if the dimensions are equal; otherwise, <c>false</c>.</returns>
    public bool Equals(GridDimensions other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Determines whether this instance is equal to another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><c>true</c> if the object is a <see cref="GridDimensions"/> with equal dimensions; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is GridDimensions other && Equals(other);

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>A hash code combining X, Y, and Z dimensions.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Returns a string representation of this instance.
    /// </summary>
    /// <returns>A string in the format "(X, Y, Z)" for debugging.</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";

    /// <summary>
    /// Determines whether two <see cref="GridDimensions"/> instances are equal.
    /// </summary>
    public static bool operator ==(GridDimensions left, GridDimensions right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="GridDimensions"/> instances are not equal.
    /// </summary>
    public static bool operator !=(GridDimensions left, GridDimensions right) => !left.Equals(right);
}
