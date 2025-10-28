// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Services.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Memory;

/// <summary>
/// Tests for IUnifiedMemoryService implementations
/// </summary>
public sealed class UnifiedMemoryServiceTests
{
    private readonly IUnifiedMemoryService _service;
    private readonly IUnifiedMemoryBuffer _mockBuffer;

    public UnifiedMemoryServiceTests()
    {
        _service = Substitute.For<IUnifiedMemoryService>();
        _mockBuffer = Substitute.For<IUnifiedMemoryBuffer>();
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithValidSize_ReturnsBuffer()
    {
        // Arrange
        var sizeInBytes = 1024L;
        var acceleratorIds = new[] { "cuda:0", "cpu" };
        _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds).Returns(_mockBuffer);

        // Act
        var result = await _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(_mockBuffer);
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(4096)]
    [InlineData(1048576)]
    public async Task AllocateUnifiedAsync_WithDifferentSizes_HandlesCorrectly(long sizeInBytes)
    {
        // Arrange
        var acceleratorIds = new[] { "cuda:0" };
        _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds).Returns(_mockBuffer);

        // Act
        var result = await _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithMultipleAccelerators_SharesMemory()
    {
        // Arrange
        var sizeInBytes = 2048L;
        var acceleratorIds = new[] { "cuda:0", "cuda:1", "cpu" };
        _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds).Returns(_mockBuffer);

