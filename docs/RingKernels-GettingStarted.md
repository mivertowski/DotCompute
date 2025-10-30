# Ring Kernels - Getting Started Guide

## Overview

Ring Kernels are persistent GPU kernels that run continuously and communicate through message passing. Unlike standard kernels that execute once and return, Ring Kernels maintain state across invocations and can coordinate with other Ring Kernels through lock-free message queues.

### Key Features

- **Persistent Execution**: Kernels stay resident on GPU, eliminating launch overhead
- **Message Passing**: Lock-free queues for inter-kernel communication
- **GPU-Native Actors**: Build actor systems entirely on GPU
- **Domain Optimizations**: Specialized patterns for graph analytics, simulations, and more

### Use Cases

- **Graph Analytics**: PageRank, BFS, community detection with vertex-centric patterns
- **Spatial Simulations**: Wave propagation, fluid dynamics, heat diffusion
- **Actor Systems**: GPU-native actors for particle systems, agent simulations
- **Streaming Pipelines**: Continuous data processing with minimal CPU involvement

---

## Quick Start: Simple Message Passing

### Step 1: Define Ring Kernel Message Types

```csharp
using DotCompute.Abstractions.RingKernels;

// Simple message payload
public struct WorkItem
{
    public int Id;
    public float Value;
}

public struct ResultItem
{
    public int Id;
    public float ProcessedValue;
}
```

### Step 2: Create Producer Ring Kernel

```csharp
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;
using Kernel = DotCompute.Generators.Kernel.Kernel;

[RingKernel(
    KernelId = "producer",
    Mode = RingKernelMode.Persistent,
    Capacity = 1024,
    OutputQueueSize = 256)]
public static void ProducerKernel(
    IMessageQueue<WorkItem> output,
    ReadOnlySpan<float> sourceData)
{
    int tid = Kernel.ThreadId.X;
    int idx = Kernel.BlockId.X * Kernel.BlockSize.X + tid;

    if (idx < sourceData.Length)
    {
        // Create work item from source data
        var workItem = new WorkItem
        {
            Id = idx,
            Value = sourceData[idx]
        };

        // Send to output queue (non-blocking)
        var message = KernelMessage<WorkItem>.CreateData(
            senderId: 0, // Producer ID
            receiverId: 1, // Consumer ID
            payload: workItem
        );

        output.TryEnqueueAsync(message);
    }
}
```

### Step 3: Create Consumer Ring Kernel

```csharp
[RingKernel(
    KernelId = "consumer",
    Mode = RingKernelMode.Persistent,
    Capacity = 1024,
    InputQueueSize = 256,
    OutputQueueSize = 256)]
public static void ConsumerKernel(
    IMessageQueue<WorkItem> input,
    IMessageQueue<ResultItem> output)
{
    int tid = Kernel.ThreadId.X;

    // Continuously process messages
    while (true)
    {
        // Try to dequeue work item (non-blocking)
        var message = input.TryDequeueAsync();

        if (message != null)
        {
            // Check for termination
            if (message.Value.Type == MessageType.Terminate)
                break;

            // Process work item
            var work = message.Value.Payload;
            var result = new ResultItem
            {
                Id = work.Id,
                ProcessedValue = work.Value * 2.0f // Example processing
            };

            // Send result
            var resultMsg = KernelMessage<ResultItem>.CreateData(
                senderId: 1, // Consumer ID
                receiverId: -1, // Broadcast
                payload: result
            );

            output.TryEnqueueAsync(resultMsg);
        }

        // Yield if no work available
        if (message == null)
            Kernel.ThreadYield();
    }
}
```

### Step 4: Launch and Manage Ring Kernels

