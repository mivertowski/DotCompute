# PerformanceMetrics Migration Guide

**Agent**: code-analyzer
**Date**: 2025-10-04
**Status**: Ready for execution

## Quick Summary

**Problem**: Two conflicting PerformanceMetrics types in the codebase
**Solution**: Consolidate to single canonical type
**Impact**: 5 files to modify, ~1-2 hours work
**Risk**: LOW ✅

## What We Found

### The Conflict

```
❌ DotCompute.Abstractions.Debugging.PerformanceMetrics (9 properties)
   └─ Nested in IKernelDebugService.cs interface
   └─ Used by 3 files
   └─ Has compatibility layer mapping to canonical

✅ DotCompute.Abstractions.Performance.PerformanceMetrics (22 properties)
   └─ Standalone class with full feature set
   └─ Used by 5+ files
   └─ Factory methods, aggregation, extensibility
```

## Migration Steps

### Step 1: Update IKernelDebugService.cs

**File**: `src/Core/DotCompute.Abstractions/Debugging/IKernelDebugService.cs`

#### Before (Lines 1-166):
```csharp
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Debugging.Types;

namespace DotCompute.Abstractions.Debugging;

public interface IKernelDebugService
{
    // ... interface methods ...
}

/// <summary>
/// Performance metrics for a specific execution.
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan ExecutionTime { get; init; }
    public long MemoryUsage { get; init; }
    public float CpuUtilization { get; init; }
    public float GpuUtilization { get; init; }
    public int ThroughputOpsPerSecond { get; init; }

    // Compatibility properties
    public long ExecutionTimeMs => (long)ExecutionTime.TotalMilliseconds;
    public long MemoryAllocatedBytes => MemoryUsage;
    public long ThroughputOpsPerSec => ThroughputOpsPerSecond;
    public double EfficiencyScore => CpuUtilization * 100;
}
```

#### After:
```csharp
using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Performance;  // ADD THIS

namespace DotCompute.Abstractions.Debugging;

public interface IKernelDebugService
{
    // ... interface methods ...
    // No changes needed - already returns PerformanceMetrics
}

// DELETE the nested PerformanceMetrics class (lines 153-166)
```

### Step 2: Update KernelDebugProfiler.cs

**File**: `src/Core/DotCompute.Core/Debugging/KernelDebugProfiler.cs`

#### Before (Lines 1-15):
```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Performance;
using DotCompute.Core.Debugging.Types;
using PerformanceMetrics = DotCompute.Abstractions.Debugging.PerformanceMetrics; // REMOVE THIS

namespace DotCompute.Core.Debugging;

public sealed partial class KernelDebugProfiler(
    ILogger<KernelDebugProfiler> logger,
```

#### After:
```csharp
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions;
using DotCompute.Core.Optimization.Performance;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Performance;
using DotCompute.Core.Debugging.Types;
// Type alias removed - use DotCompute.Abstractions.Performance.PerformanceMetrics directly

namespace DotCompute.Core.Debugging;

public sealed partial class KernelDebugProfiler(
    ILogger<KernelDebugProfiler> logger,
```

#### Code Changes in Methods:

**Before**:
```csharp
private PerformanceMetrics CreateMetrics(TimeSpan elapsed, long memory)
{
    return new PerformanceMetrics
    {
        ExecutionTime = elapsed,
        MemoryUsage = memory,
        CpuUtilization = GetCpuUtilization(),
        GpuUtilization = GetGpuUtilization()
    };
}
```

**After**:
```csharp
private PerformanceMetrics CreateMetrics(TimeSpan elapsed, long memory)
{
    var metrics = PerformanceMetrics.FromTimeSpan(elapsed, "profiled-execution");
    metrics.MemoryUsageBytes = memory;
    metrics.ComputeUtilization = GetCpuUtilization(); // CPU or GPU
    return metrics;
}
```

### Step 3: Update CPU Backend Files

**Files**:
- `src/Backends/DotCompute.Backends.CPU/CpuCompiledKernel.cs`
- `src/Backends/DotCompute.Backends.CPU/CpuKernelExecutor.cs`

