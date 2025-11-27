# Runtime Implementation

## RingKernelRuntime Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                    RingKernelRuntime                          │
├───────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐   │
│  │   Backend   │  │   Kernel    │  │   Message           │   │
│  │   Manager   │  │   Registry  │  │   Serializer        │   │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘   │
│         │                │                     │              │
│  ┌──────┴────────────────┴─────────────────────┴──────────┐  │
│  │                     Kernel States                       │  │
│  │  HashMap<String, KernelState>                          │  │
│  │  ┌─────────────────────────────────────────────────┐   │  │
│  │  │ KernelState {                                   │   │  │
│  │  │   control_block: GpuBuffer<ControlBlock>,       │   │  │
│  │  │   input_queue: MessageQueue,                    │   │  │
│  │  │   output_queue: MessageQueue,                   │   │  │
│  │  │   bridge: RingBufferBridge,                     │   │  │
│  │  │   stream: GpuStream,                            │   │  │
│  │  │   telemetry: TelemetryBuffer,                   │   │  │
│  │  │   status: KernelStatus,                         │   │  │
│  │  │ }                                               │   │  │
│  │  └─────────────────────────────────────────────────┘   │  │
│  └────────────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────┘
```

---

## Core Runtime Implementation

```rust
// crates/ringkernel/src/runtime.rs

use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::RwLock;

pub struct RingKernelRuntime {
    backend: Arc<dyn GpuBackend>,
    context: Arc<dyn GpuContext>,
    kernels: RwLock<HashMap<String, KernelState>>,
    config: RuntimeConfig,
}

impl RingKernelRuntime {
    /// Create runtime builder.
    pub fn builder() -> RuntimeBuilder {
        RuntimeBuilder::default()
    }

    /// Launch a kernel (initially inactive).
    pub async fn launch(
        &self,
        kernel_id: &str,
        grid_size: Dim3,
        block_size: Dim3,
        options: LaunchOptions,
    ) -> Result<KernelHandle> {
        // 1. Find kernel registration
        let registration = find_kernel(kernel_id)
            .ok_or(Error::KernelNotFound(kernel_id.to_string()))?;

        // 2. Allocate control block
        let control_block = self.allocate_control_block(&options).await?;

        // 3. Allocate message queues
        let input_queue = self.allocate_message_queue(options.queue_capacity).await?;
        let output_queue = self.allocate_message_queue(options.queue_capacity).await?;

        // 4. Create bridge for host↔GPU transfer
        let bridge = RingBufferBridge::new(
            &input_queue,
            &output_queue,
            self.detect_transfer_strategy(),
        )?;

        // 5. Compile kernel if needed
        let compiled = self.compile_kernel(registration).await?;

        // 6. Create kernel state
        let state = KernelState {
            id: kernel_id.to_string(),
            registration,
            control_block,
            input_queue,
            output_queue,
            bridge,
            compiled,
            stream: self.context.create_stream()?,
            telemetry: self.allocate_telemetry_buffer().await?,
            status: KernelStatus::Launched,
            grid_size,
            block_size,
            launch_time: Instant::now(),
            messages_processed: 0,
        };

        // 7. Store state
        self.kernels.write().await.insert(kernel_id.to_string(), state);

        Ok(KernelHandle {
            runtime: self,
            kernel_id: kernel_id.to_string(),
        })
    }

    /// Activate a kernel (begin processing).
    pub async fn activate(&self, kernel_id: &str) -> Result<()> {
        let mut kernels = self.kernels.write().await;
        let state = kernels.get_mut(kernel_id)
            .ok_or(Error::KernelNotFound(kernel_id.to_string()))?;

        // Set is_active = 1 in control block
        state.control_block.set_active(true).await?;

        // Launch persistent kernel (or first event-driven batch)
        self.launch_gpu_kernel(state).await?;

        // Start bridge transfer task
        state.bridge.start();

        state.status = KernelStatus::Active;
        Ok(())
    }

    /// Send message to kernel.
    pub async fn send<T: RingMessage>(&self, kernel_id: &str, message: T) -> Result<()> {
        let kernels = self.kernels.read().await;
        let state = kernels.get(kernel_id)
            .ok_or(Error::KernelNotFound(kernel_id.to_string()))?;

        // Serialize message
        let bytes = rkyv::to_bytes::<_, 4096>(&message)
            .map_err(|e| Error::Serialization(e.to_string()))?;

        // Enqueue to host-side buffer
        state.bridge.enqueue_input(&bytes).await?;

        Ok(())
    }

    /// Receive message from kernel.
    pub async fn receive<T: RingMessage>(
        &self,
        kernel_id: &str,
        timeout: Duration,
    ) -> Result<T> {
        let kernels = self.kernels.read().await;
        let state = kernels.get(kernel_id)
            .ok_or(Error::KernelNotFound(kernel_id.to_string()))?;

        // Wait for output
        let bytes = state.bridge.dequeue_output(timeout).await?;

        // Deserialize
        let archived = rkyv::check_archived_root::<T>(&bytes)
            .map_err(|e| Error::Deserialization(e.to_string()))?;

        Ok(archived.deserialize(&mut rkyv::Infallible)?)
    }

    /// Terminate kernel.
    pub async fn terminate(&self, kernel_id: &str) -> Result<()> {
        let mut kernels = self.kernels.write().await;
        let state = kernels.get_mut(kernel_id)
            .ok_or(Error::KernelNotFound(kernel_id.to_string()))?;

        // Signal termination
        state.control_block.set_terminate(true).await?;

        // Wait for kernel to finish
        self.wait_for_termination(state, Duration::from_secs(5)).await?;

        // Stop bridge
        state.bridge.stop();

        // Cleanup GPU resources
        state.status = KernelStatus::Terminated;

        // Remove from map (resources freed via Drop)
        kernels.remove(kernel_id);

        Ok(())
    }
}
```

---

## Kernel State

```rust
struct KernelState {
    id: String,
    registration: &'static KernelRegistration,