```csharp
using DotCompute.Abstractions.RingKernels;

// Get ring kernel runtime from dependency injection
var runtime = serviceProvider.GetRequiredService<IRingKernelRuntime>();

// Create message queues
var workQueue = await runtime.CreateMessageQueueAsync<WorkItem>(capacity: 256);
var resultQueue = await runtime.CreateMessageQueueAsync<ResultItem>(capacity: 256);

// Launch producer kernel
await runtime.LaunchAsync(
    kernelId: "producer",
    gridSize: 32,   // 32 blocks
    blockSize: 256  // 256 threads per block
);

// Launch consumer kernel
await runtime.LaunchAsync(
    kernelId: "consumer",
    gridSize: 8,    // 8 blocks
    blockSize: 256  // 256 threads per block
);

// Activate both kernels
await runtime.ActivateAsync("producer");
await runtime.ActivateAsync("consumer");

// Monitor progress
var status = await runtime.GetStatusAsync("consumer");
Console.WriteLine($"Consumer processed {status.MessagesProcessed} messages");

// Collect results
while (true)
{
    var result = await runtime.ReceiveMessageAsync<ResultItem>(
        kernelId: "consumer",
        timeout: TimeSpan.FromSeconds(1)
    );

    if (result == null) break; // Timeout

    Console.WriteLine($"Result {result.Value.Payload.Id}: {result.Value.Payload.ProcessedValue}");
}

// Cleanup
await runtime.TerminateAsync("producer");
await runtime.TerminateAsync("consumer");
await runtime.DisposeAsync();
```

---

## Domain-Specific Examples

### Graph Analytics: PageRank

```csharp
using DotCompute.Abstractions.RingKernels;
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Generators.Kernel.Enums;

// Message type for rank contributions
public struct RankMessage
{
    public int TargetVertex;
    public float RankContribution;
}

[RingKernel(
    KernelId = "pagerank-vertex",
    Domain = RingKernelDomain.GraphAnalytics,
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.SharedMemory,
    Capacity = 10000,
    InputQueueSize = 512,
    OutputQueueSize = 512)]
public static void PageRankVertex(
    IMessageQueue<RankMessage> incoming,
    IMessageQueue<RankMessage> outgoing,
    Span<float> pageRank,
    ReadOnlySpan<int> neighbors, // Adjacency list: [offset, count, neighbor1, neighbor2, ...]
    int numVertices,
    float dampingFactor = 0.85f)
{
    int vertexId = Kernel.ThreadId.X;

    if (vertexId >= numVertices) return;

    const int maxIterations = 100;
    for (int iter = 0; iter < maxIterations; iter++)
    {
        // Accumulate incoming rank contributions
        float incomingRank = 0.0f;
        int messageCount = 0;

        while (true)
        {
            var msg = incoming.TryDequeueAsync();
            if (msg == null) break;

            if (msg.Value.Payload.TargetVertex == vertexId)
            {
                incomingRank += msg.Value.Payload.RankContribution;
                messageCount++;
            }
        }

        // Update PageRank: PR(v) = (1-d)/N + d * sum(PR(u)/outdegree(u))
        float newRank = (1.0f - dampingFactor) / numVertices + dampingFactor * incomingRank;
        float delta = Math.Abs(newRank - pageRank[vertexId]);
        pageRank[vertexId] = newRank;

        // Send rank to neighbors
        int neighborOffset = neighbors[vertexId * 2];
        int neighborCount = neighbors[vertexId * 2 + 1];

        if (neighborCount > 0)
        {
            float rankPerNeighbor = newRank / neighborCount;

            for (int i = 0; i < neighborCount; i++)
            {
                int neighborId = neighbors[neighborOffset + i];

                var outMsg = KernelMessage<RankMessage>.CreateData(
                    senderId: vertexId,
                    receiverId: neighborId,
                    payload: new RankMessage
                    {
                        TargetVertex = neighborId,
                        RankContribution = rankPerNeighbor
                    }
                );

                outgoing.TryEnqueueAsync(outMsg);
            }
        }

        // Synchronize iteration across all vertices
        Kernel.ThreadBarrier();

        // Convergence check (simplified)
        if (delta < 1e-6f)
            break;
    }
}
```

### Spatial Simulation: 2D Wave Propagation