#### Before:
```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Debugging;
using DotCompute.Core.Memory;

namespace DotCompute.Backends.CPU;

public class CpuCompiledKernel : ICompiledKernel
{
    private PerformanceMetrics GetMetrics()
    {
        return new PerformanceMetrics
        {
            ExecutionTime = _stopwatch.Elapsed,
            MemoryUsage = GetMemoryUsage()
        };
    }
}
```

#### After:
```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Performance;  // CHANGED
using DotCompute.Core.Memory;

namespace DotCompute.Backends.CPU;

public class CpuCompiledKernel : ICompiledKernel
{
    private PerformanceMetrics GetMetrics()
    {
        return PerformanceMetrics.FromStopwatch(_stopwatch, "cpu-kernel-execution");
        // Memory tracking handled by canonical type's MemoryUsageBytes
    }
}
```

### Step 4: Verify KernelDebugAnalyzer.cs

**File**: `src/Core/DotCompute.Core/Debugging/Analytics/KernelDebugAnalyzer.cs`

This file already imports both namespaces. Verify it uses the correct type:

```csharp
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Debugging.Types;

namespace DotCompute.Core.Debugging.Analytics;

public class KernelDebugAnalyzer
{
    // Should already be using DotCompute.Abstractions.Performance.PerformanceMetrics
    // Verify no references to Debugging.PerformanceMetrics
}
```

## Property Mapping Reference

When migrating code, use these mappings:

| Old (Debugging) | New (Canonical) | Code Change |
|----------------|-----------------|-------------|
| `ExecutionTime` (TimeSpan) | `ExecutionTimeMs` (long) | Use factory: `FromTimeSpan(elapsed)` |
| `MemoryUsage` | `MemoryUsageBytes` | Direct assignment |
| `CpuUtilization` / `GpuUtilization` | `ComputeUtilization` | Use single property |
| `ThroughputOpsPerSecond` | `OperationsPerSecond` | Direct assignment |

### Common Patterns

#### Pattern 1: Creating from Stopwatch
```csharp
// Old
var metrics = new PerformanceMetrics
{
    ExecutionTime = stopwatch.Elapsed,
    MemoryUsage = memoryBytes
};

// New
var metrics = PerformanceMetrics.FromStopwatch(stopwatch, "operation-name");
metrics.MemoryUsageBytes = memoryBytes;
```

#### Pattern 2: Creating from TimeSpan
```csharp
// Old
var metrics = new PerformanceMetrics
{
    ExecutionTime = elapsed,
    CpuUtilization = cpuPercent
};

// New
var metrics = PerformanceMetrics.FromTimeSpan(elapsed, "operation-name");
metrics.ComputeUtilization = cpuPercent;
```

#### Pattern 3: Adding Custom Metrics
```csharp
// Old (not possible)
var metrics = new PerformanceMetrics { /* limited properties */ };

// New (extensible)
var metrics = PerformanceMetrics.FromStopwatch(stopwatch, "gpu-kernel");
metrics.CustomMetrics["warp_efficiency"] = 0.95;
metrics.CustomMetrics["occupancy"] = 0.87;
```

## Validation Checklist

After migration, verify:

### ✅ Compilation
```bash
dotnet build DotCompute.sln --configuration Release
# Should complete with no errors
```

### ✅ Debugging Tests
```bash
dotnet test --filter "Category=Debugging" --configuration Release
# All debugging tests should pass
```

### ✅ Performance Tests
```bash
dotnet test --filter "Category=Performance" --configuration Release
# Performance monitoring should work correctly
```

### ✅ Integration Tests
```bash
dotnet test tests/Integration/DotCompute.Integration.Tests/ --configuration Release
# End-to-end scenarios should pass
```

### ✅ Code Search Verification
```bash
# Verify no remaining references to debugging type
grep -rn "Abstractions.Debugging.PerformanceMetrics" src/
# Should return no results (except in comments/docs)

# Verify canonical type usage
grep -rn "Abstractions.Performance.PerformanceMetrics" src/
# Should show updated files
```

## Expected Test Changes

### Tests That May Need Updates

1. **KernelDebugProfiler Tests**
   - Update assertions for property names
   - Verify factory method usage
   - Check CustomMetrics access

2. **CPU Backend Tests**
   - Update metric creation assertions
   - Verify timing property access
   - Check memory tracking

