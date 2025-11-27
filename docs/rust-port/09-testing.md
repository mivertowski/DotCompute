# Testing Strategy

## Test Categories

Following DotCompute's testing structure:

| Category | Location | GPU Required | Purpose |
|----------|----------|--------------|---------|
| Unit | `tests/unit/` | No | Component isolation |
| Integration | `tests/integration/` | Optional | Cross-component |
| Hardware | `tests/hardware/` | Yes | GPU-specific |
| Benchmarks | `benches/` | Yes | Performance |

---

## Unit Tests

### Message Serialization

```rust
#[cfg(test)]
mod tests {
    use super::*;
    use rkyv::{to_bytes, from_bytes};

    #[test]
    fn test_vector_add_request_serialization() {
        let request = VectorAddRequest {
            id: Uuid::new_v4(),
            priority: priority::NORMAL,
            correlation_id: None,
            a: vec![1.0, 2.0, 3.0],
            b: vec![4.0, 5.0, 6.0],
        };

        // Serialize
        let bytes = to_bytes::<_, 1024>(&request).unwrap();

        // Deserialize
        let archived = rkyv::check_archived_root::<VectorAddRequest>(&bytes).unwrap();

        assert_eq!(archived.a.len(), 3);
        assert_eq!(archived.b.len(), 3);
        assert_eq!(archived.priority, priority::NORMAL);
    }

    #[test]
    fn test_message_size_within_limits() {
        let request = VectorAddRequest {
            id: Uuid::new_v4(),
            priority: priority::NORMAL,
            correlation_id: None,
            a: vec![0.0; 10000],
            b: vec![0.0; 10000],
        };

        let bytes = to_bytes::<_, 262144>(&request).unwrap();
        assert!(bytes.len() <= MAX_MESSAGE_SIZE);
    }
}
```

### Control Block

```rust
#[test]
fn test_control_block_size_and_alignment() {
    assert_eq!(std::mem::size_of::<ControlBlock>(), 128);
    assert_eq!(std::mem::align_of::<ControlBlock>(), 128);
}

#[test]
fn test_control_block_field_offsets() {
    use std::mem::offset_of;

    assert_eq!(offset_of!(ControlBlock, is_active), 0);
    assert_eq!(offset_of!(ControlBlock, should_terminate), 4);
    assert_eq!(offset_of!(ControlBlock, has_terminated), 8);
    assert_eq!(offset_of!(ControlBlock, messages_processed), 16);
    assert_eq!(offset_of!(ControlBlock, input_queue_head_ptr), 32);
    assert_eq!(offset_of!(ControlBlock, output_queue_head_ptr), 64);
}
```

### HLC Timestamp

```rust
#[test]
fn test_hlc_ordering() {
    let ts1 = HlcTimestamp::new(100, 0, 1);
    let ts2 = HlcTimestamp::new(100, 1, 1);
    let ts3 = HlcTimestamp::new(101, 0, 1);

    assert!(ts1 < ts2);
    assert!(ts2 < ts3);
}

#[test]
fn test_hlc_merge() {
    let mut local = HlcTimestamp::new(100, 5, 1);
    let remote = HlcTimestamp::new(100, 10, 2);

    local.merge(&remote);

    assert_eq!(local.physical(), 100);
    assert_eq!(local.logical(), 11); // max(5, 10) + 1
}
```

---

## Integration Tests

### CPU Backend

```rust
#[tokio::test]
async fn test_cpu_kernel_lifecycle() {
    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cpu)
        .build()
        .await
        .unwrap();

    // Launch
    let kernel = runtime
        .launch("echo", Dim3::linear(1), Dim3::linear(1), LaunchOptions::default())
        .await
        .unwrap();

    // Activate
    kernel.activate().await.unwrap();
    assert_eq!(kernel.status().await.unwrap(), KernelStatus::Active);

    // Send/receive
    let request = EchoRequest { value: 42 };
    kernel.send(request).await.unwrap();

    let response: EchoResponse = kernel.receive(Duration::from_secs(1)).await.unwrap();
    assert_eq!(response.value, 42);

    // Terminate
    kernel.terminate().await.unwrap();
}

#[tokio::test]
async fn test_deactivate_preserves_state() {
    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cpu)
        .build()
        .await
        .unwrap();

    let kernel = runtime
        .launch("counter", Dim3::linear(1), Dim3::linear(1), LaunchOptions::default())
        .await
        .unwrap();

    kernel.activate().await.unwrap();

    // Process some messages
    for i in 0..10 {
        kernel.send(IncrementRequest { amount: 1 }).await.unwrap();
    }

    // Deactivate
    kernel.deactivate().await.unwrap();

    // Reactivate
    kernel.activate().await.unwrap();

    // State should be preserved
    let status: CounterStatus = kernel.call(GetStatusRequest {}).await.unwrap();
    assert_eq!(status.count, 10);

    kernel.terminate().await.unwrap();
}
```

---

## Hardware Tests

### CUDA Backend

