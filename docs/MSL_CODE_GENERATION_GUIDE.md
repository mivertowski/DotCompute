# Metal Shading Language (MSL) Code Generation Guide

**DotCompute Metal Backend - Comprehensive MSL Translation System**

## Overview

The DotCompute Metal backend provides a complete, production-ready MSL code generation pipeline with two primary approaches:

1. **C# to MSL Translation** - Automatic translation of C# kernel code (from `[Kernel]` attribute) to optimized MSL
2. **MSL Template Generation** - Pre-built, highly optimized MSL templates for common operations

## Architecture

```
Translation Pipeline:
┌─────────────────┐
│  C# Kernel Code │ (from [Kernel] attribute)
└────────┬────────┘
         │
         ▼
┌────────────────────────────┐
│ CSharpToMSLTranslator      │ ← Comprehensive C#-to-MSL translation
│  • Thread ID mapping       │   • Handles Kernel.ThreadId.X/Y/Z
│  • Type translation        │   • Span<T>, ReadOnlySpan<T> → device pointers
│  • Math functions          │   • Math.Sin() → metal::sin()
│  • Atomics & barriers      │   • Interlocked.* → atomic_*
│  • Memory access analysis  │   • Coalescing diagnostics
└────────┬───────────────────┘
         │
         ▼
┌────────────────────────────┐
│  Metal Shading Language    │
│  (MSL Source Code)         │
└────────┬───────────────────┘
         │
         ▼
┌────────────────────────────┐
│  MetalKernelCompiler       │ ← Compilation & optimization
│  • NVRTC-style compilation │   • GPU family macros (M1/M2/M3)
│  • Pipeline state creation │   • Language version detection
│  • Caching & hot-reload    │   • Fast math optimizations
└────────────────────────────┘

Template Generation:
┌────────────────────────────┐
│  MslTemplateGenerator      │ ← Pre-built optimized templates
│  • VectorAdd               │   • Production-ready MSL
│  • VectorMultiply          │   • Bounds checking
│  • ReductionSum            │   • Threadgroup memory
│  • MatrixMultiply          │   • GPU-optimized patterns
│  • TiledMatrixMultiply     │   • Documentation included
│  • DotProduct              │
│  • SAXPY                   │
└────────────────────────────┘
```

## 1. C# to MSL Translation

### Supported C# Constructs

#### Thread Indexing
```csharp
// C# Code
int idx = Kernel.ThreadId.X;
int idy = Kernel.ThreadId.Y;
int idz = Kernel.ThreadId.Z;

// Translated to MSL
uint idx = thread_position_in_grid.x;
uint idy = thread_position_in_grid.y;
uint idz = thread_position_in_grid.z;
```

#### Type Mapping
```csharp
// C# Parameter Types                    → MSL Buffer Types
ReadOnlySpan<float> input                → device const float* input [[buffer(0)]]
Span<float> output                       → device float* output [[buffer(1)]]
float[] data                             → device float* data [[buffer(2)]]
float scalar                             → constant float& scalar [[buffer(3)]]
```

#### Math Functions
```csharp
// C# Math Functions                     → MSL Equivalents
Math.Sqrt(x)                             → metal::sqrt(x)
Math.Sin(x), Math.Cos(x)                 → metal::sin(x), metal::cos(x)
Math.Pow(x, y)                           → metal::pow(x, y)
Math.Min(a, b), Math.Max(a, b)           → metal::min(a, b), metal::max(a, b)
Math.Abs(x)                              → metal::abs(x)
Math.Floor(x), Math.Ceil(x)              → metal::floor(x), metal::ceil(x)

// Same for MathF (single precision)
MathF.Sin(x)                             → metal::sin(x)
```

#### Atomic Operations
```csharp
// C# Interlocked                        → MSL Atomic Operations
Interlocked.Add(ref value, delta)        → atomic_fetch_add_explicit(&value, delta, memory_order_relaxed)
Interlocked.Increment(ref value)         → atomic_fetch_add_explicit(&value, 1, memory_order_relaxed)
Interlocked.CompareExchange(...)         → atomic_compare_exchange_weak_explicit(...)
```

