// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.OpenCL.Types.Native;

namespace DotCompute.Backends.OpenCL.RingKernels;

/// <summary>
/// Simple read-only wrapper for OpenCL device memory buffers used by message queues.
/// </summary>
/// <remarks>
/// This is a lightweight wrapper that provides access to device memory buffers
/// without the full complexity of IUnifiedMemoryBuffer implementation.
/// </remarks>
public sealed class OpenCLDeviceBufferWrapper : IUnifiedMemoryBuffer
{
    private readonly OpenCLTypes.MemObject _deviceBuffer;
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLDeviceBufferWrapper"/> class.
    /// </summary>
    /// <param name="deviceBuffer">The OpenCL device memory buffer.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    public OpenCLDeviceBufferWrapper(OpenCLTypes.MemObject deviceBuffer, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
    {
        _deviceBuffer = deviceBuffer;
        _sizeInBytes = sizeInBytes;
        _options = options;
    }

    /// <summary>
    /// Gets the OpenCL device buffer handle.
    /// </summary>
    public OpenCLTypes.MemObject DeviceBuffer => _deviceBuffer;

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
        throw new NotSupportedException("OpenCLDeviceBufferWrapper is a read-only wrapper for direct device buffer access. Use OpenCLMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("OpenCLDeviceBufferWrapper is a read-only wrapper for direct device buffer access. Use OpenCLMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Note: We don't release the buffer here as the parent OpenCLMessageQueue owns it
        _disposed = true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
