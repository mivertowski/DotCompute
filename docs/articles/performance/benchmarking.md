# Performance Benchmarking Guide

Learn how to measure, analyze, and optimize the performance of your DotCompute kernels across CPU and GPU backends.

## Quick Start

### Basic Benchmarking

Use BenchmarkDotNet for accurate performance measurements:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotCompute.Backends.CPU;
using DotCompute.Backends.CUDA;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class VectorAddBenchmark
{
    private float[] _a, _b, _result;
    private CpuAccelerator _cpu;
    private CudaAccelerator _cuda;

    [GlobalSetup]
    public void Setup()
    {
        _a = new float[1_000_000];
        _b = new float[1_000_000];
        _result = new float[1_000_000];

        for (int i = 0; i < _a.Length; i++)
        {
            _a[i] = i;
            _b[i] = i * 2;
        }

        _cpu = new CpuAccelerator();
        _cuda = new CudaAccelerator();
    }

    [Benchmark(Baseline = true)]
    public void CPU_Scalar()
    {
        for (int i = 0; i < _a.Length; i++)
        {
            _result[i] = _a[i] + _b[i];
        }
    }

    [Benchmark]
    public async Task CPU_SIMD()
    {
        await Kernels.VectorAdd(_a, _b, _result, _cpu);
    }

    [Benchmark]
    public async Task GPU_CUDA()
    {
        await Kernels.VectorAdd(_a, _b, _result, _cuda);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _cpu?.Dispose();
        _cuda?.Dispose();
    }
}

class Program
{
    static void Main() => BenchmarkRunner.Run<VectorAddBenchmark>();
}
```

### Running Benchmarks

```bash
dotnet run -c Release -- --filter *VectorAdd*
```

## Measured Performance Results

### CPU Backend (SIMD Optimization)

**Operation**: Vector Addition (1M elements)

| Method       | Mean     | Ratio | Allocated |
|------------- |---------:|------:|----------:|
| CPU_Scalar   | 2.14 ms  | 1.00  | 0 B       |
| CPU_SIMD     | 0.58 ms  | 0.27  | 64 B      |

**Speedup**: **3.7x faster** with SIMD vectorization

**Key Factors**:
- AVX2/AVX512 SIMD instructions
- Cache-friendly memory access
- 90% allocation reduction via pooling

### GPU Backend (CUDA)

**Hardware**: NVIDIA RTX 2000 Ada (Compute Capability 8.9)

| Operation          | Size     | CPU Time | GPU Time | Speedup |
|--------------------|----------|----------|----------|---------|
| Vector Add         | 1M       | 2.14 ms  | 0.10 ms  | 21x     |
| Matrix Multiply    | 512²     | 45 ms    | 1.2 ms   | 38x     |
| FFT                | 16K      | 8 ms     | 0.35 ms  | 23x     |
| Convolution (2D)   | 1920x1080| 120 ms   | 1.3 ms   | 92x     |

**Real-World Speedups**: 21-92x depending on operation complexity

## Profiling Tools

### Built-in Performance Profiler

DotCompute includes a production-grade profiler:

```csharp
using DotCompute.Core.Telemetry;

var profiler = new PerformanceProfiler();

// Start profiling
profiler.Start();

// Run kernel
await kernel.ExecuteAsync(arguments);

// Get metrics
var metrics = profiler.Stop();

Console.WriteLine($"Execution Time: {metrics.ExecutionTime.TotalMilliseconds:F2} ms");
Console.WriteLine($"Memory Bandwidth: {metrics.MemoryBandwidthGBps:F2} GB/s");
Console.WriteLine($"Compute Throughput: {metrics.ThroughputGFLOPS:F2} GFLOPS");
Console.WriteLine($"GPU Occupancy: {metrics.Occupancy * 100:F1}%");
```

### Hardware Performance Counters

For detailed GPU metrics:

```csharp
var metrics = await CudaProfiler.MeasureAsync(kernel, arguments, new()
{
    EnableHardwareCounters = true,
    Metrics = new[]
    {
        "sm_efficiency",            // Streaming Multiprocessor efficiency
        "achieved_occupancy",        // Actual GPU occupancy
        "gld_efficiency",            // Global load efficiency
        "gst_efficiency",            // Global store efficiency
        "shared_efficiency",         // Shared memory efficiency
        "warp_execution_efficiency", // Warp divergence
        "l2_cache_hit_rate",        // L2 cache hits
        "dram_utilization"           // Memory utilization
    }
});

foreach (var metric in metrics)
{
    Console.WriteLine($"{metric.Name}: {metric.Value:F2}%");
}
```

## Performance Analysis

### Memory Bandwidth Analysis

Calculate effective bandwidth:

```csharp
var dataSize = inputSize + outputSize; // In bytes
var timeSeconds = executionTime.TotalSeconds;
var bandwidthGBps = (dataSize / 1e9) / timeSeconds;

