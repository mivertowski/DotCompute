// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using DotCompute.Abstractions.FaultTolerance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.FaultTolerance;

/// <summary>
/// Manages checkpoint creation, storage, and restoration for fault tolerance.
/// </summary>
/// <remarks>
/// Provides automatic checkpointing, pruning, and compression capabilities
/// for components implementing <see cref="ICheckpointable"/>.
/// </remarks>
public sealed partial class CheckpointManager : ICheckpointManager
{
    private readonly ICheckpointStorage _storage;
    private readonly CheckpointOptions _options;
    private readonly ILogger<CheckpointManager> _logger;
    private readonly Dictionary<string, DateTimeOffset> _lastCheckpointTimes = new();
    private readonly object _lock = new();
    private bool _disposed;

    // Event IDs: 9800-9899 for CheckpointManager
    [LoggerMessage(EventId = 9800, Level = LogLevel.Information,
        Message = "Checkpoint created for component {ComponentId}: {CheckpointId} ({Size} bytes)")]
    private static partial void LogCheckpointCreated(
        ILogger logger, string componentId, Guid checkpointId, long size);

    [LoggerMessage(EventId = 9801, Level = LogLevel.Information,
        Message = "Checkpoint restored for component {ComponentId} from {CheckpointId}")]
    private static partial void LogCheckpointRestored(
        ILogger logger, string componentId, Guid checkpointId);

    [LoggerMessage(EventId = 9802, Level = LogLevel.Warning,
        Message = "No checkpoint found for component {ComponentId}")]
    private static partial void LogNoCheckpointFound(ILogger logger, string componentId);

    [LoggerMessage(EventId = 9803, Level = LogLevel.Warning,
        Message = "Checkpoint {CheckpointId} not found")]
    private static partial void LogCheckpointNotFound(ILogger logger, Guid checkpointId);

    [LoggerMessage(EventId = 9804, Level = LogLevel.Debug,
        Message = "Checkpoint skipped for component {ComponentId}: minimum interval not elapsed")]
    private static partial void LogCheckpointSkipped(ILogger logger, string componentId);

    [LoggerMessage(EventId = 9805, Level = LogLevel.Information,
        Message = "Pruned {Count} checkpoints for component {ComponentId}")]
    private static partial void LogCheckpointsPruned(ILogger logger, int count, string componentId);

    [LoggerMessage(EventId = 9806, Level = LogLevel.Error,
        Message = "Checkpoint operation failed for component {ComponentId}")]
    private static partial void LogCheckpointFailed(
        ILogger logger, Exception ex, string componentId);

    [LoggerMessage(EventId = 9807, Level = LogLevel.Warning,
        Message = "Component {ComponentId} does not support checkpointing")]
    private static partial void LogCheckpointingNotSupported(ILogger logger, string componentId);

    /// <inheritdoc />
    public ICheckpointStorage Storage => _storage;

    /// <inheritdoc />
    public event EventHandler<CheckpointEventArgs>? CheckpointCreated;

    /// <inheritdoc />
    public event EventHandler<CheckpointEventArgs>? CheckpointRestored;

