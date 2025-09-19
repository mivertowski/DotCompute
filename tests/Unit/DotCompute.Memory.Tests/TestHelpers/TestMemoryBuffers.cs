// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CA2000 // Dispose objects before losing scope - Test implementations don't require disposal

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory.Tests.TestHelpers;

/// <summary>
/// Test implementation of BaseMemoryBuffer for unit testing.
/// </summary>
internal sealed class TestMemoryBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
{
    private readonly Memory<T> _memory;
    private readonly MemoryType _memoryType;
    private bool _isDisposed;

    public TestMemoryBuffer(long sizeInBytes, MemoryType memoryType = MemoryType.Host)

        : base(sizeInBytes)
    {
        var elementCount = (int)(sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        _memory = new T[elementCount];
        _memoryType = memoryType;
    }

    public override IntPtr DevicePointer => IntPtr.Zero;
    public override MemoryType MemoryType => _memoryType;
    public override bool IsDisposed => _isDisposed;
    public override bool IsOnHost => true;
    public override bool IsOnDevice => false;
    public override bool IsDirty => false;


    public override Span<T> AsSpan()

    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }


    public override ReadOnlySpan<T> AsReadOnlySpan()

    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory.Span;
    }


    public override Memory<T> AsMemory()

    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }


    public override ReadOnlyMemory<T> AsReadOnlyMemory()

    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        return _memory;
    }


    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);


    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(_memory, null);


    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => new(_memory.Slice(offset, length), null);


    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(_memory, null));


    public override void EnsureOnHost() { }
    public override void EnsureOnDevice() { }


    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void Synchronize() { }


    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void MarkHostDirty() { }
    public override void MarkDeviceDirty() { }


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        cancellationToken.ThrowIfCancellationRequested();


        if (source.Length > Length)
            throw new ArgumentOutOfRangeException(nameof(source), "Source is larger than buffer capacity");


        source.CopyTo(_memory);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        cancellationToken.ThrowIfCancellationRequested();


        if (destination.Length < Length)
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination is smaller than buffer size");


        _memory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        cancellationToken.ThrowIfCancellationRequested();


        ValidateCopyParameters(source.Length, 0, Length, offset, source.Length);
        source.CopyTo(_memory.Slice((int)offset));
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        cancellationToken.ThrowIfCancellationRequested();


        ValidateCopyParameters(Length, offset, destination.Length, 0, destination.Length);
        _memory.Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync(_memory, cancellationToken);


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync<T>(_memory.Slice(sourceOffset, count), destinationOffset, cancellationToken);


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Fill(value);
        return ValueTask.CompletedTask;
    }


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);


        return new TestMemoryBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), _memoryType);
    }


    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>()
        => throw new NotSupportedException();


    public override IAccelerator Accelerator => throw new NotSupportedException();
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    public override MemoryOptions Options => default;


    public override ValueTask DisposeAsync()
    {
        _isDisposed = true;
        return ValueTask.CompletedTask;
    }


    public override void Dispose() => _isDisposed = true;

    /// <summary>
    /// Test method to expose ValidateCopyParameters for testing.
    /// </summary>
    public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
        => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);

    /// <summary>
    /// Test method to expose ThrowIfDisposed for testing.
    /// </summary>
    public void TestThrowIfDisposed() => ThrowIfDisposed();
}

/// <summary>
/// Test implementation of BaseDeviceBuffer for unit testing.
/// </summary>
internal sealed class TestDeviceBuffer<T> : BaseDeviceBuffer<T> where T : unmanaged
{
    private readonly IAccelerator _accelerator;
    private readonly MemoryType _memoryType;
    private bool _isDisposed;

    public TestDeviceBuffer(IAccelerator accelerator, long sizeInBytes, MemoryType memoryType = MemoryType.Device)

        : base(sizeInBytes, IntPtr.Zero)
    {
        _accelerator = accelerator;
        _memoryType = memoryType;
    }

    public override IntPtr DevicePointer => IntPtr.Zero;
    public override MemoryType MemoryType => _memoryType;
    public override bool IsDisposed => _isDisposed;
    public override bool IsOnHost => false;
    public override bool IsOnDevice => true;
    public override bool IsDirty => false;


    public override IAccelerator Accelerator => _accelerator;
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    public override MemoryOptions Options => default;


    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);


    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(Memory<T>.Empty, null);


    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => new(Memory<T>.Empty, null);


    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(Memory<T>.Empty, null));


    public override void EnsureOnHost() { }
    public override void EnsureOnDevice() { }


    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void Synchronize() { }


    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void MarkHostDirty() { }
    public override void MarkDeviceDirty() { }


    public override Memory<T> AsMemory() => Memory<T>.Empty;
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;


    public override Span<T> AsSpan() => throw new NotSupportedException();
    public override ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException();


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        => new TestDeviceBuffer<T>(_accelerator, length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), _memoryType);


    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>()
        => throw new NotSupportedException();


    public override ValueTask DisposeAsync()
    {
        _isDisposed = true;
        return ValueTask.CompletedTask;
    }


    public override void Dispose() => _isDisposed = true;
}

/// <summary>
/// Test implementation of BaseUnifiedBuffer for unit testing.
/// </summary>
internal sealed unsafe class TestUnifiedBuffer<T> : BaseUnifiedBuffer<T> where T : unmanaged
{
    private readonly T[]? _data;
    private bool _isDisposed;
    private readonly IntPtr _pinnedPointer;

    public TestUnifiedBuffer(long sizeInBytes) : base(sizeInBytes, Marshal.AllocHGlobal((int)sizeInBytes))
    {
        var elementCount = (int)(sizeInBytes / sizeof(T));
        _data = new T[elementCount];
        _pinnedPointer = Marshal.AllocHGlobal((int)sizeInBytes);
    }

