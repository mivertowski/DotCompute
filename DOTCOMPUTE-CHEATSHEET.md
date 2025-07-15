# DotCompute Cheat Sheet 🚀

## Quick Start Guide

### Installation
```bash
# Install DotCompute packages
dotnet add package DotCompute.Core
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Backends.CUDA  # Optional: For GPU support
```

### Your First Kernel
```csharp
using DotCompute.Core;
using DotCompute.Backends.CPU;

// 1. Define a kernel
[ComputeKernel]
public static void VectorAdd(
    ReadOnlySpan<float> a, 
    ReadOnlySpan<float> b, 
    Span<float> result, 
    int length)
{
    var idx = KernelIndex.X;
    if (idx < length)
    {
        result[idx] = a[idx] + b[idx];
    }
}

// 2. Initialize compute system
var accelerator = new CpuAccelerator();
var kernel = accelerator.CompileKernel<VectorAddDelegate>(VectorAdd);

// 3. Execute kernel
kernel.Execute(new GridSize(1024), arrayA, arrayB, result, 1024);
```

## Common Patterns & Best Practices

### 1. Kernel Definition Patterns

#### Basic Compute Kernel
```csharp
[ComputeKernel]
public static void SimpleKernel(ReadOnlySpan<float> input, Span<float> output)
{
    var idx = KernelIndex.X;
    output[idx] = MathF.Sqrt(input[idx]);
}
```

#### 2D Grid Kernel
```csharp
[ComputeKernel]
public static void MatrixKernel(ReadOnlySpan<float> matrix, Span<float> output, int width)
{
    var x = KernelIndex.X;
    var y = KernelIndex.Y;
    var idx = y * width + x;
    output[idx] = matrix[idx] * 2.0f;
}
```

#### Shared Memory Kernel
```csharp
[ComputeKernel]
[SharedMemory(typeof(float), 256)]
public static void ReductionKernel(ReadOnlySpan<float> input, Span<float> output)
{
    var tid = KernelIndex.LocalX;
    var gid = KernelIndex.X;
    
    // Load to shared memory
    SharedMemory<float>.Store(tid, input[gid]);
    KernelBarrier.Sync();
    
    // Reduction in shared memory
    for (int s = BlockSize.X / 2; s > 0; s >>= 1)
    {
        if (tid < s)
        {
            var val1 = SharedMemory<float>.Load(tid);
            var val2 = SharedMemory<float>.Load(tid + s);
            SharedMemory<float>.Store(tid, val1 + val2);
        }
        KernelBarrier.Sync();
    }
}
```

### 2. Memory Management Patterns

#### Unified Buffer Usage
```csharp
// Create unified buffer (works on CPU/GPU)
using var buffer = UnifiedBuffer<float>.Allocate(1024);

// Write data
buffer.Write(sourceData);

// Use in kernel
kernel.Execute(gridSize, buffer.AsReadOnly(), outputBuffer);

// Read results
var results = buffer.Read();
```

#### Memory Pooling
```csharp
// Use memory pool for repeated allocations
var pool = new UnifiedMemoryPool<float>(maxBuffers: 10, bufferSize: 1024);

using (var buffer = pool.Rent())
{
    // Use buffer...
    kernel.Execute(gridSize, buffer.AsSpan());
}
// Buffer automatically returned to pool
```

#### Zero-Copy Operations
```csharp
// Pin managed memory for zero-copy access
using var pinned = UnifiedBuffer<float>.Pin(managedArray);
kernel.Execute(gridSize, pinned.AsReadOnly(), output);
```

### 3. Performance Optimization Patterns

#### SIMD Vectorization (CPU)
```csharp
[ComputeKernel]
[EnableSimd]
public static void SimdKernel(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c)
{
    var idx = KernelIndex.X * Vector<float>.Count;
    var vecA = Vector.Load(a, idx);
    var vecB = Vector.Load(b, idx);
    var result = vecA + vecB;
    result.Store(c, idx);
}
```

#### Kernel Fusion
```csharp
// Fuse multiple operations into one kernel
[ComputeKernel]
public static void FusedKernel(
    ReadOnlySpan<float> input, 
    Span<float> output,
    float scale,
    float offset)
{
    var idx = KernelIndex.X;
    // Fused: scale, add offset, and clamp in one pass
    output[idx] = Math.Clamp(input[idx] * scale + offset, 0.0f, 1.0f);
}
```

