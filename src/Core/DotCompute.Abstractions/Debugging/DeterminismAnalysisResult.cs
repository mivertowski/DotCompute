// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Results from determinism analysis of kernel execution.
/// </summary>
public class DeterminismAnalysisResult
{
    /// <summary>
    /// Name of the analyzed kernel.
    /// </summary>
    public required string KernelName { get; set; }

    /// <summary>
    /// Whether the kernel exhibits deterministic behavior.
    /// </summary>
    public bool IsDeterministic { get; set; }

    /// <summary>
    /// Number of test executions performed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Number of runs performed for analysis.
    /// </summary>
    public int RunCount { get; set; }

    /// <summary>
    /// Maximum variation detected between executions.
    /// </summary>
    public float MaxVariation { get; set; }

    /// <summary>
    /// Variability score between executions (0-1).
    /// </summary>
    public double VariabilityScore { get; set; }

    /// <summary>
    /// List of non-deterministic components identified.
    /// </summary>
    public List<string> NonDeterministicComponents { get; set; } = [];

    /// <summary>
    /// Identified source of non-determinism (if any).
    /// </summary>
    public string? NonDeterminismSource { get; set; }

    /// <summary>
    /// Recommendations for achieving deterministic behavior.
    /// </summary>
    public List<string> Recommendations { get; set; } = [];

    /// <summary>
    /// All execution results for analysis.
    /// </summary>
    public List<object> AllResults { get; set; } = [];

    /// <summary>
    /// Statistical analysis of result variations.
    /// </summary>
    public Dictionary<string, object> StatisticalAnalysis { get; set; } = [];
}