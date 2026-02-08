# DotCompute

[![NuGet](https://img.shields.io/nuget/v/DotCompute.Core.svg)](https://www.nuget.org/packages/DotCompute.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0+-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Build](https://github.com/mivertowski/DotCompute/actions/workflows/ci.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions)

**High-performance GPU and CPU compute acceleration for .NET**

DotCompute enables .NET developers to write compute kernels in pure C# and execute them across GPUs and CPUs with automatic optimization. Define kernels using attributes, and the framework handles compilation, memory management, and backend selection.

```csharp
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
        result[idx] = a[idx] + b[idx];
}
```

---

## Features

### Kernel Development
- **Attribute-Based API** - Define kernels with `[Kernel]` and `[RingKernel]` attributes using familiar C# syntax
- **Source Generators** - Compile-time code generation for optimal performance with Native AOT support
- **IDE Integration** - 12 Roslyn diagnostic rules with real-time feedback and 5 automated code fixes

### GPU Acceleration
- **Multi-Backend Support** - CUDA (NVIDIA), OpenCL (cross-platform), Metal (Apple), and CPU SIMD
- **Automatic Backend Selection** - Intelligent workload-based routing between CPU and GPU
- **Unified Memory** - Seamless data transfer with 90% allocation reduction through pooling

### Performance
- **SIMD Vectorization** - AVX2/AVX512/NEON support with measured 3.7x CPU speedup
- **GPU Acceleration** - 21-92x speedup on CUDA (benchmarked on RTX 2000 Ada)
- **Kernel Fusion** - Automatic operation merging for 50-80% bandwidth reduction

### Advanced Capabilities
- **Ring Kernels** - Persistent GPU computation with lock-free message passing for actor systems
- **Atomic Operations** - Lock-free concurrent access (`AtomicAdd`, `AtomicCAS`, `AtomicMin/Max`)
- **LINQ Integration** - GPU-accelerated LINQ queries with automatic kernel compilation
- **Cross-Backend Debugging** - Validate results across CPU and GPU for correctness

---

## Quick Start

### 1. Install the packages

```bash
dotnet add package DotCompute.Core
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Backends.CUDA  # For NVIDIA GPUs
```

### 2. Define a kernel

```csharp
using DotCompute.Core;

public static class Kernels
{
    [Kernel]
    public static void Scale(ReadOnlySpan<float> input, Span<float> output, float factor)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
            output[idx] = input[idx] * factor;
    }
}
```

### 3. Execute with dependency injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;

var services = new ServiceCollection();
services.AddDotComputeRuntime();
services.AddProductionOptimization();

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();

// Automatic backend selection (GPU if available, CPU otherwise)
await orchestrator.ExecuteAsync("Scale", input, output, 2.0f);
```

---

## Installation

```bash
# Core framework
dotnet add package DotCompute.Core

# Backends (install what you need)
dotnet add package DotCompute.Backends.CPU     # SIMD-optimized CPU execution
dotnet add package DotCompute.Backends.CUDA    # NVIDIA GPU (requires CUDA Toolkit)
dotnet add package DotCompute.Backends.OpenCL  # Cross-platform GPU (experimental)
dotnet add package DotCompute.Backends.Metal   # Apple Silicon / macOS

# Extensions
dotnet add package DotCompute.Linq             # GPU-accelerated LINQ
dotnet add package DotCompute.Algorithms       # Common parallel algorithms
```

---

## Backend Support

| Backend | Status | Performance | Hardware |
|---------|--------|-------------|----------|
| **CPU** | Production | 3.7x (SIMD) | AVX2/AVX512/NEON processors |
| **CUDA** | Production | 21-92x | NVIDIA GPUs (Compute Capability 5.0+) |
| **Metal** | Production | Native acceleration | Apple Silicon, Intel Macs (2016+) |
| **OpenCL** | Experimental | Cross-platform | NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno |

---

## Code Examples

### GPU-Accelerated LINQ

```csharp
using DotCompute.Linq;

var result = data
    .AsComputeQueryable()
    .Where(x => x > threshold)
    .Select(x => x * factor)
    .Sum();  // Executes on GPU automatically
```

### Atomic Operations

```csharp
[Kernel]
public static void Histogram(ReadOnlySpan<int> values, Span<int> bins)
{
    int idx = Kernel.ThreadId.X;
    if (idx < values.Length)
    {
        int bin = values[idx] / 10;
        AtomicOps.AtomicAdd(ref bins[bin], 1);
    }
}
```

### Ring Kernels (Persistent GPU Computation)

```csharp
[RingKernel(Mode = RingKernelMode.Persistent, Capacity = 10000)]
public static void ProcessMessages(
    IMessageQueue<Message> incoming,
    IMessageQueue<Message> outgoing,
    Span<float> state)
{
    int id = Kernel.ThreadId.X;

    while (incoming.TryDequeue(out var msg))
    {
        state[id] += msg.Value;
        outgoing.Enqueue(new Message { Id = id, Value = state[id] });
    }
}
```

### Cross-Backend Debugging

```csharp
services.AddProductionDebugging();

var debugService = provider.GetRequiredService<IKernelDebugService>();
var result = await debugService.ValidateKernelAsync("MyKernel", testData);

if (!result.IsValid)
{
    foreach (var issue in result.Issues)
        Console.WriteLine($"{issue.Severity}: {issue.Message}");
}
```

---

## Performance

Benchmarks performed with BenchmarkDotNet on .NET 9.0:

| Operation | Dataset | Baseline | DotCompute | Speedup |
|-----------|---------|----------|------------|---------|
| Vector Add | 100K floats | 2.14ms | 0.58ms | **3.7x** (CPU SIMD) |
| Matrix Multiply | 1024x1024 | 850ms | 9.2ms | **92x** (CUDA) |
| Sum Reduction | 1M elements | 10ms | 0.3ms | **33x** (CUDA) |
| Filter + Map | 1M elements | 35ms | 1.5ms | **23x** (fused kernel) |

*GPU benchmarks on NVIDIA RTX 2000 Ada. Results vary by hardware and workload.*

---

## Requirements

### Minimum
- .NET 9.0 SDK
- 64-bit OS (Windows, Linux, macOS)

### For CUDA
- NVIDIA GPU (Compute Capability 5.0+)
- [CUDA Toolkit 12.0+](https://developer.nvidia.com/cuda-downloads)

### For Metal
- macOS 10.13+ (High Sierra)
- Metal-capable GPU

### For OpenCL
- OpenCL 1.2+ runtime from your GPU vendor

### WSL2 Limitations

WSL2 has limited GPU memory coherence that affects advanced features:

| Feature | Native Linux | WSL2 |
|---------|-------------|------|
| Basic kernels | Full support | Full support |
| Persistent ring kernels | Sub-ms latency | ~5s latency |
| System-scope atomics | Works | Unreliable |

For production workloads requiring low latency, use native Linux.

---

## Documentation

### Guides
- [Getting Started](docs/articles/getting-started.md)
- [Kernel Development](docs/articles/guides/kernel-development.md)
- [Backend Selection](docs/articles/guides/backend-selection.md)
- [Performance Tuning](docs/articles/guides/performance-tuning.md)
- [Memory Management](docs/articles/guides/memory-management.md)
- [Multi-GPU Programming](docs/articles/guides/multi-gpu.md)
- [Native AOT](docs/articles/guides/native-aot.md)
- [Debugging](docs/articles/guides/debugging-guide.md)

### Advanced Topics
- [GPU Timing API](docs/articles/guides/timing-api.md) - Nanosecond-precision timestamps
- [Barrier API](docs/articles/guides/barrier-api.md) - Hardware-accelerated synchronization
- [Memory Ordering](docs/articles/guides/memory-ordering-api.md) - Consistency models and fences

### Reference
- [Diagnostic Rules (DC001-DC012)](docs/articles/reference/diagnostic-rules.md)
- [API Documentation](https://mivertowski.github.io/DotCompute/)

---

## Building from Source

```bash
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute

# Build
dotnet build DotCompute.sln --configuration Release

# Run tests (CPU only)
dotnet test --filter "Category!=Hardware"

# Run all tests (requires NVIDIA GPU)
./scripts/run-tests.sh DotCompute.sln
```

---

## Contributing

Contributions welcome in these areas:

- Performance optimizations
- Additional backend implementations
- Documentation improvements
- Bug fixes and test coverage

See the [contribution guidelines](CONTRIBUTING.md) for details.

---

## License

MIT License - see [LICENSE](LICENSE) for details.

Copyright (c) 2023 - 2026 Michael Ivertowski
