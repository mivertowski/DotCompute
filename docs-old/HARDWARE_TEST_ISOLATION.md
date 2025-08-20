# Hardware Test Isolation for CI/CD Pipeline

This document describes the comprehensive strategy for isolating hardware-dependent tests in the DotCompute project, allowing the CI/CD pipeline to run successfully without GPU hardware while preserving full hardware testing capabilities for local development.

## Overview

The DotCompute project contains tests that require specific hardware:
- **CUDA tests** - Require NVIDIA GPU with CUDA support
- **OpenCL tests** - Require OpenCL runtime and compatible devices
- **DirectCompute tests** - Require Windows with DirectX 11+
- **Metal tests** - Require macOS with Metal support
- **RTX2000 tests** - Require specific NVIDIA RTX 2000 Ada Generation GPU

## Test Categories

### Hardware-Required Categories
- `HardwareRequired` - General hardware dependency
- `CudaRequired` - Requires NVIDIA CUDA GPU
- `OpenCLRequired` - Requires OpenCL runtime
- `DirectComputeRequired` - Requires Windows/DirectX
- `MetalRequired` - Requires macOS/Metal
- `RTX2000` - Requires RTX 2000 Ada Gen GPU

### CI-Safe Categories  
- `Mock` - Mock/simulation tests
- `CI` - CI/CD safe tests
- `Unit` - Standard unit tests
- `Performance` - Performance tests (some may require hardware)

## Test Project Structure

```
tests/
├── Hardware/                          # Hardware-dependent tests
│   ├── DotCompute.Hardware.Cuda.Tests/
│   ├── DotCompute.Hardware.OpenCL.Tests/
│   ├── DotCompute.Hardware.DirectCompute.Tests/
│   ├── DotCompute.Hardware.RTX2000.Tests/
│   └── DotCompute.Hardware.Mock.Tests/   # Mock implementations
├── Unit/                               # Hardware-independent unit tests
├── Integration/                        # Integration tests
└── Shared/                            # Shared test utilities
```

## Test Configuration Files

### CI/CD Configuration (`tests/ci-test.runsettings`)
- **Purpose**: Run tests in CI/CD environments without hardware
- **Includes**: Mock, Unit, CI-safe tests
- **Excludes**: All hardware-dependent tests
- **Filter**: `(TestCategory=Mock|TestCategory=CI|TestCategory=Unit)&TestCategory!=HardwareRequired&TestCategory!=CudaRequired&TestCategory!=OpenCLRequired&TestCategory!=DirectComputeRequired&TestCategory!=RTX2000`

### Local Development (`tests/local-hardware-test.runsettings`)
- **Purpose**: Comprehensive local testing with available hardware
- **Includes**: All tests
- **Excludes**: Only tests marked as `ExcludeFromLocal`
- **Parallelization**: Disabled for hardware tests to prevent conflicts

### Hardware-Only Testing (`tests/hardware-only-test.runsettings`)
- **Purpose**: Focus on hardware-specific validation
- **Includes**: Only hardware-dependent tests
- **Excludes**: Mock and unit tests
- **Filter**: `TestCategory=HardwareRequired|TestCategory=CudaRequired|TestCategory=OpenCLRequired|TestCategory=DirectComputeRequired|TestCategory=RTX2000`

## Mock/Simulation Tests

Hardware operations are simulated in CI environments:

### CUDA Simulation (`CudaSimulationTests.cs`)
- API structure validation
- Memory calculation verification
- Kernel execution logic simulation
- Launch configuration optimization
- Compute capability feature detection

### OpenCL Simulation (`OpenCLSimulationTests.cs`)
- Platform and device enumeration simulation
- Kernel compilation validation
- Work group optimization calculation
- Memory transfer performance modeling
- Error code handling verification

### DirectCompute Simulation (`DirectComputeSimulationTests.cs`)
- Feature level detection simulation
- HLSL shader compilation validation
- Adapter enumeration simulation
- Resource binding validation
- Platform compatibility checking

## Execution Scripts

### CI/CD Script (`scripts/run-ci-tests.sh`)
```bash
./scripts/run-ci-tests.sh
```
- Runs only CI-safe tests
- Excludes all hardware dependencies
- Suitable for GitHub Actions, Azure DevOps, etc.
- Provides clear success/failure reporting

### Hardware Testing Script (`scripts/run-hardware-tests.sh`)
```bash
./scripts/run-hardware-tests.sh
```
- Detects available hardware (CUDA, OpenCL, DirectCompute)
- Runs appropriate hardware tests
- Warns about missing hardware
- Provides detailed hardware detection output

