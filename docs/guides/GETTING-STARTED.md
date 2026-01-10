# Getting Started with DotCompute

**Version**: 1.0.0

---

## Quick Start (5 minutes)

### Installation

```bash
dotnet add package DotCompute
dotnet add package DotCompute.Backends.CUDA  # For NVIDIA GPUs
```

### Hello World - Vector Addition

```csharp
using DotCompute;
using DotCompute.Abstractions;

// Create accelerator (auto-detects best GPU)
await using var accelerator = await AcceleratorFactory.CreateAsync();

// Allocate GPU memory
await using var a = await accelerator.Memory.AllocateAsync<float>(1000);
await using var b = await accelerator.Memory.AllocateAsync<float>(1000);
await using var result = await accelerator.Memory.AllocateAsync<float>(1000);

// Initialize data
await a.WriteAsync(Enumerable.Range(0, 1000).Select(i => (float)i).ToArray());
await b.WriteAsync(Enumerable.Range(0, 1000).Select(i => (float)i * 2).ToArray());

// Define kernel using [Kernel] attribute
[Kernel]
static void VectorAdd(float[] a, float[] b, float[] result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < a.Length)
        result[idx] = a[idx] + b[idx];
}

// Execute on GPU
await accelerator.ExecuteAsync<VectorAddKernel>(a, b, result);

// Read results back
var output = await result.ReadAsync();
Console.WriteLine($"Result[0] = {output[0]}");  // 0
Console.WriteLine($"Result[500] = {output[500]}");  // 1500
```

---

## Concepts

### Accelerators

An accelerator represents a compute device (GPU, CPU with SIMD, etc.):

```csharp
// Auto-detect best available
await using var accelerator = await AcceleratorFactory.CreateAsync();

// Specific backend
await using var cuda = await CudaAcceleratorFactory.CreateAsync(deviceId: 0);
await using var cpu = await CpuAcceleratorFactory.CreateAsync();
```

### Memory Management

DotCompute uses unified memory for CPU-GPU data transfer:

```csharp
// Allocate typed buffer
await using var buffer = await accelerator.Memory.AllocateAsync<float>(size);

// Write data to GPU
await buffer.WriteAsync(sourceArray);

// Read data from GPU
var result = await buffer.ReadAsync();

// Memory pooling for performance
await using var pool = await accelerator.CreateMemoryPoolAsync();
await using var pooledBuffer = await pool.AllocateAsync<float>(size);
```

### Kernels

Define compute kernels using the `[Kernel]` attribute:

```csharp
[Kernel]
static void MatrixMultiply(
    float[] a, float[] b, float[] c,
    int m, int n, int k)
{
    int row = Kernel.ThreadId.Y;
    int col = Kernel.ThreadId.X;

    if (row < m && col < n)
    {
        float sum = 0;
        for (int i = 0; i < k; i++)
            sum += a[row * k + i] * b[i * n + col];
        c[row * n + col] = sum;
    }
}
```

### Pipelines

Chain operations for complex workflows:

```csharp
var pipeline = accelerator.CreatePipeline()
    .AddStage("Load", LoadDataAsync)
    .AddStage("Process", ProcessKernel)
    .AddStage("Reduce", ReduceKernel)
    .AddStage("Save", SaveResultAsync)
    .Build();

await pipeline.ExecuteAsync(input);
```

---

## Common Patterns

### Error Handling

```csharp
try
{
    await accelerator.ExecuteAsync<MyKernel>(buffer);
}
catch (KernelExecutionException ex)
{
    Console.WriteLine($"Kernel failed: {ex.Message}");
}
catch (MemoryAllocationException ex)
{
    Console.WriteLine($"Out of GPU memory: {ex.Message}");
}
```

### Performance Optimization

```csharp
// Use memory pooling
await using var pool = await accelerator.CreateMemoryPoolAsync();

// Batch operations
await accelerator.ExecuteBatchAsync(kernels);

// Async execution with overlapping
var task1 = accelerator.ExecuteAsync<Kernel1>(buffer1);
var task2 = accelerator.ExecuteAsync<Kernel2>(buffer2);
await Task.WhenAll(task1, task2);
```

### Dependency Injection

```csharp
// In Startup.cs or Program.cs
services.AddDotCompute(options =>
{
    options.PreferredBackend = ComputeBackend.Cuda;
    options.EnableMemoryPooling = true;
});

// In your service
public class MyService
{
    private readonly IAccelerator _accelerator;

    public MyService(IAccelerator accelerator)
    {
        _accelerator = accelerator;
    }
}
```

---

## Next Steps

- [API Reference](./API-REFERENCE.md)
- [Sample Applications](./samples/)
- [Performance Tuning](./PERFORMANCE-TUNING.md)
- [Migration Guide](./MIGRATION-GUIDE.md)
