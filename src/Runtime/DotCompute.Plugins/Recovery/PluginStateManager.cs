// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Plugins.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Manages plugin state persistence and recovery data
/// </summary>
public sealed class PluginStateManager : IDisposable
{
    private readonly PluginRecoveryConfiguration _config;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, PluginStateSnapshot> _stateSnapshots;
    private readonly Timer _stateBackupTimer;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the PluginStateManager class.
    /// </summary>
    /// <param name="config">The config.</param>
    /// <param name="logger">The logger.</param>

    public PluginStateManager(PluginRecoveryConfiguration config, ILogger logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateSnapshots = new ConcurrentDictionary<string, PluginStateSnapshot>();

        // Start periodic state backup
        _stateBackupTimer = new Timer(BackupStates, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogDebugMessage("Plugin State Manager initialized");
    }

    /// <summary>
    /// Creates a snapshot of plugin state for recovery purposes
    /// </summary>
    public PluginStateSnapshot CreateStateSnapshot(string pluginId, object? state = null)
    {
        var snapshot = new PluginStateSnapshot
        {
            PluginId = pluginId,
            Timestamp = DateTimeOffset.UtcNow,
            State = state,
            IsValid = true
        };

        _ = _stateSnapshots.AddOrUpdate(pluginId, snapshot, (key, existing) => snapshot);

        _logger.LogDebugMessage($"State snapshot created for plugin {pluginId}");
        return snapshot;
    }

    /// <summary>
    /// Restores plugin state from the latest snapshot
    /// </summary>
    public PluginStateSnapshot? RestoreState(string pluginId)
    {
        if (_stateSnapshots.TryGetValue(pluginId, out var snapshot) && snapshot.IsValid)
        {
            _logger.LogDebugMessage($"State restored for plugin {pluginId} from snapshot at {snapshot.Timestamp}");
            return snapshot;
        }

        _logger.LogWarningMessage($"No valid state snapshot found for plugin {pluginId}");
        return null;
    }

    /// <summary>
    /// Clears state snapshot for a plugin
    /// </summary>
    public bool ClearState(string pluginId)
    {
        var removed = _stateSnapshots.TryRemove(pluginId, out var snapshot);
        if (removed && snapshot != null)
        {
            snapshot.IsValid = false;
            _logger.LogDebugMessage($"State cleared for plugin {pluginId}");
        }
        return removed;
    }

    /// <summary>
    /// Gets all state snapshots for monitoring
    /// </summary>
    public IReadOnlyDictionary<string, PluginStateSnapshot> GetAllSnapshots() => _stateSnapshots.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Validates state snapshot integrity
    /// </summary>
    public bool ValidateSnapshot(string pluginId)
    {
        if (_stateSnapshots.TryGetValue(pluginId, out var snapshot))
        {
            var age = DateTimeOffset.UtcNow - snapshot.Timestamp;
            var isValid = snapshot.IsValid && age < _config.StateSnapshotMaxAge;

            if (!isValid)
            {
                snapshot.IsValid = false;
                _logger.LogWarningMessage($"State snapshot for plugin {pluginId} is invalid or expired");
            }

            return isValid;
        }

        return false;
    }

    /// <summary>
    /// Exports state snapshots for backup
    /// </summary>
    public async Task<byte[]> ExportStatesAsync()
    {
        try
        {
            var states = _stateSnapshots.Values.Where(s => s.IsValid).ToList();
            var json = await Task.Run(() => System.Text.Json.JsonSerializer.Serialize(states));
            return System.Text.Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export plugin states");
            return [];
        }
    }

    /// <summary>
    /// Imports state snapshots from backup
    /// </summary>
    public async Task<bool> ImportStatesAsync(byte[] data)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(data);
            var states = await Task.Run(() => System.Text.Json.JsonSerializer.Deserialize<List<PluginStateSnapshot>>(json));

            if (states != null)
            {
                foreach (var state in states)
                {
                    _ = _stateSnapshots.TryAdd(state.PluginId, state);
                }

                _logger.LogInformation("Imported {Count} plugin state snapshots", states.Count);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import plugin states");
        }

        return false;
    }

    private void BackupStates(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            // Clean up expired snapshots
            var expiredKeys = _stateSnapshots
                .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.Timestamp > _config.StateSnapshotMaxAge)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_stateSnapshots.TryRemove(key, out var snapshot))
                {
                    snapshot.IsValid = false;
                }
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebugMessage($"Cleaned up {expiredKeys.Count} expired state snapshots");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during state backup cleanup");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _stateBackupTimer?.Dispose();

            // Invalidate all snapshots
            foreach (var snapshot in _stateSnapshots.Values)
            {
                snapshot.IsValid = false;
            }
            _stateSnapshots.Clear();

            _disposed = true;
            _logger.LogDebugMessage("Plugin State Manager disposed");
        }
    }
}

/// <summary>
/// Represents a snapshot of plugin state for recovery
/// </summary>
public sealed class PluginStateSnapshot
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>
    public required string PluginId { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    /// <value>The state.</value>
    public object? State { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether valid.
    /// </summary>
    /// <value>The is valid.</value>
    public bool IsValid { get; set; } = true;
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    /// <value>The metadata.</value>
    public Dictionary<string, object> Metadata { get; init; } = [];
}
