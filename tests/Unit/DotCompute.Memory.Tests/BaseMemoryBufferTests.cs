// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Memory;
using Xunit;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for the BaseMemoryBuffer consolidation that eliminated 45% of duplicate code.
/// </summary>
public class BaseMemoryBufferTests
{
    [Fact]
    public void BaseMemoryBuffer_CalculatesLength_Correctly()
    {
        // Arrange & Act
        var buffer = new TestMemoryBuffer<float>(1024);
        
        // Assert
        Assert.Equal(1024, buffer.SizeInBytes);
        Assert.Equal(256, buffer.Length); // 1024 bytes / 4 bytes per float
    }

    [Fact]
    public void BaseMemoryBuffer_ThrowsForInvalidSize()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestMemoryBuffer<float>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestMemoryBuffer<float>(-1));
    }

    [Fact]
    public void BaseMemoryBuffer_ThrowsForNonAlignedSize()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TestMemoryBuffer<float>(1023)); // Not divisible by 4
    }

    [Fact]
    public void ValidateCopyParameters_ThrowsForInvalidOffsets()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.TestValidateCopyParameters(100, -1, 100, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.TestValidateCopyParameters(100, 0, 100, -1, 10));
    }

    [Fact]
    public void ValidateCopyParameters_ThrowsForOverflow()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.TestValidateCopyParameters(100, 50, 100, 0, 60)); // Source overflow
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            buffer.TestValidateCopyParameters(100, 0, 100, 50, 60)); // Destination overflow
    }

    [Fact]
    public void ThrowIfDisposed_ThrowsWhenDisposed()
    {
        // Arrange
        var buffer = new TestMemoryBuffer<float>(1024);
        buffer.Dispose();
        
        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => buffer.TestThrowIfDisposed());
    }

    /// <summary>
    /// Test implementation of BaseMemoryBuffer for testing purposes.
    /// </summary>
    private class TestMemoryBuffer<T> : BaseMemoryBuffer<T> where T : unmanaged
    {
        private bool _disposed;

        public TestMemoryBuffer(long sizeInBytes) : base(sizeInBytes)
        {
        }

        public override IntPtr DevicePointer => IntPtr.Zero;
        public override MemoryType MemoryType => MemoryType.Host;
        public override bool IsDisposed => _disposed;

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() => _disposed = true;
        public override ValueTask DisposeAsync() 
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        public void TestThrowIfDisposed() => ThrowIfDisposed();
        public void TestValidateCopyParameters(long sourceLength, long sourceOffset, long destinationLength, long destinationOffset, long count)
            => ValidateCopyParameters(sourceLength, sourceOffset, destinationLength, destinationOffset, count);
    }
}

/// <summary>
/// Tests for BaseDeviceBuffer specialization.
/// </summary>
public class BaseDeviceBufferTests
{
    [Fact]
    public void BaseDeviceBuffer_HasCorrectMemoryType()
    {
        // Arrange & Act
        var buffer = new TestDeviceBuffer<float>(1024, new IntPtr(0x1000));
        
        // Assert
        Assert.Equal(MemoryType.Device, buffer.MemoryType);
        Assert.Equal(new IntPtr(0x1000), buffer.DevicePointer);
    }

    [Fact]
    public void MarkDisposed_ReturnsTrue_OnFirstCall()
    {
        // Arrange
        var buffer = new TestDeviceBuffer<float>(1024, new IntPtr(0x1000));
        
        // Act
        var firstCall = buffer.TestMarkDisposed();
        var secondCall = buffer.TestMarkDisposed();
        
        // Assert
        Assert.True(firstCall);
        Assert.False(secondCall);
        Assert.True(buffer.IsDisposed);
    }

    private class TestDeviceBuffer<T> : BaseDeviceBuffer<T> where T : unmanaged
    {
        public TestDeviceBuffer(long sizeInBytes, IntPtr devicePointer) : base(sizeInBytes, devicePointer)
        {
        }

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() => MarkDisposed();
        public override ValueTask DisposeAsync()
        {
            MarkDisposed();
            return ValueTask.CompletedTask;
        }

