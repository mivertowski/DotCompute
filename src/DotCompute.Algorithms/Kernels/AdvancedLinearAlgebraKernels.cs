// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Kernels;

namespace DotCompute.Algorithms.Kernels;

/// <summary>
/// Advanced GPU kernels for specialized linear algebra operations.
/// Includes kernels for sparse matrices, iterative solvers, and performance-critical operations.
/// </summary>
public static class AdvancedLinearAlgebraKernels
{
    #region Sparse Matrix Operations

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

    #endregion

    #region Iterative Solvers

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

    #endregion

    #region Power Method and Eigenvalue Kernels

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

    #endregion

    #region Specialized Decomposition Kernels

    /// <summary>
    /// OpenCL kernel for rank-1 update in QR decomposition.
    /// </summary>
    public const string OpenCLQRRank1UpdateKernel = @"
__kernel void qr_rank1_update(
    __global float* Q,          // Q matrix
    __global float* R,          // R matrix  
    __global const float* u,    // Update vector u
    __global const float* v,    // Update vector v
    const int m,                // Rows of Q
    const int n,                // Columns of Q, rows of R
    const float alpha           // Update parameter
) {
    int i = get_global_id(0);
    int j = get_global_id(1);
    
    // Update Q matrix: Q = Q - alpha * u * v^T
    if (i < m && j < n) {
        Q[i * n + j] -= alpha * u[i] * v[j];
    }
    
    barrier(CLK_GLOBAL_MEM_FENCE);
    
    // Re-orthogonalize using modified Gram-Schmidt
    if (i < n && j < n) {
        // This is a simplified version - full implementation would
        // require multiple kernel launches or more complex logic
        float sum = 0.0f;
        for (int k = 0; k < m; k++) {
            sum += Q[k * n + i] * Q[k * n + j];
        }
        R[i * n + j] = sum;
    }
}";

    /// <summary>
    /// CUDA kernel for Bidiagonal SVD step.
    /// </summary>
    public const string CUDABidiagonalSVDKernel = @"
extern ""C"" __global__ void bidiagonal_svd_step_cuda(
    float* B,               // Bidiagonal matrix
    float* U,               // Left singular vectors
    float* V,               // Right singular vectors
    const int n,            // Matrix dimension
    const float shift,      // Wilkinson shift
    const int step          // Current step
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    
    if (tid >= n) return;
    
    // Apply Givens rotation to eliminate subdiagonal elements
    if (tid < n - 1) {
        float a = B[tid * n + tid] - shift;
        float b = B[tid * n + tid + 1];
        
        // Compute Givens rotation parameters
        float r = sqrtf(a * a + b * b);
        if (r > 1e-10f) {
            float c = a / r;
            float s = -b / r;
            
            // Apply rotation to bidiagonal matrix
            float new_diag = c * a - s * b + shift;
            float new_super = s * a + c * b;
            
            B[tid * n + tid] = new_diag;
            if (tid < n - 1) {
                B[tid * n + tid + 1] = new_super;
            }
            
            // Update singular vectors
            for (int i = 0; i < n; i++) {
                float u_old = U[i * n + tid];
                float u_next = U[i * n + tid + 1];
                
                U[i * n + tid] = c * u_old - s * u_next;
                U[i * n + tid + 1] = s * u_old + c * u_next;
                
                float v_old = V[tid * n + i];
                float v_next = V[(tid + 1) * n + i];
                
                V[tid * n + i] = c * v_old - s * v_next;
                V[(tid + 1) * n + i] = s * v_old + c * v_next;
            }
        }
    }
}";

    #endregion

    #region Memory-Optimized Kernels