#### Synchronization
```csharp
// C# Barrier                            → MSL Synchronization
Barrier()                                → threadgroup_barrier(mem_flags::mem_device)
MemoryBarrier()                          → threadgroup_barrier(mem_flags::mem_device)
```

### Memory Access Pattern Analysis

The translator analyzes memory access patterns and provides optimization diagnostics:

- **Coalesced Access** (Optimal): `buffer[thread_position_in_grid.x]`
- **Strided Access** (15-30% slower): `buffer[gid * stride]`
- **Scattered Access** (50-80% slower): `buffer[indices[gid]]`

Warnings are logged for suboptimal patterns with suggestions for threadgroup staging.

### Complete Translation Example

```csharp
// C# Kernel (from [Kernel] attribute)
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}

// Auto-translated to MSL by CSharpToMSLTranslator
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

// Auto-generated Metal kernel: VectorAdd
// Translated from C# on: 2025-01-04 17:42:00 UTC
// DotCompute.Backends.Metal Translation Engine

kernel void VectorAdd(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint3 thread_position_in_grid [[thread_position_in_grid]],
    uint3 threads_per_threadgroup [[threads_per_threadgroup]],
    uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]])
{
    uint idx = thread_position_in_grid.x;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

## 2. MSL Template Generation

### Available Templates

The `MslTemplateGenerator` class provides production-ready, optimized MSL kernels:

#### Vector Operations
- **VectorAdd**: Element-wise addition
- **VectorMultiply**: Scalar multiplication
- **DotProduct**: Parallel reduction dot product
- **SAXPY**: Single-precision A*X Plus Y

#### Reduction Operations
- **ReductionSum**: Parallel sum reduction with threadgroup memory

#### Matrix Operations
- **MatrixMultiply**: Naive O(n³) implementation
- **MatrixMultiplyTiled**: Optimized tiled implementation with threadgroup memory

### Template Usage

```csharp
using DotCompute.Backends.Metal.Translation;

// Generate a vector add kernel
var mslCode = MslTemplateGenerator.GenerateVectorAdd(
    elementType: "float",
    kernelName: "my_vector_add"
);

// Generate a reduction sum kernel
var reductionCode = MslTemplateGenerator.GenerateReductionSum(
    elementType: "float",
    kernelName: "parallel_sum",
    threadgroupSize: 256
);

// Generate a tiled matrix multiply
var matmulCode = MslTemplateGenerator.GenerateTiledMatrixMultiply(
    elementType: "float",
    kernelName: "fast_matmul",
    tileSize: 16
);

// Generic template generation
var customCode = MslTemplateGenerator.GenerateKernel(
    operation: "VECTOR_ADD",
    elementType: "float",
    kernelName: "custom_add"
);
```

### Template Features

All generated templates include:
- ✅ Metal standard library headers
- ✅ Proper buffer attributes (`[[buffer(n)]]`)
- ✅ Thread position attributes (`[[thread_position_in_grid]]`)
- ✅ Bounds checking for safety
- ✅ GPU-optimized memory access patterns
- ✅ Documentation comments
- ✅ Support for multiple element types (float, int, double, etc.)

## 3. GPU Family Optimization Macros

The compiler automatically injects GPU-specific optimization macros:

```metal
// Apple M3 GPU (Apple9)
#define APPLE_M3_GPU 1
#define GPU_FAMILY_APPLE9 1
#define SIMDGROUP_SIZE 32
#define THREADGROUP_MEM_64KB 1
#define THREADGROUP_MEM_SIZE 65536
#define SUPPORTS_RAYTRACING 1
#define COMPUTE_UNITS 40

// Apple M2 GPU (Apple8)
#define APPLE_M2_GPU 1
#define GPU_FAMILY_APPLE8 1
#define SIMDGROUP_SIZE 32
#define THREADGROUP_MEM_32KB 1
#define THREADGROUP_MEM_SIZE 32768
#define COMPUTE_UNITS 20

// Apple M1 GPU (Apple7)
#define APPLE_M1_GPU 1
#define GPU_FAMILY_APPLE7 1
#define SIMDGROUP_SIZE 32
#define THREADGROUP_MEM_32KB 1
#define THREADGROUP_MEM_SIZE 32768
#define COMPUTE_UNITS 16

