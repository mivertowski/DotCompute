using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Core.Memory;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Performance benchmarks for kernel compilation and execution operations.
/// Tests various scenarios including cold compilation, cached execution, and different kernel sizes.
/// </summary>
[Trait("Category", "Performance")]
public class KernelPerformanceBenchmarks : PerformanceBenchmarkBase, IDisposable
{
    private readonly ITestOutputHelper _output;
    private IAccelerator _accelerator;
    private IMemoryBuffer<float> _inputBuffer;
    private IMemoryBuffer<float> _outputBuffer;
    private IKernel _vectorAddKernel;
    private IKernel _matrixMultiplyKernel;
    private IKernel _complexComputeKernel;
    
    [Params(1024, 4096, 16384, 65536, 262144)]
    public int VectorSize { get; set; }
    
    [Params(32, 64, 128, 256)]
    public int MatrixSize { get; set; }
    
    [Params(true, false)]
    public bool UseCaching { get; set; }
    
    public KernelPerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Initialize accelerator (prefer GPU if available)
        _accelerator = AcceleratorFactory.CreateBest();
        await _accelerator.InitializeAsync();
        
        // Allocate memory buffers
        _inputBuffer = _accelerator.AllocateBuffer<float>(Math.Max(VectorSize, MatrixSize * MatrixSize));
        _outputBuffer = _accelerator.AllocateBuffer<float>(Math.Max(VectorSize, MatrixSize * MatrixSize));
        
        // Initialize input data
        var inputData = new float[_inputBuffer.Length];
        var random = new Random(42); // Fixed seed for reproducibility
        for (var i = 0; i < inputData.Length; i++)
        {
            inputData[i] = (float)random.NextDouble();
        }
        await _inputBuffer.WriteAsync(inputData);
        
