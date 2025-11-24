# Ring Kernel Messaging and Telemetry: Production-Grade GPU Communication

## Executive Summary

The Ring Kernel messaging and telemetry subsystem provides production-grade inter-kernel communication and real-time health monitoring for persistent GPU workloads. These features enable sophisticated distributed actor systems, message-driven architectures, and observable GPU applications with sub-microsecond overhead.

**Key Capabilities:**
- **GPU-Accelerated Message Queues**: Lock-free atomic operations with 4 backpressure strategies
- **Real-Time Telemetry**: Sub-microsecond zero-copy polling for GPU health monitoring
- **Cross-Backend Support**: CUDA, OpenCL, Metal, and CPU implementations
- **Production Observability**: OpenTelemetry integration and comprehensive diagnostics
- **Roslyn Analyzers**: Real-time IDE feedback for correct API usage

## Table of Contents

1. [Message Queue Architecture](#message-queue-architecture)
2. [Telemetry System Design](#telemetry-system-design)
3. [Implementation Details](#implementation-details)
4. [Performance Characteristics](#performance-characteristics)
5. [Production Integration](#production-integration)
6. [Best Practices](#best-practices)

---

## Message Queue Architecture

### Overview

Ring Kernel message queues provide reliable inter-kernel communication with GPU-native atomic operations, enabling distributed actor systems and event-driven architectures directly on GPU hardware.

**Design Principles:**
- **Lock-Free Atomics**: `atomicAdd`, `atomicCAS`, `atomicExch` for thread-safe queue operations
- **Circular Buffering**: Power-of-2 capacity for efficient modulo operations
- **Zero-Copy**: Messages stored directly in GPU-accessible memory
- **Backpressure Handling**: Four strategies for queue overflow scenarios

### Message Structure

The `IRingKernelMessage` interface defines the serialization contract:

```csharp
public interface IRingKernelMessage
{
    /// <summary>
    /// Serialize message to byte array for GPU transfer.
    /// Must be deterministic and thread-safe.
    /// </summary>
    byte[] Serialize();

    /// <summary>
    /// Deserialize message from byte array after GPU transfer.
    /// Must handle corrupted or partial data gracefully.
    /// </summary>
    void Deserialize(byte[] data);

    /// <summary>
    /// Total serialized size in bytes (including 4-byte UTF-8 length prefix).
    /// Used for capacity planning and buffer allocation.
    /// </summary>
    int PayloadSize { get; }
}
```

**Implementation Requirements:**
1. **Deterministic Serialization**: Same object must produce identical byte arrays
2. **Thread Safety**: Serialization must be safe for concurrent calls
3. **Error Handling**: Deserialization must handle partial/corrupted data
4. **Size Calculation**: `PayloadSize` = UTF-8 payload length + 4-byte length prefix

### Queue Configuration

Ring Kernel message queues are configured via the `RingKernelLaunchOptions` class (v0.5.3+), which provides comprehensive control over queue behavior:

```csharp
public sealed class RingKernelLaunchOptions
{
    /// <summary>
    /// Queue capacity (power of 2, range: 16-1048576).
    /// Larger capacities reduce overflow but increase memory usage.
    /// Default: 4096 messages (production-optimized)
    /// </summary>
    public int QueueCapacity { get; set; } = 4096;

    /// <summary>
    /// Deduplication window size (range: 16-1024 messages).
    /// Prevents duplicate messages within the sliding window.
    /// Default: 1024 messages (maximum validated size)
    /// </summary>
    public int DeduplicationWindowSize { get; set; } = 1024;

    /// <summary>
    /// Backpressure strategy for queue overflow scenarios.
    /// Default: Block (wait for space, guaranteed delivery)
    /// </summary>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;

    /// <summary>
    /// Enable priority-based message ordering.
    /// Default: false (FIFO maximizes throughput)
    /// </summary>
    public bool EnablePriorityQueue { get; set; } = false;

    /// <summary>
    /// Validates configuration and throws if invalid.
    /// Auto-clamps DeduplicationWindowSize to QueueCapacity if smaller.
    /// </summary>
    public void Validate() { /* Implementation */ }

    /// <summary>
    /// Converts to MessageQueueOptions for queue creation.
    /// </summary>
    public MessageQueueOptions ToMessageQueueOptions() { /* Implementation */ }
}
```

**Factory Methods** (Recommended):

```csharp
// Production defaults (4096 capacity, Block backpressure)
var options = RingKernelLaunchOptions.ProductionDefaults();

// Low-latency defaults (256 capacity, Reject backpressure)
var options = RingKernelLaunchOptions.LowLatencyDefaults();

// High-throughput defaults (16384 capacity, Block backpressure)
var options = RingKernelLaunchOptions.HighThroughputDefaults();
```

### Backpressure Strategies

Four strategies handle queue overflow conditions:

#### 1. Block (Default)

**Behavior:** Producer blocks until queue has space
**Use Case:** Reliable delivery, no message loss acceptable
**Performance:** May cause producer stalls under high load

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 4096,
    BackpressureStrategy = BackpressureStrategy.Block
};

// Launch kernel with Block strategy
await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);

// Producer blocks if queue full
await queue.EnqueueAsync(message, ct); // May wait indefinitely
```

#### 2. Reject

**Behavior:** Enqueue fails immediately if queue full
**Use Case:** Fire-and-forget messaging, best-effort delivery
**Performance:** No blocking, predictable latency

```csharp
var options = RingKernelLaunchOptions.LowLatencyDefaults();
// QueueCapacity: 256, BackpressureStrategy: Reject

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);

// Returns false if queue full, no blocking
if (!await queue.TryEnqueueAsync(message, ct))
{
    _logger.LogWarning("Message rejected: queue full");
}
```

#### 3. DropOldest

**Behavior:** Evict oldest message to make space for new message
**Use Case:** Real-time telemetry, latest data most important
**Performance:** No blocking, always succeeds

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 512,
    BackpressureStrategy = BackpressureStrategy.DropOldest,
    DeduplicationWindowSize = 256  // Recent duplicate detection
};

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);

// Always succeeds, may drop oldest message
await queue.EnqueueAsync(message, ct);
```

#### 4. DropNew

**Behavior:** Discard new message if queue full
**Use Case:** Historical logging, preserve oldest data
**Performance:** No blocking, enqueue returns false

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 1024,
    BackpressureStrategy = BackpressureStrategy.DropNew
};

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);

// Silently drops new message if queue full
bool enqueued = await queue.TryEnqueueAsync(message, ct);
```

### Deduplication

Message deduplication prevents duplicate processing using a sliding window:

```csharp
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 4096,
    DeduplicationWindowSize = 1024,  // Track last 1024 messages (maximum)
    BackpressureStrategy = BackpressureStrategy.Block
};

