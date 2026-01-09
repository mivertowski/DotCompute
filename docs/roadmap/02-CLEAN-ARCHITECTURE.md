# DotCompute Roadmap: Clean Architecture & Maintainability

**Document Version**: 1.0
**Last Updated**: January 2026
**Target Version**: v0.6.0 - v1.0.0
**Status**: Strategic Planning

---

## Executive Summary

This document outlines strategies to improve the architectural cleanliness, maintainability, and long-term sustainability of the DotCompute codebase. While the current architecture demonstrates strong layering (Core → Backends → Extensions → Runtime), there are opportunities to strengthen boundaries, improve testability, and reduce coupling.

**Key Objectives**:
- Strengthen layer boundaries with explicit dependency rules
- Implement ports and adapters pattern for backend abstraction
- Improve dependency injection consistency
- Establish clear API surface contracts
- Reduce coupling between components

---

## 1. Architectural Layers Analysis

### Current Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Layer 4: Runtime                          │
│   DotCompute.Runtime | DotCompute.Generators | DotCompute.Plugins │
│   (DI, Orchestration, Code Generation, Plugin Management)       │
├─────────────────────────────────────────────────────────────────┤
│                       Layer 3: Extensions                        │
│            DotCompute.Linq | DotCompute.Algorithms              │
│          (GPU LINQ, Algorithm Implementations)                   │
├─────────────────────────────────────────────────────────────────┤
│                        Layer 2: Backends                         │
│          CPU | CUDA | Metal | OpenCL (Hardware Abstraction)     │
├─────────────────────────────────────────────────────────────────┤
│                         Layer 1: Core                            │
│   DotCompute.Abstractions | DotCompute.Core | DotCompute.Memory │
│        (Interfaces, Base Implementations, Memory Management)    │
└─────────────────────────────────────────────────────────────────┘
```

### Current Strengths
- Clear 4-layer separation
- Core abstractions are dependency-free
- Backends implement core interfaces
- Extensions depend on Core, optionally on Backends

### Areas for Improvement
- Some backend code references specific implementations instead of interfaces
- Runtime layer sometimes bypasses extension layer
- Memory management has tight coupling between layers
- Plugin system has circular dependency risks

---

## 2. Ports and Adapters Enhancement

### Goal
Implement a pure ports and adapters (hexagonal) architecture where Core defines all contracts and Backends are pure adapters.

### Current Pattern
```csharp
// Current: Backend directly implements interface
public class CudaAccelerator : IAccelerator
{
    // Mixes domain logic with infrastructure
    public async Task<CompiledKernel> CompileAsync(string source)
    {
        // CUDA-specific compilation
        var ptx = await _nvrtc.CompileAsync(source);
        return new CudaCompiledKernel(ptx);
    }
}
```

### Proposed Pattern (v0.7.0)

```csharp
// CORE LAYER: Port (Interface)
namespace DotCompute.Core.Ports;

public interface IKernelCompilationPort
{
    ValueTask<CompiledKernel> CompileAsync(
        KernelSource source,
        CompilationOptions options,
        CancellationToken ct = default);
}

public interface IMemoryManagementPort
{
    ValueTask<IBuffer<T>> AllocateAsync<T>(
        int length,
        BufferFlags flags = BufferFlags.None,
        CancellationToken ct = default) where T : unmanaged;

    ValueTask TransferAsync<T>(
        IBuffer<T> source,
        IBuffer<T> destination,
        CancellationToken ct = default) where T : unmanaged;
}

public interface IDeviceDiscoveryPort
{
    ValueTask<IReadOnlyList<DeviceInfo>> DiscoverDevicesAsync(
        CancellationToken ct = default);

    ValueTask<DeviceCapabilities> GetCapabilitiesAsync(
        DeviceId deviceId,
        CancellationToken ct = default);
}

// BACKEND LAYER: Adapter (Implementation)
namespace DotCompute.Backend.Cuda.Adapters;

public sealed class CudaKernelCompilationAdapter : IKernelCompilationPort
{
    private readonly INvrtcWrapper _nvrtc;
    private readonly ILogger<CudaKernelCompilationAdapter> _logger;

