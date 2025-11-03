# Performance Tuning Guide

This guide provides practical techniques for optimizing kernel performance and maximizing throughput in DotCompute applications.

## Performance Profiling

### Measuring Performance

Before optimizing, measure baseline performance:

```csharp
var debugService = services.GetRequiredService<IKernelDebugService>();

var profile = await debugService.ProfileKernelAsync(
    kernelName: "MyKernel",
    parameters: parameters,
    backend: AcceleratorType.CUDA,
    iterations: 1000
);

Console.WriteLine($"Average time: {profile.AverageTime.TotalMicroseconds:F2}μs");
Console.WriteLine($"Min/Max: {profile.MinTime.TotalMicroseconds:F2}μs / {profile.MaxTime.TotalMicroseconds:F2}μs");
Console.WriteLine($"Std dev: {profile.StandardDeviation.TotalMicroseconds:F2}μs");
Console.WriteLine($"Throughput: {profile.Throughput:F0} ops/sec");
Console.WriteLine($"GFLOPS: {profile.GFLOPS:F2}");
```

### Identify Bottlenecks

Use the bottleneck analyzer:

```csharp
var bottlenecks = await debugService.AnalyzeMemoryPatternsAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA
);

Console.WriteLine($"Sequential access rate: {bottlenecks.SequentialAccessRate:P1}");
Console.WriteLine($"Cache hit rate: {bottlenecks.CacheHitRate:P1}");
Console.WriteLine($"Bandwidth utilization: {bottlenecks.BandwidthUtilization:P1}");

foreach (var suggestion in bottlenecks.Suggestions)
{
    Console.WriteLine($"Suggestion: {suggestion}");
}
```

## Memory Optimization

### 1. Memory Pooling

**Problem**: Frequent allocations cause overhead

**Before** (slow):
```csharp
for (int i = 0; i < 1000; i++)
{
    using var buffer = await memoryManager.AllocateAsync<float>(100_000);
    await ProcessData(buffer);
    // Dispose allocates and deallocates every iteration
}
```

**After** (fast):
```csharp
// Allocate once, reuse 1000 times
using var buffer = await memoryManager.AllocateAsync<float>(100_000);

for (int i = 0; i < 1000; i++)
{
    await ProcessData(buffer);
}
```

**Performance Improvement**: 11.2x faster (measured)

### 2. Zero-Copy Operations

**Problem**: Unnecessary data copies

**Before** (copies):
```csharp
var inputArray = new float[1_000_000];
var buffer = await memoryManager.AllocateAsync<float>(1_000_000);
await buffer.CopyFromAsync(inputArray);  // Copy!
await kernel.ExecuteAsync(buffer);
var outputArray = new float[1_000_000];
await buffer.CopyToAsync(outputArray);   // Copy!
```

**After** (zero-copy on CPU):
```csharp
var inputArray = new float[1_000_000];
var buffer = memoryManager.CreateBuffer(inputArray, BufferMode.ReadWrite);
await kernel.ExecuteAsync(buffer);
// No copies needed - direct Span<T> access
var result = buffer.AsSpan();
```

**Performance Improvement**: 10x faster for CPU execution

### 3. Pinned Memory

**Problem**: Slow CPU-GPU transfers

**Before** (non-pinned):
```csharp
var buffer = await memoryManager.AllocateAsync<float>(
    1_000_000,
    AllocationMode.Default
);
// Transfer: ~6 GB/s
```

**After** (pinned):
```csharp
var buffer = await memoryManager.AllocateAsync<float>(
    1_000_000,
    AllocationMode.Pinned
);
// Transfer: ~16 GB/s (2.7x faster)
```

**Performance Improvement**: 2-3x faster transfers

### 4. Unified Memory (Apple Silicon)

**Problem**: Explicit transfers on unified memory systems

**Before** (unnecessary transfers):
```csharp
var buffer = await memoryManager.AllocateAsync<float>(1_000_000);
await buffer.CopyFromAsync(sourceData);  // Unnecessary copy!
await kernel.ExecuteAsync(buffer);
await buffer.CopyToAsync(resultData);    // Unnecessary copy!
```

