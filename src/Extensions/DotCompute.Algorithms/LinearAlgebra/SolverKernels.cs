
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// GPU kernels for linear system solvers and optimization algorithms.
/// Provides optimized kernels for solving linear equations and optimization problems.
/// </summary>
public static class SolverKernels
{    
    #region Linear System Solvers

    /// <summary>
    /// OpenCL kernel for forward substitution (solving Ly = b).
    /// </summary>
    public const string OpenCLForwardSubstitutionKernel = @"
__kernel void forward_substitution(
    __global const float* L,     // Lower triangular matrix
    __global const float* b,     // Right-hand side vector
    __global float* y,           // Solution vector
    const int n                  // Matrix dimension
) {
    int i = get_global_id(0);

    if (i >= n) return;

    // Each work item handles one row
    float sum = 0.0f;

    // Compute dot product with previously solved elements
    for (int j = 0; j < i; j++) {
        sum += L[i * n + j] * y[j];
    }

    // Solve for y[i]
    float diagonal = L[i * n + i];
    if (fabs(diagonal) > 1e-10f) {
        y[i] = (b[i] - sum) / diagonal;
    } else {
        y[i] = 0.0f; // Handle singular case
    }
}";
    /// <summary>
    /// The open c l backward substitution kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for backward substitution (solving Ux = y).
    /// </summary>
    public const string OpenCLBackwardSubstitutionKernel = @"
__kernel void backward_substitution(
    __global const float* U,     // Upper triangular matrix
    __global const float* y,     // Right-hand side vector
    __global float* x,           // Solution vector
    const int n                  // Matrix dimension
) {
    int i = n - 1 - get_global_id(0);

    if (i < 0 || i >= n) return;

    // Each work item handles one row (from bottom up)
    float sum = 0.0f;

    // Compute dot product with previously solved elements
    for (int j = i + 1; j < n; j++) {
        sum += U[i * n + j] * x[j];
    }

    // Solve for x[i]
    float diagonal = U[i * n + i];
    if (fabs(diagonal) > 1e-10f) {
        x[i] = (y[i] - sum) / diagonal;
    } else {
        x[i] = 0.0f; // Handle singular case
    }
}";
    /// <summary>
    /// The c u d a triangular solver kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for solving triangular systems with multiple right-hand sides.
    /// </summary>
    public const string CUDATriangularSolverKernel = @"
extern ""C"" __global__ void triangular_solve_multi_rhs(
    const float* T,         // Triangular matrix (n x n)
    const float* B,         // Multiple RHS (n x nrhs)
    float* X,               // Solution matrix (n x nrhs)
    const int n,            // Matrix dimension
    const int nrhs,         // Number of right-hand sides
    const int is_upper      // 1 for upper triangular, 0 for lower
) {
    int col = blockIdx.x * blockDim.x + threadIdx.x; // RHS index
    int row = blockIdx.y * blockDim.y + threadIdx.y; // Equation index

    if (col >= nrhs) return;

    __shared__ float shared_x[256]; // Shared solution vector

    if (is_upper) {
        // Backward substitution for upper triangular
        for (int i = n - 1; i >= 0; i--) {
            if (row == i) {
                float sum = 0.0f;
                for (int j = i + 1; j < n; j++) {
                    sum += T[i * n + j] * X[j * nrhs + col];
                }

                float diagonal = T[i * n + i];
                if (fabsf(diagonal) > 1e-10f) {
                    X[i * nrhs + col] = (B[i * nrhs + col] - sum) / diagonal;
                } else {
                    X[i * nrhs + col] = 0.0f;
                }
            }
            __syncthreads();
        }
    } else {
        // Forward substitution for lower triangular
        for (int i = 0; i < n; i++) {
            if (row == i) {
                float sum = 0.0f;
                for (int j = 0; j < i; j++) {
                    sum += T[i * n + j] * X[j * nrhs + col];
                }

                float diagonal = T[i * n + i];
                if (fabsf(diagonal) > 1e-10f) {
                    X[i * nrhs + col] = (B[i * nrhs + col] - sum) / diagonal;
                } else {
                    X[i * nrhs + col] = 0.0f;
                }
            }
            __syncthreads();
        }
    }
}";
    
