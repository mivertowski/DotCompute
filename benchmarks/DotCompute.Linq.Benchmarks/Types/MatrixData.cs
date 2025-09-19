// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Matrix data for linear algebra workloads.
/// </summary>
public class MatrixData
{
    public float[,] Matrix { get; set; } = new float[0,0];
    public float[] Vector { get; set; } = Array.Empty<float>();
}