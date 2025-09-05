// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Factory;
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
    private CudaAccelerator? _accelerator;

    public CooperativeGroupsTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XUnitLogger<CooperativeGroupsTests>(output);
        _factory = new CudaAcceleratorFactory(_logger);
    }

    [Fact]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "CUDA13Required")]
    public async Task CooperativeGroups_BasicReduction_ExecutesCorrectly()
    {
        // Skip if no CUDA device available
        var deviceInfo = await _factory.GetDeviceInfoAsync(0);
        if (deviceInfo == null)
        {
            _output.WriteLine("No CUDA device available - skipping test");
            return;
        }

        // Check CUDA 13.0 compatibility (requires compute capability 7.5+)
        if (deviceInfo.ComputeCapability < new Version(7, 5))
        {
            _output.WriteLine($"Device {deviceInfo.Name} (CC {deviceInfo.ComputeCapability}) does not support CUDA 13.0 - skipping test");
            return;
        }

        _accelerator = await _factory.CreateAcceleratorAsync(0) as CudaAccelerator;
        Assert.NotNull(_accelerator);

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

        var kernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition(
                "cooperativeReduction",
                kernelCode,
                "cooperativeReduction"
            ),
            new CompilationOptions 
            { 
                OptimizationLevel = OptimizationLevel.Aggressive,
                EnableSharedMemoryRegisterSpilling = true
            });

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
        using var input = await _accelerator.Memory.AllocateAsync(size * sizeof(float));
        using var output = await _accelerator.Memory.AllocateAsync(gridSize * sizeof(float));
        
        // Copy input data to device
        await input.CopyFromHostAsync(inputData);
        
        // Execute kernel with cooperative groups
        await kernel.LaunchAsync(
            new Dim3(gridSize, 1, 1),
            new Dim3(blockSize, 1, 1),
            input.DevicePointer,
            output.DevicePointer,
            size
        );

        await _accelerator.SynchronizeAsync();
        
        // Copy results back
        float[] results = new float[gridSize];
        await output.CopyToHostAsync(results);
        
        // Calculate expected sum: 1 + 2 + ... + 1024 = (1024 * 1025) / 2 = 524800
        float expectedSum = (size * (size + 1)) / 2.0f;
        float actualSum = results.Sum();
        
        _output.WriteLine($"Expected sum: {expectedSum}");
        _output.WriteLine($"Actual sum: {actualSum}");
        _output.WriteLine($"Per-block sums: {string.Join(", ", results)}");
        
        // Allow small floating-point error
        Assert.True(Math.Abs(expectedSum - actualSum) < 0.01f, 
            $"Sum mismatch: expected {expectedSum}, got {actualSum}");
    }

    [Fact]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "CUDA13Required")]
    public async Task CooperativeGroups_GridSync_ExecutesCorrectly()
    {
        // Skip if no CUDA device available
        var deviceInfo = await _factory.GetDeviceInfoAsync(0);
        if (deviceInfo == null)
        {
            _output.WriteLine("No CUDA device available - skipping test");
            return;
        }

        // Check CUDA 13.0 compatibility
        if (deviceInfo.ComputeCapability < new Version(7, 5))
        {
            _output.WriteLine($"Device {deviceInfo.Name} (CC {deviceInfo.ComputeCapability}) does not support CUDA 13.0 - skipping test");
            return;
        }

        _accelerator = await _factory.CreateAcceleratorAsync(0) as CudaAccelerator;
        Assert.NotNull(_accelerator);

        // Create grid-level synchronization kernel
        const string kernelCode = @"
#include <cooperative_groups.h>
using namespace cooperative_groups;

extern ""C"" __global__ void gridSyncTest(int* data, int n, int iterations)
{
    auto grid = this_grid();
    int idx = grid.thread_rank();
    
    if (idx < n) {
        for (int iter = 0; iter < iterations; iter++) {
            // Each thread increments its element
            atomicAdd(&data[idx], 1);
            
            // Grid-wide synchronization
            grid.sync();
            
            // After sync, all threads should see updated values
            if (idx == 0) {
                // Thread 0 checks that all elements have been incremented
                int sum = 0;
                for (int i = 0; i < min(32, n); i++) {
                    sum += data[i];
                }
                // Store check result
                data[n + iter] = sum;
            }
            
            grid.sync();
        }
    }
}";

        var kernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition(
                "gridSyncTest",
                kernelCode,
                "gridSyncTest"
            ),
            new CompilationOptions 
            { 
                OptimizationLevel = OptimizationLevel.Default,
                EnableSharedMemoryRegisterSpilling = true,
                EnableDynamicParallelism = false // Grid sync doesn't work with dynamic parallelism
            });

        // Test parameters
        const int dataSize = 128;
        const int iterations = 5;
        const int totalSize = dataSize + iterations;
        
        // Allocate and initialize data
        using var data = await _accelerator.Memory.AllocateAsync(totalSize * sizeof(int));
        await data.ClearAsync();
        
        // Launch with cooperative launch (required for grid sync)
        // Note: This requires special kernel launch for grid-wide sync
        await kernel.LaunchCooperativeAsync(
            new Dim3(4, 1, 1),  // 4 blocks
            new Dim3(32, 1, 1), // 32 threads per block
            data.DevicePointer,
            dataSize,
            iterations
        );

        await _accelerator.SynchronizeAsync();
        
        // Copy results back
        int[] results = new int[totalSize];
        await data.CopyToHostAsync(results);
        
        _output.WriteLine("Data after grid sync test:");
        _output.WriteLine($"Elements (should be {iterations} each): {string.Join(", ", results.Take(32))}");
        _output.WriteLine($"Iteration sums: {string.Join(", ", results.Skip(dataSize).Take(iterations))}");
        
        // Verify all elements were incremented correctly
        for (int i = 0; i < Math.Min(dataSize, 32); i++)
        {
            Assert.Equal(iterations, results[i]);
        }
    }

    [Fact]
    [Trait("Category", "HardwareRequired")]
    [Trait("Category", "CUDA13Required")]
    public async Task CooperativeGroups_MultiGrid_ExecutesCorrectly()
    {
        // Skip if no CUDA device available
        var deviceInfo = await _factory.GetDeviceInfoAsync(0);
        if (deviceInfo == null)
        {
            _output.WriteLine("No CUDA device available - skipping test");
            return;
        }

        // Check CUDA 13.0 compatibility and multi-GPU support
        if (deviceInfo.ComputeCapability < new Version(7, 5))
        {
            _output.WriteLine($"Device {deviceInfo.Name} (CC {deviceInfo.ComputeCapability}) does not support CUDA 13.0 - skipping test");
            return;
        }

        _accelerator = await _factory.CreateAcceleratorAsync(0) as CudaAccelerator;
        Assert.NotNull(_accelerator);

        // Test thread block partitioning
        const string kernelCode = @"
#include <cooperative_groups.h>
using namespace cooperative_groups;

extern ""C"" __global__ void threadBlockPartition(float* input, float* output, int n)
{
    auto block = this_thread_block();
    auto tile32 = tiled_partition<32>(block);
    auto tile16 = tiled_partition<16>(block);
    auto tile8 = tiled_partition<8>(block);
    
    int globalIdx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (globalIdx < n) {
        float val = input[globalIdx];
        
        // Reduce within 8-thread tile
        for (int i = tile8.size() / 2; i > 0; i /= 2) {
            val += tile8.shfl_down(val, i);
        }
        
        // Leader of each 8-thread tile writes result
        if (tile8.thread_rank() == 0) {
            int tileIdx = globalIdx / 8;
            if (tileIdx < n / 8) {
                output[tileIdx] = val;
            }
        }
    }
}";

        var kernel = await _accelerator.CompileKernelAsync(
            new KernelDefinition(
                "threadBlockPartition",
                kernelCode,
                "threadBlockPartition"
            ),
            new CompilationOptions 
            { 
                OptimizationLevel = OptimizationLevel.Aggressive,
                EnableSharedMemoryRegisterSpilling = true
            });

        // Test data
        const int size = 256;
        float[] inputData = Enumerable.Range(1, size).Select(x => (float)x).ToArray();
        
        using var input = await _accelerator.Memory.AllocateAsync(size * sizeof(float));
        using var output = await _accelerator.Memory.AllocateAsync((size / 8) * sizeof(float));
        
        await input.CopyFromHostAsync(inputData);
        
        await kernel.LaunchAsync(
            new Dim3((size + 127) / 128, 1, 1),
            new Dim3(128, 1, 1),
            input.DevicePointer,
            output.DevicePointer,
            size
        );

        await _accelerator.SynchronizeAsync();
        
        float[] results = new float[size / 8];
        await output.CopyToHostAsync(results);
        
        _output.WriteLine($"Tile reduction results (8-thread tiles): {string.Join(", ", results.Take(10))}...");
        
        // Verify first tile: 1+2+3+4+5+6+7+8 = 36
        Assert.Equal(36.0f, results[0], 1);
    }

    public void Dispose()
    {
        _accelerator?.Dispose();
        _factory.Dispose();
    }
}

/// <summary>
/// XUnit logger adapter
/// </summary>
internal class XUnitLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public XUnitLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            _output.WriteLine(exception.ToString());
        }
    }
}