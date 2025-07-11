# DotCompute API Overview

DotCompute provides a comprehensive API for high-performance GPU and accelerator programming in .NET 9+. This document provides an overview of the core APIs and their usage patterns.

## Core Namespaces

### DotCompute.Core
The main namespace containing core interfaces and services.

- `IAccelerator` - Represents a compute accelerator device
- `IComputeService` - Main service for executing kernels
- `IMemoryManager` - Manages device memory allocation
- `IKernel` - Represents a compute kernel
- `KernelContext` - Execution context within kernels

### DotCompute.Memory
Unified memory management across CPU and GPU.

- `UnifiedBuffer<T>` - Cross-device memory buffer
- `IMemoryPool` - Memory pooling for performance
- `MemoryOptions` - Configuration for memory behavior

### DotCompute.Runtime
Runtime services and execution management.

- `IAcceleratorPool` - Manages multiple accelerators
- `IKernelCompiler` - Compiles kernels at runtime
- `IExecutionScheduler` - Schedules kernel execution

## Basic Usage

### 1. Service Configuration

```csharp
// In Program.cs or Startup
builder.Services.AddDotCompute(options =>
{
    options.DefaultAccelerator = AcceleratorType.Auto;
    options.EnableMemoryPooling = true;
    options.PoolSize = 1024 * 1024 * 512; // 512MB
});
```

### 2. Kernel Definition

```csharp
[Kernel]
public static void VectorAdd(
    KernelContext ctx,
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result)
{
    var i = ctx.GlobalId.X;
    if (i < result.Length)
        result[i] = a[i] + b[i];
}
```

### 3. Kernel Execution

```csharp
public class ComputeExample
{
    private readonly IComputeService _compute;
    
    public ComputeExample(IComputeService compute)
    {
        _compute = compute;
    }
    
    public async Task<float[]> AddVectorsAsync(float[] a, float[] b)
    {
        var result = new float[a.Length];
        
        await _compute.RunAsync("VectorAdd", new
        {
            a = a,
            b = b,
            result = result,
            length = a.Length
        });
        
        return result;
    }
}
```

## Advanced Features

### Memory Management

```csharp
// Allocate unified memory
var buffer = _compute.Memory.Allocate<float>(1_000_000);

// CPU write
buffer.Span[0] = 42.0f;

// GPU kernel execution - automatic migration
await _compute.RunAsync("ProcessData", new { data = buffer });

// CPU read - automatic migration back
var result = buffer.Span[0];

// Manual memory control
buffer.PrefetchToDevice();
buffer.PrefetchToHost();
```

### Kernel Compilation

```csharp
// Compile kernel with specific options
var kernel = await _compute.CompileKernelAsync<VectorAddKernel>(
    new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Maximum,
        TargetArchitecture = "sm_80", // CUDA 8.0
        EnableFastMath = true
    });

// Execute compiled kernel
await kernel.RunAsync(new { a, b, result });
```

### LINQ Integration

```csharp
// GPU-accelerated LINQ
var results = await data
    .AsComputeQueryable()
    .Where(x => x.Value > threshold)
    .Select(x => new 
    { 
        x.Id, 
        Normalized = x.Value / max 
    })
    .OrderByDescending(x => x.Normalized)
    .Take(100)
    .ToArrayAsync();
```

### Error Handling

```csharp
try
{
    await _compute.RunAsync("MyKernel", parameters);
}
catch (KernelCompilationException ex)
{
    // Handle compilation errors
    _logger.LogError(ex, "Kernel compilation failed");
}
catch (OutOfMemoryException ex)
{
    // Handle GPU memory exhaustion
    _logger.LogError(ex, "GPU out of memory");
}
catch (AcceleratorException ex)
{
    // Handle general accelerator errors
    _logger.LogError(ex, "Accelerator error: {Message}", ex.Message);
}
```

## Performance Considerations

### 1. Memory Pooling
Enable memory pooling to reduce allocation overhead:

```csharp
services.AddDotCompute(options =>
{
    options.EnableMemoryPooling = true;
    options.PoolSize = 1024 * 1024 * 1024; // 1GB
    options.PoolRetentionTime = TimeSpan.FromMinutes(5);
});
```

### 2. Kernel Caching
Compiled kernels are automatically cached. Control caching behavior:

```csharp
services.AddDotCompute(options =>
{
    options.KernelCacheSize = 100;
    options.KernelCacheExpiration = TimeSpan.FromHours(1);
});
```

### 3. Batch Operations
Process data in batches for optimal throughput:

```csharp
const int batchSize = 1_000_000;
var batches = data.Chunk(batchSize);

await Parallel.ForEachAsync(batches, async (batch, ct) =>
{
    await _compute.RunAsync("ProcessBatch", new { data = batch });
});
```

## Monitoring and Diagnostics

### OpenTelemetry Integration

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddDotComputeInstrumentation()
        .AddPrometheusExporter()
    )
    .WithTracing(tracing => tracing
        .AddDotComputeInstrumentation()
        .AddJaegerExporter()
    );
```

### Built-in Metrics
- `dotcompute.kernel.duration` - Kernel execution time
- `dotcompute.memory.allocated` - Memory allocation
- `dotcompute.device.utilization` - GPU utilization
- `dotcompute.kernel.throughput` - Data throughput

### Diagnostic Logging

```csharp
services.AddDotCompute(options =>
{
    options.EnableDiagnostics = true;
    options.DiagnosticLevel = DiagnosticLevel.Verbose;
    options.LogKernelCompilation = true;
    options.LogMemoryTransfers = true;
});
```

## Backend-Specific APIs

### CUDA Backend

```csharp
services.AddAccelerator<CudaPlugin>(cuda =>
{
    cuda.DeviceId = 0;
    cuda.EnablePeerAccess = true;
    cuda.StreamCount = 4;
    cuda.EnableTensorCores = true;
});

// CUDA-specific features
var cudaAccelerator = accelerator as ICudaAccelerator;
cudaAccelerator.SetSharedMemoryConfig(SharedMemoryConfig.PreferL1);
```

### Metal Backend

```csharp
services.AddAccelerator<MetalPlugin>(metal =>
{
    metal.PreferredDevice = MetalDeviceType.Discrete;
    metal.EnableRaytracing = true;
});

// Metal-specific features
var metalAccelerator = accelerator as IMetalAccelerator;
metalAccelerator.CreateComputePipelineState(function);
```

## Next Steps

- [Getting Started Guide](../getting-started/quickstart.md)
- [Architecture Overview](../architecture/overview.md)
- [Kernel Programming Guide](kernel-programming.md)
- [Memory Management Guide](memory-management.md)
- [Performance Tuning](../performance/tuning.md)