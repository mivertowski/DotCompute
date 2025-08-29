// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Memory;

/// <summary>
/// OpenCL implementation of memory buffer for device memory management.
/// Provides type-safe access to OpenCL buffer objects with automatic memory management.
/// </summary>
/// <typeparam name="T">The element type stored in the buffer.</typeparam>
internal sealed class OpenCLMemoryBuffer<T> : IMemoryBuffer<T>, ISyncMemoryBuffer where T : unmanaged
{
    private readonly OpenCLContext _context;
    private readonly ILogger<OpenCLMemoryBuffer<T>> _logger;
    private readonly object _lock = new();

    private MemObject _buffer;
    private nuint _elementCount;
    private bool _disposed;

    /// <summary>
    /// Gets the OpenCL buffer handle.
    /// </summary>
    public MemObject Buffer => _buffer;

    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    public nuint ElementCount => _elementCount;

    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public unsafe nuint SizeInBytes => _elementCount * (nuint)sizeof(T);

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMemoryBuffer{T}"/> class.
    /// </summary>
    /// <param name="context">The OpenCL context.</param>
    /// <param name="elementCount">Number of elements in the buffer.</param>
    /// <param name="flags">Memory flags for buffer creation.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLMemoryBuffer(
        OpenCLContext context, 
        nuint elementCount, 
        MemoryFlags flags,
        ILogger<OpenCLMemoryBuffer<T>> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _elementCount = elementCount;

        if (elementCount == 0)
            throw new ArgumentException("Element count must be greater than zero", nameof(elementCount));

        nuint sizeInBytes;
        unsafe
        {
            sizeInBytes = elementCount * (nuint)sizeof(T);
        }
        _buffer = _context.CreateBuffer(flags, sizeInBytes);

