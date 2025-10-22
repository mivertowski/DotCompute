// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;
using DotCompute.Plugins.Logging;
using System.Collections.Concurrent;

namespace DotCompute.Plugins.Recovery;

/// <summary>
/// Specialized logger for plugin recovery operations with audit trail
/// </summary>
public sealed class PluginRecoveryLogger : IDisposable
{
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<RecoveryLogEntry> _auditTrail;
    private readonly Timer _auditCleanupTimer;
    private volatile bool _disposed;
    private const int MaxAuditEntries = 1000;
    /// <summary>
    /// Initializes a new instance of the PluginRecoveryLogger class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public PluginRecoveryLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditTrail = new ConcurrentQueue<RecoveryLogEntry>();

        // Clean up old audit entries periodically
        _auditCleanupTimer = new Timer(CleanupAuditTrail, null,
            TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

        _logger.LogDebugMessage("Plugin Recovery Logger initialized");
    }

    /// <summary>
    /// Logs successful recovery operation
    /// </summary>
    public void LogSuccessfulRecovery(string pluginId, PluginRecoveryStrategy strategy, TimeSpan duration)
    {
        var entry = new RecoveryLogEntry
        {
            PluginId = pluginId,
            Strategy = strategy,
            Success = true,
            Duration = duration,
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Plugin {pluginId} recovered successfully using {strategy} in {duration.TotalMilliseconds:F1}ms"
        };

        _auditTrail.Enqueue(entry);
        _logger.LogInformation("Plugin recovery successful for {PluginId} using {Strategy} in {Duration}ms",
            pluginId, strategy, duration.TotalMilliseconds);
    }

    /// <summary>
    /// Logs failed recovery operation
    /// </summary>
    public void LogFailedRecovery(string pluginId, PluginRecoveryStrategy strategy, string? errorMessage)
    {
        var entry = new RecoveryLogEntry
        {
            PluginId = pluginId,
            Strategy = strategy,
            Success = false,
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Plugin {pluginId} recovery failed using {strategy}: {errorMessage}",
            ErrorMessage = errorMessage
        };

        _auditTrail.Enqueue(entry);
        _logger.LogError("Plugin recovery failed for {PluginId} using {Strategy}: {Message}",
            pluginId, strategy, errorMessage);
    }

    /// <summary>
    /// Logs recovery attempt initiation
    /// </summary>
    public void LogRecoveryAttempt(string pluginId, PluginRecoveryStrategy strategy, Exception originalError)
    {
        var entry = new RecoveryLogEntry
        {
            PluginId = pluginId,
            Strategy = strategy,
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Starting recovery for plugin {pluginId} using {strategy}",
            OriginalError = originalError.Message
        };

        _auditTrail.Enqueue(entry);
        _logger.LogInformation("Starting recovery for plugin {PluginId} using strategy {Strategy} due to error: {Error}",
            pluginId, strategy, originalError.Message);
    }

    /// <summary>
    /// Logs circuit breaker events
    /// </summary>
    public void LogCircuitBreakerEvent(string pluginId, CircuitState newState, string reason)
    {
        var entry = new RecoveryLogEntry
        {
            PluginId = pluginId,
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Circuit breaker for plugin {pluginId} changed to {newState}: {reason}",
            CircuitState = newState
        };

        _auditTrail.Enqueue(entry);
        _logger.LogWarning("Circuit breaker for plugin {PluginId} changed to {State}: {Reason}",
            pluginId, newState, reason);
    }

    /// <summary>
    /// Logs health check events
    /// </summary>
    public void LogHealthCheckEvent(string pluginId, bool isHealthy, double uptimePercent)
    {
        var entry = new RecoveryLogEntry
        {
            PluginId = pluginId,
            Timestamp = DateTimeOffset.UtcNow,
            Message = $"Health check for plugin {pluginId}: {(isHealthy ? "Healthy" : "Unhealthy")} (Uptime: {uptimePercent:F1}%)",
            IsHealthy = isHealthy,
            UptimePercent = uptimePercent
        };

        _auditTrail.Enqueue(entry);

        if (isHealthy)
        {
            _logger.LogDebugMessage($"Plugin {pluginId} health check: Healthy (Uptime: {uptimePercent:F1}%)");
        }
        else
        {
            _logger.LogWarningMessage($"Plugin {pluginId} health check: Unhealthy (Uptime: {uptimePercent:F1}%)");
        }
    }

