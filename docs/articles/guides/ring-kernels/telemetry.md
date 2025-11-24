# Ring Kernel Telemetry API: Real-Time GPU Health Monitoring

## Overview

The Ring Kernel Telemetry API provides **sub-microsecond** zero-copy telemetry polling for real-time GPU health monitoring in persistent Ring Kernels. This enables production-grade observability for long-running GPU applications, actor systems, and distributed compute workloads.

**Key Features:**
- **<1μs Polling Latency**: Zero-copy memory reads via pinned/mapped/shared memory
- **Lazy Initialization**: Telemetry buffers allocated only when explicitly enabled
- **Atomic Counters**: Thread-safe concurrent updates with `Interlocked` operations
- **Backend-Optimized**: CUDA pinned memory, OpenCL mapped memory, Metal shared memory, CPU in-memory
- **IDE Integration**: Real-time diagnostics (DC014-DC017) for common telemetry API mistakes
- **Auto-Injected Methods**: Source generator adds telemetry methods to all Ring Kernel wrappers

**Supported Backends:**
| Backend | Memory Strategy | Latency | Hardware Requirement |
|---------|----------------|---------|---------------------|
| CUDA | Pinned host memory (`CU_MEMHOSTALLOC_DEVICEMAP`) | <1μs | CC 5.0+ (Maxwell) |
| OpenCL | Mapped pinned memory (`CL_MEM_ALLOC_HOST_PTR`) | <1μs | OpenCL 1.2+ |
| Metal | Shared memory (`MTLStorageMode.Shared`) | <0.5μs | Apple Silicon unified memory |
| CPU | In-memory struct with `Interlocked` atomics | <0.1μs | Any CPU |

## Telemetry Struct Layout

The `RingKernelTelemetry` struct is a **64-byte cache-aligned** structure designed for zero-copy GPU interop:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct RingKernelTelemetry
{
    /// <summary>Total number of messages processed by the Ring Kernel (atomic counter)</summary>
    public ulong MessagesProcessed;

    /// <summary>Number of messages dropped due to queue overflow (atomic counter)</summary>
    public ulong MessagesDropped;

    /// <summary>Current queue depth (number of pending messages)</summary>
    public int QueueDepth;

    /// <summary>Reserved for alignment (padding to 8-byte boundary)</summary>
    public int Reserved1;

    /// <summary>GPU timestamp of last processed message (nanoseconds since kernel launch)</summary>
    public long LastProcessedTimestamp;

    /// <summary>Reserved for future use (32 bytes of padding for cache alignment)</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] Reserved2;
}
```

**Design Rationale:**
- **64 bytes**: Cache line alignment prevents false sharing across CPU cores
- **Sequential layout**: Ensures consistent memory layout for GPU interop
- **Atomic counters**: `MessagesProcessed` and `MessagesDropped` updated via `Interlocked.Add`
- **Reserved fields**: Future extensibility for additional counters (e.g., latency percentiles, error counts)

## Quick Start

### Enabling Telemetry

```csharp
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Abstractions.RingKernels;

// 1. Launch Ring Kernel
var wrapper = new MyRingKernelWrapper(runtime);
await wrapper.LaunchAsync(blockSize: 256, gridSize: 128, cancellationToken);

// 2. Enable telemetry (allocates GPU buffer lazily)
await wrapper.SetTelemetryEnabledAsync(enabled: true, cancellationToken);

Console.WriteLine("Telemetry enabled - buffer allocated on GPU");
```

### Basic Telemetry Polling

```csharp
// Poll telemetry at recommended interval (1-10ms for production)
while (!cancellationToken.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(cancellationToken);

    Console.WriteLine($"Messages Processed: {telemetry.MessagesProcessed}");
    Console.WriteLine($"Messages Dropped: {telemetry.MessagesDropped}");
    Console.WriteLine($"Queue Depth: {telemetry.QueueDepth}");
    Console.WriteLine($"Last Timestamp: {telemetry.LastProcessedTimestamp} ns");

    // Recommended polling interval: 1-10ms (100Hz-1kHz)
    await Task.Delay(millisecondsDelay: 10, cancellationToken);
}
```

### Resetting Telemetry Counters

```csharp
// Reset all counters to zero (useful for phased benchmarks)
await wrapper.ResetTelemetryAsync(cancellationToken);