#### Coalesced Memory Access
```csharp
[ComputeKernel]
public static void CoalescedAccess(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    // Good: Adjacent threads access adjacent memory
    var idx = KernelIndex.X;
    output[idx] = input[idx];
    
    // Bad: Strided access pattern
    // output[idx] = input[idx * stride];
}
```

### 4. Multi-Backend Patterns

#### Backend Selection
```csharp
// Auto-select best available backend
var accelerator = AcceleratorManager.GetBestAccelerator();

// Or explicitly choose backend
var cpuAccelerator = AcceleratorManager.GetAccelerator(AcceleratorType.CPU);
var cudaAccelerator = AcceleratorManager.GetAccelerator(AcceleratorType.CUDA);
```

#### Portable Kernel Code
```csharp
[ComputeKernel]
[SupportedBackends(Backend.CPU | Backend.CUDA | Backend.Metal)]
public static void PortableKernel(ReadOnlySpan<float> input, Span<float> output)
{
    // This kernel works on all backends
    var idx = KernelIndex.X;
    output[idx] = MathF.Exp(input[idx]);
}
```

## API Reference Quick Guide

### Core Types

| Type | Purpose | Example |
|------|---------|---------|
| `IAccelerator` | Compute device abstraction | `var acc = new CpuAccelerator()` |
| `ICompiledKernel` | Compiled kernel instance | `var kernel = acc.CompileKernel<T>()` |
| `UnifiedBuffer<T>` | Cross-device memory | `var buf = UnifiedBuffer<T>.Allocate(size)` |
| `GridSize` | Execution configuration | `new GridSize(1024, 1, 1)` |
| `KernelIndex` | Thread index in kernel | `var idx = KernelIndex.X` |

### Kernel Attributes

| Attribute | Purpose | Usage |
|-----------|---------|-------|
| `[ComputeKernel]` | Mark kernel method | Required on all kernels |
| `[SharedMemory]` | Allocate shared memory | `[SharedMemory(typeof(float), 256)]` |
| `[EnableSimd]` | Enable SIMD optimization | CPU kernels only |
| `[SupportedBackends]` | Specify backend support | `[SupportedBackends(Backend.CPU)]` |

### Memory Operations

```csharp
// Allocation
var buffer = UnifiedBuffer<T>.Allocate(count);
var buffer = UnifiedBuffer<T>.AllocateAligned(count, alignment);

// Data Transfer
buffer.Write(sourceSpan);
buffer.Read(destSpan);
buffer.CopyTo(otherBuffer);

// Memory Views
var readOnly = buffer.AsReadOnly();
var span = buffer.AsSpan();

// Pinning
using var pinned = UnifiedBuffer<T>.Pin(array);
```

### Kernel Execution

```csharp
// Basic execution
kernel.Execute(gridSize, arg1, arg2, ...);

// With stream (async)
kernel.ExecuteAsync(stream, gridSize, arg1, arg2, ...);

// Pipeline execution
var pipeline = new ComputePipeline()
    .AddStage(kernel1)
    .AddStage(kernel2)
    .Build();

await pipeline.ExecuteAsync(input);
```

## Example Code Snippets

### Matrix Multiplication
```csharp
[ComputeKernel]
public static void MatMul(
    ReadOnlySpan<float> a, 
    ReadOnlySpan<float> b, 
    Span<float> c,
    int M, int N, int K)
{
    var row = KernelIndex.Y;
    var col = KernelIndex.X;
    
    float sum = 0.0f;
    for (int k = 0; k < K; k++)
    {
        sum += a[row * K + k] * b[k * N + col];
    }
    c[row * N + col] = sum;
}
```

### Image Convolution
```csharp
[ComputeKernel]
[SharedMemory(typeof(float), 18 * 18)] // For 16x16 tile with 1-pixel border
public static void Convolution3x3(
    ReadOnlySpan<float> image,
    ReadOnlySpan<float> kernel,
    Span<float> output,
    int width, int height)
{
    // Load tile to shared memory with borders
    // Apply convolution
    // Write result
}
```

