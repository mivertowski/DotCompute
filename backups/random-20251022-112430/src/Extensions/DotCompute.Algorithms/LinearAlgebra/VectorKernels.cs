#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.LinearAlgebra;

/// <summary>
/// GPU kernels for vector operations and parallel reductions.
/// Provides optimized kernels for vector computations on GPU accelerators.
/// </summary>
public static class VectorKernels
{
    /// <summary>
    /// The c u d a vector operations kernel.
    /// </summary>
    #region Vector Operations

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
    /// The open c l vector scale kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for vector scaling and normalization operations.
    /// </summary>
    public const string OpenCLVectorScaleKernel = @"
__kernel void vector_scale_normalize(
    __global const float* input,     // Input vector
    __global float* output,          // Output vector
    const float scale_factor,        // Scaling factor
    const int n,                     // Vector length
    const int operation              // 0=scale, 1=normalize, 2=unit_scale
) {
    int gid = get_global_id(0);

    if (gid >= n) return;

    float val = input[gid];

    switch (operation) {
        case 0: // Simple scaling
            output[gid] = val * scale_factor;
            break;
        case 1: // Normalize by given factor (typically norm)
            output[gid] = (fabs(scale_factor) > 1e-10f) ? val / scale_factor : 0.0f;
            break;
        case 2: // Unit scaling (scale to range [0,1])
            output[gid] = val * scale_factor; // scale_factor = 1/(max-min)
            break;
        default:
            output[gid] = val;
            break;
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
    /// The open c l dot product kernel.
    /// </summary>

    #endregion

    #region Dot Product and Norms

    /// <summary>
    /// OpenCL kernel for optimized dot product computation.
    /// </summary>
    public const string OpenCLDotProductKernel = @"
__kernel void dot_product_optimized(
    __global const float* a,         // Vector a
    __global const float* b,         // Vector b
    __global float* partial_results, // Partial results (one per work group)
    __local float* scratch,          // Local memory
    const int n                      // Vector length
) {
    int gid = get_global_id(0);
    int lid = get_local_id(0);
    int group_size = get_local_size(0);

    // Initialize local sum
    float local_sum = 0.0f;

    // Grid-stride loop for better memory coalescing
    for (int i = gid; i < n; i += get_global_size(0)) {
        local_sum += a[i] * b[i];
    }

    // Store in local memory
    scratch[lid] = local_sum;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Parallel reduction within work group
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            scratch[lid] += scratch[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    // Store partial result
    if (lid == 0) {
        partial_results[get_group_id(0)] = scratch[0];
    }
}";
    /// <summary>
    /// The c u d a vector norm kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for vector norm computations.
    /// </summary>
    public const string CUDAVectorNormKernel = @"
extern ""C"" __global__ void vector_norm_cuda(
    const float* input,        // Input vector
    float* output,            // Output (partial results)
    const int n,              // Vector length
    const int norm_type       // 1=L1, 2=L2, 3=Linf
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    __shared__ float shared_data[256];

    float thread_result = 0.0f;

    // Grid-stride loop
    for (int i = tid; i < n; i += blockDim.x * gridDim.x) {
        float val = input[i];

        switch (norm_type) {
            case 1: // L1 norm
                thread_result += fabsf(val);
                break;
            case 2: // L2 norm
                thread_result += val * val;
                break;
            case 3: // L-infinity norm
                thread_result = fmaxf(thread_result, fabsf(val));
                break;
            default:
                thread_result += val * val;
                break;
        }
    }

    shared_data[threadIdx.x] = thread_result;
    __syncthreads();

    // Block reduction
    for (int stride = blockDim.x / 2; stride > 0; stride /= 2) {
        if (threadIdx.x < stride) {
            if (norm_type == 3) { // Max for L-inf norm
                shared_data[threadIdx.x] = fmaxf(shared_data[threadIdx.x], shared_data[threadIdx.x + stride]);
            } else { // Sum for other norms
                shared_data[threadIdx.x] += shared_data[threadIdx.x + stride];
            }
        }
        __syncthreads();
    }

    if (threadIdx.x == 0) {
        output[blockIdx.x] = shared_data[0];
    }
}";
    /// <summary>
    /// The open c l vector comparison kernel.
    /// </summary>

    #endregion

    #region Vector Utilities

    /// <summary>
    /// OpenCL kernel for vector comparison and selection operations.
    /// </summary>
    public const string OpenCLVectorComparisonKernel = @"
__kernel void vector_comparison_select(
    __global const float* a,         // Vector a
    __global const float* b,         // Vector b
    __global float* result,          // Result vector
    const int n,                     // Vector length
    const int operation,             // 0=max, 1=min, 2=greater, 3=less, 4=equal
    const float tolerance            // Tolerance for equality
) {
    int gid = get_global_id(0);

    if (gid >= n) return;

    float val_a = a[gid];
    float val_b = b[gid];

    switch (operation) {
        case 0: // Element-wise max
            result[gid] = fmax(val_a, val_b);
            break;
        case 1: // Element-wise min
            result[gid] = fmin(val_a, val_b);
            break;
        case 2: // Greater than (1.0 if a > b, 0.0 otherwise)
            result[gid] = (val_a > val_b) ? 1.0f : 0.0f;
            break;
        case 3: // Less than
            result[gid] = (val_a < val_b) ? 1.0f : 0.0f;
            break;
        case 4: // Equal (within tolerance)
            result[gid] = (fabs(val_a - val_b) <= tolerance) ? 1.0f : 0.0f;
            break;
        default:
            result[gid] = val_a;
            break;
    }
}";
    /// <summary>
    /// The c u d a vector transform kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for vector copy and transformation operations.
    /// </summary>
    public const string CUDAVectorTransformKernel = @"
extern ""C"" __global__ void vector_transform_cuda(
    const float* input,       // Input vector
    float* output,           // Output vector
    const int n,             // Vector length
    const float param1,      // Transform parameter 1
    const float param2,      // Transform parameter 2
    const int transform_type // 0=copy, 1=scale+shift, 2=clamp, 3=abs, 4=sign
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;
    int stride = blockDim.x * gridDim.x;

    for (int i = tid; i < n; i += stride) {
        float val = input[i];
        float result = val;

        switch (transform_type) {
            case 0: // Copy
                result = val;
                break;
            case 1: // Scale and shift: result = val * param1 + param2
                result = val * param1 + param2;
                break;
            case 2: // Clamp to [param1, param2]
                result = fminf(fmaxf(val, param1), param2);
                break;
            case 3: // Absolute value
                result = fabsf(val);
                break;
            case 4: // Sign function
                result = (val > 0.0f) ? 1.0f : ((val < 0.0f) ? -1.0f : 0.0f);
                break;
            default:
                result = val;
                break;
        }

        output[i] = result;
    }
}";

    #endregion
}