**After** (unified memory):
```csharp
var buffer = await memoryManager.AllocateAsync<float>(
    1_000_000,
    AllocationMode.Unified
);
// CPU and GPU access same physical memory - no copies!
var span = buffer.AsSpan();
sourceData.CopyTo(span);
await kernel.ExecuteAsync(buffer);
buffer.AsSpan().CopyTo(resultData);
```

**Performance Improvement**: 2-3x faster on Apple Silicon

## Kernel Optimization

### 1. Memory Access Patterns

**Sequential Access** (fast):
```csharp
[Kernel]
public static void Sequential(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // Sequential: CPU cache-friendly, GPU coalesced
        output[idx] = input[idx] * 2;
    }
}
```

**Strided Access** (slower):
```csharp
[Kernel]
public static void Strided(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // Strided: Less cache-friendly, GPU may not coalesce
        output[idx] = input[idx * stride] * 2;
    }
}
```

**Random Access** (slowest):
```csharp
[Kernel]
public static void Random(
    ReadOnlySpan<float> input,
    ReadOnlySpan<int> indices,
    Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // Random: Cache-unfriendly, GPU scatter/gather
        output[idx] = input[indices[idx]] * 2;
    }
}
```

**Performance Impact**:
- Sequential: 100% bandwidth utilization
- Strided (stride=2): ~80% bandwidth
- Strided (stride=16): ~50% bandwidth
- Random: ~20% bandwidth

### 2. Data Reuse

**Before** (reads `input[idx]` three times):
```csharp
[Kernel]
public static void NoReuse(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] + input[idx] * input[idx];  // 3 reads!
    }
}
```

**After** (reads once, reuses):
```csharp
[Kernel]
public static void WithReuse(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        float value = input[idx];  // Read once
        output[idx] = value + value * value;  // Reuse
    }
}
```

**Performance Improvement**: 1.5-2x faster (memory traffic reduced)

### 3. Reduce Branching

**Branchy** (divergent warps on GPU):
```csharp
[Kernel]
public static void Branchy(ReadOnlySpan<float> input, Span<float> output, float threshold)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        if (input[idx] > threshold)  // Branch divergence!
        {
            output[idx] = input[idx] * 2;
        }
        else
        {
            output[idx] = input[idx] / 2;
        }
    }
}
```

**Branch-Free** (better for GPU):
```csharp
[Kernel]
public static void BranchFree(ReadOnlySpan<float> input, Span<float> output, float threshold)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        float value = input[idx];
        float multiplier = (value > threshold) ? 2.0f : 0.5f;
        output[idx] = value * multiplier;  // Conditional move, not branch
    }
}
```

**Performance Improvement**: 2-4x faster on GPU (depends on branch divergence)

### 4. Loop Unrolling

**Regular Loop**:
```csharp
[Kernel]
public static void RegularLoop(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X * 4;
    if (idx < output.Length)
    {
        for (int i = 0; i < 4; i++)
        {
            output[idx + i] = input[idx + i] * 2;
        }
    }
}
```

**Unrolled Loop**:
```csharp
[Kernel]
public static void UnrolledLoop(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X * 4;
    if (idx + 3 < output.Length)
    {
        output[idx + 0] = input[idx + 0] * 2;
        output[idx + 1] = input[idx + 1] * 2;
        output[idx + 2] = input[idx + 2] * 2;
        output[idx + 3] = input[idx + 3] * 2;
    }
}
```

**Performance Improvement**: 1.2-1.5x faster (reduced loop overhead)

### 5. Precision Selection

**Double Precision** (slower):
```csharp
[Kernel]
public static void DoublePrecision(ReadOnlySpan<double> input, Span<double> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2.0;  // 2-8x slower on most GPUs
    }
}
```

**Single Precision** (faster):
```csharp
[Kernel]
public static void SinglePrecision(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2.0f;  // Fast on all GPUs
    }
}
```

