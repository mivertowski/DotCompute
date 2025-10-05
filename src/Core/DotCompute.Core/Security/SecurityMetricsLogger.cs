// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Security;

namespace DotCompute.Core.Security;

/// <summary>
/// Handles security metrics tracking and statistical analysis.
/// Provides insights into security events and patterns.
/// </summary>
public sealed class SecurityMetricsLogger(ILogger<SecurityMetricsLogger> logger,
    SecurityMetrics metrics,
    ConcurrentDictionary<string, CorrelationContext> correlationContexts)
{
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SecurityMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    private readonly ConcurrentDictionary<string, CorrelationContext> _correlationContexts = correlationContexts ?? throw new ArgumentNullException(nameof(correlationContexts));

    /// <summary>
    /// Gets current security metrics.
    /// </summary>
    public SecurityMetrics GetSecurityMetrics()
    {
        // Update correlation metrics
        _metrics.ActiveCorrelations = _correlationContexts.Count;

        // Calculate average events per correlation

        if (!_correlationContexts.IsEmpty)
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
        _metrics.TotalEventCount++;

        // Update event type counters
        UpdateEventTypeMetrics(entry.EventType);

        // Update security level counters
        UpdateSecurityLevelMetrics(entry.Level);

        // Update user-specific metrics
        if (!string.IsNullOrEmpty(entry.UserId))
        {
            _metrics.UniqueUsersCount = _metrics.UserEventCounts.Count;
            _ = _metrics.UserEventCounts.AddOrUpdate(entry.UserId, 1, (key, count) => count + 1);
        }

        // Update resource-specific metrics
        if (!string.IsNullOrEmpty(entry.ResourceId))
        {
            _ = _metrics.ResourceEventCounts.AddOrUpdate(entry.ResourceId, 1, (key, count) => count + 1);
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
                _metrics.AuthenticationSuccessCount++;
                break;
            case SecurityEventType.AuthenticationFailure:
                _metrics.AuthenticationFailureCount++;
                break;
            case SecurityEventType.AccessGranted:
                _metrics.AccessGrantedCount++;
                break;
            case SecurityEventType.AccessDenied:
                _metrics.AccessDeniedCount++;
                break;
            case SecurityEventType.SecurityViolation:
                _metrics.SecurityViolationCount++;
                break;
            case SecurityEventType.DataAccess:
                _metrics.DataAccessCount++;
                break;
            case SecurityEventType.DataModification:
                _metrics.DataModificationCount++;
                break;
            case SecurityEventType.DataDeletion:
                _metrics.DataDeletionCount++;
                break;
        }
    }

    private void UpdateSecurityLevelMetrics(SecurityLevel level)
    {
        switch (level)
        {
            case SecurityLevel.Critical:
                _metrics.CriticalEventCount++;
                break;
            case SecurityLevel.High:
                _metrics.HighEventCount++;
                break;
            case SecurityLevel.Medium:
                _metrics.MediumEventCount++;
                break;
            case SecurityLevel.Low:
                _metrics.LowEventCount++;
                break;
            case SecurityLevel.Informational:
                _metrics.InformationalEventCount++;
                break;
        }
    }
}

/// <summary>
/// Security metrics summary for reporting.
/// </summary>
public sealed class SecurityMetricsSummary
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    /// <value>The period.</value>
    public TimeSpan Period { get; set; }
    /// <summary>
    /// Gets or sets the generated at.
    /// </summary>
    /// <value>The generated at.</value>
    public DateTimeOffset GeneratedAt { get; set; }
    /// <summary>
    /// Gets or sets the total events.
    /// </summary>
    /// <value>The total events.</value>
    public long TotalEvents { get; set; }
    /// <summary>
    /// Gets or sets the unique users.
    /// </summary>
    /// <value>The unique users.</value>
    public int UniqueUsers { get; set; }
    /// <summary>
    /// Gets or sets the security violations.
    /// </summary>
    /// <value>The security violations.</value>
    public long SecurityViolations { get; set; }
    /// <summary>
    /// Gets or sets the critical events.
    /// </summary>
    /// <value>The critical events.</value>
    public long CriticalEvents { get; set; }
    /// <summary>
    /// Gets or sets the failed authentications.
    /// </summary>
    /// <value>The failed authentications.</value>
    public long FailedAuthentications { get; set; }
    /// <summary>
    /// Gets or sets the access denied events.
    /// </summary>
    /// <value>The access denied events.</value>
    public long AccessDeniedEvents { get; set; }
    /// <summary>
    /// Gets or sets the events per hour.
    /// </summary>
    /// <value>The events per hour.</value>
    public double EventsPerHour { get; set; }
    /// <summary>
    /// Gets or sets the violations per hour.
    /// </summary>
    /// <value>The violations per hour.</value>
    public double ViolationsPerHour { get; set; }
    /// <summary>
    /// Gets or sets the failed auth per hour.
    /// </summary>
    /// <value>The failed auth per hour.</value>
    public double FailedAuthPerHour { get; set; }
    /// <summary>
    /// Gets or sets the authentication success rate.
    /// </summary>
    /// <value>The authentication success rate.</value>
    public double AuthenticationSuccessRate { get; set; }
    /// <summary>
    /// Gets or sets the access success rate.
    /// </summary>
    /// <value>The access success rate.</value>
    public double AccessSuccessRate { get; set; }
}