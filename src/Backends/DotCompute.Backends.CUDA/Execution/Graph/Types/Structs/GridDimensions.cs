// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Structs
{
    /// <summary>
    /// Represents the dimensions of a CUDA grid in 3D space.
    /// A grid is composed of thread blocks, and grid dimensions specify the number
    /// of blocks launched along each axis during kernel execution.
    /// </summary>
    /// <remarks>
    /// Each dimension must be at least 1. The total number of blocks (X * Y * Z)
    /// determines the overall parallelism of the kernel launch. Grid dimensions
    /// are typically calculated based on problem size and block dimensions.
    /// </remarks>
    public readonly struct GridDimensions(uint x, uint y = 1, uint z = 1) : IEquatable<GridDimensions>
    {
        /// <summary>
        /// Gets the number of thread blocks in the X dimension of the grid.
        /// </summary>
        /// <value>The block count in the X dimension. Must be at least 1.</value>
        public uint X { get; } = x;

        /// <summary>
        /// Gets the number of thread blocks in the Y dimension of the grid.
        /// </summary>
        /// <value>The block count in the Y dimension. Defaults to 1 for 1D workloads.</value>
        public uint Y { get; } = y;

        /// <summary>
        /// Gets the number of thread blocks in the Z dimension of the grid.
        /// </summary>
        /// <value>The block count in the Z dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Z { get; } = z;

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object? obj) => obj is GridDimensions other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="GridDimensions"/> is equal to the current instance.
        /// </summary>
        /// <param name="other">The <see cref="GridDimensions"/> to compare with the current instance.</param>
        /// <returns>true if the specified instance is equal to the current instance; otherwise, false.</returns>
        public bool Equals(GridDimensions other) => X == other.X && Y == other.Y && Z == other.Z;

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        /// <summary>
        /// Determines whether two <see cref="GridDimensions"/> instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(GridDimensions left, GridDimensions right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="GridDimensions"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are not equal; otherwise, false.</returns>
        public static bool operator !=(GridDimensions left, GridDimensions right) => !left.Equals(right);
    }
}
