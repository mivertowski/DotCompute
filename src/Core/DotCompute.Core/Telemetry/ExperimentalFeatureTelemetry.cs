// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Telemetry;

/// <summary>
/// Provides telemetry tracking for experimental feature usage.
/// Helps the team understand which experimental features are being used
/// and guides stabilization priorities.
/// </summary>
/// <remarks>
/// All telemetry is local-only by default. No data is sent externally
/// unless explicitly configured by the user.
/// </remarks>
public static class ExperimentalFeatureTelemetry
{
    private static readonly ConcurrentDictionary<string, FeatureUsageStats> _usageStats = new();
    private static readonly ActivitySource _activitySource = new("DotCompute.Experimental");
    private static ILogger? _logger;
    private static bool _enabled = true;

    /// <summary>
    /// Gets or sets whether telemetry is enabled.
    /// </summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Configures the telemetry logger.
    /// </summary>
    /// <param name="logger">Logger for telemetry output.</param>
    public static void Configure(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records usage of an experimental feature.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic ID (e.g., DOTCOMPUTE0001).</param>
    /// <param name="featureName">Human-readable feature name.</param>
    /// <param name="context">Optional context about the usage.</param>
    public static void RecordUsage(string diagnosticId, string featureName, string? context = null)
    {
        if (!_enabled)
        {
            return;
        }

        var stats = _usageStats.GetOrAdd(diagnosticId, id => new FeatureUsageStats
        {
            DiagnosticId = id,
            FeatureName = featureName,
            FirstUsed = DateTime.UtcNow
        });

        stats.UsageCount++;
        stats.LastUsed = DateTime.UtcNow;

        if (context != null)
        {
            stats.Contexts.TryAdd(context, 0);
            stats.Contexts.AddOrUpdate(context, 1, (_, count) => count + 1);
        }

        // Log first usage at Info level, subsequent at Debug
        if (stats.UsageCount == 1)
        {
            _logger?.LogInformation(
                "First usage of experimental feature {DiagnosticId} ({FeatureName}). " +
                "See https://github.com/mivertowski/DotCompute/blob/main/docs/diagnostics/{DiagnosticId}.md for details.",
                diagnosticId, featureName, diagnosticId);
        }
        else
        {
            _logger?.LogDebug(
                "Experimental feature {DiagnosticId} used (count: {UsageCount})",
                diagnosticId, stats.UsageCount);
        }

        // Create activity for distributed tracing
        using var activity = _activitySource.StartActivity($"Experimental.{diagnosticId}");
        activity?.SetTag("experimental.diagnostic_id", diagnosticId);
        activity?.SetTag("experimental.feature_name", featureName);
        activity?.SetTag("experimental.usage_count", stats.UsageCount);
        if (context != null)
        {
            activity?.SetTag("experimental.context", context);
        }
    }

    /// <summary>
    /// Records an error in an experimental feature.
    /// </summary>
    /// <param name="diagnosticId">The diagnostic ID.</param>
    /// <param name="error">The error that occurred.</param>
    public static void RecordError(string diagnosticId, Exception error)
    {
        if (!_enabled)
        {
            return;
        }

        if (_usageStats.TryGetValue(diagnosticId, out var stats))
        {
            stats.ErrorCount++;
            stats.LastError = error.Message;
            stats.LastErrorTime = DateTime.UtcNow;
        }

        _logger?.LogWarning(error,
            "Error in experimental feature {DiagnosticId}: {ErrorMessage}. " +
            "Please report issues at https://github.com/mivertowski/DotCompute/issues",
            diagnosticId, error.Message);
    }

    /// <summary>
    /// Gets usage statistics for all experimental features.
    /// </summary>
    /// <returns>Dictionary of diagnostic IDs to usage statistics.</returns>
    public static IReadOnlyDictionary<string, FeatureUsageStats> GetUsageStatistics()
    {
        return _usageStats;
    }

    /// <summary>
    /// Gets a summary of experimental feature usage.
    /// </summary>
    /// <returns>Formatted summary string.</returns>
    public static string GetUsageSummary()
    {
        if (_usageStats.IsEmpty)
        {
            return "No experimental features have been used.";
        }

        var builder = new global::System.Text.StringBuilder();
        builder.AppendLine("Experimental Feature Usage Summary:");
        builder.AppendLine("===================================");

        foreach (var (id, stats) in _usageStats.OrderByDescending(kvp => kvp.Value.UsageCount))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {id} ({stats.FeatureName}):");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    Usage Count: {stats.UsageCount}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    First Used: {stats.FirstUsed:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AppendLine(CultureInfo.InvariantCulture, $"    Last Used: {stats.LastUsed:yyyy-MM-dd HH:mm:ss} UTC");

            if (stats.ErrorCount > 0)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"    Errors: {stats.ErrorCount}");
                builder.AppendLine(CultureInfo.InvariantCulture, $"    Last Error: {stats.LastError}");
            }

            if (!stats.Contexts.IsEmpty)
            {
                builder.AppendLine("    Contexts:");
                foreach (var (ctx, count) in stats.Contexts.Take(5))
                {
                    builder.AppendLine(CultureInfo.InvariantCulture, $"      - {ctx}: {count}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Resets all usage statistics.
    /// </summary>
    public static void Reset()
    {
        _usageStats.Clear();
    }
}

/// <summary>
/// Statistics for a single experimental feature.
/// </summary>
public sealed class FeatureUsageStats
{
    /// <summary>
    /// Gets or sets the diagnostic ID.
    /// </summary>
    public required string DiagnosticId { get; init; }

    /// <summary>
    /// Gets or sets the feature name.
    /// </summary>
    public required string FeatureName { get; init; }

    /// <summary>
    /// Gets or sets the usage count.
    /// </summary>
    public long UsageCount { get; set; }

    /// <summary>
    /// Gets or sets the first usage time.
    /// </summary>
    public required DateTime FirstUsed { get; init; }

    /// <summary>
    /// Gets or sets the last usage time.
    /// </summary>
    public DateTime LastUsed { get; set; }

    /// <summary>
    /// Gets or sets the error count.
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the last error time.
    /// </summary>
    public DateTime? LastErrorTime { get; set; }

    /// <summary>
    /// Gets the usage contexts.
    /// </summary>
    public ConcurrentDictionary<string, long> Contexts { get; } = new();
}
