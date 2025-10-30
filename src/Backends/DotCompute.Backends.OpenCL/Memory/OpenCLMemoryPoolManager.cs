// Copyright (c) 2025 DotCompute Contributors
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Backends.OpenCL.Configuration;
using DotCompute.Backends.OpenCL.Infrastructure;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Memory;

/// <summary>
/// Production-grade memory pool manager for OpenCL buffers.
/// Implements three-tier pooling strategy (Small/Medium/Large) with comprehensive statistics tracking.
/// </summary>
/// <remarks>
/// <para>
/// This pool manager implements a sophisticated buffer pooling system optimized for OpenCL workloads:
/// </para>
/// <list type="bullet">
/// <item><description>Three-tier pooling: Small (4KB), Medium (64KB), Large (1MB) buffers</description></item>
/// <item><description>Thread-safe operations using ConcurrentDictionary and Interlocked</description></item>
/// <item><description>Automatic buffer validation and alignment</description></item>
/// <item><description>Comprehensive statistics tracking (pool hits, misses, memory usage)</description></item>
/// <item><description>Memory pressure handling with configurable pool size limits</description></item>
/// <item><description>Integration with MemoryConfiguration for all settings</description></item>
/// </list>
/// <para>
/// Target pool hit rate: >80% for optimal performance.
/// Pool hit rate calculation: PoolHits / (PoolHits + PoolMisses) × 100%
/// </para>
/// </remarks>
public sealed class OpenCLMemoryPoolManager : IAsyncDisposable
{
    private readonly OpenCLContext _context;
    private readonly MemoryConfiguration _config;
    private readonly ILogger<OpenCLMemoryPoolManager> _logger;
    private readonly Dictionary<BufferTier, BufferPool> _tierPools;
    private bool _disposed;

    // Statistics tracking - NOT readonly because modified via Interlocked
#pragma warning disable IDE0044 // Add readonly modifier
    private long _totalAllocations;
    private long _totalDeallocations;
    private long _smallTierHits;
    private long _mediumTierHits;
    private long _largeTierHits;
    private long _poolMisses;
    private long _currentPooledMemoryBytes;
    private long _totalAllocatedMemoryBytes;
#pragma warning restore IDE0044 // Add readonly modifier

    /// <summary>
    /// Buffer size tier classification for pooling strategy.
    /// </summary>
    public enum BufferTier
    {
        /// <summary>Small buffers (&lt; SmallBufferThreshold, default 4KB)</summary>
        Small,

        /// <summary>Medium buffers (&lt; MediumBufferThreshold, default 64KB)</summary>
        Medium,

        /// <summary>Large buffers (&lt; LargeBufferThreshold, default 1MB)</summary>
        Large
    }

    /// <summary>
    /// Pool for a specific buffer size tier.
    /// </summary>
    private sealed class BufferPool
    {
        private readonly ConcurrentBag<OpenCLBuffer> _pool;
        private long _currentPoolSize;
        private long _totalAllocated;
        private readonly int _maxPoolSize;

        public ConcurrentBag<OpenCLBuffer> Pool => _pool;
        public long CurrentPoolSize => Interlocked.Read(ref _currentPoolSize);
        public long TotalAllocated => Interlocked.Read(ref _totalAllocated);
        public int MaxPoolSize => _maxPoolSize;

        public BufferPool(int maxPoolSize)
        {
            _pool = new ConcurrentBag<OpenCLBuffer>();
            _maxPoolSize = maxPoolSize;
        }

        public void IncrementSize() => Interlocked.Increment(ref _currentPoolSize);
        public void DecrementSize() => Interlocked.Decrement(ref _currentPoolSize);
        public void IncrementAllocated() => Interlocked.Increment(ref _totalAllocated);
    }

    /// <summary>
    /// Wrapper for OpenCL buffer with pooling metadata.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Closely related type, simpler API")]
    public sealed class OpenCLBuffer
    {
        /// <summary>Gets the OpenCL memory object handle.</summary>
        public OpenCLMemObject Handle { get; }

        /// <summary>Gets the buffer size in bytes.</summary>
        public ulong Size { get; }

        /// <summary>Gets the memory flags used to create this buffer.</summary>
        public MemoryFlags Flags { get; }

