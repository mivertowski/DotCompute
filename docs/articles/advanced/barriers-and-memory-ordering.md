# Barriers and Memory Ordering

> **Status**: ✅ Production Ready | **Backends**: CUDA, Metal, OpenCL | **Last Updated**: January 2025

Complete guide to GPU thread synchronization, barriers, and memory consistency in DotCompute.

## 📚 Table of Contents

- [Overview](#overview)
- [Thread Barriers](#thread-barriers)
- [Memory Ordering](#memory-ordering)
- [Kernel Attribute Properties](#kernel-attribute-properties)
- [Backend Support](#backend-support)
- [Performance Considerations](#performance-considerations)
- [Common Patterns](#common-patterns)
- [Debugging](#debugging)

## Overview

Modern GPUs execute thousands of threads concurrently, requiring careful synchronization for correctness. DotCompute provides two complementary mechanisms:

1. **Thread Barriers**: Synchronize thread execution within a scope
2. **Memory Ordering**: Control visibility and ordering of memory operations

```csharp
// Example: Combining barriers with memory ordering
[Kernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire)]
public static void ProducerConsumer(Span<int> data, Span<int> flags)
{
    int tid = Kernel.ThreadId.X;

    // Phase 1: Write data
    data[tid] = ComputeValue(tid);
    Kernel.Barrier();  // Ensure all writes complete

    // Phase 2: Set flag (release semantics)
    flags[tid] = READY;
    Kernel.Barrier();

    // Phase 3: Read neighbor's data (acquire semantics)
    int neighbor = (tid + 1) % Kernel.BlockDim.X;
    while (flags[neighbor] != READY) { }  // Spin wait
    int neighborData = data[neighbor];  // Guaranteed to see write

    data[tid] = ProcessPair(data[tid], neighborData);
}
```

## Thread Barriers

### What are Barriers?

Barriers are synchronization points where all threads in a scope wait until every thread reaches that point.

**Key Concepts**:
- **Scope**: Which threads synchronize together (ThreadBlock, Warp, Grid, etc.)
- **Capacity**: Number of threads that must reach the barrier
- **Latency**: Time cost of synchronization (~1-20ns depending on scope)

### Barrier Scopes

```csharp
public enum BarrierScope
{
    ThreadBlock = 0,  // All threads in a block (~10-20ns)
    Warp = 2,         // All threads in a 32-thread warp (~1-5ns)
    Grid = 1,         // All threads across all blocks (~1-10μs, CUDA only)
    Tile = 3,         // Arbitrary subset of threads (~20ns)
    System = 4        // Multiple GPUs + CPU (~1-10ms)
}
```

### ThreadBlock Barriers (Most Common)

Synchronizes all threads within a single thread block.

```csharp
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.ThreadBlock)]
public static void MatrixTranspose(
    ReadOnlySpan<float> input,
    Span<float> output,
    int width,
    int height)
{
    // Allocate shared memory for tile
    var tile = Kernel.AllocateShared<float>(32, 32);

    int tx = Kernel.ThreadIdx.X;
    int ty = Kernel.ThreadIdx.Y;
    int bx = Kernel.BlockId.X;
    int by = Kernel.BlockId.Y;

    int x = bx * 32 + tx;
    int y = by * 32 + ty;

    // Load tile into shared memory
    if (x < width && y < height)
    {
        tile[ty, tx] = input[y * width + x];
    }

    // Wait for all threads to finish loading
    Kernel.Barrier();  // ThreadBlock barrier

    // Write transposed tile to global memory
    x = by * 32 + tx;  // Swap dimensions
    y = bx * 32 + ty;

    if (x < height && y < width)
    {
        output[y * height + x] = tile[tx, ty];  // Note: swapped indices
    }
}
```

**Backend Mapping**:
- **CUDA**: `__syncthreads()`
- **Metal**: `threadgroup_barrier(mem_flags::mem_device_and_threadgroup)`
- **OpenCL**: `barrier(CLK_GLOBAL_MEM_FENCE)`

### Warp Barriers (Fine-Grained)

Synchronizes threads within a 32-thread warp for fine-grained operations.

```csharp
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.Warp)]
public static void WarpReduce(Span<float> values, Span<float> results)
{
    int tid = Kernel.ThreadId.X;
    int warpId = tid / 32;
    int laneId = tid % 32;

    float value = values[tid];

    // Warp-level reduction using shuffle
    for (int offset = 16; offset > 0; offset /= 2)
    {
        value += Kernel.ShuffleDown(value, offset);
        Kernel.Barrier();  // Warp barrier (implicit in shuffle, but explicit for clarity)
    }

    // Lane 0 writes result
    if (laneId == 0)
    {
        results[warpId] = value;
    }
}
```

**Backend Mapping**:
- **CUDA**: `__syncwarp()` or implicit in shuffle operations
- **Metal**: `simdgroup_barrier(mem_flags::mem_none)`
- **OpenCL**: `sub_group_barrier()`

### Grid Barriers (CUDA Only)

⚠️ **Warning**: Grid barriers require cooperative kernel launch and are **NOT supported on Metal**.

```csharp
[Kernel(
    Backends = KernelBackends.CUDA,  // CUDA only!
    UseBarriers = true,
    BarrierScope = BarrierScope.Grid)]
public static void GlobalHistogram(
    ReadOnlySpan<int> data,
    Span<int> histogram,
    Span<int> blockCounts)
{
    int tid = Kernel.ThreadId.X;
    int bid = Kernel.BlockId.X;

    // Phase 1: Local histogram in each block
    int value = data[tid];
    Kernel.AtomicAdd(ref blockCounts[bid * 256 + value], 1);

    // Grid barrier - wait for all blocks to finish local histograms
    Kernel.GridBarrier();  // CUDA cooperative launch required

    // Phase 2: Aggregate block histograms into global histogram
    if (bid == 0)  // Block 0 aggregates
    {
        int sum = 0;
        for (int b = 0; b < Kernel.GridDim.X; b++)
        {
            sum += blockCounts[b * 256 + tid];
        }
        histogram[tid] = sum;
    }
}
```

**Backend Mapping**:
- **CUDA**: `cooperative_groups::grid_group::sync()`
- **Metal**: ❌ Not supported (use multiple kernel dispatches instead)
- **OpenCL**: ❌ Not supported

**Metal Alternative** (Multiple Kernel Dispatches):
```csharp
// Kernel 1: Local histograms
[Kernel(Backends = KernelBackends.Metal)]
public static void LocalHistogram(
    ReadOnlySpan<int> data,
    Span<int> blockCounts)
{
    // Block-local histogram
}

// CPU-side synchronization
await accelerator.ExecuteKernelAsync(LocalHistogram, ...);
await accelerator.SynchronizeAsync();  // Implicit barrier

// Kernel 2: Aggregate
[Kernel(Backends = KernelBackends.Metal)]
public static void AggregateHistogram(
    ReadOnlySpan<int> blockCounts,
    Span<int> histogram)
{
    // Aggregate block counts
}
```

### Barrier Capacity

Specify the expected number of threads participating in synchronization:

```csharp
[Kernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,
    BarrierCapacity = 256)]  // 256 threads per block
public static void FixedSizeKernel(Span<float> data)
{
    // Runtime validates that actual block size ≤ 256
    Kernel.Barrier();
}

// Or let runtime calculate automatically (capacity = 0)
[Kernel(UseBarriers = true, BarrierCapacity = 0)]  // Automatic
public static void FlexibleKernel(Span<float> data)
{
    // Capacity calculated from BlockDimensions at launch time
    Kernel.Barrier();
}
```

## Memory Ordering

### Why Memory Ordering Matters

GPUs use **relaxed memory models** by default for performance. Without explicit ordering:

```csharp
// Thread 0 (Producer)
data[0] = 42;      // Write 1
flag[0] = READY;   // Write 2

// Thread 1 (Consumer)
while (flag[0] != READY) { }  // Read flag
int value = data[0];           // Read data - MAY SEE OLD VALUE!
```

**Problem**: Thread 1 might see `flag[0] == READY` but still read stale `data[0]` due to reordering!

**Solution**: Memory consistency models ensure proper ordering.

### Memory Consistency Models

```csharp
public enum MemoryConsistencyModel
{
    Relaxed = 0,         // No ordering (1.0× performance)
    ReleaseAcquire = 1,  // Causal ordering (0.85× performance)
    Sequential = 2       // Total order (0.60× performance)
}
```

#### Relaxed (Default)

No ordering guarantees. Fastest but requires manual fencing.

```csharp
[Kernel(MemoryConsistency = MemoryConsistencyModel.Relaxed)]
public static void DataParallel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    // No inter-thread communication - no ordering needed
    data[idx] = MathF.Sqrt(data[idx]);
}
```

**Use When**:
- ✅ Purely data-parallel algorithms
- ✅ No inter-thread communication
- ✅ Each thread operates independently

#### Release-Acquire (Recommended for Message Passing)

Ensures causality: if A writes then B reads, B sees A's prior writes.

```csharp
[Kernel(
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
    UseBarriers = true)]
public static void MessagePassing(Span<int> messages, Span<int> flags)
{
    int tid = Kernel.ThreadId.X;
    int neighbor = (tid + 1) % Kernel.BlockDim.X;

    // Producer: Write message
    messages[tid] = ComputeMessage(tid);
    // Implicit release: message write visible before flag

    flags[tid] = READY;
    Kernel.Barrier();

    // Consumer: Wait for neighbor
    while (flags[neighbor] != READY) { }
    // Implicit acquire: see all writes before flag set

    int msg = messages[neighbor];  // Guaranteed to see message write
    ProcessMessage(msg);
}
```

**Use When**:
- ✅ Producer-consumer patterns
- ✅ Message passing between threads
- ✅ Distributed data structures
- ✅ Ring kernels with message queues

**Performance**: 15% overhead from fence insertion, acceptable for most workloads.

#### Sequential (Strongest, Highest Overhead)

Total order across all threads. Use sparingly.

```csharp
[Kernel(MemoryConsistency = MemoryConsistencyModel.Sequential)]
public static void StrictOrdering(Span<int> data)
{
    // All memory operations see total order
    // 40% performance overhead!
}
```

**Use When**:
- ❓ Debugging race conditions
- ❓ Algorithms requiring global order (rare)

**Avoid**: Start with Relaxed or ReleaseAcquire first.

### Causal Ordering (Convenience Property)

Shorthand for enabling Release-Acquire:

```csharp
[Kernel(EnableCausalOrdering = true)]  // Equivalent to MemoryConsistency = ReleaseAcquire
public static void SafeMessaging(Span<int> data, Span<int> flags)
{
    // Release-acquire semantics automatically applied
}
```

## Kernel Attribute Properties

Complete reference for barrier and memory ordering properties:

```csharp
[Kernel(
    // Barrier Configuration
    UseBarriers = true,                                  // Enable barriers
    BarrierScope = BarrierScope.ThreadBlock,            // Synchronization scope
    BarrierCapacity = 256,                              // Expected thread count (0 = auto)

    // Memory Ordering Configuration
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,  // Consistency model
    EnableCausalOrdering = true,                        // Shorthand for ReleaseAcquire

    // Other Properties
    Backends = KernelBackends.CUDA | KernelBackends.Metal,
    BlockDimensions = new int[] { 16, 16 },
    GridDimensions = new int[] { 64, 64 })]
public static void FullyConfigured(Span<float> data)
{
    // Kernel implementation
}
```

### Property Defaults

| Property | Default | Ring Kernel Default |
|----------|---------|---------------------|
| `UseBarriers` | `false` | `false` |
| `BarrierScope` | `ThreadBlock` | `ThreadBlock` |
| `BarrierCapacity` | `0` (auto) | `0` (auto) |
| `MemoryConsistency` | `Relaxed` | `ReleaseAcquire` ⭐ |
| `EnableCausalOrdering` | `false` | `true` ⭐ |

⭐ Ring kernels default to safer settings due to message-passing nature.

## Backend Support

### Feature Matrix

| Feature | CUDA | Metal | OpenCL | CPU |
|---------|------|-------|--------|-----|
| ThreadBlock Barriers | ✅ Full | ✅ Full | ✅ Full | ✅ Emulated |
| Warp Barriers | ✅ Native | ✅ Simdgroup | ✅ Subgroup | ❌ N/A |
| Grid Barriers | ✅ Cooperative | ❌ **Not Supported** | ❌ No | ❌ No |
| Tile Barriers | ✅ CC 7.0+ | ❌ No | ❌ No | ❌ No |
| System Barriers | ✅ Multi-GPU | ❌ No | ❌ No | ✅ Emulated |
| Relaxed Model | ✅ | ✅ | ✅ | ✅ |
| ReleaseAcquire | ✅ | ✅ | ✅ | ✅ |
| Sequential | ✅ | ✅ | ✅ | ✅ |

### CUDA Implementation

```cpp
// ThreadBlock barrier
__syncthreads();

// Warp barrier
__syncwarp(0xffffffff);

// Grid barrier (cooperative launch required)
cooperative_groups::grid_group g = cooperative_groups::this_grid();
g.sync();

// Memory fences
__threadfence();           // Device scope
__threadfence_block();     // Block scope
__threadfence_system();    // System scope
```

### Metal Implementation

```metal
// ThreadBlock barrier
threadgroup_barrier(mem_flags::mem_device_and_threadgroup);

// Simdgroup (warp) barrier
simdgroup_barrier(mem_flags::mem_none);

// Memory fences
threadgroup_barrier(mem_flags::mem_device);      // Device fence
threadgroup_barrier(mem_flags::mem_threadgroup); // Threadgroup fence
threadgroup_barrier(mem_flags::mem_texture);     // Texture fence
```

## Performance Considerations

### Barrier Latency

| Barrier Scope | Typical Latency | When to Use |
|---------------|-----------------|-------------|
| Warp | ~1-5ns | Fine-grained sync, shuffle operations |
| ThreadBlock | ~10-20ns | Shared memory sync, most common |
| Grid | ~1-10μs | Multi-block coordination (CUDA only) |
| System | ~1-10ms | Multi-GPU algorithms (rare) |

### Memory Consistency Overhead

| Model | Performance Multiplier | Overhead | When to Use |
|-------|----------------------|----------|-------------|
| Relaxed | 1.0× | 0% | Data-parallel, no communication |
| ReleaseAcquire | 0.85× | 15% | Message passing, recommended |
| Sequential | 0.60× | 40% | Debugging only |

### Best Practices

✅ **DO**:
- Use narrowest barrier scope needed (Warp < ThreadBlock < Grid)
- Prefer ReleaseAcquire over Sequential
- Add barriers only where necessary
- Profile before and after adding barriers
- Use shared memory to reduce global memory traffic

❌ **DON'T**:
- Add barriers "just in case"
- Use Grid barriers on Metal (not supported)
- Use Sequential consistency in production (40% overhead)
- Overuse System-wide barriers (1-10ms each)
- Add barriers inside tight loops

### Example: Optimized Reduction

```csharp
[Kernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock,
    MemoryConsistency = MemoryConsistencyModel.Relaxed)]  // No inter-thread communication
public static void OptimizedReduce(
    ReadOnlySpan<float> input,
    Span<float> output,
    int n)
{
    var shared = Kernel.AllocateShared<float>(256);

    int tid = Kernel.ThreadIdx.X;
    int i = Kernel.BlockId.X * Kernel.BlockDim.X + tid;

    // Load to shared memory
    shared[tid] = (i < n) ? input[i] : 0.0f;
    Kernel.Barrier();  // Necessary: sync after load

    // Reduction in shared memory
    for (int s = Kernel.BlockDim.X / 2; s > 0; s /= 2)
    {
        if (tid < s)
        {
            shared[tid] += shared[tid + s];
        }
        Kernel.Barrier();  // Necessary: sync after each reduction step
    }

    // Write result
    if (tid == 0)
    {
        output[Kernel.BlockId.X] = shared[0];
    }
}
```

**Barrier Count**: 2 + log2(blockSize) barriers
- 1 after load
- log2(blockSize) in reduction loop
- Total: ~10 barriers for 256-thread block
- Overhead: ~100-200ns total (acceptable)

## Common Patterns

### Pattern 1: Shared Memory Communication

```csharp
[Kernel(UseBarriers = true)]
public static void SharedMemPattern(Span<float> data)
{
    var shared = Kernel.AllocateShared<float>(256);
    int tid = Kernel.ThreadIdx.X;

    // Phase 1: Load
    shared[tid] = data[tid];
    Kernel.Barrier();  // Wait for all loads

    // Phase 2: Process (read from shared)
    float result = shared[tid] + shared[(tid + 1) % 256];
    Kernel.Barrier();  // Wait for all reads

    // Phase 3: Store
    data[tid] = result;
}
```

### Pattern 2: Producer-Consumer

```csharp
[Kernel(
    UseBarriers = true,
    EnableCausalOrdering = true)]  // Ensures flag/data visibility
public static void ProducerConsumer(
    Span<int> data,
    Span<int> flags)
{
    int tid = Kernel.ThreadId.X;
    int partner = (tid + Kernel.BlockDim.X / 2) % Kernel.BlockDim.X;

    // Produce
    data[tid] = ComputeValue(tid);
    flags[tid] = READY;
    Kernel.Barrier();

    // Consume
    while (flags[partner] != READY) { }  // Spin wait
    int partnerData = data[partner];  // Guaranteed to see write

    data[tid] = Combine(data[tid], partnerData);
}
```

### Pattern 3: Multi-Phase Algorithm

```csharp
[Kernel(UseBarriers = true)]
public static void MultiPhase(Span<float> data)
{
    var temp = Kernel.AllocateShared<float>(256);
    int tid = Kernel.ThreadIdx.X;

    // Phase 1: Forward pass
    temp[tid] = data[tid];
    Kernel.Barrier();

    // Phase 2: Process neighbors
    float left = temp[(tid - 1 + 256) % 256];
    float right = temp[(tid + 1) % 256];
    temp[tid] = (left + temp[tid] + right) / 3.0f;
    Kernel.Barrier();

    // Phase 3: Write back
    data[tid] = temp[tid];
}
```

### Pattern 4: Warp-Level Primitives

```csharp
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.Warp)]
public static void WarpScan(Span<int> data)
{
    int tid = Kernel.ThreadId.X;
    int laneId = tid % 32;

    int value = data[tid];

    // Inclusive scan using shuffle
    for (int offset = 1; offset < 32; offset *= 2)
    {
        int temp = Kernel.ShuffleUp(value, offset);
        if (laneId >= offset)
            value += temp;
        Kernel.Barrier();  // Warp sync
    }

    data[tid] = value;
}
```

## Debugging

### Detecting Race Conditions

Use Sequential consistency to debug suspected race conditions:

```csharp
// Development/Debugging
[Kernel(MemoryConsistency = MemoryConsistencyModel.Sequential)]
public static void DebugKernel(Span<int> data)
{
    // If this fixes the bug, you have a race condition
    // Then fix with proper barriers and switch back to ReleaseAcquire
}
```

### Cross-Backend Validation

```csharp
// Test with both relaxed and strict ordering
var resultRelaxed = await RunKernelAsync(
    MemoryConsistencyModel.Relaxed);

var resultSequential = await RunKernelAsync(
    MemoryConsistencyModel.Sequential);

if (!resultRelaxed.Equals(resultSequential))
{
    Console.WriteLine("Race condition detected!");
    // Add barriers or upgrade consistency model
}
```

### Common Issues

**Issue 1: Missing Barrier After Shared Memory Write**
```csharp
// ❌ BUG: Reading before all writes complete
shared[tid] = data[tid];
float value = shared[(tid + 1) % 256];  // BUG: May read stale data!

// ✅ FIX: Add barrier
shared[tid] = data[tid];
Kernel.Barrier();  // Wait for all writes
float value = shared[(tid + 1) % 256];  // OK: Sees all writes
```

**Issue 2: Conditional Barrier (Deadlock)**
```csharp
// ❌ BUG: Not all threads reach barrier
if (tid < 128)
{
    Kernel.Barrier();  // Deadlock! Threads 128-255 never reach
}

// ✅ FIX: All threads must reach barrier
Kernel.Barrier();  // All threads participate
if (tid < 128)
{
    // Process
}
```

**Issue 3: Grid Barrier on Metal**
```csharp
// ❌ BUG: Grid barriers not supported on Metal
[Kernel(
    Backends = KernelBackends.Metal,
    BarrierScope = BarrierScope.Grid)]  // Runtime error!
public static void MetalGridBarrier() { }

// ✅ FIX: Use multiple kernel dispatches
[Kernel(Backends = KernelBackends.Metal)]
public static void Phase1() { }

// CPU synchronization
await accelerator.SynchronizeAsync();

[Kernel(Backends = KernelBackends.Metal)]
public static void Phase2() { }
```

## See Also

- [Kernel Attribute Reference](../reference/kernel-attribute.md) - Complete attribute documentation
- [Ring Kernels](../architecture/ring-kernels.md) - Message-passing patterns
- [Performance Guide](../performance/optimization-strategies.md) - Optimization strategies
- [CUDA Programming](cuda-programming.md) - CUDA-specific features
- [Metal Shading](metal-shading.md) - Metal-specific features

---

**Next**: Explore [ring kernel patterns](../architecture/ring-kernels.md) for advanced message-passing algorithms.
