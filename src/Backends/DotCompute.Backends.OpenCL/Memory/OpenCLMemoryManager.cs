// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.OpenCL.Types.Native;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.OpenCL.Types.Native.OpenCLTypes;

namespace DotCompute.Backends.OpenCL.Memory;

/// <summary>
/// OpenCL implementation of the unified memory manager.
/// Extends BaseMemoryManager to eliminate duplicate buffer tracking and disposal code.
/// </summary>
internal sealed class OpenCLMemoryManager : BaseMemoryManager
{
    private readonly OpenCLContext _context;
    private readonly IAccelerator _accelerator;
    private readonly object _lock = new();
    private long _currentAllocatedMemory;

    /// <inheritdoc/>
    public override IAccelerator Accelerator => _accelerator;

    /// <inheritdoc/>
    public override MemoryStatistics Statistics => new()
    {
        TotalAllocated = TotalAllocatedBytes,
        TotalAvailable = (long)_context.DeviceInfo.GlobalMemorySize,
        AllocationCount = AllocationCount,
        PeakMemoryUsage = PeakAllocatedBytes,
        CurrentUsed = Interlocked.Read(ref _currentAllocatedMemory),
        CurrentUsage = Interlocked.Read(ref _currentAllocatedMemory)
    };

    /// <inheritdoc/>
    public override long MaxAllocationSize => (long)_context.DeviceInfo.MaxMemoryAllocationSize;

    /// <inheritdoc/>
    public override long TotalAvailableMemory => (long)_context.DeviceInfo.GlobalMemorySize;

    /// <inheritdoc/>
    public override long CurrentAllocatedMemory => Interlocked.Read(ref _currentAllocatedMemory);

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
        : base(logger)
    {
        _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        Logger.LogDebug("Created OpenCL memory manager for device: {DeviceName}", _context.DeviceInfo.Name);
    }

    /// <inheritdoc/>
    protected override async ValueTask<IUnifiedMemoryBuffer> AllocateInternalAsync(
        long sizeInBytes,
        MemoryOptions options,
        CancellationToken cancellationToken)
    {
        if (sizeInBytes > MaxAllocationSize)
        {
            throw new InvalidOperationException($"Requested allocation size {sizeInBytes} exceeds maximum {MaxAllocationSize}");
        }

        return await Task.Run(() =>
        {
            Logger.LogDebug("Allocating OpenCL buffer: size={SizeInBytes} bytes, options={Options}", sizeInBytes, options);

            var flags = DetermineMemoryFlags(options);
            var buffer = new OpenCLMemoryBuffer<byte>(
                _context,
                (nuint)sizeInBytes,
                flags,
                LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(Logger))).CreateLogger<OpenCLMemoryBuffer<byte>>());

            // Track allocation
            Interlocked.Add(ref _currentAllocatedMemory, sizeInBytes);

            Logger.LogTrace("Successfully allocated OpenCL buffer: {Handle}, total allocated: {Total} bytes",
                buffer.Buffer.Handle, CurrentAllocatedMemory);

            return (IUnifiedMemoryBuffer)buffer;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int count,
        MemoryOptions options = MemoryOptions.None,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (count <= 0)
        {
            throw new ArgumentException("Count must be positive", nameof(count));
        }

        var elementCount = (nuint)count;
        nuint sizeInBytes;
        unsafe
        {
            sizeInBytes = elementCount * (nuint)sizeof(T);
        }

        if ((long)sizeInBytes > MaxAllocationSize)
        {
            throw new InvalidOperationException($"Requested allocation size {sizeInBytes} exceeds maximum {MaxAllocationSize}");
        }

        Logger.LogDebug("Allocating OpenCL buffer: type={TypeName}, count={Count}, size={SizeInBytes} bytes", typeof(T).Name, count, sizeInBytes);

        var flags = DetermineMemoryFlags(options);
        var buffer = new OpenCLMemoryBuffer<T>(
            _context,
            elementCount,
            flags,
            LoggerFactory.Create(builder => builder.AddProvider(new SingleLoggerProvider(Logger))).CreateLogger<OpenCLMemoryBuffer<T>>());

        // Track allocation using base class
        TrackBuffer(buffer, (long)sizeInBytes);
        Interlocked.Add(ref _currentAllocatedMemory, (long)sizeInBytes);

        Logger.LogTrace("Successfully allocated OpenCL buffer: {Handle}, total allocated: {Total} bytes",
            buffer.Buffer.Handle, CurrentAllocatedMemory);

        return ValueTask.FromResult<IUnifiedMemoryBuffer<T>>(buffer);
    }

    /// <inheritdoc/>
    protected override IUnifiedMemoryBuffer CreateViewCore(IUnifiedMemoryBuffer buffer, long offset, long length)
    {
        // OpenCL sub-buffers require specific alignment and have limitations
        // For now, return a simple wrapper that references the original buffer
        Logger.LogDebug("Creating view over OpenCL buffer: offset={Offset}, length={Length}", offset, length);
        return new OpenCLBufferView(buffer, offset, length);
    }

