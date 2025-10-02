// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Buffers;

/// <summary>
/// Production memory buffer view implementation.
/// </summary>
public sealed class ProductionMemoryBufferView(long viewId, IUnifiedMemoryBuffer parentBuffer, long offset, long length, ILogger logger) : IUnifiedMemoryBuffer
{
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>
    public long SizeInBytes { get; } = length;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _parentBuffer.Options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed { get; private set; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _parentBuffer.State;

    private readonly long _viewId = viewId;
    private readonly IUnifiedMemoryBuffer _parentBuffer = parentBuffer;
    private readonly long _offset = offset;
    private readonly ILogger _logger = logger;
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
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionMemoryBufferView));
        }

        return _parentBuffer.CopyFromAsync(source, _offset + offset, cancellationToken);
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
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionMemoryBufferView));
        }

        return _parentBuffer.CopyToAsync(destination, _offset + offset, cancellationToken);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            _logger.LogTrace("Disposed memory buffer view {ViewId}", _viewId);
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