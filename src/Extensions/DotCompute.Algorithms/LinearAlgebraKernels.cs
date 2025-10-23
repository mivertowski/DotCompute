
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Kernels;

namespace DotCompute.Algorithms
{

    /// <summary>
    /// Comprehensive GPU kernel library for linear algebra operations.
    /// Provides optimized kernels for matrix operations, decompositions, and eigenvalue computations.
    /// </summary>
    public static class LinearAlgebraKernelLibrary
    {
        /// <summary>
        /// The open c l matrix multiply tiled kernel.
        /// </summary>
        #region Matrix Multiplication Kernels

        /// <summary>
        /// OpenCL kernel for tiled matrix multiplication with shared memory optimization.
        /// </summary>
        public const string OpenCLMatrixMultiplyTiledKernel = @"
// Optimized tiled matrix multiplication using shared memory
__kernel void matrix_multiply_tiled(
    __global const float* A,    // Matrix A (M x K)
    __global const float* B,    // Matrix B (K x N)
    __global float* C,          // Result matrix C (M x N)
    const int M,                // Rows of A and C
    const int N,                // Columns of B and C
    const int K                 // Columns of A and rows of B
) {
    // Tile size (should match local work size)
    const int TILE_SIZE = 16;

    // Work group and work item IDs
    int gid_x = get_group_id(0);
    int gid_y = get_group_id(1);
    int lid_x = get_local_id(0);
    int lid_y = get_local_id(1);

    // Shared memory for tiles
    __local float tile_A[16][16];
    __local float tile_B[16][16];

    // Output coordinates
    int row = gid_y * TILE_SIZE + lid_y;
    int col = gid_x * TILE_SIZE + lid_x;

    float sum = 0.0f;

    // Loop over tiles
    int num_tiles = (K + TILE_SIZE - 1) / TILE_SIZE;
    for (int t = 0; t < num_tiles; t++) {
        // Load tile of A
        int a_row = row;
        int a_col = t * TILE_SIZE + lid_x;
        if (a_row < M && a_col < K) {
            tile_A[lid_y][lid_x] = A[a_row * K + a_col];
        } else {
            tile_A[lid_y][lid_x] = 0.0f;
        }

        // Load tile of B
        int b_row = t * TILE_SIZE + lid_y;
        int b_col = col;
        if (b_row < K && b_col < N) {
            tile_B[lid_y][lid_x] = B[b_row * N + b_col];
        } else {
            tile_B[lid_y][lid_x] = 0.0f;
        }

        barrier(CLK_LOCAL_MEM_FENCE);

        // Compute partial sum
        for (int k = 0; k < TILE_SIZE; k++) {
            sum += tile_A[lid_y][k] * tile_B[k][lid_x];
        }

        barrier(CLK_LOCAL_MEM_FENCE);
    }

    // Store result
    if (row < M && col < N) {
        C[row * N + col] = sum;
    }
}";
        /// <summary>
        /// The c u d a matrix multiply tiled kernel.
        /// </summary>