    public async ValueTask<CompiledKernel> CompileAsync(
        KernelSource source,
        CompilationOptions options,
        CancellationToken ct = default)
    {
        // Pure adapter - no domain logic, only translation
        var nvrtcOptions = MapToNvrtcOptions(options);
        var result = await _nvrtc.CompileAsync(source.Code, nvrtcOptions, ct);
        return MapToCompiledKernel(result);
    }

    private static NvrtcOptions MapToNvrtcOptions(CompilationOptions options) { }
    private static CompiledKernel MapToCompiledKernel(NvrtcResult result) { }
}
```

### Implementation Plan

#### Phase 1: Define Core Ports (v0.6.0)
```
src/Core/DotCompute.Abstractions/Ports/
├── IKernelCompilationPort.cs
├── IMemoryManagementPort.cs
├── IDeviceDiscoveryPort.cs
├── IKernelExecutionPort.cs
├── IProfilingPort.cs
├── IHealthMonitoringPort.cs
└── IRingKernelPort.cs
```

#### Phase 2: Implement Backend Adapters (v0.7.0)
```
src/Backends/DotCompute.Backend.Cuda/Adapters/
├── CudaKernelCompilationAdapter.cs
├── CudaMemoryManagementAdapter.cs
├── CudaDeviceDiscoveryAdapter.cs
├── CudaKernelExecutionAdapter.cs
├── CudaProfilingAdapter.cs
├── CudaHealthMonitoringAdapter.cs
└── CudaRingKernelAdapter.cs
```

#### Phase 3: Refactor Accelerators to Use Adapters (v0.8.0)
```csharp
// Accelerator becomes a facade over adapters
public sealed class CudaAccelerator : IAccelerator
{
    private readonly IKernelCompilationPort _compilation;
    private readonly IMemoryManagementPort _memory;
    private readonly IKernelExecutionPort _execution;
    private readonly IProfilingPort _profiling;

    public CudaAccelerator(
        IKernelCompilationPort compilation,
        IMemoryManagementPort memory,
        IKernelExecutionPort execution,
        IProfilingPort profiling)
    {
        _compilation = compilation;
        _memory = memory;
        _execution = execution;
        _profiling = profiling;
    }

    // Delegation to ports
    public ValueTask<CompiledKernel> CompileAsync(KernelSource source, CompilationOptions options)
        => _compilation.CompileAsync(source, options);
}
```

---

## 3. Dependency Injection Improvements

### Current State
- `AddDotComputeRuntime()` extension provides core services
- `AddCudaBackend()`, `AddCpuBackend()`, etc. add backends
- Some services registered multiple times with different lifetimes
- Inconsistent use of factory patterns

### Improvements

#### Phase 1: Unified Service Registration (v0.6.0)

```csharp
// NEW: Fluent builder for service configuration
public static class DotComputeServiceCollectionExtensions
{
    public static IServiceCollection AddDotCompute(
        this IServiceCollection services,
        Action<DotComputeBuilder> configure)
    {
        var builder = new DotComputeBuilder(services);
        configure(builder);
        return services;
    }
}

public sealed class DotComputeBuilder
{
    private readonly IServiceCollection _services;

    public DotComputeBuilder WithCuda(Action<CudaOptions>? configure = null)
    {
        _services.AddSingleton<IKernelCompilationPort, CudaKernelCompilationAdapter>();
        _services.AddSingleton<IMemoryManagementPort, CudaMemoryManagementAdapter>();
        // ... configure CUDA services
        return this;
    }

    public DotComputeBuilder WithMetal(Action<MetalOptions>? configure = null)
    {
        // ... configure Metal services
        return this;
    }

    public DotComputeBuilder WithRingKernels(Action<RingKernelOptions>? configure = null)
    {
        // ... configure Ring Kernel services
        return this;
    }

    public DotComputeBuilder WithLinq()
    {
        // ... configure LINQ extensions
        return this;
    }
}

// Usage
services.AddDotCompute(builder => builder
    .WithCuda(cuda => cuda.DeviceIndex = 0)
    .WithRingKernels(ring => ring.EnableTelemetry = true)
    .WithLinq());
```

#### Phase 2: Scoped Service Lifetimes (v0.6.0)

```csharp
// Define clear lifetime expectations
public static class ServiceLifetimes
{
    // Singleton: Shared across all requests
    // - Accelerator instances (expensive to create)
    // - Device discovery services
    // - Memory pools

