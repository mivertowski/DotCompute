# DotCompute Roadmap: Consolidation & Technical Debt

**Document Version**: 1.0
**Last Updated**: January 2026
**Target Version**: v0.6.0 - v0.8.0
**Status**: Strategic Planning

---

## Executive Summary

This document outlines a comprehensive strategy to reduce technical debt, consolidate duplicated code, and improve maintainability across the DotCompute codebase. The current state shows 319 "god files" (files >1,000 lines with 8+ types), 30+ memory buffer implementations, and duplicated patterns across backends.

**Key Objectives**:
- Reduce god files from 319 to <200
- Consolidate memory buffer hierarchy from 30+ to <15 implementations
- Extract common backend patterns into shared abstractions
- Improve code coverage from 94% to 98%
- Reduce codebase complexity by 25%

---

## 1. God Files Elimination

### Current State
- **319 god files** remaining (reduced from 328)
- Average size: ~1,200 lines per file
- Types scattered across responsibilities
- High cognitive load for contributors

### Priority Targets

#### Tier 1: Critical (>1,500 lines, 10+ types)
| File | Lines | Types | Backend | Priority |
|------|-------|-------|---------|----------|
| CudaDevice.cs | 29,171 | 50+ | CUDA | Critical |
| OptimizedUnifiedBuffer.cs | 42,541 | 30+ | Memory | Critical |
| HighPerformanceObjectPool.cs | 19,662 | 20+ | Memory | Critical |
| MetalAccelerator.cs | 853 | 12 | Metal | High |
| CudaAccelerator.cs | 425 | 8 | CUDA | High |

#### Tier 2: High Priority (1,000-1,500 lines)
| File | Lines | Types | Backend |
|------|-------|-------|---------|
| OpenCLKernelPipeline.cs | ~1,350 | 12 | OpenCL |
| OpenCLPerformanceMonitor.cs | ~1,200 | 10 | OpenCL |
| MetalMemoryManager.cs | ~1,100 | 8 | Metal |
| CudaMemoryAllocator.cs | ~1,050 | 8 | CUDA |

### Refactoring Strategy

#### Phase 1: Extract Type Definitions (v0.6.0)
```
Target: Reduce 50 god files
Pattern: [Feature].cs → [Feature].cs + Types/[Feature]*.cs

Example:
CudaDevice.cs (29,171 lines)
├── CudaDevice.cs (5,000 lines - core logic)
├── Types/CudaDeviceEnums.cs
├── Types/CudaDeviceTypes.cs
├── Types/CudaCapabilityTypes.cs
├── Types/CudaMemoryTypes.cs
├── Types/CudaExecutionTypes.cs
└── Types/CudaProfilingTypes.cs
```

#### Phase 2: Partial Class Consolidation (v0.6.0)
```
Pattern: Keep related partial files in same directory

Before:
├── CudaAccelerator.cs
├── CudaAccelerator.Health.cs
├── CudaAccelerator.Profiling.cs
└── Integration/CudaAccelerator.Memory.cs  # Different directory!

After:
├── Accelerator/
│   ├── CudaAccelerator.cs
│   ├── CudaAccelerator.Health.cs
│   ├── CudaAccelerator.Profiling.cs
│   ├── CudaAccelerator.Memory.cs
│   └── CudaAccelerator.Barriers.cs
```

#### Phase 3: Extract Shared Abstractions (v0.7.0)
```csharp
// NEW: Shared base for all accelerator health implementations
public abstract class AcceleratorHealthBase<TAccelerator>
    where TAccelerator : IAccelerator
{
    public abstract ValueTask<HealthSnapshot> GetHealthSnapshotAsync();
    public abstract ValueTask<ProfilingSnapshot> GetProfilingSnapshotAsync();

    // Shared logic (currently duplicated across backends)
    protected virtual HealthStatus DetermineStatus(HealthMetrics metrics) { }
    protected virtual void LogHealthChange(HealthStatus previous, HealthStatus current) { }
}
```

### Metrics & Tracking

| Metric | Current | v0.6.0 Target | v0.7.0 Target | v0.8.0 Target |
|--------|---------|---------------|---------------|---------------|
| God files (>1000 lines) | 319 | 269 | 219 | <200 |
| Avg file size (top 50) | 1,847 | 1,200 | 800 | 600 |
| Files with >10 types | 45 | 25 | 15 | <10 |

---

## 2. Memory Buffer Consolidation

### Current State Analysis

The codebase has **30+ memory buffer implementations** with overlapping responsibilities:

```
Interface Layer:
├── IUnifiedMemoryBuffer
└── IUnifiedMemoryBuffer<T>

Abstract Bases (8):
├── BaseMemoryBuffer<T>
├── BaseDeviceBuffer<T>
├── BaseUnifiedBuffer<T>
├── BasePinnedBuffer<T>
├── BasePooledBuffer<T>
├── MemoryBuffer (internal)
├── HighPerformanceMemoryBuffer
└── ProductionMemoryBuffer

Core Implementations (6):
├── UnifiedBuffer<T>
├── UnifiedBufferSlice<T>
├── UnifiedBufferView<TOriginal, TView>
├── OptimizedUnifiedBuffer<T>
├── TypedMemoryBufferWrapper
└── MemoryBufferAdapter<T>

Backend-Specific (16+):
├── CPU: CpuMemoryBuffer, CpuMemoryBufferTyped, CpuMemoryBufferSlice
├── CUDA: CudaMemoryBuffer, CudaMemoryBufferView, CudaRawMemoryBuffer, SimpleCudaUnifiedMemoryBuffer
├── Metal: MetalMemoryBuffer, MetalMemoryBufferView, MetalPinnedMemoryBuffer, MetalUnifiedMemoryBuffer
└── OpenCL: OpenCLMemoryBuffer, OpenCLManagedBuffer, OpenCLSharedBuffer
```

### Consolidation Strategy

#### Phase 1: Establish Canonical Hierarchy (v0.6.0)

```csharp
// TIER 1: Core Interface
public interface IBuffer<T> : IDisposable, IAsyncDisposable where T : unmanaged
{
    int Length { get; }
    BufferLocation Location { get; }

    Span<T> AsSpan();
    Memory<T> AsMemory();

    ValueTask CopyToAsync(IBuffer<T> destination, CancellationToken ct = default);
    ValueTask SynchronizeAsync(CancellationToken ct = default);
}

// TIER 2: Abstract Base (consolidates 8 abstract classes into 1)
public abstract class BufferBase<T> : IBuffer<T> where T : unmanaged
{
    // Common implementation for all buffers
    protected readonly IMemoryPool<T>? _pool;
    protected readonly ILogger _logger;

    // Template methods for backend-specific behavior
    protected abstract Span<T> GetSpanCore();
    protected abstract ValueTask CopyToDeviceAsync(nint devicePtr, int length);
    protected abstract ValueTask CopyFromDeviceAsync(nint devicePtr, int length);
}

// TIER 3: Backend Implementations (4 total, down from 16+)
public sealed class CpuBuffer<T> : BufferBase<T> { }
public sealed class CudaBuffer<T> : BufferBase<T> { }
public sealed class MetalBuffer<T> : BufferBase<T> { }
public sealed class OpenCLBuffer<T> : BufferBase<T> { }

// TIER 4: Views & Slices (shared, not per-backend)
public readonly ref struct BufferSlice<T> { }
public sealed class BufferView<TSource, TTarget> : IBuffer<TTarget> { }
```

#### Phase 2: Deprecation Path (v0.6.0-v0.7.0)

```csharp
// Mark legacy types as obsolete
[Obsolete("Use BufferBase<T>. This type will be removed in v0.8.0")]
public abstract class BaseMemoryBuffer<T> : BufferBase<T> { }

[Obsolete("Use CudaBuffer<T>. This type will be removed in v0.8.0")]
public class CudaMemoryBuffer : CudaBuffer<byte> { }
```

#### Phase 3: Complete Migration (v0.8.0)

```
Remove deprecated types:
- 8 abstract bases → 1 BufferBase<T>
- 16 backend buffers → 4 backend implementations + 2 shared views
- 6 core implementations → 1 unified implementation + pooling extension
```

### Memory Buffer Selection Guide (Document)

Create `docs/guides/buffer-selection.md`:

| Scenario | Recommended Buffer | Reason |
|----------|-------------------|--------|
| GPU kernel input | `CudaBuffer<T>` / `MetalBuffer<T>` | Native device memory |
| CPU-GPU transfer | `UnifiedBuffer<T>` | Automatic migration |
| High-frequency alloc | Pooled variant | 90% allocation reduction |
| Read-only view | `BufferSlice<T>` | Zero-copy, no allocation |
| Type reinterpretation | `BufferView<S,T>` | Memory aliasing |

---

## 3. Backend Pattern Extraction

### Current Duplication Analysis

**Health/Profiling/Reset patterns duplicated across 4 backends:**

