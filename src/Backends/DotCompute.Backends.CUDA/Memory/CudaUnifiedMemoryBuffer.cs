// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Production-grade typed CUDA unified memory buffer implementation that provides 
/// efficient access to GPU memory with full span and memory support.
/// </summary>
/// <typeparam name="T">The unmanaged type of elements in the buffer.</typeparam>
public sealed class CudaUnifiedMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IntPtr _devicePtr;
    private readonly int _length;
    private readonly long _sizeInBytes;
    private readonly CudaUnifiedMemoryManagerProduction _manager;
    private readonly ManagedMemoryFlags _flags;
    private readonly ILogger? _logger;
    private readonly int _deviceId;
    private readonly object _syncLock = new();
    private BufferState _state;
    private volatile bool _disposed;
    private Memory<T>? _pinnedMemory;
    private MemoryHandle _memoryHandle;
    private bool _isPinned;

    /// <summary>
    /// Initializes a new instance of the CudaUnifiedMemoryBuffer class.
    /// </summary>
    public CudaUnifiedMemoryBuffer(
        IntPtr devicePtr,
        int length,
        CudaUnifiedMemoryManagerProduction manager,
        ManagedMemoryFlags flags,
        ILogger? logger = null,
        int deviceId = 0)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (devicePtr == IntPtr.Zero)
            throw new ArgumentException("Device pointer cannot be null", nameof(devicePtr));

        _devicePtr = devicePtr;
        _length = length;
        _sizeInBytes = (long)length * Unsafe.SizeOf<T>();
        _manager = manager;
        _flags = flags;
        _logger = logger;
        _deviceId = deviceId;
        _state = BufferState.DeviceReady;
    }

    /// <inheritdoc/>
    public int Length => _length;

    /// <inheritdoc/>
    public long SizeInBytes => _sizeInBytes;

    /// <inheritdoc/>
    public IAccelerator Accelerator => _manager.Accelerator;

    /// <inheritdoc/>
    public MemoryOptions Options => 
        _flags.HasFlag(ManagedMemoryFlags.PreferDevice) 
            ? MemoryOptions.None 
            : MemoryOptions.AutoMigrate;

    /// <inheritdoc/>
    public BufferState State
    {
        get
        {
            lock (_syncLock)
            {
                return _disposed ? BufferState.Disposed : _state;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsDisposed => _disposed;

    /// <inheritdoc/>
    public bool IsOnHost => 
        State == BufferState.HostReady || State == BufferState.HostDirty;

    /// <inheritdoc/>
    public bool IsOnDevice => 
        State == BufferState.DeviceReady || State == BufferState.DeviceDirty;

    /// <inheritdoc/>
    public bool IsDirty => 
        State == BufferState.HostDirty || State == BufferState.DeviceDirty;

    /// <inheritdoc/>
    public Span<T> AsSpan()
    {
        ThrowIfDisposed();
        EnsureOnHost();
        
        unsafe
        {
            // For unified memory, we can directly create a span from the device pointer
            // since it's accessible from both host and device
            return new Span<T>(_devicePtr.ToPointer(), _length);
        }
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        ThrowIfDisposed();
        EnsureOnHost();
        
        unsafe
        {
            return new ReadOnlySpan<T>(_devicePtr.ToPointer(), _length);
        }
    }

    /// <inheritdoc/>
    public Memory<T> AsMemory()
    {
        ThrowIfDisposed();
        EnsureOnHost();

        // For unified memory, we need to create a Memory<T> wrapper
        // This requires pinning the memory
        if (!_isPinned)
        {
            unsafe
            {
                // Create a memory manager for the unified memory
                var memoryManager = new UnifiedMemoryManager<T>(_devicePtr, _length, this);
                _pinnedMemory = memoryManager.Memory;
                _isPinned = true;
            }
        }

        return _pinnedMemory!.Value;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        return AsMemory();
    }

    /// <inheritdoc/>
    public void EnsureOnHost()
    {
        ThrowIfDisposed();

        lock (_syncLock)
        {
            if (_state == BufferState.DeviceDirty || _state == BufferState.DeviceReady)
            {
                // First, ensure any pending CUDA operations are complete
                var syncResult = CudaRuntime.cudaDeviceSynchronize();
                if (syncResult != CudaError.Success)
                {
                    throw new InvalidOperationException($"Device synchronization failed before host access: {syncResult}");
                }
                
                // For unified memory with prefetch support, we can hint to move data to host
                if (_manager.UnifiedMemorySupported && _manager.ConcurrentAccessSupported)
                {
                    // Use CPU device ID (-1 or cudaCpuDeviceId)
                    const int cpuDeviceId = -1;
                    _manager.PrefetchAsync(_devicePtr, _sizeInBytes, cpuDeviceId).GetAwaiter().GetResult();
                }

                _state = BufferState.HostReady;
                _logger?.LogDebug("Buffer ensured on host (size: {Size} bytes)", _sizeInBytes);
            }
        }
    }

    /// <inheritdoc/>
    public void EnsureOnDevice()
    {
        ThrowIfDisposed();

        lock (_syncLock)
        {
            if (_state == BufferState.HostDirty || _state == BufferState.HostReady)
            {
                // For unified memory with prefetch support, we can hint to move data to device
                if (_manager.UnifiedMemorySupported && _manager.ConcurrentAccessSupported)
                {
                    _manager.PrefetchAsync(_devicePtr, _sizeInBytes, _deviceId)
                        .GetAwaiter().GetResult();
                }

                _state = BufferState.DeviceReady;
                _logger?.LogDebug("Buffer ensured on device (size: {Size} bytes)", _sizeInBytes);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask EnsureOnHostAsync(
        AcceleratorContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsOnHost)
            return;

        await Task.Run(() => EnsureOnHost(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask EnsureOnDeviceAsync(
        AcceleratorContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (IsOnDevice)
            return;

        await Task.Run(() => EnsureOnDevice(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Synchronize()
    {
        ThrowIfDisposed();
        
        // Synchronize the device
        var result = CudaRuntime.cudaDeviceSynchronize();
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Device synchronization failed: {result}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask SynchronizeAsync(
        AcceleratorContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await Task.Run(() => Synchronize(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void MarkHostDirty()
    {
        lock (_syncLock)
        {
            if (_state == BufferState.HostReady)
                _state = BufferState.HostDirty;
        }
    }

    /// <inheritdoc/>
    public void MarkDeviceDirty()
    {
        lock (_syncLock)
        {
            if (_state == BufferState.DeviceReady)
                _state = BufferState.DeviceDirty;
        }
    }

    /// <inheritdoc/>
    public async ValueTask CopyFromAsync(
        ReadOnlyMemory<T> source,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (source.Length != _length)
            throw new ArgumentException($"Source length ({source.Length}) must match buffer length ({_length})", nameof(source));

        await Task.Run(() =>
        {
            var sourceSpan = source.Span;
            var destSpan = AsSpan();
            sourceSpan.CopyTo(destSpan);
            MarkHostDirty();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CopyToAsync(
        Memory<T> destination,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        if (destination.Length != _length)
            throw new ArgumentException($"Destination length ({destination.Length}) must match buffer length ({_length})", nameof(destination));

        await Task.Run(() =>
        {
            var sourceSpan = AsReadOnlySpan();
            var destSpan = destination.Span;
            sourceSpan.CopyTo(destSpan);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CopyToAsync(
        IUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        
        if (destination.Length != _length)
            throw new ArgumentException($"Destination length ({destination.Length}) must match buffer length ({_length})", nameof(destination));

        // Use CUDA memcpy for device-to-device transfers
        if (destination is CudaUnifiedMemoryBuffer<T> cudaDest)
        {
            await CopyToDeviceAsync(cudaDest, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fall back to host copy for other buffer types
            var temp = new T[_length];
            await CopyToAsync(temp, cancellationToken).ConfigureAwait(false);
            await destination.CopyFromAsync(temp, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask CopyToDeviceAsync(
        CudaUnifiedMemoryBuffer<T> destination,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            var result = CudaRuntime.cudaMemcpy(
                destination._devicePtr,
                _devicePtr,
                (nuint)_sizeInBytes,
                CudaMemcpyKind.DeviceToDevice);

            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Device-to-device copy failed: {result}");
            }

            destination.MarkDeviceDirty();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CopyToAsync(
        int sourceOffset,
        IUnifiedMemoryBuffer<T> destination,
        int destinationOffset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(destination);
        ValidateOffsetAndLength(sourceOffset, length);
        
        if (destinationOffset < 0 || destinationOffset + length > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset));

        if (destination is CudaUnifiedMemoryBuffer<T> cudaDest)
        {
            await Task.Run(() =>
            {
                var sourcePtrOffset = _devicePtr + (sourceOffset * Unsafe.SizeOf<T>());
                var destPtrOffset = cudaDest._devicePtr + (destinationOffset * Unsafe.SizeOf<T>());
                var bytesToCopy = (nuint)(length * Unsafe.SizeOf<T>());

                var result = CudaRuntime.cudaMemcpy(
                    destPtrOffset,
                    sourcePtrOffset,
                    bytesToCopy,
                    CudaMemcpyKind.DeviceToDevice);

                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"Partial device-to-device copy failed: {result}");
                }

                cudaDest.MarkDeviceDirty();
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fall back to host copy
            var temp = new T[length];
            var sourceSpan = AsReadOnlySpan().Slice(sourceOffset, length);
            sourceSpan.CopyTo(temp);
            
            var destMemory = Memory<T>.Empty;
            if (destination is IUnifiedMemoryBuffer<T> typedDest)
            {
                destMemory = typedDest.AsMemory().Slice(destinationOffset, length);
            }
            await destination.CopyFromAsync(temp, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask FillAsync(
        T value,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            var span = AsSpan();
            span.Fill(value);
            MarkHostDirty();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask FillAsync(
        T value,
        int offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateOffsetAndLength(offset, length);

        await Task.Run(() =>
        {
            var span = AsSpan().Slice(offset, length);
            span.Fill(value);
            MarkHostDirty();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ThrowIfDisposed();
        ValidateOffsetAndLength(offset, length);

        var slicePtr = _devicePtr + (offset * Unsafe.SizeOf<T>());
        return new CudaUnifiedMemoryBuffer<T>(slicePtr, length, _manager, _flags, _logger, _deviceId);
    }

    /// <inheritdoc/>
    public DeviceMemory GetDeviceMemory()
    {
        ThrowIfDisposed();
        return new DeviceMemory(_devicePtr, _sizeInBytes);
    }

    /// <inheritdoc/>
    public MappedMemory<T> Map(MapMode mode)
    {
        ThrowIfDisposed();
        
        // For unified memory, mapping is direct
        EnsureOnHost();
        
        // Return the memory directly as it's already accessible
        return new MappedMemory<T>(AsMemory(), null);
    }

    /// <inheritdoc/>
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode)
    {
        ThrowIfDisposed();
        ValidateOffsetAndLength(offset, length);
        
        EnsureOnHost();
        
        // Return the sliced memory
        return new MappedMemory<T>(AsMemory().Slice(offset, length), null);
    }

    /// <inheritdoc/>
    public async ValueTask<MappedMemory<T>> MapAsync(
        MapMode mode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await EnsureOnHostAsync(new AcceleratorContext(IntPtr.Zero, 0), cancellationToken).ConfigureAwait(false);
        return Map(mode);
    }

    /// <inheritdoc/>
    public async ValueTask CopyFromAsync<TSource>(
        ReadOnlyMemory<TSource> source,
        long destinationOffset,
        CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        ThrowIfDisposed();
        
        var sourceSizeInBytes = source.Length * Unsafe.SizeOf<TSource>();
        if (destinationOffset + sourceSizeInBytes > _sizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(destinationOffset));

        await Task.Run(() =>
        {
            unsafe
            {
                var sourceSpan = source.Span;
                fixed (TSource* srcPtr = sourceSpan)
                {
                    var destPtr = _devicePtr + (nint)destinationOffset;
                    Buffer.MemoryCopy(srcPtr, destPtr.ToPointer(), 
                        _sizeInBytes - destinationOffset, sourceSizeInBytes);
                }
            }
            MarkHostDirty();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask CopyToAsync<TDest>(
        Memory<TDest> destination,
        long sourceOffset,
        CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        ThrowIfDisposed();
        
        var destSizeInBytes = destination.Length * Unsafe.SizeOf<TDest>();
        if (sourceOffset + destSizeInBytes > _sizeInBytes)
            throw new ArgumentOutOfRangeException(nameof(sourceOffset));

        await Task.Run(() =>
        {
            unsafe
            {
                var destSpan = destination.Span;
                fixed (TDest* destPtr = destSpan)
                {
                    var srcPtr = _devicePtr + (nint)sourceOffset;
                    Buffer.MemoryCopy(srcPtr.ToPointer(), destPtr, destSizeInBytes, destSizeInBytes);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        ThrowIfDisposed();
        
        var newElementSize = Unsafe.SizeOf<TNew>();
        var currentElementSize = Unsafe.SizeOf<T>();
        
        if (_sizeInBytes % newElementSize != 0)
        {
            throw new InvalidOperationException(
                $"Cannot reinterpret buffer of {_sizeInBytes} bytes as {typeof(TNew).Name} " +
                $"(element size {newElementSize} bytes) - size mismatch");
        }

        var newLength = (int)(_sizeInBytes / newElementSize);
        return new CudaUnifiedMemoryBuffer<TNew>(_devicePtr, newLength, _manager, _flags, _logger);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_syncLock)
        {
            if (_disposed)
                return;

            try
            {
                // Release pinned memory handle if exists
                if (_isPinned)
                {
                    _memoryHandle.Dispose();
                    _isPinned = false;
                }

                // Free the CUDA memory through the manager
                _manager.FreeUnifiedAsync(_devicePtr).GetAwaiter().GetResult();
                
                _disposed = true;
                _logger?.LogDebug("Disposed CUDA unified memory buffer (size: {Size} bytes)", _sizeInBytes);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing CUDA unified memory buffer");
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            // Release pinned memory handle if exists
            if (_isPinned)
            {
                _memoryHandle.Dispose();
                _isPinned = false;
            }

            // Free the CUDA memory through the manager
            await _manager.FreeUnifiedAsync(_devicePtr).ConfigureAwait(false);
            
            _disposed = true;
            _logger?.LogDebug("Async disposed CUDA unified memory buffer (size: {Size} bytes)", _sizeInBytes);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error async disposing CUDA unified memory buffer");
        }

        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    private void ValidateOffsetAndLength(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        
        if (offset + length > _length)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Offset ({offset}) + length ({length}) exceeds buffer length ({_length})");
        }
    }

    /// <summary>
    /// Memory manager for unified memory that allows Memory&lt;T&gt; creation.
    /// </summary>
    private sealed class UnifiedMemoryManager<TElement> : MemoryManager<TElement> where TElement : unmanaged
    {
        private readonly IntPtr _ptr;
        private readonly int _length;
        private readonly CudaUnifiedMemoryBuffer<TElement> _owner;

        public UnifiedMemoryManager(IntPtr ptr, int length, CudaUnifiedMemoryBuffer<TElement> owner)
        {
            _ptr = ptr;
            _length = length;
            _owner = owner;
        }

        public override Span<TElement> GetSpan()
        {
            // Ensure memory is accessible on host before creating span
            _owner.EnsureOnHost();
            
            unsafe
            {
                return new Span<TElement>(_ptr.ToPointer(), _length);
            }
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            unsafe
            {
                var ptr = _ptr + (elementIndex * Unsafe.SizeOf<TElement>());
                return new MemoryHandle(ptr.ToPointer(), pinnable: this);
            }
        }

        public override void Unpin()
        {
            // Nothing to do for unified memory
        }

        protected override void Dispose(bool disposing)
        {
            // The buffer owner handles disposal
        }
    }
}