# Ring Kernel Fundamentals

This module introduces Ring Kernels - persistent GPU-resident computations that use actor-style message passing for continuous processing.

## What Are Ring Kernels?

Traditional GPU kernels:
- Launch, execute, terminate
- Overhead on each launch
- Stateless between invocations

Ring Kernels:
- Launch once, run continuously
- Zero overhead after initial launch
- Maintain state between messages
- Actor-style message processing

## Ring Kernel Architecture

```
┌─────────────────────────────────────────────────────────┐
│                      GPU Memory                          │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │ Input Queue │───▶│ Ring Kernel │───▶│Output Queue │  │
│  └─────────────┘    │  (Running)  │    └─────────────┘  │
│         ▲           └─────────────┘           │          │
│         │                                     │          │
└─────────┼─────────────────────────────────────┼──────────┘
          │                                     │
     Host Enqueue                         Host Dequeue
```

## Your First Ring Kernel

### Step 1: Define the Kernel

```csharp
using DotCompute.Generators.Kernel.Attributes;
using MemoryPack;

// Define message types
[MemoryPackable]
public partial struct VectorAddRequest : IRingKernelMessage
{
    public uint MessageId { get; set; }
    public ulong Timestamp { get; set; }

    public int Index { get; set; }
    public float ValueA { get; set; }
    public float ValueB { get; set; }
}

[MemoryPackable]
public partial struct VectorAddResponse : IRingKernelMessage
{
    public uint MessageId { get; set; }
    public ulong Timestamp { get; set; }

    public int Index { get; set; }
    public float Result { get; set; }
}

// Define the Ring Kernel
public static partial class MyRingKernels
{
    [RingKernel(
        KernelId = "vector-add",
        InputMessageType = typeof(VectorAddRequest),
        OutputMessageType = typeof(VectorAddResponse),
        Capacity = 1024,
        ProcessingMode = RingProcessingMode.Continuous)]
    public static VectorAddResponse ProcessVectorAdd(VectorAddRequest request)
    {
        return new VectorAddResponse
        {
            MessageId = request.MessageId,
            Timestamp = request.Timestamp,
            Index = request.Index,
            Result = request.ValueA + request.ValueB
        };
    }
}
```

### Step 2: Launch the Ring Kernel

```csharp
var ringKernelService = provider.GetRequiredService<IRingKernelService>();

// Launch the Ring Kernel
var kernel = await ringKernelService.LaunchAsync<VectorAddRequest, VectorAddResponse>(
    MyRingKernels.ProcessVectorAdd,
    new RingKernelLaunchOptions
    {
        QueueCapacity = 1024,
        BackpressureStrategy = BackpressureStrategy.Block
    });

Console.WriteLine($"Ring Kernel launched: {kernel.KernelId}");
```

### Step 3: Send and Receive Messages

```csharp
// Send messages to the kernel
for (int i = 0; i < 1000; i++)
{
    await kernel.SendAsync(new VectorAddRequest
    {
        MessageId = (uint)i,
        Index = i,
        ValueA = i * 1.0f,
        ValueB = i * 2.0f
    });
}

// Receive responses
int received = 0;
while (received < 1000)
{
    if (await kernel.TryReceiveAsync(out var response))
    {
        Console.WriteLine($"Result[{response.Index}] = {response.Result}");
        received++;
    }
}
```

### Step 4: Shutdown

```csharp
// Graceful shutdown
await kernel.DeactivateAsync();
await kernel.TerminateAsync();
```

## Processing Modes

### Continuous Mode

Process messages as they arrive:

```csharp
[RingKernel(ProcessingMode = RingProcessingMode.Continuous)]
public static Response Process(Request request)
{
    // Called continuously while messages are available
    return ProcessMessage(request);
}
```

### Batch Mode

Process messages in batches:

```csharp
[RingKernel(
    ProcessingMode = RingProcessingMode.Batch,
    MaxMessagesPerIteration = 100)]
public static Response Process(Request request)
{
    // Called for each message in batch
    return ProcessMessage(request);
}
```

### Adaptive Mode

Automatically adjust between continuous and batch based on load:

```csharp
[RingKernel(ProcessingMode = RingProcessingMode.Adaptive)]
public static Response Process(Request request)
{
    // System optimizes processing strategy
    return ProcessMessage(request);
}
```

## Message Serialization

Ring Kernels use MemoryPack for efficient GPU-compatible serialization.

### Message Requirements

```csharp
[MemoryPackable]
public partial struct MyMessage : IRingKernelMessage
{
    // Required fields
    public uint MessageId { get; set; }
    public ulong Timestamp { get; set; }

    // Your data fields
    public int CustomField1 { get; set; }
    public float CustomField2 { get; set; }

    // Arrays supported (fixed size recommended)
    [MemoryPackInclude]
    public float[] Data { get; set; }
}
```

### Serialization Performance

