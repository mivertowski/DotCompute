// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Memory;

/// <summary>
/// OpenCL implementation of the unified memory manager interface.
/// Manages memory allocations and transfers for OpenCL devices.
/// </summary>
internal sealed class OpenCLMemoryManager : IUnifiedMemoryManager
{
    private readonly OpenCLContext _context;
    private readonly ILogger<OpenCLMemoryManager> _logger;
    private readonly object _lock = new();

    private readonly ConcurrentDictionary<nint, IUnifiedMemoryBuffer> _allocatedBuffers = new();
    private long _currentAllocatedMemory;
    private bool _disposed;

    /// <summary>
    /// Gets the accelerator this memory manager is associated with.
    /// </summary>
    public IAccelerator Accelerator { get; }

    /// <summary>
    /// Gets memory usage statistics.
    /// </summary>
    public MemoryStatistics Statistics => new MemoryStatistics
    {
        TotalAllocated = _currentAllocatedMemory,
        TotalAvailable = (long)_context.DeviceInfo.GlobalMemorySize,
        AllocationCount = _allocatedBuffers.Count,
        PeakMemoryUsage = _allocatedBuffers.Values
            .Where(b => !b.IsDisposed)
            .Max(b => (long?)b.SizeInBytes) ?? 0
    };

    /// <summary>
    /// Gets the maximum memory allocation size in bytes.
    /// </summary>
    public long MaxAllocationSize => (long)_context.DeviceInfo.MaxMemoryAllocationSize;

    /// <summary>
    /// Gets the total available memory in bytes.
    /// </summary>
    public long TotalAvailableMemory => (long)_context.DeviceInfo.GlobalMemorySize;

