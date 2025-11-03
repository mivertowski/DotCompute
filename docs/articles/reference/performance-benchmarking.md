# Performance Benchmarking Guide

Comprehensive guide to measuring, analyzing, and optimizing DotCompute application performance.

## Overview

Effective performance benchmarking requires:

1. **Accurate Measurement**: Proper timing and metrics collection
2. **Statistical Validity**: Multiple runs, outlier removal, confidence intervals
3. **Systematic Comparison**: Baseline, variants, backends
4. **Actionable Insights**: Identify bottlenecks, optimization opportunities

## Quick Start

### Basic Benchmarking

```csharp
using System.Diagnostics;
using DotCompute;

var stopwatch = Stopwatch.StartNew();

await orchestrator.ExecuteKernelAsync("MyKernel", params);

stopwatch.Stop();
Console.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
```

**Issues with Basic Approach**:
- Includes cold start overhead (first run slower)
- Single measurement (variance not captured)
- No breakdown (compute vs transfer time)

### Improved Benchmarking

```csharp
// Warmup (primes caches, JIT, GPU)
for (int i = 0; i < 3; i++)
{
    await orchestrator.ExecuteKernelAsync("MyKernel", params);
}

// Measure multiple runs
const int iterations = 100;
var times = new List<double>();

for (int i = 0; i < iterations; i++)
{
    var stopwatch = Stopwatch.StartNew();
    await orchestrator.ExecuteKernelAsync("MyKernel", params);
    stopwatch.Stop();
    times.Add(stopwatch.Elapsed.TotalMilliseconds);
}

// Statistics
var average = times.Average();
var min = times.Min();
var max = times.Max();
var stdDev = Math.Sqrt(times.Select(t => Math.Pow(t - average, 2)).Average());

Console.WriteLine($"Average: {average:F3}ms");
Console.WriteLine($"Min: {min:F3}ms");
Console.WriteLine($"Max: {max:F3}ms");
Console.WriteLine($"StdDev: {stdDev:F3}ms");
```

## BenchmarkDotNet Integration

### Setup

```xml
<ItemGroup>
  <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
</ItemGroup>
```

### Basic Benchmark

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotCompute;
using Microsoft.Extensions.DependencyInjection;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 100)]
public class VectorOperationsBenchmark
{
    private IComputeOrchestrator? _orchestrator;
    private float[]? _inputA;
    private float[]? _inputB;
    private float[]? _output;

    [Params(1000, 10000, 100000, 1000000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime();
        var provider = services.BuildServiceProvider();

        _orchestrator = provider.GetRequiredService<IComputeOrchestrator>();

        _inputA = new float[Size];
        _inputB = new float[Size];
        _output = new float[Size];

        for (int i = 0; i < Size; i++)
        {
            _inputA[i] = i;
            _inputB[i] = Size - i;
        }
    }

    [Benchmark(Baseline = true)]
    public async Task<float[]> CPU_VectorAdd()
    {
        var options = new ExecutionOptions
        {
            PreferredBackend = BackendType.CPU
        };

        await _orchestrator!.ExecuteKernelAsync(
            "VectorAdd",
            new { a = _inputA, b = _inputB, result = _output },
            options);

        return _output!;
    }

    [Benchmark]
    public async Task<float[]> CUDA_VectorAdd()
    {
        var options = new ExecutionOptions
        {
            PreferredBackend = BackendType.CUDA
        };

        await _orchestrator!.ExecuteKernelAsync(
            "VectorAdd",
            new { a = _inputA, b = _inputB, result = _output },
            options);

        return _output!;
    }

    [Benchmark]
    public float[] Native_VectorAdd()
    {
        for (int i = 0; i < Size; i++)
        {
            _output![i] = _inputA![i] + _inputB![i];
        }
        return _output!;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<VectorOperationsBenchmark>();
    }
}
```

### Output Example

```
BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.4460)
AMD Ryzen 9 5950X, 1 CPU, 32 logical and 16 physical cores
.NET SDK 9.0.100