```csharp
// Halo data for boundary exchange
public struct HaloData
{
    public int Direction; // 0=left, 1=right, 2=top, 3=bottom
    public float Value;
}

[RingKernel(
    KernelId = "wave-propagation-2d",
    Domain = RingKernelDomain.SpatialSimulation,
    Mode = RingKernelMode.Persistent,
    MessagingStrategy = MessagePassingStrategy.SharedMemory,
    UseSharedMemory = true,
    SharedMemorySize = 4096)]
public static void WavePropagation2D(
    IMessageQueue<HaloData> haloIn,
    IMessageQueue<HaloData> haloOut,
    Span<float> pressure,
    Span<float> velocity,
    int width,
    int height,
    float c = 1.0f, // Wave speed
    float dt = 0.01f) // Time step
{
    int x = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockSize.X;
    int y = Kernel.ThreadId.Y + Kernel.BlockId.Y * Kernel.BlockSize.Y;

    if (x >= width || y >= height) return;

    int idx = y * width + x;

    const int maxSteps = 10000;
    for (int step = 0; step < maxSteps; step++)
    {
        // Receive halo data from neighboring blocks
        float left = (x == 0) ? 0.0f : pressure[idx - 1];
        float right = (x == width - 1) ? 0.0f : pressure[idx + 1];
        float top = (y == 0) ? 0.0f : pressure[idx - width];
        float bottom = (y == height - 1) ? 0.0f : pressure[idx + width];

        // Check for boundary halo messages
        while (true)
        {
            var msg = haloIn.TryDequeueAsync();
            if (msg == null) break;

            switch (msg.Value.Payload.Direction)
            {
                case 0: left = msg.Value.Payload.Value; break;
                case 1: right = msg.Value.Payload.Value; break;
                case 2: top = msg.Value.Payload.Value; break;
                case 3: bottom = msg.Value.Payload.Value; break;
            }
        }

        // 5-point stencil Laplacian
        float laplacian = (left + right + top + bottom - 4.0f * pressure[idx]);

        // Wave equation: ∂²p/∂t² = c² ∇²p
        float acceleration = c * c * laplacian;
        velocity[idx] += acceleration * dt;
        pressure[idx] += velocity[idx] * dt;

        // Send boundary values to neighbors
        if (x == 0)
        {
            haloOut.TryEnqueueAsync(KernelMessage<HaloData>.CreateData(
                vertexId, -1, new HaloData { Direction = 0, Value = pressure[idx] }
            ));
        }
        if (x == width - 1)
        {
            haloOut.TryEnqueueAsync(KernelMessage<HaloData>.CreateData(
                vertexId, -1, new HaloData { Direction = 1, Value = pressure[idx] }
            ));
        }
        // Similar for top/bottom boundaries...

        // Synchronize timestep
        Kernel.ThreadBarrier();
    }
}
```

### Actor Model: GPU Particle System

```csharp
// Actor message for particle interactions
public struct ParticleMessage
{
    public int ParticleId;
    public float PositionX;
    public float PositionY;
    public float Force;
}

[RingKernel(
    KernelId = "particle-actor",
    Domain = RingKernelDomain.ActorModel,
    Mode = RingKernelMode.EventDriven,
    MessagingStrategy = MessagePassingStrategy.AtomicQueue,
    Capacity = 2048)]
public static void ParticleActor(
    IMessageQueue<ParticleMessage> mailbox,
    IMessageQueue<ParticleMessage> outbox,
    Span<float> positionX,
    Span<float> positionY,
    Span<float> velocityX,
    Span<float> velocityY,
    int numParticles)
{
    int particleId = Kernel.ThreadId.X;

    if (particleId >= numParticles) return;

    // Process messages in mailbox
    while (true)
    {
        var msg = mailbox.TryDequeueAsync();
        if (msg == null) break;

        if (msg.Value.Type == MessageType.Terminate)
            return;

        var payload = msg.Value.Payload;

        // Calculate force from neighboring particle
        float dx = payload.PositionX - positionX[particleId];
        float dy = payload.PositionY - positionY[particleId];
        float distSq = dx * dx + dy * dy + 1e-6f; // Avoid division by zero

        // Simple repulsion force
        float force = -payload.Force / distSq;
        velocityX[particleId] += force * dx * 0.01f;
        velocityY[particleId] += force * dy * 0.01f;
    }

    // Update position
    positionX[particleId] += velocityX[particleId] * 0.01f;
    positionY[particleId] += velocityY[particleId] * 0.01f;

    // Broadcast position to neighbors
    var broadcastMsg = KernelMessage<ParticleMessage>.CreateData(
        senderId: particleId,
        receiverId: -1, // Broadcast
        payload: new ParticleMessage
        {
            ParticleId = particleId,
            PositionX = positionX[particleId],
            PositionY = positionY[particleId],
            Force = 1.0f // Repulsion strength
        }
    );

    outbox.TryEnqueueAsync(broadcastMsg);
}
```