    /// <summary>
    /// Gets audit trail for analysis
    /// </summary>
    public IReadOnlyList<RecoveryLogEntry> GetAuditTrail(TimeSpan? timeWindow = null)
    {
        var cutoff = timeWindow.HasValue ? DateTimeOffset.UtcNow - timeWindow.Value : DateTimeOffset.MinValue;

        return _auditTrail
            .Where(entry => entry.Timestamp >= cutoff)
            .OrderByDescending(entry => entry.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets recovery statistics for a specific plugin
    /// </summary>
    public RecoveryStatistics GetPluginStatistics(string pluginId, TimeSpan? timeWindow = null)
    {
        var entries = GetAuditTrail(timeWindow)
            .Where(entry => entry.PluginId == pluginId)
            .ToList();

        var successfulRecoveries = entries.Count(e => e.Success == true);
        var failedRecoveries = entries.Count(e => e.Success == false);
        var totalAttempts = entries.Count(e => e.Success.HasValue);

        return new RecoveryStatistics
        {
            PluginId = pluginId,
            TotalAttempts = totalAttempts,
            SuccessfulRecoveries = successfulRecoveries,
            FailedRecoveries = failedRecoveries,
            SuccessRate = totalAttempts > 0 ? (double)successfulRecoveries / totalAttempts : 0.0,
            AverageRecoveryTime = entries
                .Where(e => e.Success == true && e.Duration.HasValue)
                .Select(e => e.Duration!.Value)
                .DefaultIfEmpty(TimeSpan.Zero)
                .Average(ts => ts.TotalMilliseconds),
            TimeWindow = timeWindow ?? TimeSpan.FromDays(1),
            LastRecoveryTime = entries.FirstOrDefault()?.Timestamp
        };
    }

    /// <summary>
    /// Exports audit trail for external analysis
    /// </summary>
    public async Task<string> ExportAuditTrailAsync(TimeSpan? timeWindow = null)
    {
        var entries = GetAuditTrail(timeWindow);

        try
        {
            return await Task.Run(() => System.Text.Json.JsonSerializer.Serialize(entries, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export audit trail");
            return "{}";
        }
    }

    private void CleanupAuditTrail(object? state)
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            while (_auditTrail.Count > MaxAuditEntries)
            {
                _ = _auditTrail.TryDequeue(out _);
            }

            // Also remove entries older than 7 days
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(7);
            var tempList = new List<RecoveryLogEntry>();

            while (_auditTrail.TryDequeue(out var entry))
            {
                if (entry.Timestamp >= cutoff)
                {
                    tempList.Add(entry);
                }
            }

            foreach (var entry in tempList)
            {
                _auditTrail.Enqueue(entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during audit trail cleanup");
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _auditCleanupTimer?.Dispose();
            _disposed = true;
            _logger.LogDebugMessage("Plugin Recovery Logger disposed");
        }
    }
}

/// <summary>
/// Entry in the recovery audit trail
/// </summary>
public sealed class RecoveryLogEntry
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>
    public required string PluginId { get; init; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public PluginRecoveryStrategy? Strategy { get; init; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool? Success { get; init; }
    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    /// <value>The duration.</value>
    public TimeSpan? Duration { get; init; }
    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    /// <value>The timestamp.</value>
    public DateTimeOffset Timestamp { get; init; }
    /// <summary>
    /// Gets or sets the message.
    /// </summary>
    /// <value>The message.</value>
    public required string Message { get; init; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Gets or sets the original error.
    /// </summary>
    /// <value>The original error.</value>
    public string? OriginalError { get; init; }
    /// <summary>
    /// Gets or sets the circuit state.
    /// </summary>
    /// <value>The circuit state.</value>
    public CircuitState? CircuitState { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool? IsHealthy { get; init; }
    /// <summary>
    /// Gets or sets the uptime percent.
    /// </summary>
    /// <value>The uptime percent.</value>
    public double? UptimePercent { get; init; }
}

/// <summary>
/// Recovery statistics for analysis
/// </summary>
public sealed class RecoveryStatistics
{
    /// <summary>
    /// Gets or sets the plugin identifier.
    /// </summary>
    /// <value>The plugin id.</value>
    public required string PluginId { get; init; }
    /// <summary>
    /// Gets or sets the total attempts.
    /// </summary>
    /// <value>The total attempts.</value>
    public int TotalAttempts { get; init; }
    /// <summary>
    /// Gets or sets the successful recoveries.
    /// </summary>
    /// <value>The successful recoveries.</value>
    public int SuccessfulRecoveries { get; init; }
    /// <summary>
    /// Gets or sets the failed recoveries.
    /// </summary>
    /// <value>The failed recoveries.</value>
    public int FailedRecoveries { get; init; }
    /// <summary>
    /// Gets or sets the success rate.
    /// </summary>
    /// <value>The success rate.</value>
    public double SuccessRate { get; init; }
    /// <summary>
    /// Gets or sets the average recovery time.
    /// </summary>
    /// <value>The average recovery time.</value>
    public double AverageRecoveryTime { get; init; }
    /// <summary>
    /// Gets or sets the time window.
    /// </summary>
    /// <value>The time window.</value>
    public TimeSpan TimeWindow { get; init; }
    /// <summary>
    /// Gets or sets the last recovery time.
    /// </summary>
    /// <value>The last recovery time.</value>
    public DateTimeOffset? LastRecoveryTime { get; init; }
}