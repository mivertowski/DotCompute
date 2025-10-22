
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;

/// <summary>
/// GPU kernels for eigenvalue and eigenvector computations.
/// Includes power method, inverse power method, and other eigenvalue algorithms.
/// </summary>
public static class EigenKernels
{
    /// <summary>
    /// The open c l power method kernel.
    /// </summary>
    /// <summary>
    /// OpenCL kernel for power method iteration.
    /// </summary>
    public const string OpenCLPowerMethodKernel = @"
__kernel void power_method_iteration(
    __global const float* A,    // Matrix A (n x n)
    __global float* v,          // Current eigenvector estimate
    __global float* Av,         // A * v result
    __global float* norms,      // Local norms for reduction
    const int n                 // Matrix dimension
) {
    int i = get_global_id(0);
    int lid = get_local_id(0);

    if (i >= n) return;

    // Compute A * v for row i
    float sum = 0.0f;
    for (int j = 0; j < n; j++) {
        sum += A[i * n + j] * v[j];
    }
    Av[i] = sum;

    // Compute local contribution to norm
    __local float local_norms[256];
    local_norms[lid] = sum * sum;

    barrier(CLK_LOCAL_MEM_FENCE);

    // Parallel reduction for norm computation
    for (int offset = get_local_size(0) / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            local_norms[lid] += local_norms[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    // Store partial norm
    if (lid == 0) {
        norms[get_group_id(0)] = sqrt(local_norms[0]);
    }
}";
    /// <summary>
    /// The c u d a inverse power method kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for inverse power method with shift.
    /// </summary>
    public const string CUDAInversePowerMethodKernel = @"
extern ""C"" __global__ void inverse_power_method_cuda(
    const float* A,         // Matrix A
    float* v,               // Current vector
    float* work,            // Work array
    const float shift,      // Shift parameter
    const int n,            // Matrix size
    const int iteration     // Current iteration
) {
    int i = blockIdx.x * blockDim.x + threadIdx.x;

    if (i >= n) return;

    // Solve (A - shift*I) * v_new = v_old
    // This would typically be done with a pre-factored system

    __shared__ float shared_v[256];

    // Load vector into shared memory
    if (threadIdx.x < min(n, 256)) {
        shared_v[threadIdx.x] = v[threadIdx.x + blockIdx.x * 256];
    }

    __syncthreads();

    // Apply shifted matrix operation
    float sum = 0.0f;
    for (int j = 0; j < n; j++) {
        float a_val = A[i * n + j];
        if (i == j) a_val -= shift; // Apply shift
        sum += a_val * v[j];
    }

    work[i] = sum;
}";
}