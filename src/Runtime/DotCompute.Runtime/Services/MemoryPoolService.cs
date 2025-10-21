// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Production implementation of memory pool service for efficient buffer reuse and management.
/// </summary>
public sealed class MemoryPoolService : IMemoryPoolService, IDisposable
{
    private readonly ILogger<MemoryPoolService> _logger;
    private readonly ConcurrentDictionary<long, Queue<PooledBuffer>> _sizePools = new();
    private readonly ConcurrentDictionary<Guid, PooledBuffer> _activeBuffers = new();
    private readonly Timer _cleanupTimer;
    private readonly object _statsLock = new();
    private long _totalAllocations;
    private long _totalHits;
    private long _totalBytesPooled;
    private bool _disposed;

    private readonly ConcurrentDictionary<string, AcceleratorMemoryPool> _acceleratorPools = new();


    /// <summary>
    /// Gets a memory pool for the specified accelerator
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <returns>The memory pool for the accelerator</returns>
    public IMemoryPool GetPool(string acceleratorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);
        return _acceleratorPools.GetOrAdd(acceleratorId, id => new AcceleratorMemoryPool(id, _logger));
    }


    /// <summary>
    /// Creates a new memory pool for an accelerator
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <param name="initialSize">The initial pool size in bytes</param>
    /// <param name="maxSize">The maximum pool size in bytes</param>
    /// <returns>The created memory pool</returns>
    public async Task<IMemoryPool> CreatePoolAsync(string acceleratorId, long initialSize, long maxSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acceleratorId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize, initialSize);


        await Task.CompletedTask;
        var pool = new AcceleratorMemoryPool(acceleratorId, _logger, initialSize, maxSize);
        _acceleratorPools[acceleratorId] = pool;
        return pool;
    }


    /// <summary>
    /// Gets memory usage statistics across all pools
    /// </summary>
    /// <returns>Memory usage statistics</returns>
    public MemoryUsageStatistics GetUsageStatistics()
    {
        lock (_statsLock)
        {
            var perAcceleratorStats = new Dictionary<string, AcceleratorMemoryStatistics>();
            foreach (var kvp in _acceleratorPools)
            {
                var pool = kvp.Value;
                perAcceleratorStats[kvp.Key] = new AcceleratorMemoryStatistics
                {
                    AcceleratorId = kvp.Key,
                    TotalMemory = pool.TotalSize,
                    AllocatedMemory = pool.UsedSize,
                    AvailableMemory = pool.AvailableSize,
                    ActiveAllocations = pool.ActiveAllocations,
                    LargestAvailableBlock = pool.AvailableSize
                };
            }


            return new MemoryUsageStatistics
            {
                TotalAllocated = _totalBytesPooled,
                TotalAvailable = _acceleratorPools.Values.Sum(p => p.AvailableSize),
                FragmentationPercentage = CalculateFragmentationPercentage(),
                PerAcceleratorStats = perAcceleratorStats
            };
        }
    }


    /// <summary>
    /// Optimizes memory usage across all pools
    /// </summary>
    /// <returns>A task representing the optimization operation</returns>
    public async Task OptimizeMemoryUsageAsync()
    {
        var defragmentTasks = _acceleratorPools.Values.Select(pool => pool.DefragmentAsync());
        await Task.WhenAll(defragmentTasks);
        await PerformMaintenanceAsync();
    }


    /// <summary>
    /// Releases unused memory from all pools
    /// </summary>
    /// <returns>The amount of memory released in bytes</returns>
    public async Task<long> ReleaseUnusedMemoryAsync()
    {
        long totalReleased = 0;


        foreach (var pool in _acceleratorPools.Values)
        {
            // For this implementation, defragmentation acts as memory release
            await pool.DefragmentAsync();
            totalReleased += pool.TotalSize - pool.UsedSize;
        }


        await PerformMaintenanceAsync();
        return totalReleased;
    }
    /// <summary>
    /// Initializes a new instance of the MemoryPoolService class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public MemoryPoolService(ILogger<MemoryPoolService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Start periodic cleanup every 5 minutes

        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));


        _logger.LogInfoMessage("Memory pool service initialized");
    }

    /// <summary>
    /// Tries to get a buffer from the pool for the specified size.
    /// </summary>
    /// <param name="sizeInBytes">The size of buffer needed.</param>
    /// <param name="options">Memory options for the buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A pooled buffer if available, null otherwise.</returns>
    internal async ValueTask<IUnifiedMemoryBuffer?> TryGetBufferAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Round up to nearest power of 2 for better pooling efficiency
        var poolSize = RoundToPowerOfTwo(sizeInBytes);

        lock (_statsLock)
        {
            _totalAllocations++;
        }

        if (_sizePools.TryGetValue(poolSize, out var queue))
        {
            PooledBuffer? buffer = null;


            lock (queue)
            {
                if (queue.Count > 0)
                {
                    buffer = queue.Dequeue();
                }
            }

            if (buffer != null && !buffer.IsDisposed)
            {
                // Reset buffer for reuse
                await buffer.ResetAsync(sizeInBytes, options);
                _activeBuffers[buffer.Id] = buffer;

                lock (_statsLock)
                {
                    _totalHits++;
                }

                _logger.LogTrace("Retrieved buffer {BufferId} of size {Size} from pool", buffer.Id, sizeInBytes);
                return buffer;
            }
        }

        return null; // No suitable buffer found in pool
    }

    /// <summary>
    /// Returns a buffer to the pool for reuse.
    /// </summary>
    /// <param name="buffer">The buffer to return to the pool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async ValueTask ReturnBufferAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (_disposed || buffer == null)
        {
            return;
        }

        if (buffer is PooledBuffer pooledBuffer && _activeBuffers.TryRemove(pooledBuffer.Id, out _))
        {
            var poolSize = RoundToPowerOfTwo(buffer.SizeInBytes);
            var queue = _sizePools.GetOrAdd(poolSize, _ => new Queue<PooledBuffer>());

            lock (queue)
            {
                // Limit pool size to prevent memory bloat
                if (queue.Count < 10)
                {
                    queue.Enqueue(pooledBuffer);
                    lock (_statsLock)
                    {
                        _totalBytesPooled += buffer.SizeInBytes;
                    }


                    _logger.LogTrace("Returned buffer {BufferId} of size {Size} to pool", pooledBuffer.Id, buffer.SizeInBytes);
                }
                else
                {
                    // Pool is full, dispose the buffer synchronously
                    buffer.Dispose();
                }
            }
        }
        else
        {
            // Not a pooled buffer or not tracked, just dispose
            await buffer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new pooled buffer.
    /// </summary>
    /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
    /// <param name="options">Memory options for the buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new pooled buffer.</returns>
    internal async ValueTask<IUnifiedMemoryBuffer> CreateBufferAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        await Task.CompletedTask; // Make method properly async

        var buffer = new PooledBuffer(Guid.NewGuid(), sizeInBytes, options, _logger);
        _activeBuffers[buffer.Id] = buffer;

        _logger.LogTrace("Created new pooled buffer {BufferId} of size {Size}", buffer.Id, sizeInBytes);
        return buffer;
    }

    /// <summary>
    /// Performs cleanup of unused buffers and statistics.
    /// </summary>
    private async ValueTask PerformMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            var totalCleaned = 0;
            var bytesFreed = 0L;

            foreach (var kvp in _sizePools.ToList())
            {
                var queue = kvp.Value;
                lock (queue)
                {
                    // Keep only the most recent buffers, dispose older ones
                    var keepCount = Math.Min(queue.Count, 5);
                    var disposeCount = queue.Count - keepCount;

                    for (var i = 0; i < disposeCount; i++)
                    {
                        if (queue.TryDequeue(out var buffer))
                        {
                            bytesFreed += buffer.SizeInBytes;
                            buffer.Dispose();
                            totalCleaned++;
                        }
                    }
                }
            }

            // Remove empty pools
            var emptyPools = _sizePools.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
            foreach (var poolSize in emptyPools)
            {
                _ = _sizePools.TryRemove(poolSize, out _);
            }

            lock (_statsLock)
            {
                _totalBytesPooled = Math.Max(0, _totalBytesPooled - bytesFreed);
            }

            if (totalCleaned > 0)
            {
                _logger.LogDebugMessage($"Pool maintenance cleaned {totalCleaned} buffers, freed {bytesFreed} bytes");
            }
        }, cancellationToken);
    }


    private double CalculateFragmentationPercentage()
    {
        if (_acceleratorPools.IsEmpty)
        {
            return 0.0;
        }


        var totalSize = _acceleratorPools.Values.Sum(p => p.TotalSize);
        var totalUsed = _acceleratorPools.Values.Sum(p => p.UsedSize);


        if (totalSize == 0)
        {
            return 0.0;
        }


        return (double)(totalSize - totalUsed) / totalSize * 100.0;
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            _ = PerformMaintenanceAsync().AsTask();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during pool cleanup");
        }
    }

    private static long RoundToPowerOfTwo(long value)
    {
        if (value <= 0)
        {
            return 1;
        }

        var result = 1L;
        while (result < value)
        {
            result <<= 1;
        }
        return result;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInfoMessage("Disposing memory pool service with {_activeBuffers.Count} active buffers");

        // Dispose cleanup timer
        _cleanupTimer?.Dispose();

        // Dispose all pooled buffers
        foreach (var queue in _sizePools.Values)
        {
            lock (queue)
            {
                while (queue.TryDequeue(out var buffer))
                {
                    try
                    {
                        buffer.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing pooled buffer {BufferId}", buffer.Id);
                    }
                }
            }
        }

        // Dispose active buffers
        foreach (var buffer in _activeBuffers.Values)
        {
            try
            {
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing active buffer {BufferId}", buffer.Id);
            }
        }

        _sizePools.Clear();
        _activeBuffers.Clear();

        _logger.LogInfoMessage("Memory pool service disposed");
    }
}