| Data Size | Serialization | Deserialization |
|-----------|---------------|-----------------|
| 32 bytes | ~100 ns | ~80 ns |
| 256 bytes | ~200 ns | ~150 ns |
| 1 KB | ~500 ns | ~400 ns |

## Kernel-to-Kernel Communication

Ring Kernels can communicate directly on the GPU:

```csharp
[RingKernel(
    KernelId = "producer",
    PublishesToKernels = new[] { "consumer" })]
public static ProducerMessage Produce(InputMessage input)
{
    // Output goes to consumer kernel
    return new ProducerMessage { Data = Process(input) };
}

[RingKernel(
    KernelId = "consumer",
    SubscribesToKernels = new[] { "producer" })]
public static OutputMessage Consume(ProducerMessage message)
{
    // Receives from producer kernel
    return new OutputMessage { Result = FinalProcess(message) };
}
```

## Ring Kernel Lifecycle

```
┌───────────┐     ┌──────────┐     ┌──────────┐     ┌────────────┐
│  Launch   │────▶│  Active  │────▶│ Inactive │────▶│ Terminated │
└───────────┘     └──────────┘     └──────────┘     └────────────┘
                       │                 ▲
                       │   Deactivate    │
                       └─────────────────┘
                              Activate
```

**States:**
- **Launched**: Kernel code loaded, not processing
- **Active**: Processing messages continuously
- **Inactive**: Paused, can be reactivated
- **Terminated**: Resources freed

```csharp
// Lifecycle control
await kernel.ActivateAsync();   // Start processing
await kernel.DeactivateAsync(); // Pause processing
await kernel.ActivateAsync();   // Resume processing
await kernel.TerminateAsync();  // Cleanup
```

## Telemetry and Monitoring

```csharp
// Get kernel metrics
var metrics = await kernel.GetMetricsAsync();

Console.WriteLine($"Messages processed: {metrics.MessagesProcessed}");
Console.WriteLine($"Messages pending: {metrics.MessagesPending}");
Console.WriteLine($"Average latency: {metrics.AverageLatencyMicroseconds} µs");
Console.WriteLine($"Throughput: {metrics.MessagesPerSecond} msg/s");
```

## Error Handling

```csharp
[RingKernel(KernelId = "robust-processor")]
public static OutputMessage ProcessWithErrorHandling(InputMessage input)
{
    try
    {
        return new OutputMessage
        {
            Success = true,
            Result = ProcessSafely(input)
        };
    }
    catch
    {
        return new OutputMessage
        {
            Success = false,
            ErrorCode = ErrorCodes.ProcessingFailed
        };
    }
}
```

## Best Practices

### 1. Keep Messages Small

```csharp
// GOOD: Small message with indices
[MemoryPackable]
public partial struct SmallMessage : IRingKernelMessage
{
    public uint MessageId { get; set; }
    public ulong Timestamp { get; set; }
    public int DataIndex { get; set; }  // Reference to larger data
}

// AVOID: Large embedded data
[MemoryPackable]
public partial struct LargeMessage : IRingKernelMessage
{
    public uint MessageId { get; set; }
    public ulong Timestamp { get; set; }
    public float[] LargeArray { get; set; }  // 10K elements
}
```

### 2. Use Appropriate Queue Capacity

```csharp
// High throughput, bursty traffic
new RingKernelLaunchOptions { QueueCapacity = 4096 };

// Low latency, steady traffic
new RingKernelLaunchOptions { QueueCapacity = 256 };
```

### 3. Handle Backpressure

```csharp
// Block when queue full (default)
new RingKernelLaunchOptions { BackpressureStrategy = BackpressureStrategy.Block };

// Drop oldest messages
new RingKernelLaunchOptions { BackpressureStrategy = BackpressureStrategy.DropOldest };

// Reject new messages
new RingKernelLaunchOptions { BackpressureStrategy = BackpressureStrategy.Reject };
```

## Exercises

### Exercise 1: Echo Kernel

Create a Ring Kernel that echoes messages back with a sequence number.

### Exercise 2: Stateful Counter

Implement a Ring Kernel that maintains a running count across messages.

### Exercise 3: Pipeline

Create a two-stage pipeline with producer and consumer Ring Kernels.

## Key Takeaways

1. **Ring Kernels run persistently** - zero launch overhead after initial setup
2. **Actor model on GPU** - message-passing concurrency
3. **MemoryPack serialization** - efficient GPU-compatible format
4. **Multiple processing modes** - continuous, batch, or adaptive
5. **Full lifecycle control** - launch, activate, deactivate, terminate

## Next Module

[Synchronization Patterns →](synchronization-patterns.md)

Learn barrier synchronization and memory ordering for complex GPU coordination.

## Further Reading

- [Ring Kernels Guide](../../guides/ring-kernels/index.md) - Comprehensive reference
- [MemoryPack Format](../../guides/ring-kernels/memorypack-format.md) - Serialization details
- [Telemetry API](../../guides/ring-kernels/telemetry.md) - Monitoring and metrics
