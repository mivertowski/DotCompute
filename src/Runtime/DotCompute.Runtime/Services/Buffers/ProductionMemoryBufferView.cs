// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services.Buffers;

/// <summary>
/// Production memory buffer view implementation.
/// </summary>
public sealed class ProductionMemoryBufferView : IUnifiedMemoryBuffer
{
    public long SizeInBytes { get; }
    public MemoryOptions Options => _parentBuffer.Options;
    public bool IsDisposed { get; private set; }
    public BufferState State => _parentBuffer.State;

    private readonly long _viewId;
    private readonly IUnifiedMemoryBuffer _parentBuffer;
    private readonly long _offset;
    private readonly ILogger _logger;

    public ProductionMemoryBufferView(long viewId, IUnifiedMemoryBuffer parentBuffer, long offset, long length, ILogger logger)
    {
        _viewId = viewId;
        _parentBuffer = parentBuffer;
        _offset = offset;
        SizeInBytes = length;
        _logger = logger;
    }

    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionMemoryBufferView));
        }

        return _parentBuffer.CopyFromAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(ProductionMemoryBufferView));
        }

        return _parentBuffer.CopyToAsync(destination, _offset + offset, cancellationToken);
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            _logger.LogTrace("Disposed memory buffer view {ViewId}", _viewId);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}