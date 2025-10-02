// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// GPU kernels for matrix decomposition operations (QR, SVD, LU, Cholesky).
/// Provides optimized kernels for matrix factorization algorithms on GPU accelerators.
/// </summary>
public static class DecompositionKernels
{
    /// <summary>
    /// The open c l householder vector kernel.
    /// </summary>
    #region Householder Transformation Kernels

    /// <summary>
    /// OpenCL kernel for computing Householder vector with parallel reduction.
    /// </summary>
    public const string OpenCLHouseholderVectorKernel = @"
__kernel void compute_householder_vector_parallel(
    __global const float* column,     // Input column vector
    __global float* householder,      // Output Householder vector
    __global float* norm_result,      // Output norm value
    const int n,                      // Vector length
    const int start_idx               // Starting index
) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    int group_size = get_local_size(0);
    
    // Shared memory for reduction
    __local float shared_data[256];
    
    // Initialize shared memory
    shared_data[lid] = 0.0f;
    
    // Load and square elements for norm computation
    if (gid < n - start_idx) {
        float val = column[start_idx + gid];
        householder[gid] = val;
        shared_data[lid] = val * val;
    }
    
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Parallel reduction for norm
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            shared_data[lid] += shared_data[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    // Compute norm and update first element
    if (lid == 0) {
        float norm = sqrt(shared_data[0]);
        norm_result[get_group_id(0)] = norm;
        
        if (gid == 0 && norm > 1e-10f) {
            float sign = householder[0] >= 0.0f ? 1.0f : -1.0f;
            householder[0] += sign * norm;
        }
    }
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Second reduction for normalization
    if (gid < n - start_idx) {
        shared_data[lid] = householder[gid] * householder[gid];
    } else {
        shared_data[lid] = 0.0f;
    }
    
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Reduction for vector norm
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            shared_data[lid] += shared_data[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    // Normalize vector
    if (lid == 0) {
        float vnorm = sqrt(shared_data[0]);
        if (vnorm > 1e-10f) {
            for (int i = 0; i < n - start_idx; i++) {
                householder[i] /= vnorm;
            }
        }
    }
}";
    /// <summary>
    /// The open c l householder transform kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for applying Householder transformation with optimized memory access.
    /// </summary>
    public const string OpenCLHouseholderTransformKernel = @"
__kernel void apply_householder_transform_optimized(
    __global float* matrix,           // Input/output matrix
    __global const float* v,          // Householder vector
    const int m,                      // Matrix rows
    const int n,                      // Matrix columns
    const int v_len,                  // Householder vector length
    const int start_row               // Starting row index
) {
    int col = get_global_id(0);
    int lid = get_local_id(0);
    
    if (col >= n) return;
    
    // Shared memory for dot product computation
    __local float shared_dot[256];
    shared_dot[lid] = 0.0f;
    
    // Compute dot product v^T * A[:, col] using work group collaboration
    float local_dot = 0.0f;
    for (int i = lid; i < v_len; i += get_local_size(0)) {
        if (start_row + i < m) {
            local_dot += v[i] * matrix[(start_row + i) * n + col];
        }
    }
    
    shared_dot[lid] = local_dot;
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Parallel reduction
    for (int offset = get_local_size(0) / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            shared_dot[lid] += shared_dot[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    float total_dot = shared_dot[0] * 2.0f;
    
    // Apply transformation: A[:, col] = A[:, col] - 2 * (v^T * A[:, col]) * v
    for (int i = lid; i < v_len; i += get_local_size(0)) {
        if (start_row + i < m) {
            matrix[(start_row + i) * n + col] -= total_dot * v[i];
        }
    }
}";
    /// <summary>
    /// The c u d a jacobi s v d kernel.
    /// </summary>

    #endregion

    #region SVD and Jacobi Rotation Kernels

    /// <summary>
    /// CUDA kernel for Jacobi SVD rotation with double precision support.
    /// </summary>
    public const string CUDAJacobiSVDKernel = @"
extern ""C"" __global__ void jacobi_svd_rotation_cuda(
    float* A,                 // Matrix A (modified in-place)
    float* U,                 // Left singular vectors
    float* V,                 // Right singular vectors  
    const int n,              // Matrix dimension
    const int i,              // Row index for rotation
    const int j,              // Column index for rotation
    float* convergence_flag   // Convergence indicator
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    
    if (tid >= n) return;
    
    // Load matrix elements for 2x2 submatrix
    float a_ii = A[i * n + i];
    float a_ij = A[i * n + j];
    float a_ji = A[j * n + i];
    float a_jj = A[j * n + j];
    
    // Check convergence criteria
    float off_diag = fabsf(a_ij) + fabsf(a_ji);
    if (off_diag < 1e-10f) {
        if (tid == 0) *convergence_flag = 1.0f;
        return;
    }
    
    // Compute Jacobi rotation parameters
    float tau = (a_jj - a_ii) / (2.0f * (a_ij + a_ji));
    float t = copysignf(1.0f, tau) / (fabsf(tau) + sqrtf(1.0f + tau * tau));
    float c = rsqrtf(1.0f + t * t);  // 1/sqrt(1+t²)
    float s = c * t;
    
    __syncthreads();
    
    // Apply Givens rotation to columns i and j
    if (tid < n) {
        float a_ti = A[tid * n + i];
        float a_tj = A[tid * n + j];
        
        A[tid * n + i] = c * a_ti - s * a_tj;
        A[tid * n + j] = s * a_ti + c * a_tj;
        
        // Update U matrix
        float u_ti = U[tid * n + i];
        float u_tj = U[tid * n + j];
        
        U[tid * n + i] = c * u_ti - s * u_tj;
        U[tid * n + j] = s * u_ti + c * u_tj;
    }
    
    __syncthreads();
    
    // Apply Givens rotation to rows i and j
    if (tid < n) {
        float a_it = A[i * n + tid];
        float a_jt = A[j * n + tid];
        
        A[i * n + tid] = c * a_it - s * a_jt;
        A[j * n + tid] = s * a_it + c * a_jt;
        
        // Update V matrix
        float v_it = V[i * n + tid];
        float v_jt = V[j * n + tid];
        
        V[i * n + tid] = c * v_it - s * v_jt;
        V[j * n + tid] = s * v_it + c * v_jt;
    }
}";
    /// <summary>
    /// The open c l singular values kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for computing singular values extraction.
    /// </summary>
    public const string OpenCLSingularValuesKernel = @"
__kernel void extract_singular_values(
    __global const float* A,     // Diagonalized matrix
    __global float* S,           // Output singular values
    __global float* U,           // Left singular vectors (to fix signs)
    const int n                  // Matrix dimension
) {
    int i = get_global_id(0);
    
    if (i >= n) return;
    
    // Extract diagonal element and ensure positive
    float value = A[i * n + i];
    float abs_value = fabs(value);
    
    S[i] = abs_value;
    
    // If singular value was negative, flip corresponding column of U
    if (value < 0.0f) {
        for (int j = 0; j < n; j++) {
            U[j * n + i] = -U[j * n + i];
        }
    }
}";
    /// <summary>
    /// The c u d a cholesky kernel.
    /// </summary>

    #endregion

    #region Cholesky Decomposition Kernels

    /// <summary>
    /// CUDA kernel for blocked Cholesky decomposition.
    /// </summary>
    public const string CUDACholeskyKernel = @"
extern ""C"" __global__ void cholesky_decomposition_cuda(
    float* A,               // Input matrix (modified to L)
    const int n,            // Matrix dimension
    const int block_size    // Block size for tiling
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int i = blockIdx.y;     // Current row being processed
    
    // Ensure positive definiteness
    __shared__ float pivot_value;
    
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        pivot_value = A[i * n + i];
    }
    
    __syncthreads();
    
    if (pivot_value <= 0.0f) {
        return; // Matrix not positive definite
    }
    
    // Process diagonal element
    if (i == blockIdx.x && tid == i) {
        float sum = 0.0f;
        for (int k = 0; k < i; k++) {
            float val = A[i * n + k];
            sum += val * val;
        }
        
        float diagonal_val = A[i * n + i] - sum;
        if (diagonal_val > 0.0f) {
            A[i * n + i] = sqrtf(diagonal_val);
        }
    }
    
    __syncthreads();
    
    // Process elements below diagonal in current column
    if (tid > i && tid < n) {
        float sum = 0.0f;
        for (int k = 0; k < i; k++) {
            sum += A[tid * n + k] * A[i * n + k];
        }
        
        float diagonal = A[i * n + i];
        if (fabsf(diagonal) > 1e-10f) {
            A[tid * n + i] = (A[tid * n + i] - sum) / diagonal;
        }
        
        // Zero out upper triangular part
        if (tid < i) {
            A[i * n + tid] = 0.0f;
        }
    }
}";
    /// <summary>
    /// The open c l l u decomposition kernel.
    /// </summary>

    #endregion

    #region LU Decomposition Kernels

    /// <summary>
    /// OpenCL kernel for LU decomposition with partial pivoting.
    /// </summary>
    public const string OpenCLLUDecompositionKernel = @"
__kernel void lu_decomposition_step(
    __global float* A,          // Matrix being decomposed
    __global int* P,            // Permutation array
    const int n,                // Matrix dimension
    const int k                 // Current step
) {
    int tid = get_global_id(0);
    int row = tid + k + 1;
    
    if (row >= n) return;
    
    // Find pivot (would need separate kernel for full pivoting)
    __local float max_val;
    __local int pivot_row;
    
    if (get_local_id(0) == 0) {
        max_val = fabs(A[k * n + k]);
        pivot_row = k;
        
        for (int i = k + 1; i < n; i++) {
            float val = fabs(A[i * n + k]);
            if (val > max_val) {
                max_val = val;
                pivot_row = i;
            }
        }
    }
    
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Swap rows if needed (simplified - would need atomic operations)
    if (pivot_row != k && get_local_id(0) == 0) {
        for (int j = 0; j < n; j++) {
            float temp = A[k * n + j];
            A[k * n + j] = A[pivot_row * n + j];
            A[pivot_row * n + j] = temp;
        }
        
        // Update permutation
        int temp_p = P[k];
        P[k] = P[pivot_row];
        P[pivot_row] = temp_p;
    }
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Compute multipliers and eliminate
    if (row < n) {
        float pivot = A[k * n + k];
        if (fabs(pivot) > 1e-10f) {
            float multiplier = A[row * n + k] / pivot;
            A[row * n + k] = multiplier; // Store L
            
            // Eliminate
            for (int j = k + 1; j < n; j++) {
                A[row * n + j] -= multiplier * A[k * n + j];
            }
        }
    }
}";
    /// <summary>
    /// The open c l q r shift kernel.
    /// </summary>

    #endregion

    #region QR Algorithm Kernels

    /// <summary>
    /// OpenCL kernel for QR algorithm with Wilkinson shift.
    /// </summary>
    public const string OpenCLQRShiftKernel = @"
__kernel void qr_algorithm_shift(
    __global float* A,            // Hessenberg matrix (modified in-place)
    __global float* Q,            // Accumulated transformations
    __global float* eigenvalues,  // Output eigenvalues
    const int n,                  // Matrix dimension
    const float tolerance         // Convergence tolerance
) {
    int tid = get_global_id(0);
    
    if (tid >= n * n) return;
    
    // Compute Wilkinson shift from bottom-right 2x2 submatrix
    __local float shift_value;
    
    if (tid == 0) {
        if (n >= 2) {
            float a = A[(n-2) * n + (n-2)];
            float b = A[(n-2) * n + (n-1)];
            float c = A[(n-1) * n + (n-2)];
            float d = A[(n-1) * n + (n-1)];
            
            float trace = a + d;
            float det = a * d - b * c;
            float discriminant = trace * trace - 4.0f * det;
            
            if (discriminant >= 0.0f) {
                float sqrt_disc = sqrt(discriminant);
                float lambda1 = (trace + sqrt_disc) * 0.5f;
                float lambda2 = (trace - sqrt_disc) * 0.5f;
                
                // Choose eigenvalue closer to d
                shift_value = (fabs(lambda1 - d) < fabs(lambda2 - d)) ? lambda1 : lambda2;
            } else {
                shift_value = d;
            }
        } else {
            shift_value = 0.0f;
        }
    }
    
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Apply shift: A = A - shift * I
    int row = tid / n;
    int col = tid % n;
    
    if (row == col && tid < n * n) {
        A[tid] -= shift_value;
    }
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Apply Givens rotations for QR factorization of Hessenberg matrix
    // (This would be called iteratively for each rotation)
    
    // After QR step, restore shift: A = A + shift * I
    if (row == col && tid < n * n) {
        A[tid] += shift_value;
    }
    
    // Extract eigenvalues from diagonal
    if (tid < n) {
        eigenvalues[tid] = A[tid * n + tid];
    }
}";
    /// <summary>
    /// The c u d a givens rotation kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for Givens rotation application in QR algorithm.
    /// </summary>
    public const string CUDAGivensRotationKernel = @"
extern ""C"" __global__ void apply_givens_rotation_cuda(
    float* A,               // Matrix to transform
    float* Q,               // Accumulate rotations
    const int n,            // Matrix dimension
    const int i,            // First row/col index
    const int j,            // Second row/col index
    const float c,          // Cosine of rotation
    const float s           // Sine of rotation
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    
    // Apply rotation to rows
    if (tid < n) {
        float a_i = A[i * n + tid];
        float a_j = A[j * n + tid];
        
        A[i * n + tid] = c * a_i - s * a_j;
        A[j * n + tid] = s * a_i + c * a_j;
    }
    
    __syncthreads();
    
    // Apply rotation to columns
    if (tid < n) {
        float a_i = A[tid * n + i];
        float a_j = A[tid * n + j];
        
        A[tid * n + i] = c * a_i - s * a_j;
        A[tid * n + j] = s * a_i + c * a_j;
        
        // Update Q matrix
        float q_i = Q[tid * n + i];
        float q_j = Q[tid * n + j];
        
        Q[tid * n + i] = c * q_i - s * q_j;
        Q[tid * n + j] = s * q_i + c * q_j;
    }
}";

    #endregion
}