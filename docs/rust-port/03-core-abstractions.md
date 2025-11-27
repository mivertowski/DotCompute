# Core Abstractions

## Mapping DotCompute Types to Rust

This document details the core traits and types that form the Ring Kernel abstraction layer.

---

## 1. RingMessage Trait

**DotCompute**: `IRingKernelMessage`

```rust
// crates/ringkernel-core/src/message.rs

use std::any::TypeId;

/// Marker trait for messages that can be sent between ring kernels.
///
/// Implementations are typically derived using `#[derive(RingMessage)]`.
pub trait RingMessage: Send + Sync + Sized + 'static {
    /// Unique message type identifier for routing/deserialization.
    const MESSAGE_TYPE: &'static str;

    /// Returns the message ID (for tracking/deduplication).
    fn message_id(&self) -> Uuid;

    /// Sets the message ID.
    fn set_message_id(&mut self, id: Uuid);

    /// Priority level (0-255, higher = more urgent).
    fn priority(&self) -> u8;

    /// Optional correlation ID for request/response patterns.
    fn correlation_id(&self) -> Option<Uuid>;

    /// Serialized size in bytes.
    fn payload_size(&self) -> usize;
}

/// Priority levels following DotCompute conventions.
pub mod priority {
    pub const LOW: u8 = 32;        // 0-63: Batch processing
    pub const NORMAL: u8 = 128;    // 64-127: Default
    pub const HIGH: u8 = 160;      // 128-191: Interactive
    pub const CRITICAL: u8 = 224;  // 192-255: System messages
}
```

### Derive Macro Usage

```rust
use ringkernel::prelude::*;
use rkyv::{Archive, Serialize, Deserialize};

#[derive(RingMessage, Archive, Serialize, Deserialize)]
#[rkyv(compare(PartialEq))]
pub struct VectorAddRequest {
    #[message(id)]
    pub id: Uuid,

    #[message(priority = "priority::NORMAL")]
    pub priority: u8,

    #[message(correlation)]
    pub correlation_id: Option<Uuid>,

    // Payload fields
    pub a: Vec<f32>,
    pub b: Vec<f32>,
}
```

---

## 2. MessageQueue Trait

**DotCompute**: `IMessageQueue<T>`

```rust
// crates/ringkernel-core/src/queue.rs

use std::future::Future;

/// Lock-free ring buffer for message passing.
pub trait MessageQueue<T: RingMessage>: Send + Sync {
    /// Maximum capacity (must be power of 2).
    fn capacity(&self) -> usize;

    /// Current message count.
    fn len(&self) -> usize;

    /// Check if queue is empty.
    fn is_empty(&self) -> bool { self.len() == 0 }

    /// Check if queue is full.
    fn is_full(&self) -> bool { self.len() >= self.capacity() - 1 }

    /// Non-blocking enqueue attempt.
    fn try_enqueue(&self, message: T) -> Result<(), QueueError<T>>;

    /// Non-blocking dequeue attempt.
    fn try_dequeue(&self) -> Option<T>;

    /// Blocking enqueue with timeout.
    fn enqueue(&self, message: T, timeout: Duration) -> impl Future<Output = Result<(), QueueError<T>>> + Send;

    /// Blocking dequeue with timeout.
    fn dequeue(&self, timeout: Duration) -> impl Future<Output = Result<T, QueueError<T>>> + Send;

    /// Get queue statistics.
    fn statistics(&self) -> QueueStatistics;
}

/// Queue operation errors.
#[derive(Debug, thiserror::Error)]
pub enum QueueError<T> {
    #[error("Queue is full")]
    Full(T),

    #[error("Queue is empty")]
    Empty,

    #[error("Operation timed out")]
    Timeout,

    #[error("Queue has been closed")]
    Closed,
}

/// Queue performance statistics.
#[derive(Debug, Clone, Default)]
pub struct QueueStatistics {
    pub total_enqueued: u64,
    pub total_dequeued: u64,
    pub total_dropped: u64,
    pub average_latency_ns: u64,
    pub peak_depth: usize,
}
```

---

## 3. RingKernelRuntime Trait

**DotCompute**: `IRingKernelRuntime`

```rust
// crates/ringkernel-core/src/runtime.rs

use async_trait::async_trait;

/// Runtime for managing ring kernel lifecycles.
#[async_trait]
pub trait RingKernelRuntime: Send + Sync {
    /// Backend identifier (e.g., "cuda", "metal", "wgpu").
    fn backend_name(&self) -> &'static str;

    /// Launch a kernel (initially inactive).
    async fn launch(
        &self,
        kernel_id: &str,
        grid_size: Dim3,
        block_size: Dim3,
        options: LaunchOptions,
    ) -> Result<KernelHandle>;

    /// Activate a launched kernel (begin processing).
    async fn activate(&self, kernel_id: &str) -> Result<()>;

