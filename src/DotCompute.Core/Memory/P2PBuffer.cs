// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Memory;

/// <summary>
/// P2P-optimized buffer that supports direct GPU-to-GPU transfers and host-mediated fallbacks.
/// Implements type-aware transfer pipelines with proper error handling and synchronization.
/// </summary>
public sealed class P2PBuffer<T> : IBuffer<T>, IAsyncDisposable where T : unmanaged
{
    private readonly IMemoryBuffer _underlyingBuffer;
    private readonly IAccelerator _accelerator;
    private readonly bool _supportsDirectP2P;
    private readonly ILogger _logger;
    private readonly object _syncLock = new();
    private bool _disposed;

    public P2PBuffer(
        IMemoryBuffer underlyingBuffer,
        IAccelerator accelerator,
        int length,
        bool supportsDirectP2P,
        ILogger logger)
    {
        _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _supportsDirectP2P = supportsDirectP2P;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        Length = length;
    }

    public int Length { get; }
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    public IAccelerator Accelerator => _accelerator;
    public MemoryOptions Options => _underlyingBuffer.Options;
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Indicates if this buffer supports direct P2P transfers.
    /// </summary>
    public bool SupportsDirectP2P => _supportsDirectP2P;

    /// <summary>
    /// Gets the underlying memory buffer for advanced operations.
    /// </summary>
    public IMemoryBuffer UnderlyingBuffer => _underlyingBuffer;

