// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Enums;

namespace DotCompute.Backends.CUDA.Types.Native.Structs
{
    /// <summary>
    /// CUDA memory pool properties structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemPoolProps : IEquatable<CudaMemPoolProps>
    {
        /// <summary>
        /// The alloc type.
        /// </summary>
        public CudaMemAllocationType AllocType;
        /// <summary>
        /// The handle types.
        /// </summary>
        public CudaMemAllocationHandleType HandleTypes;
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation Location;
        /// <summary>
        /// The win32 security attributes.
        /// </summary>
        public nint win32SecurityAttributes;
        /// <summary>
        /// The max size.
        /// </summary>
        public nuint maxSize;
        /// <summary>
        /// The usage.
        /// </summary>
        public ushort usage;
        /// <summary>
        /// The reserved.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
        public byte[] reserved;
        /// <summary>
        /// Gets or sets the alloc type.
        /// </summary>
        /// <value>The alloc type.</value>

        // Legacy properties for backward compatibility
        public CudaMemAllocationType allocType { get => AllocType; set => AllocType = value; }
        /// <summary>
        /// Gets or sets the handle types.
        /// </summary>
        /// <value>The handle types.</value>
        public CudaMemAllocationHandleType handleTypes { get => HandleTypes; set => HandleTypes = value; }
        /// <summary>
        /// Gets or sets the location.
        /// </summary>
        /// <value>The location.</value>
        public CudaMemLocation location { get => Location; set => Location = value; }

        /// <summary>
        /// Determines whether the specified CudaMemPoolProps is equal to the current CudaMemPoolProps.
        /// </summary>
        /// <param name="other">The CudaMemPoolProps to compare with the current instance.</param>
        /// <returns>true if the specified CudaMemPoolProps is equal to the current CudaMemPoolProps; otherwise, false.</returns>
        public readonly bool Equals(CudaMemPoolProps other)
        {
            if (AllocType != other.AllocType ||
                HandleTypes != other.HandleTypes ||
                !Location.Equals(other.Location) ||
                win32SecurityAttributes != other.win32SecurityAttributes ||
                maxSize != other.maxSize ||
                usage != other.usage)
            {
                return false;
            }

            // Compare byte arrays
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
        /// Determines whether the specified object is equal to the current CudaMemPoolProps.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaMemPoolProps; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaMemPoolProps other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(AllocType);
            hash.Add(HandleTypes);
            hash.Add(Location);
            hash.Add(win32SecurityAttributes);
            hash.Add(maxSize);
            hash.Add(usage);

            if (reserved != null)
            {
                foreach (var b in reserved)
                {
                    hash.Add(b);
                }
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// Determines whether two specified CudaMemPoolProps structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaMemPoolProps to compare.</param>
        /// <param name="right">The second CudaMemPoolProps to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaMemPoolProps left, CudaMemPoolProps right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaMemPoolProps structures have different values.
        /// </summary>
        /// <param name="left">The first CudaMemPoolProps to compare.</param>
        /// <param name="right">The second CudaMemPoolProps to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaMemPoolProps left, CudaMemPoolProps right) => !left.Equals(right);
    }

    /// <summary>
    /// CUDA memory location structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemLocation : IEquatable<CudaMemLocation>
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaMemLocationType Type;
        /// <summary>
        /// The id.
        /// </summary>
        public int Id;
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>The type.</value>

        // Legacy properties for backward compatibility
        public CudaMemLocationType type { get => Type; set => Type = value; }
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        /// <value>The id.</value>
        public int id { get => Id; set => Id = value; }

        /// <summary>
        /// Determines whether the specified CudaMemLocation is equal to the current CudaMemLocation.
        /// </summary>
        /// <param name="other">The CudaMemLocation to compare with the current instance.</param>
        /// <returns>true if the specified CudaMemLocation is equal to the current CudaMemLocation; otherwise, false.</returns>
        public readonly bool Equals(CudaMemLocation other) => Type == other.Type && Id == other.Id;

        /// <summary>
        /// Determines whether the specified object is equal to the current CudaMemLocation.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaMemLocation; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaMemLocation other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(Type, Id);

        /// <summary>
        /// Determines whether two specified CudaMemLocation structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaMemLocation to compare.</param>
        /// <param name="right">The second CudaMemLocation to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaMemLocation left, CudaMemLocation right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaMemLocation structures have different values.
        /// </summary>
        /// <param name="left">The first CudaMemLocation to compare.</param>
        /// <param name="right">The second CudaMemLocation to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaMemLocation left, CudaMemLocation right) => !left.Equals(right);
    }

    /// <summary>
    /// CUDA memory access descriptor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAccessDesc : IEquatable<CudaMemAccessDesc>
    {
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation location;
        /// <summary>
        /// The flags.
        /// </summary>
        public CudaMemAccessFlags flags;

        /// <summary>
        /// Determines whether the specified CudaMemAccessDesc is equal to the current CudaMemAccessDesc.
        /// </summary>
        /// <param name="other">The CudaMemAccessDesc to compare with the current instance.</param>
        /// <returns>true if the specified CudaMemAccessDesc is equal to the current CudaMemAccessDesc; otherwise, false.</returns>
        public readonly bool Equals(CudaMemAccessDesc other) => location.Equals(other.location) && flags == other.flags;

        /// <summary>
        /// Determines whether the specified object is equal to the current CudaMemAccessDesc.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaMemAccessDesc; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaMemAccessDesc other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(location, flags);

        /// <summary>
        /// Determines whether two specified CudaMemAccessDesc structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaMemAccessDesc to compare.</param>
        /// <param name="right">The second CudaMemAccessDesc to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaMemAccessDesc left, CudaMemAccessDesc right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaMemAccessDesc structures have different values.
        /// </summary>
        /// <param name="left">The first CudaMemAccessDesc to compare.</param>
        /// <param name="right">The second CudaMemAccessDesc to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaMemAccessDesc left, CudaMemAccessDesc right) => !left.Equals(right);
    }

    /// <summary>
    /// CUDA memory allocation properties structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocationProp
    {
        /// <summary>
        /// The type.
        /// </summary>
        public CudaMemAllocationType type;
        /// <summary>
        /// The requested handle types.
        /// </summary>
        public CudaMemAllocationHandleType requestedHandleTypes;
        /// <summary>
        /// The location.
        /// </summary>
        public CudaMemLocation location;
        /// <summary>
        /// The win32 handle meta data.
        /// </summary>
        public nint win32HandleMetaData;
        /// <summary>
        /// The compression type.
        /// </summary>
        public CudaMemAllocationCompType compressionType;
        /// <summary>
        /// The usage.
        /// </summary>
        public ushort usage;





    }

    /// <summary>
    /// CUDA memory allocation node parameters structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocNodeParams : IEquatable<CudaMemAllocNodeParams>
    {
        /// <summary>
        /// The pool props.
        /// </summary>
        public CudaMemPoolProps poolProps;
        /// <summary>
        /// The access descs.
        /// </summary>
        public CudaMemAccessDesc accessDescs;
        /// <summary>
        /// The access desc count.
        /// </summary>
        public nuint accessDescCount;
        /// <summary>
        /// The bytesize.
        /// </summary>
        public nuint bytesize;
        /// <summary>
        /// The dptr.
        /// </summary>
        public nint dptr;

        /// <summary>
        /// Determines whether the specified CudaMemAllocNodeParams is equal to the current CudaMemAllocNodeParams.
        /// </summary>
        /// <param name="other">The CudaMemAllocNodeParams to compare with the current instance.</param>
        /// <returns>true if the specified CudaMemAllocNodeParams is equal to the current CudaMemAllocNodeParams; otherwise, false.</returns>
        public readonly bool Equals(CudaMemAllocNodeParams other)
        {
            return poolProps.Equals(other.poolProps) &&
                   accessDescs.Equals(other.accessDescs) &&
                   accessDescCount == other.accessDescCount &&
                   bytesize == other.bytesize &&
                   dptr == other.dptr;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current CudaMemAllocNodeParams.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>true if the specified object is equal to the current CudaMemAllocNodeParams; otherwise, false.</returns>
        public override readonly bool Equals(object? obj) => obj is CudaMemAllocNodeParams other && Equals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override readonly int GetHashCode() => HashCode.Combine(poolProps, accessDescs, accessDescCount, bytesize, dptr);

        /// <summary>
        /// Determines whether two specified CudaMemAllocNodeParams structures have the same value.
        /// </summary>
        /// <param name="left">The first CudaMemAllocNodeParams to compare.</param>
        /// <param name="right">The second CudaMemAllocNodeParams to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(CudaMemAllocNodeParams left, CudaMemAllocNodeParams right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified CudaMemAllocNodeParams structures have different values.
        /// </summary>
        /// <param name="left">The first CudaMemAllocNodeParams to compare.</param>
        /// <param name="right">The second CudaMemAllocNodeParams to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(CudaMemAllocNodeParams left, CudaMemAllocNodeParams right) => !left.Equals(right);
    }
}