### Complete Testing Script (`scripts/run-all-tests.sh`)
```bash
./scripts/run-all-tests.sh
```
- Runs all tests (CI + hardware)
- Best for comprehensive local validation
- Combines all test categories

## GitHub Actions Integration

### CI Workflow (`.github/workflows/ci-tests.yml`)
- Runs on every push/PR
- Uses CI test configuration
- Excludes hardware tests
- Provides coverage reporting
- Uploads test results and coverage reports

### Hardware Workflow (`.github/workflows/hardware-tests.yml`)
- Optional workflow for self-hosted runners
- Requires manual trigger or scheduled execution
- Detects available hardware
- Runs appropriate hardware test suites
- Provides separate reporting for hardware tests

## Test Implementation Guidelines

### Adding Hardware-Dependent Tests

1. **Place in appropriate hardware test project**:
   ```csharp
   // For CUDA tests
   namespace DotCompute.Hardware.Cuda.Tests;
   
   [Trait("Category", "HardwareRequired")]
   [Trait("Category", "CudaRequired")]
   [Collection("Hardware")]
   public class MyCudaTest
   {
       [SkippableFact]
       [Trait("Category", "CudaRequired")]
       public void TestCudaFeature()
       {
           Skip.IfNot(CudaBackend.IsAvailable(), "CUDA not available");
           // Test implementation
       }
   }
   ```

2. **Use `SkippableFact` attribute** for hardware tests
3. **Add appropriate `Trait` attributes** for categorization
4. **Include hardware detection** in test setup
5. **Provide meaningful skip messages**

### Adding Mock/Simulation Tests

1. **Place in Mock test project**:
   ```csharp
   namespace DotCompute.Hardware.Mock.Tests;
   
   [Trait("Category", "Mock")]
   [Trait("Category", "CI")]
   public class MyMockTest
   {
       [Fact]
       [Trait("Category", "Mock")]
       public void TestSimulatedBehavior()
       {
           // Simulation implementation
       }
   }
   ```

2. **Focus on algorithm validation** without hardware
3. **Test edge cases and error conditions**
4. **Validate calculations and logic**

## Best Practices

### Test Organization
- **Separate hardware tests** from business logic tests
- **Create mock equivalents** for complex hardware operations
- **Use clear naming conventions** indicating hardware requirements
- **Group related tests** in appropriate collections

### Hardware Detection
- **Always check hardware availability** before running hardware tests
- **Provide clear skip messages** explaining why tests were skipped
- **Fail gracefully** when hardware is not available
- **Log hardware detection results** for debugging

### CI/CD Optimization
- **Run mock tests in CI** to validate logic without hardware
- **Use appropriate test filters** to exclude hardware tests
- **Provide coverage reports** for non-hardware code paths
- **Keep CI test runs fast** by avoiding expensive operations

### Local Development
- **Run comprehensive test suites** locally before committing
- **Test with actual hardware** when available
- **Validate mock behavior** against real hardware when possible
- **Use hardware detection** to skip unavailable tests gracefully

## Troubleshooting

### Common Issues

1. **Tests hang in CI**: Hardware tests not properly excluded
   - **Solution**: Verify test filters in runsettings files

2. **Hardware tests always skip**: Hardware not detected
   - **Solution**: Check hardware detection logic and drivers

3. **Tests fail randomly**: Parallel execution conflicts
   - **Solution**: Disable parallelization for hardware tests

4. **Coverage reports incomplete**: Hardware code not covered
   - **Solution**: Ensure mock tests cover equivalent logic

### Debugging Hardware Detection

```bash
# Check CUDA
nvidia-smi -L

# Check OpenCL  
clinfo -l

# Check DirectCompute (Windows)
dxdiag /t dxdiag.txt
```

## Migration Checklist

When adding new hardware-dependent functionality:

- [ ] Create hardware tests with appropriate traits
- [ ] Add mock/simulation tests for CI
- [ ] Update test filters if needed
- [ ] Test with hardware detection
- [ ] Verify CI pipeline exclusion
- [ ] Document hardware requirements
- [ ] Update test scripts if needed

## Summary

This hardware test isolation strategy ensures:

✅ **CI/CD pipelines run successfully** without hardware dependencies  
✅ **Full hardware testing** remains available for local development  
✅ **Test coverage** is maintained through mock implementations  
✅ **Clear separation** between hardware and business logic tests  
✅ **Flexible execution** based on available hardware  
✅ **Comprehensive documentation** for maintenance and debugging  

The approach provides robust testing while maintaining the practical requirements of modern CI/CD workflows.