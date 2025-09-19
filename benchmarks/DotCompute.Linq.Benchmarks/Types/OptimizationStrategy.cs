// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Optimization strategies for benchmarking.
/// </summary>
public enum OptimizationStrategy
{
    None,
    KernelFusion,
    MemoryOptimization,
    MLOptimization,
    All
}