        _logger.LogDebug("Created OpenCL buffer: type={Type}, elements={Count}, size={Size} bytes",
            typeof(T).Name, elementCount, sizeInBytes);
    }

    /// <summary>
    /// Copies data from host memory to the device buffer.
    /// </summary>
    /// <param name="hostData">Source data array.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to copy.</param>
    public void CopyFromHost(ReadOnlySpan<T> hostData, nuint offset = 0, nuint? count = null)
    {
        ThrowIfDisposed();

        var copyCount = count ?? (nuint)hostData.Length;
        if (offset + copyCount > _elementCount)
            throw new ArgumentException("Copy operation would exceed buffer bounds");

        unsafe
        {
            fixed (T* ptr = hostData)
            {
                var byteOffset = offset * (nuint)sizeof(T);
                var byteSize = copyCount * (nuint)sizeof(T);
                
                // For simplicity, use blocking write
                _context.EnqueueWriteBuffer(_buffer, (nint)ptr + (int)byteOffset, byteSize, true);
            }
        }

        _logger.LogTrace("Copied {Count} elements from host to OpenCL buffer at offset {Offset}",
            copyCount, offset);
    }

    /// <summary>
    /// Copies data from the device buffer to host memory.
    /// </summary>
    /// <param name="hostData">Target data array.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to copy.</param>
    public void CopyToHost(Span<T> hostData, nuint offset = 0, nuint? count = null)
    {
        ThrowIfDisposed();

        var copyCount = count ?? (nuint)hostData.Length;
        if (offset + copyCount > _elementCount)
            throw new ArgumentException("Copy operation would exceed buffer bounds");

        if (copyCount > (nuint)hostData.Length)
            throw new ArgumentException("Host buffer is too small for requested copy");

        unsafe
        {
            fixed (T* ptr = hostData)
            {
                var byteOffset = offset * (nuint)sizeof(T);
                var byteSize = copyCount * (nuint)sizeof(T);
                
                // For simplicity, use blocking read
                _context.EnqueueReadBuffer(_buffer, (nint)ptr + (int)byteOffset, byteSize, true);
            }
        }

        _logger.LogTrace("Copied {Count} elements from OpenCL buffer to host at offset {Offset}",
            copyCount, offset);
    }

    /// <summary>
    /// Asynchronously copies data from host memory to the device buffer.
    /// </summary>
    /// <param name="hostData">Source data array.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask CopyFromHostAsync(
        ReadOnlyMemory<T> hostData, 
        nuint offset = 0, 
        nuint? count = null, 
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyFromHost(hostData.Span, offset, count), cancellationToken);
    }

    /// <summary>
    /// Asynchronously copies data from the device buffer to host memory.
    /// </summary>
    /// <param name="hostData">Target data array.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask CopyToHostAsync(
        Memory<T> hostData, 
        nuint offset = 0, 
        nuint? count = null, 
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyToHost(hostData.Span, offset, count), cancellationToken);
    }

    /// <summary>
    /// Fills the buffer with a specific value.
    /// </summary>
    /// <param name="value">Value to fill with.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to fill.</param>
    public void Fill(T value, nuint offset = 0, nuint? count = null)
    {
        ThrowIfDisposed();

        var fillCount = count ?? (_elementCount - offset);
        if (offset + fillCount > _elementCount)
            throw new ArgumentException("Fill operation would exceed buffer bounds");

        // For simplicity, create host array and copy to device
        // In a production implementation, you might use clEnqueueFillBuffer if available
        var fillArray = new T[fillCount];
        Array.Fill(fillArray, value);
        CopyFromHost(fillArray, offset, fillCount);

        _logger.LogTrace("Filled {Count} elements in OpenCL buffer with value at offset {Offset}",
            fillCount, offset);
    }

    /// <summary>
    /// Asynchronously fills the buffer with a specific value.
    /// </summary>
    /// <param name="value">Value to fill with.</param>
    /// <param name="offset">Offset in the buffer (in elements).</param>
    /// <param name="count">Number of elements to fill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask FillAsync(
        T value, 
        nuint offset = 0, 
        nuint? count = null, 
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Fill(value, offset, count), cancellationToken);
    }

    /// <summary>
    /// Copies data from another buffer to this buffer.
    /// </summary>
    /// <param name="sourceBuffer">Source buffer to copy from.</param>
    /// <param name="sourceOffset">Offset in the source buffer (in elements).</param>
    /// <param name="destinationOffset">Offset in this buffer (in elements).</param>
    /// <param name="count">Number of elements to copy.</param>
    public void CopyFrom(
        IMemoryBuffer<T> sourceBuffer, 
        nuint sourceOffset = 0, 
        nuint destinationOffset = 0, 
        nuint? count = null)
    {
        ThrowIfDisposed();

        if (sourceBuffer is OpenCLMemoryBuffer<T> openclBuffer)
        {
            // Direct OpenCL buffer to buffer copy could be implemented here
            // For now, use host memory as intermediate
        }

        // Fallback: copy through host memory
        var copyCount = count ?? (sourceBuffer.ElementCount - sourceOffset);
        if (destinationOffset + copyCount > _elementCount)
            throw new ArgumentException("Copy operation would exceed destination buffer bounds");

        var tempArray = new T[copyCount];
        sourceBuffer.CopyToHost(tempArray, sourceOffset, copyCount);
        CopyFromHost(tempArray, destinationOffset, copyCount);

        _logger.LogTrace("Copied {Count} elements from source buffer to OpenCL buffer", copyCount);
    }

    /// <summary>
    /// Asynchronously copies data from another buffer to this buffer.
    /// </summary>
    public async ValueTask CopyFromAsync(
        IMemoryBuffer<T> sourceBuffer, 
        nuint sourceOffset = 0, 
        nuint destinationOffset = 0, 
        nuint? count = null, 
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyFrom(sourceBuffer, sourceOffset, destinationOffset, count), cancellationToken);
    }

    /// <summary>
    /// Gets a slice of this buffer.
    /// </summary>
    /// <param name="offset">Offset in elements.</param>
    /// <param name="count">Number of elements.</param>
    /// <returns>A buffer slice.</returns>
    public IMemoryBuffer<T> Slice(nuint offset, nuint count)
    {
        ThrowIfDisposed();

        if (offset + count > _elementCount)
            throw new ArgumentException("Slice would exceed buffer bounds");

        // For simplicity, return a new buffer with copied data
        // In a production implementation, you might create a view/slice without copying
        var sliceBuffer = new OpenCLMemoryBuffer<T>(_context, count, MemoryFlags.ReadWrite, _logger);
        sliceBuffer.CopyFrom(this, offset, 0, count);
        return sliceBuffer;
    }

    /// <summary>
    /// Synchronizes the buffer with the device (no-op for OpenCL as operations are queued).
    /// </summary>
    public void Synchronize()
    {
        ThrowIfDisposed();
        _context.Finish(); // Ensure all operations are complete
    }

    /// <summary>
    /// Asynchronously synchronizes the buffer with the device.
    /// </summary>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(Synchronize, cancellationToken);
    }

    /// <summary>
    /// Throws if this buffer has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OpenCLMemoryBuffer<T>));
    }

    /// <summary>
    /// Disposes the OpenCL buffer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            _logger.LogDebug("Disposing OpenCL buffer: type={Type}, elements={Count}",
                typeof(T).Name, _elementCount);

            OpenCLContext.ReleaseObject(_buffer.Handle, OpenCLRuntime.clReleaseMemObject, "memory buffer");
            _disposed = true;
        }
    }

    /// <summary>
    /// Asynchronously disposes the OpenCL buffer.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}