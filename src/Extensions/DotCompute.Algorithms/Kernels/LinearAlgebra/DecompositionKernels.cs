
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;

/// <summary>
/// Matrix decomposition kernels for GPU execution.
/// Includes QR decomposition, LU decomposition, and Cholesky decomposition operations.
/// </summary>
public static class DecompositionKernels
{
    /// <summary>
    /// The c u d a q r decomposition kernel.
    /// </summary>
    /// <summary>
    /// CUDA kernel for QR decomposition with Householder reflections.
    /// Optimized for GPU execution with shared memory usage.
    /// </summary>
    public const string CUDAQRDecompositionKernel = @"
extern ""C"" __global__ void qr_householder_decomposition_cuda(
    float* A,              // Input/output matrix (M x N)
    float* tau,            // Householder scalars
    float* work,           // Work array
    const int M,           // Number of rows
    const int N,           // Number of columns
    const int column       // Current column being processed
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int stride = blockDim.x * gridDim.x;

    __shared__ float shared_data[256];

    // Process Householder reflection for current column
    if (column < min(M, N)) {
        // Compute norm of column below diagonal
        float norm_sq = 0.0f;
        for (int i = tid + column; i < M; i += stride) {
            float val = A[i * N + column];
            norm_sq += val * val;
        }

        // Reduction in shared memory
        shared_data[threadIdx.x] = norm_sq;
        __syncthreads();

        for (int s = blockDim.x / 2; s > 0; s >>= 1) {
            if (threadIdx.x < s) {
                shared_data[threadIdx.x] += shared_data[threadIdx.x + s];
            }
            __syncthreads();
        }

        float norm = sqrt(shared_data[0]);

        // Apply Householder reflection to remaining columns
        if (norm > 1e-10f) {
            for (int j = column + 1; j < N; j++) {
                float dot_product = 0.0f;
                for (int i = tid + column; i < M; i += stride) {
                    dot_product += A[i * N + column] * A[i * N + j];
                }

                // Apply reflection
                float factor = 2.0f * dot_product / (norm * norm);
                for (int i = tid + column; i < M; i += stride) {
                    A[i * N + j] -= factor * A[i * N + column];
                }
            }
        }
    }
}";
    /// <summary>
    /// The c u d a l u decomposition kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for LU decomposition with partial pivoting.
    /// Supports in-place decomposition with atomic operations for thread safety.
    /// </summary>
    public const string CUDALUDecompositionKernel = @"
extern ""C"" __global__ void lu_decomposition_partial_pivot_cuda(
    float* A,              // Input/output matrix (N x N)
    int* pivot,            // Pivot indices
    float* work,           // Work array
    const int N,           // Matrix dimension
    const int k            // Current elimination step
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    // Find pivot element in column k
    if (k < N) {
        __shared__ float max_val;
        __shared__ int max_idx;

        if (threadIdx.x == 0) {
            max_val = fabsf(A[k * N + k]);
            max_idx = k;
        }
        __syncthreads();

        // Find maximum element in column k below diagonal
        for (int i = tid + k + 1; i < N; i += blockDim.x * gridDim.x) {
            float val = fabsf(A[i * N + k]);
            if (val > max_val) {
                atomicCAS((int*)&max_val, *(int*)&max_val, *(int*)&val);
                max_idx = i;
            }
        }

        __syncthreads();

        // Swap rows if necessary
        if (max_idx != k && threadIdx.x == 0) {
            pivot[k] = max_idx;
            for (int j = 0; j < N; j++) {
                float temp = A[k * N + j];
                A[k * N + j] = A[max_idx * N + j];
                A[max_idx * N + j] = temp;
            }
        }

        __syncthreads();

        // Perform elimination
        float pivot_val = A[k * N + k];
        if (fabsf(pivot_val) > 1e-10f) {
            for (int i = tid + k + 1; i < N; i += blockDim.x * gridDim.x) {
                float factor = A[i * N + k] / pivot_val;
                A[i * N + k] = factor; // Store L factor

                for (int j = k + 1; j < N; j++) {
                    A[i * N + j] -= factor * A[k * N + j];
                }
            }
        }
    }
}";
    /// <summary>
    /// The c u d a cholesky decomposition kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for Cholesky decomposition of positive definite matrices.
    /// Optimized for symmetric positive definite matrices with parallel processing.
    /// </summary>
    public const string CUDACholeskyDecompositionKernel = @"
extern ""C"" __global__ void cholesky_decomposition_cuda(
    float* A,              // Input/output matrix (N x N, lower triangular on output)
    float* work,           // Work array
    const int N,           // Matrix dimension
    const int k            // Current column being processed
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    if (k < N) {
        // Compute diagonal element L[k,k] = sqrt(A[k,k] - sum(L[k,j]^2))
        if (tid == 0) {
            float sum = 0.0f;
            for (int j = 0; j < k; j++) {
                float val = A[k * N + j];
                sum += val * val;
            }
            A[k * N + k] = sqrtf(A[k * N + k] - sum);
        }

        __syncthreads();

        float diag_val = A[k * N + k];

        // Compute column k below diagonal: L[i,k] = (A[i,k] - sum(L[i,j]*L[k,j])) / L[k,k]
        for (int i = tid + k + 1; i < N; i += blockDim.x * gridDim.x) {
            float sum = 0.0f;
            for (int j = 0; j < k; j++) {
                sum += A[i * N + j] * A[k * N + j];
            }
            A[i * N + k] = (A[i * N + k] - sum) / diag_val;
        }

        __syncthreads();

        // Zero out upper triangular part for this column
        for (int i = tid; i < k; i += blockDim.x * gridDim.x) {
            A[i * N + k] = 0.0f;
        }
    }
}";
}