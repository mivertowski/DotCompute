// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This file has been REPLACED by the consolidated TestMemoryBuffer implementation.
// Use DotCompute.Tests.Common.TestMemoryBuffer<T> instead.
//
// MIGRATION GUIDE:
// - Replace TestMemoryBuffer<T> with DotCompute.Tests.Common.TestMemoryBuffer<T>
// - Add using DotCompute.Tests.Common;
// - Update constructor calls to use new options pattern
// - Use TestMemoryBufferOptions for configuring test behavior
//
// The new implementation provides:
// - Comprehensive IUnifiedMemoryBuffer<T> compatibility
// - Configurable behavior for testing edge cases
// - Deterministic failure simulation
// - Memory tracking and validation
//
// This legacy implementation is kept for backward compatibility during migration.

#pragma warning disable CA2000 // Dispose objects before losing scope - Test implementations don't require disposal
#pragma warning disable CS0618 // Type or member is obsolete

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;

namespace DotCompute.Memory.Tests.TestHelpers;

/// <summary>
/// DEPRECATED: Test implementation of BaseMemoryBuffer for unit testing.
///
/// This class is deprecated and will be removed in a future version.
/// Use DotCompute.Tests.Common.TestMemoryBuffer{T} instead.
///
/// The new implementation provides better functionality and configuration options.
/// </summary>
[Obsolete("Use DotCompute.Tests.Common.TestMemoryBuffer<T> instead. This will be removed in a future version.")]
internal sealed class TestDeviceBuffer<T>(IAccelerator accelerator, long sizeInBytes, MemoryType memoryType = MemoryType.Device) : BaseDeviceBuffer<T>(sizeInBytes, IntPtr.Zero) where T : unmanaged
{
    private readonly IAccelerator _accelerator = accelerator;
    private readonly MemoryType _memoryType = memoryType;
    private bool _isDisposed;
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>

    public override IntPtr DevicePointer => IntPtr.Zero;
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    /// <value>The memory type.</value>
    public override MemoryType MemoryType => _memoryType;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public override bool IsDisposed => _isDisposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public override bool IsOnHost => false;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public override bool IsOnDevice => true;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public override bool IsDirty => false;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>


    public override IAccelerator Accelerator => _accelerator;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public override MemoryOptions Options => default;
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(Memory<T>.Empty, null);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(Memory<T>.Empty, null);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(Memory<T>.Empty, null));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public override void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public override void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public override void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public override void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public override void MarkDeviceDirty() { }
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override Memory<T> AsMemory() => Memory<T>.Empty;
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => ReadOnlyMemory<T>.Empty;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override Span<T> AsSpan() => throw new NotSupportedException();
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlySpan<T> AsReadOnlySpan() => throw new NotSupportedException();
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        => new TestDeviceBuffer<T>(_accelerator, length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), _memoryType);
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>()
        => throw new NotSupportedException();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override ValueTask DisposeAsync()
    {
        _isDisposed = true;
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>


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
    /// <summary>
    /// Initializes a new instance of the TestUnifiedBuffer class.
    /// </summary>
    /// <param name="sizeInBytes">The size in bytes.</param>

    public TestUnifiedBuffer(long sizeInBytes) : base(sizeInBytes, Marshal.AllocHGlobal((int)sizeInBytes))
    {
        var elementCount = (int)(sizeInBytes / sizeof(T));
        _data = new T[elementCount];
        _pinnedPointer = Marshal.AllocHGlobal((int)sizeInBytes);
    }
    /// <summary>
    /// Initializes a new instance of the TestUnifiedBuffer class.
    /// </summary>
    /// <param name="existingPointer">The existing pointer.</param>
    /// <param name="sizeInBytes">The size in bytes.</param>

    public TestUnifiedBuffer(IntPtr existingPointer, long sizeInBytes) : base(sizeInBytes, existingPointer)
    {
        // Constructor for wrapping existing memory
        _data = null;
        _pinnedPointer = existingPointer;
    }
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>

    public override IntPtr DevicePointer => _pinnedPointer;
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    /// <value>The memory type.</value>
    public override MemoryType MemoryType => MemoryType.Unified;
    /// <summary>
    /// Gets or sets a value indicating whether disposed.
    /// </summary>
    /// <value>The is disposed.</value>
    public override bool IsDisposed => _isDisposed;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public override bool IsOnHost => true;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public override bool IsOnDevice => true;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public override bool IsDirty => false;
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>


    public override IAccelerator Accelerator => null!;
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
    public override MemoryOptions Options => default;
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>


    public override Memory<T> AsMemory() => _data != null ? _data.AsMemory() : Memory<T>.Empty;
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => _data != null ? _data.AsMemory() : ReadOnlyMemory<T>.Empty;


    /// <summary>
    /// Memory property for unified buffer access.
    /// </summary>
    public Memory<T> Memory => AsMemory();
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public override DeviceMemory GetDeviceMemory() => new(_pinnedPointer, SizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(AsMemory(), null);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(AsMemory().Slice(offset, length), null);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(AsMemory(), null));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public override void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public override void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public override void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public override void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public override void MarkDeviceDirty() { }
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override Span<T> AsSpan() => _data != null ? _data.AsSpan() : [];
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlySpan<T> AsReadOnlySpan() => _data != null ? _data.AsSpan() : ReadOnlySpan<T>.Empty;
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            source.CopyTo(_data);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            _data.AsMemory().CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            source.CopyTo(_data.AsMemory().Slice((int)offset));
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            _data.AsMemory().Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            return destination.CopyFromAsync(_data, cancellationToken);
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


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
    {
        if (_data != null)
            return destination.CopyFromAsync<T>(_data.AsMemory().Slice(sourceOffset, count), destinationOffset, cancellationToken);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        _data?.AsSpan().Fill(value);
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


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        _data?.AsSpan().Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>

    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length);


        return this; // Simplified for testing
    }
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>

    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>()
        => throw new NotSupportedException();
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>


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
    /// <summary>
    /// Performs dispose.
    /// </summary>


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
internal sealed class TestPooledBuffer<T>(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction) : BasePooledBuffer<T>(sizeInBytes, returnAction) where T : unmanaged
{
    private readonly Memory<T> _memory = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
    /// <summary>
    /// Gets or sets the device pointer.
    /// </summary>
    /// <value>The device pointer.</value>

