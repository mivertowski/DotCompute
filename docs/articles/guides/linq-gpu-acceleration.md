# LINQ GPU Acceleration Guide

This guide explains how to use DotCompute.Linq's GPU kernel generation to accelerate LINQ queries with automatic optimization across CUDA, OpenCL, and Metal backends.

## Overview

DotCompute.Linq provides production-ready GPU kernel generation from standard LINQ expressions. The system automatically compiles LINQ operations into optimized GPU kernels with comprehensive optimization features including kernel fusion and filter compaction.

### Key Features

- **Automatic GPU Compilation**: LINQ expressions → optimized GPU kernels
- **Three GPU Backends**: CUDA (NVIDIA), OpenCL (cross-platform), Metal (Apple)
- **Kernel Fusion**: 50-80% bandwidth reduction for chained operations
- **Filter Compaction**: Atomic stream compaction for variable-length results
- **Graceful Fallback**: CPU execution on GPU unavailability

## Quick Start

### Installation

```bash
dotnet add package DotCompute.Linq --version 0.6.2
dotnet add package DotCompute.Backends.CUDA --version 0.6.2  # NVIDIA GPUs
dotnet add package DotCompute.Backends.OpenCL --version 0.6.2 # Cross-platform GPU
dotnet add package DotCompute.Backends.Metal --version 0.6.2  # Apple GPUs (feature-complete)
```

### Basic Usage

```csharp
using DotCompute.Linq;

// Your data
float[] data = Enumerable.Range(0, 1_000_000)
    .Select(i => (float)i)
    .ToArray();

// Standard LINQ automatically compiled to GPU kernel
var result = data
    .AsComputeQueryable()
    .Where(x => x > 5000)
    .Select(x => x * 2.0f)
    .ToComputeArray();

// GPU acceleration is automatic!
// Expected: 10-30x speedup vs standard LINQ on 1M elements
```

## Supported Operations

### Map Operations (Select)

Transform each element with a function:

```csharp
// Multiply by 2 - compiles to GPU kernel
var doubled = data
    .AsComputeQueryable()
    .Select(x => x * 2.0f)
    .ToComputeArray();

// Complex transformations
var complex = data
    .AsComputeQueryable()
    .Select(x => (x * 3.0f) + 100.0f)
    .ToComputeArray();

// Math operations
var computed = data
    .AsComputeQueryable()
    .Select(x => x * x + Math.Sqrt(x))
    .ToComputeArray();
```

**Generated CUDA Kernel**:
```cuda
extern "C" __global__ void Execute(const float* input, float* output, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        output[idx] = (input[idx] * 2.0f);
    }
}
```

### Filter Operations (Where)

Select elements matching a predicate:

```csharp
// Simple filter - uses atomic stream compaction
var filtered = data
    .AsComputeQueryable()
    .Where(x => x > 1000.0f)
    .ToComputeArray();

// Complex predicates
var complexFilter = data
    .AsComputeQueryable()
    .Where(x => x > 500.0f && x < 2000.0f)
    .ToComputeArray();
```

**Generated CUDA Kernel with Atomic Compaction**:
```cuda
extern "C" __global__ void Execute(
    const float* input,
    float* output,
    int* outputCount,
    int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        if ((input[idx] > 1000.0f)) {
            // Atomically allocate output position
            int outIdx = atomicAdd(outputCount, 1);
            output[outIdx] = input[idx];
        }
    }
}
```

### Reduce Operations (Sum, Average, etc.)

Aggregate operations:

```csharp
// Sum all elements
float sum = data
    .AsComputeQueryable()
    .Sum();

// Average
float average = data
    .AsComputeQueryable()
    .Average();

// Count matching elements
int count = data
    .AsComputeQueryable()
    .Count(x => x > 5000);
```

## Kernel Fusion Optimization

One of the most powerful features is **automatic kernel fusion** - combining multiple LINQ operations into a single GPU kernel.

### Example: Map → Filter → Map

```csharp
// Three operations
var result = data
    .AsComputeQueryable()
    .Select(x => x * 2.0f)        // Map 1
    .Where(x => x > 1000.0f)      // Filter
    .Select(x => x + 100.0f)      // Map 2
    .ToComputeArray();            // Single fused GPU kernel!
```

**Without Fusion** (3 separate kernels):
```
Kernel 1 (Map):    Read 1M elements → Write 1M elements
Kernel 2 (Filter): Read 1M elements → Write 500K elements (50% pass)
Kernel 3 (Map):    Read 500K elements → Write 500K elements
Total: 2.5M reads + 2M writes = 4.5M memory operations
```