```rust
/// Skip test if no CUDA GPU available
fn require_cuda() -> bool {
    CudaBackend::new().map(|b| b.device_count() > 0).unwrap_or(false)
}

#[tokio::test]
#[ignore = "requires CUDA GPU"]
async fn test_cuda_vector_add() {
    if !require_cuda() {
        eprintln!("Skipping: no CUDA GPU");
        return;
    }

    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cuda)
        .build()
        .await
        .unwrap();

    let kernel = runtime
        .launch(
            "vector_add",
            Dim3::linear(1),
            Dim3::linear(256),
            LaunchOptions::default(),
        )
        .await
        .unwrap();

    kernel.activate().await.unwrap();

    let request = VectorAddRequest {
        id: Uuid::new_v4(),
        priority: priority::NORMAL,
        correlation_id: None,
        a: vec![1.0, 2.0, 3.0, 4.0],
        b: vec![5.0, 6.0, 7.0, 8.0],
    };

    kernel.send(request).await.unwrap();

    let response: VectorAddResponse = kernel.receive(Duration::from_secs(5)).await.unwrap();

    assert_eq!(response.result, vec![6.0, 8.0, 10.0, 12.0]);

    kernel.terminate().await.unwrap();
}

#[tokio::test]
#[ignore = "requires CUDA GPU"]
async fn test_cuda_message_throughput() {
    if !require_cuda() {
        return;
    }

    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cuda)
        .build()
        .await
        .unwrap();

    let kernel = runtime
        .launch("echo", Dim3::linear(1), Dim3::linear(256), LaunchOptions {
            queue_capacity: 16384,
            ..Default::default()
        })
        .await
        .unwrap();

    kernel.activate().await.unwrap();

    let start = Instant::now();
    let message_count = 10000;

    for i in 0..message_count {
        kernel.send(EchoRequest { value: i }).await.unwrap();
    }

    for _ in 0..message_count {
        let _: EchoResponse = kernel.receive(Duration::from_secs(1)).await.unwrap();
    }

    let elapsed = start.elapsed();
    let throughput = message_count as f64 / elapsed.as_secs_f64();

    println!("Throughput: {:.0} messages/sec", throughput);
    assert!(throughput > 10000.0, "Expected >10k msg/s, got {}", throughput);

    kernel.terminate().await.unwrap();
}
```

### WSL2 Compatibility

```rust
#[tokio::test]
#[ignore = "requires CUDA GPU"]
async fn test_wsl2_event_driven_mode() {
    if !require_cuda() || !is_wsl2() {
        return;
    }

    // On WSL2, persistent mode doesn't work reliably
    // Test that event-driven mode works

    let runtime = RingKernelRuntime::builder()
        .backend(BackendType::Cuda)
        .config(RuntimeConfig {
            // Force event-driven mode
            ..Default::default()
        })
        .build()
        .await
        .unwrap();

    let kernel = runtime
        .launch("vector_add", Dim3::linear(1), Dim3::linear(256), LaunchOptions {
            mode: RingKernelMode::EventDriven,
            ..Default::default()
        })
        .await
        .unwrap();

    kernel.activate().await.unwrap();

    // Should work even on WSL2
    let request = VectorAddRequest {
        id: Uuid::new_v4(),
        priority: priority::NORMAL,
        correlation_id: None,
        a: vec![1.0, 2.0],
        b: vec![3.0, 4.0],
    };

    kernel.send(request).await.unwrap();

    let response: VectorAddResponse = kernel.receive(Duration::from_secs(10)).await.unwrap();
    assert_eq!(response.result, vec![4.0, 6.0]);

    kernel.terminate().await.unwrap();
}
```

---

## Property-Based Testing

```rust
use proptest::prelude::*;

proptest! {
    #[test]
    fn test_serialization_roundtrip(
        a in prop::collection::vec(any::<f32>(), 0..1000),
        b in prop::collection::vec(any::<f32>(), 0..1000),
    ) {
        let request = VectorAddRequest {
            id: Uuid::new_v4(),
            priority: priority::NORMAL,
            correlation_id: None,
            a: a.clone(),
            b: b.clone(),
        };

        let bytes = rkyv::to_bytes::<_, 262144>(&request).unwrap();
        let archived = rkyv::check_archived_root::<VectorAddRequest>(&bytes).unwrap();

        prop_assert_eq!(archived.a.as_slice(), a.as_slice());
        prop_assert_eq!(archived.b.as_slice(), b.as_slice());
    }

    #[test]
    fn test_hlc_always_advances(
        timestamps in prop::collection::vec((0u64..1000, 0u32..100, 0u32..10), 1..100)
    ) {
        let mut hlc = HlcTimestamp::new(0, 0, 0);

        for (physical, logical, node_id) in timestamps {
            let received = HlcTimestamp::new(physical, logical, node_id);
            let before = hlc.clone();
            hlc.merge(&received);

            // HLC must always advance
            prop_assert!(hlc > before || hlc == before);
        }
    }
}
```

---

## CI Configuration

```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - run: cargo test --lib --all-features

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - run: cargo test --test '*' --features cpu

  hardware-tests:
    runs-on: [self-hosted, gpu]
    steps:
      - uses: actions/checkout@v4
      - uses: dtolnay/rust-toolchain@stable
      - run: cargo test --test '*' --features cuda -- --ignored
```

---

## Next: [Performance Considerations](./10-performance.md)
