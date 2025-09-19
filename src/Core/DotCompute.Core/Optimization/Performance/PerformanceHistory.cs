// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Optimization.Models;

namespace DotCompute.Core.Optimization.Performance;

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