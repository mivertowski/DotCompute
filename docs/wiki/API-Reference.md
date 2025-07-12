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

// Compile kernel with options
var compiledKernel = await compute.CompileKernelAsync(kernelDef, new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.Aggressive,
    EnableVectorization = true
});
```

### IAccelerator

Represents a compute device (CPU, GPU, etc.) with compilation and execution capabilities.

```csharp
public interface IAccelerator : IAsyncDisposable
{
    // Properties
    AcceleratorInfo Info { get; }
    IMemoryManager Memory { get; }
    
    // Kernel Operations
    ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition,
        CompilationOptions? options = null, CancellationToken cancellationToken = default);
    
    // Device Management
    ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
    ValueTask<AcceleratorMetrics> GetMetricsAsync();
}
```

#### Usage Examples

```csharp
// Access accelerator properties
Console.WriteLine($"Device: {accelerator.Info.Name}");
Console.WriteLine($"Memory: {accelerator.Info.TotalMemory / (1024*1024)} MB");
Console.WriteLine($"Cores: {accelerator.Info.ComputeUnits}");

// Allocate memory
var buffer = await accelerator.Memory.AllocateAsync<float>(1000);

// Synchronize operations
await accelerator.SynchronizeAsync();

// Get performance metrics
var metrics = await accelerator.GetMetricsAsync();
Console.WriteLine($"Utilization: {metrics.Utilization:P}");
```

### IMemoryManager

Manages memory allocation and buffer operations for compute devices.

```csharp
public interface IMemoryManager
{
    // Buffer Allocation
    ValueTask<IMemoryBuffer<T>> AllocateAsync<T>(long count, 
        MemoryFlags flags = MemoryFlags.ReadWrite) where T : unmanaged;
    ValueTask<IMemoryBuffer> AllocateAsync(Type elementType, long count,
        MemoryFlags flags = MemoryFlags.ReadWrite);
    
    // Memory Information
    ValueTask<MemoryInfo> GetMemoryInfoAsync();
    ValueTask<long> GetAvailableMemoryAsync();
    
    // Buffer Operations
    ValueTask CopyAsync<T>(ReadOnlySpan<T> source, IMemoryBuffer<T> destination)
        where T : unmanaged;
    ValueTask CopyAsync<T>(IMemoryBuffer<T> source, Span<T> destination)
        where T : unmanaged;
}
```

#### Usage Examples

```csharp
// Allocate typed buffer
var floatBuffer = await memory.AllocateAsync<float>(1000);

// Allocate with specific flags
var readOnlyBuffer = await memory.AllocateAsync<int>(500, MemoryFlags.ReadOnly);

// Copy data to device
await memory.CopyAsync(hostData.AsSpan(), deviceBuffer);

// Copy data from device
await memory.CopyAsync(deviceBuffer, hostResults.AsSpan());

// Check memory status
var memInfo = await memory.GetMemoryInfoAsync();
Console.WriteLine($"Available: {memInfo.Available / (1024*1024)} MB");
```

## 🔧 Memory System

### IMemoryBuffer&lt;T&gt;

Represents a typed memory buffer with unified host/device access.

```csharp
public interface IMemoryBuffer<T> : IMemoryBuffer, IDisposable where T : unmanaged
{
    // Properties
    long Count { get; }
    long SizeInBytes { get; }
    MemoryFlags Flags { get; }
    BufferState State { get; }
    
    // Data Access
    ValueTask<ReadOnlyMemory<T>> GetReadOnlyMemoryAsync();
    ValueTask<Memory<T>> GetMemoryAsync();
    Span<T> AsSpan();
    ReadOnlySpan<T> AsReadOnlySpan();
    
    // Buffer Operations
    IMemoryBuffer<T> CreateView(long offset, long count);
    ValueTask CopyToAsync<TOther>(IMemoryBuffer<TOther> destination) 
        where TOther : unmanaged;
    ValueTask FillAsync(T value);
    
    // State Management
    ValueTask EnsureHostAsync();
    ValueTask EnsureDeviceAsync();
    ValueTask SynchronizeAsync();
}
```

#### Usage Examples

```csharp
// Create buffer and fill with data
var buffer = await memory.AllocateAsync<float>(1000);
await buffer.FillAsync(1.0f);

// Access as span for direct manipulation
var span = buffer.AsSpan();
for (int i = 0; i < span.Length; i++)
    span[i] = i * 0.1f;

// Create view for sub-range operations
var firstHalf = buffer.CreateView(0, 500);
var secondHalf = buffer.CreateView(500, 500);

// Ensure data is on device before kernel execution
await buffer.EnsureDeviceAsync();

