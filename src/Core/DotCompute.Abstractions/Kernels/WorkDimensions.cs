// <copyright file="WorkDimensions.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents the work dimensions for kernel execution, defining the global and local work sizes
/// across up to three dimensions (X, Y, Z).
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="WorkDimensions"/> struct.
/// </remarks>
/// <param name="x">The X dimension size.</param>
/// <param name="y">The Y dimension size (default 1).</param>
/// <param name="z">The Z dimension size (default 1).</param>
public struct WorkDimensions(long x, long y = 1, long z = 1) : IEquatable<WorkDimensions>
{
    /// <summary>
    /// Gets or sets the X dimension size.
    /// </summary>
    public long X { get; set; } = x > 0 ? x : throw new ArgumentOutOfRangeException(nameof(x), "Must be greater than 0");

    /// <summary>
    /// Gets or sets the Y dimension size.
    /// </summary>
    public long Y { get; set; } = y > 0 ? y : throw new ArgumentOutOfRangeException(nameof(y), "Must be greater than 0");

    /// <summary>
    /// Gets or sets the Z dimension size.
    /// </summary>
    public long Z { get; set; } = z > 0 ? z : throw new ArgumentOutOfRangeException(nameof(z), "Must be greater than 0");

    /// <summary>
    /// Gets the number of active dimensions (1, 2, or 3).
    /// </summary>
    public int Dimensions => Z > 1 ? 3 : Y > 1 ? 2 : 1;

    /// <summary>
    /// Gets the total number of work items across all dimensions.
    /// </summary>
    public long TotalWorkItems => X * Y * Z;

    /// <summary>
    /// Creates a one-dimensional work configuration.
    /// </summary>
    /// <param name="size">The size of the single dimension.</param>
    /// <returns>A new <see cref="WorkDimensions"/> instance.</returns>
    public static WorkDimensions Create1D(long size) => new(size);

    /// <summary>
    /// Creates a two-dimensional work configuration.
    /// </summary>
    /// <param name="x">The X dimension size.</param>
    /// <param name="y">The Y dimension size.</param>
    /// <returns>A new <see cref="WorkDimensions"/> instance.</returns>
    public static WorkDimensions Create2D(long x, long y) => new(x, y);

    /// <summary>
    /// Creates a three-dimensional work configuration.
    /// </summary>
    /// <param name="x">The X dimension size.</param>
    /// <param name="y">The Y dimension size.</param>
    /// <param name="z">The Z dimension size.</param>
    /// <returns>A new <see cref="WorkDimensions"/> instance.</returns>
    public static WorkDimensions Create3D(long x, long y, long z) => new(x, y, z);

    /// <summary>
    /// Converts the work dimensions to an array.
    /// </summary>
    /// <returns>An array containing the dimensions.</returns>
    public long[] ToArray()
    {
        return Dimensions switch
        {
            1 => [X],
            2 => [X, Y],
            3 => [X, Y, Z],
            _ => [X, Y, Z]
        };
    }

    /// <summary>
    /// Creates work dimensions from an array.
    /// </summary>
    /// <param name="dimensions">The dimension array.</param>
    /// <returns>A new <see cref="WorkDimensions"/> instance.</returns>
    public static WorkDimensions FromArray(long[]? dimensions)
    {
        if (dimensions == null || dimensions.Length == 0)
        {
            return new WorkDimensions(1);
        }

        return dimensions.Length switch
        {
            1 => new WorkDimensions(dimensions[0]),
            2 => new WorkDimensions(dimensions[0], dimensions[1]),
            _ => new WorkDimensions(dimensions[0], dimensions.Length > 1 ? dimensions[1] : 1, dimensions.Length > 2 ? dimensions[2] : 1)
        };
    }

    /// <inheritdoc />
    public override string ToString() => Dimensions switch
    {
        1 => $"({X})",
        2 => $"({X}, {Y})",
        3 => $"({X}, {Y}, {Z})",
        _ => $"({X}, {Y}, {Z})"
    };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WorkDimensions other && Equals(other);

    /// <inheritdoc />
    public bool Equals(WorkDimensions other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Determines whether two <see cref="WorkDimensions"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance.</param>
    /// <param name="right">The second instance.</param>
    /// <returns>True if equal; otherwise, false.</returns>
    public static bool operator ==(WorkDimensions left, WorkDimensions right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="WorkDimensions"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance.</param>
    /// <param name="right">The second instance.</param>
    /// <returns>True if not equal; otherwise, false.</returns>
    public static bool operator !=(WorkDimensions left, WorkDimensions right) => !left.Equals(right);

    /// <summary>
    /// Creates a <see cref="WorkDimensions"/> from a <see cref="DotCompute.Abstractions.Types.Dim3"/>.
    /// </summary>
    /// <param name="dim3">The Dim3 instance to convert.</param>
    /// <returns>A WorkDimensions instance with the same dimensions.</returns>
    public static WorkDimensions FromDim3(DotCompute.Abstractions.Types.Dim3 dim3) => new(dim3.X, dim3.Y, dim3.Z);

    /// <summary>
    /// Converts this <see cref="WorkDimensions"/> to a <see cref="DotCompute.Abstractions.Types.Dim3"/>.
    /// </summary>
    /// <returns>A Dim3 instance with the same dimensions, clamped to int range.</returns>
    /// <remarks>
    /// This conversion may result in data loss if any dimension exceeds <see cref="int.MaxValue"/>.
    /// Dimensions are clamped to the range [1, <see cref="int.MaxValue"/>].
    /// </remarks>
    public readonly DotCompute.Abstractions.Types.Dim3 ToDim3()
    {
        // Clamp to int range with proper overflow handling
        static int ClampToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)Math.Max(1, value);

        return new DotCompute.Abstractions.Types.Dim3(
            ClampToInt(X),
            ClampToInt(Y),
            ClampToInt(Z));
    }
}
