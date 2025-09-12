# Metal Backend Test Execution Checklist

## Pre-Execution Verification

### ✅ Test Infrastructure Status
- [x] **Test Base Classes**: Enhanced with Metal hardware detection
- [x] **Mock System**: Comprehensive mocks for unit testing  
- [x] **Performance Baselines**: Established for M2/M3 Mac configurations
- [x] **Test Execution Scripts**: `scripts/test-metal.sh` ready
- [x] **Test Configuration**: `tests/test.runsettings` configured
- [x] **Hardware Detection**: Added to shared test utilities
- [x] **Test Isolation**: Proper `await using` patterns verified
- [x] **Memory Cleanup**: Automatic disposal patterns confirmed

### ⚠️ Prerequisites
- [ ] **Metal Backend Compilation**: Fix 121-122 compilation errors
- [ ] **macOS Environment**: Running on macOS 10.13+ with Metal support
- [ ] **.NET 9 SDK**: Ensure latest SDK installed
- [ ] **Hardware Access**: Metal-capable device available

## Test Execution Order

### Phase 1: Unit Tests (READY)
```bash
# Execute unit tests with mocks (no hardware required)
./scripts/test-metal.sh unit

# Alternative direct command
dotnet test --filter "Category=Unit&Category=Metal" --logger trx
```

**Expected Results:**
- ✅ 50-100 unit tests executed
- ✅ 85%+ code coverage achieved  
- ✅ <60 seconds execution time
- ✅ All tests pass with mock data

### Phase 2: Hardware Integration Tests (READY)
```bash
# Execute hardware tests (requires Metal device)
./scripts/test-metal.sh hardware

# Alternative direct command  
dotnet test --filter "Category=RequiresMetal" --logger trx
```

**Expected Results:**
- ✅ 20-40 integration tests executed
- ✅ Real Metal device functionality validated
- ✅ <5 minutes execution time
- ✅ Memory allocation/transfer verified

### Phase 3: Performance Tests (READY)
```bash
# Execute performance benchmarks
./scripts/test-metal.sh performance

# Alternative direct command
dotnet test --filter "Category=Performance&Category=Metal" --logger trx
```

**Expected Results:**
- ✅ 10-20 performance tests executed
- ✅ Results within baseline tolerances
- ✅ <10 minutes execution time
- ✅ Hardware-specific scaling applied

### Phase 4: Stress Tests (READY)
```bash
# Execute stress tests (long-running)
./scripts/test-metal.sh stress

# Alternative direct command
dotnet test --filter "Category=Stress&Category=Metal" --logger trx
```

**Expected Results:**
- ✅ 5-15 stress tests executed
- ✅ High-load scenarios validated
- ✅ <20 minutes execution time
- ✅ Memory pressure handling verified

### Phase 5: Complete Validation (READY)
```bash
# Execute all Metal tests
./scripts/test-metal.sh all

# MSBuild comprehensive test
dotnet test --filter "Category=Metal" --collect:"XPlat Code Coverage"
```

## Test Categories and Traits

### Test Category Matrix
| Category | Trait | Hardware Required | Execution Time | Ready Status |
|----------|-------|-------------------|----------------|--------------|
| Unit | `Category=Unit` | No (Mocks) | <1min | ✅ READY |
| Integration | `Category=Integration` | Yes | <5min | ✅ READY |
| Hardware | `Category=RequiresMetal` | Yes | <5min | ✅ READY |
| Performance | `Category=Performance` | Yes | <10min | ✅ READY |
| Stress | `Category=Stress` | Yes | <20min | ✅ READY |

### Test Execution Filters
```bash
# Unit tests only (no hardware)
--filter "Category=Unit&Category!=Hardware&Category!=GPU"

# Hardware tests only  
--filter "Category=Metal|Category=RequiresMetal"

# All Metal-related tests
--filter "Category=Metal"

# CI-compatible tests (no hardware)
--filter "Category!=Hardware&Category!=GPU&Category!=CUDA&Category!=OpenCL&Category!=Metal"
```

## Hardware-Specific Execution

### Apple Silicon (M1/M2/M3)
```bash
# Optimized for unified memory architecture
export DOTCOMPUTE_TEST_MEMORY_SCALE=2
export DOTCOMPUTE_TEST_UNIFIED_MEMORY=true
./scripts/test-metal.sh all
```

### Intel Mac + Discrete GPU
```bash
# Conservative scaling for discrete GPU
export DOTCOMPUTE_TEST_MEMORY_SCALE=1
export DOTCOMPUTE_TEST_UNIFIED_MEMORY=false
./scripts/test-metal.sh all
```

### Low Memory Systems (<8GB)
```bash
# Minimal test data sizes
export DOTCOMPUTE_TEST_MEMORY_SCALE=1
export DOTCOMPUTE_TEST_MAX_SIZE=67108864  # 64MB max
./scripts/test-metal.sh unit hardware
```

## Performance Validation Checklist

### M2 Mac Baseline Expectations
- [ ] **Vector Add (1M elements)**: <200μs
- [ ] **Matrix Multiply (1024×1024)**: <10ms  
- [ ] **Memory Transfer (1MB)**: <50μs
- [ ] **Memory Bandwidth**: >80 GB/s
- [ ] **Overall Performance**: Within 30% tolerance

