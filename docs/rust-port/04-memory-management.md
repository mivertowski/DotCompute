# Memory Management

## GPU Memory Challenges

Ring Kernels require careful memory management across CPU↔GPU boundaries:

1. **GPU Buffer Allocation/Deallocation**
2. **Pinned Host Memory** for DMA transfers
3. **Unified Memory** (where available)
4. **Memory Pools** for frequent allocations
5. **Cross-Device P2P** for multi-GPU

---

## Rust Ownership Model for GPU Memory

### GpuBuffer Wrapper

```rust
// crates/ringkernel-core/src/buffer.rs

use std::marker::PhantomData;
use std::ptr::NonNull;

/// Owned GPU memory buffer with RAII cleanup.
pub struct GpuBuffer<T: Copy + Send> {
    device_ptr: NonNull<T>,
    len: usize,
    capacity: usize,
    allocator: Box<dyn GpuAllocator>,
    _marker: PhantomData<T>,
}

// Safety: GPU memory is thread-safe when properly synchronized
unsafe impl<T: Copy + Send> Send for GpuBuffer<T> {}
unsafe impl<T: Copy + Send> Sync for GpuBuffer<T> {}

impl<T: Copy + Send> GpuBuffer<T> {
    /// Allocate new GPU buffer.
    pub fn new(len: usize, allocator: Box<dyn GpuAllocator>) -> Result<Self> {
        let size = len * std::mem::size_of::<T>();
        let device_ptr = allocator.allocate(size)?;

        Ok(Self {
            device_ptr: NonNull::new(device_ptr as *mut T)
                .ok_or(Error::AllocationFailed)?,
            len,
            capacity: len,
            allocator,
            _marker: PhantomData,
        })
    }

    /// Get raw device pointer.
    pub fn as_ptr(&self) -> *const T {
        self.device_ptr.as_ptr()
    }

    /// Get mutable device pointer.
    pub fn as_mut_ptr(&mut self) -> *mut T {
        self.device_ptr.as_ptr()
    }

    /// Length in elements.
    pub fn len(&self) -> usize {
        self.len
    }

    /// Size in bytes.
    pub fn size_bytes(&self) -> usize {
        self.len * std::mem::size_of::<T>()
    }
}

impl<T: Copy + Send> Drop for GpuBuffer<T> {
    fn drop(&mut self) {
        // Safety: we own this memory
        unsafe {
            let _ = self.allocator.free(self.device_ptr.as_ptr() as *mut u8);
        }
    }
}
```

---

## Pinned Host Memory

Required for efficient DMA transfers:

```rust
/// Pinned (page-locked) host memory for DMA transfers.
pub struct PinnedBuffer<T: Copy + Send> {
    host_ptr: NonNull<T>,
    len: usize,
    allocator: Box<dyn HostAllocator>,
    _marker: PhantomData<T>,
}

impl<T: Copy + Send> PinnedBuffer<T> {
    /// Allocate pinned memory.
    pub fn new(len: usize, allocator: Box<dyn HostAllocator>) -> Result<Self> {
        let size = len * std::mem::size_of::<T>();
        let host_ptr = allocator.allocate_pinned(size)?;

        Ok(Self {
            host_ptr: NonNull::new(host_ptr as *mut T)
                .ok_or(Error::AllocationFailed)?,
            len,
            allocator,
            _marker: PhantomData,
        })
    }

    /// Get slice for reading.
    pub fn as_slice(&self) -> &[T] {
        unsafe { std::slice::from_raw_parts(self.host_ptr.as_ptr(), self.len) }
    }

    /// Get mutable slice.
    pub fn as_mut_slice(&mut self) -> &mut [T] {
        unsafe { std::slice::from_raw_parts_mut(self.host_ptr.as_ptr(), self.len) }
    }
}

impl<T: Copy + Send> Drop for PinnedBuffer<T> {
    fn drop(&mut self) {
        unsafe {
            let _ = self.allocator.free_pinned(self.host_ptr.as_ptr() as *mut u8);
        }
    }
}
```

---

## Memory Pool for Ring Buffers

Reduces allocation overhead for message queues:

```rust
/// Thread-safe memory pool for GPU allocations.
pub struct GpuMemoryPool {
    allocator: Arc<dyn GpuAllocator>,
    pools: Mutex<HashMap<usize, Vec<NonNull<u8>>>>,
    stats: AtomicPoolStats,
}

impl GpuMemoryPool {
    /// Get a buffer from the pool or allocate new.
    pub fn acquire(&self, size: usize) -> Result<PooledBuffer> {
        let bucket_size = size.next_power_of_two();

        let mut pools = self.pools.lock().unwrap();
        if let Some(pool) = pools.get_mut(&bucket_size) {
            if let Some(ptr) = pool.pop() {
                self.stats.hits.fetch_add(1, Ordering::Relaxed);
                return Ok(PooledBuffer::from_pool(ptr, bucket_size, self));
            }
        }

        // Allocate new
        self.stats.misses.fetch_add(1, Ordering::Relaxed);
        let ptr = self.allocator.allocate(bucket_size)?;
        Ok(PooledBuffer::new(
            NonNull::new(ptr).ok_or(Error::AllocationFailed)?,
            bucket_size,
            self,
        ))
    }

    /// Return buffer to pool.
    pub fn release(&self, ptr: NonNull<u8>, size: usize) {
        let mut pools = self.pools.lock().unwrap();
        pools.entry(size).or_default().push(ptr);
        self.stats.returns.fetch_add(1, Ordering::Relaxed);
    }
}

/// RAII handle that returns to pool on drop.
pub struct PooledBuffer<'a> {
    ptr: NonNull<u8>,
    size: usize,
    pool: &'a GpuMemoryPool,
}

impl Drop for PooledBuffer<'_> {
    fn drop(&mut self) {
        self.pool.release(self.ptr, self.size);
    }
}
```

