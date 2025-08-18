// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using DotCompute.Backends.CUDA.Native;

namespace DotCompute.Backends.CUDA.Native;

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
/// CUDA error codes
/// </summary>
public enum CudaError
{
    Success = 0,
    InvalidValue = 1,
    MemoryAllocation = 2,
    InitializationError = 3,
    LaunchFailure = 4,
    LaunchTimeout = 5,
    LaunchOutOfResources = 6,
    InvalidDeviceFunction = 7,
    InvalidConfiguration = 8,
    InvalidDevice = 9,
    InvalidMemcpyDirection = 10,
    InvalidHostPointer = 11,
    InvalidDevicePointer = 12,
    InvalidTexture = 13,
    InvalidTextureBinding = 14,
    InvalidChannelDescriptor = 15,
    InvalidMemcpyChannelDesc = 16,
    InvalidFilterSetting = 17,
    InvalidNormSetting = 18,
    MixedDeviceExecution = 19,
    NotYetImplemented = 20,
    MemoryValueTooLarge = 21,
    InsufficientDriver = 22,
    InvalidSurface = 23,
    DuplicateVariableName = 24,
    DuplicateTextureName = 25,
    DuplicateSurfaceName = 26,
    DevicesUnavailable = 27,
    InvalidKernelImage = 28,
    NoKernelImageForDevice = 29,
    IncompatibleDriverContext = 30,
    PeerAccessAlreadyEnabled = 31,
    PeerAccessNotEnabled = 32,
    DeviceAlreadyInUse = 33,
    ProfilerDisabled = 34,
    ProfilerNotInitialized = 35,
    ProfilerAlreadyStarted = 36,
    ProfilerAlreadyStopped = 37,
    NoDevice = 38,
    InvalidHandle = 39,
    NotFound = 40,
    NotReady = 41,
    IllegalAddress = 42,
    LaunchIncompatibleTexturing = 43,
    PeerAccessUnsupported = 44,
    InvalidPtx = 45,
    InvalidGraphicsContext = 46,
    NvlinkUncorrectable = 47,
    JitCompilerNotFound = 48,
    InvalidSource = 49,
    FileNotFound = 50,
    SharedObjectSymbolNotFound = 51,
    SharedObjectInitFailed = 52,
    OperatingSystem = 53,
    InvalidResourceHandle = 54,
    NotSupported = 55,
    SystemNotReady = 56,
    SystemDriverMismatch = 57,
    CompatNotSupportedOnDevice = 58,
    StreamCaptureUnsupported = 59,
    StreamCaptureInvalidated = 60,
    StreamCaptureMerge = 61,
    StreamCaptureUnmatched = 62,
    StreamCaptureUnjoined = 63,
    StreamCaptureIsolation = 64,
    StreamCaptureImplicit = 65,
    CapturedEvent = 66,
    StreamCaptureWrongThread = 67,
    Timeout = 68,
    GraphExecUpdateFailure = 69,
    Unknown = 999
}

/// <summary>
/// Memory copy types
/// </summary>
public enum CudaMemcpyKind
{
    HostToHost = 0,
    HostToDevice = 1,
    DeviceToHost = 2,
    DeviceToDevice = 3,
    Default = 4
}

/// <summary>
/// CUDA memory advice for unified memory
/// </summary>
public enum CudaMemoryAdvise
{
    SetReadMostly = 1,
    UnsetReadMostly = 2,
    SetPreferredLocation = 3,
    UnsetPreferredLocation = 4,
    SetAccessedBy = 5,
    UnsetAccessedBy = 6
}

/// <summary>
/// CUDA host allocation flags
/// </summary>
[Flags]
public enum CudaHostAllocFlags : uint
{
    Default = 0,
    Pinned = 1,
    Mapped = 2,
    WriteCombined = 4,
    Portable = 8
}

