// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.Metal.Memory;

/// <summary>
/// Metal-specific memory buffer implementation.
/// </summary>
public sealed class MetalMemoryBuffer : IUnifiedMemoryBuffer
{
    /// <inheritdoc/>
    public long SizeInBytes { get; }

    /// <inheritdoc/>
    public MemoryOptions Options { get; }

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    public BufferState State { get; private set; }

    /// <summary>
    /// Gets the native Metal buffer handle.
    /// </summary>
    public IntPtr Buffer { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalMemoryBuffer"/> class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory options.</param>
    public MetalMemoryBuffer(long sizeInBytes, MemoryOptions options)
    {
        SizeInBytes = sizeInBytes;
        Options = options;
        State = BufferState.Allocated;
        Buffer = IntPtr.Zero; // TODO: Allocate actual Metal buffer
    }

    /// <inheritdoc/>
    public ValueTask CopyFromHostAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Implementation placeholder
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask CopyToHostAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        // Implementation placeholder
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!IsDisposed)
        {
            State = BufferState.Disposed;
            IsDisposed = true;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}