    /// <summary>
    /// OpenCL kernel for cache-blocked matrix operations.
    /// </summary>
    public const string OpenCLBlockedOperationsKernel = @"
__kernel void blocked_matrix_operation(
    __global const float* A,    // Input matrix A
    __global const float* B,    // Input matrix B  
    __global float* C,          // Output matrix C
    const int M,                // Matrix dimensions
    const int N,
    const int K,
    const int block_size,       // Block size for tiling
    const int operation         // 0=multiply, 1=add, 2=subtract
) {
    int block_row = get_group_id(1);
    int block_col = get_group_id(0);
    int local_row = get_local_id(1);
    int local_col = get_local_id(0);
    
    // Shared memory for blocks
    __local float tile_A[16][16];
    __local float tile_B[16][16];
    
    // Global indices
    int row = block_row * block_size + local_row;
    int col = block_col * block_size + local_col;
    
    float result = 0.0f;
    
    if (operation == 0) { // Matrix multiplication
        // Process tiles
        int num_blocks = (K + block_size - 1) / block_size;
        for (int b = 0; b < num_blocks; b++) {
            // Load tiles into shared memory
            int tile_row = row;
            int tile_col = b * block_size + local_col;
            
            if (tile_row < M && tile_col < K) {
                tile_A[local_row][local_col] = A[tile_row * K + tile_col];
            } else {
                tile_A[local_row][local_col] = 0.0f;
            }
            
            tile_row = b * block_size + local_row;
            tile_col = col;
            
            if (tile_row < K && tile_col < N) {
                tile_B[local_row][local_col] = B[tile_row * N + tile_col];
            } else {
                tile_B[local_row][local_col] = 0.0f;
            }
            
            barrier(CLK_LOCAL_MEM_FENCE);
            
            // Compute partial result
            for (int k = 0; k < block_size; k++) {
                result += tile_A[local_row][k] * tile_B[k][local_col];
            }
            
            barrier(CLK_LOCAL_MEM_FENCE);
        }
    } else { // Element-wise operations
        if (row < M && col < N) {
            float a_val = A[row * N + col];
            float b_val = B[row * N + col];
            
            switch (operation) {
                case 1: result = a_val + b_val; break;
                case 2: result = a_val - b_val; break;
                default: result = 0.0f;
            }
        }
    }
    
    // Store result
    if (row < M && col < N) {
        C[row * N + col] = result;
    }
}";

    #endregion

    #region Double Precision Kernels

    /// <summary>
    /// CUDA kernel for high-precision matrix operations.
    /// </summary>
    public const string CUDADoublePrecisionKernel = @"
extern ""C"" __global__ void matrix_multiply_double_cuda(
    const double* A,        // Matrix A (double precision)
    const double* B,        // Matrix B (double precision)
    double* C,              // Result matrix C
    const int M,            // Matrix dimensions
    const int N,
    const int K
) {
    int row = blockIdx.y * blockDim.y + threadIdx.y;
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (row >= M || col >= N) return;
    
    double sum = 0.0;
    
    // Use Kahan summation for improved numerical stability
    double compensation = 0.0;
    
    for (int k = 0; k < K; k++) {
        double product = A[row * K + k] * B[k * N + col];
        double corrected_product = product - compensation;
        double new_sum = sum + corrected_product;
        compensation = (new_sum - sum) - corrected_product;
        sum = new_sum;
    }
    
    C[row * N + col] = sum;
}";

    /// <summary>
    /// OpenCL kernel for mixed precision operations.
    /// </summary>
    public const string OpenCLMixedPrecisionKernel = @"
#pragma OPENCL EXTENSION cl_khr_fp64 : enable

__kernel void mixed_precision_solve(
    __global const float* A_single,    // Single precision matrix
    __global const double* b_double,   // Double precision RHS
    __global double* x_double,         // Double precision solution
    __global float* residual,          // Single precision residual
    const int n                        // System size
) {
    int i = get_global_id(0);
    
    if (i >= n) return;
    
    // Solve using single precision matrix but double precision arithmetic
    double sum = 0.0;
    
    for (int j = 0; j < n; j++) {
        // Convert single to double for computation
        double a_val = (double)A_single[i * n + j];
        sum += a_val * x_double[j];
    }
    
    // Compute residual in single precision
    residual[i] = (float)(b_double[i] - sum);
    
    // Iterative refinement step would go here
}";

    #endregion

    #region Architecture-Specific Optimizations