/// <summary>
/// CUDA device properties - matches cudaDeviceProp from CUDA 12.x
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 1032, CharSet = CharSet.Ansi)]
#pragma warning disable CA1815 // Override equals and operator equals on value types - P/Invoke struct doesn't need equality
public struct CudaDeviceProperties
#pragma warning restore CA1815
{
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Name;

    [FieldOffset(256)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] Uuid; // cudaUUID_t - 16 bytes

    [FieldOffset(272)]
    public ulong TotalGlobalMem;
    
    [FieldOffset(280)]
    public ulong SharedMemPerBlock;
    
    [FieldOffset(288)]
    public int RegsPerBlock;
    
    [FieldOffset(292)]
    public int WarpSize;
    
    [FieldOffset(296)]
    public ulong MemPitch;
    
    [FieldOffset(304)]
    public int MaxThreadsPerBlock;

    [FieldOffset(308)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxThreadsDim;

    [FieldOffset(320)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public int[] MaxGridSize;

    [FieldOffset(332)]
    public int ClockRate;
    
    [FieldOffset(336)]
    public ulong TotalConstMem;
    
    [FieldOffset(360)]
    public int Major;
    
    [FieldOffset(364)]
    public int Minor;
    
    [FieldOffset(368)]
    public ulong TextureAlignment;
    
    [FieldOffset(376)]
    public ulong TexturePitchAlignment;
    
    [FieldOffset(384)]
    public int DeviceOverlap;
    
    [FieldOffset(388)]
    public int MultiProcessorCount;
    
    [FieldOffset(392)]
    public int KernelExecTimeoutEnabled;
    
    [FieldOffset(396)]
    public int Integrated;
    
    [FieldOffset(400)]
    public int CanMapHostMemory;
    
    [FieldOffset(404)]
    public int ComputeMode;
    
    // Skip texture and surface fields (408-575)
    // Add PCI and concurrent kernel fields
    
    [FieldOffset(576)]
    public int ConcurrentKernels;
    
    [FieldOffset(580)]
    public int ECCEnabled;
    
    [FieldOffset(584)]
    public int PciBusId;
    
    [FieldOffset(588)]
    public int PciDeviceId;
    
    [FieldOffset(592)]
    public int PciDomainId;
    
    // Skip tccDriver field at 596
    
    [FieldOffset(600)]
    public int AsyncEngineCount;
    
    [FieldOffset(604)]
    public int UnifiedAddressing;
    
    [FieldOffset(608)]
    public int MemoryClockRate;
    
    [FieldOffset(612)]
    public int MemoryBusWidth;
    
    [FieldOffset(616)]
    public int L2CacheSize;
    
    [FieldOffset(620)]
    public int PersistingL2CacheMaxSize;
    
    [FieldOffset(624)]
    public int MaxThreadsPerMultiProcessor;
    
    [FieldOffset(628)]
    public int StreamPrioritiesSupported;
    
    [FieldOffset(632)]
    public int GlobalL1CacheSupported;
    
    [FieldOffset(636)]
    public int LocalL1CacheSupported;
    
    [FieldOffset(640)]
    public ulong SharedMemPerMultiprocessor;
    
    [FieldOffset(648)]
    public int RegsPerMultiprocessor;
    
    [FieldOffset(652)]
    public int ManagedMemory;
    
    [FieldOffset(656)]
    public int IsMultiGpuBoard;
    
    [FieldOffset(660)]
    public int MultiGpuBoardGroupID;
    
    [FieldOffset(668)]
    public int SingleToDoublePrecisionPerfRatio;
    
    [FieldOffset(672)]
    public int PageableMemoryAccess;
    
    [FieldOffset(676)]
    public int ConcurrentManagedAccess;
    
    [FieldOffset(680)]
    public int ComputePreemptionSupported;
    
    [FieldOffset(684)]
    public int CanUseHostPointerForRegisteredMem;
    
    [FieldOffset(688)]
    public int CooperativeLaunch;
    
    [FieldOffset(692)]
    public int CooperativeMultiDeviceLaunch;
    
    [FieldOffset(696)]
    public ulong SharedMemPerBlockOptin;
    
    [FieldOffset(704)]
    public int PageableMemoryAccessUsesHostPageTables;
    
    [FieldOffset(708)]
    public int DirectManagedMemAccessFromHost;
    
    // Total struct size is 1032 bytes as of CUDA 12.x
}

/// <summary>
/// CUDA exception
/// </summary>
public class CudaException : Exception
{
    public CudaError ErrorCode { get; }

    public CudaException() : base()
    {
    }

    public CudaException(string message) : base(message)
    {
    }

    public CudaException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CudaException(string message, CudaError errorCode) : base(message)
    {
        ErrorCode = errorCode;
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

