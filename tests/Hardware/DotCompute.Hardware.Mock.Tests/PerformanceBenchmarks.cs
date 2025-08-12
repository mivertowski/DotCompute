using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Tests.Shared;
using DotCompute.Tests.Shared.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Tests.Hardware;

/// <summary>
/// Performance benchmark tests for real hardware.
/// These tests measure actual performance on available hardware.
/// </summary>
public class PerformanceBenchmarks : IClassFixture<AcceleratorTestFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly AcceleratorTestFixture _fixture;

    public PerformanceBenchmarks(ITestOutputHelper output, AcceleratorTestFixture fixture)
    {
        _output = output;
        _fixture = fixture;
    }

    [Theory]
    [InlineData(1024)]        // 1K elements
    [InlineData(10240)]       // 10K elements  
    [InlineData(102400)]      // 100K elements
    [InlineData(1024000)]     // 1M elements
    [InlineData(10240000)]    // 10M elements
    public async Task Benchmark_VectorAddition_Throughput(int vectorSize)
    {
        _output.WriteLine($"=== Vector Addition Benchmark (N={vectorSize:N0}) ===");
        
        var a = TestDataGenerators.GenerateRandomVector(vectorSize);
        var b = TestDataGenerators.GenerateRandomVector(vectorSize);
        var c = new float[vectorSize];
        
        // Warmup
        for (int i = 0; i < 10; i++)
        {
            VectorAddCPU(a, b, c);
        }
        
        // Benchmark
        const int iterations = 100;
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            VectorAddCPU(a, b, c);
        }
        
        stopwatch.Stop();
        
        var bytesProcessed = (long)vectorSize * sizeof(float) * 3 * iterations; // 3 arrays
        var throughputGBps = bytesProcessed / (stopwatch.Elapsed.TotalSeconds * 1e9);
        var opsPerSecond = (double)vectorSize * iterations / stopwatch.Elapsed.TotalSeconds;
        
        _output.WriteLine($"CPU Results:");
        _output.WriteLine($"  Time: {stopwatch.ElapsedMilliseconds}ms for {iterations} iterations");
        _output.WriteLine($"  Throughput: {throughputGBps:F2} GB/s");
        _output.WriteLine($"  Operations: {opsPerSecond / 1e9:F2} GOPS");
        
        // TODO: Add GPU benchmarks when hardware backend is available
        if (AcceleratorTestFixture.IsCudaAvailable())
        {
            _output.WriteLine($"CUDA Results: [Pending implementation]");
        }
        
        if (AcceleratorTestFixture.IsOpenCLAvailable())
        {
            _output.WriteLine($"OpenCL Results: [Pending implementation]");
        }
        
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    public async Task Benchmark_MatrixMultiplication_GFLOPS(int matrixSize)
    {
        _output.WriteLine($"=== Matrix Multiplication Benchmark ({matrixSize}x{matrixSize}) ===");
        
        var a = TestDataGenerators.GenerateRandomMatrix(matrixSize, matrixSize);
        var b = TestDataGenerators.GenerateRandomMatrix(matrixSize, matrixSize);
        var c = new float[matrixSize, matrixSize];
        
        // Calculate FLOPs
        long flops = 2L * matrixSize * matrixSize * matrixSize; // 2N³ operations
        
        // Warmup
        MatrixMultiplyCPU(a, b, c);
        
        // Benchmark
        const int iterations = 10;
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            MatrixMultiplyCPU(a, b, c);
        }
        
        stopwatch.Stop();
        
        var gflops = (flops * iterations) / (stopwatch.Elapsed.TotalSeconds * 1e9);
        var bandwidth = 3L * matrixSize * matrixSize * sizeof(float) * iterations / (stopwatch.Elapsed.TotalSeconds * 1e9);
        
        _output.WriteLine($"CPU Results:");
        _output.WriteLine($"  Time: {stopwatch.ElapsedMilliseconds}ms for {iterations} iterations");
        _output.WriteLine($"  Performance: {gflops:F2} GFLOPS");
        _output.WriteLine($"  Memory Bandwidth: {bandwidth:F2} GB/s");
        _output.WriteLine($"  Efficiency: {gflops / GetTheoreticalPeakGFLOPS() * 100:F1}% of peak");
        
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData(1024 * 1024)]      // 1MB
    [InlineData(10 * 1024 * 1024)] // 10MB
    [InlineData(100 * 1024 * 1024)] // 100MB
    public async Task Benchmark_MemoryTransfer_Bandwidth(int bufferSize)
    {
        _output.WriteLine($"=== Memory Transfer Benchmark ({bufferSize / (1024 * 1024)}MB) ===");
        
        var data = new byte[bufferSize];
        var random = new Random(42);
        random.NextBytes(data);
        
        // Host-to-Host copy (baseline)
        var dest = new byte[bufferSize];
        
        const int iterations = 100;
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            Array.Copy(data, dest, bufferSize);
        }
        
        stopwatch.Stop();
        
        var bandwidthGBps = (double)bufferSize * iterations / (stopwatch.Elapsed.TotalSeconds * 1e9);
        
        _output.WriteLine($"Host-to-Host Bandwidth: {bandwidthGBps:F2} GB/s");
        
        // TODO: Add H2D and D2H benchmarks when hardware backend is available
        if (AcceleratorTestFixture.IsCudaAvailable())
        {
            _output.WriteLine($"CUDA H2D Bandwidth: [Pending]");
            _output.WriteLine($"CUDA D2H Bandwidth: [Pending]");
        }
        
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Benchmark_ParallelReduction_Performance()
    {
        _output.WriteLine($"=== Parallel Reduction Benchmark ===");
        
        var sizes = new[] { 1024, 10240, 102400, 1024000, 10240000 };
        var results = new List<(int size, double timeMs, double throughputGBps)>();
        
        foreach (var size in sizes)
        {
            var data = TestDataGenerators.GenerateRandomVector(size);
            
            // Warmup
            var sum = data.Sum();
            
            // Benchmark
            const int iterations = 100;
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                sum = ParallelReduction(data);
            }
            
            stopwatch.Stop();
            
            var timeMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
            var throughputGBps = size * sizeof(float) / (timeMs * 1e6);
            
            results.Add((size, timeMs, throughputGBps));
            
            _output.WriteLine($"Size: {size,10:N0} | Time: {timeMs,8:F3}ms | Throughput: {throughputGBps,6:F2} GB/s");
        }
        
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Benchmark_KernelLaunchOverhead()
    {
        _output.WriteLine($"=== Kernel Launch Overhead Benchmark ===");
        
        // Measure overhead of launching many small kernels vs one large kernel
        const int totalWork = 1024 * 1024;
        
        // Many small kernels
        const int smallKernelSize = 1024;
        const int numSmallKernels = totalWork / smallKernelSize;
        
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < numSmallKernels; i++)
        {
            // Simulate kernel launch
            await Task.Yield();
        }
        var smallKernelTime = stopwatch.ElapsedMilliseconds;
        
        // One large kernel
        stopwatch.Restart();
        await Task.Yield(); // Simulate single kernel launch
        var largeKernelTime = stopwatch.ElapsedMilliseconds;
        
        _output.WriteLine($"Small kernels ({numSmallKernels} x {smallKernelSize}): {smallKernelTime}ms");
        _output.WriteLine($"Large kernel (1 x {totalWork}): {largeKernelTime}ms");
        _output.WriteLine($"Overhead per kernel launch: {(smallKernelTime - largeKernelTime) / (double)numSmallKernels:F3}ms");
        
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public async Task Benchmark_Scalability_Analysis(int parallelism)
    {
        _output.WriteLine($"=== Scalability Analysis (Parallelism={parallelism}) ===");
        
        const int workSize = 10_000_000;
        var data = TestDataGenerators.GenerateRandomVector(workSize);
        
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = new Task[parallelism];
        var chunkSize = workSize / parallelism;
        
        for (int i = 0; i < parallelism; i++)
        {
            int start = i * chunkSize;
            int end = (i == parallelism - 1) ? workSize : (i + 1) * chunkSize;
            
            tasks[i] = Task.Run(() =>
            {
                double sum = 0;
                for (int j = start; j < end; j++)
                {
                    sum += Math.Sin(data[j]) * Math.Cos(data[j]);
                }
                return sum;
            });
        }
        
        await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        var timeMs = stopwatch.Elapsed.TotalMilliseconds;
        var speedup = 1000.0 / timeMs; // Relative to baseline
        var efficiency = speedup / parallelism * 100;
        
        _output.WriteLine($"  Time: {timeMs:F2}ms");
        _output.WriteLine($"  Speedup: {speedup:F2}x");
        _output.WriteLine($"  Efficiency: {efficiency:F1}%");
    }

    // Helper methods
    private void VectorAddCPU(float[] a, float[] b, float[] c)
    {
        for (int i = 0; i < a.Length; i++)
        {
            c[i] = a[i] + b[i];
        }
    }

    private void MatrixMultiplyCPU(float[,] a, float[,] b, float[,] c)
    {
        int n = a.GetLength(0);
        
        // Simple triple-loop implementation
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                float sum = 0;
                for (int k = 0; k < n; k++)
                {
                    sum += a[i, k] * b[k, j];
                }
                c[i, j] = sum;
            }
        }
    }

    private float ParallelReduction(float[] data)
    {
        // Simple parallel reduction
        if (data.Length <= 1024)
        {
            return data.Sum();
        }
        
        int halfSize = data.Length / 2;
        float sum1 = 0, sum2 = 0;
        
        Parallel.Invoke(
            () => { for (int i = 0; i < halfSize; i++) sum1 += data[i]; },
            () => { for (int i = halfSize; i < data.Length; i++) sum2 += data[i]; }
        );
        
        return sum1 + sum2;
    }

    private double GetTheoreticalPeakGFLOPS()
    {
        // Estimate based on CPU
        // This is a rough estimate - actual peak depends on CPU model
        var coresCount = Environment.ProcessorCount;
        var estimatedGHzPerCore = 3.0; // Assume 3GHz
        var flopsPerCyclePerCore = 8; // Assume AVX2 (8 single-precision ops)
        
        return coresCount * estimatedGHzPerCore * flopsPerCyclePerCore;
    }
}