| Pattern | CUDA | Metal | OpenCL | CPU | Lines Duplicated |
|---------|------|-------|--------|-----|------------------|
| Health snapshot | Yes | Yes | Yes | Partial | ~300/backend |
| Profiling snapshot | Yes | Yes | Yes | Partial | ~400/backend |
| Device reset | Yes | Yes | Yes | N/A | ~200/backend |
| Memory statistics | Yes | Yes | Yes | Yes | ~150/backend |

**Total duplicated code: ~4,200 lines** (could be ~1,200 with shared base)

### Extraction Plan

#### Phase 1: Shared Health Infrastructure (v0.6.0)

```csharp
// NEW: Shared health monitoring base
namespace DotCompute.Core.Health;

public abstract class AcceleratorHealthMonitor<TMetrics> : IHealthMonitor
    where TMetrics : struct, IHealthMetrics
{
    private readonly ILogger _logger;
    private readonly HealthThresholds _thresholds;
    private HealthStatus _currentStatus = HealthStatus.Healthy;

    // Shared logic (currently duplicated ~300 lines per backend)
    public async ValueTask<HealthSnapshot> GetSnapshotAsync()
    {
        var metrics = await CollectMetricsAsync();
        var status = EvaluateHealth(metrics);
        LogStatusChange(status);
        return new HealthSnapshot(metrics, status, DateTimeOffset.UtcNow);
    }

    // Backend-specific collection
    protected abstract ValueTask<TMetrics> CollectMetricsAsync();
    protected abstract HealthStatus EvaluateHealth(TMetrics metrics);
}

// Backend implementations become thin wrappers
public sealed class CudaHealthMonitor : AcceleratorHealthMonitor<CudaHealthMetrics>
{
    protected override async ValueTask<CudaHealthMetrics> CollectMetricsAsync()
    {
        // Only CUDA-specific logic here (~50 lines vs current ~300)
        var temperature = await _nvml.GetTemperatureAsync();
        var memory = await _device.GetMemoryInfoAsync();
        return new CudaHealthMetrics(temperature, memory);
    }
}
```

#### Phase 2: Shared Profiling Infrastructure (v0.6.0)

```csharp
// NEW: Shared profiling base
namespace DotCompute.Core.Profiling;

public abstract class AcceleratorProfiler<TProfile> : IProfiler
    where TProfile : struct, IProfilingProfile
{
    // Shared session management (~200 lines currently duplicated)
    public async ValueTask<ProfilingSession> StartSessionAsync(ProfilingOptions options)
    {
        ValidateOptions(options);
        var session = CreateSession(options);
        await InitializeBackendProfilingAsync(session);
        return session;
    }

    protected abstract ValueTask InitializeBackendProfilingAsync(ProfilingSession session);
    protected abstract ValueTask<TProfile> CollectProfileAsync(ProfilingSession session);
}
```

#### Phase 3: Shared Ring Kernel Configuration (v0.7.0)

Currently each backend has its own `RingKernelConfig`:
- `CudaRingKernelConfig`
- `MetalRingKernelConfig`
- `OpenCLRingKernelConfig`

```csharp
// NEW: Unified ring kernel configuration with backend extensions
namespace DotCompute.Core.RingKernels;

public record RingKernelConfiguration
{
    // Common properties (currently duplicated)
    public required string KernelId { get; init; }
    public int InputQueueSize { get; init; } = 1024;
    public int OutputQueueSize { get; init; } = 1024;
    public ExecutionMode Mode { get; init; } = ExecutionMode.EventDriven;
    public bool EnableTelemetry { get; init; } = true;

    // Backend-specific extensions via polymorphism
    public IRingKernelBackendConfig? BackendConfig { get; init; }
}

public sealed record CudaRingKernelBackendConfig : IRingKernelBackendConfig
{
    public int BlockDimX { get; init; } = 256;
    public int GridDimX { get; init; } = 1;
    public CudaStream? PreferredStream { get; init; }
}
```

---

## 4. Configuration Class Consolidation

### Current State
- **59 Options/Configuration classes** across the codebase
- Many have similar structures with defaults
- No inheritance or composition hierarchy

### Consolidation Strategy

#### Phase 1: Establish Options Hierarchy (v0.6.0)

