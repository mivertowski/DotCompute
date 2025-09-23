// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Security;
using DotCompute.Core.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles security metrics tracking and statistical analysis.
/// Provides insights into security events and patterns.
/// </summary>
public sealed class SecurityMetricsLogger
{
    private readonly ILogger _logger;
    private readonly SecurityMetrics _metrics;
    private readonly ConcurrentDictionary<string, CorrelationContext> _correlationContexts;

    public SecurityMetricsLogger(ILogger<SecurityMetricsLogger> logger,
        SecurityMetrics metrics,
        ConcurrentDictionary<string, CorrelationContext> correlationContexts)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _correlationContexts = correlationContexts ?? throw new ArgumentNullException(nameof(correlationContexts));
    }

    /// <summary>
    /// Gets current security metrics.
    /// </summary>
    public SecurityMetrics GetSecurityMetrics()
    {
        // Update correlation metrics
        _metrics.ActiveCorrelations = _correlationContexts.Count;
        
        // Calculate average events per correlation
        if (_correlationContexts.Count > 0)
        {
            _metrics.AverageEventsPerCorrelation = _correlationContexts.Values
                .Average(c => c.EventCount);
        }

        // Calculate metrics by event type
        _metrics.EventsByType = new Dictionary<SecurityEventType, long>
        {
            { SecurityEventType.AuthenticationSuccess, _metrics.AuthenticationSuccessCount },
            { SecurityEventType.AuthenticationFailure, _metrics.AuthenticationFailureCount },
            { SecurityEventType.AccessGranted, _metrics.AccessGrantedCount },
            { SecurityEventType.AccessDenied, _metrics.AccessDeniedCount },
            { SecurityEventType.SecurityViolation, _metrics.SecurityViolationCount },
            { SecurityEventType.DataAccess, _metrics.DataAccessCount },
            { SecurityEventType.DataModification, _metrics.DataModificationCount },
            { SecurityEventType.DataDeletion, _metrics.DataDeletionCount }
        };

        // Calculate metrics by security level
        _metrics.EventsByLevel = new Dictionary<SecurityLevel, long>
        {
            { SecurityLevel.Critical, _metrics.CriticalEventCount },
            { SecurityLevel.High, _metrics.HighEventCount },
            { SecurityLevel.Medium, _metrics.MediumEventCount },
            { SecurityLevel.Low, _metrics.LowEventCount },
            { SecurityLevel.Informational, _metrics.InformationalEventCount }
        };

        return _metrics;
    }

    /// <summary>
    /// Updates metrics based on a security log entry.
    /// </summary>
    public void UpdateMetrics(SecurityLogEntry entry)
    {
        // Update total event count
        Interlocked.Increment(ref _metrics.TotalEventCount);

        // Update event type counters
        UpdateEventTypeMetrics(entry.EventType);

        // Update security level counters
        UpdateSecurityLevelMetrics(entry.Level);

        // Update user-specific metrics
        if (!string.IsNullOrEmpty(entry.UserId))
        {
            _metrics.UniqueUsersCount = _metrics.UserEventCounts.Count;
            _metrics.UserEventCounts.AddOrUpdate(entry.UserId, 1, (key, count) => count + 1);
        }

        // Update resource-specific metrics
        if (!string.IsNullOrEmpty(entry.ResourceId))
        {
            _metrics.ResourceEventCounts.AddOrUpdate(entry.ResourceId, 1, (key, count) => count + 1);
        }

        // Update time-based metrics
        var now = DateTimeOffset.UtcNow;
        if (_metrics.FirstEventTime == default)
        {
            _metrics.FirstEventTime = now;
        }
        _metrics.LastEventTime = now;

        // Log high-frequency events
        if (_metrics.TotalEventCount % 1000 == 0)
        {
            _logger.LogDebugMessage($"Security metrics checkpoint: {_metrics.TotalEventCount} total events processed");
        }
    }

    /// <summary>
    /// Resets all metrics to initial state.
    /// </summary>
    public void ResetMetrics()
    {
        _metrics.TotalEventCount = 0;
        _metrics.AuthenticationSuccessCount = 0;
        _metrics.AuthenticationFailureCount = 0;
        _metrics.AccessGrantedCount = 0;
        _metrics.AccessDeniedCount = 0;
        _metrics.SecurityViolationCount = 0;
        _metrics.DataAccessCount = 0;
        _metrics.DataModificationCount = 0;
        _metrics.DataDeletionCount = 0;
        _metrics.CriticalEventCount = 0;
        _metrics.HighEventCount = 0;
        _metrics.MediumEventCount = 0;
        _metrics.LowEventCount = 0;
        _metrics.InformationalEventCount = 0;
        _metrics.UniqueUsersCount = 0;
        _metrics.ActiveCorrelations = 0;
        _metrics.AverageEventsPerCorrelation = 0;
        _metrics.FirstEventTime = default;
        _metrics.LastEventTime = default;
        
        _metrics.UserEventCounts.Clear();
        _metrics.ResourceEventCounts.Clear();
        _metrics.EventsByType.Clear();
        _metrics.EventsByLevel.Clear();

        _logger.LogInfoMessage("Security metrics have been reset");
    }

    /// <summary>
    /// Gets security metrics for a specific time period.
    /// </summary>
    public SecurityMetricsSummary GetMetricsSummary(TimeSpan period)
    {
        var summary = new SecurityMetricsSummary
        {
            Period = period,
            GeneratedAt = DateTimeOffset.UtcNow,
            TotalEvents = _metrics.TotalEventCount,
            UniqueUsers = _metrics.UniqueUsersCount,
            SecurityViolations = _metrics.SecurityViolationCount,
            CriticalEvents = _metrics.CriticalEventCount,
            FailedAuthentications = _metrics.AuthenticationFailureCount,
            AccessDeniedEvents = _metrics.AccessDeniedCount
        };

        // Calculate rates
        var totalHours = period.TotalHours;
        if (totalHours > 0)
        {
            summary.EventsPerHour = _metrics.TotalEventCount / totalHours;
            summary.ViolationsPerHour = _metrics.SecurityViolationCount / totalHours;
            summary.FailedAuthPerHour = _metrics.AuthenticationFailureCount / totalHours;
        }

        // Calculate authentication success rate
        var totalAuth = _metrics.AuthenticationSuccessCount + _metrics.AuthenticationFailureCount;
        if (totalAuth > 0)
        {
            summary.AuthenticationSuccessRate = (double)_metrics.AuthenticationSuccessCount / totalAuth;
        }

        // Calculate access success rate
        var totalAccess = _metrics.AccessGrantedCount + _metrics.AccessDeniedCount;
        if (totalAccess > 0)
        {
            summary.AccessSuccessRate = (double)_metrics.AccessGrantedCount / totalAccess;
        }

        return summary;
    }

    private void UpdateEventTypeMetrics(SecurityEventType eventType)
    {
        switch (eventType)
        {
            case SecurityEventType.AuthenticationSuccess:
                Interlocked.Increment(ref _metrics.AuthenticationSuccessCount);
                break;
            case SecurityEventType.AuthenticationFailure:
                Interlocked.Increment(ref _metrics.AuthenticationFailureCount);
                break;
            case SecurityEventType.AccessGranted:
                Interlocked.Increment(ref _metrics.AccessGrantedCount);
                break;
            case SecurityEventType.AccessDenied:
                Interlocked.Increment(ref _metrics.AccessDeniedCount);
                break;
            case SecurityEventType.SecurityViolation:
                Interlocked.Increment(ref _metrics.SecurityViolationCount);
                break;
            case SecurityEventType.DataAccess:
                Interlocked.Increment(ref _metrics.DataAccessCount);
                break;
            case SecurityEventType.DataModification:
                Interlocked.Increment(ref _metrics.DataModificationCount);
                break;
            case SecurityEventType.DataDeletion:
                Interlocked.Increment(ref _metrics.DataDeletionCount);
                break;
        }
    }

    private void UpdateSecurityLevelMetrics(SecurityLevel level)
    {
        switch (level)
        {
            case SecurityLevel.Critical:
                Interlocked.Increment(ref _metrics.CriticalEventCount);
                break;
            case SecurityLevel.High:
                Interlocked.Increment(ref _metrics.HighEventCount);
                break;
            case SecurityLevel.Medium:
                Interlocked.Increment(ref _metrics.MediumEventCount);
                break;
            case SecurityLevel.Low:
                Interlocked.Increment(ref _metrics.LowEventCount);
                break;
            case SecurityLevel.Informational:
                Interlocked.Increment(ref _metrics.InformationalEventCount);
                break;
        }
    }
}

/// <summary>
/// Security metrics summary for reporting.
/// </summary>
public sealed class SecurityMetricsSummary
{
    public TimeSpan Period { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public long TotalEvents { get; init; }
    public int UniqueUsers { get; init; }
    public long SecurityViolations { get; init; }
    public long CriticalEvents { get; init; }
    public long FailedAuthentications { get; init; }
    public long AccessDeniedEvents { get; init; }
    public double EventsPerHour { get; init; }
    public double ViolationsPerHour { get; init; }
    public double FailedAuthPerHour { get; init; }
    public double AuthenticationSuccessRate { get; init; }
    public double AccessSuccessRate { get; init; }
}