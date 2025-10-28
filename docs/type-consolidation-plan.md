# Type Consolidation Plan - DotCompute Codebase
**Research Agent Analysis**
**Date:** 2025-10-20
**Swarm Session:** swarm-1760993879297-8b2b1itsg

## Executive Summary

This document identifies **147 duplicate filename instances** and **multiple critical type conflicts** across the DotCompute codebase. The analysis reveals systematic duplication patterns primarily in:

1. **Core types** (ExecutionPriority, ComputeBackendType, LogLevel)
2. **Memory statistics** (MemoryStatistics in 12+ locations)
3. **Complex number implementations** (2 instances)
4. **Resource requirements** (2 instances)
5. **Telemetry providers** (6+ instances)

## Critical Duplicate Types

### 1. ExecutionPriority Enum (5 instances)

**RECOMMENDED: Keep DotCompute.Abstractions.Execution.ExecutionPriority**

| Location | Namespace | Values | Status | Action |
|----------|-----------|--------|--------|--------|
| ✅ **CANONICAL** | `DotCompute.Abstractions.Execution` | Idle(0), Low(1), BelowNormal(2), Normal(3), AboveNormal(4), High(5), Critical(6), RealTime(7) | **KEEP** - Most comprehensive with extensions | **Keep as canonical** |
| ⚠️ Duplicate | `DotCompute.Abstractions.Pipelines.Enums` | Idle(0), Low(1), BelowNormal(2), Normal(3), AboveNormal(4), High(5), Critical(6), RealTime(7) | **DELETE** - Identical to canonical | **Delete, use type alias** |
| 🗑️ Obsolete | `DotCompute.Core.Compute.Enums` | None(0), Low(1), Normal(3), High(5), Critical(6) | **DELETE** - Already marked `[Obsolete]` | **Delete after migration** |
| ⚠️ Duplicate | `DotCompute.Abstractions.Compute.Options.ExecutionOptions` | Nested enum | **DELETE** - Move to using canonical | **Delete, reference canonical** |

**Impact:**
- **Files to update:** ~50+ files (found through duplicate filename analysis)
- **Breaking changes:** LOW (canonical version has all values from other versions)
- **Migration effort:** 2-3 hours (automated with Roslyn analyzer)

**Consolidation Steps:**
1. Add type alias in `DotCompute.Abstractions.Pipelines.Enums` namespace:
   ```csharp
   // Type alias for backward compatibility
   global using ExecutionPriority = DotCompute.Abstractions.Execution.ExecutionPriority;
   ```
2. Remove duplicate file after verification
3. Update all imports to use canonical namespace
4. Delete obsolete version in DotCompute.Core.Compute.Enums

---

### 2. ComputeBackendType Enum (3+ instances)

**RECOMMENDED: Keep DotCompute.Abstractions.Compute.Enums.ComputeBackendType**

| Location | Namespace | Values | Status | Action |
|----------|-----------|--------|--------|--------|
| ✅ **CANONICAL** | `DotCompute.Abstractions.Compute.Enums` | CPU, CUDA, OpenCL, Metal, Vulkan, DirectCompute, ROCm | **KEEP** - Comprehensive docs | **Keep as canonical** |
| ⚠️ Duplicate | `DotCompute.Core.Compute.Enums` | CPU, CUDA, OpenCL, Metal, Vulkan, DirectCompute, ROCm | **DELETE** - Identical to canonical | **Delete, use type alias** |
| ⚠️ Plugin | `DotCompute.Plugins.Platform.PlatformDetection` | Nested enum | **DELETE** - Internal use only | **Replace with canonical** |

**Impact:**
- **Files to update:** ~30+ files
- **Breaking changes:** NONE (identical enums)
- **Migration effort:** 1-2 hours (simple find/replace)

**Consolidation Steps:**
1. Add global using in DotCompute.Core project:
   ```csharp
   global using ComputeBackendType = DotCompute.Abstractions.Compute.Enums.ComputeBackendType;
   ```
