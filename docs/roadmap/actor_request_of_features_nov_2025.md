# DotCompute Feature Requests for GPU-Native Actor Development

**Date**: November 13, 2025
**Context**: Orleans.GpuBridge.Core - GPU-native distributed actors
**DotCompute Version**: 0.4.2-rc2
**Priority**: High-impact features for production actor systems

---

## 1. Ring Kernel Debugging & Observability

### 1.1 Built-in Telemetry APIs
**Problem**: Ring kernels run forever in infinite loops - impossible to know if they're processing messages correctly.

**Feature Request**:
```csharp
[RingKernel(
    KernelId = "MyActor",
    EnableTelemetry = true,  // NEW
    TelemetryInterval = 1000 // Report every 1000 messages
)]
public static void ActorRing(
    Span<ActorMessage> queue,
    Span<RingKernelTelemetry> telemetry)  // NEW: Auto-injected
{
    telemetry[0].MessagesProcessed++;
    telemetry[0].LastProcessedTimestamp = GetGpuTimestamp();
    // ... process message
}
```

**Benefits**:
- CPU can poll `telemetry` buffer to check kernel health
- Detect stuck/deadlocked kernels
- Track message throughput in real-time
- Essential for production monitoring

**API Surface**:
```csharp
public struct RingKernelTelemetry
{
    public ulong MessagesProcessed;
    public ulong MessagesDropped;
    public long LastProcessedTimestamp;
    public int QueueDepth;
    public ulong TotalLatencyNanos;
}

// CPU-side access
var telemetry = await runtime.GetTelemetryAsync(kernelId);
Console.WriteLine($"Throughput: {telemetry.MessagesProcessed / uptime} msg/s");
```

---

### 1.2 GPU Debugger Integration
**Problem**: No way to step through ring kernel code or inspect variables.

**Feature Request**:
- CUDA-GDB integration via source generator hints
- Nsight Compute profile markers
- Conditional breakpoints for specific message IDs
- Watch variables in infinite loops

**Example**:
```csharp
[RingKernel(EnableDebugSymbols = true)]  // NEW
public static void ActorRing(...)
{
    // Source generator emits CUDA-GDB compatible symbols
    DebugMarker("Processing message");  // NEW: Maps to printf/assert in CUDA
}
```

---

### 1.3 CPU Simulator for Ring Kernels
**Problem**: Testing ring kernels requires GPU, slow iteration.

**Feature Request**:
```csharp
var runtime = RingKernelRuntime.CreateSimulator();  // NEW: CPU-based simulator
await runtime.LaunchAsync("MyActor", gridSize: 1, blockSize: 1);

// Simulator runs kernel on thread pool, allows debugging with VS/Rider
// Validates message queue logic before GPU deployment
```

**Benefits**:
- Fast iteration (no GPU compilation)
- Full debugger support (breakpoints, watches)
- CI/CD testing without GPU hardware
- Validate actor logic before optimization

---

## 2. Type-Safe Message Queues

### 2.1 Generic Message Queue Support
**Problem**: Current API uses `Span<T>` which is error-prone for complex messages.

**Feature Request**:
```csharp
// Define message types
public struct VectorAddRequest : IRingKernelMessage
{
    public MessageHeader Header;  // NEW: Auto-generated
    public int VectorLength;
    public GpuMemoryHandle<float> VectorA;
    public GpuMemoryHandle<float> VectorB;
}

[RingKernel(
    MessageType = typeof(VectorAddRequest),  // NEW: Type-safe
    ResponseType = typeof(VectorAddResponse)
)]
public static void VectorAddRing(
    MessageQueue<VectorAddRequest> input,   // NEW: Type-safe queue
    MessageQueue<VectorAddResponse> output
)
{
    if (input.TryDequeue(out var request))
    {
        var response = ProcessRequest(request);
        output.Enqueue(response);
    }
}
```

**Source Generator Emits**:
```csharp
// Auto-generated marshalling code
public class VectorAddRingWrapper
{
    public async Task<VectorAddResponse> SendAsync(VectorAddRequest request)
    {
        // Automatic serialization to GPU memory
        // Enqueue, wait for response, deserialize
    }
}
```

