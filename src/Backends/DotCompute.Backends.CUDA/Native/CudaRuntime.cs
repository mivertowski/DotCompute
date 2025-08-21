// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DotCompute.Backends.CUDA.Native.Types;
using DotCompute.Backends.CUDA.Native.Exceptions;

namespace DotCompute.Backends.CUDA.Native
{

    /// <summary>
    /// P/Invoke wrapper for CUDA runtime API
    /// </summary>
    public static class CudaRuntime
    {
        private const string CUDA_LIBRARY = "cudart";
        private const string CUDA_DRIVER_LIBRARY = "cuda";

        // Device Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetDeviceCount(out int count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaSetDevice(int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetDevice(out int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetDeviceProperties(ref CudaDeviceProperties prop, int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceSynchronize();

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceReset();

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

        // Memory Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMalloc(ref IntPtr devPtr, ulong size);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaFree(IntPtr devPtr);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemcpy(IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemcpyAsync(IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemset(IntPtr devPtr, int value, ulong count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemsetAsync(IntPtr devPtr, int value, ulong count, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemGetInfo(out ulong free, out ulong total);

        // Unified Memory Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMallocManaged(ref IntPtr devPtr, ulong size, uint flags = 1);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemAdvise(IntPtr devPtr, ulong count, CudaMemoryAdvise advice, int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPrefetchAsync(IntPtr devPtr, ulong count, int dstDevice, IntPtr stream);

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
        internal static extern CudaError cudaMallocFromPoolAsync(ref IntPtr ptr, ulong size, IntPtr memPool, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaFreeAsync(IntPtr devPtr, IntPtr stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetDefaultMemPool(ref IntPtr memPool, int device);

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

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphExecDestroy(IntPtr hGraphExec);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphDestroy(IntPtr hGraph);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphLaunch(IntPtr hGraphExec, IntPtr hStream);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphAddKernelNode(
            ref IntPtr phGraphNode, IntPtr hGraph, IntPtr[] dependencies, ulong numDependencies,
            ref CudaKernelNodeParams nodeParams);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphExecUpdate(
            IntPtr hGraphExec, IntPtr hGraph, ref IntPtr hErrorNode);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphGetNodes(
            IntPtr hGraph, IntPtr nodes, ref int numNodes);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuGraphDestroyNode(IntPtr hNode);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuStreamBeginCapture(IntPtr hStream, uint mode);

        [DllImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cuStreamEndCapture(IntPtr hStream, ref IntPtr phGraph);

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

        // Helper Methods
        public static string GetErrorString(CudaError error)
        {
            var ptr = cudaGetErrorString(error);
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
    }










    /// <summary>
    /// CUDA compute capability helper
    /// </summary>
    public static class ComputeCapability
    {
        public static string GetArchString(int major, int minor) => $"compute_{major}{minor}";

        public static string GetCodeString(int major, int minor) => $"sm_{major}{minor}";

        public static (int major, int minor) ParseFromDevice(int deviceId)
        {
            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            CudaRuntime.CheckError(result, "getting device properties");
            return (props.Major, props.Minor);
        }

        // Common compute capabilities
#pragma warning disable CA1724 // Type names should not match namespaces - Common is a descriptive nested class name in this context
#pragma warning disable CA1034 // Nested types should not be visible - Common is appropriately nested within ComputeCapability
        public static class KnownCapabilities
        {
            public static readonly (int major, int minor) Kepler = (3, 5);
            public static readonly (int major, int minor) Maxwell = (5, 0);
            public static readonly (int major, int minor) Pascal = (6, 0);
            public static readonly (int major, int minor) Volta = (7, 0);
            public static readonly (int major, int minor) Turing = (7, 5);
            public static readonly (int major, int minor) Ampere = (8, 0);
            public static readonly (int major, int minor) Ada = (8, 9);
            public static readonly (int major, int minor) Hopper = (9, 0);
        }
#pragma warning restore CA1034
#pragma warning restore CA1724
    }

    /// <summary>
    /// Safe handle for CUDA device memory pointers to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaDeviceMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaDeviceMemoryHandle() : base(true)
        {
        }

        public SafeCudaDeviceMemoryHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaFree(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }

    /// <summary>
    /// Safe handle for CUDA host (pinned) memory pointers to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaHostMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaHostMemoryHandle() : base(true)
        {
        }

        public SafeCudaHostMemoryHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaFreeHost(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }

    /// <summary>
    /// Safe handle for CUDA streams to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaStreamHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaStreamHandle() : base(true)
        {
        }

        public SafeCudaStreamHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaStreamDestroy(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }

    /// <summary>
    /// Safe handle for CUDA events to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaEventHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaEventHandle() : base(true)
        {
        }

        public SafeCudaEventHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cudaEventDestroy(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }

    /// <summary>
    /// Safe handle for CUDA module handles to ensure proper cleanup.
    /// </summary>
    public sealed class SafeCudaModuleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeCudaModuleHandle() : base(true)
        {
        }

        public SafeCudaModuleHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                var result = CudaRuntime.cuModuleUnload(handle);
                return result == CudaError.Success;
            }
            return true;
        }
    }
}