    /// Deactivate a kernel (pause, preserve state).
    async fn deactivate(&self, kernel_id: &str) -> Result<()>;

    /// Terminate a kernel (cleanup resources).
    async fn terminate(&self, kernel_id: &str) -> Result<()>;

    /// Send a message to a kernel.
    async fn send<T: RingMessage>(&self, kernel_id: &str, message: T) -> Result<()>;

    /// Receive a message from a kernel.
    async fn receive<T: RingMessage>(&self, kernel_id: &str, timeout: Duration) -> Result<T>;

    /// Get kernel status.
    async fn status(&self, kernel_id: &str) -> Result<KernelStatus>;

    /// Get kernel metrics.
    async fn metrics(&self, kernel_id: &str) -> Result<KernelMetrics>;

    /// Get real-time telemetry (<1μs latency).
    async fn telemetry(&self, kernel_id: &str) -> Result<TelemetrySnapshot>;

    /// List all managed kernels.
    async fn list_kernels(&self) -> Result<Vec<String>>;
}

/// Grid/block dimensions.
#[derive(Debug, Clone, Copy, Default)]
pub struct Dim3 {
    pub x: u32,
    pub y: u32,
    pub z: u32,
}

impl Dim3 {
    pub fn new(x: u32, y: u32, z: u32) -> Self {
        Self { x, y, z }
    }

    pub fn linear(size: u32) -> Self {
        Self { x: size, y: 1, z: 1 }
    }
}
```

---

## 4. RingContext Struct

**DotCompute**: `RingKernelContext` (ref struct)

```rust
// crates/ringkernel-core/src/context.rs

/// Runtime context passed to ring kernel handlers.
///
/// Methods translate to GPU intrinsics during compilation.
pub struct RingContext<'a> {
    // Internal pointers (populated by runtime)
    control_block: &'a ControlBlock,
    input_queue: *const u8,
    output_queue: *mut u8,
    hlc: &'a mut HlcState,
}

impl<'a> RingContext<'a> {
    // ═══════════════════════════════════════════════════════════════
    // Thread Identity
    // ═══════════════════════════════════════════════════════════════

    /// Thread index within block (→ threadIdx.x)
    #[inline(always)]
    pub fn thread_id(&self) -> u32 { /* intrinsic */ 0 }

    /// Block index within grid (→ blockIdx.x)
    #[inline(always)]
    pub fn block_id(&self) -> u32 { /* intrinsic */ 0 }

    /// Warp index (thread_id / 32)
    #[inline(always)]
    pub fn warp_id(&self) -> u32 { self.thread_id() / 32 }

    /// Lane within warp (thread_id % 32)
    #[inline(always)]
    pub fn lane_id(&self) -> u32 { self.thread_id() % 32 }

    /// Global thread index (→ blockIdx.x * blockDim.x + threadIdx.x)
    #[inline(always)]
    pub fn global_thread_id(&self) -> u32 { /* intrinsic */ 0 }

    // ═══════════════════════════════════════════════════════════════
    // Synchronization Barriers
    // ═══════════════════════════════════════════════════════════════

    /// Block-level barrier (→ __syncthreads())
    #[inline(always)]
    pub fn sync_threads(&self) { /* intrinsic */ }

    /// Grid-level barrier (→ cooperative_groups::grid::sync())
    #[inline(always)]
    pub fn sync_grid(&self) { /* intrinsic */ }

    /// Warp-level barrier (→ __syncwarp(mask))
    #[inline(always)]
    pub fn sync_warp(&self, mask: u32) { /* intrinsic */ }

    /// Named barrier for cross-kernel sync
    pub fn named_barrier(&self, name: &str) { /* runtime */ }

    // ═══════════════════════════════════════════════════════════════
    // Temporal Operations (HLC)
    // ═══════════════════════════════════════════════════════════════

    /// Get current HLC timestamp (→ clock64() + logical)
    #[inline(always)]
    pub fn now(&self) -> HlcTimestamp { /* intrinsic */ HlcTimestamp::default() }

    /// Advance local clock (local event)
    #[inline(always)]
    pub fn tick(&mut self) { /* intrinsic */ }

    /// Merge received timestamp (causal ordering)
    #[inline(always)]
    pub fn update_clock(&mut self, received: HlcTimestamp) { /* intrinsic */ }

    // ═══════════════════════════════════════════════════════════════
    // Memory Ordering
    // ═══════════════════════════════════════════════════════════════

    /// Device-scope fence (→ __threadfence())
    #[inline(always)]
    pub fn thread_fence(&self) { /* intrinsic */ }

    /// Block-scope fence (→ __threadfence_block())
    #[inline(always)]
    pub fn thread_fence_block(&self) { /* intrinsic */ }

    /// System-scope fence (→ __threadfence_system())
    #[inline(always)]
    pub fn thread_fence_system(&self) { /* intrinsic */ }