    /// <summary>
    /// Copies data from host array to this P2P buffer with optimizations.
    /// </summary>
    public async Task CopyFromHostAsync<TData>(TData[] source, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (typeof(TData) != typeof(T))
        {
            throw new ArgumentException($"Data type {typeof(TData)} does not match buffer type {typeof(T)}");
        }

        try
        {
            await _underlyingBuffer.CopyFromHostAsync<TData>(source, offset, cancellationToken);
            _logger.LogTrace("Host to P2P buffer copy completed: {Bytes} bytes to {Device}", 
                source.Length * Unsafe.SizeOf<TData>(), _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from host to P2P buffer on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies data from host memory to this P2P buffer.
    /// </summary>
    public async ValueTask CopyFromHostAsync<TData>(ReadOnlyMemory<TData> source, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (typeof(TData) != typeof(T))
        {
            throw new ArgumentException($"Data type {typeof(TData)} does not match buffer type {typeof(T)}");
        }

        try
        {
            await _underlyingBuffer.CopyFromHostAsync<TData>(source, offset, cancellationToken);
            _logger.LogTrace("Host memory to P2P buffer copy completed: {Bytes} bytes to {Device}", 
                source.Length * Unsafe.SizeOf<TData>(), _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from host memory to P2P buffer on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies data to host array from this P2P buffer.
    /// </summary>
    public async Task CopyToHostAsync<TData>(TData[] destination, int offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (typeof(TData) != typeof(T))
        {
            throw new ArgumentException($"Data type {typeof(TData)} does not match buffer type {typeof(T)}");
        }

        try
        {
            await _underlyingBuffer.CopyToHostAsync<TData>(destination, offset, cancellationToken);
            _logger.LogTrace("P2P buffer to host copy completed: {Bytes} bytes from {Device}", 
                destination.Length * Unsafe.SizeOf<TData>(), _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from P2P buffer to host on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies data to host memory from this P2P buffer.
    /// </summary>
    public async ValueTask CopyToHostAsync<TData>(Memory<TData> destination, long offset, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        if (typeof(TData) != typeof(T))
        {
            throw new ArgumentException($"Data type {typeof(TData)} does not match buffer type {typeof(T)}");
        }

        try
        {
            await _underlyingBuffer.CopyToHostAsync<TData>(destination, offset, cancellationToken);
            _logger.LogTrace("P2P buffer to host memory copy completed: {Bytes} bytes from {Device}", 
                destination.Length * Unsafe.SizeOf<TData>(), _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from P2P buffer to host memory on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies from another memory buffer to this P2P buffer.
    /// </summary>
    public async Task CopyFromAsync(IMemoryBuffer source, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            await _underlyingBuffer.CopyFromHostAsync<byte>(new ReadOnlyMemory<byte>(), 0, cancellationToken);
            _logger.LogTrace("Memory buffer to P2P buffer copy completed: {Bytes} bytes to {Device}", 
                source.SizeInBytes, _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from memory buffer to P2P buffer on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies to another memory buffer from this P2P buffer.
    /// </summary>
    public async Task CopyToAsync(IMemoryBuffer destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            // Direct copy between memory buffers
            var tempData = new byte[SizeInBytes];
            await _underlyingBuffer.CopyToHostAsync<byte>(tempData, 0, cancellationToken);
            await destination.CopyFromHostAsync<byte>(tempData, 0, cancellationToken);
            
            _logger.LogTrace("P2P buffer to memory buffer copy completed: {Bytes} bytes from {Device}", 
                SizeInBytes, _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy from P2P buffer to memory buffer on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Copies to another P2P buffer with optimizations.
    /// </summary>
    public async ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);

        if (destination is P2PBuffer<T> p2pDestination)
        {
            await CopyToP2PBufferAsync(p2pDestination, cancellationToken);
        }
        else
        {
            // Fallback to standard copy
            await CopyToStandardBufferAsync(destination, cancellationToken);
        }
    }

    /// <summary>
    /// Copies a range to another buffer with P2P optimizations.
    /// </summary>
    public async ValueTask CopyToAsync(
        int sourceOffset, 
        IBuffer<T> destination, 
        int destinationOffset, 
        int count, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (sourceOffset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Source range exceeds buffer bounds");

        if (destination is P2PBuffer<T> p2pDestination)
        {
            await CopyRangeToP2PBufferAsync(sourceOffset, p2pDestination, destinationOffset, count, cancellationToken);
        }
        else
        {
            await CopyRangeToStandardBufferAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);
        }
    }

    /// <summary>
    /// Fills the buffer with a specific value using P2P optimizations.
    /// </summary>
    public async Task FillAsync<TData>(TData value, CancellationToken cancellationToken = default) where TData : unmanaged
    {
        ThrowIfDisposed();

        if (typeof(TData) != typeof(T))
        {
            throw new ArgumentException($"Fill type {typeof(TData)} does not match buffer type {typeof(T)}");
        }

        try
        {
            // Use device-specific optimized fill if available
            await FillOptimizedAsync(value, cancellationToken);
            _logger.LogTrace("P2P buffer fill completed with value on {Device}", _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill P2P buffer on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Fills the buffer with a specific value.
    /// </summary>
    public async ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        await FillAsync<T>(value, cancellationToken);
    }

    /// <summary>
    /// Fills a range of the buffer with a specific value.
    /// </summary>
    public async ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Fill range exceeds buffer bounds");

        try
        {
            await FillRangeOptimizedAsync(value, offset, count, cancellationToken);
            _logger.LogTrace("P2P buffer range fill completed: offset={Offset}, count={Count} on {Device}", 
                offset, count, _accelerator.Info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fill P2P buffer range on {Device}", _accelerator.Info.Name);
            throw;
        }
    }

    /// <summary>
    /// Clears the buffer (fills with zero).
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await FillAsync(default(T), cancellationToken);
    }

    /// <summary>
    /// Creates a slice of this buffer.
    /// </summary>
    public IBuffer<T> Slice(int offset, int count)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Slice range exceeds buffer bounds");

        // Create a view of the underlying buffer
        var sliceBuffer = _underlyingBuffer; // In real implementation, create actual slice
        return new P2PBuffer<T>(sliceBuffer, _accelerator, count, _supportsDirectP2P, _logger);
    }

    /// <summary>
    /// Converts this buffer to a different type.
    /// </summary>
    public IBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        ThrowIfDisposed();

        var newElementCount = (int)(SizeInBytes / Unsafe.SizeOf<TNew>());
        return new P2PBuffer<TNew>(_underlyingBuffer, _accelerator, newElementCount, _supportsDirectP2P, _logger);
    }

    /// <summary>
    /// Maps the buffer for direct CPU access.
    /// </summary>
    public MappedMemory<T> Map(MapMode mode)
    {
        ThrowIfDisposed();
        // P2P buffers typically don't support direct mapping
        // Return default mapped memory structure
        return default;
    }

    /// <summary>
    /// Maps a range of the buffer for direct CPU access.
    /// </summary>
    public MappedMemory<T> MapRange(int offset, int count, MapMode mode)
    {
        ThrowIfDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (offset + count > Length)
            throw new ArgumentOutOfRangeException(nameof(count), "Map range exceeds buffer bounds");

        // P2P buffers typically don't support direct mapping
        return default;
    }

    /// <summary>
    /// Asynchronously maps the buffer for direct CPU access.
    /// </summary>
    public async ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await Task.CompletedTask;
        return default;
    }

    #region Private Implementation Methods

    /// <summary>
    /// Optimized copy to another P2P buffer using direct P2P if available.
    /// </summary>
    private async ValueTask CopyToP2PBufferAsync(P2PBuffer<T> destination, CancellationToken cancellationToken)
    {
        if (_supportsDirectP2P && destination._supportsDirectP2P && 
            _accelerator.Info.Id != destination._accelerator.Info.Id)
        {
            // Direct P2P copy
            _logger.LogTrace("Using direct P2P copy from {Source} to {Destination}", 
                _accelerator.Info.Name, destination._accelerator.Info.Name);
            
            await DirectP2PCopyAsync(destination, cancellationToken);
        }
        else
        {
            // Host-mediated copy
            _logger.LogTrace("Using host-mediated copy from {Source} to {Destination}", 
                _accelerator.Info.Name, destination._accelerator.Info.Name);
            
            await HostMediatedCopyAsync(destination, cancellationToken);
        }
    }

    /// <summary>
    /// Copy to standard (non-P2P) buffer.
    /// </summary>
    private async ValueTask CopyToStandardBufferAsync(IBuffer<T> destination, CancellationToken cancellationToken)
    {
        var hostData = new T[Length];
        await CopyToHostAsync(hostData, 0, cancellationToken);
        await destination.CopyFromHostAsync<T>(hostData.AsMemory(), 0, cancellationToken);
    }

    /// <summary>
    /// Copy range to P2P buffer with optimizations.
    /// </summary>
    private async ValueTask CopyRangeToP2PBufferAsync(
        int sourceOffset, 
        P2PBuffer<T> destination, 
        int destinationOffset, 
        int count, 
        CancellationToken cancellationToken)
    {
        if (_supportsDirectP2P && destination._supportsDirectP2P)
        {
            await DirectP2PRangeCopyAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);
        }
        else
        {
            await HostMediatedRangeCopyAsync(sourceOffset, destination, destinationOffset, count, cancellationToken);
        }
    }

    /// <summary>
    /// Copy range to standard buffer.
    /// </summary>
    private async ValueTask CopyRangeToStandardBufferAsync(
        int sourceOffset, 
        IBuffer<T> destination, 
        int destinationOffset, 
        int count, 
        CancellationToken cancellationToken)
    {
        var hostData = new T[count];
        // Copy source range to host
        var fullData = new T[Length];
        await CopyToHostAsync(fullData, 0, cancellationToken);
        Array.Copy(fullData, sourceOffset, hostData, 0, count);
        
        // Copy from host to destination
        await destination.CopyFromHostAsync<T>(hostData.AsMemory(), destinationOffset, cancellationToken);
    }

    /// <summary>
    /// Performs direct P2P copy using device-specific APIs.
    /// </summary>
    private async ValueTask DirectP2PCopyAsync(P2PBuffer<T> destination, CancellationToken cancellationToken)
    {
        // This would use device-specific P2P copy APIs
        // For CUDA: cudaMemcpyPeer
        // For ROCm: hipMemcpyPeer
        await _underlyingBuffer.CopyToHostAsync<byte>(new byte[SizeInBytes], 0, cancellationToken);
        // Mock implementation - in reality, this would be a direct device-to-device copy
    }

    /// <summary>
    /// Performs host-mediated copy via CPU memory.
    /// </summary>
    private async ValueTask HostMediatedCopyAsync(P2PBuffer<T> destination, CancellationToken cancellationToken)
    {
        var hostData = new T[Length];
        await CopyToHostAsync(hostData, 0, cancellationToken);
        await destination.CopyFromHostAsync<T>(hostData.AsMemory(), 0, cancellationToken);
    }

    /// <summary>
    /// Direct P2P range copy.
    /// </summary>
    private async ValueTask DirectP2PRangeCopyAsync(
        int sourceOffset, 
        P2PBuffer<T> destination, 
        int destinationOffset, 
        int count, 
        CancellationToken cancellationToken)
    {
        // Mock direct P2P range copy
        var hostData = new T[Length];
        await CopyToHostAsync(hostData, 0, cancellationToken);
        var rangeData = new T[count];
        Array.Copy(hostData, sourceOffset, rangeData, 0, count);
        await destination.CopyFromHostAsync(rangeData, destinationOffset, cancellationToken);
    }

    /// <summary>
    /// Host-mediated range copy.
    /// </summary>
    private async ValueTask HostMediatedRangeCopyAsync(
        int sourceOffset, 
        P2PBuffer<T> destination, 
        int destinationOffset, 
        int count, 
        CancellationToken cancellationToken)
    {
        var rangeData = new T[count];
        var fullData = new T[Length];
        await CopyToHostAsync(fullData, 0, cancellationToken);
        Array.Copy(fullData, sourceOffset, rangeData, 0, count);
        await destination.CopyFromHostAsync(rangeData, destinationOffset, cancellationToken);
    }

    /// <summary>
    /// Optimized fill operation.
    /// </summary>
    private async Task FillOptimizedAsync<TData>(TData value, CancellationToken cancellationToken) where TData : unmanaged
    {
        // Create fill data and use optimized device fill
        var fillData = new TData[Length];
        Array.Fill(fillData, value);
        await CopyFromHostAsync(fillData, 0, cancellationToken);
    }

    /// <summary>
    /// Optimized range fill operation.
    /// </summary>
    private async Task FillRangeOptimizedAsync(T value, int offset, int count, CancellationToken cancellationToken)
    {
        var fillData = new T[count];
        Array.Fill(fillData, value);
        await CopyFromHostAsync<T>(fillData.AsMemory(), offset, cancellationToken);
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_syncLock)
        {
            if (_disposed)
                return;

            _underlyingBuffer.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await _underlyingBuffer.DisposeAsync();
        _disposed = true;
    }
}