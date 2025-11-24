# Metal Backend for Apple Silicon

The DotCompute Metal backend provides GPU acceleration for Apple Silicon (M1/M2/M3) and Intel Macs using Apple's Metal framework. This guide covers architecture, features, performance characteristics, and best practices.

## Overview

The Metal backend delivers:

- **Native Apple Silicon support** with unified memory architecture
- **Metal Performance Shaders (MPS)** integration for optimized ML operations
- **Thread-safe parallel execution** via command queue pooling
- **Binary kernel caching** for sub-millisecond compilation
- **90%+ memory pool efficiency** with automatic buffer recycling

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    C# Managed Layer                         │
├─────────────────────────────────────────────────────────────┤
│  MetalAccelerator    │  MetalKernelCompiler   │ MPS Backend │
│  MetalMemoryPool     │  MetalCommandQueuePool │ Orchestrator│
├─────────────────────────────────────────────────────────────┤
│                    P/Invoke Interop                         │
├─────────────────────────────────────────────────────────────┤
│              libDotComputeMetal.dylib (Native)              │
│  DCMetalDevice.mm  │  DCMetalMPS.mm  │  Command Queue Pool  │
├─────────────────────────────────────────────────────────────┤
│              Apple Metal Framework + MPS                    │
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Purpose |
|-----------|---------|
| `MetalAccelerator` | Main entry point for GPU operations |
| `MetalKernelCompiler` | Compiles MSL kernels with binary caching |
| `MetalMemoryPoolManager` | Manages buffer allocation with pooling |
| `MetalCommandQueuePool` | Thread-safe command queue management |
| `MetalPerformanceShadersBackend` | MPS BLAS/CNN/Neural operations |
| `MetalMPSOrchestrator` | Auto-selects MPS vs custom kernels |

## Getting Started

### Prerequisites

- macOS 10.15+ (Catalina or later)
- .NET 9.0+
- Xcode Command Line Tools (for native compilation)

### Installation

```bash
# Add the Metal backend package
dotnet add package DotCompute.Backends.Metal
```

### Basic Usage

```csharp
using DotCompute.Backends.Metal;

// Create Metal accelerator
using var accelerator = MetalAccelerator.Create();

// Compile a kernel
var kernel = accelerator.CompileKernel(@"
    #include <metal_stdlib>
    using namespace metal;

    kernel void vector_add(
        device const float* a [[buffer(0)]],
        device const float* b [[buffer(1)]],
        device float* result [[buffer(2)]],
        uint id [[thread_position_in_grid]])
    {
        result[id] = a[id] + b[id];
    }
");

// Allocate buffers
var bufferA = accelerator.Allocate<float>(1024);
var bufferB = accelerator.Allocate<float>(1024);
var result = accelerator.Allocate<float>(1024);

// Execute kernel
kernel.Execute(bufferA, bufferB, result, gridSize: 1024);
```

## Features

### 1. Unified Memory Architecture

Apple Silicon uses unified memory, eliminating CPU-GPU data transfers:

```csharp
// Check if unified memory is available
if (accelerator.Capabilities.HasUnifiedMemory)
{
    // Direct memory access - no explicit transfers needed
    var buffer = accelerator.Allocate<float>(1024);
    buffer.CopyFrom(cpuData); // Direct copy, no staging
}
```

**Performance Impact**: 1.5-2.5x speedup for memory-bound operations compared to discrete GPU memory simulation.

### 2. Binary Kernel Caching

Compiled kernels are cached as binary archives for instant reuse:

```csharp
// First compilation: ~200ms
var kernel1 = compiler.Compile(source, "my_kernel");

// Cache hit: ~5μs (40,000x faster)
var kernel2 = compiler.Compile(source, "my_kernel");
```

Cache files are stored in the application's cache directory and persist across sessions.

### 3. Command Queue Pool

Thread-safe parallel kernel execution without crashes:

