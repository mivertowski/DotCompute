# Breaking Changes Guide

> **DRAFT** — This document describes planned features for a future release. Current version: v0.6.2.

**Version**: 1.0.0
**Last Updated**: January 5, 2026

---

## Overview

This document tracks all breaking changes across DotCompute versions and provides migration guidance for each change.

---

## v1.0.0 (January 2026)

### Breaking Changes from v0.9.x

**None** - v1.0.0 is API-compatible with v0.9.0.

---

## Pre-1.0 Breaking Changes

### v0.8.0 → v0.9.0

#### DC-BC-001: Deprecated Runtime Interfaces Removed

**Change**: Removed backward-compatibility aliases from `DotCompute.Runtime.Services`

| Removed | Replacement |
|---------|-------------|
| `Runtime.Services.IComputeOrchestrator` | `Abstractions.Interfaces.IComputeOrchestrator` |
| `Runtime.Services.Interfaces.IKernelCompiler` | `Abstractions.IUnifiedKernelCompiler` |

**Migration**:
```csharp
// Before (v0.8.x)
using DotCompute.Runtime.Services;
IComputeOrchestrator orchestrator = ...;

// After (v0.9.0+)
using DotCompute.Abstractions.Interfaces;
IComputeOrchestrator orchestrator = ...;
```

---

### v0.7.0 → v0.8.0

#### DC-BC-002: MemoryBuffer Deprecated

**Change**: `MemoryBuffer` and `HighPerformanceMemoryBuffer` marked obsolete

**Replacement**: Use `UnifiedBuffer<T>` or `OptimizedUnifiedBuffer<T>`

**Migration**:
```csharp
// Before (v0.7.x)
var buffer = new MemoryBuffer(size);

// After (v0.8.0+)
var buffer = await memoryManager.AllocateAsync<float>(size);
```

#### DC-BC-003: ExecutionPriority Consolidation

**Change**: Removed duplicate `ExecutionPriority` enum from `Core.Compute.Enums`

**Replacement**: Use `DotCompute.Core.ExecutionPriority`

---

### v0.6.0 → v0.7.0

#### DC-BC-004: Ring Kernel Message API

**Change**: `IRingKernelMessage` interface finalized

| Changed | Description |
|---------|-------------|
| `MessageId` | Now required property |
| `CorrelationId` | Added for request/response |
| `Timestamp` | Changed to `DateTimeOffset` |

**Migration**:
```csharp
// Before (v0.6.x)
[MemoryPackable]
public partial class MyMessage : IRingKernelMessage
{
    public Guid Id { get; set; }
}

// After (v0.7.0+)
[MemoryPackable]
public partial class MyMessage : IRingKernelMessage
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid? CorrelationId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
```

#### DC-BC-005: Checkpointing API

**Change**: `ICheckpointManager` interface added required methods

| Added Method | Description |
|--------------|-------------|
| `SaveAsync` | Save checkpoint with cancellation |
| `RestoreAsync` | Restore from checkpoint |
| `ListAsync` | List available checkpoints |

---

### v0.5.0 → v0.6.0

#### DC-BC-006: GPU Timing API

**Change**: `ITimingProvider` interface standardized

**Migration**:
```csharp
// Before (v0.5.x)
var timing = kernel.GetTiming();

// After (v0.6.0+)
var timing = await accelerator.Timing.MeasureAsync(kernel);
```

#### DC-BC-007: Barrier API

**Change**: `IBarrierProvider` interface added

| New Methods | Description |
|-------------|-------------|
| `CreateBarrier` | Create named barrier |
| `WaitAsync` | Async barrier wait |
| `SignalAndWait` | Combined signal and wait |

---

## Deprecation Schedule

### Scheduled for Removal in v2.0.0

| Type | Location | Deprecated In | Replacement |
|------|----------|---------------|-------------|
| `CoreKernelDebugOrchestrator` | Abstractions.Debugging | v0.8.0 | `KernelDebugOrchestrator` |
| `GeneratorSettings` | Generators.Configuration | v0.8.0 | `GeneratorConfiguration` |
| `PipelineStages` | Core.Pipelines | v0.8.0 | Use individual stage classes |

### Currently Deprecated

```csharp
// These types emit compiler warnings and will be removed in v2.0.0
[Obsolete("Use UnifiedBuffer<T>. Will be removed in v2.0.")]
public class MemoryBuffer { }

[Obsolete("Use OptimizedUnifiedBuffer<T>. Will be removed in v2.0.")]
public class HighPerformanceMemoryBuffer { }
```

---

## API Stability Guarantees

### What We Guarantee (v1.0.0+)

1. **Binary Compatibility**: Compiled code continues to work without recompilation
2. **Source Compatibility**: Existing code compiles without modification
3. **Behavioral Compatibility**: Same inputs produce same outputs

### What May Change

1. **Performance**: Optimizations may change timing characteristics
2. **Error Messages**: Text may be improved
3. **New Members**: Non-breaking additions to types
4. **New Types**: Adding new types to namespaces
5. **Default Parameter Values**: May be adjusted if behavior-compatible

---

## Semantic Versioning Policy

### Major Version (X.0.0)
- Breaking API changes
- Removal of deprecated APIs
- Incompatible runtime changes

### Minor Version (1.X.0)
- New features
- New types and methods
- Deprecation warnings added
- Non-breaking changes

### Patch Version (1.0.X)
- Bug fixes
- Security patches
- Performance improvements
- Documentation updates

---

## Migration Tools

### API Compatibility Analyzer

Run during build to detect breaking changes:
```xml
<PackageReference Include="DotCompute.Analyzers" Version="1.0.0" />
```

### Diagnostic IDs

| ID | Description | Severity |
|----|-------------|----------|
| DC1001 | Public API removed | Error |
| DC1002 | Method signature changed | Error |
| DC1003 | Visibility reduced | Error |
| DC1004 | Return type changed | Error |
| DC1005 | Parameter type changed | Error |
| DC1006 | Enum value removed | Error |
| DC1007 | Interface member added without default | Error |
| DC1008 | Sealed modifier added | Warning |
| DC1009 | Abstract modifier added | Error |
| DC1010 | Virtual modifier removed | Warning |

---

## Reporting Breaking Changes

Found an undocumented breaking change? Please report it:

1. **GitHub Issue**: https://github.com/mivertowski/DotCompute/issues
2. **Label**: `breaking-change`
3. **Include**: Version affected, code sample, expected vs actual behavior

---

**Document Version**: 1.0.0
**Last Review**: January 5, 2026
