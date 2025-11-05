# Backend Selection Guide

This guide helps you understand DotCompute's backend selection system and how to choose the optimal backend for your workload.

## Available Backends

### CPU Backend (Always Available)

**Status**: ✅ Production Ready

**Capabilities**:
- SIMD vectorization (AVX512, AVX2, SSE4.2, NEON)
- Multi-threaded execution via `Parallel.For`
- Zero-copy operations with `Span<T>`
- Hardware capability auto-detection

**Performance**: 8-23x speedup on vectorizable operations vs scalar code

**When to Use**:
- Small data (< 10,000 elements)
- Memory-bound operations (low compute intensity)
- Sequential memory access patterns
- No GPU available
- High data locality (fits in CPU cache)

**Example**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.CPU;
});
```

### CUDA Backend (NVIDIA GPUs)

**Status**: ✅ Production Ready

**Requirements**:
- NVIDIA GPU with Compute Capability 5.0+
- CUDA Toolkit 12.0+
- Windows, Linux, or WSL2

**Capabilities**:
- NVRTC runtime compilation
- PTX and CUBIN generation
- Unified memory support
- P2P GPU-to-GPU transfers (NVLink)
- Compute Capability 5.0-8.9 support

**Performance**: 21-92x speedup vs CPU (measured on RTX 2000 Ada)

**When to Use**:
- Large data (> 1M elements)
- Highly parallel workloads
- Compute-intensive operations
- Matrix operations
- Deep learning inference

**Example**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.CUDA;
});
```

### Metal Backend (Apple Silicon)

**Status**: ✅ Production Ready

**Requirements**:
- Apple Silicon Mac (M1/M2/M3)
- macOS 11.0+ (Big Sur)

**Capabilities**:
- Metal Performance Shaders (MPS): Batch normalization, max pooling 2D
- Advanced memory pooling: 90% allocation reduction (power-of-2 buckets)
- MTLBinaryArchive: Kernel binary caching (macOS 11.0+)
- Unified memory architecture
- GPU family auto-detection (Apple7/8/9)
- Command buffer management
- Zero-copy CPU-GPU access

**Performance**: 37-141x speedup vs CPU, 2-3x unified memory advantage

**When to Use**:
- Running on Apple Silicon Macs
- Applications requiring unified memory
- Video processing, image operations
- Metal-optimized workloads

**Example**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.Metal;
});
```

### OpenCL Backend (Cross-Platform)

**Status**: 🚧 Foundation Complete

**Requirements**:
- OpenCL 1.2+ compatible GPU
- OpenCL runtime installed

**Capabilities**:
- Platform and device enumeration
- Kernel compilation
- Memory management

**When to Use**:
- AMD GPUs
- Intel integrated graphics
- Cross-platform GPU code
- Legacy GPU support

**Example**:
```csharp
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.OpenCL;
});
```

## Automatic Backend Selection

### Default Behavior

By default, DotCompute automatically selects the best backend based on workload characteristics:

```csharp
// Automatic selection (default)
services.AddDotComputeRuntime();  // No options needed

await orchestrator.ExecuteKernelAsync("MyKernel", parameters);
// Backend chosen automatically based on data size, parallelism, etc.
```

### Selection Criteria

The automatic selector considers:

1. **Data Size**
   - < 10,000 elements → CPU (no transfer overhead)
   - 10,000-1M elements → GPU or CPU (depends on other factors)
   - > 1M elements → GPU (if available)

2. **Compute Intensity**
   - Low (< 10 ops/byte) → CPU (memory-bound)
   - Medium (10-100 ops/byte) → GPU or CPU
   - High (> 100 ops/byte) → GPU (compute-bound)

3. **Parallelism Potential**
   - Low (< 100 tasks) → CPU
   - Medium (100-10K tasks) → GPU or CPU
   - High (> 10K tasks) → GPU

4. **Memory Access Pattern**
   - Sequential → CPU (cache advantage)
   - Random → GPU (higher bandwidth)
   - Strided → Depends on stride size

5. **Device Availability**
   - No GPU → CPU
   - NVIDIA GPU → CUDA
   - Apple Silicon → Metal
   - AMD GPU → OpenCL

### Selection Flow

```
Kernel Execution Request
    ↓
Is data < 10,000 elements?
    Yes → CPU
    No → Continue
    ↓
