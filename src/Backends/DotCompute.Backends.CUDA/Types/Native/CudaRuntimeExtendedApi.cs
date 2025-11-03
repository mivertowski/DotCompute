// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Types.Native.Delegates;
using DotCompute.Backends.CUDA.Types.Native.Enums;
using DotCompute.Backends.CUDA.Types.Native.Structs;

namespace DotCompute.Backends.CUDA.Types.Native
{
    /// <summary>
    /// Extended CUDA Runtime API bindings for advanced features
    /// </summary>
    public static partial class CudaRuntimeExtendedApi
    {
        private const string CUDA_LIBRARY = "cudart";

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

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cudaMemPoolCreate(
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
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            ref ulong value);

        // Add overload for integer attributes (like ReuseAllowOpportunistic)
        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static partial CudaError cudaMemPoolSetAttribute(
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
        internal static extern CudaError cudaSignalExternalSemaphores(
            nint extSemArray,
            ref CudaExternalSemaphoreSignalParams paramsArray,
            uint numExtSems,
            nint stream);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaWaitExternalSemaphores(
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

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cuGraphAddMemsetNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaMemsetParams pMemsetParams);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cuGraphAddEventRecordNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint event_);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cuGraphAddEventWaitNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint event_);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cuGraphAddChildGraphNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint childGraph);

        [LibraryImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial CudaError cuGraphClone(
            out nint pGraphClone,
            nint originalGraph);

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
}
