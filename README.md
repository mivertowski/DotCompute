# DotCompute

[![NuGet](https://img.shields.io/nuget/v/DotCompute.Core.svg)](https://www.nuget.org/packages/DotCompute.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)](https://github.com/mivertowski/DotCompute)
[![Coverage](https://img.shields.io/badge/Coverage-75--85%25-green)](https://github.com/mivertowski/DotCompute)

**Universal Compute Framework for .NET 9+**

DotCompute provides GPU and CPU acceleration capabilities for .NET applications through a modern C# API. Define compute kernels using `[Kernel]` attributes for automatic optimization across different hardware backends, with IDE integration and Native AOT support.

## Key Features

- **Modern C# API**: Define kernels with `[Kernel]` attributes for cleaner code organization
- **Automatic Optimization**: CPU/GPU backend selection based on workload characteristics
- **Developer Tools**: Roslyn analyzer integration with real-time feedback and code fixes
- **Cross-Backend Debugging**: Validation system to ensure consistent results across backends
- **Performance Monitoring**: Built-in telemetry and profiling capabilities
- **Native AOT Support**: Compatible with Native AOT compilation for improved startup times

## Overview

DotCompute is a compute acceleration framework for .NET applications that provides:
- CPU SIMD vectorization using AVX2/AVX512 instruction sets
- CUDA GPU acceleration for NVIDIA hardware (Compute Capability 5.0+)
- LINQ expression compilation to optimized kernels
- Reactive Extensions integration for streaming compute
- Native AOT compilation support
- Unified memory management with automatic pooling

## Production Status

### Core Components
- **Kernel API**: `[Kernel]` attribute-based development with source generators
- **CPU Backend**: AVX2/AVX512 SIMD vectorization (benchmarked 3.7x speedup on vectorizable operations)
- **CUDA Backend**: NVIDIA GPU support for Compute Capability 5.0+ devices
- **Memory Management**: Unified buffers with pooling (measured 90% allocation reduction)
- **Developer Tools**: 12 Roslyn diagnostic rules with 5 automated code fixes
- **Debugging**: Cross-backend validation for result consistency
- **Optimization**: Adaptive backend selection with performance profiling
- **Native AOT**: Full trimming support with reduced startup times
- **Testing**: Comprehensive test suite with integration and performance benchmarks

### Backend Support

| Backend | Status | Performance | Features |
|---------|--------|-------------|----------|
| **CPU** | Production | 3.7x measured speedup | AVX2/AVX512, multi-threading |
| **CUDA** | Production | GPU acceleration | P2P transfers, unified memory |
| **Metal** | In Development | - | macOS GPU support (planned) |
| **ROCm** | Planned | - | AMD GPU support (roadmap) |
| **OpenCL** | Experimental | - | Cross-vendor GPU support |

## Installation

```bash
dotnet add package DotCompute.Core --version 0.2.0-alpha
dotnet add package DotCompute.Backends.CPU --version 0.2.0-alpha
dotnet add package DotCompute.Backends.CUDA --version 0.2.0-alpha
```

## 🚀 **Quick Start - Modern Kernel API**

### **Step 1: Define Kernels with C# Attributes**

```csharp
using DotCompute.Core;
using System;

// Modern approach - pure C# with [Kernel] attribute
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

    [Kernel]
    public static void MatrixMultiply(ReadOnlySpan<float> matA, ReadOnlySpan<float> matB,
                                     Span<float> result, int width)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;

        if (row < width && col < width)
        {
            float sum = 0.0f;
            for (int k = 0; k < width; k++)
            {
                sum += matA[row * width + k] * matB[k * width + col];
            }
            result[row * width + col] = sum;
        }
    }
}
```

### **Step 2: Service Registration and Execution**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;

// Configure services
var builder = Host.CreateApplicationBuilder(args);

// Add DotCompute with production optimizations
builder.Services.AddDotComputeRuntime();
builder.Services.AddProductionOptimization();  // Intelligent backend selection
builder.Services.AddProductionDebugging();     // Cross-backend validation

var app = builder.Build();

// Execute kernels with automatic optimization
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Automatic backend selection - uses GPU if available, CPU otherwise
var result = await orchestrator.ExecuteAsync("VectorAdd", a, b, output);

// Explicit backend selection if needed
var gpuResult = await orchestrator.ExecuteAsync("MatrixMultiply",
    matA, matB, result, width, backend: "CUDA");
```

### **Step 3: Real-Time IDE Experience**

The Roslyn analyzer provides instant feedback as you type:

```csharp
[Kernel]
public void BadKernel(object param) // ❌ DC001: Must be static
//           ~~~~~~~~~ // ❌ DC002: Invalid parameter type
{
    for (int i = 0; i < 1000; i++)   // ⚠️  DC010: Use Kernel.ThreadId.X
    {
        // Missing bounds check         // ⚠️  DC011: Add bounds validation
    }
}

// ✅ Auto-fixed version after applying IDE suggestions:
[Kernel]
public static void GoodKernel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx >= data.Length) return;

    data[idx] = data[idx] * 2.0f;
}
```

## 🛠️ **Developer Experience Features**

### **Real-Time Code Analysis**

```csharp
// Visual Studio / VS Code integration provides:
// 🔍 Real-time diagnostics (12 rules)
// 💡 One-click automated fixes (5 fixes)
// 📊 Performance suggestions
// ⚡ GPU compatibility analysis

[Kernel]
public static void ImageBlur(ReadOnlySpan<byte> input, Span<byte> output, int width, int height)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;

    if (x >= width || y >= height) return;

    // IDE shows: ✅ Optimal GPU pattern detected
    //           📊 Vectorization opportunity available
    //           ⚡ Expected 4-8x speedup on target hardware

    int idx = y * width + x;
    // Blur algorithm implementation...
}
```

### **Cross-Backend Debugging & Validation**

```csharp
// Automatic validation during development
services.AddProductionDebugging(); // Enables comprehensive validation

// Debug features:
// 🔍 CPU vs GPU result comparison
// 📊 Performance analysis and bottleneck detection
// 🧪 Determinism testing across runs
// 📋 Memory access pattern validation
// ⚠️  Automatic error detection and reporting

var debugInfo = await orchestrator.ValidateKernelAsync("MyKernel", testData);
if (debugInfo.HasIssues)
{
    foreach (var issue in debugInfo.Issues)
    {
        Console.WriteLine($"⚠️  {issue.Severity}: {issue.Message}");
        Console.WriteLine($"💡 Suggestion: {issue.Recommendation}");
    }
}
```

### **Performance Intelligence & Monitoring**

```csharp
// Built-in performance profiling
services.AddProductionOptimization();

// Automatic features:
// 🤖 ML-powered backend selection
// 📊 Real-time performance monitoring
// 🎯 Workload pattern recognition
// ⚡ Automatic optimization suggestions
// 📈 Historical performance tracking

// Get performance insights
var metrics = await orchestrator.GetPerformanceMetricsAsync("VectorAdd");
Console.WriteLine($"Average execution time: {metrics.AverageExecutionTime}ms");
Console.WriteLine($"Recommended backend: {metrics.OptimalBackend}");
Console.WriteLine($"Expected speedup: {metrics.ExpectedSpeedup:F1}x");
```

## LINQ Extensions

DotCompute.Linq provides GPU-accelerated LINQ operations through expression compilation:

```csharp
using DotCompute.Linq;

// Standard LINQ automatically accelerated
var result = data
    .AsComputeQueryable()
    .Where(x => x > threshold)
    .Select(x => x * factor)
    .Sum();

// Reactive streaming with GPU acceleration
var stream = observable
    .ToComputeObservable()
    .Window(TimeSpan.FromSeconds(1))
    .SelectMany(w => w.Average())
    .Subscribe(avg => Console.WriteLine($"Average: {avg}"));
```

### Features
- **Expression Compilation**: Automatic conversion of LINQ expressions to optimized kernels
- **Streaming Compute**: Reactive Extensions integration with adaptive batching
- **Kernel Fusion**: Multiple operations combined into single kernel execution
- **Memory Optimization**: Intelligent caching and buffer reuse

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

### Development Stack

```mermaid
graph TB
    A[C# Kernel with [Kernel] Attribute] --> B[Source Generator]
    B --> C[Runtime Orchestrator]
    C --> D[Backend Selector]
    D --> E[CPU SIMD Engine]
    D --> F[CUDA GPU Engine]
    D --> G[Future: Metal/ROCm]

    H[Roslyn Analyzer] --> A
    I[Cross-Backend Debugger] --> C
    J[Performance Profiler] --> D
```

### Component Layers

#### Kernel Development
- **Source Generator**: Compile-time kernel wrapper generation from attributes
- **Roslyn Analyzer**: 12 diagnostic rules with automated fixes
- **IDE Integration**: Real-time feedback in Visual Studio and VS Code

#### Runtime Orchestration
- **IComputeOrchestrator**: Unified execution interface
- **Backend Selector**: Workload-based backend selection
- **Performance Monitor**: Metrics collection with hardware counters
- **Memory Manager**: Unified buffers with pooling

#### Backend Acceleration
- **CPU Engine**: AVX2/AVX512 SIMD vectorization
- **CUDA Engine**: NVIDIA GPU support with memory optimization
- **Planned Backends**: Metal (macOS), ROCm (AMD)

#### Developer Tools
- **Debug Service**: Cross-backend result validation
- **Profiling Service**: Performance analysis and optimization
- **Telemetry Service**: Performance tracking and historical analysis
- **Error Reporting**: Comprehensive diagnostics with actionable insights

## Performance

### Benchmarked Performance

| Operation | Dataset Size | Standard .NET | DotCompute CPU | Improvement |
|-----------|--------------|---------------|----------------|-------------|
| Vector Operations | 100K elements | 2.14ms | 0.58ms | 3.7x |
| Sum Reduction | 100K elements | 0.65ms | 0.17ms | 3.8x |
| Memory Allocations | Per operation | 48 bytes | 0 bytes | 100% reduction |

*Benchmarks performed with BenchmarkDotNet on .NET 9.0. GPU performance requires CUDA-capable hardware and varies significantly based on data size and operation complexity.*

### Performance Features

- **Automatic Backend Selection**: Chooses between CPU and GPU based on workload
- **Memory Pooling**: Reduces allocations by reusing buffers
- **Kernel Caching**: Compiled kernels are cached for reuse
- **Native AOT Support**: Enables faster startup times
- **Performance Profiling**: Built-in metrics collection and analysis

## Production Deployment

### System Requirements

#### Minimum Requirements
- .NET 9.0 Runtime
- 64-bit operating system
- 4GB RAM

#### For GPU Acceleration
- NVIDIA GPU with Compute Capability 5.0+
- CUDA Toolkit 12.0+
- Compatible NVIDIA drivers

#### For Optimal Performance
- CPU with AVX2/AVX512 support
- 16GB+ RAM for large datasets
- NVMe SSD for improved I/O

## Contributing

Contributions are welcome in the following areas:

- Performance optimizations for specific hardware
- Additional backend implementations (Metal, ROCm)
- Documentation and examples
- Bug reports and fixes
- Test coverage improvements

### Development Setup

```bash
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute

# Build the solution
dotnet build DotCompute.sln --configuration Release

# Run tests
dotnet test --configuration Release

# Run hardware-specific tests (requires NVIDIA GPU)
dotnet test --filter "Category=Hardware"
```

## License

Copyright (c) 2025 Michael Ivertowski

Licensed under the MIT License - see [LICENSE](LICENSE) file for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)
- **Documentation**: API reference and guides in the `docs/` directory

## Project Status

DotCompute v0.2.0-alpha provides a foundation for GPU and CPU compute acceleration in .NET applications. The framework includes:

- Attribute-based kernel definition system
- CPU SIMD and CUDA GPU backends
- Source generators and Roslyn analyzers
- Cross-backend debugging capabilities
- LINQ expression compilation (experimental)
- Performance monitoring and profiling tools

The project continues to evolve with planned support for additional backends and optimization strategies.