    /// <summary>
    /// Gets the current allocated memory in bytes.
    /// </summary>
    public long CurrentAllocatedMemory => Interlocked.Read(ref _currentAllocatedMemory);

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLMemoryManager"/> class.
    /// </summary>
    /// <param name="accelerator">The parent accelerator.</param>
    /// <param name="context">The OpenCL context.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public OpenCLMemoryManager(
        IAccelerator accelerator,
        OpenCLContext context,
        ILogger<OpenCLMemoryManager> logger)
    {
        Accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("Created OpenCL memory manager for device: {_context.DeviceInfo.Name}");
    }

    /// <summary>
    /// Allocates a memory buffer for a specific number of elements.
    /// </summary>
    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));

        var elementCount = (nuint)count;
        nuint sizeInBytes;
        unsafe
        {
            sizeInBytes = elementCount * (nuint)sizeof(T);
        }

        // Check allocation limits
        if ((long)sizeInBytes > MaxAllocationSize)
            throw new OutOfMemoryException($"Requested allocation size {sizeInBytes} exceeds maximum {MaxAllocationSize}");

        _logger.LogDebug($"Allocating OpenCL buffer: type={typeof(T).Name}, count={count}, size={sizeInBytes} bytes");

        var flags = DetermineMemoryFlags(options);
        var buffer = new OpenCLMemoryBuffer<T>(
            _context,
            elementCount,
            flags,
            LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(_logger))).CreateLogger<OpenCLMemoryBuffer<T>>());

        // Track allocation
        _allocatedBuffers[buffer.Buffer.Handle] = buffer;
        Interlocked.Add(ref _currentAllocatedMemory, (long)sizeInBytes);

        _logger.LogTrace("Successfully allocated OpenCL buffer: {Handle}, total allocated: {Total} bytes",
            buffer.Buffer.Handle, CurrentAllocatedMemory);

        return ValueTask.FromResult<IUnifiedMemoryBuffer<T>>(buffer);
    }

    /// <summary>
    /// Allocates memory and copies data from host.
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAndCopyAsync<T>(
        ReadOnlyMemory<T> source,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        var buffer = await AllocateAsync<T>(source.Length, options, cancellationToken);
        await buffer.CopyFromAsync(source, cancellationToken);
        return buffer;
    }

    /// <summary>
    /// Allocates memory by size in bytes (for advanced scenarios).
    /// </summary>
    public async ValueTask<IUnifiedMemoryBuffer> AllocateRawAsync(
        long sizeInBytes,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));

        if (sizeInBytes > MaxAllocationSize)
            throw new OutOfMemoryException($"Requested allocation size {sizeInBytes} exceeds maximum {MaxAllocationSize}");

        // Allocate as byte buffer
        var byteCount = (int)sizeInBytes;
        return await AllocateAsync<byte>(byteCount, options, cancellationToken);
    }

    /// <summary>
    /// Creates a view over existing memory.
    /// </summary>
    public IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length) where T : unmanaged
    {
        ThrowIfDisposed();

        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        if (offset < 0 || length <= 0 || offset + length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid view range");

        // For OpenCL, we create a slice (which creates a copy for simplicity)
        // In a production implementation, you might create a sub-buffer
        return buffer.Slice(offset, length);
    }

    /// <summary>
    /// Copies data between buffers.
    /// </summary>
    public async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await destination.CopyToAsync(source, cancellationToken);
    }

    /// <summary>
    /// Copies data between buffers with specified ranges.
    /// </summary>
    public async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await destination.CopyToAsync(sourceOffset, source, destinationOffset, count, cancellationToken);
    }

    /// <summary>
    /// Copies data from host memory to a device buffer.
    /// </summary>
    public async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await destination.CopyFromAsync(source, cancellationToken);
    }

    /// <summary>
    /// Copies data from a device buffer to host memory.
    /// </summary>
    public async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Frees a memory buffer.
    /// </summary>
    public void Free(IUnifiedMemoryBuffer buffer)
    {
        if (buffer == null || buffer.IsDisposed)
            return;

        var sizeInBytes = buffer.SizeInBytes;

        // Remove from tracking
        if (buffer is OpenCLMemoryBuffer<byte> byteBuffer)
        {
            _allocatedBuffers.TryRemove(byteBuffer.Buffer.Handle, out _);
        }
        // Try other common types
        else if (buffer.GetType().IsGenericType)
        {
            var property = buffer.GetType().GetProperty("Buffer");
            if (property?.GetValue(buffer) is MemObject clBuffer)
            {
                _allocatedBuffers.TryRemove(clBuffer.Handle, out _);
            }
        }

        // Update memory tracking
        Interlocked.Add(ref _currentAllocatedMemory, -sizeInBytes);

        buffer.Dispose();

        _logger.LogTrace("Freed OpenCL buffer: size={Size}, remaining allocated: {Remaining} bytes",
            sizeInBytes, CurrentAllocatedMemory);
    }

    /// <summary>
    /// Asynchronously frees a memory buffer.
    /// </summary>
    public async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Free(buffer), cancellationToken);
    }

    /// <summary>
    /// Optimizes memory by defragmenting and releasing unused memory.
    /// </summary>
    public async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            // Clean up disposed buffers
            var disposedBuffers = _allocatedBuffers
                .Where(kvp => kvp.Value.IsDisposed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var handle in disposedBuffers)
            {
                _allocatedBuffers.TryRemove(handle, out _);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _logger.LogDebug($"Memory optimization completed: removed {disposedBuffers.Count} disposed buffer references");
        }, cancellationToken);
    }

    /// <summary>
    /// Clears all allocated memory and resets the manager.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            _logger.LogInformation("Clearing all OpenCL memory allocations");

            // Dispose all tracked buffers
            foreach (var buffer in _allocatedBuffers.Values.ToArray())
            {
                try
                {
                    buffer?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing buffer during clear operation");
                }
            }

            _allocatedBuffers.Clear();
            Interlocked.Exchange(ref _currentAllocatedMemory, 0);

            _logger.LogInformation("All OpenCL memory cleared");
        }
    }

    // Device-specific Operations (Legacy Support)

    /// <summary>
    /// Allocates device-specific memory.
    /// </summary>
    public DeviceMemory AllocateDevice(long sizeInBytes)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));

        var memObject = _context.CreateBuffer(MemoryFlags.ReadWrite, (nuint)sizeInBytes);
        Interlocked.Add(ref _currentAllocatedMemory, sizeInBytes);

        return new DeviceMemory(memObject.Handle, sizeInBytes);
    }

    /// <summary>
    /// Frees device-specific memory.
    /// </summary>
    public void FreeDevice(DeviceMemory deviceMemory)
    {
        ThrowIfDisposed();

        if (deviceMemory.Handle != IntPtr.Zero)
        {
            OpenCLContext.ReleaseObject(deviceMemory.Handle, OpenCLRuntime.clReleaseMemObject, "device memory");
            Interlocked.Add(ref _currentAllocatedMemory, -deviceMemory.Size);
        }
    }

    /// <summary>
    /// Sets device memory to a specific value.
    /// </summary>
    public void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
    {
        ThrowIfDisposed();

        unsafe
        {
            var pattern = stackalloc byte[1];
            pattern[0] = value;
            var error = OpenCLRuntime.clEnqueueFillBuffer(
                _context.CommandQueue.Handle,
                deviceMemory.Handle,
                (IntPtr)pattern,
                (nuint)1,
                (nuint)0,
                (nuint)sizeInBytes,
                0,
                null,
                IntPtr.Zero);
            OpenCLException.ThrowIfError(error, "Fill device buffer");
        }
    }

    /// <summary>
    /// Asynchronously sets device memory to a specific value.
    /// </summary>
    public async ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => MemsetDevice(deviceMemory, value, sizeInBytes), cancellationToken);
    }

    /// <summary>
    /// Copies data from host to device memory.
    /// </summary>
    public void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
    {
        ThrowIfDisposed();

        var error = OpenCLRuntime.clEnqueueWriteBuffer(
            _context.CommandQueue.Handle,
            deviceMemory.Handle,
            1u, // blocking write
            (nuint)0,
            (nuint)sizeInBytes,
            hostPointer,
            0,
            null,
            out _);
        OpenCLException.ThrowIfError(error, "Copy host to device");
    }

    /// <summary>
    /// Copies data from device to host memory.
    /// </summary>
    public void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
    {
        ThrowIfDisposed();

        var error = OpenCLRuntime.clEnqueueReadBuffer(
            _context.CommandQueue.Handle,
            deviceMemory.Handle,
            1u, // blocking read
            (nuint)0,
            (nuint)sizeInBytes,
            hostPointer,
            0,
            null,
            out _);
        OpenCLException.ThrowIfError(error, "Copy device to host");
    }

    /// <summary>
    /// Asynchronously copies data from host to device memory.
    /// </summary>
    public async ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyHostToDevice(hostPointer, deviceMemory, sizeInBytes), cancellationToken);
    }

    /// <summary>
    /// Asynchronously copies data from device to host memory.
    /// </summary>
    public async ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyDeviceToHost(deviceMemory, hostPointer, sizeInBytes), cancellationToken);
    }

    /// <summary>
    /// Copies data between device memories.
    /// </summary>
    public void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
    {
        ThrowIfDisposed();

        var error = OpenCLRuntime.clEnqueueCopyBuffer(
            _context.CommandQueue.Handle,
            sourceDevice.Handle,
            destinationDevice.Handle,
            (nuint)0,
            (nuint)0,
            (nuint)sizeInBytes,
            0,
            null,
            IntPtr.Zero);
        OpenCLException.ThrowIfError(error, "Copy device to device");
    }

    /// <summary>
    /// Determines OpenCL memory flags based on memory options.
    /// </summary>
    private static MemoryFlags DetermineMemoryFlags(MemoryOptions options)
    {
        var flags = MemoryFlags.ReadWrite;

        // MemoryOptions is an enum, not flags, so we use simple checks
        // For now, use default ReadWrite for simplicity
        // In a full implementation, this could map specific enum values

        if (options.HasFlag(MemoryOptions.Mapped))
            flags |= MemoryFlags.AllocHostPtr;

        return flags;
    }

    /// <summary>
    /// Throws if this memory manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// Disposes the OpenCL memory manager and all tracked buffers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            _logger.LogInformation("Disposing OpenCL memory manager");

            Clear(); // Dispose all buffers
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the OpenCL memory manager.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await Task.Run(Dispose);
    }
}