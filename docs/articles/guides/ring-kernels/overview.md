# Introduction to Ring Kernels

Ring Kernels are a revolutionary programming model in DotCompute that enables **persistent GPU-resident computation** with actor-style message passing. Unlike traditional kernels that launch, execute, and terminate for each invocation, Ring Kernels remain resident on the GPU, processing messages continuously with near-zero launch overhead.

## What Are Ring Kernels?

Ring Kernels implement the **persistent kernel pattern**, where GPU compute units remain active in a processing loop, consuming messages from lock-free queues and producing results asynchronously. This enables entirely new programming paradigms on GPUs:

### Traditional Kernel Model
```
Host → Launch Kernel → GPU Executes → Kernel Terminates → Host
         (5-50μs overhead per launch)
```

### Ring Kernel Model
```
Host → Launch Once → GPU Stays Resident → Process Messages Continuously
                        (0μs launch overhead after initial launch)
```

## Key Concepts

### 1. Persistent Execution
Ring Kernels run in an infinite loop on the GPU, waiting for and processing messages as they arrive. The kernel lifecycle:

```
Launch → Activate → [Process Messages] → Deactivate → Terminate
           ↑               ↓
           └───────────────┘
```

### 2. Lock-Free Message Passing
Messages are exchanged through **lock-free ring buffers** using atomic operations:
- **Enqueue**: Compare-and-swap to claim slot, write message
- **Dequeue**: Compare-and-swap to claim message, read data
- **Thread-safe**: Multiple producers and consumers without locks

### 3. Actor-Style Programming
Each kernel instance acts as an independent actor with:
- **Mailbox**: Input queue for receiving messages
- **State**: Persistent local state across messages
- **Behavior**: Message processing logic
- **Output**: Results sent to other actors or host

## Why Use Ring Kernels?

### Performance Benefits

**1. Eliminate Launch Overhead**
- Traditional: 5-50μs per kernel launch
- Ring Kernel: One-time launch, then 0μs

**2. High Message Throughput**
- CPU simulation: ~10K-100K messages/sec
- GPU (CUDA): ~1M-10M messages/sec
- GPU (Metal/OpenCL): ~500K-5M messages/sec

**3. Low Latency**
- Traditional: Launch overhead + execution time
- Ring Kernel: Immediate message processing (no launch)

### Programming Model Benefits

**1. Reactive Programming**
- Event-driven computation
- Asynchronous message handling
- Natural fit for streaming data

**2. Actor Systems**
- Isolated actors with message passing
- Location transparency
- Fault isolation

**3. Graph Computation**
- Vertex-centric algorithms (Pregel-style)
- Bulk synchronous parallel (BSP) patterns
- Dynamic workload distribution

## Supported Backends

Ring Kernels work across all DotCompute backends:

| Backend | Status | Performance | Platform |
|---------|--------|-------------|----------|
| **CUDA** | ✅ Production | ~1M-10M msgs/sec | NVIDIA GPUs |
| **Metal** | ✅ Production | ~500K-5M msgs/sec | Apple Silicon |
| **OpenCL** | ✅ Production | ~500K-5M msgs/sec | Cross-platform |
| **CPU** | ✅ Simulation | ~10K-100K msgs/sec | All platforms |

## Use Cases

### 1. Graph Analytics
**Problem**: Traditional batch processing inefficient for dynamic graphs

**Solution**: Vertex-centric message passing with Ring Kernels

```csharp
// PageRank with Ring Kernels
[RingKernel(Mode = RingKernelMode.Persistent, Domain = RingKernelDomain.GraphAnalytics)]
public class PageRankVertex
{
    private float _rank = 1.0f;
    private int _outDegree;

    public void ProcessMessage(VertexMessage msg)
    {
        // Accumulate contributions from neighbors
        _rank = 0.15f + 0.85f * msg.Contribution;

        // Send updated rank to outgoing edges
        float contribution = _rank / _outDegree;
        foreach (var neighbor in GetOutEdges())
        {
            SendMessage(neighbor, new VertexMessage { Contribution = contribution });
        }
    }
}
```

### 2. Spatial Simulations
**Problem**: Stencil computations with frequent halo exchanges

**Solution**: Persistent kernels with local communication

```csharp
// Heat diffusion simulation
[RingKernel(Mode = RingKernelMode.Persistent, Domain = RingKernelDomain.SpatialSimulation)]
public class HeatDiffusion
{
    private float _temperature;
    private const float Alpha = 0.1f;

    public void ProcessMessage(HaloMessage msg)
    {
        // Update temperature from neighbors
        _temperature = (1 - 4 * Alpha) * _temperature
                     + Alpha * (msg.North + msg.South + msg.East + msg.West);

        // Send updated value to neighbors
        BroadcastToNeighbors(new HaloMessage { Value = _temperature });
    }
}
```

