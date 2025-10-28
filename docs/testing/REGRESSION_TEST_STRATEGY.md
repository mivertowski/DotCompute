# DotCompute Cleanup Regression Test Strategy

## Executive Summary

This document outlines the comprehensive testing strategy to ensure **zero regressions** during the LoggerMessage consolidation and code cleanup. The strategy employs incremental validation with automated checkpoints after each module cleanup.

**Baseline Status (2025-10-22):**
- **Modified Files:** 11 files pending cleanup
- **Build Status:** 41 analyzer warnings/errors (CPU backend)
- **Test Projects:** 24 test projects across Unit/Integration/Hardware categories
- **Critical Systems:** CPU SIMD, CUDA integration, Memory management, Generators

---

## I. Testing Philosophy

### Core Principles
1. **Fail Fast:** Test immediately after each change
2. **Incremental:** Small, validated steps prevent compounding issues
3. **Reproducible:** Automated commands ensure consistency
4. **Rollback Ready:** Clear revert procedures at each checkpoint

### Success Criteria
- ✅ All existing tests pass (no test regressions)
- ✅ Build succeeds with zero warnings (Release configuration)
- ✅ LoggerMessage conversions compile and execute correctly
- ✅ Performance benchmarks show no degradation (±2% tolerance)
- ✅ Hardware tests (CUDA) maintain compatibility

---

## II. Module-Level Test Plan

### Phase 1: CPU Backend (8 Files)

#### Module 1A: SimdVectorOperations.cs
**Files:** `/src/Backends/DotCompute.Backends.CPU/SIMD/SimdVectorOperations.cs`

**Pre-Cleanup Validation:**
```bash
# Capture baseline test results
dotnet test tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj \
  --filter "FullyQualifiedName~Simd" \
  --configuration Release \
  --logger "trx;LogFileName=simd_baseline.trx" \
  --collect:"XPlat Code Coverage"

# Capture baseline warnings
dotnet build src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj \
  --configuration Release \
  /warnaserror \
  2>&1 | tee baseline_simd_warnings.log
```

**Cleanup Actions:**
- Remove unused field `_capabilities` (CA1823 error)
- Consolidate LoggerMessage definitions

**Post-Cleanup Validation:**
```bash
# Rebuild with warnings as errors
dotnet build src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj \
  --configuration Release \
  /warnaserror

# Run SIMD-specific tests
dotnet test tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj \
  --filter "FullyQualifiedName~Simd" \
  --configuration Release \
  --logger "console;verbosity=detailed"

# Compare coverage
dotnet test tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj \
  --filter "FullyQualifiedName~Simd" \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults/simd_post
```

**Success Criteria:**
- Zero CA1823 warnings
- All SIMD tests pass
- Code coverage remains ≥ baseline

**Rollback Command:**
```bash
git checkout HEAD -- src/Backends/DotCompute.Backends.CPU/SIMD/SimdVectorOperations.cs
```

---

#### Module 1B: CudaMemoryManager.cs
**Files:** `/src/Backends/DotCompute.Backends.CUDA/Integration/Components/CudaMemoryManager.cs`

**Pre-Cleanup Validation:**
```bash
# Test CUDA memory management
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Memory" \
  --configuration Release \
  --logger "trx;LogFileName=cuda_memory_baseline.trx"
```

**Cleanup Actions:**
- Convert ILogger calls to LoggerMessage.Define
- Optimize memory allocation logging

**Post-Cleanup Validation:**
```bash
# Rebuild CUDA backend
dotnet build src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj \
  --configuration Release \
  /warnaserror

# Run CUDA memory tests
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Memory" \
  --configuration Release

# Verify hardware integration
nvidia-smi && dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "Category=Hardware" \
  --configuration Release
```

**Success Criteria:**
- Zero XFIX003 (LoggerMessage) warnings
- All CUDA memory tests pass
- Hardware tests execute successfully (if GPU available)

**Rollback Command:**
```bash
git checkout HEAD -- src/Backends/DotCompute.Backends.CUDA/Integration/Components/CudaMemoryManager.cs
```

---

