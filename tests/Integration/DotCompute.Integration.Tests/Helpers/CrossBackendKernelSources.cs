// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;

#pragma warning disable CA1707 // Identifiers should not contain underscores (test helper class)

namespace DotCompute.Integration.Tests.Helpers;

/// <summary>
/// Provides kernel source code for cross-backend validation testing.
/// Each kernel is implemented in CUDA C, OpenCL C, Metal Shading Language, and C#.
/// </summary>
public static class CrossBackendKernelSources
{
    /// <summary>
    /// Vector addition: result[i] = a[i] + b[i]
    /// </summary>
    public static class VectorAdd
    {
        public const string CudaSource = @"
extern ""C"" __global__ void vectorAdd(const float* a, const float* b, float* result, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        result[idx] = a[idx] + b[idx];
    }
}";

        public const string OpenCLSource = @"
__kernel void vectorAdd(__global const float* a, __global const float* b, __global float* result, int length)
{
    int idx = get_global_id(0);
    if (idx < length) {
        result[idx] = a[idx] + b[idx];
    }
}";

        public const string MetalSource = @"
#include <metal_stdlib>
using namespace metal;

kernel void vectorAdd(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant int& length [[buffer(3)]],
    uint idx [[thread_position_in_grid]])
{
    if (idx < (uint)length) {
        result[idx] = a[idx] + b[idx];
    }
}";

        public static KernelDefinition GetDefinition(AcceleratorType type)
        {
            return type switch
            {
                AcceleratorType.CUDA => new KernelDefinition("vectorAdd", CudaSource, "vectorAdd") { Language = KernelLanguage.Cuda },
                AcceleratorType.OpenCL => new KernelDefinition("vectorAdd", OpenCLSource, "vectorAdd") { Language = KernelLanguage.OpenCL },
                AcceleratorType.Metal => new KernelDefinition("vectorAdd", MetalSource, "vectorAdd") { Language = KernelLanguage.Metal },
                _ => new KernelDefinition("vectorAdd", CudaSource, "vectorAdd") { Language = KernelLanguage.CSharp }
            };
        }
    }

    /// <summary>
    /// Vector multiplication: result[i] = a[i] * b[i]
    /// </summary>
    public static class VectorMultiply
    {
        public const string CudaSource = @"
extern ""C"" __global__ void vectorMultiply(const float* a, const float* b, float* result, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        result[idx] = a[idx] * b[idx];
    }
}";

        public const string OpenCLSource = @"
__kernel void vectorMultiply(__global const float* a, __global const float* b, __global float* result, int length)
{
    int idx = get_global_id(0);
    if (idx < length) {
        result[idx] = a[idx] * b[idx];
    }
}";

        public const string MetalSource = @"
#include <metal_stdlib>
using namespace metal;