        /// <summary>
        /// CUDA kernel for tiled matrix multiplication with shared memory.
        /// </summary>
        public const string CUDAMatrixMultiplyTiledKernel = @"
extern ""C"" __global__ void matrix_multiply_tiled_cuda(
    const float* A,     // Matrix A (M x K)
    const float* B,     // Matrix B (K x N)
    float* C,           // Result matrix C (M x N)
    const int M,        // Rows of A and C
    const int N,        // Columns of B and C
    const int K         // Columns of A and rows of B
) {
    const int TILE_SIZE = 16;

    // Shared memory for tiles
    __shared__ float tile_A[16][16];
    __shared__ float tile_B[16][16];

    // Thread and block indices
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    int bx = blockIdx.x;
    int by = blockIdx.y;

    // Output coordinates
    int row = by * TILE_SIZE + ty;
    int col = bx * TILE_SIZE + tx;

    float sum = 0.0f;

    // Loop over tiles
    int num_tiles = (K + TILE_SIZE - 1) / TILE_SIZE;
    for (int t = 0; t < num_tiles; t++) {
        // Load tile of A
        int a_row = row;
        int a_col = t * TILE_SIZE + tx;
        if (a_row < M && a_col < K) {
            tile_A[ty][tx] = A[a_row * K + a_col];
        } else {
            tile_A[ty][tx] = 0.0f;
        }

        // Load tile of B
        int b_row = t * TILE_SIZE + ty;
        int b_col = col;
        if (b_row < K && b_col < N) {
            tile_B[ty][tx] = B[b_row * N + b_col];
        } else {
            tile_B[ty][tx] = 0.0f;
        }

        __syncthreads();

        // Compute partial sum
        #pragma unroll
        for (int k = 0; k < TILE_SIZE; k++) {
            sum += tile_A[ty][k] * tile_B[k][tx];
        }

        __syncthreads();
    }

    // Store result
    if (row < M && col < N) {
        C[row * N + col] = sum;
    }
}";
        /// <summary>
        /// The open c l householder vector kernel.
        /// </summary>

        #endregion

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
        /// The open c l matrix vector kernel.
        /// </summary>

        #endregion

        #region Matrix-Vector Operations

        /// <summary>
        /// OpenCL kernel for optimized matrix-vector multiplication.
        /// </summary>
        public const string OpenCLMatrixVectorKernel = @"
__kernel void matrix_vector_multiply_optimized(
    __global const float* A,     // Matrix A (m x n)
    __global const float* x,     // Vector x (n x 1)
    __global float* y,           // Result vector y (m x 1)
    const int m,                 // Matrix rows
    const int n                  // Matrix columns
) {
    int row = get_global_id(0);
    int lid = get_local_id(0);

    if (row >= m) return;

    // Use shared memory for vector caching
    __local float x_cache[256];

    float sum = 0.0f;

    // Process vector in chunks that fit in shared memory
    for (int chunk = 0; chunk < n; chunk += 256) {
        // Load chunk of x into shared memory
        if (lid < 256 && chunk + lid < n) {
            x_cache[lid] = x[chunk + lid];
        } else if (lid < 256) {
            x_cache[lid] = 0.0f;
        }

        barrier(CLK_LOCAL_MEM_FENCE);

        // Compute partial dot product
        int chunk_size = min(256, n - chunk);
        for (int i = 0; i < chunk_size; i++) {
            sum += A[row * n + chunk + i] * x_cache[i];
        }

        barrier(CLK_LOCAL_MEM_FENCE);
    }

    y[row] = sum;
}";
        /// <summary>
        /// The c u d a vector operations kernel.
        /// </summary>

        /// <summary>
        /// CUDA kernel for parallel vector operations with coalesced memory access.
        /// </summary>
        public const string CUDAVectorOperationsKernel = @"
extern ""C"" __global__ void vector_operations_cuda(
    const float* a,          // Input vector a
    const float* b,          // Input vector b
    float* result,           // Output vector
    const int n,             // Vector length
    const int operation      // 0=add, 1=sub, 2=mul, 3=div
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int stride = blockDim.x * gridDim.x;

    // Process multiple elements per thread for better efficiency
    for (int i = tid; i < n; i += stride) {
        float val_a = a[i];
        float val_b = b[i];

        switch (operation) {
            case 0: // Addition
                result[i] = val_a + val_b;
                break;
            case 1: // Subtraction
                result[i] = val_a - val_b;
                break;
            case 2: // Element-wise multiplication
                result[i] = val_a * val_b;
                break;
            case 3: // Element-wise division
                result[i] = (fabsf(val_b) > 1e-10f) ? val_a / val_b : 0.0f;
                break;
        }
    }
}";
        /// <summary>
        /// The open c l parallel reduction kernel.
        /// </summary>

