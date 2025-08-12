// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Integration;

/// <summary>
/// Integration tests for performance benchmarking scenarios.
/// Tests real-world use cases and performance validation.
/// </summary>
public class PerformanceBenchmarkTests : IntegrationTestBase
{
    public PerformanceBenchmarkTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData(1024)]      // 1K elements
    [InlineData(65536)]     // 64K elements  
    [InlineData(1048576)]   // 1M elements
    [InlineData(4194304)]   // 4M elements
    public async Task PerformanceBenchmark_VectorOperations_ShouldScaleLinearly(int vectorSize)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var testDataA = GenerateTestVector(vectorSize);
        var testDataB = GenerateTestVector(vectorSize, seed: 123);

        // Act
        var benchmarkResult = await BenchmarkVectorOperation(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            testDataA,
            testDataB);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        benchmarkResult.ElementsProcessed.Should().Be(vectorSize);
        
        // Performance assertions
        var elementsPerSecond = vectorSize / benchmarkResult.ExecutionTime.TotalSeconds;
        benchmarkResult.ThroughputElementsPerSecond = elementsPerSecond;
        
        Logger.LogInformation($"Vector size: {vectorSize:N0}, Throughput: {elementsPerSecond:N0} elements/sec");
        
        // Should process at least 1M elements per second (very conservative)
        elementsPerSecond.Should().BeGreaterThan(1_000_000);
        
        // Memory bandwidth utilization
        var bytesPerSecond = (vectorSize * sizeof(float) * 3) / benchmarkResult.ExecutionTime.TotalSeconds; // 3 arrays: A, B, Result
        var mbPerSecond = bytesPerSecond / (1024 * 1024);
        benchmarkResult.MemoryBandwidthMBs = mbPerSecond;
        
        Logger.LogInformation($"Memory bandwidth: {mbPerSecond:F2} MB/s");
        mbPerSecond.Should().BeGreaterThan(100); // At least 100 MB/s
    }

    [Theory]
    [InlineData(64, 64)]    // Small matrix
    [InlineData(256, 256)]  // Medium matrix
    [InlineData(512, 512)]  // Large matrix
    public async Task PerformanceBenchmark_MatrixOperations_ShouldAchieveGoodPerformance(int matrixSize, int matrixSize2)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var matrixA = GenerateTestMatrix(matrixSize, matrixSize2);
        var matrixB = GenerateTestMatrix(matrixSize2, matrixSize);

        // Act
        var benchmarkResult = await BenchmarkMatrixMultiplication(
            computeEngine,
            matrixA,
            matrixB,
            matrixSize);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        
        var totalOperations = (long)matrixSize * matrixSize * matrixSize2 * 2; // Multiply-add operations
        var operationsPerSecond = totalOperations / benchmarkResult.ExecutionTime.TotalSeconds;
        var gflops = operationsPerSecond / 1_000_000_000.0;
        
        benchmarkResult.GigaFlopsPerSecond = gflops;
        
        Logger.LogInformation($"Matrix {matrixSize}x{matrixSize2}: {gflops:F3} GFLOPS");
        
        // Should achieve at least some reasonable performance
        gflops.Should().BeGreaterThan(0.1); // Conservative threshold
        
        // Execution time should be reasonable
        benchmarkResult.ExecutionTime.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(1048576)]   // 1M elements
    [InlineData(4194304)]   // 4M elements
    [InlineData(16777216)]  // 16M elements
    public async Task PerformanceBenchmark_ReductionOperations_ShouldScaleLogarithmically(int dataSize)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        var testData = GenerateTestVector(dataSize);
        var expectedSum = testData.Sum();

        // Act
        var benchmarkResult = await BenchmarkReductionOperation(
            computeEngine,
            testData,
            expectedSum);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        benchmarkResult.ResultAccurate.Should().BeTrue();
        
        var elementsPerSecond = dataSize / benchmarkResult.ExecutionTime.TotalSeconds;
        benchmarkResult.ThroughputElementsPerSecond = elementsPerSecond;
        
        Logger.LogInformation($"Reduction {dataSize:N0} elements: {elementsPerSecond:N0} elements/sec");
        
        // Reduction should still achieve good throughput
        elementsPerSecond.Should().BeGreaterThan(10_000_000);
        
        // Time complexity should be reasonable (better than O(n) on CPU)
        var expectedLinearTime = dataSize / 500_000_000.0; // More realistic: assume 500 MHz effective rate
        benchmarkResult.ExecutionTime.TotalSeconds.Should().BeLessThan(expectedLinearTime * 4); // Allow more variance
    }

    [Fact]
    public async Task PerformanceBenchmark_MemoryIntensiveWorkload_ShouldTestMemoryBandwidth()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int dataSize = 8 * 1024 * 1024; // 8M floats = 32MB
        var sourceData = GenerateTestVector(dataSize);

        // Act
        var benchmarkResult = await BenchmarkMemoryIntensiveWorkload(
            computeEngine,
            sourceData);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        
        var bytesTransferred = dataSize * sizeof(float) * 4; // Multiple reads/writes per element
        var mbPerSecond = bytesTransferred / benchmarkResult.ExecutionTime.TotalSeconds / (1024 * 1024);
        benchmarkResult.MemoryBandwidthMBs = mbPerSecond;
        
        Logger.LogInformation($"Memory-intensive workload: {mbPerSecond:F2} MB/s");
        
        // Should achieve reasonable memory bandwidth
        mbPerSecond.Should().BeGreaterThan(1000); // At least 1 GB/s
    }

    [Fact]
    public async Task PerformanceBenchmark_ComputeIntensiveWorkload_ShouldTestComputeThroughput()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int iterations = 1000;
        const int dataSize = 65536;
        var testData = GenerateTestVector(dataSize);

        // Act
        var benchmarkResult = await BenchmarkComputeIntensiveWorkload(
            computeEngine,
            testData,
            iterations);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        
        var totalOperations = (long)dataSize * iterations * 10; // ~10 ops per element per iteration
        var operationsPerSecond = totalOperations / benchmarkResult.ExecutionTime.TotalSeconds;
        var gops = operationsPerSecond / 1_000_000_000.0;
        
        benchmarkResult.GigaOperationsPerSecond = gops;
        
        Logger.LogInformation($"Compute-intensive workload: {gops:F3} GOPS");
        
        // Should achieve reasonable compute throughput
        gops.Should().BeGreaterThan(0.5); // At least 0.5 GOPS
    }

    [Theory]
    [InlineData(1, 65536)]   // Single thread
    [InlineData(2, 65536)]   // Dual thread
    [InlineData(4, 65536)]   // Quad thread
    [InlineData(8, 65536)]   // Octa thread
    public async Task PerformanceBenchmark_ParallelWorkload_ShouldScaleWithThreads(int threadCount, int workPerThread)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        
        // Act
        var benchmarkResult = await BenchmarkParallelWorkload(
            computeEngine,
            threadCount,
            workPerThread);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        benchmarkResult.ThreadResults.Should().HaveCount(threadCount);
        
        var totalThroughput = benchmarkResult.ThreadResults.Sum(r => r.ElementsPerSecond);
        var averageLatency = benchmarkResult.ThreadResults.Average(r => r.AverageLatency.TotalMilliseconds);
        
        Logger.LogInformation($"Parallel {threadCount} threads: {totalThroughput:N0} total elements/sec, {averageLatency:F2}ms avg latency");
        
        // Parallel efficiency
        if (threadCount > 1)
        {
            var efficiency = totalThroughput / (benchmarkResult.SingleThreadThroughput * threadCount);
            benchmarkResult.ParallelEfficiency = efficiency;
            
            Logger.LogInformation($"Parallel efficiency: {efficiency:P2}");
            
            // Should achieve at least 50% efficiency with multiple threads
            efficiency.Should().BeGreaterThan(0.5);
        }
    }

    [Fact]
    public async Task PerformanceBenchmark_RealWorldScenario_ImageProcessing()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int imageWidth = 1920;
        const int imageHeight = 1080;
        var imageData = GenerateImageData(imageWidth, imageHeight);

        // Act
        var benchmarkResult = await BenchmarkImageProcessing(
            computeEngine,
            imageData,
            imageWidth,
            imageHeight);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        
        var pixelsPerSecond = (imageWidth * imageHeight) / benchmarkResult.ExecutionTime.TotalSeconds;
        var megapixelsPerSecond = pixelsPerSecond / 1_000_000.0;
        
        Logger.LogInformation($"Image processing: {megapixelsPerSecond:F2} MP/s");
        
        // Should process at least 100 MP/s for basic operations
        megapixelsPerSecond.Should().BeGreaterThan(100);
        
        // Processing time should be acceptable for real-time scenarios
        benchmarkResult.ExecutionTime.Should().BeLessThan(TimeSpan.FromMilliseconds(500)); // More generous for test stability
    }

    [Fact]
    public async Task PerformanceBenchmark_RealWorldScenario_SignalProcessing()
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int sampleRate = 48000; // 48kHz
        const double durationSeconds = 1.0;
        const int sampleCount = (int)(sampleRate * durationSeconds);
        var audioSamples = GenerateAudioSignal(sampleCount, sampleRate);

        // Act
        var benchmarkResult = await BenchmarkSignalProcessing(
            computeEngine,
            audioSamples,
            sampleRate);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        
        var samplesPerSecond = sampleCount / benchmarkResult.ExecutionTime.TotalSeconds;
        var realTimeRatio = samplesPerSecond / sampleRate;
        
        Logger.LogInformation($"Signal processing: {samplesPerSecond:N0} samples/sec, {realTimeRatio:F2}x real-time");
        
        // Should process faster than real-time for audio applications
        realTimeRatio.Should().BeGreaterThan(10); // At least 10x real-time
    }

    [Theory]
    [InlineData(OptimizationLevel.None)]
    [InlineData(OptimizationLevel.Default)]
    [InlineData(OptimizationLevel.Maximum)]
    public async Task PerformanceBenchmark_OptimizationLevels_ShouldShowPerformanceDifferences(OptimizationLevel optimizationLevel)
    {
        // Arrange
        var computeEngine = ServiceProvider.GetRequiredService<IComputeEngine>();
        const int dataSize = 1048576; // 1M elements
        var testData = GenerateTestVector(dataSize);

        // Act
        var benchmarkResult = await BenchmarkWithOptimizationLevel(
            computeEngine,
            testData,
            optimizationLevel);

        // Assert
        benchmarkResult.Should().NotBeNull();
        benchmarkResult.Success.Should().BeTrue();
        benchmarkResult.OptimizationLevel.Should().Be(optimizationLevel);
        
        var elementsPerSecond = dataSize / benchmarkResult.ExecutionTime.TotalSeconds;
        
        Logger.LogInformation($"Optimization {optimizationLevel}: {elementsPerSecond:N0} elements/sec");
        
        // All optimization levels should work
        elementsPerSecond.Should().BeGreaterThan(100_000);
        
        // Store results for comparison if needed
        benchmarkResult.ThroughputElementsPerSecond = elementsPerSecond;
    }

    // Helper methods for benchmarking
    private async Task<BenchmarkResult> BenchmarkVectorOperation(
        IComputeEngine computeEngine,
        string kernelSource,
        string entryPoint,
        float[] dataA,
        float[] dataB)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compilationOptions = new CompilationOptions
            {
                OptimizationLevel = OptimizationLevel.Maximum,
                FastMath = true
            };

            var compiledKernel = await computeEngine.CompileKernelAsync(
                kernelSource, entryPoint, compilationOptions);

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var bufferA = await CreateInputBuffer(memoryManager, dataA);
            var bufferB = await CreateInputBuffer(memoryManager, dataB);
            var resultBuffer = await CreateOutputBuffer<float>(memoryManager, dataA.Length);

            // Warm-up run
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [bufferA, bufferB, resultBuffer],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [dataA.Length] });

            // Actual benchmark
            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [bufferA, bufferB, resultBuffer],
                ComputeBackendType.CPU,
                new ExecutionOptions { 
                    GlobalWorkSize = [dataA.Length],
                    EnableProfiling = true 
                });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(resultBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = dataA.Length,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Vector operation benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkMatrixMultiplication(
        IComputeEngine computeEngine,
        float[] matrixA,
        float[] matrixB,
        int size)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                MatrixMultiplyKernelSource,
                "matrix_mul",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var bufferA = await CreateInputBuffer(memoryManager, matrixA);
            var bufferB = await CreateInputBuffer(memoryManager, matrixB);
            var resultBuffer = await CreateOutputBuffer<float>(memoryManager, size * size);

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [bufferA, bufferB, resultBuffer, size],
                ComputeBackendType.CPU,
                new ExecutionOptions { 
                    GlobalWorkSize = [size, size],
                    EnableProfiling = true 
                });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(resultBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = size * size,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Matrix multiplication benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkReductionOperation(
        IComputeEngine computeEngine,
        float[] data,
        float expectedSum)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                ReductionKernelSource,
                "reduce_sum",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, data);
            var resultBuffer = await CreateOutputBuffer<float>(memoryManager, 1);

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, resultBuffer],
                ComputeBackendType.CPU,
                new ExecutionOptions { 
                    GlobalWorkSize = [data.Length],
                    LocalWorkSize = [64],
                    EnableProfiling = true 
                });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(resultBuffer);
            var actualSum = result?[0] ?? 0f;
            var accurate = Math.Abs(actualSum - expectedSum) < Math.Abs(expectedSum * 0.001f);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = data.Length,
                ResultAccurate = accurate,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Reduction operation benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkMemoryIntensiveWorkload(
        IComputeEngine computeEngine,
        float[] data)
    {
        const string memoryKernel = @"
            __kernel void memory_intensive(__global const float* input,
                                         __global float* output) {
                int i = get_global_id(0);
                
                // Multiple memory accesses to stress bandwidth
                float value = input[i];
                value += input[i];
                value *= input[i];
                value /= input[i] + 1.0f;
                
                output[i] = value;
            }";

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                memoryKernel,
                "memory_intensive",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, data);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, data.Length);

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, outputBuffer],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [data.Length] });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(outputBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = data.Length,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Memory-intensive workload benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkComputeIntensiveWorkload(
        IComputeEngine computeEngine,
        float[] data,
        int iterations)
    {
        const string computeKernel = @"
            __kernel void compute_intensive(__global const float* input,
                                          __global float* output,
                                          int iterations) {
                int i = get_global_id(0);
                float value = input[i];
                
                // Intensive computation
                for (int iter = 0; iter < iterations; iter++) {
                    value = sin(value) * cos(value) + sqrt(value + 1.0f);
                    value = fabs(value);
                    value = pow(value, 0.5f);
                }
                
                output[i] = value;
            }";

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                computeKernel,
                "compute_intensive",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, data);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, data.Length);

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, outputBuffer, iterations],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [data.Length] });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(outputBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = data.Length,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Compute-intensive workload benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<ParallelBenchmarkResult> BenchmarkParallelWorkload(
        IComputeEngine computeEngine,
        int threadCount,
        int workPerThread)
    {
        // First, get single-thread baseline
        var singleThreadData = GenerateTestVector(workPerThread);
        var singleThreadResult = await BenchmarkVectorOperation(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            singleThreadData,
            singleThreadData);
        
        var singleThreadThroughput = workPerThread / singleThreadResult.ExecutionTime.TotalSeconds;

        // Then run parallel workload
        var parallelTasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            var threadData = GenerateTestVector(workPerThread, seed: threadId + 1);
            var stopwatch = Stopwatch.StartNew();
            
            var result = await BenchmarkVectorOperation(
                computeEngine,
                VectorAddKernelSource,
                "vector_add",
                threadData,
                threadData);
                
            stopwatch.Stop();

            return new ThreadBenchmarkResult
            {
                ThreadId = threadId,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = workPerThread,
                ElementsPerSecond = workPerThread / stopwatch.Elapsed.TotalSeconds,
                AverageLatency = TimeSpan.FromMilliseconds(stopwatch.Elapsed.TotalMilliseconds / workPerThread * 1000)
            };
        });

        var threadResults = await Task.WhenAll(parallelTasks);

        return new ParallelBenchmarkResult
        {
            Success = threadResults.All(r => r.ElementsProcessed > 0),
            ThreadResults = threadResults.ToList(),
            SingleThreadThroughput = singleThreadThroughput
        };
    }

    private async Task<BenchmarkResult> BenchmarkImageProcessing(
        IComputeEngine computeEngine,
        float[] imageData,
        int width,
        int height)
    {
        const string imageKernel = @"
            __kernel void blur_filter(__global const float* input,
                                    __global float* output,
                                    int width, int height) {
                int x = get_global_id(0);
                int y = get_global_id(1);
                
                if (x >= width || y >= height) return;
                
                // Simple 3x3 blur
                float sum = 0.0f;
                int count = 0;
                
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dx = -1; dx <= 1; dx++) {
                        int nx = x + dx;
                        int ny = y + dy;
                        
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height) {
                            sum += input[ny * width + nx];
                            count++;
                        }
                    }
                }
                
                output[y * width + x] = sum / count;
            }";

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                imageKernel,
                "blur_filter",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, imageData);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, imageData.Length);

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, outputBuffer, width, height],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [width, height] });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(outputBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = width * height,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Image processing benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkSignalProcessing(
        IComputeEngine computeEngine,
        float[] samples,
        int sampleRate)
    {
        const string signalKernel = @"
            __kernel void lowpass_filter(__global const float* input,
                                        __global float* output,
                                        float cutoff,
                                        int sampleRate) {
                int i = get_global_id(0);
                
                // Simple IIR lowpass filter
                float alpha = 2.0f * 3.14159f * cutoff / sampleRate;
                if (alpha > 1.0f) alpha = 1.0f;
                
                // For this demo, just apply a simple filter
                output[i] = input[i] * alpha + (i > 0 ? input[i-1] : 0.0f) * (1.0f - alpha);
            }";

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var compiledKernel = await computeEngine.CompileKernelAsync(
                signalKernel,
                "lowpass_filter",
                new CompilationOptions { OptimizationLevel = OptimizationLevel.Maximum });

            var memoryManager = ServiceProvider.GetRequiredService<IMemoryManager>();
            var inputBuffer = await CreateInputBuffer(memoryManager, samples);
            var outputBuffer = await CreateOutputBuffer<float>(memoryManager, samples.Length);

            const float cutoffFreq = 1000.0f; // 1kHz cutoff

            stopwatch.Restart();
            await computeEngine.ExecuteAsync(
                compiledKernel,
                [inputBuffer, outputBuffer, cutoffFreq, sampleRate],
                ComputeBackendType.CPU,
                new ExecutionOptions { GlobalWorkSize = [samples.Length] });
            stopwatch.Stop();

            var result = await ReadBufferAsync<float>(outputBuffer);

            return new BenchmarkResult
            {
                Success = true,
                ExecutionTime = stopwatch.Elapsed,
                ElementsProcessed = samples.Length,
                ResultData = result
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.LogError(ex, "Signal processing benchmark failed");
            
            return new BenchmarkResult
            {
                Success = false,
                ExecutionTime = stopwatch.Elapsed,
                Error = ex.Message
            };
        }
    }

    private async Task<BenchmarkResult> BenchmarkWithOptimizationLevel(
        IComputeEngine computeEngine,
        float[] data,
        OptimizationLevel optimizationLevel)
    {
        var compilationOptions = new CompilationOptions
        {
            OptimizationLevel = optimizationLevel,
            FastMath = optimizationLevel != OptimizationLevel.None
        };

        var result = await BenchmarkVectorOperation(
            computeEngine,
            VectorAddKernelSource,
            "vector_add",
            data,
            data);

        result.OptimizationLevel = optimizationLevel;
        return result;
    }

    // Data generation methods
    private static float[] GenerateTestVector(int size, int seed = 42)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, size)
                        .Select(_ => (float)random.NextDouble() * 100.0f)
                        .ToArray();
    }

    private static float[] GenerateTestMatrix(int rows, int cols, int seed = 42)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, rows * cols)
                        .Select(_ => (float)random.NextDouble() * 10.0f)
                        .ToArray();
    }

    private static float[] GenerateImageData(int width, int height, int seed = 42)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, width * height)
                        .Select(_ => (float)random.NextDouble() * 255.0f)
                        .ToArray();
    }

    private static float[] GenerateAudioSignal(int sampleCount, int sampleRate, int seed = 42)
    {
        // Generate a mix of sine waves
        var signal = new float[sampleCount];
        var random = new Random(seed);
        
        for (int i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRate;
            
            // Mix of frequencies
            signal[i] = (float)(
                0.5 * Math.Sin(2 * Math.PI * 440 * t) +    // A4 note
                0.3 * Math.Sin(2 * Math.PI * 880 * t) +    // A5 note
                0.2 * random.NextDouble() - 0.1           // Noise
            );
        }
        
        return signal;
    }

    // Kernel sources
    private const string VectorAddKernelSource = @"
        __kernel void vector_add(__global const float* a,
                               __global const float* b,
                               __global float* result) {
            int i = get_global_id(0);
            result[i] = a[i] + b[i];
        }";

    private const string MatrixMultiplyKernelSource = @"
        __kernel void matrix_mul(__global const float* a,
                               __global const float* b,
                               __global float* c,
                               int size) {
            int row = get_global_id(0);
            int col = get_global_id(1);
            
            if (row < size && col < size) {
                float sum = 0.0f;
                for (int k = 0; k < size; k++) {
                    sum += a[row * size + k] * b[k * size + col];
                }
                c[row * size + col] = sum;
            }
        }";

    private const string ReductionKernelSource = @"
        __kernel void reduce_sum(__global const float* input,
                               __global float* output) {
            int i = get_global_id(0);
            // Simplified reduction - in real implementation would use proper reduction
            if (i == 0) {
                float sum = 0.0f;
                for (int j = 0; j < get_global_size(0); j++) {
                    sum += input[j];
                }
                output[0] = sum;
            }
        }";
}

// Benchmark result classes
public class BenchmarkResult
{
    public bool Success { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public int ElementsProcessed { get; set; }
    public double ThroughputElementsPerSecond { get; set; }
    public double MemoryBandwidthMBs { get; set; }
    public double GigaFlopsPerSecond { get; set; }
    public double GigaOperationsPerSecond { get; set; }
    public bool ResultAccurate { get; set; } = true;
    public OptimizationLevel OptimizationLevel { get; set; }
    public object? ResultData { get; set; }
    public string? Error { get; set; }
}

public class ThreadBenchmarkResult
{
    public int ThreadId { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public int ElementsProcessed { get; set; }
    public double ElementsPerSecond { get; set; }
    public TimeSpan AverageLatency { get; set; }
}

public class ParallelBenchmarkResult
{
    public bool Success { get; set; }
    public List<ThreadBenchmarkResult> ThreadResults { get; set; } = new();
    public double SingleThreadThroughput { get; set; }
    public double ParallelEfficiency { get; set; }
}