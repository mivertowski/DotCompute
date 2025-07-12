// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Compute;
using DotCompute.Core.Memory;
using DotCompute.Integration.Tests.Fixtures;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace DotCompute.Integration.Tests.Scenarios;

[Collection(nameof(IntegrationTestCollection))]
public class RealWorldScenarioTests
{
    private readonly IntegrationTestFixture _fixture;

    public RealWorldScenarioTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Image_Processing_Pipeline_Should_Work_End_To_End()
    {
        // Arrange - Create a simple test image
        const int width = 256;
        const int height = 256;
        var imageData = new float[width * height * 3]; // RGB

        // Generate a simple gradient image
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var idx = (y * width + x) * 3;
                imageData[idx] = (float)x / width;     // R
                imageData[idx + 1] = (float)y / height; // G
                imageData[idx + 2] = 0.5f;             // B
            }
        }

        // Step 1: CPU preprocessing - convert to grayscale
        const string grayscaleKernel = @"
            kernel void rgb_to_grayscale(global float* input, global float* output, int pixels) {
                int i = get_global_id(0);
                if (i < pixels) {
                    int inputIdx = i * 3;
                    float r = input[inputIdx];
                    float g = input[inputIdx + 1];
                    float b = input[inputIdx + 2];
                    // Standard luminance formula
                    output[i] = 0.299f * r + 0.587f * g + 0.114f * b;
                }
            }";

        // Step 2: Edge detection (Sobel operator)
        const string edgeDetectionKernel = @"
            kernel void sobel_edge_detection(global float* input, global float* output, int width, int height) {
                int x = get_global_id(0);
                int y = get_global_id(1);
                
                if (x > 0 && x < width - 1 && y > 0 && y < height - 1) {
                    int idx = y * width + x;
                    
                    // Sobel X kernel
                    float gx = -input[(y-1)*width + (x-1)] + input[(y-1)*width + (x+1)]
                              -2*input[y*width + (x-1)] + 2*input[y*width + (x+1)]
                              -input[(y+1)*width + (x-1)] + input[(y+1)*width + (x+1)];
                    
                    // Sobel Y kernel
                    float gy = -input[(y-1)*width + (x-1)] - 2*input[(y-1)*width + x] - input[(y-1)*width + (x+1)]
                              +input[(y+1)*width + (x-1)] + 2*input[(y+1)*width + x] + input[(y+1)*width + (x+1)];
                    
                    // Magnitude
                    output[idx] = sqrt(gx*gx + gy*gy);
                }
            }";

        // Step 3: Gaussian blur for smoothing
        const string gaussianBlurKernel = @"
            kernel void gaussian_blur(global float* input, global float* output, int width, int height) {
                int x = get_global_id(0);
                int y = get_global_id(1);
                
                if (x >= 2 && x < width - 2 && y >= 2 && y < height - 2) {
                    float sum = 0.0f;
                    float weights[5][5] = {
                        {1, 4, 6, 4, 1},
                        {4, 16, 24, 16, 4},
                        {6, 24, 36, 24, 6},
                        {4, 16, 24, 16, 4},
                        {1, 4, 6, 4, 1}
                    };
                    float weightSum = 256.0f;
                    
                    for (int dy = -2; dy <= 2; dy++) {
                        for (int dx = -2; dx <= 2; dx++) {
                            sum += input[(y + dy) * width + (x + dx)] * weights[dy + 2][dx + 2];
                        }
                    }
                    
                    output[y * width + x] = sum / weightSum;
                }
            }";

        // Act - Execute the complete pipeline
        using var inputBuffer = await _fixture.CreateBufferAsync(imageData, MemoryLocation.Host);
        using var grayscaleBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(width * height, MemoryLocation.Host);
        using var edgeBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(width * height, MemoryLocation.Host);
        using var blurredBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(width * height, MemoryLocation.Host);

        // Execute pipeline steps
        var grayscaleKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(grayscaleKernel);
        await _fixture.ComputeEngine.ExecuteAsync(grayscaleKernelCompiled,
            new object[] { inputBuffer, grayscaleBuffer, width * height },
            ComputeBackendType.CPU);

        var edgeKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(edgeDetectionKernel);
        await _fixture.ComputeEngine.ExecuteAsync(edgeKernelCompiled,
            new object[] { grayscaleBuffer, edgeBuffer, width, height },
            ComputeBackendType.CPU);

        var blurKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(gaussianBlurKernel);
        await _fixture.ComputeEngine.ExecuteAsync(blurKernelCompiled,
            new object[] { edgeBuffer, blurredBuffer, width, height },
            ComputeBackendType.CPU);

        var result = await blurredBuffer.ReadAsync();

        // Assert
        result.Should().HaveCount(width * height);
        result.Should().AllSatisfy(pixel => pixel.Should().BeGreaterOrEqualTo(0.0f).And.BeLessOrEqualTo(1.0f));

        // Verify edge detection worked by checking that edges have higher values
        var centerPixel = result[(height / 2) * width + (width / 2)];
        centerPixel.Should().BeGreaterThan(0.0f, "Center should have some edge response after processing");

        _fixture.Logger.LogInformation($"Image processing pipeline completed. Output range: [{result.Min():F3}, {result.Max():F3}]");
    }

    [Fact]
    public async Task Scientific_Computing_Monte_Carlo_Simulation()
    {
        // Monte Carlo estimation of π using random points in a unit circle
        const int numSamples = 1000000;
        
        const string monteCarloKernel = @"
            // Simple linear congruential generator for pseudo-random numbers
            float random(int seed, int index) {
                int a = 1664525;
                int c = 1013904223;
                int m = 2147483647; // 2^31 - 1
                
                int rnd = seed;
                for (int i = 0; i <= index; i++) {
                    rnd = (a * rnd + c) % m;
                }
                return (float)rnd / (float)m;
            }
            
            kernel void monte_carlo_pi(global int* results, int numSamples, int seed) {
                int i = get_global_id(0);
                if (i < numSamples) {
                    float x = random(seed, i * 2) * 2.0f - 1.0f;     // [-1, 1]
                    float y = random(seed, i * 2 + 1) * 2.0f - 1.0f; // [-1, 1]
                    
                    // Check if point is inside unit circle
                    if (x*x + y*y <= 1.0f) {
                        results[i] = 1;
                    } else {
                        results[i] = 0;
                    }
                }
            }";

        // Reduction kernel to sum the results
        const string reductionKernel = @"
            kernel void sum_reduction(global int* input, global int* output, int size) {
                int tid = get_local_id(0);
                int bid = get_group_id(0);
                int blockSize = get_local_size(0);
                int i = bid * blockSize + tid;
                
                local int shared[256]; // Assuming max block size of 256
                
                // Load data into shared memory
                shared[tid] = (i < size) ? input[i] : 0;
                barrier(CLK_LOCAL_MEM_FENCE);
                
                // Perform reduction in shared memory
                for (int stride = blockSize / 2; stride > 0; stride /= 2) {
                    if (tid < stride) {
                        shared[tid] += shared[tid + stride];
                    }
                    barrier(CLK_LOCAL_MEM_FENCE);
                }
                
                // Write result for this block
                if (tid == 0) {
                    output[bid] = shared[0];
                }
            }";

        // Execute Monte Carlo simulation
        using var resultsBuffer = await _fixture.MemoryManager.CreateBufferAsync<int>(numSamples, MemoryLocation.Host);
        
        var monteCarloKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(monteCarloKernel);
        var seed = Environment.TickCount;
        
        var startTime = DateTime.UtcNow;
        await _fixture.ComputeEngine.ExecuteAsync(monteCarloKernelCompiled,
            new object[] { resultsBuffer, numSamples, seed },
            ComputeBackendType.CPU);
        var endTime = DateTime.UtcNow;

        var results = await resultsBuffer.ReadAsync();
        var insideCircle = results.Sum();
        var estimatedPi = 4.0 * insideCircle / numSamples;

        // Assert
        Math.Abs(estimatedPi - Math.PI).Should().BeLessThan(0.1, 
            "Monte Carlo estimation should be reasonably close to π");

        var executionTime = endTime - startTime;
        _fixture.Logger.LogInformation($"Monte Carlo π estimation: {estimatedPi:F6} (actual: {Math.PI:F6})");
        _fixture.Logger.LogInformation($"Samples inside circle: {insideCircle}/{numSamples}");
        _fixture.Logger.LogInformation($"Execution time: {executionTime.TotalMilliseconds:F2}ms");
        _fixture.Logger.LogInformation($"Throughput: {numSamples / executionTime.TotalSeconds:F0} samples/sec");
    }

    [Fact]
    public async Task Machine_Learning_Matrix_Operations_Pipeline()
    {
        // Simulate a simple neural network forward pass with matrix operations
        const int inputSize = 784;   // 28x28 image
        const int hiddenSize = 128;
        const int outputSize = 10;   // 10 classes
        const int batchSize = 32;

        // Matrix multiplication kernel (simplified)
        const string matmulKernel = @"
            kernel void matrix_multiply(global float* A, global float* B, global float* C,
                                       int M, int N, int K) {
                int row = get_global_id(0);
                int col = get_global_id(1);
                
                if (row < M && col < N) {
                    float sum = 0.0f;
                    for (int k = 0; k < K; k++) {
                        sum += A[row * K + k] * B[k * N + col];
                    }
                    C[row * N + col] = sum;
                }
            }";

        // ReLU activation kernel
        const string reluKernel = @"
            kernel void relu_activation(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    output[i] = fmax(0.0f, input[i]);
                }
            }";

        // Softmax kernel (simplified version)
        const string softmaxKernel = @"
            kernel void softmax(global float* input, global float* output, int batchSize, int classSize) {
                int batch = get_global_id(0);
                if (batch < batchSize) {
                    int offset = batch * classSize;
                    
                    // Find max for numerical stability
                    float maxVal = input[offset];
                    for (int i = 1; i < classSize; i++) {
                        maxVal = fmax(maxVal, input[offset + i]);
                    }
                    
                    // Compute exponentials and sum
                    float sum = 0.0f;
                    for (int i = 0; i < classSize; i++) {
                        float exp_val = exp(input[offset + i] - maxVal);
                        output[offset + i] = exp_val;
                        sum += exp_val;
                    }
                    
                    // Normalize
                    for (int i = 0; i < classSize; i++) {
                        output[offset + i] /= sum;
                    }
                }
            }";

        // Generate test data
        var random = new Random(42);
        var inputData = new float[batchSize * inputSize];
        var weights1 = new float[inputSize * hiddenSize];
        var weights2 = new float[hiddenSize * outputSize];

        // Initialize with random values
        for (int i = 0; i < inputData.Length; i++)
            inputData[i] = (float)(random.NextDouble() * 2 - 1);
        for (int i = 0; i < weights1.Length; i++)
            weights1[i] = (float)(random.NextGaussian() * 0.1);
        for (int i = 0; i < weights2.Length; i++)
            weights2[i] = (float)(random.NextGaussian() * 0.1);

        // Create buffers
        using var inputBuffer = await _fixture.CreateBufferAsync(inputData);
        using var weights1Buffer = await _fixture.CreateBufferAsync(weights1);
        using var weights2Buffer = await _fixture.CreateBufferAsync(weights2);
        using var hidden1Buffer = await _fixture.MemoryManager.CreateBufferAsync<float>(batchSize * hiddenSize);
        using var hidden2Buffer = await _fixture.MemoryManager.CreateBufferAsync<float>(batchSize * hiddenSize);
        using var output1Buffer = await _fixture.MemoryManager.CreateBufferAsync<float>(batchSize * outputSize);
        using var outputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(batchSize * outputSize);

        // Compile kernels
        var matmulKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(matmulKernel);
        var reluKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(reluKernel);
        var softmaxKernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(softmaxKernel);

        var startTime = DateTime.UtcNow;

        // Forward pass
        // 1. Input -> Hidden (matrix multiplication)
        await _fixture.ComputeEngine.ExecuteAsync(matmulKernelCompiled,
            new object[] { inputBuffer, weights1Buffer, hidden1Buffer, batchSize, hiddenSize, inputSize },
            ComputeBackendType.CPU);

        // 2. ReLU activation
        await _fixture.ComputeEngine.ExecuteAsync(reluKernelCompiled,
            new object[] { hidden1Buffer, hidden2Buffer, batchSize * hiddenSize },
            ComputeBackendType.CPU);

        // 3. Hidden -> Output (matrix multiplication)
        await _fixture.ComputeEngine.ExecuteAsync(matmulKernelCompiled,
            new object[] { hidden2Buffer, weights2Buffer, output1Buffer, batchSize, outputSize, hiddenSize },
            ComputeBackendType.CPU);

        // 4. Softmax activation
        await _fixture.ComputeEngine.ExecuteAsync(softmaxKernelCompiled,
            new object[] { output1Buffer, outputBuffer, batchSize, outputSize },
            ComputeBackendType.CPU);

        var endTime = DateTime.UtcNow;

        // Retrieve results
        var finalOutput = await outputBuffer.ReadAsync();

        // Assert
        finalOutput.Should().HaveCount(batchSize * outputSize);

        // Verify softmax properties: each batch should sum to 1
        for (int batch = 0; batch < batchSize; batch++)
        {
            var batchSum = 0.0f;
            for (int cls = 0; cls < outputSize; cls++)
            {
                var value = finalOutput[batch * outputSize + cls];
                value.Should().BeGreaterOrEqualTo(0.0f, "Softmax outputs should be non-negative");
                value.Should().BeLessOrEqualTo(1.0f, "Softmax outputs should be <= 1");
                batchSum += value;
            }
            batchSum.Should().BeApproximately(1.0f, 0.01f, $"Batch {batch} softmax should sum to 1");
        }

        var executionTime = endTime - startTime;
        var throughput = batchSize / executionTime.TotalSeconds;
        
        _fixture.Logger.LogInformation($"Neural network forward pass completed");
        _fixture.Logger.LogInformation($"Batch size: {batchSize}, Input: {inputSize}, Hidden: {hiddenSize}, Output: {outputSize}");
        _fixture.Logger.LogInformation($"Execution time: {executionTime.TotalMilliseconds:F2}ms");
        _fixture.Logger.LogInformation($"Throughput: {throughput:F0} samples/sec");

        // Sample output distribution for first batch
        var firstBatchOutput = finalOutput.Take(outputSize).ToArray();
        var maxClass = Array.IndexOf(firstBatchOutput, firstBatchOutput.Max());
        _fixture.Logger.LogInformation($"First batch prediction: class {maxClass} with confidence {firstBatchOutput[maxClass]:F4}");
    }

    [Fact]
    public async Task Cross_Platform_Compatibility_Test()
    {
        // Test the same computation across different backends and verify consistency
        const string testKernel = @"
            kernel void cross_platform_test(global float* input, global float* output, int size) {
                int i = get_global_id(0);
                if (i < size) {
                    float x = input[i];
                    // Complex computation that might expose precision differences
                    output[i] = sin(x) * cos(x) + sqrt(fabs(x)) - log(fabs(x) + 1.0f) + x * x * 0.5f;
                }
            }";

        var testData = Enumerable.Range(1, 1000).Select(i => (float)i * 0.01f).ToArray();
        
        // Test on CPU
        using var inputBuffer = await _fixture.CreateBufferAsync(testData);
        using var cpuOutputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length);

        var kernelCompiled = await _fixture.ComputeEngine.CompileKernelAsync(testKernel);
        
        await _fixture.ComputeEngine.ExecuteAsync(kernelCompiled,
            new object[] { inputBuffer, cpuOutputBuffer, testData.Length },
            ComputeBackendType.CPU);

        var cpuResult = await cpuOutputBuffer.ReadAsync();

        // Test on CUDA if available
        if (_fixture.IsCudaAvailable)
        {
            using var cudaInputBuffer = await _fixture.CreateBufferAsync(testData, MemoryLocation.Device);
            using var cudaOutputBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length, MemoryLocation.Device);
            using var cudaResultBuffer = await _fixture.MemoryManager.CreateBufferAsync<float>(testData.Length, MemoryLocation.Host);

            await _fixture.ComputeEngine.ExecuteAsync(kernelCompiled,
                new object[] { cudaInputBuffer, cudaOutputBuffer, testData.Length },
                ComputeBackendType.CUDA);

            await _fixture.MemoryManager.CopyAsync(cudaOutputBuffer, cudaResultBuffer);
            var cudaResult = await cudaResultBuffer.ReadAsync();

            // Compare results
            for (int i = 0; i < Math.Min(100, testData.Length); i++)
            {
                cudaResult[i].Should().BeApproximately(cpuResult[i], 0.001f,
                    $"CUDA and CPU results should match within tolerance at index {i}");
            }

            _fixture.Logger.LogInformation("Cross-platform compatibility verified between CPU and CUDA backends");
        }
        else
        {
            _fixture.Logger.LogInformation("CUDA not available, cross-platform test completed with CPU only");
        }

        // Verify results are reasonable
        cpuResult.Should().NotBeNull();
        cpuResult.Should().HaveCount(testData.Length);
        cpuResult.Should().AllSatisfy(result => float.IsFinite(result).Should().BeTrue("All results should be finite"));
    }
}

// Extension method for Gaussian random numbers
public static class RandomExtensions
{
    public static double NextGaussian(this Random random, double mean = 0.0, double stdDev = 1.0)
    {
        // Box-Muller transform
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}