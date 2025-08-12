# CUDA P/Invoke Specifications

## Overview

This document defines comprehensive P/Invoke declarations for the CUDA Backend implementation, targeting CUDA 12.x APIs with support for the RTX 2000 Ada Generation GPU (Compute Capability 8.9).

## Library Organization

### Core Libraries
- **cudart64_12.dll** / **libcudart.so.12** - CUDA Runtime API
- **cuda.dll** / **libcuda.so** - CUDA Driver API  
- **nvrtc64_12.dll** / **libnvrtc.so.12** - NVIDIA Runtime Compilation
- **cublas64_12.dll** / **libcublas.so.12** - CUDA BLAS (optional)
- **cufft64_12.dll** / **libcufft.so.12** - CUDA FFT (optional)

## Enhanced P/Invoke Declarations

### 1. CUDA Runtime API (CudaRuntime.cs)

```csharp
namespace DotCompute.Backends.CUDA.Native;

/// <summary>
/// Comprehensive CUDA Runtime API P/Invoke declarations
/// </summary>
public static class CudaRuntime
{
    // Library constants for different platforms
    private const string CUDA_RUNTIME_WINDOWS = "cudart64_12";
    private const string CUDA_RUNTIME_LINUX = "libcudart.so.12";
    private const string CUDA_RUNTIME = 
#if WINDOWS
        CUDA_RUNTIME_WINDOWS;
#elif LINUX  
        CUDA_RUNTIME_LINUX;
#else
        "cudart"; // Fallback
#endif

    #region Device Management

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaGetDeviceCount(out int count);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaSetDevice(int device);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaGetDevice(out int device);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaGetDeviceProperties(
        ref CudaDeviceProperties prop, int device);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaGetDeviceFlags(out uint flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaSetDeviceFlags(uint flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaChooseDevice(out int device, ref CudaDeviceProperties prop);

    #endregion

    #region Memory Management

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMalloc(out IntPtr devPtr, ulong size);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaFree(IntPtr devPtr);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMallocPitch(
        out IntPtr devPtr, out ulong pitch, ulong width, ulong height);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMalloc3D(ref CudaPitchedPtr pitchedDevPtr, CudaExtent extent);

    // Unified Memory (CUDA 6.0+)
    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMallocManaged(
        out IntPtr devPtr, ulong size, CudaMemAttachFlags flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemPrefetchAsync(
        IntPtr devPtr, ulong count, int dstDevice, IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemAdvise(
        IntPtr devPtr, ulong count, CudaMemoryAdvice advice, int device);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemRangeGetAttribute(
        IntPtr data, ulong dataSize, CudaMemRangeAttribute attribute, 
        IntPtr devPtr, ulong count);

    // Memory Copy Operations
    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemcpy(
        IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemcpyAsync(
        IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind, IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemcpy2D(
        IntPtr dst, ulong dpitch, IntPtr src, ulong spitch, 
        ulong width, ulong height, CudaMemcpyKind kind);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemcpy3D(ref CudaMemcpy3DParms p);

    // Memory Set Operations
    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemset(IntPtr devPtr, int value, ulong count);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemsetAsync(
        IntPtr devPtr, int value, ulong count, IntPtr stream);

    // Memory Information
    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemGetInfo(out ulong free, out ulong total);

    #endregion

    #region Stream Management

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamCreate(out IntPtr pStream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamCreateWithFlags(out IntPtr pStream, uint flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamCreateWithPriority(
        out IntPtr pStream, uint flags, int priority);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamDestroy(IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamSynchronize(IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamQuery(IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamWaitEvent(IntPtr stream, IntPtr @event, uint flags);

    #endregion

    #region Event Management

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventCreate(out IntPtr @event);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventCreateWithFlags(out IntPtr @event, uint flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventDestroy(IntPtr @event);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventRecord(IntPtr @event, IntPtr stream);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventSynchronize(IntPtr @event);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventQuery(IntPtr @event);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaEventElapsedTime(out float ms, IntPtr start, IntPtr end);

    #endregion

    #region Peer-to-Peer Memory Access

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDeviceCanAccessPeer(out int canAccessPeer, int device, int peerDevice);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDeviceEnablePeerAccess(int peerDevice, uint flags);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDeviceDisablePeerAccess(int peerDevice);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaMemcpyPeerAsync(
        IntPtr dst, int dstDevice, IntPtr src, int srcDevice, ulong count, IntPtr stream);

    #endregion

    #region Device Synchronization

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDeviceSynchronize();

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDeviceReset();

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaThreadSynchronize(); // Deprecated but sometimes needed

    #endregion

    #region Version and Error Handling

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaRuntimeGetVersion(out int runtimeVersion);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaDriverGetVersion(out int driverVersion);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr cudaGetErrorString(CudaError error);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr cudaGetErrorName(CudaError error);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaGetLastError();

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaPeekAtLastError();

    #endregion

    #region Occupancy Calculation (CUDA 6.5+)

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
        out int numBlocks, IntPtr func, int blockSize, ulong dynamicSMemSize);

    [DllImport(CUDA_RUNTIME, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaOccupancyMaxPotentialBlockSize(
        out int minGridSize, out int blockSize, IntPtr func, 
        CudaOccupancyB2DSize blockSizeToSharedMemSize, ulong dynamicSMemSize, int blockSizeLimit);

    #endregion

    // Helper methods
    public static string GetErrorString(CudaError error)
    {
        var ptr = cudaGetErrorString(error);
        return Marshal.PtrToStringAnsi(ptr) ?? error.ToString();
    }

    public static string GetErrorName(CudaError error)
    {
        var ptr = cudaGetErrorName(error);
        return Marshal.PtrToStringAnsi(ptr) ?? error.ToString();
    }

    public static void CheckError(CudaError error, string operation = "")
    {
        if (error != CudaError.Success)
        {
            var message = string.IsNullOrEmpty(operation)
                ? $"CUDA error: {GetErrorString(error)}"
                : $"CUDA error during {operation}: {GetErrorString(error)}";
            throw new CudaException(message, error);
        }
    }

    public static bool IsCudaSupported()
    {
        try
        {
            var result = cudaGetDeviceCount(out var deviceCount);
            return result == CudaError.Success && deviceCount > 0;
        }
        catch
        {
            return false;
        }
    }
}
```