    /// <inheritdoc/>
    public override IUnifiedMemoryBuffer<T> CreateView<T>(
        IUnifiedMemoryBuffer<T> buffer,
        int offset,
        int length)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (offset < 0 || length <= 0 || offset + length > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid view range");
        }

        return buffer.Slice(offset, length);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        await destination.CopyToAsync(source, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int count,
        CancellationToken cancellationToken = default)
    {
        await destination.CopyToAsync(sourceOffset, source, destinationOffset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyToDeviceAsync<T>(
        ReadOnlyMemory<T> source,
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        await destination.CopyFromAsync(source, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyFromDeviceAsync<T>(
        IUnifiedMemoryBuffer<T> source,
        Memory<T> destination,
        CancellationToken cancellationToken = default)
    {
        await source.CopyToAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask FreeAsync(IUnifiedMemoryBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (buffer == null || buffer.IsDisposed)
        {
            return;
        }

        await Task.Run(() =>
        {
            var sizeInBytes = buffer.SizeInBytes;
            Interlocked.Add(ref _currentAllocatedMemory, -sizeInBytes);
            buffer.Dispose();

            Logger.LogTrace("Freed OpenCL buffer: size={Size}, remaining allocated: {Remaining} bytes",
                sizeInBytes, CurrentAllocatedMemory);
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask OptimizeAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            CleanupUnusedBuffers();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Logger.LogDebug("OpenCL memory optimization completed");
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            Logger.LogInformation("Clearing all OpenCL memory allocations");
            Interlocked.Exchange(ref _currentAllocatedMemory, 0);
            Logger.LogInformation("All OpenCL memory cleared");
        }
    }

    // ===== Device-specific Operations (Legacy Support) =====

    /// <inheritdoc/>
    public override DeviceMemory AllocateDevice(long sizeInBytes)
    {
        ThrowIfDisposed();

        if (sizeInBytes <= 0)
        {
            throw new ArgumentException("Size must be positive", nameof(sizeInBytes));
        }

        var memObject = _context.CreateBuffer(MemoryFlags.ReadWrite, (nuint)sizeInBytes);
        Interlocked.Add(ref _currentAllocatedMemory, sizeInBytes);

        return new DeviceMemory(memObject.Handle, sizeInBytes);
    }

    /// <inheritdoc/>
    public override void FreeDevice(DeviceMemory deviceMemory)
    {
        ThrowIfDisposed();

        if (deviceMemory.Handle != IntPtr.Zero)
        {
            OpenCLContext.ReleaseObject(deviceMemory.Handle, OpenCLRuntime.clReleaseMemObject, "device memory");
            Interlocked.Add(ref _currentAllocatedMemory, -deviceMemory.Size);
        }
    }

    /// <inheritdoc/>
    public override void MemsetDevice(DeviceMemory deviceMemory, byte value, long sizeInBytes)
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

    /// <inheritdoc/>
    public override async ValueTask MemsetDeviceAsync(DeviceMemory deviceMemory, byte value, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => MemsetDevice(deviceMemory, value, sizeInBytes), cancellationToken);
    }

    /// <inheritdoc/>
    public override void CopyHostToDevice(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes)
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

    /// <inheritdoc/>
    public override void CopyDeviceToHost(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes)
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

    /// <inheritdoc/>
    public override async ValueTask CopyHostToDeviceAsync(IntPtr hostPointer, DeviceMemory deviceMemory, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyHostToDevice(hostPointer, deviceMemory, sizeInBytes), cancellationToken);
    }

    /// <inheritdoc/>
    public override async ValueTask CopyDeviceToHostAsync(DeviceMemory deviceMemory, IntPtr hostPointer, long sizeInBytes, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => CopyDeviceToHost(deviceMemory, hostPointer, sizeInBytes), cancellationToken);
    }

    /// <inheritdoc/>
    public override void CopyDeviceToDevice(DeviceMemory sourceDevice, DeviceMemory destinationDevice, long sizeInBytes)
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

        if (options.HasFlag(MemoryOptions.Mapped))
        {
            flags |= MemoryFlags.AllocHostPtr;
        }

        return flags;
    }
}

/// <summary>
/// Simple buffer view for OpenCL buffers.
/// </summary>
internal sealed class OpenCLBufferView : IUnifiedMemoryBuffer
{
    private readonly IUnifiedMemoryBuffer _parent;
    private readonly long _offset;
    private readonly long _length;
    private bool _disposed;

    public OpenCLBufferView(IUnifiedMemoryBuffer parent, long offset, long length)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _offset = offset;
        _length = length;
    }

    public long SizeInBytes => _length;
    public BufferState State => _disposed ? BufferState.Disposed : _parent.State;
    public MemoryOptions Options => _parent.Options;
    public bool IsDisposed => _disposed || _parent.IsDisposed;

    public void Dispose()
    {
        _disposed = true;
        // Don't dispose parent - views don't own the underlying buffer
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyFromAsync(source, _offset + offset, cancellationToken);
    }

    public ValueTask CopyToAsync<T>(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default) where T : unmanaged
    {
        return _parent.CopyToAsync(destination, _offset + offset, cancellationToken);
    }
}