        // Pre-compile kernels for cached scenarios
        await CompileTestKernels();
    }
    
    private async Task CompileTestKernels()
    {
        // Simple vector addition kernel
        var vectorAddSource = @"
            __kernel void vector_add(__global const float* a, __global const float* b, __global float* c, int size) {
                int gid = get_global_id(0);
                if (gid < size) {
                    c[gid] = a[gid] + b[gid];
                }
            }";
        
        // Matrix multiplication kernel
        var matrixMultiplySource = @"
            __kernel void matrix_multiply(__global const float* A, __global const float* B, __global float* C, int size) {
                int row = get_global_id(0);
                int col = get_global_id(1);
                if (row < size && col < size) {
                    float sum = 0.0f;
                    for (int k = 0; k < size; k++) {
                        sum += A[row * size + k] * B[k * size + col];
                    }
                    C[row * size + col] = sum;
                }
            }";
        
        // Complex compute kernel with multiple operations
        var complexComputeSource = @"
            __kernel void complex_compute(__global const float* input, __global float* output, int size) {
                int gid = get_global_id(0);
                if (gid < size) {
                    float x = input[gid];
                    // Perform multiple mathematical operations
                    float result = sin(x) * cos(x * 2.0f) + sqrt(fabs(x)) * log(x + 1.0f);
                    result = pow(result, 1.5f) + exp(-x * 0.01f);
                    output[gid] = result;
                }
            }";
        
        _vectorAddKernel = await _accelerator.CompileKernelAsync("vector_add", vectorAddSource);
        _matrixMultiplyKernel = await _accelerator.CompileKernelAsync("matrix_multiply", matrixMultiplySource);
        _complexComputeKernel = await _accelerator.CompileKernelAsync("complex_compute", complexComputeSource);
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkKernelCompilation()
    {
        var metrics = await RunBenchmarkAsync();
        LogMetrics(metrics, "Kernel Compilation");

        // Assert compilation performance expectations

        AssertPerformanceExpectations(metrics, 
            maxAverageTimeMs: 5000.0, // 5 seconds max for compilation
            maxStandardDeviationMs: 1000.0);
        
        _output.WriteLine($"Kernel Compilation Performance: {metrics}");
    }
    
    protected override async Task ExecuteOperationAsync()
    {
        // Simulate fresh compilation by creating new kernel source each time
        var kernelSource = $@"
            __kernel void test_kernel_{Guid.NewGuid():N}(__global const float* input, __global float* output, int size) {{
                int gid = get_global_id(0);
                if (gid < size) {{
                    output[gid] = input[gid] * 2.0f + 1.0f;
                }}
            }}";
        
        var kernel = await _accelerator.CompileKernelAsync($"test_kernel_{Guid.NewGuid():N}", kernelSource);
        kernel?.Dispose();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkVectorAddExecution()
    {
        DataSize = VectorSize * sizeof(float);
        var metrics = await RunVectorAddBenchmarkAsync();
        LogMetrics(metrics, "Vector Addition Execution");
        
        // Calculate expected throughput (processing 2 input vectors + 1 output)
        var expectedMinThroughput = 100.0; // MB/s minimum
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 100.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Vector Add Execution Performance: {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunVectorAddBenchmarkAsync()
    {
        await WarmupVectorAdd();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureVectorAddAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupVectorAdd()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteVectorAddAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureVectorAddAsync()
    {
        _stopwatch.Restart();
        await ExecuteVectorAddAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteVectorAddAsync()
    {
        var workGroupSize = new[] { Math.Min(VectorSize, 256) };
        var globalWorkSize = new[] { VectorSize };
        
        await _vectorAddKernel.ExecuteAsync(globalWorkSize, workGroupSize, 
            _inputBuffer, _inputBuffer, _outputBuffer, VectorSize);
        await _accelerator.SynchronizeAsync();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkMatrixMultiplicationExecution()
    {
        DataSize = MatrixSize * MatrixSize * sizeof(float);
        var metrics = await RunMatrixMultiplyBenchmarkAsync();
        LogMetrics(metrics, "Matrix Multiplication Execution");
        
        // Matrix multiply is computationally intensive
        var expectedMinThroughput = 50.0; // Lower threshold due to complexity
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 1000.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Matrix Multiply Execution Performance: {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunMatrixMultiplyBenchmarkAsync()
    {
        await WarmupMatrixMultiply();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureMatrixMultiplyAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupMatrixMultiply()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteMatrixMultiplyAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureMatrixMultiplyAsync()
    {
        _stopwatch.Restart();
        await ExecuteMatrixMultiplyAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteMatrixMultiplyAsync()
    {
        var workGroupSize = new[] { 16, 16 };
        var globalWorkSize = new[] { 
            ((MatrixSize + workGroupSize[0] - 1) / workGroupSize[0]) * workGroupSize[0],
            ((MatrixSize + workGroupSize[1] - 1) / workGroupSize[1]) * workGroupSize[1]
        };
        
        await _matrixMultiplyKernel.ExecuteAsync(globalWorkSize, workGroupSize,
            _inputBuffer, _inputBuffer, _outputBuffer, MatrixSize);
        await _accelerator.SynchronizeAsync();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkComplexComputeExecution()
    {
        DataSize = VectorSize * sizeof(float);
        var metrics = await RunComplexComputeBenchmarkAsync();
        LogMetrics(metrics, "Complex Compute Execution");
        
        // Complex mathematical operations are slower
        var expectedMinThroughput = 25.0;
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 200.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Complex Compute Execution Performance: {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunComplexComputeBenchmarkAsync()
    {
        await WarmupComplexCompute();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureComplexComputeAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupComplexCompute()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteComplexComputeAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureComplexComputeAsync()
    {
        _stopwatch.Restart();
        await ExecuteComplexComputeAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteComplexComputeAsync()
    {
        var workGroupSize = new[] { Math.Min(VectorSize, 256) };
        var globalWorkSize = new[] { VectorSize };
        
        await _complexComputeKernel.ExecuteAsync(globalWorkSize, workGroupSize,
            _inputBuffer, _outputBuffer, VectorSize);
        await _accelerator.SynchronizeAsync();
    }
    
    [Fact]
    public async Task BenchmarkKernelCachingPerformance()
    {
        // Compare cached vs non-cached kernel execution
        var cachedMetrics = await RunCachedKernelBenchmark();
        var nonCachedMetrics = await RunNonCachedKernelBenchmark();
        
        LogMetrics(cachedMetrics, "Cached Kernel Execution");
        LogMetrics(nonCachedMetrics, "Non-Cached Kernel Execution");
        
        // Cached should be significantly faster
        Assert.True(cachedMetrics.AverageTime < nonCachedMetrics.AverageTime * 0.1,
            "Cached kernel execution should be at least 10x faster than compilation + execution");
        
        _output.WriteLine($"Cache Performance Improvement: " +
                         $"{(nonCachedMetrics.AverageTime / cachedMetrics.AverageTime):F1}x faster");
    }
    
    private async Task<PerformanceMetrics> RunCachedKernelBenchmark()
    {
        DataSize = VectorSize * sizeof(float);
        return await RunVectorAddBenchmarkAsync();
    }
    
    private async Task<PerformanceMetrics> RunNonCachedKernelBenchmark()
    {
        DataSize = VectorSize * sizeof(float);
        return await RunBenchmarkAsync(); // This compiles fresh kernels each time
    }


    [GlobalCleanup]
    public override void Cleanup() => base.Cleanup();


    public void Dispose()
    {
        _vectorAddKernel?.Dispose();
        _matrixMultiplyKernel?.Dispose();
        _complexComputeKernel?.Dispose();
        _inputBuffer?.Dispose();
        _outputBuffer?.Dispose();
        _accelerator?.Dispose();
    }
    
    private PerformanceMetrics CalculateMetrics(long allocations)
    {
        _measurements.Sort();
        
        var count = _measurements.Count;
        var sum = 0.0;
        foreach (var measurement in _measurements)
        {
            sum += measurement;
        }
        
        var average = sum / count;
        var median = count % 2 == 0 
            ? (_measurements[count / 2 - 1] + _measurements[count / 2]) / 2.0
            : _measurements[count / 2];
        
        var variance = 0.0;
        foreach (var measurement in _measurements)
        {
            variance += Math.Pow(measurement - average, 2);
        }
        var stdDev = Math.Sqrt(variance / count);
        
        var p95Index = (int)Math.Ceiling(0.95 * count) - 1;
        var p99Index = (int)Math.Ceiling(0.99 * count) - 1;
        
        var throughput = DataSize > 0 
            ? (DataSize / (1024.0 * 1024.0)) / (average / 1000.0)
            : 1000.0 / average;
        
        return new PerformanceMetrics
        {
            MinTime = _measurements[0],
            MaxTime = _measurements[count - 1],
            AverageTime = average,
            MedianTime = median,
            StandardDeviation = stdDev,
            P95Time = _measurements[p95Index],
            P99Time = _measurements[p99Index],
            Throughput = throughput,
            TotalAllocations = allocations,
            PeakMemoryUsage = GC.GetTotalMemory(false)
        };
    }
}