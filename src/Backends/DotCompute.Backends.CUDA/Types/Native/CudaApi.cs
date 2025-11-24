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
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemAlloc")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemAlloc_Internal(ref IntPtr dptr, nuint bytesize);

    public static CudaError cuMemAlloc(ref IntPtr dptr, nuint bytesize)
        => (CudaError)cuMemAlloc_Internal(ref dptr, bytesize);

    /// <summary>
    /// Frees device memory.
    /// </summary>
    /// <param name="dptr">Pointer to device memory to free.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemFree")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemFree_Internal(IntPtr dptr);

    public static CudaError cuMemFree(IntPtr dptr)
        => (CudaError)cuMemFree_Internal(dptr);

    /// <summary>
    /// Copies memory from host to device.
    /// </summary>
    /// <param name="dstDevice">Destination device pointer.</param>
    /// <param name="srcHost">Source host pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemcpyHtoD")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemcpyHtoD_Internal(IntPtr dstDevice, IntPtr srcHost, nuint byteCount);

    public static CudaError cuMemcpyHtoD(IntPtr dstDevice, IntPtr srcHost, nuint byteCount)
        => (CudaError)cuMemcpyHtoD_Internal(dstDevice, srcHost, byteCount);

    /// <summary>
    /// Copies memory from device to host.
    /// </summary>
    /// <param name="dstHost">Destination host pointer.</param>
    /// <param name="srcDevice">Source device pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemcpyDtoH")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemcpyDtoH_Internal(IntPtr dstHost, IntPtr srcDevice, nuint byteCount);

    public static CudaError cuMemcpyDtoH(IntPtr dstHost, IntPtr srcDevice, nuint byteCount)
        => (CudaError)cuMemcpyDtoH_Internal(dstHost, srcDevice, byteCount);

    /// <summary>
    /// Copies memory from device to host asynchronously.
    /// </summary>
    /// <param name="dstHost">Destination host pointer.</param>
    /// <param name="srcDevice">Source device pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <param name="hStream">Stream for the operation.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemcpyDtoHAsync_v2")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemcpyDtoHAsync_Internal(IntPtr dstHost, IntPtr srcDevice, nuint byteCount, IntPtr hStream);

    // Suppress VSTHRD200: "Async" here refers to GPU-asynchronous operation (CUDA naming), not .NET awaitable
#pragma warning disable VSTHRD200
    public static CudaError cuMemcpyDtoHAsync(IntPtr dstHost, IntPtr srcDevice, nuint byteCount, IntPtr hStream)
        => (CudaError)cuMemcpyDtoHAsync_Internal(dstHost, srcDevice, byteCount, hStream);
#pragma warning restore VSTHRD200

    /// <summary>
    /// Copies memory from host to device asynchronously.
    /// </summary>
    /// <param name="dstDevice">Destination device pointer.</param>
    /// <param name="srcHost">Source host pointer.</param>
    /// <param name="byteCount">Number of bytes to copy.</param>
    /// <param name="hStream">Stream for the operation.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemcpyHtoDAsync_v2")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemcpyHtoDAsync_Internal(IntPtr dstDevice, IntPtr srcHost, nuint byteCount, IntPtr hStream);

    // Suppress VSTHRD200: "Async" here refers to GPU-asynchronous operation (CUDA naming), not .NET awaitable
#pragma warning disable VSTHRD200
    public static CudaError cuMemcpyHtoDAsync(IntPtr dstDevice, IntPtr srcHost, nuint byteCount, IntPtr hStream)
        => (CudaError)cuMemcpyHtoDAsync_Internal(dstDevice, srcHost, byteCount, hStream);
#pragma warning restore VSTHRD200

    /// <summary>
    /// Sets device memory to a value.
    /// </summary>
    /// <param name="dstDevice">Destination device pointer.</param>
    /// <param name="uc">Value to set (unsigned char).</param>
    /// <param name="n">Number of bytes to set.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuMemsetD8")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuMemsetD8_Internal(IntPtr dstDevice, byte uc, nuint n);

    public static CudaError cuMemsetD8(IntPtr dstDevice, byte uc, nuint n)
        => (CudaError)cuMemsetD8_Internal(dstDevice, uc, n);

    #endregion

    #region Stream Management

    /// <summary>
    /// Creates a CUDA stream with specified priority.
    /// </summary>
    /// <param name="phStream">Returned stream handle.</param>
    /// <param name="flags">Stream creation flags.</param>
    /// <param name="priority">Stream priority (lower values = higher priority).</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuStreamCreateWithPriority")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuStreamCreateWithPriority_Internal(ref IntPtr phStream, uint flags, int priority);

    public static CudaError cuStreamCreateWithPriority(ref IntPtr phStream, uint flags, int priority)
        => (CudaError)cuStreamCreateWithPriority_Internal(ref phStream, flags, priority);

    /// <summary>
    /// Destroys a CUDA stream.
    /// </summary>
    /// <param name="hStream">Stream handle to destroy.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuStreamDestroy")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuStreamDestroy_Internal(IntPtr hStream);

    public static CudaError cuStreamDestroy(IntPtr hStream)
        => (CudaError)cuStreamDestroy_Internal(hStream);

    /// <summary>
    /// Creates a CUDA stream with default settings.
    /// </summary>
    /// <param name="phStream">Returned stream handle.</param>
    /// <param name="flags">Stream creation flags (0 = default, 1 = non-blocking).</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuStreamCreate")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuStreamCreate_Internal(ref IntPtr phStream, uint flags);

    public static CudaError cuStreamCreate(ref IntPtr phStream, uint flags)
        => (CudaError)cuStreamCreate_Internal(ref phStream, flags);

    /// <summary>
    /// Waits until all operations in a stream have completed.
    /// </summary>
    /// <param name="hStream">Stream handle.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuStreamSynchronize")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuStreamSynchronize_Internal(IntPtr hStream);

    public static CudaError cuStreamSynchronize(IntPtr hStream)
        => (CudaError)cuStreamSynchronize_Internal(hStream);

    /// <summary>
    /// Gets the stream priority range supported by the current device.
    /// </summary>
    /// <param name="leastPriority">Least priority value (numerically greatest).</param>
    /// <param name="greatestPriority">Greatest priority value (numerically least).</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuCtxGetStreamPriorityRange")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuCtxGetStreamPriorityRange_Internal(ref int leastPriority, ref int greatestPriority);

    public static CudaError cuCtxGetStreamPriorityRange(ref int leastPriority, ref int greatestPriority)
        => (CudaError)cuCtxGetStreamPriorityRange_Internal(ref leastPriority, ref greatestPriority);

    #endregion

    #region Module Management

    /// <summary>
    /// Unloads a module.
    /// </summary>
    /// <param name="hmod">Module handle.</param>
    /// <returns>CUDA error code.</returns>
    [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuModuleUnload")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int cuModuleUnload_Internal(IntPtr hmod);

    public static CudaError cuModuleUnload(IntPtr hmod)
        => (CudaError)cuModuleUnload_Internal(hmod);

    #endregion
}
