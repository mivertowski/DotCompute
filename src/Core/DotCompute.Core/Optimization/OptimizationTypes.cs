// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DotCompute.Abstractions;

namespace DotCompute.Core.Optimization;

/// <summary>
/// Characteristics of a computational workload used for backend selection.
/// </summary>
public class WorkloadCharacteristics
{
    /// <summary>Size of data being processed in bytes</summary>
    public long DataSize { get; set; }


    /// <summary>Compute intensity from 0.0 (low) to 1.0 (high)</summary>
    public double ComputeIntensity { get; set; }


    /// <summary>Memory intensity from 0.0 (low) to 1.0 (high)</summary>
    public double MemoryIntensity { get; set; }


    /// <summary>Parallelism level from 0.0 (sequential) to 1.0 (highly parallel)</summary>
    public double ParallelismLevel { get; set; }


    /// <summary>Expected number of operations</summary>
    public long OperationCount { get; set; }


    /// <summary>Memory access pattern classification</summary>
    public MemoryAccessPattern AccessPattern { get; set; }


    /// <summary>Additional custom characteristics</summary>
    public Dictionary<string, object> CustomCharacteristics { get; set; } = new();
}

/// <summary>
/// Unique signature for a workload pattern used for performance history tracking.
/// </summary>
public class WorkloadSignature : IEquatable<WorkloadSignature>
{
    public string KernelName { get; set; } = string.Empty;
    public long DataSize { get; set; }
    public double ComputeIntensity { get; set; }
    public double MemoryIntensity { get; set; }
    public double ParallelismLevel { get; set; }
    public WorkloadPattern WorkloadPattern { get; set; }

    public bool Equals(WorkloadSignature? other)
    {
        if (other is null)
        {
            return false;
        }


        if (ReferenceEquals(this, other))
        {
            return true;
        }


        return KernelName == other.KernelName &&
               DataSizeBucket(DataSize) == DataSizeBucket(other.DataSize) &&
               Math.Abs(ComputeIntensity - other.ComputeIntensity) < 0.1 &&
               Math.Abs(MemoryIntensity - other.MemoryIntensity) < 0.1 &&
               Math.Abs(ParallelismLevel - other.ParallelismLevel) < 0.1 &&
               WorkloadPattern == other.WorkloadPattern;
    }

    public override bool Equals(object? obj) => Equals(obj as WorkloadSignature);

    public override int GetHashCode() => HashCode.Combine(
        KernelName,
        DataSizeBucket(DataSize),
        ((int)(ComputeIntensity * 10)),
        ((int)(MemoryIntensity * 10)),
        ((int)(ParallelismLevel * 10)),
        WorkloadPattern);

    private static int DataSizeBucket(long size) => size switch
    {
        < 1024 => 0,           // < 1KB
        < 1024 * 1024 => 1,    // < 1MB
        < 10 * 1024 * 1024 => 2, // < 10MB
        < 100 * 1024 * 1024 => 3, // < 100MB
        _ => 4                 // >= 100MB
    };
}

/// <summary>
/// Classification of workload patterns for backend optimization.
/// </summary>
public enum WorkloadPattern
{
    Sequential,
    ComputeIntensive,
    MemoryIntensive,
    HighlyParallel,
    Balanced
}

/// <summary>
/// Memory access pattern classifications.
/// </summary>
public enum MemoryAccessPattern
{
    Sequential,
    Random,
    Strided,
    Coalesced,
    Scattered
}

/// <summary>
/// Result of backend selection process.
/// </summary>
public class BackendSelection
{
    /// <summary>Selected backend accelerator</summary>
    public IAccelerator? SelectedBackend { get; set; }


    /// <summary>Backend identifier</summary>
    public string BackendId { get; set; } = string.Empty;


    /// <summary>Confidence in the selection from 0.0 to 1.0</summary>
    public float ConfidenceScore { get; set; }


    /// <summary>Human-readable reason for the selection</summary>
    public string Reason { get; set; } = string.Empty;


    /// <summary>Strategy used for selection</summary>
    public SelectionStrategy SelectionStrategy { get; set; }


    /// <summary>Additional metadata about the selection</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Strategies used for backend selection.
/// </summary>
public enum SelectionStrategy
{
    Historical,      // Based on historical performance data
    RealTime,        // Based on current system performance
    Characteristics, // Based on workload characteristics
    Priority,        // Based on backend priority order
    Fallback,        // Fallback when other strategies fail
    OnlyOption       // Only one backend available
}

/// <summary>
/// Constraints for backend selection.
/// </summary>
public class SelectionConstraints
{
    /// <summary>Backends that are explicitly allowed</summary>
    public HashSet<string>? AllowedBackends { get; set; }


    /// <summary>Backends that are explicitly disallowed</summary>
    public HashSet<string>? DisallowedBackends { get; set; }