    /// <summary>
    /// Creates a new checkpoint manager.
    /// </summary>
    /// <param name="storage">The checkpoint storage backend.</param>
    /// <param name="options">Checkpoint configuration options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public CheckpointManager(
        ICheckpointStorage storage,
        IOptions<CheckpointOptions> options,
        ILogger<CheckpointManager> logger)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _options = options?.Value ?? CheckpointOptions.Default;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new checkpoint manager with default options.
    /// </summary>
    /// <param name="storage">The checkpoint storage backend.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public CheckpointManager(
        ICheckpointStorage storage,
        ILogger<CheckpointManager> logger)
        : this(storage, Options.Create(CheckpointOptions.Default), logger)
    {
    }

    /// <inheritdoc />
    public async Task<CheckpointMetadata> CheckpointAsync(
        ICheckpointable component,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(component);

        if (!component.SupportsCheckpointing)
        {
            LogCheckpointingNotSupported(_logger, component.CheckpointId);
            throw new InvalidOperationException(
                $"Component {component.CheckpointId} does not support checkpointing.");
        }

        // Check minimum interval
        if (!ShouldCheckpoint(component.CheckpointId))
        {
            LogCheckpointSkipped(_logger, component.CheckpointId);
            throw new InvalidOperationException(
                $"Checkpoint skipped: minimum interval ({_options.MinCheckpointInterval}) not elapsed.");
        }

        var stopwatch = Stopwatch.StartNew();
        CheckpointData? checkpoint = null;

        try
        {
            // Create checkpoint
            checkpoint = await component.CreateCheckpointAsync(cancellationToken).ConfigureAwait(false);

            // Store checkpoint
            var storageLocation = await _storage.StoreAsync(checkpoint, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            var metadata = new CheckpointMetadata
            {
                CheckpointId = checkpoint.CheckpointId,
                ComponentId = checkpoint.ComponentId,
                ComponentType = checkpoint.ComponentType,
                CreatedAt = checkpoint.CreatedAt,
                Size = checkpoint.Size,
                SequenceNumber = checkpoint.SequenceNumber,
                Version = checkpoint.Version,
                StorageLocation = storageLocation
            };

            // Update last checkpoint time
            UpdateLastCheckpointTime(component.CheckpointId);

            LogCheckpointCreated(_logger, checkpoint.ComponentId, checkpoint.CheckpointId, checkpoint.Size);

            // Raise event
            OnCheckpointCreated(metadata, stopwatch.Elapsed, true);

            // Auto-prune if enabled
            if (_options.EnableAutoPrune)
            {
                await PruneCheckpointsAsync(
                    component.CheckpointId,
                    _options.MaxCheckpointsPerComponent,
                    cancellationToken).ConfigureAwait(false);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogCheckpointFailed(_logger, ex, component.CheckpointId);

            if (checkpoint != null)
            {
                OnCheckpointCreated(
                    CreateErrorMetadata(checkpoint),
                    stopwatch.Elapsed,
                    false,
                    ex);
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RestoreLatestAsync(
        ICheckpointable component,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(component);

        if (!component.SupportsCheckpointing)
        {
            LogCheckpointingNotSupported(_logger, component.CheckpointId);
            return false;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var checkpoint = await _storage.RetrieveLatestAsync(
                component.CheckpointId,
                cancellationToken).ConfigureAwait(false);

            if (checkpoint == null)
            {
                LogNoCheckpointFound(_logger, component.CheckpointId);
                return false;
            }

            await component.RestoreFromCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            LogCheckpointRestored(_logger, component.CheckpointId, checkpoint.CheckpointId);

            var metadata = CreateMetadata(checkpoint);
            OnCheckpointRestored(metadata, stopwatch.Elapsed, true);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogCheckpointFailed(_logger, ex, component.CheckpointId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> RestoreFromAsync(
        ICheckpointable component,
        Guid checkpointId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(component);

        if (!component.SupportsCheckpointing)
        {
            LogCheckpointingNotSupported(_logger, component.CheckpointId);
            return false;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var checkpoint = await _storage.RetrieveAsync(checkpointId, cancellationToken).ConfigureAwait(false);

            if (checkpoint == null)
            {
                LogCheckpointNotFound(_logger, checkpointId);
                return false;
            }

            // Verify checkpoint belongs to this component
            if (checkpoint.ComponentId != component.CheckpointId)
            {
                throw new InvalidOperationException(
                    $"Checkpoint {checkpointId} belongs to component {checkpoint.ComponentId}, " +
                    $"not {component.CheckpointId}.");
            }

            await component.RestoreFromCheckpointAsync(checkpoint, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            LogCheckpointRestored(_logger, component.CheckpointId, checkpointId);

            var metadata = CreateMetadata(checkpoint);
            OnCheckpointRestored(metadata, stopwatch.Elapsed, true);

            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogCheckpointFailed(_logger, ex, component.CheckpointId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckpointMetadata>> GetCheckpointsAsync(
        string componentId,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(componentId);

        return await _storage.ListAsync(componentId, maxResults, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCheckpointAsync(
        Guid checkpointId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await _storage.DeleteAsync(checkpointId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PruneCheckpointsAsync(
        string componentId,
        int keepCount = 3,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(componentId);

        if (keepCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(keepCount), "Keep count must be non-negative.");
        }

        var checkpoints = await _storage.ListAsync(componentId, int.MaxValue, cancellationToken)
            .ConfigureAwait(false);

        if (checkpoints.Count <= keepCount)
        {
            return 0;
        }

        // Checkpoints are ordered newest first, so skip keepCount and delete the rest
        var toDelete = checkpoints.Skip(keepCount).ToList();
        var deletedCount = 0;

        foreach (var checkpoint in toDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _storage.DeleteAsync(checkpoint.CheckpointId, cancellationToken).ConfigureAwait(false))
            {
                deletedCount++;
            }
        }

        if (deletedCount > 0)
        {
            LogCheckpointsPruned(_logger, deletedCount, componentId);
        }

        return deletedCount;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _storage.DisposeAsync().ConfigureAwait(false);

        lock (_lock)
        {
            _lastCheckpointTimes.Clear();
        }
    }

    private bool ShouldCheckpoint(string componentId)
    {
        lock (_lock)
        {
            if (!_lastCheckpointTimes.TryGetValue(componentId, out var lastTime))
            {
                return true;
            }

            return DateTimeOffset.UtcNow - lastTime >= _options.MinCheckpointInterval;
        }
    }

    private void UpdateLastCheckpointTime(string componentId)
    {
        lock (_lock)
        {
            _lastCheckpointTimes[componentId] = DateTimeOffset.UtcNow;
        }
    }

    private void OnCheckpointCreated(
        CheckpointMetadata metadata,
        TimeSpan duration,
        bool success,
        Exception? error = null)
    {
        CheckpointCreated?.Invoke(this, new CheckpointEventArgs
        {
            Metadata = metadata,
            Duration = duration,
            Success = success,
            Error = error
        });
    }

    private void OnCheckpointRestored(
        CheckpointMetadata metadata,
        TimeSpan duration,
        bool success,
        Exception? error = null)
    {
        CheckpointRestored?.Invoke(this, new CheckpointEventArgs
        {
            Metadata = metadata,
            Duration = duration,
            Success = success,
            Error = error
        });
    }

    private static CheckpointMetadata CreateMetadata(CheckpointData checkpoint) => new()
    {
        CheckpointId = checkpoint.CheckpointId,
        ComponentId = checkpoint.ComponentId,
        ComponentType = checkpoint.ComponentType,
        CreatedAt = checkpoint.CreatedAt,
        Size = checkpoint.Size,
        SequenceNumber = checkpoint.SequenceNumber,
        Version = checkpoint.Version
    };

    private static CheckpointMetadata CreateErrorMetadata(CheckpointData checkpoint) => new()
    {
        CheckpointId = checkpoint.CheckpointId,
        ComponentId = checkpoint.ComponentId,
        ComponentType = checkpoint.ComponentType,
        CreatedAt = checkpoint.CreatedAt,
        Size = checkpoint.Size,
        SequenceNumber = checkpoint.SequenceNumber,
        Version = checkpoint.Version
    };
}
