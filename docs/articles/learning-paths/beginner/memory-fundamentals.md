# Memory Fundamentals

This module covers GPU memory hierarchy, buffer types, and efficient data transfer patterns.

## GPU Memory Hierarchy

### Memory Types

| Memory Type | Scope | Speed | Size | Persistence |
|-------------|-------|-------|------|-------------|
| **Registers** | Per-thread | Fastest | ~256 KB total | Kernel lifetime |
| **Shared Memory** | Per-block | Very fast | 48-164 KB | Block lifetime |
| **Global Memory** | All threads | Slower | 4-80 GB | Until freed |
| **Constant Memory** | All threads | Fast (cached) | 64 KB | Until freed |

### Memory Access Pattern

```
CPU Host Memory ←→ GPU Global Memory ←→ Registers/Shared
     [PCIe/NVLink]      [High bandwidth]
```

## Buffer Types in DotCompute

### Standard Buffer

Explicit control over memory location and transfers:

```csharp
// Create buffer on GPU
using var gpuBuffer = computeService.CreateBuffer<float>(size);

// Copy from host to GPU
await gpuBuffer.CopyFromAsync(hostArray);

// Execute kernel using buffer
await computeService.ExecuteKernelAsync(kernel, config, gpuBuffer);

// Copy results back to host
await gpuBuffer.CopyToAsync(hostArray);
```

### Unified Buffer

Automatic memory management across CPU and GPU:

```csharp
// Create unified buffer (accessible from both CPU and GPU)
using var unified = computeService.CreateUnifiedBuffer<float>(size);

// Direct CPU access (no explicit copy needed)
var span = unified.AsSpan();
for (int i = 0; i < size; i++)
{
    span[i] = i * 1.5f;
}

// Execute kernel (data automatically available on GPU)
await computeService.ExecuteKernelAsync(kernel, config, unified);

// Results automatically visible on CPU
Console.WriteLine($"Result: {span[0]}");
```

**When to use unified memory:**

| Scenario | Recommended |
|----------|-------------|
| Frequent CPU-GPU data exchange | Unified |
| Large data, infrequent transfer | Standard |
| Random access patterns | Unified |
| Streaming workloads | Standard |

## Memory Transfer Patterns

### Pattern 1: Batch Processing

Transfer all data, process, retrieve results:

```csharp
// Allocate buffers
using var inputBuffer = computeService.CreateBuffer<float>(size);
using var outputBuffer = computeService.CreateBuffer<float>(size);

// Transfer input (one large copy)
await inputBuffer.CopyFromAsync(inputData);

// Process
await computeService.ExecuteKernelAsync(kernel, config, inputBuffer, outputBuffer);

// Transfer output (one large copy)
await outputBuffer.CopyToAsync(outputData);
```

**Best for**: Large batch processing with infrequent updates.

### Pattern 2: Streaming

Overlap transfers with computation:

```csharp
// Double buffering for overlap
using var bufferA = computeService.CreateBuffer<float>(chunkSize);
using var bufferB = computeService.CreateBuffer<float>(chunkSize);

for (int i = 0; i < chunks; i++)
{
    var currentBuffer = (i % 2 == 0) ? bufferA : bufferB;
    var nextBuffer = (i % 2 == 0) ? bufferB : bufferA;

    // Start next transfer while processing current
    var transferTask = nextBuffer.CopyFromAsync(GetChunk(i + 1));
    await computeService.ExecuteKernelAsync(kernel, config, currentBuffer);
    await transferTask;
}
```

**Best for**: Large datasets that don't fit in GPU memory.

### Pattern 3: Pinned Memory

Use page-locked host memory for faster transfers:

```csharp
// Create pinned buffer (host memory, GPU accessible)
using var pinned = computeService.CreatePinnedBuffer<float>(size);

// Faster transfers due to DMA (Direct Memory Access)
var span = pinned.AsSpan();
// ... fill data ...

// Transfer is faster than regular host memory
await gpuBuffer.CopyFromPinnedAsync(pinned);
```

**Best for**: High-frequency transfers, real-time processing.

## Memory Coalescing

GPU memory is accessed in transactions (typically 32 or 128 bytes). Coalesced access patterns achieve maximum bandwidth.

### Coalesced Access (Good)

Adjacent threads access adjacent memory locations:

```csharp
[Kernel]
public static void CoalescedAccess(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // Thread 0 reads input[0], thread 1 reads input[1], etc.
        // Memory transactions are combined
        output[idx] = input[idx] * 2.0f;
    }
}
```

### Strided Access (Bad)

Threads access non-adjacent locations:

```csharp
[Kernel]
public static void StridedAccess(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    if (idx * stride < output.Length)
    {
        // Thread 0 reads input[0], thread 1 reads input[stride], etc.
        // Multiple memory transactions required
        output[idx] = input[idx * stride] * 2.0f;
    }
}
```

### Performance Impact

| Access Pattern | Relative Performance |
|----------------|---------------------|
| Coalesced | 100% (baseline) |
| Stride 2 | ~50% |
| Stride 32 | ~3% |
| Random | ~1-5% |

## Buffer Best Practices

### 1. Reuse Buffers

```csharp
// BAD: Allocate per operation
for (int i = 0; i < iterations; i++)
{
    using var buffer = computeService.CreateBuffer<float>(size);
    // ... use buffer ...
} // Buffer freed each iteration

// GOOD: Reuse allocation
using var buffer = computeService.CreateBuffer<float>(size);
for (int i = 0; i < iterations; i++)
{
    // ... reuse buffer ...
}
```

### 2. Use Memory Pools

```csharp
// Configure memory pooling
var services = new ServiceCollection();
services.AddDotCompute(options =>
{
    options.EnableMemoryPooling = true;
    options.PoolSize = 512 * 1024 * 1024; // 512 MB pool
});
```

### 3. Minimize Transfers

```csharp
// BAD: Transfer intermediate results
await computeService.ExecuteKernelAsync(kernel1, config, inputBuffer, tempBuffer);
await tempBuffer.CopyToAsync(tempHost); // Unnecessary transfer
await tempBuffer.CopyFromAsync(tempHost);
await computeService.ExecuteKernelAsync(kernel2, config, tempBuffer, outputBuffer);

// GOOD: Keep data on GPU
await computeService.ExecuteKernelAsync(kernel1, config, inputBuffer, tempBuffer);
await computeService.ExecuteKernelAsync(kernel2, config, tempBuffer, outputBuffer);
```

### 4. Align Buffer Sizes

```csharp
// Round up to alignment boundary (typically 256 bytes)
int alignedSize = ((size + 63) / 64) * 64;
using var buffer = computeService.CreateBuffer<float>(alignedSize);
```

## Practical Example: Image Processing

```csharp
public async Task ProcessImageAsync(byte[] imageData, int width, int height)
{
    int pixelCount = width * height * 4; // RGBA

    // Use unified buffer for simplicity
    using var imageBuffer = computeService.CreateUnifiedBuffer<byte>(pixelCount);
    using var outputBuffer = computeService.CreateUnifiedBuffer<byte>(pixelCount);

    // Copy input (unified memory handles transfer)
    imageData.AsSpan().CopyTo(imageBuffer.AsSpan());

    // Process on GPU
    var config = new KernelConfig
    {
        GridSize = (pixelCount + 255) / 256,
        BlockSize = 256
    };

    await computeService.ExecuteKernelAsync(
        ImageKernels.GrayscaleConvert,
        config,
        imageBuffer, outputBuffer, width, height);

    // Results immediately available
    outputBuffer.AsSpan().CopyTo(imageData);
}
```

## Exercises

### Exercise 1: Transfer Benchmark

Measure transfer speeds for different buffer sizes:

```csharp
foreach (int size in new[] { 1024, 1024*1024, 100*1024*1024 })
{
    var sw = Stopwatch.StartNew();
    // ... measure transfer time ...
    double gbps = (size / 1e9) / (sw.Elapsed.TotalSeconds);
    Console.WriteLine($"Size: {size}, Transfer: {gbps:F2} GB/s");
}
```

### Exercise 2: Coalescing Impact

Compare performance of coalesced vs strided access patterns.

### Exercise 3: Pool vs No Pool

Measure allocation overhead with and without memory pooling.

## Key Takeaways

1. **GPU memory hierarchy** affects performance significantly
2. **Minimize transfers** - keep data on GPU when possible
3. **Coalesced access** provides maximum memory bandwidth
4. **Reuse buffers** to avoid allocation overhead
5. **Unified memory** simplifies programming but has tradeoffs

## Next Module

[Backend Selection →](backend-selection.md)

Learn how to choose and configure the appropriate compute backend.