#### Module 1C: Error Handling Components
**Files:**
- `/src/Backends/DotCompute.Backends.CUDA/Integration/Components/ErrorHandling/ErrorRecoveryStrategies.cs`
- `/src/Backends/DotCompute.Backends.CUDA/Integration/Components/ErrorHandling/ErrorStatistics.cs`

**Pre-Cleanup Validation:**
```bash
# Test error handling paths
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Error" \
  --configuration Release \
  --logger "trx;LogFileName=cuda_error_baseline.trx"
```

**Cleanup Actions:**
- Consolidate error logging with LoggerMessage
- Ensure exception paths remain intact

**Post-Cleanup Validation:**
```bash
# Rebuild with strict error checking
dotnet build src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj \
  --configuration Release \
  /p:TreatWarningsAsErrors=true

# Test error recovery scenarios
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Error" \
  --configuration Release
```

**Success Criteria:**
- Error recovery logic unchanged
- All error tests pass
- Exception handling verified

**Rollback Command:**
```bash
git checkout HEAD -- src/Backends/DotCompute.Backends.CUDA/Integration/Components/ErrorHandling/
```

---

#### Module 1D: Memory Buffer Components
**Files:**
- `/src/Backends/DotCompute.Backends.CUDA/Memory/CudaMemoryBuffer.cs`
- `/src/Backends/DotCompute.Backends.CUDA/Persistent/CudaRingBufferAllocator.cs`

**Pre-Cleanup Validation:**
```bash
# Test memory buffers
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Buffer" \
  --configuration Release \
  --logger "trx;LogFileName=cuda_buffer_baseline.trx"

# Test memory pooling
dotnet test tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj \
  --filter "FullyQualifiedName~Pool" \
  --configuration Release
```

**Cleanup Actions:**
- Convert buffer logging to LoggerMessage
- Consolidate ring buffer allocator logging

**Post-Cleanup Validation:**
```bash
# Rebuild memory components
dotnet build src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj \
  --configuration Release \
  /warnaserror

# Test buffer operations
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj \
  --filter "FullyQualifiedName~Buffer" \
  --configuration Release

# Test pooling performance
dotnet test tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj \
  --configuration Release
```

**Success Criteria:**
- Buffer operations functional
- Memory pooling intact (90% allocation reduction)
- Performance benchmarks within tolerance

**Rollback Command:**
```bash
git checkout HEAD -- src/Backends/DotCompute.Backends.CUDA/Memory/ src/Backends/DotCompute.Backends.CUDA/Persistent/
```

---

### Phase 2: Metal Backend & Algorithms (3 Files)

#### Module 2A: MetalExecutionContext.cs
**Files:** `/src/Backends/DotCompute.Backends.Metal/Execution/MetalExecutionContext.cs`

**Pre-Cleanup Validation:**
```bash
# Test Metal backend (if on macOS)
dotnet test tests/Unit/DotCompute.Backends.Metal.Tests/DotCompute.Backends.Metal.Tests.csproj \
  --configuration Release \
  --logger "trx;LogFileName=metal_baseline.trx" || echo "Metal tests skipped (not macOS)"
```

**Cleanup Actions:**
- Convert Metal logging to LoggerMessage
- Ensure P/Invoke compatibility maintained

**Post-Cleanup Validation:**
```bash
# Rebuild Metal backend
dotnet build src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj \
  --configuration Release \
  /warnaserror

# Test Metal execution (platform-dependent)
dotnet test tests/Unit/DotCompute.Backends.Metal.Tests/DotCompute.Backends.Metal.Tests.csproj \
  --configuration Release || echo "Metal tests skipped"
```

**Success Criteria:**
- Metal backend compiles on all platforms
- Native interop unchanged
- Tests pass on macOS (if available)

**Rollback Command:**
```bash
git checkout HEAD -- src/Backends/DotCompute.Backends.Metal/Execution/MetalExecutionContext.cs
```

---

#### Module 2B: Algorithm Components
**Files:**
- `/src/Extensions/DotCompute.Algorithms/LinearAlgebra/Components/GpuSolverOperations.cs`
- `/src/Extensions/DotCompute.Algorithms/Management/Core/AlgorithmLoader.cs`
- `/src/Extensions/DotCompute.Algorithms/Management/Metadata/PluginMetadata.cs`
- `/src/Extensions/DotCompute.Algorithms/Types/Security/MalwareScanner.cs`

