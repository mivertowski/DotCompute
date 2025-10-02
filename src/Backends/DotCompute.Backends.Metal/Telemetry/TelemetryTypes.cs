// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Execution;
using DotCompute.Abstractions.Types;

namespace DotCompute.Backends.Metal.Telemetry;

#region Core Telemetry Types

/// <summary>
/// Comprehensive telemetry snapshot
/// </summary>
public sealed class MetalTelemetrySnapshot
{
    public DateTimeOffset Timestamp { get; set; }
    public long TotalOperations { get; set; }
    public long TotalErrors { get; set; }
    public double ErrorRate { get; set; }
    public Dictionary<string, MetalOperationMetrics> OperationMetrics { get; set; } = [];
    public Dictionary<string, MetalResourceMetrics> ResourceMetrics { get; set; } = [];
    public Dictionary<string, object> PerformanceCounters { get; set; } = [];
    public HealthStatus HealthStatus { get; set; }
    public MetalSystemInfo SystemInfo { get; set; } = new();
}

/// <summary>
/// Operation-specific metrics
/// </summary>
public sealed class MetalOperationMetrics
{
    public string OperationName { get; }
    public long TotalExecutions { get; private set; }
    public long SuccessfulExecutions { get; private set; }
    public TimeSpan TotalExecutionTime { get; private set; }
    public TimeSpan MinExecutionTime { get; private set; } = TimeSpan.MaxValue;
    public TimeSpan MaxExecutionTime { get; private set; }
    public DateTimeOffset LastUpdated { get; private set; }

    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0.0;
    public TimeSpan AverageExecutionTime => TotalExecutions > 0

        ? TimeSpan.FromMilliseconds(TotalExecutionTime.TotalMilliseconds / TotalExecutions)

        : TimeSpan.Zero;

    public MetalOperationMetrics(string operationName, TimeSpan duration, bool success)
    {
        OperationName = operationName;
        UpdateMetrics(duration, success);
    }

