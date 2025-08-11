// Test kernels for RTX 2000 Ada Generation GPU validation
// These kernels are compiled at runtime using NVRTC for hardware testing

#include <cuda_runtime.h>
#include <math.h>
#include <cooperative_groups.h>

using namespace cooperative_groups;

// Simple vector addition kernel for basic functionality testing
extern "C" __global__ void vectorAdd(float* a, float* b, float* c, int n)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        c[idx] = a[idx] + b[idx];
    }
}

// Memory bandwidth testing kernel
extern "C" __global__ void memoryBandwidthTest(float* input, float* output, int n, int iterations)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        float val = input[idx];
        
        // Perform multiple memory accesses to stress bandwidth
        for (int i = 0; i < iterations; i++) {
            val = input[(idx + i) % n];
        }
        
        output[idx] = val;
    }
}

// Compute-intensive kernel for GFLOPS measurement
extern "C" __global__ void computeIntensive(float* data, int n, int iterations)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        float val = data[idx];
        
        // Perform intensive floating-point operations
        for (int i = 0; i < iterations; i++) {
            val = val * val + sqrtf(fabsf(val)) - sinf(val) + cosf(val) + tanhf(val);
            val = fmaf(val, 0.99f, 0.01f); // Fused multiply-add
        }
        
        data[idx] = val;
    }
}

// Matrix multiplication kernel optimized for Ada architecture
extern "C" __global__ void matrixMul(float* A, float* B, float* C, int widthA, int heightA, int widthB)
{
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    int bx = blockIdx.x;
    int by = blockIdx.y;
    
    int row = by * blockDim.y + ty;
    int col = bx * blockDim.x + tx;
    
    if (row < heightA && col < widthB) {
        float sum = 0.0f;
        
        // Standard matrix multiplication
        for (int k = 0; k < widthA; ++k) {
            sum = fmaf(A[row * widthA + k], B[k * widthB + col], sum);
        }
        
        C[row * widthB + col] = sum;
    }
}

// Optimized tiled matrix multiplication for shared memory utilization
extern "C" __global__ void tiledMatrixMul(float* A, float* B, float* C, int widthA, int heightA, int widthB)
{
    const int TILE_SIZE = 16;
    
    __shared__ float As[TILE_SIZE][TILE_SIZE];
    __shared__ float Bs[TILE_SIZE][TILE_SIZE];
    
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    int bx = blockIdx.x;
    int by = blockIdx.y;
    
    int row = by * TILE_SIZE + ty;
    int col = bx * TILE_SIZE + tx;
    
    float sum = 0.0f;
    
    // Process tiles
    for (int m = 0; m < (widthA + TILE_SIZE - 1) / TILE_SIZE; ++m) {
        // Load tile into shared memory
        if (row < heightA && m * TILE_SIZE + tx < widthA) {
            As[ty][tx] = A[row * widthA + m * TILE_SIZE + tx];
        } else {
            As[ty][tx] = 0.0f;
        }
        
        if (col < widthB && m * TILE_SIZE + ty < widthA) {
            Bs[ty][tx] = B[(m * TILE_SIZE + ty) * widthB + col];
        } else {
            Bs[ty][tx] = 0.0f;
        }
        
        __syncthreads();
        
        // Compute partial sum
        #pragma unroll
        for (int k = 0; k < TILE_SIZE; ++k) {
            sum = fmaf(As[ty][k], Bs[k][tx], sum);
        }
        
        __syncthreads();
    }
    
    if (row < heightA && col < widthB) {
        C[row * widthB + col] = sum;
    }
}

// Reduction kernel for testing warp-level operations
extern "C" __global__ void reduction(float* input, float* output, int n)
{
    extern __shared__ float sdata[];
    
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    // Load data into shared memory
    sdata[tid] = (idx < n) ? input[idx] : 0.0f;
    __syncthreads();
    
    // Tree reduction
    for (int s = blockDim.x / 2; s > 32; s >>= 1) {
        if (tid < s) {
            sdata[tid] += sdata[tid + s];
        }
        __syncthreads();
    }
    
    // Warp-level reduction for RTX 2000 Ada Gen
    if (tid < 32) {
        volatile float* vsdata = sdata;
        
        if (blockDim.x >= 64) vsdata[tid] += vsdata[tid + 32];
        if (blockDim.x >= 32) vsdata[tid] += vsdata[tid + 16];
        if (blockDim.x >= 16) vsdata[tid] += vsdata[tid + 8];
        if (blockDim.x >= 8) vsdata[tid] += vsdata[tid + 4];
        if (blockDim.x >= 4) vsdata[tid] += vsdata[tid + 2];
        if (blockDim.x >= 2) vsdata[tid] += vsdata[tid + 1];
    }
    
    // Write result
    if (tid == 0) {
        output[blockIdx.x] = sdata[0];
    }
}

