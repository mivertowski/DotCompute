# Crate Structure

## Workspace Organization

```
ringkernel/
├── Cargo.toml                    # Workspace manifest
├── README.md
├── LICENSE
│
├── crates/
│   ├── ringkernel/               # Main facade crate (re-exports)
│   │   ├── Cargo.toml
│   │   └── src/lib.rs
│   │
│   ├── ringkernel-core/          # Core traits and types
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── message.rs        # RingMessage trait
│   │       ├── queue.rs          # MessageQueue trait
│   │       ├── runtime.rs        # RingKernelRuntime trait
│   │       ├── context.rs        # RingContext struct
│   │       ├── control.rs        # ControlBlock struct
│   │       ├── telemetry.rs      # TelemetryBuffer struct
│   │       ├── hlc.rs            # HlcTimestamp
│   │       └── error.rs          # Error types
│   │
│   ├── ringkernel-derive/        # Proc macros
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── ring_kernel.rs    # #[ring_kernel] macro
│   │       └── ring_message.rs   # #[derive(RingMessage)] macro
│   │
│   ├── ringkernel-cuda/          # CUDA backend
│   │   ├── Cargo.toml
│   │   ├── build.rs              # CUDA compilation
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── runtime.rs        # CudaRingKernelRuntime
│   │       ├── compiler.rs       # PTX/CUBIN compilation
│   │       ├── queue.rs          # CudaMessageQueue
│   │       ├── bridge.rs         # Host↔GPU bridge
│   │       └── ffi.rs            # CUDA driver API bindings
│   │
│   ├── ringkernel-metal/         # Metal backend (macOS/iOS)
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── runtime.rs
│   │       ├── compiler.rs       # MSL compilation
│   │       └── queue.rs
│   │
│   ├── ringkernel-wgpu/          # WebGPU backend (cross-platform)
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── runtime.rs
│   │       ├── compiler.rs       # WGSL compilation
│   │       └── queue.rs
│   │
│   ├── ringkernel-cpu/           # CPU backend (testing/fallback)
│   │   ├── Cargo.toml
│   │   └── src/
│   │       ├── lib.rs
│   │       ├── runtime.rs
│   │       └── queue.rs
│   │
│   └── ringkernel-codegen/       # Kernel code generation
│       ├── Cargo.toml
│       └── src/
│           ├── lib.rs
│           ├── cuda.rs           # C# → CUDA C translation
│           ├── metal.rs          # → MSL translation
│           └── wgsl.rs           # → WGSL translation
│
├── examples/
│   ├── vector_add.rs
│   ├── pagerank.rs
│   └── multi_gpu.rs
│
├── benches/
│   ├── message_throughput.rs
│   └── serialization.rs
│
└── tests/
    ├── integration/
    └── hardware/
```

---

## Crate Dependencies

```
                    ┌──────────────────┐
                    │   ringkernel     │  (facade)
                    │   (re-exports)   │
                    └────────┬─────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
           ▼                 ▼                 ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │ ringkernel- │  │ ringkernel- │  │ ringkernel- │
    │    cuda     │  │   metal     │  │    wgpu     │
    └──────┬──────┘  └──────┬──────┘  └──────┬──────┘
           │                │                 │
           └────────────────┼─────────────────┘
                            │
                            ▼
                    ┌──────────────────┐
                    │ ringkernel-core  │
                    │ (traits, types)  │
                    └────────┬─────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
              ▼              ▼              ▼
       ┌───────────┐  ┌───────────┐  ┌───────────┐
       │ ringkernel│  │ ringkernel│  │   rkyv    │
       │  -derive  │  │ -codegen  │  │ (serde)   │
       └───────────┘  └───────────┘  └───────────┘
```

---

## Cargo.toml (Workspace Root)

```toml
[workspace]
resolver = "2"
members = [
    "crates/ringkernel",
    "crates/ringkernel-core",
    "crates/ringkernel-derive",
    "crates/ringkernel-cuda",
    "crates/ringkernel-metal",
    "crates/ringkernel-wgpu",
    "crates/ringkernel-cpu",
    "crates/ringkernel-codegen",
]

[workspace.package]
version = "0.1.0"
edition = "2021"
rust-version = "1.75"
license = "MIT OR Apache-2.0"
repository = "https://github.com/example/ringkernel"
keywords = ["gpu", "cuda", "actor", "hpc", "compute"]
categories = ["concurrency", "asynchronous", "science"]

[workspace.dependencies]
# Async runtime
tokio = { version = "1.35", features = ["rt-multi-thread", "sync", "macros"] }
async-trait = "0.1"
futures = "0.3"

# Serialization (zero-copy)
rkyv = { version = "0.7", features = ["validation", "strict"] }
zerocopy = { version = "0.7", features = ["derive"] }
bytemuck = { version = "1.14", features = ["derive"] }

# Error handling
thiserror = "1.0"
anyhow = "1.0"

# Logging
tracing = "0.1"
tracing-subscriber = "0.3"

# GPU backends
cudarc = { version = "0.10", optional = true }         # CUDA
metal = { version = "0.27", optional = true }          # Metal
wgpu = { version = "0.19", optional = true }           # WebGPU
ash = { version = "0.37", optional = true }            # Vulkan

# Proc macros
syn = { version = "2.0", features = ["full", "parsing"] }
quote = "1.0"
proc-macro2 = "1.0"

# Testing
criterion = "0.5"
proptest = "1.4"
```

---

## Feature Flags

```toml
# crates/ringkernel/Cargo.toml
[features]
default = ["cpu"]

# Backends
cpu = ["ringkernel-cpu"]
cuda = ["ringkernel-cuda"]
metal = ["ringkernel-metal"]
wgpu = ["ringkernel-wgpu"]

# All backends
full = ["cpu", "cuda", "metal", "wgpu"]

# Optional features
telemetry = []           # Real-time metrics
k2k-messaging = []       # Kernel-to-kernel communication
topic-pubsub = []        # Topic-based pub/sub
multi-gpu = ["cuda"]     # Multi-GPU coordination
```

---

## Platform-Specific Compilation

```toml
# crates/ringkernel-cuda/Cargo.toml
[target.'cfg(any(target_os = "linux", target_os = "windows"))'.dependencies]
cudarc = { workspace = true }

# crates/ringkernel-metal/Cargo.toml
[target.'cfg(any(target_os = "macos", target_os = "ios"))'.dependencies]
metal = { workspace = true }
```

---

## Build Scripts

### CUDA Backend (build.rs)

```rust
// crates/ringkernel-cuda/build.rs
fn main() {
    println!("cargo:rerun-if-changed=src/kernels/");

    // Find CUDA installation
    let cuda_path = std::env::var("CUDA_PATH")
        .unwrap_or_else(|_| "/usr/local/cuda".to_string());

    println!("cargo:rustc-link-search=native={}/lib64", cuda_path);
    println!("cargo:rustc-link-lib=cuda");
    println!("cargo:rustc-link-lib=nvrtc");

    // Compile PTX at build time (optional)
    #[cfg(feature = "precompile-ptx")]
    compile_ptx_kernels();
}
```

---

## Next: [Core Abstractions](./03-core-abstractions.md)