**Pre-Cleanup Validation:**
```bash
# Test algorithm components
dotnet test tests/Unit/DotCompute.Algorithms.Tests/DotCompute.Algorithms.Tests.csproj \
  --configuration Release \
  --logger "trx;LogFileName=algorithms_baseline.trx"
```

**Cleanup Actions:**
- Consolidate algorithm logging
- Optimize plugin loader logging
- Secure malware scanner logging

**Post-Cleanup Validation:**
```bash
# Rebuild algorithms
dotnet build src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj \
  --configuration Release \
  /warnaserror

# Test algorithm operations
dotnet test tests/Unit/DotCompute.Algorithms.Tests/DotCompute.Algorithms.Tests.csproj \
  --configuration Release
```

**Success Criteria:**
- Algorithm operations functional
- Plugin system intact
- Security scanning unchanged

**Rollback Command:**
```bash
git checkout HEAD -- src/Extensions/DotCompute.Algorithms/
```

---

## III. Automated Test Execution Scripts

### Script 1: Pre-Cleanup Baseline Capture
**File:** `scripts/test-baseline-capture.sh`

```bash
#!/bin/bash
set -e

echo "=== DotCompute Cleanup Baseline Capture ==="
echo "Timestamp: $(date -Iseconds)"
echo "Git Commit: $(git rev-parse HEAD)"
echo ""

# Create results directory
mkdir -p TestResults/baseline
cd TestResults/baseline

# Capture full solution build warnings
echo "[1/5] Capturing build warnings..."
dotnet build ../../DotCompute.sln \
  --configuration Release \
  --no-incremental \
  2>&1 | tee build_warnings_baseline.log

# Run all unit tests
echo "[2/5] Running unit tests..."
dotnet test ../../tests/Unit/ \
  --configuration Release \
  --logger "trx;LogFileName=unit_tests_baseline.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./unit

# Run integration tests
echo "[3/5] Running integration tests..."
dotnet test ../../tests/Integration/ \
  --configuration Release \
  --logger "trx;LogFileName=integration_tests_baseline.trx" \
  --results-directory ./integration

# Run hardware tests (if GPU available)
echo "[4/5] Running hardware tests..."
if command -v nvidia-smi &> /dev/null; then
    dotnet test ../../tests/Hardware/DotCompute.Hardware.Cuda.Tests/ \
      --configuration Release \
      --logger "trx;LogFileName=cuda_tests_baseline.trx" \
      --results-directory ./hardware
else
    echo "CUDA not available, skipping GPU tests"
fi

# Generate coverage report
echo "[5/5] Generating coverage summary..."
dotnet tool restore
dotnet reportgenerator \
  -reports:"**/*.cobertura.xml" \
  -targetdir:"./coverage" \
  -reporttypes:"TextSummary;Html"

echo ""
echo "=== Baseline Capture Complete ==="
echo "Results stored in: TestResults/baseline/"
echo ""
cat ./coverage/Summary.txt
```

---

### Script 2: Module Validation Runner
**File:** `scripts/test-module-validation.sh`

