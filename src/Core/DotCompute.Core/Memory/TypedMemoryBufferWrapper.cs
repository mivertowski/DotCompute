// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Core.Memory;

/// <summary>
/// Wraps an untyped memory buffer to provide typed access.
/// </summary>
internal class TypedMemoryBufferWrapper<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private readonly IUnifiedMemoryBuffer _underlyingBuffer;
    private readonly int _length;
    
    public TypedMemoryBufferWrapper(IUnifiedMemoryBuffer underlyingBuffer, int length)
    {
        _underlyingBuffer = underlyingBuffer ?? throw new ArgumentNullException(nameof(underlyingBuffer));
        _length = length;
    }
    
    public int Length => _length;
    public long SizeInBytes => _underlyingBuffer.SizeInBytes;
    public IAccelerator Accelerator => null!; // Will be set by specific implementations
    public MemoryOptions Options => _underlyingBuffer.Options;
    public BufferState State 
    { 
        get => _underlyingBuffer.State;
        set => _underlyingBuffer.State = value;
    }
    public bool IsDisposed => _underlyingBuffer.IsDisposed;
    public bool IsOnHost => State == BufferState.HostReady || State == BufferState.HostDirty;
    public bool IsOnDevice => State == BufferState.DeviceReady || State == BufferState.DeviceDirty;
    
    public Span<T> AsSpan()
    {
        throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");
    }
    
    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        throw new NotSupportedException("Direct span access not supported for generic buffer wrapper");
    }
    
    public Memory<T> AsMemory()
    {
        throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");
    }
    
    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        throw new NotSupportedException("Direct memory access not supported for generic buffer wrapper");
    }
    
    
    public void EnsureOnHost() 
    { 
        State = BufferState.HostReady;
    }
    
    public void EnsureOnDevice() 
    { 
        State = BufferState.DeviceReady;
    }
    
    public ValueTask EnsureOnHostAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnHost();
        return ValueTask.CompletedTask;
    }
    
    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
    {
        EnsureOnDevice();
        return ValueTask.CompletedTask;
    }
    
    public void Synchronize() { }
    
    public ValueTask SynchronizeAsync(AcceleratorContext context, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public void MarkHostDirty() => State = BufferState.HostDirty;
    public void MarkDeviceDirty() => State = BufferState.DeviceDirty;
    
    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        await _underlyingBuffer.CopyFromHostAsync(source, cancellationToken: cancellationToken);
    }
    
    public async ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        await _underlyingBuffer.CopyToHostAsync(destination, cancellationToken: cancellationToken);
    }
    
    public ValueTask CopyFromHostAsync<TSource>(ReadOnlyMemory<TSource> source, long offset = 0, 
        CancellationToken cancellationToken = default) where TSource : unmanaged
    {
        return _underlyingBuffer.CopyFromHostAsync(source, offset, cancellationToken);
    }
    
    public ValueTask CopyToHostAsync<TDest>(Memory<TDest> destination, long offset = 0, 
        CancellationToken cancellationToken = default) where TDest : unmanaged
    {
        return _underlyingBuffer.CopyToHostAsync(destination, offset, cancellationToken);
    }
    
    public void Dispose() => _underlyingBuffer.Dispose();
    public ValueTask DisposeAsync() => _underlyingBuffer.DisposeAsync();
    
    // Additional interface methods
    public bool IsDirty => State == BufferState.HostDirty || State == BufferState.DeviceDirty;
    
    public DeviceMemory GetDeviceMemory()
    {
        return _underlyingBuffer.GetDeviceMemory();
    }
    
    public MappedMemory<T> Map(MapMode mode)
    {
        throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    }
    
    public MappedMemory<T> MapRange(int offset, int length, MapMode mode)
    {
        throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    }
    
    public ValueTask<MappedMemory<T>> MapAsync(MapMode mode, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Mapping not supported for generic buffer wrapper");
    }
    
    public async ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        var temp = new T[Length];
        await CopyToAsync(temp, cancellationToken);
        await destination.CopyFromAsync(temp, cancellationToken);
    }
    
    public async ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int length, CancellationToken cancellationToken = default)
    {
        var temp = new T[length];
        await CopyToHostAsync(temp.AsMemory(), sourceOffset * Unsafe.SizeOf<T>(), cancellationToken);
        await destination.CopyFromHostAsync(temp.AsMemory(), destinationOffset * Unsafe.SizeOf<T>(), cancellationToken);
    }
    
    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        // Default implementation - derived classes can override for better performance
        return ValueTask.CompletedTask;
    }
    
    public ValueTask FillAsync(T value, int offset, int length, CancellationToken cancellationToken = default)
    {
        // Default implementation - derived classes can override for better performance
        return ValueTask.CompletedTask;
    }
    
    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        throw new NotSupportedException("Slicing not supported for generic buffer wrapper");
    }
    
    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        throw new NotSupportedException("Type conversion not supported for generic buffer wrapper");
    }
}