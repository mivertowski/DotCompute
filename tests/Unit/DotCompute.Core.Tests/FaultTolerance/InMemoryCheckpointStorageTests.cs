// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DotCompute.Abstractions.FaultTolerance;
using DotCompute.Core.FaultTolerance;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.FaultTolerance;

/// <summary>
/// Unit tests for <see cref="InMemoryCheckpointStorage"/>.
/// </summary>
[Trait("Category", "Unit")]
public class InMemoryCheckpointStorageTests
{
    private readonly ILogger<InMemoryCheckpointStorage> _logger;

    public InMemoryCheckpointStorageTests()
    {
        _logger = Substitute.For<ILogger<InMemoryCheckpointStorage>>();
    }

    private InMemoryCheckpointStorage CreateStorage() => new(_logger);

    private static CheckpointData CreateCheckpoint(
        string componentId = "test-component",
        string componentType = "TestType",
        byte[]? data = null,
        long sequenceNumber = 1,
        int version = 1)
    {
        return new CheckpointData(
            componentId,
            componentType,
            data ?? new byte[] { 1, 2, 3, 4, 5 },
            sequenceNumber,
            version);
    }

    [Fact]
    public async Task StorageName_ReturnsInMemory()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var name = storage.StorageName;

        // Assert
        Assert.Equal("InMemory", name);
    }

    [Fact]
    public async Task StoreAsync_StoresCheckpointSuccessfully()
    {
        // Arrange
        await using var storage = CreateStorage();
        var checkpoint = CreateCheckpoint();

        // Act
        var location = await storage.StoreAsync(checkpoint);

        // Assert
        Assert.NotNull(location);
        Assert.StartsWith("memory://", location);
        Assert.Contains(checkpoint.CheckpointId.ToString(), location);
    }

    [Fact]
    public async Task StoreAsync_ThrowsOnNullCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => storage.StoreAsync(null!));
    }

    [Fact]
    public async Task StoreAsync_ThrowsWhenDisposed()
    {
        // Arrange
        var storage = CreateStorage();
        await storage.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => storage.StoreAsync(CreateCheckpoint()));
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsStoredCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        var original = CreateCheckpoint();
        await storage.StoreAsync(original);

        // Act
        var retrieved = await storage.RetrieveAsync(original.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(original.CheckpointId, retrieved.CheckpointId);
        Assert.Equal(original.ComponentId, retrieved.ComponentId);
        Assert.Equal(original.ComponentType, retrieved.ComponentType);
        Assert.Equal(original.SequenceNumber, retrieved.SequenceNumber);
        Assert.Equal(original.Version, retrieved.Version);
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsNullForNonExistent()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var result = await storage.RetrieveAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RetrieveAsync_ThrowsWhenDisposed()
    {
        // Arrange
        var storage = CreateStorage();
        await storage.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => storage.RetrieveAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RetrieveLatestAsync_ReturnsLatestCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        const string componentId = "test-component";

        var checkpoint1 = CreateCheckpoint(componentId, sequenceNumber: 1);
        var checkpoint2 = CreateCheckpoint(componentId, sequenceNumber: 2);
        var checkpoint3 = CreateCheckpoint(componentId, sequenceNumber: 3);

        await storage.StoreAsync(checkpoint1);
        await Task.Delay(10); // Ensure different timestamps
        await storage.StoreAsync(checkpoint2);
        await Task.Delay(10);
        await storage.StoreAsync(checkpoint3);

        // Act
        var latest = await storage.RetrieveLatestAsync(componentId);

        // Assert
        Assert.NotNull(latest);
        Assert.Equal(checkpoint3.CheckpointId, latest.CheckpointId);
    }

    [Fact]
    public async Task RetrieveLatestAsync_ReturnsNullForUnknownComponent()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var result = await storage.RetrieveLatestAsync("unknown-component");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RetrieveLatestAsync_ThrowsOnNullComponentId()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => storage.RetrieveLatestAsync(null!));
    }

    [Fact]
    public async Task ListAsync_ReturnsAllCheckpointsForComponent()
    {
        // Arrange
        await using var storage = CreateStorage();
        const string componentId = "test-component";

        var checkpoint1 = CreateCheckpoint(componentId, sequenceNumber: 1);
        var checkpoint2 = CreateCheckpoint(componentId, sequenceNumber: 2);
        var checkpoint3 = CreateCheckpoint("other-component", sequenceNumber: 1);

        await storage.StoreAsync(checkpoint1);
        await Task.Delay(10);
        await storage.StoreAsync(checkpoint2);
        await storage.StoreAsync(checkpoint3);

        // Act
        var results = await storage.ListAsync(componentId);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, m => Assert.Equal(componentId, m.ComponentId));
    }

    [Fact]
    public async Task ListAsync_RespectsMaxResults()
    {
        // Arrange
        await using var storage = CreateStorage();
        const string componentId = "test-component";

        for (var i = 0; i < 10; i++)
        {
            var checkpoint = CreateCheckpoint(componentId, sequenceNumber: i);
            await storage.StoreAsync(checkpoint);
            await Task.Delay(5);
        }

        // Act
        var results = await storage.ListAsync(componentId, maxResults: 3);

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task ListAsync_ReturnsOrderedByCreationTimeDescending()
    {
        // Arrange
        await using var storage = CreateStorage();
        const string componentId = "test-component";

        var checkpoint1 = CreateCheckpoint(componentId, sequenceNumber: 1);
        await storage.StoreAsync(checkpoint1);
        await Task.Delay(20);

        var checkpoint2 = CreateCheckpoint(componentId, sequenceNumber: 2);
        await storage.StoreAsync(checkpoint2);
        await Task.Delay(20);

        var checkpoint3 = CreateCheckpoint(componentId, sequenceNumber: 3);
        await storage.StoreAsync(checkpoint3);

        // Act
        var results = await storage.ListAsync(componentId);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(results[0].CreatedAt >= results[1].CreatedAt);
        Assert.True(results[1].CreatedAt >= results[2].CreatedAt);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyForUnknownComponent()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var results = await storage.ListAsync("unknown-component");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task DeleteAsync_RemovesCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        var checkpoint = CreateCheckpoint();
        await storage.StoreAsync(checkpoint);

        // Act
        var deleted = await storage.DeleteAsync(checkpoint.CheckpointId);
        var exists = await storage.ExistsAsync(checkpoint.CheckpointId);

        // Assert
        Assert.True(deleted);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForNonExistent()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var result = await storage.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromComponentIndex()
    {
        // Arrange
        await using var storage = CreateStorage();
        const string componentId = "test-component";
        var checkpoint = CreateCheckpoint(componentId);
        await storage.StoreAsync(checkpoint);

        // Act
        await storage.DeleteAsync(checkpoint.CheckpointId);
        var results = await storage.ListAsync(componentId);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForStoredCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        var checkpoint = CreateCheckpoint();
        await storage.StoreAsync(checkpoint);

        // Act
        var exists = await storage.ExistsAsync(checkpoint.CheckpointId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForNonExistent()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var exists = await storage.ExistsAsync(Guid.NewGuid());

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        await using var storage = CreateStorage();
        var data1 = new byte[100];
        var data2 = new byte[200];

        var checkpoint1 = CreateCheckpoint("component-1", data: data1);
        var checkpoint2 = CreateCheckpoint("component-2", data: data2);

        await storage.StoreAsync(checkpoint1);
        await storage.StoreAsync(checkpoint2);

        // Act
        var stats = await storage.GetStatisticsAsync();

        // Assert
        Assert.Equal(2, stats.TotalCheckpoints);
        Assert.Equal(300, stats.TotalSizeBytes);
        Assert.Equal(2, stats.UniqueComponents);
        Assert.NotNull(stats.OldestCheckpoint);
        Assert.NotNull(stats.NewestCheckpoint);
        Assert.True(stats.CapturedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsZeroStatsWhenEmpty()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act
        var stats = await storage.GetStatisticsAsync();

        // Assert
        Assert.Equal(0, stats.TotalCheckpoints);
        Assert.Equal(0, stats.TotalSizeBytes);
        Assert.Equal(0, stats.UniqueComponents);
        Assert.Null(stats.OldestCheckpoint);
        Assert.Null(stats.NewestCheckpoint);
    }

    [Fact]
    public async Task DisposeAsync_ClearsAllData()
    {
        // Arrange
        var storage = CreateStorage();
        var checkpoint = CreateCheckpoint();
        await storage.StoreAsync(checkpoint);

        // Act
        await storage.DisposeAsync();

        // Assert - subsequent operations should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => storage.ExistsAsync(checkpoint.CheckpointId));
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        // Arrange
        var storage = CreateStorage();

        // Act & Assert - should not throw
        await storage.DisposeAsync();
        await storage.DisposeAsync();
    }

    [Fact]
    public async Task StoreAsync_OverwritesExistingCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        var originalData = new byte[] { 1, 2, 3 };
        var newData = new byte[] { 4, 5, 6, 7 };

        var checkpoint1 = new CheckpointData(
            Guid.NewGuid(),
            "test-component",
            "TestType",
            DateTimeOffset.UtcNow,
            originalData,
            1,
            1);

        await storage.StoreAsync(checkpoint1);

        // Create new checkpoint with same ID (simulating overwrite)
        var checkpoint2 = new CheckpointData(
            checkpoint1.CheckpointId,
            "test-component",
            "TestType",
            DateTimeOffset.UtcNow,
            newData,
            2,
            1);

        // Act
        await storage.StoreAsync(checkpoint2);
        var retrieved = await storage.RetrieveAsync(checkpoint1.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(4, retrieved.StateData.Length);
    }

    [Fact]
    public async Task StoreAsync_HandlesLargeData()
    {
        // Arrange
        await using var storage = CreateStorage();
        var largeData = new byte[1024 * 1024]; // 1 MB
        new Random(42).NextBytes(largeData);

        var checkpoint = CreateCheckpoint(data: largeData);

        // Act
        await storage.StoreAsync(checkpoint);
        var retrieved = await storage.RetrieveAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(largeData.Length, retrieved.StateData.Length);
    }

    [Fact]
    public async Task ConcurrentOperations_AreThreadSafe()
    {
        // Arrange
        await using var storage = CreateStorage();
        var tasks = new List<Task>();
        var checkpointIds = new List<Guid>();

        // Act - concurrent stores
        for (var i = 0; i < 100; i++)
        {
            var checkpoint = CreateCheckpoint($"component-{i % 10}", sequenceNumber: i);
            checkpointIds.Add(checkpoint.CheckpointId);
            tasks.Add(storage.StoreAsync(checkpoint));
        }

        await Task.WhenAll(tasks);

        // Act - concurrent reads
        var readTasks = checkpointIds.Select(id => storage.RetrieveAsync(id)).ToList();
        var results = await Task.WhenAll(readTasks);

        // Assert
        Assert.All(results, r => Assert.NotNull(r));
    }

    [Fact]
    public async Task StoreAsync_PreservesMetadata()
    {
        // Arrange
        await using var storage = CreateStorage();
        var metadata = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };

        var checkpoint = new CheckpointData(
            "test-component",
            "TestType",
            new byte[] { 1, 2, 3 },
            42,
            1,
            metadata);

        // Act
        await storage.StoreAsync(checkpoint);
        var retrieved = await storage.RetrieveAsync(checkpoint.CheckpointId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Metadata);
        Assert.Equal(2, retrieved.Metadata.Count);
        Assert.Equal("value1", retrieved.Metadata["key1"]);
        Assert.Equal("value2", retrieved.Metadata["key2"]);
    }
}
