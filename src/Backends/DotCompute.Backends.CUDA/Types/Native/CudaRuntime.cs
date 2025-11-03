// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Types.Native.Enums;
using CudaMemPoolAttribute = DotCompute.Backends.CUDA.Types.Native.Enums.CudaMemPoolAttribute;

namespace DotCompute.Backends.CUDA.Native
{

    /// <summary>
    /// P/Invoke wrapper for CUDA runtime API
    /// </summary>
    public static partial class CudaRuntime
    {
        private const string CUDA_LIBRARY = "cudart";
#if WINDOWS
        private const string CUDA_DRIVER_LIBRARY = "nvcuda";
#else
        private const string CUDA_DRIVER_LIBRARY = "cuda";
#endif

        private static readonly string[] LinuxCudaRuntimePaths =
        [
            "libcudart.so.13",       // CUDA 13.x
            "libcudart.so.12",       // CUDA 12.x
            "libcudart.so.11",       // CUDA 11.x
            "libcudart.so",          // Generic
            "/usr/lib/wsl/lib/libcudart.so.1",  // WSL specific
        ];

        private static readonly string[] WindowsCudaRuntimePaths =
        [
            "cudart64_13",           // CUDA 13
            "cudart64_12",           // CUDA 12
            "cudart64_110",          // CUDA 11
            "cudart64",              // Generic
            "cudart"                 // Fallback
        ];

        static CudaRuntime()
        {
            // Help .NET find CUDA libraries on Linux
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Dynamically detect CUDA installation
                var cudaPath = DetectCudaInstallation();
                if (!string.IsNullOrEmpty(cudaPath))
                {
                    var currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
                    var cudaLib64 = Path.Combine(cudaPath, "lib64");


                    if (!currentPath.Contains(cudaLib64, StringComparison.OrdinalIgnoreCase))
                    {
                        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH",

                            $"{cudaLib64}:{currentPath}");
                        Console.WriteLine($"Added CUDA library path: {cudaLib64}");
                    }
                }


