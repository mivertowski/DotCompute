using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;

namespace DotCompute.Memory;

/// <summary>
/// A unified buffer that provides seamless host/device memory management with lazy transfer semantics.
/// Uses pinned memory allocation for zero-copy operations and GCHandle for AOT compatibility.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class UnifiedBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    private readonly IMemoryManager _memoryManager;
    private readonly object _lock = new();
    
    private GCHandle _pinnedHandle;
    private T[]? _hostArray;
    private DeviceMemory _deviceMemory;
    private BufferState _state;
    private volatile bool _disposed;
    
    /// <summary>
    /// Gets the length of the buffer in elements.
    /// </summary>
    public int Length { get; }
    
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public long SizeInBytes { get; }
    
    /// <summary>
    /// Gets whether the buffer is currently available on the host.
    /// </summary>
    public bool IsOnHost => _state is BufferState.HostOnly or BufferState.Synchronized or BufferState.HostDirty;
    
    /// <summary>
    /// Gets whether the buffer is currently available on the device.
    /// </summary>
    public bool IsOnDevice => _state is BufferState.DeviceOnly or BufferState.Synchronized or BufferState.DeviceDirty;
    
    /// <summary>
    /// Gets whether the buffer has been modified and needs synchronization.
    /// </summary>
    public bool IsDirty => _state is BufferState.HostDirty or BufferState.DeviceDirty;
    
    /// <summary>
    /// Gets the buffer state for tracking transfers.
    /// </summary>
    public BufferState State => _state;
    
    /// <summary>
    /// Initializes a new instance of the UnifiedBuffer class.
    /// </summary>
    /// <param name="memoryManager">The memory manager to use for device operations.</param>
    /// <param name="length">The length of the buffer in elements.</param>
    public UnifiedBuffer(IMemoryManager memoryManager, int length)
    {
        ArgumentNullException.ThrowIfNull(memoryManager);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        
        _memoryManager = memoryManager;
        Length = length;
        SizeInBytes = length * Unsafe.SizeOf<T>();
        _state = BufferState.Uninitialized;
        _deviceMemory = DeviceMemory.Invalid;
        
        // Allocate pinned host memory for zero-copy operations
        AllocatePinnedHost();
    }
    
    /// <summary>
    /// Initializes a new instance of the UnifiedBuffer class with initial data.
    /// </summary>
    /// <param name="memoryManager">The memory manager to use for device operations.</param>
    /// <param name="data">The initial data to populate the buffer with.</param>
    public UnifiedBuffer(IMemoryManager memoryManager, ReadOnlySpan<T> data)
        : this(memoryManager, data.Length)
    {
        data.CopyTo(AsSpan());
        _state = BufferState.HostOnly;
    }
    
    /// <summary>
    /// Gets a span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A span to the host memory.</returns>
    public Span<T> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new Span<T>(_hostArray, 0, Length);
    }
    
    /// <summary>
    /// Gets a read-only span to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only span to the host memory.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new ReadOnlySpan<T>(_hostArray, 0, Length);
    }
    
    /// <summary>
    /// Gets a memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A memory handle to the host memory.</returns>
    public Memory<T> AsMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new Memory<T>(_hostArray, 0, Length);
    }
    
    /// <summary>
    /// Gets a read-only memory handle to the host memory. Will trigger transfer if needed.
    /// </summary>
    /// <returns>A read-only memory handle to the host memory.</returns>
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new ReadOnlyMemory<T>(_hostArray, 0, Length);
    }
    
    /// <summary>
    /// Gets the device memory handle. Will trigger transfer if needed.
    /// </summary>
    /// <returns>The device memory handle.</returns>
    public DeviceMemory GetDeviceMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnDevice();
        return _deviceMemory;
    }
    
    /// <summary>
    /// Ensures the buffer is available on the host. Triggers transfer if needed.
    /// </summary>
    public void EnsureOnHost()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.Uninitialized:
                    _state = BufferState.HostOnly;
                    break;
                
                case BufferState.HostOnly:
                case BufferState.Synchronized:
                case BufferState.HostDirty:
                    // Already on host
                    break;
                
                case BufferState.DeviceOnly:
                case BufferState.DeviceDirty:
                    // Transfer from device to host
                    TransferDeviceToHost();
                    _state = BufferState.Synchronized;
                    break;
                
                default:
                    throw new InvalidOperationException($"Invalid buffer state: {_state}");
            }
        }
    }
    
    /// <summary>
    /// Ensures the buffer is available on the device. Triggers transfer if needed.
    /// </summary>
    public void EnsureOnDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.Uninitialized:
                    AllocateDeviceMemory();
                    _state = BufferState.DeviceOnly;
                    break;
                
                case BufferState.DeviceOnly:
                case BufferState.Synchronized:
                case BufferState.DeviceDirty:
                    // Already on device
                    break;
                
                case BufferState.HostOnly:
                case BufferState.HostDirty:
                    // Transfer from host to device
                    EnsureDeviceAllocated();
                    TransferHostToDevice();
                    _state = BufferState.Synchronized;
                    break;
                
                default:
                    throw new InvalidOperationException($"Invalid buffer state: {_state}");
            }
        }
    }
    
    /// <summary>
    /// Asynchronously ensures the buffer is available on the host.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    public async ValueTask EnsureOnHostAsync(AcceleratorContext context = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // For simplicity, we'll use the synchronous version for now
        // In a real implementation, you'd use async operations
        await Task.Run(() => EnsureOnHost());
    }
    
    /// <summary>
    /// Asynchronously ensures the buffer is available on the device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    public async ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // For simplicity, we'll use the synchronous version for now
        // In a real implementation, you'd use async operations
        await Task.Run(() => EnsureOnDevice());
    }
    
    /// <summary>
    /// Marks the buffer as modified on the host.
    /// </summary>
    public void MarkHostDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            _state = _state switch
            {
                BufferState.HostOnly => BufferState.HostOnly,
                BufferState.Synchronized => BufferState.HostDirty,
                BufferState.DeviceDirty => BufferState.HostDirty,
                _ => _state
            };
        }
    }
    
    /// <summary>
    /// Marks the buffer as modified on the device.
    /// </summary>
    public void MarkDeviceDirty()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            _state = _state switch
            {
                BufferState.DeviceOnly => BufferState.DeviceOnly,
                BufferState.Synchronized => BufferState.DeviceDirty,
                BufferState.HostDirty => BufferState.DeviceDirty,
                _ => _state
            };
        }
    }
    
    /// <summary>
    /// Synchronizes the buffer state between host and device.
    /// </summary>
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        lock (_lock)
        {
            switch (_state)
            {
                case BufferState.HostDirty:
                    EnsureDeviceAllocated();
                    TransferHostToDevice();
                    _state = BufferState.Synchronized;
                    break;
                
                case BufferState.DeviceDirty:
                    TransferDeviceToHost();
                    _state = BufferState.Synchronized;
                    break;
                
                case BufferState.Synchronized:
                    // Already synchronized
                    break;
                
                default:
                    throw new InvalidOperationException($"Cannot synchronize from state: {_state}");
            }
        }
    }
    
    /// <summary>
    /// Asynchronously synchronizes the buffer state between host and device.
    /// </summary>
    /// <param name="context">The accelerator context to use for the async operation.</param>
    public async ValueTask SynchronizeAsync(AcceleratorContext context = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // For simplicity, we'll use the synchronous version for now
        // In a real implementation, you'd use async operations
        await Task.Run(() => Synchronize());
    }
    
    /// <summary>
    /// Gets the underlying memory buffer. Required by CPU backend.
    /// </summary>
    /// <returns>A memory handle to the buffer.</returns>
    public Memory<T> GetMemory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureOnHost();
        return new Memory<T>(_hostArray, 0, Length);
    }
    
    /// <summary>
    /// Copies data from a source memory into this buffer.
    /// </summary>
    /// <param name="source">The source data to copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(source.Length, Length);
        
        await Task.Run(() =>
        {
            EnsureOnHost();
            source.Span.CopyTo(new Span<T>(_hostArray, 0, source.Length));
            MarkHostDirty();
        }, cancellationToken);
    }
    
    /// <summary>
    /// Copies data from this buffer to a destination memory.
    /// </summary>
    /// <param name="destination">The destination memory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, Length);
        
        await Task.Run(() =>
        {
            EnsureOnHost();
            var sourceSpan = new ReadOnlySpan<T>(_hostArray, 0, destination.Length);
            sourceSpan.CopyTo(destination.Span);
        }, cancellationToken);
    }
    
    /// <summary>
    /// Releases all resources used by the UnifiedBuffer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        lock (_lock)
        {
            if (_disposed)
                return;
            
            _disposed = true;
            
            // Free device memory
            if (_deviceMemory.IsValid)
            {
                _memoryManager.Free(_deviceMemory);
                _deviceMemory = DeviceMemory.Invalid;
            }
            
            // Free pinned host memory
            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }
            
            _hostArray = null;
            _state = BufferState.Uninitialized;
        }
    }
    
    #region Private Methods
    
    private void AllocatePinnedHost()
    {
        _hostArray = new T[Length];
        _pinnedHandle = GCHandle.Alloc(_hostArray, GCHandleType.Pinned);
    }
    
    private void AllocateDeviceMemory()
    {
        if (!_deviceMemory.IsValid)
        {
            _deviceMemory = _memoryManager.Allocate(SizeInBytes);
        }
    }
    
    private void EnsureDeviceAllocated()
    {
        if (!_deviceMemory.IsValid)
        {
            AllocateDeviceMemory();
        }
    }
    
    private void TransferHostToDevice()
    {
        if (_hostArray == null)
            throw new InvalidOperationException("Host array is not allocated");
        
        var hostSpan = new ReadOnlySpan<T>(_hostArray, 0, Length);
        _memoryManager.CopyToDevice(hostSpan, _deviceMemory);
    }
    
    private void TransferDeviceToHost()
    {
        if (_hostArray == null)
            throw new InvalidOperationException("Host array is not allocated");
        
        var hostSpan = new Span<T>(_hostArray, 0, Length);
        _memoryManager.CopyToHost(_deviceMemory, hostSpan);
    }
    
    #endregion
}