Is compute intensity low?
    Yes → CPU
    No → Continue
    ↓
Is parallelism high?
    Yes → GPU (if available)
    No → Continue
    ↓
Is GPU available?
    Yes → GPU
    No → CPU
```

## Manual Backend Selection

### Force Specific Backend

```csharp
// Force CUDA
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    parameters,
    forceBackend: AcceleratorType.CUDA
);

// Force CPU
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    parameters,
    forceBackend: AcceleratorType.CPU
);

// Force Metal
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    parameters,
    forceBackend: AcceleratorType.Metal
);
```

### Set Default Backend

```csharp
services.AddDotComputeRuntime(options =>
{
    options.DefaultAccelerator = AcceleratorType.CUDA;
    options.EnableAutoOptimization = false;  // Disable automatic selection
});
```

### Check Backend Availability

```csharp
var acceleratorManager = services.GetRequiredService<IAcceleratorManager>();

if (acceleratorManager.IsAvailable(AcceleratorType.CUDA))
{
    Console.WriteLine("CUDA is available");
}

var availableBackends = acceleratorManager.GetAvailableBackends();
foreach (var backend in availableBackends)
{
    Console.WriteLine($"Available: {backend}");
}
```

## Optimization Profiles

### Conservative Profile

**Goal**: Safety over performance

```csharp
services.AddProductionOptimization(options =>
{
    options.Profile = OptimizationProfile.Conservative;
});
```

**Behavior**:
- Prefers CPU for ambiguous cases
- Only uses GPU for clear performance wins
- Minimal risk of sub-optimal selection

**Use When**:
- Production systems with strict SLAs
- Unknown workload patterns
- Prioritizing reliability

### Balanced Profile (Default)

**Goal**: Balance performance and reliability

```csharp
services.AddProductionOptimization(options =>
{
    options.Profile = OptimizationProfile.Balanced;  // Default
});
```

**Behavior**:
- Uses heuristics with historical data
- Falls back to safe defaults when uncertain
- 70-80% optimal backend selection

**Use When**:
- General-purpose applications
- Mixed workload patterns
- Good default for most users

### Aggressive Profile

**Goal**: Maximum performance

```csharp
services.AddProductionOptimization(options =>
{
    options.Profile = OptimizationProfile.Aggressive;
});
```

**Behavior**:
- Prefers GPU for most workloads
- Uses ML model when available
- Accepts occasional sub-optimal selections for higher peak performance

**Use When**:
- Performance-critical applications
- Known GPU-friendly workloads
- Can tolerate occasional slower execution

### ML-Optimized Profile

**Goal**: Learn optimal selection from execution patterns

```csharp
services.AddProductionOptimization(options =>
{
    options.Profile = OptimizationProfile.MLOptimized;
    options.EnableMachineLearning = true;
    options.EnablePerformanceLearning = true;
});
```

**Behavior**:
- Learns from execution history
- Improves selection over time
- 85-95% optimal after learning period

**Use When**:
- Long-running applications
- Repetitive workload patterns
- Can tolerate initial learning phase

**Performance Improvement**: 10-30% average speedup after 1,000+ executions

## Workload Analysis

### Understanding Your Workload

Use the profiler to understand workload characteristics:

```csharp
var debugService = services.GetRequiredService<IKernelDebugService>();

// Profile on CPU
var cpuProfile = await debugService.ProfileKernelAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CPU,
    iterations: 100
);

// Profile on GPU
var gpuProfile = await debugService.ProfileKernelAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    iterations: 100
);

// Compare
Console.WriteLine($"CPU: {cpuProfile.AverageTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"GPU: {gpuProfile.AverageTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"Speedup: {cpuProfile.AverageTime.TotalMilliseconds / gpuProfile.AverageTime.TotalMilliseconds:F2}x");
```

### Compute Intensity Estimation

```csharp
// Low intensity (memory-bound): Use CPU
// Example: Vector addition (2 reads, 1 write, 1 add = 1 op / 12 bytes = 0.08 ops/byte)
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];  // Low compute intensity
    }
}

