using System;
using System.Runtime.InteropServices;

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
    public static extern CudaError cudaGetDeviceCount(out int count);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaSetDevice(int device);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaGetDevice(out int device);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaGetDeviceProperties(ref CudaDeviceProperties prop, int device);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaDeviceSynchronize();

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaDeviceReset();

    // Context Management (Driver API)
    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuDevicePrimaryCtxRetain(ref IntPtr pctx, int dev);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuDevicePrimaryCtxRelease(int dev);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuCtxSetCurrent(IntPtr ctx);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuCtxGetCurrent(ref IntPtr pctx);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuCtxSynchronize();

    // Memory Management
    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMalloc(ref IntPtr devPtr, ulong size);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaFree(IntPtr devPtr);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMemcpy(IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMemcpyAsync(IntPtr dst, IntPtr src, ulong count, CudaMemcpyKind kind, IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMemset(IntPtr devPtr, int value, ulong count);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMemsetAsync(IntPtr devPtr, int value, ulong count, IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaMemGetInfo(out ulong free, out ulong total);

    // Stream Management
    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaStreamCreate(ref IntPtr pStream);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaStreamCreateWithFlags(ref IntPtr pStream, uint flags);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaStreamDestroy(IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaStreamSynchronize(IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaStreamQuery(IntPtr stream);

    // Module Management (Driver API)
    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuModuleLoadData(ref IntPtr module, IntPtr image);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuModuleLoadDataEx(ref IntPtr module, IntPtr image, uint numOptions, IntPtr options, IntPtr optionValues);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuModuleUnload(IntPtr hmod);

    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, [MarshalAs(UnmanagedType.LPStr)] string name);

    // Kernel Execution (Driver API)
    [DllImport(CUDA_DRIVER_LIBRARY)]
    public static extern CudaError cuLaunchKernel(
        IntPtr f,
        uint gridDimX, uint gridDimY, uint gridDimZ,
        uint blockDimX, uint blockDimY, uint blockDimZ,
        uint sharedMemBytes,
        IntPtr hStream,
        IntPtr kernelParams,
        IntPtr extra);

    // Error Handling
    [DllImport(CUDA_LIBRARY)]
    public static extern IntPtr cudaGetErrorString(CudaError error);

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaGetLastError();

    [DllImport(CUDA_LIBRARY)]
    public static extern CudaError cudaPeekAtLastError();

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
/// CUDA device properties
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CudaDeviceProperties
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Name;
    
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
}

/// <summary>
/// CUDA exception
/// </summary>
public class CudaException : Exception
{
    public CudaError ErrorCode { get; }

    public CudaException(string message, CudaError errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}