|          Method |    Size |       Mean |     Error |    StdDev | Ratio | Allocated |
|---------------- |-------- |-----------:|----------:|----------:|------:|----------:|
|    CPU_VectorAdd |    1000 |   15.82 μs |  0.185 μs |  0.173 μs |  1.00 |     112 B |
|   CUDA_VectorAdd |    1000 |   45.23 μs |  0.891 μs |  0.834 μs |  2.86 |     312 B |
| Native_VectorAdd |    1000 |    1.12 μs |  0.012 μs |  0.010 μs |  0.07 |         - |
|                 |         |            |           |           |       |           |
|    CPU_VectorAdd |   10000 |   18.45 μs |  0.231 μs |  0.216 μs |  1.00 |     112 B |
|   CUDA_VectorAdd |   10000 |   46.78 μs |  0.823 μs |  0.770 μs |  2.54 |     312 B |
| Native_VectorAdd |   10000 |    9.87 μs |  0.078 μs |  0.073 μs |  0.53 |         - |
|                 |         |            |           |           |       |           |
|    CPU_VectorAdd |  100000 |   85.23 μs |  1.234 μs |  1.154 μs |  1.00 |     112 B |
|   CUDA_VectorAdd |  100000 |   78.45 μs |  1.456 μs |  1.362 μs |  0.92 |     312 B |
| Native_VectorAdd |  100000 |   98.12 μs |  1.123 μs |  1.051 μs |  1.15 |         - |
|                 |         |            |           |           |       |           |
|    CPU_VectorAdd | 1000000 |  845.67 μs | 12.345 μs | 11.546 μs |  1.00 |     112 B |
|   CUDA_VectorAdd | 1000000 |  156.78 μs |  2.891 μs |  2.704 μs |  0.19 |     312 B |
| Native_VectorAdd | 1000000 | 1089.45 μs | 15.678 μs | 14.665 μs |  1.29 |         - |
```

**Insights**:
- Small data (< 10K): CPU faster due to transfer overhead
- Large data (1M): GPU 5.4x faster
- Crossover point: ~50K elements

## Built-in Profiling

### Enable Profiling

```csharp
services.AddDotComputeRuntime(options =>
{
    options.Telemetry.Enabled = true;
    options.Telemetry.EnableDetailedMetrics = true;
});

// Or enable at runtime
await orchestrator.EnableProfilingAsync();
```

### Detailed Metrics

```csharp
await orchestrator.ExecuteKernelAsync("MyKernel", params);

var profile = await orchestrator.GetProfileAsync();

Console.WriteLine($"Total time: {profile.TotalTime}ms");
Console.WriteLine($"Compute time: {profile.ComputeTime}ms");
Console.WriteLine($"Transfer time: {profile.TransferTime}ms");
Console.WriteLine($"  Host→Device: {profile.HostToDeviceTime}ms");
Console.WriteLine($"  Device→Host: {profile.DeviceToHostTime}ms");
Console.WriteLine($"Overhead: {profile.OverheadTime}ms");
Console.WriteLine($"  Kernel launch: {profile.KernelLaunchTime}ms");
Console.WriteLine($"  Synchronization: {profile.SyncTime}ms");
```

**Typical Breakdown**:
```
Total time: 10.45ms
Compute time: 2.34ms (22.4%)
Transfer time: 7.83ms (74.9%)
  Host→Device: 4.12ms
  Device→Host: 3.71ms
Overhead: 0.28ms (2.7%)
  Kernel launch: 0.15ms
  Synchronization: 0.13ms
```

**Interpretation**:
- Transfer dominates (75%) → Keep data on GPU longer
- Compute only 22% → Workload may be too small for GPU
- Low overhead (3%) → Efficient runtime

### Per-Kernel Profiling

```csharp
var kernelProfile = await orchestrator.GetKernelProfileAsync("MyKernel");

Console.WriteLine($"Kernel: {kernelProfile.Name}");
Console.WriteLine($"Executions: {kernelProfile.ExecutionCount}");
Console.WriteLine($"Average time: {kernelProfile.AverageTime:F3}ms");
Console.WriteLine($"Min time: {kernelProfile.MinTime:F3}ms");
Console.WriteLine($"Max time: {kernelProfile.MaxTime:F3}ms");
Console.WriteLine($"Total time: {kernelProfile.TotalTime:F3}ms");
Console.WriteLine($"Backend: {kernelProfile.Backend}");
Console.WriteLine($"Device: {kernelProfile.DeviceName}");
```

## Performance Metrics

### Throughput

**Elements per second**:
```csharp
int elements = 1_000_000;
var stopwatch = Stopwatch.StartNew();

await orchestrator.ExecuteKernelAsync("ProcessData", new { data, elements });

stopwatch.Stop();
double throughput = elements / stopwatch.Elapsed.TotalSeconds;
Console.WriteLine($"Throughput: {throughput / 1_000_000:F2} M elements/sec");
```

**Bandwidth** (memory-bound operations):
```csharp
int elements = 1_000_000;
int bytesPerElement = sizeof(float);
int totalBytes = elements * bytesPerElement * 2;  // Read + Write

var stopwatch = Stopwatch.StartNew();
await orchestrator.ExecuteKernelAsync("VectorAdd", params);
stopwatch.Stop();

