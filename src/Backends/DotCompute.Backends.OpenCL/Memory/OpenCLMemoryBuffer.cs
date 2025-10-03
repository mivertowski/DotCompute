// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Memory;

/// <summary>
/// OpenCL implementation of memory buffer for device memory management.
/// Provides type-safe access to OpenCL buffer objects with automatic memory management.
/// </summary>
/// <typeparam name="T">The element type stored in the buffer.</typeparam>
internal sealed class OpenCLMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
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
    /// Gets the size of the buffer in bytes (for IUnifiedMemoryBuffer interface).
    /// </summary>
    public long ISizeInBytes => (long)SizeInBytes;

    /// <summary>
    /// Gets the size in bytes (legacy compatibility).
    /// </summary>
    public long Size => (long)SizeInBytes;

    /// <summary>
    /// Gets the length of the buffer in elements.
    /// </summary>
    public int Length => (int)_elementCount;


    /// <summary>
    /// Gets the memory options.
    /// </summary>
    public MemoryOptions Options => new MemoryOptions();

    /// <summary>
    /// Gets the buffer state.
    /// </summary>
    public BufferState State => _disposed ? BufferState.Disposed : BufferState.DeviceValid;

    /// <summary>
    /// Gets the accelerator this buffer is associated with.
    /// </summary>
    public IAccelerator Accelerator => throw new NotSupportedException("Direct accelerator access not supported");

    /// <summary>
    /// Gets whether the buffer is currently available on the host.
    /// </summary>
    public bool IsOnHost => false; // OpenCL buffers are device-based

    /// <summary>
    /// Gets whether the buffer is currently available on the device.
    /// </summary>
    public bool IsOnDevice => !_disposed;

    /// <summary>
    /// Gets whether the buffer has been modified and needs synchronization.
    /// </summary>
    public bool IsDirty => false; // Simplified for now

    // Explicit interface implementation for long SizeInBytes
    long IUnifiedMemoryBuffer.SizeInBytes => ISizeInBytes;

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    // Host Memory Access
    public Span<T> AsSpan()
    {
        // For OpenCL buffers, we need to copy data to host first
        var hostData = new T[Length];
        CopyToHost(hostData);
        return hostData.AsSpan();
    }

    public ReadOnlySpan<T> AsReadOnlySpan() => AsSpan();

    public Memory<T> AsMemory()
    {
        var hostData = new T[Length];
        CopyToHost(hostData);
        return hostData.AsMemory();
    }

    public ReadOnlyMemory<T> AsReadOnlyMemory() => AsMemory();

    // Device Memory Access
    public DeviceMemory GetDeviceMemory() => new DeviceMemory(_buffer.Handle, (long)SizeInBytes);

    // Memory Mapping (simplified implementation for OpenCL)
    public MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite)
    {
        var hostData = new T[Length];
        if (mode == MapMode.ReadWrite || mode == MapMode.Read)
        {
            CopyToHost(hostData);
        }
        return new MappedMemory<T>(hostData);
    }

    public MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        if (offset < 0 || length <= 0 || offset + length > Length)
            throw new ArgumentOutOfRangeException("Invalid range for mapping");

        var hostData = new T[length];
        CopyToHost(hostData, (nuint)offset, (nuint)length);
        return new MappedMemory<T>(hostData);
    }

    public async ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Map(mode), cancellationToken);
    }

    // Synchronization
    public void EnsureOnHost()
    {
        // OpenCL buffers are device-based, so this is a no-op
        // Data transfer happens during AsSpan/AsMemory calls
    }

    public void EnsureOnDevice()
    {
        // OpenCL buffers are already on device
    }

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public void MarkHostDirty()
    {
        // Could track dirty state here in the future
    }

    public void MarkDeviceDirty()
    {
        // Could track dirty state here in the future
    }

    // Type conversion
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        unsafe
        {
            if (sizeof(T) * Length % sizeof(TNew) != 0)
                throw new InvalidOperationException("Buffer size is not compatible with target type");

            var newCount = (sizeof(T) * Length) / sizeof(TNew);

            // Create a new buffer with reinterpreted data
            var newBuffer = new OpenCLMemoryBuffer<TNew>(
                _context,
                (nuint)newCount,
                MemoryFlags.ReadWrite,
                LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(_logger))).CreateLogger<OpenCLMemoryBuffer<TNew>>());

            // Copy raw bytes
            var sourceBytes = new byte[SizeInBytes];
            var sourceTSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(sourceBytes.AsSpan());
            CopyToHost(sourceTSpan);
            var sourceNewSpan = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, TNew>(sourceBytes.AsSpan());
            newBuffer.CopyFromHost(sourceNewSpan);

            return newBuffer;
        }
    }

    // Additional required interface methods
    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        CopyFromHost(source.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        CopyToHost(destination.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        var tempArray = new T[Length];
        CopyToHost(tempArray);
        return destination.CopyFromAsync(tempArray, cancellationToken);
    }

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        var tempArray = new T[count];
        CopyToHost(tempArray, (nuint)sourceOffset, (nuint)count);
        return destination.CopyFromAsync(tempArray.AsMemory(), cancellationToken);
    }

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        Fill(value);
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        Fill(value, (nuint)offset, (nuint)count);
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        return Slice((nuint)offset, (nuint)length);
    }

    // Explicit interface implementations for non-generic interface
    public ValueTask CopyFromAsync<TElement>(ReadOnlyMemory<TElement> source, long offset = 0, CancellationToken cancellationToken = default) where TElement : unmanaged
    {
        if (typeof(TElement) != typeof(T))
            throw new InvalidOperationException("Element type mismatch");

        unsafe
        {
            var typedSource = System.Runtime.InteropServices.MemoryMarshal.Cast<TElement, T>(source.Span);
            CopyFromHost(typedSource, (nuint)(offset / sizeof(T)));
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyToAsync<TElement>(Memory<TElement> destination, long offset = 0, CancellationToken cancellationToken = default) where TElement : unmanaged
    {
        if (typeof(TElement) != typeof(T))
            throw new InvalidOperationException("Element type mismatch");

        unsafe
        {
            var typedDestination = System.Runtime.InteropServices.MemoryMarshal.Cast<TElement, T>(destination.Span);
            CopyToHost(typedDestination, (nuint)(offset / sizeof(T)));
        }
        return ValueTask.CompletedTask;
    }

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

        _logger.LogDebug($"Created OpenCL buffer: type={typeof(T).Name}, elements={elementCount}, size={sizeInBytes} bytes");
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
        IUnifiedMemoryBuffer<T> sourceBuffer,
        nuint sourceOffset = 0,
        nuint destinationOffset = 0,
        nuint? count = null)
    {
        ThrowIfDisposed();

        // Fallback: copy through host memory
        var copyCount = count ?? ((nuint)sourceBuffer.Length - sourceOffset);
        if (destinationOffset + copyCount > _elementCount)
            throw new ArgumentException("Copy operation would exceed destination buffer bounds");

        var tempArray = new T[copyCount];

        // Use the proper interface methods for copying
        if (sourceBuffer is OpenCLMemoryBuffer<T> sourceOpenCLBuffer)
        {
            sourceOpenCLBuffer.CopyToHost(tempArray, sourceOffset, copyCount);
        }
        else
        {
            // Generic fallback - for now throw exception as we can't use async in sync method
            throw new NotSupportedException("Cross-buffer copying is only supported between OpenCL buffers of the same type");
        }

        CopyFromHost(tempArray, destinationOffset, copyCount);

        _logger.LogTrace("Copied {Count} elements from source buffer to OpenCL buffer", copyCount);
    }

    /// <summary>
    /// Asynchronously copies data from another buffer to this buffer.
    /// </summary>
    public async ValueTask CopyFromAsync(
        IUnifiedMemoryBuffer<T> sourceBuffer,
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
    public IUnifiedMemoryBuffer<T> Slice(nuint offset, nuint count)
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

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default) => SynchronizeAsync(cancellationToken);

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

            _logger.LogDebug($"Disposing OpenCL buffer: type={typeof(T).Name}, elements={_elementCount}");

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