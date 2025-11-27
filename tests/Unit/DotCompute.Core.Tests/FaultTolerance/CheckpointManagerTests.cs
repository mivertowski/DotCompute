// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DotCompute.Abstractions.FaultTolerance;
using DotCompute.Core.FaultTolerance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.FaultTolerance;

/// <summary>
/// Unit tests for <see cref="CheckpointManager"/>.
/// </summary>
[Trait("Category", "Unit")]
public class CheckpointManagerTests
{
    private readonly ILogger<CheckpointManager> _managerLogger;
    private readonly ILogger<InMemoryCheckpointStorage> _storageLogger;

    public CheckpointManagerTests()
    {
        _managerLogger = Substitute.For<ILogger<CheckpointManager>>();
        _storageLogger = Substitute.For<ILogger<InMemoryCheckpointStorage>>();
    }

    private InMemoryCheckpointStorage CreateStorage() => new(_storageLogger);

    private CheckpointManager CreateManager(
        ICheckpointStorage? storage = null,
        CheckpointOptions? options = null)
    {
        storage ??= CreateStorage();
        return new CheckpointManager(
            storage,
            Options.Create(options ?? CheckpointOptions.Default),
            _managerLogger);
    }

    private static TestCheckpointableComponent CreateComponent(
        string id = "test-component",
        bool supportsCheckpointing = true)
    {
        return new TestCheckpointableComponent(id, supportsCheckpointing);
    }

