// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Core.Memory;
using DotCompute.Tests.Common.Mocks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotCompute.Core.Tests.Memory;

/// <summary>
/// Tests for IUnifiedMemoryManager implementations.
/// These tests target CURRENT functionality (as of 2025).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Memory")]
public sealed class UnifiedMemoryManagerTests : IAsyncDisposable
{
    private readonly ILogger<CpuMemoryManager> _logger = NullLogger<CpuMemoryManager>.Instance;
    private readonly List<IAsyncDisposable> _disposables = [];

    #region Basic Allocation Tests

    [Fact]
    public async Task AllocateAsync_ValidSize_ReturnsBuffer()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        // Act
        var buffer = await manager.AllocateAsync<int>(1024);
        _disposables.Add(buffer);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Length.Should().Be(1024);
        buffer.SizeInBytes.Should().Be(1024 * sizeof(int));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1024)]
    [InlineData(65536)]
    public async Task AllocateAsync_VariousSizes_CreatesCorrectBuffers(int elementCount)
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        // Act
        var buffer = await manager.AllocateAsync<float>(elementCount);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(elementCount);
        buffer.SizeInBytes.Should().Be(elementCount * sizeof(float));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public async Task AllocateAsync_InvalidSize_ThrowsArgumentException(int invalidSize)
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        // Act & Assert
        var act = async () => await manager.AllocateAsync<int>(invalidSize);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region AllocateAndCopyAsync Tests

    [Fact]
    public async Task AllocateAndCopyAsync_WithData_CopiesCorrectly()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        var sourceData = new[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };

        // Act
        var buffer = await manager.AllocateAndCopyAsync<float>(sourceData);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(5);
        var result = new float[5];
        await buffer.CopyToAsync(result);
        result.Should().BeEquivalentTo(sourceData);
    }

    [Fact]
    public async Task AllocateAndCopyAsync_EmptyData_CreatesEmptyBuffer()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        var emptyData = Array.Empty<double>();

        // Act
        var buffer = await manager.AllocateAndCopyAsync<double>(emptyData);
        _disposables.Add(buffer);

        // Assert
        buffer.Length.Should().Be(0);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task Statistics_AfterAllocation_ReflectsUsage()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        var statsBefore = manager.Statistics;

        // Act
        var buffer = await manager.AllocateAsync<long>(1000);
        _disposables.Add(buffer);
        var statsAfter = manager.Statistics;

        // Assert
        statsAfter.TotalAllocated.Should().BeGreaterThan(statsBefore.TotalAllocated);
        statsAfter.AllocationCount.Should().BeGreaterThan(statsBefore.AllocationCount);
    }

    #endregion

    #region Memory Limits Tests

    [Fact]
    public void TotalAvailableMemory_ReturnsPositiveValue()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        // Act & Assert
        manager.TotalAvailableMemory.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxAllocationSize_ReturnsPositiveValue()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        // Act & Assert
        manager.MaxAllocationSize.Should().BeGreaterThan(0);
    }

    #endregion

    #region Concurrent Allocation Tests

    [Fact]
    public async Task ConcurrentAllocations_ThreadSafe_SucceedsWithoutErrors()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);
        _disposables.Add(manager);

        const int concurrentOps = 50;
        var tasks = new Task<IUnifiedMemoryBuffer<int>>[concurrentOps];

        // Act
        for (int i = 0; i < concurrentOps; i++)
        {
            tasks[i] = manager.AllocateAsync<int>(1024).AsTask();
        }

        var buffers = await Task.WhenAll(tasks);

        // Assert
        buffers.Should().HaveCount(concurrentOps);
        buffers.Should().OnlyContain(b => b != null && b.Length == 1024);

        // Cleanup
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public async Task DisposeAsync_AfterAllocation_CleansUpResources()
    {
        // Arrange
        var accelerator = ConsolidatedMockAccelerator.CreateCpuMock();
        var manager = new CpuMemoryManager(accelerator, _logger);

        var buffer = await manager.AllocateAsync<byte>(1024);

        // Act
        await buffer.DisposeAsync();
        await manager.DisposeAsync();

        // Assert - should not throw
        manager.IsDisposed.Should().BeTrue();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
        _disposables.Clear();
    }
}