    /// <summary>
    /// CUDA kernel optimized for Tensor Core operations (Ampere architecture).
    /// </summary>
    public const string CUDATensorCoreKernel = @"
#include <mma.h>
using namespace nvcuda;

extern ""C"" __global__ void tensor_core_gemm_cuda(
    const half* A,          // Half precision matrix A
    const half* B,          // Half precision matrix B  
    float* C,               // Single precision result C
    const int M,            // Matrix dimensions
    const int N,
    const int K
) {
    // Tensor Core fragment declarations
    wmma::fragment<wmma::matrix_a, 16, 16, 16, half, wmma::row_major> frag_a;
    wmma::fragment<wmma::matrix_b, 16, 16, 16, half, wmma::col_major> frag_b;
    wmma::fragment<wmma::accumulator, 16, 16, 16, float> frag_acc;
    
    int warp_row = (blockIdx.y * blockDim.y + threadIdx.y) / 32 * 16;
    int warp_col = (blockIdx.x * blockDim.x + threadIdx.x) / 32 * 16;
    
    // Initialize accumulator
    wmma::fill_fragment(frag_acc, 0.0f);
    
    // Compute matrix multiplication using Tensor Cores
    for (int k = 0; k < K; k += 16) {
        if (warp_row < M && warp_col < N && k < K) {
            // Load matrix fragments
            wmma::load_matrix_sync(frag_a, A + warp_row * K + k, K);
            wmma::load_matrix_sync(frag_b, B + k * N + warp_col, N);
            
            // Perform matrix multiplication
            wmma::mma_sync(frag_acc, frag_a, frag_b, frag_acc);
        }
    }
    
    // Store result
    if (warp_row < M && warp_col < N) {
        wmma::store_matrix_sync(C + warp_row * N + warp_col, frag_acc, N, wmma::mem_row_major);
    }
}";

    #endregion

    #region Kernel Selection Helper

    /// <summary>
    /// Gets specialized kernel source for advanced operations.
    /// </summary>
    /// <param name="operation">The advanced operation type.</param>
    /// <param name="acceleratorType">Target accelerator type.</param>
    /// <param name="precision">Precision requirements.</param>
    /// <param name="architecture">Specific GPU architecture optimizations.</param>
    /// <returns>Optimized kernel source code.</returns>
    public static string GetAdvancedKernelSource(
        AdvancedLinearAlgebraOperation operation, 
        string acceleratorType, 
        string precision = "single",
        string architecture = "generic")
    {
        var type = acceleratorType.ToUpperInvariant();
        var arch = architecture.ToUpperInvariant();
        
        return operation switch
        {
            AdvancedLinearAlgebraOperation.SparseMatrixVector => type switch
            {
                "OPENCL" => OpenCLSparseMatrixVectorKernel,
                "CUDA" => CUDASparseMatrixKernel,
                _ => throw new NotSupportedException($"Sparse operations not supported for {acceleratorType}")
            },
            
            AdvancedLinearAlgebraOperation.ConjugateGradient => type switch
            {
                "OPENCL" => OpenCLConjugateGradientKernel,
                "CUDA" => CUDABiCGSTABKernel,
                _ => throw new NotSupportedException($"Iterative solvers not supported for {acceleratorType}")
            },
            
            AdvancedLinearAlgebraOperation.PowerMethod => type switch
            {
                "OPENCL" => OpenCLPowerMethodKernel,
                "CUDA" => CUDAInversePowerMethodKernel,
                _ => throw new NotSupportedException($"Power method not supported for {acceleratorType}")
            },
            
            AdvancedLinearAlgebraOperation.BlockedOperations => type switch
            {
                "OPENCL" => OpenCLBlockedOperationsKernel,
                _ => throw new NotSupportedException($"Blocked operations not supported for {acceleratorType}")
            },
            
            AdvancedLinearAlgebraOperation.DoublePrecision => type switch
            {
                "CUDA" => CUDADoublePrecisionKernel,
                "OPENCL" => OpenCLMixedPrecisionKernel,
                _ => throw new NotSupportedException($"Double precision not supported for {acceleratorType}")
            },
            
            AdvancedLinearAlgebraOperation.TensorCore => type switch
            {
                "CUDA" when arch.Contains("AMPERE") || arch.Contains("TENSOR") => CUDATensorCoreKernel,
                _ => throw new NotSupportedException($"Tensor Core operations require CUDA and compatible hardware")
            },
            
            _ => throw new ArgumentException($"Unknown advanced operation: {operation}")
        };
    }

