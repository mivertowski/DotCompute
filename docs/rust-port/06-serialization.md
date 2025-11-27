# Serialization Strategy

## DotCompute Approach: MemoryPack

DotCompute uses [MemoryPack](https://github.com/Cysharp/MemoryPack) for:
- **Zero-copy serialization** (~20-50ns per message)
- **Source-generated** code (no runtime reflection)
- **Binary format** (little-endian, compact)

---

## Rust Equivalents

### Option 1: `rkyv` (Recommended)

[rkyv](https://rkyv.org/) provides **zero-copy deserialization**:

```rust
use rkyv::{Archive, Deserialize, Serialize};

#[derive(Archive, Deserialize, Serialize)]
#[archive(check_bytes)]  // Validation for untrusted data
pub struct VectorAddRequest {
    pub id: [u8; 16],      // UUID as bytes
    pub priority: u8,
    pub a: Vec<f32>,
    pub b: Vec<f32>,
}

// Zero-copy access (no deserialization!)
fn process(bytes: &[u8]) {
    let archived = rkyv::check_archived_root::<VectorAddRequest>(bytes).unwrap();
    // `archived` is a reference, no copying!
    println!("Priority: {}", archived.priority);
}
```

**Advantages**:
- True zero-copy: access fields without deserializing
- Alignment-aware: supports GPU memory requirements
- Validation: optional bounds checking

**Disadvantages**:
- Archived types have different Rust types (`ArchivedVectorAddRequest`)
- Slightly larger binary size

### Option 2: `zerocopy`

For simple POD (Plain Old Data) types:

```rust
use zerocopy::{AsBytes, FromBytes, FromZeroes};

#[derive(AsBytes, FromBytes, FromZeroes)]
#[repr(C)]
pub struct SimpleMessage {
    pub id: u64,
    pub value: f32,
    pub padding: [u8; 4],
}

// Direct cast (truly zero-cost)
fn from_bytes(bytes: &[u8]) -> &SimpleMessage {
    zerocopy::Ref::<_, SimpleMessage>::new(bytes).unwrap().into_ref()
}
```

**Advantages**:
- Absolute minimal overhead
- Perfect for GPU structures (ControlBlock, TelemetryBuffer)

**Disadvantages**:
- No variable-length data (Vec, String)
- Requires `#[repr(C)]` and manual padding

### Option 3: `bincode` + `serde`

For maximum flexibility:

```rust
use serde::{Serialize, Deserialize};

#[derive(Serialize, Deserialize)]
pub struct FlexibleMessage {
    pub id: uuid::Uuid,
    pub data: HashMap<String, Value>,
}

// Serialize
let bytes = bincode::serialize(&msg)?;

// Deserialize
let msg: FlexibleMessage = bincode::deserialize(&bytes)?;
```

**Advantages**:
- Works with any Serde-compatible type
- Wide ecosystem support

**Disadvantages**:
- Always copies data (not zero-copy)
- ~100-500ns per message

---

## Recommended Hybrid Approach

```rust
/// Fixed-size header using zerocopy (GPU-friendly)
#[derive(AsBytes, FromBytes, FromZeroes)]
#[repr(C, align(8))]
pub struct MessageHeader {
    pub message_type: u32,      // Type discriminator
    pub payload_size: u32,      // Size of payload
    pub message_id: [u8; 16],   // UUID
    pub priority: u8,
    pub flags: u8,
    pub _padding: [u8; 6],
}

/// Variable payload using rkyv
#[derive(Archive, Serialize, Deserialize)]
pub struct VectorAddPayload {
    pub a: Vec<f32>,
    pub b: Vec<f32>,
}

/// Combined message
pub struct Message<T: Archive> {
    header: MessageHeader,
    payload: T,
}

impl<T: Archive + Serialize<...>> Message<T> {
    pub fn serialize(&self, buffer: &mut [u8]) -> usize {
        // Write header directly (zerocopy)
        let header_size = std::mem::size_of::<MessageHeader>();
        buffer[..header_size].copy_from_slice(self.header.as_bytes());

        // Write payload (rkyv)
        let payload_bytes = rkyv::to_bytes::<_, 256>(&self.payload).unwrap();
        buffer[header_size..header_size + payload_bytes.len()]
            .copy_from_slice(&payload_bytes);

        header_size + payload_bytes.len()
    }
}
```

---

## GPU-Side Deserialization

For CUDA kernels, we need C-compatible structures:

```rust
/// Generate CUDA C serialization code
pub fn generate_cuda_deserializer(message_type: &MessageType) -> String {
    format!(r#"
// Auto-generated deserializer for {name}
struct {name} {{
{fields}
}};

__device__ {name} deserialize_{name}(const unsigned char* data) {{
    {name} msg;
    size_t offset = 0;

{deserialize_code}

    return msg;
}}
"#,
        name = message_type.name,
        fields = generate_cuda_fields(message_type),
        deserialize_code = generate_cuda_deserialize_code(message_type),
    )
}
```

### Example Generated Code

```cuda
// Auto-generated deserializer for VectorAddRequest
struct VectorAddRequest {
    unsigned char id[16];
    unsigned char priority;
    int a_len;
    float* a;
    int b_len;
    float* b;
};

__device__ VectorAddRequest deserialize_VectorAddRequest(
    const unsigned char* data,
    float* a_storage,
    float* b_storage
) {
    VectorAddRequest msg;
    size_t offset = 0;

    // Read id (16 bytes)
    memcpy(msg.id, data + offset, 16);
    offset += 16;

    // Read priority (1 byte)
    msg.priority = data[offset];
    offset += 1;

    // Read a_len (4 bytes, little-endian)
    msg.a_len = *((int*)(data + offset));
    offset += 4;

    // Copy a data
    msg.a = a_storage;
    memcpy(msg.a, data + offset, msg.a_len * sizeof(float));
    offset += msg.a_len * sizeof(float);

    // Read b_len
    msg.b_len = *((int*)(data + offset));
    offset += 4;

    // Copy b data
    msg.b = b_storage;
    memcpy(msg.b, data + offset, msg.b_len * sizeof(float));

    return msg;
}
```

---

## Performance Comparison

| Method | Serialize | Deserialize | Zero-Copy | GPU-Friendly |
|--------|-----------|-------------|-----------|--------------|
| MemoryPack (.NET) | ~20ns | ~20ns | ✅ | ✅ |
| rkyv | ~30ns | 0ns | ✅ | ⚠️ (needs wrapper) |
| zerocopy | 0ns | 0ns | ✅ | ✅ |
| bincode | ~100ns | ~100ns | ❌ | ⚠️ |

---

## Message Size Limits

Following DotCompute conventions:

```rust
/// Maximum message sizes
pub const MAX_HEADER_SIZE: usize = 256;
pub const MAX_PAYLOAD_SIZE: usize = 64 * 1024; // 64KB
pub const MAX_MESSAGE_SIZE: usize = MAX_HEADER_SIZE + MAX_PAYLOAD_SIZE;

/// Validate message size at compile time
pub trait ValidateMessageSize: RingMessage {
    const SIZE_VALID: () = {
        // This will fail at compile time if too large
        assert!(std::mem::size_of::<Self>() <= MAX_MESSAGE_SIZE);
    };
}
```

---

## Next: [Proc Macros](./07-proc-macros.md)