Console.WriteLine($"Effective Bandwidth: {bandwidthGBps:F2} GB/s");
Console.WriteLine($"Peak Bandwidth: {accelerator.PeakBandwidthGBps:F2} GB/s");
Console.WriteLine($"Efficiency: {(bandwidthGBps / accelerator.PeakBandwidthGBps) * 100:F1}%");
```

**Typical Results**:
- **CPU DDR4**: 10-15 GB/s effective (50-60% of peak)
- **GPU CUDA**: 200-400 GB/s effective (40-80% of peak)
- **Apple Metal**: 150-300 GB/s unified memory (60-90% of peak)

### Compute Throughput Analysis

```csharp
var operationsCount = elements * operationsPerElement;
var timeSeconds = executionTime.TotalSeconds;
var gflops = (operationsCount / 1e9) / timeSeconds;

Console.WriteLine($"Throughput: {gflops:F2} GFLOPS");
Console.WriteLine($"Peak Compute: {accelerator.PeakGFLOPS:F2} GFLOPS");
Console.WriteLine($"Efficiency: {(gflops / accelerator.PeakGFLOPS) * 100:F1}%");
```

### Occupancy Analysis

GPU occupancy measures how well you're utilizing SM resources:

```csharp
var occupancy = await CudaProfiler.CalculateOccupancyAsync(kernel, blockSize);

Console.WriteLine($"Theoretical Occupancy: {occupancy.Theoretical * 100:F1}%");
Console.WriteLine($"Achieved Occupancy: {occupancy.Achieved * 100:F1}%");

if (occupancy.LimitingFactor != OccupancyLimiter.None)
{
    Console.WriteLine($"Limiting Factor: {occupancy.LimitingFactor}");
    Console.WriteLine($"Recommendation: {occupancy.Suggestion}");
}
```

**Common Limiting Factors**:
- **Registers**: Reduce local variables, enable spilling
- **Shared Memory**: Reduce shared allocations
- **Block Size**: Adjust threadgroup dimensions
- **Warp Allocation**: Use multiples of warp size (32)

## Optimization Strategies

### 1. Memory Access Optimization

**Problem**: Uncoalesced memory access
```csharp
// Bad: Strided access
output[threadId * stride] = input[threadId * stride];
```

**Solution**: Coalesced access pattern
```csharp
// Good: Sequential access
output[threadId] = input[threadId];
```

**Impact**: 5-10x bandwidth improvement

### 2. Shared Memory Usage

**Problem**: Repeated global memory access
```csharp
for (int i = 0; i < 10; i++)
{
    sum += input[globalId]; // 10 global loads
}
```

**Solution**: Cache in shared memory
```csharp
var shared = Kernel.AllocateShared<float>(blockSize);
shared[threadIdx] = input[globalId]; // 1 global load
Kernel.Barrier();

for (int i = 0; i < 10; i++)
{
    sum += shared[threadIdx]; // 10 shared loads (fast)
}
```

**Impact**: 50-100x faster for repeated access

### 3. Reduce Atomic Contention

**Problem**: High atomic contention
```csharp
for (int i = 0; i < 100; i++)
{
    Kernel.AtomicAdd(ref total, data[i]); // 100 atomic ops
}
```

**Solution**: Local accumulation
```csharp
float local = 0;
for (int i = 0; i < 100; i++)
{
    local += data[i]; // No atomics
}
Kernel.AtomicAdd(ref total, local); // 1 atomic op
```

**Impact**: 10-50x reduction in atomic operations

### 4. Occupancy Optimization

**Check current occupancy**:
```csharp
var occupancy = await profiler.GetOccupancyAsync(kernel);
if (occupancy < 0.5)
{
    Console.WriteLine("Low occupancy detected!");
    // Try: Reduce registers, reduce shared memory, increase block size
}
```

**Adjust block size**:
```csharp
// Find optimal block size
var optimal = await CudaProfiler.FindOptimalBlockSizeAsync(kernel);
Console.WriteLine($"Optimal block size: {optimal}");

// Use optimal configuration
await kernel.ExecuteAsync(arguments,
    gridSize: (totalThreads + optimal - 1) / optimal,
    blockSize: optimal);
```

## Comparative Benchmarks

### Backend Comparison

```csharp
var backends = new IAccelerator[]
{
    new CpuAccelerator(),
    new CudaAccelerator(),
    new MetalAccelerator(),
    new OpenCLAccelerator()
};

foreach (var backend in backends)
{
    var sw = Stopwatch.StartNew();
    await kernel.ExecuteAsync(arguments, backend);
    sw.Stop();

    Console.WriteLine($"{backend.Type}: {sw.Elapsed.TotalMilliseconds:F2} ms");
}
```

**Typical Results** (1M element vector add):
- CPU (scalar): 2.14 ms
- CPU (SIMD): 0.58 ms
- CUDA: 0.10 ms
- Metal: 0.12 ms
- OpenCL: 0.15 ms

### Data Size Scaling

```csharp
var sizes = new[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000 };

