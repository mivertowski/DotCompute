// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using CudaGraphCaptureModeCanonical = DotCompute.Backends.CUDA.Types.CudaGraphCaptureMode;

namespace DotCompute.Backends.CUDA.Execution.Graph.Types
{
    /// <summary>
    /// Graph node parameters for kernel execution
    /// </summary>
    public class KernelNodeParams
    {
        public nint Function { get; set; }
        public GridDimensions GridDim { get; set; }

        public BlockDimensions BlockDim { get; set; }

        public uint SharedMemoryBytes { get; set; }
        public nint KernelParams { get; set; }
    }

    /// <summary>
    /// Grid dimensions for kernel launch
    /// </summary>
    public struct GridDimensions
    {
        public uint X { get; set; }
        public uint Y { get; set; }
        public uint Z { get; set; }

        public GridDimensions(uint x, uint y = 1, uint z = 1)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// Block dimensions for kernel launch
    /// </summary>
    public struct BlockDimensions : IEquatable<BlockDimensions>
    {
        public uint X { get; set; }
        public uint Y { get; set; }
        public uint Z { get; set; }

        public BlockDimensions(uint x, uint y = 1, uint z = 1)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override bool Equals(object? obj)
        {
            return obj is BlockDimensions other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static bool operator ==(BlockDimensions left, BlockDimensions right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BlockDimensions left, BlockDimensions right)
        {
            return !(left == right);
        }

        public bool Equals(BlockDimensions other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }
    }

    /// <summary>
    /// Memory copy node parameters
    /// </summary>
    public class MemcpyNodeParams
    {
        public nint Source { get; set; }
        public nint Destination { get; set; }
        public nuint ByteCount { get; set; }
        public MemcpyKind Kind { get; set; }
    }

    /// <summary>
    /// Memory copy kind
    /// </summary>
    public enum MemcpyKind
    {
        HostToHost = 0,
        HostToDevice = 1,
        DeviceToHost = 2,
        DeviceToDevice = 3,
        Default = 4
    }

    /// <summary>
    /// Memory set node parameters
    /// </summary>
    public class MemsetNodeParams
    {
        public nint Destination { get; set; }
        public uint Value { get; set; }
        public uint ElementSize { get; set; }
        public nuint Width { get; set; }
        public nuint Height { get; set; }
        public nuint Pitch { get; set; }
    }

    /// <summary>
    /// Host callback node parameters
    /// </summary>
    public class HostNodeParams
    {
        public Action<nint> Function { get; set; } = null!;
        public nint UserData { get; set; }
    }

    /// <summary>
    /// CUDA graph capture mode
    /// </summary>
    public enum CudaGraphCaptureMode : uint
    {
        Global = 0,
        ThreadLocal = 1,
        Relaxed = 2
    }

    /// <summary>
    /// Graph statistics for performance tracking
    /// </summary>
    public class GraphStatistics
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastExecutedAt { get; set; }
        public int NodeCount { get; set; }
        public int EdgeCount { get; set; }
        public int ExecutionCount { get; set; }
        public int InstantiationCount { get; set; }
        public int UpdateCount { get; set; }
        public int OptimizationCount { get; set; }
        public int ErrorCount { get; set; }
        public float TotalExecutionTimeMs { get; set; }
        public float LastExecutionTimeMs { get; set; }
        public string? ClonedFrom { get; set; }
        public CudaGraphCaptureModeCanonical? CaptureMode { get; set; }
    }

