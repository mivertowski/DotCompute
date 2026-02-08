// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.FaultTolerance;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.FaultTolerance;

/// <summary>
/// In-memory checkpoint storage implementation.
/// </summary>
/// <remarks>
/// Suitable for development and testing. For production, use a persistent
/// storage implementation like file-based or database storage.
/// </remarks>
public sealed partial class InMemoryCheckpointStorage : ICheckpointStorage
{
    private readonly ConcurrentDictionary<Guid, StoredCheckpoint> _checkpoints = new();
    private readonly ConcurrentDictionary<string, List<Guid>> _componentIndex = new();
    private readonly ILogger<InMemoryCheckpointStorage> _logger;
    private readonly object _lock = new();
    private bool _disposed;

    // Event IDs: 9700-9799 for InMemoryCheckpointStorage
    [LoggerMessage(EventId = 9700, Level = LogLevel.Debug,
        Message = "Checkpoint {CheckpointId} stored for component {ComponentId}")]
    private static partial void LogCheckpointStored(ILogger logger, Guid checkpointId, string componentId);

    [LoggerMessage(EventId = 9701, Level = LogLevel.Debug,
        Message = "Checkpoint {CheckpointId} retrieved")]
    private static partial void LogCheckpointRetrieved(ILogger logger, Guid checkpointId);

    [LoggerMessage(EventId = 9702, Level = LogLevel.Debug,
        Message = "Checkpoint {CheckpointId} deleted")]
    private static partial void LogCheckpointDeleted(ILogger logger, Guid checkpointId);

    [LoggerMessage(EventId = 9703, Level = LogLevel.Warning,
        Message = "Checkpoint {CheckpointId} not found")]
    private static partial void LogCheckpointNotFound(ILogger logger, Guid checkpointId);

    /// <inheritdoc />
    public string StorageName => "InMemory";

    /// <summary>
    /// Creates a new in-memory checkpoint storage.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public InMemoryCheckpointStorage(ILogger<InMemoryCheckpointStorage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<string> StoreAsync(CheckpointData checkpoint, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(checkpoint);

        cancellationToken.ThrowIfCancellationRequested();

        var stored = new StoredCheckpoint
        {
            CheckpointId = checkpoint.CheckpointId,
            ComponentId = checkpoint.ComponentId,
            ComponentType = checkpoint.ComponentType,
            CreatedAt = checkpoint.CreatedAt,
            StateData = checkpoint.StateData.ToArray(),
            SequenceNumber = checkpoint.SequenceNumber,
            Version = checkpoint.Version,
            Metadata = checkpoint.Metadata
        };

        _checkpoints[checkpoint.CheckpointId] = stored;

        lock (_lock)
        {
            if (!_componentIndex.TryGetValue(checkpoint.ComponentId, out var list))
            {
                list = new List<Guid>();
                _componentIndex[checkpoint.ComponentId] = list;
            }

            list.Add(checkpoint.CheckpointId);
        }

        LogCheckpointStored(_logger, checkpoint.CheckpointId, checkpoint.ComponentId);

        return Task.FromResult($"memory://{checkpoint.CheckpointId}");
    }

    /// <inheritdoc />
    public Task<CheckpointData?> RetrieveAsync(Guid checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_checkpoints.TryGetValue(checkpointId, out var stored))
        {
            LogCheckpointRetrieved(_logger, checkpointId);
            return Task.FromResult<CheckpointData?>(stored.ToCheckpointData());
        }

        LogCheckpointNotFound(_logger, checkpointId);
        return Task.FromResult<CheckpointData?>(null);
    }

    /// <inheritdoc />
    public Task<CheckpointData?> RetrieveLatestAsync(string componentId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(componentId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            if (!_componentIndex.TryGetValue(componentId, out var checkpointIds) || checkpointIds.Count == 0)
            {
                return Task.FromResult<CheckpointData?>(null);
            }

            // Find the latest by creation time
            StoredCheckpoint? latest = null;
            foreach (var id in checkpointIds)
            {
                if (_checkpoints.TryGetValue(id, out var checkpoint))
                {
                    if (latest == null || checkpoint.CreatedAt > latest.CreatedAt)
                    {
                        latest = checkpoint;
                    }
                }
            }

            if (latest != null)
            {
                LogCheckpointRetrieved(_logger, latest.CheckpointId);
                return Task.FromResult<CheckpointData?>(latest.ToCheckpointData());
            }
        }

        return Task.FromResult<CheckpointData?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CheckpointMetadata>> ListAsync(
        string componentId,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(componentId);
        cancellationToken.ThrowIfCancellationRequested();

        var results = new List<CheckpointMetadata>();

        lock (_lock)
        {
            if (_componentIndex.TryGetValue(componentId, out var checkpointIds))
            {
                foreach (var id in checkpointIds.OrderByDescending(id =>
                    _checkpoints.TryGetValue(id, out var c) ? c.CreatedAt : DateTimeOffset.MinValue))
                {
                    if (results.Count >= maxResults)
                    {
                        break;
                    }

                    if (_checkpoints.TryGetValue(id, out var checkpoint))
                    {
                        results.Add(checkpoint.ToMetadata());
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<CheckpointMetadata>>(results);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_checkpoints.TryRemove(checkpointId, out var removed))
        {
            lock (_lock)
            {
                if (_componentIndex.TryGetValue(removed.ComponentId, out var list))
                {
                    list.Remove(checkpointId);
                    if (list.Count == 0)
                    {
                        _componentIndex.TryRemove(removed.ComponentId, out _);
                    }
                }
            }

            LogCheckpointDeleted(_logger, checkpointId);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid checkpointId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_checkpoints.ContainsKey(checkpointId));
    }

    /// <inheritdoc />
    public Task<CheckpointStorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var checkpoints = _checkpoints.Values.ToList();

        var stats = new CheckpointStorageStatistics
        {
            TotalCheckpoints = checkpoints.Count,
            TotalSizeBytes = checkpoints.Sum(c => c.StateData.Length),
            UniqueComponents = _componentIndex.Count,
            OldestCheckpoint = checkpoints.MinBy(c => c.CreatedAt)?.CreatedAt,
            NewestCheckpoint = checkpoints.MaxBy(c => c.CreatedAt)?.CreatedAt,
            CapturedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(stats);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _checkpoints.Clear();
        _componentIndex.Clear();

        return ValueTask.CompletedTask;
    }

    private sealed class StoredCheckpoint
    {
        public required Guid CheckpointId { get; init; }
        public required string ComponentId { get; init; }
        public required string ComponentType { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required byte[] StateData { get; init; }
        public required long SequenceNumber { get; init; }
        public required int Version { get; init; }
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        public CheckpointData ToCheckpointData() => new(
            CheckpointId,
            ComponentId,
            ComponentType,
            CreatedAt,
            StateData,
            SequenceNumber,
            Version,
            Metadata);

        public CheckpointMetadata ToMetadata() => new()
        {
            CheckpointId = CheckpointId,
            ComponentId = ComponentId,
            ComponentType = ComponentType,
            CreatedAt = CreatedAt,
            Size = StateData.Length,
            SequenceNumber = SequenceNumber,
            Version = Version,
            StorageLocation = $"memory://{CheckpointId}"
        };
    }
}