```bash
#!/bin/bash
set -e

MODULE_NAME=$1
MODULE_PATH=$2

if [ -z "$MODULE_NAME" ] || [ -z "$MODULE_PATH" ]; then
    echo "Usage: $0 <module_name> <module_path>"
    echo "Example: $0 SimdVectorOperations src/Backends/DotCompute.Backends.CPU/SIMD/SimdVectorOperations.cs"
    exit 1
fi

echo "=== Validating Module: $MODULE_NAME ==="
echo "Timestamp: $(date -Iseconds)"
echo "Path: $MODULE_PATH"
echo ""

# Create module-specific results directory
mkdir -p TestResults/modules/$MODULE_NAME
cd TestResults/modules/$MODULE_NAME

# Build the containing project
echo "[1/4] Building project..."
PROJECT_DIR=$(dirname $MODULE_PATH)
PROJECT_FILE=$(find ../../$PROJECT_DIR -name "*.csproj" | head -1)
dotnet build ../../$PROJECT_FILE \
  --configuration Release \
  /warnaserror \
  2>&1 | tee build_output.log

# Run project-specific tests
echo "[2/4] Running project tests..."
PROJECT_NAME=$(basename $PROJECT_FILE .csproj)
TEST_PROJECT=$(find ../../tests -name "${PROJECT_NAME}.Tests.csproj" -o -name "*${PROJECT_NAME}*.Tests.csproj" | head -1)

if [ -n "$TEST_PROJECT" ]; then
    dotnet test ../../$TEST_PROJECT \
      --configuration Release \
      --logger "console;verbosity=detailed" \
      --logger "trx;LogFileName=${MODULE_NAME}_tests.trx" \
      --collect:"XPlat Code Coverage" \
      --results-directory .
else
    echo "No test project found for $PROJECT_NAME"
fi

# Run solution-wide smoke test
echo "[3/4] Running solution smoke tests..."
dotnet test ../../DotCompute.sln \
  --configuration Release \
  --filter "Category=Smoke" \
  --logger "trx;LogFileName=${MODULE_NAME}_smoke.trx" \
  --results-directory .

# Check for new warnings
echo "[4/4] Checking for new warnings..."
NEW_WARNINGS=$(grep -c "warning" build_output.log || echo "0")
echo "New warnings detected: $NEW_WARNINGS"

if [ "$NEW_WARNINGS" -gt "0" ]; then
    echo "⚠️  WARNING: New warnings introduced!"
    grep "warning" build_output.log
fi

echo ""
echo "=== Module Validation Complete: $MODULE_NAME ==="
echo "Results: TestResults/modules/$MODULE_NAME/"
```

---

### Script 3: Full Regression Suite
**File:** `scripts/test-full-regression.sh`

```bash
#!/bin/bash
set -e

echo "=== DotCompute Full Regression Suite ==="
echo "Timestamp: $(date -Iseconds)"
echo "Git Branch: $(git branch --show-current)"
echo "Git Commit: $(git rev-parse HEAD)"
echo ""

# Create regression results directory
mkdir -p TestResults/regression
cd TestResults/regression

# Step 1: Clean build
echo "[1/7] Clean build..."
dotnet clean ../../DotCompute.sln
dotnet build ../../DotCompute.sln \
  --configuration Release \
  /p:TreatWarningsAsErrors=true \
  2>&1 | tee regression_build.log

# Step 2: Unit tests
echo "[2/7] Unit tests..."
dotnet test ../../tests/Unit/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_unit.trx" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./unit

# Step 3: Integration tests
echo "[3/7] Integration tests..."
dotnet test ../../tests/Integration/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_integration.trx" \
  --results-directory ./integration

# Step 4: Hardware tests
echo "[4/7] Hardware tests (CUDA)..."
if command -v nvidia-smi &> /dev/null; then
    nvidia-smi --query-gpu=name,driver_version,memory.total --format=csv
    dotnet test ../../tests/Hardware/DotCompute.Hardware.Cuda.Tests/ \
      --configuration Release \
      --logger "trx;LogFileName=regression_cuda.trx" \
      --results-directory ./hardware
else
    echo "CUDA not available, skipping GPU tests"
fi

# Step 5: Generator tests
echo "[5/7] Generator integration tests..."
dotnet test ../../tests/Integration/DotCompute.Generators.Integration.Tests/ \
  --configuration Release \
  --logger "trx;LogFileName=regression_generators.trx" \
  --results-directory ./generators

# Step 6: Performance benchmarks
echo "[6/7] Performance benchmarks..."
if [ -d "../../benchmarks" ]; then
    dotnet run --project ../../benchmarks/DotCompute.Benchmarks.csproj \
      --configuration Release \
      --filter "*VectorAdd*" \
      2>&1 | tee regression_benchmarks.log
else
    echo "Benchmarks not found, skipping"
fi

# Step 7: Coverage report
echo "[7/7] Generating coverage report..."
dotnet tool restore
dotnet reportgenerator \
  -reports:"**/*.cobertura.xml" \
  -targetdir:"./coverage" \
  -reporttypes:"TextSummary;HtmlInline_AzurePipelines;Cobertura"

echo ""
echo "=== Full Regression Suite Complete ==="
echo ""
echo "Summary:"
grep -E "Total tests|Passed|Failed|Skipped" regression_*.trx || echo "Test results in .trx files"
echo ""
echo "Coverage:"
cat ./coverage/Summary.txt
echo ""
echo "Results: TestResults/regression/"
```

