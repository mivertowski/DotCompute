// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Configuration options for the debug service.
/// </summary>
public class DebugServiceOptions
{
    public LogLevel VerbosityLevel { get; set; } = LogLevel.Information;
    public bool EnableProfiling { get; set; } = true;
    public bool EnableMemoryAnalysis { get; set; } = true;
    public bool SaveExecutionLogs { get; set; }

    public string LogOutputPath { get; set; } = "./debug-logs";
    public int MaxConcurrentExecutions { get; set; } = Environment.ProcessorCount;
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enables detailed execution tracing.
    /// </summary>
    public bool EnableDetailedTracing { get; set; }

    /// <summary>
    /// Enables memory profiling during execution.
    /// </summary>
    public bool EnableMemoryProfiling { get; set; } = true;

    /// <summary>
    /// Enables performance analysis and reporting.
    /// </summary>
    public bool EnablePerformanceAnalysis { get; set; } = true;

    /// <summary>
    /// Maximum number of historical entries to keep.
    /// </summary>
    public int MaxHistoricalEntries { get; set; } = 1000;

    /// <summary>
    /// Maximum number of metric points to collect.
    /// </summary>
    public int MaxMetricPoints { get; set; } = 10000;

    /// <summary>
    /// Threshold for considering an array as large (in elements).
    /// </summary>
    public int LargeArrayThreshold { get; set; } = 1000000;

    /// <summary>
    /// Memory efficiency threshold (percentage 0-100).
    /// </summary>
    public double MemoryEfficiencyThreshold { get; set; } = 85.0;

    /// <summary>
    /// Threshold for long-running kernels (in milliseconds).
    /// </summary>
    public int LongRunningThreshold { get; set; } = 5000;

    /// <summary>
    /// Memory pressure threshold (percentage 0-100).
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 90.0;

    /// <summary>
    /// Development profile configuration.
    /// </summary>
    public static DebugServiceOptions Development => new()
    {
        VerbosityLevel = LogLevel.Debug,
        EnableProfiling = true,
        EnableMemoryAnalysis = true,
        EnableDetailedTracing = true,
        SaveExecutionLogs = true,
        MaxHistoricalEntries = 5000,
        MaxMetricPoints = 50000
    };
}
