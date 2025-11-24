# Unified Ring Kernel System - Migration Guide

## Overview

This guide helps the Orleans.GpuBridge team migrate from the legacy Ring Kernel system to the new **Unified Ring Kernel System**. The unified system uses a single `RingKernelControlBlock*` parameter and auto-generates handler stubs.

### Current Status (v0.4.2-rc2)

**Implemented:**
- Single `RingKernelControlBlock*` parameter (replaces multiple parameters)
- Auto-generated handler function stubs
- K2K messaging infrastructure in generated CUDA code
- Pub/Sub topic routing infrastructure
- Temporal timestamp fields in control block
- `RingKernelContext` API for GPU operations in C#
- Inline handler detection (methods with RingKernelContext parameter)
- C# to CUDA transpiler for RingKernelContext calls

**In Progress:**
- Full automatic C# to CUDA transpilation for method bodies
- Source generator integration for compile-time transpilation

---

## Breaking Changes Summary

| Component | Old API | New API |
|-----------|---------|---------|
| Control Structure | Multiple `RingBuffer*` parameters | Single `RingKernelControlBlock*` |
| Termination Flag | `int* terminate_flag` | `control_block->should_terminate` |
| Message Counter | Separate parameter | `control_block->messages_processed` |
| Handler Definition | Separate `.cu` file | Auto-generated stub (customize in CUDA) |
| K2K Messaging | Manual implementation | Infrastructure generated automatically |

---

## Migration Steps

### Step 1: Update RingKernel Attribute

> **Important**: Ring Kernels **MUST return void**. This is enforced by the analyzer.
> The kernel method body should be empty - handler logic goes in the generated CUDA code.

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

**New API (Current):**
```csharp
[RingKernel(
    KernelId = "my_kernel",
    Capacity = 1024,
    InputQueueSize = 256,
    OutputQueueSize = 256,
    MaxInputMessageSizeBytes = 65792,
    MaxOutputMessageSizeBytes = 65792,
    ProcessingMode = RingProcessingMode.Continuous,
    EnableTimestamps = true)]
public static void MyKernel()
{
    // Empty - handler logic is in generated CUDA stub
    // Customize the generated process_my_message() function in CUDA
}
```

### Step 2: Customize Generated CUDA Handler

The stub generator creates a handler function that you can customize:

```cuda
// Auto-generated handler function - customize this
__device__ bool process_my_message(
    const unsigned char* msg_buffer,
    int msg_size,
    unsigned char* output_buffer,
    int* output_size_ptr,
    RingKernelControlBlock* control_block)
{
    // TODO: Add your message processing logic here
    // Default stub just validates and echoes

    if (msg_size <= 0) return false;

    // Copy input to output (echo behavior)
    for (int i = 0; i < msg_size; i++) {
        output_buffer[i] = msg_buffer[i];
    }
    *output_size_ptr = msg_size;

    return true;
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
__device__ bool process_my_message(
    const unsigned char* msg_buffer,
    int msg_size,
    unsigned char* output_buffer,
    int* output_size_ptr,
    RingKernelControlBlock* control_block)
{
    // Stub implementation - customize as needed
    return true;
}

extern "C" __global__ void my_kernel_kernel(
    RingKernelControlBlock* control_block)
{
    // Unified control block contains all state
    while (!control_block->should_terminate) {
        // Auto-generated message processing loop
        bool success = process_my_message(...);
        if (success) {
            atomicAdd(&control_block->messages_processed, 1ULL);
        }
    }
}
```

### Step 4: Configure K2K (Kernel-to-Kernel) Messaging

**Attribute Configuration:**
```csharp
[RingKernel(
    KernelId = "producer",
    PublishesToKernels = new[] { "consumer" })]
public static void ProducerKernel()
{
    // Empty - K2K infrastructure generated in CUDA
}

[RingKernel(
    KernelId = "consumer",
    SubscribesToKernels = new[] { "producer" })]
public static void ConsumerKernel()
{
    // Empty - K2K infrastructure generated in CUDA
}
```

**Generated CUDA Infrastructure:**
```cuda
// K2K message header structure
typedef struct {
    int source_kernel_id;
    int target_kernel_id;
    int message_type;
    int payload_size;
    long long timestamp;
} K2KMessageHeader;

// K2K send queue and receive channels are initialized in control block
// control_block->k2k_send_queue_ptr
// control_block->k2k_receive_channels_ptr
```

