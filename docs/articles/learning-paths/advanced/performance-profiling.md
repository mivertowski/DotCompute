# Performance Profiling

This module covers GPU-native performance measurement using timing APIs, profiling tools, and optimization strategies.

## Why GPU-Native Timing?

CPU-based timing includes:
- Kernel launch overhead
- Host-device synchronization
- PCIe latency

GPU-native timing provides:
- Sub-microsecond resolution
- Accurate kernel-only measurement
- Hardware timestamp counters

## GPU Timing API

### Basic Usage

```csharp
// Create timing provider
var timingProvider = computeService.GetTimingProvider();

// Calibrate for accuracy
await timingProvider.CalibrateAsync();

// Get GPU timestamp
var timestamp = await timingProvider.GetGpuTimestampAsync();
Console.WriteLine($"GPU timestamp: {timestamp} ns");

// Get clock frequency
var frequency = timingProvider.GetGpuClockFrequency();
Console.WriteLine($"Clock frequency: {frequency / 1e6:F0} MHz");

// Get timer resolution
var resolution = timingProvider.GetTimerResolutionNanos();
Console.WriteLine($"Resolution: {resolution} ns");
```

### Measuring Kernel Execution

```csharp
public async Task<double> MeasureKernelExecutionAsync(Delegate kernel, KernelConfig config, params object[] args)
{
    var timingProvider = computeService.GetTimingProvider();

    // Record start timestamp
    var startTimestamp = await timingProvider.GetGpuTimestampAsync();

    // Execute kernel
    await computeService.ExecuteKernelAsync(kernel, config, args);

    // Synchronize to ensure kernel completed
    await computeService.SynchronizeAsync();

    // Record end timestamp
    var endTimestamp = await timingProvider.GetGpuTimestampAsync();

    // Calculate elapsed time in milliseconds
    double elapsedNs = endTimestamp - startTimestamp;
    return elapsedNs / 1_000_000.0;
}
```

### Batch Timestamp Collection

```csharp
// For high-frequency measurements
var timestamps = await timingProvider.GetGpuTimestampsBatchAsync(
    count: 100,
    interval: TimeSpan.FromMicroseconds(10));

// Analyze timing distribution
var deltas = new List<double>();
for (int i = 1; i < timestamps.Length; i++)
{
    deltas.Add((timestamps[i] - timestamps[i-1]) / 1000.0); // microseconds
}

Console.WriteLine($"Average interval: {deltas.Average():F2} µs");
Console.WriteLine($"Std deviation: {CalculateStdDev(deltas):F2} µs");
```

## Calibration Strategies

### Available Strategies

```csharp
// Quick calibration (default)
await timingProvider.CalibrateAsync(CalibrationStrategy.Quick);

// Accurate calibration (takes longer)
await timingProvider.CalibrateAsync(CalibrationStrategy.Accurate);

// Statistical calibration (most accurate)
await timingProvider.CalibrateAsync(CalibrationStrategy.Statistical);

// No calibration (raw timestamps)
await timingProvider.CalibrateAsync(CalibrationStrategy.None);
```

### Calibration Parameters

```csharp
var calibrationOptions = new CalibrationOptions
{
    Strategy = CalibrationStrategy.Accurate,
    Iterations = 1000,
    WarmupIterations = 100,
    OutlierRejection = true,
    OutlierThreshold = 2.0  // Standard deviations
};

await timingProvider.CalibrateAsync(calibrationOptions);
```

## Profiling Patterns

### Pattern 1: Comprehensive Kernel Profile

```csharp
public class KernelProfile
{
    public double ExecutionTimeMs { get; set; }
    public double MemoryThroughputGBps { get; set; }
    public double ComputeThroughputGFlops { get; set; }
    public double Occupancy { get; set; }
    public long RegistersPerThread { get; set; }
    public long SharedMemoryBytes { get; set; }
}

public async Task<KernelProfile> ProfileKernelAsync(
    Delegate kernel,
    KernelConfig config,
    int dataSize,
    int flopsPerElement)
{
    var timingProvider = computeService.GetTimingProvider();

    // Warmup
    for (int i = 0; i < 10; i++)
    {
        await computeService.ExecuteKernelAsync(kernel, config, buffer);
    }
    await computeService.SynchronizeAsync();

    // Measure multiple iterations
    const int iterations = 100;
    var times = new double[iterations];

    for (int i = 0; i < iterations; i++)
    {
        var start = await timingProvider.GetGpuTimestampAsync();
        await computeService.ExecuteKernelAsync(kernel, config, buffer);
        await computeService.SynchronizeAsync();
        var end = await timingProvider.GetGpuTimestampAsync();

        times[i] = (end - start) / 1_000_000.0; // ms
    }

    // Calculate metrics
    double avgTime = times.Average();
    double dataBytes = dataSize * sizeof(float) * 2; // read + write
    double memBandwidth = dataBytes / (avgTime / 1000.0) / 1e9; // GB/s

    double totalFlops = (double)dataSize * flopsPerElement;
    double computeThroughput = totalFlops / (avgTime / 1000.0) / 1e9; // GFLOPS

    // Get occupancy info
    var occupancy = await computeService.GetOccupancyAsync(kernel, config.BlockSize);

    return new KernelProfile
    {
        ExecutionTimeMs = avgTime,
        MemoryThroughputGBps = memBandwidth,
        ComputeThroughputGFlops = computeThroughput,
        Occupancy = occupancy.TheoreticalOccupancy,
        RegistersPerThread = occupancy.RegistersPerThread,
        SharedMemoryBytes = occupancy.SharedMemoryPerBlock
    };
}
```