// High intensity (compute-bound): Use GPU
// Example: Matrix multiplication (K operations per element)
[Kernel]
public static void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int K)
{
    int idx = Kernel.ThreadId.X;
    if (idx < c.Length)
    {
        float sum = 0;
        for (int k = 0; k < K; k++)  // K operations per output element
        {
            sum += a[idx * K + k] * b[k];
        }
        c[idx] = sum;  // High compute intensity (K ops / 8 bytes ≈ K/8 ops/byte)
    }
}
```

### Memory Access Pattern Analysis

```csharp
// Sequential access: CPU-friendly
[Kernel]
public static void Sequential(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;  // Sequential: CPU cache-friendly
    }
}

// Random access: GPU-friendly (higher bandwidth)
[Kernel]
public static void Gather(ReadOnlySpan<float> input, ReadOnlySpan<int> indices, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[indices[idx]];  // Random: GPU bandwidth helps
    }
}
```

## Backend-Specific Optimizations

### CUDA-Specific

```csharp
// Check compute capability
var cudaAccelerator = await acceleratorManager.GetOrCreateAcceleratorAsync(AcceleratorType.CUDA);
var capabilities = cudaAccelerator.Capabilities;

Console.WriteLine($"Max threads per block: {capabilities.MaxThreadsPerBlock}");
Console.WriteLine($"Max shared memory: {capabilities.MaxSharedMemoryPerWorkGroup} bytes");
Console.WriteLine($"Supports double precision: {capabilities.SupportsDouble}");

// Use P2P transfers between GPUs
if (capabilities.ExtendedCapabilities.TryGetValue("P2PSupport", out var p2pSupport) && (bool)p2pSupport)
{
    // Enable P2P transfers between GPUs
    var p2pManager = services.GetRequiredService<P2PManager>();
    await p2pManager.EnablePeerAccessAsync(deviceId1: 0, deviceId2: 1);
}
```

### Metal-Specific

```csharp
// Check GPU family
var metalAccelerator = await acceleratorManager.GetOrCreateAcceleratorAsync(AcceleratorType.Metal);
var capabilities = metalAccelerator.Capabilities;

if (capabilities.ExtendedCapabilities.TryGetValue("GPUFamily", out var family))
{
    Console.WriteLine($"GPU Family: {family}");  // Apple7, Apple8, or Apple9
}

// Leverage unified memory
if (capabilities.SupportsUnifiedMemory)
{
    // Use AllocationMode.Unified for zero-copy access
    var buffer = await memoryManager.AllocateAsync<float>(
        1_000_000,
        AllocationMode.Unified
    );
}
```

### CPU-Specific

```csharp
// Check SIMD support
var cpuAccelerator = await acceleratorManager.GetOrCreateAcceleratorAsync(AcceleratorType.CPU);
var capabilities = cpuAccelerator.Capabilities;

// Determine available instruction sets
if (Vector.IsHardwareAccelerated)
{
    Console.WriteLine($"SIMD vector size: {Vector<float>.Count}");
}

if (Avx512F.IsSupported)
{
    Console.WriteLine("AVX-512 available (512-bit vectors)");
}
else if (Avx2.IsSupported)
{
    Console.WriteLine("AVX2 available (256-bit vectors)");
}
else if (Sse42.IsSupported)
{
    Console.WriteLine("SSE4.2 available (128-bit vectors)");
}
```

## Decision Tree

### Should I Use CPU or GPU?

```
START
  ↓
Is data size < 10,000 elements?
  Yes → USE CPU
  No → Continue
  ↓
Is compute intensity < 10 ops/byte?
  Yes → USE CPU
  No → Continue
  ↓
Is memory access sequential?
  Yes → BENCHMARK BOTH
  No → Continue
  ↓
Is GPU available?
  Yes → USE GPU
  No → USE CPU