    // Scoped: Per-operation
    // - Kernel execution contexts
    // - Profiling sessions
    // - Ring kernel runtimes

    // Transient: Per-resolution
    // - Buffer instances
    // - Compiled kernels
    // - Options validators
}

// Enforce with registration
builder.Services.AddSingleton<IAccelerator, CudaAccelerator>();
builder.Services.AddScoped<IKernelExecutionContext, CudaKernelExecutionContext>();
builder.Services.AddTransient<IBuffer<float>, CudaBuffer<float>>();
```

#### Phase 3: Factory Pattern Standardization (v0.7.0)

```csharp
// Standard factory interface
public interface IAcceleratorFactory
{
    ValueTask<IAccelerator> CreateAsync(
        BackendType backend,
        AcceleratorOptions options,
        CancellationToken ct = default);
}

// Named factory for multiple instances
public interface INamedBufferFactory
{
    IBuffer<T> Create<T>(string name, int length, BufferFlags flags)
        where T : unmanaged;

    IBuffer<T>? TryGet<T>(string name) where T : unmanaged;
}

// Pool-aware factory
public interface IPooledBufferFactory
{
    IBuffer<T> Rent<T>(int minLength) where T : unmanaged;
    void Return<T>(IBuffer<T> buffer) where T : unmanaged;
}
```

---

## 4. API Surface Definition

### Goal
Define clear, versioned API surfaces for public consumption vs internal implementation.

### Current Issues
- Many internal types are accidentally public
- API surface not explicitly defined
- Breaking changes not tracked

### Proposed Structure

#### Phase 1: API Surface Project (v0.6.0)

```
src/
├── DotCompute.PublicApi/           # NEW: Public API definitions only
│   ├── DotCompute.PublicApi.csproj
│   ├── Kernel/
│   │   ├── IKernelBuilder.cs
│   │   ├── IKernelExecutor.cs
│   │   └── KernelAttribute.cs
│   ├── Memory/
│   │   ├── IBuffer.cs
│   │   ├── IBufferFactory.cs
│   │   └── BufferFlags.cs
│   ├── Accelerator/
│   │   ├── IAccelerator.cs
│   │   ├── IAcceleratorFactory.cs
│   │   └── BackendType.cs
│   └── RingKernel/
│       ├── IRingKernelRuntime.cs
│       ├── IRingKernelMessage.cs
│       └── RingKernelAttribute.cs
```

#### Phase 2: Internal Visibility (v0.6.0)

```csharp
// Use InternalsVisibleTo for test access
[assembly: InternalsVisibleTo("DotCompute.Core.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")] // For mocking

// Mark internal types explicitly
namespace DotCompute.Core.Internal;

internal sealed class KernelCompilationPipeline
{
    // This is an implementation detail, not API
}

// Use public for API surface only
namespace DotCompute.Core;

public interface IKernelCompiler
{
    // This is part of the public API
}
```

#### Phase 3: API Compatibility Analyzer (v0.7.0)

```xml
<!-- Add to Directory.Build.props -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4" />
</ItemGroup>
```

```
# PublicAPI.Shipped.txt - tracks stable API
DotCompute.Core.IAccelerator
DotCompute.Core.IAccelerator.CompileAsync(string, DotCompute.Core.CompilationOptions)
DotCompute.Core.IBuffer<T>

# PublicAPI.Unshipped.txt - tracks upcoming API
~DotCompute.Core.IAccelerator.CompileAsync(DotCompute.Core.KernelSource, DotCompute.Core.CompilationOptions)
```

---

## 5. Module Boundary Enforcement

### Goal
Prevent invalid cross-module dependencies using architecture tests.

### Implementation

#### Phase 1: Architecture Tests (v0.6.0)

```csharp
// Use NetArchTest for architecture validation
using NetArchTest.Rules;

