// <copyright file="PerformanceMetrics.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Metrics;

/// <summary>
/// Tracks performance metrics for BLAS operations.
/// Used for profiling and optimization of linear algebra computations.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Gets or sets the name of the BLAS operation.
    /// Examples: "SGEMM", "DGEMV", "SAXPY".
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of floating-point operations performed.
    /// Used to calculate GFLOPS (billion floating-point operations per second).
    /// </summary>
    public long TotalFlops { get; set; }

    /// <summary>
    /// Gets or sets the number of times this operation has been called.
    /// Used for averaging performance metrics.
    /// </summary>
    public int CallCount { get; set; }

    /// <summary>
    /// Gets or sets the total execution time in milliseconds.
    /// Accumulated across all calls for this operation.
    /// </summary>
    public double TotalTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// Calculated as TotalTimeMs / CallCount.
    /// </summary>
    public double AverageTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the performance in GFLOPS.
    /// Calculated as (TotalFlops / TotalTimeMs) / 1e6.
    /// </summary>
    public double GFlops { get; set; }
}