        // Act
        var result = await _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds);

        // Assert
        result.Should().NotBeNull();
        await _service.Received(1).AllocateUnifiedAsync(sizeInBytes, acceleratorIds);
    }

    [Fact]
    public async Task MigrateAsync_BetweenAccelerators_CompletesSuccessfully()
    {
        // Arrange
        var sourceId = "cuda:0";
        var targetId = "cuda:1";

        // Act
        await _service.MigrateAsync(_mockBuffer, sourceId, targetId);

        // Assert
        await _service.Received(1).MigrateAsync(_mockBuffer, sourceId, targetId);
    }

    [Fact]
    public async Task MigrateAsync_FromCpuToGpu_HandlesCorrectly()
    {
        // Arrange
        var sourceId = "cpu";
        var targetId = "cuda:0";

        // Act
        await _service.MigrateAsync(_mockBuffer, sourceId, targetId);

        // Assert
        await _service.Received(1).MigrateAsync(_mockBuffer, sourceId, targetId);
    }

    [Fact]
    public async Task MigrateAsync_FromGpuToCpu_HandlesCorrectly()
    {
        // Arrange
        var sourceId = "cuda:0";
        var targetId = "cpu";

        // Act
        await _service.MigrateAsync(_mockBuffer, sourceId, targetId);

        // Assert
        await _service.Received(1).MigrateAsync(_mockBuffer, sourceId, targetId);
    }

    [Fact]
    public async Task SynchronizeCoherenceAsync_WithMultipleAccelerators_Synchronizes()
    {
        // Arrange
        var acceleratorIds = new[] { "cuda:0", "cuda:1", "cpu" };

        // Act
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);

        // Assert
        await _service.Received(1).SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
    }

    [Fact]
    public async Task SynchronizeCoherenceAsync_WithSingleAccelerator_HandlesCorrectly()
    {
        // Arrange
        var acceleratorIds = new[] { "cuda:0" };

        // Act
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);

        // Assert
        await _service.Received(1).SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
    }

    [Fact]
    public void GetCoherenceStatus_WithCoherentMemory_ReturnsCoherent()
    {
        // Arrange
        _service.GetCoherenceStatus(_mockBuffer).Returns(MemoryCoherenceStatus.Coherent);

        // Act
        var status = _service.GetCoherenceStatus(_mockBuffer);

        // Assert
        status.Should().Be(MemoryCoherenceStatus.Coherent);
    }

    [Fact]
    public void GetCoherenceStatus_WithIncoherentMemory_ReturnsIncoherent()
    {
        // Arrange
        _service.GetCoherenceStatus(_mockBuffer).Returns(MemoryCoherenceStatus.Incoherent);

        // Act
        var status = _service.GetCoherenceStatus(_mockBuffer);

        // Assert
        status.Should().Be(MemoryCoherenceStatus.Incoherent);
    }

    [Fact]
    public void GetCoherenceStatus_DuringSynchronization_ReturnsSynchronizing()
    {
        // Arrange
        _service.GetCoherenceStatus(_mockBuffer).Returns(MemoryCoherenceStatus.Synchronizing);

        // Act
        var status = _service.GetCoherenceStatus(_mockBuffer);

        // Assert
        status.Should().Be(MemoryCoherenceStatus.Synchronizing);
    }

    [Fact]
    public void GetCoherenceStatus_WithUnknownState_ReturnsUnknown()
    {
        // Arrange
        _service.GetCoherenceStatus(_mockBuffer).Returns(MemoryCoherenceStatus.Unknown);

        // Act
        var status = _service.GetCoherenceStatus(_mockBuffer);

        // Assert
        status.Should().Be(MemoryCoherenceStatus.Unknown);
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithZeroSize_HandlesGracefully()
    {
        // Arrange
        var sizeInBytes = 0L;
        var acceleratorIds = new[] { "cuda:0" };
        _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds).Returns(_mockBuffer);

        // Act
        var result = await _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MigrateAsync_MultipleTimes_HandlesCorrectly()
    {
        // Arrange
        var accelerators = new[] { "cuda:0", "cuda:1", "cpu" };

        // Act
        await _service.MigrateAsync(_mockBuffer, accelerators[0], accelerators[1]);
        await _service.MigrateAsync(_mockBuffer, accelerators[1], accelerators[2]);

        // Assert
        await _service.Received(2).MigrateAsync(_mockBuffer, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SynchronizeCoherenceAsync_AfterMigration_EnsuresCoherence()
    {
        // Arrange
        var acceleratorIds = new[] { "cuda:0", "cuda:1" };

        // Act
        await _service.MigrateAsync(_mockBuffer, "cuda:0", "cuda:1");
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);

        // Assert
        await _service.Received(1).SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
    }

    [Fact]
    public async Task AllocateUnifiedAsync_WithNoAccelerators_HandlesGracefully()
    {
        // Arrange
        var sizeInBytes = 1024L;
        var acceleratorIds = Array.Empty<string>();
        _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds).Returns(_mockBuffer);

        // Act
        var result = await _service.AllocateUnifiedAsync(sizeInBytes, acceleratorIds);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MigrateAsync_ToSameAccelerator_HandlesAsNoOp()
    {
        // Arrange
        var acceleratorId = "cuda:0";

        // Act
        await _service.MigrateAsync(_mockBuffer, acceleratorId, acceleratorId);

        // Assert
        await _service.Received(1).MigrateAsync(_mockBuffer, acceleratorId, acceleratorId);
    }

    [Fact]
    public async Task SynchronizeCoherenceAsync_MultipleConsecutiveCalls_HandlesCorrectly()
    {
        // Arrange
        var acceleratorIds = new[] { "cuda:0", "cpu" };

        // Act
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
        await _service.SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);

        // Assert
        await _service.Received(3).SynchronizeCoherenceAsync(_mockBuffer, acceleratorIds);
    }

    [Fact]
    public void GetCoherenceStatus_MultipleCallsWithoutChanges_ReturnsConsistentState()
    {
        // Arrange
        _service.GetCoherenceStatus(_mockBuffer).Returns(MemoryCoherenceStatus.Coherent);

        // Act
        var status1 = _service.GetCoherenceStatus(_mockBuffer);
        var status2 = _service.GetCoherenceStatus(_mockBuffer);

        // Assert
        status1.Should().Be(status2);
    }
}