    /// <summary>
    /// CUDA device attribute enumeration
    /// </summary>
    public enum CudaDeviceAttribute : uint
    {
        MaxThreadsPerBlock = 1,
        MaxBlockDimX = 2,
        MaxBlockDimY = 3,
        MaxBlockDimZ = 4,
        MaxGridDimX = 5,
        MaxGridDimY = 6,
        MaxGridDimZ = 7,
        MaxSharedMemoryPerBlock = 8,
        TotalConstantMemory = 9,
        WarpSize = 10,
        MaxPitch = 11,
        MaxRegistersPerBlock = 12,
        ClockRate = 13,
        TextureAlignment = 14,
        GpuOverlap = 15,
        MultiProcessorCount = 16,
        KernelExecTimeout = 17,
        Integrated = 18,
        CanMapHostMemory = 19,
        ComputeMode = 20,
        MaxTexture1DWidth = 21,
        MaxTexture2DWidth = 22,
        MaxTexture2DHeight = 23,
        MaxTexture3DWidth = 24,
        MaxTexture3DHeight = 25,
        MaxTexture3DDepth = 26,
        MaxTexture2DLayeredWidth = 27,
        MaxTexture2DLayeredHeight = 28,
        MaxTexture2DLayeredLayers = 29,
        SurfaceAlignment = 30,
        ConcurrentKernels = 31,
        EccEnabled = 32,
        PciBusId = 33,
        PciDeviceId = 34,
        TccDriver = 35,
        MemoryClockRate = 36,
        GlobalMemoryBusWidth = 37,
        L2CacheSize = 38,
        MaxThreadsPerMultiProcessor = 39,
        AsyncEngineCount = 40,
        UnifiedAddressing = 41,
        MaxTexture1DLayeredWidth = 42,
        MaxTexture1DLayeredLayers = 43,
        CanTex2DGather = 44,
        MaxTexture2DGatherWidth = 45,
        MaxTexture2DGatherHeight = 46,
        MaxTexture3DWidthAlt = 47,
        MaxTexture3DHeightAlt = 48,
        MaxTexture3DDepthAlt = 49,
        PciDomainId = 50,
        TexturePitchAlignment = 51,
        MaxTextureCubemapWidth = 52,
        MaxTextureCubemapLayeredWidth = 53,
        MaxTextureCubemapLayeredLayers = 54,
        MaxSurface1DWidth = 55,
        MaxSurface2DWidth = 56,
        MaxSurface2DHeight = 57,
        MaxSurface3DWidth = 58,
        MaxSurface3DHeight = 59,
        MaxSurface3DDepth = 60,
        MaxSurface1DLayeredWidth = 61,
        MaxSurface1DLayeredLayers = 62,
        MaxSurface2DLayeredWidth = 63,
        MaxSurface2DLayeredHeight = 64,
        MaxSurface2DLayeredLayers = 65,
        MaxSurfaceCubemapWidth = 66,
        MaxSurfaceCubemapLayeredWidth = 67,
        MaxSurfaceCubemapLayeredLayers = 68,
        MaxTexture1DLinearWidth = 69,
        MaxTexture2DLinearWidth = 70,
        MaxTexture2DLinearHeight = 71,
        MaxTexture2DLinearPitch = 72,
        MaxTexture2DMipmappedWidth = 73,
        MaxTexture2DMipmappedHeight = 74,
        ComputeCapabilityMajor = 75,
        ComputeCapabilityMinor = 76,
        MaxTexture1DMipmappedWidth = 77,
        StreamPrioritiesSupported = 78,
        GlobalL1CacheSupported = 79,
        LocalL1CacheSupported = 80,
        MaxSharedMemoryPerMultiprocessor = 81,
        MaxRegistersPerMultiprocessor = 82,
        ManagedMemory = 83,
        MultiGpuBoard = 84,
        MultiGpuBoardGroupId = 85,
        HostNativeAtomicSupported = 86,
        SingleToDoublePrecisionPerfRatio = 87,
        PageableMemoryAccess = 88,
        ConcurrentManagedAccess = 89,
        ComputePreemptionSupported = 90,
        CanUseHostPointerForRegisteredMem = 91,
        CooperativeLaunch = 92,
        CooperativeMultiDeviceLaunch = 93,
        MaxSharedMemoryPerBlockOptin = 94,
        CanFlushRemoteWrites = 95,
        HostRegisterSupported = 96,
        PageableMemoryAccessUsesHostPageTables = 97,
        DirectManagedMemAccessFromHost = 98,
        MaxBlocksPerMultiprocessor = 99,
        MaxPersistingL2CacheSize = 100,
        MaxAccessPolicyWindowSize = 101,
        ReservedSharedMemoryPerBlock = 102,
        SparseCudaArraySupported = 103,
        HostRegisterReadOnlySupported = 104,
        TimelineSemaphoreInteropSupported = 105,
        MemoryPoolsSupported = 106,
        GpuDirectRdmaSupported = 107,
        GpuDirectRdmaFlushWritesOptions = 108,
        GpuDirectRdmaWritesOrdering = 109,
        MempoolSupportedHandleTypes = 110,
        DeferredMappingCudaArraySupported = 111,
        IpcEventSupported = 112,
        ClusterLaunch = 113,
        UnifiedFunctionPointers = 114,
        NumaConfig = 115,
        NumaId = 116,
        MpsEnabled = 117,
        HostNumaId = 118
    }

    /// <summary>
    /// CUDA cache configuration
    /// </summary>
    public enum CudaCacheConfig : uint
    {
        PreferNone = 0,
        PreferShared = 1,
        PreferCache = 2,
        PreferEqual = 3
    }

    /// <summary>
    /// CUDA shared memory configuration
    /// </summary>
    public enum CudaSharedMemConfig : uint
    {
        BankSizeDefault = 0,
        BankSizeFourByte = 1,
        BankSizeEightByte = 2
    }

    /// <summary>
    /// CUDA limit types
    /// </summary>
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
}