**Performance Impact**: 2-8x faster with `float` vs `double` on most GPUs

## Batching Operations

### Problem: Many Small Kernel Calls

**Before** (slow):
```csharp
for (int i = 0; i < 1000; i++)
{
    await orchestrator.ExecuteKernelAsync("SmallKernel", data[i]);
    // 1000 kernel launches = high overhead
}
```

**After** (fast):
```csharp
// Combine into single large kernel
await orchestrator.ExecuteKernelAsync("BatchKernel", allData);
// Single kernel launch
```

**Performance Improvement**: 10-100x faster (kernel launch overhead eliminated)

### Batch Size Tuning

```csharp
// Too small: High overhead
const int batchSize = 100;

// Too large: May exceed GPU memory
const int batchSize = 100_000_000;

// Just right: Maximize GPU utilization without exceeding memory
const int batchSize = CalculateOptimalBatchSize(availableMemory, dataSize);

private static int CalculateOptimalBatchSize(long availableMemory, int elementSize)
{
    // Use 80% of available memory
    long usableMemory = (long)(availableMemory * 0.8);
    int maxElements = (int)(usableMemory / elementSize);

    // Round down to power of 2 for efficient indexing
    return (int)Math.Pow(2, Math.Floor(Math.Log2(maxElements)));
}
```

## Parallelization Strategies

### 1. CPU Multi-Threading

DotCompute automatically uses `Parallel.For` on CPU:

```csharp
[Kernel]
public static void ParallelCPU(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;
    }
}
// Automatically uses all CPU cores
```

**Control Thread Count**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.MaxCpuThreads = Environment.ProcessorCount; // Default
    // or
    options.MaxCpuThreads = Environment.ProcessorCount / 2; // Half cores
});
```

### 2. GPU Thread Configuration

**1D Grid** (typical):
```csharp
// Automatically configured:
// - Threads per block: 256 (typical)
// - Blocks: Ceiling(dataSize / 256)
await orchestrator.ExecuteKernelAsync("MyKernel", parameters);
```

**Manual Configuration** (advanced):
```csharp
var compilationOptions = new CompilationOptions
{
    ThreadsPerBlock = 512,  // Must be multiple of 32 (warp size)
    BlocksPerGrid = (dataSize + 511) / 512
};

await orchestrator.ExecuteKernelAsync("MyKernel", parameters, compilationOptions);
```

**Guidelines**:
- **Threads per block**: 128-512 (256 is good default)
- **Occupancy**: Aim for 50%+ occupancy
- **Warp size**: Multiple of 32 for NVIDIA, 64 for AMD

### 3. Pipeline Parallelism

**Before** (sequential):
```csharp
await kernel1.ExecuteAsync(data1);
await kernel2.ExecuteAsync(data2);
await kernel3.ExecuteAsync(data3);
// Total time: T1 + T2 + T3
```

**After** (pipelined):
```csharp
var task1 = kernel1.ExecuteAsync(data1);
var task2 = kernel2.ExecuteAsync(data2);
var task3 = kernel3.ExecuteAsync(data3);
await Task.WhenAll(task1, task2, task3);
// Total time: Max(T1, T2, T3)
```

**Performance Improvement**: 2-3x faster (overlapped execution)

## Backend-Specific Optimizations

### CUDA Optimization

**1. Coalesced Memory Access**:
```csharp
// ✅ Coalesced: Adjacent threads access adjacent memory
[Kernel]
public static void Coalesced(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx];  // Thread 0: addr 0, Thread 1: addr 4, etc.
    }
}

// ❌ Uncoalesced: Adjacent threads access strided memory
[Kernel]
public static void Uncoalesced(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx * stride];  // Poor memory access pattern
    }
}
```

**2. Occupancy Optimization**:
```csharp
// Check occupancy
var capabilities = cudaAccelerator.Capabilities;
int maxThreadsPerBlock = capabilities.MaxThreadsPerBlock;  // e.g., 1024

