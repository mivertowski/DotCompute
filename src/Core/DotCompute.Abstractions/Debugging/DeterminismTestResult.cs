// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.ObjectModel;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Represents the result of a determinism test for a kernel.
/// </summary>
public sealed class DeterminismTestResult
{
    /// <summary>
    /// Gets the name of the kernel that was tested.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of accelerator used for the test.
    /// </summary>
    public AcceleratorType AcceleratorType { get; init; }

    /// <summary>
    /// Gets the number of iterations performed.
    /// </summary>
    public int Iterations { get; init; }

    /// <summary>
    /// Gets the time when the test was performed.
    /// </summary>
    public DateTime TestTime { get; init; }

    /// <summary>
    /// Gets whether the kernel produces deterministic results.
    /// </summary>
    public bool IsDeterministic { get; set; }

    /// <summary>
    /// Gets the execution results from each iteration.
    /// </summary>
    public Collection<object?> ExecutionResults { get; init; } = [];

    /// <summary>
    /// Gets the list of issues found during determinism testing.
    /// </summary>
    public Collection<string> Issues { get; init; } = [];

    /// <summary>
    /// Gets the total execution time for all iterations.
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Gets the variance in execution times between iterations.
    /// </summary>
    public double ExecutionTimeVariance { get; init; }
}
