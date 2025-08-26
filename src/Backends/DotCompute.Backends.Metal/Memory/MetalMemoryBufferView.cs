// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal-specific memory buffer view implementation.
/// </summary>
public sealed class MetalMemoryBufferView : IUnifiedMemoryBuffer
{
    private readonly MetalMemoryBuffer _parent;
    private readonly long _offset;
    private readonly long _length;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryBufferView"/> class.
    /// </summary>
    /// <param name="parent">The parent buffer.</param>
    /// <param name="offset">The offset in the parent buffer.</param>
    /// <param name="length">The length of the view.</param>
    public MetalMemoryBufferView(MetalMemoryBuffer parent, long offset, long length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
    }

    /// <inheritdoc/>
    public long SizeInBytes => _length;

    /// <inheritdoc/>
    public MemoryOptions Options => _parent.Options;

    /// <inheritdoc/>
    public bool IsDisposed => _parent.IsDisposed;

    /// <inheritdoc/>
    public BufferState State => _parent.State;

    /// <inheritdoc/>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyFromHostAsync(source, _offset + offset, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyToHostAsync(destination, _offset + offset, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Views don't dispose the parent buffer
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Views don't dispose the parent buffer
        return ValueTask.CompletedTask;
    }
}