    public void UpdateMetrics(TimeSpan duration, bool success)
    {
        TotalExecutions++;
        if (success)
        {
            SuccessfulExecutions++;
        }


        TotalExecutionTime = TotalExecutionTime.Add(duration);

        if (duration < MinExecutionTime)
        {
            MinExecutionTime = duration;
        }


        if (duration > MaxExecutionTime)
        {
            MaxExecutionTime = duration;
        }


        LastUpdated = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Resource-specific metrics
/// </summary>
public sealed class MetalResourceMetrics(string resourceName, long currentUsage, long limit)
{
    public string ResourceName { get; } = resourceName;
    public long CurrentUsage { get; private set; } = currentUsage;
    public long PeakUsage { get; private set; } = currentUsage;
    public long Limit { get; private set; } = limit;
    public double UtilizationPercentage => Limit > 0 ? (double)CurrentUsage / Limit * 100.0 : 0.0;
    public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;

    public void UpdateUsage(long currentUsage, long peakUsage, long limit)
    {
        CurrentUsage = currentUsage;
        if (peakUsage > PeakUsage)
        {
            PeakUsage = peakUsage;
        }


        Limit = limit;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    public void UpdateUtilization(double gpuUtilization, double memoryUtilization, long usedMemory)
    {
        CurrentUsage = usedMemory;
        if (usedMemory > PeakUsage)
        {
            PeakUsage = usedMemory;
        }


        LastUpdated = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// System information
/// </summary>
public sealed class MetalSystemInfo
{
    public int DeviceCount { get; set; }
    public long TotalSystemMemory { get; set; }
    public long AvailableSystemMemory { get; set; }
    public int ProcessorCount { get; set; }
    public string OSVersion { get; set; } = string.Empty;
    public string RuntimeVersion { get; set; } = string.Empty;
}

/// <summary>
/// Production telemetry report
/// </summary>
public sealed class MetalProductionReport
{
    public MetalTelemetrySnapshot Snapshot { get; set; } = new();
    public MetalPerformanceAnalysis PerformanceAnalysis { get; set; } = new();
    public MetalHealthAnalysis HealthAnalysis { get; set; } = new();
    public List<Alert> AlertsSummary { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public Dictionary<string, object> ExportedMetrics { get; set; } = [];
}

#endregion

#region Performance Analysis Types

/// <summary>
/// Performance analysis results
/// </summary>
public sealed class MetalPerformanceAnalysis
{
    public DateTimeOffset Timestamp { get; set; }
    public string AnalysisVersion { get; set; } = string.Empty;
    public ThroughputAnalysis ThroughputAnalysis { get; set; } = new();
    public ErrorRateAnalysis ErrorRateAnalysis { get; set; } = new();
    public ResourceUtilizationAnalysis ResourceUtilizationAnalysis { get; set; } = new();
    public List<PerformanceTrend> PerformanceTrends { get; set; } = [];
    public double OverallPerformanceScore { get; set; }
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Throughput analysis
/// </summary>
public sealed class ThroughputAnalysis
{
    public double KernelThroughput { get; set; }
    public double MemoryThroughput { get; set; }
}

/// <summary>
/// Error rate analysis
/// </summary>
public sealed class ErrorRateAnalysis
{
    public double OverallErrorRate { get; set; }
    public double MemoryErrorRate { get; set; }
}

/// <summary>
/// Resource utilization analysis
/// </summary>
public sealed class ResourceUtilizationAnalysis
{
    public double GpuUtilization { get; set; }
    public double MemoryUtilization { get; set; }
}

// PerformanceTrends class removed - using unified PerformanceTrend from DotCompute.Abstractions.Types

/// <summary>
/// Performance counter statistics
/// </summary>
public sealed class CounterStatistics
{
    public string CounterName { get; }
    public double CurrentValue { get; private set; }
    public double TotalValue { get; private set; }
    public double MinValue { get; private set; } = double.MaxValue;
    public double MaxValue { get; private set; } = double.MinValue;
    public int SampleCount { get; private set; }
    public DateTimeOffset LastUpdated { get; private set; }

    public double Average => SampleCount > 0 ? TotalValue / SampleCount : 0.0;

    public CounterStatistics(string counterName, double initialValue = 0.0)
    {
        CounterName = counterName;
        UpdateValue(initialValue);
    }

    public void UpdateValue(double value)
    {
        CurrentValue = value;
        TotalValue += value;
        SampleCount++;

        if (value < MinValue)
        {
            MinValue = value;
        }


        if (value > MaxValue)
        {
            MaxValue = value;
        }


        LastUpdated = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Performance counter wrapper
/// </summary>
public sealed class PerformanceCounter(string counterName) : IDisposable
{
    public string CounterName { get; } = counterName;
    private volatile bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

#endregion

#region Health Monitoring Types

/// <summary>
/// Health status enumeration
/// </summary>
public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Critical
}

/// <summary>
/// Health event types
/// </summary>
public enum HealthEventType
{
    Success,
    Error,
    MemoryPressure,
    ResourcePressure,
    Anomaly
}

/// <summary>
/// Health severity levels
/// </summary>
public enum HealthSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Memory pressure levels
/// </summary>
public enum MemoryPressureLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Resource types
/// </summary>
public enum ResourceType
{
    Memory,
    GPU,
    Storage,
    Network
}

/// <summary>
/// Health event
/// </summary>
public sealed class HealthEvent
{
    public DateTimeOffset Timestamp { get; set; }
    public HealthEventType EventType { get; set; }
    public string Component { get; set; } = string.Empty;
    public HealthSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object>? Properties { get; set; }
    public Dictionary<string, object> Data { get; set; } = [];
}

/// <summary>
/// Component health information
/// </summary>
public sealed class ComponentHealth(string componentName)
{
    public string ComponentName { get; } = componentName;
    public HealthStatus Status { get; set; } = HealthStatus.Healthy;
    public DateTimeOffset LastCheckTime { get; set; } = DateTimeOffset.UtcNow;
    public int ErrorCount { get; private set; }
    public int SuccessCount { get; private set; }
    public string? LastError { get; set; }
    public Dictionary<string, object> Properties { get; set; } = [];

    public double SuccessRate => (ErrorCount + SuccessCount) > 0

        ? (double)SuccessCount / (ErrorCount + SuccessCount)

        : 1.0;

    public void RecordError(MetalError error, string context)
    {
        ErrorCount++;
        LastError = $"{error}: {context}";
        Status = error switch
        {
            MetalError.DeviceLost => HealthStatus.Critical,
            MetalError.OutOfMemory or MetalError.DeviceUnavailable => HealthStatus.Degraded,
            _ => HealthStatus.Degraded
        };
        LastCheckTime = DateTimeOffset.UtcNow;
    }

    public void RecordSuccess(string operation, TimeSpan duration)
    {
        SuccessCount++;
        Status = HealthStatus.Healthy;
        LastCheckTime = DateTimeOffset.UtcNow;
        Properties["last_success_operation"] = operation;
        Properties["last_success_duration_ms"] = duration.TotalMilliseconds;
    }

    public void RecordMemoryPressure(MemoryPressureLevel level, double percentage)
    {
        Status = level switch
        {
            MemoryPressureLevel.Critical => HealthStatus.Critical,
            MemoryPressureLevel.High => HealthStatus.Degraded,
            _ => HealthStatus.Healthy
        };
        Properties["memory_pressure_level"] = level;
        Properties["memory_pressure_percentage"] = percentage;
        LastCheckTime = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Health report
/// </summary>
public sealed class MetalHealthReport
{
    public DateTimeOffset Timestamp { get; set; }
    public HealthStatus OverallHealth { get; set; }
    public Dictionary<string, ComponentHealth> ComponentHealthMap { get; set; } = [];
    public List<HealthEvent> RecentEvents { get; set; } = [];
    public Dictionary<string, CircuitBreakerState> CircuitBreakerStates { get; set; } = [];
    public Dictionary<string, object> SystemMetrics { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Health analysis
/// </summary>
public sealed class MetalHealthAnalysis
{
    public DateTimeOffset Timestamp { get; set; }
    public TimeSpan AnalysisPeriod { get; set; }
    public int TotalEvents { get; set; }
    public Dictionary<string, object> ErrorPatterns { get; set; } = [];
    public Dictionary<string, object> PerformanceDegradation { get; set; } = [];
    public Dictionary<string, object> ResourcePressureTrends { get; set; } = [];
    public double HealthScore { get; set; }
    public List<string> PredictedIssues { get; set; } = [];
}

/// <summary>
/// Alert history
/// </summary>
public sealed class AlertHistory(string alertKey)
{
    public string AlertKey { get; } = alertKey;
    private readonly List<HealthEvent> _events = [];
    private readonly object _lock = new();

    public void RecordEvent(DateTimeOffset timestamp, Dictionary<string, object> properties)
    {
        lock (_lock)
        {
            _events.Add(new HealthEvent
            {
                Timestamp = timestamp,
                Properties = properties
            });

            // Keep only recent events
            if (_events.Count > 1000)
            {
                _events.RemoveAt(0);
            }
        }
    }

    public List<HealthEvent> GetEventsInWindow(TimeSpan window)
    {
        var cutoffTime = DateTimeOffset.UtcNow.Subtract(window);


        lock (_lock)
        {
            return [.. _events.Where(e => e.Timestamp >= cutoffTime)];
        }
    }
}

/// <summary>
/// Circuit breaker
/// </summary>
public sealed class CircuitBreaker(string name, int threshold, TimeSpan timeout)
{
    public string Name { get; } = name;
    private int _failureCount;
    private DateTimeOffset _lastFailureTime;
    private readonly int _threshold = threshold;
    private readonly TimeSpan _timeout = timeout;

    public CircuitBreakerState State { get; private set; } = CircuitBreakerState.Closed;

    public void RecordSuccess()
    {
        _failureCount = 0;
        State = CircuitBreakerState.Closed;
    }

    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTimeOffset.UtcNow;

        if (_failureCount >= _threshold)
        {
            State = CircuitBreakerState.Open;
        }
    }

    public CircuitBreakerState GetState()
    {
        if (State == CircuitBreakerState.Open && DateTimeOffset.UtcNow - _lastFailureTime > _timeout)
        {
            State = CircuitBreakerState.HalfOpen;
        }

        return State;
    }
}

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Time window for analysis
/// </summary>
public sealed class TimeWindow
{
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public TimeSpan Duration { get; set; }
}

#endregion

#region Logging Types

/// <summary>
/// Logging context
/// </summary>
public sealed class LogContext(string correlationId, string operationType, DateTimeOffset startTime)
{
    public string CorrelationId { get; } = correlationId;
    public string OperationType { get; } = operationType;
    public DateTimeOffset StartTime { get; } = startTime;
}

/// <summary>
/// Structured log entry
/// </summary>
public sealed class StructuredLogEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Export and Alert Types

/// <summary>
/// Exporter types
/// </summary>
public enum ExporterType
{
    Prometheus,
    OpenTelemetry,
    ApplicationInsights,
    DataDog,
    Grafana,
    Custom
}

/// <summary>
/// Exporter configuration
/// </summary>
public sealed class ExporterConfiguration
{
    public string Name { get; set; } = string.Empty;
    public ExporterType Type { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public Dictionary<string, string>? Headers { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Alert severities
/// </summary>
public enum AlertSeverity
{
    Info,
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Alert
/// </summary>
public sealed class Alert
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset LastOccurrence { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public int OccurrenceCount { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
    public string[]? RecommendedActions { get; set; }
    public string? Resolution { get; set; }
}

/// <summary>
/// Alert rule
/// </summary>
public sealed class AlertRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Threshold configuration
/// </summary>
public sealed class ThresholdConfiguration
{
    public int MaxFailuresPerWindow { get; set; }
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromMinutes(5);
}


#endregion