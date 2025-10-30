// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CPU.Threading;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// CPU-based memory buffer implementation that uses host system memory.
/// Implements the IUnifiedMemoryBuffer interface for CPU computations.
/// </summary>
public sealed class CpuMemoryBuffer : IUnifiedMemoryBuffer<byte>, IDisposable
{
    private readonly long _sizeInBytes;
    private readonly MemoryOptions _options;
    private readonly CpuMemoryManager _memoryManager;
    private readonly int _numaNode;
    private readonly NumaMemoryPolicy _policy;
    private readonly ILogger<CpuMemoryBuffer>? _logger;


    private IntPtr _nativeHandle;
    private bool _isDisposed;
    private BufferState _state;


    /// <summary>
    /// Initializes a new instance of the CpuMemoryBuffer class.
    /// </summary>
    /// <param name="sizeInBytes">Size of the buffer in bytes.</param>
    /// <param name="options">Memory allocation options.</param>
    /// <param name="memoryManager">The memory manager that owns this buffer.</param>
    /// <param name="numaNode">The NUMA node to allocate memory on.</param>
    /// <param name="policy">The NUMA memory policy to use.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public CpuMemoryBuffer(
        long sizeInBytes,

        MemoryOptions options,

        CpuMemoryManager memoryManager,

        int numaNode,