// Cooperative groups reduction for advanced features
extern "C" __global__ void cooperativeReduction(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    float val = (idx < n) ? input[idx] : 0.0f;
    
    // Use cooperative groups for warp reduction
    auto warp = tiled_partition<32>(this_thread_block());
    
    // Warp-level reduce
    for (int offset = warp.size() / 2; offset > 0; offset /= 2) {
        val += warp.shfl_down(val, offset);
    }
    
    // Store per-warp result
    __shared__ float warpResults[32];
    
    if (warp.thread_rank() == 0) {
        warpResults[warp.meta_group_rank()] = val;
    }
    
    __syncthreads();
    
    // Final reduction by first warp
    if (warp.meta_group_rank() == 0) {
        val = (warp.thread_rank() < (blockDim.x + 31) / 32) ? warpResults[warp.thread_rank()] : 0.0f;
        
        for (int offset = warp.size() / 2; offset > 0; offset /= 2) {
            val += warp.shfl_down(val, offset);
        }
        
        if (warp.thread_rank() == 0) {
            output[blockIdx.x] = val;
        }
    }
}

// Memory latency measurement kernel
extern "C" __global__ void memoryLatency(float* data, int* indices, float* results, int n)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        // Pointer chasing to measure latency
        int current_idx = indices[idx];
        float val = data[current_idx];
        
        // Multiple pointer chases
        for (int i = 0; i < 10; i++) {
            current_idx = indices[current_idx % n];
            val += data[current_idx];
        }
        
        results[idx] = val;
    }
}

// Stress test kernel for thermal and stability testing
extern "C" __global__ void stressTest(float* data, int n, int duration_ms)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        float val = data[idx];
        
        // High-intensity computation for stress testing
        clock_t start = clock();
        clock_t duration_clocks = duration_ms * (clock_t)(1000); // Approximate
        
        while ((clock() - start) < duration_clocks) {
            val = val * 1.01f + sinf(val) - cosf(val * 2.0f);
            val = sqrtf(fabsf(val)) + powf(val, 1.1f);
            val = fmaf(val, 0.99f, 0.01f);
            
            // Prevent optimization
            if (val > 1e10f) val *= 0.5f;
            if (val < -1e10f) val *= 0.5f;
        }
        
        data[idx] = val;
    }
}

// Multi-GPU P2P transfer test kernel
extern "C" __global__ void p2pTransferTest(float* src, float* dst, int n, int device_id)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx < n) {
        // Simple copy with device identification
        dst[idx] = src[idx] + (float)device_id;
    }
}

// Tensor operation kernel for ML workload simulation
extern "C" __global__ void tensorOperation(
    float* input, 
    float* weights, 
    float* bias, 
    float* output, 
    int batch_size, 
    int input_dim, 
    int output_dim)
{
    int batch_idx = blockIdx.x;
    int out_idx = blockIdx.y * blockDim.y + threadIdx.y;
    int tid = threadIdx.x;
    
    if (batch_idx >= batch_size || out_idx >= output_dim) return;
    
    __shared__ float shared_input[256];
    __shared__ float shared_weights[256];
    
    float sum = 0.0f;
    
    // Process input in tiles
    for (int tile = 0; tile < (input_dim + 255) / 256; ++tile) {
        int input_idx = tile * 256 + tid;
        
        // Load input tile
        if (input_idx < input_dim) {
            shared_input[tid] = input[batch_idx * input_dim + input_idx];
        } else {
            shared_input[tid] = 0.0f;
        }
        
        // Load weight tile
        if (input_idx < input_dim) {
            shared_weights[tid] = weights[out_idx * input_dim + input_idx];
        } else {
            shared_weights[tid] = 0.0f;
        }
        
        __syncthreads();
        
        // Compute partial sum
        #pragma unroll
        for (int k = 0; k < 256 && (tile * 256 + k) < input_dim; ++k) {
            sum = fmaf(shared_input[k], shared_weights[k], sum);
        }
        
        __syncthreads();
    }
    
    // Apply bias and activation (ReLU)
    if (tid == 0) {
        sum += bias[out_idx];
        output[batch_idx * output_dim + out_idx] = fmaxf(0.0f, sum);
    }
}

// FFT butterfly operation for signal processing validation
extern "C" __global__ void fftButterfly(float2* data, int n, int stage)
{
    int idx = blockDim.x * blockIdx.x + threadIdx.x;
    if (idx >= n / 2) return;
    
    int stride = 1 << stage;
    int group = idx / stride;
    int pos_in_group = idx % stride;
    
    int i = group * stride * 2 + pos_in_group;
    int j = i + stride;
    
    if (j < n) {
        // Twiddle factor
        float angle = -2.0f * M_PI * pos_in_group / (stride * 2);
        float2 twiddle = make_float2(cosf(angle), sinf(angle));
        
        // Complex multiplication: data[j] *= twiddle
        float2 temp = data[j];
        data[j].x = temp.x * twiddle.x - temp.y * twiddle.y;
        data[j].y = temp.x * twiddle.y + temp.y * twiddle.x;
        
        // Butterfly operation
        temp = data[i];
        data[i].x = temp.x + data[j].x;
        data[i].y = temp.y + data[j].y;
        data[j].x = temp.x - data[j].x;
        data[j].y = temp.y - data[j].y;
    }
}