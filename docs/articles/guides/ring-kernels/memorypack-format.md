# MemoryPack Binary Format for Ring Kernels

This document describes the exact binary format used by MemoryPack serialization in the DotCompute Ring Kernel system, enabling accurate CUDA deserializer generation.

## Overview

MemoryPack is a high-performance binary serializer for .NET that uses a "zero-encoding" approach, copying C# memory structures as directly as possible. Unlike self-describing formats (JSON, MessagePack), MemoryPack requires a schema for deserialization.

## Format Specification

### Endianness

- **Little-endian** (matches x86/x64 architecture)
- No cross-endian support

### Object Header

For classes/reference types with `[MemoryPackable]`:

```
┌─────────────────────────────────────────────────────────┐
│ Member Count (1 byte)                                   │
│   0-249: Number of serialized members                   │
│   255: Object is null                                   │
├─────────────────────────────────────────────────────────┤
│ Member 1 data (variable size based on type)             │
├─────────────────────────────────────────────────────────┤
│ Member 2 data (variable size based on type)             │
├─────────────────────────────────────────────────────────┤
│ ... (members in declaration order)                      │
└─────────────────────────────────────────────────────────┘
```

**Important**: Members are serialized in **declaration order** in the C# source file.

### Primitive Types

All primitives use fixed-size little-endian encoding:

| C# Type  | Size (bytes) | CUDA Type   | Notes                    |
|----------|-------------|-------------|--------------------------|
| byte     | 1           | uint8_t     | Direct copy              |
| sbyte    | 1           | int8_t      | Direct copy              |
| bool     | 1           | bool        | 0 = false, 1 = true      |
| short    | 2           | int16_t     | Little-endian            |
| ushort   | 2           | uint16_t    | Little-endian            |
| int      | 4           | int32_t     | Little-endian            |
| uint     | 4           | uint32_t    | Little-endian            |
| long     | 8           | int64_t     | Little-endian            |
| ulong    | 8           | uint64_t    | Little-endian            |
| float    | 4           | float       | IEEE 754 single          |
| double   | 8           | double      | IEEE 754 double          |
| Guid     | 16          | uint8_t[16] | Byte-by-byte (see below) |

### Guid Serialization

`System.Guid` is serialized as 16 consecutive bytes using `Guid.ToByteArray()` ordering:

```
Guid: 12345678-90AB-CDEF-1234-567890ABCDEF

Bytes (indices 0-15):
  0-3:   12 34 56 78  (Data1 - little-endian int32)
  4-5:   90 AB        (Data2 - little-endian int16)
  6-7:   CD EF        (Data3 - little-endian int16)
  8-15:  12 34 56 78 90 AB CD EF  (Data4 - byte array)
```

**CUDA equivalent**:
```cuda
struct guid_t {
    uint8_t bytes[16];
};
```

### Nullable Types

Nullable value types (`T?`) use a presence byte prefix:

```
┌─────────────────────────────────────────────────────────┐
│ Has Value (1 byte): 0 = null, non-zero = has value      │
├─────────────────────────────────────────────────────────┤
│ Value (if has_value != 0): type-specific encoding       │
└─────────────────────────────────────────────────────────┘
```

**Example: `Guid?`**

| Scenario     | Bytes                                        |
|--------------|----------------------------------------------|
| null         | `[0x00]` (1 byte)                            |
| has value    | `[0x01][16 bytes Guid data]` (17 bytes)      |

**CUDA equivalent**:
```cuda
struct nullable_guid {
    uint8_t has_value;  // 0 = null, 1 = present
    uint8_t value[16];  // Only valid if has_value != 0
};
```

### Collections

Collections use a 4-byte signed length prefix:

```
┌─────────────────────────────────────────────────────────┐
│ Count (4 bytes, signed int32)                           │
│   -1: Collection is null                                │
│   0+: Number of elements                                │
├─────────────────────────────────────────────────────────┤
│ Element 0 data                                          │
├─────────────────────────────────────────────────────────┤
│ Element 1 data                                          │
├─────────────────────────────────────────────────────────┤
│ ... (count elements)                                    │
└─────────────────────────────────────────────────────────┘
```

**Note**: For primitive arrays, elements are packed without padding.

### Strings

Strings use UTF-8 encoding with a 4-byte length prefix:

```
┌─────────────────────────────────────────────────────────┐
│ Byte Count (4 bytes, signed int32)                      │
│   -1: String is null                                    │
│   0+: Number of UTF-8 bytes                             │
├─────────────────────────────────────────────────────────┤
│ UTF-8 bytes (no null terminator)                        │
└─────────────────────────────────────────────────────────┘
```

## Ring Kernel Message Examples

### VectorAddRequest

C# Definition:
```csharp
[MemoryPackable]
public partial class VectorAddRequest : IRingKernelMessage
{
    public Guid MessageId { get; set; }           // 16 bytes
    public string MessageType => "VectorAddRequest"; // NOT serialized (read-only)
    public byte Priority { get; set; }            // 1 byte
    public Guid? CorrelationId { get; set; }      // 1 + 16 = 17 bytes
    public float A { get; set; }                  // 4 bytes
    public float B { get; set; }                  // 4 bytes
}
```

**MemoryPack Binary Format** (43 bytes total):

```
Offset  Size  Field           Description
──────  ────  ──────────────  ─────────────────────────────────────
0       1     [Header]        Member count = 5 (0x05)
1       16    MessageId       Guid bytes [0-15]
17      1     Priority        uint8_t value
18      1     CorrelationId   has_value flag
19      16    CorrelationId   Guid value (if has_value)
35      4     A               float (IEEE 754)
39      4     B               float (IEEE 754)
──────  ────
Total:  43 bytes
```

