# VectorAdd Reference Implementation for Ring Kernels

## Overview

The VectorAdd reference implementation demonstrates a complete end-to-end GPU-native actor system using DotCompute ring kernels. This implementation serves as the canonical example for building persistent GPU computation with message-passing semantics, targeting <50μs end-to-end latency and 100K+ messages/sec throughput.

## Architecture

### Message Flow

```
┌──────────────┐
│  CPU Host    │
│  (Orleans)   │
└──────┬───────┘
       │ Serialize VectorAddRequest (41 bytes)
       │ MessageId + Priority + CorrelationId + A + B
       ↓
┌──────────────────────────────────────────┐
│  GPU Ring Buffer (Lock-Free Queue)      │
│  Capacity: 1024 messages                 │
│  Message Size: 256 bytes max             │
└──────┬───────────────────────────────────┘
       │ try_dequeue(input_buffer)
       ↓
┌──────────────────────────────────────────┐
│  Persistent CUDA Kernel                  │
│  process_vector_add_message()            │
│                                          │
│  1. Deserialize request (16 bytes GUID) │
│  2. Compute: result = a + b              │
│  3. Serialize response (37 bytes)        │
│  4. Increment msg_count atomic           │
└──────┬───────────────────────────────────┘
       │ try_enqueue(output_buffer)
       ↓
┌──────────────────────────────────────────┐
│  GPU Ring Buffer (Output Queue)          │
└──────┬───────────────────────────────────┘
       │ Deserialize VectorAddResponse
       │ MessageId + Priority + CorrelationId + Result
       ↓
┌──────────────┐
│  CPU Host    │
│  (Orleans)   │
└──────────────┘
```

### Binary Message Format

#### VectorAddRequest (41 bytes)

| Offset | Size | Field         | Type   | Description                           |
|--------|------|---------------|--------|---------------------------------------|
| 0      | 16   | MessageId     | Guid   | Unique message identifier             |
| 16     | 1    | Priority      | byte   | Message priority (0-255)              |
| 17     | 16   | CorrelationId | Guid   | Request correlation identifier        |
| 33     | 4    | A             | float  | First operand (little-endian)         |
| 37     | 4    | B             | float  | Second operand (little-endian)        |

**Total:** 41 bytes

#### VectorAddResponse (37 bytes)

| Offset | Size | Field         | Type   | Description                           |
|--------|------|---------------|--------|---------------------------------------|
| 0      | 16   | MessageId     | Guid   | Unique message identifier             |
| 16     | 1    | Priority      | byte   | Message priority (0-255)              |
| 17     | 16   | CorrelationId | Guid   | Matches request MessageId for pairing |
| 33     | 4    | Result        | float  | Computed sum (little-endian)          |

**Total:** 37 bytes

### Key Design Decisions

1. **Stack Allocation vs Dynamic Allocation**
   - **Choice:** Stack allocation with compile-time `MAX_MESSAGE_SIZE` constant
   - **Rationale:** GPU dynamic allocation (`new`/`delete`) adds 10-50μs overhead per message, consuming 100% of latency budget. Stack allocation is 0ns overhead.
   - **Configurability:** Message size is fully configurable via `RingKernelConfig.MaxInputMessageSize`

2. **Property Naming Clarity**
   - **Choice:** Renamed from ambiguous `Capacity` and `InputQueueSize` to:
     - `QueueCapacity`: Number of messages queue can hold (e.g., 1024 messages)
     - `MaxInputMessageSize`: Maximum bytes per input message (e.g., 256 bytes)
     - `MaxOutputMessageSize`: Maximum bytes per output message (e.g., 256 bytes)
   - **Rationale:** Eliminates confusion between queue capacity and message size

3. **Request-Response Pairing**
   - **Pattern:** `Response.CorrelationId = Request.MessageId`
   - **Rationale:** Enables Orleans actors to match responses to requests in async message passing

## Implementation Components

### 1. CUDA Serialization (`VectorAddSerialization.cu`)

The GPU-side serialization functions mirror C# `BitConverter` and `Guid.TryWriteBytes` behavior exactly:

