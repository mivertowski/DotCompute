# Unified Ring Kernel System - Migration Guide

## Overview

This guide helps the Orleans.GpuBridge team migrate from the legacy Ring Kernel system to the new **Unified Ring Kernel System**. The unified system consolidates all kernel logic into a single location, eliminating the need for separate handler classes.

### Key Benefits
- **Single Source of Truth**: All kernel logic in ONE place (no separate handler files)
- **Automatic C# to CUDA Translation**: Write handlers in C#, get CUDA automatically
- **K2K Messaging**: Built-in kernel-to-kernel actor-style messaging
- **Pub/Sub Support**: Topic-based message routing
- **Temporal APIs**: GPU timestamp tracking for actor systems

---

## Breaking Changes Summary

| Component | Old API | New API |
|-----------|---------|---------|
| Control Structure | Multiple `RingBuffer*` parameters | Single `RingKernelControlBlock*` |
| Termination Flag | `int* terminate_flag` | `control_block->should_terminate` |
| Message Counter | Separate parameter | `control_block->messages_processed` |
| Handler Definition | Separate `.cu` file | Auto-generated or inline |
| K2K Messaging | Manual implementation | Built-in `SendToKernel()` / `TryReceiveFromKernel()` |

---

## Migration Steps

### Step 1: Update RingKernel Attribute

> **Important**: Ring Kernels **MUST return void**. This is enforced by the analyzer.
> Output is sent via `ctx.EnqueueOutput()` or K2K messaging, not return values.

**Old API:**
```csharp
[RingKernel(
    KernelId = "my_kernel",
    Capacity = 1024,
    InputQueueSize = 256,
    OutputQueueSize = 256)]
public static void MyKernel(Span<byte> input, Span<byte> output)
{
    // Handler logic was in separate file
}
```

**New API (Unified):**
```csharp
[RingKernel(
    KernelId = "my_kernel",
    Capacity = 1024,
    InputQueueSize = 256,
    OutputQueueSize = 256,
    MaxInputMessageSizeBytes = 65792,
    MaxOutputMessageSizeBytes = 65792,
    ProcessingMode = RingProcessingMode.Continuous,
    EnableTimestamps = true)]  // NEW: Orleans.GpuBridge.Core integration
public static void MyKernel(RingKernelContext ctx, MyInputMessage input)
{
    // Handler logic is NOW HERE - auto-translated to CUDA
    var result = ProcessMessage(input);
    ctx.EnqueueOutput(result);
}
```

### Step 2: Use RingKernelContext for GPU Operations

The `RingKernelContext` provides a C#-like API that gets translated to CUDA intrinsics:

```csharp
[RingKernel(KernelId = "processor", EnableTimestamps = true)]
public static void ProcessorKernel(RingKernelContext ctx, DataMessage input)
{
    // Barrier synchronization (translates to __syncthreads())
    ctx.SyncThreads();

    // Get GPU timestamp (translates to clock64())
    long timestamp = ctx.Now();

    // Atomic operations (translates to atomicAdd())
    ctx.AtomicAdd(ref counter, 1);

    // K2K messaging - send to another kernel
    ctx.SendToKernel("aggregator_kernel", new AggregateMessage { Value = result });

    // Pub/Sub - publish to topic
    ctx.PublishToTopic("results", new ResultMessage { Data = output });
}
```

### Step 3: Update CUDA Code Generation Expectations

**Old Generated CUDA Structure:**
```cuda
extern "C" __global__ void my_kernel(
    RingBuffer* input_ring,
    RingBuffer* output_ring,
    int* terminate_flag,
    int* message_count)
{
    while (!*terminate_flag) {
        // Manual message processing
    }
}
```

**New Generated CUDA Structure:**
```cuda
// Auto-generated handler function
__device__ bool process_my_kernel_message(
    const unsigned char* msg_buffer,
    int msg_size,
    unsigned char* output_buffer,
    int* output_size_ptr,
    RingKernelControlBlock* control_block)
{
    // Auto-translated from C# or default stub
    // Validates input, processes message, sets output
    return true;
}

extern "C" __global__ void my_kernel_kernel(
    RingKernelControlBlock* control_block)
{
    // Unified control block contains all state
    while (!control_block->should_terminate) {
        // Auto-generated message processing loop
        bool success = process_my_kernel_message(...);
        if (success) {
            atomicAdd(&control_block->messages_processed, 1ULL);
        }
    }
}
```