**With Fusion** (1 kernel):
```
Fused Kernel: Read 1M elements → Write 500K elements (only passing elements)
Total: 1M reads + 500K writes = 1.5M memory operations
```

**Result**: **66.7% bandwidth reduction** (4.5M → 1.5M operations)

### Generated Fused CUDA Kernel

```cuda
extern "C" __global__ void Execute(const float* input, float* output, int length)
{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < length) {
        // Fused operations: Map -> Filter -> Map
        // Performance: Eliminates intermediate memory transfers
        float value = input[idx];
        bool passesFilter = true;

        // Map: x * 2
        if (passesFilter) {
            value = (value * 2.0f);
        }

        // Filter: Check predicate
        passesFilter = passesFilter && ((value > 1000.0f));

        // Map: x + 100
        if (passesFilter) {
            value = (value + 100.0f);
        }

        // Write only if passed filter
        if (passesFilter) {
            output[idx] = value;
        }
    }
}
```

### Supported Fusion Patterns

| Pattern | Description | Bandwidth Reduction |
|---------|-------------|---------------------|
| **Map + Map** | Two transformations | 33% (3 ops → 2 ops) |
| **Map + Filter** | Transform then filter | 50% (3 ops → 2 ops) |
| **Filter + Map** | Filter then transform | 50% (3 ops → 2 ops) |
| **Map + Map + Map** | Three transformations | 66.7% (5 ops → 2 ops) |
| **Map + Filter + Map** | Complex chain | 66.7% (5 ops → 2 ops) |

## Backend Selection

### Automatic Selection

The system automatically selects the best backend:

```csharp
// Automatic backend selection
var result = data.AsComputeQueryable().Select(x => x * 2).ToComputeArray();
// Uses GPU if available, CPU otherwise
```

### Backend Selection Behavior

The system automatically selects the best available backend based on:
- Hardware availability (GPU vs CPU)
- Data size (larger datasets benefit more from GPU)
- Operation type (some operations are more GPU-friendly)

```csharp
// Backend selection is automatic - no configuration needed
var result = data
    .AsComputeQueryable()
    .Select(x => x * 2)
    .ToComputeArray();

// The system will use:
// - CUDA if NVIDIA GPU is available
// - Metal if Apple Silicon is available
// - OpenCL for cross-platform GPU support
// - CPU SIMD as fallback (always available)
```

> **Note**: Manual backend selection via `WithBackend()` is planned for a future release.
> Currently, the system automatically selects the optimal backend based on hardware availability.

## Performance Expectations

Based on GPU architecture and workload characteristics:

### Small Data (1K - 100K elements)

| Operation | CPU LINQ | GPU (CUDA/OpenCL/Metal) | Speedup |
|-----------|----------|-------------------------|---------|
| Map | ~0.5ms | ~0.2ms | 2-3x |
| Filter | ~0.4ms | ~0.2ms | 2x |
| Reduce | ~0.3ms | ~0.1ms | 3x |

**Note**: GPU overhead dominates for small datasets. Use CPU for < 10K elements.

### Medium Data (100K - 1M elements)

| Operation | CPU LINQ | GPU (CUDA/OpenCL/Metal) | Speedup |
|-----------|----------|-------------------------|---------|
| Map | ~15ms | 0.5-1.5ms | **10-30x** ✅ |
| Filter | ~12ms | 1-2ms | **6-12x** ✅ |
| Reduce | ~10ms | 0.3-1ms | **10-33x** ✅ |

**Note**: Sweet spot for GPU acceleration. Significant speedups with manageable overhead.

### Large Data (10M+ elements)

| Operation | CPU LINQ | GPU (CUDA/OpenCL/Metal) | Speedup |
|-----------|----------|-------------------------|---------|
| Map | ~150ms | 3-5ms | **30-50x** ✅ |
| Filter | ~120ms | 5-10ms | **12-24x** ✅ |
| Reduce | ~100ms | 2-5ms | **20-50x** ✅ |

**Note**: Maximum GPU efficiency. CPU SIMD can help but GPU dominates.

## Hardware Requirements

### CUDA Backend (NVIDIA GPUs)

**Supported Architectures**:
- Maxwell (CC 5.0-5.3): GTX 900 series, GTX Titan X
- Pascal (CC 6.0-6.2): GTX 1000 series, P100, Titan Xp
- Volta (CC 7.0-7.2): V100, Titan V
- Turing (CC 7.5): RTX 2000 series, T4, Titan RTX
- Ampere (CC 8.0-8.6): RTX 3000 series, A100, A10
- Ada Lovelace (CC 8.9): RTX 4000 series, L4, L40

**Requirements**:
- CUDA Toolkit 12.0 or later
- Compatible NVIDIA drivers
- Windows, Linux, or WSL2

