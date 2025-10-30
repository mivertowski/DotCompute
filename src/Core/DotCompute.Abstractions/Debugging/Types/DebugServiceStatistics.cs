// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Statistics for the debug service operations.
/// Tracks usage metrics and service health indicators.
/// </summary>
public sealed class DebugServiceStatistics
{
    /// <summary>
    /// Gets or sets the total number of validation operations performed.
    /// </summary>
    public long TotalValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of successful validations.
    /// </summary>
    public long SuccessfulValidations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed validations.
    /// </summary>
    public long FailedValidations { get; set; }

    /// <summary>
    /// Gets or sets the total number of performance analyses performed.
    /// </summary>
    public long TotalPerformanceAnalyses { get; set; }

    /// <summary>
    /// Gets or sets the total number of memory analyses performed.
    /// </summary>
    public long TotalMemoryAnalyses { get; set; }

    /// <summary>
    /// Gets or sets the total number of determinism tests performed.
    /// </summary>
    public long TotalDeterminismTests { get; set; }

    /// <summary>
    /// Gets or sets the total number of execution traces.
    /// </summary>
    public long TotalExecutionTraces { get; set; }

    /// <summary>
    /// Gets or sets the total number of reports generated.
    /// </summary>
    public long TotalReportsGenerated { get; set; }

    /// <summary>
    /// Gets or sets the average validation time.
    /// </summary>
    public TimeSpan AverageValidationTime { get; set; }

    /// <summary>
    /// Gets or sets the average analysis time.
    /// </summary>
    public TimeSpan AverageAnalysisTime { get; set; }

    /// <summary>
    /// Gets or sets the total execution time for all operations.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets when the service was started.
    /// </summary>
    public DateTime ServiceStartTime { get; set; }

    /// <summary>
    /// Gets or sets the service uptime.
    /// </summary>
    public TimeSpan Uptime => DateTime.UtcNow - ServiceStartTime;

    /// <summary>
    /// Gets or sets the number of registered accelerators.
    /// </summary>
    public int RegisteredAccelerators { get; set; }

    /// <summary>
    /// Gets or sets the number of active profiling sessions.
    /// </summary>
    public int ActiveProfilingSessions { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate for debug operations (0-1).
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets operations per second throughput.
    /// </summary>
    public double OperationsPerSecond { get; set; }

    /// <summary>
    /// Gets or sets per-backend statistics.
    /// </summary>
    public Dictionary<string, BackendDebugStats> BackendStatistics { get; init; } = [];
}

/// <summary>
/// Statistics for a specific backend in the debug service.
/// </summary>
public sealed class BackendDebugStats
{
    /// <summary>
    /// Gets or sets the backend type.
    /// </summary>
    public string BackendType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of executions on this backend.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the success rate (0-1).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets whether the backend is currently available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the last execution timestamp.
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }
}
