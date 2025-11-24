# Architecture Deep Dive

This module provides an in-depth understanding of DotCompute's architecture, enabling you to extend and contribute effectively.

## Four-Layer Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Extensions Layer                         │
│   DotCompute.Algorithms  │  DotCompute.Linq                 │
├─────────────────────────────────────────────────────────────┤
│                      Runtime Layer                           │
│   DotCompute.Generators  │  DotCompute.Analyzers            │
├─────────────────────────────────────────────────────────────┤
│                     Backends Layer                           │
│   CUDA  │  Metal  │  OpenCL  │  CPU                         │
├─────────────────────────────────────────────────────────────┤
│                       Core Layer                             │
│   DotCompute.Abstractions  │  DotCompute.Memory             │
└─────────────────────────────────────────────────────────────┘
```

## Core Layer

### DotCompute.Abstractions

Defines interfaces that all backends implement:

```csharp
// Core interfaces
public interface IComputeService
{
    IComputeBackend ActiveBackend { get; }
    IEnumerable<IComputeBackend> GetAvailableBackends();
    IBuffer<T> CreateBuffer<T>(int size) where T : unmanaged;
    Task ExecuteKernelAsync(Delegate kernel, KernelConfig config, params object[] args);
    Task SynchronizeAsync();
}

public interface IComputeBackend
{
    string Name { get; }
    BackendType Type { get; }
    bool IsAvailable { get; }
    DeviceInfo DeviceInfo { get; }

    IBuffer<T> CreateBuffer<T>(int size) where T : unmanaged;
    Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition);
    Task ExecuteKernelAsync(ICompiledKernel kernel, KernelConfig config, params IBuffer[] buffers);
}

public interface IBuffer<T> : IDisposable where T : unmanaged
{
    int Length { get; }
    long SizeInBytes { get; }

    Task CopyFromAsync(ReadOnlySpan<T> source);
    Task CopyToAsync(Span<T> destination);
    Span<T> AsSpan();  // For unified memory
}
```

### Key Abstractions

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IComputeService` | Main entry point | Abstractions |
| `IComputeBackend` | Backend abstraction | Abstractions |
| `IBuffer<T>` | GPU memory | Abstractions |
| `IKernelCompiler` | Kernel compilation | Abstractions |
| `ITimingProvider` | GPU timestamps | Abstractions |
| `IBarrierProvider` | Synchronization | Abstractions |

### DotCompute.Memory

Memory management infrastructure:

```csharp
// Memory pool for efficient allocation
public interface IMemoryPool
{
    IBuffer<T> Rent<T>(int size) where T : unmanaged;
    void Return<T>(IBuffer<T> buffer) where T : unmanaged;
    MemoryPoolStatistics GetStatistics();
}

// Unified memory across CPU and GPU
public interface IUnifiedBuffer<T> : IBuffer<T> where T : unmanaged
{
    MemoryLocation PreferredLocation { get; set; }
    void PrefetchToDevice();
    void PrefetchToHost();
}

// Peer-to-peer transfers
public interface IP2PManager
{
    bool CanAccessPeer(int sourceDevice, int targetDevice);
    Task EnablePeerAccessAsync(int sourceDevice, int targetDevice);
    Task CopyPeerAsync<T>(IBuffer<T> source, IBuffer<T> destination);
}
```

## Backends Layer

### Backend Implementation Pattern

Each backend follows a consistent pattern:

```csharp
namespace DotCompute.Backends.CUDA;

public class CudaBackend : IComputeBackend
{
    private readonly CudaDevice _device;
    private readonly CudaKernelCompiler _compiler;
    private readonly ILogger<CudaBackend> _logger;

    public string Name => "CUDA";
    public BackendType Type => BackendType.CUDA;
    public bool IsAvailable => CudaNative.IsAvailable();

    public DeviceInfo DeviceInfo => new()
    {
        Name = _device.Name,
        TotalMemory = _device.TotalMemory,
        ComputeCapability = _device.ComputeCapability,
        MaxThreadsPerBlock = _device.MaxThreadsPerBlock
    };

    public IBuffer<T> CreateBuffer<T>(int size) where T : unmanaged
    {
        return new CudaBuffer<T>(_device, size);
    }

    public async Task<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
    {
        // Generate PTX/CUBIN from kernel definition
        var compiled = await _compiler.CompileAsync(definition);
        return compiled;
    }

    public async Task ExecuteKernelAsync(
        ICompiledKernel kernel,
        KernelConfig config,
        params IBuffer[] buffers)
    {
        var cudaKernel = (CudaCompiledKernel)kernel;

        // Set up kernel arguments
        var args = buffers.Select(b => ((ICudaBuffer)b).DevicePtr).ToArray();

        // Launch kernel
        await _device.LaunchKernelAsync(
            cudaKernel.Function,
            config.GridSize,
            config.BlockSize,
            args);
    }
}
```

### Backend Registration

```csharp
// In DotCompute.Backends.CUDA
public static class CudaServiceExtensions
{
    public static IServiceCollection AddCudaBackend(
        this IServiceCollection services,
        Action<CudaOptions>? configure = null)
    {
        var options = new CudaOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IComputeBackend, CudaBackend>();
        services.AddSingleton<CudaKernelCompiler>();
        services.AddSingleton<CudaMemoryPool>();

        return services;
    }
}
```