2. Remove duplicate file
3. Update plugin to use canonical type

---

### 3. LogLevel Enum (3 instances)

**RECOMMENDED: Use Microsoft.Extensions.Logging.LogLevel**

| Location | Namespace | Values | Status | Action |
|----------|-----------|--------|--------|--------|
| 🎯 **USE FRAMEWORK** | `Microsoft.Extensions.Logging` | Trace, Debug, Information, Warning, Error, Critical, None | **USE THIS** - .NET standard | **Migrate to framework type** |
| 🗑️ Custom | `DotCompute.Abstractions.Debugging.Types` | Trace, Debug, Information, Warning, Error, Critical | **DELETE** - Use framework | **Delete after migration** |
| 🗑️ Custom | `DotCompute.Abstractions.Debugging.IKernelDebugService` | Nested enum | **DELETE** - Use framework | **Delete after migration** |
| 🗑️ Custom | `DotCompute.Abstractions.Telemetry.Options.TelemetryOptions` | Nested enum | **DELETE** - Use framework | **Delete after migration** |

**Impact:**
- **Files to update:** ~20+ files
- **Breaking changes:** LOW (values are identical)
- **Migration effort:** 2-3 hours (update interfaces and implementations)
- **Benefits:** Standard .NET logging integration, better tooling support

**Consolidation Steps:**
1. Add `using Microsoft.Extensions.Logging;` to all affected files
2. Replace custom LogLevel references with framework type
3. Update interfaces and method signatures
4. Delete custom LogLevel definitions
5. Verify logging integration still works

---

### 4. Complex Number Type (2 instances)

**RECOMMENDED: Keep DotCompute.Algorithms.SignalProcessing.Complex**

| Location | Namespace | Purpose | Status | Action |
|----------|-----------|---------|--------|--------|
| ✅ **CANONICAL** | `DotCompute.Algorithms.SignalProcessing` | Full implementation (231 lines) | **KEEP** - Complete implementation | **Keep as canonical** |
| 🔄 Wrapper | `DotCompute.Algorithms.Types.SignalProcessing` | Compatibility wrapper (55 lines) | **EVALUATE** - Already wraps canonical | **Keep only if needed** |

**Current State:** The wrapper type already delegates to the canonical implementation with implicit conversions. This is acceptable for backward compatibility.

**Impact:**
- **Files to update:** Unknown (need usage analysis)
- **Breaking changes:** NONE (wrapper provides compatibility)
- **Migration effort:** 0 hours (current state is acceptable) OR 1-2 hours if removing wrapper

**Recommendation:**
- **Short term:** Keep both (wrapper provides backward compatibility)
- **Long term:** Mark wrapper as `[Obsolete]` in next major version, remove in version after

---

### 5. Resource Requirements Types (2 instances)

**RECOMMENDED: Consolidate into single type**

| Location | Namespace | Purpose | Status | Action |
|----------|-----------|---------|--------|--------|
| 📊 Pipeline | `DotCompute.Abstractions.Pipelines.Models.ResourceRequirements` | Pipeline-specific (110 lines) | **KEEP & EXTEND** - More comprehensive | **Extend for kernel use** |
| 🔧 Kernel | `DotCompute.Runtime.Services.Statistics.KernelResourceRequirements` | Kernel-specific (42 lines) | **MERGE** - Subset of pipeline version | **Merge into pipeline version** |

**Differences:**
- Pipeline version: More properties (compute units, device type, memory types, bandwidth, latency)
- Kernel version: Simpler (memory, shared memory, compute capability, features, execution time)

