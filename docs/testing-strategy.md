# DotCompute Hive Mind Cleanup - Testing Strategy

## Overview

This document outlines the comprehensive testing strategy for the analyzer-driven cleanup operation involving 52+ modified files across CPU, CUDA, Metal backends, and Algorithm extensions.

**Last Updated**: 2025-10-21
**Status**: Active
**Test Coverage Target**: 95%+ for modified components

---

## 1. Testing Phases

### Phase 1: Compilation Verification (CRITICAL)
**Objective**: Ensure all modified files compile successfully

**Execution**:
```bash
./docs/scripts/quick-compile-check.sh
```

**Success Criteria**:
- All modified projects build without errors
- No new warnings introduced
- All dependencies resolve correctly

**Priority**: **HIGHEST** - Must pass before any other tests

---

### Phase 2: Unit Testing
**Objective**: Validate individual component functionality

**Test Projects by Component**:

#### CPU Backend Tests
- **Project**: `tests/Unit/DotCompute.Backends.CPU.Tests/`
- **Coverage**: CpuKernelOptimizer, CpuMemoryBuffer*, SIMD operations
- **Modified Files**:
  - `CpuKernelOptimizer.cs`
  - `CpuMemoryBufferSlice.cs`
  - `CpuMemoryBufferTyped.cs`
  - `SimdInstructionSetDetector.cs`
  - `NumaScheduler.cs`

**Execution**:
```bash
dotnet test tests/Unit/DotCompute.Backends.CPU.Tests/ --configuration Release
```

#### CUDA Backend Tests
- **Project**: `tests/Hardware/DotCompute.Hardware.Cuda.Tests/`
- **Coverage**: Stream management, kernel launching, memory operations
- **Modified Files**:
  - `CudaStreamManager.cs`
  - `CudaKernelLauncher.cs`
  - `CudaMemoryPrefetcher.cs`
  - `UnifiedMemoryBuffer.cs`
  - `CudaRuntime*.cs`
  - `NvrtcInterop.cs`

**Execution**:
```bash
# Requires NVIDIA GPU
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/ --configuration Release
```

#### Metal Backend Tests
- **Project**: `tests/Unit/DotCompute.Backends.Metal.Tests/`
- **Coverage**: Graph execution, memory management, telemetry
- **Modified Files**:
  - `MetalGraphExecutor.cs`
  - `MetalExecutionTypes.cs`
  - `MetalKernelCache.cs`
  - `MetalMemoryManager.cs`
  - `MetalTelemetryManager.cs`

**Execution**:
```bash
# macOS only
dotnet test tests/Unit/DotCompute.Backends.Metal.Tests/ --configuration Release
```

#### Algorithm Tests
- **Project**: `tests/Unit/DotCompute.Algorithms.Tests/`
- **Coverage**: Linear algebra, plugin system, security
- **Modified Files**: 20+ files in LinearAlgebra and Management

**Execution**:
```bash
dotnet test tests/Unit/DotCompute.Algorithms.Tests/ --configuration Release
```

---

### Phase 3: Integration Testing
**Objective**: Validate component interactions

**Test Projects**:
- `tests/Integration/DotCompute.Integration.Tests/`
- `tests/Integration/DotCompute.Generators.Integration.Tests/`
- `tests/Integration/DotCompute.Linq.Integration.Tests/`

**Key Scenarios**:
1. Cross-backend kernel execution (CPU ↔ CUDA)
2. Memory buffer transfers between backends
3. Plugin loading and execution
4. End-to-end computation pipelines

**Execution**:
```bash
dotnet test tests/Integration/ --configuration Release
```

---

### Phase 4: Performance Regression Testing
**Objective**: Ensure no performance degradation

**Execution**:
```bash
./docs/scripts/performance-regression-check.sh
```

**Benchmarks to Monitor**:
- SIMD operation throughput (CPU backend)
- CUDA kernel launch latency
- Memory allocation/pooling performance
- Plugin loading time

**Baseline Metrics** (from CLAUDE.md):
- CPU SIMD: 3.7x speedup
- Memory pooling: 90% reduction in allocations
- Native AOT: Sub-10ms startup

**Regression Threshold**: <5% degradation acceptable

---

## 2. Test Execution Scripts

All scripts are located in `docs/scripts/` and are executable:

### 1. incremental-verification.sh
**Purpose**: Full sequential test suite with comprehensive logging
**Runtime**: 15-30 minutes
**Usage**: `./docs/scripts/incremental-verification.sh`

### 2. quick-compile-check.sh
**Purpose**: Fast compilation verification (modified files only)
**Runtime**: 2-5 minutes
**Usage**: `./docs/scripts/quick-compile-check.sh`

### 3. performance-regression-check.sh
**Purpose**: Benchmark validation against baseline
**Runtime**: 10-20 minutes
**Usage**: `./docs/scripts/performance-regression-check.sh`

### 4. logger-delegate-verification.sh
**Purpose**: Validate LoggerMessage delegate conversions
**Runtime**: 1-3 minutes
**Usage**: `./docs/scripts/logger-delegate-verification.sh`

### 5. pinvoke-verification.sh
**Purpose**: Validate LibraryImport migrations
**Runtime**: 2-5 minutes
**Usage**: `./docs/scripts/pinvoke-verification.sh`

### 6. rollback-plan.sh
**Purpose**: Interactive rollback with backup creation
**Runtime**: Instant
**Usage**: `./docs/scripts/rollback-plan.sh`

---

## 3. Rollback Strategy

### When to Rollback
Trigger rollback if:
1. **Compilation Failure**: Any project fails to build
2. **Test Failure Rate**: >10% of existing tests fail
3. **Performance Regression**: >10% degradation in critical paths
4. **Integration Failure**: Cross-component tests fail

### Rollback Execution
```bash
./docs/scripts/rollback-plan.sh
```

**Options**:
1. Rollback specific file(s)
2. Rollback entire directory
3. Rollback all changes (nuclear option)
4. Stash changes for later review

---

## 4. Success Criteria

- ✅ **Build**: 100% success rate
- ✅ **Unit Tests**: ≥95% pass rate
- ✅ **Integration Tests**: ≥90% pass rate
- ✅ **Performance**: <5% regression
- ✅ **Code Quality**: No new analyzer warnings

---

## Quick Reference

```bash
# Daily verification
./docs/scripts/quick-compile-check.sh

# Full test run
./docs/scripts/incremental-verification.sh

# Specific verifications
./docs/scripts/logger-delegate-verification.sh
./docs/scripts/pinvoke-verification.sh

# Emergency rollback
./docs/scripts/rollback-plan.sh
```
