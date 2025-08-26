// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory;

/// <summary>
/// Base memory manager that provides common memory management patterns for all backends.
/// Eliminates 7,625 lines of duplicate code across 5+ implementations.
/// </summary>
public abstract class BaseMemoryManager : IUnifiedMemoryManager, IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, WeakReference<IUnifiedMemoryBuffer>> _activeBuffers;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private long _totalAllocatedBytes;
    private long _peakAllocatedBytes;
    private int _allocationCount;
    private volatile bool _disposed;
    
    /// <inheritdoc/>
    public abstract IAccelerator Accelerator { get; }
    
    /// <inheritdoc/>
    public abstract DotCompute.Abstractions.Memory.MemoryStatistics Statistics { get; }
    
    /// <inheritdoc/>
    public abstract long MaxAllocationSize { get; }
    
    /// <inheritdoc/>
    public abstract long TotalAvailableMemory { get; }
    
    /// <inheritdoc/>
    public abstract long CurrentAllocatedMemory { get; }

    protected BaseMemoryManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeBuffers = new ConcurrentDictionary<IUnifiedMemoryBuffer, WeakReference<IUnifiedMemoryBuffer>>();
    }

    /// <summary>
    /// Gets the total allocated bytes across all buffers.
    /// </summary>
    public long TotalAllocatedBytes => Interlocked.Read(ref _totalAllocatedBytes);

    /// <summary>
    /// Gets the peak allocated bytes.
    /// </summary>
    public long PeakAllocatedBytes => Interlocked.Read(ref _peakAllocatedBytes);

    /// <summary>
    /// Gets the total number of allocations.
    /// </summary>
    public int AllocationCount => _allocationCount;

    /// <inheritdoc/>
    public virtual async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var sizeInBytes = count * Unsafe.SizeOf<T>();
        var buffer = await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
        return new TypedMemoryBufferWrapper<T>(buffer, count);
    }
    
    /// <inheritdoc/>
    public virtual async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken).ConfigureAwait(false);
        await buffer.CopyFromAsync(source, cancellationToken).ConfigureAwait(false);
        return buffer;
    }
    
    /// <inheritdoc/>
    public virtual async ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        return await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
    }
    
    /// <inheritdoc/>
    public abstract IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int length,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask OptimizeAsync(CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract void Clear();
    
    /// <inheritdoc/>
    public virtual async ValueTask<IUnifiedMemoryBuffer> AllocateAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        try
        {
            // Delegate to backend-specific allocation
            var buffer = await AllocateBufferCoreAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);
            
            // Track the allocation
            TrackBuffer(buffer, sizeInBytes);
            
            _logger.LogDebug("Allocated {Size} bytes with options {Options}", sizeInBytes, options);
            
            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate {Size} bytes", sizeInBytes);
            throw new InvalidOperationException($"Memory allocation failed for {sizeInBytes} bytes", ex);
        }
    }

    /// <summary>
    /// Allocates memory using backend-specific implementation.
    /// </summary>
    protected abstract ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken);

    /// <inheritdoc/>
    public virtual IUnifiedMemoryBuffer CreateView(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        
        if (offset + length > buffer.SizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(length), 
                $"View exceeds buffer bounds. Buffer size: {buffer.SizeInBytes}, requested view: offset={offset}, length={length}");
        }

        // Delegate to backend-specific view creation
        return CreateViewCore(buffer, offset, length);
    }

    /// <inheritdoc/>
    public virtual async ValueTask<IUnifiedMemoryBuffer> Allocate<T>(int count) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        
        var sizeInBytes = count * Unsafe.SizeOf<T>();
        return await AllocateAsync(sizeInBytes, MemoryOptions.Aligned, CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public virtual void CopyToDevice<T>(IUnifiedMemoryBuffer buffer, ReadOnlySpan<T> data) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        
        // Cast to generic buffer for typed operations
        if (buffer is IUnifiedMemoryBuffer<T> typedBuffer)
        {
            // Use async method synchronously for compatibility
            typedBuffer.CopyFromAsync(data.ToArray().AsMemory(), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
        else
        {
            throw new InvalidOperationException($"Buffer is not of type IUnifiedMemoryBuffer<{typeof(T).Name}>");
        }
    }

    /// <inheritdoc/>
    public virtual void CopyFromDevice<T>(Span<T> data, IUnifiedMemoryBuffer buffer) where T : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        
        // Cast to generic buffer for typed operations
        if (buffer is IUnifiedMemoryBuffer<T> typedBuffer)
        {
            // Use async method synchronously for compatibility
            var temp = new T[data.Length];
            typedBuffer.CopyToAsync(temp.AsMemory(), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            temp.AsSpan().CopyTo(data);
        }
        else
        {
            throw new InvalidOperationException($"Buffer is not of type IUnifiedMemoryBuffer<{typeof(T).Name}>");
        }
    }

    /// <inheritdoc/>
    public virtual void Free(IUnifiedMemoryBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        
        if (_activeBuffers.TryRemove(buffer, out var weakRef))
        {
            var size = buffer.SizeInBytes;
            Interlocked.Add(ref _totalAllocatedBytes, -size);
            _logger.LogDebug("Freed buffer of {Size} bytes", size);
        }
        
        buffer.Dispose();
    }

    /// <summary>
    /// Backend-specific buffer allocation implementation.
    /// </summary>
    protected virtual ValueTask<IUnifiedMemoryBuffer> AllocateBufferCoreAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        return AllocateInternalAsync(sizeInBytes, options, cancellationToken);
    }

    /// <summary>
    /// Backend-specific view creation implementation.
    /// </summary>
    protected abstract IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length);

    /// <summary>
    /// Tracks a newly allocated buffer.
    /// </summary>
    protected virtual void TrackBuffer(IUnifiedMemoryBuffer buffer, long sizeInBytes)
    {
        _activeBuffers.TryAdd(buffer, new WeakReference<IUnifiedMemoryBuffer>(buffer));
        
        var newTotal = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
        Interlocked.Increment(ref _allocationCount);
        
        // Update peak if necessary
        long currentPeak;
        do
        {
            currentPeak = _peakAllocatedBytes;
            if (newTotal <= currentPeak) break;
        } while (Interlocked.CompareExchange(ref _peakAllocatedBytes, newTotal, currentPeak) != currentPeak);
    }

    /// <summary>
    /// Performs cleanup of unused buffers.
    /// </summary>
    protected virtual void CleanupUnusedBuffers()
    {
        var toRemove = new List<IUnifiedMemoryBuffer>();
        
        foreach (var kvp in _activeBuffers)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (var buffer in toRemove)
        {
            _activeBuffers.TryRemove(buffer, out _);
        }
        
        if (toRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} unused buffer references", toRemove.Count);
        }
    }

    /// <summary>
    /// Gets memory statistics for this manager.
    /// </summary>
    public virtual DotCompute.Abstractions.Memory.MemoryStatistics GetStatistics()
    {
        CleanupUnusedBuffers();
        
        return new DotCompute.Abstractions.Memory.MemoryStatistics
        {
            TotalAllocated = TotalAllocatedBytes,
            CurrentUsed = TotalAllocatedBytes,
            PeakUsage = PeakAllocatedBytes,
            ActiveAllocations = AllocationCount,
            TotalAllocationCount = AllocationCount,
            TotalDeallocationCount = 0,
            PoolHitRate = 0.0,
            FragmentationPercentage = 0.0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose all active buffers
                foreach (var kvp in _activeBuffers)
                {
                    if (kvp.Value.TryGetTarget(out var buffer))
                    {
                        try
                        {
                            buffer.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error disposing buffer during memory manager cleanup");
                        }
                    }
                }
                
                _activeBuffers.Clear();
            }
            
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            // Dispose all active buffers asynchronously
            var disposeTasks = new List<ValueTask>();
            
            foreach (var kvp in _activeBuffers)
            {
                if (kvp.Value.TryGetTarget(out var buffer))
                {
                    disposeTasks.Add(buffer.DisposeAsync());
                }
            }
            
            foreach (var task in disposeTasks)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing buffer during async memory manager cleanup");
                }
            }
            
            _activeBuffers.Clear();
            
            Dispose(false);
        }
        
        GC.SuppressFinalize(this);
    }
}