### 3. Real-Time Event Processing
**Problem**: Low-latency stream processing on GPU

**Solution**: Event-driven Ring Kernels with immediate processing

```csharp
// Real-time anomaly detection
[RingKernel(Mode = RingKernelMode.EventDriven, Domain = RingKernelDomain.ActorModel)]
public class AnomalyDetector
{
    private MovingAverage _average = new();
    private float _threshold = 3.0f;

    public void ProcessEvent(SensorReading reading)
    {
        float deviation = Math.Abs(reading.Value - _average.Current);

        if (deviation > _threshold * _average.StdDev)
        {
            // Anomaly detected - alert immediately
            SendAlert(new AnomalyAlert
            {
                Timestamp = reading.Timestamp,
                Value = reading.Value,
                ExpectedRange = _average.Current ± _average.StdDev
            });
        }

        _average.Update(reading.Value);
    }
}
```

### 4. Distributed Actor Systems
**Problem**: Scalable actor-based computation

**Solution**: GPU-resident actors with mailbox-based communication

```csharp
// Distributed key-value store actors
[RingKernel(Mode = RingKernelMode.Persistent, Domain = RingKernelDomain.ActorModel)]
public class KVStoreActor
{
    private Dictionary<int, string> _storage = new();

    public void ProcessMessage(KVMessage msg)
    {
        switch (msg.Type)
        {
            case MessageType.Get:
                var value = _storage.TryGetValue(msg.Key, out var v) ? v : null;
                Reply(new KVResponse { Key = msg.Key, Value = value });
                break;

            case MessageType.Put:
                _storage[msg.Key] = msg.Value;
                Reply(new KVResponse { Success = true });
                break;

            case MessageType.Delete:
                _storage.Remove(msg.Key);
                Reply(new KVResponse { Success = true });
                break;
        }
    }
}
```

## Execution Modes

Ring Kernels support two execution modes:

### Persistent Mode
**Behavior**: Kernel runs continuously until explicitly terminated

**Best For**:
- Long-running services
- Continuous stream processing
- Actor systems with steady workload

**Trade-offs**:
- ✅ Zero launch overhead
- ✅ Immediate message processing
- ❌ Consumes GPU resources continuously

```csharp
[RingKernel(Mode = RingKernelMode.Persistent)]
public class PersistentProcessor { }
```

### Event-Driven Mode
**Behavior**: Kernel activates on-demand, processes batch, then idles

**Best For**:
- Bursty workloads
- Power-constrained devices
- Shared GPU resources

**Trade-offs**:
- ✅ Conserves GPU resources
- ✅ Automatic power management
- ❌ Small activation overhead (~1-10μs)

```csharp
[RingKernel(Mode = RingKernelMode.EventDriven)]
public class EventDrivenProcessor { }
```

## Message Passing Strategies

Choose the right strategy for your workload:

### 1. SharedMemory (Fastest)
**Use For**: Intra-block communication, low capacity (<64KB)

```csharp
[RingKernel(MessagingStrategy = MessagePassingStrategy.SharedMemory)]
public class SharedMemoryKernel { }
```

**Characteristics**:
- ⚡ Lowest latency (~10ns access)
- 📊 Limited capacity (GPU shared memory size)
- 🔒 Lock-free with atomic operations
- ✅ Best for producer-consumer patterns

### 2. AtomicQueue (Scalable)
**Use For**: Inter-block communication, larger capacity

```csharp
[RingKernel(MessagingStrategy = MessagePassingStrategy.AtomicQueue)]
public class GlobalMemoryKernel { }
```

**Characteristics**:
- ⚡ Medium latency (~100ns access)
- 📊 Large capacity (GPU global memory)
- 🔒 Lock-free with exponential backoff
- ✅ Best for distributed actors

### 3. P2P (Multi-GPU)
**Use For**: GPU-to-GPU direct transfers

```csharp
[RingKernel(MessagingStrategy = MessagePassingStrategy.P2P)]
public class MultiGPUKernel { }
```

**Characteristics**:
- ⚡ Low latency (~1μs direct copy)
- 🔗 Requires P2P capable GPUs
- 📡 Direct GPU memory access
- ✅ Best for multi-GPU pipelines

### 4. NCCL (Collective)
**Use For**: Multi-GPU reductions and broadcasts

```csharp
[RingKernel(MessagingStrategy = MessagePassingStrategy.NCCL)]
public class CollectiveKernel { }
```