public class ArchitectureTests
{
    [Fact]
    public void Core_ShouldNotDependOn_Backends()
    {
        var result = Types.InAssembly(typeof(IAccelerator).Assembly)
            .ShouldNot()
            .HaveDependencyOn("DotCompute.Backend.Cuda")
            .And()
            .ShouldNot()
            .HaveDependencyOn("DotCompute.Backend.Metal")
            .And()
            .ShouldNot()
            .HaveDependencyOn("DotCompute.Backend.OpenCL")
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Core has invalid dependencies: {string.Join(", ", result.FailingTypes.Select(t => t.Name))}");
    }

    [Fact]
    public void Backends_ShouldNotDependOn_EachOther()
    {
        var cudaTypes = Types.InAssembly(typeof(CudaAccelerator).Assembly);

        var result = cudaTypes
            .ShouldNot()
            .HaveDependencyOn("DotCompute.Backend.Metal")
            .And()
            .ShouldNot()
            .HaveDependencyOn("DotCompute.Backend.OpenCL")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void Extensions_ShouldOnlyDependOn_Core()
    {
        var linqTypes = Types.InAssembly(typeof(ComputeQueryable).Assembly);

        var result = linqTypes
            .Should()
            .OnlyHaveDependenciesOn(
                "DotCompute.Abstractions",
                "DotCompute.Core",
                "DotCompute.Memory",
                "System",
                "Microsoft")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
```

#### Phase 2: Layer Constraint Enforcement (v0.6.0)

```
Allowed Dependencies:

Layer 4 (Runtime) →
├── Layer 3 (Extensions) ✓
├── Layer 2 (Backends) ✓
└── Layer 1 (Core) ✓

Layer 3 (Extensions) →
├── Layer 1 (Core) ✓
└── Layer 2 (Backends) ✓ (optional, via abstraction)

Layer 2 (Backends) →
└── Layer 1 (Core) ✓

Layer 1 (Core) →
└── No dependencies on other layers ✓
```

---

## 6. Naming Convention Standardization

### Current Issues
- Inconsistent suffixes: `*Manager`, `*Service`, `*Handler`, `*Provider`
- Some classes use both Hungarian and PascalCase
- Interface naming not always `I` prefixed consistently

### Naming Standards

#### Classes

| Type | Convention | Example |
|------|------------|---------|
| Interface | `I` + PascalCase | `IAccelerator`, `IBuffer` |
| Abstract Base | `*Base` | `BufferBase<T>` |
| Factory | `*Factory` | `AcceleratorFactory` |
| Service | `*Service` | `KernelExecutionService` |
| Adapter | `*Adapter` | `CudaKernelCompilationAdapter` |
| Provider | `*Provider` (readonly) | `DeviceInfoProvider` |
| Manager | `*Manager` (lifecycle) | `MemoryPoolManager` |
| Options | `*Options` | `CudaAcceleratorOptions` |
| Configuration | `*Configuration` | `RingKernelConfiguration` |
| Builder | `*Builder` | `KernelBuilder` |
| Context | `*Context` | `ExecutionContext` |

#### Methods

| Pattern | Convention | Example |
|---------|------------|---------|
| Async method | `*Async` suffix | `CompileAsync`, `ExecuteAsync` |
| Try pattern | `Try*` prefix | `TryGet`, `TryParse` |
| Factory method | `Create*` | `CreateAccelerator` |
| Boolean property | `Is*`, `Has*`, `Can*` | `IsValid`, `HasCapability` |

### Refactoring Plan

```csharp
// BEFORE
public class CudaStreamPool { }
public class CudaStreamHandler { }
public class CudaStreamService { }  // All doing similar things

// AFTER
public class CudaStreamManager { }  // Single, clear responsibility
```

---

## 7. Error Handling Architecture

### Current State
- 33 exception types with some overlap
- Inconsistent error codes
- No structured error information

### Proposed Architecture

#### Phase 1: Structured Error Results (v0.6.0)

```csharp
// Result pattern for expected failures
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public ComputeError? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(ComputeError error) => new(false, default, error);
}

public sealed record ComputeError(
    ErrorCode Code,
    string Message,
    string? Details = null,
    Exception? InnerException = null);

public enum ErrorCode
{
    // Compilation Errors (1xxx)
    CompilationFailed = 1001,
    SyntaxError = 1002,
    UnsupportedOperation = 1003,

    // Memory Errors (2xxx)
    OutOfMemory = 2001,
    AllocationFailed = 2002,
    TransferFailed = 2003,

    // Execution Errors (3xxx)
    KernelLaunchFailed = 3001,
    Timeout = 3002,
    DeviceError = 3003,

    // Configuration Errors (4xxx)
    InvalidConfiguration = 4001,
    MissingBackend = 4002
}
```

#### Phase 2: Exception Hierarchy Cleanup (v0.6.0)

```csharp
// Base exception with error code
public class ComputeException : Exception
{
    public ErrorCode Code { get; }
    public string? Details { get; }

    public ComputeException(ErrorCode code, string message, string? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }
}

// Specific exceptions for different categories
public class CompilationException : ComputeException
{
    public string? SourceLocation { get; }
    public IReadOnlyList<string>? DiagnosticMessages { get; }
}

public class MemoryException : ComputeException
{
    public long RequestedBytes { get; }
    public long AvailableBytes { get; }
}

public class ExecutionException : ComputeException
{
    public string? KernelName { get; }
    public (int x, int y, int z)? GridDimensions { get; }
    public (int x, int y, int z)? BlockDimensions { get; }
}
```

#### Phase 3: Error Logging Standards (v0.7.0)

```csharp
// Structured logging for errors
public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Error,
        Message = "Kernel execution failed: {KernelName} on device {DeviceId}. Grid: {GridDim}, Block: {BlockDim}")]
    public static partial void LogKernelExecutionFailed(
        this ILogger logger,
        string kernelName,
        int deviceId,
        string gridDim,
        string blockDim,
        Exception ex);
}
```

---

## 8. Testability Improvements

### Goal
Make all components easily testable through dependency injection and clear contracts.

### Implementation

#### Phase 1: Mock-Friendly Interfaces (v0.6.0)

```csharp
// BEFORE: Hard to mock
public sealed class CudaDevice
{
    public async Task<DeviceInfo> GetInfoAsync()
    {
        // Direct CUDA API call
        return await NativeMethods.cuDeviceGetProperties(...);
    }
}

