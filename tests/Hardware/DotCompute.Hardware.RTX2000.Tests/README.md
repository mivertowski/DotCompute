# RTX 2000 Ada Generation Hardware Test Suite

This comprehensive test suite validates real GPU functionality on the NVIDIA RTX 2000 Ada Generation GPU, providing thorough hardware validation, performance benchmarking, and integration testing.

## Overview

The RTX 2000 Ada Generation test suite includes:

- **Hardware Validation Tests**: Verify GPU specifications and capabilities
- **NVRTC Compilation Tests**: Test runtime kernel compilation and execution
- **Performance Benchmarks**: Measure and validate memory bandwidth, compute throughput, and latency
- **Integration Tests**: End-to-end workflow validation from LINQ to GPU execution
- **Stress Tests**: Thermal stability, memory allocation stress, and error recovery
- **Utilities**: Helper classes and CUDA kernel libraries for testing

## Test Categories

### 1. Hardware Validation (`RTX2000HardwareValidationTests`)

Validates RTX 2000 Ada Gen specific hardware characteristics:
- Compute capability 8.9 validation
- 8GB GDDR6 memory verification
- Multiprocessor count and specifications
- Memory bandwidth measurement against GDDR6 specs
- Concurrent kernel execution capability
- Error handling and recovery validation

**Key Tests:**
- `ValidateComputeCapability89_ShouldConfirmRTX2000AdaGen()`
- `ValidateGDDR6Memory_ShouldReportCorrectSpecifications()`
- `MeasureActualMemoryBandwidth_ShouldMeetGDDR6Specifications()`
- `TestConcurrentKernelExecution_ShouldSupportMultipleStreams()`

### 2. NVRTC Compilation (`NVRTCKernelCompilationTests`)

Tests real-time kernel compilation using NVIDIA Runtime Compilation:
- Simple kernel compilation for compute capability 8.9
- Complex kernel compilation with optimization flags
- Kernel execution and result validation
- Compilation performance benchmarking

**Key Tests:**
- `CompileSimpleKernel_ShouldSucceed()`
- `CompileAndExecuteKernel_ShouldProduceCorrectResults()`
- `CompileComplexKernel_WithOptimizations_ShouldSucceed()`
- `CompilationBenchmark_ShouldMeetPerformanceExpectations()`

### 3. Performance Benchmarks (`RTX2000PerformanceBenchmarks`)

Comprehensive performance validation and baseline establishment:
- Memory bandwidth testing (H2D, D2H, D2D)
- Kernel launch overhead measurement
- Compute throughput (GFLOPS) validation
- Memory latency testing
- Performance regression detection

**Key Tests:**
- `BenchmarkMemoryBandwidth_ShouldMeetGDDR6Specifications()`
- `BenchmarkKernelLaunchOverhead_ShouldBeMinimal()`
- `BenchmarkComputeThroughput_ShouldMeetFP32Specifications()`
- `BenchmarkMemoryLatency_ShouldBeOptimal()`

### 4. Integration Tests (`EndToEndWorkflowTests`)

End-to-end workflow validation:
- Complete vector addition pipeline
- Matrix multiplication workflows
- Complex data processing pipelines
- LINQ to GPU compilation integration

**Key Tests:**
- `CompleteVectorAdditionWorkflow_ShouldExecuteEndToEnd()`
- `CompleteMatrixMultiplicationWorkflow_ShouldExecuteEndToEnd()`
- `ComplexDataPipelineWorkflow_ShouldProcessLargeDataset()`

### 5. Stress Tests (`HardwareStressTests`)

System stability and robustness testing:
- Memory allocation stress testing
- Thermal stress testing with intensive compute workloads
- Concurrent stream stress testing
- Error recovery validation

**Key Tests:**
- `MemoryAllocationStress_ShouldHandleExtremeAllocations()`
- `ThermalStressTest_ShouldMaintainStability()`
- `ConcurrentStreamStressTest_ShouldMaintainPerformance()`
- `ErrorRecoveryStressTest_ShouldRecoverGracefully()`

## RTX 2000 Ada Gen Specifications

The test suite validates against these RTX 2000 Ada Gen specifications:

- **Compute Capability**: 8.9
- **Memory**: 8GB GDDR6
- **Memory Bus**: 128-bit
- **Memory Bandwidth**: ~224 GB/s
- **Streaming Multiprocessors**: ~35 (approximate)
- **CUDA Cores**: ~2816 (approximate)
- **Base Clock**: ~1470 MHz
- **Boost Clock**: ~2610 MHz

## Usage

### Quick Start

Run the complete test suite:
```bash
./run-hardware-tests.sh
```

### Selective Testing

Run specific test categories:
```bash
./run-hardware-tests.sh validation     # Hardware validation only
./run-hardware-tests.sh nvrtc          # NVRTC compilation only
./run-hardware-tests.sh performance    # Performance benchmarks only
./run-hardware-tests.sh integration    # Integration tests only
./run-hardware-tests.sh stress         # Stress tests only
./run-hardware-tests.sh baseline       # Create performance baseline
```

### Manual Testing

Run tests using dotnet CLI:
```bash
# Build the test project
dotnet build --configuration Release

# Run all RTX 2000 tests
dotnet test --filter "Category=RTX2000"

# Run specific test categories
dotnet test --filter "Category=HardwareValidation"
dotnet test --filter "Category=NVRTC"
dotnet test --filter "Category=Performance"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=StressTest"

# Run with detailed output
dotnet test --filter "Category=RTX2000" --verbosity detailed
```

