// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Telemetry.System;

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Current performance state of a backend.
/// </summary>
public class BackendPerformanceState
{
    /// <summary>
    /// Gets or sets the backend identifier.
    /// </summary>
    /// <value>The backend id.</value>
    public string BackendId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the current utilization.
    /// </summary>
    /// <value>The current utilization.</value>
    public double CurrentUtilization { get; set; }
    /// <summary>
    /// Gets or sets the recent average execution time ms.
    /// </summary>
    /// <value>The recent average execution time ms.</value>
    public double RecentAverageExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the recent execution count.
    /// </summary>
    /// <value>The recent execution count.</value>
    public int RecentExecutionCount { get; set; }
    /// <summary>
    /// Gets or sets the last execution time.
    /// </summary>
    /// <value>The last execution time.</value>
    public DateTimeOffset LastExecutionTime { get; set; }
    /// <summary>
    /// Gets or sets the recent results.
    /// </summary>
    /// <value>The recent results.</value>
    public Queue<PerformanceResult> RecentResults { get; set; } = new();
    private readonly object _lock = new();
    /// <summary>
    /// Performs record execution.
    /// </summary>
    /// <param name="result">The result.</param>

    public void RecordExecution(PerformanceResult result)
    {
        lock (_lock)
        {
            RecentResults.Enqueue(result);

            // Keep only last 100 results
            while (RecentResults.Count > 100)
            {
                _ = RecentResults.Dequeue();
            }

            UpdateAverages();
            LastExecutionTime = result.Timestamp;
        }
    }
    /// <summary>
    /// Updates the state.
    /// </summary>
    /// <param name="systemSnapshot">The system snapshot.</param>

    public void UpdateState(SystemPerformanceSnapshot systemSnapshot)
    {
        lock (_lock)
        {
            // Update utilization based on system state
            CurrentUtilization = CalculateCurrentUtilization(systemSnapshot);
        }
    }
    /// <summary>
    /// Gets the summary.
    /// </summary>
    /// <returns>The summary.</returns>

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