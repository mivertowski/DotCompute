# GPU Backends

## Backend Trait Hierarchy

```rust
// crates/ringkernel-core/src/backend.rs

use async_trait::async_trait;

/// Core GPU backend abstraction.
#[async_trait]
pub trait GpuBackend: Send + Sync + 'static {
    /// Backend identifier.
    fn name(&self) -> &'static str;

    /// Number of available devices.
    fn device_count(&self) -> usize;

    /// Get device properties.
    fn device_info(&self, device_id: usize) -> Result<DeviceInfo>;

    /// Create a new execution context.
    async fn create_context(&self, device_id: usize) -> Result<Box<dyn GpuContext>>;
}

/// GPU execution context (one per device).
#[async_trait]
pub trait GpuContext: Send + Sync {
    /// Allocate device memory.
    async fn allocate(&self, size: usize) -> Result<DevicePtr>;

    /// Free device memory.
    async fn free(&self, ptr: DevicePtr) -> Result<()>;

    /// Copy host → device.
    async fn copy_to_device(&self, src: &[u8], dst: DevicePtr) -> Result<()>;

    /// Copy device → host.
    async fn copy_to_host(&self, src: DevicePtr, dst: &mut [u8]) -> Result<()>;

    /// Copy device → device.
    async fn copy_device_to_device(&self, src: DevicePtr, dst: DevicePtr, size: usize) -> Result<()>;

    /// Compile kernel source.
    async fn compile(&self, source: &str, options: &CompileOptions) -> Result<Box<dyn CompiledKernel>>;

    /// Create execution stream.
    fn create_stream(&self) -> Result<Box<dyn GpuStream>>;

    /// Synchronize all pending operations.
    async fn synchronize(&self) -> Result<()>;
}

/// Compiled GPU kernel.
#[async_trait]
pub trait CompiledKernel: Send + Sync {
    /// Get kernel function by name.
    fn get_function(&self, name: &str) -> Result<Box<dyn KernelFunction>>;
}

/// Executable kernel function.
#[async_trait]
pub trait KernelFunction: Send + Sync {
    /// Launch kernel.
    async fn launch(
        &self,
        grid: Dim3,
        block: Dim3,
        shared_mem: usize,
        stream: &dyn GpuStream,
        args: &[KernelArg],
    ) -> Result<()>;
}
```

---

## CUDA Backend

### Implementation with `cudarc`

```rust
// crates/ringkernel-cuda/src/lib.rs

use cudarc::driver::{CudaDevice, CudaSlice, LaunchAsync, LaunchConfig};
use cudarc::nvrtc::Ptx;

pub struct CudaBackend {
    devices: Vec<Arc<CudaDevice>>,
}

impl CudaBackend {
    pub fn new() -> Result<Self> {
        let device_count = CudaDevice::count()? as usize;
        let devices = (0..device_count)
            .map(|i| CudaDevice::new(i))
            .collect::<Result<Vec<_>, _>>()?;

        Ok(Self {
            devices: devices.into_iter().map(Arc::new).collect(),
        })
    }
}

#[async_trait]
impl GpuBackend for CudaBackend {
    fn name(&self) -> &'static str { "cuda" }

    fn device_count(&self) -> usize { self.devices.len() }

    fn device_info(&self, device_id: usize) -> Result<DeviceInfo> {
        let dev = &self.devices[device_id];
        Ok(DeviceInfo {
            name: dev.name()?,
            compute_capability: dev.compute_capability(),
            total_memory: dev.total_memory()?,
            // ...
        })
    }

    async fn create_context(&self, device_id: usize) -> Result<Box<dyn GpuContext>> {
        Ok(Box::new(CudaContext {
            device: self.devices[device_id].clone(),
        }))
    }
}

pub struct CudaContext {
    device: Arc<CudaDevice>,
}

#[async_trait]
impl GpuContext for CudaContext {
    async fn allocate(&self, size: usize) -> Result<DevicePtr> {
        let slice: CudaSlice<u8> = self.device.alloc_zeros(size)?;
        Ok(DevicePtr::from_cuda(slice))
    }

    async fn compile(&self, source: &str, options: &CompileOptions) -> Result<Box<dyn CompiledKernel>> {
        let ptx = Ptx::from_src(source)?;
        let module = self.device.load_ptx(ptx, "ring_kernel", &[])?;
        Ok(Box::new(CudaCompiledKernel { module }))
    }

    // ... other methods
}
```