```

### Real-World Examples

**Use Case 1: Image Blur (3x3 kernel)**
- **Data Size**: 1920×1080 = 2M pixels
- **Compute Intensity**: 9 operations per pixel = ~2 ops/byte
- **Memory Access**: Spatial locality (mostly sequential)
- **Parallelism**: Very high (2M independent operations)
- **Recommendation**: GPU (37-141x speedup measured)

**Use Case 2: Vector Addition**
- **Data Size**: 1M elements
- **Compute Intensity**: 1 operation per element = 0.08 ops/byte
- **Memory Access**: Sequential
- **Parallelism**: High
- **Recommendation**: CPU for small sizes, GPU for large sizes

**Use Case 3: Matrix Multiplication (512×512)**
- **Data Size**: 512×512 = 262K elements
- **Compute Intensity**: 512 operations per element = 64 ops/byte
- **Memory Access**: Complex (both sequential and random)
- **Parallelism**: Very high
- **Recommendation**: GPU (21-92x speedup measured)

## Benchmarking Guide

### Comparing Backends

```csharp
public static async Task BenchmarkBackends()
{
    var orchestrator = GetService<IComputeOrchestrator>();
    var parameters = GenerateTestData();

    // Benchmark CPU
    var cpuTime = await BenchmarkBackend(orchestrator, AcceleratorType.CPU, parameters);

    // Benchmark CUDA
    var cudaTime = await BenchmarkBackend(orchestrator, AcceleratorType.CUDA, parameters);

    // Benchmark Metal
    var metalTime = await BenchmarkBackend(orchestrator, AcceleratorType.Metal, parameters);

    // Print results
    Console.WriteLine($"CPU:   {cpuTime:F2}ms");
    Console.WriteLine($"CUDA:  {cudaTime:F2}ms (speedup: {cpuTime / cudaTime:F2}x)");
    Console.WriteLine($"Metal: {metalTime:F2}ms (speedup: {cpuTime / metalTime:F2}x)");
}

private static async Task<double> BenchmarkBackend(
    IComputeOrchestrator orchestrator,
    AcceleratorType backend,
    object parameters)
{
    // Warm-up
    await orchestrator.ExecuteKernelAsync("MyKernel", parameters, forceBackend: backend);

    // Benchmark
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < 100; i++)
    {
        await orchestrator.ExecuteKernelAsync("MyKernel", parameters, forceBackend: backend);
    }
    stopwatch.Stop();

    return stopwatch.Elapsed.TotalMilliseconds / 100;
}
```

## Troubleshooting

### GPU Not Being Used

**Symptom**: Kernels always run on CPU even though GPU is available

**Causes**:
1. Data size too small (< 10,000 elements)
2. `EnableAutoOptimization = false` with `DefaultAccelerator = CPU`
3. GPU not detected

**Solution**:
```csharp
// Check GPU availability
var manager = services.GetRequiredService<IAcceleratorManager>();
if (!manager.IsAvailable(AcceleratorType.CUDA))
{
    Console.WriteLine("CUDA not available. Reasons:");
    // - No NVIDIA GPU
    // - CUDA Toolkit not installed
    // - Driver version mismatch
}

// Force GPU usage
await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    parameters,
    forceBackend: AcceleratorType.CUDA
);
```

### Slower on GPU Than CPU

**Symptom**: GPU execution is slower than CPU

**Causes**:
1. Data transfer overhead dominates (small data)
2. Low parallelism (few threads)
3. Memory-bound operation
4. First execution (compilation overhead)

**Solution**:
```csharp
// Profile both backends
var debugService = GetService<IKernelDebugService>();

var cpuProfile = await debugService.ProfileKernelAsync(
    "MyKernel", parameters, AcceleratorType.CPU, iterations: 100
);

var gpuProfile = await debugService.ProfileKernelAsync(
    "MyKernel", parameters, AcceleratorType.CUDA, iterations: 100
);

Console.WriteLine($"CPU avg: {cpuProfile.AverageTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"GPU avg: {gpuProfile.AverageTime.TotalMilliseconds:F2}ms");
Console.WriteLine($"GPU transfer overhead: {EstimateTransferTime(parameters)}ms");
```

## Best Practices

### ✅ Do

1. **Trust automatic selection** for most workloads
2. **Profile before forcing backends** - measure, don't guess
3. **Use ML-optimized profile** for long-running apps
4. **Check GPU availability** before assuming GPU execution
5. **Benchmark with realistic data sizes** - small test data may favor CPU

### ❌ Don't

1. **Don't force GPU for all workloads** - CPU is faster for small data
2. **Don't ignore transfer overhead** - factor in CPU→GPU→CPU time
3. **Don't benchmark first execution** - compilation skews results
4. **Don't assume GPU is always faster** - profile your specific workload

## Further Reading

- [Performance Tuning Guide](performance-tuning.md) - Optimize execution
- [Architecture: Backend Integration](../architecture/backend-integration.md) - Technical details
- [Architecture: Optimization Engine](../architecture/optimization-engine.md) - Selection algorithms
- [Multi-GPU Programming](multi-gpu.md) - Using multiple GPUs

---

**Choose Wisely • Profile First • Trust the Optimizer**
