# API Reference

Complete API documentation for DotCompute framework components.

## 📚 Core Interfaces

### IComputeService

Main entry point for kernel execution and accelerator management.

```csharp
public interface IComputeService : IAsyncDisposable
{
    // Accelerator Management
    ValueTask<IAccelerator> GetAcceleratorAsync(string? backendName = null);
    ValueTask<IReadOnlyList<IAccelerator>> GetAvailableAcceleratorsAsync();
    
    // Kernel Execution
    ValueTask ExecuteAsync<T>(string kernelName, T parameters, 
        CompilationOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask<TResult> ExecuteAsync<TParameters, TResult>(string kernelName, 
        TParameters parameters, CancellationToken cancellationToken = default);
    
    // Kernel Management
    ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition,
        CompilationOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask<bool> IsKernelRegisteredAsync(string kernelName);
}
```

#### Usage Examples

```csharp
// Get compute service from DI
var compute = serviceProvider.GetRequiredService<IComputeService>();

// Simple kernel execution
await compute.ExecuteAsync("VectorAdd", new 
{ 
    a = inputA.AsSpan(),
    b = inputB.AsSpan(),
    result = output.AsSpan()
});

// Get specific accelerator
var cpuAccelerator = await compute.GetAcceleratorAsync("CPU");
var cudaAccelerator = await compute.GetAcceleratorAsync("CUDA");

// List all available accelerators
var accelerators = await compute.GetAvailableAcceleratorsAsync();
foreach (var accelerator in accelerators)
{
    Console.WriteLine($"Available: {accelerator.Name} ({accelerator.Type})");
}
```

### IAccelerator

Represents a compute accelerator (CPU, GPU, etc.).

```csharp
public interface IAccelerator : IAsyncDisposable
{
    string Name { get; }
    AcceleratorType Type { get; }
    AcceleratorInfo Info { get; }
    
    // Memory Management
    ValueTask<IMemoryBuffer<T>> AllocateAsync<T>(int size) where T : unmanaged;
    ValueTask<IUnifiedBuffer<T>> CreateUnifiedBufferAsync<T>(T[] data) where T : unmanaged;
    
    // Kernel Execution
    ValueTask<IKernel> LoadKernelAsync(string kernelName);
    ValueTask ExecuteKernelAsync(IKernel kernel, object parameters);
    
    // Context Management
    IAcceleratorContext CreateContext();
    ValueTask SynchronizeAsync();
}
```

#### Usage Examples

```csharp
// Allocate memory on accelerator
var buffer = await accelerator.AllocateAsync<float>(1024);

// Create unified buffer (automatic synchronization)
var unifiedBuffer = await accelerator.CreateUnifiedBufferAsync(inputData);

// Load and execute kernel
var kernel = await accelerator.LoadKernelAsync("MatrixMultiply");
await accelerator.ExecuteKernelAsync(kernel, new { A = matrixA, B = matrixB, C = result });

// Synchronize all operations
await accelerator.SynchronizeAsync();
```

### IMemoryBuffer<T>

Basic memory buffer interface for unmanaged types.

```csharp
public interface IMemoryBuffer<T> : IDisposable where T : unmanaged
{
    int Length { get; }
    Memory<T> AsMemory();
    Span<T> AsSpan();
    
    ValueTask CopyFromAsync(ReadOnlyMemory<T> source);
    ValueTask CopyToAsync(Memory<T> destination);
}
```

### IUnifiedBuffer<T>

Advanced memory buffer with automatic device synchronization.

```csharp
public interface IUnifiedBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    // Device Management
    ValueTask TransferToAsync(IAccelerator accelerator);
    ValueTask SynchronizeAsync();
    bool IsValidOn(IAccelerator accelerator);
    
    // Advanced Operations
    IUnifiedBuffer<T> Slice(int start, int length);
    ValueTask<IUnifiedBuffer<T>> CloneAsync();
    
    // Performance Tracking
    MemoryStatistics GetStatistics();
}
```

#### Usage Examples

```csharp
// Create unified buffer
var buffer = await accelerator.CreateUnifiedBufferAsync(data);

// Automatically transfers to GPU when needed
await compute.ExecuteAsync("GpuKernel", new { input = buffer });

// Automatically syncs back to CPU when accessed
var result = buffer.AsSpan(); // Data is now on CPU

// Manual synchronization
await buffer.SynchronizeAsync();

// Performance monitoring
var stats = buffer.GetStatistics();
Console.WriteLine($"Transfers: {stats.TransferCount}, Size: {stats.TotalBytes}");
```