```cuda
// VectorAddRequest binary layout (41 bytes)
struct VectorAddRequest
{
    unsigned char message_id[16];
    unsigned char priority;
    unsigned char correlation_id[16];
    float a;
    float b;
};

// Deserialize VectorAddRequest from buffer
__device__ bool deserialize_vector_add_request(
    const unsigned char* buffer,
    int buffer_size,
    VectorAddRequest* request)
{
    if (buffer_size < 41) return false;

    int offset = 0;
    // MessageId (16 bytes)
    for (int i = 0; i < 16; i++) request->message_id[i] = buffer[offset + i];
    offset += 16;

    // Priority (1 byte)
    request->priority = buffer[offset];
    offset += 1;

    // CorrelationId (16 bytes)
    for (int i = 0; i < 16; i++) request->correlation_id[i] = buffer[offset + i];
    offset += 16;

    // A (4 bytes, little-endian float)
    unsigned char a_bytes[4];
    for (int i = 0; i < 4; i++) a_bytes[i] = buffer[offset + i];
    request->a = *reinterpret_cast<float*>(a_bytes);
    offset += 4;

    // B (4 bytes, little-endian float)
    unsigned char b_bytes[4];
    for (int i = 0; i < 4; i++) b_bytes[i] = buffer[offset + i];
    request->b = *reinterpret_cast<float*>(b_bytes);

    return true;
}

// Complete VectorAdd processing
__device__ bool process_vector_add_message(
    const unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    VectorAddRequest request;
    if (!deserialize_vector_add_request(input_buffer, input_size, &request)) {
        return false;
    }

    // Compute VectorAdd: result = a + b
    float result = request.a + request.b;

    VectorAddResponse response;
    create_vector_add_response(&request, result, &response);

    int bytes_written = serialize_vector_add_response(output_buffer, output_size, &response);
    return bytes_written == 37;
}
```

### 2. Ring Kernel Compiler Integration

The `CudaRingKernelCompiler` generates persistent kernel code with VectorAdd processing:

```csharp
private static void GenerateHeaders(StringBuilder sb, RingKernelConfig config)
{
    sb.AppendLine("// Configuration constants");
    sb.AppendLine($"#define MAX_MESSAGE_SIZE {config.MaxInputMessageSize}");
    sb.AppendLine();
    sb.AppendLine("// VectorAdd message serialization");
    sb.AppendLine("#include \"VectorAddSerialization.cu\"");
}

private static void GeneratePersistentLoop(StringBuilder sb, RingKernelConfig config)
{
    sb.AppendLine("    // Process VectorAdd messages");
    sb.AppendLine("    unsigned char input_buffer[MAX_MESSAGE_SIZE];");  // Stack allocation
    sb.AppendLine("    unsigned char output_buffer[MAX_MESSAGE_SIZE];");
    sb.AppendLine();
    sb.AppendLine("    if (input_queue->try_dequeue(input_buffer)) {");
    sb.AppendLine("        // Process VectorAdd: Deserialize request -> Compute -> Serialize response");
    sb.AppendLine("        bool success = process_vector_add_message(");
    sb.AppendLine("            input_buffer, MAX_MESSAGE_SIZE,");
    sb.AppendLine("            output_buffer, MAX_MESSAGE_SIZE);");
    sb.AppendLine();
    sb.AppendLine("        if (success) {");
    sb.AppendLine("            // Enqueue response to output queue");
    sb.AppendLine("            output_queue->try_enqueue(output_buffer);");
    sb.AppendLine();
    sb.AppendLine("            // Update message counter");
    sb.AppendLine("            control->msg_count.fetch_add(1, cuda::memory_order_relaxed);");
    sb.AppendLine("        }");
    sb.AppendLine("    }");
}
```

### 3. Performance Benchmarks

Comprehensive BenchmarkDotNet suite validates performance targets:

```csharp
[MemoryDiagnoser]
[ThreadingDiagnoser]
[HardwareCounters(HardwareCounter.CacheMisses, HardwareCounter.BranchMispredictions)]
public class VectorAddPerformanceBenchmarks
{
    [Benchmark(Description = "VectorAdd Serialization (CPU)")]
    public void BenchmarkMessageSerialization()
    {
        // Target: <1μs per message
        // Serialize VectorAddRequest (41 bytes)
        var buffer = new byte[256];
        var messageId = Guid.NewGuid().ToByteArray();
        // ... serialization logic ...
    }

    [Benchmark(Description = "VectorAdd Deserialization (CPU)")]
    public void BenchmarkMessageDeserialization()
    {
        // Target: <1μs per message
        // Deserialize VectorAddResponse (37 bytes)
        var buffer = new byte[256];
        // ... deserialization logic ...
    }

    [Benchmark(Description = "Batch Serialization (1000 messages)")]
    public void BenchmarkBatchSerialization()
    {
        // Target: 100K+ messages/sec = 10μs per message
        const int batchSize = 1000;
        // ... batch processing ...
    }
}
```

