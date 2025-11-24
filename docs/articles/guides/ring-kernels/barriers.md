# Barrier API: Hardware-Accelerated GPU Synchronization

## Overview

The Barrier API provides hardware-accelerated synchronization primitives for coordinating GPU threads across different execution scopes. Built on CUDA Cooperative Groups, it enables efficient thread coordination with minimal latency overhead.

**Key Features:**
- **Four Synchronization Scopes**: Warp (32 threads), Thread-Block, Grid, and Tile levels
- **Hardware-Accelerated**: Direct GPU synchronization (~10ns thread-block, ~1ns warp)
- **Named Barriers**: Up to 16 independent barriers per block for complex coordination patterns
- **Cooperative Launch**: Grid-wide synchronization with guaranteed concurrent execution
- **Thread-Safe**: Concurrent creation and management from multiple CPU threads
- **Resource Pooling**: Automatic memory management for grid-level barriers

**Supported Backends:**
| Backend | Thread-Block | Grid | Warp | Named | Min. Compute Capability |
|---------|--------------|------|------|-------|------------------------|
| CUDA | ✅ | ✅ | ✅ | ✅ | 1.0 (Block), 6.0 (Grid), 7.0 (Warp/Named) |
| OpenCL | ✅ | ❌ | ❌ | ❌ | barrier() built-in |
| Metal | ✅ | ❌ | ❌ | ❌ | threadgroup_barrier() |
| CPU | ✅ | ❌ | ❌ | ❌ | Software emulation |

## Quick Start

### Basic Thread-Block Barrier

```csharp
using DotCompute.Abstractions.Barriers;
using DotCompute.Backends.CUDA.Factory;

// Create accelerator
using var factory = new CudaAcceleratorFactory();
await using var accelerator = factory.CreateProductionAccelerator(0);

// Get barrier provider
var barrierProvider = accelerator.GetBarrierProvider();
if (barrierProvider == null)
{
    Console.WriteLine("Barriers not supported on this device");
    return;
}

// Create thread-block barrier for 256 threads
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: "compute-barrier");

Console.WriteLine($"Barrier ID: {barrier.BarrierId}");
Console.WriteLine($"Scope: {barrier.Scope}");
Console.WriteLine($"Capacity: {barrier.Capacity}");
```

### Grid-Wide Cooperative Barrier

```csharp
// Check for cooperative launch support (CC 6.0+)
if (barrierProvider.GetMaxCooperativeGridSize() == 0)
{
    Console.WriteLine("Cooperative launch not supported");
    return;
}

// Enable cooperative launch mode
barrierProvider.EnableCooperativeLaunch(true);

// Create grid-wide barrier
const int numBlocks = 16;
const int threadsPerBlock = 256;
const int totalThreads = numBlocks * threadsPerBlock;

using var gridBarrier = barrierProvider.CreateBarrier(
    BarrierScope.Grid,
    capacity: totalThreads,
    name: "global-sync");

Console.WriteLine($"Grid barrier created for {numBlocks} blocks × {threadsPerBlock} threads");
Console.WriteLine($"Max cooperative size: {barrierProvider.GetMaxCooperativeGridSize()}");
```

### Named Barriers for Multi-Phase Algorithms

```csharp
// Create multiple barriers for producer-consumer pattern
using var producerBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: "producers-ready");

using var consumerBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: "consumers-ready");

// Retrieve barrier by name
var retrievedBarrier = barrierProvider.GetBarrier("producers-ready");
Console.WriteLine($"Retrieved barrier: {retrievedBarrier?.BarrierId}");
Console.WriteLine($"Active barriers: {barrierProvider.ActiveBarrierCount}");
```

## Barrier Scopes

### 1. Thread-Block Barriers

**Hardware:** `__syncthreads()` primitive
**Latency:** ~10 nanoseconds
**Use Case:** Coordinating threads within a single block

**Example CUDA Kernel:**
```cuda
extern "C" __global__ void thread_block_example(float* data, int size)
{
    int tid = blockIdx.x * blockDim.x + threadIdx.x;

    // Phase 1: All threads read and process data
    float value = (tid < size) ? data[tid] : 0.0f;
    value = sqrt(value);

    // Barrier: Ensure all threads finish Phase 1
    __syncthreads();

    // Phase 2: All threads can safely write results
    if (tid < size)
    {
        data[tid] = value;
    }
}
```