```csharp
// Base options for all accelerators
public abstract record AcceleratorOptions
{
    public int DeviceIndex { get; init; } = 0;
    public bool EnableProfiling { get; init; } = false;
    public bool EnableHealthMonitoring { get; init; } = true;
    public LogLevel MinimumLogLevel { get; init; } = LogLevel.Information;
}

// Backend-specific options inherit from base
public sealed record CudaAcceleratorOptions : AcceleratorOptions
{
    public CudaCompilationOptions Compilation { get; init; } = new();
    public CudaMemoryOptions Memory { get; init; } = new();
}

// Nested options for logical grouping
public sealed record CudaCompilationOptions
{
    public bool GenerateDebugInfo { get; init; } = false;
    public OptimizationLevel Optimization { get; init; } = OptimizationLevel.O3;
    public string? TargetComputeCapability { get; init; }
}
```

#### Phase 2: Configuration Validation (v0.6.0)

```csharp
// Add validation attributes
public sealed record CudaMemoryOptions
{
    [Range(1024, int.MaxValue)]
    public int PoolInitialSize { get; init; } = 64 * 1024 * 1024; // 64MB

    [Range(0.0, 1.0)]
    public double PoolGrowthFactor { get; init; } = 0.25;
}

// Implement IValidatableObject for complex validation
public sealed record RingKernelConfiguration : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (InputQueueSize is not (> 0 and <= 1024 * 1024))
            yield return new("InputQueueSize must be 1-1048576", new[] { nameof(InputQueueSize) });
    }
}
```

---

## 5. Exception Hierarchy Cleanup

### Current State
- **33 exception classes** across the codebase
- Some overlap in purpose
- Inconsistent naming conventions

### Consolidation Plan

```csharp
// BEFORE: Fragmented exception hierarchy
ComputeException
├── CompilationException
├── KernelCompilationException    // Overlaps with above
├── DeviceException
├── CudaDeviceException           // Could extend DeviceException
├── MemoryException
├── BufferException               // Overlaps with above
└── ... (25 more)

// AFTER: Clean hierarchy
ComputeException (base)
├── CompilationException
│   ├── KernelCompilationException
│   └── ShaderCompilationException
├── DeviceException
│   ├── DeviceNotAvailableException
│   ├── DeviceOutOfMemoryException
│   └── DeviceUnsupportedOperationException
├── MemoryException
│   ├── AllocationException
│   ├── TransferException
│   └── CoherenceException
├── ExecutionException
│   ├── KernelExecutionException
│   ├── TimeoutException
│   └── SynchronizationException
└── ConfigurationException
    ├── InvalidOptionsException
    └── MissingBackendException
```

---

## 6. Test Infrastructure Consolidation

### Current State
- **486 test files** across 108 directories
- Shared utilities split across multiple projects
- Some test duplication across backends

### Consolidation Strategy

#### Phase 1: Unified Test Base Classes (v0.6.0)

```csharp
// NEW: Shared test base for all backend tests
namespace DotCompute.Tests.Shared;

public abstract class AcceleratorTestBase<TAccelerator> : IAsyncLifetime
    where TAccelerator : IAccelerator
{
    protected TAccelerator Accelerator { get; private set; }
    protected ILoggerFactory LoggerFactory { get; }

    // Shared setup/teardown
    public async Task InitializeAsync()
    {
        Accelerator = await CreateAcceleratorAsync();
        await WarmupAsync();
    }

    protected abstract Task<TAccelerator> CreateAcceleratorAsync();

    // Shared test patterns
    protected async Task AssertKernelExecutesCorrectly<T>(
        string kernelName,
        T[] input,
        T[] expectedOutput) where T : unmanaged
    {
        // Common assertion logic
    }
}

// Backend tests become focused
public class CudaVectorAddTests : AcceleratorTestBase<CudaAccelerator>
{
    protected override Task<CudaAccelerator> CreateAcceleratorAsync()
        => CudaAccelerator.CreateAsync(_loggerFactory);

    [Fact]
    public async Task VectorAdd_1MElements_ProducesCorrectResult()
    {
        // Test uses shared infrastructure
        await AssertKernelExecutesCorrectly("VectorAdd", input, expected);
    }
}
```

#### Phase 2: Backend-Agnostic Test Suite (v0.7.0)

```csharp
// Tests that run on ALL backends
[Theory]
[MemberData(nameof(AllBackends))]
public async Task BufferAllocation_ReturnsValidMemory(BackendType backend)
{
    using var accelerator = await CreateAccelerator(backend);
    using var buffer = await accelerator.AllocateAsync<float>(1024);

    Assert.Equal(1024, buffer.Length);
    Assert.True(buffer.IsValid);
}

public static IEnumerable<object[]> AllBackends => new[]
{
    new object[] { BackendType.CPU },
    new object[] { BackendType.CUDA },
    new object[] { BackendType.Metal },
    new object[] { BackendType.OpenCL }
};
```

