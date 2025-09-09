// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Accelerators;

/// <summary>
/// Provides a typed view of a CpuMemoryBuffer for strongly-typed element access.
/// </summary>
internal sealed class CpuMemoryBufferTyped<T> : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly CpuMemoryBuffer _parentBuffer;
    private readonly int _elementCount;
    private readonly CpuMemoryManager _memoryManager;
    private readonly ILogger? _logger;
    private bool _isDisposed;

    public CpuMemoryBufferTyped(
        CpuMemoryBuffer parentBuffer,
        int elementCount,
        CpuMemoryManager memoryManager,
        ILogger? logger)
    {
        _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        _elementCount = elementCount;
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger;
    }

    public long SizeInBytes => _elementCount * Unsafe.SizeOf<T>();
    public int Count => _elementCount;
    public BufferState State => _parentBuffer.State;
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    public IAccelerator Accelerator => _parentBuffer.Accelerator;

    public Span<T> AsSpan()
    {
        EnsureNotDisposed();
        // Cast the byte span to the target type
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(_parentBuffer.AsSpan());
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(_parentBuffer.AsReadOnlySpan());
    }

    public Memory<T> AsMemory()
    {
        EnsureNotDisposed();
        // For simplicity, we'll use the span-based approach
        // In production, this would need a proper MemoryManager<T> implementation
        var span = AsSpan();
        return new Memory<T>(span.ToArray());
    }

    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        var span = AsReadOnlySpan();
        return new ReadOnlyMemory<T>(span.ToArray());
    }

    public void EnsureOnHost() => _parentBuffer.EnsureOnHost();
    public void EnsureOnDevice() => _parentBuffer.EnsureOnDevice();

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnHostAsync(context, cancellationToken);

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);

    public void Synchronize() => _parentBuffer.Synchronize();

    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.SynchronizeAsync(context, cancellationToken);

    public void MarkHostDirty() => _parentBuffer.MarkHostDirty();
    public void MarkDeviceDirty() => _parentBuffer.MarkDeviceDirty();

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _elementCount)
            throw new ArgumentException("Source contains more elements than buffer capacity", nameof(source));

        // Convert to bytes and copy
        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
        return _parentBuffer.CopyFromAsync(sourceBytes, cancellationToken);
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
            throw new ArgumentException("Destination has insufficient capacity", nameof(destination));

        // Convert to bytes and copy
        var destinationBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(destination.Span);
        return _parentBuffer.CopyToAsync(destinationBytes, cancellationToken);
    }

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
        if (offset + count > _elementCount)
            throw new ArgumentOutOfRangeException(nameof(count), "Fill operation extends beyond buffer boundaries");

        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        EnsureNotDisposed();
        
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        if (offset + length > _elementCount)
            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond buffer boundaries");

        // Calculate byte offset and length
        int elementSize = Unsafe.SizeOf<T>();
        int byteOffset = offset * elementSize;
        int byteLength = length * elementSize;

        return new CpuMemoryBufferTypedSlice<T>(_parentBuffer, byteOffset, length, _memoryManager, _logger);
    }

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();
        
        int currentSize = (int)SizeInBytes;
        int newElementSize = Unsafe.SizeOf<TNew>();
        
        if (currentSize % newElementSize != 0)
            throw new InvalidOperationException($"Buffer size {currentSize} is not compatible with element size {newElementSize}");

        int newElementCount = currentSize / newElementSize;
        return new CpuMemoryBufferTyped<TNew>(_parentBuffer, newElementCount, _memoryManager, _logger);
    }

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CpuMemoryBufferTyped<T>));
        if (_parentBuffer.IsDisposed)
            throw new ObjectDisposedException(nameof(CpuMemoryBuffer), "Parent buffer has been disposed");
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            // Note: We don't dispose the parent buffer as we don't own it
        }
    }
}

