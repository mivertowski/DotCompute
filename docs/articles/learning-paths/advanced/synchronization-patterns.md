# Synchronization Patterns

This module covers GPU synchronization primitives including barriers, memory ordering, and multi-kernel coordination.

## Why Synchronization Matters

GPU threads execute in parallel without inherent ordering. Synchronization is required for:

- Ensuring all threads reach a point before proceeding
- Making writes visible to other threads
- Coordinating between kernels
- Implementing reduction and scan operations

## Barrier Types

### Thread Block Barrier

Synchronize all threads within a block:

```csharp
[Kernel]
public static void BlockBarrierExample(Span<float> data)
{
    // Shared memory visible to all threads in block
    var shared = Kernel.AllocateShared<float>(256);

    int tid = Kernel.ThreadId.X;
    int gid = Kernel.BlockId.X * Kernel.BlockDim.X + tid;

    // Phase 1: Load to shared memory
    if (gid < data.Length)
    {
        shared[tid] = data[gid];
    }

    // Barrier: Wait for all threads to complete loading
    Kernel.SyncThreads();

    // Phase 2: Process with neighbor data (now safe to read)
    if (gid < data.Length && tid > 0 && tid < Kernel.BlockDim.X - 1)
    {
        data[gid] = (shared[tid - 1] + shared[tid] + shared[tid + 1]) / 3.0f;
    }
}
```

### Grid Barrier (Cooperative Launch)

Synchronize all threads across the entire grid:

```csharp
[Kernel(RequiresCooperativeLaunch = true)]
public static void GridBarrierExample(Span<float> data, Span<float> temp)
{
    int gid = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadId.X;

    // Phase 1: All blocks write to temp
    if (gid < data.Length)
    {
        temp[gid] = Process(data[gid]);
    }

    // Grid barrier: Wait for ALL blocks to complete
    Kernel.SyncGrid();

    // Phase 2: Safe to read any location in temp
    if (gid < data.Length)
    {
        int neighbor = (gid + 1) % data.Length;
        data[gid] = Combine(temp[gid], temp[neighbor]);
    }
}
```

### Named Barriers

For complex synchronization patterns:

```csharp
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.Grid)]
public static void NamedBarrierExample(Span<float> data)
{
    var barrier = Kernel.GetBarrier("phase-sync");

    // Phase 1
    ComputePhase1(data);
    barrier.Wait();

    // Phase 2
    ComputePhase2(data);
    barrier.Wait();

    // Phase 3
    ComputePhase3(data);
}
```

## Memory Ordering

### Memory Fence Operations

Control visibility of memory operations:

```csharp
[Kernel(MemoryConsistency = MemoryConsistency.Sequential)]
public static void MemoryFenceExample(Span<int> flags, Span<float> data)
{
    int tid = Kernel.ThreadId.X;

    if (tid == 0)
    {
        // Producer: Write data, then set flag
        data[0] = ComputeResult();

        // Memory fence: Ensure data write is visible before flag
        Kernel.MemoryFence(MemoryScope.Device);

        flags[0] = 1;  // Signal data is ready
    }
    else
    {
        // Consumer: Wait for flag, then read data
        while (Kernel.AtomicLoad(ref flags[0]) != 1)
        {
            // Spin wait
        }

        // Memory fence: Ensure we see the data write
        Kernel.MemoryFence(MemoryScope.Device);

        float result = data[0];  // Safe to read
    }
}
```

### Atomic Operations

Thread-safe memory operations:

```csharp
[Kernel]
public static void AtomicExample(
    ReadOnlySpan<float> input,
    Span<int> histogram,
    int bucketCount)
{
    int idx = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadId.X;

    if (idx < input.Length)
    {
        // Compute bucket
        float value = input[idx];
        int bucket = (int)(value * bucketCount) % bucketCount;

        // Atomic increment - thread safe
        Kernel.AtomicAdd(ref histogram[bucket], 1);
    }
}
```

Available atomic operations:

| Operation | Description |
|-----------|-------------|
| `AtomicAdd` | Add and return old value |
| `AtomicSub` | Subtract and return old value |
| `AtomicMax` | Maximum and return old value |
| `AtomicMin` | Minimum and return old value |
| `AtomicExchange` | Exchange values |
| `AtomicCompareExchange` | Compare and swap |
| `AtomicLoad` | Load with memory ordering |
| `AtomicStore` | Store with memory ordering |

## Multi-Kernel Coordination

### Using Barriers Across Kernels

```csharp
// Create multi-kernel barrier
var barrier = await orchestrator.CreateMultiKernelBarrierAsync(
    participantCount: 3,  // Number of kernels that will synchronize
    options: new BarrierOptions { Timeout = TimeSpan.FromSeconds(5) });

// Launch kernels that coordinate via barrier
await Task.WhenAll(
    orchestrator.ExecuteKernelAsync(kernel1, config1, buffer1, barrier.GetHandle(0)),
    orchestrator.ExecuteKernelAsync(kernel2, config2, buffer2, barrier.GetHandle(1)),
    orchestrator.ExecuteKernelAsync(kernel3, config3, buffer3, barrier.GetHandle(2))
);

// Kernels synchronize at barrier points
[Kernel]
public static void CoordinatedKernel(Span<float> data, BarrierHandle barrier)
{
    // Phase 1 work
    ComputePhase1(data);

    // Wait for all participating kernels
    barrier.Wait();

    // Phase 2 work (can safely read data from other kernels)
    ComputePhase2(data);
}
```