// AFTER: Easy to mock
public interface ICudaDeviceWrapper
{
    ValueTask<DeviceInfo> GetInfoAsync(int deviceId, CancellationToken ct = default);
}

public sealed class CudaDeviceWrapper : ICudaDeviceWrapper
{
    public async ValueTask<DeviceInfo> GetInfoAsync(int deviceId, CancellationToken ct = default)
    {
        return await NativeMethods.cuDeviceGetProperties(deviceId);
    }
}

// Test can now mock the wrapper
var mockDevice = new Mock<ICudaDeviceWrapper>();
mockDevice.Setup(d => d.GetInfoAsync(0, default))
    .ReturnsAsync(new DeviceInfo { Name = "Test GPU" });
```

#### Phase 2: Test Doubles Factory (v0.6.0)

```csharp
namespace DotCompute.Testing;

public static class TestDoubles
{
    public static IAccelerator CreateFakeAccelerator(
        Action<FakeAcceleratorBuilder>? configure = null)
    {
        var builder = new FakeAcceleratorBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }

    public static IBuffer<T> CreateFakeBuffer<T>(
        int length,
        Func<int, T>? initializer = null) where T : unmanaged
    {
        return new FakeBuffer<T>(length, initializer);
    }

    public static IRingKernelRuntime CreateFakeRingKernelRuntime(
        Action<FakeRingKernelBuilder>? configure = null)
    {
        var builder = new FakeRingKernelBuilder();
        configure?.Invoke(builder);
        return builder.Build();
    }
}

// Usage in tests
[Fact]
public async Task Orchestrator_ExecutesKernel_WhenAcceleratorAvailable()
{
    var accelerator = TestDoubles.CreateFakeAccelerator(b => b
        .WithDevice("Test GPU", computeCapability: 8.6)
        .WithMemory(8L * 1024 * 1024 * 1024)); // 8GB

    var orchestrator = new ComputeOrchestrator(accelerator);
    var result = await orchestrator.ExecuteAsync("VectorAdd", input);

    Assert.True(result.IsSuccess);
}
```

#### Phase 3: Integration Test Helpers (v0.7.0)

```csharp
namespace DotCompute.Testing.Integration;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IServiceProvider Services { get; private set; }
    protected IAccelerator Accelerator { get; private set; }

    public virtual async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
        Accelerator = await Services.GetRequiredService<IAcceleratorFactory>()
            .CreateAsync(GetBackendType());
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddDotCompute(b => ConfigureDotCompute(b));
    }

    protected virtual void ConfigureDotCompute(DotComputeBuilder builder)
    {
        // Default: CPU backend for CI
        builder.WithCpu();
    }

    protected virtual BackendType GetBackendType() => BackendType.CPU;

    public virtual async Task DisposeAsync()
    {
        if (Accelerator is IAsyncDisposable ad)
            await ad.DisposeAsync();
    }
}