// Copy between buffers
await buffer.CopyToAsync(anotherBuffer);
```

### UnifiedBuffer&lt;T&gt;

Implementation of unified memory buffer with lazy transfer optimization.

```csharp
public sealed class UnifiedBuffer<T> : IMemoryBuffer<T> where T : unmanaged
{
    // Constructor
    public UnifiedBuffer(long count, MemoryFlags flags, IMemoryAllocator allocator);
    
    // State Machine
    public BufferState State { get; private set; }
    
    // Transfer Operations
    public ValueTask TransferToDeviceAsync();
    public ValueTask TransferToHostAsync();
    public ValueTask SynchronizeAsync();
    
    // Performance Monitoring
    public long TransferCount { get; }
    public TimeSpan TotalTransferTime { get; }
}
```

#### Buffer State Machine

```csharp
public enum BufferState
{
    Uninitialized,  // No data allocated
    HostOnly,       // Data only on host
    DeviceOnly,     // Data only on device
    Synchronized,   // Host and device in sync
    HostDirty,      // Host has newer data
    DeviceDirty     // Device has newer data
}
```

## 🎯 Kernel System

### KernelDefinition

Defines a compute kernel with metadata and compilation information.

```csharp
public sealed class KernelDefinition
{
    // Properties
    public string Name { get; init; }
    public MethodInfo Method { get; init; }
    public KernelParameters Parameters { get; init; }
    public KernelMetadata Metadata { get; init; }
    
    // Compilation
    public CompiledKernelSource? CompiledSource { get; set; }
    public CompilationOptions? DefaultOptions { get; init; }
}
```

### ICompiledKernel

Represents a compiled kernel ready for execution.

```csharp
public interface ICompiledKernel : IAsyncDisposable
{
    // Properties
    KernelDefinition Definition { get; }
    AcceleratorInfo TargetDevice { get; }
    CompilationOptions Options { get; }
    
    // Execution
    ValueTask ExecuteAsync(object[] arguments, ExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
    ValueTask ExecuteAsync(KernelExecutionContext context,
        CancellationToken cancellationToken = default);
    
    // Metadata
    ValueTask<KernelMetrics> GetMetricsAsync();
    ValueTask<string> GetDisassemblyAsync();
}
```

#### Usage Examples

```csharp
// Compile kernel
var compiledKernel = await accelerator.CompileKernelAsync(kernelDef);

// Execute with arguments
await compiledKernel.ExecuteAsync(new object[] { inputBuffer, outputBuffer });

// Get performance metrics
var metrics = await compiledKernel.GetMetricsAsync();
Console.WriteLine($"Executions: {metrics.ExecutionCount}");
Console.WriteLine($"Average time: {metrics.AverageExecutionTime}ms");

// View generated assembly (for debugging)
var assembly = await compiledKernel.GetDisassemblyAsync();
Console.WriteLine(assembly);
```

## 🧮 Kernel Attributes

### KernelAttribute

Marks a method as a compute kernel.

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class KernelAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }
    public KernelType Type { get; set; } = KernelType.Compute;
    public int? PreferredWorkGroupSize { get; set; }
    
    public KernelAttribute(string name) => Name = name;
}
```

#### Usage Examples

```csharp
// Basic kernel
[Kernel("VectorAdd")]
public static void VectorAdd(KernelContext ctx, 
    ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    var i = ctx.GlobalId.X;
    if (i < result.Length)
        result[i] = a[i] + b[i];
}

// Kernel with metadata
[Kernel("MatrixMultiply", Description = "Optimized matrix multiplication")]
public static void MatrixMultiply(KernelContext ctx,
    ReadOnlySpan2D<float> a, ReadOnlySpan2D<float> b, Span2D<float> result)
{
    // Implementation...
}

// Kernel with work group hint
[Kernel("ParticleUpdate", PreferredWorkGroupSize = 256)]
public static void ParticleUpdate(KernelContext ctx, Span<Particle> particles)
{
    // Implementation...
}
```

### KernelContext

Provides execution context information within kernels.

```csharp
public readonly struct KernelContext
{
    // Global identifiers
    public uint3 GlobalId { get; }
    public uint3 GlobalSize { get; }
    
    // Local identifiers (work group)
    public uint3 LocalId { get; }
    public uint3 LocalSize { get; }
    
    // Group identifiers
    public uint3 GroupId { get; }
    public uint3 GroupSize { get; }
    
    // Utility properties
    public uint GlobalIndex { get; }
    public uint LocalIndex { get; }
    public uint GroupIndex { get; }
}
```

#### Usage Examples

```csharp
[Kernel("ImageProcessing")]
public static void ImageProcessing(KernelContext ctx, 
    ReadOnlySpan2D<byte> input, Span2D<byte> output)
{
    var x = ctx.GlobalId.X; // Column
    var y = ctx.GlobalId.Y; // Row
    
    if (x >= input.Width || y >= input.Height)
        return;
    
    // Process pixel at (x, y)
    output[y, x] = (byte)(input[y, x] * 0.8f);
}

[Kernel("ReductionSum")]
public static void ReductionSum(KernelContext ctx,
    ReadOnlySpan<float> input, Span<float> output)
{
    var localId = ctx.LocalId.X;
    var groupId = ctx.GroupId.X;
    var localSize = ctx.LocalSize.X;
    
    // Parallel reduction within work group
    // Implementation details...
}
```

## 🔌 Backend Configuration

### CPU Backend

Configure CPU-specific acceleration options.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCpuBackend(
        this IServiceCollection services,
        Action<CpuBackendOptions>? configure = null)
    {
        // Implementation...
    }
}

