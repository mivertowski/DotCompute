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

## Working with Memory in DotCompute

DotCompute simplifies memory management through automatic buffer handling:

### Automatic Memory Management

The `IComputeOrchestrator` handles memory transfers automatically:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Abstractions.Interfaces;

// Setup
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Prepare data (host arrays)
float[] inputA = new float[10000];
float[] inputB = new float[10000];
float[] result = new float[10000];

// Fill input data
for (int i = 0; i < 10000; i++)
{
    inputA[i] = i;
    inputB[i] = i * 2;
}

// Execute kernel - memory transfers handled automatically
await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { inputA, inputB, result }
);

// Results are automatically copied back to host array
Console.WriteLine($"Result[0] = {result[0]}");  // 0
Console.WriteLine($"Result[100] = {result[100]}");  // 300
```

**What happens automatically:**
1. Input arrays are copied to GPU memory
2. Kernel executes on GPU
3. Output array is copied back to host
4. GPU memory is released

### Understanding Span Parameters

In kernels, use `Span<T>` and `ReadOnlySpan<T>` for buffer parameters:

```csharp
using DotCompute.Generators.Kernel.Attributes;

public static partial class MyKernels
{
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,     // Input (read-only on GPU)
        ReadOnlySpan<float> b,     // Input (read-only on GPU)
        Span<float> result)        // Output (read-write on GPU)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

**Parameter conventions:**
- `ReadOnlySpan<T>`: Input data (GPU reads only)
- `Span<T>`: Output data (GPU reads and writes)
- Scalar types (`int`, `float`): Constants passed to all threads

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

### 1. Minimize Transfers

Keep data on GPU between kernel calls when possible:

```csharp
// BAD: Transfer intermediate results unnecessarily
float[] temp = new float[size];
await orchestrator.ExecuteKernelAsync("Kernel1", new object[] { input, temp });
// temp is copied back to CPU here
await orchestrator.ExecuteKernelAsync("Kernel2", new object[] { temp, output });
// temp is copied to GPU again
```

For multi-stage pipelines, consider combining kernels or using advanced memory patterns (see Intermediate Path).

### 2. Batch Operations

```csharp
// BAD: Many small kernel calls
for (int i = 0; i < 1000; i++)
{
    float[] smallData = GetSmallChunk(i);
    await orchestrator.ExecuteKernelAsync("Process", new object[] { smallData });
}

// GOOD: Single large kernel call
float[] allData = GetAllData();
await orchestrator.ExecuteKernelAsync("ProcessBatch", new object[] { allData });
```

### 3. Choose Right Data Types

```csharp
// Use float for most GPU operations (faster)
float[] data = new float[size];

// Use double only when precision is critical
double[] preciseData = new double[size];

// Note: float64 may not be supported on all GPUs
// Check device capabilities before using double
```

## Practical Example: Image Grayscale

```csharp
using DotCompute.Generators.Kernel.Attributes;

public static partial class ImageKernels
{
    [Kernel]
    public static void Grayscale(
        ReadOnlySpan<byte> input,   // RGBA input
        Span<byte> output,          // Grayscale output
        int pixelCount)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < pixelCount)
        {
            int inputIdx = idx * 4;  // RGBA = 4 bytes per pixel

            // Luminosity formula
            byte r = input[inputIdx];
            byte g = input[inputIdx + 1];
            byte b = input[inputIdx + 2];

            output[idx] = (byte)(0.299f * r + 0.587f * g + 0.114f * b);
        }
    }
}

// Usage
public async Task ProcessImage(byte[] rgbaImage, int width, int height)
{
    var host = Host.CreateApplicationBuilder().Build();
    host.Services.AddDotComputeRuntime();
    var app = host.Build();

    var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

    int pixelCount = width * height;
    byte[] grayscale = new byte[pixelCount];

    await orchestrator.ExecuteKernelAsync(
        "Grayscale",
        new object[] { rgbaImage, grayscale, pixelCount }
    );

    // grayscale now contains the result
}
```

## Understanding Thread Configuration

DotCompute automatically configures thread dimensions, but understanding them helps write efficient kernels:

```
Total threads = GridSize × BlockSize

For 10,000 elements with BlockSize=256:
- GridSize = ceil(10000/256) = 40 blocks
- Total threads = 40 × 256 = 10,240 threads
- Extra 240 threads are handled by bounds checking
```

**Why bounds checking is critical:**

```csharp
[Kernel]
public static void Process(Span<float> data)
{
    int idx = Kernel.ThreadId.X;

    // ALWAYS check bounds - some threads may be beyond data size
    if (idx < data.Length)
    {
        data[idx] = data[idx] * 2.0f;
    }
}
```

## Exercises

### Exercise 1: Transfer Benchmark

Measure how array size affects total execution time:

```csharp
foreach (int size in new[] { 1000, 100_000, 10_000_000 })
{
    float[] data = new float[size];
    var sw = Stopwatch.StartNew();

    await orchestrator.ExecuteKernelAsync("VectorAdd", new object[] { data, data, data });

    Console.WriteLine($"Size: {size:N0}, Time: {sw.ElapsedMilliseconds}ms");
}
```

### Exercise 2: Coalescing Impact

Compare performance of coalesced vs strided access patterns.

### Exercise 3: Batch vs Individual

Compare performance of many small kernel calls vs one large call.

## Key Takeaways

1. **GPU memory hierarchy** affects performance significantly
2. **DotCompute handles transfers** automatically for simple cases
3. **Coalesced access** provides maximum memory bandwidth
4. **Always include bounds checks** in kernels
5. **Minimize transfers** by batching operations

## Next Module

[Backend Selection →](backend-selection.md)

Learn how to choose and configure the appropriate compute backend.
