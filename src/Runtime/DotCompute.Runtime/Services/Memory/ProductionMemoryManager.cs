// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Memory;
using DotCompute.Runtime.Logging;
using DotCompute.Runtime.Services.Buffers;
using DotCompute.Runtime.Services.Buffers.Views;
using DotCompute.Runtime.Services.Memory.Pool;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Production memory manager implementation with advanced memory pool management, P2P transfers,
/// and comprehensive error handling for accelerated computing workloads.
/// </summary>
public sealed class ProductionMemoryManager : BaseMemoryManager
{
    private readonly ILogger<ProductionMemoryManager> _logger;
    private readonly ConcurrentDictionary<long, ProductionMemoryBuffer> _buffers = new();
    private readonly ConcurrentDictionary<long, WeakReference<ProductionMemoryBuffer>> _bufferRegistry = new();
    private readonly MemoryPool _memoryPool;
    private readonly Statistics.MemoryStatistics _statistics = new();
    private readonly SemaphoreSlim _allocationSemaphore = new(32, 32); // Limit concurrent allocations
    private long _nextId = 1;
    private bool _disposed;
    private readonly IAccelerator? _accelerator;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>

    // Interface Properties

    public override IAccelerator Accelerator => _accelerator ?? throw new InvalidOperationException("No accelerator associated with this memory manager");
    /// <summary>
    /// Gets or sets the statistics.
    /// </summary>
    /// <value>The statistics.</value>
    public override MemoryStatistics Statistics => new()
    {
        TotalAllocated = _statistics.TotalBytesAllocated,
        CurrentUsage = _statistics.CurrentlyAllocatedBytes,
        CurrentUsed = _statistics.CurrentlyAllocatedBytes,
        PeakUsage = _statistics.TotalBytesAllocated,
        AllocationCount = _statistics.TotalAllocations,
        DeallocationCount = _statistics.TotalDeallocations,
        ActiveAllocations = _statistics.TotalAllocations - _statistics.TotalDeallocations,
        AvailableMemory = TotalAvailableMemory - _statistics.CurrentlyAllocatedBytes,
        TotalCapacity = TotalAvailableMemory,
        FragmentationPercentage = 0.0,
        AverageAllocationSize = _statistics.AverageAllocationTime,
        TotalAllocationCount = _statistics.TotalAllocations,
        TotalDeallocationCount = _statistics.TotalDeallocations,
        PoolHitRate = _statistics.PoolHitRate
    };
    /// <summary>
    /// Gets or sets the max allocation size.
    /// </summary>
    /// <value>The max allocation size.</value>
    public override long MaxAllocationSize => 16L * 1024 * 1024 * 1024; // 16GB
    /// <summary>
    /// Gets or sets the total available memory.
    /// </summary>
    /// <value>The total available memory.</value>
    public override long TotalAvailableMemory => 32L * 1024 * 1024 * 1024; // 32GB simulated
    /// <summary>
    /// Gets or sets the current allocated memory.
    /// </summary>
    /// <value>The current allocated memory.</value>
    public override long CurrentAllocatedMemory => _statistics.CurrentlyAllocatedBytes;
    /// <summary>
    /// Initializes a new instance of the ProductionMemoryManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="accelerator">The accelerator.</param>

    public ProductionMemoryManager(ILogger<ProductionMemoryManager> logger, IAccelerator? accelerator = null) : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accelerator = accelerator; // Can be null for CPU scenarios
        _memoryPool = new MemoryPool(_logger);

        // Start background cleanup task
        _ = Task.Run(PerformPeriodicCleanupAsync);