        NumaMemoryPolicy policy,
        ILogger<CpuMemoryBuffer>? logger = null)
    {
        _sizeInBytes = sizeInBytes;
        _options = options;
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _numaNode = numaNode;
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _logger = logger;
        _state = BufferState.HostOnly;


        AllocateNativeMemory();
    }

    private void AllocateNativeMemory()
    {
        try
        {
            // Allocate aligned memory for better SIMD performance
            _nativeHandle = checked((IntPtr)Marshal.AllocHGlobal((nint)_sizeInBytes));
            if (_nativeHandle == IntPtr.Zero)
            {
                throw new InsufficientMemoryException($"Failed to allocate {_sizeInBytes} bytes of CPU memory");
            }

            // Initialize memory if requested

            if (_options.InitializeToZero())
            {
                unsafe
                {
                    var span = new Span<byte>(_nativeHandle.ToPointer(), (int)_sizeInBytes);
                    span.Clear();
                }
            }


            _logger?.LogTrace("Allocated {Size} bytes of CPU memory at {Handle} on NUMA node {Node}",

                _sizeInBytes, _nativeHandle, _numaNode);
        }
        catch (Exception ex) when (ex is OutOfMemoryException or InsufficientMemoryException)
        {
            _logger?.LogError(ex, "Failed to allocate {Size} bytes of CPU memory", _sizeInBytes);
            throw;
        }
    }
    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    /// <value>The size in bytes.</value>

    #region IUnifiedMemoryBuffer<byte> Implementation

    public long SizeInBytes => _sizeInBytes;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public MemoryOptions Options => _options;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public bool IsDisposed => _isDisposed;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public BufferState State => _state;
    /// <summary>
    /// Gets or sets the length.
    /// </summary>
    /// <value>The length.</value>
    public int Length => (int)_sizeInBytes;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public IAccelerator Accelerator => _memoryManager.Accelerator;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public bool IsOnHost => _state is BufferState.HostOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public bool IsOnDevice => _state is BufferState.DeviceOnly or BufferState.Synchronized;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Span<byte> AsSpan()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new Span<byte>(_nativeHandle.ToPointer(), (int)_sizeInBytes);
        }
    }
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new ReadOnlySpan<byte>(_nativeHandle.ToPointer(), (int)_sizeInBytes);
        }
    }
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public Memory<byte> AsMemory()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new UnmanagedMemoryManager<byte>((byte*)_nativeHandle, (int)_sizeInBytes).Memory;
        }
    }

    /// <summary>
    /// Gets the memory for the buffer. Alias for AsMemory for compatibility.
    /// </summary>
    public Memory<byte> GetMemory() => AsMemory();
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ReadOnlyMemory<byte> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new UnmanagedMemoryManager<byte>((byte*)_nativeHandle, (int)_sizeInBytes).Memory;
        }
    }
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>

    public DeviceMemory GetDeviceMemory()
        // For CPU backend, device memory is the same as host memory




        => new(_nativeHandle, _sizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<byte> Map(MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        var memory = AsMemory();
        return new MappedMemory<byte>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>

    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _sizeInBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid range for mapping");
        }


        var memory = AsMemory().Slice(offset, length);
        return new MappedMemory<byte>(memory, () =>
        {
            if (mode != MapMode.Read)
            {
                MarkHostDirty();
            }
        });
    }
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "MappedMemory is returned to caller who is responsible for disposal")]
    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>

    public void EnsureOnHost()
        // CPU buffer is always on host




        => _state = BufferState.HostOnly;
    /// <summary>
    /// Performs ensure on device.
    /// </summary>

    public void EnsureOnDevice()
        // CPU buffer is always on host (CPU is the device)




        => _state = BufferState.DeviceOnly;
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        EnsureOnHost();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        EnsureOnDevice();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs synchronize.
    /// </summary>

    public void Synchronize() => _state = BufferState.Synchronized;
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        Synchronize();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>

    public void MarkHostDirty() => _state = BufferState.HostDirty;
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>

    public void MarkDeviceDirty() => _state = BufferState.DeviceDirty;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyFromAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _sizeInBytes)
        {

            throw new ArgumentException("Source is larger than buffer");
        }


        source.Span.CopyTo(AsSpan());
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _sizeInBytes)
        {

            throw new ArgumentException("Destination is smaller than buffer");
        }


        AsReadOnlySpan().CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(IUnifiedMemoryBuffer<byte> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination is CpuMemoryBuffer cpuDest)
        {
            AsReadOnlySpan().CopyTo(cpuDest.AsSpan());
            cpuDest.MarkHostDirty();
        }
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<byte> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination is CpuMemoryBuffer cpuDest)
        {
            var sourceSpan = AsReadOnlySpan().Slice(sourceOffset, count);
            var destSpan = cpuDest.AsSpan().Slice(destinationOffset, count);
            sourceSpan.CopyTo(destSpan);
            cpuDest.MarkHostDirty();
        }
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask FillAsync(byte value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<byte> Slice(int offset, int length)
    {
        EnsureNotDisposed();


        if (offset < 0)
        {

            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }


        if (offset + length > _sizeInBytes)
        {

            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond buffer boundaries");
        }

        // Create a view of the existing buffer

        return new CpuMemoryBufferSlice(this, offset, length, _memoryManager, _logger);
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();


        var elementSize = System.Runtime.CompilerServices.Unsafe.SizeOf<TNew>();
        if (_sizeInBytes % elementSize != 0)
        {

            throw new InvalidOperationException($"Buffer size {_sizeInBytes} is not compatible with element size {elementSize}");
        }

        // Calculate new element count

        var elementCount = (int)(_sizeInBytes / elementSize);

        // Create a typed wrapper around the same memory

        return new CpuMemoryBufferTyped<TNew>(this, elementCount, _memoryManager, _logger);
    }

    #endregion

    #region IUnifiedMemoryBuffer Base Implementation

    ValueTask IUnifiedMemoryBuffer.CopyFromAsync<T>(ReadOnlyMemory<T> source, long offset, CancellationToken cancellationToken)
    {
        var byteSource = MemoryMarshal.AsBytes(source.Span);
        var byteMemory = new Memory<byte>(byteSource.ToArray());
        return CopyFromAsync(byteMemory, cancellationToken);
    }

    ValueTask IUnifiedMemoryBuffer.CopyToAsync<T>(Memory<T> destination, long offset, CancellationToken cancellationToken)
    {
        var byteDestination = MemoryMarshal.AsBytes(destination.Span);
        var byteMemory = new Memory<byte>(byteDestination.ToArray());
        return CopyToAsync(byteMemory, cancellationToken);
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_nativeHandle != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_nativeHandle);
                _nativeHandle = IntPtr.Zero;
                _logger?.LogTrace("Freed CPU memory buffer of {Size} bytes", _sizeInBytes);
            }
            _isDisposed = true;
        }
    }
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);
}

/// <summary>
/// Unmanaged memory manager for CPU memory buffers.
/// </summary>
internal unsafe class UnmanagedMemoryManager<T>(T* pointer, int length) : MemoryManager<T> where T : unmanaged
{
    private readonly T* _pointer = pointer;
    private readonly int _length = length;
    private bool _disposed;
    /// <summary>
    /// Gets the span.
    /// </summary>
    /// <returns>The span.</returns>

    public override Span<T> GetSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new Span<T>(_pointer, _length);
    }
    /// <summary>
    /// Gets pin.
    /// </summary>
    /// <param name="elementIndex">The element index.</param>
    /// <returns>The result of the operation.</returns>

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new MemoryHandle(_pointer + elementIndex);
    }
    /// <summary>
    /// Performs unpin.
    /// </summary>

    public override void Unpin()
    {
        // Nothing to do for unmanaged memory
    }

    protected override void Dispose(bool disposing) => _disposed = true;
}