---

## IV. Rollback Strategy

### Incremental Rollback (Per Module)
```bash
# Rollback single file
git checkout HEAD -- <file_path>

# Verify rollback
dotnet build --configuration Release
dotnet test --configuration Release
```

### Phase Rollback (Multiple Modules)
```bash
# Rollback entire phase (e.g., CPU backend)
git checkout HEAD -- src/Backends/DotCompute.Backends.CPU/

# Clean rebuild
dotnet clean
dotnet build DotCompute.sln --configuration Release

# Re-run tests
./scripts/test-full-regression.sh
```

### Emergency Rollback (Full Reset)
```bash
# Reset all modified files
git checkout HEAD -- $(git diff --name-only)

# Verify clean state
git status
dotnet build DotCompute.sln --configuration Release
dotnet test DotCompute.sln --configuration Release
```

---

## V. Success Criteria Checklist

### ✅ Build Quality
- [ ] Zero warnings in Release build
- [ ] Zero analyzer errors (CA*, XFIX*, VSTHRD*)
- [ ] All projects compile successfully
- [ ] Native AOT compatibility maintained

### ✅ Test Quality
- [ ] All unit tests pass (100%)
- [ ] All integration tests pass (100%)
- [ ] Hardware tests pass (if GPU available)
- [ ] No test timeouts or flaky tests

### ✅ Performance Quality
- [ ] Benchmarks within ±2% of baseline
- [ ] Memory usage unchanged
- [ ] Startup time <10ms (Native AOT)
- [ ] SIMD vectorization intact (3.7x speedup)

### ✅ Code Quality
- [ ] LoggerMessage patterns correct
- [ ] Exception handling preserved
- [ ] Resource disposal correct
- [ ] Thread safety maintained

---

## VI. Continuous Monitoring

### Post-Cleanup Validation Commands
```bash
# Daily regression check
./scripts/test-full-regression.sh

# Weekly coverage analysis
dotnet test --collect:"XPlat Code Coverage"
dotnet reportgenerator -reports:**/*.cobertura.xml -targetdir:./coverage

# Monthly performance baseline
dotnet run --project benchmarks/DotCompute.Benchmarks.csproj --configuration Release
```

### Metrics to Track
- Test pass rate (target: 100%)
- Code coverage (target: ≥75%)
- Build warnings (target: 0)
- Performance deviation (target: ±2%)

---

## VII. Communication Protocol

### Success Notification
```bash
echo "✅ Module <name> validated successfully"
echo "   Build: PASS"
echo "   Tests: X/X passed"
echo "   Coverage: X%"
echo "   Warnings: 0"
```

### Failure Notification
```bash
echo "❌ Module <name> validation FAILED"
echo "   Issue: <description>"
echo "   Rollback initiated: git checkout HEAD -- <file>"
echo "   See: TestResults/modules/<name>/build_output.log"
```

---

## VIII. Quick Reference

### Command Shortcuts
```bash
# Baseline capture
./scripts/test-baseline-capture.sh

# Module validation
./scripts/test-module-validation.sh SimdVectorOperations src/Backends/DotCompute.Backends.CPU/SIMD/SimdVectorOperations.cs

# Full regression
./scripts/test-full-regression.sh

# Emergency rollback
git checkout HEAD -- $(git diff --name-only)
```

### Test Categories
- `Category=Unit` - Fast, isolated tests
- `Category=Integration` - Cross-component tests
- `Category=Hardware` - GPU/CUDA tests (requires hardware)
- `Category=Smoke` - Quick sanity checks

---

**Document Version:** 1.0
**Last Updated:** 2025-10-22
**Maintainer:** DotCompute Test Team
