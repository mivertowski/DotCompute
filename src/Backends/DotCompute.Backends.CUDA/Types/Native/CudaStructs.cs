// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Native.Types
{

    /// <summary>
    /// DEPRECATED: Use DotCompute.Backends.CUDA.Types.Native.CudaKernelNodeParams from CudaGraphTypes.cs instead.
    /// This struct is kept for backward compatibility during migration.
    /// </summary>
    [Obsolete("Use DotCompute.Backends.CUDA.Types.Native.CudaKernelNodeParams from CudaGraphTypes.cs instead.", false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaKernelNodeParams : IEquatable<CudaKernelNodeParams>
    {
        /// <summary>
        /// The function.
        /// </summary>
        public IntPtr Function;
        /// <summary>
        /// The grid dim x.
        /// </summary>
        public uint GridDimX, GridDimY, GridDimZ;
        /// <summary>
        /// The block dim x.
        /// </summary>
        public uint BlockDimX, BlockDimY, BlockDimZ;
        /// <summary>
        /// The shared mem bytes.
        /// </summary>
        public uint SharedMemBytes;
        /// <summary>
        /// The kernel params.
        /// </summary>
        public IntPtr KernelParams;
        /// <summary>
        /// The extra.
        /// </summary>
        public IntPtr Extra;

        /// <summary>
        /// Determines whether this instance is equal to another CudaKernelNodeParams.
        /// </summary>
        /// <param name="other">The other CudaKernelNodeParams to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly bool Equals(CudaKernelNodeParams other)
        {
            return Function == other.Function &&
                   GridDimX == other.GridDimX &&
                   GridDimY == other.GridDimY &&
                   GridDimZ == other.GridDimZ &&
                   BlockDimX == other.BlockDimX &&
                   BlockDimY == other.BlockDimY &&
                   BlockDimZ == other.BlockDimZ &&
                   SharedMemBytes == other.SharedMemBytes &&
                   KernelParams == other.KernelParams &&
                   Extra == other.Extra;
        }

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaKernelNodeParams other && Equals(other);

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Function);
            hash.Add(GridDimX);
            hash.Add(GridDimY);
            hash.Add(GridDimZ);
            hash.Add(BlockDimX);
            hash.Add(BlockDimY);
            hash.Add(BlockDimZ);
            hash.Add(SharedMemBytes);
            hash.Add(KernelParams);
            hash.Add(Extra);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Determines whether two CudaKernelNodeParams instances are equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public static bool operator ==(CudaKernelNodeParams left, CudaKernelNodeParams right) => left.Equals(right);

        /// <summary>
        /// Determines whether two CudaKernelNodeParams instances are not equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if not equal; otherwise, false.</returns>
        public static bool operator !=(CudaKernelNodeParams left, CudaKernelNodeParams right) => !left.Equals(right);
    }
}
