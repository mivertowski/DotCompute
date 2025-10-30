// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA context creation parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaContextCreateParams
    {
        /// <summary>
        /// The ctx.
        /// </summary>
        public nint ctx;
        /// <summary>
        /// The flags.
        /// </summary>
        public uint flags;
        /// <summary>
        /// The nv sci sync attr list.
        /// </summary>
        public nint nvSciSyncAttrList;
        /// <summary>
        /// The priority.
        /// </summary>
        public uint priority;





    }

    /// <summary>
    /// CUDA array format structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaArrayFormat
    {
        /// <summary>
        /// The x.
        /// </summary>
        public uint x;
        /// <summary>
        /// The y.
        /// </summary>
        public uint y;
        /// <summary>
        /// The z.
        /// </summary>
        public uint z;
        /// <summary>
        /// The w.
        /// </summary>
        public uint w;
        /// <summary>
        /// The f.
        /// </summary>
        public CudaArrayFormatKind f;





    }

    /// <summary>
    /// CUDA extent structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExtent : IEquatable<CudaExtent>
    {
        /// <summary>
        /// The width.
        /// </summary>
        public nuint width;
        /// <summary>
        /// The height.
        /// </summary>
        public nuint height;
        /// <summary>
        /// The depth.
        /// </summary>
        public nuint depth;

        /// <summary>
        /// Determines whether this instance is equal to another CudaExtent.
        /// </summary>
        /// <param name="other">The other CudaExtent to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public readonly bool Equals(CudaExtent other)
        {
            return width == other.width &&
                   height == other.height &&
                   depth == other.depth;
        }

        /// <summary>
        /// Determines whether this instance is equal to another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaExtent other && Equals(other);

        /// <summary>
        /// Gets the hash code for this instance.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(width, height, depth);

        /// <summary>
        /// Determines whether two CudaExtent instances are equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if equal; otherwise, false.</returns>
        public static bool operator ==(CudaExtent left, CudaExtent right) => left.Equals(right);

        /// <summary>
        /// Determines whether two CudaExtent instances are not equal.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <returns>True if not equal; otherwise, false.</returns>
        public static bool operator !=(CudaExtent left, CudaExtent right) => !left.Equals(right);
    }
}
