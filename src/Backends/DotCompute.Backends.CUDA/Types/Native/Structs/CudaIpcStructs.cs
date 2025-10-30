// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA IPC event handle structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcEventHandle
    {
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;





    }

    /// <summary>
    /// CUDA IPC memory handle structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcMemHandle : IEquatable<CudaIpcMemHandle>
    {
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;

        /// <summary>
        /// Determines whether the specified CudaIpcMemHandle is equal to the current CudaIpcMemHandle.
        /// </summary>
        /// <param name="other">The CudaIpcMemHandle to compare with the current instance.</param>
        /// <returns>true if the specified CudaIpcMemHandle is equal to the current CudaIpcMemHandle; otherwise, false.</returns>
        public readonly bool Equals(CudaIpcMemHandle other)
        {
            if (reserved == null && other.reserved == null)
            {
                return true;
            }
            if (reserved == null || other.reserved == null)
            {
                return false;
            }
            if (reserved.Length != other.reserved.Length)
            {
                return false;
            }

            for (var i = 0; i < reserved.Length; i++)
            {
                if (reserved[i] != other.reserved[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CudaIpcMemHandle.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaIpcMemHandle; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaIpcMemHandle other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode()
        {
            if (reserved == null)
            {
                return 0;
            }

            var hash = new HashCode();
            foreach (var b in reserved)
            {
                hash.Add(b);
            }
            return hash.ToHashCode();
        }

        /// <summary>
        /// Determines whether two specified CudaIpcMemHandle structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaIpcMemHandle to compare.</param>
        /// <param name="right">The second CudaIpcMemHandle to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaIpcMemHandle left, CudaIpcMemHandle right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaIpcMemHandle structures have different values.
        /// </summary>
        /// <param name="left">The first CudaIpcMemHandle to compare.</param>
        /// <param name="right">The second CudaIpcMemHandle to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaIpcMemHandle left, CudaIpcMemHandle right) => !left.Equals(right);
    }
}
