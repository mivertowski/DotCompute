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
        /// Gets or sets the device ID.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Gets or sets the device name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compute capability (major.minor).
        /// </summary>
        public (int Major, int Minor) ComputeCapability { get; set; }

        /// <summary>
        /// Gets or sets the architecture generation name (e.g., "Ada Lovelace", "Ampere", "Turing").
        /// </summary>
        public string ArchitectureGeneration { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this device is an RTX 2000 Ada generation GPU.
        /// </summary>
        public bool IsRTX2000Ada { get; set; }

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
        /// Gets or sets the number of streaming multiprocessors.
        /// </summary>
        public int StreamingMultiprocessors { get; set; }

        /// <summary>
        /// Gets or sets the estimated number of CUDA cores based on the architecture and SM count.
        /// </summary>
        public int EstimatedCudaCores { get; set; }

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

        /// <summary>
        /// Gets or sets the memory bandwidth in GB/s.
        /// </summary>
        public double MemoryBandwidthGBps { get; set; }

        /// <summary>
        /// Gets or sets the L2 cache size in bytes.
        /// </summary>
        public int L2CacheSize { get; set; }

        /// <summary>
        /// Gets or sets the GPU core clock rate in kHz.
        /// </summary>
        public int ClockRate { get; set; }

        /// <summary>
        /// Gets or sets the memory clock rate in kHz.
        /// </summary>
        public int MemoryClockRate { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports unified addressing.
        /// </summary>
        public bool SupportsUnifiedAddressing { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports managed memory.
        /// </summary>
        public bool SupportsManagedMemory { get; set; }

        /// <summary>
        /// Gets or sets whether the device supports concurrent kernel execution.
        /// </summary>
        public bool SupportsConcurrentKernels { get; set; }

        /// <summary>
        /// Gets or sets whether ECC (Error-Correcting Code) memory is enabled on the device.
        /// </summary>
        public bool IsECCEnabled { get; set; }

        /// <summary>
        /// Gets or sets the total memory available on the device in bytes.
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Gets or sets the currently available memory on the device in bytes.
        /// </summary>
        public long AvailableMemory { get; set; }
    }

    /// <summary>
    /// CUDA memory manager for device memory allocation and management.
    /// </summary>
    public sealed class CudaMemoryTracker : IAsyncDisposable, IDisposable
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<nint, MemoryAllocationInfo> _allocations;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaMemoryTracker"/> class.
        /// </summary>
        public CudaMemoryTracker(CudaContext context, Microsoft.Extensions.Logging.ILogger logger)
        {
            Context = context?.Handle ?? nint.Zero;
            DeviceIndex = context?.DeviceId ?? 0;
            _logger = logger;
            _allocations = new System.Collections.Concurrent.ConcurrentDictionary<nint, MemoryAllocationInfo>();
            
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

        /// <summary>
        /// Resets the memory manager and clears all memory pools.
        /// </summary>
        public void Reset()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CudaMemoryTracker));
            }

            try
            {
                _logger?.LogDebug("Resetting CUDA memory manager for device {DeviceIndex}", DeviceIndex);

                // Free all tracked allocations
                var allocationCount = 0;
                foreach (var kvp in _allocations)
                {
                    try
                    {
                        if (kvp.Key != nint.Zero)
                        {
                            // Free device memory
                            var result = Native.CudaRuntime.cudaFree(kvp.Key);
                            if (result != Native.CudaError.Success)
                            {
                                _logger?.LogWarning("Failed to free CUDA memory during reset: {Error}", result);
                            }
                            allocationCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.LogError(ex, "Error freeing allocation during reset");
                    }
                }

                // Clear the allocations dictionary
                _allocations.Clear();

                // Reset counters
                TotalAllocatedBytes = 0;
                UsedMemory = 0;

                _logger?.LogInformation("Reset complete. Freed {AllocationCount} allocations for device {DeviceIndex}", 
                    allocationCount, DeviceIndex);
            }
            catch (System.Exception ex)
            {
                _logger?.LogError(ex, "Error during CUDA memory manager reset for device {DeviceIndex}", DeviceIndex);
                throw;
            }
        }

        /// <summary>
        /// Asynchronously disposes the memory manager and all allocated resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                try
                {
                    _logger?.LogDebug("Starting async disposal of CUDA memory manager for device {DeviceIndex}", DeviceIndex);

                    // Perform cleanup operations asynchronously
                    await Task.Run(() =>
                    {
                        // Reset and free all memory
                        Reset();
                    }).ConfigureAwait(false);

                    _disposed = true;
                    _logger?.LogDebug("Async disposal completed for CUDA memory manager device {DeviceIndex}", DeviceIndex);
                }
                catch (System.Exception ex)
                {
                    _logger?.LogError(ex, "Error during async disposal of CUDA memory manager device {DeviceIndex}", DeviceIndex);
                    // Still mark as disposed to prevent further operations
                    _disposed = true;
                    throw;
                }
            }
        }

        /// <summary>
        /// Synchronously disposes the memory manager and all allocated resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    Reset();
                    _disposed = true;
                }
                catch (System.Exception ex)
                {
                    _logger?.LogError(ex, "Error during disposal of CUDA memory manager device {DeviceIndex}", DeviceIndex);
                    _disposed = true;
                    throw;
                }
            }
        }

        /// <summary>
        /// Tracks a memory allocation.
        /// </summary>
        internal void TrackAllocation(nint devicePtr, long sizeInBytes)
        {
            if (devicePtr != nint.Zero)
            {
                var info = new MemoryAllocationInfo
                {
                    DevicePointer = devicePtr,
                    SizeInBytes = sizeInBytes,
                    AllocatedAt = DateTime.UtcNow
                };
                
                _allocations.TryAdd(devicePtr, info);
                TotalAllocatedBytes += sizeInBytes;
                UsedMemory += sizeInBytes;
            }
        }

        /// <summary>
        /// Removes tracking for a memory allocation.
        /// </summary>
        internal void UntrackAllocation(nint devicePtr)
        {
            if (_allocations.TryRemove(devicePtr, out var info))
            {
                TotalAllocatedBytes -= info.SizeInBytes;
                UsedMemory -= info.SizeInBytes;
            }
        }

        /// <summary>
        /// Information about a memory allocation.
        /// </summary>
        private sealed class MemoryAllocationInfo
        {
            public nint DevicePointer { get; init; }
            public long SizeInBytes { get; init; }
            public DateTime AllocatedAt { get; init; }
        }
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