await runtime.LaunchAsync("kernel_id", gridSize: 1, blockSize: 256, options);

// Duplicate messages within window are silently dropped
await queue.EnqueueAsync(message1, ct); // Enqueued
await queue.EnqueueAsync(message1, ct); // Dropped (duplicate)
await queue.EnqueueAsync(message2, ct); // Enqueued
```

**Implementation Details:**
- **Window Size**: Configurable 16-1024 messages (default: 1024 = maximum validated size)
- **Hash Function**: FNV-1a 32-bit hash of serialized payload
- **Collision Handling**: Birthday paradox: <0.1% collision rate at 1024 messages
- **Performance Impact**: ~5ns per enqueue for hash calculation
- **Auto-Clamping**: Window size automatically clamped to QueueCapacity if QueueCapacity < 1024
- **Memory Cost**: ~32 bytes × DeduplicationWindowSize per queue

### Named Queues

Multiple queues per Ring Kernel enable parallel message streams. All queues share the same `RingKernelLaunchOptions` configuration (v0.5.3+):

```csharp
// Configure queue behavior (applies to ALL queues)
var options = new RingKernelLaunchOptions
{
    QueueCapacity = 4096,              // Shared capacity for all queues
    DeduplicationWindowSize = 1024,     // Shared deduplication window
    BackpressureStrategy = BackpressureStrategy.Block,  // Shared backpressure strategy
    EnablePriorityQueue = false         // Shared priority setting
};

