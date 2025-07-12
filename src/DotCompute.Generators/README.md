# DotCompute.Generators

Source generators for the DotCompute framework that enable compile-time code generation for high-performance compute kernels.

## Overview

The DotCompute.Generators project provides Roslyn-based source generators that automatically generate optimized backend-specific implementations for compute kernels marked with the `[Kernel]` attribute.

## Features

### 1. **KernelSourceGenerator**
- Incremental source generator using `IIncrementalGenerator` for optimal performance
- Detects methods marked with `[Kernel]` attribute
- Generates backend-specific implementations (CPU, CUDA, Metal, OpenCL)
- Creates a kernel registry for runtime dispatch
- Supports SIMD vectorization and parallel execution

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

## Usage

### 1. Add the Generator to Your Project

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\DotCompute.Generators\DotCompute.Generators.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 2. Mark Methods with Kernel Attribute

```csharp
using DotCompute.Generators.Kernel;

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
└── Utils/
    └── SourceGeneratorHelpers.cs   # Helper utilities
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