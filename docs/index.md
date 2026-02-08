# DotCompute Documentation

Welcome to the DotCompute documentation. DotCompute is a high-performance, Native AOT-compatible universal compute framework for .NET 9+ with CPU, CUDA, Metal and OpenCL acceleration.

**Current Version**: v0.6.2 (NuGet Packaging & Infrastructure Release)

## Quick Links

- [Getting Started](articles/getting-started.md)
- [API Reference](~/api/index.md)
- [Architecture](articles/architecture/overview.md)
- [Performance Guide](articles/guides/performance-tuning.md)
- [GitHub Repository](https://github.com/mivertowski/DotCompute)

## What is DotCompute?

DotCompute provides GPU-accelerated compute capabilities for .NET applications with:

- **Production-Ready Performance**: Measured 3.7x-92x speedup over CPU on real workloads
- **Multiple Backends**: CPU (SIMD), CUDA, plus experimental Metal & OpenCL support
- **Native AOT Compatible**: Sub-10ms startup times, zero runtime code generation
- **Simple API**: Write kernels once in C# with `[Kernel]` attributes
- **Comprehensive Testing**: 80%+ pass rate across components

## Status

| Component | Status | Description |
|-----------|--------|-------------|
| **Core Runtime** | ✅ Production Ready | Orchestration, debugging, optimization |
| **CPU Backend** | ✅ Production Ready | SIMD vectorization (AVX512/AVX2/NEON) - 3.7x speedup |
| **CUDA Backend** | ✅ Production Ready | NVIDIA GPU support (CC 5.0+) - 21-92x speedup |
| **Memory Management** | ✅ Production Ready | Unified memory with 90% pooling |
| **Ring Kernels** | ✅ Production Ready | Persistent GPU computation (Phase 5 complete - 94/94 tests) |
| **Metal Backend** | ✅ Feature-Complete | Apple Silicon support with MSL translation |
| **OpenCL Backend** | ⚠️ Experimental | Cross-platform GPU support |
| **LINQ Extensions** | 🚧 80% Complete | GPU-accelerated LINQ queries (43/54 tests) |
| **GPU Atomics** | ✅ Production Ready | Lock-free concurrent operations (v0.5.2) |

## System Requirements

- **.NET 9.0** or later
- **For GPU Acceleration**:
  - NVIDIA GPU with CUDA 12.0+ (Compute Capability 5.0+)
  - Apple Silicon M1/M2/M3 with Metal 2.4+
  - OpenCL 1.2+ compatible device

## Installation

```bash
# Core runtime (stable)
dotnet add package DotCompute.Core --version 0.6.2

# CPU backend (stable)
dotnet add package DotCompute.Backends.CPU --version 0.6.2

# CUDA backend (stable)
dotnet add package DotCompute.Backends.CUDA --version 0.6.2

# OpenCL backend (experimental - cross-platform GPU)
dotnet add package DotCompute.Backends.OpenCL --version 0.6.2

# Metal backend (feature-complete - macOS / Apple Silicon)
dotnet add package DotCompute.Backends.Metal --version 0.6.2
```

## Quick Example

```csharp
using DotCompute.Abstractions;

// Define kernel
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
        result[idx] = a[idx] + b[idx];
}

// Configure services
var services = new ServiceCollection();
services.AddDotComputeRuntime();
var provider = services.BuildServiceProvider();

// Execute on GPU
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
var result = await orchestrator.ExecuteKernelAsync<float[], float[]>(
    nameof(VectorAdd),
    new { a = dataA, b = dataB, length = 1_000_000 }
);
```

## Architecture

DotCompute follows a layered architecture:

```
┌─────────────────────────────────────────┐
│   Application Code ([Kernel] methods)   │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│  Source Generators & Analyzers          │
│  (Compile-time code generation)         │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│  Core Runtime & Orchestration           │
│  (Debugging, optimization, telemetry)   │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│  Backend Implementations                │
│  (CPU, CUDA, Metal, OpenCL)            │
└─────────────────────────────────────────┘
            ↓
┌─────────────────────────────────────────┐
│  Memory Management                      │
│  (Unified buffers, pooling, transfers)  │
└─────────────────────────────────────────┘
```

## Performance Claims

All performance claims are validated through automated benchmarks:

| Operation | Hardware | CPU Time | GPU Time | Speedup |
|-----------|----------|----------|----------|---------|
| Vector Add (10M floats) | RTX 2000 Ada | 45ms | 2.1ms | **21x** |
| Matrix Multiply (2048×2048) | RTX 2000 Ada | 8.2s | 89ms | **92x** |
| FFT (1M complex) | RTX 2000 Ada | 156ms | 8.4ms | **18x** |
| SIMD Vector Add (10M) | Intel Core Ultra 7 | 4.33ms | 187μs | **23x** |

## Key Features

### Production-Grade Quality
- **Comprehensive Testing**: 1700+ tests passing
- **Code Coverage**: ~85% across core components
- **Real-World Validation**: Benchmarked on production workloads
- **Error Handling**: Comprehensive fault tolerance and recovery

### Developer Experience
- **IDE Integration**: Real-time analyzer feedback in VS/VS Code
- **Source Generators**: Automatic kernel wrapper generation
- **12 Diagnostic Rules**: DC001-DC012 for kernel quality
- **5 Automated Fixes**: Quick fixes in IDE

### Performance Features
- **Adaptive Backend Selection**: ML-powered optimal backend choice
- **Kernel Fusion**: Automatic operation combining
- **Memory Pooling**: 90% allocation reduction
- **Cross-Backend Debugging**: CPU vs GPU validation

### Enterprise Features
- **Native AOT Support**: Sub-10ms cold start
- **Telemetry Integration**: OpenTelemetry metrics and tracing
- **Dependency Injection**: Full Microsoft.Extensions.DependencyInjection support
- **Plugin System**: Hot-reload capable plugin architecture

## Documentation Structure

- **[Articles](articles/index.md)**: Guides, tutorials, and conceptual documentation
  - [Getting Started](articles/getting-started.md)
  - [Architecture](articles/architecture/overview.md)
  - [Performance Tuning](articles/guides/performance-tuning.md)
  - [Debugging Guide](articles/guides/debugging-guide.md)
  - [Native AOT](articles/guides/native-aot.md)

- **[API Reference](~/api/index.md)**: Complete API documentation
  - [DotCompute.Abstractions](~/api/DotCompute.Abstractions.yml)
  - [DotCompute.Core](~/api/DotCompute.Core.yml)
  - [Backend APIs](~/api/index.md#backends)

## Limitations

**Experimental Backends:**
- **OpenCL backend**: Cross-platform support functional but not fully production-tested
- **ROCm backend**: Placeholder only, AMD GPU support not yet implemented

**Feature Limitations:**
- LINQ extensions: 80% complete, missing Join/GroupBy/OrderBy operations
- Ring Kernels on WSL2: EventDriven mode only (persistent mode has memory visibility issues)

**Platform Limitations:**
- CUDA: Requires NVIDIA GPU with CC 5.0+ (Maxwell or newer)
- Metal: Requires macOS 12.0+ with Metal 2.4+
- Native AOT: Some reflection-based features unavailable

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](../CONTRIBUTING.md) for guidelines.

## License

DotCompute is licensed under the MIT License. See [LICENSE](../LICENSE) for details.

## Support

- **GitHub Issues**: [Report bugs and request features](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [Ask questions and share ideas](https://github.com/mivertowski/DotCompute/discussions)
- **Documentation**: This site

---

(c) 2025-2026 Michael Ivertowski

---

**Built with professional quality • Validated performance claims • Honest limitations**