// Calculate optimal threads per block
int threadsPerBlock = Math.Min(256, maxThreadsPerBlock);  // 256 is good default

// Ensure enough blocks for full occupancy
int blocksPerGrid = (dataSize + threadsPerBlock - 1) / threadsPerBlock;
int minBlocksForFullOccupancy = capabilities.MultiProcessorCount * 2;

if (blocksPerGrid < minBlocksForFullOccupancy)
{
    // Reduce threads per block to increase block count
    threadsPerBlock = 128;
    blocksPerGrid = (dataSize + threadsPerBlock - 1) / threadsPerBlock;
}
```

### Metal Optimization

**1. Threadgroup Memory** (shared memory):
```csharp
[Kernel]
public static void WithThreadgroupMemory(
    ReadOnlySpan<float> input,
    Span<float> output,
    int n)
{
    // Metal automatically allocates threadgroup memory for local arrays
    // Not yet exposed in C# kernel syntax - use direct MSL for now
}
```

**2. Unified Memory**:
```csharp
// Leverage zero-copy unified memory on Apple Silicon
var buffer = await memoryManager.AllocateAsync<float>(
    1_000_000,
    AllocationMode.Unified
);

// CPU can write directly
buffer.AsSpan()[0] = 42;

// GPU reads directly (no explicit transfer)
await orchestrator.ExecuteKernelAsync("MyKernel", new { buffer });

// CPU reads directly (no explicit transfer)
var result = buffer.AsSpan()[0];
```

### CPU Optimization

**1. SIMD Vectorization**:
```csharp
// Automatically vectorized by source generator
[Kernel]
public static void AutoVectorized(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;  // Becomes Vector<float> operation
    }
}
// On AVX512: Processes 16 floats per instruction
// On AVX2: Processes 8 floats per instruction
// On SSE4.2: Processes 4 floats per instruction
```

**2. Cache Optimization**:
```csharp
// ✅ Cache-friendly: Sequential access
[Kernel]
public static void CacheFriendly(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;
    }
}

// ❌ Cache-unfriendly: Large stride
[Kernel]
public static void CacheUnfriendly(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx * 1000] * 2;  // Cache misses!
    }
}
```

## Common Performance Pitfalls

### Pitfall 1: Not Warming Up

**Problem**: First execution includes compilation time

```csharp
// ❌ Bad: Benchmark includes compilation
var stopwatch = Stopwatch.StartNew();
await orchestrator.ExecuteKernelAsync("MyKernel", parameters);
stopwatch.Stop();
Console.WriteLine($"Time: {stopwatch.Elapsed.TotalMilliseconds}ms");
// May show 200ms instead of 2ms!

// ✅ Good: Warm up first
await orchestrator.ExecuteKernelAsync("MyKernel", parameters);  // Warm-up

var stopwatch = Stopwatch.StartNew();
for (int i = 0; i < 100; i++)
{
    await orchestrator.ExecuteKernelAsync("MyKernel", parameters);
}
stopwatch.Stop();
Console.WriteLine($"Avg time: {stopwatch.Elapsed.TotalMilliseconds / 100:F2}ms");
```

### Pitfall 2: Synchronous Waits

**Problem**: Not overlapping CPU and GPU work

```csharp
// ❌ Bad: CPU waits for each GPU operation
for (int i = 0; i < 10; i++)
{
    await orchestrator.ExecuteKernelAsync("Kernel1", data[i]);
    ProcessOnCPU(data[i]);  // CPU work while GPU idle
}

// ✅ Good: Overlap CPU and GPU work
var gpuTasks = new List<Task>();
for (int i = 0; i < 10; i++)
{
    gpuTasks.Add(orchestrator.ExecuteKernelAsync("Kernel1", data[i]));
    ProcessOnCPU(data[i - 1]);  // Process previous result while GPU works
}
await Task.WhenAll(gpuTasks);
```

### Pitfall 3: Unnecessary Synchronization

**Problem**: Forcing synchronization between independent operations

```csharp
// ❌ Bad: Forced synchronization
await orchestrator.ExecuteKernelAsync("Kernel1", data1);
await accelerator.SynchronizeAsync();  // Unnecessary!
await orchestrator.ExecuteKernelAsync("Kernel2", data2);