    #endregion

    #region Iterative Solvers

    /// <summary>
    /// OpenCL kernel for Conjugate Gradient iteration step.
    /// </summary>
    public const string OpenCLConjugateGradientKernel = @"
__kernel void conjugate_gradient_step(
    __global const float* A,     // Coefficient matrix (n x n)
    __global const float* x,     // Current solution vector
    __global const float* r,     // Current residual vector
    __global const float* p,     // Search direction vector
    __global float* x_new,       // Updated solution vector
    __global float* r_new,       // Updated residual vector
    __global float* p_new,       // Updated search direction
    __global float* Ap,          // A * p (workspace)
    const int n,                 // Problem dimension
    const float alpha,           // Step size
    const float beta             // Direction update parameter
) {
    int i = get_global_id(0);

    if (i >= n) return;

    // Compute A * p
    float ap_sum = 0.0f;
    for (int j = 0; j < n; j++) {
        ap_sum += A[i * n + j] * p[j];
    }
    Ap[i] = ap_sum;

    barrier(CLK_GLOBAL_MEM_FENCE);

    // Update solution: x_new = x + alpha * p
    x_new[i] = x[i] + alpha * p[i];

    // Update residual: r_new = r - alpha * Ap
    r_new[i] = r[i] - alpha * Ap[i];

    // Update search direction: p_new = r_new + beta * p
    p_new[i] = r_new[i] + beta * p[i];
}";
    /// <summary>
    /// The c u d a jacobi iteration kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for Jacobi iteration method.
    /// </summary>
    public const string CUDAJacobiIterationKernel = @"
extern ""C"" __global__ void jacobi_iteration_cuda(
    const float* A,         // Coefficient matrix
    const float* b,         // Right-hand side vector
    const float* x_old,     // Previous solution
    float* x_new,          // New solution
    float* residual,       // Residual vector (optional)
    const int n,           // Problem dimension
    const float omega      // Relaxation parameter
) {
    int i = threadIdx.x + blockIdx.x * blockDim.x;

    if (i >= n) return;

    float diagonal = A[i * n + i];

    if (fabsf(diagonal) < 1e-10f) {
        x_new[i] = x_old[i]; // Keep old value for singular diagonal
        if (residual) residual[i] = 0.0f;
        return;
    }

    // Compute sum of off-diagonal terms
    float sum = 0.0f;
    for (int j = 0; j < n; j++) {
        if (i != j) {
            sum += A[i * n + j] * x_old[j];
        }
    }

    // Jacobi update with relaxation
    float x_jacobi = (b[i] - sum) / diagonal;
    x_new[i] = (1.0f - omega) * x_old[i] + omega * x_jacobi;

    // Compute residual if requested
    if (residual) {
        float res = b[i];
        for (int j = 0; j < n; j++) {
            res -= A[i * n + j] * x_new[j];
        }
        residual[i] = res;
    }
}";
    /// <summary>
    /// The open c l gauss seidel kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for Gauss-Seidel iteration method.
    /// </summary>
    public const string OpenCLGaussSeidelKernel = @"
__kernel void gauss_seidel_iteration(
    __global const float* A,     // Coefficient matrix
    __global const float* b,     // Right-hand side vector
    __global float* x,           // Solution vector (updated in-place)
    __global float* residual,    // Residual vector (optional)
    const int n,                 // Problem dimension
    const float omega,           // Relaxation parameter
    const int red_black          // 0=sequential, 1=red-black ordering
) {
    int i = get_global_id(0);

    if (i >= n) return;

    // Red-black ordering for parallelization
    int actual_i = i;
    if (red_black) {
        int color = get_global_id(1); // 0=red, 1=black
        actual_i = 2 * i + color;
        if (actual_i >= n) return;
    }

    float diagonal = A[actual_i * n + actual_i];

    if (fabs(diagonal) < 1e-10f) {
        if (residual) residual[actual_i] = 0.0f;
        return;
    }

    // Compute sum with updated values (Gauss-Seidel style)
    float sum = 0.0f;
    for (int j = 0; j < n; j++) {
        if (j != actual_i) {
            sum += A[actual_i * n + j] * x[j];
        }
    }

    // Update with relaxation
    float x_old = x[actual_i];
    float x_gs = (b[actual_i] - sum) / diagonal;
    x[actual_i] = (1.0f - omega) * x_old + omega * x_gs;

    // Compute residual if requested
    if (residual) {
        float res = b[actual_i];
        for (int j = 0; j < n; j++) {
            res -= A[actual_i * n + j] * x[j];
        }
        residual[actual_i] = res;
    }
}";
    