**C# Integration:**
```csharp
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256);

// Barrier coordinates threads within CUDA kernel
// Actual synchronization happens on GPU via __syncthreads()
```

### 2. Grid-Wide Barriers

**Hardware:** Cooperative Groups `grid.sync()`
**Latency:** ~1-10 microseconds (depends on grid size)
**Use Case:** Coordinating threads across multiple blocks
**Requirements:** Compute Capability 6.0+ (Pascal), cooperative launch

**Example CUDA Kernel:**
```cuda
#include <cooperative_groups.h>
namespace cg = cooperative_groups;

extern "C" __global__ void grid_barrier_example(int* counter, int* results, int numBlocks)
{
    auto grid = cg::this_grid();
    int globalId = blockIdx.x * blockDim.x + threadIdx.x;

    // Phase 1: All threads increment counter
    atomicAdd(counter, 1);

    // Grid-wide barrier: Wait for ALL blocks
    grid.sync();

    // Phase 2: All threads can read final counter
    results[globalId] = *counter; // Should equal total thread count
}
```

**C# Integration:**
```csharp
barrierProvider.EnableCooperativeLaunch(true);

using var gridBarrier = barrierProvider.CreateBarrier(
    BarrierScope.Grid,
    capacity: numBlocks * threadsPerBlock);

// Launch kernel with cudaLaunchCooperativeKernel
// Grid barrier ensures all blocks synchronize together
```

**Important:** Grid barriers require special kernel launch with `cudaLaunchCooperativeKernel`. Query `GetMaxCooperativeGridSize()` to determine device limits.

### 3. Warp-Level Barriers

**Hardware:** `__syncwarp()` primitive
**Latency:** ~1 nanosecond (SIMD lockstep execution)
**Use Case:** Coordinating 32-thread SIMD groups
**Requirements:** Compute Capability 7.0+ (Volta)

**Example CUDA Kernel:**
```cuda
extern "C" __global__ void warp_shuffle_example(int* data, int size)
{
    int tid = blockIdx.x * blockDim.x + threadIdx.x;
    int laneId = threadIdx.x % 32;
    int value = (tid < size) ? data[tid] : 0;

    // Warp barrier before shuffle
    __syncwarp();

    // Shuffle values within warp (XOR with 16)
    int shuffled = __shfl_xor_sync(0xFFFFFFFF, value, 16);

    // Warp barrier after shuffle
    __syncwarp();

    if (tid < size)
    {
        data[tid] = shuffled;
    }
}
```

**C# Integration:**
```csharp
using var warpBarrier = barrierProvider.CreateBarrier(
    BarrierScope.Warp,
    capacity: 32); // Must be exactly 32 threads

// Warp barriers coordinate SIMD execution
// Used with shuffle, vote, and warp-level primitives
```

**Note:** Warp barriers must have capacity of exactly 32 threads (CUDA warp size). Attempting to create a warp barrier with a different capacity throws `ArgumentOutOfRangeException`.

### 4. Named Barriers (Advanced)

**Hardware:** `__barrier_sync(barrier_id)` primitive
**Use Case:** Multiple independent synchronization points in a single kernel
**Requirements:** Compute Capability 7.0+ (Volta)
**Limit:** Maximum 16 named barriers per thread block

**Example CUDA Kernel:**
```cuda
extern "C" __global__ void multi_barrier_example(float* input, float* output, int size)
{
    __shared__ float sharedData[256];
    int tid = threadIdx.x;

    // Even threads use barrier 0
    if (tid % 2 == 0)
    {
        sharedData[tid] = input[tid] * 2.0f;
        __barrier_sync(0); // Barrier ID 0
        output[tid] = sharedData[tid];
    }
    // Odd threads use barrier 1
    else
    {
        sharedData[tid] = input[tid] * 3.0f;
        __barrier_sync(1); // Barrier ID 1
        output[tid] = sharedData[tid];
    }
}
```

