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
        /// <summary>
        /// Gets or sets the function.
        /// </summary>
        /// <value>The function.</value>
        public nint Function { get; set; }
        /// <summary>
        /// Gets or sets the grid dim.
        /// </summary>
        /// <value>The grid dim.</value>
        public GridDimensions GridDim { get; set; }
        /// <summary>
        /// Gets or sets the block dim.
        /// </summary>
        /// <value>The block dim.</value>

        public BlockDimensions BlockDim { get; set; }
        /// <summary>
        /// Gets or sets the shared memory bytes.
        /// </summary>
        /// <value>The shared memory bytes.</value>

        public uint SharedMemoryBytes { get; set; }
        /// <summary>
        /// Gets or sets the kernel params.
        /// </summary>
        /// <value>The kernel params.</value>
        public nint KernelParams { get; set; }
    }

    /// <summary>
    /// Grid dimensions for kernel launch
    /// </summary>
    public struct GridDimensions(uint x, uint y = 1, uint z = 1) : IEquatable<GridDimensions>
    {
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <value>The x.</value>
        public uint X { get; set; } = x;
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <value>The y.</value>
        public uint Y { get; set; } = y;
        /// <summary>
        /// Gets or sets the z.
        /// </summary>
        /// <value>The z.</value>
        public uint Z { get; set; } = z;

        public override bool Equals(object? obj) => obj is GridDimensions other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public static bool operator ==(GridDimensions left, GridDimensions right) => left.Equals(right);

        public static bool operator !=(GridDimensions left, GridDimensions right) => !(left == right);

        public bool Equals(GridDimensions other) => X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <summary>
    /// Block dimensions for kernel launch
    /// </summary>
    public struct BlockDimensions(uint x, uint y = 1, uint z = 1) : IEquatable<BlockDimensions>
    {
        /// <summary>
        /// Gets or sets the x.
        /// </summary>
        /// <value>The x.</value>
        public uint X { get; set; } = x;
        /// <summary>
        /// Gets or sets the y.
        /// </summary>
        /// <value>The y.</value>
        public uint Y { get; set; } = y;
        /// <summary>
        /// Gets or sets the z.
        /// </summary>
        /// <value>The z.</value>
        public uint Z { get; set; } = z;
        /// <summary>
        /// Determines equals.
        /// </summary>
        /// <param name="obj">The obj.</param>
        /// <returns>The result of the operation.</returns>

        public override bool Equals(object? obj) => obj is BlockDimensions other && Equals(other);
        /// <summary>
        /// Gets the hash code.
        /// </summary>
        /// <returns>The hash code.</returns>

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        /// <summary>
        /// Implements the equality operator to determine whether two values are equal.
        /// </summary>

        public static bool operator ==(BlockDimensions left, BlockDimensions right) => left.Equals(right);
        /// <summary>
        /// Implements the inequality operator to determine whether two values are not equal.
        /// </summary>

        public static bool operator !=(BlockDimensions left, BlockDimensions right) => !(left == right);
        /// <summary>
        /// Determines equals.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns>The result of the operation.</returns>

        public bool Equals(BlockDimensions other) => X == other.X && Y == other.Y && Z == other.Z;
    }

    /// <summary>
    /// Memory copy node parameters
    /// </summary>
    public class MemcpyNodeParams
    {
        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        /// <value>The source.</value>
        public nint Source { get; set; }
        /// <summary>
        /// Gets or sets the destination.
        /// </summary>
        /// <value>The destination.</value>
        public nint Destination { get; set; }
        /// <summary>
        /// Gets or sets the byte count.
        /// </summary>
        /// <value>The byte count.</value>
        public nuint ByteCount { get; set; }
        /// <summary>
        /// Gets or sets the kind.
        /// </summary>
        /// <value>The kind.</value>
        public MemcpyKind Kind { get; set; }
    }
    /// <summary>
    /// An memcpy kind enumeration.
    /// </summary>

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
        /// <summary>
        /// Gets or sets the destination.
        /// </summary>
        /// <value>The destination.</value>
        public nint Destination { get; set; }
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        public uint Value { get; set; }
        /// <summary>
        /// Gets or sets the element size.
        /// </summary>
        /// <value>The element size.</value>
        public uint ElementSize { get; set; }
        /// <summary>
        /// Gets or sets the width.
        /// </summary>
        /// <value>The width.</value>
        public nuint Width { get; set; }
        /// <summary>
        /// Gets or sets the height.
        /// </summary>
        /// <value>The height.</value>
        public nuint Height { get; set; }
        /// <summary>
        /// Gets or sets the pitch.
        /// </summary>
        /// <value>The pitch.</value>
        public nuint Pitch { get; set; }
    }

    /// <summary>
    /// Host callback node parameters
    /// </summary>
    public class HostNodeParams
    {
        /// <summary>
        /// Gets or sets the function.
        /// </summary>
        /// <value>The function.</value>
        public Action<nint> Function { get; set; } = null!;
        /// <summary>
        /// Gets or sets the user data.
        /// </summary>
        /// <value>The user data.</value>
        public nint UserData { get; set; }
    }
    /// <summary>
    /// An cuda graph capture mode enumeration.
    /// </summary>

    /// <summary>
    /// CUDA graph capture mode
    /// </summary>