## 🔧 Kernel Definition

### Kernel Attribute

Mark methods as compute kernels.

```csharp
[Kernel("KernelName")]
public static void MyKernel(
    KernelContext ctx,
    ReadOnlySpan<float> input,
    Span<float> output)
{
    var i = ctx.GlobalId.X;
    if (i < output.Length)
        output[i] = input[i] * 2.0f;
}
```

### KernelContext

Provides kernel execution context and thread information.

```csharp
public struct KernelContext
{
    public Dim3 GlobalId { get; }      // Global thread ID
    public Dim3 LocalId { get; }       // Local thread ID within group
    public Dim3 GroupId { get; }       // Work group ID
    public Dim3 GroupSize { get; }     // Work group size
    public Dim3 GlobalSize { get; }    // Total number of threads
    
    // Synchronization (GPU backends)
    public void Barrier();
    public void GroupBarrier();
}
```

### Compilation Options

Configure kernel compilation behavior.

```csharp
public class CompilationOptions
{
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;
    public bool EnableDebugging { get; set; } = false;
    public IReadOnlyDictionary<string, object>? Defines { get; set; }
    public string[]? IncludePaths { get; set; }
    public TargetBackend? PreferredBackend { get; set; }
}
```

## 📦 Service Registration

### Basic Setup

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Core DotCompute services
    services.AddDotCompute();
    
    // Add CPU backend (always available)
    services.AddCpuBackend();
    
    // Add GPU backends (requires hardware)
    services.AddCudaBackend();
    services.AddMetalBackend();  // macOS only
    
    // Add algorithm libraries
    services.AddDotComputeAlgorithms();
    
    // Add LINQ support
    services.AddDotComputeLinq();
}
```

### Advanced Configuration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddDotCompute(options =>
    {
        // Memory pool configuration
        options.Memory.PoolSize = 1_000_000_000; // 1GB pool
        options.Memory.EnableNUMA = true;
        
        // Performance tuning
        options.Performance.EnableProfiling = true;
        options.Performance.OptimizationLevel = OptimizationLevel.Aggressive;
        
        // Security settings
        options.Security.EnableKernelValidation = true;
        options.Security.RequireSignedPlugins = false; // Development only
    });
    
    // Backend-specific configuration
    services.AddCudaBackend(cudaOptions =>
    {
        cudaOptions.EnableP2P = true;
        cudaOptions.MemoryPoolSize = 2_000_000_000; // 2GB GPU pool
        cudaOptions.PreferredComputeCapability = new Version(8, 0); // RTX 30xx+
    });
}
```

## 🧮 Algorithm Library

### Linear Algebra

```csharp
public interface ILinearAlgebra
{
    // BLAS Level 1 (Vector operations)
    ValueTask<T> DotAsync<T>(IUnifiedBuffer<T> x, IUnifiedBuffer<T> y) where T : unmanaged;
    ValueTask ScaleAsync<T>(IUnifiedBuffer<T> vector, T scalar) where T : unmanaged;
    ValueTask AddAsync<T>(IUnifiedBuffer<T> x, IUnifiedBuffer<T> y, IUnifiedBuffer<T> result) where T : unmanaged;
    
    // BLAS Level 2 (Matrix-Vector operations)  
    ValueTask MatrixVectorMultiplyAsync<T>(
        IUnifiedBuffer<T> matrix, int rows, int cols,
        IUnifiedBuffer<T> vector,
        IUnifiedBuffer<T> result) where T : unmanaged;
    
    // BLAS Level 3 (Matrix-Matrix operations)
    ValueTask MatrixMultiplyAsync<T>(
        IUnifiedBuffer<T> a, int rowsA, int colsA,
        IUnifiedBuffer<T> b, int rowsB, int colsB,
        IUnifiedBuffer<T> c) where T : unmanaged;
}
```

#### Usage Examples

```csharp
var algebra = serviceProvider.GetRequiredService<ILinearAlgebra>();

// Vector operations
var a = await accelerator.CreateUnifiedBufferAsync(vectorA);
var b = await accelerator.CreateUnifiedBufferAsync(vectorB);
var result = await accelerator.AllocateAsync<float>(vectorA.Length);

await algebra.AddAsync(a, b, result);

// Matrix multiplication
var matA = await accelerator.CreateUnifiedBufferAsync(matrixA);
var matB = await accelerator.CreateUnifiedBufferAsync(matrixB);
var matC = await accelerator.AllocateAsync<float>(rowsA * colsB);

await algebra.MatrixMultiplyAsync(matA, rowsA, colsA, matB, rowsB, colsB, matC);
```

