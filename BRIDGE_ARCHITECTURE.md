# Ring Kernel Message Queue Bridge Architecture

## Problem Statement
Users call `SendToNamedQueueAsync("ringkernel_VectorAddRequestMessage_...", request)` but messages never appear in the kernel's `Span<VectorAddRequestMessage> requestQueue` buffer. The two queue systems are disconnected.

## Architecture Analysis

### Current System
1. **Named Queues** (Host-side): `IMessageQueue<T> where T : IRingKernelMessage`
   - CPU memory
   - Managed objects with Guid, Priority, CorrelationId
   - User-facing API via `SendToNamedQueueAsync()`

2. **GPU-Resident Queues**: `IMessageQueue<T> where T : unmanaged`
   - GPU VRAM
   - Unmanaged structs only
   - Kernel polls via `Span<TMessage>`

3. **The Gap**: No bridge to transfer messages from #1 → #2

## Proposed Bridge Architecture

### High-Level Data Flow
```
┌─────────────────────────────────────────────────────────────────────────┐
│ USER CODE                                                                │
├─────────────────────────────────────────────────────────────────────────┤
│ SendToNamedQueueAsync("queue", new VectorAddRequestMessage {...})      │
└────────────────────┬────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ NAMED QUEUE (Host-side)                                                 │
├─────────────────────────────────────────────────────────────────────────┤
│ IMessageQueue<IRingKernelMessage> - CPU RAM                            │
│ - Holds managed VectorAddRequestMessage objects                         │
│ - Properties: Guid MessageId, byte Priority, Guid? CorrelationId       │
└────────────────────┬────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ BRIDGE (Background Pump Thread)                                         │
├─────────────────────────────────────────────────────────────────────────┤
│ MessageQueueBridge<T>:                                                  │
│ 1. Dequeue message from named queue                                     │
│ 2. Serialize with MemoryPack (ultra-fast source generator)             │
│ 3. Stage in PinnedStagingBuffer (zero-copy, lock-free ring buffer)     │
│ 4. Batch DMA transfer (cuMemcpyHtoD: 1-512 msgs, minimize PCIe)        │
└────────────────────┬────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ GPU MEMORY BUFFER                                                        │
├─────────────────────────────────────────────────────────────────────────┤
│ Raw GPU VRAM allocation (cuMemAlloc)                                    │
│ Layout: [SerializedMsg1][SerializedMsg2]...[SerializedMsgN]            │
│ Format: MemoryPack binary (compact, aligned)                            │
└────────────────────┬────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ GPU DESERIALIZATION (CUDA Kernel Helper)                                │
├─────────────────────────────────────────────────────────────────────────┤
│ __device__ void DeserializeMessages(                                    │
│     byte* serializedBuffer,                                              │
│     VectorAddRequestMessage* outputQueue,                                │
│     int* count)                                                          │
│ - Reads MemoryPack format                                               │
│ - Writes to Span<VectorAddRequestMessage> buffer                        │
└────────────────────┬────────────────────────────────────────────────────┘
                     ↓
┌─────────────────────────────────────────────────────────────────────────┐
│ RING KERNEL                                                              │
├─────────────────────────────────────────────────────────────────────────┤
│ void RingKernel(                                                         │
│     Span<long> timestamps,                                               │
│     Span<VectorAddRequestMessage> requestQueue,  ← NOW POPULATED!       │
│     Span<VectorAddResponseMessage> responseQueue)                        │
│ {                                                                        │
│     while (true) {                                                       │
│         if (requestQueue.Length > 0) {                                   │
│             var msg = requestQueue[0];  // Process message               │
│         }                                                                │
│     }                                                                    │
│ }                                                                        │
└─────────────────────────────────────────────────────────────────────────┘
```

## Key Question: Message Type Duality

**IRingKernelMessage types must be BOTH:**
1. **Managed class** (for host-side named queue communication)
   - Has Guid, string, nullable properties
   - Lives in CPU RAM
2. **Unmanaged struct** (for GPU kernel consumption)
   - Blittable layout
   - Lives in GPU VRAM

**Solution: Use MemoryPack with [MemoryPackable] + [StructLayout(Sequential)]**

