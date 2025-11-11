# DotCompute

[![NuGet](https://img.shields.io/nuget/v/DotCompute.Core.svg)](https://www.nuget.org/packages/DotCompute.Core/)
[![Release](https://img.shields.io/github/v/release/mivertowski/DotCompute?include_prereleases)](https://github.com/mivertowski/DotCompute/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Build Status](https://img.shields.io/badge/Build-Passing-brightgreen)](https://github.com/mivertowski/DotCompute)
[![Coverage](https://img.shields.io/badge/Coverage-75--85%25-green)](https://github.com/mivertowski/DotCompute)

**Universal Compute Framework for .NET 9+** | **[v0.4.1-rc3 Released](https://github.com/mivertowski/DotCompute/releases/tag/v0.4.1-rc3)** 🎉

DotCompute provides production-ready GPU and CPU acceleration capabilities for .NET applications through a modern C# API. Define compute kernels using `[Kernel]` and `[RingKernel]` attributes for automatic optimization across different hardware backends, with comprehensive IDE integration and Native AOT support.

## Key Features

- **Modern C# API**: Define kernels with `[Kernel]` and `[RingKernel]` attributes for cleaner code organization
- **Persistent Ring Kernels**: GPU-resident actor systems with lock-free message passing for graph analytics and spatial simulations
- **Automatic Optimization**: CPU/GPU backend selection based on workload characteristics
- **Cross-Platform GPU**: Full OpenCL support for NVIDIA, AMD, Intel, and ARM GPUs, as well as specialized backends for Cuda, Metal and CPU SIMD
- **High-Precision GPU Timing**: Nanosecond-resolution timing with 4 calibration strategies for CPU-GPU clock synchronization
- **Developer Tools**: Roslyn analyzer integration with real-time feedback and code fixes
- **Cross-Backend Debugging**: Validation system to ensure consistent results across backends
- **Performance Monitoring**: Built-in telemetry and profiling capabilities
- **Native AOT Support**: Compatible with Native AOT compilation for improved startup times

## Overview

DotCompute is a compute acceleration framework for .NET applications that provides:
- CPU SIMD vectorization using AVX2/AVX512 instruction sets
- CUDA GPU acceleration for NVIDIA hardware (Compute Capability 5.0+)
- OpenCL cross-platform GPU support (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- Ring Kernel persistent GPU computation with message passing capabilities
- **Production-ready GPU kernel generation** from LINQ expressions with automatic optimization
- Kernel fusion optimization (50-80% bandwidth reduction for chained operations)
- Reactive Extensions integration for streaming compute
- Native AOT compilation support
- Unified memory management with automatic pooling

## Production Status (v0.4.1-rc3)

**Released:** November 10, 2025 | **[Release Notes](https://github.com/mivertowski/DotCompute/releases/tag/v0.4.1-rc3)** | **[NuGet Packages](https://www.nuget.org/packages?q=DotCompute)**

### Core Components (Production-Ready)
- **Kernel API**: `[Kernel]` attribute-based development with source generators and automatic GPU compilation
- **CPU Backend**: AVX2/AVX512 SIMD vectorization with measured 3.7x speedup (2.14ms → 0.58ms on vector operations)
- **CUDA Backend**: NVIDIA GPU support for Compute Capability 5.0-8.9 with 21-92x measured speedup on RTX 2000 Ada
- **OpenCL Backend**: Cross-platform GPU acceleration for NVIDIA, AMD, Intel, ARM Mali, and Qualcomm Adreno
- **LINQ Integration**: End-to-end GPU acceleration from LINQ queries to hardware execution (Phase 6 complete)
- **GPU Timing API**: High-precision nanosecond timestamps with 4 calibration strategies and automatic timestamp injection
- **Barrier API**: Hardware-accelerated GPU synchronization with 5 barrier scopes including multi-GPU system barriers
- **Memory Ordering API**: Causal memory ordering and fence operations with 3 consistency models (Relaxed, ReleaseAcquire, Sequential)
- **Memory Management**: Unified buffers with pooling achieving 90% allocation reduction
- **Developer Tools**: 12 Roslyn diagnostic rules (DC001-DC012) with 5 automated code fixes
- **Debugging**: Cross-backend validation system for CPU vs GPU result consistency
- **Optimization**: Adaptive backend selection with ML-powered performance profiling
- **Native AOT**: Full trimming support with sub-10ms startup times
- **Testing**: 80% test coverage with comprehensive integration and performance benchmarks

### Backend Support

| Backend | Status | Performance | Features |
|---------|--------|-------------|----------|
| **CPU** | Production | 3.7x measured speedup | AVX2/AVX512, multi-threading, Ring Kernels |
| **CUDA** | Production | GPU acceleration | P2P transfers, unified memory, Ring Kernels |
| **OpenCL** | Production | Cross-platform GPU | Multi-vendor support, Ring Kernels |
| **Metal** | Production | Native GPU acceleration | MPS operations, 90% memory pooling efficiency, binary caching |
| **ROCm** | Planned | - | AMD GPU support (roadmap) |

## Installation

```bash
dotnet add package DotCompute.Core --version 0.4.1-rc3
dotnet add package DotCompute.Backends.CPU --version 0.4.1-rc3
dotnet add package DotCompute.Backends.CUDA --version 0.4.1-rc3
dotnet add package DotCompute.Backends.OpenCL --version 0.4.1-rc3  # Cross-platform GPU
dotnet add package DotCompute.Backends.Metal --version 0.4.1-rc3   # Apple Silicon / macOS
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

## LINQ Extensions - End-to-End GPU Integration (Production Ready)

DotCompute.Linq provides **production-ready end-to-end GPU acceleration** with complete query provider integration. The system automatically compiles LINQ operations into optimized GPU kernels and executes them across CUDA, OpenCL, and Metal backends with zero configuration required.

**Phase 6 Complete**: GPU kernel compilation and execution fully integrated into the LINQ query provider, enabling transparent GPU acceleration for all supported LINQ operations.

> **📖 For detailed implementation guide, see [LINQ GPU Integration README](src/Extensions/DotCompute.Linq/README.md)**
> **📖 For GPU kernel generation details, see [GPU Kernel Generation Guide](~/docs/articles/advanced/gpu-kernel-generation.md)**

### Key Features

- **Automatic GPU Acceleration**: Zero-configuration GPU execution for LINQ queries
- **Multi-Backend Support**: Seamless CUDA, OpenCL, and Metal backend integration
- **Intelligent Fallback**: Automatic CPU execution when GPU unavailable or on failure
- **Kernel Fusion**: 50-80% memory bandwidth reduction for chained operations
- **Production Testing**: Comprehensive test suite with 80% pass rate

### Quick Start

```csharp
using DotCompute.Linq;

// Standard LINQ automatically accelerated on GPU (no configuration needed)
var result = data
    .AsComputeQueryable()
    .Where(x => x > threshold)
    .Select(x => x * factor)
    .Sum();

// Kernel fusion automatically combines multiple operations
var optimized = data
    .AsComputeQueryable()
    .Select(x => x * 2.0f)        // Map
    .Where(x => x > 1000.0f)      // Filter
    .Select(x => x + 100.0f)      // Map
    .ToComputeArray();            // Single fused GPU kernel!

// Reactive streaming with GPU acceleration
var stream = observable
    .ToComputeObservable()
    .Window(TimeSpan.FromSeconds(1))
    .SelectMany(w => w.Average())
    .Subscribe(avg => Console.WriteLine($"Average: {avg}"));
```

### Production-Ready Features (Phase 6: 100% Complete)

#### ✅ End-to-End GPU Integration
- **Query Provider Integration**: GPU compilation and execution fully integrated into LINQ pipeline
- **Zero Configuration**: Automatic GPU acceleration without explicit backend selection
- **Graceful Degradation**: Multi-level fallback system ensures CPU execution on any GPU failure
- **9-Stage Execution Pipeline**: Expression analysis → GPU compilation → execution with intelligent fallback

#### ✅ GPU Kernel Generation
- **Three GPU Backends**: CUDA, OpenCL, and Metal with full feature parity
- **Automatic Compilation**: LINQ expressions → optimized GPU kernels
- **Operation Support**: Map, Filter, Reduce operations with more coming
- **Runtime Compilation**: NVRTC for CUDA, runtime compilation for OpenCL/Metal

#### ✅ Kernel Fusion Optimization
- **Automatic Merging**: Combines multiple LINQ operations into single GPU kernel
- **Bandwidth Reduction**: 50-80% reduction in memory transfers
- **Supported Patterns**: Map→Filter, Filter→Map, Map→Map, Filter→Filter
- **Example Performance**: 3-operation chain becomes 1 kernel (66.7% bandwidth reduction)

#### ✅ Filter Compaction (Stream Compaction)
- **Atomic Operations**: Thread-safe output allocation for variable-length results
- **Backend Support**: CUDA `atomicAdd()`, OpenCL `atomic_inc()`, Metal `atomic_fetch_add_explicit()`
- **Memory Efficiency**: Compact output with no wasted space

#### ✅ Cross-Backend Support
- **CUDA**: NVIDIA GPUs, Compute Capability 5.0+ (Maxwell through Ada Lovelace)
- **OpenCL**: Cross-platform (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- **Metal**: Apple Silicon and discrete GPUs on macOS

### Expected Performance

Based on GPU architecture and workload characteristics:

| Operation | Data Size | Standard LINQ | GPU (CUDA/OpenCL/Metal) | Expected Speedup |
|-----------|-----------|---------------|------------------------|-----------------|
| Map (x * 2) | 1M elements | ~15ms | 0.5-1.5ms | **10-30x** |
| Filter (x > 5000) | 1M elements | ~12ms | 1-2ms | **6-12x** |
| Reduce (Sum) | 1M elements | ~10ms | 0.3-1ms | **10-33x** |
| Fused (Map→Filter→Map) | 1M elements | ~35ms | 1.5-3ms | **12-23x** |

*Performance varies based on GPU architecture, data size, and operation complexity. Benchmarks should be performed for production workloads.*

### Additional Features
- **Streaming Compute**: Reactive Extensions integration with adaptive batching
- **Memory Optimization**: Intelligent caching and buffer reuse
- **Expression Analysis**: Type inference and dependency detection
- **Error Handling**: Comprehensive diagnostics with actionable error messages

## Ring Kernels - GPU-Resident Actor Systems

Ring Kernels enable persistent GPU computation with lock-free message passing, ideal for graph analytics, spatial simulations, and actor-based systems.

### Persistent Kernel Example

```csharp
using DotCompute.Abstractions.RingKernels;

// Define a persistent ring kernel for PageRank algorithm
[RingKernel(
    KernelId = "pagerank-vertex",
    Domain = RingKernelDomain.GraphAnalytics,
    Mode = RingKernelMode.Persistent,
    Capacity = 10000,
    InputQueueSize = 256,
    OutputQueueSize = 256)]
public static void PageRankVertex(
    IMessageQueue<VertexMessage> incoming,
    IMessageQueue<VertexMessage> outgoing,
    Span<float> pageRank,
    Span<int> neighbors)
{
    int vertexId = Kernel.ThreadId.X;

    // Process incoming rank contributions from neighbors
    while (incoming.TryDequeue(out var msg))
    {
        if (msg.TargetVertex == vertexId)
        {
            pageRank[vertexId] += msg.Rank * 0.85f;
        }
    }

    // Distribute updated rank to neighbors
    float distributedRank = pageRank[vertexId] / neighbors.Length;
    for (int i = 0; i < neighbors.Length; i++)
    {
        outgoing.Enqueue(new VertexMessage
        {
            TargetVertex = neighbors[i],
            Rank = distributedRank
        });
    }
}

// Launch and manage ring kernel
var runtime = orchestrator.GetRingKernelRuntime();
await runtime.LaunchAsync("pagerank-vertex", gridSize: 1024, blockSize: 256);
await runtime.ActivateAsync("pagerank-vertex");

// Send initial messages
await runtime.SendMessageAsync("pagerank-vertex", new VertexMessage { ... });

// Monitor kernel status
var status = await runtime.GetStatusAsync("pagerank-vertex");
var metrics = await runtime.GetMetricsAsync("pagerank-vertex");
Console.WriteLine($"Messages processed: {metrics.MessagesReceived}");
Console.WriteLine($"Throughput: {metrics.ThroughputMsgsPerSec:F2} msgs/sec");
```

### Ring Kernel Features

- **Execution Modes**:
  - `Persistent` - Continuously running for streaming workloads
  - `EventDriven` - Activated on-demand for sporadic tasks

- **Messaging Strategies**:
  - `SharedMemory` - Lock-free queues in GPU shared memory (fastest for single GPU)
  - `AtomicQueue` - Global memory atomics (scalable to larger queues)
  - `P2P` - Direct GPU-to-GPU transfers (CUDA only, requires NVLink)
  - `NCCL` - Multi-GPU collectives (CUDA only, optimal for distributed)

- **Application Domains**:
  - `GraphAnalytics` - Optimized for irregular memory access (PageRank, BFS, shortest paths)
  - `SpatialSimulation` - Stencil patterns and halo exchange (fluids, physics)
  - `ActorModel` - Message-heavy workloads with dynamic distribution
  - `General` - No domain-specific optimizations

- **Cross-Backend Support**: Implemented for CPU (simulation), CUDA, OpenCL, and Metal backends

## Requirements

### System Requirements
- .NET 9.0 SDK or later
- C# 13.0 language features
- 64-bit operating system (Windows, Linux, macOS)

### For GPU Support

#### CUDA (NVIDIA)
- NVIDIA GPU with Compute Capability 5.0 or higher
- CUDA Toolkit 12.0 or later
- Compatible NVIDIA drivers

#### OpenCL (Cross-Platform)
- OpenCL 1.2+ compatible device (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- Vendor-specific OpenCL runtime:
  - **NVIDIA**: CUDA Toolkit or nvidia-opencl-icd
  - **AMD**: ROCm or amdgpu-pro drivers
  - **Intel**: intel-opencl-icd or beignet
  - **ARM/Mobile**: Vendor-provided OpenCL runtime

#### Metal (macOS - Direct MSL Only)
- macOS 10.13+ (High Sierra or later) for Metal 2.0
- Metal-capable GPU (Apple Silicon or Intel Mac 2016+)
- **Note**: C# to MSL automatic translation not yet available; write kernels in MSL directly

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
    A[C# Kernel with -Kernel- Attribute] --> B[Source Generator]
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

## Documentation

Comprehensive documentation is available covering all aspects of DotCompute:

### Getting Started
- **[Installation & Quick Start](docs/articles/getting-started.md)** - Get up and running in minutes
- **[Kernel Development Guide](docs/articles/guides/kernel-development.md)** - Writing efficient compute kernels

### Developer Guides
- **[Backend Selection](docs/articles/guides/backend-selection.md)** - Choosing the optimal execution backend
- **[Performance Tuning](docs/articles/guides/performance-tuning.md)** - Optimization techniques and best practices
- **[GPU Timing API](docs/articles/guides/timing-api.md)** - High-precision temporal measurements and clock calibration
- **[Barrier API](docs/articles/guides/barrier-api.md)** - Hardware-accelerated GPU thread synchronization
- **[Memory Ordering API](docs/articles/guides/memory-ordering-api.md)** - Causal memory ordering for distributed correctness
- **[Memory Management](docs/articles/guides/memory-management.md)** - Unified buffers and memory pooling
- **[Multi-GPU Programming](docs/articles/guides/multi-gpu.md)** - Scaling across multiple GPUs
- **[Native AOT Guide](docs/articles/guides/native-aot.md)** - Sub-10ms startup times
- **[Debugging Guide](docs/articles/guides/debugging-guide.md)** - Cross-backend validation and troubleshooting
- **[Dependency Injection](docs/articles/guides/dependency-injection.md)** - DI integration and testing
- **[Troubleshooting](docs/articles/guides/troubleshooting.md)** - Common issues and solutions

### Architecture
- **[System Overview](docs/articles/architecture/overview.md)** - High-level architecture and design principles
- **[Core Orchestration](docs/articles/architecture/core-orchestration.md)** - Kernel execution pipeline
- **[Backend Integration](docs/articles/architecture/backend-integration.md)** - Plugin system and accelerators
- **[Memory Management](docs/articles/architecture/memory-management.md)** - Unified memory architecture
- **[Source Generators](docs/articles/architecture/source-generators.md)** - Compile-time code generation

### Examples
- **[Basic Vector Operations](docs/articles/examples/basic-vector-operations.md)** - Fundamental operations with benchmarks
- **[Image Processing](docs/articles/examples/image-processing.md)** - Real-world GPU-accelerated filters
- **[Matrix Operations](docs/articles/examples/matrix-operations.md)** - Linear algebra and optimizations
- **[Multi-Kernel Pipelines](docs/articles/examples/multi-kernel-pipelines.md)** - Chaining operations efficiently

### Reference
- **[Diagnostic Rules (DC001-DC012)](docs/articles/reference/diagnostic-rules.md)** - Complete analyzer reference
- **[Performance Benchmarking](docs/articles/reference/performance-benchmarking.md)** - Profiling and optimization techniques
- **[API Documentation](api/index.md)** - Complete API reference

## Support

- **Documentation**: [Comprehensive Guides](docs/index.md) - Architecture, guides, examples, and API reference
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues) - Bug reports and feature requests
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions) - Questions and community

## Project Status

**Current Release**: v0.5.0-rc1 (November 10, 2025) | **Status**: Release Candidate

DotCompute v0.5.0-rc1 delivers a comprehensive platform for GPU and CPU compute acceleration in .NET applications. This release completes the temporal correctness API trilogy with the Memory Ordering API, enabling causal memory ordering and fence operations for distributed GPU computing.

### Key Capabilities

- **Modern Kernel API**: Attribute-based development with `[Kernel]` and `[RingKernel]` attributes
- **Multi-Backend Support**: Production-ready CPU SIMD, CUDA GPU, and OpenCL backends with Metal foundation
- **End-to-End GPU Integration**: Complete LINQ-to-GPU pipeline with automatic compilation and execution (Phase 6: 100% complete)
- **Intelligent Optimization**: Kernel fusion (50-80% bandwidth reduction), adaptive backend selection, and ML-powered optimization
- **Developer Experience**: Source generators with 12 Roslyn diagnostic rules (DC001-DC012) and 5 automated code fixes
- **Production Tooling**: Cross-backend debugging, performance profiling with hardware counters, and comprehensive telemetry
- **Native AOT Ready**: Full trimming support with sub-10ms startup times and 90% allocation reduction through memory pooling

### Phase 6 Achievements (End-to-End GPU Integration)

This release completes the GPU acceleration pipeline with production-ready features:

- **GPU Compilation Pipeline**: LINQ expressions automatically compile to optimized CUDA, OpenCL, and Metal kernels
- **Zero-Configuration Acceleration**: Transparent GPU execution without explicit backend selection
- **Graceful Fallback**: Multi-level fallback system ensures CPU execution on GPU failure
- **Kernel Fusion**: Automatic operation merging reducing memory bandwidth by 50-80%
- **Filter Compaction**: Atomic stream compaction for efficient variable-length results
- **Cross-Backend Validation**: Comprehensive testing with 80% pass rate across all backends
- **Performance Verification**: Measured 3.7x CPU SIMD speedup and 21-92x CUDA GPU speedup on RTX 2000 Ada

### What's New in v0.5.0-rc1

**Memory Ordering API (Production-Ready) - Phase 3 Complete**:
- **Three Consistency Models**: Relaxed (1.0× baseline), ReleaseAcquire (0.85×, 15% overhead), Sequential (0.60×, 40% overhead)
- **Three Fence Types**: ThreadBlock (~10ns), Device (~100ns), System (~200ns) with hardware acceleration
- **Causal Primitives**: Release/acquire semantics for producer-consumer patterns and distributed systems
- **Hardware Detection**: Native CC 7.0+ (Volta) acquire-release, CC 2.0+ UVA system fences
- **Comprehensive Testing**: 33 unit tests + 8 integration tests with producer-consumer validation
- **Production Documentation**: 1,101-line guide with Orleans.GpuBridge integration examples

**Phase 1.6 & Phase 2 Enhancements (NEW)**:
- **Automatic Timestamp Injection**: PTX-level kernel modification for transparent timestamp recording (<20ns overhead)
- **ExecuteWithBarrierAsync()**: Convenience method with automatic cooperative launch for grid barriers
- **Multi-GPU System Barriers**: Cross-device synchronization for 2-8 GPUs (~1-10ms latency) with three-phase protocol

**Technical Achievements**:
- Implemented CUDA `__threadfence_*()` intrinsics for all three fence scopes
- Causal read/write primitives with automatic release-acquire semantics
- Measured performance overhead matches theoretical predictions
- Lock-free data structure support with atomic causal operations

**Previous Releases**:
- **v0.4.2-rc1**: Barrier API with cooperative groups and 5 barrier scopes
- **v0.4.1-rc3**: GPU Timing API with 1ns precision and 4 calibration strategies

See **[Memory Ordering API Guide](docs/articles/guides/memory-ordering-api.md)** for complete documentation and **[Release Notes](https://github.com/mivertowski/DotCompute/releases/tag/v0.5.0-rc1)** for full details.