double bandwidth = totalBytes / stopwatch.Elapsed.TotalSeconds / (1024 * 1024 * 1024);
Console.WriteLine($"Bandwidth: {bandwidth:F2} GB/s");

// Compare to hardware limit
double hardwareLimit = 760.0;  // RTX 3090: 760 GB/s
double efficiency = (bandwidth / hardwareLimit) * 100;
Console.WriteLine($"Efficiency: {efficiency:F1}%");
```

### FLOPS (Compute-bound operations)

```csharp
long operations = 2L * 1024 * 1024;  // 2M operations per element
int elements = 1_000_000;
long totalOps = operations * elements;

var stopwatch = Stopwatch.StartNew();
await orchestrator.ExecuteKernelAsync("ComputeIntensive", params);
stopwatch.Stop();

double gflops = totalOps / stopwatch.Elapsed.TotalSeconds / 1_000_000_000;
Console.WriteLine($"Performance: {gflops:F2} GFLOPS");

// Compare to hardware limit
double hardwareLimit = 35580.0;  // RTX 3090: 35.58 TFLOPS
double efficiency = (gflops * 1000 / hardwareLimit) * 100;
Console.WriteLine($"Efficiency: {efficiency:F1}%");
```

### Speedup

```csharp
// Baseline (CPU)
var cpuTime = await BenchmarkKernel(BackendType.CPU);

// Variant (GPU)
var gpuTime = await BenchmarkKernel(BackendType.CUDA);

double speedup = cpuTime / gpuTime;
Console.WriteLine($"Speedup: {speedup:F2}x");

// Efficiency
int threadCount = 10496;  // RTX 3090 CUDA cores
double efficiency = (speedup / threadCount) * 100;
Console.WriteLine($"Parallel efficiency: {efficiency:F2}%");
```

## Bottleneck Analysis

### Transfer vs Compute

```csharp
var profile = await orchestrator.GetProfileAsync();

double transferRatio = profile.TransferTime / profile.TotalTime;

if (transferRatio > 0.5)
{
    Console.WriteLine("Transfer-bound (>50% transfer time)");
    Console.WriteLine("Optimization: Keep data on GPU, chain kernels");
}
else if (transferRatio < 0.1)
{
    Console.WriteLine("Compute-bound (<10% transfer time)");
    Console.WriteLine("Optimization: Algorithm improvements, kernel fusion");
}
else
{
    Console.WriteLine("Balanced workload");
}
```

### Memory Access Patterns

```csharp
// Coalesced access (fast)
var coalescedTime = await BenchmarkCoalescedAccess();

// Strided access (slow)
var stridedTime = await BenchmarkStridedAccess();

double penalty = stridedTime / coalescedTime;
Console.WriteLine($"Strided access penalty: {penalty:F2}x");

if (penalty > 2.0)
{
    Console.WriteLine("Warning: Non-coalesced memory access detected");
    Console.WriteLine("Recommendation: Reorganize data layout for linear access");
}
```

### Kernel Launch Overhead

```csharp
// Measure launch overhead
var times = new List<double>();

for (int i = 0; i < 1000; i++)
{
    var stopwatch = Stopwatch.StartNew();
    await orchestrator.ExecuteKernelAsync("EmptyKernel", params);
    stopwatch.Stop();
    times.Add(stopwatch.Elapsed.TotalMicroseconds);
}

double avgOverhead = times.Average();
Console.WriteLine($"Average launch overhead: {avgOverhead:F2}μs");

// Typical values:
// CUDA: 10-50μs
// Metal: 5-20μs
// CPU: 1-5μs

if (avgOverhead > 100)
{
    Console.WriteLine("Warning: High launch overhead");
    Console.WriteLine("Recommendation: Batch operations, reduce kernel launch frequency");
}
```

## Optimization Strategies

### 1. Workload Size Analysis

```csharp
public static async Task<(int OptimalSize, double BestPerformance)> FindOptimalBatchSize(
    IComputeOrchestrator orchestrator)
{
    var results = new Dictionary<int, double>();

    foreach (int size in new[] { 1000, 10000, 100000, 1000000, 10000000 })
    {
        var data = new float[size];
        var stopwatch = Stopwatch.StartNew();

        await orchestrator.ExecuteKernelAsync("ProcessData", new { data });

        stopwatch.Stop();
        double throughput = size / stopwatch.Elapsed.TotalSeconds;
        results[size] = throughput;

        Console.WriteLine($"Size: {size,10:N0}, Throughput: {throughput / 1_000_000:F2} M/s");
    }

    var optimal = results.OrderByDescending(kvp => kvp.Value).First();
    Console.WriteLine($"\nOptimal batch size: {optimal.Key:N0} elements");
    Console.WriteLine($"Peak throughput: {optimal.Value / 1_000_000:F2} M elements/sec");

    return (optimal.Key, optimal.Value);
}
```

**Output**:
```
Size:      1,000, Throughput: 42.35 M/s
Size:     10,000, Throughput: 78.12 M/s
Size:    100,000, Throughput: 145.67 M/s
Size:  1,000,000, Throughput: 678.90 M/s   ← Peak
Size: 10,000,000, Throughput: 623.45 M/s   (starts degrading)