## Configuration

### Ring Kernel Configuration

```csharp
var config = new RingKernelConfig
{
    KernelId = "vectoradd_kernel",
    Mode = RingKernelMode.Persistent,
    QueueCapacity = 1024,               // 1024 messages
    Domain = RingKernelDomain.General,
    MaxInputMessageSize = 256,          // 256 bytes per message
    MaxOutputMessageSize = 256,
    MessagingStrategy = MessagePassingStrategy.SharedMemory
};
```

### Key Parameters

- **QueueCapacity:** Number of messages the ring buffer can hold (default: 1024)
- **MaxInputMessageSize:** Maximum size of a single input message in bytes (default: 256)
- **MaxOutputMessageSize:** Maximum size of a single output message in bytes (default: 256)
- **Mode:** `Persistent` for continuously running kernels, `EventDriven` for on-demand processing
- **MessagingStrategy:** `SharedMemory` for GPU-to-GPU, `AtomicQueue` for CPU-GPU

## Performance Targets and Results

### Latency Targets

| Metric         | Target       | Measured | Status |
|----------------|--------------|----------|--------|
| Serialization  | P95 < 1μs    | TBD      | ⏳     |
| Deserialization| P95 < 1μs    | TBD      | ⏳     |
| GPU Processing | < 1μs        | TBD      | ⏳     |
| End-to-End     | P99 < 50μs   | TBD      | ⏳     |

### Latency Breakdown (Target)

```
CPU → GPU → CPU Round-Trip (<50μs):
  ├─ Serialization:       ~1μs   (2%)
  ├─ CPU → GPU transfer:  ~5μs   (10%)
  ├─ GPU processing:      ~1μs   (2%)
  ├─ GPU → CPU transfer:  ~5μs   (10%)
  ├─ Deserialization:     ~1μs   (2%)
  └─ Queue overhead:      ~7μs   (14%)
  ────────────────────────────────────
  Total:                  ~20μs  (40% of budget)
```

### Throughput Targets

| Metric            | Target             | Measured | Status |
|-------------------|--------------------|----------|--------|
| Baseline          | 100K msg/sec       | TBD      | ⏳     |
| Stretch Goal      | 2M msg/sec         | TBD      | ⏳     |
| GPU-to-GPU        | 100-500ns latency  | TBD      | ⏳     |

## Testing

### Integration Tests

The implementation includes comprehensive integration tests:

```csharp
[Fact(DisplayName = "Compiler: Generate valid CUDA C for VectorAdd ring kernel")]
public void Compiler_GenerateVectorAddKernel_ShouldProduceValidCode()
{
    var config = new RingKernelConfig
    {
        KernelId = "vectoradd_kernel",
        Mode = RingKernelMode.Persistent,
        QueueCapacity = 1024,
        MaxInputMessageSize = 256,
        MaxOutputMessageSize = 256
    };

    var cudaSource = _compiler.CompileToCudaC(kernelDef, "// VectorAdd kernel code", config);

    // Assert: Generated code contains essential components
    cudaSource.Should().Contain("#include <cuda_runtime.h>");
    cudaSource.Should().Contain("#include \"VectorAddSerialization.cu\"");
    cudaSource.Should().Contain("#define MAX_MESSAGE_SIZE");
    cudaSource.Should().Contain("process_vector_add_message");
    cudaSource.Should().Contain("unsigned char input_buffer[MAX_MESSAGE_SIZE]");
}
```

### Test Results

- **Compiler Tests:** 3/3 passing (100%)
- **Integration Tests:** 2/2 passing (100%)
- **Performance Benchmarks:** 2/2 validation tests passing, 3/3 hardware tests properly skipped

## Best Practices

### 1. Message Size Configuration

Always configure message sizes based on actual payload requirements:

```csharp
// For VectorAdd: 41-byte request, 37-byte response
var config = new RingKernelConfig
{
    MaxInputMessageSize = 64,   // Round up to power of 2
    MaxOutputMessageSize = 64
};
```

### 2. Queue Capacity Tuning