### Step 4: Migrate K2K (Kernel-to-Kernel) Messaging

**Old Approach (Manual):**
```csharp
// Required manual buffer management and synchronization
// across multiple kernel launches
```

**New Approach (Built-in):**
```csharp
[RingKernel(
    KernelId = "producer",
    PublishesToKernels = new[] { "consumer" })]
public static void ProducerKernel(RingKernelContext ctx, InputMessage msg)
{
    var result = Process(msg);
    ctx.SendToKernel("consumer", result);
}

[RingKernel(
    KernelId = "consumer",
    SubscribesToKernels = new[] { "producer" })]
public static void ConsumerKernel(RingKernelContext ctx, ProcessedMessage msg)
{
    // Automatically receives from producer
    var data = ctx.TryReceiveFromKernel<ProcessedMessage>("producer");
    if (data.HasValue) {
        // Process received message
    }
}
```

### Step 5: Update Orleans.GpuBridge.Core Integration

The new system supports Orleans-specific features directly in the attribute:

```csharp
[RingKernel(
    KernelId = "orleans_actor",

    // Orleans.GpuBridge.Core Properties
    EnableTimestamps = true,           // GPU clock64() for temporal consistency
    ProcessingMode = RingProcessingMode.Continuous,  // or Batch, Adaptive
    MaxMessagesPerIteration = 100,     // Batch size limit
    MessageQueueSize = 512,            // Unified queue size

    // Memory consistency for actor systems
    MemoryConsistency = "ReleaseAcquire",
    EnableCausalOrdering = true,

    // Barriers for synchronization
    UseBarriers = true,
    BarrierScope = "ThreadBlock")]
public static void OrleansActorKernel(RingKernelContext ctx, ActorMessage msg)
{
    // GPU timestamp for temporal ordering
    long now = ctx.Now();

    // Process with causal ordering guarantees
    ctx.ThreadFence();  // Memory fence

    // ... actor logic ...
}
```

---

## RingKernelControlBlock Structure

The new unified control block replaces multiple parameters:

```cuda
struct RingKernelControlBlock {
    // Lifecycle control
    int is_active;              // Atomic: 1 = active, 0 = inactive
    int should_terminate;       // Atomic: 1 = terminate, 0 = continue
    int has_terminated;         // Atomic: 1 = terminated, 0 = running
    int errors_encountered;     // Atomic error counter

    // Performance metrics
    long long messages_processed;    // Atomic message counter
    long long last_activity_ticks;   // Atomic timestamp

    // Queue pointers (device memory)
    long long input_queue_head_ptr;
    long long input_queue_tail_ptr;
    long long output_queue_head_ptr;
    long long output_queue_tail_ptr;

    // K2K messaging (if enabled)
    long long k2k_send_queue_ptr;
    long long k2k_receive_channels_ptr;
    int k2k_channel_count;

    // Temporal (if EnableTimestamps = true)
    long long hlc_physical;
    long long hlc_logical;
};
```

---

## RingKernelContext API Reference

### Synchronization

| Method | CUDA Translation | Description |
|--------|-----------------|-------------|
| `ctx.SyncThreads()` | `__syncthreads()` | Block-level barrier |
| `ctx.SyncGrid()` | `cooperative_groups::grid_group::sync()` | Grid-level barrier |
| `ctx.SyncWarp()` | `__syncwarp()` | Warp-level barrier |
| `ctx.NamedBarrier("name")` | Named barrier | Custom synchronization point |
| `ctx.ThreadFence()` | `__threadfence()` | Memory fence |

### Atomic Operations

| Method | CUDA Translation |
|--------|-----------------|
| `ctx.AtomicAdd(ref x, val)` | `atomicAdd(&x, val)` |
| `ctx.AtomicCAS(ref x, cmp, val)` | `atomicCAS(&x, cmp, val)` |
| `ctx.AtomicExch(ref x, val)` | `atomicExch(&x, val)` |
| `ctx.AtomicMin(ref x, val)` | `atomicMin(&x, val)` |
| `ctx.AtomicMax(ref x, val)` | `atomicMax(&x, val)` |

### Warp Primitives

