// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Wrapper.BLAS.Models;

/// <summary>
/// Performance metrics for BLAS operations
/// </summary>
public class PerformanceMetrics
{
    public string Operation { get; set; } = string.Empty;
    public long TotalFlops { get; set; }
    public int CallCount { get; set; }
    public double AverageFlops => CallCount > 0 ? (double)TotalFlops / CallCount : 0;
}