# DotCompute OpenCL Hardware Test Suite

## Overview

This is a comprehensive, production-grade test suite for the DotCompute OpenCL backend, designed to achieve 90%+ code coverage and ensure reliability across diverse OpenCL hardware.

## Test Structure

```
DotCompute.Hardware.OpenCL.Tests/
├── Execution/                    # Kernel execution tests
│   └── OpenCLKernelExecutionTests.cs
├── Memory/                       # Memory management tests
│   └── OpenCLMemoryTests.cs
├── Compilation/                  # Kernel compilation tests
│   └── OpenCLCompilationTests.cs
├── ErrorHandling/                # Error handling and recovery tests
│   └── OpenCLErrorHandlingTests.cs
├── Performance/                  # Performance benchmarks and stress tests
│   ├── OpenCLPerformanceBenchmarks.cs
│   └── OpenCLStressTests.cs
├── Helpers/                      # Test utilities
│   ├── OpenCLTestBase.cs
│   └── OpenCLDetection.cs
└── OpenCLCrossBackendValidationTests.cs  # Cross-backend validation
```

## Test Categories

### 1. Kernel Execution Tests (6 tests)
- **Vector Addition**: Basic kernel execution with 1M elements
- **Matrix Multiplication**: 2D kernel execution with 128x128 matrices
- **Local Memory Reduction**: Shared memory usage and work group synchronization
- **Complex Compute**: Compute-intensive operations with trigonometric functions
- **Performance Benchmark**: Iterative performance measurement (10 iterations)
- **Work Group Optimization**: Testing different work group sizes

**Coverage Areas:**
- Kernel compilation and launching
- 1D and 2D work item distribution
- Local/shared memory allocation
- Work group synchronization
- Performance profiling

### 2. Memory Management Tests (10 tests)
- **Basic Allocation**: Various allocation sizes (1KB to 64MB)
- **Large Allocation**: Testing device memory limits
- **Host-to-Device Transfer**: Bandwidth measurement for H2D transfers
- **Device-to-Host Transfer**: Bandwidth measurement for D2H transfers
- **Bidirectional Transfer**: Concurrent H2D and D2H operations
- **Memory Bandwidth Kernel**: Kernel-based bandwidth testing
- **Memory Statistics**: Allocation tracking and reporting
- **Buffer Copy**: Device-to-device copy operations
- **Multiple Allocations**: Stress testing with 10+ allocations
- **Memory Pressure**: Large-scale allocation testing

**Coverage Areas:**
- Memory allocation and deallocation
- Data transfer operations
- Memory bandwidth measurement
- Memory pooling and statistics
- Buffer management

### 3. Compilation Tests (10 tests)
- **Simple Kernel Compilation**: Basic compilation with timing
- **Complex Kernel Compilation**: Heavy compute kernel with optimization
- **Preprocessor Defines**: Custom defines in kernel compilation
- **Compilation Caching**: Verifying kernel cache effectiveness
- **Multiple Kernel Compilation**: Compiling several kernels
- **Optimization Levels**: None, Default, Aggressive optimization
- **Debug Information**: Compilation with debug info enabled
- **Invalid Kernel Handling**: Error detection for invalid source
- **Execution After Compilation**: Functional verification
- **Parallel Compilation**: Concurrent kernel compilation

**Coverage Areas:**
- Kernel compilation pipeline
- Compilation options and defines
- Optimization levels
- Kernel caching mechanism
- Error handling during compilation

### 4. Error Handling Tests (11 tests)
- **Zero Element Allocation**: Invalid allocation size handling
- **Negative Element Allocation**: Invalid parameter detection
- **Null Kernel Compilation**: Null reference handling
- **Empty Source Compilation**: Invalid source handling
- **Invalid Work Group Size**: Device limit validation
- **Error Recovery**: System stability after errors
- **Disposed Accelerator Usage**: Resource lifecycle management
- **Disposed Buffer Usage**: Buffer lifecycle management
- **Out of Bounds Write**: Buffer boundary validation
- **Out of Bounds Read**: Read operation validation
- **Concurrent Error Handling**: Thread-safe error handling

**Coverage Areas:**
- Input validation
- Error propagation
- Resource lifecycle management
- Error recovery mechanisms
- Concurrent error scenarios

### 5. Performance Benchmarks (5 tests)
- **Small Kernel Latency**: Measuring launch latency (100 iterations)
- **Large Kernel Throughput**: 16M element throughput test
- **Memory Transfer Bandwidth**: Comprehensive bandwidth testing
- **Scalability**: Performance scaling from 1K to 1M elements
- **Concurrent Execution**: Multi-kernel concurrent performance

**Coverage Areas:**
- Kernel launch latency
- Memory throughput
- Computational throughput
- Scalability characteristics
- Concurrent execution performance

### 6. Stress Tests (6 tests)
- **Many Kernel Launches**: 1000 consecutive kernel launches
- **Many Allocations**: 100 allocation/deallocation cycles
- **Large Data Transfers**: 256MB transfers over 20 iterations
- **Concurrent Operations**: 4 threads with 100 operations each
- **Memory Pressure**: Allocating 50% of device memory
- **Long Running Computation**: 500 iterations of compute-intensive work