## Runtime Layer

### Source Generators

Compile-time code generation:

```
[Kernel] attribute
       ↓
KernelSourceGenerator (Roslyn)
       ↓
Generated wrapper code
       ↓
Backend-specific compilation
```

### Analyzers

Real-time code validation:

```
User writes kernel code
       ↓
KernelAnalyzer runs
       ↓
Diagnostics reported
       ↓
Code fixes suggested
```

## Extensions Layer

### Algorithm Extensions

```csharp
// High-level algorithms
public static class MatrixExtensions
{
    public static async Task<Matrix<T>> MultiplyAsync<T>(
        this Matrix<T> a,
        Matrix<T> b,
        IComputeService service) where T : unmanaged
    {
        // Uses optimal kernel based on size and backend
        var kernel = MatrixKernels.GetOptimalMultiplyKernel<T>(
            a.Rows, a.Cols, b.Cols, service.ActiveBackend);

        var result = new Matrix<T>(a.Rows, b.Cols);
        await service.ExecuteKernelAsync(kernel, config, a.Buffer, b.Buffer, result.Buffer);
        return result;
    }
}
```

### LINQ Extensions

```csharp
// GPU-accelerated LINQ
public static class GpuLinqExtensions
{
    public static IGpuQueryable<T> AsGpuQueryable<T>(
        this IEnumerable<T> source,
        IComputeService service) where T : unmanaged
    {
        return new GpuQueryable<T>(source, service);
    }
}

// Usage
var result = data.AsGpuQueryable(service)
    .Where(x => x > 0)
    .Select(x => x * 2)
    .Sum();
```

## Extension Points

### Adding a New Backend

1. **Implement core interfaces:**

```csharp
public class MyBackend : IComputeBackend
{
    // Implement all interface members
}

public class MyBuffer<T> : IBuffer<T> where T : unmanaged
{
    // Implement buffer operations
}

public class MyKernelCompiler : IKernelCompiler
{
    // Implement kernel compilation
}
```

2. **Create service extensions:**

```csharp
public static class MyBackendExtensions
{
    public static IServiceCollection AddMyBackend(
        this IServiceCollection services)
    {
        services.AddSingleton<IComputeBackend, MyBackend>();
        return services;
    }
}
```

3. **Register in DI:**

```csharp
services.AddDotCompute()
    .AddMyBackend();
```

### Adding New Kernel Attributes

1. **Define the attribute:**

```csharp
[AttributeUsage(AttributeTargets.Method)]
public class MyKernelAttribute : Attribute
{
    public string Option1 { get; set; }
    public int Option2 { get; set; }
}
```

2. **Create source generator:**

```csharp
[Generator]
public class MyKernelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var kernels = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsMyKernelCandidate(node),
                transform: (ctx, _) => GetMyKernelInfo(ctx))
            .Where(k => k != null);

        context.RegisterSourceOutput(kernels, GenerateCode);
    }
}
```

### Adding New Analyzers

1. **Define diagnostic:**

```csharp
public static class MyDiagnostics
{
    public static readonly DiagnosticDescriptor InvalidPattern = new(
        id: "DC100",
        title: "Invalid GPU pattern",
        messageFormat: "Pattern '{0}' is not valid for GPU execution",
        category: "DotCompute",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

2. **Implement analyzer:**

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MyPatternAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MyDiagnostics.InvalidPattern);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }
}
```

## Dependency Injection Architecture

```csharp
// Core DI setup
public static class DotComputeServiceExtensions
{
    public static IServiceCollection AddDotCompute(
        this IServiceCollection services,
        Action<DotComputeOptions>? configure = null)
    {
        var options = new DotComputeOptions();
        configure?.Invoke(options);

        // Core services
        services.AddSingleton(options);
        services.AddSingleton<IComputeService, ComputeService>();
        services.AddSingleton<IMemoryPool, MemoryPool>();

        // Register backends based on availability
        if (CudaBackend.IsAvailable())
            services.AddSingleton<IComputeBackend, CudaBackend>();
        if (MetalBackend.IsAvailable())
            services.AddSingleton<IComputeBackend, MetalBackend>();
        if (OpenCLBackend.IsAvailable())
            services.AddSingleton<IComputeBackend, OpenCLBackend>();

        // Always available
        services.AddSingleton<IComputeBackend, CpuBackend>();

        return services;
    }
}
```

## Exercises

### Exercise 1: Interface Analysis

Examine the `IComputeBackend` interface and list all methods a new backend must implement.

### Exercise 2: Service Resolution

Trace how `IComputeService` is resolved and how it selects the active backend.

### Exercise 3: Extension Design

Design the interface for a new feature (e.g., automatic differentiation).

## Key Takeaways

1. **Four-layer architecture** separates concerns cleanly
2. **Interfaces define contracts** between layers
3. **Backends implement abstractions** for specific hardware
4. **DI enables loose coupling** and testing
5. **Extension points** allow adding backends, analyzers, and generators

## Next Module

[Source Generator Development →](source-generators.md)

Learn to build compile-time code generators for GPU kernels.

## Further Reading

- [Architecture Overview](../../architecture/overview.md) - High-level architecture
- [Backend Integration](../../architecture/backend-integration.md) - Backend details
- [Source Generators](../../architecture/source-generators.md) - Generator architecture