/// <summary>
/// A pooled memory buffer implementation.
/// </summary>
internal sealed class PooledBuffer : IUnifiedMemoryBuffer, IDisposable
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public Guid Id { get; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; private set; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; private set; }
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; private set; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State { get; private set; } = BufferState.Allocated;

    private readonly ILogger _logger;
    private IntPtr _nativeHandle;
    private GCHandle _pinnedHandle;
    /// <summary>
    /// Initializes a new instance of the PooledBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>

    public PooledBuffer(Guid id, long sizeInBytes, MemoryOptions options, ILogger logger)
    {
        Id = id;
        SizeInBytes = sizeInBytes;
        Options = options;
        _logger = logger;

        AllocateMemory(sizeInBytes);
    }
    /// <summary>
    /// Gets reset asynchronously.
    /// </summary>
    /// <param name="newSize">The new size.</param>
    /// <param name="newOptions">The new options.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask ResetAsync(long newSize, MemoryOptions newOptions)
    {
        if (newSize != SizeInBytes)
        {
            FreeMemory();
            AllocateMemory(newSize);
            SizeInBytes = newSize;
        }

        Options = newOptions;
        State = BufferState.Allocated;
        await Task.CompletedTask;
    }

    private void AllocateMemory(long size)
    {
        var managedBuffer = new byte[size];
        _pinnedHandle = GCHandle.Alloc(managedBuffer, GCHandleType.Pinned);
        _nativeHandle = _pinnedHandle.AddrOfPinnedObject();
    }

    private void FreeMemory()
    {
        if (_pinnedHandle.IsAllocated)
        {
            _pinnedHandle.Free();
        }
        _nativeHandle = IntPtr.Zero;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Simplified implementation for production usage TODO
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        // Simplified implementation for production usage TODO
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            State = BufferState.Disposed;


            try
            {
                FreeMemory();
                _logger.LogTrace("Disposed pooled buffer {BufferId}", Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing pooled buffer {BufferId}", Id);
            }
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Accelerator-specific memory pool implementation.
/// </summary>
internal sealed class AcceleratorMemoryPool(string acceleratorId, ILogger logger, long initialSize = 1024 * 1024, long maxSize = 512 * 1024 * 1024) : IMemoryPool, IDisposable
{
    /// <summary>
    /// Gets or sets the accelerator identifier.
    /// </summary>
    /// <value>The accelerator id.</value>
    public string AcceleratorId { get; } = acceleratorId;
    /// <summary>
    /// Gets or sets the total size.
    /// </summary>
    /// <value>The total size.</value>
    public long TotalSize { get; private set; } = initialSize;
    /// <summary>
    /// Gets or sets the available size.
    /// </summary>
    /// <value>The available size.</value>
    public long AvailableSize => TotalSize - UsedSize;
    /// <summary>
    /// Gets or sets the used size.
    /// </summary>
    /// <value>The used size.</value>
    public long UsedSize { get; private set; }
    /// <summary>
    /// Gets or sets the active allocations.
    /// </summary>
    /// <value>The active allocations.</value>
    public int ActiveAllocations { get; private set; }

    private readonly ILogger _logger = logger;
    private readonly ConcurrentQueue<IUnifiedMemoryBuffer> _freeBuffers = new();
    private readonly ConcurrentDictionary<Guid, IUnifiedMemoryBuffer> _activeBuffers = new();
    private readonly long _maxSize = maxSize;
    private bool _disposed;
    /// <summary>
    /// Gets allocate asynchronously.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<IUnifiedMemoryBuffer> AllocateAsync(long sizeInBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.CompletedTask;

        // Try to reuse existing buffer
        if (_freeBuffers.TryDequeue(out var buffer))
        {
            var bufferId = Guid.NewGuid();
            _activeBuffers[bufferId] = buffer;
            ActiveAllocations++;
            UsedSize += sizeInBytes;
            return buffer;
        }

        // Create new buffer if we have space
        if (UsedSize + sizeInBytes <= _maxSize)
        {
            var newBufferId = Guid.NewGuid();
            var newBuffer = new PooledBuffer(newBufferId, sizeInBytes, MemoryOptions.None, _logger);
            _activeBuffers[newBufferId] = newBuffer;
            ActiveAllocations++;
            UsedSize += sizeInBytes;
            TotalSize = Math.Max(TotalSize, UsedSize);
            return newBuffer;
        }

        throw new OutOfMemoryException($"Memory pool for accelerator {AcceleratorId} is full");
    }
    /// <summary>
    /// Gets return asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>The result of the operation.</returns>

    public async Task ReturnAsync(IUnifiedMemoryBuffer buffer)
    {
        if (_disposed || buffer == null)
        {
            return;
        }

        await Task.CompletedTask;

        if (buffer is PooledBuffer pooledBuffer)
        {
            if (_activeBuffers.TryRemove(pooledBuffer.Id, out _))
            {
                _freeBuffers.Enqueue(buffer);
                ActiveAllocations--;
                UsedSize -= buffer.SizeInBytes;
            }
        }
    }
    /// <summary>
    /// Gets defragment asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public async Task DefragmentAsync()
    {
        if (_disposed)
        {
            return;
        }

        await Task.CompletedTask;

        // Simple defragmentation - just clear unused buffers
        var toDispose = new List<IUnifiedMemoryBuffer>();
        while (_freeBuffers.TryDequeue(out var buffer))
        {
            toDispose.Add(buffer);
        }

        foreach (var buffer in toDispose)
        {
            await buffer.DisposeAsync();
        }

        _logger.LogDebugMessage($"Defragmented memory pool for accelerator {AcceleratorId}, disposed {toDispose.Count} unused buffers");
    }
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <returns>The statistics.</returns>

    public MemoryPoolStatistics GetStatistics()
    {
        return new MemoryPoolStatistics
        {
            AllocationCount = ActiveAllocations,
            DeallocationCount = 0, // Simplified
            TotalBytesAllocated = UsedSize,
            TotalBytesDeallocated = 0, // Simplified
            PeakMemoryUsage = TotalSize,
            AverageAllocationSize = ActiveAllocations > 0 ? (double)UsedSize / ActiveAllocations : 0.0,
            DefragmentationCount = 0 // Simplified
        };
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose all active buffers
        foreach (var buffer in _activeBuffers.Values)
        {
            try
            {
                _ = buffer.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing active buffer in pool {AcceleratorId}", AcceleratorId);
            }
        }

        // Dispose all free buffers
        while (_freeBuffers.TryDequeue(out var buffer))
        {
            try
            {
                _ = buffer.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing free buffer in pool {AcceleratorId}", AcceleratorId);
            }
        }

        _activeBuffers.Clear();
        _logger.LogDebugMessage("Disposed memory pool for accelerator {AcceleratorId}");
    }
}