        /// <summary>Gets the timestamp when this buffer was last used.</summary>
        public DateTimeOffset LastUsed { get; private set; }

        /// <summary>Gets the buffer tier classification.</summary>
        public BufferTier Tier { get; }

        public OpenCLBuffer(OpenCLMemObject handle, ulong size, MemoryFlags flags, BufferTier tier)
        {
            Handle = handle;
            Size = size;
            Flags = flags;
            Tier = tier;
            LastUsed = DateTimeOffset.UtcNow;
        }

        /// <summary>Updates the last used timestamp.</summary>
        public void MarkUsed() => LastUsed = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// RAII handle for pooled buffers with automatic return to pool on disposal.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Closely related type, simpler API")]
    public readonly struct PooledBufferHandle : IAsyncDisposable, IEquatable<PooledBufferHandle>
    {
        private readonly OpenCLMemoryPoolManager? _manager;
        private readonly OpenCLBuffer _buffer;
        private readonly Guid _id;

        /// <summary>Gets the underlying OpenCL buffer.</summary>
        public OpenCLBuffer Buffer => _buffer;

        internal PooledBufferHandle(OpenCLMemoryPoolManager manager, OpenCLBuffer buffer)
        {
            _manager = manager;
            _buffer = buffer;
            _id = Guid.NewGuid();
        }

        /// <summary>
        /// Returns the buffer to the pool automatically.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_manager != null)
            {
                await _manager.ReleaseBufferAsync(_buffer).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Determines whether the specified handle is equal to the current handle.
        /// </summary>
        public bool Equals(PooledBufferHandle other) => _id.Equals(other._id);

        /// <summary>
        /// Determines whether the specified object is equal to the current handle.
        /// </summary>
        public override bool Equals(object? obj) => obj is PooledBufferHandle other && Equals(other);

        /// <summary>
        /// Returns the hash code for this handle.
        /// </summary>
        public override int GetHashCode() => _id.GetHashCode();

        /// <summary>
        /// Determines whether two handles are equal.
        /// </summary>
        public static bool operator ==(PooledBufferHandle left, PooledBufferHandle right) => left.Equals(right);

        /// <summary>
        /// Determines whether two handles are not equal.
        /// </summary>
        public static bool operator !=(PooledBufferHandle left, PooledBufferHandle right) => !left.Equals(right);
    }

    /// <summary>
    /// Comprehensive statistics about pool performance and memory usage.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Closely related type, simpler API")]
    public sealed class PoolStatistics
    {
        /// <summary>Gets the number of small buffer pool hits.</summary>
        public long SmallBufferPoolHits { get; init; }

        /// <summary>Gets the number of medium buffer pool hits.</summary>
        public long MediumBufferPoolHits { get; init; }

        /// <summary>Gets the number of large buffer pool hits.</summary>
        public long LargeBufferPoolHits { get; init; }

        /// <summary>Gets the total number of buffer allocations.</summary>
        public long TotalAllocations { get; init; }

        /// <summary>Gets the total number of buffer deallocations.</summary>
        public long TotalDeallocations { get; init; }

        /// <summary>Gets the pool hit rate as a percentage (0.0-100.0).</summary>
        public double HitRate
        {
            get
            {
                long totalHits = SmallBufferPoolHits + MediumBufferPoolHits + LargeBufferPoolHits;
                long totalRequests = totalHits + PoolMisses;
                return totalRequests == 0 ? 0.0 : (totalHits * 100.0) / totalRequests;
            }
        }

        /// <summary>Gets the number of pool misses (new buffer allocations).</summary>
        public long PoolMisses { get; init; }

        /// <summary>Gets the current amount of memory pooled (in bytes).</summary>
        public long CurrentPooledMemoryBytes { get; init; }

        /// <summary>Gets the total amount of memory allocated (in bytes).</summary>
        public long TotalAllocatedMemoryBytes { get; init; }

        /// <summary>Gets the number of buffers currently in the small tier pool.</summary>
        public long SmallTierPoolSize { get; init; }

        /// <summary>Gets the number of buffers currently in the medium tier pool.</summary>
        public long MediumTierPoolSize { get; init; }

        /// <summary>Gets the number of buffers currently in the large tier pool.</summary>
        public long LargeTierPoolSize { get; init; }
    }

    /// <summary>
    /// Gets the manager name for logging purposes.
    /// </summary>
    private const string ManagerName = "OpenCLMemoryPool";

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMemoryPoolManager"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context for buffer creation.</param>
    /// <param name="config">Memory configuration settings.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public OpenCLMemoryPoolManager(
        OpenCLContext context,
        MemoryConfiguration config,
        ILogger<OpenCLMemoryPoolManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize tier pools with appropriate sizes
        // Allocate pool capacity proportionally: Small 50%, Medium 35%, Large 15%
        int totalPoolCapacity = (int)(_config.MaximumPoolSize / _config.MediumBufferThreshold);
        int smallPoolSize = (int)(totalPoolCapacity * 0.5);
        int mediumPoolSize = (int)(totalPoolCapacity * 0.35);
        int largePoolSize = (int)(totalPoolCapacity * 0.15);

        _tierPools = new Dictionary<BufferTier, BufferPool>
        {
            [BufferTier.Small] = new BufferPool(smallPoolSize),
            [BufferTier.Medium] = new BufferPool(mediumPoolSize),
            [BufferTier.Large] = new BufferPool(largePoolSize)
        };

        _logger.LogInformation(
            "{ManagerName} initialized: Small={SmallThreshold}B, Medium={MediumThreshold}B, Large={LargeThreshold}B, MaxPool={MaxPool}B",
            ManagerName, _config.SmallBufferThreshold, _config.MediumBufferThreshold,
            _config.LargeBufferThreshold, _config.MaximumPoolSize);
    }