## Prerequisites

### Hardware Requirements
- NVIDIA RTX 2000 Ada Generation GPU (or compatible)
- Sufficient system memory (16GB+ recommended)
- PCIe 4.0 x16 slot (recommended)
- Adequate power supply and cooling

### Software Requirements
- NVIDIA GPU drivers (version 536.25 or later)
- CUDA Toolkit 12.0 or later
- .NET 9.0 runtime
- Windows 10/11 or Linux (Ubuntu 20.04+)

### Installation
1. Install NVIDIA drivers and CUDA toolkit
2. Install .NET 9.0 SDK
3. Verify GPU detection: `nvidia-smi`
4. Verify CUDA installation: `nvcc --version`

## Test Data and Kernels

### Test Kernels (`Kernels/TestKernels.cu`)

The suite includes optimized CUDA kernels for validation:
- **Vector Addition**: Basic functionality testing
- **Matrix Multiplication**: Standard and tiled implementations
- **Memory Bandwidth**: Bandwidth stress testing
- **Compute Intensive**: GFLOPS measurement
- **Reduction**: Warp-level operations testing
- **Stress Testing**: Thermal and stability validation
- **FFT Butterfly**: Signal processing validation

### Utilities (`RTX2000TestUtilities`)

Helper utilities provide:
- Hardware specification validation
- NVRTC compilation helpers
- Performance measurement utilities
- Test data generation
- Result validation functions

## Performance Baselines

The test suite establishes and maintains performance baselines:

### Baseline Metrics
- **Memory Bandwidth**: H2D, D2H, D2D measurements
- **Kernel Launch Overhead**: Microsecond precision timing
- **Compute Throughput**: GFLOPS measurement
- **Memory Latency**: Nanosecond precision measurement

### Regression Testing
- Baselines stored in JSON format
- Automatic comparison with previous results
- Configurable tolerance thresholds
- Trend analysis capabilities

## Results and Reporting

Test results are saved to `hardware-test-results/`:
- **Test Results**: XML format with detailed outcomes
- **Performance Data**: JSON baselines and measurements
- **Summary Reports**: Human-readable test summaries
- **Benchmark Data**: CSV format for analysis

### Sample Output
```
RTX 2000 Ada Generation Hardware Test Summary
==============================================
Date: 2024-01-15 14:30:00
GPU: NVIDIA RTX 2000 Ada Generation
Memory: 8192 MB
Compute Capability: 8.9

Results:
✓ Hardware validation: All tests passed
✓ NVRTC compilation: 15/15 kernels compiled successfully
✓ Performance benchmarks: All metrics within expected ranges
✓ Integration tests: End-to-end workflows validated
⚠ Stress tests: Minor thermal throttling detected under extreme load
```

## Configuration

### Test Categories
Tests are organized using xUnit traits:
- `[Trait("Category", "RTX2000")]`: All RTX 2000 specific tests
- `[Trait("Category", "HardwareValidation")]`: Hardware verification
- `[Trait("Category", "NVRTC")]`: Runtime compilation tests
- `[Trait("Category", "Performance")]`: Benchmarking tests
- `[Trait("Category", "Integration")]`: End-to-end tests
- `[Trait("Category", "StressTest")]`: Stability tests
- `[Trait("Category", "RequiresGPU")]`: Hardware-dependent tests

### Skippable Tests
Tests automatically skip when hardware is unavailable:
```csharp
[SkippableFact]
public void TestName()
{
    Skip.IfNot(_cudaAvailable, "CUDA not available");
    // Test implementation
}
```

## Troubleshooting

### Common Issues

1. **CUDA Not Found**
   ```
   Solution: Install CUDA Toolkit and verify with `nvcc --version`
   ```

2. **GPU Not Detected**
   ```
   Solution: Check drivers with `nvidia-smi` and verify GPU compatibility
   ```

3. **Tests Timeout**
   ```
   Solution: Increase test timeout or run tests individually
   dotnet test -- RunConfiguration.TestSessionTimeout=600000
   ```

4. **Memory Allocation Failures**
   ```
   Solution: Close GPU applications, verify available memory with `nvidia-smi`
   ```

5. **Thermal Throttling**
   ```
   Solution: Improve cooling, reduce ambient temperature, lower test intensity
   ```

### Debug Mode
Enable detailed logging:
```bash
export DOTNET_LOG_LEVEL=Debug
dotnet test --verbosity diagnostic
```

## Contributing

When adding new tests:
1. Follow the established test patterns and naming conventions
2. Use appropriate test categories and traits
3. Include hardware requirement checks
4. Provide detailed test output for diagnostics
5. Update this README with new test descriptions

### Test Structure
```csharp
[Trait("Category", "RTX2000")]
[Trait("Category", "NewCategory")]
public class NewTestClass : IDisposable
{
    private readonly ITestOutputHelper _output;
    
    [SkippableFact]
    public async Task TestName_ShouldValidateExpectedBehavior()
    {
        Skip.IfNot(_prerequisitesMet, "Prerequisites not met");
        
        // Test implementation with detailed logging
        _output.WriteLine("Test progress information");
        
        // Assertions with descriptive messages
        result.Should().BeGreaterThan(expected, "Detailed failure message");
    }
}
```

## License

This test suite is part of the DotCompute project and follows the project's licensing terms.