**Benefits**:
- Compile-time type checking
- No manual memory layout
- Automatic versioning/compatibility checks
- IntelliSense for message fields

---

### 2.2 Message Priority Queues
**Problem**: All messages have equal priority, no urgency handling.

**Feature Request**:
```csharp
public enum MessagePriority { Low, Normal, High, Critical }

[RingKernel(EnablePriorities = true)]  // NEW
public static void ActorRing(
    PriorityMessageQueue<ActorMessage> queue  // NEW: Multi-queue
)
{
    // Automatically processes Critical first, then High, Normal, Low
    if (queue.TryDequeueHighestPriority(out var msg))
    {
        // Process urgent message first
    }
}
```

**Use Cases**:
- Heartbeats (Critical) vs analytics (Low)
- Control plane (High) vs data plane (Normal)
- Deadlines and SLA handling

---

## 3. GPU Memory Management Enhancements

### 3.1 Automatic Memory Pooling
**Problem**: Frequent GPU allocations cause 10-50μs latency spikes.

**Feature Request**:
```csharp
[RingKernel(
    MemoryPoolSize = 1024 * 1024 * 100,  // NEW: 100MB pool
    EnablePooling = true
)]
public static void ActorRing(
    MemoryPool<float> pool  // NEW: Auto-managed pool
)
{
    var buffer = pool.Rent(vectorSize);  // Fast O(1) allocation
    // ... use buffer
    pool.Return(buffer);  // Returns to pool, no GPU free
}
```

**Benefits**:
- Predictable latency (no allocation spikes)
- Reduced GPU memory fragmentation
- Automatic defragmentation during idle periods

---

### 3.2 Strongly-Typed GPU Handles
**Problem**: `ulong` handles are error-prone, no type safety.

**Feature Request**:
```csharp
// Type-safe handle wrapper
public readonly struct GpuMemoryHandle<T> where T : unmanaged
{
    private readonly ulong _handle;
    public int Length { get; }

    // Prevents mixing float[] handles with int[] handles
}

[RingKernel]
public static void ActorRing(
    GpuMemoryHandle<float> vectorA,  // Type-safe!
    GpuMemoryHandle<int> indices     // Won't compile if swapped
)
{
    // Source generator validates type compatibility
}
```

---

### 3.3 GPU Memory Leak Detection
**Problem**: Long-running ring kernels can leak GPU memory.

**Feature Request**:
```csharp
var runtime = new CudaRingKernelRuntime(logger, enableLeakDetection: true);  // NEW

// After kernel terminates:
var leakReport = await runtime.GetMemoryLeakReportAsync(kernelId);
if (leakReport.LeakedBytes > 0)
{
    logger.LogWarning($"Kernel leaked {leakReport.LeakedBytes} bytes");
    // leakReport.LeakedHandles - list of handles not freed
}
```

---

## 4. Actor-Specific Abstractions

### 4.1 Mailbox Abstraction
**Problem**: Manually managing request/response queues is boilerplate-heavy.

**Feature Request**:
```csharp
[RingKernel(
    MailboxType = MailboxType.RequestResponse  // NEW: Built-in pattern
)]
public static void ActorRing(
    ActorMailbox<VectorAddRequest, VectorAddResponse> mailbox  // NEW
)
{
    while (mailbox.TryReceive(out var request, out var sender))
    {
        var response = ProcessRequest(request);
        mailbox.Reply(sender, response);  // Automatic routing
    }
}
```

**Mailbox Types**:
- `RequestResponse` - RPC pattern
- `FireAndForget` - One-way messages
- `PublishSubscribe` - Broadcast to multiple actors
- `RequestMulticast` - Scatter-gather pattern

---

### 4.2 Backpressure & Flow Control
**Problem**: Fast producers can overwhelm slow consumer actors.

**Feature Request**:
```csharp
[RingKernel(
    BackpressureStrategy = BackpressureStrategy.DropOldest,  // NEW
    MaxQueueDepth = 1024
)]
public static void ActorRing(...)
{
    // Automatically drops oldest messages when queue full
    // Alternative strategies: DropNewest, Block, DynamicBatching
}
```