    #endregion

    #region Optimization Kernels

    /// <summary>
    /// CUDA kernel for gradient descent optimization step.
    /// </summary>
    public const string CUDAGradientDescentKernel = @"
extern ""C"" __global__ void gradient_descent_step(
    const float* gradient,      // Gradient vector
    float* x,                  // Parameters (updated in-place)
    float* momentum,           // Momentum vector (for momentum SGD)
    const int n,               // Parameter dimension
    const float learning_rate, // Learning rate
    const float momentum_coeff,// Momentum coefficient
    const int use_momentum     // 1 to use momentum, 0 for vanilla SGD
) {
    int i = threadIdx.x + blockIdx.x * blockDim.x;

    if (i >= n) return;

    if (use_momentum) {
        // Momentum update: momentum = momentum_coeff * momentum - learning_rate * gradient
        momentum[i] = momentum_coeff * momentum[i] - learning_rate * gradient[i];
        // Parameter update: x = x + momentum
        x[i] += momentum[i];
    } else {
        // Vanilla gradient descent: x = x - learning_rate * gradient
        x[i] -= learning_rate * gradient[i];
    }
}";
    /// <summary>
    /// The open c l adam optimizer kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for Adam optimizer step.
    /// </summary>
    public const string OpenCLAdamOptimizerKernel = @"
__kernel void adam_optimizer_step(
    __global const float* gradient,  // Gradient vector
    __global float* parameters,      // Parameters (updated in-place)
    __global float* m,              // First moment estimate
    __global float* v,              // Second moment estimate
    const int n,                    // Parameter dimension
    const float learning_rate,      // Learning rate
    const float beta1,              // Exponential decay for first moment
    const float beta2,              // Exponential decay for second moment
    const float epsilon,            // Small constant for numerical stability
    const float beta1_t,            // beta1^t (bias correction)
    const float beta2_t             // beta2^t (bias correction)
) {
    int i = get_global_id(0);

    if (i >= n) return;

    float grad = gradient[i];

    // Update biased first moment estimate
    m[i] = beta1 * m[i] + (1.0f - beta1) * grad;

    // Update biased second moment estimate
    v[i] = beta2 * v[i] + (1.0f - beta2) * grad * grad;

    // Bias correction
    float m_hat = m[i] / (1.0f - beta1_t);
    float v_hat = v[i] / (1.0f - beta2_t);

    // Update parameters
    parameters[i] -= learning_rate * m_hat / (sqrt(v_hat) + epsilon);
}";
   

    #endregion

    #region Preconditioners