3. **Debugging Service Tests**
   - Update mock object creation
   - Verify interface contract unchanged
   - Check performance analysis

### Sample Test Update

**Before**:
```csharp
[Fact]
public void ProfileExecution_CreatesMetrics()
{
    // Arrange
    var profiler = CreateProfiler();

    // Act
    var metrics = profiler.ProfileExecution(kernel);

    // Assert
    Assert.NotNull(metrics.ExecutionTime);
    Assert.True(metrics.MemoryUsage > 0);
}
```

**After**:
```csharp
[Fact]
public void ProfileExecution_CreatesMetrics()
{
    // Arrange
    var profiler = CreateProfiler();

    // Act
    var metrics = profiler.ProfileExecution(kernel);

    // Assert
    Assert.True(metrics.ExecutionTimeMs > 0);
    Assert.True(metrics.MemoryUsageBytes > 0);
    Assert.NotNull(metrics.Operation);
}
```

## Benefits Realized

After migration, you'll have:

### ✨ Single Source of Truth
- One canonical PerformanceMetrics type
- No confusion about which type to use
- Consistent API across all components

### ✨ Better Extensibility
```csharp
metrics.CustomMetrics["backend_specific_metric"] = value;
metrics.CustomMetrics["optimization_level"] = "aggressive";
```

### ✨ Richer Analysis
```csharp
var aggregated = PerformanceMetrics.Aggregate(allMetrics);
Console.WriteLine($"Avg: {aggregated.AverageTimeMs}ms");
Console.WriteLine($"StdDev: {aggregated.StandardDeviation}");
Console.WriteLine($"Min: {aggregated.MinTimeMs}ms, Max: {aggregated.MaxTimeMs}ms");
```

### ✨ Error Tracking
```csharp
if (!metrics.IsSuccessful)
{
    logger.LogError("Execution failed: {Error}", metrics.ErrorMessage);
}
```

### ✨ Better Telemetry
```csharp
metrics.Timestamp = DateTimeOffset.UtcNow;
metrics.Operation = "matrix-multiply-gpu";
metrics.CallCount = executionCount;
```

## Rollback Plan (If Needed)

If issues arise, rollback is simple:

1. Revert `IKernelDebugService.cs` - restore nested class
2. Revert `KernelDebugProfiler.cs` - restore type alias
3. Revert CPU backend files - restore Debugging namespace import
4. Run tests to verify rollback

**Git commands**:
```bash
git checkout HEAD -- src/Core/DotCompute.Abstractions/Debugging/IKernelDebugService.cs
git checkout HEAD -- src/Core/DotCompute.Core/Debugging/KernelDebugProfiler.cs
git checkout HEAD -- src/Backends/DotCompute.Backends.CPU/CpuCompiledKernel.cs
git checkout HEAD -- src/Backends/DotCompute.Backends.CPU/CpuKernelExecutor.cs
```

## Timeline

| Phase | Task | Duration |
|-------|------|----------|
| 1 | Update IKernelDebugService.cs | 10 min |
| 2 | Update KernelDebugProfiler.cs | 20 min |
| 3 | Update CPU backend files | 15 min |
| 4 | Verify KernelDebugAnalyzer.cs | 5 min |
| 5 | Run test suite | 15 min |
| 6 | Fix any test failures | 15 min |
| 7 | Documentation updates | 10 min |

**Total**: ~90 minutes (1.5 hours)

## Success Criteria

- ✅ All files compile without errors
- ✅ All debugging tests pass
- ✅ All performance tests pass
- ✅ No references to `Abstractions.Debugging.PerformanceMetrics` in code
- ✅ All usages now use `Abstractions.Performance.PerformanceMetrics`
- ✅ Test coverage maintained or improved

## Next Steps

1. **Get Approval** from hive coordinator
2. **Create Feature Branch**: `git checkout -b fix/consolidate-performance-metrics`
3. **Execute Migration** following steps 1-4
4. **Run Tests** and verify all pass
5. **Update CHANGELOG.md** with breaking change notice
6. **Create PR** with detailed description
7. **Merge** after review

---

**Ready for execution**: ✅
**Approval needed**: Awaiting coordinator
**Estimated completion**: 1.5 hours after approval