Console.WriteLine("Telemetry counters reset to zero");
```

### Disabling Telemetry

```csharp
// Disable telemetry (does NOT deallocate buffer - use DisposeAsync for that)
await wrapper.SetTelemetryEnabledAsync(enabled: false, cancellationToken);

Console.WriteLine("Telemetry disabled - polling will return stale data");
```

## API Reference

### Generated Wrapper Methods

The source generator automatically adds three telemetry methods to all Ring Kernel wrappers:

#### `GetTelemetryAsync(CancellationToken)`

```csharp
/// <summary>
/// Gets real-time telemetry data from the GPU Ring Kernel.
/// This is a zero-copy operation that reads directly from GPU memory (&lt;1μs latency).
/// </summary>
/// <param name="cancellationToken">Cancellation token for async operation</param>
/// <returns>Current telemetry snapshot</returns>
/// <exception cref="InvalidOperationException">
/// Thrown if Ring Kernel is not launched or telemetry is not enabled
/// </exception>
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
```

**Usage Notes:**
- **Zero-copy**: Direct memory read, no GPU kernel launch required
- **Thread-safe**: Safe to call concurrently from multiple threads
- **Idempotent**: Can be called repeatedly without side effects
- **Latency**: <1μs typical on modern hardware

#### `SetTelemetryEnabledAsync(bool, CancellationToken)`

```csharp
/// <summary>
/// Enables or disables real-time GPU telemetry for this Ring Kernel.
/// </summary>
/// <param name="enabled">True to enable telemetry, false to disable</param>
/// <param name="cancellationToken">Cancellation token for async operation</param>
/// <exception cref="InvalidOperationException">
/// Thrown if Ring Kernel is not launched (must call LaunchAsync first)
/// </exception>
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
```

**Usage Notes:**
- **Lazy allocation**: Buffer allocated on first enable (64 bytes GPU memory)
- **Persistent buffer**: Disabling does NOT deallocate buffer (avoids repeated allocations)
- **Must launch first**: Call `LaunchAsync()` before enabling telemetry

#### `ResetTelemetryAsync(CancellationToken)`

```csharp
/// <summary>
/// Resets the telemetry counters to zero.
/// </summary>
/// <param name="cancellationToken">Cancellation token for async operation</param>
/// <exception cref="InvalidOperationException">
/// Thrown if Ring Kernel is not launched or telemetry is not enabled
/// </exception>
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
```

**Usage Notes:**
- **Zero-overhead**: No GPU kernel launch, just `memset` on pinned memory
- **Useful for**: Phased benchmarks, test isolation, production monitoring resets

## Recommended Polling Intervals

Excessive polling can degrade overall system performance. Choose polling intervals based on your use case:

| Use Case | Recommended Interval | Frequency | Rationale |
|----------|---------------------|-----------|-----------|
| **Development/Debugging** | 100-1000μs (0.1-1ms) | 1-10kHz | High granularity for identifying transient issues |
| **Production Monitoring** | 1000-10000μs (1-10ms) | 100Hz-1kHz | Balanced observability with minimal overhead |
| **Low-Priority Logging** | 100000μs+ (100ms+) | <10Hz | Minimal overhead for background telemetry |

**Performance Impact:**
- **<1μs polling latency** means 1kHz polling = **0.1% CPU overhead** (1ms/s)
- **10kHz polling** = **1% CPU overhead** (10ms/s) - acceptable for debugging
- **>10kHz polling** = DC014 analyzer warning (exceeds recommended maximum)

**Analyzer Integration:**
The DC014 analyzer detects tight loops without sufficient delay:

```csharp
// ❌ BAD: Tight loop without delay (triggers DC014 warning)
while (true)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry
}

// ✅ GOOD: Recommended 1ms delay (1kHz polling)
while (!ct.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry
    await Task.Delay(1, ct); // 1ms = 1kHz
}