    public TestUnifiedBuffer(IntPtr existingPointer, long sizeInBytes) : base(sizeInBytes, existingPointer)
    {
        // Constructor for wrapping existing memory
        _data = null;
        _pinnedPointer = existingPointer;
    }

    public override IntPtr DevicePointer => _pinnedPointer;
    public override MemoryType MemoryType => MemoryType.Unified;
    public override bool IsDisposed => _isDisposed;
    public override bool IsOnHost => true;
    public override bool IsOnDevice => true;
    public override bool IsDirty => false;


    public override IAccelerator Accelerator => null!;
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    public override MemoryOptions Options => default;


    public override Memory<T> AsMemory() => _data != null ? _data.AsMemory() : Memory<T>.Empty;
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data != null ? _data.AsMemory() : ReadOnlyMemory<T>.Empty;


    /// <summary>
    /// Memory property for unified buffer access.
    /// </summary>
    public Memory<T> Memory => AsMemory();


    public override DeviceMemory GetDeviceMemory() => new(_pinnedPointer, SizeInBytes);


    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(AsMemory(), null);


    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => new(AsMemory().Slice(offset, length), null);


    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(AsMemory(), null));


    public override void EnsureOnHost() { }
    public override void EnsureOnDevice() { }


    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void Synchronize() { }


    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void MarkHostDirty() { }
    public override void MarkDeviceDirty() { }

    public override Span<T> AsSpan() => _data != null ? _data.AsSpan() : [];
    public override ReadOnlySpan<T> AsReadOnlySpan() => _data != null ? _data.AsSpan() : ReadOnlySpan<T>.Empty;

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            source.CopyTo(_data);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            _data.AsMemory().CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            source.CopyTo(_data.AsMemory().Slice((int)offset));
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            _data.AsMemory().Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            return destination.CopyFromAsync(_data, cancellationToken);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            return destination.CopyFromAsync<T>(_data.AsMemory().Slice(sourceOffset, count), destinationOffset, cancellationToken);
        return ValueTask.CompletedTask;
    }


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        _data?.AsSpan().Fill(value);
        return ValueTask.CompletedTask;
    }


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _data?.AsSpan().Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);


        return this; // Simplified for testing
    }

    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>()
        => throw new NotSupportedException();


    public override ValueTask DisposeAsync()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_data != null && _pinnedPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pinnedPointer);
            }
        }
        return ValueTask.CompletedTask;
    }


    public override void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_data != null && _pinnedPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pinnedPointer);
            }
        }
    }
}

/// <summary>
/// Test implementation of BasePooledBuffer for unit testing.
/// </summary>
internal sealed class TestPooledBuffer<T> : BasePooledBuffer<T> where T : unmanaged
{
    private readonly Memory<T> _memory;

    public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction)

        : base(sizeInBytes, returnAction)
    {
        _memory = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
    }

    public override IntPtr DevicePointer => IntPtr.Zero;
    public override MemoryType MemoryType => MemoryType.Host;
    public override bool IsOnHost => true;
    public override bool IsOnDevice => false;
    public override bool IsDirty => false;


    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);


    public override MappedMemory<T> Map(MapMode mode = MapMode.ReadWrite) => new(_memory, null);


    public override MappedMemory<T> MapRange(int offset, int length, MapMode mode = MapMode.ReadWrite) => new(_memory.Slice(offset, length), null);


    public override ValueTask<MappedMemory<T>> MapAsync(MapMode mode = MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(_memory, null));


    public override void EnsureOnHost() { }
    public override void EnsureOnDevice() { }


    public override ValueTask EnsureOnHostAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override ValueTask EnsureOnDeviceAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void Synchronize() { }


    public override ValueTask SynchronizeAsync(AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public override void MarkHostDirty() { }
    public override void MarkDeviceDirty() { }

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory);
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        _memory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory.Slice((int)offset));
        return ValueTask.CompletedTask;
    }

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _memory.Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync(_memory, cancellationToken);


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync<T>(_memory.Slice(sourceOffset, count), destinationOffset, cancellationToken);


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Fill(value);
        return ValueTask.CompletedTask;
    }


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        => new TestPooledBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), null);

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // All required abstract method implementations for BasePooledBuffer<T>
    public override Span<T> AsSpan() => _memory.Span;
    public override ReadOnlySpan<T> AsReadOnlySpan() => _memory.Span;
    public override Memory<T> AsMemory() => _memory;
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => _memory;
    public override Memory<T> Memory => _memory;


    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
    public override IAccelerator Accelerator => throw new NotSupportedException();
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    public override MemoryOptions Options => default;

    // BasePooledBuffer already provides implementation for Dispose and DisposeAsync
    // We need to override DisposeCore for cleanup

    protected override void DisposeCore()
    {
        // Cleanup logic if needed
    }


    protected override async ValueTask DisposeCoreAsync()
    {
        // Call base implementation to ensure proper disposal chain
        await base.DisposeCoreAsync();
        DisposeCore();
    }
}

/// <summary>
/// Mock implementation of IAccelerator for testing.
/// </summary>
internal sealed class TestAccelerator : IAccelerator
{
    public AcceleratorType Type => AcceleratorType.CPU;

    public string DeviceType => "TestDevice";

    public AcceleratorInfo Info => new(Type, "Test Accelerator", "1.0.0", 1024 * 1024 * 1024);

    public IUnifiedMemoryManager Memory => null!;

    public IUnifiedMemoryManager MemoryManager => null!;

    public AcceleratorContext Context => AcceleratorContext.Invalid;


    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ICompiledKernel>(null!);


    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;


    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}