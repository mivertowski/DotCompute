// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Linq;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Hardware.Cuda.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests;

/// <summary>
/// Tests for CUDA cooperative groups functionality requiring CUDA 13.0+
/// </summary>
public class CooperativeGroupsTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<CooperativeGroupsTests> _logger;
    private readonly CudaAcceleratorFactory _factory;

    public CooperativeGroupsTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XUnitLogger<CooperativeGroupsTests>(output);
        _factory = new CudaAcceleratorFactory();
    }

    [Fact]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "CUDA13Required")]
    public async Task CooperativeGroups_BasicReduction_ExecutesCorrectly()
    {
        // Skip if no CUDA device available
        if (!CudaTestHelpers.IsCudaAvailable())
        {
            _output.WriteLine("No CUDA device available - skipping test");
            return;
        }

        using var accelerator = _factory.CreateProductionAccelerator(0);
        
        // Create cooperative groups reduction kernel
        const string kernelCode = @"
#include <cooperative_groups.h>
using namespace cooperative_groups;

extern ""C"" __global__ void cooperativeReduction(float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    float val = (idx < n) ? input[idx] : 0.0f;
    
    // Use cooperative groups for warp reduction
    auto warp = tiled_partition<32>(this_thread_block());
    
    // Warp-level reduce using shuffle operations
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
}";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
            "cooperativeReduction",
            kernelCode
        );
        
        var options = CudaTestHelpers.CreateTestCompilationOptions(
            CudaOptimizationLevel.O3,
            enableRegisterSpilling: true
        );

        var kernel = await accelerator.CompileKernelAsync(kernelDef, new DotCompute.Abstractions.CompilationOptions());

        // Test data
        const int size = 1024;
        const int blockSize = 256;
        const int gridSize = (size + blockSize - 1) / blockSize;
        
        float[] inputData = new float[size];
        for (int i = 0; i < size; i++)
        {
            inputData[i] = i + 1.0f; // 1, 2, 3, ..., 1024
        }
        
        // Allocate device memory
        using var input = await accelerator.Memory.AllocateAsync<float>(size);
        using var output = await accelerator.Memory.AllocateAsync<float>(gridSize);
        
        // Copy input data to device
        await input.CopyFromAsync(inputData.AsMemory());
        
        // Execute kernel with cooperative groups
        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(gridSize, 1, 1, blockSize, 1, 1);
        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
            new object[] { input, output, size },
            grid,
            block
        );
        await kernel.ExecuteAsync(kernelArgs);

        await accelerator.SynchronizeAsync();
        
        // Copy results back
        float[] results = new float[gridSize];
        await output.CopyToAsync(results.AsMemory());
        
        // Calculate expected sum: 1 + 2 + ... + 1024 = (1024 * 1025) / 2 = 524800
        float expectedSum = (size * (size + 1)) / 2.0f;
        float actualSum = results.Sum();
        
        _output.WriteLine($"Expected sum: {expectedSum}");
        _output.WriteLine($"Actual sum: {actualSum}");
        _output.WriteLine($"Per-block sums: {string.Join(", ", results)}");
        
        // Allow small floating-point tolerance
        Assert.True(Math.Abs(expectedSum - actualSum) < 0.01f,
            $"Sum mismatch: expected {expectedSum}, got {actualSum}");
    }

    [Fact]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "CUDA13Required")]
    public async Task TensorCoreOps_MatrixMultiply_WithSharedMemorySpilling()
    {
        // Skip if no CUDA device available
        if (!CudaTestHelpers.IsCudaAvailable())
        {
            _output.WriteLine("No CUDA device available - skipping test");
            return;
        }

        using var accelerator = _factory.CreateProductionAccelerator(0);

        // Matrix multiply kernel using register spilling for large shared memory
        const string kernelCode = @"
extern ""C"" __global__ void matmul_with_spilling(
    const float* __restrict__ A,
    const float* __restrict__ B,
    float* __restrict__ C,
    const int M, const int N, const int K)
{
    // Large shared memory arrays that may cause register spilling
    __shared__ float tileA[32][33]; // Avoid bank conflicts
    __shared__ float tileB[32][33];
    
    int bx = blockIdx.x;
    int by = blockIdx.y;
    int tx = threadIdx.x;
    int ty = threadIdx.y;
    
    int row = by * 32 + ty;
    int col = bx * 32 + tx;
    
    float sum = 0.0f;
    
    // Tile-based matrix multiplication
    for (int t = 0; t < (K + 31) / 32; t++)
    {
        // Load tiles into shared memory
        if (row < M && t * 32 + tx < K)
            tileA[ty][tx] = A[row * K + t * 32 + tx];
        else
            tileA[ty][tx] = 0.0f;
            
        if (col < N && t * 32 + ty < K)
            tileB[ty][tx] = B[(t * 32 + ty) * N + col];
        else
            tileB[ty][tx] = 0.0f;
            
        __syncthreads();
        
        // Compute partial products
        #pragma unroll
        for (int k = 0; k < 32; k++)
        {
            sum += tileA[ty][k] * tileB[k][tx];
        }
        
        __syncthreads();
    }
    
    // Store result
    if (row < M && col < N)
    {
        C[row * N + col] = sum;
    }
}";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
            "matmul_with_spilling",
            kernelCode
        );

        var options = CudaTestHelpers.CreateTestCompilationOptions(
            CudaOptimizationLevel.O3,
            enableRegisterSpilling: true  // Enable register spilling for shared memory pressure
        );

        var kernel = await accelerator.CompileKernelAsync(kernelDef, new DotCompute.Abstractions.CompilationOptions());

        // Test with small matrices
        const int M = 64, N = 64, K = 64;
        
        // Initialize test data
        float[] A = new float[M * K];
        float[] B = new float[K * N];
        float[] expectedC = new float[M * N];
        
        // Simple initialization for verification
        for (int i = 0; i < M * K; i++)
        {
            A[i] = (i % 10) * 0.1f;
        }
        
        for (int i = 0; i < K * N; i++)
        {
            B[i] = (i % 10) * 0.1f;
        }
        
        // Calculate expected result on CPU
        for (int i = 0; i < M; i++)
        {
            for (int j = 0; j < N; j++)
            {
                float sum = 0.0f;
                for (int k = 0; k < K; k++)
                {
                    sum += A[i * K + k] * B[k * N + j];
                }
                expectedC[i * N + j] = sum;
            }
        }
        
        // Allocate device memory
        using var dA = await accelerator.Memory.AllocateAsync<float>(M * K);
        using var dB = await accelerator.Memory.AllocateAsync<float>(K * N);
        using var dC = await accelerator.Memory.AllocateAsync<float>(M * N);
        
        await dA.CopyFromAsync(A.AsMemory());
        await dB.CopyFromAsync(B.AsMemory());
        
        // Launch kernel with 32x32 thread blocks
        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(
            (N + 31) / 32, (M + 31) / 32, 1,
            32, 32, 1
        );
        
        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
            new object[] { dA, dB, dC, M, N, K },
            grid,
            block
        );
        await kernel.ExecuteAsync(kernelArgs);
        
        await accelerator.SynchronizeAsync();
        
        // Verify results
        float[] actualC = new float[M * N];
        await dC.CopyToAsync(actualC.AsMemory());
        
        float maxError = 0.0f;
        for (int i = 0; i < M * N; i++)
        {
            float error = Math.Abs(expectedC[i] - actualC[i]);
            maxError = Math.Max(maxError, error);
        }
        
        _output.WriteLine($"Matrix multiplication max error: {maxError}");
        Assert.True(maxError < 0.001f, $"Matrix multiplication error too large: {maxError}");
    }

    public void Dispose()
    {
        // Factory will dispose of created accelerators
        _factory?.Dispose();
    }

    // Test logger implementation
    private class XUnitLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public XUnitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, 
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }
}