    /// <summary>Maximum acceptable execution time in milliseconds</summary>
    public double? MaxExecutionTimeMs { get; set; }


    /// <summary>Maximum acceptable memory usage in MB</summary>
    public long? MaxMemoryUsageMB { get; set; }


    /// <summary>Minimum required confidence score</summary>
    public float? MinConfidenceScore { get; set; }


    /// <summary>Custom constraint predicates</summary>
    public List<Func<string, bool>> CustomConstraints { get; set; } = new();

    /// <summary>
    /// Checks if a backend is allowed based on all constraints.
    /// </summary>
    public bool IsBackendAllowed(string backendId)
    {
        if (DisallowedBackends?.Contains(backendId) == true)
        {
            return false;
        }


        if (AllowedBackends != null && !AllowedBackends.Contains(backendId))
        {

            return false;
        }


        return CustomConstraints.All(constraint => constraint(backendId));
    }
}

/// <summary>
/// Performance result for recording actual execution metrics.
/// </summary>
public class PerformanceResult
{
    public double ExecutionTimeMs { get; set; }
    public double ThroughputOpsPerSecond { get; set; }
    public long MemoryUsedBytes { get; set; }
    public double CpuUtilization { get; set; }
    public double GpuUtilization { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Historical performance statistics for a backend on a specific workload.
/// </summary>
public class BackendPerformanceStats
{
    public int SampleCount { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public double ExecutionTimeStdDev { get; set; }
    public double MinExecutionTimeMs { get; set; }
    public double MaxExecutionTimeMs { get; set; }
    public double AverageThroughput { get; set; }
    public double AverageMemoryUsage { get; set; }
    public float ReliabilityScore { get; set; } // 0-1 based on success rate
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// Current performance state of a backend.
/// </summary>
public class BackendPerformanceState
{
    public string BackendId { get; set; } = string.Empty;
    public double CurrentUtilization { get; set; }
    public double RecentAverageExecutionTimeMs { get; set; }
    public int RecentExecutionCount { get; set; }
    public DateTimeOffset LastExecutionTime { get; set; }
    public Queue<PerformanceResult> RecentResults { get; set; } = new();
    private readonly object _lock = new();

    public void RecordExecution(PerformanceResult result)
    {
        lock (_lock)
        {
            RecentResults.Enqueue(result);

            // Keep only last 100 results

            while (RecentResults.Count > 100)
            {
                RecentResults.Dequeue();
            }

            UpdateAverages();
            LastExecutionTime = result.Timestamp;
        }
    }

    public void UpdateState(SystemPerformanceSnapshot systemSnapshot)
    {
        lock (_lock)
        {
            // Update utilization based on system state
            CurrentUtilization = CalculateCurrentUtilization(systemSnapshot);
        }
    }

    public BackendPerformanceStateSummary GetSummary()
    {
        lock (_lock)
        {
            return new BackendPerformanceStateSummary
            {
                BackendId = BackendId,
                CurrentUtilization = CurrentUtilization,
                RecentAverageExecutionTimeMs = RecentAverageExecutionTimeMs,
                RecentExecutionCount = RecentExecutionCount,
                LastExecutionTime = LastExecutionTime
            };
        }
    }

    private void UpdateAverages()
    {
        if (RecentResults.Count == 0)
        {
            return;
        }


        RecentAverageExecutionTimeMs = RecentResults.Average(r => r.ExecutionTimeMs);
        RecentExecutionCount = RecentResults.Count;
    }

    private double CalculateCurrentUtilization(SystemPerformanceSnapshot snapshot)
    {
        // Simplified utilization calculation based on system metrics
        return BackendId.ToUpperInvariant() switch
        {
            "CPU" => snapshot.CpuUsage,
            "CUDA" => snapshot.GpuUsage,
            "METAL" => snapshot.GpuUsage,
            _ => 0.5 // Default moderate utilization
        };
    }
}

/// <summary>
/// Summary of backend performance state for external consumption.
/// </summary>
public class BackendPerformanceStateSummary
{
    public string BackendId { get; set; } = string.Empty;
    public double CurrentUtilization { get; set; }
    public double RecentAverageExecutionTimeMs { get; set; }
    public int RecentExecutionCount { get; set; }
    public DateTimeOffset LastExecutionTime { get; set; }
}

/// <summary>
/// Historical performance data for a specific workload signature.
/// </summary>
public class PerformanceHistory
{
    private readonly ConcurrentDictionary<string, List<PerformanceResult>> _backendResults;
    private readonly WorkloadSignature _signature;
    private readonly int _maxEntries;

    public WorkloadSignature Signature => _signature;
    public int TotalEntries => _backendResults.Values.Sum(results => results.Count);

    public PerformanceHistory(WorkloadSignature signature, int maxEntries = 1000)
    {
        _signature = signature;
        _maxEntries = maxEntries;
        _backendResults = new ConcurrentDictionary<string, List<PerformanceResult>>();
    }

    public void AddPerformanceResult(string backendId, PerformanceResult result)
    {
        _backendResults.AddOrUpdate(backendId,
            _ => new List<PerformanceResult> { result },
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(result);

                    // Trim old entries if needed

                    if (existing.Count > _maxEntries)
                    {
                        existing.RemoveRange(0, existing.Count - _maxEntries);
                    }


                    return existing;
                }
            });
    }

    public Dictionary<string, BackendPerformanceStats> GetPerformanceStats()
    {
        var stats = new Dictionary<string, BackendPerformanceStats>();


        foreach (var kvp in _backendResults)
        {
            var backendId = kvp.Key;
            var results = kvp.Value;


            lock (results)
            {
                if (results.Count == 0)
                {
                    continue;
                }


                var successfulResults = results.Where(r => r.Success).ToList();


                if (successfulResults.Count == 0)
                {
                    continue;
                }


                var executionTimes = successfulResults.Select(r => r.ExecutionTimeMs).ToList();
                var avgExecutionTime = executionTimes.Average();
                var stdDev = CalculateStandardDeviation(executionTimes, avgExecutionTime);


                stats[backendId] = new BackendPerformanceStats
                {
                    SampleCount = successfulResults.Count,
                    AverageExecutionTimeMs = avgExecutionTime,
                    ExecutionTimeStdDev = stdDev,
                    MinExecutionTimeMs = executionTimes.Min(),
                    MaxExecutionTimeMs = executionTimes.Max(),
                    AverageThroughput = successfulResults.Average(r => r.ThroughputOpsPerSecond),
                    AverageMemoryUsage = successfulResults.Average(r => r.MemoryUsedBytes),
                    ReliabilityScore = (float)successfulResults.Count / results.Count,
                    LastUpdated = successfulResults.Max(r => r.Timestamp)
                };
            }
        }


        return stats;
    }

    private static double CalculateStandardDeviation(List<double> values, double mean)
    {
        if (values.Count <= 1)
        {
            return 0;
        }


        var sumOfSquares = values.Sum(value => Math.Pow(value - mean, 2));
        return Math.Sqrt(sumOfSquares / values.Count);
    }
}

/// <summary>
/// Analysis results for a specific workload.
/// </summary>
public class WorkloadAnalysis
{
    public WorkloadSignature WorkloadSignature { get; set; } = new();
    public WorkloadPattern WorkloadPattern { get; set; }
    public double EstimatedExecutionTimeMs { get; set; }
    public long EstimatedMemoryUsageMB { get; set; }
    public Dictionary<string, BackendPerformanceStats> HistoricalPerformance { get; set; } = new();
    public int TotalHistoryEntries { get; set; }
    public bool HasSufficientHistory { get; set; }
}

/// <summary>
/// System performance snapshot for real-time decision making.
/// </summary>
public class SystemPerformanceSnapshot
{
    public DateTimeOffset Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double GpuUsage { get; set; }
    public long MemoryAvailable { get; set; }
    public int ActiveThreads { get; set; }
}

/// <summary>
/// Comprehensive performance insights for monitoring and debugging.
/// </summary>
public class PerformanceInsights
{
    public DateTimeOffset Timestamp { get; set; }
    public int TotalWorkloadSignatures { get; set; }
    public int TotalBackends { get; set; }
    public Dictionary<string, BackendPerformanceStateSummary> BackendStates { get; set; } = new();
    public List<(WorkloadSignature Workload, string Backend, double PerformanceScore)> TopPerformingPairs { get; set; } = new();
    public LearningStatistics LearningStatistics { get; set; } = new();
}

/// <summary>
/// Statistics about the learning effectiveness of the adaptive system.
/// </summary>
public class LearningStatistics
{
    public int TotalPerformanceSamples { get; set; }
    public int AverageSamplesPerWorkload { get; set; }
    public int WorkloadsWithSufficientHistory { get; set; }
    public float LearningEffectiveness { get; set; }
}

/// <summary>
/// Configuration options for adaptive backend selection.
/// </summary>
public class AdaptiveSelectionOptions
{
    /// <summary>Whether to enable machine learning from performance history</summary>
    public bool EnableLearning { get; set; } = true;


    /// <summary>Minimum confidence threshold for making selections</summary>
    public float MinConfidenceThreshold { get; set; } = 0.6f;


    /// <summary>Maximum number of historical entries per workload</summary>
    public int MaxHistoryEntries { get; set; } = 1000;


    /// <summary>Minimum history entries required for learning-based decisions</summary>
    public int MinHistoryForLearning { get; set; } = 5;


    /// <summary>Minimum samples required for high confidence</summary>
    public int MinSamplesForHighConfidence { get; set; } = 20;


    /// <summary>Interval in seconds for updating backend performance states</summary>
    public int PerformanceUpdateIntervalSeconds { get; set; } = 10;
}