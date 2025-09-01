// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using DotCompute.Abstractions.Memory;
using DotCompute.Memory;
using DotCompute.Memory.Tests.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Tests for BasePooledBuffer specialization.
/// </summary>
public class BasePooledBufferTests
{
    private readonly ITestOutputHelper _output;
    
    public BasePooledBufferTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_InitializesCorrectly()
    {
        // Arrange
        var returnAction = new Action<BasePooledBuffer<float>>(b => { });
        
        // Act
        using var buffer = new TestPooledBuffer<float>(1024, returnAction);
        
        // Assert
        buffer.Should().NotBeNull();
        buffer.MemoryType.Should().Be(MemoryType.Host);
        buffer.SizeInBytes.Should().Be(1024);
        buffer.Length.Should().Be(256); // 1024 bytes / 4 bytes per float
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_CallsReturnActionOnDispose()
    {
        // Arrange
        var returnCalled = false;
        var returnAction = new Action<BasePooledBuffer<int>>(b =>
        {
            returnCalled = true;
        });
        
        var buffer = new TestPooledBuffer<int>(512, returnAction);
        
        // Act
        buffer.Dispose();
        
        // Assert
        returnCalled.Should().BeTrue("return action should be called on dispose");
        buffer.IsDisposed.Should().BeTrue();
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_ResetsClearsState()
    {
        // Arrange
        var returnCount = 0;
        var pool = new Queue<TestPooledBuffer<int>>();
        
        TestPooledBuffer<int> CreateOrRent()
        {
            if (pool.Count > 0)
            {
                var buffer = pool.Dequeue();
                buffer.Reset();
                return buffer;
            }
            return new TestPooledBuffer<int>(1024, b => 
            {
                returnCount++;
                pool.Enqueue((TestPooledBuffer<int>)b);
            });
        }
        
        // Act - Simulate pool usage
        var buffer1 = CreateOrRent();
        buffer1.Dispose(); // Returns to pool
        
        var buffer2 = CreateOrRent(); // Reuses from pool
        buffer2.Dispose();
        
        // Assert
        returnCount.Should().Be(2, "buffer should be returned twice");
        pool.Count.Should().Be(1, "one buffer should be in the pool");
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_ProvidesFunctionalMemoryAccess()
    {
        // Arrange
        using var buffer = new TestPooledBuffer<float>(64, null);
        
        // Act
        var span = buffer.AsSpan();
        var readOnlySpan = buffer.AsReadOnlySpan();
        var memory = buffer.Memory;
        
        // Fill span with test data
        for (var i = 0; i < span.Length; i++)
        {
            span[i] = i * 1.5f;
        }
        
        // Assert
        span.Length.Should().Be(16); // 64 bytes / 4 bytes per float
        readOnlySpan.Length.Should().Be(16);
        memory.Length.Should().Be(16);
        
        // Verify data integrity
        for (var i = 0; i < span.Length; i++)
        {
            span[i].Should().Be(i * 1.5f);
        }
    }
    
    [Fact]
    [Trait("Category", "BufferTypes")]
    public void PooledBuffer_SupportsMultipleInstances()
    {
        // Arrange
        var buffers = new List<TestPooledBuffer<double>>();
        var returnCount = 0;
        
        // Act - Create multiple pooled buffers
        for (var i = 0; i < 5; i++)
        {
            buffers.Add(new TestPooledBuffer<double>(512, _ => returnCount++));
        }
        
        // Dispose all buffers
        foreach (var buffer in buffers)
        {
            buffer.Dispose();
        }
        
        // Assert
        returnCount.Should().Be(5, "all buffers should call return action");
        buffers.Should().OnlyContain(b => b.IsDisposed, "all buffers should be disposed");
    }
}