---

## Best Practices

### 1. **Queue Sizing**
- Input/output queues should be powers of 2 (256, 512, 1024)
- Size based on message arrival rate × processing time
- Monitor queue utilization with `GetStatisticsAsync()`

### 2. **Message Design**
- Keep messages small (< 128 bytes)
- Use unmanaged types only (no managed references)
- Consider indirect references (buffer indices) for large payloads

### 3. **Deadlock Avoidance**
- Always use non-blocking `TryEnqueue`/`TryDequeue`
- Design acyclic message flow graphs when possible
- Implement timeout mechanisms for blocking operations

### 4. **Performance Optimization**
- Use `SharedMemory` strategy for single-GPU (default)
- Use `P2P` for multi-GPU with NVLink
- Profile with `GetMetricsAsync()` to identify bottlenecks
- Adjust grid/block sizes based on GPU occupancy

### 5. **Domain Selection**
- Choose correct `RingKernelDomain` for automatic optimizations
- `GraphAnalytics`: Sparse irregular workloads
- `SpatialSimulation`: Regular grid stencil operations
- `ActorModel`: Message-driven discrete event simulation

---

## Advanced Topics

### Multi-GPU Deployment

```csharp
// Create runtime per GPU
var runtime0 = CreateRuntimeForGpu(gpuId: 0);
var runtime1 = CreateRuntimeForGpu(gpuId: 1);

// Launch kernels on different GPUs
await runtime0.LaunchAsync("producer", gridSize: 32, blockSize: 256);
await runtime1.LaunchAsync("consumer", gridSize: 32, blockSize: 256);

// Use P2P messaging strategy for cross-GPU communication
[RingKernel(
    KernelId = "cross-gpu-kernel",
    MessagingStrategy = MessagePassingStrategy.P2P,
    Backends = KernelBackends.CUDA)]
public static void CrossGpuKernel(...)
{
    // Direct GPU-to-GPU transfers without CPU involvement
}
```

### Profiling and Monitoring

```csharp
// Get detailed metrics
var metrics = await runtime.GetMetricsAsync("my-kernel");

Console.WriteLine($"Throughput: {metrics.ThroughputMsgsPerSec:F2} msgs/sec");
Console.WriteLine($"Avg Latency: {metrics.AvgProcessingTimeMs:F2} ms");
Console.WriteLine($"GPU Utilization: {metrics.GpuUtilizationPercent:F1}%");

// Monitor queue health
var queueStats = await messageQueue.GetStatisticsAsync();

if (queueStats.Utilization > 0.9)
{
    Console.WriteLine("Warning: Queue near capacity, consider increasing size");
}

if (queueStats.TotalDropped > 0)
{
    Console.WriteLine($"Dropped {queueStats.TotalDropped} messages due to full queue");
}
```

---

## Troubleshooting

### Issue: Kernel not processing messages
**Solution**: Ensure kernel is activated with `ActivateAsync()` after launch

### Issue: Messages getting dropped
**Solution**: Increase queue capacity or reduce message production rate

### Issue: Low GPU utilization
**Solution**: Increase grid/block size or reduce message processing complexity

### Issue: Deadlock between kernels
**Solution**: Use non-blocking operations and implement timeouts

---

## Next Steps

- **Week 2 Implementation**: CUDA backend with full persistent kernel support
- **Week 3 Implementation**: OpenCL and Metal backends
- **Week 4 Implementation**: Roslyn analyzers (DC013-DC020) for Ring Kernel validation

For more information, see:
- `docs/DotCompute-0.2.0-Plan.md` - Full implementation roadmap
- API Reference - Complete interface documentation
- Examples - Domain-specific example projects