## 🔗 LINQ Provider

### GPU LINQ Operations

```csharp
// Extension methods for GPU acceleration
public static class GpuLinqExtensions
{
    public static IGpuQueryable<T> AsGpuQueryable<T>(this T[] array);
    public static IGpuQueryable<T> AsGpuQueryable<T>(this IUnifiedBuffer<T> buffer);
    
    public static IGpuQueryable<TResult> Select<T, TResult>(
        this IGpuQueryable<T> source, 
        Expression<Func<T, TResult>> selector);
    
    public static IGpuQueryable<T> Where<T>(
        this IGpuQueryable<T> source,
        Expression<Func<T, bool>> predicate);
    
    public static ValueTask<TResult> AggregateAsync<T, TResult>(
        this IGpuQueryable<T> source,
        Expression<Func<T, T, T>> aggregator);
}
```

#### Usage Examples

```csharp
var data = Enumerable.Range(0, 1_000_000).ToArray();

// GPU-accelerated LINQ operations
var result = await data.AsGpuQueryable()
    .Where(x => x % 2 == 0)           // Filter even numbers
    .Select(x => x * x)               // Square each number
    .AggregateAsync((a, b) => a + b); // Sum all values

// Complex operations with multiple steps
var processedData = await inputBuffer.AsGpuQueryable()
    .Select(x => Math.Sin(x) * Math.Cos(x))
    .Where(x => x > 0.5f)
    .ToArrayAsync();
```

## 🔌 Plugin System

### Creating Custom Backends

```csharp
public class MyCustomBackend : BackendPluginBase
{
    public override string Name => "MyCustom";
    public override AcceleratorType SupportedType => AcceleratorType.Custom;
    public override bool IsAvailable => CheckHardwareAvailability();
    
    public override ValueTask<IAccelerator> CreateAcceleratorAsync()
    {
        return new ValueTask<IAccelerator>(new MyCustomAccelerator());
    }
    
    protected override ValueTask<IKernelCompiler> CreateCompilerAsync()
    {
        return new ValueTask<IKernelCompiler>(new MyCustomCompiler());
    }
    
    private bool CheckHardwareAvailability()
    {
        // Custom hardware detection logic
        return true;
    }
}
```

### Plugin Registration

```csharp
// Register plugin at startup
services.AddSingleton<IBackendPlugin, MyCustomBackend>();

// Or load plugins from assemblies
services.AddDotCompute(options =>
{
    options.Plugins.LoadFromDirectory("./plugins");
    options.Plugins.EnableHotReload = true;
});
```

## 📊 Performance Monitoring

### PerformanceProfiler

```csharp
public interface IPerformanceProfiler
{
    ValueTask<ExecutionMetrics> ProfileKernelAsync(
        string kernelName, 
        object parameters,
        int iterations = 100);
    
    ValueTask<MemoryMetrics> ProfileMemoryAsync(
        Func<ValueTask> operation,
        int iterations = 10);
    
    ValueTask<BenchmarkResults> RunBenchmarkAsync(
        BenchmarkDefinition benchmark);
}
```

#### Usage Examples

```csharp
var profiler = serviceProvider.GetRequiredService<IPerformanceProfiler>();

// Profile kernel execution
var metrics = await profiler.ProfileKernelAsync("MatrixMultiply", 
    new { A = matrixA, B = matrixB, C = result });

Console.WriteLine($"Average execution time: {metrics.AverageTime:F2}ms");
Console.WriteLine($"Memory bandwidth: {metrics.MemoryBandwidth:F1} GB/s");
Console.WriteLine($"FLOPS: {metrics.FLOPS:E2}");

// Profile memory operations
var memMetrics = await profiler.ProfileMemoryAsync(async () =>
{
    await buffer.TransferToAsync(gpuAccelerator);
});

Console.WriteLine($"Transfer time: {memMetrics.TransferTime:F2}ms");
Console.WriteLine($"Bandwidth: {memMetrics.Bandwidth:F1} GB/s");
```

## 🔒 Security Features

### Kernel Validation

```csharp
public interface IKernelValidator
{
    ValueTask<ValidationResult> ValidateAsync(string kernelSource);
    ValueTask<SecurityReport> ScanForVulnerabilitiesAsync(string kernelSource);
    bool IsKernelTrusted(string kernelName);
}
```