---

## Unified Memory (CUDA Managed Memory)

```rust
/// Unified memory accessible from CPU and GPU.
pub struct UnifiedBuffer<T: Copy + Send> {
    ptr: NonNull<T>,
    len: usize,
    allocator: Box<dyn UnifiedAllocator>,
    _marker: PhantomData<T>,
}

impl<T: Copy + Send> UnifiedBuffer<T> {
    pub fn new(len: usize, allocator: Box<dyn UnifiedAllocator>) -> Result<Self> {
        let ptr = allocator.allocate_managed(len * std::mem::size_of::<T>())?;
        Ok(Self {
            ptr: NonNull::new(ptr as *mut T).ok_or(Error::AllocationFailed)?,
            len,
            allocator,
            _marker: PhantomData,
        })
    }

    /// Access from CPU (may trigger page migration).
    pub fn as_slice(&self) -> &[T] {
        unsafe { std::slice::from_raw_parts(self.ptr.as_ptr(), self.len) }
    }

    /// Prefetch to GPU.
    pub fn prefetch_to_device(&self, device_id: i32) -> Result<()> {
        self.allocator.prefetch(self.ptr.as_ptr() as *const u8, self.len, device_id)
    }
}
```

---

## WSL2 Memory Visibility Issues

**Critical**: WSL2 has limited GPU memory coherence (see CLAUDE.md).

```rust
/// Memory visibility strategy for different platforms.
#[derive(Debug, Clone, Copy)]
pub enum MemoryVisibility {
    /// Full system-scope atomics (native Linux)
    SystemScope,

    /// Device-scope only, requires explicit sync (WSL2)
    DeviceScope,

    /// Explicit DMA transfers only
    ExplicitDma,
}

impl MemoryVisibility {
    /// Detect platform capabilities.
    pub fn detect() -> Self {
        #[cfg(target_os = "linux")]
        {
            if is_wsl2() {
                return Self::DeviceScope; // WSL2 limitation
            }
            Self::SystemScope
        }

        #[cfg(target_os = "windows")]
        {
            Self::SystemScope // Native Windows CUDA works
        }

        #[cfg(target_os = "macos")]
        {
            Self::DeviceScope // Metal doesn't have system atomics
        }
    }
}

fn is_wsl2() -> bool {
    std::fs::read_to_string("/proc/version")
        .map(|v| v.contains("microsoft") || v.contains("WSL"))
        .unwrap_or(false)
}
```

---

## Memory Transfer Strategies

```rust
/// Strategy for host↔device data transfer.
pub enum TransferStrategy {
    /// Synchronous copy (blocking)
    Sync,

    /// Async copy with stream
    Async { stream: StreamHandle },

    /// Zero-copy via unified memory
    Unified,

    /// Explicit DMA with polling
    DmaPolling { poll_interval_us: u32 },
}

/// Bridge for host↔GPU ring buffer synchronization.
pub struct RingBufferBridge<T: RingMessage> {
    host_buffer: PinnedBuffer<T>,
    gpu_buffer: GpuBuffer<T>,
    strategy: TransferStrategy,

    // Transfer tracking
    host_to_gpu_count: AtomicU64,
    gpu_to_host_count: AtomicU64,
}

impl<T: RingMessage> RingBufferBridge<T> {
    /// Transfer pending messages from host to GPU.
    pub async fn flush_to_device(&mut self) -> Result<usize> {
        match &self.strategy {
            TransferStrategy::Sync => {
                self.sync_copy_to_device()
            }
            TransferStrategy::Async { stream } => {
                self.async_copy_to_device(stream).await
            }
            TransferStrategy::Unified => {
                // No-op, unified memory handles it
                Ok(0)
            }
            TransferStrategy::DmaPolling { poll_interval_us } => {
                self.dma_poll_to_device(*poll_interval_us).await
            }
        }
    }
}
```

---

## Cache-Line Alignment

Critical for GPU performance:

```rust
/// Ensure cache-line alignment for GPU structures.
#[repr(C, align(128))]
pub struct CacheLineAligned<T> {
    value: T,
}

/// Control block must be 128-byte aligned (dual cache line).
#[repr(C, align(128))]
pub struct ControlBlock {
    // ... fields (see 01-architecture-overview.md)
}

/// Telemetry buffer is 64-byte aligned (single cache line).
#[repr(C, align(64))]
pub struct TelemetryBuffer {
    // ... fields
}

// Compile-time size assertions
const _: () = assert!(std::mem::size_of::<ControlBlock>() == 128);
const _: () = assert!(std::mem::size_of::<TelemetryBuffer>() == 64);
```

---

## Next: [GPU Backends](./05-gpu-backends.md)
