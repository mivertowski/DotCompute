# DotCompute Test Filtering and CI/CD Configuration

This directory contains comprehensive test filtering and CI/CD configuration for the DotCompute project, enabling separation of hardware-independent and hardware-dependent tests for efficient continuous integration.

## 🏗️ Architecture Overview

The test filtering system is designed with the following components:

- **GitHub Actions Workflows** (`.github/workflows/tests.yml`) - Multi-job CI/CD pipeline
- **MSBuild Properties** (`test-categories.props`) - Test filtering configuration
- **Test Categories** (`TestCategories.cs`) - Centralized category definitions
- **Test Scripts** (`run-tests.ps1`, `run-tests.sh`) - Cross-platform test execution
- **Directory Build Props** - Global test configuration and coverage thresholds

## 📋 Test Categories

### Core Categories
- **`Unit`** - Fast, isolated unit tests (no external dependencies)
- **`Integration`** - Integration tests with controlled dependencies
- **`Performance`** - Performance and benchmark tests
- **`Mock`** - Hardware simulation tests (no real hardware required)

### Hardware Categories
- **`Hardware`** - Generic hardware-dependent tests
- **`GPU`** - General GPU tests
- **`CUDA`** - NVIDIA CUDA-specific tests
- **`OpenCL`** - OpenCL cross-platform tests  
- **`DirectCompute`** - Microsoft DirectCompute (Windows only)
- **`Metal`** - Apple Metal (macOS only)

### Hardware Models
- **`RTX2000`** - RTX 20-series specific tests
- **`RTX3000`** - RTX 30-series specific tests
- **`RTX4000`** - RTX 40-series specific tests

### Special Categories
- **`LongRunning`** - Tests that take significant time
- **`MemoryIntensive`** - Memory-heavy tests
- **`LocalOnly`** - Tests that cannot run in CI
- **`Regression`** - Regression test suite
- **`Security`** - Security-focused tests

## 🚀 CI/CD Pipeline

### GitHub Actions Jobs

#### 1. Unit & Integration Tests (`unit-tests`)
- **Triggers**: All PRs and pushes
- **Runners**: `ubuntu-latest`
- **Matrix**: Debug/Release configurations
- **Filters**: Excludes hardware-dependent tests
- **Coverage**: Uploads to Codecov with `unittests` flag

#### 2. Mock Hardware Tests (`mock-hardware-tests`)
- **Triggers**: All PRs and pushes
- **Runners**: `ubuntu-latest`
- **Purpose**: Test hardware interfaces without real hardware
- **Coverage**: Uploads to Codecov with `mockhardware` flag

#### 3. Performance Tests (`performance-tests`)
- **Triggers**: Pull requests only
- **Purpose**: Detect performance regressions
- **Comparison**: Baseline vs current performance
- **Artifacts**: Performance results and comparisons

#### 4. Hardware Tests (`hardware-tests`)
- **Triggers**: Manual dispatch or main branch pushes
- **Runners**: `[self-hosted, gpu]` (requires GPU runners)
- **Matrix**: CUDA, OpenCL, DirectCompute
- **Hardware Detection**: Automatic capability checking
- **Coverage**: Per-hardware backend coverage

#### 5. Test Summary (`test-summary`)
- **Dependencies**: All other test jobs
- **Purpose**: Aggregate results and generate reports
- **Artifacts**: Combined test results and coverage

### Test Filtering Logic

```bash
# Hardware-independent (CI)
Category!=Hardware&Category!=GPU&Category!=CUDA&Category!=OpenCL&Category!=DirectCompute&Category!=Metal

# Mock hardware tests
Category=Mock

# Specific hardware backend
Category=CUDA|Category=GPU

# Performance tests
Category=Performance&Category!=Hardware
```

## 🛠️ Local Development

### Using Test Scripts

#### PowerShell (Windows/Cross-platform)
```powershell
# Run unit tests
./tests/run-tests.ps1 -Category Unit

# Run hardware tests with specific backend
./tests/run-tests.ps1 -Category Hardware -Hardware CUDA

# Run all tests without coverage
./tests/run-tests.ps1 -Category All -Coverage $false

# Performance tests with custom timeout
./tests/run-tests.ps1 -Category Performance -Timeout 600
```

#### Bash (Linux/macOS)
```bash
# Run unit tests
./tests/run-tests.sh --category Unit

# Run hardware tests with specific backend
./tests/run-tests.sh --category Hardware --hardware CUDA

# Run all tests without coverage
./tests/run-tests.sh --category All --coverage false

# Verbose output with custom configuration
./tests/run-tests.sh --category Integration --configuration Release --verbose
```

### Using MSBuild Targets

```bash
# Run CI-compatible tests
dotnet build -t:RunCITests

# Run all tests including hardware
dotnet build -t:RunAllTests

# Check coverage thresholds
dotnet build -t:CheckCoverage
```

### Direct dotnet test Commands

```bash
# Unit tests only
dotnet test --filter "Category!=Hardware&Category!=GPU&Category!=CUDA&Category!=OpenCL&Category!=DirectCompute&Category!=Metal"

# CUDA tests only  
dotnet test --filter "Category=CUDA|Category=GPU"

# Integration tests (hardware-independent)
dotnet test --filter "Category=Integration&Category!=Hardware"

# Mock hardware tests
dotnet test --filter "Category=Mock"
```

## 📊 Coverage Configuration

### Thresholds (configurable in Directory.Build.props)
- **Overall Coverage**: 80%
- **Unit Test Coverage**: 85%
- **Integration Test Coverage**: 70%