**Characteristics**:
- ⚡ Optimized collective operations
- 🌐 Multi-node support
- 📊 Scales to hundreds of GPUs
- ✅ Best for distributed training

## Synchronization and Memory Ordering

Ring kernels have unique synchronization needs due to message passing. Unlike regular kernels (which default to relaxed memory ordering), ring kernels default to **Release-Acquire consistency** for correct message visibility.

### Barrier Support

Ring kernels support GPU thread barriers for coordinating threads within a kernel instance:

```csharp
[RingKernel(
    UseBarriers = true,                      // Enable barriers
    BarrierScope = BarrierScope.ThreadBlock, // Sync within thread block
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire, // Default for ring kernels
    EnableCausalOrdering = true)]            // Default true for message passing
public static void RingKernelWithBarriers(
    MessageQueue<float> incoming,
    MessageQueue<float> outgoing)
{
    var shared = Kernel.AllocateShared<float>(256);
    int tid = Kernel.ThreadId.X;

    // Phase 1: Process incoming messages into shared memory
    if (incoming.TryDequeue(out var msg))
    {
        shared[tid] = msg;
    }

    Kernel.Barrier();  // Wait for all threads

    // Phase 2: Aggregate and send results
    if (tid == 0)
    {
        float sum = 0;
        for (int i = 0; i < 256; i++)
            sum += shared[i];

        outgoing.Enqueue(sum / 256.0f);
    }
}
```

### Ring Kernel vs Regular Kernel Defaults

Ring kernels have **safer defaults** for message passing:

| Property | Regular Kernel Default | Ring Kernel Default | Reason |
|----------|----------------------|--------------------|---------|
| `MemoryConsistency` | `Relaxed` | `ReleaseAcquire` | Message passing requires causality |
| `EnableCausalOrdering` | `false` | `true` | Ensures message visibility |
| Performance Overhead | 0% | 15% | Acceptable for persistent kernels |

**Key Insight**: Ring kernels run persistently, so the 15% overhead of Release-Acquire consistency is amortized over the kernel's lifetime. This provides safety by default for message-passing patterns.

### When to Use Barriers in Ring Kernels

**Use Barriers**:
- Coordinating shared memory access for message batching
- Implementing reduction operations on incoming messages
- Multi-phase message processing with dependencies
- Aggregating results before sending outgoing messages

**Example: Message Batch Processing**:
```csharp
[RingKernel(
    UseBarriers = true,
    BarrierScope = BarrierScope.ThreadBlock)]
public static void BatchProcessor(
    MessageQueue<int> incoming,
    MessageQueue<int> outgoing)
{
    var shared = Kernel.AllocateShared<int>(256);
    int tid = Kernel.ThreadId.X;

    // Each thread dequeues one message
    shared[tid] = incoming.TryDequeue(out var msg) ? msg : 0;

    Kernel.Barrier();  // Ensure all messages loaded

    // Thread 0 aggregates batch
    if (tid == 0)
    {
        int batchSum = 0;
        for (int i = 0; i < 256; i++)
            batchSum += shared[i];

        outgoing.Enqueue(batchSum);
    }
}
```

**See Also**: [Barriers and Memory Ordering](../advanced/barriers-and-memory-ordering.md) for comprehensive details

## Domain Optimizations

Specify your application domain for automatic optimizations:

### General
```csharp
[RingKernel(Domain = RingKernelDomain.General)]
```
No specific optimizations. Good default.

### GraphAnalytics
```csharp
[RingKernel(Domain = RingKernelDomain.GraphAnalytics)]
```
Optimized for:
- Irregular memory access patterns
- Load imbalance
- Grid synchronization (BSP)

### SpatialSimulation
```csharp
[RingKernel(Domain = RingKernelDomain.SpatialSimulation)]
```
Optimized for:
- Regular memory access patterns
- Local communication
- Halo exchange

### ActorModel
```csharp
[RingKernel(Domain = RingKernelDomain.ActorModel)]
```
Optimized for:
- Message-heavy workloads
- Low-latency delivery
- Dynamic workload distribution

## Getting Started

### 1. Define Your Ring Kernel

```csharp
using DotCompute.Abstractions.RingKernels;

[RingKernel(
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.AtomicQueue,
    Domain = RingKernelDomain.General)]
public class MyFirstRingKernel
{
    private int _messageCount = 0;

    public void ProcessMessage(int data)
    {
        // Process incoming message
        int result = data * 2;
        _messageCount++;

        // Send result
        SendResult(result);
    }
}
```

### 2. Launch the Kernel