**C# Integration:**
```csharp
// Create up to 16 named barriers per block
using var evenBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 128,
    name: "even-threads");

using var oddBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 128,
    name: "odd-threads");

// Check limit
if (barrierProvider.ActiveBarrierCount >= 16)
{
    throw new InvalidOperationException("Named barrier limit reached");
}
```

## Advanced Usage

### Barrier Reset and Reuse

```csharp
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256);

// Use barrier in first kernel launch
// ... kernel execution ...

// Reset barrier for reuse
barrier.Reset();
barrier.ThreadsWaiting.Should().Be(0);
barrier.IsActive.Should().BeFalse();

// Barrier can now be used in another kernel launch
// ... second kernel execution ...
```

**Important:** Cannot reset an active barrier. Wait for all threads to complete synchronization first.

### Producer-Consumer Pattern

```csharp
// Create separate barriers for producer and consumer phases
using var producerBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: "producers");

using var consumerBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: "consumers");

// Kernel uses both barriers to coordinate data exchange
// Producers write → producer barrier → consumers read → consumer barrier
```

### Thread Safety and Concurrent Management

```csharp
// Barrier provider is thread-safe for concurrent CPU access
var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
{
    using var barrier = barrierProvider.CreateBarrier(
        BarrierScope.ThreadBlock,
        capacity: 256,
        name: $"barrier-{i}");

    // Each task can safely create and manage barriers
    return barrier.BarrierId;
}));

var barrierIds = await Task.WhenAll(tasks);
Console.WriteLine($"Created {barrierIds.Length} barriers concurrently");
```

## Performance Characteristics

### Barrier Latency

| Scope | Typical Latency | Hardware Mechanism |
|-------|----------------|-------------------|
| Warp | ~1 ns | SIMD lockstep execution |
| Thread-Block | ~10 ns | Hardware barrier units |
| Grid | ~1-10 μs | Cross-SM coordination |
| Tile | ~20 ns | Cooperative groups |

### Memory Overhead

| Barrier Type | Device Memory | Host Memory |
|--------------|--------------|-------------|
| Thread-Block | 0 bytes | ~64 bytes |
| Grid | 4-16 bytes | ~96 bytes |
| Warp | 0 bytes | ~64 bytes |
| Named | 0 bytes | ~80 bytes |

**Note:** Grid barriers allocate device memory for synchronization state. Thread-block and warp barriers use hardware units with zero device memory overhead.

### Scalability

```csharp
// Benchmark barrier creation and reset
var sw = Stopwatch.StartNew();
for (int i = 0; i < 1000; i++)
{
    using var barrier = barrierProvider.CreateBarrier(
        BarrierScope.ThreadBlock,
        capacity: 256);
    barrier.Reset();
}
sw.Stop();

Console.WriteLine($"Average creation time: {sw.ElapsedMilliseconds / 1000.0:F3} ms");
// Typical: ~0.05-0.1 ms per barrier (creation + reset)
```

## Best Practices

### 1. Use Appropriate Scope

```csharp
// ✅ GOOD: Thread-block for intra-block coordination
using var blockBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256);

// ✅ GOOD: Grid barrier only when absolutely necessary
if (requiresCrossBlockSync)
{
    barrierProvider.EnableCooperativeLaunch(true);
    using var gridBarrier = barrierProvider.CreateBarrier(
        BarrierScope.Grid,
        capacity: totalThreads);
}

// ❌ BAD: Grid barrier for simple intra-block sync (overkill)
using var overkillBarrier = barrierProvider.CreateBarrier(
    BarrierScope.Grid,
    capacity: 256); // Should use ThreadBlock scope
```

### 2. Validate Hardware Support

```csharp
// Check compute capability before using advanced features
var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();

if (major < 6)
{
    Console.WriteLine("Grid barriers require CC 6.0+ (Pascal)");
    // Fall back to multi-kernel approach
}

if (major < 7)
{
    Console.WriteLine("Named barriers and warp primitives require CC 7.0+ (Volta)");
}

// Check cooperative launch support
if (barrierProvider.GetMaxCooperativeGridSize() == 0)
{
    Console.WriteLine("Cooperative launch not supported");
}
```

### 3. Respect Named Barrier Limits