### Step 5: Configure Orleans.GpuBridge.Core Integration

The new system supports Orleans-specific features via attribute configuration:

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
public static void OrleansActorKernel()
{
    // Empty - implement handler logic in generated CUDA
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

## Handler Function Signature

All handler functions follow this fixed signature:

```cuda
__device__ bool process_{kernel_name}_message(
    const unsigned char* msg_buffer,    // Input message bytes
    int msg_size,                        // Input message size
    unsigned char* output_buffer,        // Output buffer to write to
    int* output_size_ptr,               // Pointer to set output size
    RingKernelControlBlock* control_block  // Access to kernel state
);
```

**Return value:**
- `true`: Message processed successfully, increment counter
- `false`: Message processing failed, don't increment counter

---

## Future: RingKernelContext API (Planned)

> **Note**: This API is planned for future releases. Currently, you must implement handler logic directly in CUDA.

The `RingKernelContext` will provide a C#-like API that gets translated to CUDA intrinsics:

### Synchronization (Planned)

| Method | CUDA Translation | Description |
|--------|-----------------|-------------|
| `ctx.SyncThreads()` | `__syncthreads()` | Block-level barrier |
| `ctx.SyncGrid()` | `cooperative_groups::grid_group::sync()` | Grid-level barrier |
| `ctx.SyncWarp()` | `__syncwarp()` | Warp-level barrier |
| `ctx.ThreadFence()` | `__threadfence()` | Memory fence |

### Atomic Operations (Planned)

| Method | CUDA Translation |
|--------|-----------------|
| `ctx.AtomicAdd(ref x, val)` | `atomicAdd(&x, val)` |
| `ctx.AtomicCAS(ref x, cmp, val)` | `atomicCAS(&x, cmp, val)` |
| `ctx.AtomicExch(ref x, val)` | `atomicExch(&x, val)` |

### Temporal APIs (Planned)

| Method | Description |
|--------|-------------|
| `ctx.Now()` | Returns GPU clock64() timestamp |
| `ctx.Tick()` | Increments HLC logical counter |

### K2K Messaging (Planned)

| Method | Description |
|--------|-------------|
| `ctx.SendToKernel(kernelId, msg)` | Send message to another kernel |
| `ctx.TryReceiveFromKernel<T>(kernelId)` | Try to receive from kernel |

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
1. Keep the handler logic in CUDA
2. Update it to match the new handler signature (5 parameters)
3. Rename to follow `process_{name}_message` convention
4. The generated kernel stub will call your handler function

---

## Common Migration Issues

### Issue 1: Handler Function Not Found

**Error:**
```
error: identifier "process_my_kernel_message" is undefined
```

**Solution:** Ensure the kernel method name follows the convention. The handler name is derived from the method name:
- Method: `MyKernel` → Handler: `process_my_message`
- Method: `ProcessDataKernel` → Handler: `process_process_data_message`

### Issue 2: Parameter Type Mismatch

**Error:**
```
error: argument of type "int" is incompatible with parameter of type "int *"
```

**Solution:** The handler function signature is fixed. Ensure your handler uses:
- `int* output_size_ptr` (pointer, not value)
- `RingKernelControlBlock* control_block` (5th parameter)

### Issue 3: Missing Control Block Members

**Error:**
```
error: 'RingKernelControlBlock' has no member named 'old_field'
```

**Solution:** Use the new control block member names:
- `terminate_flag` → `control_block->should_terminate`
- `message_count` → `control_block->messages_processed`

### Issue 4: Kernel Method Has Parameters

**Error:**
```
DC007: Ring kernel methods must have no parameters (current implementation)
```

**Solution:** Remove parameters from the kernel method. The current implementation requires empty kernel methods:
```csharp
// Wrong
public static void MyKernel(RingKernelContext ctx, Message msg) { }

// Correct (current)
public static void MyKernel() { }
```

---

## Support

- **Documentation**: `/docs/articles/guides/ring-kernels-advanced.md`
- **Tests**: `/tests/Unit/DotCompute.Backends.CUDA.Tests/Compilation/`
- **Issues**: https://github.com/mivertowski/DotCompute/issues

---

**Last Updated:** November 2025
**Applies To:** DotCompute v0.4.2-rc2+
**Authors:** Michael Ivertowski
