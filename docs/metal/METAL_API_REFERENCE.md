# DotCompute Metal Backend API Reference

**Version:** v0.2.0
**Target Framework:** .NET 9.0+
**Platform:** macOS 12.0+ with Metal support

---

## Table of Contents

1. [Core APIs](#core-apis)
2. [Accelerator Management](#accelerator-management)
3. [Kernel Compilation](#kernel-compilation)
4. [Memory Management](#memory-management)
5. [Execution Engine](#execution-engine)
6. [Telemetry & Monitoring](#telemetry--monitoring)
7. [Configuration](#configuration)
8. [Error Handling](#error-handling)
9. [Advanced Features](#advanced-features)

---

## Core APIs

### MetalAccelerator

Primary interface for Metal GPU acceleration.

```csharp
public sealed class MetalAccelerator : BaseAccelerator
{
    public MetalAccelerator(
        IOptions<MetalAcceleratorOptions> options,
        ILogger<MetalAccelerator> logger,
        IOptions<MetalTelemetryOptions>? telemetryOptions = null,
        ILoggerFactory? loggerFactory = null);

    // Inherited from IAccelerator
    public AcceleratorInfo Info { get; }
    public AcceleratorType Type { get; }
    public IUnifiedMemoryManager Memory { get; }

    // Metal-specific properties
    public IntPtr Device { get; }
    public IntPtr CommandQueue { get; }
    public MetalPerformanceProfiler Profiler { get; }
}
```

**Usage Example:**
```csharp
var services = new ServiceCollection();
services.AddDotComputeMetalBackend(options =>
{
    options.PreferredDeviceIndex = 0;
    options.EnableUnifiedMemory = true;
    options.EnableProfiling = true;
});

var provider = services.BuildServiceProvider();
var accelerator = provider.GetRequiredService<IAccelerator>();

await accelerator.InitializeAsync();
Console.WriteLine($"Device: {accelerator.Info.Name}");
```

---

## Accelerator Management

### MetalAcceleratorOptions

Configuration options for the Metal accelerator.

```csharp
public class MetalAcceleratorOptions
{
    /// <summary>Metal device index (default: 0 = system default)</summary>
    public int PreferredDeviceIndex { get; set; } = 0;

    /// <summary>Enable unified memory optimization (Apple Silicon)</summary>
    public bool EnableUnifiedMemory { get; set; } = true;

    /// <summary>Enable GPU performance profiling</summary>
    public bool EnableProfiling { get; set; } = true;

    /// <summary>Enable debug markers in command buffers</summary>
    public bool EnableDebugMarkers { get; set; } = false;

    /// <summary>Kernel cache directory (default: ./metal_cache)</summary>
    public string CacheDirectory { get; set; } = "./metal_cache";

    /// <summary>Maximum cached kernels (LRU eviction)</summary>
    public int MaxCachedKernels { get; set; } = 1000;

    /// <summary>Memory pool size classes (default: 21)</summary>
    public int MemoryPoolSizeClasses { get; set; } = 21;

    /// <summary>Command queue count (default: 4)</summary>
    public int CommandQueueCount { get; set; } = 4;

    /// <summary>Enable automatic retry on transient failures</summary>
    public bool EnableAutoRetry { get; set; } = true;

    /// <summary>Maximum retry attempts (default: 3)</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Compilation optimization level (O0, O2, O3)</summary>
    public string OptimizationLevel { get; set; } = "O3";

    /// <summary>Command buffer cache size (default: 16)</summary>
    public int CommandBufferCacheSize { get; set; } = 16;
}
```

### MetalCapabilityManager

Hardware capability detection and management.

```csharp
public static class MetalCapabilityManager
{
    public static MetalCapabilities GetCapabilities(IntPtr device);
    public static bool SupportsUnifiedMemory(IntPtr device);
    public static int GetMaxThreadsPerThreadgroup(IntPtr device);
    public static long GetMaxBufferLength(IntPtr device);
}

public class MetalCapabilities
{
    public string DeviceName { get; init; }
    public string GpuFamily { get; init; }
    public int MaxThreadsPerThreadgroup { get; init; }
    public int MaxThreadgroupMemoryLength { get; init; }
    public long RecommendedMaxWorkingSetSize { get; init; }
    public bool SupportsNonUniformThreadgroups { get; init; }
    public bool SupportsRaytracing { get; init; }
}
```

**Usage Example:**
```csharp
var capabilities = MetalCapabilityManager.GetCapabilities(device);
Console.WriteLine($"GPU Family: {capabilities.GpuFamily}");
Console.WriteLine($"Max Threadgroup Size: {capabilities.MaxThreadsPerThreadgroup}");
Console.WriteLine($"Supports Raytracing: {capabilities.SupportsRaytracing}");
```

---

## Kernel Compilation

### MetalKernelCompiler

Compiles C# and MSL kernels for Metal execution.

```csharp
public sealed class MetalKernelCompiler : IUnifiedKernelCompiler
{
    public MetalKernelCompiler(
        IntPtr device,
        IntPtr commandQueue,
        ILogger logger,
        MetalCommandBufferPool? commandBufferPool = null,
        MetalKernelCache? kernelCache = null);

    // Compile kernel from definition
    public Task<ICompiledKernel> CompileAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);

    // Precompile for caching
    public Task<ValidationResult> ValidateAsync(
        string source,
        KernelLanguage language,
        CancellationToken cancellationToken = default);
}
```

### KernelDefinition

Defines a compute kernel for compilation.

```csharp
public class KernelDefinition
{
    public string Name { get; set; }
    public string Source { get; set; }
    public string EntryPoint { get; set; }
    public KernelLanguage Language { get; set; }
    public KernelParameter[] Parameters { get; set; }
    public ThreadingModel ThreadingModel { get; set; }
}

public enum KernelLanguage
{
    CSharp,     // Automatic translation to MSL
    Metal,      // Direct MSL source
    Binary      // Precompiled metallib
}

public enum ThreadingModel
{
    Linear,     // 1D grid (thread_position_in_grid.x)
    Grid2D,     // 2D grid (thread_position_in_grid.xy)
    Grid3D      // 3D grid (thread_position_in_grid.xyz)
}
```

**Usage Example:**
```csharp
var definition = new KernelDefinition
{
    Name = "vector_add",
    Source = """
        #include <metal_stdlib>
        using namespace metal;

        kernel void vector_add(
            device const float* a [[buffer(0)]],
            device const float* b [[buffer(1)]],
            device float* result [[buffer(2)]],
            uint gid [[thread_position_in_grid]])
        {
            result[gid] = a[gid] + b[gid];
        }
        """,
    EntryPoint = "vector_add",
    Language = KernelLanguage.Metal,
    ThreadingModel = ThreadingModel.Linear
};

var compiler = new MetalKernelCompiler(device, commandQueue, logger);
var compiled = await compiler.CompileAsync(definition);
```

### CompilationOptions

Options for kernel compilation.

```csharp
public class CompilationOptions
{
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Maximum;
    public bool GenerateDebugInfo { get; set; } = false;
    public bool EnableFastMath { get; set; } = true;
    public bool EnableAutoTuning { get; set; } = true;
    public Dictionary<string, string> Defines { get; set; } = new();
}

public enum OptimizationLevel
{
    None,       // -O0, fastest compilation
    Basic,      // -O1
    Default,    // -O2
    Maximum     // -O3, best performance
}
```

### CSharpToMSLTranslator

Translates C# kernel code to Metal Shading Language.

```csharp
public class CSharpToMSLTranslator
{
    public CSharpToMSLTranslator(ILogger logger);

    public string Translate(
        string csharpSource,
        string kernelName,
        KernelParameter[] parameters);

    public ValidationResult Validate(string csharpSource);
}
```

**Translation Example:**
```csharp
var translator = new CSharpToMSLTranslator(logger);

// C# kernel
string csharpKernel = """
    [Kernel]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
            result[idx] = a[idx] + b[idx];
    }
    """;

// Translate to MSL
string mslKernel = translator.Translate(csharpKernel, "VectorAdd", parameters);
```

---

## Memory Management

### MetalMemoryManager

Unified memory management with pooling and zero-copy optimization.

```csharp
public sealed class MetalMemoryManager : IUnifiedMemoryManager
{
    public MetalMemoryManager(
        IntPtr device,
        ILogger logger,
        MetalMemoryManagerOptions? options = null);

    // Allocate memory buffer
    public ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(
        int length,
        CancellationToken cancellationToken = default) where T : unmanaged;

    // Get memory statistics
    public MemoryStatistics GetStatistics();

    // Get current memory pressure level
    public MemoryPressureLevel CurrentPressureLevel { get; }
}
```

### MetalMemoryPool

Efficient memory pooling with size classes.

```csharp
public sealed class MetalMemoryPool
{
    public MetalMemoryPool(
        IntPtr device,
        ILogger logger,
        int sizeClasses = 21);

    // Get buffer from pool or allocate new
    public IUnifiedMemoryBuffer<T> GetBuffer<T>(int length) where T : unmanaged;

    // Return buffer to pool
    public void ReturnBuffer<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged;

    // Get pool statistics
    public PoolStatistics GetStatistics();
}

public class PoolStatistics
{
    public long TotalAllocations { get; }
    public long PoolHits { get; }
    public long PoolMisses { get; }
    public double HitRate => PoolHits / (double)TotalAllocations;
    public long CurrentPooledBuffers { get; }
    public long CurrentActiveBuffers { get; }
}
```

### MetalMemoryPressureMonitor

Real-time memory pressure monitoring.

```csharp
public sealed class MetalMemoryPressureMonitor
{
    public MetalMemoryPressureMonitor(IntPtr device, ILogger logger);

    public MemoryPressureLevel GetCurrentPressureLevel();
    public long GetAvailableMemory();
    public long GetUsedMemory();
    public event EventHandler<MemoryPressureEventArgs>? PressureChanged;
}

public enum MemoryPressureLevel
{
    Normal,     // < 60% usage
    Moderate,   // 60-75% usage
    High,       // 75-85% usage
    Critical,   // 85-95% usage
    Emergency   // > 95% usage
}
```

**Usage Example:**
```csharp
var monitor = new MetalMemoryPressureMonitor(device, logger);
monitor.PressureChanged += (sender, args) =>
{
    Console.WriteLine($"Memory pressure: {args.Level}");
    if (args.Level >= MemoryPressureLevel.High)
    {
        // Trigger cleanup or reduce allocations
    }
};
```

---

## Execution Engine

### MetalExecutionEngine

Command buffer lifecycle and execution management.

```csharp
public sealed class MetalExecutionEngine
{
    public MetalExecutionEngine(
        IntPtr device,
        IntPtr commandQueue,
        ILogger logger);

    // Execute kernel with automatic resource management
    public Task<ExecutionResult> ExecuteKernelAsync(
        ICompiledKernel kernel,
        MTLSize gridSize,
        MTLSize threadgroupSize,
        params object[] arguments);

    // Execute with profiling
    public Task<ProfiledExecutionResult> ExecuteWithProfilingAsync(
        ICompiledKernel kernel,
        MTLSize gridSize,
        MTLSize threadgroupSize,
        params object[] arguments);
}

public class ExecutionResult
{
    public bool Success { get; init; }
    public TimeSpan Duration { get; init; }
    public string? Error { get; init; }
}

public class ProfiledExecutionResult : ExecutionResult
{
    public long GpuTime { get; init; }
    public long CpuTime { get; init; }
    public long MemoryBandwidth { get; init; }
}
```

### MetalComputeGraph

DAG-based kernel scheduling with automatic parallelization.

```csharp
public sealed class MetalComputeGraph
{
    public MetalComputeGraph(string name, ILogger logger);

    // Add kernel node to graph
    public NodeId AddKernelNode(
        ICompiledKernel kernel,
        MTLSize gridSize,
        MTLSize threadgroupSize,
        object[] arguments,
        NodeId[]? dependencies = null);

    // Add memory copy node
    public NodeId AddMemoryCopyNode(
        IUnifiedMemoryBuffer source,
        IUnifiedMemoryBuffer destination,
        NodeId[]? dependencies = null);

    // Build and optimize graph
    public void Build();

    // Get execution order
    public List<NodeId> GetExecutionOrder();
}
```

### MetalGraphExecutor

Parallel graph execution with dependency resolution.

```csharp
public sealed class MetalGraphExecutor
{
    public MetalGraphExecutor(
        ILogger logger,
        int maxConcurrentOperations = 4);

    // Execute compute graph
    public Task<GraphExecutionResult> ExecuteAsync(
        MetalComputeGraph graph,
        IntPtr commandQueue,
        CancellationToken cancellationToken = default);
}

public class GraphExecutionResult
{
    public int NodesExecuted { get; init; }
    public double TotalExecutionTimeMs { get; init; }
    public double GpuExecutionTimeMs { get; init; }
    public Dictionary<NodeId, double> NodeTimings { get; init; }
}
```

**Usage Example:**
```csharp
var graph = new MetalComputeGraph("ml_pipeline", logger);

var preprocessNode = graph.AddKernelNode(
    preprocessKernel,
    gridSize: new MTLSize(1024, 1, 1),
    threadgroupSize: new MTLSize(256, 1, 1),
    arguments: new object[] { inputBuffer, normalizedBuffer });

var inferenceNode = graph.AddKernelNode(
    inferenceKernel,
    gridSize: new MTLSize(512, 1, 1),
    threadgroupSize: new MTLSize(128, 1, 1),
    arguments: new object[] { normalizedBuffer, outputBuffer },
    dependencies: new[] { preprocessNode });

graph.Build();

var executor = new MetalGraphExecutor(logger);
var result = await executor.ExecuteAsync(graph, commandQueue);
Console.WriteLine($"Executed {result.NodesExecuted} nodes in {result.TotalExecutionTimeMs}ms");
```

---

## Telemetry & Monitoring

### MetalTelemetryManager

Production-grade telemetry and metrics collection.

```csharp
public sealed class MetalTelemetryManager : IDisposable
{
    public MetalTelemetryManager(
        IOptions<MetalTelemetryOptions> options,
        ILogger<MetalTelemetryManager> logger,
        ILoggerFactory loggerFactory);

    // Record kernel execution
    public void RecordKernelExecution(
        string kernelName,
        double durationMs,
        long memoryUsed);

    // Record compilation metrics
    public void RecordCompilation(
        string kernelName,
        double compilationTimeMs,
        bool cacheHit);

    // Get current metrics
    public TelemetryMetrics GetCurrentMetrics();
}

public class TelemetryMetrics
{
    public double AverageKernelExecutionMs { get; }
    public double CacheHitRate { get; }
    public long TotalKernelExecutions { get; }
    public long TotalCompilations { get; }
    public Dictionary<string, KernelMetrics> PerKernelMetrics { get; }
}
```

### MetalPerformanceProfiler

Detailed performance profiling with hardware counters.

```csharp
public sealed class MetalPerformanceProfiler
{
    public MetalPerformanceProfiler(ILogger logger);

    // Start profiling session
    public void StartProfiling(string sessionName);

    // Record kernel execution
    public void RecordKernelExecution(
        string kernelName,
        double gpuTimeMs,
        double cpuTimeMs);

    // End session and get report
    public ProfilingReport EndProfiling();
}

public class ProfilingReport
{
    public string SessionName { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public Dictionary<string, KernelProfile> KernelProfiles { get; init; }
    public double TotalGpuTimeMs { get; init; }
    public double TotalCpuTimeMs { get; init; }
}
```

---

## Configuration

### Service Registration

```csharp
public static class MetalServiceCollectionExtensions
{
    // Register Metal backend
    public static IServiceCollection AddDotComputeMetalBackend(
        this IServiceCollection services,
        Action<MetalAcceleratorOptions>? configure = null);

    // Register with telemetry
    public static IServiceCollection AddDotComputeMetalBackendWithTelemetry(
        this IServiceCollection services,
        Action<MetalAcceleratorOptions>? configureAccelerator = null,
        Action<MetalTelemetryOptions>? configureTelemetry = null);
}
```

---

## Error Handling

### Metal-Specific Exceptions

```csharp
public class MetalException : Exception
{
    public MetalException(string message);
    public MetalException(string message, Exception innerException);
}

public class MetalCompilationException : MetalException
{
    public string CompilationLog { get; }
    public string SourceCode { get; }
}

public class MetalDeviceNotFoundException : MetalException
{
    public int RequestedDeviceIndex { get; }
}

public class MetalMemoryException : MetalException
{
    public long RequestedBytes { get; }
    public long AvailableBytes { get; }
}
```

### Error Recovery

```csharp
public static class MetalErrorRecovery
{
    public static RecoveryStrategy AnalyzeException(Exception exception);
    public static bool CanRecover(Exception exception);
    public static Task<bool> AttemptRecoveryAsync(Exception exception, ILogger logger);
}

public enum RecoveryStrategy
{
    Retry,              // Retry operation
    FallbackToCpu,      // Use CPU backend
    ReduceMemory,       // Reduce memory usage
    RecompileKernel,    // Recompile with different options
    Unrecoverable       // Cannot recover
}
```

---

## Advanced Features

### Metal Performance Shaders (MPS) Integration

```csharp
public static class MetalMPSIntegration
{
    // Matrix multiplication using MPS
    public static Task<IUnifiedMemoryBuffer<float>> MatrixMultiplyMPSAsync(
        IUnifiedMemoryBuffer<float> a,
        IUnifiedMemoryBuffer<float> b,
        int rowsA,
        int colsA,
        int colsB,
        IntPtr device,
        IntPtr commandQueue);

    // Convolution using MPS
    public static Task<IUnifiedMemoryBuffer<float>> ConvolutionMPSAsync(
        IUnifiedMemoryBuffer<float> input,
        IUnifiedMemoryBuffer<float> kernel,
        ConvolutionDescriptor descriptor,
        IntPtr device,
        IntPtr commandQueue);
}
```

### Command Buffer Pooling

```csharp
public sealed class MetalCommandBufferPool : IDisposable
{
    public MetalCommandBufferPool(
        IntPtr commandQueue,
        ILogger logger,
        int maxPoolSize = 16);

    // Get command buffer from pool
    public IntPtr GetCommandBuffer();

    // Return buffer to pool
    public void ReturnCommandBuffer(IntPtr commandBuffer);

    // Get pool statistics
    public CommandBufferPoolStatistics GetStatistics();
}
```

---

## Best Practices

### 1. Memory Management
```csharp
// ✅ Good: Use memory pooling
using var buffer = await memoryManager.AllocateAsync<float>(1_000_000);

// ❌ Bad: Frequent allocations without pooling
for (int i = 0; i < 1000; i++)
{
    var temp = await memoryManager.AllocateAsync<float>(1000);
    // Memory pressure increases
}
```

### 2. Kernel Compilation
```csharp
// ✅ Good: Compile once, execute many times
var compiled = await compiler.CompileAsync(definition);
for (int i = 0; i < 1000; i++)
{
    await compiled.ExecuteAsync(bufferA, bufferB, result);
}

// ❌ Bad: Recompile every iteration
for (int i = 0; i < 1000; i++)
{
    var compiled = await compiler.CompileAsync(definition);
    await compiled.ExecuteAsync(bufferA, bufferB, result);
}
```

### 3. Error Handling
```csharp
// ✅ Good: Handle Metal-specific errors
try
{
    await accelerator.ExecuteAsync(kernel, args);
}
catch (MetalMemoryException ex)
{
    logger.LogWarning("Out of GPU memory, falling back to CPU");
    await cpuAccelerator.ExecuteAsync(kernel, args);
}
```

---

## Performance Tips

1. **Enable Unified Memory** on Apple Silicon for zero-copy transfers
2. **Use Memory Pooling** to reduce allocation overhead by 90%+
3. **Enable Kernel Caching** for >95% cache hit rates
4. **Batch Operations** using compute graphs for parallel execution
5. **Profile Regularly** using MetalPerformanceProfiler
6. **Monitor Memory Pressure** to avoid OOM conditions
7. **Use MPS Integration** for standard operations (3-4x speedup)

---

## See Also

- [Metal Backend README](/src/Backends/DotCompute.Backends.Metal/README.md)
- [Getting Started Guide](/docs/metal/GETTING_STARTED_METAL.md)
- [Architecture Overview](/docs/ARCHITECTURE.md)
- [Troubleshooting Guide](/docs/metal/TROUBLESHOOTING.md)
- [Apple Metal Documentation](https://developer.apple.com/documentation/metal)

---

**Last Updated:** December 2025
**API Version:** v0.2.0
**License:** MIT
