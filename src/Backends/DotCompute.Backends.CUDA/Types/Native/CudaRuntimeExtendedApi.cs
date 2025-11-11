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
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCreateTextureObject(
            out ulong pTexObject,
            ref CudaResourceDesc pResDesc,
            ref CudaTextureDesc pTexDesc,
            ref CudaResourceViewDesc pResViewDesc);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDestroyTextureObject(ulong texObject);

        // Surface Object Management
        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCreateSurfaceObject(
            out ulong pSurfObject,
            ref CudaResourceDesc pResDesc);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDestroySurfaceObject(ulong surfObject);

        #endregion

        #region Dynamic Parallelism

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetAttribute(
            out int value,
            Execution.Graph.Types.CudaDeviceAttribute attr,
            int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceSetCacheConfig(CudaCacheConfig cacheConfig);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetCacheConfig(out CudaCacheConfig cacheConfig);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceSetSharedMemConfig(CudaSharedMemConfig config);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetSharedMemConfig(out CudaSharedMemConfig config);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceSetLimit(CudaLimit limit, nuint value);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDeviceGetLimit(out nuint value, CudaLimit limit);

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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaIpcCloseMemHandle(nint devPtr);

        #endregion

        #region Memory Pools (CUDA 11.2+)

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cudaMemPoolCreate(
            out nint memPool,
            ref CudaMemPoolProps poolProps);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolDestroy(nint memPool);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAttribute(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolGetAttribute(
            nint memPool,
            CudaMemPoolAttribute attr,
            out nint value);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolTrimTo(
            nint memPool,
            nuint minBytesToKeep);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolGetAccess(
            out CudaMemAccessFlags flags,
            nint memPool,
            ref CudaMemLocation location);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemPoolSetAccess(
            nint memPool,
            ref CudaMemAccessDesc desc,
            nuint count);

        #endregion

        #region Virtual Memory Management (CUDA 10.2+)

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemAddressReserve(
            out nint ptr,
            nuint size,
            nuint alignment,
            nint addr,
            ulong flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemAddressFree(
            nint ptr,
            nuint size);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemCreate(
            out nint handle,
            nuint size,
            ref CudaMemAllocationProp prop,
            ulong flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemRelease(nint handle);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemMap(
            nint ptr,
            nuint size,
            nuint offset,
            nint handle,
            ulong flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemUnmap(
            nint ptr,
            nuint size);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemSetAccess(
            nint ptr,
            nuint size,
            ref CudaMemAccessDesc desc,
            nuint count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaMemGetAccess(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphAddMemFreeNode(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphMemFreeNodeGetParams(
            nint node,
            out nint ptr);

        #endregion

        #region External Memory

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaImportExternalMemory(
            out nint extMem,
            ref CudaExternalMemoryHandleDesc memHandleDesc);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaExternalMemoryGetMappedBuffer(
            out nint devPtr,
            nint extMem,
            ref CudaExternalMemoryBufferDesc bufferDesc);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaExternalMemoryGetMappedMipmappedArray(
            out nint mipmappedArray,
            nint extMem,
            ref CudaExternalMemoryMipmappedArrayDesc mipmapDesc);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDestroyExternalMemory(nint extMem);

        #endregion

        #region External Semaphores

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaImportExternalSemaphore(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaDestroyExternalSemaphore(nint extSem);

        #endregion

        #region Graph Batch Operations

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphExecUpdateBatch(
            nint hGraphExec,
            nint hGraph,
            out nint hErrorNode_out,
            CudaGraphExecUpdateResult updateResult_out);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphUpload(
            nint graphExec,
            nint stream);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphAddBatchMemOpNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaBatchMemOpNodeParams pNodeParams);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphBatchMemOpNodeGetParams(
            nint node,
            out CudaBatchMemOpNodeParams pNodeParams);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphBatchMemOpNodeSetParams(
            nint node,
            ref CudaBatchMemOpNodeParams pNodeParams);

        #endregion

        #region Task Management

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaLaunchHostFunc(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cuGraphAddMemsetNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            ref CudaMemsetParams pMemsetParams);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cuGraphAddEventRecordNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint event_);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cuGraphAddEventWaitNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint event_);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cuGraphAddChildGraphNode(
            out nint pGraphNode,
            nint graph,
            nint pDependencies,
            nuint numDependencies,
            nint childGraph);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static extern CudaError cuGraphClone(
            out nint pGraphClone,
            nint originalGraph);

        [DllImport(CUDA_LIBRARY, CallingConvention = CallingConvention.Cdecl)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphHostNodeSetParams(
            nint node,
            ref CudaHostNodeParams pNodeParams);

        #endregion

        #region User Objects

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaUserObjectCreate(
            out nint object_out,
            nint ptr,
            CudaHostFn destroy,
            uint initialRefcount,
            uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaUserObjectRetain(
            nint object_,
            uint count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaUserObjectRelease(
            nint object_,
            uint count);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphRetainUserObject(
            nint graph,
            nint object_,
            uint count,
            uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGraphReleaseUserObject(
            nint graph,
            nint object_,
            uint count);

        #endregion

        #region Cluster Launch (CUDA 12.0+)

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaLaunchKernelExC(
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

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxCreate(
            out nint pctx,
            uint flags,
            int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxCreateEx(
            out nint pctx,
            ref CudaContextCreateParams params_,
            int numParams,
            uint flags,
            int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxDestroy(nint ctx);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxPushCurrent(nint ctx);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxPopCurrent(out nint pctx);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxGetApiVersion(
            nint ctx,
            out uint version);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxGetDevice(out int device);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxGetFlags(out uint flags);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaCtxGetId(
            nint ctx,
            out ulong ctxId);

        #endregion

        #region Driver Entry Points

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA2101:Specify marshaling for P/Invoke string arguments",
            Justification = "String marshaling is explicitly specified with MarshalAs attribute")]
        [DllImport(CUDA_LIBRARY, CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetDriverEntryPoint(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string symbol,
            out nint funcPtr,
            ulong flags,
            CudaDriverEntryPointQueryResult driverStatus);

        [DllImport(CUDA_LIBRARY)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        internal static extern CudaError cudaGetFuncBySymbol(
            out CudaFunc func,
            nint symbol);

        #endregion
    }
}