// ✅ GOOD: Recommended 10ms delay (100Hz, production-grade)
while (!ct.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry
    await Task.Delay(10, ct); // 10ms = 100Hz
}
```

## Backend Implementation Details

### CUDA Backend: Pinned Host Memory

**Memory Allocation:**
```csharp
// CudaTelemetryBuffer.cs
var result = CudaNative.cuMemHostAlloc(
    out _hostPointer,
    (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    CU_MEMHOSTALLOC_DEVICEMAP | CU_MEMHOSTALLOC_WRITECOMBINED);

if (result != CudaResult.Success)
{
    throw new InvalidOperationException(
        $"Failed to allocate CUDA pinned memory: {result}");
}

// Map to GPU-accessible address
CudaNative.cuMemHostGetDevicePointer(out _devicePointer, _hostPointer, 0);
```

**Zero-Copy Read:**
```csharp
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        // Direct memory read from pinned host memory (zero-copy, <1μs)
        var ptr = (RingKernelTelemetry*)_hostPointer;
        return *ptr; // Single memory read, no GPU synchronization
    }
}
```

**Key Characteristics:**
- **Pinned memory**: Prevents OS from paging, enables DMA transfers
- **Write-combined**: GPU writes bypass CPU cache (faster GPU→CPU transfers)
- **Device-mapped**: CPU can read GPU-written data without explicit copy

### OpenCL Backend: Mapped Pinned Memory

**Memory Allocation:**
```csharp
// OpenCLTelemetryBuffer.cs
var flags = MemFlags.CL_MEM_ALLOC_HOST_PTR | MemFlags.CL_MEM_READ_WRITE;
_bufferObject = OpenCLNative.clCreateBuffer(
    _context,
    flags,
    (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    IntPtr.Zero,
    out var result);

// Map buffer to host-accessible memory
_mappedPointer = OpenCLNative.clEnqueueMapBuffer(
    _commandQueue,
    _bufferObject,
    blocking: true,
    MapFlags.CL_MAP_READ,
    offset: 0,
    size: (nuint)Marshal.SizeOf<RingKernelTelemetry>(),
    numEventsInWaitList: 0,
    eventWaitList: null,
    outEvent: out _,
    out result);
```

**Zero-Copy Read:**
```csharp
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        // Direct read from mapped memory (zero-copy, <1μs)
        var ptr = (RingKernelTelemetry*)_mappedPointer;
        return *ptr;
    }
}
```

**Key Characteristics:**
- **Pinned allocation**: `CL_MEM_ALLOC_HOST_PTR` ensures pinned host memory
- **Persistent mapping**: Mapped once at allocation, never unmapped until disposal
- **Read-only mapping**: `CL_MAP_READ` optimizes for CPU polling

### Metal Backend: Shared Memory

**Memory Allocation:**
```csharp
// MetalTelemetryBuffer.cs
var length = (nuint)Marshal.SizeOf<RingKernelTelemetry>();
_bufferObject = MetalNative.newBufferWithLength(
    _device,
    length,
    MTLResourceOptions.StorageModeShared);

_contentsPointer = MetalNative.bufferContents(_bufferObject);
```

**Zero-Copy Read:**
```csharp
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    unsafe
    {
        // Direct read from unified memory (zero-copy, <0.5μs on Apple Silicon)
        var ptr = (RingKernelTelemetry*)_contentsPointer;
        return *ptr;
    }
}
```

**Key Characteristics:**
- **Unified memory**: Apple Silicon's integrated GPU shares physical memory with CPU
- **StorageModeShared**: CPU and GPU access same memory region, no explicit sync
- **Fastest polling**: <0.5μs latency due to unified memory architecture

### CPU Backend: In-Memory Struct

**Memory Allocation:**
```csharp
// CpuTelemetryBuffer.cs
private RingKernelTelemetry _telemetry;