```csharp
// Parallel execution uses pooled command queues
await Task.WhenAll(
    Task.Run(() => kernel1.Execute(data1)),
    Task.Run(() => kernel2.Execute(data2)),
    Task.Run(() => kernel3.Execute(data3))
);
```

**Implementation**: The native layer maintains a thread-safe pool of `MTLCommandQueue` instances, preventing resource contention and ensuring safe concurrent execution.

### 4. Metal Performance Shaders Integration

MPS provides Apple-optimized implementations for common operations:

```csharp
using var mps = new MetalMPSOrchestrator(accelerator);

// Matrix multiplication (auto-selects MPS vs custom kernel)
var result = mps.MatrixMultiply(matrixA, matrixB);

// Neural network operations
mps.ReLU(input, output);
mps.BatchNormalization(input, gamma, beta, mean, variance, output);

// CNN operations
mps.Convolution2D(input, kernel, output, stride: 1, padding: 0);
```

**When to Use MPS**:
- Matrix sizes > 256x256 elements
- CNN operations (convolution, pooling)
- Neural network activations on large tensors
- When leveraging Apple's optimized implementations matters more than raw flexibility

### 5. Memory Pool Management

Automatic buffer recycling reduces allocation overhead:

```csharp
// Buffers are automatically pooled and recycled
for (int i = 0; i < 1000; i++)
{
    using var temp = accelerator.Allocate<float>(1024);
    // Buffer returned to pool on dispose
}
// 90%+ of allocations served from pool
```

## Performance Characteristics

### Benchmark Results (Apple M2)

| Operation | Performance | Target Met |
|-----------|-------------|------------|
| Kernel Cache Hit | 5.5 μs | ✅ (<1ms) |
| Command Buffer Acquisition | 0.27 μs | ✅ (<100μs) |
| Backend Cold Start | 7.76 ms | ✅ (<10ms) |
| Unified Memory Speedup | 1.5-2.5x | ✅ (>1.5x) |
| Parallel Execution Speedup | 2.2x | ✅ (>1.5x) |

### GPU Family Support

| GPU Family | Devices | Features |
|------------|---------|----------|
| Apple8 | M2, M2 Pro/Max/Ultra | Full MPS, advanced neural |
| Apple7 | M1, M1 Pro/Max/Ultra | Full MPS |
| Apple6 | A14 | MPS BLAS/CNN |
| Apple5 | A13 | Basic MPS |
| Mac2 | Intel Macs | Limited MPS |

## Thread Safety

### Command Queue Pool Architecture

The Metal backend implements thread-safe parallel execution through:

1. **Shared Command Queue Pool**: A map of device → command queues protected by mutex
2. **Per-Thread Command Buffers**: Each execution gets its own command buffer from the shared queue
3. **Explicit Cleanup**: Resources are properly released on dispose to prevent exit-time crashes

```cpp
// Native implementation (DCMetalDevice.mm)
static std::mutex s_commandQueueMutex;
static std::map<void*, id<MTLCommandQueue>> s_commandQueues;

id<MTLCommandQueue> getOrCreateCommandQueue(id<MTLDevice> device) {
    std::lock_guard<std::mutex> lock(s_commandQueueMutex);
    // Thread-safe queue retrieval/creation
}
```

### MPS Thread Safety

MPS operations use their own shared command queue pool with explicit cleanup:

```csharp
// Proper disposal prevents exit-time crashes
using var mpsBackend = new MetalPerformanceShadersBackend(device, logger);
// Operations...
// Dispose calls native cleanup automatically
```

## Best Practices

### 1. Kernel Optimization

```metal
// Use threadgroup memory for frequently accessed data
kernel void optimized_kernel(
    device float* data [[buffer(0)]],
    threadgroup float* shared [[threadgroup(0)]],
    uint tid [[thread_index_in_threadgroup]],
    uint gid [[thread_position_in_grid]])
{
    // Load to threadgroup memory
    shared[tid] = data[gid];
    threadgroup_barrier(mem_flags::mem_threadgroup);

    // Process with fast threadgroup access
    // ...
}
```

