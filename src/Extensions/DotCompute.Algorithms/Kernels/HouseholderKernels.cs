
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Kernels;


/// <summary>
/// GPU kernel templates for Householder transformations used in QR decomposition.
/// These kernels provide GPU-accelerated matrix operations for advanced linear algebra.
/// </summary>
public static class HouseholderKernels
{
    /// <summary>
    /// The compute householder vector kernel.
    /// </summary>
    /// <summary>
    /// OpenCL kernel for computing Householder vector.
    /// </summary>
    public const string ComputeHouseholderVectorKernel = @"
// Compute Householder vector for QR decomposition
__kernel void compute_householder_vector(
    __global const float* column,     // Input column vector
    __global float* householder,      // Output Householder vector
    const int n,                      // Vector length
    const int start_idx               // Starting index
) {
    int gid = get_global_id(0);

    if (gid >= n - start_idx) return;

    // Copy column data
    float val = column[start_idx + gid];
    householder[gid] = val;

    // Compute norm (reduction needed)
    __local float local_norm[256];
    local_norm[get_local_id(0)] = val * val;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Reduction to compute norm
    for (int offset = get_local_size(0) / 2; offset > 0; offset /= 2) {
        if (get_local_id(0) < offset) {
            local_norm[get_local_id(0)] += local_norm[get_local_id(0) + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    float norm = sqrt(local_norm[0]);

    // Update first element with sign
    if (gid == 0) {
        float sign = householder[0] >= 0.0f ? 1.0f : -1.0f;
        householder[0] += sign * norm;
    }

    barrier(CLK_GLOBAL_MEM_FENCE);

    // Normalize vector
    float vnorm = 0.0f;
    for (int i = 0; i < n - start_idx; i++) {
        vnorm += householder[i] * householder[i];
    }
    vnorm = sqrt(vnorm);

    if (vnorm > 1e-10f) {
        householder[gid] /= vnorm;
    }
}";
    /// <summary>
    /// The apply householder transformation kernel.
    /// </summary>

    /// <summary>
    /// OpenCL kernel for applying Householder transformation to matrix.
    /// </summary>
    public const string ApplyHouseholderTransformationKernel = @"
// Apply Householder transformation H = I - 2*v*v^T to matrix A
__kernel void apply_householder_left(
    __global float* matrix,           // Input/output matrix (modified in place)
    __global const float* v,          // Householder vector
    const int m,                      // Matrix rows
    const int n,                      // Matrix columns
    const int v_len,                  // Householder vector length
    const int start_row               // Starting row index
) {
    int col = get_global_id(0);      // Column index
    int row_group = get_global_id(1); // Row group index

    if (col >= n) return;

    // Compute v^T * A[:, col] for this column
    __local float dot_products[256];
    int lid = get_local_id(1);

    float dot = 0.0f;
    for (int i = lid; i < v_len; i += get_local_size(1)) {
        if (start_row + i < m) {
            dot += v[i] * matrix[(start_row + i) * n + col];
        }
    }

    dot_products[lid] = dot;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Reduction to compute full dot product
    for (int offset = get_local_size(1) / 2; offset > 0; offset /= 2) {
        if (lid < offset) {
            dot_products[lid] += dot_products[lid + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    float total_dot = dot_products[0] * 2.0f;

    // Apply transformation: A[:, col] = A[:, col] - 2 * (v^T * A[:, col]) * v
    for (int i = lid; i < v_len; i += get_local_size(1)) {
        if (start_row + i < m) {
            matrix[(start_row + i) * n + col] -= total_dot * v[i];
        }
    }
}";
    /// <summary>
    /// The cuda compute householder vector kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for computing Householder vector.
    /// </summary>
    public const string CudaComputeHouseholderVectorKernel = @"
extern ""C"" __global__ void compute_householder_vector_cuda(
    const float* column,      // Input column vector
    float* householder,       // Output Householder vector
    const int n,              // Vector length
    const int start_idx       // Starting index
) {
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    if (tid >= n - start_idx) return;

    // Copy column data
    float val = column[start_idx + tid];
    householder[tid] = val;

    __syncthreads();

    // Compute norm using shared memory reduction
    __shared__ float shared_norm[1024];
    shared_norm[threadIdx.x] = val * val;
    __syncthreads();

    // Reduction
    for (int offset = blockDim.x / 2; offset > 0; offset >>= 1) {
        if (threadIdx.x < offset) {
            shared_norm[threadIdx.x] += shared_norm[threadIdx.x + offset];
        }
        __syncthreads();
    }

    float norm = sqrtf(shared_norm[0]);

    // Update first element with sign
    if (tid == 0) {
        float sign = householder[0] >= 0.0f ? 1.0f : -1.0f;
        householder[0] += sign * norm;
    }

    __syncthreads();

    // Compute vector norm for normalization
    shared_norm[threadIdx.x] = householder[tid] * householder[tid];
    __syncthreads();

    // Reduction for normalization
    for (int offset = blockDim.x / 2; offset > 0; offset >>= 1) {
        if (threadIdx.x < offset) {
            shared_norm[threadIdx.x] += shared_norm[threadIdx.x + offset];
        }
        __syncthreads();
    }

    float vnorm = sqrtf(shared_norm[0]);

    // Normalize
    if (vnorm > 1e-10f) {
        householder[tid] /= vnorm;
    }
}";
    /// <summary>
    /// The cuda apply householder transformation kernel.
    /// </summary>

    /// <summary>
    /// CUDA kernel for applying Householder transformation.
    /// </summary>
    public const string CudaApplyHouseholderTransformationKernel = @"
extern ""C"" __global__ void apply_householder_left_cuda(
    float* matrix,            // Input/output matrix
    const float* v,           // Householder vector
    const int m,              // Matrix rows
    const int n,              // Matrix columns
    const int v_len,          // Vector length
    const int start_row       // Starting row
) {
    int col = blockIdx.x * blockDim.x + threadIdx.x;
    int row = blockIdx.y * blockDim.y + threadIdx.y;

    if (col >= n || row >= v_len) return;

    __shared__ float shared_dot[32][32];

    // Compute dot product v^T * A[:, col]
    float dot = 0.0f;
    if (start_row + row < m) {
        dot = v[row] * matrix[(start_row + row) * n + col];
    }

    shared_dot[threadIdx.y][threadIdx.x] = dot;
    __syncthreads();

    // Reduce along rows (within each column)
    for (int offset = blockDim.y / 2; offset > 0; offset >>= 1) {
        if (threadIdx.y < offset) {
            shared_dot[threadIdx.y][threadIdx.x] +=
                shared_dot[threadIdx.y + offset][threadIdx.x];
        }
        __syncthreads();
    }

    float total_dot = shared_dot[0][threadIdx.x] * 2.0f;

    // Apply transformation
    if (start_row + row < m) {
        matrix[(start_row + row) * n + col] -= total_dot * v[row];
    }
}";
    /// <summary>
    /// The h l s l householder kernel.
    /// </summary>

    /// <summary>
    /// DirectCompute/HLSL kernel for Householder transformations.
    /// </summary>
    public const string HLSLHouseholderKernel = @"
// HLSL Compute Shader for Householder transformations
[numthreads(16, 16, 1)]
void CSHouseholderTransform(
    uint3 id : SV_DispatchThreadID,
    uint3 gid : SV_GroupID,
    uint3 gtid : SV_GroupThreadID
) {
    uint col = id.x;
    uint row = id.y;

    if (col >= MatrixCols || row >= VectorLength) return;

    // Shared memory for reduction
    groupshared float shared_data[16][16];

    // Load data and compute dot product
    float dot = 0.0f;
    if (StartRow + row < MatrixRows) {
        dot = HouseholderVector[row] * InputMatrix[StartRow + row][col];
    }

    shared_data[gtid.y][gtid.x] = dot;
    GroupMemoryBarrierWithGroupSync();

    // Reduction
    [unroll]
    for (uint offset = 8; offset > 0; offset >>= 1) {
        if (gtid.y < offset) {
            shared_data[gtid.y][gtid.x] += shared_data[gtid.y + offset][gtid.x];
        }
        GroupMemoryBarrierWithGroupSync();
    }

    float total_dot = shared_data[0][gtid.x] * 2.0f;

    // Apply transformation
    if (StartRow + row < MatrixRows) {
        OutputMatrix[StartRow + row][col] =
            InputMatrix[StartRow + row][col] - total_dot * HouseholderVector[row];
    }
}";
    /// <summary>
    /// The metal householder kernel.
    /// </summary>

    /// <summary>
    /// Metal kernel for Householder transformations (Apple Silicon).
    /// </summary>
    public const string MetalHouseholderKernel = @"
#include <metal_stdlib>
using namespace metal;

// Metal kernel for computing Householder vector
kernel void compute_householder_vector_metal(
    const device float* column [[buffer(0)]],
    device float* householder [[buffer(1)]],
    constant int& n [[buffer(2)]],
    constant int& start_idx [[buffer(3)]],
    uint tid [[thread_position_in_grid]],
    uint lid [[thread_position_in_threadgroup]],
    threadgroup float* shared_norm [[threadgroup(0)]]
) {
    if (tid >= n - start_idx) return;

    // Copy and compute norm
    float val = column[start_idx + tid];
    householder[tid] = val;
    shared_norm[lid] = val * val;

    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Reduction
    for (uint offset = 128; offset > 0; offset >>= 1) {
        if (lid < offset && lid + offset < 256) {
            shared_norm[lid] += shared_norm[lid + offset];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    float norm = sqrt(shared_norm[0]);

    // Update first element
    if (tid == 0) {
        float sign = householder[0] >= 0.0f ? 1.0f : -1.0f;
        householder[0] += sign * norm;
    }

    threadgroup_barrier(mem_flags::mem_device);

    // Normalize
    shared_norm[lid] = householder[tid] * householder[tid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Reduction for normalization
    for (uint offset = 128; offset > 0; offset >>= 1) {
        if (lid < offset) {
            shared_norm[lid] += shared_norm[lid + offset];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    float vnorm = sqrt(shared_norm[0]);
    if (vnorm > 1e-10f) {
        householder[tid] /= vnorm;
    }
}";

    /// <summary>
    /// Gets kernel source code for the specified accelerator type.
    /// </summary>
    /// <param name="acceleratorType">Type of accelerator (OpenCL, CUDA, DirectCompute, Metal).</param>
    /// <param name="operation">Operation type (ComputeVector, ApplyTransformation).</param>
    /// <returns>Kernel source code string.</returns>
    public static string GetKernelSource(string acceleratorType, string operation)
    {
        return acceleratorType.ToUpperInvariant() switch
        {
            "OPENCL" => operation switch
            {
                "ComputeVector" => ComputeHouseholderVectorKernel,
                "ApplyTransformation" => ApplyHouseholderTransformationKernel,
                _ => throw new ArgumentException($"Unknown OpenCL operation: {operation}")
            },
            "CUDA" => operation switch
            {
                "ComputeVector" => CudaComputeHouseholderVectorKernel,
                "ApplyTransformation" => CudaApplyHouseholderTransformationKernel,
                _ => throw new ArgumentException($"Unknown CUDA operation: {operation}")
            },
            "DIRECTCOMPUTE" => HLSLHouseholderKernel,
            "METAL" => MetalHouseholderKernel,
            _ => throw new NotSupportedException($"Accelerator type {acceleratorType} not supported")
        };
    }
}