#### Usage Examples

```csharp
var validator = serviceProvider.GetRequiredService<IKernelValidator>();

// Validate kernel before compilation
var result = await validator.ValidateAsync(kernelSource);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Security Error: {error.Message}");
    }
    return;
}

// Security scan
var report = await validator.ScanForVulnerabilitiesAsync(kernelSource);
if (report.HasVulnerabilities)
{
    Console.WriteLine($"Found {report.Vulnerabilities.Count} security issues");
}
```

## 🏗️ Advanced Features

### Pipeline Operations

```csharp
public interface IKernelPipeline : IDisposable
{
    IKernelPipeline AddStage(string kernelName, object parameters);
    IKernelPipeline AddStage<T>(Expression<Action<T>> kernelExpression, T parameters);
    ValueTask ExecuteAsync();
    ValueTask<T> ExecuteAsync<T>();
}
```

#### Usage Examples

```csharp
// Create processing pipeline
var pipeline = accelerator.CreatePipeline()
    .AddStage("Preprocess", new { input = rawData, output = preprocessed })
    .AddStage("Transform", new { input = preprocessed, output = transformed })
    .AddStage("Postprocess", new { input = transformed, output = final });

await pipeline.ExecuteAsync();
```

### Multi-GPU Coordination

```csharp
public interface IMultiGpuManager
{
    ValueTask<IReadOnlyList<IAccelerator>> GetGpuAcceleratorsAsync();
    ValueTask EnableP2PAsync(IAccelerator source, IAccelerator target);
    ValueTask<IDistributedBuffer<T>> CreateDistributedBufferAsync<T>(
        int totalSize, 
        IReadOnlyList<IAccelerator> accelerators) where T : unmanaged;
}
```

#### Usage Examples

```csharp
var multiGpu = serviceProvider.GetRequiredService<IMultiGpuManager>();
var gpus = await multiGpu.GetGpuAcceleratorsAsync();

// Enable P2P between GPUs
for (int i = 0; i < gpus.Count; i++)
{
    for (int j = i + 1; j < gpus.Count; j++)
    {
        await multiGpu.EnableP2PAsync(gpus[i], gpus[j]);
    }
}

// Create distributed buffer across GPUs
var distributedBuffer = await multiGpu.CreateDistributedBufferAsync<float>(
    1_000_000, gpus);

// Execute on all GPUs in parallel
await Parallel.ForEachAsync(gpus, async (gpu, ct) =>
{
    await gpu.ExecuteKernelAsync("ProcessChunk", 
        new { data = distributedBuffer.GetChunk(gpu) });
});
```

## 🔧 Error Handling

### Exception Types

```csharp
public class ComputeException : Exception
{
    public string? KernelName { get; }
    public AcceleratorInfo? AcceleratorInfo { get; }
}

public class CompilationException : ComputeException
{
    public IReadOnlyList<CompilerError> CompilerErrors { get; }
}

public class MemoryException : ComputeException
{
    public long RequestedSize { get; }
    public long AvailableSize { get; }
}

public class AcceleratorException : ComputeException
{
    public int ErrorCode { get; }
    public string? DriverMessage { get; }
}
```

### Error Recovery

```csharp
try
{
    await compute.ExecuteAsync("MyKernel", parameters);
}
catch (CompilationException ex)
{
    // Handle compilation errors
    Console.WriteLine($"Kernel compilation failed: {ex.Message}");
    foreach (var error in ex.CompilerErrors)
    {
        Console.WriteLine($"  Line {error.Line}: {error.Message}");
    }
}
catch (MemoryException ex)
{
    // Handle memory allocation failures
    Console.WriteLine($"Out of memory: requested {ex.RequestedSize}, available {ex.AvailableSize}");
    
    // Fallback to CPU execution
    await compute.ExecuteAsync("MyKernel", parameters, 
        new CompilationOptions { PreferredBackend = TargetBackend.CPU });
}
catch (AcceleratorException ex)
{
    // Handle hardware errors
    Console.WriteLine($"Hardware error {ex.ErrorCode}: {ex.DriverMessage}");
    
    // Try different accelerator
    var fallbackAccelerator = await compute.GetAcceleratorAsync("CPU");
    // ... retry with fallback
}
```

---

This API reference provides comprehensive documentation for all major DotCompute components. For additional examples and advanced usage patterns, see the [Getting Started Guide](GETTING_STARTED.md) and [Performance Guide](PERFORMANCE.md).