### 2. Buffer Reuse

```csharp
// Reuse buffers instead of reallocating
var buffer = accelerator.Allocate<float>(size);
for (int iteration = 0; iteration < 100; iteration++)
{
    buffer.CopyFrom(newData);
    kernel.Execute(buffer);
}
buffer.Dispose();
```

### 3. Batch Operations

```csharp
// Batch multiple operations in single command buffer
using var batch = accelerator.CreateCommandBatch();
batch.Add(kernel1, args1);
batch.Add(kernel2, args2);
batch.Add(kernel3, args3);
await batch.ExecuteAsync();
```

### 4. MPS vs Custom Kernels

```csharp
// Use MPSOrchestrator for automatic selection
var orchestrator = new MetalMPSOrchestrator(accelerator);

// Orchestrator checks:
// - Data size (MPS for large, custom for small)
// - Operation type
// - Device capabilities
var result = orchestrator.MatrixMultiply(a, b); // Auto-selects best path
```

## Troubleshooting

### Common Issues

**1. SIGSEGV on Disposal**
- **Cause**: Command queue cleanup conflicts with Objective-C ARC
- **Solution**: Ensure proper disposal order; use latest native library with cleanup functions

**2. Slow First Kernel Execution**
- **Cause**: JIT compilation of Metal shaders
- **Solution**: Use warm-up calls or enable binary caching

**3. Memory Pressure Warnings**
- **Cause**: Buffer pool exhaustion
- **Solution**: Dispose buffers promptly; check pool configuration

**4. Parallel Execution Crashes**
- **Cause**: Pre-v0.5 lacked command queue pooling
- **Solution**: Update to latest version with `MetalCommandQueuePool`

### Diagnostic Logging

Enable detailed Metal logging:

```csharp
var accelerator = MetalAccelerator.Create(options =>
{
    options.EnableDebugLogging = true;
    options.LogLevel = LogLevel.Trace;
});
```

### Native Library Issues

```bash
# Verify native library is loaded
otool -L libDotComputeMetal.dylib

# Check exported symbols
nm -gU libDotComputeMetal.dylib | grep DCMetal

# Rebuild native library
cd src/Backends/DotCompute.Backends.Metal/native
./build.sh
```

## API Reference

### MetalAccelerator

```csharp
public class MetalAccelerator : IAccelerator
{
    // Create accelerator for default GPU
    public static MetalAccelerator Create();

    // Device capabilities
    public MetalCapabilities Capabilities { get; }

    // Memory allocation
    public IBuffer<T> Allocate<T>(int count) where T : unmanaged;

    // Kernel compilation
    public ICompiledKernel CompileKernel(string source, string entryPoint);
}
```

### MetalCapabilities

```csharp
public readonly struct MetalCapabilities
{
    public string DeviceName { get; }
    public string GPUFamily { get; }       // Apple7, Apple8, etc.
    public bool HasUnifiedMemory { get; }
    public int MaxThreadsPerThreadgroup { get; }
    public long MaxBufferLength { get; }
}
```

### MetalMPSOrchestrator

```csharp
public class MetalMPSOrchestrator : IDisposable
{
    // BLAS operations
    public void MatrixMultiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b,
                               Span<float> c, int m, int n, int k);

    // Neural operations
    public void ReLU(ReadOnlySpan<float> input, Span<float> output);
    public void BatchNormalization(...);

    // CNN operations
    public void Convolution2D(...);
    public void MaxPooling2D(...);
}
```

## Version History

| Version | Changes |
|---------|---------|
| 0.5.0 | Command queue pooling, MPS cleanup fix |
| 0.4.2 | Binary kernel caching, memory pool optimization |
| 0.4.0 | MPS integration, unified memory support |
| 0.3.0 | Initial Metal backend release |

## See Also

- [Performance Optimization Guide](performance-optimization.md)
- [Memory Management](memory-management.md)
- [Backend Selection](backend-selection.md)
- [Apple Metal Documentation](https://developer.apple.com/metal/)
