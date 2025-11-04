// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#include <metal_stdlib>
using namespace metal;

/// <summary>
/// Naive matrix multiplication kernel for integration testing.
/// Computes: C = A × B where A is M×K, B is K×N, C is M×N.
/// </summary>
/// <param name="A">Input matrix A (M×K, row-major).</param>
/// <param name="B">Input matrix B (K×N, row-major).</param>
/// <param name="C">Output matrix C (M×N, row-major).</param>
/// <param name="M">Number of rows in A and C.</param>
/// <param name="N">Number of columns in B and C.</param>
/// <param name="K">Number of columns in A and rows in B.</param>
/// <param name="gid">2D global thread ID (x=col, y=row).</param>
kernel void matrix_multiply(
    device const float* A [[buffer(0)]],
    device const float* B [[buffer(1)]],
    device float* C [[buffer(2)]],
    constant uint& M [[buffer(3)]],
    constant uint& N [[buffer(4)]],
    constant uint& K [[buffer(5)]],
    uint2 gid [[thread_position_in_grid]])
{
    uint row = gid.y;
    uint col = gid.x;

    // Bounds check
    if (row >= M || col >= N) {
        return;
    }

    // Compute dot product for C[row, col]
    float sum = 0.0f;
    for (uint k = 0; k < K; k++) {
        sum += A[row * K + k] * B[k * N + col];
    }

    C[row * N + col] = sum;
}