kernel void vectorMultiply(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    constant int& length [[buffer(3)]],
    uint idx [[thread_position_in_grid]])
{
    if (idx < (uint)length) {
        result[idx] = a[idx] * b[idx];
    }
}";

        public static KernelDefinition GetDefinition(AcceleratorType type)
        {
            return type switch
            {
                AcceleratorType.CUDA => new KernelDefinition("vectorMultiply", CudaSource, "vectorMultiply") { Language = KernelLanguage.Cuda },
                AcceleratorType.OpenCL => new KernelDefinition("vectorMultiply", OpenCLSource, "vectorMultiply") { Language = KernelLanguage.OpenCL },
                AcceleratorType.Metal => new KernelDefinition("vectorMultiply", MetalSource, "vectorMultiply") { Language = KernelLanguage.Metal },
                _ => new KernelDefinition("vectorMultiply", CudaSource, "vectorMultiply") { Language = KernelLanguage.CSharp }
            };
        }
    }

    /// <summary>
    /// Dot product with two-phase reduction: result[0] = sum(a[i] * b[i])
    /// </summary>
    public static class DotProduct
    {
        // Phase 1: Parallel multiplication and local reduction
        public const string CudaPhase1Source = @"
extern ""C"" __global__ void dotProductPhase1(const float* a, const float* b, float* partialSums, int length)
{
    __shared__ float sdata[256];

    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;

    // Multiply and load into shared memory
    sdata[tid] = (idx < length) ? a[idx] * b[idx] : 0.0f;
    __syncthreads();

    // Parallel reduction in shared memory
    for (int s = blockDim.x / 2; s > 0; s >>= 1) {
        if (tid < s) {
            sdata[tid] += sdata[tid + s];
        }
        __syncthreads();
    }

    // Write partial sum
    if (tid == 0) {
        partialSums[blockIdx.x] = sdata[0];
    }
}";

        // Phase 2: Final reduction
        public const string CudaPhase2Source = @"
extern ""C"" __global__ void dotProductPhase2(const float* partialSums, float* result, int numBlocks)
{
    __shared__ float sdata[256];

    int tid = threadIdx.x;

    // Load partial sums
    sdata[tid] = (tid < numBlocks) ? partialSums[tid] : 0.0f;
    __syncthreads();

    // Final reduction
    for (int s = blockDim.x / 2; s > 0; s >>= 1) {
        if (tid < s) {
            sdata[tid] += sdata[tid + s];
        }
        __syncthreads();
    }

    if (tid == 0) {
        result[0] = sdata[0];
    }
}";

        public const string OpenCLPhase1Source = @"
__kernel void dotProductPhase1(__global const float* a, __global const float* b, __global float* partialSums, int length, __local float* scratch)
{
    int lid = get_local_id(0);
    int gid = get_global_id(0);
    int groupId = get_group_id(0);
    int localSize = get_local_size(0);

    // Multiply and load into local memory
    scratch[lid] = (gid < length) ? a[gid] * b[gid] : 0.0f;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Parallel reduction
    for (int s = localSize / 2; s > 0; s >>= 1) {
        if (lid < s) {
            scratch[lid] += scratch[lid + s];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    // Write partial sum
    if (lid == 0) {
        partialSums[groupId] = scratch[0];
    }
}";

        public const string OpenCLPhase2Source = @"
__kernel void dotProductPhase2(__global const float* partialSums, __global float* result, int numBlocks, __local float* scratch)
{
    int lid = get_local_id(0);

    // Load partial sums
    scratch[lid] = (lid < numBlocks) ? partialSums[lid] : 0.0f;
    barrier(CLK_LOCAL_MEM_FENCE);

    // Final reduction
    for (int s = get_local_size(0) / 2; s > 0; s >>= 1) {
        if (lid < s) {
            scratch[lid] += scratch[lid + s];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    if (lid == 0) {
        result[0] = scratch[0];
    }
}";

        public const string MetalPhase1Source = @"
#include <metal_stdlib>
using namespace metal;

kernel void dotProductPhase1(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* partialSums [[buffer(2)]],
    constant int& length [[buffer(3)]],
    threadgroup float* scratch [[threadgroup(0)]],
    uint lid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]],
    uint groupId [[threadgroup_position_in_grid]],
    uint groupSize [[threads_per_threadgroup]])
{
    // Multiply and load into threadgroup memory
    scratch[lid] = (gid < (uint)length) ? a[gid] * b[gid] : 0.0f;
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Parallel reduction
    for (uint s = groupSize / 2; s > 0; s >>= 1) {
        if (lid < s) {
            scratch[lid] += scratch[lid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    // Write partial sum
    if (lid == 0) {
        partialSums[groupId] = scratch[0];
    }
}";

        public const string MetalPhase2Source = @"
#include <metal_stdlib>
using namespace metal;

kernel void dotProductPhase2(
    device const float* partialSums [[buffer(0)]],
    device float* result [[buffer(1)]],
    constant int& numBlocks [[buffer(2)]],
    threadgroup float* scratch [[threadgroup(0)]],
    uint lid [[thread_position_in_threadgroup]],
    uint groupSize [[threads_per_threadgroup]])
{
    // Load partial sums
    scratch[lid] = (lid < (uint)numBlocks) ? partialSums[lid] : 0.0f;
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Final reduction
    for (uint s = groupSize / 2; s > 0; s >>= 1) {
        if (lid < s) {
            scratch[lid] += scratch[lid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    if (lid == 0) {
        result[0] = scratch[0];
    }
}";
    }
}