### Pattern 2: Pipeline Stage Timing

```csharp
public class PipelineProfile
{
    public Dictionary<string, double> StageTimes { get; set; } = new();
    public double TotalTimeMs { get; set; }
}

public async Task<PipelineProfile> ProfilePipelineAsync()
{
    var profile = new PipelineProfile();
    var timingProvider = computeService.GetTimingProvider();

    var totalStart = await timingProvider.GetGpuTimestampAsync();

    // Stage 1: Preprocessing
    var stage1Start = await timingProvider.GetGpuTimestampAsync();
    await computeService.ExecuteKernelAsync(preprocessKernel, config, buffer1, buffer2);
    await computeService.SynchronizeAsync();
    var stage1End = await timingProvider.GetGpuTimestampAsync();
    profile.StageTimes["Preprocess"] = (stage1End - stage1Start) / 1_000_000.0;

    // Stage 2: Main processing
    var stage2Start = await timingProvider.GetGpuTimestampAsync();
    await computeService.ExecuteKernelAsync(mainKernel, config, buffer2, buffer3);
    await computeService.SynchronizeAsync();
    var stage2End = await timingProvider.GetGpuTimestampAsync();
    profile.StageTimes["Main"] = (stage2End - stage2Start) / 1_000_000.0;

    // Stage 3: Postprocessing
    var stage3Start = await timingProvider.GetGpuTimestampAsync();
    await computeService.ExecuteKernelAsync(postprocessKernel, config, buffer3, buffer4);
    await computeService.SynchronizeAsync();
    var stage3End = await timingProvider.GetGpuTimestampAsync();
    profile.StageTimes["Postprocess"] = (stage3End - stage3Start) / 1_000_000.0;

    var totalEnd = await timingProvider.GetGpuTimestampAsync();
    profile.TotalTimeMs = (totalEnd - totalStart) / 1_000_000.0;

    return profile;
}
```

### Pattern 3: Memory Transfer Profiling

```csharp
public async Task ProfileTransfersAsync(int size)
{
    var timingProvider = computeService.GetTimingProvider();
    var hostData = new float[size];

    using var buffer = computeService.CreateBuffer<float>(size);

    // Profile Host → GPU
    var h2dStart = await timingProvider.GetGpuTimestampAsync();
    await buffer.CopyFromAsync(hostData);
    await computeService.SynchronizeAsync();
    var h2dEnd = await timingProvider.GetGpuTimestampAsync();

    double h2dTimeMs = (h2dEnd - h2dStart) / 1_000_000.0;
    double h2dBandwidth = (size * sizeof(float)) / (h2dTimeMs / 1000.0) / 1e9;

    Console.WriteLine($"Host→GPU: {h2dTimeMs:F3} ms, {h2dBandwidth:F1} GB/s");

    // Profile GPU → Host
    var d2hStart = await timingProvider.GetGpuTimestampAsync();
    await buffer.CopyToAsync(hostData);
    await computeService.SynchronizeAsync();
    var d2hEnd = await timingProvider.GetGpuTimestampAsync();

    double d2hTimeMs = (d2hEnd - d2hStart) / 1_000_000.0;
    double d2hBandwidth = (size * sizeof(float)) / (d2hTimeMs / 1000.0) / 1e9;

    Console.WriteLine($"GPU→Host: {d2hTimeMs:F3} ms, {d2hBandwidth:F1} GB/s");
}
```

## Bottleneck Analysis

### Identifying Bottlenecks

```csharp
public enum BottleneckType
{
    Compute,
    MemoryBandwidth,
    MemoryLatency,
    LaunchOverhead,
    Synchronization
}

public async Task<BottleneckType> AnalyzeBottleneckAsync(KernelProfile profile, HardwareInfo hw)
{
    // Calculate theoretical peaks
    double peakMemBw = hw.MemoryBandwidthGBps;
    double peakCompute = hw.PeakGFlops;

    // Calculate utilization
    double memUtilization = profile.MemoryThroughputGBps / peakMemBw;
    double computeUtilization = profile.ComputeThroughputGFlops / peakCompute;

    Console.WriteLine($"Memory utilization: {memUtilization:P0}");
    Console.WriteLine($"Compute utilization: {computeUtilization:P0}");

    if (memUtilization > 0.7 && computeUtilization < 0.3)
    {
        return BottleneckType.MemoryBandwidth;
    }
    else if (computeUtilization > 0.7 && memUtilization < 0.3)
    {
        return BottleneckType.Compute;
    }
    else if (profile.Occupancy < 0.3)
    {
        return BottleneckType.MemoryLatency;
    }
    else if (profile.ExecutionTimeMs < 0.1)
    {
        return BottleneckType.LaunchOverhead;
    }
    else
    {
        return BottleneckType.Synchronization;
    }
}
```

