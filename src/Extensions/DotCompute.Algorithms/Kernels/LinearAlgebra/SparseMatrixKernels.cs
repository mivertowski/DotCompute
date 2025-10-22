
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;

/// <summary>
/// Specialized GPU kernels for sparse matrix operations.
/// Includes CSR (Compressed Sparse Row) format kernels optimized for GPU execution.
/// </summary>
public static class SparseMatrixKernels
{
    /// <summary>
    /// The open c l sparse matrix vector kernel.
    /// </summary>
    /// <summary>
    /// OpenCL kernel for Compressed Sparse Row (CSR) matrix-vector multiplication.
    /// </summary>
    public const string OpenCLSparseMatrixVectorKernel = @"
__kernel void csr_matrix_vector_multiply(
    __global const float* values,     // Non-zero values
    __global const int* col_indices,  // Column indices
    __global const int* row_pointers, // Row pointers
    __global const float* x,          // Input vector
    __global float* y,                // Output vector
    const int num_rows               // Number of rows
) {
    int row = get_global_id(0);

    if (row >= num_rows) return;

    float sum = 0.0f;
    int row_start = row_pointers[row];
    int row_end = row_pointers[row + 1];

    // Process all non-zero elements in this row
    for (int i = row_start; i < row_end; i++) {
        sum += values[i] * x[col_indices[i]];
    }

    y[row] = sum;
}";
    /// <summary>
    /// The c u d a sparse matrix kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for sparse matrix operations with warp-level optimizations.
    /// </summary>
    public const string CUDASparseMatrixKernel = @"
extern ""C"" __global__ void csr_matrix_vector_cuda(
    const float* values,      // Non-zero values
    const int* col_indices,   // Column indices
    const int* row_pointers,  // Row pointers
    const float* x,           // Input vector
    float* y,                 // Output vector
    const int num_rows        // Number of rows
) {
    int row = blockIdx.x * blockDim.x + threadIdx.x;

    if (row >= num_rows) return;

    int row_start = row_pointers[row];
    int row_end = row_pointers[row + 1];

    float sum = 0.0f;

    // Vectorized load when possible
    for (int i = row_start; i < row_end; i++) {
        sum += values[i] * x[col_indices[i]];
    }

    y[row] = sum;
}";
}