        public bool TestMarkDisposed() => MarkDisposed();
    }
}

/// <summary>
/// Tests for BaseUnifiedBuffer specialization.
/// </summary>
public class BaseUnifiedBufferTests
{
    [Fact]
    public void BaseUnifiedBuffer_HasCorrectMemoryType()
    {
        // Arrange & Act
        using var buffer = new TestUnifiedBuffer<float>(1024);
        
        // Assert
        Assert.Equal(MemoryType.Unified, buffer.MemoryType);
    }

    [Fact]
    public unsafe void AsSpan_ReturnsCorrectSpan()
    {
        // Arrange
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        fixed (float* ptr = data)
        {
            using var buffer = new TestUnifiedBuffer<float>(16, new IntPtr(ptr));
            
            // Act
            var span = buffer.AsSpan();
            
            // Assert
            Assert.Equal(4, span.Length);
            Assert.Equal(1.0f, span[0]);
            Assert.Equal(4.0f, span[3]);
        }
    }

    private unsafe class TestUnifiedBuffer<T> : BaseUnifiedBuffer<T> where T : unmanaged
    {
        private readonly T[]? _data;

        public TestUnifiedBuffer(long sizeInBytes) : this(sizeInBytes, IntPtr.Zero)
        {
            _data = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        }

        public TestUnifiedBuffer(long sizeInBytes, IntPtr ptr) : base(sizeInBytes, ptr == IntPtr.Zero ? new IntPtr(1) : ptr)
        {
        }

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override void Dispose() => MarkDisposed();
        public override ValueTask DisposeAsync()
        {
            MarkDisposed();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Tests for BasePooledBuffer specialization.
/// </summary>
public class BasePooledBufferTests
{
    [Fact]
    public void BasePooledBuffer_CallsReturnAction_OnDispose()
    {
        // Arrange
        var returnCalled = false;
        var buffer = new TestPooledBuffer<float>(1024, b => returnCalled = true);
        
        // Act
        buffer.Dispose();
        
        // Assert
        Assert.True(returnCalled);
        Assert.True(buffer.IsDisposed);
    }

    [Fact]
    public async Task BasePooledBuffer_CallsReturnAction_OnDisposeAsync()
    {
        // Arrange
        var returnCalled = false;
        var buffer = new TestPooledBuffer<float>(1024, b => returnCalled = true);
        
        // Act
        await buffer.DisposeAsync();
        
        // Assert
        Assert.True(returnCalled);
        Assert.True(buffer.IsDisposed);
    }

    [Fact]
    public void Reset_ResetsDisposedState()
    {
        // Arrange
        var buffer = new TestPooledBuffer<float>(1024, null);
        buffer.Dispose();
        
        // Act
        buffer.Reset();
        
        // Assert
        Assert.False(buffer.IsDisposed);
    }

    private class TestPooledBuffer<T> : BasePooledBuffer<T> where T : unmanaged
    {
        private readonly Memory<T> _memory;

        public TestPooledBuffer(long sizeInBytes, Action<BasePooledBuffer<T>>? returnAction) 
            : base(sizeInBytes, returnAction)
        {
            _memory = new T[sizeInBytes / System.Runtime.CompilerServices.Unsafe.SizeOf<T>()];
        }

        public override IntPtr DevicePointer => IntPtr.Zero;
        public override MemoryType MemoryType => MemoryType.Host;
        public override Memory<T> Memory => _memory;

        public override ValueTask CopyFromAsync(ReadOnlyMemory<T> source, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyToAsync(Memory<T> destination, long offset = 0, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public override ValueTask CopyFromAsync(IMemoryBuffer<T> source, long sourceOffset = 0, long destinationOffset = 0, long count = -1, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}