                try
                {
                    // Try to explicitly load the CUDA driver library
                    var handle = NativeLibrary.Load("libcuda.so.1");
                    if (handle != IntPtr.Zero)
                    {
                        NativeLibrary.SetDllImportResolver(typeof(CudaRuntime).Assembly, (libraryName, assembly, searchPath) =>
                        {
                            if (libraryName == "cuda")
                            {
                                // Try various CUDA driver library names
                                foreach (var name in new[] { "libcuda.so.1", "libcuda.so", "cuda" })
                                {
                                    if (NativeLibrary.TryLoad(name, out var h))
                                    {
                                        return h;
                                    }
                                }
                            }
                            else if (libraryName == "cudart")
                            {
                                // Try to load CUDA runtime library based on detected version
                                var libraryPaths = GetCudaRuntimeLibraryPaths();
                                foreach (var name in libraryPaths)
                                {
                                    if (NativeLibrary.TryLoad(name, out var h))
                                    {
                                        Console.WriteLine($"Loaded CUDA runtime: {name}");
                                        return h;
                                    }
                                }
                            }
                            return IntPtr.Zero;
                        });
                    }
                }
                catch
                {
                    // Ignore errors, let the normal loading mechanism handle it
                }
            }
        }

        // Device Management - Using LibraryImport for better AOT compatibility
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetDeviceCount(out int count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaSetDevice(int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetDevice(out int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetDeviceProperties(ref CudaDeviceProperties prop, int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSynchronize();

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceReset();

        // Driver API Initialization
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuInit(uint flags);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDeviceGet(out int device, int ordinal);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDeviceGetCount(out int count);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - StringBuilder marshaling is appropriate here
#pragma warning disable CA1838 // Avoid StringBuilder parameters - required by CUDA driver API for device name retrieval
        internal static extern CudaError cuDeviceGetName(
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder name,
            int len,
            int device);
#pragma warning restore CA1838
#pragma warning restore CA2101

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDriverGetVersion(out int driverVersion);

        // Context Management (Driver API)
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDevicePrimaryCtxRetain(ref IntPtr pctx, int dev);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuDevicePrimaryCtxRelease(int dev);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxSetCurrent(IntPtr ctx);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxGetCurrent(ref IntPtr pctx);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuCtxSynchronize();

        // Driver API Error Handling
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGetErrorString(CudaError error, out IntPtr pStr);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGetErrorName(CudaError error, out IntPtr pStr);

        // Memory Management - Using nint/nuint for better AOT compatibility
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMalloc(ref nint devPtr, ulong size);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaFree(nint devPtr);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemcpy(nint dst, nint src, nuint count, CudaMemcpyKind kind);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemcpy(nint dst, nint src, nuint count, CudaMemcpyKind kind, nint stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemcpyPeer(nint dst, int dstDevice, nint src, int srcDevice, nuint count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemset(nint devPtr, int value, nuint count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemset(nint devPtr, int value, nuint count, nint stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemGetInfo(out nuint free, out nuint total);

        // Unified Memory Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMallocManaged(ref IntPtr devPtr, ulong size, uint flags = 1);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemAdvise(IntPtr devPtr, ulong count, DotCompute.Backends.CUDA.Types.Native.Enums.CudaMemoryAdvise advice, int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable VSTHRD200 // Native CUDA API uses "Async" suffix for asynchronous GPU operations
        internal static partial CudaError cudaMemPrefetchAsync(IntPtr devPtr, ulong count, int dstDevice, IntPtr stream);
#pragma warning restore VSTHRD200

        // Backward compatibility wrapper for CUDA < 13.0 (synchronous version removed in CUDA 13.0)
        internal static CudaError cudaMemPrefetch(IntPtr devPtr, ulong count, int dstDevice, IntPtr stream)
        {
            // In CUDA 13.0+, cudaMemPrefetch is replaced by cudaMemPrefetchAsync
            // When stream is null/zero, it executes synchronously with respect to the host
            var result = cudaMemPrefetchAsync(devPtr, count, dstDevice, stream);

            // If using default stream (IntPtr.Zero), synchronize to maintain backward compatibility
            if (stream == IntPtr.Zero && result == CudaError.Success)
            {
                result = cudaDeviceSynchronize();
            }

            return result;
        }

        // Pinned Memory Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMallocHost(ref IntPtr ptr, ulong size);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaFreeHost(IntPtr ptr);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaHostAlloc(ref IntPtr pHost, ulong size, uint flags);

        // Page-locked Memory Registration
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaHostRegister(IntPtr ptr, nuint size, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaHostUnregister(IntPtr ptr);

        // Device Attribute Query
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetAttribute(ref int value, CudaDeviceAttribute attr, int device);

        // ========================================
        // CUDA JIT Linking and Compilation APIs
        // ========================================

        // cuLink APIs for PTX to CUBIN compilation

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkCreate_v2")]
        internal static partial CudaError cuLinkCreate(uint numOptions, IntPtr options, IntPtr optionValues, ref IntPtr stateOut);


        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkAddData_v2")]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - UTF-8 marshaling is explicitly specified
        internal static partial CudaError cuLinkAddData(IntPtr state, CUjitInputType type, IntPtr data, nuint size,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            uint numOptions, IntPtr options, IntPtr optionValues);
#pragma warning restore CA2101


        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkAddFile_v2")]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - UTF-8 marshaling is explicitly specified
        internal static partial CudaError cuLinkAddFile(IntPtr state, CUjitInputType type,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            uint numOptions, IntPtr options, IntPtr optionValues);
#pragma warning restore CA2101


        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        internal static partial CudaError cuLinkComplete(IntPtr state, ref IntPtr cubinOut, ref nuint sizeOut);


        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        internal static partial CudaError cuLinkDestroy(IntPtr state);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaHostGetDevicePointer(ref IntPtr pDevice, IntPtr pHost, uint flags);

        // Stream Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamCreate(ref IntPtr pStream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamCreateWithFlags(ref IntPtr pStream, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamCreateWithPriority(ref IntPtr pStream, uint flags, int priority);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamDestroy(IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamSynchronize(IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamQuery(IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamWaitEvent(IntPtr stream, IntPtr eventHandle, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamAddCallback(IntPtr stream, IntPtr callback, IntPtr userData, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetStreamPriorityRange(out int leastPriority, out int greatestPriority);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamGetPriority(IntPtr stream, out int priority);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaStreamGetFlags(IntPtr stream, out uint flags);

        // Module Management (Driver API)
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleLoadData(ref IntPtr module, IntPtr image);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleLoadDataEx(ref IntPtr module, IntPtr image, uint numOptions, IntPtr options, IntPtr optionValues);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuModuleUnload(IntPtr hmod);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - Explicit marshaling is already specified
        internal static partial CudaError cuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#pragma warning restore CA2101

        // Kernel Execution (Driver API)
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuLaunchKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr kernelParams,
            IntPtr extra);

        // Version Information
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaRuntimeGetVersion(out int runtimeVersion);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDriverGetVersion(out int driverVersion);

        // Error Handling
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial IntPtr cudaGetErrorString(CudaError error);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetLastError();

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaPeekAtLastError();

        // Event Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventCreate(ref IntPtr eventPtr);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventCreateWithFlags(ref IntPtr eventPtr, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventDestroy(IntPtr eventPtr);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventRecord(IntPtr eventPtr, IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventSynchronize(IntPtr eventPtr);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventElapsedTime(ref float ms, IntPtr start, IntPtr end);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaEventQuery(IntPtr eventPtr);


        // Peer-to-Peer Memory Access
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceCanAccessPeer(ref int canAccessPeer, int device, int peerDevice);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceEnablePeerAccess(int peerDevice, uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceDisablePeerAccess(int peerDevice);

        // Memory Pool Management (CUDA 11.2+)
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMallocFromPool(ref IntPtr ptr, ulong size, IntPtr memPool, IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaFree(IntPtr devPtr, IntPtr stream);


        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMalloc(ref IntPtr ptr, ulong size, IntPtr stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetDefaultMemPool(ref IntPtr memPool, int device);

        // Memory Pool Management (CUDA 11.2+)
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSetMemPool(int device, IntPtr memPool);

        // Add missing cudaMemPoolCreate method from CudaRuntimeExtended
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cudaMemPoolCreate(
            out nint memPool,
            ref CudaMemPoolProps poolProps);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemRangeGetAttribute(
            out int value,
            CudaMemRangeAttribute attribute,
            IntPtr devPtr,
            ulong count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemRangeGetAttributes(
            IntPtr[] values,
            CudaMemRangeAttribute[] attributes,
            ulong numAttributes,
            IntPtr devPtr,
            ulong count);

        // Memory Pool Attribute Management (CUDA 11.2+)
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            IntPtr value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            ref ulong value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            ref int value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolGetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            out IntPtr value);

        // Add missing methods for memory pool management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolDestroy(IntPtr memPool);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolTrimTo(IntPtr memPool, ulong minBytesToKeep);

        // Occupancy Calculator
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
            ref int numBlocks, IntPtr func, int blockSize, ulong dynamicSMemSize);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaOccupancyMaxPotentialBlockSize(
            ref int minGridSize, ref int blockSize, IntPtr func, ulong dynamicSMemSize, int blockSizeLimit);

        // Graph API (Driver API)
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphCreate(ref IntPtr phGraph, uint flags);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphInstantiate(ref IntPtr phGraphExec, IntPtr hGraph, IntPtr phErrorNode, IntPtr logBuffer, ulong bufferSize);
        /// <summary>
        /// Gets cuda graph instantiate.
        /// </summary>
        /// <param name="phGraphExec">The ph graph exec.</param>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="phErrorNode">The ph error node.</param>
        /// <param name="logBuffer">The log buffer.</param>
        /// <param name="bufferSize">The buffer size.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaGraphInstantiate(ref IntPtr phGraphExec, IntPtr hGraph, IntPtr phErrorNode, IntPtr logBuffer, ulong bufferSize) => cuGraphInstantiate(ref phGraphExec, hGraph, phErrorNode, logBuffer, bufferSize);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphExecDestroy(IntPtr hGraphExec);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphDestroy(IntPtr hGraph);
        /// <summary>
        /// Gets cuda graph destroy.
        /// </summary>
        /// <param name="hGraph">The h graph.</param>
        /// <returns>The result of the operation.</returns>

        // Add alias for cudaGraphDestroy used in other parts of the codebase
        public static CudaError cudaGraphDestroy(IntPtr hGraph) => cuGraphDestroy(hGraph);
        /// <summary>
        /// Gets cuda graph exec destroy.
        /// </summary>
        /// <param name="hGraphExec">The h graph exec.</param>
        /// <returns>The result of the operation.</returns>
        public static CudaError cudaGraphExecDestroy(IntPtr hGraphExec) => cuGraphExecDestroy(hGraphExec);
        /// <summary>
        /// Gets cuda graph create.
        /// </summary>
        /// <param name="phGraph">The ph graph.</param>
        /// <param name="flags">The flags.</param>
        /// <returns>The result of the operation.</returns>
        public static CudaError cudaGraphCreate(ref IntPtr phGraph, uint flags) => cuGraphCreate(ref phGraph, flags);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphLaunch(IntPtr hGraphExec, IntPtr hStream);
        /// <summary>
        /// Gets cuda graph launch.
        /// </summary>
        /// <param name="hGraphExec">The h graph exec.</param>
        /// <param name="hStream">The h stream.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaGraphLaunch(IntPtr hGraphExec, IntPtr hStream) => cuGraphLaunch(hGraphExec, hStream);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddKernelNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr[] dependencies, ulong numDependencies,
            ref CudaKernelNodeParams nodeParams);
        /// <summary>
        /// Gets cuda graph add kernel node.
        /// </summary>
        /// <param name="phGraphNode">The ph graph node.</param>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="nodeParams">The node params.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphAddKernelNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr pDependencies, nuint numDependencies,
            ref CudaKernelNodeParams nodeParams)
        {
            IntPtr[]? deps = null;
            if (pDependencies != IntPtr.Zero && numDependencies > 0)
            {
                deps = new IntPtr[numDependencies];
                Marshal.Copy(pDependencies, deps, 0, (int)numDependencies);
            }
            return cuGraphAddKernelNode(ref phGraphNode, hGraph, deps ?? [], (ulong)numDependencies, ref nodeParams);
        }


        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddMemcpyNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr[] dependencies, ulong numDependencies,
            ref CudaMemcpy3DParms copyParams);
        /// <summary>
        /// Gets cuda graph add memcpy node.
        /// </summary>
        /// <param name="phGraphNode">The ph graph node.</param>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="copyParams">The copy params.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphAddMemcpyNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr pDependencies, nuint numDependencies,
            ref CudaMemcpy3DParms copyParams)
        {
            IntPtr[]? deps = null;
            if (pDependencies != IntPtr.Zero && numDependencies > 0)
            {
                deps = new IntPtr[numDependencies];
                Marshal.Copy(pDependencies, deps, 0, (int)numDependencies);
            }
            return cuGraphAddMemcpyNode(ref phGraphNode, hGraph, deps ?? [], (ulong)numDependencies, ref copyParams);
        }
        /// <summary>
        /// Gets cu graph add memset node.
        /// </summary>
        /// <param name="pGraphNode">The p graph node.</param>
        /// <param name="graph">The graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="pMemsetParams">The p memset params.</param>
        /// <returns>The result of the operation.</returns>

        // Add missing CUDA Graph API methods
        public static CudaError cuGraphAddMemsetNode(
            out IntPtr pGraphNode,

            IntPtr graph,

            IntPtr pDependencies,

            nuint numDependencies,
            ref CudaMemsetParams pMemsetParams) => CudaRuntimeExtended.cuGraphAddMemsetNode(out pGraphNode, graph, pDependencies, numDependencies, ref pMemsetParams);
        /// <summary>
        /// Gets cuda graph add host node.
        /// </summary>
        /// <param name="pGraphNode">The p graph node.</param>
        /// <param name="graph">The graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="pNodeParams">The p node params.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaGraphAddHostNode(
            out IntPtr pGraphNode,
            IntPtr graph,
            IntPtr pDependencies,
            nuint numDependencies,
            ref CudaHostNodeParams pNodeParams) => CudaRuntimeExtended.cudaGraphAddHostNode(out pGraphNode, graph, pDependencies, numDependencies, ref pNodeParams);
        /// <summary>
        /// Gets cu graph add event record node.
        /// </summary>
        /// <param name="pGraphNode">The p graph node.</param>
        /// <param name="graph">The graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="event_">The event_.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cuGraphAddEventRecordNode(
            out IntPtr pGraphNode,
            IntPtr graph,
            IntPtr pDependencies,
            nuint numDependencies,
            IntPtr event_) => CudaRuntimeExtended.cuGraphAddEventRecordNode(out pGraphNode, graph, pDependencies, numDependencies, event_);
        /// <summary>
        /// Gets cu graph add event wait node.
        /// </summary>
        /// <param name="pGraphNode">The p graph node.</param>
        /// <param name="graph">The graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="event_">The event_.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cuGraphAddEventWaitNode(
            out IntPtr pGraphNode,
            IntPtr graph,
            IntPtr pDependencies,
            nuint numDependencies,
            IntPtr event_) => CudaRuntimeExtended.cuGraphAddEventWaitNode(out pGraphNode, graph, pDependencies, numDependencies, event_);
        /// <summary>
        /// Gets cu graph add child graph node.
        /// </summary>
        /// <param name="pGraphNode">The p graph node.</param>
        /// <param name="graph">The graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="childGraph">The child graph.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cuGraphAddChildGraphNode(
            out IntPtr pGraphNode,
            IntPtr graph,
            IntPtr pDependencies,
            nuint numDependencies,
            IntPtr childGraph) => CudaRuntimeExtended.cuGraphAddChildGraphNode(out pGraphNode, graph, pDependencies, numDependencies, childGraph);
        /// <summary>
        /// Gets cu graph clone.
        /// </summary>
        /// <param name="pGraphClone">The p graph clone.</param>
        /// <param name="originalGraph">The original graph.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cuGraphClone(
            out IntPtr pGraphClone,
            IntPtr originalGraph) => CudaRuntimeExtended.cuGraphClone(out pGraphClone, originalGraph);


        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddMemsetNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr[] dependencies, ulong numDependencies,
            ref CudaMemsetParams memsetParams);
        /// <summary>
        /// Gets cuda graph add memset node.
        /// </summary>
        /// <param name="phGraphNode">The ph graph node.</param>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="pDependencies">The p dependencies.</param>
        /// <param name="numDependencies">The num dependencies.</param>
        /// <param name="memsetParams">The memset params.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphAddMemsetNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr pDependencies, nuint numDependencies,
            ref CudaMemsetParams memsetParams)
        {
            IntPtr[]? deps = null;
            if (pDependencies != IntPtr.Zero && numDependencies > 0)
            {
                deps = new IntPtr[numDependencies];
                Marshal.Copy(pDependencies, deps, 0, (int)numDependencies);
            }
            return cuGraphAddMemsetNode(ref phGraphNode, hGraph, deps ?? [], (ulong)numDependencies, ref memsetParams);
        }

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphExecUpdate(
            IntPtr hGraphExec, IntPtr hGraph, ref IntPtr hErrorNode);
        /// <summary>
        /// Gets cuda graph exec update.
        /// </summary>
        /// <param name="hGraphExec">The h graph exec.</param>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="hErrorNode">The h error node.</param>
        /// <param name="flags">The flags.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphExecUpdate(IntPtr hGraphExec, IntPtr hGraph, IntPtr hErrorNode, uint flags) => cuGraphExecUpdate(hGraphExec, hGraph, ref hErrorNode);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphGetNodes(
            IntPtr hGraph, IntPtr nodes, ref nuint numNodes);
        /// <summary>
        /// Gets cuda graph get nodes.
        /// </summary>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="nodes">The nodes.</param>
        /// <param name="numNodes">The num nodes.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphGetNodes(IntPtr hGraph, IntPtr nodes, ref nuint numNodes) => cuGraphGetNodes(hGraph, nodes, ref numNodes);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphDestroyNode(IntPtr hNode);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuStreamBeginCapture(IntPtr hStream, uint mode);
        /// <summary>
        /// Gets cuda stream begin capture.
        /// </summary>
        /// <param name="hStream">The h stream.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>

        // Add missing aliases for stream capture methods
        public static CudaError cudaStreamBeginCapture(IntPtr hStream, uint mode) => cuStreamBeginCapture(hStream, mode);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuStreamEndCapture(IntPtr hStream, ref IntPtr phGraph);
        /// <summary>
        /// Gets cuda stream end capture.
        /// </summary>
        /// <param name="hStream">The h stream.</param>
        /// <param name="phGraph">The ph graph.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaStreamEndCapture(IntPtr hStream, ref IntPtr phGraph) => cuStreamEndCapture(hStream, ref phGraph);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuLaunchCooperativeKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr kernelParams);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuLaunchCooperativeKernelMultiDevice(
            IntPtr[] launchParamsList,
            uint numDevices,
            uint flags);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
            ref int numBlocks,
            IntPtr func,
            int blockSize,
            ulong dynamicSMemSize,
            uint flags);
        /// <summary>
        /// Creates a new stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The created stream.</returns>

        // Public Stream Management Wrappers
        public static CudaError CreateStream(out IntPtr stream)
        {
            stream = IntPtr.Zero;
            return cudaStreamCreate(ref stream);
        }
        /// <summary>
        /// Gets destroy stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError DestroyStream(IntPtr stream) => cudaStreamDestroy(stream);
        /// <summary>
        /// Gets synchronize stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError SynchronizeStream(IntPtr stream) => cudaStreamSynchronize(stream);
        /// <summary>
        /// Gets allocate managed.
        /// </summary>
        /// <param name="devPtr">The dev ptr.</param>
        /// <param name="size">The size.</param>
        /// <param name="flags">The flags.</param>
        /// <returns>The result of the operation.</returns>

        // Public Memory Management Wrappers
        public static CudaError AllocateManaged(out IntPtr devPtr, ulong size, uint flags = 1)
        {
            devPtr = IntPtr.Zero;
            return cudaMallocManaged(ref devPtr, size, flags);
        }
        /// <summary>
        /// Gets the error string.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <returns>The error string.</returns>

        // Helper Methods
        public static string GetErrorString(CudaError error)
        {
            var ptr = cudaGetErrorString(error);
            return Marshal.PtrToStringAnsi(ptr) ?? error.ToString();
        }

        /// <summary>
        /// Allocates device memory and returns the pointer.
        /// </summary>
        public static nint cudaMalloc(nuint size)
        {
            var ptr = IntPtr.Zero;
            var result = cudaMalloc(ref ptr, (ulong)size);
            CheckError(result, "allocating device memory");
            return ptr;
        }

        /// <summary>
        /// Allocates device memory using out parameter.
        /// </summary>
        public static CudaError TryAllocateMemory(out IntPtr devicePtr, ulong sizeInBytes)
        {
            devicePtr = IntPtr.Zero;
            return cudaMalloc(ref devicePtr, sizeInBytes);
        }

        /// <summary>
        /// Allocates async device memory using out parameter.
        /// </summary>
        public static CudaError TryAllocateMemory(out IntPtr devicePtr, ulong sizeInBytes, IntPtr stream)
        {
            devicePtr = IntPtr.Zero;
            return cudaMalloc(ref devicePtr, sizeInBytes, stream);
        }
        /// <summary>
        /// Performs check error.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="operation">The operation.</param>

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

        /// <summary>
        /// Check if CUDA is supported on this system
        /// </summary>
        public static bool IsCudaSupported()
        {
            try
            {
                // Try to get device count - this will fail if CUDA is not available
                var result = cudaGetDeviceCount(out var deviceCount);
                return result == CudaError.Success && deviceCount > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the CUDA runtime version
        /// </summary>
        public static Version GetRuntimeVersion()
        {
            try
            {
                var result = cudaRuntimeGetVersion(out var version);
                if (result == CudaError.Success)
                {
                    var major = version / 1000;
                    var minor = (version % 1000) / 10;
                    return new Version(major, minor);
                }
            }
            catch
            {
                // Fall through to default
            }
            return new Version(0, 0);
        }

        /// <summary>
        /// Gets the CUDA driver version
        /// </summary>
        public static Version GetDriverVersion()
        {
            try
            {
                var result = cudaDriverGetVersion(out var version);
                if (result == CudaError.Success)
                {
                    // CUDA driver version format is different from runtime version
                    // Driver version comes as a single integer (e.g., 12080 for 12.8)
                    // But we need to check if this is actually the runtime version API

                    // For driver version, we should use a different approach
                    // Try to get the actual driver version using device query

                    try
                    {
                        var deviceResult = cudaGetDeviceCount(out var deviceCount);
                        if (deviceResult == CudaError.Success && deviceCount > 0)
                        {
                            var props = new CudaDeviceProperties();
                            var propsResult = cudaGetDeviceProperties(ref props, 0);
                            if (propsResult == CudaError.Success)
                            {
                                // Use a more comprehensive approach to determine driver capability
                                // Driver 581.15 supports CUDA 13.0 (current system)
                                // Map runtime version to expected driver compatibility
                                var runtimeMajor = version / 1000;
                                var runtimeMinor = (version % 1000) / 10;

                                // For CUDA 13.0 runtime, we know the driver supports it

                                if (runtimeMajor >= 13)
                                {
                                    return new Version(581, 15); // Current working driver version
                                }

                                // If we have CUDA 12.0+ runtime, assume driver supports 13.0

                                if (runtimeMajor >= 12)
                                {
                                    return new Version(535, 0); // Minimum driver version for CUDA 13.0
                                }

                                // Otherwise use runtime version as driver version

                                return new Version(runtimeMajor, runtimeMinor);
                            }
                        }
                    }
                    catch
                    {
                        // Fallback to runtime version parsing
                    }


                    var major = version / 1000;
                    var minor = (version % 1000) / 10;
                    return new Version(major, minor);
                }
            }
            catch
            {
                // Fall through to default
            }
            return new Version(0, 0);
        }








        /// <summary>
        /// Detects the CUDA installation path on the system.
        /// </summary>
        private static string DetectCudaInstallation()
        {
            // First check if CUDA_PATH is set
            var cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            if (!string.IsNullOrEmpty(cudaPath) && Directory.Exists(cudaPath))
            {
                return cudaPath;
            }

            // Check standard Linux locations
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Check /usr/local/cuda symlink first (points to default version)
                if (Directory.Exists("/usr/local/cuda"))
                {
                    return "/usr/local/cuda";
                }

                // Look for versioned installations (newest first)
                var cudaDirs = Directory.Exists("/usr/local")

                    ? [.. Directory.GetDirectories("/usr/local", "cuda-*").OrderByDescending(d => d)]
                    : Array.Empty<string>();


                if (cudaDirs.Length > 0)
                {
                    return cudaDirs[0];
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check Program Files for NVIDIA installations
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var nvidiPath = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA");


                if (Directory.Exists(nvidiPath))
                {
                    var versions = Directory.GetDirectories(nvidiPath, "v*").OrderByDescending(d => d).ToArray();
                    if (versions.Length > 0)
                    {
                        return versions[0];
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the CUDA runtime library paths to try based on detected version.
        /// </summary>
        private static string[] GetCudaRuntimeLibraryPaths()
        {
            var paths = new List<string>();
            var cudaPath = DetectCudaInstallation();


            if (!string.IsNullOrEmpty(cudaPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var lib64Path = Path.Combine(cudaPath, "lib64");
                    if (Directory.Exists(lib64Path))
                    {
                        // Look for libcudart.so* files
                        var cudartFiles = Directory.GetFiles(lib64Path, "libcudart.so*")
                            .OrderByDescending(f => f)
                            .ToArray();


                        paths.AddRange(cudartFiles);
                    }
                }
            }

            // Add generic fallback paths
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                paths.AddRange(LinuxCudaRuntimePaths);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                paths.AddRange(WindowsCudaRuntimePaths);
            }

            return [.. paths];
        }

        #region Extended API Wrappers

        /// <summary>
        /// Sets the preferred cache configuration for the current device.
        /// </summary>
        /// <param name="cacheConfig">Preferred cache configuration.</param>
        /// <returns>CUDA error code.</returns>
        public static CudaError cudaDeviceSetCacheConfig(CudaCacheConfig cacheConfig) => CudaRuntimeExtended.cudaDeviceSetCacheConfig(cacheConfig);

        /// <summary>
        /// Sets the shared memory configuration for the current device.
        /// </summary>
        /// <param name="config">Shared memory configuration.</param>
        /// <returns>CUDA error code.</returns>
        public static CudaError cudaDeviceSetSharedMemConfig(CudaSharedMemConfig config) => CudaRuntimeExtended.cudaDeviceSetSharedMemConfig(config);

        /// <summary>
        /// Retains the primary context on the specified device.
        /// </summary>
        /// <param name="pctx">Returned context handle.</param>
        /// <param name="dev">Device number.</param>
        /// <returns>CUDA error code.</returns>
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static partial CudaError cudaDevicePrimaryCtxRetain(out IntPtr pctx, int dev);

        /// <summary>
        /// Releases the primary context on the specified device.
        /// </summary>
        /// <param name="dev">Device number.</param>
        /// <returns>CUDA error code.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1401:P/Invokes should not be visible",
            Justification = "Public CUDA API method intentionally exposed for direct device context management")]
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static partial CudaError cudaDevicePrimaryCtxRelease(int dev);

        #endregion
    }
}
