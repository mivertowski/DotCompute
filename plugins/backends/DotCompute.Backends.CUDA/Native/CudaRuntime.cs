// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;

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

    // Stream Management
    [DllImport(CUDA_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamCreate(ref IntPtr pStream);

    [DllImport(CUDA_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamCreateWithFlags(ref IntPtr pStream, uint flags);

    [DllImport(CUDA_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamDestroy(IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamSynchronize(IntPtr stream);

    [DllImport(CUDA_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern CudaError cudaStreamQuery(IntPtr stream);

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
    internal static extern CudaError cuModuleGetFunction(ref IntPtr hfunc, IntPtr hmod, [MarshalAs(UnmanagedType.LPStr)] string name);

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
#pragma warning disable CA1815 // Override equals and operator equals on value types - P/Invoke struct doesn't need equality
public struct CudaDeviceProperties
#pragma warning restore CA1815
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
/// P/Invoke wrapper for NVRTC (NVIDIA Runtime Compilation) API
/// </summary>
public static class NvrtcRuntime
{
    private const string NVRTC_LIBRARY = "nvrtc";

    // NVRTC Core Functions
    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcVersion(out int major, out int minor);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetSupportedArchs(out int[] supportedArchs);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcCreateProgram(
        out IntPtr prog,
        [MarshalAs(UnmanagedType.LPStr)] string src,
        [MarshalAs(UnmanagedType.LPStr)] string? name,
        int numHeaders,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? headers,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? includeNames);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcDestroyProgram(ref IntPtr prog);

    [DllImport(NVRTC_LIBRARY, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcCompileProgram(
        IntPtr prog,
        int numOptions,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? options);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetPTXSize(IntPtr prog, out IntPtr ptxSizeRet);

    [DllImport(NVRTC_LIBRARY, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes - Performance optimization is handled by the caller
    internal static extern NvrtcResult nvrtcGetPTX(IntPtr prog, [Out] StringBuilder ptx);
#pragma warning restore CA1838

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetCUBINSize(IntPtr prog, out IntPtr cubinSizeRet);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetCUBIN(IntPtr prog, [Out] byte[] cubin);

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern NvrtcResult nvrtcGetProgramLogSize(IntPtr prog, out IntPtr logSizeRet);

    [DllImport(NVRTC_LIBRARY, CharSet = CharSet.Ansi)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes - Performance optimization is handled by the caller
    internal static extern NvrtcResult nvrtcGetProgramLog(IntPtr prog, [Out] StringBuilder log);
#pragma warning restore CA1838

    [DllImport(NVRTC_LIBRARY)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern IntPtr nvrtcGetErrorString(NvrtcResult result);

    // Helper Methods
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
}

/// <summary>
/// NVRTC result codes
/// </summary>
public enum NvrtcResult
{
    Success = 0,
    OutOfMemory = 1,
    ProgramCreationFailure = 2,
    InvalidInput = 3,
    InvalidProgram = 4,
    InvalidOption = 5,
    Compilation = 6,
    BuiltinOperationFailure = 7,
    NoLoweredNamesBeforeCompilation = 8,
    NoNameExpressionsAfterCompilation = 9,
    CompilationFailure = 10,
    InternalError = 11
}

/// <summary>
/// NVRTC compilation exception
/// </summary>
public class NvrtcException : Exception
{
    public NvrtcResult ResultCode { get; }

    public NvrtcException() : base()
    {
    }

    public NvrtcException(string message) : base(message)
    {
    }

    public NvrtcException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public NvrtcException(string message, NvrtcResult resultCode) : base(message)
    {
        ResultCode = resultCode;
    }

    public NvrtcException(string message, NvrtcResult resultCode, Exception innerException) : base(message, innerException)
    {
        ResultCode = resultCode;
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
    public static class Common
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
/// Compilation exception for kernel compilation failures
/// </summary>
public class KernelCompilationException : Exception
{
    public string? CompilerLog { get; }

    public KernelCompilationException(string message) : base(message) { }

    public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }

    public KernelCompilationException(string message, string? compilerLog) : base(message)
    {
        CompilerLog = compilerLog;
    }

    public KernelCompilationException(string message, string? compilerLog, Exception innerException) : base(message, innerException)
    {
        CompilerLog = compilerLog;
    }
    public KernelCompilationException()
    {
    }
}
