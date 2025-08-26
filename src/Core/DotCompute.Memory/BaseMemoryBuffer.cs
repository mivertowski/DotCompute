// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Buffers;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory;

/// <summary>
/// Base abstract class for memory buffer implementations, consolidating common patterns.
/// This addresses the critical issue of 15+ duplicate buffer implementations.
/// </summary>
public abstract class BaseMemoryBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly int _elementSize;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseMemoryBuffer{T}"/> class.
    /// </summary>
    protected BaseMemoryBuffer(long sizeInBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeInBytes);
        
        _elementSize = Unsafe.SizeOf<T>();
        SizeInBytes = sizeInBytes;
        Length = (int)(sizeInBytes / _elementSize);
        
        if ((long)Length * _elementSize != sizeInBytes)
        {
            throw new ArgumentException($"Size {sizeInBytes} is not evenly divisible by element size {_elementSize}", nameof(sizeInBytes));
        }
    }
    
    /// <inheritdoc/>
    public long SizeInBytes { get; }
    
    /// <inheritdoc/>
    public int Length { get; }
    
    /// <inheritdoc/>
    public abstract IntPtr DevicePointer { get; }
    
    /// <inheritdoc/>
    public abstract MemoryType MemoryType { get; }
    
    /// <inheritdoc/>
    public abstract bool IsDisposed { get; }
    
    /// <inheritdoc/>
    public abstract IAccelerator Accelerator { get; }
    
    /// <inheritdoc/>
    public abstract BufferState State { get; }
    
    /// <inheritdoc/>
    public abstract MemoryOptions Options { get; }
    
    /// <inheritdoc/>
    public abstract bool IsOnHost { get; }
    
    /// <inheritdoc/>
    public abstract bool IsOnDevice { get; }
    
    /// <inheritdoc/>
    public abstract bool IsDirty { get; }
    
    /// <inheritdoc/>
    public abstract Span<T> AsSpan();
    
    /// <inheritdoc/>
    public abstract ReadOnlySpan<T> AsReadOnlySpan();
    
    /// <inheritdoc/>
    public abstract Memory<T> AsMemory();
    
    /// <inheritdoc/>
    public abstract ReadOnlyMemory<T> AsReadOnlyMemory();
    
    /// <inheritdoc/>
    public abstract DeviceMemory GetDeviceMemory();
    
    /// <inheritdoc/>
    public abstract MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite);
    
    /// <inheritdoc/>
    public abstract MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite);
    
    /// <inheritdoc/>
    public abstract ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract void EnsureOnHost();
    
    /// <inheritdoc/>
    public abstract void EnsureOnDevice();
    
    /// <inheritdoc/>
    public abstract ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract void Synchronize();
    
    /// <inheritdoc/>
    public abstract ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract void MarkHostDirty();
    
    /// <inheritdoc/>
    public abstract void MarkDeviceDirty();
    
    /// <inheritdoc/>
    public abstract ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask FillAsync(T value, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract IUnifiedMemoryBuffer<T> Slice(int offset, int length);
    
    /// <inheritdoc/>
    public abstract IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged;
    
    /// <inheritdoc/>
    public abstract ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default);
    
    /// <inheritdoc/>
    public abstract ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates copy parameters.
    /// </summary>
    protected void ValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
    {
        ThrowIfDisposed();
        
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(destinationOffset);
        
        if (count < 0)
        {
            count = Math.Min(sourceLength - sourceOffset, destinationLength - destinationOffset);
        }
        
        if (sourceOffset + count > sourceLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Source buffer overflow");
        }
        
        if (destinationOffset + count > destinationLength)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Destination buffer overflow");
        }
    }
    
    /// <summary>
    /// Throws if the buffer has been disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
    }
    
    /// <inheritdoc/>
    public abstract void Dispose();
    
    /// <inheritdoc/>
    public abstract ValueTask DisposeAsync();
}

/// <summary>
/// Base class for device-specific memory buffers (GPU memory).
/// </summary>
public abstract class BaseDeviceBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
{
    private readonly IntPtr _devicePointer;
    private volatile int _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseDeviceBuffer{T}"/> class.
    /// </summary>
    protected BaseDeviceBuffer(long sizeInBytes, IntPtr devicePointer) : base(sizeInBytes)
    {
        ArgumentNullException.ThrowIfNull(devicePointer);
        _devicePointer = devicePointer;
    }
    