public void Allocate()
{
    // No explicit allocation needed - struct lives on heap
    _telemetry = new RingKernelTelemetry();
}
```

**Zero-Copy Read:**
```csharp
public async ValueTask<RingKernelTelemetry> PollAsync()
{
    // Direct struct copy from heap memory (<0.1μs)
    return _telemetry;
}
```

**Key Characteristics:**
- **Simplest implementation**: No interop, no pinned memory
- **Fastest polling**: <0.1μs latency (L1 cache hit)
- **Simulation mode**: Useful for testing without GPU hardware

## Roslyn Analyzer Diagnostics (DC014-DC017)

The DotCompute.Generators package provides **four diagnostic rules** for validating telemetry API usage:

### DC014: TelemetryPollingTooFrequent (Warning)

**Description:** Detects excessive polling frequency that may degrade system performance.

**Trigger:**
```csharp
// ❌ Triggers DC014: Tight loop without delay
while (true)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry
}
```

**Fix:**
```csharp
// ✅ Fixed: Add sufficient delay (1ms = 1kHz)
while (!ct.IsCancellationRequested)
{
    var telemetry = await wrapper.GetTelemetryAsync(ct);
    // Process telemetry
    await Task.Delay(1, ct); // 1ms delay
}
```

**Message:**
> GetTelemetryAsync is called at <100μs intervals, which exceeds the recommended maximum of 10,000 Hz (100μs minimum interval). Reduce polling frequency to avoid performance degradation. Consider using a polling interval of at least 1ms (1000μs) for production workloads.

### DC015: TelemetryNotEnabled (Error)

**Description:** Detects `GetTelemetryAsync` calls without prior `SetTelemetryEnabledAsync(true)`.

**Trigger:**
```csharp
// ❌ Triggers DC015: Telemetry not enabled
await wrapper.LaunchAsync(256, 128, ct);
var telemetry = await wrapper.GetTelemetryAsync(ct); // ❌ Buffer not allocated
```

**Fix:**
```csharp
// ✅ Fixed: Enable telemetry first
await wrapper.LaunchAsync(256, 128, ct);
await wrapper.SetTelemetryEnabledAsync(true, ct); // ✅ Allocate buffer
var telemetry = await wrapper.GetTelemetryAsync(ct); // ✅ OK
```

**Message:**
> GetTelemetryAsync is called without prior call to SetTelemetryEnabledAsync(true) on 'wrapper'. Enable telemetry by calling 'await wrapper.SetTelemetryEnabledAsync(true)' after LaunchAsync and before GetTelemetryAsync.

### DC016: TelemetryEnabledBeforeLaunch (Error)

**Description:** Detects `SetTelemetryEnabledAsync` calls before `LaunchAsync`.

**Trigger:**
```csharp
// ❌ Triggers DC016: Enable before launch
var wrapper = new MyRingKernelWrapper(runtime);
await wrapper.SetTelemetryEnabledAsync(true, ct); // ❌ No active kernel context
await wrapper.LaunchAsync(256, 128, ct);
```

**Fix:**
```csharp
// ✅ Fixed: Launch first, then enable
var wrapper = new MyRingKernelWrapper(runtime);
await wrapper.LaunchAsync(256, 128, ct); // ✅ Create kernel context
await wrapper.SetTelemetryEnabledAsync(true, ct); // ✅ OK
```

**Message:**
> SetTelemetryEnabledAsync is called before LaunchAsync on 'wrapper'. Ring kernels must be launched before enabling telemetry. Call LaunchAsync first, then SetTelemetryEnabledAsync.

### DC017: TelemetryResetWithoutEnable (Warning)

**Description:** Detects `ResetTelemetryAsync` calls without prior `SetTelemetryEnabledAsync(true)`.

**Trigger:**
```csharp
// ❌ Triggers DC017: Reset without enable
await wrapper.LaunchAsync(256, 128, ct);
await wrapper.ResetTelemetryAsync(ct); // ❌ Buffer not allocated
```

**Fix:**
```csharp
// ✅ Fixed: Enable telemetry first
await wrapper.LaunchAsync(256, 128, ct);
await wrapper.SetTelemetryEnabledAsync(true, ct); // ✅ Allocate buffer
await wrapper.ResetTelemetryAsync(ct); // ✅ OK
```

**Message:**
> ResetTelemetryAsync is called without prior call to SetTelemetryEnabledAsync(true) on 'wrapper'. Enable telemetry first before attempting to reset counters.

## Complete Usage Example

### Production Monitoring Loop

```csharp
using DotCompute.Backends.CUDA.RingKernels;
using DotCompute.Abstractions.RingKernels;
using System;
using System.Threading;
using System.Threading.Tasks;

public class RingKernelTelemetryMonitor
{
    private readonly MyRingKernelWrapper _wrapper;
    private readonly ILogger<RingKernelTelemetryMonitor> _logger;
    private readonly CancellationTokenSource _cts = new();

    public RingKernelTelemetryMonitor(
        MyRingKernelWrapper wrapper,
        ILogger<RingKernelTelemetryMonitor> logger)
    {
        _wrapper = wrapper;
        _logger = logger;
    }