/// <summary>
/// Provides a typed slice view of a CpuMemoryBuffer for strongly-typed element access.
/// </summary>
internal sealed class CpuMemoryBufferTypedSlice<T> : IUnifiedMemoryBuffer<T>, IDisposable
    where T : unmanaged
{
    private readonly CpuMemoryBuffer _parentBuffer;
    private readonly int _byteOffset;
    private readonly int _elementCount;
    private readonly CpuMemoryManager _memoryManager;
    private readonly ILogger? _logger;
    private bool _isDisposed;

    public CpuMemoryBufferTypedSlice(
        CpuMemoryBuffer parentBuffer,
        int byteOffset,
        int elementCount,
        CpuMemoryManager memoryManager,
        ILogger? logger)
    {
        _parentBuffer = parentBuffer ?? throw new ArgumentNullException(nameof(parentBuffer));
        _byteOffset = byteOffset;
        _elementCount = elementCount;
        _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        _logger = logger;
    }

    public long SizeInBytes => _elementCount * Unsafe.SizeOf<T>();
    public int Count => _elementCount;
    public BufferState State => _parentBuffer.State;
    public bool IsDisposed => _isDisposed || _parentBuffer.IsDisposed;
    public IAccelerator Accelerator => _parentBuffer.Accelerator;

    public Span<T> AsSpan()
    {
        EnsureNotDisposed();
        var byteSpan = _parentBuffer.AsSpan().Slice(_byteOffset, (int)SizeInBytes);
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    public ReadOnlySpan<T> AsReadOnlySpan()
    {
        EnsureNotDisposed();
        var byteSpan = _parentBuffer.AsReadOnlySpan().Slice(_byteOffset, (int)SizeInBytes);
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, T>(byteSpan);
    }

    public Memory<T> AsMemory()
    {
        EnsureNotDisposed();
        var span = AsSpan();
        return new Memory<T>(span.ToArray());
    }

    public ReadOnlyMemory<T> AsReadOnlyMemory()
    {
        EnsureNotDisposed();
        var span = AsReadOnlySpan();
        return new ReadOnlyMemory<T>(span.ToArray());
    }

    // Implementation of other methods similar to CpuMemoryBufferTyped<T>
    // but with proper offset handling...

    public void EnsureOnHost() => _parentBuffer.EnsureOnHost();
    public void EnsureOnDevice() => _parentBuffer.EnsureOnDevice();

    public ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnHostAsync(context, cancellationToken);

    public ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.EnsureOnDeviceAsync(context, cancellationToken);

    public void Synchronize() => _parentBuffer.Synchronize();
    public ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => _parentBuffer.SynchronizeAsync(context, cancellationToken);

    public void MarkHostDirty() => _parentBuffer.MarkHostDirty();
    public void MarkDeviceDirty() => _parentBuffer.MarkDeviceDirty();

    public ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (source.Length > _elementCount)
            throw new ArgumentException("Source contains more elements than slice capacity", nameof(source));

        var sourceBytes = System.Runtime.InteropServices.MemoryMarshal.Cast<T, byte>(source.Span);
        return _parentBuffer.CopyFromAsync(sourceBytes, _byteOffset, cancellationToken);
    }

    public ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (destination.Length < _elementCount)
            throw new ArgumentException("Destination has insufficient capacity", nameof(destination));

        var span = AsSpan();
        span.CopyTo(destination.Span);
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        AsSpan().Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
        if (offset + count > _elementCount)
            throw new ArgumentOutOfRangeException(nameof(count), "Fill operation extends beyond slice boundaries");

        AsSpan().Slice(offset, count).Fill(value);
        MarkHostDirty();
        return ValueTask.CompletedTask;
    }

    public IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        EnsureNotDisposed();
        
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        if (offset + length > _elementCount)
            throw new ArgumentOutOfRangeException(nameof(length), "Slice extends beyond current slice boundaries");

        int elementSize = Unsafe.SizeOf<T>();
        int newByteOffset = _byteOffset + (offset * elementSize);
        
        return new CpuMemoryBufferTypedSlice<T>(_parentBuffer, newByteOffset, length, _memoryManager, _logger);
    }

    public IUnifiedMemoryBuffer<TNew> AsType<TNew>() where TNew : unmanaged
    {
        EnsureNotDisposed();
        
        long currentSize = SizeInBytes;
        int newElementSize = Unsafe.SizeOf<TNew>();
        
        if (currentSize % newElementSize != 0)
            throw new InvalidOperationException($"Slice size {currentSize} is not compatible with element size {newElementSize}");

        int newElementCount = (int)(currentSize / newElementSize);
        return new CpuMemoryBufferTypedSlice<TNew>(_parentBuffer, _byteOffset, newElementCount, _memoryManager, _logger);
    }

    private void EnsureNotDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CpuMemoryBufferTypedSlice<T>));
        if (_parentBuffer.IsDisposed)
            throw new ObjectDisposedException(nameof(CpuMemoryBuffer), "Parent buffer has been disposed");
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            // Note: We don't dispose the parent buffer as we don't own it
        }
    }
}