    public override IntPtr DevicePointer => IntPtr.Zero;
    /// <summary>
    /// Gets or sets the memory type.
    /// </summary>
    /// <value>The memory type.</value>
    public override MemoryType MemoryType => MemoryType.Host;
    /// <summary>
    /// Gets or sets a value indicating whether on host.
    /// </summary>
    /// <value>The is on host.</value>
    public override bool IsOnHost => true;
    /// <summary>
    /// Gets or sets a value indicating whether on device.
    /// </summary>
    /// <value>The is on device.</value>
    public override bool IsOnDevice => false;
    /// <summary>
    /// Gets or sets a value indicating whether dirty.
    /// </summary>
    /// <value>The is dirty.</value>
    public override bool IsDirty => false;
    /// <summary>
    /// Gets the device memory.
    /// </summary>
    /// <returns>The device memory.</returns>


    public override DeviceMemory GetDeviceMemory() => new(IntPtr.Zero, SizeInBytes);
    /// <summary>
    /// Gets map.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> Map(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(_memory, null);
    /// <summary>
    /// Gets map range.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The result of the operation.</returns>


    public override MappedMemory<T> MapRange(int offset, int length, Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite) => new(_memory.Slice(offset, length), null);
    /// <summary>
    /// Gets map asynchronously.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask<MappedMemory<T>> MapAsync(Abstractions.Memory.MapMode mode = Abstractions.Memory.MapMode.ReadWrite, CancellationToken cancellationToken = default) => ValueTask.FromResult(new MappedMemory<T>(_memory, null));
    /// <summary>
    /// Performs ensure on host.
    /// </summary>


    public override void EnsureOnHost() { }
    /// <summary>
    /// Performs ensure on device.
    /// </summary>
    public override void EnsureOnDevice() { }
    /// <summary>
    /// Gets ensure on host asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnHostAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets ensure on device asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask EnsureOnDeviceAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs synchronize.
    /// </summary>


    public override void Synchronize() { }
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask SynchronizeAsync(Abstractions.AcceleratorContext context = default, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Performs mark host dirty.
    /// </summary>


    public override void MarkHostDirty() { }
    /// <summary>
    /// Performs mark device dirty.
    /// </summary>
    public override void MarkDeviceDirty() { }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, CancellationToken cancellationToken = default)
    {
        _memory.CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
    {
        source.CopyTo(_memory.Slice((int)offset));
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
    {
        _memory.Slice((int)offset).CopyTo(destination);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(IUnifiedMemoryBuffer<T> destination, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync(_memory, cancellationToken);
    /// <summary>
    /// Gets copy to asynchronously.
    /// </summary>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask CopyToAsync(int sourceOffset, IUnifiedMemoryBuffer<T> destination, int destinationOffset, int count, CancellationToken cancellationToken = default)
        => destination.CopyFromAsync<T>(_memory.Slice(sourceOffset, count), destinationOffset, cancellationToken);
    /// <summary>
    /// Gets fill asynchronously.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public override ValueTask FillAsync(T value, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Fill(value);
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


    public override ValueTask FillAsync(T value, int offset, int count, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, GetType());
        _memory.Span.Slice(offset, count).Fill(value);
        return ValueTask.CompletedTask;
    }
    /// <summary>
    /// Gets slice.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>The result of the operation.</returns>


    public override IUnifiedMemoryBuffer<T> Slice(int offset, int length)
        => new TestPooledBuffer<T>(length * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), null);
    /// <summary>
    /// Gets copy from asynchronously.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="destinationOffset">The destination offset.</param>
    /// <param name="count">The count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public override ValueTask CopyFromAsync(IUnifiedMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    /// <summary>
    /// Gets as span.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    // All required abstract method implementations for BasePooledBuffer<T>
    public override Span<T> AsSpan() => _memory.Span;
    /// <summary>
    /// Gets as read only span.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlySpan<T> AsReadOnlySpan() => _memory.Span;
    /// <summary>
    /// Gets as memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override Memory<T> AsMemory() => _memory;
    /// <summary>
    /// Gets as read only memory.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override ReadOnlyMemory<T> AsReadOnlyMemory() => _memory;
    /// <summary>
    /// Gets or sets the memory.
    /// </summary>
    /// <value>The memory.</value>
    public override Memory<T> Memory => _memory;
    /// <summary>
    /// Gets as type.
    /// </summary>
    /// <typeparam name="TNew">The TNew type parameter.</typeparam>
    /// <returns>The result of the operation.</returns>


    public override IUnifiedMemoryBuffer<TNew> AsType<TNew>() => throw new NotSupportedException();
    /// <summary>
    /// Gets or sets the accelerator.
    /// </summary>
    /// <value>The accelerator.</value>
    public override IAccelerator Accelerator => throw new NotSupportedException();
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public override BufferState State => IsDisposed ? BufferState.Disposed : BufferState.Allocated;
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    /// <value>The options.</value>
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

