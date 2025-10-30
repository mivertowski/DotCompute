// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Memory;

/// <summary>
/// Production memory buffer implementation with comprehensive error handling and performance monitoring.
/// </summary>
public sealed class ProductionMemoryBuffer : IUnifiedMemoryBuffer, IDisposable
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    /// <value>The id.</value>
    public long Id { get; }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; }
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options { get; }
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
    private readonly Statistics.MemoryStatistics _statistics;
    private readonly IntPtr _nativeHandle;
    private readonly GCHandle _pinnedHandle;
    private readonly bool _fromPool;
    private readonly Lock _disposeLock = new();
    /// <summary>
    /// Initializes a new instance of the ProductionMemoryBuffer class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">The options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="pooledHandle">The pooled handle.</param>
    /// <param name="statistics">The statistics.</param>

    public ProductionMemoryBuffer(long id, long sizeInBytes, MemoryOptions options, ILogger logger,
        IntPtr? pooledHandle, Statistics.MemoryStatistics statistics)
    {
        Id = id;
        SizeInBytes = sizeInBytes;
        Options = options;
        _logger = logger;
        _statistics = statistics;
        _fromPool = pooledHandle.HasValue;

        try
        {
            if (pooledHandle.HasValue)
            {
                _nativeHandle = pooledHandle.Value;
            }
            else
            {
                // Allocate pinned memory for device simulation
                var managedBuffer = new byte[sizeInBytes];
                _pinnedHandle = GCHandle.Alloc(managedBuffer, GCHandleType.Pinned);
                _nativeHandle = _pinnedHandle.AddrOfPinnedObject();
            }

            Statistics.MemoryStatistics.RecordBufferCreation(sizeInBytes);
            _logger.LogTrace("Created memory buffer {BufferId} with native handle 0x{Handle:X}", id, _nativeHandle.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create memory buffer {id}");
            throw;
        }
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var sizeInBytes = source.Length * Unsafe.SizeOf<T>();
        if (offset + sizeInBytes > SizeInBytes)
        {
            throw new ArgumentException("Copy operation would exceed buffer size");
        }

        var startTime = Stopwatch.GetTimestamp();

        try
        {
            // Simulate async copy with actual memory operations
            await Task.Run(() =>
            {
                unsafe
                {
                    var sourceSpan = MemoryMarshal.AsBytes(source.Span);
                    var destPtr = (byte*)(_nativeHandle + (int)offset);
                    sourceSpan.CopyTo(new Span<byte>(destPtr, sourceSpan.Length));
                }
            }, cancellationToken);

            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
            _statistics.RecordCopyOperation(sizeInBytes, elapsedMs, isHostToDevice: true);

            _logger.LogTrace("Copied {SizeBytes} bytes to buffer {BufferId} at offset {Offset} in {ElapsedMs:F2}ms",
                sizeInBytes, Id, offset, elapsedMs);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to copy data to buffer {Id}");
            throw;
        }
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public async ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        var sizeInBytes = destination.Length * Unsafe.SizeOf<T>();
        if (offset + sizeInBytes > SizeInBytes)
        {
            throw new ArgumentException("Copy operation would exceed buffer size");
        }

        var startTime = Stopwatch.GetTimestamp();

        try
        {
            // Simulate async copy with actual memory operations
            await Task.Run(() =>
            {
                unsafe
                {
                    var destSpan = MemoryMarshal.AsBytes(destination.Span);
                    var sourcePtr = (byte*)(_nativeHandle + (int)offset);
                    new ReadOnlySpan<byte>(sourcePtr, destSpan.Length).CopyTo(destSpan);
                }
            }, cancellationToken);

            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency;
            _statistics.RecordCopyOperation(sizeInBytes, elapsedMs, isHostToDevice: false);

            _logger.LogTrace("Copied {SizeBytes} bytes from buffer {BufferId} at offset {Offset} in {ElapsedMs:F2}ms",
                sizeInBytes, Id, offset, elapsedMs);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to copy data from buffer {Id}");
            throw;
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                State = BufferState.Disposed;

                try
                {
                    if (!_fromPool && _pinnedHandle.IsAllocated)
                    {
                        _pinnedHandle.Free();
                    }

                    _statistics.RecordBufferDestruction(SizeInBytes);
                    _logger.LogTrace("Disposed memory buffer {BufferId}", Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing memory buffer {BufferId}", Id);
                }
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
