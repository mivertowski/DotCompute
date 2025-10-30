
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Selection.Enums;

/// <summary>
/// Parallel algorithm strategies for different concurrency patterns.
/// Each strategy is optimized for different problem characteristics and hardware.
/// </summary>
public enum ParallelStrategy
{
    /// <summary>
    /// No parallelization - single-threaded execution.
    /// </summary>
    Sequential,

    /// <summary>
    /// Task-based parallelism using .NET Task Parallel Library.
    /// </summary>
    TaskParallel,

    /// <summary>
    /// Fork-join parallelism for divide-and-conquer algorithms.
    /// </summary>
    ForkJoin,

    /// <summary>
    /// Work-stealing parallelism for dynamic load balancing.
    /// </summary>
    WorkStealing
}
