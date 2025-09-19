// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// ML optimization benchmark result.
/// </summary>
public class MLOptimizationResult
{
    public string OptimalBackend { get; set; } = string.Empty;
    public TimeSpan OptimalExecutionTime { get; set; }
    public List<(string Backend, TimeSpan ExecutionTime, bool Success)> AllResults { get; set; } = new();
    public int DataSize { get; set; }
    public OptimizationStrategy OptimizationStrategy { get; set; }
}