    // ═══════════════════════════════════════════════════════════════
    // Atomic Operations
    // ═══════════════════════════════════════════════════════════════

    /// Atomic add (→ atomicAdd)
    #[inline(always)]
    pub fn atomic_add(&self, target: &mut i32, value: i32) -> i32 { /* intrinsic */ 0 }

    /// Atomic CAS (→ atomicCAS)
    #[inline(always)]
    pub fn atomic_cas(&self, target: &mut i32, compare: i32, value: i32) -> i32 { /* intrinsic */ 0 }

    /// Atomic exchange (→ atomicExch)
    #[inline(always)]
    pub fn atomic_exch(&self, target: &mut i32, value: i32) -> i32 { /* intrinsic */ 0 }

    // ═══════════════════════════════════════════════════════════════
    // Warp Primitives
    // ═══════════════════════════════════════════════════════════════

    /// Warp shuffle (→ __shfl_sync)
    #[inline(always)]
    pub fn warp_shuffle(&self, value: i32, src_lane: u32, mask: u32) -> i32 { /* intrinsic */ 0 }

    /// Warp ballot (→ __ballot_sync)
    #[inline(always)]
    pub fn warp_ballot(&self, predicate: bool, mask: u32) -> u32 { /* intrinsic */ 0 }

    // ═══════════════════════════════════════════════════════════════
    // Queue Operations
    // ═══════════════════════════════════════════════════════════════

    /// Enqueue output message
    pub fn enqueue_output<T: RingMessage>(&mut self, message: T) -> bool { false }

    /// Check output queue capacity
    pub fn output_queue_free_slots(&self) -> usize { 0 }

    // ═══════════════════════════════════════════════════════════════
    // K2K Messaging
    // ═══════════════════════════════════════════════════════════════

    /// Send to another kernel
    pub fn send_to_kernel<T: RingMessage>(&mut self, target: &str, message: T) -> bool { false }

    /// Receive from another kernel
    pub fn try_receive_from_kernel<T: RingMessage>(&mut self, source: &str) -> Option<T> { None }

    // ═══════════════════════════════════════════════════════════════
    // Control
    // ═══════════════════════════════════════════════════════════════

    /// Request graceful termination
    pub fn request_termination(&self) { /* write control block */ }

    /// Check if termination requested
    pub fn is_termination_requested(&self) -> bool { false }

    /// Report an error
    pub fn report_error(&self) { /* atomic increment */ }
}
```

---

## 5. HlcTimestamp

**DotCompute**: `HlcTimestamp` (Hybrid Logical Clock)

```rust
// crates/ringkernel-core/src/hlc.rs

use std::cmp::Ordering;

/// Hybrid Logical Clock timestamp for causal ordering.
///
/// Combines physical time (wall clock) with logical counter
/// to provide total ordering even when physical clocks disagree.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(C)]
pub struct HlcTimestamp {
    /// Physical time component (nanoseconds since epoch or GPU ticks)
    physical: u64,

    /// Logical counter for events at same physical time
    logical: u32,

    /// Node/kernel identifier (for tie-breaking)
    node_id: u32,
}

impl HlcTimestamp {
    /// Create a new timestamp.
    pub fn new(physical: u64, logical: u32, node_id: u32) -> Self {
        Self { physical, logical, node_id }
    }

    /// Get physical time component.
    pub fn physical(&self) -> u64 { self.physical }

    /// Get logical counter.
    pub fn logical(&self) -> u32 { self.logical }

    /// Advance for local event (tick).
    pub fn tick(&mut self) {
        self.logical = self.logical.wrapping_add(1);
    }

    /// Merge with received timestamp (max + 1).
    pub fn merge(&mut self, other: &HlcTimestamp) {
        if other.physical > self.physical {
            self.physical = other.physical;
            self.logical = other.logical + 1;
        } else if other.physical == self.physical {
            self.logical = self.logical.max(other.logical) + 1;
        } else {
            self.logical += 1;
        }
    }

    /// Update physical time if wall clock advanced.
    pub fn update_physical(&mut self, wall_clock: u64) {
        if wall_clock > self.physical {
            self.physical = wall_clock;
            self.logical = 0;
        }
    }
}

impl Ord for HlcTimestamp {
    fn cmp(&self, other: &Self) -> Ordering {
        match self.physical.cmp(&other.physical) {
            Ordering::Equal => match self.logical.cmp(&other.logical) {
                Ordering::Equal => self.node_id.cmp(&other.node_id),
                other => other,
            },
            other => other,
        }
    }
}

impl PartialOrd for HlcTimestamp {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}
```

---

## 6. ControlBlock and TelemetryBuffer

See [Architecture Overview](./01-architecture-overview.md) for the full struct definitions.

---

## Next: [Memory Management](./04-memory-management.md)
