// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.Metal.RingKernels;

/// <summary>
/// Simple read-only wrapper for Metal device memory buffers used by message queues.
/// </summary>
/// <remarks>
/// This is a lightweight wrapper that provides access to device memory buffers
/// without the full complexity of IUnifiedMemoryBuffer implementation.
/// Metal uses MTLBuffer objects instead of raw pointers like CUDA.
/// </remarks>
public sealed class MetalDeviceBufferWrapper : IUnifiedMemoryBuffer
{
    private readonly IntPtr _metalBuffer;
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalDeviceBufferWrapper"/> class.
    /// </summary>
    /// <param name="metalBuffer">The Metal buffer pointer (MTLBuffer*).</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    public MetalDeviceBufferWrapper(IntPtr metalBuffer, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
    {
        _metalBuffer = metalBuffer;
        _sizeInBytes = sizeInBytes;
        _options = options;
    }

    /// <summary>
    /// Gets the Metal buffer pointer (MTLBuffer*).
    /// </summary>
    public IntPtr MetalBuffer => _metalBuffer;

    /// <inheritdoc/>
    public long SizeInBytes => _sizeInBytes;

    /// <inheritdoc/>
    public MemoryOptions Options => _options;

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public BufferState State => _disposed ? BufferState.Disposed : BufferState.Allocated;

    /// <inheritdoc/>
    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("MetalDeviceBufferWrapper is a read-only wrapper for direct buffer access. Use MetalMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("MetalDeviceBufferWrapper is a read-only wrapper for direct buffer access. Use MetalMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Note: We don't release the buffer here as the parent MetalMessageQueue owns the buffer
        _disposed = true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
