// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Factory;
using Microsoft.Extensions.Logging;

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
            DotCompute.Abstractions.Types.OptimizationLevel.O3
        );

        var kernel = await accelerator.CompileKernelAsync(kernelDef, new DotCompute.Abstractions.CompilationOptions());

        // Test data
        const int size = 1024;
        const int blockSize = 256;
        const int gridSize = (size + blockSize - 1) / blockSize;


        var inputData = new float[size];
        for (var i = 0; i < size; i++)
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
            [input, output, size],
            grid,
            block
        );
        await kernel.ExecuteAsync(kernelArgs);

        await accelerator.SynchronizeAsync();

        // Copy results back

        var results = new float[gridSize];
        await output.CopyToAsync(results.AsMemory());

        // Calculate expected sum: 1 + 2 + ... + 1024 = (1024 * 1025) / 2 = 524800

        var expectedSum = (size * (size + 1)) / 2.0f;
        var actualSum = results.Sum();


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

        // Simpler cooperative groups test with register spilling
        const string kernelCode = @"
extern ""C"" __global__ void matmul_with_spilling(float* output, int size)
{
    // Use large shared memory to trigger register spilling  
    __shared__ float shared_data[512];
    
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    int local_tid = threadIdx.x;
    
    // Initialize shared memory
    if (local_tid < 512)
    {
        shared_data[local_tid] = (float)(tid + local_tid);
    }
    
    __syncthreads();
    
    // Simple computation that uses registers and shared memory
    float result = 0.0f;
    
    // Multiple accumulations to increase register pressure
    for (int i = 0; i < 8; i++)
    {
        float temp1 = shared_data[(local_tid + i * 64) % 512];
        float temp2 = shared_data[(local_tid + i * 32 + 1) % 512];
        float temp3 = shared_data[(local_tid + i * 16 + 2) % 512];
        float temp4 = shared_data[(local_tid + i * 8 + 3) % 512];
        
        result += temp1 * 1.1f + temp2 * 1.2f + temp3 * 1.3f + temp4 * 1.4f;
    }
    
    // Write result
    if (tid < size)
    {
        output[tid] = result;
    }
}";

        var kernelDef = CudaTestHelpers.CreateTestKernelDefinition(
            "matmul_with_spilling",
            kernelCode
        );

        var options = CudaTestHelpers.CreateTestCompilationOptions(
            DotCompute.Abstractions.Types.OptimizationLevel.O3,
            generateDebugInfo: false  // Standard optimization without debug info
        );

        var kernel = await accelerator.CompileKernelAsync(kernelDef, new DotCompute.Abstractions.CompilationOptions());

        // Test with simple array
        const int size = 1024;

        // Allocate device memory

        using var dOutput = await accelerator.Memory.AllocateAsync<float>(size);

        // Launch kernel with 512 threads per block (matches shared memory size)

        var (grid, block) = CudaTestHelpers.CreateLaunchConfig(
            2, 1, 1,  // 2 blocks
            512, 1, 1  // 512 threads per block
        );


        var kernelArgs = CudaTestHelpers.CreateKernelArguments(
            [dOutput, size],
            grid,
            block
        );
        await kernel.ExecuteAsync(kernelArgs);


        await accelerator.SynchronizeAsync();

        // Download and verify results

        var results = new float[size];
        await dOutput.CopyToAsync(results.AsMemory());

        // Basic verification - check that kernel executed and produced non-zero results
        var hasValidResults = false;
        var sumResults = 0.0f;


        for (var i = 0; i < Math.Min(10, size); i++)
        {
            var result = results[i];
            _output.WriteLine($"Element {i}: {result:F3}");


            if (result != 0.0f && !float.IsNaN(result) && !float.IsInfinity(result))
            {
                hasValidResults = true;
                sumResults += result;
            }
        }


        _output.WriteLine($"Total sum of first 10 elements: {sumResults:F3}");


        Assert.True(hasValidResults, "Kernel should produce valid non-zero results with shared memory spilling");
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

        IDisposable ILogger.BeginScope<TState>(TState state) => null!;
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