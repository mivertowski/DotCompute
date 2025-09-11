# Getting Started with DotCompute v0.2.0-alpha

DotCompute is a high-performance, Native AOT-compatible universal compute framework for .NET 9+ that provides seamless CPU and CUDA acceleration with sub-10ms startup times.

## Table of Contents

- [Installation](#installation)
- [System Requirements](#system-requirements)
- [Quick Start](#quick-start)
- [Basic CPU Compute Example](#basic-cpu-compute-example)
- [Basic GPU Compute Example with CUDA](#basic-gpu-compute-example-with-cuda)
- [Memory Management Basics](#memory-management-basics)
- [Performance Tips](#performance-tips)
- [Next Steps](#next-steps)

## Installation

### Package Installation

DotCompute is distributed as a set of NuGet packages. Install the core package and any backend-specific packages you need:

```bash
# Core package (required)
dotnet add package DotCompute.Core

# CPU backend with SIMD acceleration (recommended)
dotnet add package DotCompute.Backends.CPU

# CUDA backend for NVIDIA GPU acceleration (optional)
dotnet add package DotCompute.Backends.CUDA

# Memory management (recommended)
dotnet add package DotCompute.Memory

# Runtime services and dependency injection (recommended)
dotnet add package DotCompute.Runtime

# Source generators and analyzers (recommended)
dotnet add package DotCompute.Generators
```

### Project Setup

Add the DotCompute runtime to your dependency injection container:

```csharp
using DotCompute.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Add DotCompute services
builder.Services.AddDotComputeRuntime();

// Optional: Add production optimizations
builder.Services.AddProductionOptimization();
builder.Services.AddProductionDebugging();

var host = builder.Build();
```

## System Requirements

### Minimum Requirements

- **.NET 9.0 SDK** or later
- **C# 13** language features
- **Visual Studio 2022 17.8+** or VS Code with C# extension
- **Windows 10/11**, **macOS 12+**, or **Linux** (Ubuntu 20.04+)

### For GPU Acceleration (Optional)

- **NVIDIA GPU** with Compute Capability 5.0+ (Maxwell architecture or newer)
- **CUDA Toolkit 12.0+** installed
- **NVIDIA Driver 520+**

### Hardware Capabilities Check

Use the built-in capability detection to verify your system:

```csharp
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Runtime.Services;

// Check CUDA availability
var cudaCapability = CudaCapabilityManager.GetTargetComputeCapability();
if (cudaCapability != null)
{
    Console.WriteLine($"CUDA supported: Compute Capability {cudaCapability}");
}

// Check CPU capabilities
var cpuInfo = Environment.ProcessorCount;
Console.WriteLine($"CPU cores available: {cpuInfo}");
```

## Quick Start

The fastest way to get started is using the modern [Kernel] attribute approach introduced in v0.2.0:

```csharp
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Core.Kernels;

public static class MyKernels
{
    [Kernel]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

Execute the kernel:

```csharp
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

// Get the orchestrator from DI container
var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();

// Prepare data
var a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
var b = new float[] { 5.0f, 6.0f, 7.0f, 8.0f };
var result = new float[4];

// Execute kernel (automatically selects best backend)
await orchestrator.ExecuteAsync<object>("MyKernels.VectorAdd", a, b, result);

// Result contains [6.0f, 8.0f, 10.0f, 12.0f]
Console.WriteLine($"Result: [{string.Join(", ", result)}]");
```

## Basic CPU Compute Example

This example demonstrates high-performance CPU computation with SIMD vectorization:

```csharp
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Core.Kernels;
using DotCompute.Memory;
using DotCompute.Abstractions.Interfaces;

public static class CpuKernels
{
    [Kernel]
    public static void MatrixMultiply(
        ReadOnlySpan<float> a, 
        ReadOnlySpan<float> b, 
        Span<float> result, 
        int rows, 
        int cols, 
        int shared)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;
        
        if (row < rows && col < cols)
        {
            float sum = 0.0f;
            for (int k = 0; k < shared; k++)
            {
                sum += a[row * shared + k] * b[k * cols + col];
            }
            result[row * cols + col] = sum;
        }
    }
}

// Usage
public async Task CpuComputeExample(IComputeOrchestrator orchestrator)
{
    const int size = 1000;
    var a = new float[size * size];
    var b = new float[size * size];
    var result = new float[size * size];
    
    // Initialize matrices with test data
    Random.Shared.Fill(a.AsSpan());
    Random.Shared.Fill(b.AsSpan());
    
    // Force CPU execution
    await orchestrator.ExecuteAsync<object>(
        "CpuKernels.MatrixMultiply", 
        "CPU", // Preferred backend
        a, b, result, size, size, size);
    
    Console.WriteLine($"Matrix multiplication completed on CPU");
}
```

## Basic GPU Compute Example with CUDA

This example shows GPU acceleration for computationally intensive tasks:

```csharp
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Core.Kernels;
using DotCompute.Memory;

public static class GpuKernels
{
    [Kernel]
    public static void ParallelReduction(ReadOnlySpan<float> input, Span<float> output)
    {
        int tid = Kernel.ThreadId.X;
        int blockSize = Kernel.BlockDim.X;
        int blockId = Kernel.BlockId.X;
        
        // Each thread processes one element
        int index = blockId * blockSize + tid;
        
        if (index < input.Length)
        {
            // Parallel reduction within block
            float value = input[index];
            
            // Simple reduction - sum all values
            // In real CUDA, you'd use shared memory and synchronization
            output[blockId] = value; // Simplified for example
        }
    }
}

// Usage with CUDA
public async Task GpuComputeExample(IComputeOrchestrator orchestrator)
{
    // Large dataset that benefits from GPU parallelism
    const int dataSize = 1024 * 1024; // 1M elements
    var input = new float[dataSize];
    var output = new float[1024]; // Reduced output
    
    // Initialize with test data
    for (int i = 0; i < dataSize; i++)
    {
        input[i] = (float)Math.Sin(i * 0.01);
    }
    
    try
    {
        // Prefer CUDA execution
        await orchestrator.ExecuteAsync<object>(
            "GpuKernels.ParallelReduction",
            "CUDA", // Preferred backend
            input, output);
        
        Console.WriteLine("Parallel reduction completed on GPU");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"GPU execution failed, falling back to CPU: {ex.Message}");
        
        // Automatic fallback to CPU
        await orchestrator.ExecuteAsync<object>(
            "GpuKernels.ParallelReduction",
            input, output);
    }
}
```

## Memory Management Basics

DotCompute provides unified memory management that automatically handles transfers between CPU and GPU:

### Using UnifiedBuffer for Zero-Copy Operations

```csharp
using DotCompute.Memory;
using DotCompute.Abstractions.Memory;

public async Task UnifiedMemoryExample(IUnifiedMemoryManager memoryManager)
{
    // Create unified buffers that work across CPU/GPU
    var bufferA = new UnifiedBuffer<float>(memoryManager, 1000);
    var bufferB = new UnifiedBuffer<float>(memoryManager, 1000);
    var result = new UnifiedBuffer<float>(memoryManager, 1000);
    
    try
    {
        // Initialize data on host (CPU)
        var hostSpan = bufferA.AsSpan();
        for (int i = 0; i < hostSpan.Length; i++)
        {
            hostSpan[i] = i * 1.5f;
        }
        bufferA.MarkHostDirty(); // Mark as modified on host
        
        // Copy initial data
        bufferB.AsSpan().Fill(2.0f);
        bufferB.MarkHostDirty();
        
        // Execute kernel - data automatically transferred to GPU if needed
        var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();
        await orchestrator.ExecuteWithBuffersAsync<object>(
            "MyKernels.VectorAdd",
            new[] { bufferA, bufferB, result },
            Array.Empty<object>());
        
        // Access results - automatically transferred back to CPU
        var resultSpan = result.AsReadOnlySpan();
        Console.WriteLine($"First result: {resultSpan[0]}, Last result: {resultSpan[^1]}");
    }
    finally
    {
        // Proper disposal
        bufferA.Dispose();
        bufferB.Dispose();
        result.Dispose();
    }
}
```

### Memory Pool Usage for Performance

```csharp
using DotCompute.Memory;

public class HighPerformanceCompute
{
    private readonly IUnifiedMemoryManager _memoryManager;
    
    public HighPerformanceCompute(IUnifiedMemoryManager memoryManager)
    {
        _memoryManager = memoryManager;
    }
    
    public async Task PooledMemoryExample()
    {
        // Memory pool reduces allocations by 90%+
        using var buffer1 = await _memoryManager.AllocateAsync<float>(1000);
        using var buffer2 = await _memoryManager.AllocateAsync<float>(1000);
        
        // Buffers are automatically returned to pool when disposed
        // Subsequent allocations reuse pooled memory
    }
}
```

## Performance Tips

### 1. Use Appropriate Data Sizes

```csharp
// GPU excels at large parallel workloads
const int smallSize = 1000;    // Better on CPU
const int largeSize = 100000;  // Better on GPU

// Let DotCompute choose automatically based on workload size
await orchestrator.ExecuteAsync<object>("MyKernel", largeData); // Auto-selects GPU
await orchestrator.ExecuteAsync<object>("MyKernel", smallData); // Auto-selects CPU
```

### 2. Minimize Memory Transfers

```csharp
// Bad: Multiple small transfers
for (int i = 0; i < iterations; i++)
{
    var smallResult = await orchestrator.ExecuteAsync<float[]>("SmallKernel", smallData);
    // Process result...
}

// Good: Batch operations
var batchResult = await orchestrator.ExecuteAsync<float[]>("BatchKernel", allData);
```

### 3. Precompile Kernels for Better Performance

```csharp
// Precompile frequently used kernels
await orchestrator.PrecompileKernelAsync("MyKernels.VectorAdd");
await orchestrator.PrecompileKernelAsync("MyKernels.MatrixMultiply");

// Subsequent executions will be faster
```

### 4. Use Source Generator Optimizations

The v0.2.0 source generator provides compile-time optimizations:

```csharp
[Kernel]
[CompilerOptimizations(EnableVectorization = true, UnrollLoops = true)]
public static void OptimizedKernel(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        // Compiler will automatically vectorize and unroll
        output[idx] = input[idx] * 2.0f + 1.0f;
    }
}
```

### 5. Monitor Performance with Built-in Profiling

```csharp
// Enable performance monitoring in DI setup
builder.Services.AddProductionOptimization(); // Includes profiling

// Access performance metrics
var profiler = serviceProvider.GetRequiredService<IKernelProfiler>();
var metrics = await profiler.GetMetricsAsync("MyKernels.VectorAdd");

Console.WriteLine($"Average execution time: {metrics.AverageExecutionTime:F2}ms");
Console.WriteLine($"Throughput: {metrics.ElementsPerSecond:F0} elements/sec");
```

## Next Steps

### Learn More

1. **[Architecture Guide](Architecture.md)** - Understand DotCompute's internal architecture
2. **[API Reference](API-Reference.md)** - Complete API documentation
3. **[Examples](Examples.md)** - Advanced usage patterns and real-world scenarios

### Advanced Features

- **Cross-Backend Debugging**: Validate results across CPU and GPU
- **Adaptive Backend Selection**: ML-powered automatic optimization  
- **Plugin System**: Extend with custom backends
- **LINQ Integration**: Use familiar LINQ syntax with compute acceleration

### Development Workflow

```bash
# Build and test your application
dotnet build --configuration Release
dotnet test --configuration Release

# Run hardware-specific tests (requires NVIDIA GPU)
dotnet test --filter "Category=Hardware" --configuration Release

# Generate code coverage
./scripts/run-coverage.sh

# Run benchmarks
dotnet run --project benchmarks --configuration Release
```

### Community and Support

- **GitHub Repository**: [DotCompute GitHub](https://github.com/YourOrg/DotCompute)
- **Documentation**: [docs/](../docs/)
- **Issue Tracker**: Report bugs and request features
- **Discussions**: Community Q&A and examples

---

**Ready to accelerate your .NET applications?** Start with the [Basic CPU Example](#basic-cpu-compute-example) and gradually explore GPU acceleration as your needs grow.