### 2. CUDA Driver API (CudaDriver.cs)

```csharp
namespace DotCompute.Backends.CUDA.Native;

/// <summary>
/// CUDA Driver API P/Invoke declarations for advanced functionality
/// </summary>
public static class CudaDriver
{
    private const string CUDA_DRIVER_WINDOWS = "nvcuda";
    private const string CUDA_DRIVER_LINUX = "libcuda.so";
    private const string CUDA_DRIVER = 
#if WINDOWS
        CUDA_DRIVER_WINDOWS;
#elif LINUX
        CUDA_DRIVER_LINUX;
#else
        "cuda"; // Fallback
#endif

    #region Driver Initialization

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuInit(uint flags);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDriverGetVersion(out int driverVersion);

    #endregion

    #region Device Management

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDeviceGet(out int device, int ordinal);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDeviceGetCount(out int count);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDeviceGetAttribute(
        out int pi, CudaDeviceAttribute attrib, int dev);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDeviceGetName(
        [Out] StringBuilder name, int len, int dev);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDeviceComputeCapability(
        out int major, out int minor, int dev);

    #endregion

    #region Context Management

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuCtxCreate_v2")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuCtxCreate(out IntPtr pctx, uint flags, int dev);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuCtxDestroy_v2")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuCtxDestroy(IntPtr ctx);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuCtxSetCurrent(IntPtr ctx);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuCtxGetCurrent(out IntPtr pctx);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuCtxSynchronize();

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDevicePrimaryCtxRetain(out IntPtr pctx, int dev);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuDevicePrimaryCtxRelease(int dev);

    #endregion

    #region Module Management

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl, EntryPoint = "cuModuleLoadData")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuModuleLoadData(out IntPtr module, IntPtr image);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuModuleLoadDataEx(
        out IntPtr module, IntPtr image, uint numOptions,
        [MarshalAs(UnmanagedType.LPArray)] CudaJitOption[] options,
        [MarshalAs(UnmanagedType.LPArray)] IntPtr[] optionValues);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuModuleUnload(IntPtr hmod);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuModuleGetFunction(
        out IntPtr hfunc, IntPtr hmod, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuModuleGetGlobal(
        out IntPtr dptr, out ulong bytes, IntPtr hmod, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    #endregion

    #region Kernel Execution

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuLaunchKernel(
        IntPtr f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        IntPtr hStream,
        IntPtr kernelParams,
        IntPtr extra);

    // Cooperative kernel launch (CUDA 9.0+, requires Compute Capability 6.0+)
    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuLaunchCooperativeKernel(
        IntPtr f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        IntPtr hStream,
        IntPtr kernelParams);

    // Multi-device cooperative kernel launch
    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuLaunchCooperativeKernelMultiDevice(
        [MarshalAs(UnmanagedType.LPArray)] CudaLaunchParams[] launchParamsList,
        uint numDevices, uint flags);

    #endregion

    #region Function Attributes

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuFuncGetAttribute(
        out int pi, CudaFuncAttribute attrib, IntPtr hfunc);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuFuncSetAttribute(
        IntPtr hfunc, CudaFuncAttribute attrib, int value);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuFuncSetCacheConfig(
        IntPtr hfunc, CudaFuncCache config);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuFuncSetSharedMemConfig(
        IntPtr hfunc, CudaSharedConfig config);

    #endregion

    #region Occupancy Calculation

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuOccupancyMaxActiveBlocksPerMultiprocessor(
        out int numBlocks, IntPtr func, int blockSize, ulong dynamicSMemSize);

    [DllImport(CUDA_DRIVER, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaResult cuOccupancyMaxPotentialBlockSize(
        out int minGridSize, out int blockSize, IntPtr func,
        CudaOccupancyB2DSize blockSizeToDynamicSMemSize, ulong dynamicSMemSize, int blockSizeLimit);

    #endregion

    // Helper methods
    public static void CheckResult(CudaResult result, string operation = "")
    {
        if (result != CudaResult.Success)
        {
            var message = string.IsNullOrEmpty(operation)
                ? $"CUDA Driver API error: {result}"
                : $"CUDA Driver API error during {operation}: {result}";
            throw new CudaException(message, (CudaError)result);
        }
    }
}
```