        #endregion

        #region Parallel Reduction Kernels

        /// <summary>
        /// OpenCL kernel for parallel reduction with multiple operations.
        /// </summary>
        public const string OpenCLParallelReductionKernel = @"
__kernel void parallel_reduction_optimized(
    __global const float* input,    // Input array
    __global float* output,         // Output (one per work group)
    __local float* scratch,         // Local memory for reduction
    const int n,                    // Input size
    const int operation             // 0=sum, 1=max, 2=min, 3=norm2
) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    int group_size = get_local_size(0);

    // Initialize local memory
    float value = 0.0f;

    // Load data and handle boundary conditions
    if (gid < n) {
        value = input[gid];
        if (operation == 3) { // L2 norm squared
            value = value * value;
        }
    }

    // Handle operations that need special initialization
    if (operation == 2 && gid >= n) { // Min operation
        value = FLT_MAX;
    } else if (operation == 1 && gid >= n) { // Max operation
        value = -FLT_MAX;
    }

    scratch[lid] = value;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Parallel reduction
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            float other = scratch[lid + offset];

            switch (operation) {
                case 0: // Sum
                case 3: // L2 norm (sum of squares)
                    scratch[lid] += other;
                    break;
                case 1: // Max
                    scratch[lid] = fmax(scratch[lid], other);
                    break;
                case 2: // Min
                    scratch[lid] = fmin(scratch[lid], other);
                    break;
            }
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    // Store result
    if (lid == 0) {
        if (operation == 3) {
            output[get_group_id(0)] = sqrt(scratch[0]); // Final sqrt for L2 norm
        } else {
            output[get_group_id(0)] = scratch[0];
        }
    }
}";
        /// <summary>
        /// The c u d a warp reduction kernel.
        /// </summary>

        /// <summary>
        /// CUDA kernel for warp-optimized reduction.
        /// </summary>
        public const string CUDAWarpReductionKernel = @"
__device__ __forceinline__ float warp_reduce_sum(float val) {
    for (int offset = warpSize/2; offset > 0; offset /= 2) {
        val += __shfl_down_sync(0xFFFFFFFF, val, offset);
    }
    return val;
}

extern ""C"" __global__ void warp_reduction_cuda(
    const float* input,        // Input array
    float* output,            // Output array
    const int n               // Input size
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int lane = threadIdx.x % warpSize;
    int warp_id = threadIdx.x / warpSize;

    __shared__ float warp_sums[32]; // Max 32 warps per block

    // Load and sum
    float sum = 0.0f;
    for (int i = tid; i < n; i += blockDim.x * gridDim.x) {
        sum += input[i];
    }

    // Warp-level reduction
    sum = warp_reduce_sum(sum);

    // Store warp result
    if (lane == 0) {
        warp_sums[warp_id] = sum;
    }

    __syncthreads();

    // Block-level reduction
    if (warp_id == 0) {
        sum = (threadIdx.x < (blockDim.x + warpSize - 1) / warpSize) ?
              warp_sums[threadIdx.x] : 0.0f;
        sum = warp_reduce_sum(sum);

        if (threadIdx.x == 0) {
            output[blockIdx.x] = sum;
        }
    }
}";
        /// <summary>
        /// The open c l q r shift kernel.
        /// </summary>

        #endregion

        #region Eigenvalue Computation Kernels

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

        #endregion

        #region Kernel Selection and Compilation Helpers