### Parallel Reduction
```csharp
[ComputeKernel]
public static void ParallelSum(
    ReadOnlySpan<float> input,
    Span<float> partialSums,
    int n)
{
    var tid = KernelIndex.X;
    var gridSize = GridSize.X;
    
    float sum = 0.0f;
    for (int i = tid; i < n; i += gridSize)
    {
        sum += input[i];
    }
    
    // Warp-level reduction
    sum = WarpReduce.Sum(sum);
    
    if (KernelIndex.LocalX == 0)
    {
        partialSums[KernelIndex.BlockX] = sum;
    }
}
```

## Troubleshooting Guide

### Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| `KernelCompilationException` | Unsupported operation in kernel | Check kernel constraints for backend |
| `OutOfMemoryException` | Buffer allocation failed | Reduce allocation size or use pooling |
| `AccessViolationException` | Out-of-bounds access | Verify index calculations and bounds |
| `InvalidOperationException` | Backend not available | Check platform support and drivers |

### Performance Debugging

```csharp
// Enable performance monitoring
var options = new AcceleratorOptions
{
    EnableProfiling = true,
    EnableDebugMode = true
};

var accelerator = new CpuAccelerator(options);

// Get performance metrics
var metrics = accelerator.GetPerformanceMetrics();
Console.WriteLine($"Kernel time: {metrics.KernelExecutionTime}ms");
Console.WriteLine($"Memory transfer: {metrics.MemoryTransferTime}ms");
```

### Memory Leak Detection

```csharp
// Track buffer allocations
UnifiedMemoryManager.EnableTracking = true;

// ... run your code ...

// Check for leaks
var stats = UnifiedMemoryManager.GetStatistics();
if (stats.ActiveAllocations > 0)
{
    Console.WriteLine($"Leaked buffers: {stats.ActiveAllocations}");
    Console.WriteLine($"Leaked bytes: {stats.ActiveBytes}");
}
```

## Performance Optimization Tips

### 1. **Choose the Right Grid Size**
```csharp
// CPU: Use number of cores
var gridSize = new GridSize(Environment.ProcessorCount * 4);

// GPU: Use multiple of warp/wavefront size
var gridSize = new GridSize(1024 * 256); // CUDA
```

### 2. **Minimize Memory Transfers**
```csharp
// Bad: Multiple small transfers
for (int i = 0; i < n; i++)
    buffer.Write(data[i], offset: i);

// Good: Single bulk transfer
buffer.Write(data);
```

### 3. **Use Appropriate Data Types**
```csharp
// Use float instead of double when precision allows
[ComputeKernel]
public static void FastKernel(ReadOnlySpan<float> input, Span<float> output)
{
    // float operations are 2x faster on most GPUs
}
```

### 4. **Enable Compiler Optimizations**
```csharp
var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.Maximum,
    EnableFastMath = true,
    UnrollLoops = true
};
```

### 5. **Profile and Measure**
```csharp
using (var profiler = accelerator.BeginProfiling())
{
    kernel.Execute(gridSize, input, output);
}

var report = profiler.GetReport();
Console.WriteLine(report.ToString());
```

## Platform-Specific Notes

### Windows
- CUDA requires NVIDIA drivers 470+
- CPU backend uses AVX512 when available
- Enable Windows Hardware Accelerated GPU Scheduling

### Linux
- Install CUDA toolkit for GPU support
- CPU backend auto-detects NEON on ARM64
- Use `numactl` for NUMA optimization

### macOS
- Metal backend requires macOS 12+
- Optimized for Apple Silicon (M1/M2/M3)
- Rosetta 2 supported for x64 emulation

## Quick Reference Card

```csharp
// === SETUP ===
using DotCompute.Core;
var acc = AcceleratorManager.GetBestAccelerator();

// === MEMORY ===
var buf = UnifiedBuffer<float>.Allocate(1024);
buf.Write(data);
var result = buf.Read();

// === KERNELS ===
[ComputeKernel]
static void MyKernel(ReadOnlySpan<float> input, Span<float> output)
{
    output[KernelIndex.X] = input[KernelIndex.X] * 2;
}

// === EXECUTION ===
var kernel = acc.CompileKernel<Action<ReadOnlySpan<float>, Span<float>>>(MyKernel);
kernel.Execute(new GridSize(1024), inputBuf, outputBuf);

// === CLEANUP ===
buf.Dispose();
acc.Dispose();
```

---

**Need more help?** Check the [full documentation](docs/README.md) or [open an issue](https://github.com/yourusername/DotCompute/issues).