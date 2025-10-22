// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA batch memory operation node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaBatchMemOpNodeParams : IEquatable<CudaBatchMemOpNodeParams>
    {
        /// <summary>
        /// The ctx.
        /// </summary>
        public nint ctx;
        /// <summary>
        /// The count.
        /// </summary>
        public uint count;
        /// <summary>
        /// The param array.
        /// </summary>
        public nint paramArray;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;

        /// <summary>
        /// Determines whether the specified CudaBatchMemOpNodeParams is equal to the current instance.
        /// </summary>
        /// <param name="other">The CudaBatchMemOpNodeParams to compare with the current instance.</param>
        /// <returns>true if the specified CudaBatchMemOpNodeParams is equal to the current instance; otherwise, false.</returns>
        public readonly bool Equals(CudaBatchMemOpNodeParams other)
        {
            return ctx == other.ctx &&
                   count == other.count &&
                   paramArray == other.paramArray &&
                   flags == other.flags;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current instance; otherwise, false.</returns>
        public override readonly bool Equals(object? obj)
        {
            return obj is CudaBatchMemOpNodeParams other && Equals(other);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(ctx, count, paramArray, flags);
        }

        /// <summary>
        /// Determines whether two specified instances of CudaBatchMemOpNodeParams are equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaBatchMemOpNodeParams left, CudaBatchMemOpNodeParams right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified instances of CudaBatchMemOpNodeParams are not equal.
        /// </summary>
        /// <param name="left">The first instance to compare.</param>
        /// <param name="right">The second instance to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaBatchMemOpNodeParams left, CudaBatchMemOpNodeParams right)
        {
            return !left.Equals(right);
        }
    }
}