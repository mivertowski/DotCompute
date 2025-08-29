// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Native CUDA graph API types and structures - extension methods
    /// </summary>
    public static partial class CudaGraphExtensions
    {
        private const string CUDA_DRIVER_LIBRARY = "cuda";

        #region Graph Memory Operations

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddMemcpyNode(
            ref nint phGraphNode,
            nint hGraph,
            nint[] dependencies,
            nuint numDependencies,
            ref CudaMemcpy3DParams copyParams,
            nint ctx);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddMemsetNode(
            ref nint phGraphNode,
            nint hGraph,
            nint[] dependencies,
            nuint numDependencies,
            ref CudaMemsetParams memsetParams,
            nint ctx);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddEventRecordNode(
            ref nint phGraphNode,
            nint hGraph,
            nint[] dependencies,
            nuint numDependencies,
            nint hEvent);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddEventWaitNode(
            ref nint phGraphNode,
            nint hGraph,
            nint[] dependencies,
            nuint numDependencies,
            nint hEvent);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphAddChildGraphNode(
            ref nint phGraphNode,
            nint hGraph,
            nint[] dependencies,
            nuint numDependencies,
            nint childGraph);

        [LibraryImport(CUDA_DRIVER_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cuGraphClone(ref nint phGraphClone, nint originalGraph);

        #endregion
    }

    /// <summary>
    /// CUDA kernel node parameters for graph API
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaKernelNodeParams
    {
        public nint func;
        public uint gridDimX;
        public uint gridDimY;
        public uint gridDimZ;
        public uint blockDimX;
        public uint blockDimY;
        public uint blockDimZ;
        public uint sharedMemBytes;
        public nint kernelParams;
        public nint extra;
    }

    /// <summary>
    /// 3D memory copy parameters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemcpy3DParams
    {
        public nint srcArray;
        public CudaPos srcPos;
        public CudaMemcpy3DPeer srcPtr;
        public nint dstArray;
        public CudaPos dstPos;
        public CudaMemcpy3DPeer dstPtr;
        public CudaExtent3D extent;
        public CudaMemcpyKind kind;
    }

    /// <summary>
    /// 3D position
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaPos
    {
        public nuint x;
        public nuint y;
        public nuint z;
    }

    /// <summary>
    /// 3D memory copy peer
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemcpy3DPeer
    {
        public nint ptr;
        public nuint pitch;
        public nuint height;
        public nuint xsize;
    }

    /// <summary>
    /// 3D extent
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExtent3D
    {
        public nuint width;
        public nuint height;
        public nuint depth;
    }

    /// <summary>
    /// Memory set parameters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemsetParams
    {
        public nint dst;
        public uint value;
        public uint elementSize;
        public nuint width;
        public nuint height;
        public nuint pitch;
    }

    /// <summary>
    /// Memory range attribute
    /// </summary>
    public enum CudaMemRangeAttribute : uint
    {
        ReadMostly = 1,
        PreferredLocation = 2,
        AccessedBy = 3,
        LastPrefetchLocation = 4,
        PreferredLocationType = 5,
        PreferredLocationId = 6,
        LastAccessedBy = 7
    }

    /// <summary>
    /// Memory advise enum
    /// </summary>
    public enum CudaMemoryAdvise : uint
    {
        Unset = 0,
        SetReadMostly = 1,
        UnsetReadMostly = 2,
        SetPreferredLocation = 3,
        UnsetPreferredLocation = 4,
        SetAccessedBy = 5,
        UnsetAccessedBy = 6
    }

    /// <summary>
    /// Host function pointer for callbacks
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CudaHostFn(nint userData);
}