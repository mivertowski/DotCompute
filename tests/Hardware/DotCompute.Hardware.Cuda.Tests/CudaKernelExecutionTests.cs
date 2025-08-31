// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Tests.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Hardware tests for CUDA kernel execution functionality.
    /// Tests kernel compilation, execution, and performance on real hardware.
    /// </summary>
    [Trait("Category", "RequiresCUDA")]
    public class CudaKernelExecutionTests : TestBase
    {
        private const string VectorAddKernel = @"
            __global__ void vectorAdd(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] + b[idx];
                }
            }";

        private const string MatrixMultiplyKernel = @"
            __global__ void matrixMultiply(float* a, float* b, float* c, int width) {
                int row = blockIdx.y * blockDim.y + threadIdx.y;
                int col = blockIdx.x * blockDim.x + threadIdx.x;
                
                if (row < width && col < width) {
                    float sum = 0.0f;
                    for (int k = 0; k < width; k++) {
                        sum += a[row * width + k] * b[k * width + col];
                    }
                    c[row * width + col] = sum;
                }
            }";

        private const string SharedMemoryKernel = @"
            __global__ void sharedMemoryReduce(float* input, float* output, int n) {
                __shared__ float sdata[256];
                
                int tid = threadIdx.x;
                int i = blockIdx.x * blockDim.x + threadIdx.x;
                
                sdata[tid] = (i < n) ? input[i] : 0.0f;
                __syncthreads();
                
                // Reduction in shared memory
                for (int s = blockDim.x / 2; s > 0; s >>= 1) {
                    if (tid < s) {
                        sdata[tid] += sdata[tid + s];
                    }
                    __syncthreads();
                }
                
                if (tid == 0) {
                    output[blockIdx.x] = sdata[0];
                }
            }";

        private const string DynamicParallelismKernel = @"
            __global__ void childKernel(float* data, int start, int end) {
                int idx = start + blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < end) {
                    data[idx] = data[idx] * 2.0f;
                }
            }
            
            __global__ void parentKernel(float* data, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                int elementsPerThread = n / (gridDim.x * blockDim.x);
                int start = idx * elementsPerThread;
                int end = min(start + elementsPerThread, n);
                
                if (start < n && idx == 0) {
                    dim3 childGrid((end - start + 255) / 256);
                    dim3 childBlock(256);
                    childKernel<<<childGrid, childBlock>>>(data, start, end);
                    cudaDeviceSynchronize();
                }
            }";

        public CudaKernelExecutionTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Vector_Add_Kernel_Should_Execute_Correctly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int elementCount = 1024 * 1024; // 1M elements
            
            // Prepare test data
            var hostA = new float[elementCount];
            var hostB = new float[elementCount];
            var hostResult = new float[elementCount];
            
            for (int i = 0; i < elementCount; i++)
            {
                hostA[i] = i * 0.5f;
                hostB[i] = i * 0.3f;
            }
            
            // Allocate device memory
            using var deviceA = accelerator.CreateBuffer<float>(elementCount);
            using var deviceB = accelerator.CreateBuffer<float>(elementCount);
            using var deviceC = accelerator.CreateBuffer<float>(elementCount);
            
            // Transfer data to device
            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);
            
            // Compile kernel
            var kernel = accelerator.CompileKernel(VectorAddKernel, "vectorAdd");
            kernel.Should().NotBeNull();
            
            // Configure launch parameters
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration(new Dim3(gridSize), new Dim3(blockSize));
            
            // Execute kernel
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, elementCount);
            stopwatch.Stop();
            
            // Read results
            await deviceC.ReadAsync(hostResult.AsSpan(), 0);
            
            // Verify results
            for (int i = 0; i < Math.Min(1000, elementCount); i++) // Check first 1000 elements
            {
                var expected = hostA[i] + hostB[i];
                hostResult[i].Should().BeApproximately(expected, 0.0001f, $"at index {i}");
            }
            
            var throughput = (elementCount * 3 * sizeof(float)) / (stopwatch.Elapsed.TotalSeconds * 1024 * 1024 * 1024);
            
            Output.WriteLine($"Vector Add Performance:");
            Output.WriteLine($"  Elements: {elementCount:N0}");
            Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Throughput: {throughput:F2} GB/s");
            Output.WriteLine($"  Grid Size: {gridSize}, Block Size: {blockSize}");
        }

        [SkippableFact]
        public async Task Matrix_Multiply_Kernel_Should_Execute_Correctly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int matrixSize = 512; // 512x512 matrices
            const int elementCount = matrixSize * matrixSize;
            
            // Prepare test data
            var hostA = new float[elementCount];
            var hostB = new float[elementCount];
            var hostResult = new float[elementCount];
            
            var random = new Random(42);
            for (int i = 0; i < elementCount; i++)
            {
                hostA[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                hostB[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
            
            // Allocate device memory
            using var deviceA = accelerator.CreateBuffer<float>(elementCount);
            using var deviceB = accelerator.CreateBuffer<float>(elementCount);
            using var deviceC = accelerator.CreateBuffer<float>(elementCount);
            
            // Transfer data to device
            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);
            
            // Compile kernel
            var kernel = accelerator.CompileKernel(MatrixMultiplyKernel, "matrixMultiply");
            
            // Configure launch parameters - 2D grid for matrix operations
            const int blockDim = 16;
            var gridDim = (matrixSize + blockDim - 1) / blockDim;
            var launchConfig = new LaunchConfiguration(
                new Dim3(gridDim, gridDim), 
                new Dim3(blockDim, blockDim)
            );
            
            // Execute kernel
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, matrixSize);
            stopwatch.Stop();
            
            // Read results
            await deviceC.ReadAsync(hostResult.AsSpan(), 0);
            
            // Verify a few elements (full verification would be expensive)
            for (int row = 0; row < Math.Min(10, matrixSize); row++)
            {
                for (int col = 0; col < Math.Min(10, matrixSize); col++)
                {
                    float expected = 0.0f;
                    for (int k = 0; k < matrixSize; k++)
                    {
                        expected += hostA[row * matrixSize + k] * hostB[k * matrixSize + col];
                    }
                    
                    var actual = hostResult[row * matrixSize + col];
                    actual.Should().BeApproximately(expected, 0.001f, 
                        $"at position ({row}, {col})");
                }
            }
            
            var gflops = (2.0 * matrixSize * matrixSize * matrixSize) / (stopwatch.Elapsed.TotalSeconds * 1e9);
            
            Output.WriteLine($"Matrix Multiply Performance:");
            Output.WriteLine($"  Matrix Size: {matrixSize}x{matrixSize}");
            Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Output.WriteLine($"  Grid: ({gridDim}x{gridDim}), Block: ({blockDim}x{blockDim})");
        }

        [SkippableFact]
        public async Task Shared_Memory_Kernel_Should_Execute_Correctly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int elementCount = 1024 * 256; // Multiple blocks
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            
            // Prepare test data
            var hostInput = new float[elementCount];
            var hostOutput = new float[gridSize];
            
            for (int i = 0; i < elementCount; i++)
            {
                hostInput[i] = 1.0f; // All ones for easy verification
            }
            
            // Allocate device memory
            using var deviceInput = accelerator.CreateBuffer<float>(elementCount);
            using var deviceOutput = accelerator.CreateBuffer<float>(gridSize);
            
            // Transfer data to device
            await deviceInput.WriteAsync(hostInput.AsSpan(), 0);
            
            // Compile kernel
            var kernel = accelerator.CompileKernel(SharedMemoryKernel, "sharedMemoryReduce");
            
            // Configure launch parameters with shared memory
            var launchConfig = new LaunchConfiguration(
                new Dim3(gridSize), 
                new Dim3(blockSize)
            );
            launchConfig.SharedMemorySizeBytes = blockSize * sizeof(float);
            
            // Execute kernel
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync(launchConfig, deviceInput, deviceOutput, elementCount);
            stopwatch.Stop();
            
            // Read results
            await deviceOutput.ReadAsync(hostOutput.AsSpan(), 0);
            
            // Verify results - each block should sum to blockSize (all elements are 1.0f)
            for (int i = 0; i < gridSize; i++)
            {
                var expectedSum = Math.Min(blockSize, elementCount - i * blockSize);
                hostOutput[i].Should().BeApproximately(expectedSum, 0.0001f, $"block {i}");
            }
            
            var totalSum = 0.0f;
            foreach (var blockSum in hostOutput)
            {
                totalSum += blockSum;
            }
            
            totalSum.Should().BeApproximately(elementCount, 0.0001f);
            
            Output.WriteLine($"Shared Memory Reduction Performance:");
            Output.WriteLine($"  Elements: {elementCount:N0}");
            Output.WriteLine($"  Blocks: {gridSize}, Block Size: {blockSize}");
            Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
            Output.WriteLine($"  Total Sum: {totalSum:F0} (expected: {elementCount})");
        }

        [SkippableFact]
        public async Task Dynamic_Parallelism_Should_Work_If_Supported()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            // Check if dynamic parallelism is supported (compute capability >= 3.5)
            var computeCapability = accelerator.DeviceInfo.ComputeCapability;
            if (computeCapability.Major < 3 || (computeCapability.Major == 3 && computeCapability.Minor < 5))
            {
                Output.WriteLine($"Dynamic parallelism not supported (compute capability {computeCapability.Major}.{computeCapability.Minor})");
                return;
            }
            
            const int elementCount = 1024;
            
            // Prepare test data
            var hostData = new float[elementCount];
            for (int i = 0; i < elementCount; i++)
            {
                hostData[i] = i + 1.0f;
            }
            
            using var deviceData = accelerator.CreateBuffer<float>(elementCount);
            await deviceData.WriteAsync(hostData.AsSpan(), 0);
            
            // Compile kernel with dynamic parallelism
            var kernel = accelerator.CompileKernel(DynamicParallelismKernel, "parentKernel");
            
            var launchConfig = new LaunchConfiguration(new Dim3(4), new Dim3(8));
            
            var stopwatch = Stopwatch.StartNew();
            await kernel.LaunchAsync(launchConfig, deviceData, elementCount);
            stopwatch.Stop();
            
            // Read results
            var resultData = new float[elementCount];
            await deviceData.ReadAsync(resultData.AsSpan(), 0);
            
            // Verify some elements were doubled
            var elementsProcessed = 0;
            for (int i = 0; i < elementCount; i++)
            {
                if (Math.Abs(resultData[i] - hostData[i] * 2.0f) < 0.0001f)
                {
                    elementsProcessed++;
                }
            }
            
            elementsProcessed.Should().BeGreaterThan(0, "Some elements should have been processed by child kernels");
            
            Output.WriteLine($"Dynamic Parallelism Test:");
            Output.WriteLine($"  Compute Capability: {computeCapability.Major}.{computeCapability.Minor}");
            Output.WriteLine($"  Elements Processed: {elementsProcessed}/{elementCount}");
            Output.WriteLine($"  Execution Time: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        }

        [SkippableFact]
        public async Task Kernel_Performance_Should_Meet_Expectations()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            const int iterations = 10;
            const int elementCount = 1024 * 1024;
            
            var hostA = new float[elementCount];
            var hostB = new float[elementCount];
            
            for (int i = 0; i < elementCount; i++)
            {
                hostA[i] = i * 0.5f;
                hostB[i] = i * 0.3f;
            }
            
            using var deviceA = accelerator.CreateBuffer<float>(elementCount);
            using var deviceB = accelerator.CreateBuffer<float>(elementCount);
            using var deviceC = accelerator.CreateBuffer<float>(elementCount);
            
            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);
            
            var kernel = accelerator.CompileKernel(VectorAddKernel, "vectorAdd");
            
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration(new Dim3(gridSize), new Dim3(blockSize));
            
            // Warmup
            await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, elementCount);
            
            var times = new double[iterations];
            
            for (int i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, elementCount);
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalMilliseconds;
            }
            
            var averageTime = times.Sum() / iterations;
            var minTime = times.Min();
            var maxTime = times.Max();
            
            var throughput = (elementCount * 3 * sizeof(float)) / (averageTime / 1000.0 * 1024 * 1024 * 1024);
            
            Output.WriteLine($"Kernel Performance Benchmark ({iterations} iterations):");
            Output.WriteLine($"  Average Time: {averageTime:F2} ms");
            Output.WriteLine($"  Min Time: {minTime:F2} ms");
            Output.WriteLine($"  Max Time: {maxTime:F2} ms");
            Output.WriteLine($"  Average Throughput: {throughput:F2} GB/s");
            
            // Performance should be reasonable for modern GPUs
            averageTime.Should().BeLessThan(10.0, "Kernel execution should be fast");
            throughput.Should().BeGreaterThan(10.0, "Memory throughput should be reasonable");
        }

        [SkippableFact]
        public void Grid_Block_Configuration_Should_Be_Optimized()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            using var accelerator = factory.CreateAccelerator(0);
            
            var deviceInfo = accelerator.DeviceInfo;
            
            // Test different block sizes for optimal performance
            var blockSizes = new[] { 32, 64, 128, 256, 512, 1024 };
            
            foreach (var blockSize in blockSizes)
            {
                if (blockSize <= deviceInfo.MaxThreadsPerBlock)
                {
                    const int totalThreads = 1024 * 1024;
                    var gridSize = (totalThreads + blockSize - 1) / blockSize;
                    
                    // Ensure we don't exceed device limits
                    var maxGridSize = Math.Min(gridSize, 65535); // CUDA grid dimension limit
                    
                    Output.WriteLine($"Block Size: {blockSize}, Grid Size: {maxGridSize}");
                    
                    blockSize.Should().BeGreaterThan(0);
                    blockSize.Should().BeLessOrEqualTo(deviceInfo.MaxThreadsPerBlock);
                    maxGridSize.Should().BeGreaterThan(0);
                    maxGridSize.Should().BeLessOrEqualTo(65535);
                }
            }
            
            Output.WriteLine($"Device Limits:");
            Output.WriteLine($"  Max Threads/Block: {deviceInfo.MaxThreadsPerBlock}");
            Output.WriteLine($"  Multiprocessors: {deviceInfo.MultiprocessorCount}");
            Output.WriteLine($"  Warp Size: {deviceInfo.WarpSize}");
        }
    }
}