### CUDA Ring Kernel Template

```cuda
// Generated CUDA C for ring kernels
#include <cuda_runtime.h>
#include <cooperative_groups.h>
#include <cuda/atomic>

namespace cg = cooperative_groups;

struct ControlBlock {
    cuda::atomic<int> is_active;
    cuda::atomic<int> should_terminate;
    cuda::atomic<int> has_terminated;
    cuda::atomic<int> errors_encountered;
    cuda::atomic<long long> messages_processed;
    // ... queue pointers
};

template<typename TInput, typename TOutput>
__global__ void ring_kernel_persistent(
    ControlBlock* control,
    TInput* input_buffer,
    cuda::atomic<int>* input_head,
    cuda::atomic<int>* input_tail,
    TOutput* output_buffer,
    cuda::atomic<int>* output_head,
    cuda::atomic<int>* output_tail,
    int queue_capacity
) {
    auto grid = cg::this_grid();

    // Persistent kernel loop
    while (!control->should_terminate.load(cuda::memory_order_acquire)) {
        // Check if active
        if (!control->is_active.load(cuda::memory_order_acquire)) {
            // Yield to avoid busy-waiting
            __nanosleep(1000);
            continue;
        }

        // Try dequeue input
        int head = input_head->load(cuda::memory_order_relaxed);
        int tail = input_tail->load(cuda::memory_order_acquire);

        if (head != tail) {
            // Claim slot
            int next_head = (head + 1) & (queue_capacity - 1);
            if (input_head->compare_exchange_strong(head, next_head,
                    cuda::memory_order_acq_rel)) {

                TInput msg = input_buffer[head];

                // === USER HANDLER CODE ===
                TOutput response = process_message(msg);
                // =========================

                // Enqueue output
                enqueue_output(output_buffer, output_head, output_tail,
                              queue_capacity, response);

                control->messages_processed.fetch_add(1, cuda::memory_order_relaxed);
            }
        }

        grid.sync(); // Cooperative sync
    }

    // Mark terminated
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        control->has_terminated.store(1, cuda::memory_order_release);
    }
}
```

---

## Metal Backend

### Implementation with `metal-rs`

```rust
// crates/ringkernel-metal/src/lib.rs

use metal::{Device, Library, CommandQueue, Buffer, MTLResourceOptions};

pub struct MetalBackend {
    device: Device,
    command_queue: CommandQueue,
}

impl MetalBackend {
    pub fn new() -> Result<Self> {
        let device = Device::system_default()
            .ok_or(Error::NoDevice)?;
        let command_queue = device.new_command_queue();

        Ok(Self { device, command_queue })
    }
}

#[async_trait]
impl GpuBackend for MetalBackend {
    fn name(&self) -> &'static str { "metal" }

    fn device_count(&self) -> usize { 1 } // Metal typically has one GPU

    async fn create_context(&self, _device_id: usize) -> Result<Box<dyn GpuContext>> {
        Ok(Box::new(MetalContext {
            device: self.device.clone(),
            queue: self.command_queue.clone(),
        }))
    }
}

pub struct MetalContext {
    device: Device,
    queue: CommandQueue,
}

#[async_trait]
impl GpuContext for MetalContext {
    async fn allocate(&self, size: usize) -> Result<DevicePtr> {
        let buffer = self.device.new_buffer(
            size as u64,
            MTLResourceOptions::StorageModeShared,
        );
        Ok(DevicePtr::from_metal(buffer))
    }

    async fn compile(&self, source: &str, _options: &CompileOptions) -> Result<Box<dyn CompiledKernel>> {
        let library = self.device.new_library_with_source(source, &Default::default())?;
        Ok(Box::new(MetalCompiledKernel { library }))
    }
}
```

### Metal Shading Language Template