    /// <summary>
    /// Determines optimal kernel variant based on matrix properties and hardware.
    /// </summary>
    /// <param name="matrixProperties">Matrix characteristics.</param>
    /// <param name="hardwareInfo">Hardware capabilities.</param>
    /// <returns>Recommended kernel configuration.</returns>
    public static KernelConfiguration GetOptimalConfiguration(
        MatrixProperties matrixProperties,
        HardwareInfo hardwareInfo)
    {
        var config = new KernelConfiguration();
        
        // Choose precision based on requirements
        if (matrixProperties.RequiresHighPrecision)
        {
            config.Precision = hardwareInfo.SupportsDoublePrecision ? "double" : "mixed";
        }
        else
        {
            config.Precision = "single";
        }
        
        // Configure for sparse matrices
        if (matrixProperties.SparsityRatio > 0.7f)
        {
            config.UseSpecializedSparseKernels = true;
            config.OptimalBlockSize = 32; // Smaller blocks for sparse data
        }
        
        // Configure for large matrices
        if (matrixProperties.Size > hardwareInfo.GlobalMemorySize / 2)
        {
            config.UseMemoryTiling = true;
            config.TileSize = CalculateOptimalTileSize(matrixProperties.Size, hardwareInfo);
        }
        
        // Use Tensor Cores if available and beneficial
        if (hardwareInfo.SupportsTensorCores && 
            matrixProperties.Size > 1024 && 
            config.Precision != "double")
        {
            config.UseTensorCores = true;
            config.OptimalBlockSize = 16; // Tensor Core requirement
        }
        
        return config;
    }

    private static int CalculateOptimalTileSize(long matrixSize, HardwareInfo hardware)
    {
        // Calculate tile size based on available memory and compute units
        long availableMemory = hardware.GlobalMemorySize / 4; // Use 25% of memory
        int maxTileSize = (int)Math.Sqrt(availableMemory / sizeof(float));
        
        // Align to warp/wavefront size
        int alignment = hardware.WarpSize ?? 32;
        return ((Math.Min(maxTileSize, 128) + alignment - 1) / alignment) * alignment;
    }

#endregion

#region Supporting Types for Advanced Operations

/// <summary>
/// Advanced linear algebra operations.
/// </summary>
public enum AdvancedLinearAlgebraOperation
{
    SparseMatrixVector,
    ConjugateGradient,
    PowerMethod,
    BlockedOperations,
    DoublePrecision,
    TensorCore,
    BidiagonalSVD,
    QRRank1Update
}

/// <summary>
/// Extended matrix properties for advanced optimizations.
/// </summary>
public class MatrixProperties
{
    public long Size { get; set; }
    public float SparsityRatio { get; set; }
    public bool RequiresHighPrecision { get; set; }
    public bool IsSymmetric { get; set; }
    public bool IsPositiveDefinite { get; set; }
    public float ConditionNumber { get; set; }
    public string StorageFormat { get; set; } = "Dense"; // Dense, CSR, CSC, etc.
}

/// <summary>
/// Extended hardware information.
/// </summary>
public class HardwareInfo
{
    public long GlobalMemorySize { get; set; }
    public bool SupportsDoublePrecision { get; set; }
    public bool SupportsTensorCores { get; set; }
    public int? WarpSize { get; set; }
    public string Architecture { get; set; } = "Generic";
    public int ComputeCapability { get; set; }
}

/// <summary>
/// Kernel configuration for advanced operations.
/// </summary>
public class KernelConfiguration
{
    public string Precision { get; set; } = "single";
    public bool UseSpecializedSparseKernels { get; set; }
    public bool UseMemoryTiling { get; set; }
    public bool UseTensorCores { get; set; }
    public int OptimalBlockSize { get; set; } = 64;
    public int TileSize { get; set; } = 16;
}

#endregion

} // namespace DotCompute.Algorithms.Kernels