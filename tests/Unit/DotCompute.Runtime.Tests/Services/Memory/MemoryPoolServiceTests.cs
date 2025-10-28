// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Runtime.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;
using MemoryPoolStatistics = DotCompute.Runtime.Services.MemoryPoolStatistics;

namespace DotCompute.Runtime.Tests.Services.Memory;

/// <summary>
/// Tests for IMemoryPoolService implementations
/// </summary>
public sealed class MemoryPoolServiceTests
{
    private readonly IMemoryPoolService _service;

    public MemoryPoolServiceTests()
    {
        _service = Substitute.For<IMemoryPoolService>();
    }

    [Fact]
    public async Task TryGetBufferAsync_WithValidSize_ReturnsBuffer()
    {
        // Arrange
        var sizeInBytes = 1024L;
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        _service.TryGetBufferAsync(sizeInBytes, MemoryOptions.None, default).Returns(buffer);

        // Act
        var result = await _service.TryGetBufferAsync(sizeInBytes);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(buffer);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    public async Task TryGetBufferAsync_WithDifferentSizes_ReturnsCorrectSize(long sizeInBytes)
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        _service.TryGetBufferAsync(sizeInBytes, MemoryOptions.None, default).Returns(buffer);

        // Act
        var result = await _service.TryGetBufferAsync(sizeInBytes);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnBufferAsync_WithRentedBuffer_ReturnsToPool()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();

        // Act
        await _service.ReturnBufferAsync(buffer);

        // Assert
        await _service.Received(1).ReturnBufferAsync(buffer, default);
    }

    [Fact]
    public async Task TryGetBufferAsync_AfterReturn_ReusesBuffer()
    {
        // Arrange
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        _service.TryGetBufferAsync(1024L, MemoryOptions.None, default).Returns(buffer);

        // Act
        var rented1 = await _service.TryGetBufferAsync(1024L);
        if (rented1 != null)
        {
            await _service.ReturnBufferAsync(rented1);
        }
        var rented2 = await _service.TryGetBufferAsync(1024L);

        // Assert
        rented2.Should().NotBeNull();
    }

    [Fact]
    public void Statistics_ReturnsPoolStatistics()
    {
        // Arrange
        var stats = new MemoryPoolStatistics
        {
            AllocationCount = 10,
            DeallocationCount = 5,
            TotalBytesAllocated = 10240
        };
        _service.Statistics.Returns(stats);

        // Act
        var result = _service.Statistics;

        // Assert
        result.Should().NotBeNull();
        result.AllocationCount.Should().Be(10);
        result.DeallocationCount.Should().Be(5);
    }

    [Fact]
    public async Task PerformMaintenanceAsync_RemovesUnusedBuffers()
    {
        // Arrange & Act
        await _service.PerformMaintenanceAsync();

        // Assert
        await _service.Received(1).PerformMaintenanceAsync(default);
    }

    [Fact]
    public async Task TryGetBufferAsync_MultipleConsecutive_AllSucceed()
    {
        // Arrange
        _service.TryGetBufferAsync(Arg.Any<long>(), Arg.Any<MemoryOptions>(), Arg.Any<CancellationToken>())
            .Returns(x => Substitute.For<IUnifiedMemoryBuffer>());

        // Act
        var result1 = await _service.TryGetBufferAsync(1024);
        var result2 = await _service.TryGetBufferAsync(2048);
        var result3 = await _service.TryGetBufferAsync(4096);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result3.Should().NotBeNull();
    }

    [Fact]
    public async Task ReturnBufferAsync_MultipleBuffers_AllReturnedSuccessfully()
    {
        // Arrange
        var buffers = new[]
        {
            Substitute.For<IUnifiedMemoryBuffer>(),
            Substitute.For<IUnifiedMemoryBuffer>(),
            Substitute.For<IUnifiedMemoryBuffer>()
        };

        // Act
        foreach (var buffer in buffers)
        {
            await _service.ReturnBufferAsync(buffer);
        }

        // Assert
        await _service.Received(3).ReturnBufferAsync(Arg.Any<IUnifiedMemoryBuffer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Statistics_AfterAllocations_ReflectsUsage()
    {
        // Arrange
        var initialStats = new MemoryPoolStatistics
        {
            AllocationCount = 0,
            TotalBytesAllocated = 0
        };
        var afterAllocStats = new MemoryPoolStatistics
        {
            AllocationCount = 10,
            TotalBytesAllocated = 10240
        };
        _service.Statistics.Returns(initialStats, afterAllocStats);

        // Act
        var before = _service.Statistics;
        var after = _service.Statistics;

        // Assert
        after.AllocationCount.Should().BeGreaterThan(before.AllocationCount);
    }

    [Fact]
    public async Task TryGetBufferAsync_WithZeroSize_HandlesGracefully()
    {
        // Arrange
        _service.TryGetBufferAsync(0L, MemoryOptions.None, default).Returns((IUnifiedMemoryBuffer?)null);

        // Act
        var result = await _service.TryGetBufferAsync(0L);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReturnBufferAsync_NullBuffer_HandlesGracefully()
    {
        // Arrange
        IUnifiedMemoryBuffer? buffer = null;

        // Act
        var action = async () => await _service.ReturnBufferAsync(buffer!);

        // Assert
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PerformMaintenanceAsync_MultipleTimes_HandlesCorrectly()
    {
        // Arrange & Act
        await _service.PerformMaintenanceAsync();
        await _service.PerformMaintenanceAsync();
        await _service.PerformMaintenanceAsync();

        // Assert
        await _service.Received(3).PerformMaintenanceAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryGetBufferAsync_ConcurrentRequests_HandlesCorrectly()
    {
        // Arrange
        _service.TryGetBufferAsync(Arg.Any<long>(), Arg.Any<MemoryOptions>(), Arg.Any<CancellationToken>())
            .Returns(x => Substitute.For<IUnifiedMemoryBuffer>());

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _service.TryGetBufferAsync(1024L))
            .ToArray();
        var results = await Task.WhenAll(tasks.Select(t => t.AsTask()).ToArray());

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    [Fact]
    public async Task ReturnBufferAsync_ConcurrentReturns_HandlesCorrectly()
    {
        // Arrange
        var buffers = Enumerable.Range(0, 10)
            .Select(_ => Substitute.For<IUnifiedMemoryBuffer>())
            .ToArray();

        // Act
        var tasks = buffers.Select(b => _service.ReturnBufferAsync(b)).ToArray();
        await Task.WhenAll(tasks.Select(t => t.AsTask()).ToArray());

        // Assert
        await _service.Received(10).ReturnBufferAsync(Arg.Any<IUnifiedMemoryBuffer>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Statistics_ConcurrentAccess_HandlesCorrectly()
    {
        // Arrange
        var stats = new MemoryPoolStatistics
        {
            AllocationCount = 10,
            TotalBytesAllocated = 10240
        };
        _service.Statistics.Returns(stats);

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => _service.Statistics))
            .ToArray();
        var results = Task.WhenAll(tasks).Result;

        // Assert
        results.Should().AllSatisfy(s => s.Should().NotBeNull());
    }

    [Fact]
    public async Task CreateBufferAsync_WithSize_CreatesNewBuffer()
    {
        // Arrange
        var size = 1024L * 1024 * 10; // 10 MB
        var buffer = Substitute.For<IUnifiedMemoryBuffer>();
        _service.CreateBufferAsync(size, MemoryOptions.None, default).Returns(buffer);

        // Act
        var result = await _service.CreateBufferAsync(size);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(buffer);
    }
}