**CUDA Struct**:
```cuda
struct vector_add_request {
    uint8_t message_id[16];       // Offset: 0
    uint8_t priority;             // Offset: 16
    struct {
        uint8_t has_value;        // Offset: 17
        uint8_t value[16];        // Offset: 18
    } correlation_id;
    float a;                      // Offset: 34
    float b;                      // Offset: 38
};

// Note: Struct size = 42 bytes (excludes 1-byte header)
// Buffer offset 1 is where struct data starts (after header)
```

**CUDA Deserializer**:
```cuda
__device__ bool deserialize_vector_add_request(
    const uint8_t* buffer,
    int buffer_size,
    vector_add_request* out)
{
    // Buffer layout: [header:1][struct_data:42]
    if (buffer_size < 43) return false;

    // Check header (member count must be 5)
    if (buffer[0] != 5) return false;

    // Skip header byte
    const uint8_t* data = buffer + 1;

    // MessageId: 16 bytes at offset 0
    #pragma unroll
    for (int i = 0; i < 16; i++) {
        out->message_id[i] = data[i];
    }

    // Priority: 1 byte at offset 16
    out->priority = data[16];

    // CorrelationId: nullable Guid at offset 17
    out->correlation_id.has_value = data[17] != 0;
    if (out->correlation_id.has_value) {
        #pragma unroll
        for (int i = 0; i < 16; i++) {
            out->correlation_id.value[i] = data[18 + i];
        }
    }

    // A: float at offset 34
    out->a = *reinterpret_cast<const float*>(&data[34]);

    // B: float at offset 38
    out->b = *reinterpret_cast<const float*>(&data[38]);

    return true;
}
```

### VectorAddResponse

C# Definition:
```csharp
[MemoryPackable]
public partial class VectorAddResponse : IRingKernelMessage
{
    public Guid MessageId { get; set; }           // 16 bytes
    public string MessageType => "VectorAddResponse"; // NOT serialized
    public byte Priority { get; set; }            // 1 byte
    public Guid? CorrelationId { get; set; }      // 1 + 16 = 17 bytes
    public float Result { get; set; }             // 4 bytes
}
```

**MemoryPack Binary Format** (39 bytes total):

```
Offset  Size  Field           Description
──────  ────  ──────────────  ─────────────────────────────────────
0       1     [Header]        Member count = 4 (0x04)
1       16    MessageId       Guid bytes [0-15]
17      1     Priority        uint8_t value
18      1     CorrelationId   has_value flag
19      16    CorrelationId   Guid value (if has_value)
35      4     Result          float (IEEE 754)
──────  ────
Total:  39 bytes
```

## Key Differences from IRingKernelMessage.Serialize()

The manual `IRingKernelMessage.Serialize()` implementation does **NOT** include the 1-byte member count header. This creates a format mismatch:

| Aspect           | MemoryPack Format    | Manual Serialize()    |
|------------------|----------------------|-----------------------|
| Header           | 1-byte member count  | None                  |
| VectorAddRequest | 43 bytes             | 42 bytes              |
| VectorAddResponse| 39 bytes             | 38 bytes              |

**Resolution**: The CUDA deserializer must be aware of which format is being used and handle accordingly.

## Debug Byte Layout

For debugging message format issues, use this format to log byte contents:

```
Message: VectorAddRequest (43 bytes)
─────────────────────────────────────────────────────────
Offset   Hex                                        ASCII
0000:    05                                         .
         ^^-- Member count header (5 members)
0001:    12 34 56 78 90 AB CD EF 12 34 56 78 90 AB ............
000F:    CD EF                                      ..
         ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^-- MessageId
0011:    80                                         .
         ^^-- Priority (128)
0012:    01                                         .
         ^^-- CorrelationId.has_value (true)
0013:    AA BB CC DD EE FF 00 11 22 33 44 55 66 77 ..........
0021:    88 99                                      ..
         ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^-- CorrelationId value
0023:    00 00 20 41                                .. A
         ^^^^^^^^^^-- A = 10.0f (0x41200000 in IEEE 754)
0027:    00 00 A0 41                                ...A
         ^^^^^^^^^^-- B = 20.0f (0x41A00000 in IEEE 754)
```

## CUDA Memory Alignment Considerations

For optimal GPU performance, consider alignment when accessing struct fields:

1. **4-byte aligned access**: Use `reinterpret_cast<const float*>` only on 4-byte aligned addresses
2. **Unaligned access**: Use byte-by-byte copy for Guid and other non-aligned fields
3. **Coalesced access**: When processing message batches, ensure messages are aligned to warp boundaries

Example of safe unaligned float read:
```cuda
__device__ float read_float_unaligned(const uint8_t* ptr) {
    float result;
    memcpy(&result, ptr, sizeof(float));
    return result;
}
```

## Version Compatibility

MemoryPack format is **not** version-tolerant by default. Adding, removing, or reordering members will break compatibility.

For Ring Kernels, use:
1. **Fixed message schemas** - Define message types once, never modify
2. **Versioned message types** - Create `VectorAddRequestV2` instead of modifying original
3. **Schema registry** - Track message type versions at runtime

## See Also

- [Ring Kernel Architecture](../architecture/ring-kernels.md)
- [MemoryPack GitHub](https://github.com/Cysharp/MemoryPack)
- [CUDA Programming Guide](https://docs.nvidia.com/cuda/cuda-c-programming-guide/)