#pragma warning disable CA1028 // Enum Storage should be Int32

    public enum CudaGraphCaptureMode : uint
#pragma warning restore CA1028 // Enum Storage should be Int32

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
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public DateTime CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets the last executed at.
        /// </summary>
        /// <value>The last executed at.</value>
        public DateTime? LastExecutedAt { get; set; }
        /// <summary>
        /// Gets or sets the node count.
        /// </summary>
        /// <value>The node count.</value>
        public int NodeCount { get; set; }
        /// <summary>
        /// Gets or sets the edge count.
        /// </summary>
        /// <value>The edge count.</value>
        public int EdgeCount { get; set; }
        /// <summary>
        /// Gets or sets the execution count.
        /// </summary>
        /// <value>The execution count.</value>
        public int ExecutionCount { get; set; }
        /// <summary>
        /// Gets or sets the instantiation count.
        /// </summary>
        /// <value>The instantiation count.</value>
        public int InstantiationCount { get; set; }
        /// <summary>
        /// Gets or sets the update count.
        /// </summary>
        /// <value>The update count.</value>
        public int UpdateCount { get; set; }
        /// <summary>
        /// Gets or sets the optimization count.
        /// </summary>
        /// <value>The optimization count.</value>
        public int OptimizationCount { get; set; }
        /// <summary>
        /// Gets or sets the error count.
        /// </summary>
        /// <value>The error count.</value>
        public int ErrorCount { get; set; }
        /// <summary>
        /// Gets or sets the total execution time ms.
        /// </summary>
        /// <value>The total execution time ms.</value>
        public float TotalExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the last execution time ms.
        /// </summary>
        /// <value>The last execution time ms.</value>
        public float LastExecutionTimeMs { get; set; }
        /// <summary>
        /// Gets or sets the cloned from.
        /// </summary>
        /// <value>The cloned from.</value>
        public string? ClonedFrom { get; set; }
        /// <summary>
        /// Gets or sets the capture mode.
        /// </summary>
        /// <value>The capture mode.</value>
        public CudaGraphCaptureModeCanonical? CaptureMode { get; set; }
    }
    /// <summary>
    /// An cuda device attribute enumeration.
    /// </summary>

    /// <summary>
    /// CUDA device attribute enumeration
    /// </summary>
#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1028 // Enum Storage should be Int32
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
    public enum CudaDeviceAttribute : uint
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
#pragma warning restore CA1028 // Enum Storage should be Int32
#pragma warning restore CA1008 // Enums should have zero value



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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1700:Do not name enum values 'Reserved'",
            Justification = "CUDA API constant - matches official NVIDIA SDK naming")]
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
    /// An cuda cache config enumeration.
    /// </summary>

    /// <summary>
    /// CUDA cache configuration
    /// </summary>
#pragma warning disable CA1028 // Enum Storage should be Int32

    public enum CudaCacheConfig : uint
#pragma warning restore CA1028 // Enum Storage should be Int32

    {
        PreferNone = 0,
        PreferShared = 1,
        PreferCache = 2,
        PreferEqual = 3
    }
    /// <summary>
    /// An cuda shared mem config enumeration.
    /// </summary>

    /// <summary>
    /// CUDA shared memory configuration
    /// </summary>
#pragma warning disable CA1028 // Enum Storage should be Int32

    public enum CudaSharedMemConfig : uint
#pragma warning restore CA1028 // Enum Storage should be Int32

    {
        BankSizeDefault = 0,
        BankSizeFourByte = 1,
        BankSizeEightByte = 2
    }
    /// <summary>
    /// An cuda limit enumeration.
    /// </summary>

    /// <summary>
    /// CUDA limit types
    /// </summary>
#pragma warning disable CA1028 // Enum Storage should be Int32

    public enum CudaLimit : uint
#pragma warning restore CA1028 // Enum Storage should be Int32

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