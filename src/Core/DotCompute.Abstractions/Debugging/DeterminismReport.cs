// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Report on kernel determinism across multiple executions.
/// </summary>
public class DeterminismReport
{
    /// <summary>
    /// Name of the kernel that was tested.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the kernel exhibits deterministic behavior.
    /// </summary>
    public bool IsDeterministic { get; init; }

    /// <summary>
    /// Number of test executions performed.
    /// </summary>
    public int ExecutionCount { get; init; }

    /// <summary>
    /// All execution results for analysis.
    /// </summary>
    public List<object> AllResults { get; init; } = [];

    /// <summary>
    /// Maximum variation detected between executions.
    /// </summary>
    public float MaxVariation { get; init; }

    /// <summary>
    /// Identified source of non-determinism (if any).
    /// </summary>
    public string? NonDeterminismSource { get; init; }

    /// <summary>
    /// Recommendations for achieving deterministic behavior.
    /// </summary>
    public List<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Number of runs performed during determinism testing.
    /// </summary>
    public int RunCount { get; init; }

    /// <summary>
    /// Components identified as non-deterministic.
    /// </summary>
    public List<string> NonDeterministicComponents { get; init; } = [];

    /// <summary>
    /// Time taken to perform the analysis.
    /// </summary>
    public TimeSpan AnalysisTime { get; init; }
}