    /// <summary>
    /// Acquires a buffer from the pool or allocates a new one.
    /// </summary>
    /// <param name="size">The size of the buffer in bytes.</param>
    /// <param name="flags">Memory flags for buffer creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the acquired buffer with RAII semantics.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the pool has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when size is 0.</exception>
    public async ValueTask<PooledBufferHandle> AcquireBufferAsync(
        ulong size,
        MemoryFlags flags,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (size == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Buffer size must be greater than 0");
        }

        // Apply alignment
        size = AlignSize(size);

        Interlocked.Increment(ref _totalAllocations);

        var tier = GetBufferTier(size);

        // Try to get buffer from pool first
        if (_config.EnableBufferPooling && TryTakeFromPool(tier, size, flags, out var buffer))
        {
            IncrementTierHit(tier);
            buffer.MarkUsed();

            _logger.LogDebug(
                "{ManagerName}: Pool hit for {Tier} buffer (size={Size}, hit rate={HitRate:F2}%)",
                ManagerName, tier, size, GetStatistics().HitRate);

            return new PooledBufferHandle(this, buffer);
        }

        // Pool miss - allocate new buffer
        Interlocked.Increment(ref _poolMisses);
        buffer = await AllocateNewBufferAsync(size, flags, tier, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "{ManagerName}: Pool miss, allocated new {Tier} buffer (size={Size}, total allocated={TotalBytes}B)",
            ManagerName, tier, size, Interlocked.Read(ref _totalAllocatedMemoryBytes));

        return new PooledBufferHandle(this, buffer);
    }

    /// <summary>
    /// Releases a buffer back to the pool for reuse.
    /// </summary>
    /// <param name="buffer">The buffer to release.</param>
    /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
    public async ValueTask ReleaseBufferAsync(OpenCLBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (_disposed)
        {
            // If disposed, destroy buffer immediately
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            return;
        }

        Interlocked.Increment(ref _totalDeallocations);

        if (!_config.EnableBufferPooling)
        {
            // Pooling disabled, destroy immediately
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            return;
        }

        // Validate buffer before returning to pool
        if (!ValidateBuffer(buffer))
        {
            _logger.LogWarning(
                "{ManagerName}: Buffer validation failed, destroying instead of pooling",
                ManagerName);
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            return;
        }

        // Check if pool has capacity
        long currentPooled = Interlocked.Read(ref _currentPooledMemoryBytes);
        if (currentPooled + (long)buffer.Size > _config.MaximumPoolSize)
        {
            _logger.LogDebug(
                "{ManagerName}: Pool full ({Current}/{Max} bytes), destroying buffer",
                ManagerName, currentPooled, _config.MaximumPoolSize);
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            return;
        }

        // Check tier-specific pool capacity
        var pool = _tierPools[buffer.Tier];
        if (pool.CurrentPoolSize >= pool.MaxPoolSize)
        {
            _logger.LogDebug(
                "{ManagerName}: {Tier} tier pool full ({Current}/{Max}), destroying buffer",
                ManagerName, buffer.Tier, pool.CurrentPoolSize, pool.MaxPoolSize);
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            return;
        }

        // Return to pool
        pool.Pool.Add(buffer);
        pool.IncrementSize();
        Interlocked.Add(ref _currentPooledMemoryBytes, (long)buffer.Size);

        _logger.LogTrace(
            "{ManagerName}: Buffer returned to {Tier} pool (size={Size}, pooled={Pooled}B)",
            ManagerName, buffer.Tier, buffer.Size, currentPooled + (long)buffer.Size);
    }