// Create Ring Kernel runtime with MessageQueueRegistry
var registry = new MessageQueueRegistry();
var runtime = new CudaRingKernelRuntime(logger, compiler, registry);

// Launch kernel with shared queue configuration
await runtime.LaunchAsync("my_kernel", gridSize: 128, blockSize: 256, options);

// Named queues are created automatically by the runtime
// using the configuration from RingKernelLaunchOptions

// Enqueue to specific named queues
await wrapper.EnqueueAsync("commands", commandMessage, ct);
await wrapper.EnqueueAsync("events", eventMessage, ct);
await wrapper.EnqueueAsync("telemetry", telemetryMessage, ct);

// Dequeue from specific named queues
var command = await wrapper.DequeueAsync<CommandMessage>("commands", ct);
var event = await wrapper.DequeueAsync<EventMessage>("events", ct);
```

**Registry Management:**
- **Automatic Registration**: Source generator registers all named queues with `MessageQueueRegistry`
- **Thread-Safe Access**: `MessageQueueRegistry` provides concurrent queue access via `GetQueue(name)`
- **Shared Configuration**: All queues use the same `RingKernelLaunchOptions` settings
- **Lifetime Management**: Queues disposed automatically when Ring Kernel terminates
- **Cross-Backend**: Registry works identically across CUDA, OpenCL, Metal, and CPU backends

### GPU Kernel Integration

Message queues integrate seamlessly with CUDA/OpenCL/Metal kernels:

**CUDA Example (C++):**
```cuda
__global__ void processingKernel(
    MessageQueue* queue,
    int* results,
    int resultCount)
{
    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    // Dequeue message atomically
    Message msg;
    if (queue->dequeue(&msg))
    {
        // Process message
        int result = processMessage(&msg);

        // Store result atomically
        if (tid < resultCount)
        {
            atomicExch(&results[tid], result);
        }
    }
}
```

**OpenCL Example (C):**
```opencl
__kernel void processingKernel(
    __global MessageQueue* queue,
    __global int* results,
    int resultCount)
{
    int tid = get_global_id(0);

    // Dequeue message atomically
    Message msg;
    if (dequeueMessage(queue, &msg))
    {
        // Process message
        int result = processMessage(&msg);

        // Store result atomically
        if (tid < resultCount)
        {
            atomic_xchg(&results[tid], result);
        }
    }
}
```

---

## Telemetry System Design

### Overview

The Ring Kernel telemetry subsystem provides sub-microsecond real-time monitoring of GPU health metrics via zero-copy memory polling. This enables production-grade observability for long-running GPU applications without impacting performance.

**Architecture Goals:**
- **<1μs Polling Latency**: Direct memory reads, no GPU synchronization
- **Zero Copy**: CPU reads GPU-written telemetry buffers directly
- **Lazy Initialization**: Buffers allocated only when telemetry enabled
- **Atomic Updates**: Thread-safe concurrent counter updates
- **IDE Integration**: Roslyn analyzers prevent common API misuse

### Telemetry Struct

The `RingKernelTelemetry` struct is a 64-byte cache-aligned structure:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct RingKernelTelemetry
{
    /// <summary>Total messages processed (atomic counter)</summary>
    public ulong MessagesProcessed;

    /// <summary>Messages dropped due to queue overflow (atomic counter)</summary>
    public ulong MessagesDropped;

    /// <summary>Current queue depth (pending messages)</summary>
    public int QueueDepth;

    /// <summary>Reserved for alignment (padding)</summary>
    public int Reserved1;

    /// <summary>GPU timestamp of last processed message (nanoseconds)</summary>
    public long LastProcessedTimestamp;

    /// <summary>Reserved for future counters (32 bytes padding)</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] Reserved2;
}
```

**Design Rationale:**
- **64 bytes**: Cache line alignment prevents false sharing
- **Sequential layout**: Consistent memory layout for GPU interop
- **Atomic counters**: `MessagesProcessed`/`MessagesDropped` updated via `Interlocked.Add`
- **Reserved fields**: Future extensibility (latency percentiles, error counts)

