# Multi-GPU Programming

This module covers scaling GPU applications across multiple devices with peer-to-peer transfers, load balancing, and coordination strategies.

## Multi-GPU Architecture

### System Topologies

```
Single Node, Multiple GPUs:
┌─────────────────────────────────────┐
│              CPU Host               │
│                                     │
│  ┌─────┐    ┌─────┐    ┌─────┐     │
│  │GPU 0│◄──▶│GPU 1│◄──▶│GPU 2│     │
│  └─────┘    └─────┘    └─────┘     │
│       P2P / NVLink / PCIe           │
└─────────────────────────────────────┘
```

### Communication Methods

| Method | Bandwidth | Latency | Use Case |
|--------|-----------|---------|----------|
| Host staging | 12-32 GB/s | High | Fallback |
| PCIe P2P | 16-32 GB/s | Medium | Cross-root |
| NVLink | 300-900 GB/s | Low | Adjacent GPUs |

## Device Management

### Enumerating Devices

```csharp
var acceleratorFactory = provider.GetRequiredService<IUnifiedAcceleratorFactory>();

var devices = acceleratorFactory.GetAvailableDevices();

Console.WriteLine($"Found {devices.Count} GPU devices:");
foreach (var device in devices)
{
    Console.WriteLine($"  [{device.Id}] {device.Name}");
    Console.WriteLine($"      Memory: {device.TotalMemory / (1024*1024*1024.0):F1} GB");
    Console.WriteLine($"      Compute: {device.ComputeCapability}");
    Console.WriteLine($"      P2P Capable: {device.SupportsP2P}");
}
```

### Creating Multi-Device Context

```csharp
services.AddDotComputeRuntime();

// Multi-GPU support is configured through the orchestrator
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
var acceleratorFactory = provider.GetRequiredService<IUnifiedAcceleratorFactory>();

// Get available devices for multi-GPU operations
var devices = acceleratorFactory.GetAvailableDevices();
```

## Peer-to-Peer Transfers

### Enabling P2P

```csharp
// Check P2P availability
bool canP2P = await multiGpuService.CanAccessPeerAsync(deviceA: 0, deviceB: 1);

if (canP2P)
{
    // Enable bidirectional P2P access
    await multiGpuService.EnablePeerAccessAsync(0, 1);
    await multiGpuService.EnablePeerAccessAsync(1, 0);
}
```

### Direct P2P Transfer

```csharp
// Create buffers on different GPUs
using var bufferGpu0 = multiGpuService.CreateBuffer<float>(size, deviceId: 0);
using var bufferGpu1 = multiGpuService.CreateBuffer<float>(size, deviceId: 1);

// Copy from host to GPU 0
await bufferGpu0.CopyFromAsync(hostData);

// Direct GPU-to-GPU transfer (no host staging)
await multiGpuService.CopyPeerAsync(
    source: bufferGpu0,
    destination: bufferGpu1);

// Now bufferGpu1 has the data
```

### Benchmark P2P vs Host Staging

```csharp
// P2P transfer (fast)
var sw = Stopwatch.StartNew();
await multiGpuService.CopyPeerAsync(bufferGpu0, bufferGpu1);
Console.WriteLine($"P2P: {sw.ElapsedMilliseconds} ms");

// Host staging (slow)
sw.Restart();
await bufferGpu0.CopyToAsync(hostTemp);
await bufferGpu1.CopyFromAsync(hostTemp);
Console.WriteLine($"Host staged: {sw.ElapsedMilliseconds} ms");
```

## Data Partitioning Strategies

### Strategy 1: Block Distribution

Divide data into equal chunks per GPU:

```csharp
public async Task ProcessBlockDistributed(float[] data)
{
    int deviceCount = multiGpuService.DeviceCount;
    int chunkSize = (data.Length + deviceCount - 1) / deviceCount;

    var tasks = new List<Task>();
    var buffers = new List<IBuffer<float>>();

    for (int i = 0; i < deviceCount; i++)
    {
        int start = i * chunkSize;
        int length = Math.Min(chunkSize, data.Length - start);

        if (length <= 0) break;

        // Allocate on each GPU
        var buffer = multiGpuService.CreateBuffer<float>(length, deviceId: i);
        buffers.Add(buffer);

        // Copy chunk to GPU
        await buffer.CopyFromAsync(data.AsSpan(start, length));

        // Launch kernel on this GPU
        var task = multiGpuService.ExecuteOnDeviceAsync(i, async () =>
        {
            await orchestrator.ExecuteKernelAsync(kernel, config, buffer);
        });
        tasks.Add(task);
    }

    // Wait for all GPUs
    await Task.WhenAll(tasks);

    // Gather results
    for (int i = 0; i < buffers.Count; i++)
    {
        int start = i * chunkSize;
        int length = buffers[i].Length;
        await buffers[i].CopyToAsync(data.AsSpan(start, length));
        buffers[i].Dispose();
    }
}
```

### Strategy 2: Cyclic Distribution

Interleave elements across GPUs:

```csharp
public async Task ProcessCyclicDistributed(float[] data)
{
    int deviceCount = multiGpuService.DeviceCount;
    var deviceData = new List<float>[deviceCount];

    // Partition data cyclically
    for (int i = 0; i < deviceCount; i++)
    {
        deviceData[i] = new List<float>();
    }

    for (int i = 0; i < data.Length; i++)
    {
        deviceData[i % deviceCount].Add(data[i]);
    }

    // Process on each GPU
    var tasks = new Task[deviceCount];
    var buffers = new IBuffer<float>[deviceCount];

    for (int i = 0; i < deviceCount; i++)
    {
        var chunk = deviceData[i].ToArray();
        buffers[i] = multiGpuService.CreateBuffer<float>(chunk.Length, deviceId: i);
        await buffers[i].CopyFromAsync(chunk);

        int deviceId = i;
        tasks[i] = multiGpuService.ExecuteOnDeviceAsync(deviceId, async () =>
        {
            await orchestrator.ExecuteKernelAsync(kernel, config, buffers[deviceId]);
        });
    }

    await Task.WhenAll(tasks);

    // Reconstruct results
    for (int i = 0; i < deviceCount; i++)
    {
        var results = new float[buffers[i].Length];
        await buffers[i].CopyToAsync(results);

        for (int j = 0; j < results.Length; j++)
        {
            data[j * deviceCount + i] = results[j];
        }
        buffers[i].Dispose();
    }
}
```

## Multi-GPU Ring Kernels

### Distributed Ring Kernel Pipeline

```csharp
// Create Ring Kernels on different GPUs
var producerKernel = await ringKernelService.LaunchAsync<Input, Intermediate>(
    ProducerKernel,
    new RingKernelLaunchOptions { DeviceId = 0 });

var consumerKernel = await ringKernelService.LaunchAsync<Intermediate, Output>(
    ConsumerKernel,
    new RingKernelLaunchOptions { DeviceId = 1 });

// Connect via P2P queue
await ringKernelService.ConnectP2PAsync(
    source: producerKernel,
    destination: consumerKernel);

// Messages flow: GPU 0 → GPU 1 via P2P
await producerKernel.SendAsync(input);
var result = await consumerKernel.ReceiveAsync();
```

## Load Balancing

### Static Load Balancing

```csharp
services.AddDotComputeRuntime();

// Static load balancing is configured per-operation
// by manually distributing work based on device capabilities
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
```

### Dynamic Load Balancing

```csharp
services.AddDotComputeRuntime();
services.AddPerformanceMonitoring();

// Dynamic load balancing can be implemented using work queues
// and monitoring device utilization through performance metrics
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
```

### Work Stealing

```csharp
var workQueue = new ConcurrentQueue<WorkItem>(allWorkItems);

var tasks = Enumerable.Range(0, deviceCount).Select(deviceId =>
    Task.Run(async () =>
    {
        while (workQueue.TryDequeue(out var work))
        {
            await multiGpuService.ExecuteOnDeviceAsync(deviceId, async () =>
            {
                await ProcessWorkItem(work);
            });
        }
    })
).ToArray();

await Task.WhenAll(tasks);
```

