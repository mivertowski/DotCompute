# DotCompute.Generators

Source generators for the DotCompute framework that enable compile-time code generation for high-performance compute kernels.

## Overview

The DotCompute.Generators project provides Roslyn-based source generators that automatically generate optimized backend-specific implementations for compute kernels marked with the `[Kernel]` or `[RingKernel]` attributes.

## Features

### 1. **KernelSourceGenerator**
- Incremental source generator using `IIncrementalGenerator` for optimal performance
- Detects methods marked with `[Kernel]` and `[RingKernel]` attributes
- Generates backend-specific implementations (CPU, CUDA, Metal, OpenCL)
- Creates a kernel registry for runtime dispatch
- Supports SIMD vectorization, parallel execution, and persistent kernels
- Generates message queue infrastructure for Ring Kernels

### 2. **KernelCompilationAnalyzer**
- Compile-time diagnostics for kernel methods
- Validates parameter types and vector sizes
- Detects performance issues (nested loops, allocations in loops)
- Ensures unsafe context for optimal performance
- Provides actionable error messages

### 3. **Backend Code Generators**
- **CpuCodeGenerator**: Generates optimized CPU implementations with:
  - Scalar fallback implementation
  - Platform-agnostic SIMD using `Vector<T>`
  - AVX2 optimizations for x86/x64
  - AVX-512 optimizations for latest processors
  - Parallel execution with task partitioning
  - Automatic hardware capability detection

## Installation

```bash
dotnet add package DotCompute.Generators --version 0.6.0
```

## Usage

### 1. Add the Generator to Your Project

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\DotCompute.Generators\DotCompute.Generators.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. Mark Methods with Kernel or RingKernel Attribute

```csharp
using DotCompute.Generators.Kernel;

// Standard kernel for one-shot execution
public static unsafe class VectorMath
{
    [Kernel(
        Backends = KernelBackends.CPU | KernelBackends.CUDA,
        VectorSize = 8,
        IsParallel = true,
        Optimizations = OptimizationHints.AggressiveInlining | OptimizationHints.Vectorize)]
    public static void AddVectors(float* a, float* b, float* result, int length)
    {
        for (int i = 0; i < length; i++)
        {
            result[i] = a[i] + b[i];
        }
    }
}

// Ring kernel for persistent GPU-resident computation
public static class GraphAlgorithms
{
    [RingKernel(
        KernelId = "pagerank-vertex",
        Domain = RingKernelDomain.GraphAnalytics,
        Mode = RingKernelMode.Persistent,
        Capacity = 10000,
        Backends = KernelBackends.CUDA | KernelBackends.OpenCL)]
    public static void PageRankVertex(
        IMessageQueue<VertexMessage> incoming,
        IMessageQueue<VertexMessage> outgoing,
        Span<float> pageRank)
    {
        int vertexId = Kernel.ThreadId.X;

        while (incoming.TryDequeue(out var msg))
        {
            if (msg.TargetVertex == vertexId)
                pageRank[vertexId] += msg.Rank;
        }

        // Send to neighbors...
    }
}
```

### 3. Generated Code

The source generator will create:

1. **Kernel Registry** (`KernelRegistry.g.cs`):
   - Catalog of all kernels with metadata
   - Runtime lookup capabilities
   - Backend support information

2. **CPU Implementation** (`AddVectors_CPU.g.cs`):
   - Multiple implementations (scalar, SIMD, AVX2, AVX-512)
   - Automatic hardware detection and dispatch
   - Parallel execution support

3. **Kernel Invoker** (`VectorMathInvoker.g.cs`):
   - Dynamic dispatch based on backend
   - Parameter validation
   - Type-safe invocation

## Kernel Attribute Options

### Standard Kernel Attributes

### Backends
- `CPU`: CPU backend with SIMD support
- `CUDA`: NVIDIA GPU backend
- `Metal`: Apple GPU backend
- `OpenCL`: Cross-platform GPU backend
- `All`: All available backends

### Optimization Hints
- `AggressiveInlining`: Force method inlining
- `LoopUnrolling`: Unroll loops for better performance
- `Vectorize`: Enable SIMD vectorization
- `Prefetch`: Add memory prefetch hints
- `FastMath`: Use fast math operations (may reduce accuracy)

### Memory Access Patterns
- `Sequential`: Linear memory access
- `Strided`: Fixed-stride memory access
- `Random`: Random memory access
- `Coalesced`: GPU-optimized coalesced access
- `Tiled`: Tiled/blocked memory access

## RingKernel Attribute Options

Ring Kernels enable persistent GPU computation with message passing capabilities:

### Execution Modes
- `Persistent`: Kernel stays active continuously, ideal for streaming workloads
- `EventDriven`: Kernel launches on-demand when messages arrive, conserves resources

