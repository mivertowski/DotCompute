# Kernel Performance

This module covers kernel optimization techniques including thread configuration, occupancy analysis, and profiling.

## Understanding GPU Execution

### Execution Hierarchy

```
Grid
├── Block 0
│   ├── Warp 0 (threads 0-31)
│   ├── Warp 1 (threads 32-63)
│   └── ...
├── Block 1
│   └── ...
└── ...
```

**Key concepts:**
- **Warp**: 32 threads executing in lockstep (NVIDIA) / 64 threads (AMD)
- **Block**: Group of warps sharing resources
- **Grid**: All blocks in a kernel launch

### Resource Limits

| Resource | Typical Limit | Impact |
|----------|---------------|--------|
| Threads per block | 1024 | Max parallelism per SM |
| Registers per thread | 255 | Spilling reduces performance |
| Shared memory | 48-164 KB | Limits block size |
| Blocks per SM | 16-32 | Concurrent execution |

## Thread Configuration

### Calculating Optimal Configuration

```csharp
public static KernelConfig CalculateConfig(int dataSize, IComputeBackend backend)
{
    // Get device limits
    int maxThreads = backend.MaxThreadsPerBlock;     // e.g., 1024
    int warpSize = backend.WarpSize;                 // e.g., 32

    // Choose block size (multiple of warp size)
    int blockSize = Math.Min(256, maxThreads);       // 256 is often optimal

    // Calculate grid size to cover all data
    int gridSize = (dataSize + blockSize - 1) / blockSize;

    return new KernelConfig
    {
        BlockSize = blockSize,
        GridSize = gridSize
    };
}
```

### Multi-dimensional Configuration

```csharp
// 2D kernel for image processing
public static KernelConfig2D CalculateConfig2D(int width, int height)
{
    // 16x16 = 256 threads per block (good default for 2D)
    var blockDim = new Dim3(16, 16, 1);

    var gridDim = new Dim3(
        (width + blockDim.X - 1) / blockDim.X,
        (height + blockDim.Y - 1) / blockDim.Y,
        1);

    return new KernelConfig2D
    {
        BlockDim = blockDim,
        GridDim = gridDim
    };
}

[Kernel]
public static void ProcessImage(
    ReadOnlySpan2D<byte> input,
    Span2D<byte> output,
    int width, int height)
{
    int x = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadId.X;
    int y = Kernel.BlockId.Y * Kernel.BlockDim.Y + Kernel.ThreadId.Y;

    if (x < width && y < height)
    {
        output[y, x] = ProcessPixel(input[y, x]);
    }
}
```

## Occupancy Optimization

### What is Occupancy?

Occupancy = Active Warps / Maximum Warps per SM

Higher occupancy helps hide memory latency through warp switching.

### Factors Affecting Occupancy

| Factor | Effect |
|--------|--------|
| Block size | Fewer threads = more blocks can fit |
| Register usage | More registers = fewer blocks |
| Shared memory | More shared = fewer blocks |

### Querying Occupancy

```csharp
var occupancyInfo = await computeService.GetOccupancyAsync(
    MyKernels.VectorAdd,
    blockSize: 256);

Console.WriteLine($"Theoretical occupancy: {occupancyInfo.TheoreticalOccupancy:P0}");
Console.WriteLine($"Active blocks per SM: {occupancyInfo.ActiveBlocksPerSM}");
Console.WriteLine($"Active warps per SM: {occupancyInfo.ActiveWarpsPerSM}");
Console.WriteLine($"Registers per thread: {occupancyInfo.RegistersPerThread}");
Console.WriteLine($"Shared memory per block: {occupancyInfo.SharedMemoryPerBlock}");
```

### Auto-tuning Block Size

```csharp
public static async Task<int> FindOptimalBlockSize(
    IComputeService service,
    Delegate kernel)
{
    var blockSizes = new[] { 64, 128, 256, 512, 1024 };
    var best = (blockSize: 256, occupancy: 0.0);

    foreach (var blockSize in blockSizes)
    {
        var info = await service.GetOccupancyAsync(kernel, blockSize);
        if (info.TheoreticalOccupancy > best.occupancy)
        {
            best = (blockSize, info.TheoreticalOccupancy);
        }
    }

    return best.blockSize;
}
```

## Reducing Register Pressure

### Problem: Register Spilling

When a kernel uses too many registers, variables spill to slow local memory.

