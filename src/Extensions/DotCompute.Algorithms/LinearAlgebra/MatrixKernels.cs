
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// GPU kernels for matrix operations including multiplication and transposition.
/// Provides optimized kernels for matrix computation on GPU accelerators.
/// </summary>
public static class MatrixKernels
{

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


    #endregion

    #region Matrix Transpose Operations

    /// <summary>
    /// OpenCL kernel for cache-optimized matrix transpose.
    /// </summary>
    public const string OpenCLMatrixTransposeKernel = @"
__kernel void matrix_transpose_optimized(
    __global const float* A,     // Input matrix (rows x cols)
    __global float* AT,          // Transposed matrix (cols x rows)
    const int rows,              // Input matrix rows
    const int cols               // Input matrix columns
) {
    const int TILE_SIZE = 16;
    __local float tile[16][16];

    int bx = get_group_id(0);
    int by = get_group_id(1);
    int tx = get_local_id(0);
    int ty = get_local_id(1);

    // Calculate global positions
    int x = bx * TILE_SIZE + tx;
    int y = by * TILE_SIZE + ty;

    // Load tile from A into shared memory
    if (y < rows && x < cols) {
        tile[ty][tx] = A[y * cols + x];
    } else {
        tile[ty][tx] = 0.0f;
    }

    barrier(CLK_LOCAL_MEM_FENCE);

    // Write transposed tile to AT
    int x_new = by * TILE_SIZE + tx;
    int y_new = bx * TILE_SIZE + ty;

    if (y_new < cols && x_new < rows) {
        AT[y_new * rows + x_new] = tile[tx][ty];
    }
}";
    /// <summary>
    /// The c u d a matrix transpose kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for cache-optimized matrix transpose.
    /// </summary>
    public const string CUDAMatrixTransposeKernel = @"
extern ""C"" __global__ void matrix_transpose_cuda(
    const float* A,     // Input matrix (rows x cols)
    float* AT,          // Transposed matrix (cols x rows)
    const int rows,     // Input matrix rows
    const int cols      // Input matrix columns
) {
    const int TILE_SIZE = 16;
    __shared__ float tile[16][16];

    int bx = blockIdx.x;
    int by = blockIdx.y;
    int tx = threadIdx.x;
    int ty = threadIdx.y;

    // Calculate global positions
    int x = bx * TILE_SIZE + tx;
    int y = by * TILE_SIZE + ty;

    // Load tile from A into shared memory
    if (y < rows && x < cols) {
        tile[ty][tx] = A[y * cols + x];
    }

    __syncthreads();

    // Write transposed tile to AT
    int x_new = by * TILE_SIZE + tx;
    int y_new = bx * TILE_SIZE + ty;

    if (y_new < cols && x_new < rows) {
        AT[y_new * rows + x_new] = tile[tx][ty];
    }
}";


    #endregion

    #region Matrix Utility Kernels

    /// <summary>
    /// OpenCL kernel for matrix element-wise operations.
    /// </summary>
    public const string OpenCLMatrixElementwiseKernel = @"
__kernel void matrix_elementwise_operation(
    __global const float* A,     // Input matrix A
    __global const float* B,     // Input matrix B (optional)
    __global float* C,           // Output matrix C
    const int rows,              // Matrix rows
    const int cols,              // Matrix columns
    const int operation          // 0=copy, 1=add, 2=sub, 3=mul, 4=div, 5=scale
) {
    int idx = get_global_id(0);
    int total_elements = rows * cols;

    if (idx >= total_elements) return;

    float a = A[idx];
    float b = (B != 0) ? B[idx] : 0.0f;

    switch (operation) {
        case 0: // Copy
            C[idx] = a;
            break;
        case 1: // Add
            C[idx] = a + b;
            break;
        case 2: // Subtract
            C[idx] = a - b;
            break;
        case 3: // Multiply
            C[idx] = a * b;
            break;
        case 4: // Divide
            C[idx] = (fabs(b) > 1e-10f) ? a / b : 0.0f;
            break;
        case 5: // Scale (B is scalar stored in B[0])
            C[idx] = a * b;
            break;
        default:
            C[idx] = a;
            break;
    }
}";
    /// <summary>
    /// The c u d a matrix norm kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for matrix norm computation.
    /// </summary>
    public const string CUDAMatrixNormKernel = @"
extern ""C"" __global__ void matrix_norm_cuda(
    const float* A,         // Input matrix
    float* norm_result,     // Output norm (one value per block)
    const int rows,         // Matrix rows
    const int cols,         // Matrix columns
    const int norm_type     // 1=L1, 2=L2, 3=Frobenius, 4=Infinity
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int total_elements = rows * cols;

    __shared__ float shared_data[256];

    float thread_sum = 0.0f;

    // Grid-stride loop to process elements
    for (int i = tid; i < total_elements; i += blockDim.x * gridDim.x) {
        float val = A[i];

        switch (norm_type) {
            case 1: // L1 norm
                thread_sum += fabsf(val);
                break;
            case 2: // L2 norm
            case 3: // Frobenius norm
                thread_sum += val * val;
                break;
            case 4: // Infinity norm
                thread_sum = fmaxf(thread_sum, fabsf(val));
                break;
            default:
                thread_sum += val * val;
                break;
        }
    }

    // Store in shared memory
    shared_data[threadIdx.x] = thread_sum;
    __syncthreads();

    // Reduction within block
    for (int stride = blockDim.x / 2; stride > 0; stride /= 2) {
        if (threadIdx.x < stride) {
            if (norm_type == 4) { // Max reduction for infinity norm
                shared_data[threadIdx.x] = fmaxf(shared_data[threadIdx.x], shared_data[threadIdx.x + stride]);
            } else { // Sum reduction
                shared_data[threadIdx.x] += shared_data[threadIdx.x + stride];
            }
        }
        __syncthreads();
    }

    // Store block result
    if (threadIdx.x == 0) {
        norm_result[blockIdx.x] = shared_data[0];
    }
}";

    #endregion
}