    /// <summary>
    /// Clears a specific tier pool, destroying all buffers.
    /// </summary>
    /// <param name="tier">The tier to clear.</param>
    public async ValueTask ClearTierAsync(BufferTier tier)
    {
        ThrowIfDisposed();

        var pool = _tierPools[tier];
        long destroyedCount = 0;
        long freedBytes = 0;

        while (pool.Pool.TryTake(out var buffer))
        {
            freedBytes += (long)buffer.Size;
            await DestroyBufferAsync(buffer).ConfigureAwait(false);
            pool.DecrementSize();
            destroyedCount++;
        }

        _logger.LogInformation(
            "{ManagerName}: Cleared {Tier} tier pool: {Count} buffers, {Bytes}B freed",
            ManagerName, tier, destroyedCount, freedBytes);
    }

    /// <summary>
    /// Gets comprehensive statistics about pool performance.
    /// </summary>
    /// <returns>A snapshot of current pool statistics.</returns>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics
        {
            SmallBufferPoolHits = Interlocked.Read(ref _smallTierHits),
            MediumBufferPoolHits = Interlocked.Read(ref _mediumTierHits),
            LargeBufferPoolHits = Interlocked.Read(ref _largeTierHits),
            TotalAllocations = Interlocked.Read(ref _totalAllocations),
            TotalDeallocations = Interlocked.Read(ref _totalDeallocations),
            PoolMisses = Interlocked.Read(ref _poolMisses),
            CurrentPooledMemoryBytes = Interlocked.Read(ref _currentPooledMemoryBytes),
            TotalAllocatedMemoryBytes = Interlocked.Read(ref _totalAllocatedMemoryBytes),
            SmallTierPoolSize = _tierPools[BufferTier.Small].CurrentPoolSize,
            MediumTierPoolSize = _tierPools[BufferTier.Medium].CurrentPoolSize,
            LargeTierPoolSize = _tierPools[BufferTier.Large].CurrentPoolSize
        };
    }

    /// <summary>
    /// Determines the appropriate tier for a buffer size.
    /// </summary>
    /// <param name="size">Buffer size in bytes.</param>
    /// <returns>The buffer tier classification.</returns>
    private BufferTier GetBufferTier(ulong size)
    {
        if (size < (ulong)_config.SmallBufferThreshold)
        {
            return BufferTier.Small;
        }

        if (size < (ulong)_config.MediumBufferThreshold)
        {
            return BufferTier.Medium;
        }

        return BufferTier.Large;
    }

    /// <summary>
    /// Aligns buffer size according to configuration.
    /// </summary>
    /// <param name="size">Original size in bytes.</param>
    /// <returns>Aligned size in bytes.</returns>
    private ulong AlignSize(ulong size)
    {
        var alignment = (ulong)_config.BufferAlignment;
        return ((size + alignment - 1) / alignment) * alignment;
    }

    /// <summary>
    /// Tries to take a buffer from the pool matching the requested size and flags.
    /// </summary>
    private bool TryTakeFromPool(BufferTier tier, ulong size, MemoryFlags flags, out OpenCLBuffer buffer)
    {
        var pool = _tierPools[tier];

        // Simple strategy: take any buffer from the tier that matches flags
        // In production, you might implement more sophisticated matching
        if (pool.Pool.TryTake(out buffer!))
        {
            // Check if buffer is suitable (size and flags match)
            if (buffer.Size >= size && buffer.Flags == flags)
            {
                pool.DecrementSize();
                Interlocked.Add(ref _currentPooledMemoryBytes, -(long)buffer.Size);
                return true;
            }

            // Buffer doesn't match, put it back
            pool.Pool.Add(buffer);
        }

        buffer = null!;
        return false;
    }