    // GPU resources
    control_block: GpuBuffer<ControlBlock>,
    input_queue: CudaMessageQueue,
    output_queue: CudaMessageQueue,
    compiled: Box<dyn CompiledKernel>,
    stream: Box<dyn GpuStream>,

    // Host↔GPU bridge
    bridge: RingBufferBridge,

    // Telemetry
    telemetry: GpuBuffer<TelemetryBuffer>,

    // Status
    status: KernelStatus,
    grid_size: Dim3,
    block_size: Dim3,
    launch_time: Instant,
    messages_processed: u64,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum KernelStatus {
    Created,
    Launched,
    Active,
    Deactivated,
    Terminating,
    Terminated,
}
```

---

## Runtime Builder

```rust
#[derive(Default)]
pub struct RuntimeBuilder {
    backend: Option<BackendType>,
    device_id: usize,
    config: RuntimeConfig,
}

impl RuntimeBuilder {
    pub fn backend(mut self, backend: BackendType) -> Self {
        self.backend = Some(backend);
        self
    }

    pub fn device(mut self, device_id: usize) -> Self {
        self.device_id = device_id;
        self
    }

    pub fn config(mut self, config: RuntimeConfig) -> Self {
        self.config = config;
        self
    }

    pub async fn build(self) -> Result<RingKernelRuntime> {
        let backend: Arc<dyn GpuBackend> = match self.backend {
            Some(BackendType::Cuda) => Arc::new(CudaBackend::new()?),
            Some(BackendType::Metal) => Arc::new(MetalBackend::new()?),
            Some(BackendType::Wgpu) => Arc::new(WgpuBackend::new().await?),
            Some(BackendType::Cpu) => Arc::new(CpuBackend::new()),
            None => select_backend()?,
        };

        let context = backend.create_context(self.device_id).await?;

        Ok(RingKernelRuntime {
            backend,
            context,
            kernels: RwLock::new(HashMap::new()),
            config: self.config,
        })
    }
}

#[derive(Debug, Clone)]
pub struct RuntimeConfig {
    pub default_queue_capacity: usize,
    pub default_message_size: usize,
    pub bridge_poll_interval: Duration,
    pub termination_timeout: Duration,
    pub enable_telemetry: bool,
}

impl Default for RuntimeConfig {
    fn default() -> Self {
        Self {
            default_queue_capacity: 4096,
            default_message_size: 65792, // 64KB + header
            bridge_poll_interval: Duration::from_micros(100),
            termination_timeout: Duration::from_secs(5),
            enable_telemetry: true,
        }
    }
}
```

---

## Kernel Handle (Ergonomic API)

```rust
/// Handle for interacting with a launched kernel.
pub struct KernelHandle<'a> {
    runtime: &'a RingKernelRuntime,
    kernel_id: String,
}

impl<'a> KernelHandle<'a> {
    pub async fn activate(&self) -> Result<()> {
        self.runtime.activate(&self.kernel_id).await
    }

    pub async fn deactivate(&self) -> Result<()> {
        self.runtime.deactivate(&self.kernel_id).await
    }

    pub async fn terminate(self) -> Result<()> {
        self.runtime.terminate(&self.kernel_id).await
    }

    pub async fn send<T: RingMessage>(&self, message: T) -> Result<()> {
        self.runtime.send(&self.kernel_id, message).await
    }

    pub async fn receive<T: RingMessage>(&self, timeout: Duration) -> Result<T> {
        self.runtime.receive(&self.kernel_id, timeout).await
    }

    pub async fn call<Req, Resp>(&self, request: Req) -> Result<Resp>
    where
        Req: RingMessage,
        Resp: RingMessage,
    {
        self.send(request).await?;
        self.receive(Duration::from_secs(30)).await
    }

    pub async fn status(&self) -> Result<KernelStatus> {
        self.runtime.status(&self.kernel_id).await
    }

    pub async fn metrics(&self) -> Result<KernelMetrics> {
        self.runtime.metrics(&self.kernel_id).await
    }
}
```

---

## Bridge Transfer Loop

```rust
impl RingBufferBridge {
    /// Start background transfer task.
    pub fn start(&mut self) {
        let input_host = self.input_host.clone();
        let input_gpu = self.input_gpu.clone();
        let output_host = self.output_host.clone();
        let output_gpu = self.output_gpu.clone();
        let running = self.running.clone();
        let poll_interval = self.poll_interval;

        self.transfer_task = Some(tokio::spawn(async move {
            while running.load(Ordering::Relaxed) {
                // Host → GPU transfer
                Self::transfer_to_device(&input_host, &input_gpu).await;

                // GPU → Host transfer
                Self::transfer_to_host(&output_gpu, &output_host).await;

                // Yield with SpinWait pattern (like DotCompute)
                tokio::time::sleep(poll_interval).await;
            }
        }));
    }

    async fn transfer_to_device(
        host: &PinnedBuffer<u8>,
        gpu: &GpuBuffer<u8>,
    ) {
        // Check if host has pending messages
        // Copy to GPU via DMA
        // Update GPU tail pointer
    }

    async fn transfer_to_host(
        gpu: &GpuBuffer<u8>,
        host: &PinnedBuffer<u8>,
    ) {
        // Check if GPU has output messages
        // Copy to host via DMA
        // Update host tail pointer
    }
}
```

---

## Next: [Testing Strategy](./09-testing.md)