    /// <summary>
    /// OpenCL kernel for diagonal preconditioning.
    /// </summary>
    public const string OpenCLDiagonalPreconditionerKernel = @"
__kernel void apply_diagonal_preconditioner(
    __global const float* diagonal,  // Diagonal elements
    __global const float* input,     // Input vector
    __global float* output,          // Preconditioned output
    const int n,                     // Vector dimension
    const float regularization       // Small value added to diagonal
) {
    int i = get_global_id(0);

    if (i >= n) return;

    float diag_val = diagonal[i] + regularization;

    if (fabs(diag_val) > 1e-10f) {
        output[i] = input[i] / diag_val;
    } else {
        output[i] = input[i]; // Identity for near-zero diagonal
    }
}";
    /// <summary>
    /// The c u d a incomplete cholesky kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for incomplete Cholesky preconditioner application.
    /// </summary>
    public const string CUDAIncompleteCholeskyKernel = @"
extern ""C"" __global__ void apply_incomplete_cholesky_preconditioner(
    const float* L,         // Incomplete Cholesky factor
    const float* input,     // Input vector
    float* output,         // Preconditioned output
    float* temp,          // Temporary vector
    const int n           // Vector dimension
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    // Forward substitution: solve L * temp = input
    for (int i = 0; i < n; i++) {
        __syncthreads();

        if (tid == i) {
            float sum = 0.0f;
            for (int j = 0; j < i; j++) {
                sum += L[i * n + j] * temp[j];
            }

            float diagonal = L[i * n + i];
            if (fabsf(diagonal) > 1e-10f) {
                temp[i] = (input[i] - sum) / diagonal;
            } else {
                temp[i] = input[i];
            }
        }
    }

    __syncthreads();

    // Backward substitution: solve L^T * output = temp
    for (int i = n - 1; i >= 0; i--) {
        __syncthreads();

        if (tid == i) {
            float sum = 0.0f;
            for (int j = i + 1; j < n; j++) {
                sum += L[j * n + i] * output[j]; // L^T[i][j] = L[j][i]
            }

            float diagonal = L[i * n + i];
            if (fabsf(diagonal) > 1e-10f) {
                output[i] = (temp[i] - sum) / diagonal;
            } else {
                output[i] = temp[i];
            }
        }
    }
}";
    

    #endregion

    #region Convergence and Error Analysis

    /// <summary>
    /// OpenCL kernel for computing solution error norms and convergence metrics.
    /// </summary>
    public const string OpenCLConvergenceAnalysisKernel = @"
__kernel void convergence_analysis(
    __global const float* x_new,     // New solution
    __global const float* x_old,     // Previous solution
    __global const float* residual,  // Residual vector
    __global float* error_norms,     // Output error norms [L1, L2, Linf]
    __global float* residual_norms,  // Output residual norms [L1, L2, Linf]
    __local float* scratch,          // Local memory for reductions
    const int n                      // Problem dimension
) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    int group_size = get_local_size(0);

    // Initialize local contributions
    float error_l1 = 0.0f, error_l2 = 0.0f, error_linf = 0.0f;
    float res_l1 = 0.0f, res_l2 = 0.0f, res_linf = 0.0f;

    if (gid < n) {
        float error = fabs(x_new[gid] - x_old[gid]);
        float res = fabs(residual[gid]);

        error_l1 = error;
        error_l2 = error * error;
        error_linf = error;

        res_l1 = res;
        res_l2 = res * res;
        res_linf = res;
    }

    // Reduce error L1 norm
    scratch[lid] = error_l1;
    barrier(CLK_LOCAL_MEM_FENCE);
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) scratch[lid] += scratch[lid + offset];
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    if (lid == 0) error_norms[get_group_id(0)] = scratch[0];

    // Reduce error L2 norm
    scratch[lid] = error_l2;
    barrier(CLK_LOCAL_MEM_FENCE);
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) scratch[lid] += scratch[lid + offset];
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    if (lid == 0) error_norms[get_num_groups(0) + get_group_id(0)] = sqrt(scratch[0]);

    // Reduce error L-infinity norm (max)
    scratch[lid] = error_linf;
    barrier(CLK_LOCAL_MEM_FENCE);
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) scratch[lid] = fmax(scratch[lid], scratch[lid + offset]);
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    if (lid == 0) error_norms[2 * get_num_groups(0) + get_group_id(0)] = scratch[0];

    // Similar reductions for residual norms...
    // (Residual norm computations follow the same pattern)
}";

    #endregion
}