### 3. NVRTC Runtime Compilation API (NvrtcRuntime.cs)

```csharp
namespace DotCompute.Backends.CUDA.Native;

/// <summary>
/// NVIDIA Runtime Compilation (NVRTC) API P/Invoke declarations
/// </summary>
public static class NvrtcRuntime
{
    private const string NVRTC_WINDOWS = "nvrtc64_12";
    private const string NVRTC_LINUX = "libnvrtc.so.12";
    private const string NVRTC = 
#if WINDOWS
        NVRTC_WINDOWS;
#elif LINUX
        NVRTC_LINUX;
#else
        "nvrtc"; // Fallback
#endif

    #region Program Management

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcVersion(out int major, out int minor);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetSupportedArchs(out int[] supportedArchs);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcCreateProgram(
        out IntPtr prog,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string src,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? name,
        int numHeaders,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? headers,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? includeNames);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcDestroyProgram(ref IntPtr prog);

    #endregion

    #region Compilation

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcCompileProgram(
        IntPtr prog, int numOptions,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? options);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcVerifyProgram(IntPtr prog, int numOptions,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[]? options);

    #endregion

    #region PTX Generation

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetPTXSize(IntPtr prog, out ulong ptxSizeRet);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetPTX(IntPtr prog, [Out] byte[] ptx);

    #endregion

    #region CUBIN Generation (CUDA 11.1+)

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetCUBINSize(IntPtr prog, out ulong cubinSizeRet);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetCUBIN(IntPtr prog, [Out] byte[] cubin);

    #endregion

    #region LTO-IR Generation (CUDA 11.4+)

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetLTOIRSize(IntPtr prog, out ulong ltoirSizeRet);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetLTOIR(IntPtr prog, [Out] byte[] ltoir);

    #endregion

    #region OptiX IR Generation (OptiX 7.0+)

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetOptiXIRSize(IntPtr prog, out ulong optixirSizeRet);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetOptiXIR(IntPtr prog, [Out] byte[] optixir);

    #endregion

    #region Compilation Log

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetProgramLogSize(IntPtr prog, out ulong logSizeRet);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetProgramLog(IntPtr prog, [Out] byte[] log);

    #endregion

    #region Name Management

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcAddNameExpression(
        IntPtr prog, [MarshalAs(UnmanagedType.LPUTF8Str)] string nameExpression);

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetLoweredName(
        IntPtr prog, [MarshalAs(UnmanagedType.LPUTF8Str)] string nameExpression,
        out IntPtr loweredName);

    #endregion

    #region Error Handling

    [DllImport(NVRTC, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr nvrtcGetErrorString(NvrtcResult result);

    #endregion

    // Helper methods
    public static string GetErrorString(NvrtcResult result)
    {
        var ptr = nvrtcGetErrorString(result);
        return Marshal.PtrToStringAnsi(ptr) ?? result.ToString();
    }

    public static void CheckResult(NvrtcResult result, string operation = "")
    {
        if (result != NvrtcResult.Success)
        {
            var message = string.IsNullOrEmpty(operation)
                ? $"NVRTC error: {GetErrorString(result)}"
                : $"NVRTC error during {operation}: {GetErrorString(result)}";
            throw new NvrtcException(message, result);
        }
    }

    /// <summary>
    /// Creates a safe program handle that automatically cleans up
    /// </summary>
    public static unsafe IntPtr CreateProgramSafe(
        string source, 
        string? name = null, 
        string[]? headers = null, 
        string[]? includeNames = null)
    {
        var result = nvrtcCreateProgram(
            out var program,
            source, 
            name,
            headers?.Length ?? 0, 
            headers, 
            includeNames);
            
        CheckResult(result, "Creating NVRTC program");
        return program;
    }

    /// <summary>
    /// Safely gets PTX from compiled program
    /// </summary>
    public static string GetPtxSafe(IntPtr program)
    {
        var result = nvrtcGetPTXSize(program, out var ptxSize);
        CheckResult(result, "Getting PTX size");

        if (ptxSize == 0)
            return string.Empty;

        var ptxBytes = new byte[ptxSize];
        result = nvrtcGetPTX(program, ptxBytes);
        CheckResult(result, "Getting PTX");

        return Encoding.UTF8.GetString(ptxBytes, 0, (int)ptxSize - 1); // Remove null terminator
    }

    /// <summary>
    /// Safely gets compilation log
    /// </summary>
    public static string GetLogSafe(IntPtr program)
    {
        var result = nvrtcGetProgramLogSize(program, out var logSize);
        CheckResult(result, "Getting log size");

        if (logSize <= 1)
            return string.Empty;

        var logBytes = new byte[logSize];
        result = nvrtcGetProgramLog(program, logBytes);
        CheckResult(result, "Getting compilation log");

        return Encoding.UTF8.GetString(logBytes, 0, (int)logSize - 1); // Remove null terminator
    }
}
```

