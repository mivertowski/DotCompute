# OpenCL Performance Optimization Guide

Comprehensive guide to maximizing performance with the OpenCL backend.

## Table of Contents

- [Overview](#overview)
- [Kernel Optimization](#kernel-optimization)
- [Memory Optimization](#memory-optimization)
- [Work Group Sizing](#work-group-sizing)
- [Data Transfer Optimization](#data-transfer-optimization)
- [Compilation Optimization](#compilation-optimization)
- [Profiling and Benchmarking](#profiling-and-benchmarking)
- [Vendor-Specific Tips](#vendor-specific-tips)

## Overview

Performance optimization for OpenCL involves multiple layers:

1. **Kernel Code**: Algorithm efficiency, memory access patterns
2. **Memory Management**: Buffer reuse, transfer minimization
3. **Execution Configuration**: Work group sizes, local memory usage
4. **Compilation**: Compiler flags, binary caching
5. **Host-Device Interaction**: Asynchronous operations, command batching

## Kernel Optimization

### Memory Access Patterns

#### ✅ Good: Coalesced Memory Access

```opencl
__kernel void CoalescedAccess(
    __global const float* input,
    __global float* output,
    const int width)
{
    int gid = get_global_id(0);
    int x = gid % width;
    int y = gid / width;

    // Sequential access - coalesced
    int index = y * width + x;
    output[index] = input[index] * 2.0f;
}
```

#### ❌ Bad: Strided Memory Access

```opencl
__kernel void StridedAccess(
    __global const float* input,
    __global float* output,
    const int width)
{
    int gid = get_global_id(0);

    // Non-sequential access - poor performance
    for (int i = 0; i < width; i++)
    {
        output[i * width + gid] = input[i * width + gid] * 2.0f;
    }
}
```

### Local Memory Usage

#### Using Local Memory for Data Reuse

```opencl
__kernel void MatrixMultiplyTiled(
    __global const float* A,
    __global const float* B,
    __global float* C,
    const int N,
    __local float* tileA,
    __local float* tileB)
{
    const int TILE_SIZE = 16;
    int row = get_global_id(1);
    int col = get_global_id(0);
    int localRow = get_local_id(1);
    int localCol = get_local_id(0);

    float sum = 0.0f;

    // Process matrix in tiles
    for (int t = 0; t < N / TILE_SIZE; t++)
    {
        // Load tile into local memory
        tileA[localRow * TILE_SIZE + localCol] =
            A[row * N + (t * TILE_SIZE + localCol)];
        tileB[localRow * TILE_SIZE + localCol] =
            B[(t * TILE_SIZE + localRow) * N + col];

        // Synchronize to ensure all data is loaded
        barrier(CLK_LOCAL_MEM_FENCE);

        // Compute using local memory (fast)
        for (int k = 0; k < TILE_SIZE; k++)
        {
            sum += tileA[localRow * TILE_SIZE + k] *
                   tileB[k * TILE_SIZE + localCol];
        }

        // Synchronize before loading next tile
        barrier(CLK_LOCAL_MEM_FENCE);
    }

    C[row * N + col] = sum;
}
```

**C# Invocation**:
```csharp
const int tileSize = 16;
const int N = 1024;

var execution = accelerator.CreateExecution(kernel)
    .WithArgument(matrixA)
    .WithArgument(matrixB)
    .WithArgument(matrixC)
    .WithArgument(N)
    .WithLocalMemory(tileSize * tileSize * sizeof(float))
    .WithLocalMemory(tileSize * tileSize * sizeof(float))
    .WithGlobalWorkSize(N, N)
    .WithLocalWorkSize(tileSize, tileSize);

await execution.ExecuteAsync();
```

### Vector Types

```opencl
// Slower: Scalar operations
__kernel void ScalarAdd(
    __global const float* a,
    __global const float* b,
    __global float* result)
{
    int gid = get_global_id(0);
    result[gid] = a[gid] + b[gid];
}

// Faster: Vector operations (4x throughput)
__kernel void VectorAdd(
    __global const float4* a,
    __global const float4* b,
    __global float4* result)
{
    int gid = get_global_id(0);
    result[gid] = a[gid] + b[gid]; // Processes 4 elements at once
}
```

### Loop Unrolling

```opencl
// Manual loop unrolling for small, known iterations
__kernel void UnrolledSum(
    __global const float* input,
    __global float* output,
    const int stride)
{
    int gid = get_global_id(0);
    float sum = 0.0f;

    // Unrolled loop (better instruction-level parallelism)
    int base = gid * stride;
    sum += input[base];
    sum += input[base + 1];
    sum += input[base + 2];
    sum += input[base + 3];

    output[gid] = sum;
}
```

### Avoid Divergent Branches

```opencl
// ❌ Bad: Divergent branches within work group
__kernel void DivergentBranches(__global float* data)
{
    int gid = get_global_id(0);

    if (gid % 2 == 0)
    {
        // Complex computation
        data[gid] = sqrt(data[gid] * data[gid] + 1.0f);
    }
    else
    {
        // Simple computation
        data[gid] *= 2.0f;
    }
}

// ✅ Good: Eliminate branches
__kernel void NoBranches(__global float* data)
{
    int gid = get_global_id(0);

    // Compute both paths, select with multiplication
    float even_result = sqrt(data[gid] * data[gid] + 1.0f);
    float odd_result = data[gid] * 2.0f;

    float is_even = (gid % 2 == 0) ? 1.0f : 0.0f;
    data[gid] = is_even * even_result + (1.0f - is_even) * odd_result;
}
```

## Memory Optimization

### Buffer Pooling

```csharp
using DotCompute.Backends.OpenCL.Memory;

// Enable memory pooling in configuration
var config = new OpenCLConfiguration
{
    Memory = new MemoryConfiguration
    {
        EnablePooling = true,
        InitialPoolSize = 64 * 1024 * 1024, // 64 MB
        MaxPoolSize = 1024 * 1024 * 1024 // 1 GB
    }
};

var accelerator = new OpenCLAccelerator(loggerFactory, config);

// Buffers are automatically pooled
var buffer1 = await accelerator.AllocateAsync<float>(1000);
await buffer1.DisposeAsync(); // Returns to pool

var buffer2 = await accelerator.AllocateAsync<float>(1000);
// buffer2 may reuse buffer1's memory
```

### Minimize Host-Device Transfers

```csharp
// ❌ Bad: Multiple transfers
for (int i = 0; i < 100; i++)
{
    await inputBuffer.CopyFromAsync(hostData);
    await kernel.ExecuteAsync();
    await outputBuffer.CopyToAsync(results);
}

// ✅ Good: Batch processing on device
await inputBuffer.CopyFromAsync(hostData);

for (int i = 0; i < 100; i++)
{
    await kernel.ExecuteAsync(); // No transfer
}

await outputBuffer.CopyToAsync(results);
```

### Use Pinned Memory for Frequent Transfers

```csharp
// Allocate with CL_MEM_ALLOC_HOST_PTR for faster transfers
var options = MemoryOptions.Mapped; // Maps to CL_MEM_ALLOC_HOST_PTR

var buffer = await accelerator.AllocateAsync<float>(
    1000000,
    options);

// Transfers will be faster due to pinned memory
await buffer.CopyFromAsync(largeHostArray);
```

### Asynchronous Transfers

```csharp
// Overlap computation and transfer
var buffer1 = await accelerator.AllocateAsync<float>(size);
var buffer2 = await accelerator.AllocateAsync<float>(size);

// Start transfer for buffer1 (non-blocking)
var transferTask = buffer1.CopyFromAsync(hostData1);

// Execute on buffer2 (from previous iteration)
await kernel.ExecuteAsync(buffer2, output);

// Wait for transfer to complete
await transferTask;

// Now execute on buffer1
await kernel.ExecuteAsync(buffer1, output);
```

## Work Group Sizing

### Automatic Sizing

```csharp
// Let OpenCL choose optimal work group size
var execution = accelerator.CreateExecution(kernel)
    .WithArgument(input)
    .WithArgument(output)
    .WithGlobalWorkSize(totalWorkItems);
    // No local work size specified - automatic selection

await execution.ExecuteAsync();
```

### Manual Tuning

```csharp
// Query device capabilities
var device = accelerator.DeviceInfo;
var maxWorkGroupSize = device.MaxWorkGroupSize;
var preferredMultiple = device.PreferredWorkGroupSizeMultiple;

Console.WriteLine($"Max work group size: {maxWorkGroupSize}");
Console.WriteLine($"Preferred multiple: {preferredMultiple}");

// Set work group size as multiple of preferred size
int localSize = 256; // Common choice for NVIDIA (multiple of 32)
int globalSize = (totalItems + localSize - 1) / localSize * localSize;

var execution = accelerator.CreateExecution(kernel)
    .WithArgument(input)
    .WithArgument(output)
    .WithGlobalWorkSize(globalSize)
    .WithLocalWorkSize(localSize);

await execution.ExecuteAsync();
```

### Vendor-Specific Recommendations

```csharp
// Use vendor adapter for optimal sizing
var vendorAdapter = accelerator.VendorAdapter;
var optimalSize = vendorAdapter.GetOptimalWorkGroupSize(
    device, compiledKernel);

Console.WriteLine($"Vendor-recommended work group size: {optimalSize}");
```

### 2D/3D Work Groups

```csharp
// Image processing with 2D work groups
int width = 1920;
int height = 1080;
int tileWidth = 16;
int tileHeight = 16;

var execution = accelerator.CreateExecution(imageKernel)
    .WithArgument(inputImage)
    .WithArgument(outputImage)
    .WithGlobalWorkSize(width, height)
    .WithLocalWorkSize(tileWidth, tileHeight);

await execution.ExecuteAsync();
```

## Data Transfer Optimization

### Batch Transfers

```csharp
// ❌ Bad: Multiple small transfers
for (int i = 0; i < 100; i++)
{
    await buffer.CopyFromAsync(smallChunk[i]);
}

// ✅ Good: Single large transfer
var allData = smallChunks.SelectMany(x => x).ToArray();
await buffer.CopyFromAsync(allData);
```

### Buffer Reuse

```csharp
// ✅ Reuse buffers across iterations
var workBuffer = await accelerator.AllocateAsync<float>(size);

for (int iteration = 0; iteration < 100; iteration++)
{
    // Reuse same buffer
    await workBuffer.CopyFromAsync(inputData[iteration]);
    await kernel.ExecuteAsync(workBuffer, outputBuffer);
}

await workBuffer.DisposeAsync();
```

### Mapped Buffers for CPU Access

```opencl
// For frequently accessed buffers, use mapping instead of copies
var mappedOptions = MemoryOptions.Mapped;
var buffer = await accelerator.AllocateAsync<float>(size, mappedOptions);

// Map for direct CPU access (zero-copy on some platforms)
var mapped = buffer.Map(MapFlags.Write);
try
{
    // Direct memory access
    for (int i = 0; i < size; i++)
    {
        mapped[i] = ComputeValue(i);
    }
}
finally
{
    buffer.Unmap(mapped);
}
```

## Compilation Optimization

### Compilation Options

```csharp
using DotCompute.Backends.OpenCL.Compilation;

var options = new CompilationOptions
{
    OptimizationLevel = 3, // Maximum optimization
    EnableFastMath = true, // Relaxed math precision
    EnableMadEnable = true, // Multiply-add fusion
    EnableDebugInfo = false, // Disable for production
    AdditionalFlags = new[]
    {
        "-cl-finite-math-only", // Assume finite values
        "-cl-no-signed-zeros", // Optimize zero handling
        "-cl-unsafe-math-optimizations" // Aggressive math opts
    }
};

var kernel = await compiler.CompileAsync(source, "MyKernel", options);
```

### Cache Utilization

```csharp
// Compilation cache is automatic
// First compilation:
var kernel1 = await accelerator.CompileKernelAsync(definition); // ~100ms

// Subsequent compilations (cache hit):
var kernel2 = await accelerator.CompileKernelAsync(definition); // ~1ms

// Check cache status
var cache = OpenCLCompilationCache.Instance;
Console.WriteLine($"Cache entries: {cache.EntryCount}");
Console.WriteLine($"Cache size: {cache.TotalSize / 1024} KB");
```

### Pre-compilation

```csharp
// Pre-compile kernels at application startup
public async Task PrecompileKernelsAsync(OpenCLAccelerator accelerator)
{
    var kernelSources = new[]
    {
        ("VectorAdd", vectorAddSource),
        ("MatrixMultiply", matrixMultiplySource),
        ("Reduction", reductionSource)
    };

    var tasks = kernelSources.Select(async (name, source) =>
    {
        var definition = new KernelDefinition
        {
            Name = name,
            Source = source,
            EntryPoint = name
        };

        await accelerator.CompileKernelAsync(definition);
    });

    await Task.WhenAll(tasks);

    Console.WriteLine("All kernels pre-compiled");
}
```

## Profiling and Benchmarking

### Enable Profiling

```csharp
using DotCompute.Backends.OpenCL.Profiling;

var config = new OpenCLConfiguration
{
    Event = new EventConfiguration
    {
        EnableProfiling = true
    }
};

var accelerator = new OpenCLAccelerator(loggerFactory, config);
```

### Collect Performance Metrics

```csharp
var profiler = new OpenCLProfiler(context, logger);

// Start profiling session
var session = profiler.BeginSession("VectorAddBenchmark");

// Execute operations
await inputBuffer.CopyFromAsync(inputData);
await kernel.ExecuteAsync();
await outputBuffer.CopyToAsync(outputData);

// End session and get results
var stats = profiler.EndSession(session);

Console.WriteLine($"Total time: {stats.ExecutionTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"Kernel time: {stats.KernelTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"Memory transfer: {stats.MemoryTransferTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"Overhead: {stats.QueuedTime.TotalMilliseconds:F3} ms");

// Calculate throughput
var elementsProcessed = inputData.Length;
var throughputGBps = (elementsProcessed * sizeof(float) * 2) /
                     stats.ExecutionTime.TotalSeconds / (1024 * 1024 * 1024);
Console.WriteLine($"Throughput: {throughputGBps:F2} GB/s");
```

### Benchmark Framework

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, targetCount: 10)]
public class OpenCLBenchmarks
{
    private OpenCLAccelerator _accelerator;
    private ICompiledKernel _kernel;
    private IUnifiedMemoryBuffer<float> _inputBuffer;
    private IUnifiedMemoryBuffer<float> _outputBuffer;

    [Params(1000, 10000, 100000, 1000000)]
    public int N;

    [GlobalSetup]
    public async Task Setup()
    {
        _accelerator = new OpenCLAccelerator(LoggerFactory.Create(_ => { }));
        await _accelerator.InitializeAsync();

        var definition = new KernelDefinition { /* ... */ };
        _kernel = await _accelerator.CompileKernelAsync(definition);

        _inputBuffer = await _accelerator.AllocateAsync<float>((nuint)N);
        _outputBuffer = await _accelerator.AllocateAsync<float>((nuint)N);
    }

    [Benchmark]
    public async Task<float> VectorAddOpenCL()
    {
        await _kernel.ExecuteAsync(_inputBuffer, _outputBuffer);
        await _accelerator.SynchronizeAsync();

        var result = new float[1];
        await _outputBuffer.CopyToAsync(result.AsMemory(0, 1));
        return result[0];
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _inputBuffer?.Dispose();
        _outputBuffer?.Dispose();
        _accelerator?.Dispose();
    }
}

// Run benchmarks
BenchmarkRunner.Run<OpenCLBenchmarks>();
```

## Vendor-Specific Tips

### NVIDIA

```csharp
// Optimal work group size: Multiple of warp size (32)
int workGroupSize = 256; // 8 warps

// Use float4 for memory coalescing
__kernel void NvidiaOptimized(__global const float4* data)
{
    int gid = get_global_id(0);
    float4 val = data[gid]; // Coalesced 128-bit load
    // Process...
}

// Compiler flags
var options = new CompilationOptions
{
    AdditionalFlags = new[]
    {
        "-cl-nv-maxrregcount=32", // Limit register usage
        "-cl-nv-verbose" // Get compilation info
    }
};
```

### AMD

```csharp
// Optimal work group size: Multiple of wavefront (64)
int workGroupSize = 256; // 4 wavefronts

// Use LDS (Local Data Share) effectively
__kernel void AmdOptimized(
    __global float* data,
    __local float* lds)
{
    int lid = get_local_id(0);

    // Load to LDS
    lds[lid] = data[get_global_id(0)];
    barrier(CLK_LOCAL_MEM_FENCE);

    // Process from LDS (faster)
    float result = lds[lid] * 2.0f;
    data[get_global_id(0)] = result;
}

// Compiler flags
var options = new CompilationOptions
{
    AdditionalFlags = new[]
    {
        "-cl-amd-media-ops" // Enable media operations
    }
};
```

### Intel

```csharp
// Use sub-groups for better SIMD utilization
__kernel void IntelOptimized(__global float* data)
{
    int gid = get_global_id(0);
    int sub_group_size = get_sub_group_size();

    // Sub-group operations are highly efficient on Intel
    float val = data[gid];
    float sum = sub_group_reduce_add(val);

    if (get_sub_group_local_id() == 0)
    {
        data[gid / sub_group_size] = sum;
    }
}

// Work group size should be multiple of sub-group size
int subGroupSize = 16; // Typical for Intel
int workGroupSize = subGroupSize * 4; // 64
```

## Performance Checklist

✅ **Memory**
- [ ] Use memory pooling
- [ ] Minimize host-device transfers
- [ ] Use coalesced memory access
- [ ] Utilize local memory for data reuse

✅ **Kernels**
- [ ] Avoid divergent branches
- [ ] Use vector types (float4, int4)
- [ ] Unroll small loops
- [ ] Optimize work group size

✅ **Compilation**
- [ ] Enable aggressive optimizations
- [ ] Use fast math where appropriate
- [ ] Pre-compile kernels at startup
- [ ] Leverage binary cache

✅ **Profiling**
- [ ] Enable profiling in production
- [ ] Measure kernel execution time
- [ ] Track memory transfer overhead
- [ ] Identify bottlenecks

✅ **Vendor-Specific**
- [ ] Use vendor adapter recommendations
- [ ] Apply vendor-specific compiler flags
- [ ] Match work group size to hardware

## Next Steps

- [OpenCL Architecture](OpenCL-Architecture.md) - Understand internal design
- [Backend Comparison](Backend-Comparison.md) - Compare with CUDA and Metal
- [Sample Projects](../../samples/OpenCL/README.md) - Performance examples