// GPU-specific test base (skipped in CI without GPU)
public abstract class CudaIntegrationTestBase : IntegrationTestBase
{
    protected override BackendType GetBackendType() => BackendType.CUDA;

    public override async Task InitializeAsync()
    {
        Skip.If(!CudaDeviceDetector.HasCudaDevice(),
            "No CUDA device available");
        await base.InitializeAsync();
    }
}
```

---

## 9. Documentation Architecture

### Goal
Ensure architecture decisions are documented and discoverable.

### Implementation

#### Phase 1: Architecture Decision Records (v0.6.0)

```
docs/architecture/decisions/
├── 0001-use-ports-and-adapters.md
├── 0002-unified-buffer-hierarchy.md
├── 0003-ring-kernel-design.md
├── 0004-native-aot-compatibility.md
├── 0005-memory-pooling-strategy.md
└── TEMPLATE.md
```

```markdown
<!-- TEMPLATE.md -->
# ADR-XXXX: Title

## Status
Proposed | Accepted | Deprecated | Superseded by ADR-YYYY

## Context
What is the issue that we're seeing that is motivating this decision?

## Decision
What is the change that we're proposing and/or doing?

## Consequences
What becomes easier or more difficult to do because of this change?

## Alternatives Considered
What other options were evaluated?
```

#### Phase 2: Living Architecture Diagrams (v0.6.0)

```csharp
// Generate architecture diagrams from code
// Using Structurizr or C4 model

var workspace = new Workspace("DotCompute", "Universal Compute Framework");
var model = workspace.Model;

var dotcompute = model.AddSoftwareSystem("DotCompute", "GPU Compute Framework");

var core = dotcompute.AddContainer("Core", "Abstractions and Base", ".NET 9");
var cuda = dotcompute.AddContainer("CUDA Backend", "NVIDIA GPU", ".NET 9");
var metal = dotcompute.AddContainer("Metal Backend", "Apple GPU", ".NET 9");
var opencl = dotcompute.AddContainer("OpenCL Backend", "Cross-platform", ".NET 9");

cuda.Uses(core, "Implements");
metal.Uses(core, "Implements");
opencl.Uses(core, "Implements");
```

---

## 10. Implementation Timeline

### v0.6.0 (3 months)
- [ ] Define core ports (interfaces)
- [ ] Implement fluent DI builder
- [ ] Add architecture tests
- [ ] Standardize naming conventions
- [ ] Create ADR template and initial records

### v0.7.0 (3 months)
- [ ] Implement backend adapters
- [ ] Add API surface project
- [ ] Implement structured error handling
- [ ] Add test doubles factory
- [ ] Complete naming convention refactoring

### v0.8.0 (3 months)
- [ ] Refactor accelerators to use adapters
- [ ] Add API compatibility analyzer
- [ ] Complete error handling migration
- [ ] Add integration test helpers

### v1.0.0 (3 months)
- [ ] Freeze public API surface
- [ ] Complete architecture documentation
- [ ] Performance validation
- [ ] Security review

---

## 11. Success Metrics

| Metric | Current | v0.7.0 | v1.0.0 |
|--------|---------|--------|--------|
| Architecture test coverage | 0% | 80% | 100% |
| Interface/implementation ratio | 0.3 | 0.6 | 0.8 |
| Circular dependencies | 5 | 2 | 0 |
| ADRs documented | 0 | 10 | 20 |
| Breaking API changes (per release) | Unknown | <5 | 0 |

---

**Document Owner**: Core Architecture Team
**Review Cycle**: Quarterly
**Next Review**: April 2026
