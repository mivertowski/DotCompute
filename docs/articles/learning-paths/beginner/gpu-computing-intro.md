# Introduction to GPU Computing

This module introduces the fundamental concepts of GPU computing and helps you understand when GPU acceleration provides meaningful benefits.

## CPU vs GPU Architecture

### CPU: Few Powerful Cores

CPUs are optimized for sequential tasks with complex control flow:

- **4-64 cores** optimized for single-thread performance
- Large caches (L1, L2, L3) for low-latency memory access
- Branch prediction and out-of-order execution
- Ideal for: business logic, I/O operations, complex algorithms

### GPU: Many Simple Cores

GPUs are optimized for parallel data processing:

- **Thousands of cores** optimized for throughput
- High memory bandwidth (hundreds of GB/s)
- SIMD execution (Single Instruction, Multiple Data)
- Ideal for: matrix operations, image processing, simulations

## When to Use GPU Acceleration

### Good Candidates

| Workload | Why GPU Helps |
|----------|---------------|
| Matrix multiplication | Highly parallel, regular memory access |
| Image/video processing | Independent pixel operations |
| Scientific simulations | Parallel numerical computations |
| Machine learning inference | Batch processing of data |
| Signal processing (FFT) | Parallel frequency domain operations |

### Poor Candidates

| Workload | Why GPU Hurts |
|----------|---------------|
| Small data sets (<1000 elements) | Transfer overhead dominates |
| Sequential algorithms | Cannot parallelize |
| Branch-heavy code | GPU threads diverge |
| Random memory access | Poor memory coalescing |
| I/O-bound operations | GPU cannot help |

## GPU Programming Model

### Threads and Thread Blocks

GPU computation is organized hierarchically:

```
Grid (entire computation)
└── Thread Blocks (groups of threads)
    └── Threads (individual execution units)
```

**Key concepts:**

- **Thread**: Smallest unit of execution
- **Thread Block**: Group of threads that can synchronize
- **Grid**: Collection of thread blocks

### Example: Vector Addition

Adding two arrays of 10,000 elements:

```
CPU approach: 1 thread processes 10,000 elements sequentially
GPU approach: 10,000 threads each process 1 element in parallel
```

## DotCompute's Approach

DotCompute simplifies GPU programming through:

### 1. Declarative Kernel Definition

```csharp
using DotCompute.Generators.Kernel.Attributes;

public static partial class MyKernels
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

### 2. Automatic Backend Selection

DotCompute detects available hardware and selects the best backend:

- **CUDA**: NVIDIA GPUs (highest performance) - Production
- **CPU**: SIMD-optimized fallback - Production
- **Metal**: Apple Silicon (native macOS/iOS) - Experimental
- **OpenCL**: AMD, Intel, and cross-vendor - Experimental

### 3. Source Generation

The `[Kernel]` attribute triggers compile-time code generation:

- No runtime reflection
- Native AOT compatible
- Optimized for each backend

## Performance Expectations

### Realistic Speedups

| Operation | Data Size | Typical Speedup |
|-----------|-----------|-----------------|
| Vector operations | 1M elements | 10-50x |
| Matrix multiplication | 1024x1024 | 20-100x |
| Image convolution | 4K image | 30-80x |
| Reduction (sum) | 10M elements | 5-20x |

### Break-Even Points

GPU acceleration has overhead. Typical break-even points:

- **CPU SIMD**: ~100 elements (3.7x speedup)
- **GPU**: ~10,000 elements (21-92x speedup)

Below these thresholds, CPU processing is often faster due to transfer overhead.

## Hands-On Exercise

Before proceeding, verify your environment:

```bash
# Check .NET version
dotnet --version

# Create a test project
dotnet new console -n GpuTest
cd GpuTest

# Add DotCompute packages
dotnet add package DotCompute.Core
dotnet add package DotCompute.Abstractions
dotnet add package DotCompute.Runtime
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Generators
```

Create `Program.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Abstractions.Factories;

// Build host with DotCompute services
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

// Get the accelerator factory
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();

// Enumerate available devices
var devices = await factory.GetAvailableDevicesAsync();

Console.WriteLine("Available devices:");
foreach (var device in devices)
{
    Console.WriteLine($"  - {device.DeviceType}: {device.Name}");
    Console.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
}
```

Run and verify you see at least one device (CPU is always available).

## Key Takeaways

1. **GPUs excel at parallel data processing** with thousands of simple cores
2. **Not all workloads benefit** from GPU acceleration
3. **Data transfer overhead** determines minimum efficient data size
4. **DotCompute abstracts complexity** while providing full control when needed

## Next Module

[Your First Kernel →](first-kernel.md)

Learn to write and execute a complete GPU kernel.
