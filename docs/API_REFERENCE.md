# DotCompute API Reference

Version: 0.1.0-alpha

## Table of Contents

1. [Core Abstractions](#core-abstractions)
2. [Accelerators](#accelerators)
3. [Kernels](#kernels)
4. [Memory Management](#memory-management)
5. [Compilation](#compilation)
6. [Execution](#execution)

---

## Core Abstractions

### Namespace: `DotCompute.Abstractions`

#### `IAccelerator`

Primary interface for all compute accelerators.

```csharp
public interface IAccelerator : IDisposable
{
    // Properties
    AcceleratorInfo Info { get; }
    bool IsAvailable { get; }
    AcceleratorCapabilities Capabilities { get; }
    
    // Methods
    Task<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
        
    Task<IMemoryBuffer> AllocateBufferAsync(
        long sizeInBytes,
        MemoryFlags flags = MemoryFlags.ReadWrite);
        
    Task ExecuteAsync(
        ICompiledKernel kernel,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default);
}
```

#### `IComputeService`

High-level compute orchestration service.

```csharp
public interface IComputeService
{
    Task<IAccelerator> GetAcceleratorAsync(
        AcceleratorType preferredType = AcceleratorType.Auto);
        
    Task<TResult> ExecuteKernelAsync<TResult>(
        string kernelName,
        object parameters,
        CancellationToken cancellationToken = default);
}
```

---

## Accelerators

### Namespace: `DotCompute.Backends.CPU`

#### `CpuAccelerator`

CPU-based compute accelerator with SIMD support.

```csharp
public sealed class CpuAccelerator : BaseAccelerator
{
    // Constructor
    public CpuAccelerator(ILogger<CpuAccelerator>? logger = null);
    
    // Properties
    public int ThreadCount { get; init; }
    public SimdLevel SimdSupport { get; }
    
    // Methods
    public override async Task<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Namespace: `DotCompute.Backends.CUDA`

#### `CudaAccelerator`

NVIDIA CUDA GPU accelerator.

```csharp
public sealed class CudaAccelerator : BaseAccelerator
{
    // Constructor
    public CudaAccelerator(
        int deviceId = 0,
        ILogger<CudaAccelerator>? logger = null);
    
    // Properties
    public int DeviceId { get; }
    public CudaDeviceProperties DeviceProperties { get; }
    public (int Major, int Minor) ComputeCapability { get; }
    
    // Methods
    public override async Task<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

---

## Kernels

### Namespace: `DotCompute.Abstractions.Kernels`

#### `KernelDefinition`

Defines a compute kernel for compilation.

```csharp
public sealed class KernelDefinition
{
    // Required Properties
    public required string Name { get; init; }
    public required string Code { get; init; }
    
    // Optional Properties
    public string? EntryPoint { get; init; }
    public KernelLanguage Language { get; init; } = KernelLanguage.C;
    public Dictionary<string, object>? Metadata { get; init; }
    public string[]? RequiredHeaders { get; init; }
}
```

#### `ICompiledKernel`

Represents a compiled kernel ready for execution.

```csharp
public interface ICompiledKernel : IDisposable
{
    string Name { get; }
    string EntryPoint { get; }
    KernelMetadata Metadata { get; }
    
    Task<KernelExecutionResult> ExecuteAsync(
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default);
}
```

#### `KernelExecutionParameters`

Parameters for kernel execution.

```csharp
public sealed class KernelExecutionParameters
{
    // Grid Configuration
    public GridDimensions GridDim { get; init; }
    public BlockDimensions BlockDim { get; init; }
    
    // Kernel Arguments
    public object[] Arguments { get; init; }
    
    // Optional Settings
    public int SharedMemoryBytes { get; init; }
    public Stream? OutputStream { get; init; }
}
```

---

## Memory Management

### Namespace: `DotCompute.Memory`

#### `UnifiedBuffer<T>`

Zero-copy unified memory buffer.

```csharp
public sealed class UnifiedBuffer<T> : IMemoryBuffer<T>
    where T : unmanaged
{
    // Constructor
    public UnifiedBuffer(long elementCount, IAccelerator accelerator);
    
    // Properties
    public long Length { get; }
    public long SizeInBytes { get; }
    public bool IsDisposed { get; }
    
    // Methods
    public Span<T> AsSpan();
    public Memory<T> AsMemory();
    public Task CopyToAsync(T[] destination, int offset = 0);
    public Task CopyFromAsync(T[] source, int offset = 0);
    public void Dispose();
}
```

#### `MemoryPool`

Memory pooling for efficient allocation.

```csharp
public sealed class MemoryPool : IMemoryPool
{
    // Constructor
    public MemoryPool(MemoryPoolOptions? options = null);
    
    // Methods
    public IMemoryBuffer Rent(long sizeInBytes);
    public void Return(IMemoryBuffer buffer);
    public void Clear();
    
    // Properties
    public long TotalAllocated { get; }
    public long TotalPooled { get; }
    public double HitRate { get; }
}
```

---

## Compilation

### Namespace: `DotCompute.Abstractions`

#### `CompilationOptions`

Options for kernel compilation.

```csharp
public sealed class CompilationOptions
{
    // Optimization
    public OptimizationLevel OptimizationLevel { get; init; } = OptimizationLevel.O2;
    public bool UseFastMath { get; init; } = false;
    public bool GenerateDebugInfo { get; init; } = false;
    
    // Target Architecture
    public ComputeCapability ComputeCapability { get; init; }
    public int MaxRegistersPerThread { get; init; } = 0; // 0 = auto
    
    // Include Paths
    public string[]? IncludePaths { get; init; }
    public string[]? PreprocessorDefines { get; init; }
}
```

---

## Execution

### Namespace: `DotCompute.Core.Execution`

#### `KernelExecutor`

Manages kernel execution across accelerators.

```csharp
public sealed class KernelExecutor
{
    // Constructor
    public KernelExecutor(IAccelerator accelerator, ILogger? logger = null);
    
    // Methods
    public async Task<TResult> ExecuteAsync<TResult>(
        ICompiledKernel kernel,
        Func<KernelExecutionResult, TResult> resultSelector,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default);
        
    public async Task ExecuteAsync(
        ICompiledKernel kernel,
        KernelExecutionParameters parameters,
        CancellationToken cancellationToken = default);
}
```

---

## Error Handling

### Common Exceptions

#### `AcceleratorException`
Base exception for accelerator-related errors.

```csharp
public class AcceleratorException : Exception
{
    public AcceleratorError ErrorCode { get; }
    public string? DeviceInfo { get; }
}
```

#### `KernelCompilationException`
Thrown when kernel compilation fails.

```csharp
public class KernelCompilationException : AcceleratorException
{
    public string? CompilerOutput { get; }
    public CompilationStage FailedStage { get; }
}
```

#### `MemoryAllocationException`
Thrown when memory allocation fails.

```csharp
public class MemoryAllocationException : AcceleratorException
{
    public long RequestedBytes { get; }
    public long AvailableBytes { get; }
}
```

---

## Extension Methods

### Namespace: `DotCompute.Extensions`

#### Buffer Extensions

```csharp
public static class BufferExtensions
{
    public static UnifiedBuffer<T> ToUnifiedBuffer<T>(
        this T[] array,
        IAccelerator accelerator) where T : unmanaged;
        
    public static async Task<T[]> ToArrayAsync<T>(
        this IMemoryBuffer<T> buffer) where T : unmanaged;
}
```

---

## Usage Examples

### Basic Vector Addition

```csharp
using DotCompute.Core;
using DotCompute.Backends.CPU;

// Initialize accelerator
using var accelerator = new CpuAccelerator();

// Define kernel
var kernel = new KernelDefinition
{
    Name = "VectorAdd",
    Code = @"
        void VectorAdd(float* a, float* b, float* c, int n) {
            for (int i = 0; i < n; i++) {
                c[i] = a[i] + b[i];
            }
        }",
    EntryPoint = "VectorAdd"
};

// Compile kernel
var compiled = await accelerator.CompileKernelAsync(kernel);

// Prepare data
var size = 1000;
using var bufferA = new UnifiedBuffer<float>(size, accelerator);
using var bufferB = new UnifiedBuffer<float>(size, accelerator);
using var bufferC = new UnifiedBuffer<float>(size, accelerator);

// Execute kernel
await compiled.ExecuteAsync(new KernelExecutionParameters
{
    Arguments = new object[] { bufferA, bufferB, bufferC, size },
    GridDim = new GridDimensions(1, 1, 1),
    BlockDim = new BlockDimensions(256, 1, 1)
});
```

### CUDA GPU Execution

```csharp
using DotCompute.Backends.CUDA;

// Initialize CUDA accelerator
using var accelerator = new CudaAccelerator(deviceId: 0);

// Check capabilities
if (!accelerator.IsAvailable)
{
    throw new InvalidOperationException("CUDA not available");
}

// Compile with CUDA-specific options
var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.O3,
    UseFastMath = true,
    ComputeCapability = accelerator.ComputeCapability
};

var compiled = await accelerator.CompileKernelAsync(kernel, options);
```

---

## Thread Safety

- All accelerator instances are thread-safe for concurrent kernel compilation
- Memory buffers are not thread-safe; synchronization required for concurrent access
- Kernel execution is thread-safe when using different compiled kernel instances
- Memory pool is thread-safe for rent/return operations

---

## Performance Considerations

1. **Memory Allocation**: Use memory pooling for frequent allocations
2. **Kernel Compilation**: Cache compiled kernels when possible
3. **Data Transfer**: Minimize host-device transfers
4. **Grid Configuration**: Choose appropriate block/grid dimensions
5. **SIMD Alignment**: Ensure data alignment for optimal SIMD performance

---

## Versioning

This API reference corresponds to DotCompute version 0.1.0-alpha. The API is subject to breaking changes in future releases.

---

*Last Updated: 2025*  
*Author: Michael Ivertowski*