    /// <summary>
    /// Allocates a new OpenCL buffer.
    /// </summary>
    private async ValueTask<OpenCLBuffer> AllocateNewBufferAsync(
        ulong size,
        MemoryFlags flags,
        BufferTier tier,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var handle = OpenCLRuntime.clCreateBuffer(
                    _context.Context.Handle,
                    flags,
                    (nuint)size,
                    nint.Zero,
                    out var error);

                OpenCLException.ThrowIfError(error, "Create buffer");

                Interlocked.Add(ref _totalAllocatedMemoryBytes, (long)size);

                var pool = _tierPools[tier];
                pool.IncrementAllocated();

                return new OpenCLBuffer(new OpenCLMemObject(handle), size, flags, tier);
            }
            catch (OpenCLException ex)
            {
                _logger.LogError(ex,
                    "{ManagerName}: Failed to allocate buffer (size={Size}, flags={Flags})",
                    ManagerName, size, flags);
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates whether a buffer is still valid for pooling.
    /// </summary>
    /// <param name="buffer">The buffer to validate.</param>
    /// <returns>True if the buffer is valid; otherwise, false.</returns>
    private bool ValidateBuffer(OpenCLBuffer buffer)
    {
        // Check if buffer handle is valid
        if (buffer.Handle.Handle == nint.Zero)
        {
            return false;
        }

        // Check if buffer has been idle too long
        var idleTime = DateTimeOffset.UtcNow - buffer.LastUsed;
        if (idleTime > _config.BufferIdleTimeout)
        {
            _logger.LogDebug(
                "{ManagerName}: Buffer idle for {IdleTime}, exceeds timeout {Timeout}",
                ManagerName, idleTime, _config.BufferIdleTimeout);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Destroys an OpenCL buffer and releases its memory.
    /// </summary>
    private async ValueTask DestroyBufferAsync(OpenCLBuffer buffer)
    {
        await Task.Run(() =>
        {
            if (buffer.Handle.Handle == nint.Zero)
            {
                return;
            }

            var error = OpenCLRuntime.clReleaseMemObject(buffer.Handle.Handle);
            if (error != OpenCLError.Success)
            {
                _logger.LogWarning(
                    "{ManagerName}: Failed to release buffer {Handle}: {Error}",
                    ManagerName, buffer.Handle.Handle, error);
            }

            Interlocked.Add(ref _totalAllocatedMemoryBytes, -(long)buffer.Size);
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Increments the appropriate tier hit counter.
    /// </summary>
    private void IncrementTierHit(BufferTier tier)
    {
        switch (tier)
        {
            case BufferTier.Small:
                Interlocked.Increment(ref _smallTierHits);
                break;
            case BufferTier.Medium:
                Interlocked.Increment(ref _mediumTierHits);
                break;
            case BufferTier.Large:
                Interlocked.Increment(ref _largeTierHits);
                break;
        }
    }

    /// <summary>
    /// Throws if this manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    /// <summary>
    /// Asynchronously disposes the memory pool manager and all pooled buffers.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("{ManagerName}: Disposing memory pool manager", ManagerName);

        try
        {
            // Clear all tier pools sequentially
            await ClearTierAsync(BufferTier.Small).ConfigureAwait(false);
            await ClearTierAsync(BufferTier.Medium).ConfigureAwait(false);
            await ClearTierAsync(BufferTier.Large).ConfigureAwait(false);

            var stats = GetStatistics();
            _logger.LogInformation(
                "{ManagerName} disposed. Final stats: Allocations={Allocations}, Deallocations={Deallocations}, " +
                "Hit rate={HitRate:F2}%, Total allocated={TotalBytes}B",
                ManagerName, stats.TotalAllocations, stats.TotalDeallocations,
                stats.HitRate, stats.TotalAllocatedMemoryBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ManagerName}: Error during disposal", ManagerName);
        }
    }
}