| Method | CUDA Translation |
|--------|-----------------|
| `ctx.WarpShuffle(val, lane)` | `__shfl_sync(mask, val, lane)` |
| `ctx.WarpShuffleDown(val, delta)` | `__shfl_down_sync(mask, val, delta)` |
| `ctx.WarpReduce(val)` | Warp reduction pattern |
| `ctx.WarpBallot(pred)` | `__ballot_sync(mask, pred)` |
| `ctx.WarpAll(pred)` | `__all_sync(mask, pred)` |
| `ctx.WarpAny(pred)` | `__any_sync(mask, pred)` |

### Temporal APIs (Orleans.GpuBridge.Core)

| Method | Description |
|--------|-------------|
| `ctx.Now()` | Returns GPU clock64() timestamp |
| `ctx.Tick()` | Increments HLC logical counter |
| `ctx.UpdateClock(remote)` | Updates HLC from remote timestamp |

### K2K Messaging

| Method | Description |
|--------|-------------|
| `ctx.SendToKernel(kernelId, msg)` | Send message to another kernel |
| `ctx.TryReceiveFromKernel<T>(kernelId)` | Try to receive from kernel |
| `ctx.GetPendingMessageCount(kernelId)` | Get pending message count |

### Pub/Sub

| Method | Description |
|--------|-------------|
| `ctx.PublishToTopic(topic, msg)` | Publish to topic |
| `ctx.TryReceiveFromTopic<T>(topic)` | Try to receive from topic |

---

## Testing Migration

### Update Test Assertions

**Old Test Pattern:**
```csharp
// Old: Check for individual ring buffer parameters
Assert.Contains("RingBuffer* input_ring", generatedCode);
Assert.Contains("int* terminate_flag", generatedCode);
```

**New Test Pattern:**
```csharp
// New: Check for unified control block
Assert.Contains("RingKernelControlBlock* control_block", generatedCode);
Assert.Contains("control_block->should_terminate", generatedCode);
Assert.Contains("control_block->messages_processed", generatedCode);

// Check handler function is generated
Assert.Contains("__device__ bool process_", generatedCode);
Assert.Contains("_message(", generatedCode);
```

### Test K2K Infrastructure

```csharp
[Fact]
public void StubGenerator_WithK2KMessaging_GeneratesInfrastructure()
{
    var kernel = CreateKernelWithK2K();
    var generator = new CudaRingKernelStubGenerator(logger);

    var code = generator.GenerateKernelStub(kernel);

    Assert.Contains("k2k_send_queue", code);
    Assert.Contains("k2k_receive_channels", code);
    Assert.Contains("K2KMessageHeader", code);
}
```

---

## Backward Compatibility

**Note**: This migration is **NOT backward compatible**. The old API has been removed entirely as per project requirements.

If you have existing `.cu` handler files:
1. Convert the handler logic to C# using `RingKernelContext`
2. Place the logic inside the `[RingKernel]` attributed method
3. Remove the separate handler files
4. The system will auto-generate equivalent CUDA code

---

## Common Migration Issues

### Issue 1: Handler Function Not Found

**Error:**
```
error: identifier "process_my_kernel_message" is undefined
```

**Solution:** Ensure the kernel method name follows the convention. The handler name is derived from the method name:
- Method: `MyKernel` → Handler: `process_my_kernel_message`
- Method: `ProcessDataKernel` → Handler: `process_process_data_message`

### Issue 2: Parameter Type Mismatch

**Error:**
```
error: argument of type "int" is incompatible with parameter of type "int *"
```

**Solution:** The handler function signature is fixed. Ensure your code doesn't manually generate handler calls with incorrect parameters.

### Issue 3: Missing Control Block Members

**Error:**
```
error: 'RingKernelControlBlock' has no member named 'old_field'
```

**Solution:** Use the new control block member names:
- `terminate_flag` → `control_block->should_terminate`
- `message_count` → `control_block->messages_processed`

---

## Support

- **Documentation**: `/docs/articles/guides/ring-kernels-advanced.md`
- **Tests**: `/tests/Unit/DotCompute.Backends.CUDA.Tests/Compilation/`
- **Issues**: https://github.com/mivertowski/DotCompute/issues

---

**Last Updated:** November 2025
**Applies To:** DotCompute v0.4.2-rc2+
**Authors:** Michael Ivertowski