### Coverage Reports
- **Format**: OpenCover XML + HTML + Cobertura
- **Location**: `TestResults/Coverage/`
- **Uploads**: Automatic to Codecov in CI

### Coverage Collection
```bash
# With coverage (default)
dotnet test --collect:"XPlat Code Coverage"

# Custom coverage output
dotnet test --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

## 🔧 Configuration Files

### `test-categories.props`
MSBuild properties file containing:
- Test filter definitions
- Coverage thresholds
- Timeout configurations
- Default test sets
- MSBuild targets for test execution

### `TestCategories.cs`
Central definition of all test categories with:
- Category constants
- Helper methods for category validation
- Hardware requirement checking
- CI compatibility verification

### `Directory.Build.props` (Enhanced)
Global configuration including:
- Test project detection
- Automatic test dependency inclusion
- Coverage configuration
- Test-specific MSBuild targets

## 🎯 Hardware Detection

### Automatic Detection
The test scripts automatically detect available hardware:

```bash
# CUDA Detection
nvidia-smi --query-gpu=name --format=csv,noheader,nounits

# OpenCL Detection  
clinfo -l

# DirectCompute Detection
# Windows OS check

# Metal Detection
# macOS OS check
```

### Hardware Requirements Matrix

| Backend | OS Support | Detection Command | Test Categories |
|---------|------------|-------------------|-----------------|
| CUDA | Windows, Linux | `nvidia-smi` | `CUDA`, `GPU` |
| OpenCL | All | `clinfo` | `OpenCL`, `GPU` |
| DirectCompute | Windows | OS check | `DirectCompute`, `GPU` |
| Metal | macOS | OS check | `Metal`, `GPU` |

## 📈 Performance Monitoring

### Regression Detection
- **Baseline**: Previous commit on target branch
- **Current**: PR changes
- **Comparison**: Automated performance delta analysis
- **Thresholds**: Configurable performance regression limits

### Performance Test Categories
- **Category=Performance**: General performance tests
- **Category=Performance&Category!=Hardware**: CI-compatible performance tests
- **Category=LongRunning**: Extended performance tests

## 🚨 Troubleshooting

### Common Issues

#### 1. Hardware Tests Failing in CI
```bash
# Solution: Check if running on self-hosted GPU runner
# Verify hardware detection in workflow logs
```

#### 2. Coverage Thresholds Not Met
```bash
# Check coverage reports in TestResults/Coverage/
# Adjust thresholds in Directory.Build.props if needed
# Add more unit tests to improve coverage
```

#### 3. Test Categories Not Working
```bash
# Ensure test-categories.props is imported correctly
# Verify [Trait("Category", "YourCategory")] attributes on tests
# Check test project references TestCategories.cs
```

#### 4. Mock Tests vs Real Hardware
```bash
# Mock tests: Use Category="Mock" - should run everywhere
# Real hardware: Use Category="CUDA" - needs actual hardware
# Integration: Combine both for comprehensive testing
```

### Debug Commands

```bash
# List all test methods with categories
dotnet test --list-tests --verbosity normal

# Run specific test with detailed output
dotnet test --filter "FullyQualifiedName~YourTestMethod" --verbosity diagnostic

# Check available GPU hardware
nvidia-smi  # For CUDA
clinfo      # For OpenCL
```

## 🔄 Integration with Development Workflow

### Pull Request Process
1. **Automatic Triggers**: Unit and integration tests run on all PRs
2. **Performance Check**: Regression detection on performance-critical changes
3. **Coverage Validation**: Ensures coverage thresholds are maintained
4. **Hardware Tests**: Optional for hardware-related changes

### Main Branch Process
1. **Full Test Suite**: All tests including hardware-dependent
2. **Coverage Upload**: Complete coverage metrics to Codecov
3. **Performance Baseline**: Establishes new performance baselines

### Local Development
1. **Quick Feedback**: Run unit tests during development
2. **Integration Testing**: Test with mock hardware for rapid iteration
3. **Hardware Validation**: Test with real hardware before PR
4. **Performance Profiling**: Use performance tests for optimization

## 📝 Adding New Tests

### Test Method Template
```csharp
[Fact]
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Core)]
public void YourTestMethod()
{
    // Your test implementation
}

[Fact]
[Trait("Category", TestCategories.CUDA)]
[Trait("Category", TestCategories.Hardware)]
public void YourHardwareTest()
{
    // Hardware-dependent test
    // Will only run when CUDA is available
}
```

### New Category Guidelines
1. **Add to TestCategories.cs**: Define new category constant
2. **Update test-categories.props**: Add filter definitions
3. **Update CI workflow**: Add to appropriate job filters
4. **Document**: Update this README with new category usage

## 🎛️ Configuration Customization

### Environment Variables
```bash
# Test configuration
export DOTNET_CLI_UI_LANGUAGE=en
export VSTEST_HOST_DEBUG=0

# Coverage configuration  
export COVERAGE_THRESHOLD=85
export UNIT_TEST_COVERAGE_THRESHOLD=90

# Hardware test configuration
export CUDA_TEST_TIMEOUT=600
export HARDWARE_TEST_PARALLEL=false
```

### MSBuild Properties
```xml
<!-- Custom coverage thresholds -->
<PropertyGroup>
  <CoverageThreshold>85</CoverageThreshold>
  <UnitTestCoverageThreshold>90</UnitTestCoverageThreshold>
</PropertyGroup>

<!-- Custom test filters -->
<PropertyGroup>
  <CustomUnitTestFilter>Category=Unit&amp;Category!=Slow</CustomUnitTestFilter>
</PropertyGroup>
```

This comprehensive test filtering system enables efficient CI/CD while supporting local development with full hardware testing capabilities.