---

## 7. Documentation Consolidation

### Current Issues
- 3 blog articles in docs root (should be in `/blogs`)
- Status documents scattered (should be in `/internal`)
- CONTRIBUTING.md 90% incomplete

### Cleanup Actions

#### Phase 1: File Reorganization (v0.6.0)

```bash
# Move misplaced files
docs/cmhl24l19000102jp61h0dz1k.md → docs/blog/2025-11-...md
docs/cmhnacp8y000d02ju3kvge7gg.md → docs/blog/2025-11-...md
docs/pagerank-phase-6-status.md → docs/internal/status/...md

# Consolidate status documents
docs/internal/status/
├── ring-kernels.md
├── linq-extensions.md
├── metal-backend.md
└── cuda-backend.md
```

#### Phase 2: Complete CONTRIBUTING.md (v0.6.0)

```markdown
# Contributing to DotCompute

## Development Setup
- Prerequisites: .NET 9 SDK, CUDA Toolkit 12.0+
- Clone, build, test instructions
- WSL2 configuration for Windows

## Coding Standards
- C# coding conventions
- CUDA/MSL coding conventions
- Documentation requirements

## Testing Requirements
- Unit test coverage expectations
- Hardware test annotations
- CI/CD pipeline details

## Pull Request Process
- Branch naming: `feature/`, `fix/`, `docs/`
- Commit message format
- Code review checklist
```

---

## 8. Build & CI Consolidation

### Current State
- Multiple build scripts with overlapping functionality
- Some duplication between CI and local builds

### Consolidation Plan

```bash
# Unified script structure
scripts/
├── build.sh              # Main build script (consolidated)
├── test.sh               # Test runner (wraps run-tests.sh)
├── ci/
│   ├── setup-environment.sh
│   ├── run-ci-build.sh
│   └── run-ci-tests.sh
├── dev/
│   ├── format.sh
│   ├── lint.sh
│   └── generate-docs.sh
└── utils/
    ├── detect-cuda.sh
    └── setup-wsl2.sh
```

---

## 9. Implementation Timeline

### v0.6.0 (3 months)
- [ ] Eliminate 50 god files (319 → 269)
- [ ] Establish canonical buffer hierarchy
- [ ] Extract shared health/profiling infrastructure
- [ ] Complete CONTRIBUTING.md
- [ ] Reorganize documentation structure

### v0.7.0 (3 months)
- [ ] Eliminate 50 more god files (269 → 219)
- [ ] Complete buffer deprecation path
- [ ] Implement backend-agnostic test suite
- [ ] Consolidate configuration classes

### v0.8.0 (3 months)
- [ ] Final god file elimination (<200 target)
- [ ] Remove deprecated buffer types
- [ ] Complete exception hierarchy cleanup
- [ ] Achieve 98% code coverage

---

## 10. Success Metrics

| Metric | Current | v0.6.0 | v0.7.0 | v0.8.0 |
|--------|---------|--------|--------|--------|
| God files | 319 | 269 | 219 | <200 |
| Buffer implementations | 30+ | 20 | 12 | <15 |
| Duplicated backend code | ~4,200 LOC | ~2,500 LOC | ~1,500 LOC | <1,200 LOC |
| Code coverage | 94% | 95% | 97% | 98% |
| Configuration classes | 59 | 45 | 35 | 30 |
| Exception classes | 33 | 28 | 22 | 18 |

---

## Appendix: Priority File List

### Immediate Action (v0.6.0)
1. `CudaDevice.cs` - 29,171 lines (highest priority)
2. `OptimizedUnifiedBuffer.cs` - 42,541 lines
3. `HighPerformanceObjectPool.cs` - 19,662 lines
4. `MetalAccelerator.cs` - 853 lines
5. `CudaAccelerator.cs` - 425 lines

### High Priority (v0.6.0-v0.7.0)
6. `OpenCLKernelPipeline.cs` - ~1,350 lines
7. `OpenCLPerformanceMonitor.cs` - ~1,200 lines
8. `MetalMemoryManager.cs` - ~1,100 lines
9. `CudaMemoryAllocator.cs` - ~1,050 lines
10. `CudaStreamManager.cs` - 1,180 lines (refactored, verify completion)

---

**Document Owner**: Core Architecture Team
**Review Cycle**: Quarterly
**Next Review**: April 2026
