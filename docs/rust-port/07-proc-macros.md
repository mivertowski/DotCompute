# Proc Macros

## Overview

DotCompute uses C# Source Generators for:
- `[RingKernel]` attribute → CUDA kernel generation
- `[MemoryPackable]` → serialization code
- `[RingKernelMessage]` → message infrastructure

Rust equivalent: **Procedural Macros** (proc-macros)

---

## #[derive(RingMessage)]

Generates `RingMessage` trait implementation:

```rust
// crates/ringkernel-derive/src/ring_message.rs

use proc_macro::TokenStream;
use quote::quote;
use syn::{parse_macro_input, DeriveInput, Data, Fields};

#[proc_macro_derive(RingMessage, attributes(message))]
pub fn derive_ring_message(input: TokenStream) -> TokenStream {
    let input = parse_macro_input!(input as DeriveInput);
    let name = &input.ident;
    let name_str = name.to_string();

    // Find fields with #[message(...)] attributes
    let (id_field, priority_field, correlation_field) = extract_message_fields(&input);

    let expanded = quote! {
        impl ::ringkernel_core::RingMessage for #name {
            const MESSAGE_TYPE: &'static str = #name_str;

            fn message_id(&self) -> ::uuid::Uuid {
                self.#id_field
            }

            fn set_message_id(&mut self, id: ::uuid::Uuid) {
                self.#id_field = id;
            }

            fn priority(&self) -> u8 {
                self.#priority_field
            }

            fn correlation_id(&self) -> Option<::uuid::Uuid> {
                self.#correlation_field
            }

            fn payload_size(&self) -> usize {
                ::std::mem::size_of::<Self>() // Simplified; real impl uses rkyv
            }
        }
    };

    TokenStream::from(expanded)
}

fn extract_message_fields(input: &DeriveInput) -> (syn::Ident, syn::Ident, syn::Ident) {
    // Parse #[message(id)], #[message(priority)], #[message(correlation)]
    // ... implementation
    todo!()
}
```

### Usage

```rust
use ringkernel::prelude::*;
use rkyv::{Archive, Serialize, Deserialize};

#[derive(RingMessage, Archive, Serialize, Deserialize)]
pub struct ComputeRequest {
    #[message(id)]
    pub id: Uuid,

    #[message(priority = "priority::HIGH")]
    pub priority: u8,

    #[message(correlation)]
    pub correlation_id: Option<Uuid>,

    // Payload
    pub input_data: Vec<f32>,
    pub operation: OperationType,
}
```

---

## #[ring_kernel]

The main macro for defining ring kernels:

```rust
// crates/ringkernel-derive/src/ring_kernel.rs

use proc_macro::TokenStream;
use quote::quote;
use syn::{parse_macro_input, ItemFn, AttributeArgs};

#[proc_macro_attribute]
pub fn ring_kernel(attr: TokenStream, item: TokenStream) -> TokenStream {
    let args = parse_macro_input!(attr as AttributeArgs);
    let input_fn = parse_macro_input!(item as ItemFn);

    let config = parse_kernel_config(&args);
    let fn_name = &input_fn.sig.ident;
    let kernel_id = config.id.unwrap_or_else(|| fn_name.to_string());

    // Extract input/output types from signature
    let (input_type, output_type) = extract_message_types(&input_fn.sig);

    // Generate kernel registration
    let registration = generate_registration(&kernel_id, &input_type, &output_type);

    // Generate GPU code (CUDA/Metal/WGSL)
    let gpu_code = generate_gpu_kernel(&input_fn, &config);

    let expanded = quote! {
        // Original function (for CPU backend / testing)
        #input_fn

        // Kernel registration
        #registration

        // GPU kernel source (embedded as const)
        #gpu_code
    };

    TokenStream::from(expanded)
}

#[derive(Default)]
struct KernelConfig {
    id: Option<String>,
    mode: RingKernelMode,
    grid_size: Option<(u32, u32, u32)>,
    block_size: Option<(u32, u32, u32)>,
    publishes_to: Vec<String>,
    subscribes_to: Vec<String>,
    named_barriers: Vec<String>,
}

fn parse_kernel_config(args: &AttributeArgs) -> KernelConfig {
    let mut config = KernelConfig::default();

    for arg in args {
        match arg {
            // Parse: id = "kernel_name"
            // Parse: mode = "persistent" | "event_driven"
            // Parse: publishes_to = ["other_kernel"]
            // ...
        }
    }

    config
}
```

### Usage

