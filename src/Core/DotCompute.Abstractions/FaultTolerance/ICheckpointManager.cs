// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace DotCompute.Abstractions.FaultTolerance;

/// <summary>
/// Represents a component that can be checkpointed and restored.
/// </summary>
/// <remarks>
/// Ring Kernels implementing this interface can have their state
/// saved and restored for fault tolerance and recovery scenarios.
/// </remarks>
public interface ICheckpointable
{
    /// <summary>
    /// Gets the unique identifier for this checkpointable component.
    /// </summary>
    public string CheckpointId { get; }

    /// <summary>
    /// Gets a value indicating whether the component supports checkpointing.
    /// </summary>
    public bool SupportsCheckpointing { get; }

    /// <summary>
    /// Creates a checkpoint of the current state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint data.</returns>
    public Task<CheckpointData> CreateCheckpointAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores state from a checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RestoreFromCheckpointAsync(CheckpointData checkpoint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a point-in-time snapshot of component state.
/// </summary>
public sealed class CheckpointData : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the unique checkpoint identifier.
    /// </summary>
    public Guid CheckpointId { get; }

    /// <summary>
    /// Gets the identifier of the component that created this checkpoint.
    /// </summary>
    public string ComponentId { get; }

    /// <summary>
    /// Gets the component type name.
    /// </summary>
    public string ComponentType { get; }

    /// <summary>
    /// Gets when the checkpoint was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the checkpoint version for compatibility checking.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Gets the serialized state data.
    /// </summary>
    public ReadOnlyMemory<byte> StateData { get; }

    /// <summary>
    /// Gets the sequence number at checkpoint time.
    /// </summary>
    public long SequenceNumber { get; }

    /// <summary>
    /// Gets optional metadata associated with the checkpoint.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; }

    /// <summary>
    /// Gets the size of the checkpoint in bytes.
    /// </summary>
    public long Size => StateData.Length;

    /// <summary>
    /// Creates a new checkpoint data instance.
    /// </summary>
    public CheckpointData(
        string componentId,
        string componentType,
        ReadOnlyMemory<byte> stateData,
        long sequenceNumber,
        int version = 1,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        CheckpointId = Guid.NewGuid();
        ComponentId = componentId ?? throw new ArgumentNullException(nameof(componentId));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        CreatedAt = DateTimeOffset.UtcNow;
        StateData = stateData;
        SequenceNumber = sequenceNumber;
        Version = version;
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a checkpoint data instance from stored data.
    /// </summary>
    public CheckpointData(
        Guid checkpointId,
        string componentId,
        string componentType,
        DateTimeOffset createdAt,
        ReadOnlyMemory<byte> stateData,
        long sequenceNumber,
        int version,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        CheckpointId = checkpointId;
        ComponentId = componentId ?? throw new ArgumentNullException(nameof(componentId));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
        CreatedAt = createdAt;
        StateData = stateData;
        SequenceNumber = sequenceNumber;
        Version = version;
        Metadata = metadata;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // StateData memory is managed by caller
    }
}

/// <summary>
/// Manages checkpoint creation, storage, and restoration.
/// </summary>
public interface ICheckpointManager : IAsyncDisposable
{
    /// <summary>
    /// Gets the checkpoint storage being used.
    /// </summary>
    public ICheckpointStorage Storage { get; }

    /// <summary>
    /// Creates and stores a checkpoint for a component.
    /// </summary>
    /// <param name="component">The component to checkpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored checkpoint metadata.</returns>
    public Task<CheckpointMetadata> CheckpointAsync(
        ICheckpointable component,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a component from its latest checkpoint.
    /// </summary>
    /// <param name="component">The component to restore.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if restored successfully, false if no checkpoint found.</returns>
    public Task<bool> RestoreLatestAsync(
        ICheckpointable component,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a component from a specific checkpoint.
    /// </summary>
    /// <param name="component">The component to restore.</param>
    /// <param name="checkpointId">The checkpoint ID to restore from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if restored successfully.</returns>
    public Task<bool> RestoreFromAsync(
        ICheckpointable component,
        Guid checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all checkpoints for a component.
    /// </summary>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoint metadata.</returns>
    public Task<IReadOnlyList<CheckpointMetadata>> GetCheckpointsAsync(
        string componentId,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted.</returns>
    public Task<bool> DeleteCheckpointAsync(
        Guid checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old checkpoints, keeping only the most recent.
    /// </summary>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="keepCount">Number of checkpoints to keep.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of checkpoints deleted.</returns>
    public Task<int> PruneCheckpointsAsync(
        string componentId,
        int keepCount = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Occurs when a checkpoint is created.
    /// </summary>
    public event EventHandler<CheckpointEventArgs>? CheckpointCreated;

    /// <summary>
    /// Occurs when a checkpoint is restored.
    /// </summary>
    public event EventHandler<CheckpointEventArgs>? CheckpointRestored;
}

/// <summary>
/// Metadata about a stored checkpoint.
/// </summary>
public sealed record CheckpointMetadata
{
    /// <summary>
    /// Gets the checkpoint identifier.
    /// </summary>
    public required Guid CheckpointId { get; init; }

    /// <summary>
    /// Gets the component identifier.
    /// </summary>
    public required string ComponentId { get; init; }

    /// <summary>
    /// Gets the component type.
    /// </summary>
    public required string ComponentType { get; init; }

    /// <summary>
    /// Gets when the checkpoint was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the checkpoint size in bytes.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    /// Gets the sequence number at checkpoint time.
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Gets the checkpoint version.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets the storage location or identifier.
    /// </summary>
    public string? StorageLocation { get; init; }
}

/// <summary>
/// Event arguments for checkpoint events.
/// </summary>
public sealed class CheckpointEventArgs : EventArgs
{
    /// <summary>
    /// Gets the checkpoint metadata.
    /// </summary>
    public required CheckpointMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the operation duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets any error that occurred.
    /// </summary>
    public Exception? Error { get; init; }
}

/// <summary>
/// Storage backend for checkpoints.
/// </summary>
public interface ICheckpointStorage : IAsyncDisposable
{
    /// <summary>
    /// Gets the storage name/type.
    /// </summary>
    public string StorageName { get; }

    /// <summary>
    /// Stores a checkpoint.
    /// </summary>
    /// <param name="checkpoint">The checkpoint data to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The storage location identifier.</returns>
    public Task<string> StoreAsync(
        CheckpointData checkpoint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a checkpoint by ID.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint data, or null if not found.</returns>
    public Task<CheckpointData?> RetrieveAsync(
        Guid checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest checkpoint for a component.
    /// </summary>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The checkpoint data, or null if not found.</returns>
    public Task<CheckpointData?> RetrieveLatestAsync(
        string componentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists checkpoint metadata for a component.
    /// </summary>
    /// <param name="componentId">The component identifier.</param>
    /// <param name="maxResults">Maximum results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of checkpoint metadata.</returns>
    public Task<IReadOnlyList<CheckpointMetadata>> ListAsync(
        string componentId,
        int maxResults = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a checkpoint.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted.</returns>
    public Task<bool> DeleteAsync(
        Guid checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a checkpoint exists.
    /// </summary>
    /// <param name="checkpointId">The checkpoint ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if exists.</returns>
    public Task<bool> ExistsAsync(
        Guid checkpointId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage statistics.</returns>
    public Task<CheckpointStorageStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about checkpoint storage.
/// </summary>
public sealed record CheckpointStorageStatistics
{
    /// <summary>
    /// Gets the total number of checkpoints stored.
    /// </summary>
    public required int TotalCheckpoints { get; init; }

    /// <summary>
    /// Gets the total storage size in bytes.
    /// </summary>
    public required long TotalSizeBytes { get; init; }

    /// <summary>
    /// Gets the number of unique components with checkpoints.
    /// </summary>
    public required int UniqueComponents { get; init; }

    /// <summary>
    /// Gets the oldest checkpoint date.
    /// </summary>
    public DateTimeOffset? OldestCheckpoint { get; init; }

    /// <summary>
    /// Gets the newest checkpoint date.
    /// </summary>
    public DateTimeOffset? NewestCheckpoint { get; init; }

    /// <summary>
    /// Gets when statistics were captured.
    /// </summary>
    public required DateTimeOffset CapturedAt { get; init; }
}

/// <summary>
/// Configuration options for checkpoint management.
/// </summary>
public sealed class CheckpointOptions
{
    /// <summary>
    /// Gets or sets whether automatic checkpointing is enabled.
    /// Default: false.
    /// </summary>
    public bool EnableAutoCheckpoint { get; init; }

    /// <summary>
    /// Gets or sets the interval for automatic checkpointing.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan AutoCheckpointInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum checkpoints to retain per component.
    /// Default: 5.
    /// </summary>
    public int MaxCheckpointsPerComponent { get; init; } = 5;

    /// <summary>
    /// Gets or sets whether to auto-prune old checkpoints.
    /// Default: true.
    /// </summary>
    public bool EnableAutoPrune { get; init; } = true;

    /// <summary>
    /// Gets or sets the checkpoint retention period.
    /// Default: 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets whether to compress checkpoint data.
    /// Default: true.
    /// </summary>
    public bool EnableCompression { get; init; } = true;

    /// <summary>
    /// Gets or sets the minimum interval between checkpoints.
    /// Prevents excessive checkpointing.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan MinCheckpointInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default checkpoint options.
    /// </summary>
    public static CheckpointOptions Default { get; } = new();
}

/// <summary>
/// Result of a checkpoint operation.
/// </summary>
public sealed record CheckpointResult
{
    /// <summary>
    /// Gets whether the operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the checkpoint metadata if successful.
    /// </summary>
    public CheckpointMetadata? Metadata { get; init; }

    /// <summary>
    /// Gets the operation duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets any error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the exception if failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static CheckpointResult Succeeded(CheckpointMetadata metadata, TimeSpan duration) => new()
    {
        Success = true,
        Metadata = metadata,
        Duration = duration
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static CheckpointResult Failed(string errorMessage, TimeSpan duration, Exception? exception = null) => new()
    {
        Success = false,
        Duration = duration,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}