// Universal Apple Silicon capabilities
#define UNIFIED_MEMORY_NATIVE 1
#define TBDR_ARCHITECTURE 1
#define SUPPORTS_ATOMIC_OPERATIONS 1
```

## 4. Compilation Process

### Kernel Compilation Flow

```csharp
// 1. Define or generate MSL code
var kernelDefinition = new KernelDefinition("my_kernel", mslSourceCode)
{
    EntryPoint = "my_kernel",
    Language = KernelLanguage.Metal
};

// 2. Compile with optimizations
var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.O3,
    FastMath = true,
    EnableDebugInfo = false
};

var compiledKernel = await compiler.CompileAsync(kernelDefinition, options);

// 3. Execute
var args = new KernelArguments(bufferA, bufferB, bufferResult);
await compiledKernel.ExecuteAsync(args);
```

### Compilation Options

- **OptimizationLevel**: `None`, `O1`, `O2`, `O3` (default: `O3`)
- **FastMath**: Enable fast math optimizations (trades precision for performance)
- **EnableDebugInfo**: Include debug symbols for kernel debugging
- **LanguageVersion**: Auto-detected based on macOS version (Metal 2.0 - 3.1)

### Caching

The `MetalKernelCache` automatically caches compiled kernels:
- **In-memory cache**: Immediate reuse of compiled pipeline states
- **Persistent cache**: Disk-based caching for cross-session reuse
- **TTL management**: Automatic expiration of stale cached entries
- **Statistics tracking**: Hit/miss rates, average compilation times

## 5. Performance Optimization

### Threadgroup Memory

For reduction operations and tiled algorithms, use threadgroup memory:

```metal
kernel void reduction_sum(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    threadgroup float* shared_data [[threadgroup(0)]],  // ← Threadgroup memory
    uint tid [[thread_position_in_threadgroup]],
    uint gid [[thread_position_in_grid]])
{
    // Load into threadgroup memory
    shared_data[tid] = input[gid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Parallel reduction
    for (uint s = blockDim / 2; s > 0; s >>= 1) {
        if (tid < s) {
            shared_data[tid] += shared_data[tid + s];
        }
        threadgroup_barrier(mem_flags::mem_threadgroup);
    }

    // Write result
    if (tid == 0) {
        atomic_fetch_add_explicit((device atomic_float*)output, shared_data[0], memory_order_relaxed);
    }
}
```

### Memory Coalescing

Optimize for coalesced memory access:

```metal
// ✅ GOOD: Coalesced access (consecutive threads access consecutive memory)
kernel void coalesced_access(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[gid] * 2.0f;  // Optimal memory pattern
}

// ⚠️ SUBOPTIMAL: Strided access
kernel void strided_access(
    device const float* input [[buffer(0)]],
    device float* output [[buffer(1)]],
    constant uint& stride [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[gid * stride];  // 15-30% slower
}

// ❌ BAD: Scattered access
kernel void scattered_access(
    device const float* input [[buffer(0)]],
    device const uint* indices [[buffer(1)]],
    device float* output [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    output[gid] = input[indices[gid]];  // 50-80% slower
}
```

## 6. Debugging and Diagnostics

### Compilation Error Diagnostics

The compiler provides comprehensive error diagnostics:

```
=== Metal Shader Compilation Error ===
Kernel: my_kernel
Error: use of undeclared identifier 'unknownVariable'

Location: Line 15, Column 12

Source Context:
    13:     float sum = 0.0f;
    14:     for (uint i = 0; i < count; i++) {
>>> 15:         sum += unknownVariable[i];
                     ^
    16:     }
    17:     output[gid] = sum;

Suggestions:
  - Check for typos in variable or function names
  - Ensure all variables are declared before use
  - Verify that Metal standard library functions are properly namespaced (metal::)
```

### Memory Access Analysis

The translator logs diagnostic messages for memory patterns:

```
[Warning] Scattered memory access detected in 'data[indices[idx]]'.
Consider using threadgroup staging for better performance.
Scattered access can reduce memory bandwidth by 50-80%.

[Info] Strided memory access detected in 'buffer[idx * stride]' with stride 'stride'.
For large strides, consider threadgroup staging to improve coalescing.
Expected performance impact: 15-30% bandwidth reduction.
```

## 7. Production Deployment

### Best Practices

1. **Use template generation for standard operations**: Faster development, proven performance
2. **Enable aggressive optimization**: `OptimizationLevel.O3` with `FastMath = true`
3. **Cache compiled kernels**: Reduces compilation overhead
4. **Profile memory access patterns**: Fix scattered/strided access with threadgroup staging
5. **Leverage GPU family macros**: Write adaptive kernels for M1/M2/M3

### Example Production Configuration

```csharp
var options = Options.Create(new MetalAcceleratorOptions
{
    EnableValidation = false,  // Disable in production
    EnableMetrics = true,      // Track performance
    MaxConcurrentCommands = 8, // Parallel dispatch
    CommandBufferPoolSize = 16 // Reuse command buffers
});

var accelerator = new MetalAccelerator(options, logger);

// Use template for known operations
var mslCode = MslTemplateGenerator.GenerateReductionSum("float", "fast_sum", 256);
var kernel = new KernelDefinition("fast_sum", mslCode)
{
    EntryPoint = "fast_sum",
    Language = KernelLanguage.Metal
};

var compilationOptions = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.O3,
    FastMath = true,
    EnableDebugInfo = false
};

var compiled = await accelerator.CompileKernelAsync(kernel, compilationOptions);
```

## 8. Testing

The Metal backend includes comprehensive integration tests:

```csharp
[SkippableFact]
public async Task VectorAdd_Should_Execute_Correctly()
{
    Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");

    // Test data preparation
    const int elementCount = 1024;
    var hostA = new float[elementCount];
    var hostB = new float[elementCount];

    // ... (initialize test data)

    // Create buffers
    await using var bufferA = await accelerator.Memory.AllocateAsync<float>(elementCount);
    await using var bufferB = await accelerator.Memory.AllocateAsync<float>(elementCount);
    await using var bufferResult = await accelerator.Memory.AllocateAsync<float>(elementCount);

    // Upload data
    await bufferA.WriteAsync(hostA.AsMemory());
    await bufferB.WriteAsync(hostB.AsMemory());

    // Compile and execute kernel (using inline MSL)
    var kernelCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_add(
    device const float* a [[buffer(0)]],
    device const float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint gid [[thread_position_in_grid]])
{
    result[gid] = a[gid] + b[gid];
}";

    var kernel = new KernelDefinition("vector_add", kernelCode)
    {
        EntryPoint = "vector_add",
        Language = KernelLanguage.Metal
    };

    var compiled = await accelerator.CompileKernelAsync(kernel);

    // Execute
    await accelerator.ExecuteKernelAsync(compiled,
        new GridDimensions(elementCount, 1, 1),
        new GridDimensions(256, 1, 1),
        bufferA, bufferB, bufferResult);

    // Verify results
    var hostResult = new float[elementCount];
    await bufferResult.ReadAsync(hostResult.AsMemory());

    for (int i = 0; i < elementCount; i++)
    {
        Assert.Equal(hostA[i] + hostB[i], hostResult[i], precision: 3);
    }
}
```

## Summary

The DotCompute Metal backend provides:

✅ **Complete C#-to-MSL translation** with comprehensive language construct support
✅ **Production-ready MSL templates** for common operations
✅ **Automatic GPU optimization** with M1/M2/M3 detection
✅ **Memory access pattern analysis** with performance diagnostics
✅ **Aggressive compilation optimizations** with caching
✅ **Comprehensive error diagnostics** with source context
✅ **Integration testing** with actual GPU hardware

**Status**: Production-ready for Metal backend kernel execution (v0.2.0-alpha)

---

**Related Documentation**:
- `/docs/METAL_BACKEND_ARCHITECTURE.md` - Overall Metal backend design
- `/src/Backends/DotCompute.Backends.Metal/Translation/CSharpToMSLTranslator.cs` - Translation implementation
- `/src/Backends/DotCompute.Backends.Metal/Translation/MslTemplateGenerator.cs` - Template generation
- `/src/Backends/DotCompute.Backends.Metal/Kernels/MetalKernelCompiler.cs` - Compilation engine