```csharp
// BAD: Many intermediate variables
[Kernel]
public static void TooManyRegisters(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    float a = data[idx];
    float b = a * 2.0f;
    float c = b + 1.0f;
    float d = c * c;
    float e = MathF.Sin(d);
    float f = MathF.Cos(d);
    float g = e + f;
    float h = g * 0.5f;
    // ... more variables
    data[idx] = h;
}

// BETTER: Reuse variables
[Kernel]
public static void FewerRegisters(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    float val = data[idx];
    val = val * 2.0f + 1.0f;
    val = val * val;
    val = (MathF.Sin(val) + MathF.Cos(val)) * 0.5f;
    data[idx] = val;
}
```

### Controlling Register Usage

```csharp
[Kernel(MaxRegisters = 32)]  // Limit registers per thread
public static void LimitedRegisters(Span<float> data)
{
    // ...
}
```

## Profiling Kernels

### Basic Timing

```csharp
var sw = Stopwatch.StartNew();
await computeService.ExecuteKernelAsync(kernel, config, buffers);
await computeService.SynchronizeAsync(); // Wait for GPU completion
sw.Stop();

Console.WriteLine($"Kernel execution: {sw.ElapsedMilliseconds} ms");
```

### GPU-Native Timing

```csharp
// Create timing context
using var timing = computeService.CreateTimingContext();

// Record start event
timing.RecordStart();

await computeService.ExecuteKernelAsync(kernel, config, buffers);

// Record end event
timing.RecordEnd();

// Get precise GPU time
float gpuTimeMs = await timing.GetElapsedMillisecondsAsync();
Console.WriteLine($"GPU kernel time: {gpuTimeMs:F3} ms");
```

### Performance Metrics

```csharp
var metrics = await computeService.ProfileKernelAsync(
    kernel, config, buffers,
    ProfileMetrics.All);

Console.WriteLine($"Execution time: {metrics.ExecutionTimeMs:F3} ms");
Console.WriteLine($"Memory throughput: {metrics.MemoryThroughputGBps:F1} GB/s");
Console.WriteLine($"Compute throughput: {metrics.ComputeThroughputGFlops:F1} GFLOPS");
Console.WriteLine($"Achieved occupancy: {metrics.AchievedOccupancy:P0}");
Console.WriteLine($"Memory efficiency: {metrics.MemoryEfficiency:P0}");
```

## Common Performance Issues

### Issue 1: Warp Divergence

When threads in a warp take different branches, both paths execute serially.

```csharp
// BAD: High divergence
[Kernel]
public static void HighDivergence(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (data[idx] > 0)  // Half the warp goes here
        data[idx] = MathF.Sqrt(data[idx]);
    else                // Other half goes here
        data[idx] = 0;
}

// BETTER: Branchless
[Kernel]
public static void LowDivergence(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    float val = data[idx];
    float positive = MathF.Max(val, 0);
    data[idx] = MathF.Sqrt(positive); // All threads same path
}
```

### Issue 2: Uncoalesced Memory Access

```csharp
// BAD: Strided access
[Kernel]
public static void UncoalescedAccess(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = input[idx * stride];  // Non-contiguous reads
}

// GOOD: Sequential access
[Kernel]
public static void CoalescedAccess(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = input[idx];  // Contiguous reads
}
```

### Issue 3: Bank Conflicts (Shared Memory)

```csharp
// BAD: Same bank access
[Kernel]
public static void BankConflict(Span<float> shared, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    // All threads in warp access bank 0
    output[idx] = shared[idx * 32];
}

// GOOD: Different banks
[Kernel]
public static void NoBankConflict(Span<float> shared, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    // Each thread accesses different bank
    output[idx] = shared[idx];
}
```

## Optimization Checklist

- [ ] **Block size**: Multiple of warp size (32), typically 128-256
- [ ] **Occupancy**: Target >50% theoretical occupancy
- [ ] **Memory access**: Coalesced, aligned to 128 bytes
- [ ] **Branching**: Minimize warp divergence
- [ ] **Registers**: Keep under limit to avoid spilling
- [ ] **Shared memory**: Avoid bank conflicts

## Exercises

### Exercise 1: Occupancy Analysis

Profile a kernel and experiment with different block sizes to maximize occupancy.

### Exercise 2: Divergence Elimination

Rewrite a kernel with branches to use branchless operations.

### Exercise 3: Memory Pattern Analysis

Identify and fix uncoalesced memory access in a matrix transpose kernel.

## Key Takeaways

1. **Block size affects occupancy** - tune for your kernel's resource usage
2. **Warp divergence serializes execution** - minimize branching
3. **Memory coalescing is critical** - adjacent threads should access adjacent memory
4. **Profile before optimizing** - measure to find actual bottlenecks
5. **Auto-tune for different hardware** - optimal settings vary by device

## Next Module

[Multi-Kernel Pipelines →](multi-kernel-pipelines.md)

Learn to chain kernels and manage complex data flows.