### Backend Memory Strategies

#### CUDA: Pinned Host Memory

**Memory Type:** `cuMemHostAlloc` with `CU_MEMHOSTALLOC_DEVICEMAP`
**Polling Latency:** <1μs (measured on RTX 2000 Ada)
**Characteristics:**
- Pinned host memory prevents OS paging
- Write-combined for faster GPU→CPU transfers
- Device-mapped for zero-copy CPU access

**Implementation:**
```csharp
// CudaTelemetryBuffer.cs
var result = CudaNative.cuMemHostAlloc(
    out _hostPointer,
    (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    CU_MEMHOSTALLOC_DEVICEMAP | CU_MEMHOSTALLOC_WRITECOMBINED);

// Map to GPU-accessible address
CudaNative.cuMemHostGetDevicePointer(out _devicePointer, _hostPointer, 0);

// Zero-copy poll
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        var ptr = (RingKernelTelemetry*)_hostPointer;
        return *ptr; // Single memory read, no synchronization
    }
}
```

#### OpenCL: Mapped Pinned Memory

**Memory Type:** `clCreateBuffer` with `CL_MEM_ALLOC_HOST_PTR`
**Polling Latency:** <1μs (measured on NVIDIA GPU)
**Characteristics:**
- Pinned allocation via `CL_MEM_ALLOC_HOST_PTR`
- Persistent mapping for zero-copy access
- Read-only mapping optimizes for CPU polling

**Implementation:**
```csharp
// OpenCLTelemetryBuffer.cs
var flags = MemFlags.CL_MEM_ALLOC_HOST_PTR | MemFlags.CL_MEM_READ_WRITE;
_bufferObject = OpenCLNative.clCreateBuffer(
    _context, flags,
    (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    IntPtr.Zero, out var result);

// Map once at initialization
_mappedPointer = OpenCLNative.clEnqueueMapBuffer(
    _commandQueue, _bufferObject,
    blocking: true, MapFlags.CL_MAP_READ,
    offset: 0, size: (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    numEventsInWaitList: 0, eventWaitList: null,
    outEvent: out _, out result);

// Zero-copy poll
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        var ptr = (RingKernelTelemetry*)_mappedPointer;
        return *ptr; // Direct read from mapped memory
    }
}
```

#### Metal: Shared Memory (Unified Memory Architecture)

**Memory Type:** `MTLStorageMode.Shared`
**Polling Latency:** <0.5μs (measured on M1 Max)
**Characteristics:**
- Unified memory on Apple Silicon
- CPU and GPU share physical memory
- No explicit synchronization required
- Fastest polling latency

**Implementation:**
```csharp
// MetalTelemetryBuffer.cs
var length = (nuint)Marshal.SizeOf<RingKernelTelemetry>();
_bufferObject = MetalNative.newBufferWithLength(
    _device, length,
    MTLResourceOptions.StorageModeShared);

_contentsPointer = MetalNative.bufferContents(_bufferObject);

// Zero-copy poll (fastest on unified memory)
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        var ptr = (RingKernelTelemetry*)_contentsPointer;
        return *ptr; // L1 cache hit on unified memory
    }
}
```

#### CPU: In-Memory Struct

**Memory Type:** Heap-allocated struct
**Polling Latency:** <0.1μs (L1 cache)
**Characteristics:**
- Simplest implementation
- No GPU interop overhead
- Useful for testing/simulation

**Implementation:**
```csharp
// CpuTelemetryBuffer.cs
private RingKernelTelemetry _telemetry;

public void Allocate()
{
    _telemetry = new RingKernelTelemetry();
}

public async ValueTask<RingKernelTelemetry> PollAsync()
{
    return _telemetry; // Direct struct copy from heap
}
```

### Telemetry API

Three methods are auto-injected into all Ring Kernel wrappers by the source generator:

#### GetTelemetryAsync

Poll current telemetry snapshot:

```csharp
// Production monitoring loop (100Hz = 10ms interval)
while (!ct.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);

    _logger.LogInformation(
        "Messages: {Processed} processed, {Dropped} dropped, " +
        "Queue: {Depth} pending",
        telemetry.MessagesProcessed,
        telemetry.MessagesDropped,
        telemetry.QueueDepth);

    await Task.Delay(millisecondsDelay: 10, ct);
}
```

**Performance:** <1μs latency, zero GPU synchronization

#### SetTelemetryEnabledAsync

Enable or disable telemetry:

```csharp
// Enable telemetry after launch
await wrapper.LaunchAsync(blockSize: 256, gridSize: 128, ct);
await wrapper.SetTelemetryEnabledAsync(enabled: true, ct);

// Disable telemetry (buffer persists, avoids reallocation)
await wrapper.SetTelemetryEnabledAsync(enabled: false, ct);
```

**Lazy Initialization:** 64-byte GPU buffer allocated on first enable

#### ResetTelemetryAsync

Reset all counters to zero:

```csharp
// Reset counters for phased benchmark
await wrapper.ResetTelemetryAsync(ct);

// Run benchmark phase
await RunBenchmarkPhaseAsync(ct);

// Capture results
var telemetry = await wrapper.GetTelemetryAsync(ct);
_logger.LogInformation(
    "Phase completed: {Messages} messages in {Time}ms",
    telemetry.MessagesProcessed, phaseTime.TotalMilliseconds);
```

**Performance:** Zero overhead, just `memset` on pinned memory

### Recommended Polling Intervals

Excessive polling degrades system performance. Choose intervals based on use case:

| Use Case | Interval | Frequency | CPU Overhead |
|----------|---------|-----------|-------------|
| Background Logging | 100ms+ | <10 Hz | <0.001% |
| **Production Monitoring** | **10ms** | **100 Hz** | **0.01%** |
| Development/Debugging | 1ms | 1 kHz | 0.1% |
| Stress Testing | 100μs | 10 kHz | 1% |

**Analyzer Integration (DC014):**

The DC014 analyzer warns about excessive polling:

```csharp
// ❌ BAD: Tight loop triggers DC014 warning
while (true)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry (no delay!)
}

// ✅ GOOD: 10ms delay (100Hz, production-grade)
while (!ct.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    await Task.Delay(10, ct); // 10ms = 100Hz
}
```

---

## Implementation Details

### Source Generator Integration

The source generator automatically injects messaging and telemetry APIs into all Ring Kernel wrappers.

**Generated Code Example:**

