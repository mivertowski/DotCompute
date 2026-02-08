// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Backends.CUDA.RingKernels;

/// <summary>
/// Simple read-only wrapper for CUDA device memory pointers used by message queues.
/// </summary>
/// <remarks>
/// This is a lightweight wrapper that provides access to device memory pointers
/// without the full complexity of IUnifiedMemoryBuffer implementation.
/// </remarks>
public sealed class CudaDevicePointerBuffer : IUnifiedMemoryBuffer
{
    private readonly IntPtr _devicePointer;
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaDevicePointerBuffer"/> class.
    /// </summary>
    /// <param name="devicePointer">The device memory pointer.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    public CudaDevicePointerBuffer(IntPtr devicePointer, long sizeInBytes, MemoryOptions options = MemoryOptions.None)
    {
        _devicePointer = devicePointer;
        _sizeInBytes = sizeInBytes;
        _options = options;
    }

    /// <summary>
    /// Gets the device memory pointer.
    /// </summary>
    public IntPtr DevicePointer => _devicePointer;

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
        throw new NotSupportedException("CudaDevicePointerBuffer is a read-only wrapper for direct device pointer access. Use CudaMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        throw new NotSupportedException("CudaDevicePointerBuffer is a read-only wrapper for direct device pointer access. Use CudaMessageQueue methods for data transfer.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Note: We don't free memory here as the parent CudaMessageQueue owns the memory
        _disposed = true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
