// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Execution.Graph.Types.Structs
{
    /// <summary>
    /// Represents the dimensions of a CUDA thread block in 3D space.
    /// Thread blocks are the fundamental unit of parallel execution in CUDA,
    /// containing threads that can cooperate through shared memory and synchronization.
    /// </summary>
    /// <remarks>
    /// Each dimension must be at least 1. The total number of threads (X * Y * Z)
    /// is limited by the device's maxThreadsPerBlock attribute, typically 1024.
    /// </remarks>
    public readonly struct BlockDimensions(uint x, uint y = 1, uint z = 1) : IEquatable<BlockDimensions>
    {
        /// <summary>
        /// Gets the number of threads in the X dimension of the block.
        /// </summary>
        /// <value>The thread count in the X dimension. Must be at least 1.</value>
        public uint X { get; } = x;

        /// <summary>
        /// Gets the number of threads in the Y dimension of the block.
        /// </summary>
        /// <value>The thread count in the Y dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Y { get; } = y;

        /// <summary>
        /// Gets the number of threads in the Z dimension of the block.
        /// </summary>
        /// <value>The thread count in the Z dimension. Defaults to 1 for 1D/2D workloads.</value>
        public uint Z { get; } = z;

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override bool Equals(object? obj) => obj is BlockDimensions other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="BlockDimensions"/> is equal to the current instance.
        /// </summary>
        /// <param name="other">The <see cref="BlockDimensions"/> to compare with the current instance.</param>
        /// <returns>true if the specified instance is equal to the current instance; otherwise, false.</returns>
        public bool Equals(BlockDimensions other) => X == other.X && Y == other.Y && Z == other.Z;

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        /// <summary>
        /// Determines whether two <see cref="BlockDimensions"/> instances are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are equal; otherwise, false.</returns>
        public static bool operator ==(BlockDimensions left, BlockDimensions right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="BlockDimensions"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if the instances are not equal; otherwise, false.</returns>
        public static bool operator !=(BlockDimensions left, BlockDimensions right) => !left.Equals(right);
    }
}