Choose queue capacity based on expected burst traffic:

```csharp
// High throughput: Larger queue capacity
QueueCapacity = 4096  // 4K messages

// Low latency: Smaller queue capacity
QueueCapacity = 256   // 256 messages
```

### 3. Error Handling

Always validate serialization results:

```cuda
__device__ bool process_vector_add_message(
    const unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    VectorAddRequest request;
    if (!deserialize_vector_add_request(input_buffer, input_size, &request)) {
        return false;  // Graceful failure
    }

    // Process...

    int bytes_written = serialize_vector_add_response(output_buffer, output_size, &response);
    return bytes_written == 37;  // Validate expected size
}
```

### 4. Performance Monitoring

Track message processing statistics:

```cuda
// Update message counter atomically
control->msg_count.fetch_add(1, cuda::memory_order_relaxed);
```

## Common Issues and Troubleshooting

### Issue: Buffer Size Mismatch

**Symptom:** Serialization fails with buffer overflow

**Cause:** `MaxInputMessageSize` is smaller than actual message size

**Solution:**
```csharp
// VectorAddRequest is 41 bytes
var config = new RingKernelConfig
{
    MaxInputMessageSize = 256  // Must be >= 41
};
```

### Issue: Queue Full

**Symptom:** Messages not processing, high latency

**Cause:** Producer rate exceeds consumer rate

**Solution:**
```csharp
// Increase queue capacity or add backpressure
var config = new RingKernelConfig
{
    QueueCapacity = 4096  // Increase from 1024
};
```

### Issue: Performance Below Target

**Symptom:** End-to-end latency > 50μs

**Root Causes:**
1. Dynamic allocation (should use stack allocation)
2. Incorrect message size configuration
3. Queue contention

**Diagnostics:**
```bash
# Run performance benchmarks
dotnet run --project benchmarks/VectorAddBenchmarks

# Check for dynamic allocation in generated code
grep -n "new char\|delete\[\]" generated_kernel.cu
```

## Related Documentation

- [Ring Kernels Getting Started](ring-kernels-getting-started.md)
- [Ring Kernels Performance Tuning](ring-kernels-performance-tuning.md)
- [Ring Kernels Implementation Plan](../../ring-kernels-vectoradd-implementation-plan.md)

## References

- **Source Code:**
  - `src/Backends/DotCompute.Backends.CUDA/Messaging/VectorAddSerialization.cu`
  - `src/Backends/DotCompute.Backends.CUDA/RingKernels/CudaRingKernelCompiler.cs`
  - `src/Backends/DotCompute.Backends.CUDA/RingKernels/RingKernelConfig.cs`

- **Tests:**
  - `tests/Hardware/DotCompute.Hardware.Cuda.Tests/RingKernels/VectorAddIntegrationTests.cs`
  - `tests/Hardware/DotCompute.Hardware.Cuda.Tests/RingKernels/VectorAddPerformanceBenchmarks.cs`

- **Planning:**
  - `docs/ring-kernels-vectoradd-implementation-plan.md`

## Changelog

### Phase 1: VectorAdd CUDA Serialization
- Created `VectorAddSerialization.cu` with GPU-side serialization functions
- Binary format: 41-byte request, 37-byte response
- Commit: `f4c0b8c3` (2024-11-15)

### Phase 2: Buffer Size Refactoring
- Replaced hardcoded buffer sizes with configurable `MAX_MESSAGE_SIZE` constant
- Zero runtime overhead with compile-time configuration
- Commit: `54011f34` (2024-11-15)

### Phase 3: Integration Tests
- Created `VectorAddIntegrationTests.cs` with compiler validation
- 3 tests covering code generation, buffer sizes, serialization logic
- Commit: `26114bd4` (2024-11-15)

### Phase 4: Property Renaming Refactoring
- Renamed `Capacity` → `QueueCapacity`
- Renamed `InputQueueSize` → `MaxInputMessageSize`
- Renamed `OutputQueueSize` → `MaxOutputMessageSize`
- Commit: `9c07d964` (2024-11-15)

### Phase 5: Performance Benchmarks
- Created `VectorAddPerformanceBenchmarks.cs` with BenchmarkDotNet suite
- 5 benchmarks covering compilation, serialization, deserialization, batch processing
- Commit: `80461208` (2024-11-15)

---

**Copyright © 2025 Michael Ivertowski. All rights reserved.**
