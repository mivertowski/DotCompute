// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native.Types;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Extended CUDA Runtime API bindings for advanced features
    /// </summary>
    public static partial class CudaRuntimeExtended
    {
        private const string CUDA_LIBRARY = "cudart";
        private const string CUDA_DRIVER_LIBRARY = "cuda";
        private const string NVRTC_LIBRARY = "nvrtc";
        private const string CUBLAS_LIBRARY = "cublas";
        private const string CUDNN_LIBRARY = "cudnn";
        private const string CUFFT_LIBRARY = "cufft";
        private const string CUSPARSE_LIBRARY = "cusparse";
        private const string CURAND_LIBRARY = "curand";
        private const string CUSOLVER_LIBRARY = "cusolver";
        private const string NCCL_LIBRARY = "nccl";

        #region Texture and Surface Memory

        // Texture Object Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCreateTextureObject(
            out ulong pTexObject,
            ref CudaResourceDesc pResDesc,
            ref CudaTextureDesc pTexDesc,
            ref CudaResourceViewDesc pResViewDesc);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDestroyTextureObject(ulong texObject);

        // Surface Object Management
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCreateSurfaceObject(
            out ulong pSurfObject,
            ref CudaResourceDesc pResDesc);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDestroySurfaceObject(ulong surfObject);

        #endregion

        #region Dynamic Parallelism

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetAttribute(
            out int value,
            Execution.Graph.Types.CudaDeviceAttribute attr,
            int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSetCacheConfig(CudaCacheConfig cacheConfig);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetCacheConfig(out CudaCacheConfig cacheConfig);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSetSharedMemConfig(CudaSharedMemConfig config);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetSharedMemConfig(out CudaSharedMemConfig config);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceSetLimit(CudaLimit limit, nuint value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDeviceGetLimit(out nuint value, CudaLimit limit);

        #endregion

        #region IPC (Inter-Process Communication)

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaIpcGetEventHandle(
            out CudaIpcEventHandle handle,
            nint eventHandle);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaIpcOpenEventHandle(
            out nint eventHandle,
            CudaIpcEventHandle handle);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaIpcGetMemHandle(
            out CudaIpcMemHandle handle,
            nint devPtr);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaIpcOpenMemHandle(
            out nint devPtr,
            CudaIpcMemHandle handle,
            uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaIpcCloseMemHandle(nint devPtr);

        #endregion

        #region Memory Pools (CUDA 11.2+)

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolCreate(
            out nint memPool,
            ref CudaMemPoolProps poolProps);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolDestroy(nint memPool);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            nint value);

        // Add overload for ReleaseThreshold
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            ref ulong value);

        // Add overload for integer attributes (like ReuseAllowOpportunistic)
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            ref int value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolGetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            out nint value);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolTrimTo(
            nint memPool,
            nuint minBytesToKeep);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolGetAccess(
            out CudaMemAccessFlags flags,
            nint memPool,
            ref CudaMemLocation location);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAccess(
            nint memPool,
            ref CudaMemAccessDesc desc,
            nuint count);

        #endregion

        #region Virtual Memory Management (CUDA 10.2+)

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemAddressReserve(
            out nint ptr,
            nuint size,
            nuint alignment,
            nint addr,
            ulong flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemAddressFree(
            nint ptr,
            nuint size);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemCreate(
            out nint handle,
            nuint size,
            ref CudaMemAllocationProp prop,
            ulong flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemRelease(nint handle);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemMap(
            nint ptr,
            nuint size,
            nuint offset,
            nint handle,
            ulong flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemUnmap(
            nint ptr,
            nuint size);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemSetAccess(
            nint ptr,
            nuint size,
            ref CudaMemAccessDesc desc,
            nuint count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemGetAccess(
            out ulong flags,
            ref CudaMemLocation location,
            nint ptr);

        #endregion

        #region Graph Memory Nodes (CUDA 11.4+)

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphAddMemAllocNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaMemAllocNodeParams nodeParams);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphAddMemFreeNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint ptr);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphMemAllocNodeGetParams(
            nint node,
            out CudaMemAllocNodeParams pNodeParams);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphMemFreeNodeGetParams(
            nint node,
            out nint ptr);

        #endregion

        #region External Memory

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaImportExternalMemory(
            out nint extMem,
            ref CudaExternalMemoryHandleDesc memHandleDesc);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaExternalMemoryGetMappedBuffer(
            out nint devPtr,
            nint extMem,
            ref CudaExternalMemoryBufferDesc bufferDesc);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaExternalMemoryGetMappedMipmappedArray(
            out nint mipmappedArray,
            nint extMem,
            ref CudaExternalMemoryMipmappedArrayDesc mipmapDesc);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDestroyExternalMemory(nint extMem);

        #endregion

        #region External Semaphores

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaImportExternalSemaphore(
            out nint extSem,
            ref CudaExternalSemaphoreHandleDesc semHandleDesc);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaSignalExternalSemaphoresAsync(
            nint extSemArray,
            ref CudaExternalSemaphoreSignalParams paramsArray,
            uint numExtSems,
            nint stream);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaWaitExternalSemaphoresAsync(
            nint extSemArray,
            ref CudaExternalSemaphoreWaitParams paramsArray,
            uint numExtSems,
            nint stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaDestroyExternalSemaphore(nint extSem);

        #endregion

        #region Graph Batch Operations

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphExecUpdateBatch(
            nint hGraphExec,
            nint hGraph,
            out nint hErrorNode_out,
            CudaGraphExecUpdateResult updateResult_out);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphUpload(
            nint graphExec,
            nint stream);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphAddBatchMemOpNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaBatchMemOpNodeParams pNodeParams);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphBatchMemOpNodeGetParams(
            nint node,
            out CudaBatchMemOpNodeParams pNodeParams);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphBatchMemOpNodeSetParams(
            nint node,
            ref CudaBatchMemOpNodeParams pNodeParams);

        #endregion

        #region Task Management

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaLaunchHostFunc(
            nint stream,
            CudaHostFn fn,
            nint userData);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphAddHostNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaHostNodeParams pNodeParams);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphHostNodeGetParams(
            nint node,
            out CudaHostNodeParams pNodeParams);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphHostNodeSetParams(
            nint node,
            ref CudaHostNodeParams pNodeParams);

        #endregion

        #region User Objects

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaUserObjectCreate(
            out nint object_out,
            nint ptr,
            CudaHostFn destroy,
            uint initialRefcount,
            uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaUserObjectRetain(
            nint object_,
            uint count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaUserObjectRelease(
            nint object_,
            uint count);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphRetainUserObject(
            nint graph,
            nint object_,
            uint count,
            uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGraphReleaseUserObject(
            nint graph,
            nint object_,
            uint count);

        #endregion

        #region Cluster Launch (CUDA 12.0+)

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaLaunchKernelExC(
            ref CudaNativeLaunchConfig config,
            CudaKernelFunc kernel_func,
            nint kernel_args);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaOccupancyMaxPotentialClusterSize(
            out int clusterSize,
            CudaKernelFunc func,
            ref CudaLaunchAttribute launchAttr,
            int numAttributes);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaOccupancyMaxActiveClusters(
            out int numClusters,
            CudaKernelFunc func,
            ref CudaLaunchAttribute launchAttr,
            int numAttributes,
            int clusterSize);

        #endregion

        #region Green Contexts (CUDA 12.4+)

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxCreate(
            out nint pctx,
            uint flags,
            int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxCreateEx(
            out nint pctx,
            ref CudaContextCreateParams params_,
            int numParams,
            uint flags,
            int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxDestroy(nint ctx);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxPushCurrent(nint ctx);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxPopCurrent(out nint pctx);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxGetApiVersion(
            nint ctx,
            out uint version);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxGetDevice(out int device);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxGetFlags(out uint flags);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaCtxGetId(
            nint ctx,
            out ulong ctxId);

        #endregion

        #region Driver Entry Points

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetDriverEntryPoint(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string symbol,
            out nint funcPtr,
            ulong flags,
            CudaDriverEntryPointQueryResult driverStatus);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaGetFuncBySymbol(
            out CudaFunc func,
            nint symbol);

        #endregion
    }

    #region Supporting Types

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaResourceDesc
    {
        public CudaResourceType resType;
        public nint array;
        public nint mipmap;
        public nint devPtr;
        public nuint sizeInBytes;
        public uint format;
        public uint numChannels;
        public nuint width;
        public nuint height;
        public nuint depth;
        public nuint pitchInBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaTextureDesc
    {
        public CudaTextureAddressMode addressMode0;
        public CudaTextureAddressMode addressMode1;
        public CudaTextureAddressMode addressMode2;
        public CudaTextureFilterMode filterMode;
        public CudaTextureReadMode readMode;
        public int sRGB;
        public float borderColor0;
        public float borderColor1;
        public float borderColor2;
        public float borderColor3;
        public int normalizedCoords;
        public uint maxAnisotropy;
        public CudaTextureFilterMode mipmapFilterMode;
        public float mipmapLevelBias;
        public float minMipmapLevelClamp;
        public float maxMipmapLevelClamp;
        public int disableTrilinearOptimization;
        public int seamlessCubemap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaResourceViewDesc
    {
        public CudaResourceViewFormat format;
        public nuint width;
        public nuint height;
        public nuint depth;
        public uint firstMipmapLevel;
        public uint lastMipmapLevel;
        public uint firstLayer;
        public uint lastLayer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcEventHandle
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaIpcMemHandle
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemPoolProps
    {
        public CudaMemAllocationType AllocType;
        public CudaMemAllocationHandleType HandleTypes;
        public CudaMemLocation Location;
        public nint win32SecurityAttributes;
        public nuint maxSize;
        public ushort usage;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 56)]
        public byte[] reserved;

        // Legacy properties for backward compatibility
        public CudaMemAllocationType allocType { get => AllocType; set => AllocType = value; }
        public CudaMemAllocationHandleType handleTypes { get => HandleTypes; set => HandleTypes = value; }
        public CudaMemLocation location { get => Location; set => Location = value; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemLocation
    {
        public CudaMemLocationType Type;
        public int Id;

        // Legacy properties for backward compatibility
        public CudaMemLocationType type { get => Type; set => Type = value; }
        public int id { get => Id; set => Id = value; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAccessDesc
    {
        public CudaMemLocation location;
        public CudaMemAccessFlags flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocationProp
    {
        public CudaMemAllocationType type;
        public CudaMemAllocationHandleType requestedHandleTypes;
        public CudaMemLocation location;
        public nint win32HandleMetaData;
        public CudaMemAllocationCompType compressionType;
        public ushort usage;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaMemAllocNodeParams
    {
        public CudaMemPoolProps poolProps;
        public CudaMemAccessDesc accessDescs;
        public nuint accessDescCount;
        public nuint bytesize;
        public nint dptr;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryHandleDesc
    {
        public CudaExternalMemoryHandleType type;
        public nint handle;
        public nint win32Handle;
        public nint name;
        public nuint size;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryBufferDesc
    {
        public nuint offset;
        public nuint size;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalMemoryMipmappedArrayDesc
    {
        public nuint offset;
        public CudaArrayFormat formatDesc;
        public CudaExtent extent;
        public uint flags;
        public uint numLevels;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreHandleDesc
    {
        public CudaExternalSemaphoreHandleType type;
        public nint handle;
        public nint win32Handle;
        public nint name;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalParams
    {
        public CudaExternalSemaphoreSignalFlags flags;
        public CudaExternalSemaphoreSignalNodeParams nodeParams;
        public ulong keyedMutex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitParams
    {
        public CudaExternalSemaphoreWaitFlags flags;
        public CudaExternalSemaphoreWaitNodeParams nodeParams;
        public ulong keyedMutex;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public uint[] reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreSignalNodeParams
    {
        public nint extSemArray;
        public nint paramsArray;
        public uint numExtSems;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExternalSemaphoreWaitNodeParams
    {
        public nint extSemArray;
        public nint paramsArray;
        public uint numExtSems;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaBatchMemOpNodeParams
    {
        public nint ctx;
        public uint count;
        public nint paramArray;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaNativeLaunchConfig
    {
        public CudaDim3 gridDim;
        public CudaDim3 blockDim;
        public nuint dynamicSmemBytes;
        public nint stream;
        public nint attrs;
        public uint numAttrs;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDim3
    {
        public uint x;
        public uint y;
        public uint z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchAttribute
    {
        public CudaLaunchAttributeID id;
        public CudaLaunchAttributeValue val;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct CudaLaunchAttributeValue
    {
        [FieldOffset(0)] public CudaAccessPolicyWindow accessPolicyWindow;
        [FieldOffset(0)] public int cooperative;
        [FieldOffset(0)] public CudaSynchronizationPolicy syncPolicy;
        [FieldOffset(0)] public CudaClusterDimension clusterDim;
        [FieldOffset(0)] public CudaClusterSchedulingPolicy clusterSchedulingPolicyPreference;
        [FieldOffset(0)] public uint programmaticStreamSerializationAllowed;
        [FieldOffset(0)] public CudaEvent programmaticEvent;
        [FieldOffset(0)] public int priority;
        [FieldOffset(0)] public CudaLaunchMemSyncDomain memSyncDomain;
        [FieldOffset(0)] public CudaLaunchMemSyncDomainMap memSyncDomainMap;
        [FieldOffset(0)] public ulong launchCompletionEvent;
        [FieldOffset(0)] public CudaDeviceNumaConfig deviceUpdatableKernelNode;
        [FieldOffset(0)] public uint preferredShmemCarveout;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaAccessPolicyWindow
    {
        public nint base_ptr;
        public nuint num_bytes;
        public float hitRatio;
        public CudaAccessProperty hitProp;
        public CudaAccessProperty missProp;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaClusterDimension
    {
        public uint x;
        public uint y;
        public uint z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaLaunchMemSyncDomainMap
    {
        public byte default_;
        public byte remote;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaDeviceNumaConfig
    {
        public byte numaNode;
        public byte numaNodeId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaContextCreateParams
    {
        public nint ctx;
        public uint flags;
        public nint nvSciSyncAttrList;
        public uint priority;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaArrayFormat
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;
        public CudaArrayFormatKind f;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CudaExtent
    {
        public nuint width;
        public nuint height;
        public nuint depth;
    }

    // Enums
    public enum CudaResourceType : uint
    {
        Array = 0,
        MipmappedArray = 1,
        Linear = 2,
        Pitch2D = 3
    }

    public enum CudaResourceViewFormat : uint
    {
        None = 0,
        Uint1X8 = 1,
        Uint1X16 = 2,
        Uint1X32 = 3,
        Uint2X8 = 4,
        Uint2X16 = 5,
        Uint2X32 = 6,
        Uint4X8 = 7,
        Uint4X16 = 8,
        Uint4X32 = 9,
        Sint1X8 = 10,
        Sint1X16 = 11,
        Sint1X32 = 12,
        Sint2X8 = 13,
        Sint2X16 = 14,
        Sint2X32 = 15,
        Sint4X8 = 16,
        Sint4X16 = 17,
        Sint4X32 = 18,
        Float1X16 = 19,
        Float1X32 = 20,
        Float2X16 = 21,
        Float2X32 = 22,
        Float4X16 = 23,
        Float4X32 = 24
    }

    public enum CudaTextureAddressMode : uint
    {
        Wrap = 0,
        Clamp = 1,
        Mirror = 2,
        Border = 3
    }

    public enum CudaTextureFilterMode : uint
    {
        Point = 0,
        Linear = 1
    }

    public enum CudaTextureReadMode : uint
    {
        ElementType = 0,
        NormalizedFloat = 1
    }

    public enum CudaCacheConfig : uint
    {
        PreferNone = 0,
        PreferShared = 1,
        PreferCache = 2,
        PreferEqual = 3
    }

    public enum CudaSharedMemConfig : uint
    {
        BankSizeDefault = 0,
        BankSizeFourByte = 1,
        BankSizeEightByte = 2
    }

    public enum CudaLimit : uint
    {
        StackSize = 0,
        PrintfFifoSize = 1,
        MallocHeapSize = 2,
        DevRuntimeSyncDepth = 3,
        DevRuntimePendingLaunchCount = 4,
        MaxL2FetchGranularity = 5,
        PersistingL2CacheSize = 6
    }

    public enum CudaMemPoolAttribute : uint
    {
        ReuseFollowEventDependencies = 1,
        ReuseAllowOpportunistic = 2,
        ReuseAllowInternalDependencies = 3,
        ReleaseThreshold = 4,
        AttrReleaseThreshold = 4,
        AttrReservedMemCurrent = 5,
        AttrReservedMemHigh = 6,
        AttrUsedMemCurrent = 7,
        AttrUsedMemHigh = 8,
        // Additional commonly used attributes
        Used = AttrUsedMemCurrent,
        Reserved = AttrReservedMemCurrent
    }

    public enum CudaMemAccessFlags : uint
    {
        None = 0,
        Read = 1,
        ReadWrite = 3
    }

    public enum CudaMemLocationType : uint
    {
        Invalid = 0,
        Device = 1,
        Host = 2,
        HostNuma = 3,
        HostNumaCurrent = 4
    }

    public enum CudaMemAllocationType : uint
    {
        Invalid = 0,
        Pinned = 1,
        Max = 0xFFFFFFFF
    }

    public enum CudaMemAllocationHandleType : uint
    {
        None = 0,
        PosixFileDescriptor = 1,
        Win32 = 2,
        Win32Kmt = 4,
        Max = 0xFFFFFFFF
    }

    public enum CudaMemAllocationCompType : byte
    {
        None = 0,
        Generic = 1
    }

    public enum CudaGraphExecUpdateResult : uint
    {
        Success = 0,
        Error = 1,
        ErrorTopologyChanged = 2,
        ErrorNodeTypeChanged = 3,
        ErrorFunctionChanged = 4,
        ErrorParametersChanged = 5,
        ErrorNotSupported = 6,
        ErrorUnsupportedFunctionChange = 7,
        ErrorAttributesChanged = 8
    }

    public enum CudaExternalMemoryHandleType : uint
    {
        OpaqueFd = 1,
        OpaqueWin32 = 2,
        OpaqueWin32Kmt = 3,
        D3D12Heap = 4,
        D3D12Resource = 5,
        D3D11Resource = 6,
        D3D11ResourceKmt = 7,
        NvSciBuf = 8
    }

    public enum CudaExternalSemaphoreHandleType : uint
    {
        OpaqueFd = 1,
        OpaqueWin32 = 2,
        OpaqueWin32Kmt = 3,
        D3D12Fence = 4,
        D3D11Fence = 5,
        NvSciSync = 6,
        KeyedMutex = 7,
        KeyedMutexKmt = 8,
        TimelineSemaphoreFd = 9,
        TimelineSemaphoreWin32 = 10
    }

    public enum CudaExternalSemaphoreSignalFlags : uint
    {
        None = 0,
        SignalSkipNvSciBufMemSync = 1,
        SignalSkipNvSciSyncFence = 2
    }

    public enum CudaExternalSemaphoreWaitFlags : uint
    {
        None = 0,
        WaitSkipNvSciBufMemSync = 1,
        WaitSkipNvSciSyncFence = 2
    }

    public enum CudaLaunchAttributeID : uint
    {
        Ignore = 0,
        AccessPolicyWindow = 1,
        Cooperative = 2,
        SynchronizationPolicy = 3,
        ClusterDimension = 4,
        ClusterSchedulingPolicyPreference = 5,
        ProgrammaticStreamSerialization = 6,
        ProgrammaticEvent = 7,
        Priority = 8,
        MemSyncDomainMap = 9,
        MemSyncDomain = 10,
        LaunchCompletionEvent = 12,
        DeviceUpdatableKernelNode = 13,
        PreferredSharedMemoryCarveout = 14
    }

    public enum CudaSynchronizationPolicy : uint
    {
        Auto = 1,
        Spin = 2,
        Yield = 3,
        BlockingSync = 4
    }

    public enum CudaClusterSchedulingPolicy : uint
    {
        Default = 0,
        Spread = 1,
        LoadBalancing = 2
    }

    public enum CudaLaunchMemSyncDomain : uint
    {
        Default = 0,
        Remote = 1
    }

    public enum CudaAccessProperty : uint
    {
        Normal = 0,
        Streaming = 1,
        Persisting = 2
    }

    public enum CudaDriverEntryPointQueryResult : uint
    {
        Success = 0,
        SymbolNotFound = 1,
        VersionNotSufficent = 2
    }

    public enum CudaArrayFormatKind : uint
    {
        Unsigned = 0,
        Signed = 1,
        Float = 2,
        None = 3,
        Nv12 = 4,
        UnsignedNormalized8X1 = 0x10,
        UnsignedNormalized8X2 = 0x11,
        UnsignedNormalized8X4 = 0x12,
        UnsignedNormalized16X1 = 0x13,
        UnsignedNormalized16X2 = 0x14,
        UnsignedNormalized16X4 = 0x15,
        SignedNormalized8X1 = 0x16,
        SignedNormalized8X2 = 0x17,
        SignedNormalized8X4 = 0x18,
        SignedNormalized16X1 = 0x19,
        SignedNormalized16X2 = 0x1a,
        SignedNormalized16X4 = 0x1b,
        Unsigned64 = 0x1c,
        Signed64 = 0x1d,
        Half = 0x1e,
        Half2 = 0x1f,
        Half4 = 0x20,
        Half8 = 0x21,
        Half16 = 0x22,
        Bfloat16 = 0x23,
        Bfloat162 = 0x24,
        Bfloat164 = 0x25
    }

    // Delegate is already defined in CudaGraphTypes.cs

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaKernelFunc();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaFunc();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint CudaEvent();

    #endregion
}