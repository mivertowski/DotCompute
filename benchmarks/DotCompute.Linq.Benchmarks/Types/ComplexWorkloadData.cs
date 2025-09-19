// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Complex workload data structure.
/// </summary>
public class ComplexWorkloadData
{
    public int Id { get; set; }
    public float[] Values { get; set; } = Array.Empty<float>();
    public float[] Weights { get; set; } = Array.Empty<float>();
    public int Category { get; set; }
    public DateTime Timestamp { get; set; }
}