foreach (var size in sizes)
{
    var data = GenerateData(size);
    var sw = Stopwatch.StartNew();
    await kernel.ExecuteAsync(data);
    sw.Stop();

    var throughput = size / sw.Elapsed.TotalSeconds / 1e6; // Melements/sec
    Console.WriteLine($"Size: {size,10:N0} - Time: {sw.Elapsed.TotalMilliseconds,8:F2} ms - Throughput: {throughput:F2} M/s");
}
```

## Automated Performance Regression Testing

### Set Performance Baselines

```csharp
[Fact]
public async Task VectorAdd_PerformanceBaseline()
{
    var sw = Stopwatch.StartNew();
    await Kernels.VectorAdd(a, b, result, accelerator);
    sw.Stop();

    // Ensure performance doesn't regress below baseline
    Assert.True(sw.Elapsed.TotalMilliseconds < 1.0,
        $"Vector add took {sw.Elapsed.TotalMilliseconds:F2} ms, baseline is 1.0 ms");
}
```

### Track Performance Over Time

```csharp
var results = new Dictionary<string, double>();

for (int run = 0; run < 100; run++)
{
    var time = await BenchmarkKernel(kernel);
    results.Add($"Run {run}", time.TotalMilliseconds);
}

var mean = results.Values.Average();
var stdDev = Math.Sqrt(results.Values.Sum(x => Math.Pow(x - mean, 2)) / results.Count);

Console.WriteLine($"Mean: {mean:F2} ms");
Console.WriteLine($"Std Dev: {stdDev:F2} ms");
Console.WriteLine($"CV: {(stdDev / mean) * 100:F1}%"); // Coefficient of variation
```

## Performance Dashboard

Create a real-time performance dashboard:

```csharp
using DotCompute.Core.Telemetry;

var dashboard = new PerformanceDashboard();

// Register metrics
dashboard.RegisterKernel("VectorAdd", vectorAddKernel);
dashboard.RegisterKernel("MatrixMul", matrixKernel);

// Start monitoring
await dashboard.StartMonitoringAsync();

// Access metrics
var report = dashboard.GenerateReport();
Console.WriteLine($"Total GPU Time: {report.TotalGpuTime.TotalSeconds:F2} seconds");
Console.WriteLine($"Total Data Transferred: {report.TotalDataTransferredGB:F2} GB");
Console.WriteLine($"Average Bandwidth: {report.AverageBandwidthGBps:F2} GB/s");
Console.WriteLine($"Kernel Efficiency: {report.AverageEfficiency * 100:F1}%");
```

## Best Practices

### 1. Warm-up Runs

Always exclude first run from measurements:

```csharp
// Warm-up
await kernel.ExecuteAsync(arguments);

// Actual measurement
var sw = Stopwatch.StartNew();
for (int i = 0; i < 100; i++)
{
    await kernel.ExecuteAsync(arguments);
}
sw.Stop();

var avgTime = sw.Elapsed.TotalMilliseconds / 100;
```

### 2. Statistical Significance

Run multiple times and calculate statistics:

```csharp
var times = new List<double>();
for (int i = 0; i < 100; i++)
{
    var sw = Stopwatch.StartNew();
    await kernel.ExecuteAsync(arguments);
    sw.Stop();
    times.Add(sw.Elapsed.TotalMilliseconds);
}

times.Sort();
var median = times[times.Count / 2];
var p95 = times[(int)(times.Count * 0.95)];
var p99 = times[(int)(times.Count * 0.99)];

Console.WriteLine($"Median: {median:F2} ms");
Console.WriteLine($"95th percentile: {p95:F2} ms");
Console.WriteLine($"99th percentile: {p99:F2} ms");
```

### 3. Memory Transfer Considerations

Include transfer time in benchmarks:

```csharp
var sw = Stopwatch.StartNew();

// Transfer to GPU
await buffer.CopyFromAsync(data);

// Execute kernel
await kernel.ExecuteAsync(arguments);

// Transfer from GPU
await buffer.CopyToAsync(result);

sw.Stop();

Console.WriteLine($"Total time (including transfers): {sw.Elapsed.TotalMilliseconds:F2} ms");
```

## Tools and Resources

### NVIDIA Tools
- **Nsight Compute**: Detailed kernel profiling
- **Nsight Systems**: System-wide timeline
- **nvprof**: Command-line profiler

```bash
nsys profile --stats=true ./YourApp
ncu --metrics sm_efficiency,achieved_occupancy ./YourApp
```

### Apple Metal Tools
- **Xcode Instruments**: Metal profiling
- **Metal Debugger**: Frame capture and analysis

### Intel VTune
- CPU and GPU profiling
- Memory bandwidth analysis

## See Also

- [Performance Characteristics](characteristics.md) - Understanding performance
- [Optimization Strategies](optimization-strategies.md) - Advanced optimization
- [CUDA Programming](../advanced/cuda-programming.md) - CUDA-specific profiling

---

**Performance Tip**: Focus on bandwidth optimization first, then occupancy, then algorithmic improvements. Most GPU kernels are memory-bound, not compute-bound.