// ✅ Good: Let runtime manage synchronization
await orchestrator.ExecuteKernelAsync("Kernel1", data1);
await orchestrator.ExecuteKernelAsync("Kernel2", data2);
// Runtime automatically handles dependencies
```

### Pitfall 4: Small Data on GPU

**Problem**: Transfer overhead exceeds compute time

```csharp
// ❌ Bad: GPU for tiny data
var tinyData = new float[100];
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    new { tinyData },
    forceBackend: AcceleratorType.CUDA
);
// Transfer time (50μs) >> Compute time (1μs)

// ✅ Good: CPU for small data
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    new { tinyData },
    forceBackend: AcceleratorType.CPU
);
// No transfer, compute time ~1μs
```

## Performance Monitoring

### OpenTelemetry Integration

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddDotComputeInstrumentation())
    .WithTracing(tracing => tracing
        .AddDotComputeInstrumentation());

// Metrics collected:
// - dotcompute.kernel.executions (count)
// - dotcompute.kernel.duration (histogram)
// - dotcompute.memory.allocated (count)
// - dotcompute.memory.transferred (histogram)
// - dotcompute.backend.selection_time (histogram)
```

### Real-Time Performance Dashboard

```csharp
services.AddDotComputeRuntime(options =>
{
    options.EnableTelemetry = true;
    options.TelemetrySamplingRate = 0.1;  // Sample 10% of executions
});

// Query performance data
var telemetryProvider = services.GetRequiredService<ITelemetryProvider>();
var metrics = await telemetryProvider.GetMetricsAsync(TimeSpan.FromHours(1));

Console.WriteLine($"Total executions: {metrics.TotalExecutions}");
Console.WriteLine($"Average duration: {metrics.AverageDuration.TotalMilliseconds:F2}ms");
Console.WriteLine($"P50: {metrics.P50.TotalMilliseconds:F2}ms");
Console.WriteLine($"P95: {metrics.P95.TotalMilliseconds:F2}ms");
Console.WriteLine($"P99: {metrics.P99.TotalMilliseconds:F2}ms");
```

## Performance Checklist

### Before Optimization

- [ ] Profile baseline performance
- [ ] Identify bottleneck (CPU, GPU, memory, transfer)
- [ ] Measure memory bandwidth utilization
- [ ] Check cache hit rates
- [ ] Analyze memory access patterns

### Memory Optimization

- [ ] Use memory pooling for frequent allocations
- [ ] Reuse buffers across iterations
- [ ] Use pinned memory for CPU-GPU transfers
- [ ] Use unified memory on Apple Silicon
- [ ] Batch small operations

### Kernel Optimization

- [ ] Ensure sequential/coalesced memory access
- [ ] Reduce branching in GPU kernels
- [ ] Reuse loaded values
- [ ] Use appropriate precision (float vs double)
- [ ] Check bounds only once per thread

### Backend Selection

- [ ] Profile on both CPU and GPU
- [ ] Use automatic selection for production
- [ ] Enable ML-optimized profile for long-running apps
- [ ] Verify GPU is being used for large data

### Monitoring

- [ ] Enable telemetry in production
- [ ] Monitor P95 and P99 latencies
- [ ] Track memory usage trends
- [ ] Set up alerts for performance regressions

## Further Reading

- [Kernel Development Guide](kernel-development.md) - Writing efficient kernels
- [Backend Selection Guide](backend-selection.md) - Choosing optimal backend
- [Memory Management Guide](memory-management.md) - Memory best practices
- [Architecture: Optimization Engine](../architecture/optimization-engine.md) - ML-powered selection
- [Architecture: Memory Management](../architecture/memory-management.md) - Memory system design

---

**Measure • Optimize • Validate • Repeat**