### OpenCL Backend (Cross-Platform)

**Supported Vendors**:
- **NVIDIA**: GeForce, Quadro, Tesla (via OpenCL or CUDA)
- **AMD**: Radeon RX, Radeon Pro, Instinct (via ROCm or AMDGPU Pro)
- **Intel**: Arc, Iris Xe, UHD Graphics (via Intel Compute Runtime)
- **ARM Mali**: Mobile and embedded GPUs
- **Qualcomm Adreno**: Mobile GPUs

**Requirements**:
- OpenCL 1.2+ runtime
- Vendor-specific OpenCL drivers
- Cross-platform support (Windows, Linux, macOS, mobile)

### Metal Backend (Apple GPUs)

**Supported Hardware**:
- Apple Silicon: M1, M2, M3, M4 (Unified Memory)
- AMD Discrete GPUs in Intel Macs
- Intel Integrated Graphics in older Macs

**Requirements**:
- macOS 10.13+ (High Sierra or later)
- Metal 2.0+ support
- Native macOS only

## Best Practices

### 1. Data Size Considerations

```csharp
// Good: Large datasets benefit from GPU
if (data.Length > 10_000)
{
    result = data.AsComputeQueryable().Select(x => x * 2).ToComputeArray();
}
else
{
    result = data.Select(x => x * 2).ToArray(); // Standard LINQ
}
```

### 2. Operation Chaining for Fusion

```csharp
// Good: Chain operations for kernel fusion
var result = data
    .AsComputeQueryable()
    .Select(x => x * 2)    // Will be fused
    .Where(x => x > 100)   // into single kernel
    .Select(x => x + 50)   // automatically!
    .ToComputeArray();

// Avoid: Breaking chain prevents fusion
var temp1 = data.AsComputeQueryable().Select(x => x * 2).ToComputeArray();
var temp2 = temp1.AsComputeQueryable().Where(x => x > 100).ToComputeArray();
// Creates 2 separate kernels instead of 1 fused kernel
```

### 3. Memory Management

```csharp
// Reuse buffers when possible
using var buffer = new UnifiedBuffer<float>(length);

// Multiple queries on same data
var result1 = data.AsComputeQueryable().Select(x => x * 2).ToComputeArray();
var result2 = data.AsComputeQueryable().Select(x => x * 3).ToComputeArray();
// Kernel compilation is cached automatically
```

### 4. Backend Selection Strategy

```csharp
// Let the system choose for optimal performance (recommended)
var result = data
    .AsComputeQueryable()
    .Select(x => x * 2)
    .ToComputeArray(); // Automatic backend selection

// The system automatically selects the best available backend:
// Priority: CUDA > Metal > OpenCL > CPU SIMD
// Backend selection considers data size and operation type
```

## Troubleshooting

### GPU Not Detected

**Problem**: GPU acceleration not available.

**Solutions**:
1. Verify GPU drivers installed: `nvidia-smi` (CUDA) or `clinfo` (OpenCL)
2. Check CUDA Toolkit version: `nvcc --version` (should be 12.0+)
3. Ensure correct NuGet packages installed
4. Check logs for GPU initialization errors

### Poor Performance

**Problem**: GPU slower than expected.

**Solutions**:
1. **Data Size**: Ensure dataset is large enough (> 10K elements)
2. **Kernel Fusion**: Chain operations to enable fusion
3. **Backend**: Verify the expected backend is available (check logs)
4. **Memory Transfers**: Minimize CPU↔GPU transfers
5. **Profiling**: Use BenchmarkDotNet to measure actual performance

### Compilation Errors

**Problem**: GPU kernel compilation fails.

**Solutions**:
1. **Check Expression**: Ensure LINQ expression uses supported operations
2. **Type Support**: Verify data types are supported (byte, int, float, double)
3. **Fallback**: System automatically falls back to CPU on errors
4. **Logs**: Check debug logs for detailed error messages

## Next Steps

- **Advanced Topics**: [GPU Kernel Generation Deep Dive](../advanced/gpu-kernel-generation.md)
- **Performance**: [Benchmarking LINQ Queries](../reference/performance-benchmarking.md)
- **Examples**: [LINQ GPU Acceleration Examples](../examples/linq-gpu-examples.md)
- **API Reference**: [DotCompute.Linq API Documentation](../../api/DotCompute.Linq.html)

## Additional Resources

- **GitHub**: [DotCompute Repository](https://github.com/mivertowski/DotCompute)
- **NuGet**: [DotCompute.Linq Package](https://www.nuget.org/packages/DotCompute.Linq/)
- **Issues**: [Report Bugs](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [Community Support](https://github.com/mivertowski/DotCompute/discussions)