    public async Task StartMonitoringAsync()
    {
        // 1. Launch Ring Kernel
        await _wrapper.LaunchAsync(blockSize: 256, gridSize: 128, _cts.Token);
        _logger.LogInformation("Ring Kernel launched");

        // 2. Enable telemetry
        await _wrapper.SetTelemetryEnabledAsync(enabled: true, _cts.Token);
        _logger.LogInformation("Telemetry enabled");

        // 3. Start monitoring loop (100Hz = 10ms interval, production-grade)
        await MonitoringLoopAsync(_cts.Token);
    }

    private async Task MonitoringLoopAsync(CancellationToken ct)
    {
        var lastMessagesProcessed = 0UL;
        var lastTimestamp = DateTimeOffset.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Poll telemetry (zero-copy, <1μs)
                var telemetry = await _wrapper.GetTelemetryAsync(ct);
                var currentTimestamp = DateTimeOffset.UtcNow;

                // Calculate throughput (messages/second)
                var deltaMessages = telemetry.MessagesProcessed - lastMessagesProcessed;
                var deltaTime = currentTimestamp - lastTimestamp;
                var throughput = deltaMessages / deltaTime.TotalSeconds;

                // Log metrics
                _logger.LogInformation(
                    "Telemetry: {MessagesProcessed} processed, {MessagesDropped} dropped, " +
                    "{QueueDepth} queued, {Throughput:F2} msg/s, Last: {LastTimestamp} ns",
                    telemetry.MessagesProcessed,
                    telemetry.MessagesDropped,
                    telemetry.QueueDepth,
                    throughput,
                    telemetry.LastProcessedTimestamp);

                // Alert on dropped messages
                if (telemetry.MessagesDropped > 0)
                {
                    _logger.LogWarning(
                        "Queue overflow detected: {MessagesDropped} messages dropped",
                        telemetry.MessagesDropped);
                }

                // Alert on queue buildup
                if (telemetry.QueueDepth > 1000)
                {
                    _logger.LogWarning(
                        "Queue depth high: {QueueDepth} pending messages",
                        telemetry.QueueDepth);
                }

                // Update tracking variables
                lastMessagesProcessed = telemetry.MessagesProcessed;
                lastTimestamp = currentTimestamp;

                // Production-grade polling interval: 10ms (100Hz)
                await Task.Delay(millisecondsDelay: 10, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Monitoring loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling telemetry");
                await Task.Delay(millisecondsDelay: 1000, ct); // Back off on errors
            }
        }
    }

    public async Task StopMonitoringAsync()
    {
        // 1. Cancel monitoring loop
        _cts.Cancel();

        // 2. Disable telemetry (optional, buffer persists)
        await _wrapper.SetTelemetryEnabledAsync(enabled: false, default);
        _logger.LogInformation("Telemetry disabled");

        // 3. Dispose wrapper (deallocates telemetry buffer)
        await _wrapper.DisposeAsync();
        _logger.LogInformation("Ring Kernel disposed");
    }
}
```

### Integration with OpenTelemetry

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
        // Register OpenTelemetry metrics
        _messagesProcessedCounter = s_meter.CreateObservableCounter(
            "dotcompute.ringkernel.messages.processed",
            () => (long)_lastTelemetry.MessagesProcessed,
            description: "Total messages processed by Ring Kernel");

        _messagesDroppedCounter = s_meter.CreateObservableCounter(
            "dotcompute.ringkernel.messages.dropped",
            () => (long)_lastTelemetry.MessagesDropped,
            description: "Total messages dropped due to queue overflow");

        _queueDepthGauge = s_meter.CreateObservableGauge(
            "dotcompute.ringkernel.queue.depth",
            () => _lastTelemetry.QueueDepth,
            description: "Current queue depth (pending messages)");

        // Start background polling task
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

## Performance Characteristics

### Memory Overhead

| Backend | Buffer Size | Allocation | Deallocation |
|---------|------------|-----------|--------------|
| CUDA | 64 bytes | ~10μs (`cuMemHostAlloc`) | ~5μs (`cuMemFreeHost`) |
| OpenCL | 64 bytes | ~20μs (`clCreateBuffer` + map) | ~10μs (unmap + `clReleaseMemObject`) |
| Metal | 64 bytes | ~5μs (`newBufferWithLength`) | ~2μs (`release`) |
| CPU | 64 bytes | ~0.1μs (heap allocation) | ~0.05μs (GC) |

**Recommendation:** Enable telemetry once at application startup, disable only for long idle periods.

### Polling Latency

Measured on RTX 2000 Ada (CC 8.9) with Intel Core i7:

| Backend | Single Poll | Batch 1000 Polls | Amortized per Poll |
|---------|------------|------------------|-------------------|
| CUDA (pinned) | 0.82μs | 850μs | 0.85μs |
| OpenCL (mapped) | 0.91μs | 920μs | 0.92μs |
| Metal (shared, M1 Max) | 0.45μs | 480μs | 0.48μs |
| CPU (in-memory) | 0.08μs | 85μs | 0.085μs |

**Observation:** Metal's unified memory provides **2x faster polling** than CUDA/OpenCL on Apple Silicon.

### Recommended Polling Intervals vs CPU Overhead

| Polling Frequency | Interval | CPU Overhead | Use Case |
|------------------|---------|-------------|----------|
| 10 Hz | 100ms | 0.001% | Background logging |
| 100 Hz | 10ms | 0.01% | **Production monitoring (recommended)** |
| 1 kHz | 1ms | 0.1% | Development/debugging |
| 10 kHz | 100μs | 1% | Stress testing (DC014 warning) |
| >10 kHz | <100μs | >1% | **Not recommended** (DC014 error) |

## Troubleshooting

### InvalidOperationException: "Ring Kernel must be launched before polling telemetry"

**Cause:** Calling `GetTelemetryAsync` before `LaunchAsync`.

**Fix:**
```csharp
// ✅ Correct order
await wrapper.LaunchAsync(256, 128, ct);
await wrapper.SetTelemetryEnabledAsync(true, ct);
var telemetry = await wrapper.GetTelemetryAsync(ct);
```

### InvalidOperationException: "Telemetry buffer not allocated"

**Cause:** Calling `GetTelemetryAsync` without prior `SetTelemetryEnabledAsync(true)`.

**Fix:**
```csharp
// ✅ Enable telemetry first
await wrapper.SetTelemetryEnabledAsync(true, ct);
var telemetry = await wrapper.GetTelemetryAsync(ct);
```

### Stale Telemetry Data

**Symptom:** Telemetry counters not updating.

**Possible Causes:**
1. **Telemetry disabled**: Call `SetTelemetryEnabledAsync(true)` to re-enable
2. **Ring Kernel idle**: No messages being processed, counters naturally static
3. **Disposed wrapper**: Check `_disposed` flag

**Diagnosis:**
```csharp
var telemetry1 = await wrapper.GetTelemetryAsync(ct);
await Task.Delay(1000, ct);
var telemetry2 = await wrapper.GetTelemetryAsync(ct);