```csharp
[MemoryPackable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial struct VectorAddRequestMessage : IRingKernelMessage
{
    // IRingKernelMessage properties (aligned for GPU)
    public Guid MessageId { get; set; }
    public byte Priority { get; set; }
    public Guid? CorrelationId { get; set; }  // Nullable handled by MemoryPack

    // Application data (unmanaged types only)
    public float A { get; set; }
    public float B { get; set; }

    // MemoryPack auto-generates ultra-fast serialization
    public readonly string MessageType => "VectorAddRequestMessage";
    public readonly int PayloadSize => MemoryPackSerializer.GetSerializedSize(this);

    public readonly ReadOnlySpan<byte> Serialize()
        => MemoryPackSerializer.Serialize(this);

    public void Deserialize(ReadOnlySpan<byte> data)
        => this = MemoryPackSerializer.Deserialize<VectorAddRequestMessage>(data);
}
```

## Performance Optimizations

### 1. MemoryPack Serialization
- **2-5x faster** than MessagePack
- Source generator (zero-reflection, Native AOT compatible)
- Supports nullable types, Guid, DateTime
- Minimal allocations

### 2. Zero-Copy Pipeline
- Pinned memory for DMA (GCHandle.Alloc with pinned: true)
- Lock-free ring buffer (CAS operations)
- Direct cuMemcpyHtoD to GPU

### 3. Batch Transfers
- Accumulate 1-512 messages before DMA
- Single PCIe transaction reduces latency
- Adaptive batching based on queue depth

### 4. Adaptive Backoff
- SpinWait(10) for <100ns latency (idle queue)
- Thread.Yield() for <1μs latency (light load)
- Thread.Sleep(1) for >1ms latency (heavy load)

## Implementation Steps

1. **Add MemoryPack NuGet package**
   ```xml
   <PackageReference Include="MemoryPack" Version="1.21.1" />
   ```

2. **Update IMessageSerializer to use MemoryPack**
   ```csharp
   public sealed class MemoryPackMessageSerializer<T> : IMessageSerializer<T>
       where T : IRingKernelMessage
   {
       public int Serialize(T message, Span<byte> destination)
       {
           return MemoryPackSerializer.Serialize(destination, message);
       }

       public T Deserialize(ReadOnlySpan<byte> source)
       {
           return MemoryPackSerializer.Deserialize<T>(source);
       }
   }
   ```

3. **Simplify CudaMessageQueueBridgeFactory**
   - Allocate raw GPU buffer with cuMemAlloc
   - Track buffer pointer + size
   - Transfer serialized bytes directly
   - No intermediate CudaMessageQueue<byte>

4. **GPU-side Deserialization** (Future enhancement)
   - Generate CUDA kernel helper for MemoryPack format
   - Or: Deserialize on host, transfer structs directly

## Recommended Approach

**For MVP (Minimal Viable Product):**
Use IRingKernelMessage's existing `Serialize()` method (already optimized with BinaryPrimitives). This avoids:
- Adding MemoryPack dependency
- Requiring users to annotate types
- GPU-side deserialization complexity

**For Performance-Optimized Version:**
Migrate to MemoryPack after MVP works. Benefits:
- 2-5x faster serialization
- Better nullable handling
- Future-proof for complex types

## Decision Point

**Which serialization approach should we use?**

**Option A: Use IRingKernelMessage.Serialize() (built-in)**
- ✅ Already implemented via source generator
- ✅ No new dependencies
- ✅ Works with existing code
- ✅ Fast (BinaryPrimitives)
- ❌ Less flexible than MemoryPack

**Option B: Migrate to MemoryPack**
- ✅ 2-5x faster serialization
- ✅ Modern, battle-tested library
- ✅ Better complex type support
- ❌ New dependency
- ❌ Requires user code changes
- ❌ Migration effort

**Recommendation: Start with Option A for working MVP, then benchmark Option B.**

## Next Steps

1. Simplify CudaMessageQueueBridgeFactory to allocate raw GPU memory
2. Use existing IRingKernelMessage.Serialize() for MVP
3. Fix compilation errors
4. Get end-to-end test working
5. Benchmark and decide on MemoryPack migration