**Coverage Areas:**
- System stability under load
- Memory leak detection
- Resource exhaustion handling
- Concurrent operation safety
- Extended runtime reliability

### 7. Cross-Backend Validation Tests (5 tests)
- **Vector Addition Validation**: Comparing with CPU implementation (10K elements)
- **Vector Multiplication Validation**: Element-wise multiplication comparison
- **Dot Product Validation**: Reduction operation accuracy
- **Determinism Testing**: Verifying consistent results across runs
- **Edge Case Validation**: Testing special float values (Zero, MaxValue, MinValue, Epsilon)

**Coverage Areas:**
- Computational correctness
- Numerical accuracy
- Result determinism
- Edge case handling
- Cross-backend consistency

## Test Execution

### Running All Tests
```bash
dotnet test tests/Hardware/DotCompute.Hardware.OpenCL.Tests/DotCompute.Hardware.OpenCL.Tests.csproj
```

### Running Specific Categories
```bash
# Execution tests only
dotnet test --filter "Category=RequiresOpenCL&FullyQualifiedName~Execution"

# Performance tests only
dotnet test --filter "Category=Performance"

# Stress tests only
dotnet test --filter "Category=Stress"

# Cross-backend validation
dotnet test --filter "Category=CrossBackend"
```

### Prerequisites
- OpenCL 1.2+ compatible device (CPU, GPU, or accelerator)
- OpenCL runtime installed
- Appropriate device drivers

## Expected Coverage

### Code Coverage Targets
- **Overall Target**: 90%+ for OpenCL backend
- **Critical Paths**: 95%+ (kernel execution, memory management)
- **Error Handling**: 85%+ (edge cases and error paths)
- **Compilation**: 90%+ (all compilation options)

### Coverage by Module
- **OpenCLAccelerator**: 95%+
- **OpenCLMemoryManager**: 90%+
- **OpenCLKernelCompiler**: 90%+
- **OpenCLContext**: 85%+
- **OpenCLDeviceManager**: 80%+

## Test Metrics

### Total Test Count: 53 Tests
- Execution: 6 tests
- Memory: 10 tests
- Compilation: 10 tests
- Error Handling: 11 tests
- Performance: 5 tests
- Stress: 6 tests
- Cross-Backend: 5 tests

### Estimated Execution Time
- **Quick Tests** (Execution, Memory, Compilation): ~2-3 minutes
- **Performance Benchmarks**: ~3-5 minutes
- **Stress Tests**: ~5-10 minutes
- **Full Suite**: ~10-15 minutes (hardware dependent)

## Test Quality Features

### Production-Grade Patterns
1. **Hardware Detection**: Automatic OpenCL device detection with Skip.IfNot
2. **Resource Management**: Proper async disposal of all resources
3. **Isolation**: Each test is independent with clean setup/teardown
4. **Determinism**: Fixed random seeds for reproducible results
5. **Comprehensive Assertions**: FluentAssertions for readable test failures
6. **Performance Metrics**: Detailed timing and throughput measurements
7. **Error Tolerance**: Appropriate floating-point comparison tolerances

### Best Practices
- ✅ All tests use `[SkippableFact]` for hardware availability
- ✅ Proper async/await patterns throughout
- ✅ Resource cleanup with `await using` and `try/finally`
- ✅ Clear test names following "Should_When" pattern
- ✅ Comprehensive output logging via `ITestOutputHelper`
- ✅ Multiple assertion points per test for thorough validation
- ✅ Edge case coverage (zero, max, min, epsilon values)

## Integration with CI/CD

### GitHub Actions Configuration
```yaml
- name: OpenCL Hardware Tests
  run: |
    # Check for OpenCL availability
    if command -v clinfo &> /dev/null; then
      dotnet test tests/Hardware/DotCompute.Hardware.OpenCL.Tests \
        --filter "Category=RequiresOpenCL" \
        --logger "trx;LogFileName=opencl-tests.trx"
    else
      echo "OpenCL not available, skipping hardware tests"
    fi
```

## Performance Baselines

### Expected Performance Ranges (GPU)
- **Kernel Launch Latency**: < 100 μs
- **Memory Bandwidth**: > 50 GB/s (device dependent)
- **Compute Throughput**: > 100 GFLOPS (device dependent)

### Expected Performance Ranges (CPU)
- **Kernel Launch Latency**: < 500 μs
- **Memory Bandwidth**: > 10 GB/s
- **Compute Throughput**: > 10 GFLOPS

## Known Limitations

1. **Hardware Dependent**: Test performance varies by device
2. **Driver Dependent**: Some tests may fail with outdated drivers
3. **Concurrent Execution**: May be limited on some platforms
4. **Memory Limits**: Large allocation tests bounded by device memory

## Contributing

When adding new tests:
1. Follow existing test patterns and structure
2. Use `OpenCLTestBase` for common utilities
3. Add appropriate `[Trait]` attributes
4. Include comprehensive assertions and output logging
5. Test on multiple devices if possible
6. Update this README with new test categories

## License

Copyright (c) 2025 Michael Ivertowski
Licensed under the MIT License. See LICENSE file in the project root for license information.