```csharp
// Track active barriers to avoid hitting 16-barrier limit
Console.WriteLine($"Active barriers: {barrierProvider.ActiveBarrierCount}/16");

if (barrierProvider.ActiveBarrierCount >= 16)
{
    // Consider reusing existing barriers or using anonymous barriers
    throw new InvalidOperationException("Named barrier limit reached");
}

// Anonymous barriers don't count toward the limit
using var anonymousBarrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256,
    name: null); // No name = no limit
```

### 4. Proper Resource Management

```csharp
// ✅ GOOD: Use 'using' for automatic disposal
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.Grid,
    capacity: 1024);

// Barrier automatically freed when scope exits
// Device memory released, provider tracking updated

// ❌ BAD: Forgetting to dispose
var leakedBarrier = barrierProvider.CreateBarrier(
    BarrierScope.Grid,
    capacity: 1024);
// Device memory leaked!
```

### 5. Reset Between Kernel Launches

```csharp
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256);

for (int iteration = 0; iteration < 10; iteration++)
{
    // Reset barrier before each kernel launch
    barrier.Reset();

    // Launch kernel with barrier
    // ... kernel execution ...
}
```

## Error Handling

### Common Errors and Solutions

#### 1. Unsupported Scope

```csharp
try
{
    var gridBarrier = barrierProvider.CreateBarrier(
        BarrierScope.Grid,
        capacity: 1024);
}
catch (NotSupportedException ex)
{
    // "Grid barriers require Compute Capability 6.0+ (Pascal or newer)"
    Console.WriteLine($"Grid barriers not supported: {ex.Message}");
    // Fall back to thread-block barriers
}
```

#### 2. Invalid Warp Size

```csharp
try
{
    var warpBarrier = barrierProvider.CreateBarrier(
        BarrierScope.Warp,
        capacity: 16); // Wrong!
}
catch (ArgumentOutOfRangeException ex)
{
    // "Warp barriers must have capacity of exactly 32 threads (warp size)"
    Console.WriteLine($"Invalid warp size: {ex.Message}");
}
```

#### 3. Named Barrier Limit Exceeded

```csharp
try
{
    var barriers = new List<IBarrierHandle>();
    for (int i = 0; i < 20; i++)
    {
        barriers.Add(barrierProvider.CreateBarrier(
            BarrierScope.ThreadBlock,
            capacity: 256,
            name: $"barrier-{i}"));
    }
}
catch (InvalidOperationException ex)
{
    // "Maximum named barriers (16) reached"
    Console.WriteLine($"Too many named barriers: {ex.Message}");
}
```

#### 4. Resetting Active Barrier

```csharp
using var barrier = barrierProvider.CreateBarrier(
    BarrierScope.ThreadBlock,
    capacity: 256);

// Simulate partial thread arrival
for (int i = 0; i < 10; i++)
{
    barrier.Sync();
}

try
{
    barrier.Reset(); // Error!
}
catch (InvalidOperationException ex)
{
    // "Cannot reset an active barrier"
    Console.WriteLine($"Reset failed: {ex.Message}");

    // Wait for all threads to arrive or use a different barrier
}
```

## Hardware Requirements

### Compute Capability Matrix

| Feature | CC 1.0 | CC 2.0 | CC 5.0 | CC 6.0 | CC 7.0 | CC 8.0+ |
|---------|--------|--------|--------|--------|--------|---------|
| Thread-Block | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Grid | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Warp | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Named (16 max) | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Cooperative Launch | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |

### Device Query

```csharp
using DotCompute.Backends.CUDA.Configuration;

// Check device capabilities
var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
Console.WriteLine($"Compute Capability: {major}.{minor}");

// Feature availability
bool supportsGrid = major >= 6;
bool supportsNamed = major >= 7;
bool supportsWarp = major >= 7;

Console.WriteLine($"Grid barriers: {(supportsGrid ? "✅" : "❌")}");
Console.WriteLine($"Named barriers: {(supportsNamed ? "✅" : "❌")}");
Console.WriteLine($"Warp barriers: {(supportsWarp ? "✅" : "❌")}");

// Cooperative launch limits
if (supportsGrid)
{
    int maxCoopSize = barrierProvider.GetMaxCooperativeGridSize();
    Console.WriteLine($"Max cooperative grid size: {maxCoopSize:N0} threads");
}
```