```csharp
// Generated wrapper class (partial)
public sealed partial class MyRingKernelWrapper : IAsyncDisposable
{
    private readonly IRingKernelRuntime _runtime;
    private readonly Guid _kernelId;
    private bool _disposed;
    private bool _isLaunched;

    // ========== MESSAGE QUEUE METHODS ==========

    public async Task EnqueueAsync<TMessage>(
        string queueName,
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isLaunched)
        {
            throw new InvalidOperationException(
                "Ring Kernel must be launched before enqueueing messages.");
        }

        var queue = _runtime.MessageQueueRegistry.GetQueue(queueName);
        await queue.EnqueueAsync(message, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TMessage?> DequeueAsync<TMessage>(
        string queueName,
        CancellationToken cancellationToken = default)
        where TMessage : IRingKernelMessage, new()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isLaunched)
        {
            throw new InvalidOperationException(
                "Ring Kernel must be launched before dequeuing messages.");
        }

        var queue = _runtime.MessageQueueRegistry.GetQueue(queueName);
        return await queue.DequeueAsync<TMessage>(cancellationToken)
            .ConfigureAwait(false);
    }

    // ========== TELEMETRY METHODS ==========

    public async Task<RingKernelTelemetry> GetTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isLaunched)
        {
            throw new InvalidOperationException(
                "Ring Kernel must be launched before polling telemetry.");
        }

        return await _runtime.GetTelemetryAsync(_kernelId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SetTelemetryEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isLaunched)
        {
            throw new InvalidOperationException(
                "Ring Kernel must be launched before enabling telemetry.");
        }

        await _runtime.SetTelemetryEnabledAsync(_kernelId, enabled, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ResetTelemetryAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_isLaunched)
        {
            throw new InvalidOperationException(
                "Ring Kernel must be launched before resetting telemetry.");
        }

        await _runtime.ResetTelemetryAsync(_kernelId, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

### Roslyn Analyzers

Six diagnostic rules ensure correct API usage:

#### DC014: TelemetryPollingTooFrequent (Warning)

Detects excessive polling frequency (>10kHz):

```csharp
// Triggers DC014 warning
while (true)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // No delay - polling at maximum frequency
}
```

**Fix:** Add `Task.Delay` with appropriate interval (1-10ms recommended)

#### DC015: TelemetryNotEnabled (Error)

Detects `GetTelemetryAsync` without prior enable:

```csharp
// Triggers DC015 error
await wrapper.LaunchAsync(256, 128, ct);
var telemetry = await wrapper.GetTelemetryAsync(ct); // ❌ Buffer not allocated
```

**Fix:** Call `SetTelemetryEnabledAsync(true)` after launch

#### DC016: TelemetryEnabledBeforeLaunch (Error)

Detects telemetry enabled before kernel launch:

```csharp
// Triggers DC016 error
var wrapper = new MyRingKernelWrapper(runtime);
await wrapper.SetTelemetryEnabledAsync(true, ct); // ❌ No kernel context
await wrapper.LaunchAsync(256, 128, ct);
```

**Fix:** Launch kernel before enabling telemetry

#### DC017: TelemetryResetWithoutEnable (Warning)

Detects reset without prior enable:

```csharp
// Triggers DC017 warning
await wrapper.LaunchAsync(256, 128, ct);
await wrapper.ResetTelemetryAsync(ct); // ❌ Buffer not allocated
```

**Fix:** Enable telemetry before resetting

---

## Performance Characteristics

### Message Queue Performance

Measured on RTX 2000 Ada (Compute Capability 8.9):

| Operation | Latency | Throughput | Notes |
|-----------|---------|-----------|-------|
| Enqueue (Block) | 2-5μs | 200K-500K msg/s | Includes atomic operations |
| Enqueue (Reject) | 1-3μs | 300K-1M msg/s | No blocking, immediate return |
| Enqueue (DropOldest) | 1-3μs | 300K-1M msg/s | Eviction adds ~1μs |
| Dequeue | 1-3μs | 300K-1M msg/s | Atomic read + deserialization |
| Deduplication | +5ns | Negligible | FNV-1a hash overhead |

**Memory Overhead:**
- **Queue Structure**: 64 bytes (head/tail/capacity/metadata)
- **Message Storage**: `Capacity × MaxMessageSize` bytes
- **Deduplication**: `DeduplicationWindow × 4` bytes (hash table)

**Example:** 256-message queue with 1KB max size = 64 + (256 × 1024) = 262KB GPU memory

### Telemetry Performance

Measured across all backends:

| Backend | Polling Latency | Throughput | Hardware |
|---------|----------------|-----------|----------|
| **CUDA (pinned)** | **0.82μs** | **1.2M polls/s** | RTX 2000 Ada |
| **OpenCL (mapped)** | **0.91μs** | **1.1M polls/s** | RTX 2000 Ada |
| **Metal (shared)** | **0.45μs** | **2.2M polls/s** | M1 Max (unified) |
| **CPU (in-memory)** | **0.08μs** | **12.5M polls/s** | Intel Core i7 |

**Memory Overhead:**
- **Telemetry Buffer**: 64 bytes per Ring Kernel
- **Lazy Allocation**: Zero overhead when telemetry disabled

**Polling Overhead (Production 100Hz):**
- 100 polls/second × 0.82μs = 82μs/s = **0.0082% CPU overhead**

### Scalability

**Message Queues:**
- Up to 16 named queues per Ring Kernel
- Queue capacity: 16-1024 messages (power of 2)
- Tested with 1M+ messages/second sustained throughput

**Telemetry:**
- Tested with 100 concurrent Ring Kernels
- Aggregate polling: 10,000 polls/second across all kernels
- Zero contention on unified memory (Metal)

---

## Production Integration

### OpenTelemetry Integration

Export telemetry to OpenTelemetry for centralized monitoring:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using System.Diagnostics.Metrics;

public class RingKernelMetrics
{
    private static readonly Meter s_meter = new("DotCompute.RingKernel");

    private readonly ObservableCounter<long> _messagesProcessedCounter;
    private readonly ObservableCounter<long> _messagesDroppedCounter;
    private readonly ObservableGauge<int> _queueDepthGauge;

    private RingKernelTelemetry _lastTelemetry;

    public RingKernelMetrics(MyRingKernelWrapper wrapper)
    {
        _messagesProcessedCounter = s_meter.CreateObservableCounter(
            "dotcompute.ringkernel.messages.processed",
            () => (long)_lastTelemetry.MessagesProcessed,
            description: "Total messages processed");

        _messagesDroppedCounter = s_meter.CreateObservableCounter(
            "dotcompute.ringkernel.messages.dropped",
            () => (long)_lastTelemetry.MessagesDropped,
            description: "Total messages dropped");

        _queueDepthGauge = s_meter.CreateObservableGauge(
            "dotcompute.ringkernel.queue.depth",
            () => _lastTelemetry.QueueDepth,
            description: "Current queue depth");

        _ = PollTelemetryAsync(wrapper);
    }

    private async Task PollTelemetryAsync(MyRingKernelWrapper wrapper)
    {
        while (true)
        {
            _lastTelemetry = await wrapper.GetTelemetryAsync(default);
            await Task.Delay(millisecondsDelay: 100); // 10Hz for OpenTelemetry
        }
    }
}

// Configure OpenTelemetry
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("DotCompute.RingKernel")
    .AddPrometheusExporter()
    .Build();
```

