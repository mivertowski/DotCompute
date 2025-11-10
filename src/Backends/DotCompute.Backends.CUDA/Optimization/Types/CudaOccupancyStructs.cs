// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Optimization;

/// <summary>
/// 3D dimension structure for CUDA launch configurations.
/// </summary>
/// <remarks>
/// <para>
/// Represents grid or block dimensions in three-dimensional space. While CUDA
/// supports 3D configurations, most kernels use 1D (X only) or 2D (X, Y) layouts.
/// </para>
/// <para><b>Thread Safety</b>: This structure is immutable and thread-safe.</para>
/// </remarks>
internal readonly struct Dim3(int x, int y, int z) : IEquatable<Dim3>
{
    /// <summary>
    /// Gets the X dimension (width).
    /// </summary>
    /// <remarks>
    /// Primary dimension for 1D kernels. Always populated even for higher-dimensional configurations.
    /// </remarks>
    public int X { get; } = x;

    /// <summary>
    /// Gets the Y dimension (height).
    /// </summary>
    /// <remarks>
    /// Used for 2D and 3D kernels. Set to 1 for 1D kernels.
    /// </remarks>
    public int Y { get; } = y;

    /// <summary>
    /// Gets the Z dimension (depth).
    /// </summary>
    /// <remarks>
    /// Used for 3D kernels. Set to 1 for 1D and 2D kernels.
    /// </remarks>
    public int Z { get; } = z;

    /// <summary>
    /// Returns a string representation of the dimension.
    /// </summary>
    /// <returns>String in format "(X,Y,Z)".</returns>
    public override string ToString() => $"({X},{Y},{Z})";

    /// <summary>
    /// Determines whether the specified object is equal to the current Dim3.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj) => obj is Dim3 other && Equals(other);

    /// <summary>
    /// Determines whether this instance is equal to another Dim3.
    /// </summary>
    /// <param name="other">The other Dim3 to compare.</param>
    /// <returns>True if all dimensions are equal, false otherwise.</returns>
    public bool Equals(Dim3 other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>
    /// Returns the hash code for this Dim3.
    /// </summary>
    /// <returns>Hash code combining all three dimensions.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>
    /// Determines whether two Dim3 instances are equal.
    /// </summary>
    public static bool operator ==(Dim3 left, Dim3 right) => left.Equals(right);

    /// <summary>
    /// Determines whether two Dim3 instances are not equal.
    /// </summary>
    public static bool operator !=(Dim3 left, Dim3 right) => !left.Equals(right);
}

/// <summary>
/// 2D dimension structure for CUDA launch configurations.
/// </summary>
/// <remarks>
/// <para>
/// Simplified 2D dimension type commonly used for image processing and matrix
/// operations. More convenient than Dim3 when depth dimension is not needed.
/// </para>
/// <para><b>Thread Safety</b>: This structure is immutable and thread-safe.</para>
/// </remarks>
internal readonly struct Dim2(int x, int y) : IEquatable<Dim2>
{
    /// <summary>
    /// Gets the X dimension (width/columns).
    /// </summary>
    /// <remarks>
    /// Typically maps to horizontal dimension or column index in matrices.
    /// </remarks>
    public int X { get; } = x;

    /// <summary>
    /// Gets the Y dimension (height/rows).
    /// </summary>
    /// <remarks>
    /// Typically maps to vertical dimension or row index in matrices.
    /// </remarks>
    public int Y { get; } = y;

    /// <summary>
    /// Returns a string representation of the dimension.
    /// </summary>
    /// <returns>String in format "(X,Y)".</returns>
    public override string ToString() => $"({X},{Y})";

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns>True if the current object is equal to the other parameter; otherwise, false.</returns>
    public bool Equals(Dim2 other) => X == other.X && Y == other.Y;

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>True if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj) => obj is Dim2 other && Equals(other);

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>
    /// Indicates whether two instances are equal.
    /// </summary>
    public static bool operator ==(Dim2 left, Dim2 right) => left.Equals(right);

    /// <summary>
    /// Indicates whether two instances are not equal.
    /// </summary>
    public static bool operator !=(Dim2 left, Dim2 right) => !left.Equals(right);
}

/// <summary>
/// CUDA kernel function attributes from cudaFuncGetAttributes API.
/// </summary>
/// <remarks>
/// <para>
/// Interop structure matching the layout of CUDA's cudaFuncAttributes_t.
/// Contains metadata about compiled kernel resource requirements.
/// </para>
/// <para><b>Memory Layout</b>: Must match native CUDA structure exactly (sequential layout).</para>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct CudaFuncAttributes
{
    /// <summary>
    /// Size of statically-allocated shared memory per block in bytes.
    /// </summary>
    /// <remarks>
    /// Does not include dynamically-allocated shared memory specified at launch time.
    /// </remarks>
    public nuint SharedSizeBytes;

    /// <summary>
    /// Size of constant memory used by kernel in bytes.
    /// </summary>
    public nuint ConstSizeBytes;

    /// <summary>
    /// Size of local memory per thread in bytes.
    /// </summary>
    /// <remarks>
    /// Local memory resides in global memory and can impact performance if excessive.
    /// </remarks>
    public nuint LocalSizeBytes;

    /// <summary>
    /// Maximum threads per block supported by this kernel.
    /// </summary>
    /// <remarks>
    /// May be lower than device maximum if kernel has high resource requirements.
    /// </remarks>
    public int MaxThreadsPerBlock;

    /// <summary>
    /// Number of 32-bit registers used per thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Critical for occupancy calculation. High register usage limits concurrent blocks.
    /// </para>
    /// <para><b>Optimization</b>: Use --maxrregcount or __launch_bounds__ to control.</para>
    /// </remarks>
    public int NumRegs;

    /// <summary>
    /// PTX version of compiled kernel.
    /// </summary>
    public int PtxVersion;

    /// <summary>
    /// Binary version of compiled kernel.
    /// </summary>
    public int BinaryVersion;

    /// <summary>
    /// Preferred cache configuration for this kernel.
    /// </summary>
    public IntPtr CacheModeCA;

    /// <summary>
    /// Maximum dynamic shared memory per block that can be used by this kernel.
    /// </summary>
    /// <remarks>
    /// Hardware limit that may be lower than per-block shared memory if kernel
    /// uses statically-allocated shared memory.
    /// </remarks>
    public int MaxDynamicSharedSizeBytes;

    /// <summary>
    /// Preferred shared memory carveout (percentage of L1 cache used as shared memory).
    /// </summary>
    public int PreferredShmemCarveout;

    /// <summary>
    /// Pointer to null-terminated kernel name string.
    /// </summary>
    /// <remarks>
    /// Use KernelName property to safely access as managed string.
    /// </remarks>
    public IntPtr Name;

    /// <summary>
    /// Gets the kernel name as a managed string.
    /// </summary>
    /// <remarks>
    /// Safely marshals the native string pointer. Returns "Unknown" if marshaling fails
    /// or if Name is IntPtr.Zero.
    /// </remarks>
    public readonly string KernelName
    {
        get
        {
            try
            {
                return Name != IntPtr.Zero ? Marshal.PtrToStringAnsi(Name) ?? "Unknown" : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