        _logger.LogInfoMessage("Production memory manager initialized with advanced memory pooling");
    }
    /// <summary>
    /// Gets allocate asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="count">The count.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Generic allocation for typed buffers
    public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        var sizeInBytes = count * Unsafe.SizeOf<T>();
        var buffer = await AllocateRawAsync(sizeInBytes, options, cancellationToken);
        return new TypedMemoryBufferWrapper<T>(buffer, count);
    }
    /// <summary>
    /// Gets allocate raw asynchronously.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(long sizeInBytes, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
        // Delegate to the internal implementation

        => await AllocateInternalAsync(sizeInBytes, options, cancellationToken);
    /// <summary>
    /// Gets allocate and copy asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(ReadOnlyMemory<T> source, MemoryOptions options = MemoryOptions.None, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);

        try
        {
            await buffer.CopyFromAsync(source, cancellationToken);
            return buffer;
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }
    /// <summary>
    /// Creates a new view.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The created view.</returns>

    public override IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (offset + length > buffer.Length)
        {
            throw new ArgumentException("View extends beyond buffer boundaries");
        }

        var viewId = Interlocked.Increment(ref _nextId);
        var view = new TypedMemoryBufferView<T>(buffer, offset, length);

        _logger.LogTrace("Created typed memory buffer view {ViewId} with offset {Offset} and length {Length}",
            viewId, offset, length);

        return view;
    }
    /// <summary>
    /// Creates a new view.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The created view.</returns>


    public override IUnifiedMemoryBuffer CreateView(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        if (offset + length > buffer.SizeInBytes)
        {
            throw new ArgumentException("View extends beyond buffer boundaries");
        }

        var viewId = Interlocked.Increment(ref _nextId);
        var view = new ProductionMemoryBufferView(viewId, buffer, offset, length, _logger);

        _logger.LogTrace("Created memory buffer view {ViewId} with offset {Offset} and length {Length}",
            viewId, offset, length);

        return view;
    }
    /// <summary>
    /// Gets the statistics.
    /// </summary>
    /// <returns>The statistics.</returns>

    public Statistics.MemoryStatistics GetStatistics() => _statistics.CreateSnapshot();
    /// <summary>
    /// Gets copy asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Copy operations

    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);


        if (source.Length != destination.Length)
        {
            throw new ArgumentException("Source and destination buffers must be the same size");
        }


        await source.CopyToAsync(destination, cancellationToken);
    }
    /// <summary>
    /// Gets copy asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="length">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);


        await source.CopyToAsync(sourceOffset, destination, destinationOffset, length, cancellationToken);
    }
    /// <summary>
    /// Gets copy to device asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        await destination.CopyFromAsync(source, cancellationToken);
    }
    /// <summary>
    /// Gets copy from device asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        await source.CopyToAsync(destination, cancellationToken);
    }
    /// <summary>
    /// Gets free asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    // Free operations

    public override async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (buffer is ProductionMemoryBuffer prodBuffer)
        {
            _ = _buffers.TryRemove(prodBuffer.Id, out _);
            await prodBuffer.DisposeAsync();
        }
        else
        {
            await (buffer?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
    }
    /// <summary>
    /// Gets optimize asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }


        await _memoryPool.PerformMaintenanceAsync();

        // Trigger garbage collection for optimization

        GC.Collect(2, GCCollectionMode.Optimized, true, true);
        GC.WaitForPendingFinalizers();


        _logger.LogInfoMessage("Memory optimization completed");
    }
    /// <summary>
    /// Performs clear.
    /// </summary>


    public override void Clear()
    {
        if (_disposed)
        {
            return;
        }


        var buffersToDispose = _buffers.Values.ToList();
        _buffers.Clear();
        _bufferRegistry.Clear();


        foreach (var buffer in buffersToDispose)
        {
            try
            {
                buffer.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing buffer during clear operation");
            }
        }


        _logger.LogInfoMessage("Memory manager cleared - all buffers disposed");
    }

    /// <summary>
    /// Allocates memory for a specific number of elements.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="count">The number of elements to allocate.</param>
    /// <returns>A memory buffer for the allocated elements.</returns>
    public override async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        // Call the base class generic overload which handles wrapping
        return await base.AllocateAsync<T>(count);
    }

    /// <summary>
    /// Copies data from host memory to a device buffer.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="buffer">The destination buffer.</param>
    /// <param name="data">The source data span.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task CopyToDeviceAsync<T>(IUnifiedMemoryBuffer buffer, ReadOnlyMemory<T> data, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(buffer);

        var memory = data;
        await buffer.CopyFromAsync(memory, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Copies data from a device buffer to host memory.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="data">The destination data span.</param>
    /// <param name="buffer">The source buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task CopyFromDeviceAsync<T>(Memory<T> data, IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentNullException.ThrowIfNull(buffer);

        await buffer.CopyToAsync(data, 0, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Frees a memory buffer.
    /// </summary>
    /// <param name="buffer">The buffer to free.</param>
    public override void Free(IUnifiedMemoryBuffer buffer)
    {
        if (buffer is ProductionMemoryBuffer prodBuffer)
        {
            _ = _buffers.TryRemove(prodBuffer.Id, out _);
            prodBuffer.Dispose();
        }
        else
        {
            buffer?.Dispose();
        }
    }

    private async Task PerformPeriodicCleanupAsync()
    {
        while (!_disposed)
        {
            try
            {
                // Use a more efficient cleanup interval with cancellation support
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
                cts.CancelAfter(TimeSpan.FromMinutes(5));
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected after 5 minutes, continue cleanup
                }

                // Clean up dead weak references
                var deadReferences = new List<long>();
                foreach (var kvp in _bufferRegistry)
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        deadReferences.Add(kvp.Key);
                    }
                }

                foreach (var deadId in deadReferences)
                {
                    _ = _bufferRegistry.TryRemove(deadId, out _);
                }

                // Trigger memory pool cleanup
                await _memoryPool.PerformMaintenanceAsync();

                if (deadReferences.Count > 0)
                {
                    _logger.LogDebugMessage($"Cleaned up {deadReferences.Count} dead buffer references during periodic maintenance");
                }
            }
            catch (Exception ex) when (!_disposed)
            {
                _logger.LogWarning(ex, "Error during periodic memory cleanup - continuing");
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _disposed = true;

            _logger.LogInfoMessage($"Disposing production memory manager with {_buffers.Count} active buffers");

            // Dispose all active buffers
            foreach (var buffer in _buffers.Values)
            {
                try
                {
                    buffer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing buffer {BufferId}", buffer.Id);
                }
            }

            _buffers.Clear();
            _bufferRegistry.Clear();

            // Dispose memory pool
            _memoryPool?.Dispose();
            _allocationSemaphore?.Dispose();

            _logger.LogInfoMessage("Production memory manager disposed successfully");
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Performs dispose.
    /// </summary>
#pragma warning disable CA1816 // Dispose methods should call GC.SuppressFinalize - This is the correct pattern for public Dispose()
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
#pragma warning restore CA1816
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            _logger.LogInfoMessage($"Disposing production memory manager with {_buffers.Count} active buffers");

            // Dispose all active buffers asynchronously
            var disposeTasks = _buffers.Values.Select(async buffer =>
            {
                try
                {
                    await buffer.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing buffer {BufferId}", buffer.Id);
                }
            }).ToArray();


            await Task.WhenAll(disposeTasks);

            _buffers.Clear();
            _bufferRegistry.Clear();

            // Dispose memory pool
            _memoryPool?.Dispose();
            _allocationSemaphore?.Dispose();

            _logger.LogInfoMessage("Production memory manager disposed successfully");

            await base.DisposeAsync();
        }
    }

    /// <summary>
    /// Backend-specific view creation implementation.
    /// </summary>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        var viewId = Interlocked.Increment(ref _nextId);
        var view = new ProductionMemoryBufferView(viewId, buffer, offset, length, _logger);

        _logger.LogTrace("Created memory buffer view {ViewId} with offset {Offset} and length {Length}",
            viewId, offset, length);

        return view;
    }

    /// <summary>
    /// Backend-specific buffer allocation implementation.
    /// </summary>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        // Delegate to our production-specific allocation logic
        ObjectDisposedException.ThrowIf(_disposed, this);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Limit memory allocation size to prevent excessive memory consumption
        if (sizeInBytes > 16L * 1024 * 1024 * 1024) // 16GB limit
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Allocation size exceeds maximum limit of 16GB");
        }

        await _allocationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var id = Interlocked.Increment(ref _nextId);
            var startTime = Stopwatch.GetTimestamp();

            // Attempt to get buffer from memory pool first
            var pooledBuffer = await _memoryPool.TryGetBufferAsync(sizeInBytes, options, cancellationToken);
            if (pooledBuffer != null)
            {
                var buffer = new ProductionMemoryBuffer(id, sizeInBytes, options, _logger, pooledBuffer, _statistics);
                _ = _buffers.TryAdd(id, buffer);
                _ = _bufferRegistry.TryAdd(id, new WeakReference<ProductionMemoryBuffer>(buffer));

                var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
                _statistics.RecordAllocation(sizeInBytes, elapsedMs, fromPool: true);

                _logger.LogDebugMessage($"Allocated pooled memory buffer {id} with size {sizeInBytes} in {elapsedMs}ms");

                return buffer;
            }

            // Allocate new buffer if pool doesn't have suitable buffer
            var newBuffer = new ProductionMemoryBuffer(id, sizeInBytes, options, _logger, null, _statistics);
            _ = _buffers.TryAdd(id, newBuffer);
            _ = _bufferRegistry.TryAdd(id, new WeakReference<ProductionMemoryBuffer>(newBuffer));

            var allocElapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
            _statistics.RecordAllocation(sizeInBytes, allocElapsedMs, fromPool: false);

            _logger.LogDebugMessage($"Allocated new memory buffer {id} with size {sizeInBytes} in {allocElapsedMs}ms");

            return newBuffer;
        }
        catch (OutOfMemoryException ex)
        {
            Services.Statistics.MemoryStatistics.RecordFailedAllocation(sizeInBytes);
            _logger.LogErrorMessage(ex, $"Failed to allocate memory buffer of size {sizeInBytes} - out of memory");

            // Attempt garbage collection and retry once
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Allow GC to complete before retrying allocation
            await Task.Yield(); // Yield control to allow GC to complete without blocking

            try
            {
                var retryBuffer = new ProductionMemoryBuffer(Interlocked.Increment(ref _nextId), sizeInBytes, options, _logger, null, _statistics);
                _logger.LogInfoMessage("Successfully allocated memory buffer after GC retry");
                return retryBuffer;
            }
            catch (OutOfMemoryException)
            {
                _logger.LogCritical("Failed to allocate memory even after garbage collection - system may be low on memory");
                throw;
            }
        }
        catch (Exception ex)
        {
            Services.Statistics.MemoryStatistics.RecordFailedAllocation(sizeInBytes);
            _logger.LogErrorMessage(ex, $"Unexpected error during memory allocation for size {sizeInBytes}");
            throw new InvalidOperationException($"Memory allocation failed: {ex.Message}", ex);
        }
        finally
        {
            _ = _allocationSemaphore.Release();
        }
    }
}