## Cross-GPU Synchronization

### Barrier Across GPUs

```csharp
// Create multi-GPU barrier
var barrier = await multiGpuService.CreateCrossDeviceBarrierAsync(
    deviceIds: new[] { 0, 1, 2 },
    participantsPerDevice: 1);

// Each GPU waits at barrier
var tasks = new[]
{
    ExecuteWithBarrier(0, barrier),
    ExecuteWithBarrier(1, barrier),
    ExecuteWithBarrier(2, barrier)
};

await Task.WhenAll(tasks);

async Task ExecuteWithBarrier(int deviceId, ICrossDeviceBarrier barrier)
{
    await multiGpuService.ExecuteOnDeviceAsync(deviceId, async () =>
    {
        // Phase 1
        await orchestrator.ExecuteKernelAsync(phase1Kernel, config, buffer);

        // Wait for all GPUs
        await barrier.WaitAsync(deviceId);

        // Phase 2
        await orchestrator.ExecuteKernelAsync(phase2Kernel, config, buffer);
    });
}
```

## Multi-GPU Reduction

```csharp
public async Task<float> ReduceAcrossGpus(float[] data)
{
    int deviceCount = multiGpuService.DeviceCount;
    int chunkSize = (data.Length + deviceCount - 1) / deviceCount;

    // Per-GPU partial results
    var partialSums = new float[deviceCount];
    var tasks = new Task[deviceCount];

    for (int i = 0; i < deviceCount; i++)
    {
        int start = i * chunkSize;
        int length = Math.Min(chunkSize, data.Length - start);
        int deviceId = i;

        tasks[i] = Task.Run(async () =>
        {
            using var buffer = multiGpuService.CreateBuffer<float>(length, deviceId);
            using var result = multiGpuService.CreateBuffer<float>(1, deviceId);

            await buffer.CopyFromAsync(data.AsSpan(start, length));

            await multiGpuService.ExecuteOnDeviceAsync(deviceId, async () =>
            {
                await orchestrator.ExecuteKernelAsync(reduceKernel, config, buffer, result);
            });

            var partialResult = new float[1];
            await result.CopyToAsync(partialResult);
            partialSums[deviceId] = partialResult[0];
        });
    }

    await Task.WhenAll(tasks);

    // Final reduction on CPU
    return partialSums.Sum();
}
```

## Performance Monitoring

```csharp
// Get multi-GPU metrics
var metrics = await multiGpuService.GetMetricsAsync();

foreach (var device in metrics.Devices)
{
    Console.WriteLine($"GPU {device.Id}:");
    Console.WriteLine($"  Utilization: {device.Utilization:P0}");
    Console.WriteLine($"  Memory used: {device.MemoryUsed / (1024*1024)} MB");
    Console.WriteLine($"  P2P transfers: {device.P2PTransferCount}");
    Console.WriteLine($"  P2P bandwidth: {device.P2PBandwidthGBps:F1} GB/s");
}

Console.WriteLine($"Total P2P transfers: {metrics.TotalP2PTransfers}");
Console.WriteLine($"Load imbalance: {metrics.LoadImbalance:P1}");
```

## Exercises

### Exercise 1: Multi-GPU Vector Add

Implement vector addition distributed across multiple GPUs.

### Exercise 2: P2P Benchmark

Measure P2P bandwidth between all GPU pairs in your system.

### Exercise 3: Dynamic Balancing

Implement work stealing for variable-length tasks.

## Key Takeaways

1. **P2P enables direct GPU-to-GPU transfers** without host staging
2. **Data partitioning strategy** affects performance significantly
3. **Load balancing** is essential for heterogeneous systems
4. **Cross-GPU synchronization** has higher latency than intra-GPU
5. **Profile P2P topology** to optimize communication patterns

## Next Module

[Performance Profiling →](performance-profiling.md)

Learn to profile and optimize GPU execution with timing APIs.

## Further Reading

- [Multi-GPU Guide](../../guides/multi-gpu.md) - Complete reference
- [P2P Memory Transfers](../../architecture/memory-management.md) - Technical details