    /// <inheritdoc/>
    public override IntPtr DevicePointer => _devicePointer;
    
    /// <inheritdoc/>
    public override MemoryType MemoryType => MemoryType.Device;
    
    /// <inheritdoc/>
    public override bool IsDisposed => _disposed != 0;
    
    /// <summary>
    /// Marks the buffer as disposed.
    /// </summary>
    protected bool MarkDisposed() => Interlocked.CompareExchange(ref _disposed, 1, 0) == 0;
}

/// <summary>
/// Base class for unified memory buffers (accessible from both CPU and GPU).
/// </summary>
public abstract class BaseUnifiedBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
{
    private readonly IntPtr _unifiedPointer;
    private volatile int _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseUnifiedBuffer{T}"/> class.
    /// </summary>
    protected BaseUnifiedBuffer(long sizeInBytes, IntPtr unifiedPointer) : base(sizeInBytes)
    {
        ArgumentNullException.ThrowIfNull(unifiedPointer);
        _unifiedPointer = unifiedPointer;
    }
    
    /// <inheritdoc/>
    public override IntPtr DevicePointer => _unifiedPointer;
    
    /// <inheritdoc/>
    public override MemoryType MemoryType => MemoryType.Unified;
    
    /// <inheritdoc/>
    public override bool IsDisposed => _disposed != 0;
    
    /// <summary>
    /// Gets a span view of the unified memory for CPU access.
    /// </summary>
    public override unsafe Span<T> AsSpan()
    {
        ThrowIfDisposed();
        return new Span<T>(_unifiedPointer.ToPointer(), (int)Length);
    }
    
    /// <summary>
    /// Marks the buffer as disposed.
    /// </summary>
    protected bool MarkDisposed() => Interlocked.CompareExchange(ref _disposed, 1, 0) == 0;
}

/// <summary>
/// Base class for pooled memory buffers with automatic recycling.
/// </summary>
public abstract class BasePooledBuffer<T> : BaseMemoryBuffer<T>, IMemoryOwner<T> where T : unmanaged
{
    private readonly Action<BasePooledBuffer<T>>? _returnAction;
    private volatile int _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BasePooledBuffer{T}"/> class.
    /// </summary>
    protected BasePooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction = null) : base(sizeInBytes)
    {
        _returnAction = returnAction;
    }
    
    /// <inheritdoc/>
    public override bool IsDisposed => _disposed != 0;
    
    /// <inheritdoc/>
    public abstract Memory<T> Memory { get; }
    
    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            // Return to pool if return action is provided
            _returnAction?.Invoke(this);
            DisposeCore();
            GC.SuppressFinalize(this);
        }
    }
    
    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            // Return to pool if return action is provided
            _returnAction?.Invoke(this);
            await DisposeCoreAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
    
    /// <summary>
    /// Core disposal logic to be implemented by derived classes.
    /// </summary>
    protected virtual void DisposeCore() { }
    
    /// <summary>
    /// Core async disposal logic to be implemented by derived classes.
    /// </summary>
    protected virtual ValueTask DisposeCoreAsync() => ValueTask.CompletedTask;
    
    /// <summary>
    /// Resets the buffer for reuse in the pool.
    /// </summary>
    public virtual void Reset()
    {
        _disposed = 0;
    }
}

/// <summary>
/// Base class for pinned memory buffers (CPU memory pinned for GPU access).
/// </summary>
public abstract class BasePinnedBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
{
    private readonly GCHandle _pinnedHandle;
    private readonly IntPtr _pinnedPointer;
    private volatile int _disposed;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BasePinnedBuffer{T}"/> class.
    /// </summary>
    protected BasePinnedBuffer(T[] array) : base(array.Length * Unsafe.SizeOf<T>())
    {
        ArgumentNullException.ThrowIfNull(array);
        
        _pinnedHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
        _pinnedPointer = _pinnedHandle.AddrOfPinnedObject();
    }
    
    /// <inheritdoc/>
    public override IntPtr DevicePointer => _pinnedPointer;
    
    /// <inheritdoc/>
    public override MemoryType MemoryType => MemoryType.Pinned;
    
    /// <inheritdoc/>
    public override bool IsDisposed => _disposed != 0;
    
    /// <inheritdoc/>
    public override void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
        {
            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }
            GC.SuppressFinalize(this);
        }
    }
    
    /// <inheritdoc/>
    public override ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}