## Troubleshooting

### Issue: Grid Barrier Hangs

**Symptoms:** Kernel never completes when using grid barriers

**Solutions:**
1. Ensure cooperative launch is enabled:
   ```csharp
   barrierProvider.EnableCooperativeLaunch(true);
   ```

2. Verify all threads reach the barrier:
   ```cuda
   // ❌ BAD: Not all threads reach barrier
   if (globalId < size)
   {
       grid.sync(); // Some threads skip this!
   }

   // ✅ GOOD: All threads reach barrier
   grid.sync(); // All threads execute
   if (globalId < size)
   {
       // Work here...
   }
   ```

3. Check grid size doesn't exceed device limit:
   ```csharp
   int maxSize = barrierProvider.GetMaxCooperativeGridSize();
   int requestedSize = numBlocks * threadsPerBlock;
   if (requestedSize > maxSize)
   {
       Console.WriteLine($"Grid size {requestedSize} exceeds max {maxSize}");
   }
   ```

### Issue: "Named barrier limit reached"

**Solutions:**
1. Reuse existing barriers:
   ```csharp
   var barrier = barrierProvider.GetBarrier("my-barrier");
   if (barrier != null)
   {
       barrier.Reset();
       // Reuse existing barrier
   }
   else
   {
       barrier = barrierProvider.CreateBarrier(/*...*/);
   }
   ```

2. Use anonymous barriers when possible:
   ```csharp
   // Anonymous barriers don't count toward limit
   using var barrier = barrierProvider.CreateBarrier(
       BarrierScope.ThreadBlock,
       capacity: 256,
       name: null); // No name
   ```

3. Dispose barriers when done:
   ```csharp
   using var tempBarrier = barrierProvider.CreateBarrier(/*...*/);
   // Use barrier...
   // Automatically disposed, freeing slot
   ```

### Issue: Poor Performance with Grid Barriers

**Analysis:**
```csharp
// Measure barrier overhead
var sw = Stopwatch.StartNew();
// ... kernel with grid barrier ...
sw.Stop();
Console.WriteLine($"Grid barrier latency: {sw.Elapsed.TotalMicroseconds:F2} μs");
```

**Solutions:**
1. Use thread-block barriers for intra-block sync (much faster)
2. Minimize grid barrier frequency in kernel
3. Consider multi-kernel approach if grid barriers cause bottleneck

## See Also

- [Kernel Development Guide](kernel-development.md) - Write GPU kernels
- [Performance Tuning](performance-tuning.md) - Optimize barrier usage
- [Multi-GPU Guide](multi-gpu.md) - Cross-device synchronization
- [Timing API](timing-api.md) - High-precision temporal measurements
- [CUDA Programming Guide](https://docs.nvidia.com/cuda/cuda-c-programming-guide/index.html#cooperative-groups) - CUDA Cooperative Groups documentation

## API Reference

### IBarrierProvider Interface

```csharp
public interface IBarrierProvider
{
    IBarrierHandle CreateBarrier(BarrierScope scope, int capacity, string? name = null);
    IBarrierHandle? GetBarrier(string name);
    void EnableCooperativeLaunch(bool enable = true);
    bool IsCooperativeLaunchEnabled { get; }
    int GetMaxCooperativeGridSize();
    int ActiveBarrierCount { get; }
    void ResetAllBarriers();
}
```

### IBarrierHandle Interface

```csharp
public interface IBarrierHandle : IDisposable
{
    int BarrierId { get; }
    BarrierScope Scope { get; }
    int Capacity { get; }
    int ThreadsWaiting { get; }
    bool IsActive { get; }
    void Sync();
    void Reset();
}
```

### BarrierScope Enum

```csharp
public enum BarrierScope
{
    ThreadBlock,  // __syncthreads() - intra-block sync
    Grid,         // grid.sync() - cross-block sync (CC 6.0+)
    Warp,         // __syncwarp() - 32-thread SIMD (CC 7.0+)
    Tile          // Cooperative groups tile (advanced)
}
```
