// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// A Metal memory buffer that is managed by a memory pool and automatically
/// returns to the pool when disposed.
/// </summary>
internal sealed class MetalPooledBuffer : IUnifiedMemoryBuffer
{
    private readonly MetalMemoryPool _pool;
    private readonly int _originalBucketSize;
    private bool _disposed;

    public MetalPooledBuffer(MetalMemoryBuffer underlyingBuffer, MetalMemoryPool pool, int originalBucketSize)
    {
        UnderlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _originalBucketSize = originalBucketSize;
    }

    /// <summary>
    /// Gets the underlying Metal buffer.
    /// </summary>
    public MetalMemoryBuffer UnderlyingBuffer { get; }

    /// <summary>
    /// Gets the original bucket size for pool return.
    /// </summary>
    public int OriginalBucketSize => _originalBucketSize;

    /// <inheritdoc/>
    public long SizeInBytes => UnderlyingBuffer.SizeInBytes;

    /// <inheritdoc/>
    public MemoryOptions Options => UnderlyingBuffer.Options;

    /// <inheritdoc/>
    public bool IsDisposed => _disposed || UnderlyingBuffer.IsDisposed;

    /// <inheritdoc/>
    public BufferState State => _disposed ? BufferState.Disposed : UnderlyingBuffer.State;

    /// <inheritdoc/>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return UnderlyingBuffer.CopyFromAsync(source, offset, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();
        return UnderlyingBuffer.CopyToAsync(destination, offset, cancellationToken);
    }

    /// <summary>
    /// Legacy support method (calls CopyFromAsync).
    /// </summary>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyFromAsync(source, offset, cancellationToken);

    /// <summary>
    /// Legacy support method (calls CopyToAsync).
    /// </summary>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
        => CopyToAsync(destination, offset, cancellationToken);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Return to pool instead of disposing directly
        try
        {
#pragma warning disable VSTHRD002 // Synchronous wait acceptable in Dispose pattern
            // Convert ValueTask to Task before blocking in synchronous Dispose
            _pool.ReturnAsync(this, CancellationToken.None).AsTask().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        catch
        {
            // If return to pool fails, dispose the underlying buffer
            UnderlyingBuffer.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Return to pool instead of disposing directly
        try
        {
            await _pool.ReturnAsync(this, CancellationToken.None);
        }
        catch
        {
            // If return to pool fails, dispose the underlying buffer
            await UnderlyingBuffer.DisposeAsync();
        }
    }
}
