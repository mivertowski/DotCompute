// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Core.Extensions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Performance benchmark tests for CUDA operations.
    /// Tests real-world performance scenarios and validates against expected performance metrics.
    /// </summary>
    [Trait("Category", "RequiresCUDA")]
    [Trait("Category", "Performance")]
    public class CudaPerformanceBenchmarkTests : CudaTestBase
    {
        private const string MemoryBandwidthKernel = @"
            __global__ void memoryBandwidthTest(float4* input, float4* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    float4 data = input[idx];
                    // Simple computation to ensure memory access
                    data.x = data.x * 1.1f + data.y;
                    data.y = data.y * 1.1f + data.z;
                    data.z = data.z * 1.1f + data.w;
                    data.w = data.w * 1.1f + data.x;
                    output[idx] = data;
                }
            }";

        private const string ComputeIntensiveKernel = @"
            __global__ void computeIntensiveTest(float* input, float* output, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    float value = input[idx];
                    // Compute intensive operations
                    for (int i = 0; i < 1000; i++) {
                        value = sinf(value) * cosf(value) + sqrtf(fabsf(value));
                    }
                    output[idx] = value;
                }
            }";

        private const string MatrixMultiplyOptimized = @"
            __global__ void matrixMultiplyOptimized(float* A, float* B, float* C, int N) {
                __shared__ float As[16][16];
                __shared__ float Bs[16][16];
                
                int bx = blockIdx.x, by = blockIdx.y;
                int tx = threadIdx.x, ty = threadIdx.y;
                
                int row = by * 16 + ty;
                int col = bx * 16 + tx;
                
                float sum = 0.0f;
                
                for (int m = 0; m < (N + 15) / 16; ++m) {
                    if (row < N && m * 16 + tx < N)
                        As[ty][tx] = A[row * N + m * 16 + tx];
                    else
                        As[ty][tx] = 0.0f;
                    
                    if (col < N && m * 16 + ty < N)
                        Bs[ty][tx] = B[(m * 16 + ty) * N + col];
                    else
                        Bs[ty][tx] = 0.0f;
                    
                    __syncthreads();
                    
                    for (int k = 0; k < 16; ++k) {
                        sum += As[ty][k] * Bs[k][tx];
                    }
                    
                    __syncthreads();
                }
                
                if (row < N && col < N) {
                    C[row * N + col] = sum;
                }
            }";

        public CudaPerformanceBenchmarkTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public async Task Memory_Bandwidth_Benchmark_Should_Achieve_Expected_Performance()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            using var memoryTracker = new MemoryTracker(Output);
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);
            
            LogDeviceCapabilities();
            
            const int elementCount = 16 * 1024 * 1024; // 16M float4s = 256MB
            const int iterations = 20;
            const int warmupIterations = 5;
            
            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount * 4); // float4
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount * 4);
            
            var kernelDef = new KernelDefinition("memoryBandwidthTest", MemoryBandwidthKernel, "memoryBandwidthTest");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };
            
            var measure = new PerformanceMeasurement("Memory Bandwidth", Output);
            
            // Warmup
            for (var i = 0; i < warmupIterations; i++)
            {
                await kernel.LaunchAsync(launchConfig, deviceInput, deviceOutput, elementCount);
            }
            
            var times = new double[iterations];
            
            for (var i = 0; i < iterations; i++)
            {
                measure.Start();
                await kernel.LaunchAsync(launchConfig, deviceInput, deviceOutput, elementCount);
                measure.Stop();
                times[i] = measure.ElapsedTime.TotalSeconds;
            }
            
            var avgTime = times.Average();
            var minTime = times.Min();
            var maxTime = times.Max();
            
            // Calculate bandwidth (read + write)
            var bytesPerIteration = elementCount * 4 * sizeof(float) * 2; // read input + write output
            var avgBandwidthGBps = bytesPerIteration / (avgTime * 1024 * 1024 * 1024);
            var peakBandwidthGBps = bytesPerIteration / (minTime * 1024 * 1024 * 1024);
            
            var expectedBandwidth = accelerator.Info.MemoryBandwidthGBps();
            var achievedRatio = avgBandwidthGBps / expectedBandwidth;
            
            Output.WriteLine($"Memory Bandwidth Benchmark Results:");
            Output.WriteLine($"  Data Size: {bytesPerIteration / (1024 * 1024):F1} MB per iteration");
            Output.WriteLine($"  Iterations: {iterations} (after {warmupIterations} warmup)");
            Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
            Output.WriteLine($"  Min/Max Time: {minTime * 1000:F2} / {maxTime * 1000:F2} ms");
            Output.WriteLine($"  Average Bandwidth: {avgBandwidthGBps:F1} GB/s");
            Output.WriteLine($"  Peak Bandwidth: {peakBandwidthGBps:F1} GB/s");
            Output.WriteLine($"  Theoretical Max: {expectedBandwidth:F1} GB/s");
            Output.WriteLine($"  Efficiency: {achievedRatio:P1}");

            // Performance validations
            _ = avgBandwidthGBps.Should().BeGreaterThan(50, "Should achieve reasonable memory bandwidth");
            _ = achievedRatio.Should().BeGreaterThan(0.3, "Should achieve at least 30% of theoretical bandwidth");
            _ = peakBandwidthGBps.Should().BeGreaterThan(avgBandwidthGBps, "Peak should exceed average");
            
            memoryTracker.LogCurrentUsage("After benchmark");
        }

        [SkippableFact]
        public async Task Compute_Performance_Benchmark_Should_Meet_Expectations()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);
            
            const int elementCount = 1024 * 1024; // 1M elements
            const int iterations = 10;
            const int operationsPerElement = 1000;
            
            var hostInput = TestDataGenerator.CreateRandomData(elementCount, seed: 42, min: 0.1f, max: 10.0f);
            
            await using var deviceInput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceOutput = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            await deviceInput.WriteAsync(hostInput.AsSpan(), 0);
            
            var kernelDef = new KernelDefinition("computeIntensiveTest", ComputeIntensiveKernel, "computeIntensiveTest");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };
            
            // Warmup
            await kernel.LaunchAsync(launchConfig, deviceInput, deviceOutput, elementCount);
            
            var times = new double[iterations];
            
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await kernel.LaunchAsync(launchConfig, deviceInput, deviceOutput, elementCount);
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalSeconds;
            }
            
            var avgTime = times.Average();
            var totalOperations = (long)elementCount * operationsPerElement * iterations;
            var gflops = totalOperations / (avgTime * iterations * 1e9);
            
            // Verify computation completed (results should be different from input)
            var hostOutput = new float[elementCount];
            await deviceOutput.ReadAsync(hostOutput.AsSpan(), 0);
            
            var significantDifferences = 0;
            for (var i = 0; i < Math.Min(1000, elementCount); i++)
            {
                if (Math.Abs(hostOutput[i] - hostInput[i]) > 0.1f)
                {
                    significantDifferences++;
                }
            }
            
            Output.WriteLine($"Compute Performance Benchmark:");
            Output.WriteLine($"  Elements: {elementCount:N0}");
            Output.WriteLine($"  Operations per Element: {operationsPerElement:N0}");
            Output.WriteLine($"  Total Operations: {totalOperations:N0}");
            Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
            Output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Output.WriteLine($"  Verification: {significantDifferences}/1000 elements changed significantly");

            _ = gflops.Should().BeGreaterThan(1.0, "Should achieve reasonable compute performance");
            _ = significantDifferences.Should().BeGreaterThan(900, "Most elements should be modified by computation");
        }

        [SkippableFact]
        public async Task Matrix_Multiply_Performance_Should_Be_Optimized()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);
            
            var matrixSizes = new[] { 512, 1024, 2048 };
            
            foreach (var matrixSize in matrixSizes)
            {
                if (accelerator.Info.AvailableMemory < (long)matrixSize * matrixSize * sizeof(float) * 3 * 2)
                {
                    Output.WriteLine($"Skipping matrix size {matrixSize} due to insufficient memory");
                    continue;
                }
                
                await BenchmarkMatrixMultiply(accelerator, matrixSize);
            }
        }

        private async Task BenchmarkMatrixMultiply(IAccelerator accelerator, int matrixSize)
        {
            const int iterations = 5;
            var elementCount = matrixSize * matrixSize;
            
            var hostA = TestDataGenerator.CreateRandomData(elementCount, seed: 42);
            var hostB = TestDataGenerator.CreateRandomData(elementCount, seed: 43);
            
            await using var deviceA = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceB = await accelerator.Memory.AllocateAsync<float>(elementCount);
            await using var deviceC = await accelerator.Memory.AllocateAsync<float>(elementCount);
            
            await deviceA.WriteAsync(hostA.AsSpan(), 0);
            await deviceB.WriteAsync(hostB.AsSpan(), 0);
            
            var kernelDef = new KernelDefinition("matrixMultiplyOptimized", MatrixMultiplyOptimized, "matrixMultiplyOptimized");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            
            var gridDim = (matrixSize + 15) / 16; // 16x16 blocks
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridDim, gridDim),
                BlockSize = new Dim3(16, 16)
            };
            
            // Warmup
            await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, matrixSize);
            
            var times = new double[iterations];
            
            for (var i = 0; i < iterations; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await kernel.LaunchAsync(launchConfig, deviceA, deviceB, deviceC, matrixSize);
                stopwatch.Stop();
                times[i] = stopwatch.Elapsed.TotalSeconds;
            }
            
            var avgTime = times.Average();
            var minTime = times.Min();
            
            // Calculate GFLOPS (2 * N^3 operations for matrix multiply)
            var totalOps = 2.0 * matrixSize * matrixSize * matrixSize;
            var avgGflops = totalOps / (avgTime * 1e9);
            var peakGflops = totalOps / (minTime * 1e9);
            
            // Verify a few results
            var hostResult = new float[elementCount];
            await deviceC.ReadAsync(hostResult.AsSpan(), 0);
            
            // Quick verification - compute first few elements on CPU
            for (var row = 0; row < Math.Min(3, matrixSize); row++)
            {
                for (var col = 0; col < Math.Min(3, matrixSize); col++)
                {
                    var expected = 0.0f;
                    for (var k = 0; k < matrixSize; k++)
                    {
                        expected += hostA[row * matrixSize + k] * hostB[k * matrixSize + col];
                    }
                    
                    var actual = hostResult[row * matrixSize + col];
                    _ = Math.Abs(actual - expected).Should().BeLessThan(0.01f,
                        $"Matrix multiply result incorrect at ({row},{col})");
                }
            }
            
            Output.WriteLine($"Matrix Multiply ({matrixSize}x{matrixSize}):");
            Output.WriteLine($"  Average Time: {avgTime * 1000:F2} ms");
            Output.WriteLine($"  Min Time: {minTime * 1000:F2} ms");
            Output.WriteLine($"  Average Performance: {avgGflops:F1} GFLOPS");
            Output.WriteLine($"  Peak Performance: {peakGflops:F1} GFLOPS");
            Output.WriteLine($"  Memory Usage: {elementCount * 3 * sizeof(float) / (1024 * 1024):F1} MB");
            
            // Performance expectations based on matrix size
            var expectedMinGflops = matrixSize switch
            {
                512 => 100.0,   // Smaller matrices may not fully utilize GPU
                1024 => 500.0,  // Good utilization expected
                2048 => 1000.0, // Should achieve high performance
                _ => 50.0
            };

            _ = avgGflops.Should().BeGreaterThan(expectedMinGflops,
                $"Matrix multiply should achieve reasonable performance for {matrixSize}x{matrixSize}");
        }

        [SkippableFact]
        public async Task Transfer_Performance_Should_Meet_PCIe_Expectations()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);
            
            var transferSizes = new[] { 1, 4, 16, 64, 256 }; // MB
            const int iterations = 10;
            
            Output.WriteLine("PCIe Transfer Performance Benchmark:");
            Output.WriteLine("Size (MB)\tH2D (GB/s)\tD2H (GB/s)\tBidirectional (GB/s)");
            
            foreach (var sizeMB in transferSizes)
            {
                var sizeBytes = sizeMB * 1024 * 1024;
                var elementCount = (int)(sizeBytes / sizeof(float));
                
                var hostData = TestDataGenerator.CreateRandomData(elementCount);
                await using var deviceBuffer = await accelerator.Memory.AllocateAsync<float>(elementCount);
                
                // Host to Device
                var h2dTimes = new double[iterations];
                for (var i = 0; i < iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await deviceBuffer.WriteAsync(hostData.AsSpan(), 0);
                    stopwatch.Stop();
                    h2dTimes[i] = stopwatch.Elapsed.TotalSeconds;
                }
                var h2dBandwidth = sizeBytes / (h2dTimes.Average() * 1024 * 1024 * 1024);
                
                // Device to Host
                var d2hTimes = new double[iterations];
                var resultData = new float[elementCount];
                for (var i = 0; i < iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await deviceBuffer.ReadAsync(resultData.AsSpan(), 0);
                    stopwatch.Stop();
                    d2hTimes[i] = stopwatch.Elapsed.TotalSeconds;
                }
                var d2hBandwidth = sizeBytes / (d2hTimes.Average() * 1024 * 1024 * 1024);
                
                // Bidirectional
                var biTimes = new double[iterations];
                for (var i = 0; i < iterations; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var uploadTask = deviceBuffer.WriteAsync(hostData.AsSpan(), 0);
                    var downloadTask = deviceBuffer.ReadAsync(resultData.AsSpan(), 0);
                    await Task.WhenAll(uploadTask.AsTask(), downloadTask.AsTask());
                    stopwatch.Stop();
                    biTimes[i] = stopwatch.Elapsed.TotalSeconds;
                }
                var biBandwidth = (sizeBytes * 2) / (biTimes.Average() * 1024 * 1024 * 1024);
                
                Output.WriteLine($"{sizeMB}\t\t{h2dBandwidth:F2}\t\t{d2hBandwidth:F2}\t\t{biBandwidth:F2}");
                
                // Performance validations for larger transfers
                if (sizeMB >= 16)
                {
                    _ = h2dBandwidth.Should().BeGreaterThan(2.0, $"H2D bandwidth should be reasonable for {sizeMB}MB");
                    _ = d2hBandwidth.Should().BeGreaterThan(2.0, $"D2H bandwidth should be reasonable for {sizeMB}MB");
                }
            }
        }

        [SkippableFact]
        public async Task Stream_Concurrency_Performance_Should_Show_Benefit()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(2, 0), "Concurrent streams require compute capability 2.0+");
            
            var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);
            
            const int elementCount = 1024 * 1024;
            const int numStreams = 4;
            const int iterations = 5;
            
            var kernelDef = new KernelDefinition("simpleAdd", SimpleKernel, "simpleAdd");
            var kernel = await accelerator.CompileKernelAsync(kernelDef);
            const int blockSize = 256;
            var gridSize = (elementCount + blockSize - 1) / blockSize;
            var launchConfig = new LaunchConfiguration
            {
                GridSize = new Dim3(gridSize),
                BlockSize = new Dim3(blockSize)
            };
            
            // Prepare data for each stream
            var hostDataA = new float[numStreams][];
            var hostDataB = new float[numStreams][];
            var deviceBuffersA = new IUnifiedMemoryBuffer<float>[numStreams];
            var deviceBuffersB = new IUnifiedMemoryBuffer<float>[numStreams];
            var deviceBuffersC = new IUnifiedMemoryBuffer<float>[numStreams];
            var streams = new IComputeStream[numStreams];
            
            for (var i = 0; i < numStreams; i++)
            {
                hostDataA[i] = TestDataGenerator.CreateLinearSequence(elementCount, i * 1000);
                hostDataB[i] = TestDataGenerator.CreateLinearSequence(elementCount, i * 2000);
                deviceBuffersA[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
                deviceBuffersB[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
                deviceBuffersC[i] = await accelerator.Memory.AllocateAsync<float>(elementCount);
                streams[i] = accelerator.CreateStream();
            }
            
            try
            {
                // Sequential execution
                var sequentialTimes = new double[iterations];
                
                for (var iter = 0; iter < iterations; iter++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    for (var i = 0; i < numStreams; i++)
                    {
                        await deviceBuffersA[i].WriteAsync(hostDataA[i].AsSpan(), 0);
                        await deviceBuffersB[i].WriteAsync(hostDataB[i].AsSpan(), 0);
                        await kernel.LaunchAsync(launchConfig, deviceBuffersA[i], deviceBuffersB[i], deviceBuffersC[i], elementCount);
                    }
                    
                    stopwatch.Stop();
                    sequentialTimes[iter] = stopwatch.Elapsed.TotalSeconds;
                }
                
                // Concurrent execution
                var concurrentTimes = new double[iterations];
                
                for (var iter = 0; iter < iterations; iter++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    
                    var tasks = new Task[numStreams];
                    for (var i = 0; i < numStreams; i++)
                    {
                        var streamIndex = i; // Capture for lambda
                        tasks[i] = Task.Run(async () =>
                        {
                            await deviceBuffersA[streamIndex].CopyFromAsync(hostDataA[streamIndex].AsMemory());
                            await deviceBuffersB[streamIndex].CopyFromAsync(hostDataB[streamIndex].AsMemory());
                            var args = new KernelArguments
                            {
                                deviceBuffersA[streamIndex],
                                deviceBuffersB[streamIndex],
                                deviceBuffersC[streamIndex],
                                elementCount
                            };
                            await kernel.LaunchAsync(
                                (launchConfig.GridSize.X, launchConfig.GridSize.Y, launchConfig.GridSize.Z),
                                (launchConfig.BlockSize.X, launchConfig.BlockSize.Y, launchConfig.BlockSize.Z),
                                args);
                        });
                    }
                    
                    await Task.WhenAll(tasks);
                    stopwatch.Stop();
                    concurrentTimes[iter] = stopwatch.Elapsed.TotalSeconds;
                }
                
                var avgSequential = sequentialTimes.Average();
                var avgConcurrent = concurrentTimes.Average();
                var speedup = avgSequential / avgConcurrent;
                
                Output.WriteLine($"Stream Concurrency Performance:");
                Output.WriteLine($"  Streams: {numStreams}");
                Output.WriteLine($"  Elements per stream: {elementCount:N0}");
                Output.WriteLine($"  Sequential time: {avgSequential * 1000:F2} ms");
                Output.WriteLine($"  Concurrent time: {avgConcurrent * 1000:F2} ms");
                Output.WriteLine($"  Speedup: {speedup:F2}x");

                // Should see some benefit from concurrency
                _ = speedup.Should().BeGreaterThan(1.1, "Concurrent streams should provide some performance benefit");
            }
            finally
            {
                // Cleanup
                for (var i = 0; i < numStreams; i++)
                {
                    deviceBuffersA[i]?.Dispose();
                    deviceBuffersB[i]?.Dispose();
                    deviceBuffersC[i]?.Dispose();
                    streams[i]?.Dispose();
                }
            }
        }

        private const string SimpleKernel = @"
            __global__ void simpleAdd(float* a, float* b, float* c, int n) {
                int idx = blockIdx.x * blockDim.x + threadIdx.x;
                if (idx < n) {
                    c[idx] = a[idx] + b[idx];
                }
            }";
    }
}