### Orleans.GpuBridge Integration

Integrate with Orleans actor systems for distributed GPU computation:

```csharp
using Orleans;
using DotCompute.Abstractions.RingKernels;

public class GpuActorGrain : Grain, IGpuActorGrain
{
    private MyRingKernelWrapper _wrapper = null!;
    private CancellationTokenSource _monitoringCts = null!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var runtime = GrainFactory.GetGrain<IRingKernelRuntime>(0);

        // Launch Ring Kernel
        _wrapper = new MyRingKernelWrapper(runtime);
        await _wrapper.LaunchAsync(blockSize: 256, gridSize: 128, ct);

        // Enable telemetry
        await _wrapper.SetTelemetryEnabledAsync(enabled: true, ct);

        // Start monitoring loop
        _monitoringCts = new CancellationTokenSource();
        _ = MonitorTelemetryAsync(_monitoringCts.Token);

        await base.OnActivateAsync(ct);
    }

    public async Task<int> ProcessMessageAsync(ActorMessage message)
    {
        // Enqueue message to GPU
        await _wrapper.EnqueueAsync("commands", message, default);

        // Dequeue result
        var result = await _wrapper.DequeueAsync<ActorResult>("results", default);
        return result?.Value ?? -1;
    }

    private async Task MonitorTelemetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var telemetry = await _wrapper.GetTelemetryAsync(ct);

            if (telemetry.MessagesDropped > 0)
            {
                Logger.LogWarning(
                    "Grain {GrainId}: {Dropped} messages dropped",
                    this.GetPrimaryKeyLong(), telemetry.MessagesDropped);
            }

            await Task.Delay(millisecondsDelay: 1000, ct); // 1Hz for Orleans
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _monitoringCts.Cancel();
        await _wrapper.DisposeAsync();
        await base.OnDeactivateAsync(reason, ct);
    }
}
```

---

## Best Practices

### Message Queue Best Practices

1. **Choose Appropriate Backpressure Strategy**
   - **Block**: Use for critical messages requiring guaranteed delivery
   - **Reject**: Use for fire-and-forget, best-effort scenarios
   - **DropOldest**: Use for real-time telemetry where latest data matters
   - **DropNew**: Use for historical logging preserving oldest data