Optimal batch size: 1,000,000 elements
Peak throughput: 678.90 M elements/sec
```

### 2. Backend Comparison

```csharp
public static async Task CompareBackends(IComputeOrchestrator orchestrator)
{
    var backends = new[] { BackendType.CPU, BackendType.CUDA, BackendType.Metal };
    var results = new Dictionary<BackendType, double>();

    foreach (var backend in backends)
    {
        try
        {
            var options = new ExecutionOptions { PreferredBackend = backend };
            var times = new List<double>();

            for (int i = 0; i < 100; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await orchestrator.ExecuteKernelAsync("MyKernel", params, options);
                stopwatch.Stop();
                times.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            results[backend] = times.Average();
            Console.WriteLine($"{backend,-10}: {times.Average():F3}ms");
        }
        catch
        {
            Console.WriteLine($"{backend,-10}: Not available");
        }
    }

    var fastest = results.OrderBy(kvp => kvp.Value).First();
    Console.WriteLine($"\nFastest: {fastest.Key} ({fastest.Value:F3}ms)");

    foreach (var (backend, time) in results.Where(kvp => kvp.Key != fastest.Key))
    {
        double speedup = time / fastest.Value;
        Console.WriteLine($"  vs {backend}: {speedup:F2}x faster");
    }
}
```

### 3. Scaling Analysis

```csharp
public static async Task AnalyzeScaling(IComputeOrchestrator orchestrator)
{
    var sizes = new[] { 1000, 10000, 100000, 1000000, 10000000 };
    var results = new List<(int Size, double Time, double Throughput)>();

    Console.WriteLine("Size        | Time (ms) | Throughput (M/s) | Scaling");
    Console.WriteLine("------------|-----------|------------------|----------");

    double? previousThroughput = null;

    foreach (var size in sizes)
    {
        var data = new float[size];
        var stopwatch = Stopwatch.StartNew();

        await orchestrator.ExecuteKernelAsync("ProcessData", new { data });

        stopwatch.Stop();
        double time = stopwatch.Elapsed.TotalMilliseconds;
        double throughput = size / stopwatch.Elapsed.TotalSeconds / 1_000_000;

        string scaling = previousThroughput == null
            ? "baseline"
            : $"{throughput / previousThroughput.Value:F2}x";

        Console.WriteLine($"{size,10:N0} | {time,9:F3} | {throughput,16:F2} | {scaling}");

        results.Add((size, time, throughput));
        previousThroughput = throughput;
    }

    // Analyze scaling efficiency
    if (results.Count >= 2)
    {
        var first = results[0];
        var last = results[^1];

        double theoreticalSpeedup = (double)last.Size / first.Size;
        double actualSpeedup = last.Throughput / first.Throughput;
        double efficiency = (actualSpeedup / theoreticalSpeedup) * 100;

        Console.WriteLine($"\nScaling efficiency: {efficiency:F1}%");
    }
}
```

**Output**:
```
Size        | Time (ms) | Throughput (M/s) | Scaling
------------|-----------|------------------|----------
     1,000 |     0.125 |            8.00 | baseline
    10,000 |     0.234 |           42.74 | 5.34x
   100,000 |     1.456 |           68.68 | 1.61x
 1,000,000 |    12.345 |           81.00 | 1.18x
10,000,000 |   118.234 |           84.58 | 1.04x

Scaling efficiency: 92.8%
```

## Best Practices

1. **Always warm up**: First run includes initialization overhead
2. **Multiple measurements**: Single measurement is unreliable
3. **Statistical analysis**: Mean, min, max, standard deviation
4. **Baseline comparison**: Measure speedup vs CPU or previous version
5. **Profile first**: Identify bottlenecks before optimizing
6. **Measure after changes**: Verify optimizations actually help
7. **Document methodology**: Record configuration, hardware, environment

## Related Documentation

- [Performance Tuning Guide](../guides/performance-tuning.md) - Optimization techniques
- [Debugging Guide](../guides/debugging-guide.md) - Performance debugging
- [Architecture Overview](../architecture/overview.md) - System design

---

**Benchmarking • Profiling • Performance Analysis • Production Ready**