```csharp
using DotCompute.Backends.CUDA.RingKernels; // or Metal, OpenCL
using DotCompute.Abstractions.RingKernels;

// Create runtime
var logger = loggerFactory.CreateLogger<CudaRingKernelRuntime>();
var compiler = new CudaRingKernelCompiler(compilerLogger);
var registry = new MessageQueueRegistry();
var runtime = new CudaRingKernelRuntime(logger, compiler, registry);

// Configure launch options (optional - defaults to ProductionDefaults)
var options = RingKernelLaunchOptions.ProductionDefaults();
// Or use:
// var options = RingKernelLaunchOptions.LowLatencyDefaults();
// var options = RingKernelLaunchOptions.HighThroughputDefaults();

// Launch kernel (stays resident)
await runtime.LaunchAsync("my_kernel", gridSize: 1, blockSize: 256, options);

// Activate processing
await runtime.ActivateAsync("my_kernel");
```

### 3. Send Messages

```csharp
// Send 1000 messages
for (int i = 0; i < 1000; i++)
{
    var message = KernelMessage<int>.CreateData(
        senderId: 0,
        receiverId: -1,
        payload: i
    );

    await runtime.SendMessageAsync("my_kernel", message);
}
```

### 4. Monitor Status

```csharp
// Get kernel status
var status = await runtime.GetStatusAsync("my_kernel");
Console.WriteLine($"Active: {status.IsActive}");
Console.WriteLine($"Messages Processed: {status.MessagesProcessed}");

// Get performance metrics
var metrics = await runtime.GetMetricsAsync("my_kernel");
Console.WriteLine($"Throughput: {metrics.ThroughputMsgsPerSec:F0} msgs/sec");
Console.WriteLine($"Avg Latency: {metrics.AvgProcessingTimeMs:F2}ms");
```

### 5. Cleanup

```csharp
// Deactivate (pause processing)
await runtime.DeactivateAsync("my_kernel");

// Terminate (cleanup resources)
await runtime.TerminateAsync("my_kernel");

// Dispose runtime
await runtime.DisposeAsync();
```

## Best Practices

### 1. Choose the Right Mode
- **Persistent**: Steady workloads, low latency critical
- **EventDriven**: Bursty workloads, power efficiency important

### 2. Size Your Queues Appropriately
- Too small: Messages dropped, throughput limited
- Too large: Memory waste, cache pollution
- **Rule of thumb**: 256-1024 messages per queue

### 3. Use Appropriate Message Sizes
- Keep messages small (< 256 bytes ideal)
- Use indirection for large data (pointers to buffers)
- Pad to avoid false sharing (64-byte cache lines)

### 4. Monitor Queue Utilization
```csharp
var metrics = await runtime.GetMetricsAsync("kernel_id");
if (metrics.InputQueueUtilization > 0.8)
{
    // Queue nearly full - increase capacity or add more kernels
}
```

### 5. Handle Termination Gracefully
```csharp
// Set timeout for graceful shutdown
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await runtime.TerminateAsync("kernel_id", cts.Token);
```

## Queue Configuration with RingKernelLaunchOptions

Ring Kernels use message queues for communication, and their behavior is fully configurable via the `RingKernelLaunchOptions` class (introduced in v0.6.2).

### Configuration Properties

```csharp
public sealed class RingKernelLaunchOptions
{
    // Queue capacity (default: 4096, range: 16-1M, must be power-of-2)
    public int QueueCapacity { get; set; } = 4096;

    // Deduplication window (default: 1024, range: 16-1024)
    public int DeduplicationWindowSize { get; set; } = 1024;

    // Backpressure strategy (default: Block)
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;

    // Enable priority-based message ordering (default: false)
    public bool EnablePriorityQueue { get; set; } = false;
}
```

### Factory Methods

**Production Defaults (Recommended for most use cases)**
```csharp
var options = RingKernelLaunchOptions.ProductionDefaults();
// QueueCapacity: 4096 messages (handles burst traffic, 2M+ msg/s)
// DeduplicationWindowSize: 1024 messages (covers recent messages)
// BackpressureStrategy: Block (no message loss)
// EnablePriorityQueue: false (maximize throughput)

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);
```

**Low-Latency Defaults (Sub-microsecond response)**
```csharp
var options = RingKernelLaunchOptions.LowLatencyDefaults();
// QueueCapacity: 256 messages (minimal memory footprint)
// DeduplicationWindowSize: 256 messages (proportional to capacity)
// BackpressureStrategy: Reject (fail-fast, no blocking)
// EnablePriorityQueue: false (FIFO is fastest)

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);
```

