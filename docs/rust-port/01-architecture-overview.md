# Architecture Overview

## DotCompute Ring Kernel Architecture

The Ring Kernel system implements a **GPU-native actor model** with persistent state. Here's the component mapping from .NET to Rust:

## Component Mapping

| DotCompute Component | Rust Equivalent | Purpose |
|---------------------|-----------------|---------|
| `IRingKernelRuntime` | `trait RingKernelRuntime` | Kernel lifecycle management |
| `IRingKernelMessage` | `trait RingMessage` | Type-safe message protocol |
| `IMessageQueue<T>` | `trait MessageQueue<T>` | Lock-free ring buffer |
| `RingKernelContext` | `struct RingContext` | GPU intrinsics facade |
| `RingKernelControlBlock` | `#[repr(C)] struct ControlBlock` | GPU-resident state (128 bytes) |
| `HlcTimestamp` | `struct HlcTimestamp` | Hybrid Logical Clock |
| `MemoryPackSerializer` | `rkyv` / `zerocopy` derive | Zero-copy serialization |

---

## System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HOST (CPU) SIDE                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────┐    ┌──────────────────┐    ┌───────────────────┐   │
│  │  Application    │───▶│ RingKernelRuntime│───▶│  Message Bridge   │   │
│  │  (async/await)  │    │  (lifecycle mgmt)│    │  (Host↔GPU DMA)   │   │
│  └─────────────────┘    └──────────────────┘    └─────────┬─────────┘   │
│                                │                          │              │
│                         ┌──────┴──────┐                   │              │
│                         ▼             ▼                   ▼              │
│                 ┌───────────┐  ┌───────────┐    ┌─────────────────┐     │
│                 │  Launch   │  │ Terminate │    │  Serialization  │     │
│                 │  Options  │  │  Handler  │    │  (rkyv/zerocopy)│     │
│                 └───────────┘  └───────────┘    └─────────────────┘     │
│                                                                          │
├──────────────────────────────────PCIe────────────────────────────────────┤
│                                                                          │
│                         DEVICE (GPU) SIDE                                │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    PERSISTENT KERNEL                             │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │    │
│  │  │ Control     │  │ Input Queue │  │ Message Handler         │  │    │
│  │  │ Block       │  │ (lock-free) │  │ (user-defined logic)    │  │    │
│  │  │ (128 bytes) │  │             │  │                         │  │    │
│  │  │ - is_active │  │ head ──────▶│  │ process(ctx, msg) {     │  │    │
│  │  │ - terminate │  │ tail ◀──────│  │   ctx.sync_threads();   │  │    │
│  │  │ - msg_count │  │ buffer[]    │  │   // GPU computation    │  │    │
│  │  │ - errors    │  │             │  │   ctx.enqueue_output(); │  │    │
│  │  └─────────────┘  └─────────────┘  │ }                       │  │    │
│  │                                     └─────────────────────────┘  │    │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │    │
│  │  │ Telemetry   │  │Output Queue │  │ K2K Messaging           │  │    │
│  │  │ Buffer      │  │ (lock-free) │  │ (kernel-to-kernel)      │  │    │
│  │  │ (64 bytes)  │  │             │  │                         │  │    │
│  │  │ - processed │  │ head ◀──────│  │ send_to_kernel("other") │  │    │
│  │  │ - latency   │  │ tail ──────▶│  │ recv_from_kernel()      │  │    │
│  │  │ - errors    │  │ buffer[]    │  │                         │  │    │
│  │  └─────────────┘  └─────────────┘  └─────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Kernel Lifecycle State Machine

