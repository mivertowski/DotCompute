# Quick Start Guide

Get up and running with DotCompute in minutes. This guide shows you how to write your first GPU-accelerated computation.

## Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 17.8+ or VS Code with C# extension
- (Optional) NVIDIA GPU with Compute Capability 5.0+ for CUDA support
- (Optional) macOS with Apple Silicon for Metal support

## Installation

Install DotCompute via NuGet:

```bash
dotnet add package DotCompute.Core
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Backends.CUDA  # Optional: NVIDIA GPU support
dotnet add package DotCompute.Backends.Metal # Optional: Apple GPU support
```

## Your First Kernel

The simplest way to create a compute kernel is using the `[Kernel]` attribute:

```csharp
using DotCompute;
using DotCompute.Abstractions;

public static class MyKernels
{
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

## Executing on CPU

The CPU backend uses SIMD vectorization for high performance:

```csharp
using DotCompute.Backends.CPU;
using DotCompute.Memory;

// Create accelerator
using var accelerator = new CpuAccelerator();

// Prepare data
var a = new float[] { 1, 2, 3, 4, 5 };
var b = new float[] { 10, 20, 30, 40, 50 };
var result = new float[5];

// Create unified buffers
using var bufferA = new UnifiedBuffer<float>(a, accelerator);
using var bufferB = new UnifiedBuffer<float>(b, accelerator);
using var bufferResult = new UnifiedBuffer<float>(result, accelerator);

// Execute kernel
await MyKernels.VectorAdd(bufferA, bufferB, bufferResult);

// Read results
result = bufferResult.ToArray();
Console.WriteLine(string.Join(", ", result)); // Output: 11, 22, 33, 44, 55
```

## Executing on GPU (CUDA)

For NVIDIA GPUs, use the CUDA backend:

```csharp
using DotCompute.Backends.CUDA;

// Create CUDA accelerator (automatically selects best GPU)
using var accelerator = new CudaAccelerator();

// Same code as CPU - DotCompute handles the differences!
using var bufferA = new UnifiedBuffer<float>(a, accelerator);
using var bufferB = new UnifiedBuffer<float>(b, accelerator);
using var bufferResult = new UnifiedBuffer<float>(result, accelerator);

await MyKernels.VectorAdd(bufferA, bufferB, bufferResult);

result = bufferResult.ToArray();
Console.WriteLine($"GPU Result: {string.Join(", ", result)}");
```

## Automatic Backend Selection

Let DotCompute choose the best backend automatically:

```csharp
using DotCompute.Core;
using DotCompute.Runtime.Services;

// Create orchestrator with automatic backend selection
var orchestrator = ComputeOrchestrator.CreateDefault();

// Execute kernel - runs on best available backend
var result = await orchestrator.ExecuteKernelAsync(
    "VectorAdd",
    new[] { bufferA, bufferB, bufferResult },
    gridSize: new(result.Length, 1, 1),
    blockSize: new(256, 1, 1)
);
```

## Matrix Operations

DotCompute includes built-in algorithm libraries:

```csharp
using DotCompute.Algorithms.LinearAlgebra;

var matrix1 = new Matrix(3, 3, new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
var matrix2 = Matrix.Identity(3);

// GPU-accelerated matrix multiplication
var result = await MatrixOperations.MultiplyAsync(matrix1, matrix2, accelerator);

Console.WriteLine($"Result:\n{result}");
```

## Debugging Cross-Backend

DotCompute includes debugging tools to validate GPU results against CPU:

```csharp
using DotCompute.Core.Debugging;

// Enable cross-backend validation
var debugService = new KernelDebugService();
var results = await debugService.CompareBackendsAsync(
    kernel,
    arguments,
    cpuAccelerator,
    gpuAccelerator
);

if (!results.OutputsMatch)
{
    Console.WriteLine($"GPU deviation: {results.MaxAbsoluteDifference}");
}
```

## Performance Optimization

### Enable Adaptive Backend Selection

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddDotComputeRuntime();
services.AddProductionOptimization(); // ML-based backend selection

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
```

### Use Memory Pooling

```csharp
// Memory pooling is automatic with UnifiedBuffer
// Reduces allocations by 90%+
using var pool = new MemoryPool(accelerator);
var buffer = pool.Rent<float>(1000); // Reuses memory

// Use buffer...

pool.Return(buffer); // Returns to pool for reuse
```

### Batch Operations

```csharp
// Process multiple operations in one GPU call
var operations = new[]
{
    new KernelArguments { Inputs = new[] { a1, b1 }, Outputs = new[] { result1 } },
    new KernelArguments { Inputs = new[] { a2, b2 }, Outputs = new[] { result2 } },
    new KernelArguments { Inputs = new[] { a3, b3 }, Outputs = new[] { result3 } }
};

await kernel.ExecuteBatchAsync(operations);
```

## Advanced: Writing Raw MSL (Metal)

For Apple Silicon, you can write Metal Shading Language directly:

```csharp
var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_add(
    const device float* a [[buffer(0)]],
    const device float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint id [[thread_position_in_grid]])
{
    result[id] = a[id] + b[id];
}
";

var kernel = await accelerator.CompileKernelAsync(
    new KernelDefinition
    {
        Name = "vector_add",
        Code = mslCode,
        EntryPoint = "vector_add"
    }
);
```

## Native AOT Support

DotCompute is fully Native AOT compatible:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

Startup time: **< 10ms** with Native AOT

## Next Steps

- **[Kernel Attribute Reference](reference/kernel-attribute.md)** - Learn all kernel features
- **[Performance Guide](performance/characteristics.md)** - Optimize your kernels
- **[CUDA Programming](advanced/cuda-programming.md)** - Advanced CUDA features
- **[Algorithm Library](api/DotCompute.Algorithms.html)** - Pre-built operations

## Example Projects

Check out complete examples in the repository:

- **Vector Addition** - `samples/VectorAdd/`
- **Matrix Multiplication** - `samples/MatrixMultiply/`
- **Image Processing** - `samples/ImageFilters/`
- **Signal Processing** - `samples/FFT/`

## Getting Help

- **Documentation**: https://mivertowski.github.io/DotCompute/
- **Issues**: https://github.com/mivertowski/DotCompute/issues
- **Discussions**: https://github.com/mivertowski/DotCompute/discussions

---

**Performance Tip**: Start with CPU backend for development and debugging, then switch to GPU for production performance. DotCompute makes this transition seamless!