public class CpuBackendOptions
{
    // Threading
    public int ThreadCount { get; set; } = Environment.ProcessorCount;
    public bool EnableWorkStealing { get; set; } = true;
    public bool EnableThreadAffinity { get; set; } = false;
    
    // Vectorization
    public bool EnableVectorization { get; set; } = true;
    public SimdInstructionSet PreferredInstructionSet { get; set; } = SimdInstructionSet.Auto;
    
    // Memory
    public long MemoryPoolSize { get; set; } = 1024 * 1024 * 1024; // 1GB
    public bool EnableMemoryPooling { get; set; } = true;
    
    // Performance
    public bool EnablePerformanceCounters { get; set; } = false;
    public bool TrackMemoryUsage { get; set; } = false;
}
```

#### Usage Examples

```csharp
// Basic CPU backend
services.AddCpuBackend();

// Optimized for high-performance workloads
services.AddCpuBackend(options =>
{
    options.ThreadCount = Environment.ProcessorCount;
    options.EnableVectorization = true;
    options.EnableWorkStealing = true;
    options.EnableThreadAffinity = true;
    options.PreferredInstructionSet = SimdInstructionSet.AVX2;
    options.MemoryPoolSize = 2L * 1024 * 1024 * 1024; // 2GB
});

// Development configuration with monitoring
services.AddCpuBackend(options =>
{
    options.EnablePerformanceCounters = true;
    options.TrackMemoryUsage = true;
});
```

## 📊 Performance Monitoring

### AcceleratorMetrics

Performance metrics for compute devices.

```csharp
public class AcceleratorMetrics
{
    // Execution Statistics
    public long KernelExecutions { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    
    // Resource Utilization
    public double CpuUtilization { get; set; }
    public double MemoryUtilization { get; set; }
    public long MemoryBandwidth { get; set; } // bytes/sec
    
    // Throughput
    public double OperationsPerSecond { get; set; }
    public double MemoryTransfersPerSecond { get; set; }
    
    // Error Statistics
    public long ErrorCount { get; set; }
    public string? LastError { get; set; }
}
```

### KernelMetrics

Performance metrics for specific kernels.

```csharp
public class KernelMetrics
{
    public string KernelName { get; set; }
    public long ExecutionCount { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public long TotalInstructions { get; set; }
    public double InstructionsPerSecond { get; set; }
    public long MemoryAccesses { get; set; }
    public double CacheHitRate { get; set; }
}
```

## 🛠️ Compilation Options

### CompilationOptions

Controls kernel compilation behavior.

```csharp
public class CompilationOptions
{
    // Optimization
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;
    public bool EnableVectorization { get; set; } = true;
    public bool EnableInlining { get; set; } = true;
    
    // Debug Information
    public bool IncludeDebugInfo { get; set; } = false;
    public bool GenerateAssembly { get; set; } = false;
    
    // Execution
    public int? WorkGroupSize { get; set; }
    public MemoryModel MemoryModel { get; set; } = MemoryModel.Unified;
    
    // Backend-Specific
    public Dictionary<string, object> BackendOptions { get; set; } = new();
}

public enum OptimizationLevel
{
    None,
    Default,
    Aggressive,
    Size
}
```

#### Usage Examples

```csharp
// High-performance compilation
var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.Aggressive,
    EnableVectorization = true,
    EnableInlining = true
};

// Debug compilation
var debugOptions = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.None,
    IncludeDebugInfo = true,
    GenerateAssembly = true
};

// Size-optimized compilation
var sizeOptions = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.Size,
    EnableInlining = false
};
```

This API reference covers the essential interfaces and classes for building high-performance applications with DotCompute.