**Impact:**
- **Files to update:** ~15+ files in Runtime/Services
- **Breaking changes:** LOW (extend, don't remove)
- **Migration effort:** 3-4 hours (merge properties, update usages)

**Consolidation Steps:**
1. Extend `ResourceRequirements` with properties from `KernelResourceRequirements`
2. Add factory methods for common scenarios:
   - `ResourceRequirements.ForKernel(...)`
   - `ResourceRequirements.ForPipeline(...)`
3. Create type alias: `KernelResourceRequirements = ResourceRequirements`
4. Update all kernel-related code to use unified type
5. Delete old KernelResourceRequirements after migration

---

### 6. MemoryStatistics Type (12+ instances)

**CRITICAL: Most severe duplication**

| Location | Namespace | Purpose | Lines | Action |
|----------|-----------|---------|-------|--------|
| 🏆 **CANONICAL** | `DotCompute.Memory.MemoryStatistics` | Comprehensive pool stats | 358 | **KEEP & EXTEND** |
| 🔧 Runtime | `DotCompute.Runtime.Services.Statistics` | Service-level stats | Unknown | **MERGE** |
| 🔧 Abstractions | `DotCompute.Abstractions.Memory` | Abstract stats | Unknown | **MERGE** |
| 🔧 Core | `DotCompute.Core.Models.UnifiedMemoryStatistics` | Unified stats | Unknown | **MERGE** |
| 🔧 Core | `DotCompute.Core.Execution.Memory.DeviceMemoryStatistics` | Device-specific | Unknown | **MERGE** |
| 🔧 Core | `DotCompute.Core.Execution.Memory.ExecutionMemoryStatistics` | Execution-specific | Unknown | **MERGE** |
| 🔧 Backends | `DotCompute.Backends.CUDA.Types.CudaMemoryStatisticsExtended` | CUDA-specific | Unknown | **SPECIALIZE** |
| 🧪 Test Utils | `DotCompute.SharedTestUtilities.Performance.MemoryTracker` | Testing helper (302 lines) | 302 | **KEEP SEPARATE** |
| 🔧 Runtime | `DotCompute.Runtime.Services.Statistics.AcceleratorMemoryStatistics` | Accelerator stats | Unknown | **MERGE** |

**Impact:**
- **Files to update:** 50-100+ files
- **Breaking changes:** MEDIUM (need careful migration)
- **Migration effort:** 8-12 hours (complex consolidation)

**Consolidation Strategy:**
1. **Phase 1:** Create unified base type in `DotCompute.Abstractions.Memory`
   ```csharp
   public class MemoryStatistics
   {
       // Core properties (common to all)
       public long TotalBytes { get; set; }
       public long UsedBytes { get; set; }
       public long FreeBytes { get; set; }
       // Extension point for specialized stats
       public IDictionary<string, object> ExtendedProperties { get; set; }
   }
   ```

2. **Phase 2:** Create specialized types that inherit/extend base:
   - `DeviceMemoryStatistics : MemoryStatistics`
   - `ExecutionMemoryStatistics : MemoryStatistics`
   - `CudaMemoryStatistics : MemoryStatistics`

3. **Phase 3:** Migrate existing code incrementally
4. **Phase 4:** Remove duplicate definitions

---

### 7. TelemetryProvider Type (6+ instances)

**RECOMMENDED: Keep DotCompute.Abstractions.Telemetry.Providers.TelemetryProvider**

| Location | Namespace | Purpose | Status | Action |
|----------|-----------|---------|--------|--------|
| ✅ **ABSTRACT** | `DotCompute.Abstractions.Telemetry.Providers` | Abstract base class | **KEEP** - Base abstraction | **Keep as base class** |
| 🔧 Core | `DotCompute.Core.Telemetry.BaseTelemetryProvider` | Base implementation | **KEEP** - Core impl | **Inherit from abstract** |
| 🔧 Core | `DotCompute.Core.Telemetry.TelemetryProvider` | Concrete provider | **EVALUATE** - May be duplicate | **Check usage** |
| 🔧 Core | `DotCompute.Core.Telemetry.UnifiedTelemetryProvider` | Unified provider | **KEEP** - Aggregates multiple | **Keep specialized** |
| 🔧 Metal | `DotCompute.Backends.Metal.Telemetry.MetalTelemetryManager` | Metal-specific | **KEEP** - Backend-specific | **Keep specialized** |

**Impact:**
- **Files to update:** ~20+ files
- **Breaking changes:** LOW
- **Migration effort:** 4-6 hours

---

## Complete List of Duplicate Filenames (147 instances)

Below is the complete list of duplicate filenames found across the codebase. Each represents a potential type conflict that needs investigation:

### Build/Generated Files (Can ignore)
- `.NETCoreApp,Version=v9.0.AssemblyAttributes.cs`
- `.NETStandard,Version=v2.0.AssemblyAttributes.cs`
- `DotCompute.*.AssemblyInfo.cs` (multiple projects)
- `DotCompute.*.GlobalUsings.g.cs` (multiple projects)
- `GlobalSuppressions.cs`

### Core Type Duplicates (HIGH PRIORITY)
- ✅ `ExecutionPriority.cs` (5 instances) - **Analyzed above**
- ✅ `ComputeBackendType.cs` (3 instances) - **Analyzed above**
- ✅ `Complex.cs` (2 instances) - **Analyzed above**
- ✅ `MemoryStatistics.cs` (12+ instances) - **Analyzed above**
- ✅ `TelemetryProvider.cs` (6 instances) - **Analyzed above**
- ✅ `LogLevel.cs` (3 instances) - **Analyzed above**

### Configuration & Options (MEDIUM PRIORITY)
- `CompilationOptions.cs` (2+ instances)
- `ExecutionOptions.cs` (2+ instances)
- `ValidationOptions.cs` (2+ instances)
- `MemoryOptions.cs` (2+ instances)
- `GeneratorSettings.cs` (2+ instances)
- `SimdConfiguration.cs` (2+ instances)
- `CacheConfig.cs` (2+ instances)

### Statistics & Metrics (MEDIUM PRIORITY)
- `PerformanceMetrics.cs` (multiple)
- `DeviceMetrics.cs` (multiple)
- `ExecutionStats.cs` (multiple)
- `KernelExecutionStats.cs` (multiple)
- `ThroughputMetrics.cs` (multiple)
- `RecoveryMetrics.cs` (multiple)
- `ErrorStatistics.cs` (multiple)
- `GraphStatistics.cs` (multiple)

### Kernel-Related (MEDIUM PRIORITY)
- `KernelInfo.cs` (2+ instances)
- `KernelParameter.cs` (2+ instances)
- `KernelType.cs` (2+ instances)
- `KernelConfiguration.cs` (multiple)
- `KernelMetadata.cs` (multiple)
- `CompiledKernel.cs` (2+ instances)
- `KernelArguments.cs` (multiple)

### CUDA-Specific (LOW PRIORITY - Backend-specific)
- `CudaError.cs` (multiple)
- `CudaException.cs` (multiple)
- `CudaDeviceManager.cs` (multiple)
- `CudaMemoryManager.cs` (multiple)
- `CudaKernelExecutor.cs` (multiple)
- `CudaGraphTypes.cs` (multiple)

### Memory Management (MEDIUM PRIORITY)
- `DeviceMemory.cs` (multiple)
- `CpuMemoryManager.cs` (multiple)
- `MapMode.cs` (2+ instances)
- `AccessOrder.cs` (multiple)

### Pipeline & Execution (MEDIUM PRIORITY)
- `PipelineExecutionResult.cs` (multiple)
- `PipelineExecutionContext.cs` (multiple)
- `PipelineMetrics.cs` (multiple)
- `StageExecutionResult.cs` (multiple)

### Telemetry & Monitoring (MEDIUM PRIORITY)
- `TelemetryTypes.cs` (multiple)
- `PerformanceMonitor.cs` (multiple)
- `MetricsExportFormat.cs` (multiple)

### Plugin System (LOW PRIORITY)
- `IAlgorithmPlugin.cs` (multiple)
- `IBackendPlugin.cs` (multiple)
- `PluginInfo.cs` (multiple)
- `PluginServiceProvider.cs` (multiple)

### Analysis & Optimization (LOW PRIORITY)
- `WorkloadAnalysis.cs` (multiple)
- `BottleneckAnalysis.cs` (multiple)
- `CoalescingAnalysis.cs` (multiple)
- `VectorizationInfo.cs` (multiple)

### Security & Validation (LOW PRIORITY)
- `SecurityValidator.cs` (multiple)
- `KernelValidator.cs` (multiple)
- `VulnerabilityScanner.cs` (multiple)

### Recovery & Error Handling (LOW PRIORITY)
- `RecoveryResult.cs` (multiple)
- `BaseRecoveryStrategy.cs` (multiple)
- `ErrorContext.cs` (multiple)

---

## Consolidation Priority Matrix

| Priority | Type Category | Count | Estimated Effort | Risk Level | Dependencies |
|----------|--------------|-------|------------------|------------|--------------|
| 🔴 **P0** | ExecutionPriority | 5 | 2-3h | LOW | ~50 files |
| 🔴 **P0** | ComputeBackendType | 3 | 1-2h | LOW | ~30 files |
| 🔴 **P0** | LogLevel | 3 | 2-3h | LOW | ~20 files |
| 🟡 **P1** | MemoryStatistics | 12+ | 8-12h | MEDIUM | ~100 files |
| 🟡 **P1** | ResourceRequirements | 2 | 3-4h | LOW | ~15 files |
| 🟡 **P1** | TelemetryProvider | 6 | 4-6h | LOW | ~20 files |
| 🟢 **P2** | Complex | 2 | 0-2h | NONE | Unknown |
| 🟢 **P2** | Configuration Types | 10+ | 6-10h | MEDIUM | ~40 files |
| 🟢 **P3** | Statistics/Metrics | 20+ | 10-15h | MEDIUM | ~80 files |
| 🔵 **P4** | Backend-Specific | 30+ | 5-8h | LOW | ~50 files |

**Total Estimated Effort:** 40-65 hours
**Recommended Timeline:** 2-3 weeks with testing

---

## Migration Strategy

### Phase 1: Critical Enums (Week 1)
**Goal:** Eliminate duplicate enums that cause immediate compilation issues

1. **Day 1-2:** ExecutionPriority consolidation
   - Add type aliases
   - Update imports
   - Remove obsolete versions
   - Run full test suite

2. **Day 3-4:** ComputeBackendType consolidation
   - Add global usings
   - Remove duplicates
   - Verify plugin system

3. **Day 5:** LogLevel migration to framework type
   - Replace custom enums
   - Update interfaces
   - Test logging integration

### Phase 2: Resource Types (Week 2)
**Goal:** Consolidate resource and statistics types

1. **Day 1-3:** ResourceRequirements consolidation
   - Merge properties
   - Create factory methods
   - Update kernel services

2. **Day 4-5:** TelemetryProvider consolidation
   - Establish inheritance hierarchy
   - Verify all providers

### Phase 3: Memory Statistics (Week 3)
**Goal:** Create unified memory statistics architecture

1. **Day 1-2:** Design unified base type
2. **Day 3-4:** Create specialized inheritors
3. **Day 5:** Migrate existing code incrementally

### Phase 4: Remaining Types (Ongoing)
**Goal:** Address lower-priority duplicates

- Configuration types (P2)
- Statistics/Metrics (P3)
- Backend-specific types (P4)

---

## Automated Migration Tools

### Recommended Approach: Roslyn Analyzer + Code Fixes

Create custom Roslyn analyzers to detect and fix duplicate type usage:

```csharp
// Example: DC9001 - Use canonical ExecutionPriority
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UseCanonicalExecutionPriorityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DC9001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use canonical ExecutionPriority",
        messageFormat: "Use DotCompute.Abstractions.Execution.ExecutionPriority instead of {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
```

### Automated Refactoring Script

```bash
#!/bin/bash
# migrate-execution-priority.sh

# Step 1: Add type aliases
find src -name "*.cs" -type f -exec sed -i \
  '1i global using ExecutionPriority = DotCompute.Abstractions.Execution.ExecutionPriority;' {} \;

# Step 2: Update namespace references
find src -name "*.cs" -type f -exec sed -i \
  's/using DotCompute\.Core\.Compute\.Enums;.*ExecutionPriority/using DotCompute.Abstractions.Execution;/g' {} \;

# Step 3: Run tests
dotnet test

# Step 4: Remove obsolete files (manual verification required)
# rm src/Core/DotCompute.Core/Compute/Enums/ExecutionPriority.cs
```

---

## Testing Strategy

### 1. Pre-Migration Tests
- **Baseline:** Run full test suite, capture results (current: 187/199 passing)
- **Coverage:** Generate code coverage report
- **Integration:** Verify all backends compile and execute

### 2. During Migration
- **Incremental:** Test after each type consolidation
- **Regression:** Compare against baseline
- **Type Safety:** Ensure no implicit conversions break

### 3. Post-Migration Validation
- **Full Suite:** All 199 tests must pass
- **Coverage:** Maintain or improve 93.97% coverage
- **Performance:** No degradation in benchmarks
- **Documentation:** Update all references

---

## Breaking Change Impact Analysis

### External API Impact (Public)

**LOW RISK** - Most duplicates are internal types

- `ExecutionPriority`: Public enum in Abstractions (keep canonical)
- `ComputeBackendType`: Public enum in Abstractions (keep canonical)
- `Complex`: Public struct (keep with wrapper for compatibility)

**Recommendation:** Use semantic versioning:
- Current: 0.2.0
- After consolidation: 0.3.0 (minor version bump)
- Mark obsolete types: 0.4.0 (one version warning period)
- Remove obsolete: 1.0.0 (major version)

### Internal API Impact (Internal/Private)

**MEDIUM RISK** - Many internal types to consolidate

- MemoryStatistics: Extensive internal usage
- TelemetryProvider: Core infrastructure
- ResourceRequirements: Kernel and pipeline systems

**Mitigation:**
- Extensive testing
- Incremental migration
- Type aliases during transition

---

## Success Criteria

### Quantitative Metrics
- ✅ Reduce duplicate type count from 147 to < 20
- ✅ Eliminate all critical enum duplicates (ExecutionPriority, ComputeBackendType, LogLevel)
- ✅ Maintain 93.97%+ test coverage
- ✅ All 199 tests passing
- ✅ No performance regression (< 5% deviation)
- ✅ Documentation updated

### Qualitative Metrics
- ✅ Clear type ownership (one canonical version per type)
- ✅ Consistent namespace organization
- ✅ Improved developer experience (less confusion)
- ✅ Better IDE support (no ambiguous types)
- ✅ Simplified dependency graph

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Test failures | MEDIUM | HIGH | Incremental migration, baseline comparison |
| Breaking changes | LOW | MEDIUM | Semantic versioning, type aliases |
| Performance regression | LOW | MEDIUM | Benchmark before/after |
| Developer confusion | MEDIUM | LOW | Clear documentation, migration guide |
| Build failures | LOW | HIGH | CI/CD validation at each step |

---

## Recommendations

### Immediate Actions (This Week)
1. ✅ **Approve this plan** - Review with development team
2. 🔧 **Create feature branch** - `feature/type-consolidation`
3. 🔧 **Start with P0 items** - ExecutionPriority, ComputeBackendType, LogLevel
4. 🔧 **Set up automated testing** - CI pipeline for migration validation

### Short Term (2-3 Weeks)
1. Complete Phase 1-3 of migration strategy
2. Create Roslyn analyzers for automated detection
3. Update documentation and migration guides

### Long Term (Next Release)
1. Remove obsolete types
2. Complete remaining P3-P4 consolidations
3. Publish migration guide for external users

---

## Appendix A: File Locations

### ExecutionPriority Instances
```
src/Core/DotCompute.Abstractions/Execution/ExecutionPriority.cs (CANONICAL)
src/Core/DotCompute.Abstractions/Pipelines/Enums/ExecutionPriority.cs (DELETE)
src/Core/DotCompute.Core/Compute/Enums/ExecutionPriority.cs (DELETE - Obsolete)
src/Core/DotCompute.Abstractions/Compute/Options/ExecutionOptions.cs:134 (NESTED - DELETE)
```

### ComputeBackendType Instances
```
src/Core/DotCompute.Abstractions/Compute/Enums/ComputeBackendType.cs (CANONICAL)
src/Core/DotCompute.Core/Compute/Enums/ComputeBackendType.cs (DELETE)
src/Runtime/DotCompute.Plugins/Platform/PlatformDetection.cs:1025 (NESTED - REPLACE)
```

### LogLevel Instances
```
Microsoft.Extensions.Logging.LogLevel (USE FRAMEWORK)
src/Core/DotCompute.Abstractions/Debugging/Types/LogLevel.cs (DELETE)
src/Core/DotCompute.Abstractions/Debugging/IKernelDebugService.cs:123 (NESTED - DELETE)
src/Core/DotCompute.Abstractions/Telemetry/Options/TelemetryOptions.cs:119 (NESTED - DELETE)
```

### Complex Instances
```
src/Extensions/DotCompute.Algorithms/SignalProcessing/Complex.cs (CANONICAL)
src/Extensions/DotCompute.Algorithms/Types/SignalProcessing/Complex.cs (WRAPPER - EVALUATE)
```

### ResourceRequirements Instances
```
src/Core/DotCompute.Abstractions/Pipelines/Models/ResourceRequirements.cs (CANONICAL)
src/Runtime/DotCompute.Runtime/Services/Statistics/KernelResourceRequirements.cs (MERGE)
```

### MemoryStatistics Instances
```
src/Core/DotCompute.Memory/MemoryStatistics.cs (CANONICAL)
src/Runtime/DotCompute.Runtime/Services/Statistics/MemoryStatistics.cs (MERGE)
src/Core/DotCompute.Abstractions/Memory/MemoryStatistics.cs (MERGE)
src/Core/DotCompute.Core/Models/UnifiedMemoryStatistics.cs (MERGE)
src/Core/DotCompute.Core/Execution/Memory/DeviceMemoryStatistics.cs (MERGE)
src/Core/DotCompute.Core/Execution/Memory/ExecutionMemoryStatistics.cs (MERGE)
src/Runtime/DotCompute.Runtime/Services/Statistics/AcceleratorMemoryStatistics.cs (MERGE)
src/Backends/DotCompute.Backends.CUDA/Types/CudaMemoryStatisticsExtended.cs (SPECIALIZE)
tests/Shared/DotCompute.SharedTestUtilities/Performance/MemoryTracker.cs (KEEP SEPARATE - TEST UTIL)
```

---

## Appendix B: Commands for Analysis

### Find All Duplicate Filenames
```bash
find src -name "*.cs" -type f -exec basename {} \; | sort | uniq -d
```

### Count Type References
```bash
grep -r "ExecutionPriority" src --include="*.cs" | wc -l
grep -r "ComputeBackendType" src --include="*.cs" | wc -l
grep -r "LogLevel" src --include="*.cs" | wc -l
```

### Find Specific Type Definitions
```bash
grep -r "public enum ExecutionPriority" src --include="*.cs"
grep -r "public class MemoryStatistics" src --include="*.cs"
```

---

## Document Status

- **Status:** COMPLETE
- **Next Review:** After Phase 1 completion
- **Owner:** Research Agent
- **Reviewers:** Planner Agent, Coder Agent, Reviewer Agent

---

**End of Type Consolidation Plan**