### Message Passing Strategies
- `SharedMemory`: Lock-free queues in GPU shared memory (fastest for single-GPU)
- `AtomicQueue`: Lock-free queues in global memory with atomics (scalable)
- `P2P`: Direct GPU-to-GPU memory transfers (CUDA only, requires NVLink)
- `NCCL`: Multi-GPU collectives (CUDA only, optimal for distributed workloads)

### Application Domains
- `General`: No domain-specific optimizations
- `GraphAnalytics`: Optimized for irregular memory access patterns (graph algorithms)
- `SpatialSimulation`: Optimized for regular access with halo exchange (physics, fluids)
- `ActorModel`: Optimized for message-heavy workloads with dynamic distribution

### Configuration Options
- `KernelId`: Unique identifier for the kernel (required)
- `Capacity`: Maximum concurrent work items (default: 1024, must be power of 2)
- `InputQueueSize`: Size of incoming message queue (default: 256, must be power of 2)
- `OutputQueueSize`: Size of outgoing message queue (default: 256, must be power of 2)
- `GridDimensions`: Number of thread blocks per dimension (auto-calculated if null)
- `BlockDimensions`: Threads per block per dimension (auto-selected if null)
- `UseSharedMemory`: Enable shared memory for thread-block coordination
- `SharedMemorySize`: Shared memory size in bytes per block

## Analyzer Diagnostics

| ID | Severity | Description |
|----|----------|-------------|
| DC0001 | Error | Unsupported type in kernel |
| DC0002 | Error | Kernel method missing buffer parameter |
| DC0003 | Error | Invalid vector size (must be 4, 8, or 16) |
| DC0004 | Warning | Unsafe code context required |
| DC0005 | Warning | Potential performance issue |

## Architecture

```
DotCompute.Generators/
├── Kernel/
│   ├── KernelSourceGenerator.cs    # Main generator
│   ├── KernelAttribute.cs          # Attribute definitions
│   ├── KernelCompilationAnalyzer.cs # Compile-time analysis
│   └── AcceleratorType.cs          # Backend enum
├── Backend/
│   └── CpuCodeGenerator.cs         # CPU code generation
├── Models/
│   ├── KernelParameter.cs          # Parameter model
│   └── VectorizationInfo.cs        # Vectorization analysis model
├── Configuration/
│   └── GeneratorConfiguration.cs   # Generator configuration
└── Utils/
    ├── SourceGeneratorHelpers.cs   # Legacy facade (deprecated)
    ├── CodeFormatter.cs             # Code formatting utilities
    ├── ParameterValidator.cs       # Parameter validation
    ├── LoopOptimizer.cs            # Loop optimization
    ├── VectorizationAnalyzer.cs    # Vectorization analysis
    ├── MethodBodyExtractor.cs      # Method body extraction
    └── SimdTypeMapper.cs           # SIMD type mapping
```

## Future Enhancements

1. **CUDA Code Generation**
   - PTX generation for NVIDIA GPUs
   - Shared memory optimization
   - Warp-level primitives

2. **Metal Shader Generation**
   - Metal Shading Language generation
   - Compute pipeline setup
   - Resource binding

3. **OpenCL Kernel Generation**
   - OpenCL C kernel generation
   - Work-group optimization
   - Memory coalescing

4. **Advanced Optimizations**
   - Auto-vectorization analysis
   - Loop fusion and tiling
   - Memory layout optimization
   - Cache-aware algorithms

5. **Debugging Support**
   - Source maps for generated code
   - Performance counters injection
   - Validation code generation

## Development Notes

- The generator targets .NET Standard 2.0 for compatibility
- Uses incremental generation for optimal IDE performance
- Follows Roslyn best practices for analyzers
- Includes comprehensive unit tests (see tests project)

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[Source Generators](../../../docs/articles/architecture/source-generators.md)** - Compile-time code generation (12 diagnostic rules, 5 automated fixes)
- **[System Overview](../../../docs/articles/architecture/overview.md)** - Generator integration in architecture

### Developer Guides
- **[Getting Started](../../../docs/articles/getting-started.md)** - Using [Kernel] attributes
- **[Kernel Development](../../../docs/articles/guides/kernel-development.md)** - Writing kernels with attributes
- **[Native AOT Guide](../../../docs/articles/guides/native-aot.md)** - Native AOT compatibility

### Reference
- **[Diagnostic Rules (DC001-DC012)](../../../docs/articles/reference/diagnostic-rules.md)** - Complete analyzer reference with automated fixes

### Examples
- **[Basic Vector Operations](../../../docs/articles/examples/basic-vector-operations.md)** - [Kernel] attribute usage examples

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)