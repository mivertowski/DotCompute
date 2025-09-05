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

### Implemented Features
- ✅ Basic CPU backend with SIMD support
- ✅ Partial CUDA backend implementation
- ✅ Native AOT compilation compatibility
- ✅ Memory pooling and management
- ✅ Basic kernel compilation pipeline

### Known Limitations
- CUDA support is incomplete and may have compatibility issues
- Limited to NVIDIA GPUs (no AMD/Intel GPU support)
- Metal and ROCm backends are not implemented
- API is subject to breaking changes
- Performance optimizations are ongoing

## Installation

```bash
dotnet add package DotCompute.Core --version 0.1.0-alpha
dotnet add package DotCompute.Backends.CPU --version 0.1.0-alpha
dotnet add package DotCompute.Backends.CUDA --version 0.1.0-alpha
```

## Basic Usage

```csharp
using DotCompute.Core;
using DotCompute.Backends.CPU;

// Create an accelerator
var accelerator = new CpuAccelerator();

// Define a simple kernel
var kernel = new KernelDefinition
{
    Name = "VectorAdd",
    Code = @"
        void VectorAdd(float* a, float* b, float* result, int length) {
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < length) {
                result[idx] = a[idx] + b[idx];
            }
        }"
};

// Compile and execute (simplified example)
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

DotCompute uses a modular architecture with clear separation between:
- **Core**: Abstract interfaces and kernel definitions
- **Backends**: Device-specific implementations (CPU, CUDA)
- **Memory**: Unified memory management system
- **Runtime**: Service orchestration and plugin management

## Performance Expectations

Performance characteristics are highly workload and hardware dependent:
- CPU SIMD can provide 2-8x speedup for vectorizable operations
- GPU acceleration benefits are most pronounced for large parallel workloads
- Native AOT reduces startup time but may impact peak performance

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