**CPU-Side API**:
```csharp
try
{
    await actor.SendAsync(message, timeout: TimeSpan.FromMilliseconds(100));
}
catch (QueueFullException)
{
    // Handle backpressure
}
```

---

### 4.3 Dead Letter Queue
**Problem**: Failed messages disappear, hard to debug failures.

**Feature Request**:
```csharp
[RingKernel(
    DeadLetterQueue = true  // NEW
)]
public static void ActorRing(
    MessageQueue<Request> input,
    DeadLetterQueue<Request> deadLetters  // NEW: Failed messages
)
{
    if (!ValidateMessage(request))
    {
        deadLetters.Enqueue(request, reason: "Invalid format");
        return;
    }
}
```

**CPU-Side Inspection**:
```csharp
var deadLetters = await runtime.GetDeadLettersAsync(kernelId);
foreach (var (message, reason, timestamp) in deadLetters)
{
    logger.LogError($"Failed message: {reason} at {timestamp}");
}
```

---

## 5. Performance & Profiling

### 5.1 Built-in Latency Tracking
**Problem**: Manually tracking message latency is tedious and slow.

**Feature Request**:
```csharp
[RingKernel(
    TrackLatency = true,  // NEW: Automatic histogram
    LatencyBuckets = new[] { 100, 500, 1000, 5000 }  // Nanoseconds
)]
public static void ActorRing(...)
{
    // DotCompute automatically tracks:
    // - Enqueue timestamp
    // - Dequeue timestamp
    // - Processing time
    // - Response time
}
```

**CPU-Side Query**:
```csharp
var latency = await runtime.GetLatencyHistogramAsync(kernelId);
Console.WriteLine($"P50: {latency.P50}ns, P99: {latency.P99}ns");
```

---

### 5.2 Nsight Compute Integration
**Problem**: Profiling ring kernels requires manual NVTX markers.

**Feature Request**:
```csharp
[RingKernel(EnableNsightMarkers = true)]  // NEW
public static void ActorRing(...)
{
    // Source generator auto-inserts NVTX ranges:
    // nvtxRangePush("ProcessMessage");
    ProcessMessage(msg);
    // nvtxRangePop();
}
```

**Benefits**:
- Zero-overhead when profiling disabled
- Automatic correlation with CPU timeline
- Per-message-type breakdown in Nsight

---

### 5.3 Adaptive Performance Tuning
**Problem**: Optimal block size varies by workload, manual tuning required.

**Feature Request**:
```csharp
[RingKernel(
    AdaptiveBlockSize = true  // NEW: Runtime auto-tuning
)]
public static void ActorRing(...)
{
    // DotCompute monitors:
    // - GPU occupancy
    // - Queue depth
    // - Processing latency
    // Automatically adjusts blockSize for optimal throughput
}
```

---

## 6. Multi-GPU & Distributed Features

### 6.1 Multi-GPU Actor Distribution
**Problem**: Manually distributing actors across GPUs is complex.

**Feature Request**:
```csharp
var runtime = new CudaRingKernelRuntime(new CudaRuntimeOptions
{
    MultiGpuStrategy = MultiGpuStrategy.RoundRobin,  // NEW
    GpuDevices = new[] { 0, 1, 2, 3 }  // Use 4 GPUs
});

await runtime.LaunchAsync("Actor1", gpuId: 0);  // Pin to specific GPU
await runtime.LaunchAsync("Actor2", gpuId: MultiGpu.Auto);  // Auto-select
```

---

### 6.2 GPU-to-GPU Messaging (NCCL/NVLink)
**Problem**: Cross-GPU messages go through CPU, adds 10-50μs latency.

**Feature Request**:
```csharp
[RingKernel(
    MessagingStrategy = MessagePassingStrategy.NCCL  // Already exists!
)]
public static void Actor1Ring(...)
{
    // Messages to Actor2 (on different GPU) use NCCL/NVLink
    // Zero CPU involvement, <1μs latency
}
```

**Needs**:
- Better documentation for NCCL setup
- Automatic topology discovery
- Fallback to CPU path if NCCL unavailable

---

