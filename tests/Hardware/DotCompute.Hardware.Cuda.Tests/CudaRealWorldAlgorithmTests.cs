// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Tests.Common.Specialized;
using DotCompute.Backends.CUDA;
using DotCompute.Backends.CUDA.Factory;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;
using DotCompute.Tests.Common.Helpers;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for real-world algorithm implementations on CUDA
    /// </summary>
    public class CudaRealWorldAlgorithmTests : CudaTestBase
    {
        private readonly CudaAccelerator? _accelerator;
        private readonly ILogger<CudaRealWorldAlgorithmTests>? _logger;

        public CudaRealWorldAlgorithmTests(ITestOutputHelper output) : base(output)
        {
            if (IsCudaAvailable())
            {
                using var factory = new CudaAcceleratorFactory();
                // Create base CUDA accelerator for tests
                _accelerator = new CudaAccelerator(0, Microsoft.Extensions.Logging.Abstractions.NullLogger<CudaAccelerator>.Instance);


                using var loggerFactory = LoggerFactory.Create(builder =>

                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                _logger = loggerFactory.CreateLogger<CudaRealWorldAlgorithmTests>();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _accelerator?.DisposeAsync().AsTask().Wait();
            }
            base.Dispose(disposing);
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "LinearAlgebra")]
        public async Task MatrixMultiplication_Tiled_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int m = 512, n = 768, k = 384;
            var a = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(m * k, 42);
            var b = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(k * n, 43);
            var result = new float[m * n];

            // Calculate expected result (CPU reference)

            var expected = new float[m * n];
            var perf = new PerformanceMeasurement("CPU Matrix Multiply", Output);
            perf.Start();
            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    var sum = 0.0f;
                    for (var l = 0; l < k; l++)
                    {
                        sum += a[i * k + l] * b[l * n + j];
                    }
                    expected[i * n + j] = sum;
                }
            }
            _ = perf.Stop();
            perf.LogResults(m * n * k * 2L); // 2 ops per multiply-add

            // Act - Execute tiled matrix multiplication on GPU
            perf = new PerformanceMeasurement("GPU Tiled Matrix Multiply", Output);
            perf.Start();
            await ExecuteTiledMatrixMultiply(a, b, result, m, n, k);
            _ = perf.Stop();
            perf.LogResults(m * n * k * 2L);

            // Assert
            VerifyFloatArraysMatch(expected, result, 0.001f, "Tiled matrix multiplication");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Reduction")]
        public async Task ParallelReduction_Should_ComputeSumCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int size = 1_000_000;
            var data = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(size, 42, 0.0f, 1.0f);

            // Calculate expected sum

            var expectedSum = data.Sum(x => (double)x);

            // Act
            var perf = new PerformanceMeasurement("Parallel Reduction", Output);
            perf.Start();
            var gpuSum = await ExecuteParallelReduction(data);
            _ = perf.Stop();
            perf.LogResults(size * sizeof(float));

            // Assert
            _ = gpuSum.Should().BeApproximately((float)expectedSum, (float)(expectedSum * 0.001),

                "GPU reduction should match CPU sum within tolerance");


            Output.WriteLine($"Data size: {size:N0} elements");
            Output.WriteLine($"CPU sum: {expectedSum:F6}");
            Output.WriteLine($"GPU sum: {gpuSum:F6}");
            Output.WriteLine($"Relative error: {Math.Abs(gpuSum - expectedSum) / expectedSum:E3}");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Scan")]
        public async Task PrefixSum_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange
            const int size = 10000;
            var input = UnifiedTestHelpers.TestDataGenerator.CreateLinearSequence(size, 1.0f, 1.0f);
            var result = new float[size];

            // Calculate expected prefix sum

            var expected = new float[size];
            expected[0] = input[0];
            for (var i = 1; i < size; i++)
            {
                expected[i] = expected[i - 1] + input[i];
            }

            // Act
            await ExecutePrefixSum(input, result);

            // Assert
            VerifyFloatArraysMatch(expected, result, 0.001f, "Prefix sum (inclusive scan)");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Sorting")]
        public async Task BitonicSort_Should_SortCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - Use power of 2 size for bitonic sort
            const int size = 8192;
            var data = UnifiedTestHelpers.TestDataGenerator.CreateRandomData(size, 42);
            var gpuData = (float[])data.Clone();
            var expected = (float[])data.Clone();
            Array.Sort(expected);

            // Act
            var perf = new PerformanceMeasurement("Bitonic Sort", Output);
            perf.Start();
            await ExecuteBitonicSort(gpuData);
            _ = perf.Stop();
            perf.LogResults(size * sizeof(float));

            // Assert
            VerifyFloatArraysMatch(expected, gpuData, 0.0f, "Bitonic sort");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Transform")]
        public async Task FastFourierTransform_Should_ComputeCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - Simple FFT test with known pattern

            const int size = 1024; // Power of 2 for FFT
            var input = new float[size * 2]; // Complex numbers (real, imag pairs)

            // Create a simple sinusoidal signal

            const float frequency = 10.0f;
            for (var i = 0; i < size; i++)
            {
                input[i * 2] = MathF.Sin(2.0f * MathF.PI * frequency * i / size); // Real part
                input[i * 2 + 1] = 0.0f; // Imaginary part
            }


            var output = new float[size * 2];

            // Act
            await ExecuteFFT(input, output, size);

            // Assert - Check for peak at expected frequency
            var maxMagnitude = 0.0f;
            var maxIndex = 0;


            for (var i = 0; i < size / 2; i++)
            {
                var real = output[i * 2];
                var imag = output[i * 2 + 1];
                var magnitude = MathF.Sqrt(real * real + imag * imag);


                if (magnitude > maxMagnitude)
                {
                    maxMagnitude = magnitude;
                    maxIndex = i;
                }
            }

            // Peak should be at frequency bin 10

            _ = maxIndex.Should().BeCloseTo((int)frequency, 1,

                "FFT should show peak at input frequency");


            Output.WriteLine($"FFT peak found at frequency bin {maxIndex} with magnitude {maxMagnitude:F2}");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Stencil")]
        public async Task HeatDiffusion2D_Should_SimulateCorrectly()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - 2D heat diffusion simulation
            const int width = 256;
            const int height = 256;
            const int iterations = 100;
            const float alpha = 0.1f; // Thermal diffusivity


            var temperature = new float[width * height];

            // Initial conditions - hot spot in center

            var centerX = width / 2;
            var centerY = height / 2;
            var radius = 20;


            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        temperature[y * width + x] = 100.0f; // Hot spot
                    }
                    else
                    {
                        temperature[y * width + x] = 20.0f; // Room temperature
                    }
                }
            }


            var initialEnergy = temperature.Sum();

            // Act
            var perf = new PerformanceMeasurement($"Heat Diffusion {iterations} iterations", Output);
            perf.Start();
            await ExecuteHeatDiffusion2D(temperature, width, height, alpha, iterations);
            _ = perf.Stop();
            perf.LogResults(width * height * sizeof(float) * iterations);

            // Assert
            var finalEnergy = temperature.Sum();

            // Energy should be approximately conserved (with small numerical error)

            _ = finalEnergy.Should().BeApproximately(initialEnergy, initialEnergy * 0.01f,
                "Total energy should be conserved in heat diffusion");

            // Temperature should have diffused (max temp should decrease)

            var maxTemp = temperature.Max();
            _ = maxTemp.Should().BeLessThan(100.0f, "Maximum temperature should decrease due to diffusion");

            // Temperature should be more uniform (lower standard deviation)

            var avgTemp = temperature.Average();
            var variance = temperature.Select(t => (t - avgTemp) * (t - avgTemp)).Average();
            var stdDev = MathF.Sqrt(variance);


            Output.WriteLine($"Initial energy: {initialEnergy:F2}");
            Output.WriteLine($"Final energy: {finalEnergy:F2}");
            Output.WriteLine($"Max temperature: {maxTemp:F2}");
            Output.WriteLine($"Std deviation: {stdDev:F2}");
        }

        [SkippableFact]
        [Trait("Category", "Hardware")]
        [Trait("Algorithm", "Graph")]
        public async Task BreadthFirstSearch_Should_FindShortestPaths()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Arrange - Create a simple graph
            const int numNodes = 1000;
            const int edgesPerNode = 10;

            // Generate random graph (adjacency list format)

            var edges = GenerateRandomGraph(numNodes, edgesPerNode);
            var distances = new int[numNodes];
            Array.Fill(distances, -1); // -1 means unvisited


            const int startNode = 0;
            distances[startNode] = 0;

            // Act
            await ExecuteBFS(edges, distances, startNode, numNodes);

            // Assert
            _ = distances[startNode].Should().Be(0, "Start node should have distance 0");
            _ = distances.Should().NotContain(-1, "All nodes should be reachable in connected graph");

            // Verify monotonicity of distances

            for (var i = 1; i < numNodes; i++)
            {
                _ = distances[i].Should().BeGreaterThan(0, "All non-start nodes should have positive distance");
                _ = distances[i].Should().BeLessThan(numNodes, "Distance should not exceed number of nodes");
            }


            Output.WriteLine($"BFS completed. Max distance: {distances.Max()}");
        }

        // Helper methods for kernel execution
        private async Task ExecuteTiledMatrixMultiply(float[] a, float[] b, float[] result, int m, int n, int k)
        {
            await using var bufferA = await _accelerator.Memory.AllocateAsync<float>(a.Length);
            await using var bufferB = await _accelerator.Memory.AllocateAsync<float>(b.Length);
            await using var bufferC = await _accelerator.Memory.AllocateAsync<float>(result.Length);

            await bufferA.CopyFromAsync(a);
            await bufferB.CopyFromAsync(b);

            var kernel = new KernelDefinition
            {
                Name = "tiled_matrix_multiply",
                Source = GetTiledMatrixMultiplyKernel(),
                EntryPoint = "tiled_matrix_multiply",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                Buffers = new[] { bufferA, bufferB, bufferC },
                ScalarArguments = new object[] { m, n, k }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferC.CopyToAsync(result);
        }

        private async Task<float> ExecuteParallelReduction(float[] data)
        {
            using var bufferData = await _accelerator.Memory.AllocateAsync<float>(data.Length);
            using var bufferResult = await _accelerator.Memory.AllocateAsync<float>(1);

            await bufferData.CopyFromAsync(data);
            await bufferResult.CopyFromAsync(new float[] { 0.0f });

            var kernel = new KernelDefinition
            {
                Name = "parallel_reduction",
                Source = GetParallelReductionKernel(),
                EntryPoint = "parallel_reduction",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                Buffers = new[] { bufferData, bufferResult },
                ScalarArguments = new object[] { data.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();


            var result = new float[1];
            await bufferResult.CopyToAsync(result);
            return result[0];
        }

        private async Task ExecutePrefixSum(float[] input, float[] output)
        {
            using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);

            var kernel = new KernelDefinition
            {
                Name = "prefix_sum",
                Source = GetPrefixSumKernel(),
                EntryPoint = "prefix_sum",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                Buffers = new[] { bufferInput, bufferOutput },
                ScalarArguments = new object[] { input.Length }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
        }

        private async Task ExecuteBitonicSort(float[] data)
        {
            using var bufferData = await _accelerator.Memory.AllocateAsync<float>(data.Length);
            await bufferData.CopyFromAsync(data);

            var kernel = new KernelDefinition
            {
                Name = "bitonic_sort",
                Source = GetBitonicSortKernel(),
                EntryPoint = "bitonic_sort_step",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);

            // Bitonic sort requires multiple passes

            var n = data.Length;
            for (var k = 2; k <= n; k *= 2)
            {
                for (var j = k / 2; j > 0; j /= 2)
                {
                    var args = new KernelArguments
                    {
                        Buffers = new[] { bufferData },
                        ScalarArguments = new object[] { n, j, k }
                    };
                    await compiled.ExecuteAsync(args);
                }
            }


            await _accelerator.SynchronizeAsync();
            await bufferData.CopyToAsync(data);
        }

        private async Task ExecuteFFT(float[] input, float[] output, int n)
        {
            using var bufferInput = await _accelerator.Memory.AllocateAsync<float>(input.Length);
            using var bufferOutput = await _accelerator.Memory.AllocateAsync<float>(output.Length);

            await bufferInput.CopyFromAsync(input);

            var kernel = new KernelDefinition
            {
                Name = "fft_radix2",
                Source = GetFFTKernel(),
                EntryPoint = "fft_radix2",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            var args = new KernelArguments
            {
                Buffers = new[] { bufferInput, bufferOutput },
                ScalarArguments = new object[] { n }
            };

            await compiled.ExecuteAsync(args);
            await _accelerator.SynchronizeAsync();
            await bufferOutput.CopyToAsync(output);
        }

        private async Task ExecuteHeatDiffusion2D(float[] temperature, int width, int height, float alpha, int iterations)
        {
            await using var bufferTemp1 = await _accelerator.Memory.AllocateAsync<float>(temperature.Length);
            await using var bufferTemp2 = await _accelerator.Memory.AllocateAsync<float>(temperature.Length);

            await bufferTemp1.CopyFromAsync(temperature);
            await bufferTemp2.CopyFromAsync(temperature);

            var kernel = new KernelDefinition
            {
                Name = "heat_diffusion_2d",
                Source = GetHeatDiffusionKernel(),
                EntryPoint = "heat_diffusion_step",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);


            for (var iter = 0; iter < iterations; iter++)
            {
                var args = new KernelArguments
                {
                    Buffers = (iter % 2 == 0)

                        ? new[] { bufferTemp1, bufferTemp2 }
                        : [bufferTemp2, bufferTemp1],
                    ScalarArguments = new object[] { width, height, alpha }
                };
                await compiled.ExecuteAsync(args);
            }


            await _accelerator.SynchronizeAsync();

            // Copy final result back

            if (iterations % 2 == 0)
                await bufferTemp1.CopyToAsync(temperature);
            else
                await bufferTemp2.CopyToAsync(temperature);
        }

        private async Task ExecuteBFS(int[][] edges, int[] distances, int startNode, int numNodes)
        {
            // Flatten edge list for GPU
            var flatEdges = edges.SelectMany(e => e).ToArray();
            var edgeOffsets = new int[numNodes + 1];
            edgeOffsets[0] = 0;
            for (var i = 0; i < numNodes; i++)
            {
                edgeOffsets[i + 1] = edgeOffsets[i] + edges[i].Length;
            }

            await using var bufferEdges = await _accelerator.Memory.AllocateAsync<int>(flatEdges.Length);
            await using var bufferOffsets = await _accelerator.Memory.AllocateAsync<int>(edgeOffsets.Length);
            await using var bufferDistances = await _accelerator.Memory.AllocateAsync<int>(distances.Length);

            await bufferEdges.CopyFromAsync(flatEdges);
            await bufferOffsets.CopyFromAsync(edgeOffsets);
            await bufferDistances.CopyFromAsync(distances);

            var kernel = new KernelDefinition
            {
                Name = "bfs_kernel",
                Source = GetBFSKernel(),
                EntryPoint = "bfs_kernel",
                // Language would be set via source generator
            };

            var compiled = await _accelerator.CompileKernelAsync(kernel);

            // BFS requires multiple iterations

            for (var level = 0; level < numNodes; level++)
            {
                var args = new KernelArguments
                {
                    Buffers = new[] { bufferEdges, bufferOffsets, bufferDistances },
                    ScalarArguments = new object[] { numNodes, level }
                };
                await compiled.ExecuteAsync(args);
                await _accelerator.SynchronizeAsync();
            }


            await bufferDistances.CopyToAsync(distances);
        }

        private static int[][] GenerateRandomGraph(int numNodes, int edgesPerNode)
        {
            var random = new Random(42);
            var edges = new int[numNodes][];


            for (var i = 0; i < numNodes; i++)
            {
                var nodeEdges = new HashSet<int>();

                // Add some random edges

                for (var j = 0; j < edgesPerNode; j++)
                {
                    var target = random.Next(numNodes);
                    if (target != i)
                        _ = nodeEdges.Add(target);
                }

                // Ensure connectivity by connecting to next node

                if (i < numNodes - 1)
                    _ = nodeEdges.Add(i + 1);
                if (i > 0)
                    _ = nodeEdges.Add(i - 1);


                edges[i] = [.. nodeEdges];
            }


            return edges;
        }

        // CUDA kernel source code
        private static string GetTiledMatrixMultiplyKernel() => @"
#define TILE_SIZE 16

extern ""C"" __global__ void tiled_matrix_multiply(
    const float* A, const float* B, float* C,
    int M, int N, int K)
{
    __shared__ float tileA[TILE_SIZE][TILE_SIZE];
    __shared__ float tileB[TILE_SIZE][TILE_SIZE];
    
    int row = blockIdx.y * TILE_SIZE + threadIdx.y;
    int col = blockIdx.x * TILE_SIZE + threadIdx.x;
    
    float sum = 0.0f;
    
    for (int t = 0; t < (K + TILE_SIZE - 1) / TILE_SIZE; t++) {
        if (row < M && t * TILE_SIZE + threadIdx.x < K)
            tileA[threadIdx.y][threadIdx.x] = A[row * K + t * TILE_SIZE + threadIdx.x];
        else
            tileA[threadIdx.y][threadIdx.x] = 0.0f;
            
        if (col < N && t * TILE_SIZE + threadIdx.y < K)
            tileB[threadIdx.y][threadIdx.x] = B[(t * TILE_SIZE + threadIdx.y) * N + col];
        else
            tileB[threadIdx.y][threadIdx.x] = 0.0f;
            
        __syncthreads();
        
        for (int k = 0; k < TILE_SIZE; k++)
            sum += tileA[threadIdx.y][k] * tileB[k][threadIdx.x];
            
        __syncthreads();
    }
    
    if (row < M && col < N)
        C[row * N + col] = sum;
}";

        private static string GetParallelReductionKernel() => @"
extern ""C"" __global__ void parallel_reduction(
    const float* input, float* output, int n)
{
    extern __shared__ float sdata[];
    
    int tid = threadIdx.x;
    int i = blockIdx.x * blockDim.x * 2 + threadIdx.x;
    
    sdata[tid] = (i < n) ? input[i] : 0.0f;
    if (i + blockDim.x < n)
        sdata[tid] += input[i + blockDim.x];
    
    __syncthreads();
    
    for (int s = blockDim.x / 2; s > 32; s >>= 1) {
        if (tid < s)
            sdata[tid] += sdata[tid + s];
        __syncthreads();
    }
    
    if (tid < 32) {
        volatile float* smem = sdata;
        if (blockDim.x >= 64) smem[tid] += smem[tid + 32];
        if (blockDim.x >= 32) smem[tid] += smem[tid + 16];
        if (blockDim.x >= 16) smem[tid] += smem[tid + 8];
        if (blockDim.x >= 8) smem[tid] += smem[tid + 4];
        if (blockDim.x >= 4) smem[tid] += smem[tid + 2];
        if (blockDim.x >= 2) smem[tid] += smem[tid + 1];
    }
    
    if (tid == 0)
        atomicAdd(output, sdata[0]);
}";

        private static string GetPrefixSumKernel() => @"
extern ""C"" __global__ void prefix_sum(
    const float* input, float* output, int n)
{
    extern __shared__ float temp[];
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (idx < n)
        temp[tid] = input[idx];
    else
        temp[tid] = 0.0f;
    
    __syncthreads();
    
    // Up-sweep phase
    for (int stride = 1; stride < blockDim.x; stride *= 2) {
        int index = (tid + 1) * stride * 2 - 1;
        if (index < blockDim.x)
            temp[index] += temp[index - stride];
        __syncthreads();
    }
    
    // Down-sweep phase
    for (int stride = blockDim.x / 4; stride > 0; stride /= 2) {
        __syncthreads();
        int index = (tid + 1) * stride * 2 - 1;
        if (index + stride < blockDim.x)
            temp[index + stride] += temp[index];
    }
    __syncthreads();
    
    if (idx < n)
        output[idx] = temp[tid];
}";

        private static string GetBitonicSortKernel() => @"
extern ""C"" __global__ void bitonic_sort_step(
    float* data, int n, int j, int k)
{
    int i = blockIdx.x * blockDim.x + threadIdx.x;
    int ixj = i ^ j;
    
    if (ixj > i && i < n && ixj < n) {
        if ((i & k) == 0) {
            if (data[i] > data[ixj]) {
                float temp = data[i];
                data[i] = data[ixj];
                data[ixj] = temp;
            }
        } else {
            if (data[i] < data[ixj]) {
                float temp = data[i];
                data[i] = data[ixj];
                data[ixj] = temp;
            }
        }
    }
}";

        private static string GetFFTKernel() => @"
#define PI 3.14159265358979323846f

extern ""C"" __global__ void fft_radix2(
    const float* input, float* output, int n)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx >= n) return;
    
    // Simplified DFT for demonstration (not optimized FFT)
    float real = 0.0f;
    float imag = 0.0f;
    
    for (int k = 0; k < n; k++) {
        float angle = -2.0f * PI * idx * k / n;
        real += input[k * 2] * cosf(angle) - input[k * 2 + 1] * sinf(angle);
        imag += input[k * 2] * sinf(angle) + input[k * 2 + 1] * cosf(angle);
    }
    
    output[idx * 2] = real;
    output[idx * 2 + 1] = imag;
}";

        private static string GetHeatDiffusionKernel() => @"
extern ""C"" __global__ void heat_diffusion_step(
    const float* temp_in, float* temp_out,
    int width, int height, float alpha)
{
    int x = blockIdx.x * blockDim.x + threadIdx.x;
    int y = blockIdx.y * blockDim.y + threadIdx.y;
    
    if (x > 0 && x < width - 1 && y > 0 && y < height - 1) {
        int idx = y * width + x;
        
        float center = temp_in[idx];
        float left = temp_in[idx - 1];
        float right = temp_in[idx + 1];
        float top = temp_in[idx - width];
        float bottom = temp_in[idx + width];
        
        // 5-point stencil for 2D heat equation
        temp_out[idx] = center + alpha * (left + right + top + bottom - 4.0f * center);
    } else if (x < width && y < height) {
        // Boundary conditions (no heat flux)
        temp_out[y * width + x] = temp_in[y * width + x];
    }
}";

        private static string GetBFSKernel() => @"
extern ""C"" __global__ void bfs_kernel(
    const int* edges, const int* offsets, int* distances,
    int num_nodes, int current_level)
{
    int node = blockIdx.x * blockDim.x + threadIdx.x;
    
    if (node < num_nodes && distances[node] == current_level) {
        int start = offsets[node];
        int end = offsets[node + 1];
        
        for (int i = start; i < end; i++) {
            int neighbor = edges[i];
            if (distances[neighbor] == -1) {
                distances[neighbor] = current_level + 1;
            }
        }
    }
}";
    }
}