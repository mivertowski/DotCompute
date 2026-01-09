# DotCompute v1.0.0 Public API

**Status**: 🔒 FROZEN (v1.0.0-rc.1)
**Frozen Date**: January 5, 2026
**Breaking Changes Policy**: None allowed until v2.0.0

---

## API Surface Overview

| Module | Public Types | Interfaces | Classes | Structs | Enums |
|--------|--------------|------------|---------|---------|-------|
| DotCompute.Abstractions | 245 | 89 | 78 | 45 | 33 |
| DotCompute.Core | 312 | 42 | 198 | 38 | 34 |
| DotCompute.Memory | 48 | 12 | 24 | 8 | 4 |
| DotCompute.Runtime | 156 | 28 | 96 | 18 | 14 |
| DotCompute.Backends.CUDA | 187 | 18 | 124 | 28 | 17 |
| DotCompute.Backends.OpenCL | 98 | 12 | 62 | 14 | 10 |
| DotCompute.Backends.Metal | 76 | 8 | 48 | 12 | 8 |
| DotCompute.Backends.CPU | 34 | 4 | 22 | 4 | 4 |
| DotCompute.Algorithms | 89 | 14 | 52 | 16 | 7 |
| DotCompute.Linq | 67 | 18 | 38 | 6 | 5 |
| **Total** | **1312** | **245** | **742** | **189** | **136** |

---

## Core Interfaces (Frozen)

### Compute Abstractions

```csharp
// Primary compute interfaces - DO NOT MODIFY
public interface IAccelerator : IAsyncDisposable
public interface IComputeOrchestrator : IAsyncDisposable
public interface IUnifiedMemoryManager : IAsyncDisposable
public interface IUnifiedMemoryBuffer<T> : IAsyncDisposable where T : unmanaged
public interface IUnifiedKernelCompiler
public interface ICompiledKernel : IAsyncDisposable
```

### Memory Management

```csharp
// Memory interfaces - DO NOT MODIFY
public interface IMemoryBuffer : IAsyncDisposable
public interface IUnifiedMemoryPool : IAsyncDisposable
public interface IMemoryOrderingProvider
```

### Pipeline System

```csharp
// Pipeline interfaces - DO NOT MODIFY
public interface IPipeline<TInput, TOutput> : IAsyncDisposable
public interface IKernelPipeline : IPipeline<object, object>
public interface IKernelChainBuilder<T>
public interface IPipelineOptimizer
public interface IPipelineProfiler
```

### Ring Kernel System

```csharp
// Ring kernel interfaces - DO NOT MODIFY
public interface IRingKernelRuntime : IAsyncDisposable
public interface IRingKernelMessage
public interface IMessageQueue<T>
public interface IMessageSerializer
```

---

## Stable Enumerations

### Device Types
```csharp
public enum ComputeDeviceType { Unknown, CPU, CUDA, ROCm, Metal, OpenCL, ... }
public enum AcceleratorType { Unknown, CPU, CUDA, OpenCL, Metal, ROCm }
```

### Execution Control
```csharp
public enum ExecutionStatus { Pending, Running, Completed, Failed, Cancelled }
public enum OptimizationLevel { None, Speed, Balanced, Size, MaxPerformance }
public enum MemoryAccessPattern { Sequential, Random, Strided, Broadcast }
```

### Memory Operations
```csharp
public enum MemoryLocation { Host, Device, Unified }
public enum TransferDirection { HostToDevice, DeviceToHost, DeviceToDevice }
public enum MemoryConsistencyModel { Relaxed, Acquire, Release, AcquireRelease }
```

---

## Extension Points

### Plugin System
```csharp
public interface IBackendPlugin { ... }
public interface IAlgorithmPlugin { ... }
public interface IPluginExecutor { ... }
```

### Custom Backends
```csharp
public abstract class BaseAccelerator : IAccelerator { ... }
public abstract class BaseKernelCompiler : IUnifiedKernelCompiler { ... }
public abstract class BaseMemoryManager : IUnifiedMemoryManager { ... }
```

---

## Breaking Change History

### v1.0.0 (January 2026)
- Initial stable release
- All APIs frozen
- No breaking changes from v0.9.0

### Pre-release Breaking Changes (v0.x)

| Version | Change | Migration |
|---------|--------|-----------|
| v0.8.0 | Removed deprecated `IComputeOrchestrator` from Runtime | Use `Abstractions.IComputeOrchestrator` |
| v0.7.0 | `MemoryBuffer` deprecated | Use `UnifiedBuffer<T>` |
| v0.6.0 | `ExecutionPriority` consolidated | Use `Core.ExecutionPriority` |
| v0.5.0 | Ring kernel API stabilized | Update message types |

---

## Compatibility Guarantees

### Binary Compatibility
- All public APIs maintain binary compatibility
- Adding optional parameters is allowed
- Adding new members to interfaces requires default implementations

### Source Compatibility
- Method signatures will not change
- Return types will not change
- Parameter types will not change

### Behavioral Compatibility
- Exception types thrown will not change
- Observable side effects will not change
- Performance characteristics documented

---

## API Review Process

1. **Proposed Changes**: Submit PR with `[api-change]` label
2. **Review**: Core team reviews within 5 business days
3. **Approval**: Requires 2 core maintainer approvals
4. **Documentation**: Update this file and CHANGELOG.md
5. **Announcement**: Major changes announced in release notes

---

## Deprecation Policy

1. **Announcement**: Deprecated APIs announced 2 minor versions ahead
2. **Warning**: `[Obsolete]` attribute with migration guidance
3. **Removal**: Only in major version (v2.0.0, v3.0.0, etc.)
4. **Documentation**: Deprecation reason and migration path documented

---

## Version Compatibility Matrix

| DotCompute | .NET | CUDA | OpenCL | Metal |
|------------|------|------|--------|-------|
| 1.0.x | 9.0+ | 12.0+ | 2.0+ | 2.0+ |
| 1.1.x | 9.0+ | 12.0+ | 2.0+ | 2.0+ |
| 2.0.x | 10.0+ | 13.0+ | 3.0+ | 3.0+ |

---

**Last Updated**: January 5, 2026
**Next Review**: Before v1.1.0 release
