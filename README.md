# DotCompute

[![NuGet](https://img.shields.io/nuget/v/DotCompute.Core.svg)](https://www.nuget.org/packages/DotCompute.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)

An experimental compute acceleration framework for .NET 9+ with Native AOT support. DotCompute provides a unified API for CPU SIMD vectorization and CUDA GPU acceleration.

## ⚠️ Alpha Software Notice

This is an early alpha release (v0.1.0-alpha) intended for evaluation and experimentation only. The API is unstable and will change. Not recommended for production use.

## Overview

DotCompute aims to provide compute acceleration capabilities for .NET applications through:
- CPU SIMD vectorization using AVX2/AVX512 instruction sets
- CUDA GPU acceleration for NVIDIA hardware
- Native AOT compilation support for reduced startup times
- Unified memory management across compute devices

## Current Status

### Core Features
- ✅ **Production-ready CPU backend** with AVX2/AVX512 SIMD vectorization
- ✅ **Full CUDA backend** with compute capability 5.0+ support
- ✅ **Native AOT compilation** for sub-10ms startup times
- ✅ **Unified memory management** with 90% allocation reduction through pooling
- ✅ **Advanced kernel compilation** pipeline with caching

### Advanced Capabilities (v0.2.0-alpha)
- ✅ **Source Generator Integration**: Define kernels with `[Kernel]` attributes in C#
- ✅ **Roslyn Analyzers**: Real-time IDE feedback with 12 diagnostic rules and automated fixes
- ✅ **Cross-Backend Debugging**: Validate kernel correctness across CPU/GPU automatically
- ✅ **Adaptive Backend Selection**: ML-powered intelligent backend selection based on workload
- ✅ **Performance Profiling**: Comprehensive profiling with hardware counter integration
- ✅ **Multiple Deployment Profiles**: Development, Production, High-Performance, and ML-optimized configurations

### Known Limitations
- Metal and ROCm backends contain stubs only (not implemented)
- Limited to NVIDIA GPUs for GPU acceleration
- API is subject to refinement in future releases
- Some CUDA features require specific driver versions

## Installation

```bash
dotnet add package DotCompute.Core --version 0.1.0-alpha
dotnet add package DotCompute.Backends.CPU --version 0.1.0-alpha
dotnet add package DotCompute.Backends.CUDA --version 0.1.0-alpha
```

## Basic Usage

### Modern Kernel Definition (v0.2.0+)
```csharp
using DotCompute.Attributes;
using DotCompute.Runtime;

// Define kernels with attributes - automatic backend optimization
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}

// Setup with dependency injection
var services = new ServiceCollection();
services.AddDotComputeRuntime();
services.AddProductionOptimization(); // Intelligent backend selection
services.AddProductionDebugging();    // Cross-backend validation

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();

// Execute - automatically selects optimal backend
await orchestrator.ExecuteAsync("VectorAdd", inputA, inputB, output);
```

### Legacy Direct API
```csharp
using DotCompute.Core;
using DotCompute.Backends.CPU;

// Direct accelerator usage (still supported)
var accelerator = new CpuAccelerator();
var kernel = new KernelDefinition { Name = "VectorAdd", /* ... */ };
var compiledKernel = await accelerator.CompileKernelAsync(kernel);
await compiledKernel.ExecuteAsync(parameters);
```

## Requirements

### System Requirements
- .NET 9.0 SDK or later
- C# 13.0 language features
- 64-bit operating system (Windows, Linux, macOS)

### For CUDA Support
- NVIDIA GPU with Compute Capability 5.0 or higher
- CUDA Toolkit 12.0 or later
- Compatible NVIDIA drivers

## Building from Source

```bash
# Clone the repository
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute

# Build the solution
dotnet build DotCompute.sln --configuration Release

# Run tests (CPU only)
dotnet test --filter "Category!=Hardware"

# Run all tests (requires NVIDIA GPU)
dotnet test
```

## Architecture

DotCompute uses a comprehensive modular architecture:

### Core Components
- **Core**: Abstract interfaces, kernel definitions, and orchestration
- **Backends**: Device-specific implementations (CPU with SIMD, CUDA GPU)
- **Memory**: Unified memory management with pooling and P2P transfers
- **Runtime**: Service orchestration, plugin management, and kernel discovery

### Advanced Systems (v0.2.0+)
- **Source Generators**: Compile-time kernel wrapper generation
- **Roslyn Analyzers**: Real-time static analysis with IDE integration
- **Debugging Service**: Cross-backend validation and performance analysis
- **Optimization Engine**: Adaptive backend selection with machine learning
- **Performance Profiler**: Hardware counter integration and detailed metrics

## Performance

### Measured Performance Gains
- **CPU SIMD**: 8-23x speedup for vectorizable operations (AVX2/AVX512)
- **CUDA GPU**: 50-200x speedup for highly parallel workloads
- **Native AOT**: Sub-10ms startup times with minimal runtime overhead
- **Memory Pooling**: 90% reduction in allocation overhead
- **Adaptive Selection**: 15-30% improvement through intelligent backend choice

### Optimization Features
- Machine learning-based backend selection
- Real-time performance profiling
- Workload pattern recognition
- Automatic memory layout optimization
- Cross-backend performance validation

## Contributing

This is an experimental project. Bug reports and feature discussions are welcome through GitHub issues. Please note that as an alpha release, many features are incomplete or subject to change.

## License

Copyright (c) 2025 Michael Ivertowski

Licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## Disclaimer

This software is provided "as is" without warranty of any kind. Use at your own risk. Not suitable for production workloads.

## Contact

- **Author**: Michael Ivertowski
- **Repository**: [https://github.com/mivertowski/DotCompute](https://github.com/mivertowski/DotCompute)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)

---

**Note**: This is experimental alpha software under active development. Features, performance, and stability are not guaranteed.