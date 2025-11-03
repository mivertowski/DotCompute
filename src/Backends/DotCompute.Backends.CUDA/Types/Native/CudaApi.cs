// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native;

namespace DotCompute.Backends.CUDA.Native;

/// <summary>
/// CUDA Driver API wrappers for memory management and module operations.
/// </summary>
/// <remarks>
/// Provides P/Invoke bindings to the CUDA Driver API for low-level GPU operations.
/// Used primarily by Ring Kernels for direct memory and kernel management.
/// </remarks>
public static partial class CudaApi
{
#if WINDOWS
    private const string CUDA_DRIVER_LIBRARY = "nvcuda";
#else
    private const string CUDA_DRIVER_LIBRARY = "cuda";
#endif

    #region Memory Management

    /// <summary>
    /// Allocates device memory.
    /// </summary>
    /// <param name="dptr">Pointer to allocated device memory.</param>
    /// <param name="bytesize">Size in bytes to allocate.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuMemAlloc(ref IntPtr dptr, nuint bytesize);

    /// <summary>
    /// Frees device memory.
    /// </summary>
    /// <param name="dptr">Pointer to device memory to free.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuMemFree(IntPtr dptr);

    /// <summary>
    /// Copies memory from host to device.
    /// </summary>
    /// <param name="dstDevice">Destination device pointer.</param>
    /// <param name="srcHost">Source host pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, nuint byteCount);

    /// <summary>
    /// Copies memory from device to host.
    /// </summary>
    /// <param name="dstHost">Destination host pointer.</param>
    /// <param name="srcDevice">Source device pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, nuint byteCount);

    /// <summary>
    /// Sets device memory to a value.
    /// </summary>
    /// <param name="dstDevice">Destination device pointer.</param>
    /// <param name="uc">Value to set (unsigned char).</param>
    /// <param name="n">Number of bytes to set.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuMemsetD8(IntPtr dstDevice, byte uc, nuint n);

    #endregion

    #region Module Management

    /// <summary>
    /// Unloads a module.
    /// </summary>
    /// <param name="hmod">Module handle.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    public static partial CudaError cuModuleUnload(IntPtr hmod);

    #endregion
}