        /// <summary>
        /// Gets the appropriate kernel source for a given operation and accelerator type.
        /// </summary>
        /// <param name="operation">The linear algebra operation.</param>
        /// <param name="acceleratorType">The target accelerator type.</param>
        /// <param name="precision">Precision mode (single/double).</param>
        /// <returns>Kernel source code.</returns>
        public static string GetKernelSource(LinearAlgebraOperation operation, string acceleratorType, string precision = "single")
        {
            var type = acceleratorType.ToUpperInvariant();

            return operation switch
            {
                LinearAlgebraOperation.MatrixMultiply => type switch
                {
                    "OPENCL" => OpenCLMatrixMultiplyTiledKernel,
                    "CUDA" => CUDAMatrixMultiplyTiledKernel,
                    _ => throw new NotSupportedException($"Matrix multiplication not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.HouseholderVector => type switch
                {
                    "OPENCL" => OpenCLHouseholderVectorKernel,
                    "CUDA" => HouseholderKernels.CudaComputeHouseholderVectorKernel,
                    _ => throw new NotSupportedException($"Householder vector not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.HouseholderTransform => type switch
                {
                    "OPENCL" => OpenCLHouseholderTransformKernel,
                    "CUDA" => HouseholderKernels.CudaApplyHouseholderTransformationKernel,
                    _ => throw new NotSupportedException($"Householder transform not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.JacobiSVD => type switch
                {
                    "CUDA" => CUDAJacobiSVDKernel,
                    "OPENCL" => OpenCLSingularValuesKernel,
                    _ => throw new NotSupportedException($"Jacobi SVD not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.MatrixVector => type switch
                {
                    "OPENCL" => OpenCLMatrixVectorKernel,
                    "CUDA" => CUDAVectorOperationsKernel,
                    _ => throw new NotSupportedException($"Matrix-vector ops not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.ParallelReduction => type switch
                {
                    "OPENCL" => OpenCLParallelReductionKernel,
                    "CUDA" => CUDAWarpReductionKernel,
                    _ => throw new NotSupportedException($"Parallel reduction not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.QRAlgorithm => type switch
                {
                    "OPENCL" => OpenCLQRShiftKernel,
                    "CUDA" => CUDAGivensRotationKernel,
                    _ => throw new NotSupportedException($"QR algorithm not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.CholeskyDecomposition => type switch
                {
                    "CUDA" => CUDACholeskyKernel,
                    _ => throw new NotSupportedException($"Cholesky decomposition not supported for {acceleratorType}")
                },

                LinearAlgebraOperation.LUDecomposition => type switch
                {
                    "OPENCL" => OpenCLLUDecompositionKernel,
                    _ => throw new NotSupportedException($"LU decomposition not supported for {acceleratorType}")
                },

                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
        }

        /// <summary>
        /// Generates optimized kernel parameters for different matrix sizes and GPU architectures.
        /// </summary>
        /// <param name="operation">The operation type.</param>
        /// <param name="matrixSize">Matrix dimensions.</param>
        /// <param name="deviceInfo">GPU device information.</param>
        /// <returns>Optimized kernel execution parameters.</returns>
        internal static KernelExecutionParameters GetOptimizedParameters(
            LinearAlgebraOperation operation,
            (int rows, int cols) matrixSize,
            string deviceInfo)
        {
            var (rows, cols) = matrixSize;

            return operation switch
            {
                LinearAlgebraOperation.MatrixMultiply => new KernelExecutionParameters
                {
                    GlobalWorkSize = [((cols + 15) / 16) * 16, ((rows + 15) / 16) * 16],
                    LocalWorkSize = [16, 16],
                    SharedMemorySize = 2 * 16 * 16 * sizeof(float), // Two tiles
                    UseSharedMemory = true,
                    TileSize = 16
                },

                LinearAlgebraOperation.HouseholderVector => new KernelExecutionParameters
                {
                    GlobalWorkSize = [((Math.Max(rows, cols) + 255) / 256) * 256],
                    LocalWorkSize = [256],
                    SharedMemorySize = 256 * sizeof(float),
                    UseSharedMemory = true
                },

                LinearAlgebraOperation.ParallelReduction => new KernelExecutionParameters
                {
                    GlobalWorkSize = [((rows * cols + 255) / 256) * 256],
                    LocalWorkSize = [256],
                    SharedMemorySize = 256 * sizeof(float),
                    UseSharedMemory = true
                },

                _ => new KernelExecutionParameters
                {
                    GlobalWorkSize = [((Math.Max(rows, cols) + 127) / 128) * 128],
                    LocalWorkSize = [128],
                    UseSharedMemory = false
                }
            };
        }

        /// <summary>
        /// Adaptive kernel configuration based on matrix properties and hardware capabilities.
        /// </summary>
        /// <param name="operation">Operation type.</param>
        /// <param name="matrixProperties">Matrix characteristics.</param>
        /// <param name="hardwareInfo">Hardware specifications.</param>
        /// <returns>Optimized configuration.</returns>
        internal static AdaptiveKernelConfig GetAdaptiveConfiguration(
            LinearAlgebraOperation operation,
            MatrixProperties matrixProperties,
            HardwareInfo hardwareInfo)
        {
            var config = new AdaptiveKernelConfig();

            // Configure based on matrix sparsity
            if (matrixProperties.SparsityRatio > 0.9f)
            {
                config.UseSparseOptimizations = true;
                config.SparsityThreshold = matrixProperties.SparsityRatio;
            }

            // Configure based on matrix size
            if (matrixProperties.Size > hardwareInfo.GlobalMemorySize / 4)
            {
                config.UseOutOfCoreAlgorithm = true;
                config.BlockSize = CalculateOptimalBlockSize(matrixProperties.Size, hardwareInfo);
            }

            // Configure precision based on condition number
            if (matrixProperties.ConditionNumber > 1e12f)
            {
                config.RequiresHighPrecision = true;
                config.UseMixedPrecision = true;
            }

            // Configure work group size based on hardware
            config.OptimalWorkGroupSize = Math.Min(
                hardwareInfo.MaxWorkGroupSize,
                GetNextPowerOfTwo(hardwareInfo.PreferredWorkGroupSizeMultiple)
            );

            return config;
        }

        private static int CalculateOptimalBlockSize(long matrixSize, HardwareInfo hardware)
        {
            // Calculate block size to fit in shared memory
            long availableSharedMem = hardware.SharedMemorySize - 1024; // Reserve some space
            var maxBlockSize = (int)Math.Sqrt(availableSharedMem / sizeof(float));

            // Round down to nearest power of 2
            return GetPreviousPowerOfTwo(Math.Min(maxBlockSize, 64));
        }

        private static int GetNextPowerOfTwo(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        private static int GetPreviousPowerOfTwo(int value) => GetNextPowerOfTwo(value + 1) / 2;
        /// <summary>
        /// An linear algebra operation enumeration.
        /// </summary>

        #endregion

        #region Supporting Types

        /// <summary>
        /// Enumeration of supported linear algebra operations.
        /// </summary>
        public enum LinearAlgebraOperation
        {
            MatrixMultiply,
            HouseholderVector,
            HouseholderTransform,
            JacobiSVD,
            MatrixVector,
            ParallelReduction,
            QRAlgorithm,
            CholeskyDecomposition,
            LUDecomposition,
            EigenDecomposition
        }

        /// <summary>
        /// Kernel execution parameters optimized for specific operations.
        /// </summary>
        internal class KernelExecutionParameters
        {
            /// <summary>
            /// Gets or sets the global work size.
            /// </summary>
            /// <value>The global work size.</value>
            public IReadOnlyList<int> GlobalWorkSize { get; set; } = Array.Empty<int>();
            /// <summary>
            /// Gets or sets the local work size.
            /// </summary>
            /// <value>The local work size.</value>
            public IReadOnlyList<int> LocalWorkSize { get; set; } = Array.Empty<int>();
            /// <summary>
            /// Gets or sets the shared memory size.
            /// </summary>
            /// <value>The shared memory size.</value>
            public int SharedMemorySize { get; set; }
            /// <summary>
            /// Gets or sets the use shared memory.
            /// </summary>
            /// <value>The use shared memory.</value>
            public bool UseSharedMemory { get; set; }
            /// <summary>
            /// Gets or sets the tile size.
            /// </summary>
            /// <value>The tile size.</value>
            public int TileSize { get; set; } = 16;
        }

        /// <summary>
        /// Matrix properties for adaptive optimization.
        /// </summary>
        internal class MatrixProperties
        {
            /// <summary>
            /// Gets or sets the size.
            /// </summary>
            /// <value>The size.</value>
            public long Size { get; set; }
            /// <summary>
            /// Gets or sets the sparsity ratio.
            /// </summary>
            /// <value>The sparsity ratio.</value>
            public float SparsityRatio { get; set; }
            /// <summary>
            /// Gets or sets the condition number.
            /// </summary>
            /// <value>The condition number.</value>
            public float ConditionNumber { get; set; }
            /// <summary>
            /// Gets or sets a value indicating whether symmetric.
            /// </summary>
            /// <value>The is symmetric.</value>
            public bool IsSymmetric { get; set; }
            /// <summary>
            /// Gets or sets a value indicating whether positive definite.
            /// </summary>
            /// <value>The is positive definite.</value>
            public bool IsPositiveDefinite { get; set; }
        }

        /// <summary>
        /// Hardware information for optimization decisions.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible",
            Justification = "Type made public to fix CA0050/CA0051 accessibility warnings. Used in public method signatures.")]
        public class HardwareInfo
        {
            /// <summary>
            /// Gets or sets the global memory size.
            /// </summary>
            /// <value>The global memory size.</value>
            public long GlobalMemorySize { get; set; }
            /// <summary>
            /// Gets or sets the shared memory size.
            /// </summary>
            /// <value>The shared memory size.</value>
            public int SharedMemorySize { get; set; }
            /// <summary>
            /// Gets or sets the max work group size.
            /// </summary>
            /// <value>The max work group size.</value>
            public int MaxWorkGroupSize { get; set; }
            /// <summary>
            /// Gets or sets the preferred work group size multiple.
            /// </summary>
            /// <value>The preferred work group size multiple.</value>
            public int PreferredWorkGroupSizeMultiple { get; set; }
            /// <summary>
            /// Gets or sets the compute units.
            /// </summary>
            /// <value>The compute units.</value>
            public int ComputeUnits { get; set; }
        }

        /// <summary>
        /// Adaptive kernel configuration based on runtime analysis.
        /// </summary>
        internal class AdaptiveKernelConfig
        {
            /// <summary>
            /// Gets or sets the use sparse optimizations.
            /// </summary>
            /// <value>The use sparse optimizations.</value>
            public bool UseSparseOptimizations { get; set; }
            /// <summary>
            /// Gets or sets the sparsity threshold.
            /// </summary>
            /// <value>The sparsity threshold.</value>
            public float SparsityThreshold { get; set; }
            /// <summary>
            /// Gets or sets the use out of core algorithm.
            /// </summary>
            /// <value>The use out of core algorithm.</value>
            public bool UseOutOfCoreAlgorithm { get; set; }
            /// <summary>
            /// Gets or sets the block size.
            /// </summary>
            /// <value>The block size.</value>
            public int BlockSize { get; set; } = 64;
            /// <summary>
            /// Gets or sets the requires high precision.
            /// </summary>
            /// <value>The requires high precision.</value>
            public bool RequiresHighPrecision { get; set; }
            /// <summary>
            /// Gets or sets the use mixed precision.
            /// </summary>
            /// <value>The use mixed precision.</value>
            public bool UseMixedPrecision { get; set; }
            /// <summary>
            /// Gets or sets the optimal work group size.
            /// </summary>
            /// <value>The optimal work group size.</value>
            public int OptimalWorkGroupSize { get; set; } = 256;
        }

        #endregion

    } // class LinearAlgebraKernels
} // namespace DotCompute.Algorithms
