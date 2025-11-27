# Migration Guide

## Overview

This guide helps developers migrate from DotCompute (.NET) to the Rust Ring Kernel implementation.

---

## Type Mapping

### Core Types

| DotCompute (C#) | Rust | Notes |
|-----------------|------|-------|
| `IRingKernelRuntime` | `trait RingKernelRuntime` | Async trait |
| `IRingKernelMessage` | `trait RingMessage` | With `#[derive(RingMessage)]` |
| `IMessageQueue<T>` | `trait MessageQueue<T>` | Generic over message type |
| `RingKernelContext` | `struct RingContext<'a>` | Lifetime for control block |
| `RingKernelControlBlock` | `struct ControlBlock` | `#[repr(C, align(128))]` |
| `RingKernelTelemetry` | `struct TelemetryBuffer` | `#[repr(C, align(64))]` |
| `HlcTimestamp` | `struct HlcTimestamp` | Same semantics |
| `Guid` | `uuid::Uuid` | Use `uuid` crate |
| `Span<byte>` | `&[u8]` / `&mut [u8]` | Native slices |
| `ReadOnlySpan<byte>` | `&[u8]` | Immutable slice |

### Primitive Types

| C# | Rust | Notes |
|----|------|-------|
| `int` | `i32` | Signed 32-bit |
| `long` | `i64` | Signed 64-bit |
| `uint` | `u32` | Unsigned 32-bit |
| `ulong` | `u64` | Unsigned 64-bit |
| `float` | `f32` | IEEE 754 |
| `double` | `f64` | IEEE 754 |
| `bool` | `bool` | Same |
| `byte` | `u8` | Unsigned byte |

---

## Attribute Migration

### Message Definition

**DotCompute (C#):**
```csharp
[MemoryPackable]
public partial class VectorAddRequest : IRingKernelMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string MessageType => "VectorAddRequest";
    public byte Priority { get; set; } = 128;
    public Guid? CorrelationId { get; set; }

    public float[] A { get; set; } = Array.Empty<float>();
    public float[] B { get; set; } = Array.Empty<float>();

    public int PayloadSize => /* calculated */;
    public ReadOnlySpan<byte> Serialize() => /* generated */;
    public void Deserialize(ReadOnlySpan<byte> data) { /* generated */ }
}
```

**Rust:**
```rust
use ringkernel::prelude::*;
use rkyv::{Archive, Serialize, Deserialize};

#[derive(RingMessage, Archive, Serialize, Deserialize)]
pub struct VectorAddRequest {
    #[message(id)]
    pub id: Uuid,

    #[message(priority = "priority::NORMAL")]
    pub priority: u8,

    #[message(correlation)]
    pub correlation_id: Option<Uuid>,

    pub a: Vec<f32>,
    pub b: Vec<f32>,
}
```

### Kernel Definition

**DotCompute (C#):**
```csharp
[RingKernel(KernelId = "vector_add")]
public static VectorAddResponse Process(VectorAddRequest req, RingKernelContext ctx)
{
    ctx.SyncThreads();

    var idx = ctx.GlobalThreadId;
    if (idx < req.A.Length)
    {
        var result = req.A[idx] + req.B[idx];
        // ...
    }

    return new VectorAddResponse { /* ... */ };
}
```

**Rust:**
```rust
#[ring_kernel(id = "vector_add")]
pub fn process(ctx: &mut RingContext, req: VectorAddRequest) -> VectorAddResponse {
    ctx.sync_threads();

    let idx = ctx.global_thread_id() as usize;
    if idx < req.a.len() {
        let result = req.a[idx] + req.b[idx];
        // ...
    }

    VectorAddResponse { /* ... */ }
}
```

---

## API Migration

### Runtime Creation

**DotCompute:**
```csharp
var services = new ServiceCollection();
services.AddRingKernelRuntime(options => {
    options.Backend = BackendType.Cuda;
    options.QueueCapacity = 4096;
});

var provider = services.BuildServiceProvider();
var runtime = provider.GetRequiredService<IRingKernelRuntime>();
```

**Rust:**
```rust
let runtime = RingKernelRuntime::builder()
    .backend(BackendType::Cuda)
    .config(RuntimeConfig {
        default_queue_capacity: 4096,
        ..Default::default()
    })
    .build()
    .await?;
```

### Kernel Lifecycle

**DotCompute:**
```csharp
await runtime.LaunchAsync("vector_add", gridSize, blockSize, options);
await runtime.ActivateAsync("vector_add");
await runtime.SendMessageAsync("vector_add", request);
var response = await runtime.ReceiveMessageAsync<VectorAddResponse>("vector_add", timeout);
await runtime.TerminateAsync("vector_add");
```

**Rust:**
```rust
let kernel = runtime.launch("vector_add", grid_size, block_size, options).await?;
kernel.activate().await?;
kernel.send(request).await?;
let response: VectorAddResponse = kernel.receive(timeout).await?;
kernel.terminate().await?;
```

### Context Methods

| DotCompute | Rust | Notes |
|------------|------|-------|
| `ctx.SyncThreads()` | `ctx.sync_threads()` | snake_case |
| `ctx.SyncGrid()` | `ctx.sync_grid()` | |
| `ctx.SyncWarp(mask)` | `ctx.sync_warp(mask)` | |
| `ctx.Now()` | `ctx.now()` | Returns `HlcTimestamp` |
| `ctx.Tick()` | `ctx.tick()` | |
| `ctx.ThreadFence()` | `ctx.thread_fence()` | |
| `ctx.AtomicAdd(ref x, v)` | `ctx.atomic_add(&mut x, v)` | |
| `ctx.SendToKernel(id, msg)` | `ctx.send_to_kernel(id, msg)` | |
| `ctx.EnqueueOutput(msg)` | `ctx.enqueue_output(msg)` | |
| `ctx.ThreadId` | `ctx.thread_id()` | Method, not property |
| `ctx.BlockId` | `ctx.block_id()` | |
| `ctx.GlobalThreadId` | `ctx.global_thread_id()` | |

---

## Async/Await Differences

**DotCompute (C#):**
```csharp
// Task-based async
public async Task ProcessAsync(CancellationToken ct)
{
    await runtime.LaunchAsync("kernel", grid, block, options, ct);
    await runtime.ActivateAsync("kernel", ct);

    try
    {
        var response = await runtime.ReceiveMessageAsync<Response>("kernel", timeout, ct);
    }
    catch (OperationCanceledException)
    {
        // Handle cancellation
    }
}
```

**Rust:**
```rust
// tokio-based async
pub async fn process() -> Result<()> {
    let kernel = runtime.launch("kernel", grid, block, options).await?;
    kernel.activate().await?;

    // With timeout
    match tokio::time::timeout(timeout, kernel.receive::<Response>()).await {
        Ok(Ok(response)) => { /* success */ }
        Ok(Err(e)) => { /* receive error */ }
        Err(_) => { /* timeout */ }
    }

    Ok(())
}

// With cancellation token equivalent
pub async fn process_with_cancel(cancel: CancellationToken) -> Result<()> {
    tokio::select! {
        result = kernel.receive::<Response>() => {
            let response = result?;
        }
        _ = cancel.cancelled() => {
            // Handle cancellation
        }
    }
    Ok(())
}
```

---

## Error Handling

**DotCompute (C#):**
```csharp
try
{
    await runtime.SendMessageAsync("kernel", message);
}
catch (QueueFullException)
{
    // Handle backpressure
}
catch (KernelNotFoundException)
{
    // Handle missing kernel
}
catch (SerializationException)
{
    // Handle serialization error
}
```

**Rust:**
```rust
match runtime.send("kernel", message).await {
    Ok(()) => { /* success */ }
    Err(Error::QueueFull(_)) => { /* backpressure */ }
    Err(Error::KernelNotFound(_)) => { /* missing kernel */ }
    Err(Error::Serialization(_)) => { /* serialization error */ }
    Err(e) => { /* other error */ }
}

// Or with ? operator
runtime.send("kernel", message).await?;
```

---

## Memory Management

**DotCompute (C#):**
```csharp
// IDisposable pattern
using var buffer = new UnifiedBuffer<float>(1024);

// Span for zero-copy
Span<float> data = buffer.AsSpan();
```

**Rust:**
```rust
// RAII - automatic cleanup on drop
let buffer = UnifiedBuffer::<f32>::new(1024, allocator)?;

// Slice for zero-copy
let data: &mut [f32] = buffer.as_mut_slice();

// Buffer automatically freed when `buffer` goes out of scope
```

---

## Testing Migration

**DotCompute (xUnit):**
```csharp
[Fact]
public async Task VectorAdd_ReturnsCorrectResult()
{
    var runtime = CreateTestRuntime();
    await runtime.LaunchAsync("vector_add", ...);
    await runtime.ActivateAsync("vector_add");

    var request = new VectorAddRequest { A = [1, 2], B = [3, 4] };
    await runtime.SendMessageAsync("vector_add", request);

    var response = await runtime.ReceiveMessageAsync<VectorAddResponse>("vector_add", TimeSpan.FromSeconds(5));

    Assert.Equal([4, 6], response.Result);
}

[SkippableFact]
public async Task CudaKernel_RequiresGpu()
{
    Skip.IfNot(CudaRuntime.IsAvailable);
    // ...
}
```

**Rust (tokio-test):**
```rust
#[tokio::test]
async fn vector_add_returns_correct_result() {
    let runtime = create_test_runtime().await;
    let kernel = runtime.launch("vector_add", ...).await.unwrap();
    kernel.activate().await.unwrap();

    let request = VectorAddRequest { a: vec![1.0, 2.0], b: vec![3.0, 4.0], ..Default::default() };
    kernel.send(request).await.unwrap();

    let response: VectorAddResponse = kernel.receive(Duration::from_secs(5)).await.unwrap();

    assert_eq!(response.result, vec![4.0, 6.0]);
}

#[tokio::test]
#[ignore = "requires CUDA GPU"]
async fn cuda_kernel_requires_gpu() {
    if !cuda_available() {
        eprintln!("Skipping: no CUDA GPU");
        return;
    }
    // ...
}
```

---

## Configuration Migration

**DotCompute (appsettings.json):**
```json
{
  "RingKernel": {
    "Backend": "Cuda",
    "DeviceId": 0,
    "QueueCapacity": 4096,
    "EnableTelemetry": true
  }
}
```

**Rust (config.toml):**
```toml
[ringkernel]
backend = "cuda"
device_id = 0
queue_capacity = 4096
telemetry_enabled = true
```

---

## Common Pitfalls

### 1. Lifetime Issues

```rust
// WRONG: dangling reference
fn get_context<'a>() -> RingContext<'a> {
    let control = ControlBlock::default();
    RingContext::new(&control) // `control` dropped here!
}

// RIGHT: return owned or use Arc
fn get_context(control: Arc<ControlBlock>) -> RingContext {
    RingContext::new(control)
}
```

### 2. Async in Sync Context

```rust
// WRONG: blocking in async
async fn process() {
    std::thread::sleep(Duration::from_secs(1)); // Blocks executor!
}

// RIGHT: use async sleep
async fn process() {
    tokio::time::sleep(Duration::from_secs(1)).await;
}
```

### 3. Missing Send/Sync Bounds

```rust
// WRONG: won't compile for async
struct MyMessage {
    data: Rc<Vec<f32>>, // Rc is not Send!
}

// RIGHT: use Arc for shared ownership
struct MyMessage {
    data: Arc<Vec<f32>>,
}
```

---

## Migration Checklist

- [ ] Replace `[MemoryPackable]` with `#[derive(Archive, Serialize, Deserialize)]`
- [ ] Replace `IRingKernelMessage` with `#[derive(RingMessage)]`
- [ ] Replace `[RingKernel]` with `#[ring_kernel]`
- [ ] Convert `async Task` to `async fn` with `Result<T>`
- [ ] Replace `IDisposable` with RAII (automatic Drop)
- [ ] Convert `Span<T>` to `&[T]` / `&mut [T]`
- [ ] Replace dependency injection with builder pattern
- [ ] Update exception handling to `Result<T, Error>` with `?`
- [ ] Convert xUnit tests to `#[tokio::test]`
- [ ] Update configuration format (JSON → TOML/YAML)

---

## Getting Help

- **Documentation**: https://docs.rs/ringkernel
- **Examples**: `ringkernel/examples/`
- **Issues**: https://github.com/example/ringkernel/issues
- **Discord**: #ringkernel channel

---

## Conclusion

The Rust port maintains API compatibility while leveraging Rust's safety guarantees:

| Aspect | DotCompute | Rust Port |
|--------|------------|-----------|
| Memory safety | Runtime checks | Compile-time |
| Async model | Task-based | Future-based |
| Serialization | MemoryPack | rkyv (zero-copy) |
| Error handling | Exceptions | Result types |
| Resource cleanup | IDisposable | RAII (Drop) |
| Configuration | DI container | Builder pattern |

The core Ring Kernel concepts—persistent GPU actors, HLC timestamps, K2K messaging—remain identical.
