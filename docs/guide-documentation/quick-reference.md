# DotCompute Quick Reference Guide

## Essential Commands

### Package Installation
```bash
# Core package (required)
dotnet add package DotCompute.Core

# Backend packages (choose one or more)
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Backends.CUDA
dotnet add package DotCompute.Backends.Metal

# Optional packages
dotnet add package DotCompute.SourceGenerators
dotnet add package DotCompute.Plugins
```

### Basic Usage Pattern
```csharp
// 1. Initialize
var accelerator = AcceleratorManager.GetBestAccelerator();

// 2. Compile kernel
var kernel = accelerator.CompileKernel<Action<ReadOnlySpan<float>, Span<float>>>(MyKernel);

// 3. Allocate memory
using var input = UnifiedBuffer<float>.Allocate(size);
using var output = UnifiedBuffer<float>.Allocate(size);

// 4. Execute
kernel.Execute(new GridSize(size), input.AsReadOnly(), output.AsSpan());

// 5. Get results
var results = output.Read();
```

## Kernel Patterns

### Simple Kernel
```csharp
[ComputeKernel]
public static void SimpleKernel(ReadOnlySpan<float> input, Span<float> output)
{
    var idx = KernelIndex.X;
    output[idx] = input[idx] * 2.0f;
}
```

### Reduction Kernel
```csharp
[ComputeKernel]
[SharedMemory(typeof(float), 256)]
public static void ReductionKernel(ReadOnlySpan<float> input, Span<float> output)
{
    var tid = KernelIndex.LocalX;
    var gid = KernelIndex.X;
    
    SharedMemory<float>.Store(tid, input[gid]);
    KernelBarrier.Sync();
    
    // Reduction loop
    for (int s = BlockSize.X / 2; s > 0; s >>= 1)
    {
        if (tid < s)
        {
            var sum = SharedMemory<float>.Load(tid) + SharedMemory<float>.Load(tid + s);
            SharedMemory<float>.Store(tid, sum);
        }
        KernelBarrier.Sync();
    }
    
    if (tid == 0)
    {
        output[KernelIndex.BlockX] = SharedMemory<float>.Load(0);
    }
}
```

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
    
    if (row < M && col < N)
    {
        float sum = 0.0f;
        for (int k = 0; k < K; k++)
        {
            sum += a[row * K + k] * b[k * N + col];
        }
        c[row * N + col] = sum;
    }
}
```

## Memory Management

### Buffer Operations
```csharp
// Allocation
var buffer = UnifiedBuffer<T>.Allocate(count);
var aligned = UnifiedBuffer<T>.AllocateAligned(count, 64);

// Data transfer
buffer.Write(sourceData);
var data = buffer.Read();

// Memory views
var readOnly = buffer.AsReadOnly();
var span = buffer.AsSpan();

// Pinning
using var pinned = UnifiedBuffer<T>.Pin(managedArray);

// Disposal
buffer.Dispose();
```

### Memory Pooling
```csharp
var pool = new UnifiedMemoryPool<float>(maxBuffers: 10, bufferSize: 1024);

using (var buffer = pool.Rent())
{
    // Use buffer
}
// Automatically returned
```

## Pipeline Usage

### Basic Pipeline
```csharp
var pipeline = new ComputePipeline()
    .AddStage("normalize", normalizeKernel)
    .AddStage("compute", computeKernel)
    .AddStage("reduce", reduceKernel)
    .Build();

var result = await pipeline.ExecuteAsync(input);
```

### Pipeline with Parameters
```csharp
var pipeline = new ComputePipeline()
    .AddStage("scale", scaleKernel, new { factor = 2.0f })
    .AddStage("threshold", thresholdKernel, new { min = 0.0f, max = 1.0f })
    .Build();
```

## Performance Tips

### 1. Grid Size Selection
```csharp
// CPU: Multiple of processor count
var cpuGrid = new GridSize(Environment.ProcessorCount * 4);

// GPU: Multiple of warp size
var gpuGrid = new GridSize(256 * 1024); // CUDA
```

### 2. Memory Alignment
```csharp
// Align to cache line for better performance
var buffer = UnifiedBuffer<float>.AllocateAligned(size, 64);
```

### 3. Kernel Fusion
```csharp
// Instead of multiple kernels:
kernel1.Execute(grid, a, b);
kernel2.Execute(grid, b, c);

// Fuse into one:
[ComputeKernel]
public static void FusedKernel(ReadOnlySpan<float> a, Span<float> c)
{
    var idx = KernelIndex.X;
    var temp = a[idx] * 2.0f; // kernel1 operation
    c[idx] = MathF.Sqrt(temp); // kernel2 operation
}
```

### 4. SIMD Usage (CPU)
```csharp
[ComputeKernel]
[EnableSimd]
public static void SimdKernel(ReadOnlySpan<float> a, Span<float> b)
{
    var idx = KernelIndex.X * Vector<float>.Count;
    var vec = Vector.Load(a, idx);
    var result = vec * new Vector<float>(2.0f);
    result.Store(b, idx);
}
```

## Debugging

### Enable Debug Mode
```csharp
var options = new AcceleratorOptions
{
    EnableDebugMode = true,
    EnableProfiling = true
};

var accelerator = new CpuAccelerator(options);
```

### Performance Profiling
```csharp
using (var profiler = accelerator.BeginProfiling())
{
    kernel.Execute(grid, input, output);
}

var metrics = profiler.GetMetrics();
Console.WriteLine($"Execution time: {metrics.KernelTime}ms");
```

### Memory Tracking
```csharp
UnifiedMemoryManager.EnableTracking = true;

// Run code...

var stats = UnifiedMemoryManager.GetStatistics();
Console.WriteLine($"Peak memory: {stats.PeakUsage / 1024 / 1024}MB");
```

## Error Handling

### Specific Exception Types
```csharp
try
{
    kernel.Execute(grid, input, output);
}
catch (MemoryException ex) when (ex.ErrorType == MemoryErrorType.OutOfMemory)
{
    Console.WriteLine($"Out of memory: requested {ex.RequestedSize} bytes");
}
catch (AcceleratorException ex) when (ex.ErrorType == AcceleratorErrorType.KernelExecutionFailed)
{
    Console.WriteLine($"Kernel failed on device {ex.DeviceId}");
}
catch (KernelCompilationException ex)
{
    Console.WriteLine($"Compilation error: {ex.Message}");
}
```

## Platform-Specific Notes

### Windows
```csharp
// Check for AVX support
if (CpuInfo.IsAvxSupported)
{
    // Use AVX-optimized kernels
}
```

### Linux
```csharp
// Check CUDA availability
if (CudaAccelerator.IsSupported())
{
    var cuda = new CudaAccelerator();
}
```

### macOS
```csharp
// Use Metal on Apple Silicon
if (MetalAccelerator.IsSupported())
{
    var metal = new MetalAccelerator();
}
```

## Common Gotchas

1. **Index Bounds**: Always check bounds in kernels
2. **Memory Alignment**: Use aligned allocations for SIMD
3. **Grid Size**: Must cover all data elements
4. **Shared Memory**: Limited size per block
5. **Disposal**: Always dispose buffers and accelerators

## Need Help?

- 📚 [Full Documentation](wiki-home.md)
- 🐛 [Report Issues](https://github.com/yourusername/DotCompute/issues)
- 💬 [Discussions](https://github.com/yourusername/DotCompute/discussions)
- 📊 [Examples](../example-code/)