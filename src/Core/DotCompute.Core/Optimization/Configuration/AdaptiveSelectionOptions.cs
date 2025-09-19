// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Configuration;

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