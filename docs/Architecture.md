# DotCompute Architecture Overview

This document provides a comprehensive overview of DotCompute's architecture, designed for developers who want to understand the internal workings of the framework.

## Table of Contents

- [High-Level Architecture](#high-level-architecture)
- [Component Diagram](#component-diagram)
- [Core Layers](#core-layers)
- [Backend Abstraction Layer](#backend-abstraction-layer)
- [Memory Management System](#memory-management-system)
- [Kernel Compilation Pipeline](#kernel-compilation-pipeline)
- [Plugin Architecture](#plugin-architecture)
- [Runtime Integration](#runtime-integration)
- [Performance Optimization](#performance-optimization)
- [Cross-Cutting Concerns](#cross-cutting-concerns)

## High-Level Architecture

DotCompute follows a layered architecture with clear separation of concerns, designed for Native AOT compatibility and production-grade performance:

```
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ User Code with │  │ LINQ Queries   │  │ Algorithm      │ │
│  │ [Kernel] attrs │  │ + Extensions   │  │ Libraries      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
┌─────────────────────────────────────────────────────────────┐
│                 Source Generation Layer                     │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ Kernel Source  │  │ Roslyn         │  │ Code Fixes &   │ │
│  │ Generator      │  │ Analyzers      │  │ Refactoring    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
┌─────────────────────────────────────────────────────────────┐
│                   Runtime Orchestration                     │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ ICompute       │  │ Kernel         │  │ Backend        │ │
│  │ Orchestrator   │  │ Execution      │  │ Selection      │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
┌─────────────────────────────────────────────────────────────┐
│                    Backend Layer                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ CPU Backend    │  │ CUDA Backend   │  │ Plugin         │ │
│  │ (SIMD/AVX)     │  │ (NVIDIA GPU)   │  │ Backends       │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                               │
┌─────────────────────────────────────────────────────────────┐
│                  Memory Management                         │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │ Unified Memory │  │ Memory Pool    │  │ P2P Transfer   │ │
│  │ Buffers        │  │ Management     │  │ Manager        │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Component Diagram

### Core Components and Their Relationships

```mermaid
graph TB
    subgraph "Application Layer"
        A1[User Code with [Kernel]]
        A2[LINQ Extensions]
        A3[Algorithm Libraries]
    end
    
    subgraph "Source Generation"
        S1[KernelSourceGenerator]
        S2[DotComputeAnalyzer]
        S3[CodeFixProvider]
    end
    
    subgraph "Runtime Services"
        R1[IComputeOrchestrator]
        R2[KernelExecutionService]
        R3[GeneratedKernelDiscoveryService]
        R4[AdaptiveBackendSelector]
    end
    
    subgraph "Core Abstractions"
        C1[IAccelerator]
        C2[IKernelCompiler]
        C3[IUnifiedMemoryManager]
        C4[KernelDefinition]
    end
    
    subgraph "Backend Implementations"
        B1[CpuAccelerator]
        B2[CudaAccelerator] 
        B3[PluginAccelerator]
    end
    
    subgraph "Memory System"
        M1[UnifiedBuffer<T>]
        M2[MemoryPool]
        M3[P2PManager]
    end
    
    A1 --> S1
    S1 --> R3
    R3 --> R2
    R2 --> R1
    R1 --> R4
    R4 --> C1
    C1 --> B1
    C1 --> B2
    C1 --> B3
    R2 --> C2
    R2 --> C3
    C3 --> M1
    M1 --> M2
    B2 --> M3
```

## Core Layers

### 1. Abstractions Layer (`DotCompute.Abstractions`)

Defines the fundamental interfaces and contracts:

```csharp
// Core computation interface
public interface IComputeOrchestrator
{
    Task<T> ExecuteAsync<T>(string kernelName, params object[] args);
    Task<IAccelerator?> GetOptimalAcceleratorAsync(string kernelName);
    Task PrecompileKernelAsync(string kernelName, IAccelerator? accelerator = null);
}

// Backend abstraction
public interface IAccelerator : IAsyncDisposable
{
    AcceleratorInfo Info { get; }
    AcceleratorType Type { get; }
    IUnifiedMemoryManager Memory { get; }
    ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition);
}

// Memory abstraction
public interface IUnifiedMemoryBuffer<T> : IAsyncDisposable where T : unmanaged
{
    int Length { get; }
    bool IsOnHost { get; }
    bool IsOnDevice { get; }
    Span<T> AsSpan();
    void EnsureOnDevice();
    ValueTask SynchronizeAsync(CancellationToken cancellationToken = default);
}
```

### 2. Core Layer (`DotCompute.Core`)

Implements core runtime functionality:

- **Orchestration Engine**: Coordinates kernel execution across backends
- **Debugging Services**: Cross-backend validation and profiling
- **Optimization Engine**: ML-powered backend selection
- **Telemetry System**: Performance monitoring and metrics collection

```csharp
// Core orchestration with debugging integration
public class DebugIntegratedOrchestrator : IComputeOrchestrator
{
    private readonly IComputeOrchestrator _inner;
    private readonly IKernelDebugService _debugService;
    
    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        // Cross-backend validation in debug builds
        if (_debugProfile.EnableCrossBackendValidation)
        {
            await _debugService.ValidateAcrossBackendsAsync(kernelName, args);
        }
        
        return await _inner.ExecuteAsync<T>(kernelName, args);
    }
}
```

### 3. Memory Layer (`DotCompute.Memory`)

Advanced memory management with unified addressing:

```csharp
// Unified buffer with lazy transfer semantics
public sealed class UnifiedBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    private BufferState _state;
    private T[]? _hostArray;
    private DeviceMemory _deviceMemory;
    
    public void EnsureOnDevice()
    {
        switch (_state)
        {
            case BufferState.HostOnly:
            case BufferState.HostDirty:
                TransferHostToDevice();
                _state = BufferState.Synchronized;
                break;
        }
    }
}
```

### 4. Runtime Layer (`DotCompute.Runtime`)

Service orchestration and dependency injection integration:

```csharp
// Production-grade kernel execution service
public class KernelExecutionService : IComputeOrchestrator
{
    private readonly AcceleratorRuntime _runtime;
    private readonly IKernelCache _cache;
    private readonly IKernelProfiler _profiler;
    
    public async Task<T> ExecuteAsync<T>(string kernelName, params object[] args)
    {
        // 1. Resolve kernel from registry
        var kernel = await ResolveKernelAsync(kernelName);
        
        // 2. Select optimal accelerator
        var accelerator = await SelectAcceleratorAsync(kernel);
        
        // 3. Compile if needed (with caching)
        var compiled = await CompileKernelAsync(kernel, accelerator);
        
        // 4. Execute with profiling
        return await ExecuteWithProfilingAsync<T>(compiled, args);
    }
}
```

## Backend Abstraction Layer

### Backend Discovery and Selection

```csharp
public class AcceleratorRuntime
{
    private readonly List<IAccelerator> _accelerators = new();
    
    public async Task InitializeAsync()
    {
        // Discover available backends
        await DiscoverCpuAcceleratorsAsync();
        await DiscoverCudaAcceleratorsAsync();
        await DiscoverPluginAcceleratorsAsync();
        
        // Initialize backend-specific resources
        foreach (var accelerator in _accelerators)
        {
            await accelerator.InitializeAsync();
        }
    }
    
    public IAccelerator? SelectOptimal(KernelDefinition kernel, WorkloadCharacteristics workload)
    {
        return _backendSelector.SelectOptimal(_accelerators, kernel, workload);
    }
}
```

### CPU Backend Architecture

```csharp
public class CpuAccelerator : IAccelerator
{
    private readonly SimdProcessor _simdProcessor;
    private readonly ThreadPoolScheduler _scheduler;
    
    public async ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
    {
        // Compile to optimized CPU code with SIMD intrinsics
        var optimizedCode = _simdProcessor.OptimizeForTarget(definition, _cpuInfo);
        return new CpuCompiledKernel(optimizedCode, _scheduler);
    }
}
```

### CUDA Backend Architecture

```csharp
public class CudaAccelerator : IAccelerator
{
    private readonly CudaKernelCompiler _compiler;
    private readonly CudaCapabilityManager _capabilityManager;
    
    public async ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition)
    {
        // Determine target architecture
        var computeCapability = _capabilityManager.GetTargetComputeCapability();
        
        // Compile CUDA C to PTX/CUBIN
        var options = new CompilationOptions
        {
            TargetArchitecture = computeCapability,
            OptimizationLevel = OptimizationLevel.Maximum,
            GenerateDebugInfo = _debugMode
        };
        
        return await _compiler.CompileAsync(definition, options);
    }
}
```

## Memory Management System

### Unified Memory Architecture

DotCompute's memory system provides automatic, lazy transfers between host and device memory:

```csharp
public enum BufferState
{
    Uninitialized,    // No memory allocated
    HostOnly,         // Data exists only on host
    DeviceOnly,       // Data exists only on device  
    Synchronized,     // Data synchronized between host/device
    HostDirty,        // Host has newer data
    DeviceDirty       // Device has newer data
}

public class UnifiedBuffer<T> : IUnifiedMemoryBuffer<T>
{
    private BufferState _state;
    
    // Automatic transfer management
    public Span<T> AsSpan()
    {
        EnsureOnHost();      // Transfer from device if needed
        return _hostSpan;
    }
    
    public DeviceMemory GetDeviceMemory()
    {
        EnsureOnDevice();    // Transfer from host if needed
        return _deviceMemory;
    }
}
```

### Memory Pool Management

```csharp
public class MemoryPool : IUnifiedMemoryManager
{
    private readonly ConcurrentDictionary<int, ConcurrentQueue<IUnifiedMemoryBuffer>> _pools = new();
    
    public async ValueTask<IUnifiedMemoryBuffer<T>> AllocateAsync<T>(int length) where T : unmanaged
    {
        var sizeCategory = GetSizeCategory(length * sizeof(T));
        
        // Try to reuse from pool first
        if (_pools[sizeCategory].TryDequeue(out var pooled))
        {
            return (IUnifiedMemoryBuffer<T>)pooled;
        }
        
        // Allocate new buffer
        return new UnifiedBuffer<T>(_deviceMemoryManager, length);
    }
}
```

### Peer-to-Peer (P2P) Transfers

```csharp
public class P2PManager
{
    public async Task<bool> CanTransferDirectlyAsync(IAccelerator source, IAccelerator destination)
    {
        // Check for CUDA P2P capabilities
        if (source is CudaAccelerator srcCuda && destination is CudaAccelerator destCuda)
        {
            return await CheckCudaP2PAsync(srcCuda.DeviceId, destCuda.DeviceId);
        }
        
        return false; // Fallback to host-mediated transfer
    }
}
```

## Kernel Compilation Pipeline

### Source Generation Phase

```csharp
[Generator]
public class KernelSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find methods with [Kernel] attribute
        var kernelMethods = context.SyntaxProvider
            .CreateSyntaxProvider(IsKernelMethod, GetKernelMethodInfo)
            .Where(m => m is not null);
        
        // Generate wrapper code
        context.RegisterSourceOutput(kernelMethods, GenerateKernelWrapper);
    }
    
    private static void GenerateKernelWrapper(SourceProductionContext context, KernelMethodInfo method)
    {
        var source = $$"""
            // Generated wrapper for {{method.Name}}
            public static class {{method.ContainingType}}_Generated
            {
                public static KernelDefinition {{method.Name}} => new()
                {
                    Name = "{{method.FullyQualifiedName}}",
                    SourceCode = "{{EscapeSourceCode(method.SourceCode)}}",
                    Parameters = new[] { {{GenerateParameters(method.Parameters)}} },
                    ThreadingModel = ThreadingModel.{{method.ThreadingModel}}
                };
            }
            """;
        
        context.AddSource($"{method.ContainingType}_{method.Name}.g.cs", source);
    }
}
```

### Runtime Compilation

```csharp
public class CudaKernelCompiler : IKernelCompiler
{
    public async Task<ICompiledKernel> CompileAsync(KernelDefinition definition, CompilationOptions options)
    {
        // 1. Generate CUDA C source
        var cudaSource = GenerateCudaSource(definition);
        
        // 2. Compile with NVRTC
        var ptxCode = await CompileWithNvrtcAsync(cudaSource, options);
        
        // 3. Create executable module
        var module = await LoadModuleAsync(ptxCode);
        
        // 4. Return compiled kernel
        return new CudaCompiledKernel(module, definition.Name);
    }
    
    private async Task<string> CompileWithNvrtcAsync(string source, CompilationOptions options)
    {
        var program = nvrtcCreateProgram(source, "kernel.cu", 0, null, null);
        
        var compileOptions = new[]
        {
            $"--gpu-architecture=compute_{options.TargetArchitecture.Major}{options.TargetArchitecture.Minor}",
            "--use_fast_math",
            options.OptimizationLevel == OptimizationLevel.Maximum ? "-O3" : "-O2"
        };
        
        var result = nvrtcCompileProgram(program, compileOptions.Length, compileOptions);
        if (result != nvrtcResult.NVRTC_SUCCESS)
        {
            var log = GetCompilationLog(program);
            throw new KernelCompilationException($"CUDA compilation failed: {log}");
        }
        
        return GetPTXCode(program);
    }
}
```

## Plugin Architecture

### Plugin Discovery and Loading

```csharp
public class PluginManager
{
    private readonly List<IAcceleratorPlugin> _plugins = new();
    
    public async Task LoadPluginsAsync(string pluginDirectory)
    {
        var assemblyFiles = Directory.GetFiles(pluginDirectory, "*.dll");
        
        foreach (var file in assemblyFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(file);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IAcceleratorPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                
                foreach (var type in pluginTypes)
                {
                    var plugin = (IAcceleratorPlugin)Activator.CreateInstance(type)!;
                    await plugin.InitializeAsync();
                    _plugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to load plugin from {File}: {Error}", file, ex.Message);
            }
        }
    }
}

// Plugin interface
public interface IAcceleratorPlugin
{
    string Name { get; }
    Version Version { get; }
    Task InitializeAsync();
    Task<IAccelerator> CreateAcceleratorAsync(AcceleratorConfig config);
    bool IsSupported();
}
```

## Runtime Integration

### Dependency Injection Setup

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDotComputeRuntime(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<AcceleratorRuntime>();
        services.AddSingleton<IComputeOrchestrator, KernelExecutionService>();
        services.AddSingleton<IKernelCache, DistributedKernelCache>();
        
        // Memory management
        services.AddSingleton<IUnifiedMemoryManager, MemoryPool>();
        services.AddTransient<UnifiedBuffer<float>>(); // Example typed buffer
        
        // Backend selection
        services.AddSingleton<IBackendSelector, AdaptiveBackendSelector>();
        
        // Discovery services
        services.AddSingleton<GeneratedKernelDiscoveryService>();
        
        return services;
    }
    
    public static IServiceCollection AddProductionOptimization(this IServiceCollection services)
    {
        // Wrap orchestrator with optimization
        services.Decorate<IComputeOrchestrator, PerformanceOptimizedOrchestrator>();
        
        // Add profiling
        services.AddSingleton<IKernelProfiler, HardwareCounterProfiler>();
        
        // Add adaptive selection with ML
        services.AddSingleton<IBackendSelector, MLBackendSelector>();
        
        return services;
    }
    
    public static IServiceCollection AddProductionDebugging(this IServiceCollection services)
    {
        // Wrap orchestrator with debugging
        services.Decorate<IComputeOrchestrator, DebugIntegratedOrchestrator>();
        
        // Add debug services
        services.AddSingleton<IKernelDebugService, KernelDebugService>();
        
        return services;
    }
}
```

## Performance Optimization

### Adaptive Backend Selection

```csharp
public class AdaptiveBackendSelector : IBackendSelector
{
    private readonly Dictionary<string, BackendPerformanceHistory> _performanceHistory = new();
    
    public IAccelerator SelectOptimal(IEnumerable<IAccelerator> available, KernelDefinition kernel, WorkloadCharacteristics workload)
    {
        // Get historical performance data
        var history = _performanceHistory.GetValueOrDefault(kernel.Name);
        
        if (history == null || workload.DataSize > history.LargestTestedSize)
        {
            // No history available, use heuristics
            return UseHeuristics(available, workload);
        }
        
        // Select based on measured performance
        return history.GetBestAcceleratorFor(workload);
    }
    
    private IAccelerator UseHeuristics(IEnumerable<IAccelerator> available, WorkloadCharacteristics workload)
    {
        // Simple heuristics for backend selection
        if (workload.DataSize < 10000 && workload.ComputeIntensity < 0.5)
        {
            return available.FirstOrDefault(a => a.Type == AcceleratorType.CPU) ?? available.First();
        }
        
        return available.FirstOrDefault(a => a.Type == AcceleratorType.GPU) ?? available.First();
    }
}
```

### Caching Strategy

```csharp
public class DistributedKernelCache : IKernelCache
{
    private readonly IMemoryCache _l1Cache;  // Fast in-memory cache
    private readonly IDistributedCache _l2Cache; // Persistent distributed cache
    
    public async Task<ICompiledKernel?> GetAsync(string kernelName, IAccelerator accelerator)
    {
        var key = $"{kernelName}_{accelerator.Info.Id}";
        
        // Check L1 cache first
        if (_l1Cache.TryGetValue(key, out ICompiledKernel? cached))
        {
            return cached;
        }
        
        // Check L2 cache
        var serialized = await _l2Cache.GetAsync(key);
        if (serialized != null)
        {
            var compiled = DeserializeKernel(serialized);
            _l1Cache.Set(key, compiled, TimeSpan.FromHours(1));
            return compiled;
        }
        
        return null;
    }
}
```

## Cross-Cutting Concerns

### Logging and Telemetry

```csharp
public partial class KernelExecutionService
{
    // High-performance logging with source generators
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Executing kernel {KernelName} on {AcceleratorType} with {ElementCount} elements")]
    private partial void LogKernelExecution(string kernelName, string acceleratorType, int elementCount);
    
    // Performance telemetry
    private void RecordExecutionMetrics(string kernelName, IAccelerator accelerator, TimeSpan duration, int elementCount)
    {
        _telemetryService.RecordMetric("kernel.execution.duration", duration.TotalMilliseconds, new TagList
        {
            ["kernel"] = kernelName,
            ["accelerator"] = accelerator.Type.ToString(),
            ["elements"] = elementCount.ToString()
        });
    }
}
```

### Error Handling and Resilience

```csharp
public class ResilientKernelExecutor
{
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        var exceptions = new List<Exception>();
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (CudaException ex) when (ex.CudaError == CudaError.OutOfMemory && attempt < maxRetries)
            {
                // Handle GPU OOM by falling back to CPU
                exceptions.Add(ex);
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt))); // Exponential backoff
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                exceptions.Add(ex);
                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt));
            }
        }
        
        throw new AggregateException("Kernel execution failed after all retries", exceptions);
    }
}
```

This architecture provides a robust, scalable foundation for high-performance computing in .NET, with clear separation of concerns and extensibility points for future enhancements.