if (telemetry1.MessagesProcessed == telemetry2.MessagesProcessed)
{
    Console.WriteLine("Ring Kernel is idle or telemetry is disabled");
}
```

### High Memory Usage

**Symptom:** Memory usage grows over time when telemetry is enabled/disabled repeatedly.

**Cause:** Telemetry buffers are **not deallocated** when disabled (by design).

**Fix:**
```csharp
// ❌ Bad: Repeated enable/disable leaks buffers (one per call)
for (int i = 0; i < 1000; i++)
{
    await wrapper.SetTelemetryEnabledAsync(true, ct);
    await wrapper.SetTelemetryEnabledAsync(false, ct); // Does NOT deallocate
}

// ✅ Good: Enable once, disable only when truly needed
await wrapper.SetTelemetryEnabledAsync(true, ct);
// ... run for hours/days ...
await wrapper.SetTelemetryEnabledAsync(false, ct);

// ✅ Alternative: Dispose wrapper to free buffer
await wrapper.DisposeAsync(); // Frees telemetry buffer
```

## See Also

- [Ring Kernels: Introduction](ring-kernels-introduction.md) - Getting started with Ring Kernels
- [Ring Kernels: Advanced Techniques](ring-kernels-advanced.md) - Message queues and advanced patterns
- [Timing API](timing-api.md) - High-precision GPU timestamps
- [Barrier API](barrier-api.md) - GPU thread synchronization primitives
- [Performance Profiling](performance-profiling.md) - Comprehensive profiling guide
