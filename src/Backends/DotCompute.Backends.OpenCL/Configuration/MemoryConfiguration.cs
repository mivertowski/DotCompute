// Copyright (c) DotCompute. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;

namespace DotCompute.Backends.OpenCL.Configuration;

/// <summary>
/// Configuration for OpenCL memory management, buffer pooling, and peer-to-peer transfers.
/// </summary>
/// <remarks>
/// <para>
/// This configuration controls memory allocation strategies, buffer pooling behavior,
/// and advanced features like pinned memory and peer-to-peer (P2P) transfers across
/// multiple OpenCL devices.
/// </para>
/// <para>
/// Memory management is critical for GPU compute performance:
/// <list type="bullet">
/// <item><description>Buffer pooling reduces allocation overhead (90%+ reduction possible)</description></item>
/// <item><description>Proper alignment ensures coalesced memory access</description></item>
/// <item><description>Pinned memory accelerates CPU-GPU transfers</description></item>
/// <item><description>P2P transfers eliminate host memory copies between GPUs</description></item>
/// </list>
/// </para>
/// </remarks>
[DisplayName("OpenCL Memory Configuration")]
public sealed class MemoryConfiguration
{
    /// <summary>
    /// Gets a value indicating whether to enable memory buffer pooling.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable pooling; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Buffer pooling reuses allocated memory to avoid clCreateBuffer overhead.
    /// This can reduce allocation latency by 90%+ and minimize memory fragmentation.
    /// Disable only for debugging or when precise memory tracking is required.
    /// </remarks>
    public bool EnableBufferPooling { get; init; } = true;