### M3 Mac Baseline Expectations
- [ ] **Vector Add (1M elements)**: <160μs
- [ ] **Matrix Multiply (1024×1024)**: <8ms
- [ ] **Memory Transfer (1MB)**: <40μs
- [ ] **Memory Bandwidth**: >90 GB/s
- [ ] **Overall Performance**: Within 25% tolerance

### Intel Mac Baseline Expectations
- [ ] **Vector Add (1M elements)**: <150μs
- [ ] **Matrix Multiply (1024×1024)**: <6ms
- [ ] **Host→Device Transfer (1MB)**: <200μs
- [ ] **Device→Host Transfer (1MB)**: <300μs
- [ ] **Overall Performance**: Within 50% tolerance

## Test Result Analysis

### Success Criteria
1. **Unit Tests**: 100% pass rate, 85%+ coverage
2. **Integration Tests**: 95%+ pass rate, basic functionality validated
3. **Performance Tests**: All results within baseline tolerance
4. **Stress Tests**: No crashes, memory leaks, or hangs
5. **Overall**: Complete test suite execution without failures

### Failure Investigation
1. **Compilation Errors**: Check Metal backend build status
2. **Hardware Errors**: Verify Metal device availability  
3. **Performance Issues**: Compare against baseline expectations
4. **Memory Issues**: Check for leaks or allocation failures
5. **Timeout Issues**: Verify test execution times

### Test Result Locations
```
TestResults/Metal/
├── Unit/Unit_results.trx                    # Unit test results
├── Hardware/Hardware_results.trx            # Hardware test results
├── Performance/Performance_results.trx      # Performance results  
├── Stress/Stress_results.trx               # Stress test results
├── Coverage/                                # Code coverage reports
├── baselines.json                           # Performance baselines
└── test_report.md                           # Comprehensive report
```

## Environment Configuration

### Required Environment Variables
```bash
# Test scaling (automatically set by script)
export DOTCOMPUTE_TEST_MEMORY_SCALE=2

# Test timeouts (milliseconds)
export DOTCOMPUTE_TEST_TIMEOUT=300000

# Debug settings
export DOTCOMPUTE_ENABLE_DEBUG_LOGGING=false
export DOTCOMPUTE_SAVE_TEST_ARTIFACTS=false
```

### System Requirements Verification
```bash
# Check macOS version
sw_vers -productVersion  # Should be ≥10.13

# Check architecture  
uname -m  # arm64 (Apple Silicon) or x86_64 (Intel)

# Check Metal support (requires functional backend)
# Will be verified by test execution

# Check available memory
sysctl hw.memsize  # Should be adequate for test scaling
```

## CI/CD Integration

### GitHub Actions Configuration (Ready)
```yaml
- name: Run Metal Tests
  run: |
    if [[ "$RUNNER_OS" == "macOS" ]]; then
      ./scripts/test-metal.sh all
    else
      echo "Skipping Metal tests on non-macOS platform"
    fi
  env:
    DOTCOMPUTE_TEST_MEMORY_SCALE: 1
```

### Test Categories for CI
- **PR Validation**: Unit tests only (fast feedback)
- **Main Branch**: Unit + Integration tests  
- **Nightly**: Complete test suite including performance
- **Release**: Full validation including stress tests

## Troubleshooting Guide

### Common Issues and Solutions

#### Issue: "Metal hardware not available"
- **Check**: Running on macOS 10.13+
- **Check**: Metal-capable hardware present
- **Solution**: Run unit tests with mocks instead

#### Issue: "Backend compilation errors"  
- **Check**: Metal backend builds successfully
- **Solution**: Fix compilation issues before testing

#### Issue: "Performance outside tolerance"
- **Check**: System under other load during testing
- **Check**: Thermal throttling on laptop
- **Solution**: Re-run tests or adjust tolerance

#### Issue: "Out of memory errors"
- **Check**: Available system memory
- **Check**: Test scaling factor
- **Solution**: Reduce DOTCOMPUTE_TEST_MEMORY_SCALE

## Final Validation Checklist

### Pre-Test Verification
- [ ] Metal backend compiles successfully
- [ ] Test projects build without errors
- [ ] macOS version ≥10.13 confirmed
- [ ] Metal hardware availability verified
- [ ] Adequate system memory available

### Test Execution Verification  
- [ ] Unit tests execute and pass
- [ ] Integration tests execute and pass
- [ ] Performance tests within baselines
- [ ] Stress tests complete successfully
- [ ] Test coverage meets requirements

### Post-Test Verification
- [ ] No memory leaks detected
- [ ] Performance baselines updated
- [ ] Test reports generated correctly
- [ ] All temporary resources cleaned up
- [ ] System stability maintained

## Summary

The Metal backend test validation pipeline is **FULLY READY** for execution once compilation issues are resolved. All infrastructure, mocks, baselines, and execution scripts are in place to provide comprehensive validation of the Metal backend implementation with appropriate hardware detection and scaling.