// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Telemetry.System;

namespace DotCompute.Core.Optimization.Performance;

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