### 6.3 GPUDirect Storage Integration
**Problem**: Loading actor state from SSD goes through CPU RAM.

**Feature Request**:
```csharp
[RingKernel(EnableGPUDirectStorage = true)]  // NEW
public static void ActorRing(
    GpuDirectFile<ActorState> stateFile  // NEW: Direct SSD->GPU
)
{
    // Restore state from SSD directly to GPU memory
    var state = await stateFile.ReadAsync(offset: actorId * stateSize);

    // Process messages...

    // Checkpoint state back to SSD (GPU->SSD, no CPU)
    await stateFile.WriteAsync(state, offset: actorId * stateSize);
}
```

**Benefits**:
- 10x faster state restoration (no CPU RAM copy)
- Essential for large actor state (GBs)
- Enables persistent actors with checkpointing

---

## 7. Development Experience

### 7.1 Hot Reload for Kernels
**Problem**: Kernel changes require full rebuild/relaunch (slow iteration).

**Feature Request**:
```csharp
var runtime = new CudaRingKernelRuntime(new CudaRuntimeOptions
{
    EnableHotReload = true  // NEW: Watch for .cs changes
});

// Developer modifies VectorAddRing() code
// DotCompute detects change, recompiles PTX, hot-swaps kernel
// Messages in flight preserved, minimal downtime
```

---

### 7.2 Better Error Messages
**Problem**: Cryptic CUDA errors like "invalid configuration" don't help.

**Current**:
```
error: CUDA kernel launch failed: invalid configuration
```

**Feature Request**:
```
error: CUDA kernel launch failed: invalid configuration
Kernel: VectorAddRing
Block size: 512
Max threads per block: 256 (on device 0)
Suggestion: Reduce BlockDimensions to [256] or lower
```

---

### 7.3 Source Generator Diagnostics
**Problem**: Silent failures when attributes are misconfigured.

**Feature Request**:
- Analyzer warnings for common mistakes:
  - `warning DC1001: KernelId should be unique. 'VectorAdd' already used by SampleKernels.cs`
  - `warning DC1002: InputQueueSize (256) should be power of 2 for optimal performance. Suggested: 256`
  - `warning DC1003: MemoryConsistency=Relaxed dangerous without manual fences. Consider ReleaseAcquire.`

---

## 8. Safety & Correctness

### 8.1 Deadlock Detection
**Problem**: Circular message dependencies can deadlock actors.

**Feature Request**:
```csharp
var runtime = new CudaRingKernelRuntime(new CudaRuntimeOptions
{
    EnableDeadlockDetection = true,  // NEW
    DeadlockTimeoutMs = 5000
});

// If Actor1 waits for Actor2, and Actor2 waits for Actor1:
// - Detects cycle after 5 seconds
// - Logs warning with dependency graph
// - Optionally terminates one actor to break cycle
```

---

### 8.2 Queue Overflow Handling
**Problem**: Queue overflow crashes or drops messages silently.

**Feature Request**:
```csharp
[RingKernel(
    OverflowStrategy = QueueOverflowStrategy.Expand  // NEW
)]
public static void ActorRing(...)
{
    // Strategies:
    // - Block: Sender waits (default)
    // - DropOldest: Remove oldest message
    // - DropNewest: Reject new message
    // - Expand: Dynamically grow queue (dangerous, but useful)
}
```

---

### 8.3 Automatic Retry with Exponential Backoff
**Problem**: Transient GPU errors (OOM, etc.) should retry.

**Feature Request**:
```csharp
[RingKernel(
    RetryStrategy = RetryStrategy.ExponentialBackoff,  // NEW
    MaxRetries = 3,
    InitialRetryDelayMs = 100
)]
public static void ActorRing(...)
{
    // If kernel crashes (OOM, etc.):
    // 1st retry: 100ms delay
    // 2nd retry: 200ms delay
    // 3rd retry: 400ms delay
    // After 3 failures: Propagate exception to CPU
}
```

---

## 9. Documentation & Examples

