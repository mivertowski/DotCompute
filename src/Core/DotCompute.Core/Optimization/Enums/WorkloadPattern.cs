// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Enums;

/// <summary>
/// Classification of workload patterns for backend optimization.
/// </summary>
public enum WorkloadPattern
{
    Sequential,
    ComputeIntensive,
    MemoryIntensive,
    HighlyParallel,
    Balanced
}