### Optimization Recommendations

```csharp
public string GetOptimizationRecommendation(BottleneckType bottleneck)
{
    return bottleneck switch
    {
        BottleneckType.MemoryBandwidth =>
            "• Improve memory coalescing\n" +
            "• Use shared memory for reused data\n" +
            "• Reduce memory transactions per thread",

        BottleneckType.Compute =>
            "• Increase arithmetic intensity\n" +
            "• Use faster math functions (e.g., __sinf vs sin)\n" +
            "• Consider mixed precision",

        BottleneckType.MemoryLatency =>
            "• Increase occupancy (more warps)\n" +
            "• Prefetch data to hide latency\n" +
            "• Reduce register usage",

        BottleneckType.LaunchOverhead =>
            "• Batch multiple operations\n" +
            "• Use persistent kernels (Ring Kernels)\n" +
            "• Increase work per kernel launch",

        BottleneckType.Synchronization =>
            "• Reduce barrier frequency\n" +
            "• Use warp-level primitives\n" +
            "• Overlap compute and communication",

        _ => "Profile more to identify specific issues"
    };
}
```

## BenchmarkDotNet Integration

```csharp
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class GpuBenchmarks
{
    private IComputeService _service;
    private IBuffer<float> _buffer;

    [Params(1024, 1024*1024, 10*1024*1024)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddDotCompute();
        var provider = services.BuildServiceProvider();
        _service = provider.GetRequiredService<IComputeService>();
        _buffer = _service.CreateBuffer<float>(Size);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _buffer.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task VectorAddBaseline()
    {
        await _service.ExecuteKernelAsync(
            MyKernels.VectorAdd,
            new KernelConfig { BlockSize = 256, GridSize = (Size + 255) / 256 },
            _buffer, _buffer, _buffer);
        await _service.SynchronizeAsync();
    }

    [Benchmark]
    public async Task VectorAddOptimized()
    {
        await _service.ExecuteKernelAsync(
            MyKernels.VectorAddOptimized,
            new KernelConfig { BlockSize = 256, GridSize = (Size + 255) / 256 },
            _buffer, _buffer, _buffer);
        await _service.SynchronizeAsync();
    }
}
```

## Continuous Performance Monitoring

```csharp
public class PerformanceMonitor
{
    private readonly ConcurrentQueue<PerformanceSample> _samples = new();
    private readonly ITimingProvider _timingProvider;

    public void RecordSample(string operation, double durationMs, long dataSize)
    {
        _samples.Enqueue(new PerformanceSample
        {
            Timestamp = DateTime.UtcNow,
            Operation = operation,
            DurationMs = durationMs,
            DataSize = dataSize,
            ThroughputGBps = dataSize / (durationMs / 1000.0) / 1e9
        });

        // Detect performance regression
        var recentSamples = GetRecentSamples(operation, TimeSpan.FromMinutes(5));
        var avgThroughput = recentSamples.Average(s => s.ThroughputGBps);
        var baseline = GetBaseline(operation);

        if (avgThroughput < baseline * 0.8)
        {
            _logger.LogWarning(
                "Performance regression detected in {Operation}: " +
                "{Current:F1} GB/s vs baseline {Baseline:F1} GB/s",
                operation, avgThroughput, baseline);
        }
    }
}
```

## Exercises

### Exercise 1: Kernel Profile

Profile a kernel and identify whether it's compute or memory bound.

### Exercise 2: Pipeline Analysis

Profile a multi-stage pipeline and find the slowest stage.

### Exercise 3: Optimization Benchmark

Optimize a kernel based on profiling data and verify improvement with benchmarks.

## Key Takeaways

1. **GPU-native timing** provides accurate kernel-only measurements
2. **Calibration improves accuracy** - use appropriate strategy for your needs
3. **Multiple iterations** reduce measurement noise
4. **Bottleneck analysis** guides optimization efforts
5. **Continuous monitoring** catches performance regressions

## Path Complete

Congratulations! You've completed the Advanced Learning Path.

**What you learned:**
- Ring Kernel persistent GPU computation
- Barrier and synchronization patterns
- Multi-GPU programming with P2P
- GPU-native performance profiling

**Next steps:**
- [Contributor Path](../contributor/index.md) - Extend DotCompute
- [Ring Kernels Guide](../../guides/ring-kernels/index.md) - Complete reference
- [Performance Guide](../../guides/performance-tuning.md) - Deep dive

## Further Reading

- [GPU Timing API](../../guides/timing-api.md) - Complete API reference
- [Performance Profiling](../../guides/performance-profiling.md) - Advanced techniques
- [Benchmarking](../../performance/benchmarking.md) - Benchmark methodology