```metal
// Generated MSL for ring kernels
#include <metal_stdlib>
#include <metal_atomic>
using namespace metal;

struct ControlBlock {
    atomic_int is_active;
    atomic_int should_terminate;
    atomic_int has_terminated;
    atomic_int errors_encountered;
    atomic_long messages_processed;
    // ... queue pointers
};

kernel void ring_kernel_persistent(
    device ControlBlock* control [[buffer(0)]],
    device InputMessage* input_buffer [[buffer(1)]],
    device atomic_int* input_head [[buffer(2)]],
    device atomic_int* input_tail [[buffer(3)]],
    device OutputMessage* output_buffer [[buffer(4)]],
    device atomic_int* output_head [[buffer(5)]],
    device atomic_int* output_tail [[buffer(6)]],
    constant int& queue_capacity [[buffer(7)]],
    uint tid [[thread_position_in_grid]]
) {
    // Note: Metal doesn't support true persistent kernels
    // Must use event-driven relaunch pattern

    int head = atomic_load_explicit(input_head, memory_order_relaxed);
    int tail = atomic_load_explicit(input_tail, memory_order_acquire);

    if (head == tail) return; // No messages

    // Process messages in batch
    while (head != tail) {
        int next_head = (head + 1) & (queue_capacity - 1);
        if (atomic_compare_exchange_weak_explicit(
                input_head, &head, next_head,
                memory_order_acq_rel, memory_order_relaxed)) {

            InputMessage msg = input_buffer[head];
            OutputMessage response = process_message(msg);
            enqueue_output(output_buffer, output_head, output_tail,
                          queue_capacity, response);

            atomic_fetch_add_explicit(&control->messages_processed, 1,
                                      memory_order_relaxed);
        }
        head = atomic_load_explicit(input_head, memory_order_relaxed);
        tail = atomic_load_explicit(input_tail, memory_order_acquire);
    }
}
```

---

## WebGPU Backend

### Implementation with `wgpu`

```rust
// crates/ringkernel-wgpu/src/lib.rs

use wgpu::{Device, Queue, ShaderModule, ComputePipeline};

pub struct WgpuBackend {
    instance: wgpu::Instance,
    adapters: Vec<wgpu::Adapter>,
}

impl WgpuBackend {
    pub async fn new() -> Result<Self> {
        let instance = wgpu::Instance::new(wgpu::InstanceDescriptor {
            backends: wgpu::Backends::all(),
            ..Default::default()
        });

        let adapters = instance.enumerate_adapters(wgpu::Backends::all())
            .collect();

        Ok(Self { instance, adapters })
    }
}

#[async_trait]
impl GpuBackend for WgpuBackend {
    fn name(&self) -> &'static str { "wgpu" }

    fn device_count(&self) -> usize { self.adapters.len() }

    async fn create_context(&self, device_id: usize) -> Result<Box<dyn GpuContext>> {
        let adapter = &self.adapters[device_id];
        let (device, queue) = adapter.request_device(
            &wgpu::DeviceDescriptor::default(),
            None,
        ).await?;

        Ok(Box::new(WgpuContext { device, queue }))
    }
}
```

### WGSL Template

```wgsl
// Generated WGSL for ring kernels
struct ControlBlock {
    is_active: atomic<i32>,
    should_terminate: atomic<i32>,
    has_terminated: atomic<i32>,
    errors_encountered: atomic<i32>,
    messages_processed: atomic<i32>, // WGSL doesn't have 64-bit atomics
}

@group(0) @binding(0) var<storage, read_write> control: ControlBlock;
@group(0) @binding(1) var<storage, read_write> input_buffer: array<InputMessage>;
@group(0) @binding(2) var<storage, read_write> input_head: atomic<i32>;
@group(0) @binding(3) var<storage, read_write> input_tail: atomic<i32>;
@group(0) @binding(4) var<storage, read_write> output_buffer: array<OutputMessage>;

@compute @workgroup_size(256)
fn ring_kernel(@builtin(global_invocation_id) gid: vec3<u32>) {
    let head = atomicLoad(&input_head);
    let tail = atomicLoad(&input_tail);

    if (head == tail) { return; }

    // Process message
    // ...
}
```

---

## Backend Selection

```rust
/// Automatic backend selection based on platform.
pub fn select_backend() -> Result<Box<dyn GpuBackend>> {
    #[cfg(feature = "cuda")]
    if let Ok(backend) = CudaBackend::new() {
        if backend.device_count() > 0 {
            return Ok(Box::new(backend));
        }
    }

    #[cfg(all(feature = "metal", target_os = "macos"))]
    if let Ok(backend) = MetalBackend::new() {
        return Ok(Box::new(backend));
    }

    #[cfg(feature = "wgpu")]
    if let Ok(backend) = WgpuBackend::new().await {
        if backend.device_count() > 0 {
            return Ok(Box::new(backend));
        }
    }

    #[cfg(feature = "cpu")]
    return Ok(Box::new(CpuBackend::new()));

    Err(Error::NoBackendAvailable)
}
```

---

## Next: [Serialization Strategy](./06-serialization.md)
