// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.CUDA.Types
{
    /// <summary>
    /// Data type enumeration for CUDA operations.
    /// </summary>
    public enum DataType
    {
        Float32,
        Float64,
        Float16,
        Int32,
        Int64,
        Int16,
        Int8,
        UInt32,
        UInt64,
        UInt16,
        UInt8,
        Boolean,
        Complex64,
        Complex128,
        BFloat16
    }


    /// <summary>
    /// Extended memory statistics for CUDA memory management
    /// </summary>
    public sealed class CudaMemoryStatisticsExtended
    {
        // Base statistics (from MemoryStatistics equivalent)
        public long TotalMemoryBytes { get; set; }
        public long UsedMemoryBytes { get; set; }
        public long AvailableMemoryBytes { get; set; }
        public int AllocationCount { get; set; }
        public int DeallocationCount { get; set; }
        public long PeakMemoryUsageBytes { get; set; }
        
        // CUDA-specific extensions
        public long PinnedMemoryBytes { get; set; }
        public long UnifiedMemoryBytes { get; set; }
        public long PooledMemoryBytes { get; set; }
        public int PoolHits { get; set; }
        public int PoolMisses { get; set; }
        public double PoolEfficiency => PoolHits + PoolMisses > 0 ?
            (double)PoolHits / (PoolHits + PoolMisses) : 0.0;
        public long HostToDeviceTransfers { get; set; }
        public long DeviceToHostTransfers { get; set; }
        public long TotalTransferredBytes { get; set; }
        public double AverageBandwidth { get; set; } // MB/s
        public double MemoryFragmentation { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// CUDA memory allocation flags and options
    /// </summary>
    [Flags]
    public enum CudaMemoryFlags : uint
    {
        None = 0,
        DeviceLocal = 1,
        HostVisible = 2,
        HostCoherent = 4,
        HostCached = 8,
        LazilyAllocated = 16,
        Protected = 32,
        Unified = 64,
        Pinned = 128
    }

    /// <summary>
    /// CUDA memory type enumeration
    /// </summary>
    public enum CudaMemoryType
    {
        Device,
        Host,
        Unified,
        Pinned,
        Mapped
    }

    /// <summary>
    /// CUDA memory alignment requirements
    /// </summary>
    public static class CudaMemoryAlignment
    {
        public const int MinAlignment = 256; // 256 bytes minimum for coalesced access
        public const int OptimalAlignment = 512; // 512 bytes optimal for RTX 2000 series
        public const int TextureAlignment = 512; // Texture memory alignment
        public const int SurfaceAlignment = 512; // Surface memory alignment

        public static long AlignUp(long size, int alignment = OptimalAlignment) => ((size + alignment - 1) / alignment) * alignment;

        public static bool IsAligned(long size, int alignment = OptimalAlignment) => size % alignment == 0;
    }

    /// <summary>
    /// Kernel arguments for CUDA execution
    /// </summary>
    public sealed class CudaKernelArguments
    {
        private readonly List<object> _arguments = [];

        public CudaKernelArguments() { }

        public CudaKernelArguments(params object[] arguments)
        {
            _arguments.AddRange(arguments);
        }

        public void Add<T>(T argument) where T : unmanaged => _arguments.Add(argument);

        public void AddBuffer(IntPtr devicePointer) => _arguments.Add(devicePointer);

        public object[] ToArray() => [.. _arguments];

        public int Count => _arguments.Count;
    }


    /// <summary>
    /// Validation result for CUDA operations
    /// </summary>
    public sealed class UnifiedValidationResult
    {
        public bool IsValid { get; init; }
        public string? ErrorMessage { get; init; }
        public List<string> Warnings { get; init; } = [];

        public static UnifiedValidationResult Success() => new() { IsValid = true };
        public static UnifiedValidationResult Success(string message) => new() { IsValid = true, ErrorMessage = message };
        public static UnifiedValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
        public static UnifiedValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
        public static UnifiedValidationResult SuccessWithWarnings(List<string> warnings) => new() { IsValid = true, Warnings = warnings };
        public static UnifiedValidationResult SuccessWithWarnings(string[] warnings) => new() { IsValid = true, Warnings = new List<string>(warnings) };
    }

    /// <summary>
    /// Cache statistics for CUDA operations
    /// </summary>
    public sealed class CacheStatistics
    {
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public int TotalRequests => HitCount + MissCount;
        public int TotalEntries { get; set; }
        public long TotalSizeBytes { get; set; }
        public double HitRate { get; set; }

        public double AverageAccessCount { get; set; }
        public DateTime? OldestEntryTime { get; set; } = DateTime.UtcNow;
        public DateTime? NewestEntryTime { get; set; } = DateTime.UtcNow;
        public long CacheSizeBytes { get; set; }
        public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents cached kernel compilation data.
    /// </summary>
    public sealed class CachedKernel
    {
        /// <summary>
        /// Gets or sets the kernel name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compiled kernel binary.
        /// </summary>
        public byte[] Binary { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the compilation timestamp.
        /// </summary>
        public DateTime CompilationTime { get; set; }

        /// <summary>
        /// Gets or sets the source code hash.
        /// </summary>
        public string SourceHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compilation options hash.
        /// </summary>
        public string OptionsHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents CUDA device information.
    /// </summary>
    public sealed class DeviceInfo
    {
        /// <summary>
        /// Gets or sets the device index.
        /// </summary>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability (major.minor).
        /// </summary>
        public (int Major, int Minor) ComputeCapability { get; set; }

        /// <summary>
        /// Gets or sets the total global memory in bytes.
        /// </summary>
        public long GlobalMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets the shared memory per block in bytes.
        /// </summary>
        public int SharedMemoryPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the number of multiprocessors.
        /// </summary>
        public int MultiprocessorCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum threads per block.
        /// </summary>
        public int MaxThreadsPerBlock { get; set; }

        /// <summary>
        /// Gets or sets the warp size.
        /// </summary>
        public int WarpSize { get; set; } = 32;

        /// <summary>
        /// Gets or sets whether the device supports unified memory.
        /// </summary>
        public bool SupportsUnifiedMemory { get; set; }
    }

    /// <summary>
    /// CUDA memory manager for device memory allocation and management.
    /// </summary>
    public sealed class CudaMemoryManager
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryManager"/> class.
        /// </summary>
        public CudaMemoryManager(CudaContext context, Microsoft.Extensions.Logging.ILogger logger)
        {
            Context = context?.Handle ?? nint.Zero;
            DeviceIndex = context?.DeviceId ?? 0;
            _logger = logger;
            
            // Initialize with reasonable defaults
            TotalMemory = 8L * 1024 * 1024 * 1024; // 8GB default
            MaxAllocationSize = TotalMemory / 2;
        }

        /// <summary>
        /// Gets or sets the associated CUDA context.
        /// </summary>
        public nint Context { get; set; }

        /// <summary>
        /// Gets or sets the device index.
        /// </summary>
        public int DeviceIndex { get; set; }

        /// <summary>
        /// Gets or sets the total allocated memory in bytes.
        /// </summary>
        public long TotalAllocatedBytes { get; set; }

        /// <summary>
        /// Gets the total memory available on the device.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets the currently used memory.
        /// </summary>
        public long UsedMemory { get; set; }

        /// <summary>
        /// Gets the maximum allocation size.
        /// </summary>
        public long MaxAllocationSize { get; set; }
    }

    /// <summary>
    /// Exception thrown during kernel compilation
    /// </summary>
    public sealed class KernelCompilationException : Exception
    {
        public string? CompilerOutput { get; }
        public string? SourceCode { get; }
        public int? ErrorCode { get; }

        public KernelCompilationException() : base() { }

        public KernelCompilationException(string message) : base(message) { }

        public KernelCompilationException(string message, Exception innerException) : base(message, innerException) { }

        public KernelCompilationException(string message, string? compilerOutput, string? sourceCode = null, int? errorCode = null)
            : base(message)
        {
            CompilerOutput = compilerOutput;
            SourceCode = sourceCode;
            ErrorCode = errorCode;
        }
    }
}