**High-Throughput Defaults (Batch processing)**
```csharp
var options = RingKernelLaunchOptions.HighThroughputDefaults();
// QueueCapacity: 16384 messages (large burst buffer)
// DeduplicationWindowSize: 1024 messages (maximum window)
// BackpressureStrategy: Block (no message loss)
// EnablePriorityQueue: false (maximize throughput)

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);
```

### Custom Configuration

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 8192,                          // Power-of-2 (16-1M)
    DeduplicationWindowSize = 512,                 // 16-1024 messages
    BackpressureStrategy = BackpressureStrategy.DropOldest,  // Real-time telemetry
    EnablePriorityQueue = true                     // Enable priority ordering
};

// Validate before launch
options.Validate();  // Throws ArgumentOutOfRangeException if invalid

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);
```

### Backpressure Strategies

Choose the right strategy for your workload:

| Strategy | Behavior | Use Case | Performance |
|----------|----------|----------|-------------|
| **Block** | Wait for space in queue | Guaranteed delivery, no message loss | May stall producer |
| **Reject** | Return false immediately | Fire-and-forget, latency-sensitive | No blocking, predictable latency |
| **DropOldest** | Evict oldest message to make space | Real-time telemetry, latest data most important | No blocking, always succeeds |
| **DropNew** | Discard new message | Historical logging, preserve oldest data | No blocking, returns false |

### Configuration Guidelines

**Queue Capacity Sizing**
- **Too Small**: Messages dropped/rejected, throughput limited
- **Too Large**: Memory waste, cache pollution, stale data
- **Formula**: `Capacity ≥ Peak Message Rate × Polling Interval × 2`
- **Example**: 1000 msg/s × 10ms × 2 = 20 messages minimum, use 64-256 for safety

**Deduplication Window**
- **Cost**: ~32 bytes × window size per queue
- **Benefit**: Prevents duplicate processing (useful for retry scenarios)
- **Auto-Clamping**: Window size automatically clamped to QueueCapacity if smaller

**Memory Usage**
- **Queue Structure**: 64 bytes (head/tail/capacity/metadata)
- **Message Storage**: `QueueCapacity × 32` bytes (for IRingKernelMessage types)
- **Deduplication**: `DeduplicationWindowSize × 32` bytes (hash table)
- **Example**: 4096 capacity + 1024 dedup = ~128KB + 32KB = 160KB per queue

## Performance Expectations

### Message Throughput

| Backend | Single Kernel | Multi-Kernel (4x) |
|---------|---------------|-------------------|
| CUDA | 1-10M msgs/sec | 4-40M msgs/sec |
| Metal | 500K-5M msgs/sec | 2-20M msgs/sec |
| OpenCL | 500K-5M msgs/sec | 2-20M msgs/sec |
| CPU | 10-100K msgs/sec | 40-400K msgs/sec |

### Latency

| Operation | Typical Latency |
|-----------|----------------|
| Launch (one-time) | 1-10ms |
| Activate/Deactivate | 10-100μs |
| Message enqueue (host) | 100-500ns |
| Message processing (GPU) | 10-100ns |
| Terminate | 10-100ms |

### Comparison vs Traditional Kernels

For a workload with 1000 invocations:

**Traditional Kernels**:
- Launch overhead: 1000 × 25μs = 25ms
- Execution time: Variable
- **Total**: 25ms + execution

**Ring Kernels**:
- Launch overhead: 1 × 5ms = 5ms (one-time)
- Execution time: Variable (same as traditional)
- **Total**: 5ms + execution

**Speedup**: ~5x reduction in overhead for this example

## Next Steps

Ready to dive deeper? Check out these resources:

- **[Advanced Ring Kernel Programming](ring-kernels-advanced.md)** - Deep dive into patterns and optimization
- **[Ring Kernel API Reference](/api/DotCompute.Abstractions.RingKernels.html)** - Complete API documentation
- **[Multi-GPU Programming](multi-gpu.md)** - Using Ring Kernels across multiple GPUs
- **[Performance Tuning](performance-tuning.md)** - Optimize your Ring Kernel performance

## Summary

Ring Kernels enable **persistent GPU-resident computation** with:
- ✅ Zero launch overhead after initial launch
- ✅ Actor-style message passing with lock-free queues
- ✅ Cross-backend support (CUDA, Metal, OpenCL, CPU)
- ✅ Multiple execution modes and message strategies
- ✅ Domain-specific optimizations

Perfect for:
- Graph analytics and network algorithms
- Spatial simulations and stencil computations
- Real-time event processing and streaming
- Distributed actor systems

Start building high-performance, GPU-resident applications today!