2. **Size Queues Appropriately**
   - **Too Small**: Frequent overflow, message loss
   - **Too Large**: Excessive memory usage, stale data
   - **Rule of Thumb**: 2× peak message rate × polling interval
   - **Example**: 1000 msg/s × 10ms = 10 messages minimum, use 32-64 for safety

3. **Enable Deduplication Judiciously**
   - **Cost**: 5ns per enqueue, negligible for most workloads
   - **Benefit**: Prevents duplicate processing in retry scenarios
   - **Window Size**: Set to 2-4× max expected burst size
   - **Disable**: If messages are guaranteed unique or cost-sensitive

4. **Serialize Efficiently**
   - **Avoid Reflection**: Use explicit field serialization
   - **Fixed-Size Preferred**: Reduces buffer management overhead
   - **Validate Deserialization**: Handle partial/corrupted data gracefully

### Telemetry Best Practices

1. **Enable Telemetry Only When Needed**
   - 64-byte GPU allocation per Ring Kernel
   - Zero overhead when disabled (lazy initialization)
   - Consider production vs. development profiles

2. **Poll at Appropriate Intervals**
   - **Production**: 10ms (100Hz) - 0.01% CPU overhead
   - **Development**: 1ms (1kHz) - 0.1% CPU overhead
   - **Avoid**: <100μs (>10kHz) - triggers DC014 warning

3. **Monitor for Anomalies**
   - **MessagesDropped > 0**: Queue overflow, increase capacity or adjust backpressure
   - **QueueDepth consistently high**: Producer faster than consumer, scale consumers
   - **MessagesProcessed stagnant**: Ring Kernel may be stalled, check GPU status

4. **Reset Counters Between Phases**
   - Use `ResetTelemetryAsync` for phased benchmarks
   - Provides clean metrics per test iteration
   - Zero overhead operation

5. **Integrate with Observability Stack**
   - Export to OpenTelemetry for centralized monitoring
   - Set up alerts for `MessagesDropped` spikes
   - Track `MessagesProcessed` rate for throughput metrics

### Error Handling

```csharp
// Robust message processing with error handling
public async Task ProcessMessagesAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            // Dequeue with timeout
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var message = await _wrapper.DequeueAsync<MyMessage>("commands", cts.Token);
            if (message == null)
            {
                continue; // Queue empty, retry
            }

            // Process message
            var result = await ProcessMessageAsync(message);

            // Enqueue result with backpressure handling
            if (!await _wrapper.TryEnqueueAsync("results", result, ct))
            {
                _logger.LogWarning("Result queue full, message {Id} dropped", message.Id);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing cancelled");
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            await Task.Delay(millisecondsDelay: 100, ct); // Back off on errors
        }
    }
}
```

---

## Summary

The Ring Kernel messaging and telemetry subsystem provides production-grade GPU communication and observability:

**Messaging Features:**
- Lock-free atomic message queues with 4 backpressure strategies
- Cross-backend support (CUDA, OpenCL, Metal, CPU)
- Deduplication, named queues, and flexible configuration
- Measured throughput: 200K-1M messages/second

**Telemetry Features:**
- Sub-microsecond zero-copy polling (<1μs on CUDA/OpenCL, <0.5μs on Metal)
- Lazy initialization with 64-byte GPU buffers
- Atomic counters for thread-safe concurrent updates
- OpenTelemetry integration for production monitoring

**Developer Experience:**
- Auto-injected APIs via source generators
- Roslyn analyzers (DC014-DC017) for real-time IDE feedback
- Comprehensive documentation and examples
- Orleans.GpuBridge integration for distributed actor systems

These features enable sophisticated distributed GPU applications, message-driven architectures, and observable long-running GPU workloads with negligible performance overhead.

## See Also

- [Ring Kernels: Introduction](ring-kernels-introduction.md)
- [Ring Kernels: Advanced Programming](ring-kernels-advanced.md)
- [Ring Kernel Telemetry API](ring-kernel-telemetry.md)
- [Orleans Integration](orleans-integration.md)
- [Performance Profiling](performance-profiling.md)