    [Fact]
    public void Constructor_ThrowsOnNullStorage()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new CheckpointManager(null!, Options.Create(CheckpointOptions.Default), _managerLogger));
    }

    [Fact]
    public async Task Constructor_ThrowsOnNullLogger()
    {
        // Arrange
        await using var storage = CreateStorage();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new CheckpointManager(storage, Options.Create(CheckpointOptions.Default), null!));
    }

    [Fact]
    public async Task Storage_ReturnsProvidedStorage()
    {
        // Arrange
        await using var storage = CreateStorage();
        await using var manager = new CheckpointManager(storage, _managerLogger);

        // Act & Assert
        Assert.Same(storage, manager.Storage);
    }

    [Fact]
    public async Task CheckpointAsync_CreatesAndStoresCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        await using var manager = CreateManager(storage);
        var component = CreateComponent();

        // Act
        var metadata = await manager.CheckpointAsync(component);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(component.CheckpointId, metadata.ComponentId);
        Assert.True(await storage.ExistsAsync(metadata.CheckpointId));
    }

    [Fact]
    public async Task CheckpointAsync_ThrowsOnNullComponent()
    {
        // Arrange
        await using var manager = CreateManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => manager.CheckpointAsync(null!));
    }

    [Fact]
    public async Task CheckpointAsync_ThrowsWhenDisposed()
    {
        // Arrange
        var manager = CreateManager();
        await manager.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => manager.CheckpointAsync(CreateComponent()));
    }

    [Fact]
    public async Task CheckpointAsync_ThrowsWhenNotSupported()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent(supportsCheckpointing: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CheckpointAsync(component));
        Assert.Contains("does not support checkpointing", ex.Message);
    }

    [Fact]
    public async Task CheckpointAsync_RespectsMinimumInterval()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.FromMinutes(5)
        };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        // Create first checkpoint
        await manager.CheckpointAsync(component);

        // Act & Assert - Second checkpoint should fail
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.CheckpointAsync(component));
        Assert.Contains("minimum interval", ex.Message);
    }

    [Fact]
    public async Task CheckpointAsync_AllowsCheckpointAfterIntervalElapsed()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.FromMilliseconds(50)
        };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        await manager.CheckpointAsync(component);
        await Task.Delay(100); // Wait for interval to elapse

        // Act
        var metadata = await manager.CheckpointAsync(component);

        // Assert
        Assert.NotNull(metadata);
    }

    [Fact]
    public async Task CheckpointAsync_RaisesCheckpointCreatedEvent()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent();
        CheckpointEventArgs? eventArgs = null;

        manager.CheckpointCreated += (sender, args) => eventArgs = args;

        // Act
        await manager.CheckpointAsync(component);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.Success);
        Assert.Equal(component.CheckpointId, eventArgs.Metadata.ComponentId);
        Assert.True(eventArgs.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckpointAsync_AutoPrunesWhenEnabled()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            EnableAutoPrune = true,
            MaxCheckpointsPerComponent = 2,
            MinCheckpointInterval = TimeSpan.Zero
        };
        await using var storage = CreateStorage();
        await using var manager = CreateManager(storage, options);
        var component = CreateComponent();

        // Act - Create 5 checkpoints
        for (var i = 0; i < 5; i++)
        {
            await manager.CheckpointAsync(component);
            await Task.Delay(10); // Ensure different timestamps
        }

        // Assert - Only 2 should remain
        var checkpoints = await storage.ListAsync(component.CheckpointId);
        Assert.Equal(2, checkpoints.Count);
    }

    [Fact]
    public async Task RestoreLatestAsync_RestoresFromLatestCheckpoint()
    {
        // Arrange
        var options = new CheckpointOptions { MinCheckpointInterval = TimeSpan.Zero };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        // Checkpoint 1: Counter = 0 (initial)
        await manager.CheckpointAsync(component);
        component.Counter = 10;
        await Task.Delay(10);

        // Checkpoint 2: Counter = 10
        await manager.CheckpointAsync(component);
        component.Counter = 20;
        await Task.Delay(10);

        // Checkpoint 3: Counter = 20 (this is the last checkpoint)
        await manager.CheckpointAsync(component);

        // Change counter after last checkpoint
        component.Counter = 999;

        // Act - restore should bring back the value from the last checkpoint (20)
        var restored = await manager.RestoreLatestAsync(component);

        // Assert
        Assert.True(restored);
        Assert.Equal(20, component.Counter); // Restored to last checkpoint value (Counter was 20 when checkpoint was taken)
    }

    [Fact]
    public async Task RestoreLatestAsync_ReturnsFalseWhenNoCheckpoint()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent();

        // Act
        var restored = await manager.RestoreLatestAsync(component);

        // Assert
        Assert.False(restored);
    }

    [Fact]
    public async Task RestoreLatestAsync_ReturnsFalseWhenNotSupported()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent(supportsCheckpointing: false);

        // Act
        var restored = await manager.RestoreLatestAsync(component);

        // Assert
        Assert.False(restored);
    }

    [Fact]
    public async Task RestoreLatestAsync_RaisesCheckpointRestoredEvent()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent();
        await manager.CheckpointAsync(component);

        CheckpointEventArgs? eventArgs = null;
        manager.CheckpointRestored += (sender, args) => eventArgs = args;

        // Act
        await manager.RestoreLatestAsync(component);

        // Assert
        Assert.NotNull(eventArgs);
        Assert.True(eventArgs.Success);
        Assert.Equal(component.CheckpointId, eventArgs.Metadata.ComponentId);
    }

    [Fact]
    public async Task RestoreFromAsync_RestoresFromSpecificCheckpoint()
    {
        // Arrange
        var options = new CheckpointOptions { MinCheckpointInterval = TimeSpan.Zero };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        var metadata1 = await manager.CheckpointAsync(component);
        component.Counter = 10;
        await Task.Delay(10);

        await manager.CheckpointAsync(component);
        component.Counter = 20;

        // Act - Restore from first checkpoint
        var restored = await manager.RestoreFromAsync(component, metadata1.CheckpointId);

        // Assert
        Assert.True(restored);
        Assert.Equal(0, component.Counter); // Restored to first checkpoint value
    }

    [Fact]
    public async Task RestoreFromAsync_ReturnsFalseWhenCheckpointNotFound()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent();

        // Act
        var restored = await manager.RestoreFromAsync(component, Guid.NewGuid());

        // Assert
        Assert.False(restored);
    }

    [Fact]
    public async Task RestoreFromAsync_ThrowsWhenCheckpointBelongsToDifferentComponent()
    {
        // Arrange
        await using var manager = CreateManager();
        var component1 = CreateComponent("component-1");
        var component2 = CreateComponent("component-2");

        var metadata = await manager.CheckpointAsync(component1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RestoreFromAsync(component2, metadata.CheckpointId));
    }

    [Fact]
    public async Task GetCheckpointsAsync_ReturnsAllCheckpoints()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.Zero,
            EnableAutoPrune = false
        };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        for (var i = 0; i < 5; i++)
        {
            await manager.CheckpointAsync(component);
            await Task.Delay(10);
        }

        // Act
        var checkpoints = await manager.GetCheckpointsAsync(component.CheckpointId);

        // Assert
        Assert.Equal(5, checkpoints.Count);
    }

    [Fact]
    public async Task GetCheckpointsAsync_RespectsMaxResults()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.Zero,
            EnableAutoPrune = false
        };
        await using var manager = CreateManager(options: options);
        var component = CreateComponent();

        for (var i = 0; i < 10; i++)
        {
            await manager.CheckpointAsync(component);
            await Task.Delay(5);
        }

        // Act
        var checkpoints = await manager.GetCheckpointsAsync(component.CheckpointId, maxResults: 3);

        // Assert
        Assert.Equal(3, checkpoints.Count);
    }

    [Fact]
    public async Task DeleteCheckpointAsync_DeletesCheckpoint()
    {
        // Arrange
        await using var storage = CreateStorage();
        await using var manager = CreateManager(storage);
        var component = CreateComponent();

        var metadata = await manager.CheckpointAsync(component);

        // Act
        var deleted = await manager.DeleteCheckpointAsync(metadata.CheckpointId);

        // Assert
        Assert.True(deleted);
        Assert.False(await storage.ExistsAsync(metadata.CheckpointId));
    }

    [Fact]
    public async Task DeleteCheckpointAsync_ReturnsFalseForNonExistent()
    {
        // Arrange
        await using var manager = CreateManager();

        // Act
        var deleted = await manager.DeleteCheckpointAsync(Guid.NewGuid());

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task PruneCheckpointsAsync_RemovesOldCheckpoints()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.Zero,
            EnableAutoPrune = false
        };
        await using var storage = CreateStorage();
        await using var manager = CreateManager(storage, options);
        var component = CreateComponent();

        for (var i = 0; i < 10; i++)
        {
            await manager.CheckpointAsync(component);
            await Task.Delay(10);
        }

        // Act
        var pruned = await manager.PruneCheckpointsAsync(component.CheckpointId, keepCount: 3);

        // Assert
        Assert.Equal(7, pruned);
        var remaining = await storage.ListAsync(component.CheckpointId);
        Assert.Equal(3, remaining.Count);
    }

    [Fact]
    public async Task PruneCheckpointsAsync_ReturnsZeroWhenNothingToPrune()
    {
        // Arrange
        await using var manager = CreateManager();
        var component = CreateComponent();

        await manager.CheckpointAsync(component);

        // Act
        var pruned = await manager.PruneCheckpointsAsync(component.CheckpointId, keepCount: 5);

        // Assert
        Assert.Equal(0, pruned);
    }

    [Fact]
    public async Task PruneCheckpointsAsync_ThrowsOnNegativeKeepCount()
    {
        // Arrange
        await using var manager = CreateManager();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => manager.PruneCheckpointsAsync("component", keepCount: -1));
    }

    [Fact]
    public async Task PruneCheckpointsAsync_KeepsNewestCheckpoints()
    {
        // Arrange
        var options = new CheckpointOptions
        {
            MinCheckpointInterval = TimeSpan.Zero,
            EnableAutoPrune = false
        };
        await using var storage = CreateStorage();
        await using var manager = CreateManager(storage, options);
        var component = CreateComponent();

        var checkpointIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var metadata = await manager.CheckpointAsync(component);
            checkpointIds.Add(metadata.CheckpointId);
            component.Counter = i + 1;
            await Task.Delay(20);
        }

        // Act
        await manager.PruneCheckpointsAsync(component.CheckpointId, keepCount: 2);

        // Assert - The two newest should remain
        Assert.True(await storage.ExistsAsync(checkpointIds[4]));
        Assert.True(await storage.ExistsAsync(checkpointIds[3]));
        Assert.False(await storage.ExistsAsync(checkpointIds[0]));
        Assert.False(await storage.ExistsAsync(checkpointIds[1]));
        Assert.False(await storage.ExistsAsync(checkpointIds[2]));
    }

    [Fact]
    public async Task DisposeAsync_DisposesStorage()
    {
        // Arrange
        await using var storage = CreateStorage();
        var manager = CreateManager(storage);
        var component = CreateComponent();
        await manager.CheckpointAsync(component);

        // Act
        await manager.DisposeAsync();

        // Assert - Storage should be disposed
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => storage.ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        // Arrange
        var manager = CreateManager();

        // Act & Assert - Should not throw
        await manager.DisposeAsync();
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentCheckpoints_ForDifferentComponents_AreThreadSafe()
    {
        // Arrange
        await using var storage = CreateStorage();
        var options = new CheckpointOptions { MinCheckpointInterval = TimeSpan.Zero };
        await using var manager = CreateManager(storage, options);

        var components = Enumerable.Range(0, 10)
            .Select(i => CreateComponent($"component-{i}"))
            .ToList();

        // Act
        var tasks = components.Select(c => manager.CheckpointAsync(c));
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        Assert.All(results, m => Assert.NotNull(m));

        var stats = await storage.GetStatisticsAsync();
        Assert.Equal(10, stats.TotalCheckpoints);
        Assert.Equal(10, stats.UniqueComponents);
    }

    /// <summary>
    /// Test implementation of ICheckpointable.
    /// </summary>
    private sealed class TestCheckpointableComponent : ICheckpointable
    {
        public string CheckpointId { get; }
        public bool SupportsCheckpointing { get; }
        public int Counter { get; set; }

        public TestCheckpointableComponent(string id, bool supportsCheckpointing = true)
        {
            CheckpointId = id;
            SupportsCheckpointing = supportsCheckpointing;
        }

        public Task<CheckpointData> CreateCheckpointAsync(CancellationToken cancellationToken = default)
        {
            var data = BitConverter.GetBytes(Counter);
            var checkpoint = new CheckpointData(
                CheckpointId,
                nameof(TestCheckpointableComponent),
                data,
                Counter,
                1);
            return Task.FromResult(checkpoint);
        }

        public Task RestoreFromCheckpointAsync(CheckpointData checkpoint, CancellationToken cancellationToken = default)
        {
            Counter = BitConverter.ToInt32(checkpoint.StateData.Span);
            return Task.CompletedTask;
        }
    }
}
