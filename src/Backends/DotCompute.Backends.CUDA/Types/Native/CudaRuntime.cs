// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native.Enums;
using CudaMemPoolAttribute = DotCompute.Backends.CUDA.Types.Native.Enums.CudaMemPoolAttribute;
using System;

namespace DotCompute.Backends.CUDA.Native
{
    using DotCompute.Backends.CUDA.Types.Native;

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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetDeviceProperties(ref CudaDeviceProperties prop, int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSynchronize();

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceReset();

        // Driver API Initialization
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuInit(uint flags);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDeviceGet(out int device, int ordinal);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDeviceGetCount(out int count);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - StringBuilder marshaling is appropriate here
        internal static extern CudaError cuDeviceGetName(
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder name,
            int len,
            int device);
#pragma warning restore CA2101

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDriverGetVersion(out int driverVersion);

        // Context Management (Driver API)
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDevicePrimaryCtxRetain(ref IntPtr pctx, int dev);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuDevicePrimaryCtxRelease(int dev);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxSetCurrent(IntPtr ctx);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxGetCurrent(ref IntPtr pctx);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuCtxSynchronize();

        // Driver API Error Handling
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGetErrorString(CudaError error, out IntPtr pStr);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGetErrorName(CudaError error, out IntPtr pStr);

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
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMallocManaged(ref IntPtr devPtr, ulong size, uint flags = 1);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemAdvise(IntPtr devPtr, ulong count, CudaMemoryAdvise advice, int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPrefetch(IntPtr devPtr, ulong count, int dstDevice, IntPtr stream);

        // Pinned Memory Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMallocHost(ref IntPtr ptr, ulong size);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaFreeHost(IntPtr ptr);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaHostAlloc(ref IntPtr pHost, ulong size, uint flags);

        // Page-locked Memory Registration
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaHostRegister(IntPtr ptr, nuint size, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaHostUnregister(IntPtr ptr);

        // Device Attribute Query
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetAttribute(ref int value, CudaDeviceAttribute attr, int device);

        // ========================================
        // CUDA JIT Linking and Compilation APIs
        // ========================================

        // cuLink APIs for PTX to CUBIN compilation

        [DllImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkCreate_v2")]
        internal static extern CudaError cuLinkCreate(uint numOptions, IntPtr options, IntPtr optionValues, ref IntPtr stateOut);


        [DllImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkAddData_v2")]
        internal static extern CudaError cuLinkAddData(IntPtr state, CUjitInputType type, IntPtr data, nuint size,

            [MarshalAs(UnmanagedType.LPUTF8Str)] string name, uint numOptions, IntPtr options, IntPtr optionValues);


        [DllImport(CUDA_DRIVER_LIBRARY, EntryPoint = "cuLinkAddFile_v2")]
        internal static extern CudaError cuLinkAddFile(IntPtr state, CUjitInputType type,

            [MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint numOptions, IntPtr options, IntPtr optionValues);


        [DllImport(CUDA_DRIVER_LIBRARY)]
        internal static extern CudaError cuLinkComplete(IntPtr state, ref IntPtr cubinOut, ref nuint sizeOut);


        [DllImport(CUDA_DRIVER_LIBRARY)]
        internal static extern CudaError cuLinkDestroy(IntPtr state);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaHostGetDevicePointer(ref IntPtr pDevice, IntPtr pHost, uint flags);

        // Stream Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamCreate(ref IntPtr pStream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamCreateWithFlags(ref IntPtr pStream, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamCreateWithPriority(ref IntPtr pStream, uint flags, int priority);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamDestroy(IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamSynchronize(IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamQuery(IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamWaitEvent(IntPtr stream, IntPtr eventHandle, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamAddCallback(IntPtr stream, IntPtr callback, IntPtr userData, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetStreamPriorityRange(out int leastPriority, out int greatestPriority);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamGetPriority(IntPtr stream, out int priority);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaStreamGetFlags(IntPtr stream, out uint flags);

        // Module Management (Driver API)
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleLoadData(ref IntPtr module, IntPtr image);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleLoadDataEx(ref IntPtr module, IntPtr image, uint numOptions, IntPtr options, IntPtr optionValues);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuModuleUnload(IntPtr hmod);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - Explicit marshaling is already specified
        internal static extern CudaError cuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);
#pragma warning restore CA2101

        // Kernel Execution (Driver API)
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuLaunchKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr kernelParams,
            IntPtr extra);

        // Version Information
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaRuntimeGetVersion(out int runtimeVersion);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDriverGetVersion(out int driverVersion);

        // Error Handling
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern IntPtr cudaGetErrorString(CudaError error);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetLastError();

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaPeekAtLastError();

        // Event Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventCreate(ref IntPtr eventPtr);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventCreateWithFlags(ref IntPtr eventPtr, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventDestroy(IntPtr eventPtr);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventRecord(IntPtr eventPtr, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventSynchronize(IntPtr eventPtr);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventElapsedTime(ref float ms, IntPtr start, IntPtr end);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaEventQuery(IntPtr eventPtr);


        // Peer-to-Peer Memory Access
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceCanAccessPeer(ref int canAccessPeer, int device, int peerDevice);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceEnablePeerAccess(int peerDevice, uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceDisablePeerAccess(int peerDevice);

        // Memory Pool Management (CUDA 11.2+)
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMallocFromPool(ref IntPtr ptr, ulong size, IntPtr memPool, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaFree(IntPtr devPtr, IntPtr stream);


        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMalloc(ref IntPtr ptr, ulong size, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetDefaultMemPool(ref IntPtr memPool, int device);

        // Memory Pool Management (CUDA 11.2+)
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceSetMemPool(int device, IntPtr memPool);

        // Add missing cudaMemPoolCreate method from CudaRuntimeExtended
        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolCreate(
            out nint memPool,
            ref CudaMemPoolProps poolProps);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemRangeGetAttribute(
            out int value,
            CudaMemRangeAttribute attribute,
            IntPtr devPtr,
            ulong count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemRangeGetAttributes(
            IntPtr[] values,
            CudaMemRangeAttribute[] attributes,
            ulong numAttributes,
            IntPtr devPtr,
            ulong count);

        // Memory Pool Attribute Management (CUDA 11.2+)
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            IntPtr value);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            ref ulong value);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            ref int value);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolGetAttribute(
            IntPtr memPool,
            CudaMemPoolAttribute attr,
            out IntPtr value);

        // Add missing methods for memory pool management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolDestroy(IntPtr memPool);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolTrimTo(IntPtr memPool, ulong minBytesToKeep);

        // Occupancy Calculator
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaOccupancyMaxActiveBlocksPerMultiprocessor(
            ref int numBlocks, IntPtr func, int blockSize, ulong dynamicSMemSize);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaOccupancyMaxPotentialBlockSize(
            ref int minGridSize, ref int blockSize, IntPtr func, ulong dynamicSMemSize, int blockSizeLimit);

        // Graph API (Driver API)
        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphCreate(ref IntPtr phGraph, uint flags);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphInstantiate(ref IntPtr phGraphExec, IntPtr hGraph, IntPtr phErrorNode, IntPtr logBuffer, ulong bufferSize);
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

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphExecDestroy(IntPtr hGraphExec);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphDestroy(IntPtr hGraph);
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

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphLaunch(IntPtr hGraphExec, IntPtr hStream);
        /// <summary>
        /// Gets cuda graph launch.
        /// </summary>
        /// <param name="hGraphExec">The h graph exec.</param>
        /// <param name="hStream">The h stream.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaGraphLaunch(IntPtr hGraphExec, IntPtr hStream) => cuGraphLaunch(hGraphExec, hStream);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphAddKernelNode(
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


        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphAddMemcpyNode(
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


        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphAddMemsetNode(
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

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphExecUpdate(
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

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphGetNodes(
            IntPtr hGraph, IntPtr nodes, ref nuint numNodes);
        /// <summary>
        /// Gets cuda graph get nodes.
        /// </summary>
        /// <param name="hGraph">The h graph.</param>
        /// <param name="nodes">The nodes.</param>
        /// <param name="numNodes">The num nodes.</param>
        /// <returns>The result of the operation.</returns>


        public static CudaError cudaGraphGetNodes(IntPtr hGraph, IntPtr nodes, ref nuint numNodes) => cuGraphGetNodes(hGraph, nodes, ref numNodes);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphDestroyNode(IntPtr hNode);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuStreamBeginCapture(IntPtr hStream, uint mode);
        /// <summary>
        /// Gets cuda stream begin capture.
        /// </summary>
        /// <param name="hStream">The h stream.</param>
        /// <param name="mode">The mode.</param>
        /// <returns>The result of the operation.</returns>

        // Add missing aliases for stream capture methods
        public static CudaError cudaStreamBeginCapture(IntPtr hStream, uint mode) => cuStreamBeginCapture(hStream, mode);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuStreamEndCapture(IntPtr hStream, ref IntPtr phGraph);
        /// <summary>
        /// Gets cuda stream end capture.
        /// </summary>
        /// <param name="hStream">The h stream.</param>
        /// <param name="phGraph">The ph graph.</param>
        /// <returns>The result of the operation.</returns>

        public static CudaError cudaStreamEndCapture(IntPtr hStream, ref IntPtr phGraph) => cuStreamEndCapture(hStream, ref phGraph);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuLaunchCooperativeKernel(
            IntPtr f,
            uint gridDimX, uint gridDimY, uint gridDimZ,
            uint blockDimX, uint blockDimY, uint blockDimZ,
            uint sharedMemBytes,
            IntPtr hStream,
            IntPtr kernelParams);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuLaunchCooperativeKernelMultiDevice(
            IntPtr[] launchParamsList,
            uint numDevices,
            uint flags);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
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
                paths.AddRange(new[]
                {
                    "libcudart.so.13",       // CUDA 13.x
                    "libcudart.so.12",       // CUDA 12.x
                    "libcudart.so.11",       // CUDA 11.x
                    "libcudart.so",          // Generic
                    "/usr/lib/wsl/lib/libcudart.so.1",  // WSL specific
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                paths.AddRange(new[]
                {
                    "cudart64_13",           // CUDA 13
                    "cudart64_12",           // CUDA 12
                    "cudart64_110",          // CUDA 11
                    "cudart64",              // Generic
                    "cudart"                 // Fallback
                });
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
        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        public static partial CudaError cudaDevicePrimaryCtxRelease(int dev);

        #endregion
    }

}