### Event-Based Coordination

```csharp
// Create events for signaling
var dataReadyEvent = orchestrator.CreateEvent();
var processingCompleteEvent = orchestrator.CreateEvent();

// Producer kernel signals when data is ready
await orchestrator.ExecuteKernelAsync(
    producerKernel, config, dataBuffer,
    signalOnComplete: dataReadyEvent);

// Consumer kernel waits for data
await orchestrator.ExecuteKernelAsync(
    consumerKernel, config, dataBuffer,
    waitForEvents: new[] { dataReadyEvent },
    signalOnComplete: processingCompleteEvent);

// Host waits for all processing
await processingCompleteEvent.WaitAsync();
```

## Reduction with Synchronization

### Block-Level Reduction

```csharp
[Kernel(UseSharedMemory = true)]
public static void BlockReduceSum(
    ReadOnlySpan<float> input,
    Span<float> output)
{
    var shared = Kernel.AllocateShared<float>(256);

    int tid = Kernel.ThreadId.X;
    int gid = Kernel.BlockId.X * Kernel.BlockDim.X + tid;

    // Load to shared memory
    shared[tid] = (gid < input.Length) ? input[gid] : 0;
    Kernel.SyncThreads();

    // Reduction in shared memory
    for (int stride = Kernel.BlockDim.X / 2; stride > 0; stride >>= 1)
    {
        if (tid < stride)
        {
            shared[tid] += shared[tid + stride];
        }
        Kernel.SyncThreads();
    }

    // Write block result
    if (tid == 0)
    {
        output[Kernel.BlockId.X] = shared[0];
    }
}
```

### Warp-Level Reduction (Faster)

```csharp
[Kernel]
public static void WarpReduceSum(
    ReadOnlySpan<float> input,
    Span<float> output)
{
    int gid = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadId.X;

    float value = (gid < input.Length) ? input[gid] : 0;

    // Warp shuffle reduction (no synchronization needed within warp)
    for (int offset = 16; offset > 0; offset >>= 1)
    {
        value += Kernel.ShuffleDown(value, offset);
    }

    // First thread in warp writes result
    if (Kernel.ThreadId.X % 32 == 0)
    {
        Kernel.AtomicAdd(ref output[Kernel.BlockId.X], value);
    }
}
```

## Deadlock Prevention

### Common Deadlock Patterns

```csharp
// DEADLOCK: Not all threads reach barrier
[Kernel]
public static void DeadlockExample(Span<float> data)
{
    int tid = Kernel.ThreadId.X;

    if (tid % 2 == 0)  // Only even threads
    {
        Kernel.SyncThreads();  // DEADLOCK: Odd threads never arrive
    }
}

// CORRECT: All threads reach barrier
[Kernel]
public static void CorrectExample(Span<float> data)
{
    int tid = Kernel.ThreadId.X;

    if (tid % 2 == 0)
    {
        data[tid] = Process(data[tid]);
    }

    Kernel.SyncThreads();  // All threads reach this point

    // Continue with synchronized state
}
```

### Timeout-Based Recovery

```csharp
var barrierOptions = new BarrierOptions
{
    Timeout = TimeSpan.FromSeconds(5),
    TimeoutAction = BarrierTimeoutAction.LogAndContinue
};

try
{
    await barrier.WaitAsync(barrierOptions);
}
catch (BarrierTimeoutException ex)
{
    _logger.LogError(ex, "Barrier timeout - possible deadlock");
    await RecoverFromDeadlock();
}
```

## Performance Considerations

### Barrier Overhead

| Barrier Type | Typical Latency |
|--------------|-----------------|
| Block barrier | ~20 ns |
| Grid barrier | ~1-10 µs |
| Named barrier | ~50-100 ns |
| Cross-kernel | ~10-50 µs |

### Minimizing Synchronization

```csharp
// BAD: Excessive synchronization
[Kernel]
public static void TooManySyncs(Span<float> data)
{
    for (int i = 0; i < 100; i++)
    {
        data[Kernel.ThreadId.X] += 1;
        Kernel.SyncThreads();  // 100 barriers!
    }
}

// GOOD: Minimize barriers
[Kernel]
public static void OptimizedSyncs(Span<float> data)
{
    // Local accumulation (no sync needed)
    float local = 0;
    for (int i = 0; i < 100; i++)
    {
        local += 1;
    }

    // Single write and sync
    data[Kernel.ThreadId.X] = local;
    Kernel.SyncThreads();
}
```

## Exercises

### Exercise 1: Parallel Prefix Sum

Implement a parallel prefix sum (scan) using barriers.

### Exercise 2: Producer-Consumer

Create a producer-consumer pattern with event signaling.

### Exercise 3: Matrix Transpose

Implement matrix transpose using shared memory and barriers.

## Key Takeaways

1. **Block barriers** synchronize threads within a block
2. **Grid barriers** require cooperative launch for full-grid sync
3. **Memory fences** control visibility of writes
4. **Atomics** enable thread-safe updates
5. **Avoid divergent barriers** - all threads must participate

## Next Module

[Multi-GPU Programming →](multi-gpu-programming.md)

Learn to scale applications across multiple GPUs.

## Further Reading

- [Barrier API](../../guides/ring-kernels/barriers.md) - Complete barrier reference
- [Memory Ordering](../../guides/ring-kernels/memory-ordering.md) - Consistency models
- [Phase 3 Coordination](../../guides/ring-kernels/phase3-coordination.md) - Multi-kernel patterns
