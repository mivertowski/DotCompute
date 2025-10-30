// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;
using DotCompute.Abstractions.TypeConverters;

namespace DotCompute.Abstractions.Types;

/// <summary>
/// Represents a three-dimensional structure for defining grid and block dimensions in GPU computing.
/// </summary>
/// <remarks>
/// This structure is commonly used to specify the dimensions of thread blocks and grids
/// in GPU kernel execution configurations.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="Dim3"/> struct with specified dimensions.
/// </remarks>
/// <param name="x">The X dimension value.</param>
/// <param name="y">The Y dimension value. Defaults to 1.</param>
/// <param name="z">The Z dimension value. Defaults to 1.</param>
[TypeConverter(typeof(Dim3TypeConverter))]
public readonly struct Dim3(int x, int y = 1, int z = 1) : IEquatable<Dim3>
{
    /// <summary>
    /// Gets the X dimension value.
    /// </summary>
    /// <value>The number of elements in the X dimension.</value>
    public int X { get; } = x;

    /// <summary>
    /// Gets the Y dimension value.
    /// </summary>
    /// <value>The number of elements in the Y dimension.</value>
    public int Y { get; } = y;

    /// <summary>
    /// Gets the Z dimension value.
    /// </summary>
    /// <value>The number of elements in the Z dimension.</value>
    public int Z { get; } = z;

    /// <summary>
    /// Gets the total number of elements across all dimensions.
    /// </summary>
    /// <value>The product of X, Y, and Z dimensions.</value>
    public int Length => X * Y * Z;

    /// <summary>
    /// Gets a value indicating whether any dimension is zero or negative.
    /// </summary>
    /// <value><c>true</c> if any dimension is less than or equal to zero; otherwise, <c>false</c>.</value>
    public bool IsEmpty => X <= 0 || Y <= 0 || Z <= 0;

    /// <summary>
    /// Determines whether the specified <see cref="Dim3"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="Dim3"/> to compare with the current instance.</param>
    /// <returns><c>true</c> if the specified object is equal to the current instance; otherwise, <c>false</c>.</returns>
    public bool Equals(Dim3 other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Determines whether the specified object is equal to the current instance.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns><c>true</c> if the specified object is equal to the current instance; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is Dim3 other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Returns a string representation of the current instance.
    /// </summary>
    /// <returns>A string in the format "(X, Y, Z)".</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";

    /// <summary>
    /// Determines whether two <see cref="Dim3"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><c>true</c> if the instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Dim3 left, Dim3 right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="Dim3"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><c>true</c> if the instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Dim3 left, Dim3 right) => !left.Equals(right);

    /// <summary>
    /// Implicitly converts an integer value to a <see cref="Dim3"/> with all dimensions set to that value.
    /// </summary>
    /// <param name="value">The integer value.</param>
    public static implicit operator Dim3(int value) => new(value);

    /// <summary>
    /// Implicitly converts a two-component tuple to a <see cref="Dim3"/> with Z dimension set to 1.
    /// </summary>
    /// <param name="tuple">The two-component tuple containing X and Y values.</param>
    public static implicit operator Dim3((int x, int y) tuple) => new(tuple.x, tuple.y);

    /// <summary>
    /// Implicitly converts a three-component tuple to a <see cref="Dim3"/>.
    /// </summary>
    /// <param name="tuple">The three-component tuple containing X, Y, and Z values.</param>
    public static implicit operator Dim3((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);


    /// <summary>
    /// Creates a <see cref="Dim3"/> from an integer value with all dimensions set to that value.
    /// </summary>
    /// <param name="value">The integer value for all dimensions.</param>
    /// <returns>A new <see cref="Dim3"/> instance.</returns>
    public static Dim3 FromInt32(int value) => new(value);


    /// <summary>
    /// Creates a <see cref="Dim3"/> from a two-component tuple with Z dimension set to 1.
    /// </summary>
    /// <param name="tuple">The two-component tuple containing X and Y values.</param>
    /// <returns>A new <see cref="Dim3"/> instance.</returns>
    public static Dim3 FromValueTuple((int x, int y) tuple) => new(tuple.x, tuple.y);


    /// <summary>
    /// Creates a <see cref="Dim3"/> from a three-component tuple.
    /// </summary>
    /// <param name="tuple">The three-component tuple containing X, Y, and Z values.</param>
    /// <returns>A new <see cref="Dim3"/> instance.</returns>
    public static Dim3 FromValueTuple((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);

    /// <summary>
    /// Converts this <see cref="Dim3"/> to a <see cref="DotCompute.Abstractions.Kernels.WorkDimensions"/>.
    /// </summary>
    /// <returns>A WorkDimensions instance with the same dimensions.</returns>
    public DotCompute.Abstractions.Kernels.WorkDimensions ToWorkDimensions() => new(X, Y, Z);

    /// <summary>
    /// Creates a <see cref="Dim3"/> from a <see cref="DotCompute.Abstractions.Kernels.WorkDimensions"/>.
    /// </summary>
    /// <param name="workDimensions">The WorkDimensions instance to convert.</param>
    /// <returns>A Dim3 instance with the same dimensions, clamped to int range.</returns>
    /// <remarks>
    /// This conversion may result in data loss if any dimension exceeds <see cref="int.MaxValue"/>.
    /// Dimensions are clamped to the range [1, <see cref="int.MaxValue"/>].
    /// </remarks>
    public static Dim3 FromWorkDimensions(DotCompute.Abstractions.Kernels.WorkDimensions workDimensions)
    {
        // Clamp to int range with proper overflow handling
        static int ClampToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)Math.Max(1, value);

        return new Dim3(
            ClampToInt(workDimensions.X),
            ClampToInt(workDimensions.Y),
            ClampToInt(workDimensions.Z));
    }

    /// <summary>
    /// Implicitly converts a <see cref="Dim3"/> to <see cref="DotCompute.Abstractions.Kernels.WorkDimensions"/>.
    /// </summary>
    /// <param name="dim3">The Dim3 instance to convert.</param>
    /// <returns>A WorkDimensions instance with the same dimensions.</returns>
    public static implicit operator DotCompute.Abstractions.Kernels.WorkDimensions(Dim3 dim3) => dim3.ToWorkDimensions();

    /// <summary>
    /// Explicitly converts a <see cref="DotCompute.Abstractions.Kernels.WorkDimensions"/> to <see cref="Dim3"/>.
    /// </summary>
    /// <param name="workDimensions">The WorkDimensions instance to convert.</param>
    /// <returns>A Dim3 instance with the same dimensions, clamped to int range.</returns>
    /// <remarks>
    /// This conversion may result in data loss if any dimension exceeds <see cref="int.MaxValue"/>.
    /// Dimensions are clamped to the range [1, <see cref="int.MaxValue"/>].
    /// </remarks>
    public static explicit operator Dim3(DotCompute.Abstractions.Kernels.WorkDimensions workDimensions) => FromWorkDimensions(workDimensions);
}
