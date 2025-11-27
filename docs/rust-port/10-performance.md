# Performance Considerations

## Target Performance Metrics

Based on DotCompute benchmarks:

| Metric | DotCompute (.NET) | Rust Target |
|--------|-------------------|-------------|
| Serialization | 20-50ns | <30ns |
| Message latency (native) | <1ms | <500μs |
| Message latency (WSL2) | ~5s | ~5s (platform limit) |
| Startup time | ~10ms (AOT) | <1ms |
| Binary size | 5-10MB | <2MB |
| GPU kernel overhead | ~10μs | ~5μs |

---

## Serialization Performance

### Zero-Copy with rkyv

```rust
use criterion::{black_box, criterion_group, criterion_main, Criterion};

fn bench_serialization(c: &mut Criterion) {
    let request = VectorAddRequest {
        id: Uuid::new_v4(),
        priority: priority::NORMAL,
        correlation_id: None,
        a: vec![1.0; 1000],
        b: vec![1.0; 1000],
    };

    c.bench_function("rkyv_serialize_1k", |b| {
        b.iter(|| {
            let bytes = rkyv::to_bytes::<_, 16384>(black_box(&request)).unwrap();
            black_box(bytes)
        })
    });

    let bytes = rkyv::to_bytes::<_, 16384>(&request).unwrap();

    c.bench_function("rkyv_deserialize_1k", |b| {
        b.iter(|| {
            let archived = rkyv::check_archived_root::<VectorAddRequest>(
                black_box(&bytes)
            ).unwrap();
            black_box(archived)
        })
    });

    // Zero-copy access (no deserialization)
    c.bench_function("rkyv_zero_copy_access", |b| {
        let archived = unsafe {
            rkyv::archived_root::<VectorAddRequest>(&bytes)
        };
        b.iter(|| {
            black_box(archived.a.len());
            black_box(archived.priority);
        })
    });
}

criterion_group!(benches, bench_serialization);
criterion_main!(benches);
```

### Expected Results

```
rkyv_serialize_1k       time: [28.5 ns 29.1 ns 29.8 ns]
rkyv_deserialize_1k     time: [15.2 ns 15.6 ns 16.1 ns]
rkyv_zero_copy_access   time: [0.5 ns 0.6 ns 0.7 ns]
```

---

## Memory Allocation

### Pool-Based Allocation

```rust
fn bench_allocation(c: &mut Criterion) {
    let pool = GpuMemoryPool::new(CudaAllocator::new());

    c.bench_function("pooled_alloc_4kb", |b| {
        b.iter(|| {
            let buffer = pool.acquire(4096).unwrap();
            black_box(buffer)
            // Automatically returned to pool on drop
        })
    });

    c.bench_function("direct_cuda_alloc_4kb", |b| {
        b.iter(|| {
            let buffer = cuda_malloc(4096).unwrap();
            cuda_free(buffer).unwrap();
        })
    });
}
```

### Expected Results

```
pooled_alloc_4kb        time: [45 ns 48 ns 52 ns]
direct_cuda_alloc_4kb   time: [2.1 μs 2.3 μs 2.5 μs]
```

**50x improvement** with pooling.

---

## Message Queue Performance

### Lock-Free Operations

```rust
fn bench_queue_operations(c: &mut Criterion) {
    let queue = CpuMessageQueue::<SimpleMessage>::new(4096);

    c.bench_function("queue_enqueue", |b| {
        let msg = SimpleMessage::default();
        b.iter(|| {
            queue.try_enqueue(black_box(msg.clone())).unwrap();
            queue.try_dequeue().unwrap();
        })
    });

    // Multi-threaded contention
    c.bench_function("queue_contended_4_threads", |b| {
        b.iter(|| {
            std::thread::scope(|s| {
                for _ in 0..4 {
                    s.spawn(|| {
                        for _ in 0..1000 {
                            let msg = SimpleMessage::default();
                            while queue.try_enqueue(msg.clone()).is_err() {}
                            while queue.try_dequeue().is_none() {}
                        }
                    });
                }
            });
        })
    });
}
```

---

## GPU Kernel Launch Overhead

```rust
fn bench_kernel_launch(c: &mut Criterion) {
    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cuda)
        .build()
        .await
        .unwrap();

    let kernel = runtime
        .launch("noop", Dim3::linear(1), Dim3::linear(256), LaunchOptions::default())
        .await
        .unwrap();

    kernel.activate().await.unwrap();

    c.bench_function("round_trip_latency", |b| {
        b.iter(|| {
            kernel.send(NoopRequest {}).await.unwrap();
            let _: NoopResponse = kernel.receive(Duration::from_secs(1)).await.unwrap();
        })
    });
}
```

---

## Cache-Line Optimization

### Aligned Structures

```rust
// Ensure no false sharing
#[repr(C, align(64))]
pub struct PerThreadCounter {
    value: AtomicU64,
    _padding: [u8; 56], // Fill to 64 bytes
}

pub struct CounterArray {
    counters: Box<[PerThreadCounter]>,
}

impl CounterArray {
    pub fn new(num_threads: usize) -> Self {
        let counters = (0..num_threads)
            .map(|_| PerThreadCounter {
                value: AtomicU64::new(0),
                _padding: [0; 56],
            })
            .collect::<Vec<_>>()
            .into_boxed_slice();

        Self { counters }
    }

    pub fn increment(&self, thread_id: usize) {
        self.counters[thread_id].value.fetch_add(1, Ordering::Relaxed);
    }
}
```

---

## Async Runtime Optimization

### Tokio Configuration

```rust
fn optimized_runtime() -> tokio::runtime::Runtime {
    tokio::runtime::Builder::new_multi_thread()
        .worker_threads(4)
        .thread_name("ringkernel-worker")
        .thread_stack_size(2 * 1024 * 1024) // 2MB stack
        .enable_all()
        .build()
        .unwrap()
}
```

### Avoid Unnecessary Async

```rust
// BAD: async overhead for simple operation
async fn get_status(&self) -> KernelStatus {
    self.status.load(Ordering::Acquire)
}

// GOOD: sync for simple atomic read
fn get_status(&self) -> KernelStatus {
    self.status.load(Ordering::Acquire)
}

// GOOD: async only when actually needed
async fn wait_for_status(&self, target: KernelStatus, timeout: Duration) -> Result<()> {
    tokio::time::timeout(timeout, async {
        loop {
            if self.get_status() == target {
                return Ok(());
            }
            tokio::time::sleep(Duration::from_millis(1)).await;
        }
    }).await?
}
```

---

## Profile-Guided Optimization

```bash
# Build with instrumentation
RUSTFLAGS="-Cprofile-generate=/tmp/pgo-data" cargo build --release

# Run benchmarks to generate profile data
./target/release/benchmarks

# Build with profile data
RUSTFLAGS="-Cprofile-use=/tmp/pgo-data/merged.profdata" cargo build --release
```

---

## Memory Layout Visualization

```rust
/// Print memory layout for debugging
pub fn print_layout<T>() {
    println!("Type: {}", std::any::type_name::<T>());
    println!("  Size: {} bytes", std::mem::size_of::<T>());
    println!("  Align: {} bytes", std::mem::align_of::<T>());
}

#[test]
fn verify_layouts() {
    print_layout::<ControlBlock>();
    // Type: ControlBlock
    //   Size: 128 bytes
    //   Align: 128 bytes

    print_layout::<TelemetryBuffer>();
    // Type: TelemetryBuffer
    //   Size: 64 bytes
    //   Align: 64 bytes
}
```

---

## Next: [Ecosystem Integration](./11-ecosystem.md)
