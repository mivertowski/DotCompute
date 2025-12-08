# [FEATURE] GPU Atomic Operations for Lock-Free Data Structures

**Labels:** enhancement, performance, gpu-backend

## Is your feature request related to a problem? Please describe.

When building high-performance GPU-resident data structures for financial applications (order books, temporal graphs), we need lock-free concurrent access patterns using atomic operations. Currently, DotCompute doesn't expose GPU atomic primitives, forcing us to either:

1. Serialize all updates (performance bottleneck)
2. Implement custom PTX/SPIR-V inline assembly (non-portable, complex)
3. Use coarse-grained memory barriers (reduces parallelism)

Specific use cases requiring atomics:
- **Order Book Matching**: Concurrent bid/ask queue updates with atomic compare-and-swap
- **Temporal Graph Updates**: Lock-free edge insertion with atomic increment for node degrees
- **Ring Buffer Management**: Atomic head/tail pointer updates for producer/consumer patterns
- **Aggregation Counters**: Atomic add for transaction volume tracking, statistics

## Describe the solution you'd like

Add first-class support for GPU atomic operations that compile to native atomics on each backend (CUDA `atomicAdd`, OpenCL `atomic_add`, etc.).

### Required Atomic Operations

**Basic Atomics:**
- `AtomicAdd<T>(ref T, T)` - Add and return old value
- `AtomicSub<T>(ref T, T)` - Subtract and return old value
- `AtomicExchange<T>(ref T, T)` - Swap and return old value
- `AtomicCompareExchange<T>(ref T, T expected, T desired)` - CAS operation

**Extended Atomics:**
- `AtomicMin<T>(ref T, T)` - Minimum and return old value
- `AtomicMax<T>(ref T, T)` - Maximum and return old value
- `AtomicAnd<T>(ref T, T)` - Bitwise AND
- `AtomicOr<T>(ref T, T)` - Bitwise OR
- `AtomicXor<T>(ref T, T)` - Bitwise XOR

**Memory Ordering:**
- `AtomicLoad<T>(ref T, MemoryOrder)` - Atomic load with ordering
- `AtomicStore<T>(ref T, T, MemoryOrder)` - Atomic store with ordering
- `ThreadFence(MemoryScope)` - Memory barrier/fence

## Describe alternatives you've considered

1. **External synchronization**: Use CPU-side locks to coordinate GPU access - defeats the purpose of GPU parallelism

2. **Kernel-level serialization**: Run single-threaded kernels for critical sections - eliminates GPU throughput advantage

3. **Double-buffering**: Maintain two copies and swap atomically - doubles memory usage, adds complexity

4. **Custom backend extensions**: Write PTX/SPIR-V directly - not portable, maintenance burden

None of these alternatives provide the performance characteristics needed for <10μs latency order matching.

## Proposed API

```csharp
using DotCompute.Atomics;

// Basic atomic operations
[GpuKernel]
public static void OrderBookUpdateKernel(
    OrderBook* book,
    Order* incomingOrder)
{
    // Atomic increment for order count
    var oldCount = Atomics.AtomicAdd(ref book->BidCount, 1);

    // CAS for best bid price update
    long currentBest = book->BestBidPriceCents;
    while (incomingOrder->PriceCents > currentBest)
    {
        var exchanged = Atomics.AtomicCompareExchange(
            ref book->BestBidPriceCents,
            expected: currentBest,
            desired: incomingOrder->PriceCents);

        if (exchanged == currentBest)
            break; // Success

        currentBest = exchanged; // Retry with new value
    }

    // Atomic volume tracking
    Atomics.AtomicAdd(ref book->TotalVolume, incomingOrder->Quantity);
}

// Ring buffer with atomics
[GpuKernel]
public static void RingBufferEnqueue<T>(
    RingBuffer<T>* buffer,
    T item) where T : unmanaged
{
    // Atomic increment of tail pointer (wrapping)
    var slot = Atomics.AtomicAdd(ref buffer->Tail, 1) & buffer->Mask;

    // Store item
    buffer->Data[slot] = item;

    // Memory fence to ensure visibility
    Atomics.ThreadFence(MemoryScope.Device);
}

// Memory ordering options
public enum MemoryOrder
{
    Relaxed,      // No ordering guarantees
    Acquire,      // Acquire semantics
    Release,      // Release semantics
    AcquireRelease, // Both acquire and release
    SequentiallyConsistent // Full ordering
}

public enum MemoryScope
{
    Workgroup,    // Within workgroup
    Device,       // Within device
    System        // Across system (unified memory)
}
```

## Additional context

### Use Case: High-Frequency Order Matching

We're building Orleans.GpuBridge.Kernels, a commercial GPU-accelerated kernel library. Our order matching grain needs:

- **10μs P99 latency** per order
- **100K+ orders/second** throughput
- **Lock-free concurrent access** to bid/ask queues

Without native atomics, we cannot achieve these targets while maintaining deterministic ordering.

### Use Case: Fraud Detection Temporal Graph

Our fraud detection system maintains GPU-resident transaction graphs:

- **Concurrent edge insertion** from multiple transaction streams
- **Atomic node degree updates** for graph traversal
- **Lock-free cycle detection** algorithms (Tarjan's SCC)

### Reference Implementations

- **CUDA**: `atomicAdd`, `atomicCAS`, `atomicExch` - well-documented, stable
- **OpenCL**: `atomic_add`, `atomic_cmpxchg` - requires `cl_khr_global_int32_base_atomics`
- **Metal**: `atomic_fetch_add_explicit`, `atomic_compare_exchange_weak_explicit`
- **DirectCompute**: `InterlockedAdd`, `InterlockedCompareExchange`

## Performance Implications

**Positive:**
- Enables true parallel GPU execution for concurrent data structures
- Eliminates serialization bottlenecks in high-throughput scenarios
- Unlocks <10μs latency for financial applications

**Considerations:**
- Atomic operations have higher latency than regular memory access (~100-500 cycles vs ~4-200 cycles)
- Contention on hot atomics can cause serialization (warp divergence)
- Compiler must preserve atomic semantics (no reordering)

**Mitigation Strategies:**
- Document best practices for atomic usage (minimize contention, cache-line alignment)
- Provide atomic-friendly data structure patterns in documentation
- Consider warp-aggregated atomics as an advanced feature

## GPU Backend Support

Which GPU backends would this feature affect?
- [x] CUDA - Full support via `atomicAdd`, `atomicCAS`, etc.
- [x] OpenCL - Requires `cl_khr_int32_base_atomics` extension
- [x] DirectCompute - `Interlocked*` intrinsics
- [x] Metal - `atomic_*` functions
- [x] CPU (fallback) - `Interlocked.*` .NET methods

## Priority

**High** - This is a blocking requirement for Orleans.GpuBridge.Kernels commercial package. Without GPU atomics, we cannot ship the Banking and OrderMatching domains with production-grade performance characteristics.

## Contacts

- **Requester**: Orleans.GpuBridge.Kernels team
- **Use Case**: High-frequency trading, fraud detection
- **Target Release**: DotCompute 1.0 or earlier patch release