## Data Structures and Enumerations

### Enhanced CUDA Structures

```csharp
namespace DotCompute.Backends.CUDA.Native;

/// <summary>
/// Enhanced CUDA device properties with all available fields
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CudaDeviceProperties
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Name;
    
    public Guid Uuid;  // CUDA 9.2+
    public byte[] Luid; // CUDA 10.0+ (Windows only)
    public uint LuidDeviceNodeMask; // CUDA 10.0+ (Windows only)
    
    public ulong TotalGlobalMem;
    public ulong SharedMemPerBlock;
    public int RegsPerBlock;
    public int WarpSize;
    public ulong MemPitch;
    public int MaxThreadsPerBlock;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxThreadsDim;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxGridSize;
    
    public int ClockRate;
    public ulong TotalConstMem;
    public int Major;
    public int Minor;
    public ulong TextureAlignment;
    public ulong TexturePitchAlignment;
    public int DeviceOverlap;
    public int MultiProcessorCount;
    public int KernelExecTimeoutEnabled;
    public int Integrated;
    public int CanMapHostMemory;
    public int ComputeMode;
    public int MaxTexture1D;
    public int MaxTexture1DMipmap;
    public int MaxTexture1DLinear;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxTexture2D;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxTexture2DMipmap;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxTexture2DLinear;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxTexture2DGather;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxTexture3D;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxTexture3DAlt;
    
    public int MaxTextureCubemap;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxTexture1DLayered;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxTexture2DLayered;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxTextureCubemapLayered;
    
    public int MaxSurface1D;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxSurface2D;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxSurface3D;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxSurface1DLayered;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxSurface2DLayered;
    
    public int MaxSurfaceCubemap;
    
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public int[] MaxSurfaceCubemapLayered;
    
    public ulong SurfaceAlignment;
    public int ConcurrentKernels;
    public int ECCEnabled;
    public int PciBusId;
    public int PciDeviceId;
    public int PciDomainId;
    public int TccDriver;
    public int AsyncEngineCount;
    public int UnifiedAddressing;
    public int MemoryClockRate;
    public int MemoryBusWidth;
    public int L2CacheSize;
    public int MaxThreadsPerMultiProcessor;
    public int StreamPrioritiesSupported;
    public int GlobalL1CacheSupported;
    public int LocalL1CacheSupported;
    public ulong SharedMemPerMultiprocessor;
    public int RegsPerMultiprocessor;
    public int ManagedMemory;
    public int IsMultiGpuBoard;
    public int MultiGpuBoardGroupID;
    public int HostNativeAtomicSupported;
    public int SingleToDoublePrecisionPerfRatio;
    public int PageableMemoryAccess;
    public int ConcurrentManagedAccess;
    public int ComputePreemptionSupported;
    public int CanUseHostPointerForRegisteredMem;
    public int CooperativeLaunch;
    public int CooperativeMultiDeviceLaunch;
    public ulong SharedMemPerBlockOptin;
    public int PageableMemoryAccessUsesHostPageTables;
    public int DirectManagedMemAccessFromHost;
    public int MaxBlocksPerMultiProcessor; // CUDA 11.0+
    public int AccessPolicyMaxWindowSize; // CUDA 11.0+
    public ulong ReservedSharedMemoryPerBlock; // CUDA 11.0+
}

/// <summary>
/// Cooperative kernel launch parameters
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CudaLaunchParams
{
    public IntPtr Function;
    public uint GridDimX, GridDimY, GridDimZ;
    public uint BlockDimX, BlockDimY, BlockDimZ;
    public uint SharedMemBytes;
    public IntPtr Stream;
    public IntPtr Args;
}

/// <summary>
/// CUDA JIT compilation options
/// </summary>
public enum CudaJitOption
{
    MaxRegisters = 0,
    ThreadsPerBlock = 1,
    WallTime = 2,
    InfoLogBuffer = 3,
    InfoLogBufferSizeBytes = 4,
    ErrorLogBuffer = 5,
    ErrorLogBufferSizeBytes = 6,
    OptimizationLevel = 7,
    TargetFromCuContext = 8,
    Target = 9,
    FallbackStrategy = 10,
    GenerateDebugInfo = 11,
    LogVerbose = 12,
    GenerateLineInfo = 13,
    CacheMode = 14,
    NewSm3xOpt = 15,
    FastCompile = 16,
    GlobalSymbolNames = 17,
    GlobalSymbolAddresses = 18,
    GlobalSymbolCount = 19,
    LtoOptions = 20,
    FtzMode = 21,
    PrecisionDivisionSqrt = 22
}

// Additional enumerations for memory management, device attributes, etc.
```

## Platform-Specific Considerations

### Windows-Specific
- Use `.dll` extensions and Windows-specific DLL search paths
- Handle Windows-specific LUID for device identification
- Support for Windows Display Driver Model (WDDM)

### Linux-Specific
- Use `.so` extensions with version numbers
- Handle Linux-specific device enumeration
- Support for X11/Wayland display systems

### Cross-Platform Helper
```csharp
public static class PlatformHelper
{
    public static string GetCudaLibraryName(string baseName) =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
            ? $"{baseName}64_12.dll"
            : $"lib{baseName}.so.12";
}
```

This comprehensive P/Invoke specification provides full access to CUDA 12.x APIs while maintaining type safety and proper resource management for the DotCompute CUDA backend.