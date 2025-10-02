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
            _nativeHandle = Marshal.AllocHGlobal((IntPtr)_sizeInBytes);
            if (_nativeHandle == IntPtr.Zero)
            {
                throw new OutOfMemoryException($"Failed to allocate {_sizeInBytes} bytes of CPU memory");
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
        catch (OutOfMemoryException)
        {
            _logger?.LogError("Failed to allocate {Size} bytes of CPU memory", _sizeInBytes);
            throw;
        }
    }

    #region IUnifiedMemoryBuffer<byte> Implementation

    public long SizeInBytes => _sizeInBytes;
    public MemoryOptions Options => _options;
    public bool IsDisposed => _isDisposed;
    public BufferState State => _state;
    public int Length => (int)_sizeInBytes;
    public IAccelerator Accelerator => _memoryManager.Accelerator;
    public bool IsOnHost => _state == BufferState.HostOnly || _state == BufferState.Synchronized;
    public bool IsOnDevice => _state == BufferState.DeviceOnly || _state == BufferState.Synchronized;
    public bool IsDirty => _state == BufferState.HostDirty || _state == BufferState.DeviceDirty;

    public Span<byte> AsSpan()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new Span<byte>(_nativeHandle.ToPointer(), (int)_sizeInBytes);
        }
    }

    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new ReadOnlySpan<byte>(_nativeHandle.ToPointer(), (int)_sizeInBytes);
        }
    }

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

    public ReadOnlyMemory<byte> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        unsafe
        {
            return new UnmanagedMemoryManager<byte>((byte*)_nativeHandle, (int)_sizeInBytes).Memory;
        }
    }

    public DeviceMemory GetDeviceMemory()
        // For CPU backend, device memory is the same as host memory



        => new(_nativeHandle, _sizeInBytes);

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

    public MappedMemory<byte> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite)
    {
        EnsureNotDisposed();
        if (offset < 0 || length < 0 || offset + length > _sizeInBytes)
        {

            throw new ArgumentOutOfRangeException();
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

    public ValueTask<MappedMemory<byte>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(Map(mode));

    public void EnsureOnHost()
        // CPU buffer is always on host



        => _state = BufferState.HostOnly;

    public void EnsureOnDevice()
        // CPU buffer is always on host (CPU is the device)



        => _state = BufferState.DeviceOnly;

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        EnsureOnHost();
        return ValueTask.CompletedTask;
    }

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        EnsureOnDevice();
        return ValueTask.CompletedTask;
    }

    public void Synchronize() => _state = BufferState.Synchronized;

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
    {
        Synchronize();
        return ValueTask.CompletedTask;
    }

    public void MarkHostDirty() => _state = BufferState.HostDirty;

    public void MarkDeviceDirty() => _state = BufferState.DeviceDirty;

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

    public ValueTask FillAsync(byte value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(byte value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

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

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
        {

            throw new ObjectDisposedException(nameof(CpuMemoryBuffer));
        }
    }
}

/// <summary>
/// Unmanaged memory manager for CPU memory buffers.
/// </summary>
internal unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private readonly T* _pointer;
    private readonly int _length;
    private bool _disposed;

    public UnmanagedMemoryManager(T* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
    }

    public override Span<T> GetSpan()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(UnmanagedMemoryManager<T>));
        }


        return new Span<T>(_pointer, _length);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(UnmanagedMemoryManager<T>));
        }


        return new MemoryHandle(_pointer + elementIndex);
    }

    public override void Unpin()
    {
        // Nothing to do for unmanaged memory
    }

    protected override void Dispose(bool disposing) => _disposed = true;
}

