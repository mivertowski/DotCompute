
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels.LinearAlgebra;

/// <summary>
/// LAPACK-style iterative solver kernels for GPU execution.
/// Includes Conjugate Gradient, BiCGSTAB, and other iterative methods.
/// </summary>
public static class LAPACKKernels
{
    /// <summary>
    /// The open c l conjugate gradient kernel.
    /// </summary>
    /// <summary>
    /// OpenCL kernel for Conjugate Gradient iteration.
    /// </summary>
    public const string OpenCLConjugateGradientKernel = @"
__kernel void cg_iteration_step(
    __global float* x,          // Current solution
    __global float* r,          // Current residual
    __global float* p,          // Search direction
    __global const float* Ap,   // Matrix-vector product A*p
    const float alpha,          // Step size
    const float beta,           // CG parameter
    const int n                 // Vector size
) {
    int i = get_global_id(0);

    if (i >= n) return;

    // Update solution: x = x + alpha * p
    x[i] = x[i] + alpha * p[i];

    // Update residual: r = r - alpha * Ap
    float new_r = r[i] - alpha * Ap[i];
    r[i] = new_r;

    // Update search direction: p = r + beta * p
    p[i] = new_r + beta * p[i];
}";
    /// <summary>
    /// The c u d a bi c g s t a b kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for BiCGSTAB iteration with preconditioning.
    /// </summary>
    public const string CUDABiCGSTABKernel = @"
extern ""C"" __global__ void bicgstab_iteration_cuda(
    float* x,              // Solution vector
    float* r,              // Residual vector
    float* r_star,         // Conjugate residual
    float* p,              // Search direction
    float* v,              // Temporary vector
    const float* Ap,       // Matrix-vector product
    const float alpha,     // Step size parameters
    const float beta,
    const float omega,
    const int n            // Vector size
) {
    int i = blockIdx.x * blockDim.x + threadIdx.x;

    if (i >= n) return;

    // BiCGSTAB update steps
    float s_i = r[i] - alpha * v[i];

    // Check if we can stop early
    float norm_s = s_i * s_i; // Will need reduction

    // Update solution
    x[i] = x[i] + alpha * p[i] + omega * s_i;

    // Update residual
    float new_r = s_i - omega * Ap[i];
    r[i] = new_r;

    // Compute beta for next iteration
    float beta_next = (alpha / omega) * beta;
    p[i] = new_r + beta_next * (p[i] - omega * v[i]);
}";
}