### 9.1 More Ring Kernel Examples
**Needed**:
- ✅ Simple request-response actor
- ✅ Actor supervision hierarchy (parent watches children)
- ✅ Scatter-gather pattern (1 coordinator, N workers)
- ✅ Pipeline pattern (Actor1 → Actor2 → Actor3)
- ✅ Publish-subscribe with topics
- ✅ State machine actor (FSM on GPU)
- ✅ Rate limiter actor (token bucket)

---

### 9.2 Performance Tuning Guide
**Needed**:
- Queue sizing formulas (based on message rate)
- Block size selection (occupancy vs latency tradeoff)
- Memory consistency model comparison (Relaxed vs ReleaseAcquire vs Sequential)
- Multi-GPU scaling best practices
- When to use NCCL vs Atomics vs SharedMemory

---

### 9.3 Orleans Integration Guide
**Needed**:
- How to integrate ring kernels with Orleans grains
- GPU-aware placement strategies
- Failover handling (GPU crash → CPU fallback)
- State persistence patterns
- Distributed actor coordination

---

## 10. Priority Ranking

### Must-Have (Block Production Use):
1. **Telemetry APIs** (1.1) - Can't monitor actors without this
2. **Type-Safe Message Queues** (2.1) - Error-prone without type safety
3. **Memory Pooling** (3.1) - Latency spikes unacceptable
4. **Dead Letter Queue** (4.3) - Must not lose failed messages
5. **Better Error Messages** (7.2) - Debugging is painful

### High Priority (Significantly Improve DX):
6. **CPU Simulator** (1.3) - Fast iteration essential
7. **Mailbox Abstraction** (4.1) - Too much boilerplate currently
8. **Latency Tracking** (5.1) - Manual tracking is tedious
9. **Multi-GPU Distribution** (6.1) - Needed for scale
10. **Hot Reload** (7.1) - Developer productivity

### Nice-to-Have (Quality of Life):
11. GPU Debugger Integration (1.2)
12. Priority Queues (2.2)
13. Strongly-Typed Handles (3.2)
14. Backpressure (4.2)
15. Nsight Integration (5.2)

### Future Enhancements:
16. Adaptive Tuning (5.3)
17. GPUDirect Storage (6.3)
18. Deadlock Detection (8.1)
19. Automatic Retry (8.3)

---

## 11. Estimated Impact

| Feature | Dev Time Saved | Performance Gain | Production Readiness |
|---------|----------------|------------------|----------------------|
| Telemetry APIs | 50% (monitoring) | 0% | 🔴 Blocker |
| Type-Safe Queues | 30% (less bugs) | 0% | 🔴 Blocker |
| Memory Pooling | 10% | 90% (latency) | 🔴 Blocker |
| CPU Simulator | 70% (iteration) | 0% | 🟡 High Priority |
| Mailbox Abstraction | 40% (boilerplate) | 0% | 🟡 High Priority |

---

## Conclusion

These enhancements would transform DotCompute from a **low-level GPU kernel framework** into a **production-ready GPU-native actor platform**.

**Key Themes**:
1. **Observability**: Can't run actors in production without telemetry
2. **Type Safety**: Manual memory layout is error-prone
3. **Developer Experience**: Fast iteration crucial for adoption
4. **Performance**: Pooling and profiling essential
5. **Safety**: Deadlock detection and retry logic needed

**Next Steps**:
1. Prioritize top 5 "Must-Have" features for DotCompute v0.5.0
2. Prototype Telemetry APIs and Type-Safe Queues first
3. Collaborate with Orleans.GpuBridge.Core team for validation
4. Publish performance benchmarks comparing actors with/without enhancements

---

**Orleans.GpuBridge.Core**: https://github.com/mivertowski/Orleans.GpuBridge.Core

---

further findings:

The DotCompute.Generators v0.4.2-rc2 source generator creates code that references
  unpublished DotCompute APIs:

  - ❌ OpenCLDeviceManager - exists in DotCompute source but NOT in NuGet packages
  - ❌ Metal.RingKernels namespace - same issue
  - ❌ Wrong logger types - expects ILogger<T> but generates ILogger

  These types exist in /home/mivertowski/DotCompute/DotCompute/src but weren't published
  to NuGet v0.4.2-rc2.

fix the generator to only create factories for published backends