```rust
use ringkernel::prelude::*;

#[ring_kernel(
    id = "vector_add",
    mode = "persistent",
    block_size = (256, 1, 1),
    publishes_to = ["aggregator"],
)]
pub fn vector_add(ctx: &mut RingContext, req: VectorAddRequest) -> VectorAddResponse {
    ctx.sync_threads();

    let idx = ctx.global_thread_id() as usize;
    if idx < req.a.len() {
        let result = req.a[idx] + req.b[idx];
        // ...
    }

    VectorAddResponse { /* ... */ }
}
```

### Generated Code

```rust
// Kernel registration
inventory::submit! {
    ::ringkernel_core::KernelRegistration {
        id: "vector_add",
        input_type: ::std::any::TypeId::of::<VectorAddRequest>(),
        output_type: ::std::any::TypeId::of::<VectorAddResponse>(),
        mode: ::ringkernel_core::RingKernelMode::Persistent,
        handler: |ctx, bytes| {
            let req: VectorAddRequest = ::rkyv::from_bytes(bytes)?;
            let resp = vector_add(ctx, req);
            Ok(::rkyv::to_bytes(&resp)?)
        },
    }
}

// CUDA source (for CUDA backend)
#[cfg(feature = "cuda")]
pub const VECTOR_ADD_CUDA: &str = r#"
__global__ void vector_add_kernel(
    ControlBlock* control,
    unsigned char* input_buffer,
    cuda::atomic<int>* input_head,
    // ...
) {
    // Generated persistent loop
    while (!control->should_terminate.load()) {
        // Dequeue, deserialize, process, enqueue
    }
}
"#;
```

---

## Context Method Translation

The proc macro translates `RingContext` method calls to GPU intrinsics:

```rust
/// Mapping from Rust methods to GPU intrinsics
const INTRINSIC_MAP: &[(&str, IntrinsicInfo)] = &[
    ("sync_threads", IntrinsicInfo {
        cuda: "__syncthreads()",
        metal: "threadgroup_barrier(mem_flags::mem_threadgroup)",
        wgsl: "workgroupBarrier()",
    }),
    ("sync_warp", IntrinsicInfo {
        cuda: "__syncwarp({mask})",
        metal: "simdgroup_barrier(mem_flags::mem_none)",
        wgsl: "/* no equivalent */",
    }),
    ("atomic_add", IntrinsicInfo {
        cuda: "atomicAdd({ptr}, {value})",
        metal: "atomic_fetch_add_explicit({ptr}, {value}, memory_order_relaxed)",
        wgsl: "atomicAdd({ptr}, {value})",
    }),
    ("now", IntrinsicInfo {
        cuda: "make_hlc_timestamp(clock64(), hlc_logical++, kernel_id)",
        metal: "make_hlc_timestamp(/* metal clock */, hlc_logical++, kernel_id)",
        wgsl: "/* limited timing support */",
    }),
    // ... more mappings
];

fn translate_to_cuda(rust_code: &syn::ItemFn) -> String {
    let mut cuda_code = String::new();

    // Walk AST and translate method calls
    for stmt in &rust_code.block.stmts {
        cuda_code.push_str(&translate_statement(stmt, Backend::Cuda));
    }

    cuda_code
}
```

---

## Compile-Time Validation

```rust
// Validate kernel signature at compile time
fn validate_kernel_signature(sig: &syn::Signature) -> syn::Result<()> {
    // Must have exactly 2 parameters: ctx and message
    if sig.inputs.len() != 2 {
        return Err(syn::Error::new_spanned(
            sig,
            "Ring kernel must have signature: fn(ctx: &mut RingContext, msg: T) -> R"
        ));
    }

    // First param must be &mut RingContext
    // Second param must implement RingMessage
    // Return type must implement RingMessage

    Ok(())
}

// Validate message types at compile time
fn validate_message_types(input: &syn::Type, output: &syn::Type) -> syn::Result<()> {
    // Check that types implement RingMessage + Archive + Serialize + Deserialize
    // This is done via trait bounds, but we can add helpful errors

    Ok(())
}
```

---

## Inventory Pattern for Registration

Use the `inventory` crate for automatic kernel discovery:

```rust
// crates/ringkernel-core/src/registry.rs

use inventory;

/// Kernel registration entry.
pub struct KernelRegistration {
    pub id: &'static str,
    pub input_type: std::any::TypeId,
    pub output_type: std::any::TypeId,
    pub mode: RingKernelMode,
    pub handler: fn(&mut RingContext, &[u8]) -> Result<Vec<u8>>,
}

inventory::collect!(KernelRegistration);

/// Get all registered kernels.
pub fn registered_kernels() -> impl Iterator<Item = &'static KernelRegistration> {
    inventory::iter::<KernelRegistration>()
}

/// Find kernel by ID.
pub fn find_kernel(id: &str) -> Option<&'static KernelRegistration> {
    registered_kernels().find(|k| k.id == id)
}
```

---

## Next: [Runtime Implementation](./08-runtime.md)