    /// <summary>
    /// Gets the minimum size threshold for small memory buffers (in bytes).
    /// </summary>
    /// <value>
    /// The small buffer threshold. Must be greater than 0. Default is 4 KB (4096 bytes).
    /// </value>
    /// <remarks>
    /// Buffers smaller than this size are allocated from the small buffer pool.
    /// Common for kernel parameters, indices, and small working sets.
    /// Typical range: 1 KB - 64 KB.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 256.
    /// </exception>
    public int SmallBufferThreshold
    {
        get => _smallBufferThreshold;
        init
        {
            if (value < 256)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(SmallBufferThreshold),
                    value,
                    "Small buffer threshold must be at least 256 bytes.");
            }
            _smallBufferThreshold = value;
        }
    }
    private readonly int _smallBufferThreshold = 4 * 1024; // 4 KB

    /// <summary>
    /// Gets the size threshold for medium memory buffers (in bytes).
    /// </summary>
    /// <value>
    /// The medium buffer threshold. Must be greater than SmallBufferThreshold. Default is 64 KB (65536 bytes).
    /// </value>
    /// <remarks>
    /// Buffers between small and medium thresholds use the medium buffer pool.
    /// Common for intermediate computations and temporary storage.
    /// Typical range: 32 KB - 1 MB.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to SmallBufferThreshold.
    /// </exception>
    public int MediumBufferThreshold
    {
        get => _mediumBufferThreshold;
        init
        {
            if (value <= SmallBufferThreshold)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MediumBufferThreshold),
                    value,
                    $"Medium buffer threshold must be greater than SmallBufferThreshold ({SmallBufferThreshold}).");
            }
            _mediumBufferThreshold = value;
        }
    }
    private readonly int _mediumBufferThreshold = 64 * 1024; // 64 KB

    /// <summary>
    /// Gets the size threshold for large memory buffers (in bytes).
    /// </summary>
    /// <value>
    /// The large buffer threshold. Must be greater than MediumBufferThreshold. Default is 1 MB (1048576 bytes).
    /// </value>
    /// <remarks>
    /// Buffers between medium and large thresholds use the large buffer pool.
    /// Buffers larger than this threshold are not pooled to avoid excessive memory consumption.
    /// Typical range: 512 KB - 16 MB.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to MediumBufferThreshold.
    /// </exception>
    public int LargeBufferThreshold
    {
        get => _largeBufferThreshold;
        init
        {
            if (value <= MediumBufferThreshold)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(LargeBufferThreshold),
                    value,
                    $"Large buffer threshold must be greater than MediumBufferThreshold ({MediumBufferThreshold}).");
            }
            _largeBufferThreshold = value;
        }
    }
    private readonly int _largeBufferThreshold = 1024 * 1024; // 1 MB

    /// <summary>
    /// Gets the maximum total size of pooled memory (in bytes).
    /// </summary>
    /// <value>
    /// The maximum pool size. Must be greater than 0. Default is 1 GB (1073741824 bytes).
    /// </value>
    /// <remarks>
    /// This limits the total amount of memory that can be pooled to prevent
    /// over-commitment of device memory. When this limit is reached, older
    /// buffers are released to make room for new allocations.
    /// Typical values: 25-50% of available device memory.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1 MB.
    /// </exception>
    public long MaximumPoolSize
    {
        get => _maximumPoolSize;
        init
        {
            if (value < 1024 * 1024)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaximumPoolSize),
                    value,
                    "Maximum pool size must be at least 1 MB.");
            }
            _maximumPoolSize = value;
        }
    }
    private readonly long _maximumPoolSize = 1024L * 1024 * 1024; // 1 GB

    /// <summary>
    /// Gets the memory alignment for buffer allocations (in bytes).
    /// </summary>
    /// <value>
    /// The alignment. Must be a power of 2. Default is 128 bytes.
    /// </value>
    /// <remarks>
    /// Proper alignment ensures coalesced memory access on GPUs:
    /// - NVIDIA: 128-byte alignment for coalescing
    /// - AMD: 256-byte alignment for optimal access
    /// - Intel: 64-byte alignment (cache line size)
    /// This value affects the padding added to buffer allocations.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is not a power of 2 or less than 16.
    /// </exception>
    public int BufferAlignment
    {
        get => _bufferAlignment;
        init
        {
            if (value < 16 || (value & (value - 1)) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(BufferAlignment),
                    value,
                    "Buffer alignment must be at least 16 and a power of 2.");
            }
            _bufferAlignment = value;
        }
    }
    private readonly int _bufferAlignment = 128; // 128 bytes

    /// <summary>
    /// Gets a value indicating whether to use pinned (page-locked) host memory.
    /// </summary>
    /// <value>
    /// <c>true</c> to use pinned memory; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Pinned memory (CL_MEM_ALLOC_HOST_PTR) is page-locked and can be transferred
    /// to/from the device faster than regular memory. However, it's a limited resource
    /// and excessive pinning can degrade system performance. Use for frequently
    /// transferred buffers in performance-critical paths.
    /// </remarks>
    public bool UsePinnedMemory { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to enable peer-to-peer (P2P) transfers.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable P2P; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// P2P transfers allow direct GPU-to-GPU memory copies without going through
    /// host memory. This requires:
    /// - Multiple GPUs in the same system
    /// - Driver support (NVIDIA: NVLink/PCIe P2P, AMD: Infinity Fabric/PCIe P2P)
    /// - OpenCL extension: cl_khr_p2p_buffer
    /// When unavailable, transfers automatically fall back to host-mediated copies.
    /// </remarks>
    public bool EnablePeerToPeerTransfers { get; init; } = true;

    /// <summary>
    /// Gets the maximum size for asynchronous memory transfers (in bytes).
    /// </summary>
    /// <value>
    /// The async transfer threshold. Must be greater than 0. Default is 64 KB (65536 bytes).
    /// </value>
    /// <remarks>
    /// Transfers smaller than this size are performed synchronously to avoid
    /// async overhead. Larger transfers use asynchronous operations with events
    /// for better pipelining and CPU-GPU overlap.
    /// Typical range: 32 KB - 256 KB.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 1.
    /// </exception>
    public int AsyncTransferThreshold
    {
        get => _asyncTransferThreshold;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(AsyncTransferThreshold),
                    value,
                    "Async transfer threshold must be at least 1 byte.");
            }
            _asyncTransferThreshold = value;
        }
    }
    private readonly int _asyncTransferThreshold = 64 * 1024; // 64 KB

    /// <summary>
    /// Gets the idle timeout for pooled buffers.
    /// </summary>
    /// <value>
    /// The idle timeout. Must be greater than TimeSpan.Zero. Default is 10 minutes.
    /// </value>
    /// <remarks>
    /// Buffers that have been idle in the pool longer than this duration may be
    /// released to free device memory. This prevents memory leaks from pooling
    /// while maintaining performance for active workloads.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to TimeSpan.Zero.
    /// </exception>
    public TimeSpan BufferIdleTimeout
    {
        get => _bufferIdleTimeout;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(BufferIdleTimeout),
                    value,
                    "Buffer idle timeout must be greater than zero.");
            }
            _bufferIdleTimeout = value;
        }
    }
    private readonly TimeSpan _bufferIdleTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets a value indicating whether to enable zero-copy buffers where supported.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable zero-copy; otherwise, <c>false</c>. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Zero-copy buffers (CL_MEM_USE_HOST_PTR) allow the device to directly access
    /// host memory without explicit copies. This is beneficial for:
    /// - Integrated GPUs with shared memory (AMD APUs, Intel integrated graphics)
    /// - Buffers that are infrequently accessed by the device
    /// - Write-only or read-only patterns
    /// Not recommended for discrete GPUs where PCIe bandwidth limits performance.
    /// </remarks>
    public bool EnableZeroCopy { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to enable memory usage statistics collection.
    /// </summary>
    /// <value>
    /// <c>true</c> to collect statistics; otherwise, <c>false</c>. Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// When enabled, tracks:
    /// - Total allocated memory
    /// - Pool hit/miss rates
    /// - Buffer utilization
    /// - Memory fragmentation
    /// This adds minimal overhead (~1%) but provides valuable monitoring data.
    /// Enable for development and performance tuning; may disable in production.
    /// </remarks>
    public bool EnableStatistics { get; init; }

    /// <summary>
    /// Creates a default memory configuration instance.
    /// </summary>
    /// <returns>
    /// A new <see cref="MemoryConfiguration"/> instance with default settings.
    /// </returns>
    public static MemoryConfiguration Default => new();

    /// <summary>
    /// Creates a memory configuration optimized for large buffer workloads.
    /// </summary>
    /// <returns>
    /// A new <see cref="MemoryConfiguration"/> instance optimized for large buffers.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Uses higher buffer size thresholds
    /// - Larger maximum pool size (2 GB)
    /// - Longer idle timeouts to keep buffers warm
    /// - Aggressive async transfer threshold
    /// - Suitable for workloads with many large allocations (ML training, simulation)
    /// </remarks>
    public static MemoryConfiguration LargeBuffers => new()
    {
        EnableBufferPooling = true,
        SmallBufferThreshold = 16 * 1024,        // 16 KB
        MediumBufferThreshold = 256 * 1024,      // 256 KB
        LargeBufferThreshold = 4 * 1024 * 1024,  // 4 MB
        MaximumPoolSize = 2L * 1024 * 1024 * 1024, // 2 GB
        BufferAlignment = 256,                    // 256 bytes (AMD optimal)
        UsePinnedMemory = true,
        EnablePeerToPeerTransfers = true,
        AsyncTransferThreshold = 256 * 1024,     // 256 KB
        BufferIdleTimeout = TimeSpan.FromMinutes(30),
        EnableZeroCopy = false,                  // Prefer explicit copies for large data
        EnableStatistics = false
    };

    /// <summary>
    /// Creates a memory configuration optimized for development and debugging.
    /// </summary>
    /// <returns>
    /// A new <see cref="MemoryConfiguration"/> instance optimized for development.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Smaller pool sizes to track allocations
    /// - Statistics enabled for monitoring
    /// - Shorter idle timeouts to detect leaks quickly
    /// - Disabled optimizations for easier debugging
    /// - More conservative settings overall
    /// </remarks>
    public static MemoryConfiguration Development => new()
    {
        EnableBufferPooling = true,
        SmallBufferThreshold = 4 * 1024,         // 4 KB
        MediumBufferThreshold = 32 * 1024,       // 32 KB
        LargeBufferThreshold = 512 * 1024,       // 512 KB
        MaximumPoolSize = 256L * 1024 * 1024,    // 256 MB
        BufferAlignment = 64,                     // 64 bytes (cache line)
        UsePinnedMemory = false,                  // Easier to debug regular memory
        EnablePeerToPeerTransfers = false,        // Simpler transfer paths
        AsyncTransferThreshold = 32 * 1024,      // 32 KB
        BufferIdleTimeout = TimeSpan.FromMinutes(2),
        EnableZeroCopy = false,
        EnableStatistics = true
    };

    /// <summary>
    /// Creates a memory configuration optimized for low-latency workloads.
    /// </summary>
    /// <returns>
    /// A new <see cref="MemoryConfiguration"/> instance optimized for low latency.
    /// </returns>
    /// <remarks>
    /// This configuration:
    /// - Aggressive pooling with warm caches
    /// - Pinned memory for fast transfers
    /// - Small async threshold for maximum overlap
    /// - Zero-copy for integrated GPUs
    /// - Suitable for interactive applications and streaming workloads
    /// </remarks>
    public static MemoryConfiguration LowLatency => new()
    {
        EnableBufferPooling = true,
        SmallBufferThreshold = 2 * 1024,         // 2 KB
        MediumBufferThreshold = 32 * 1024,       // 32 KB
        LargeBufferThreshold = 256 * 1024,       // 256 KB
        MaximumPoolSize = 512L * 1024 * 1024,    // 512 MB
        BufferAlignment = 128,                    // 128 bytes (NVIDIA optimal)
        UsePinnedMemory = true,
        EnablePeerToPeerTransfers = true,
        AsyncTransferThreshold = 16 * 1024,      // 16 KB (aggressive async)
        BufferIdleTimeout = TimeSpan.FromMinutes(15),
        EnableZeroCopy = true,                   // Good for integrated GPUs
        EnableStatistics = false
    };
}