```
                    ┌─────────────┐
                    │   Created   │
                    └──────┬──────┘
                           │ launch()
                           ▼
                    ┌─────────────┐
         ┌─────────│  Launched   │─────────┐
         │         │  (inactive) │         │
         │         └──────┬──────┘         │
         │                │ activate()     │
         │                ▼                │
         │         ┌─────────────┐         │
         │    ┌───▶│   Active    │◀───┐    │
         │    │    │ (processing)│    │    │
         │    │    └──────┬──────┘    │    │
         │    │           │           │    │
         │ terminate()    │      reactivate()
         │    │           │           │    │
         │    │    deactivate()       │    │
         │    │           │           │    │
         │    │           ▼           │    │
         │    │    ┌─────────────┐    │    │
         │    └────│  Deactivated│────┘    │
         │         │  (paused)   │         │
         │         └──────┬──────┘         │
         │                │ terminate()    │
         │                ▼                │
         │         ┌─────────────┐         │
         └────────▶│ Terminating │◀────────┘
                   │  (cleanup)  │
                   └──────┬──────┘
                          │
                          ▼
                   ┌─────────────┐
                   │ Terminated  │
                   │ (resources  │
                   │  freed)     │
                   └─────────────┘
```

---

## Message Flow

### Host → GPU (Input)

```
1. Application calls kernel.send(message)
2. Message serialized via rkyv (zero-copy)
3. Bridge copies to pinned host buffer
4. DMA transfer to GPU input queue
5. Kernel dequeues and processes
```

### GPU → Host (Output)

```
1. Kernel calls ctx.enqueue_output(response)
2. Message written to GPU output queue
3. Bridge polls/DMA copies to host
4. Deserialized via rkyv
5. Future resolved, application receives response
```

### GPU → GPU (K2K Messaging)

```
1. Kernel A calls ctx.send_to_kernel("B", msg)
2. Routing table lookup (O(1) hash)
3. Message copied to Kernel B's K2K queue
4. Kernel B calls ctx.try_receive_from_kernel("A")
5. Direct GPU memory access (no PCIe)
```

---

## Memory Layout Requirements

### Control Block (128 bytes, cache-line aligned)

```rust
#[repr(C, align(128))]
pub struct ControlBlock {
    // Flags (offset 0-15)
    pub is_active: AtomicI32,           // 0: inactive, 1: processing
    pub should_terminate: AtomicI32,    // 0: run, 1: shutdown
    pub has_terminated: AtomicI32,      // 0: running, 1: done, 2: relaunchable
    pub errors_encountered: AtomicI32,

    // Counters (offset 16-31)
    pub messages_processed: AtomicI64,
    pub last_activity_ticks: AtomicI64,

    // Input queue descriptors (offset 32-63)
    pub input_queue_head_ptr: u64,      // Device pointer
    pub input_queue_tail_ptr: u64,
    pub input_queue_buffer_ptr: u64,
    pub input_queue_capacity: i32,
    pub input_queue_message_size: i32,

    // Output queue descriptors (offset 64-95)
    pub output_queue_head_ptr: u64,
    pub output_queue_tail_ptr: u64,
    pub output_queue_buffer_ptr: u64,
    pub output_queue_capacity: i32,
    pub output_queue_message_size: i32,

    // Reserved (offset 96-127)
    _reserved: [u64; 4],
}
```

### Telemetry Buffer (64 bytes, cache-line aligned)

```rust
#[repr(C, align(64))]
pub struct TelemetryBuffer {
    pub messages_processed: AtomicU64,
    pub messages_dropped: AtomicU64,
    pub last_processed_timestamp: AtomicI64,
    pub queue_depth: AtomicI32,
    pub total_latency_nanos: AtomicU64,
    pub max_latency_nanos: AtomicU64,
    pub min_latency_nanos: AtomicU64,
    pub error_code: AtomicI32,
}
```

---

## Backend Abstraction

```rust
pub trait GpuBackend: Send + Sync {
    type Buffer: GpuBuffer;
    type Stream: GpuStream;
    type Module: GpuModule;
    type Function: GpuFunction;

    fn name(&self) -> &'static str;
    fn device_count(&self) -> usize;

    async fn allocate(&self, size: usize) -> Result<Self::Buffer>;
    async fn copy_to_device(&self, src: &[u8], dst: &Self::Buffer) -> Result<()>;
    async fn copy_to_host(&self, src: &Self::Buffer, dst: &mut [u8]) -> Result<()>;

    async fn compile(&self, source: &str, options: &CompileOptions) -> Result<Self::Module>;
    async fn launch(&self, func: &Self::Function, grid: Dim3, block: Dim3, args: &[KernelArg]) -> Result<()>;
}
```

---

## Next: [Crate Structure](./02-crate-structure.md)
