# CUDA Backend Test Suite

This comprehensive test suite validates the DotCompute CUDA backend implementation across multiple dimensions including unit tests, integration tests, hardware validation, and mock testing for CI/CD environments.

## Test Structure

```
DotCompute.Hardware.Cuda.Tests/
├── Unit/                           # Unit tests for individual components
│   ├── CudaDeviceTests.cs         # Device detection and properties
│   ├── CudaMemoryManagerTests.cs  # Memory allocation/deallocation
│   ├── CudaKernelCompilerTests.cs # Kernel compilation
│   └── CudaAcceleratorTests.cs    # Accelerator lifecycle
├── Integration/                    # Integration and end-to-end tests
│   ├── CudaEndToEndTests.cs       # Full pipeline tests
│   ├── CudaPerformanceTests.cs    # Performance benchmarks
│   └── CudaMemoryTransferTests.cs # Host-device transfers
├── Hardware/                       # Hardware-specific validation
│   └── RTX2000ValidationTests.cs  # RTX 2000 series validation
├── Mock/                          # CI/CD mock tests
│   └── CudaMockDeviceTests.cs     # Simulated device tests
├── Utilities/                     # Test utilities and helpers
│   └── CudaTestUtilities.cs       # Common test utilities
└── README.md                      # This file
```

## Test Categories

### Unit Tests (90+ tests)
- **Device Detection**: Validates CUDA device enumeration and properties
- **Memory Management**: Tests allocation, deallocation, and memory statistics
- **Kernel Compilation**: Validates NVRTC compilation pipeline with various options
- **Accelerator Lifecycle**: Tests accelerator initialization, reset, and disposal

### Integration Tests (50+ tests)
- **End-to-End Workflows**: Complete pipeline from kernel compilation to execution
- **Memory Transfers**: Host-device data transfer validation with integrity checks
- **Performance Benchmarks**: Memory bandwidth, compilation time, and execution metrics
- **Error Recovery**: Device error handling and recovery scenarios

### Hardware Validation (25+ tests)
- **RTX 2000 Series**: Turing architecture-specific validation
- **Tensor Core Support**: Validates first-generation Tensor Core features
- **Compute Capability**: Architecture-specific feature validation
- **Memory Specifications**: Hardware memory configuration validation

### Mock Tests (30+ tests)
- **CI/CD Compatibility**: Tests that run without actual hardware
- **Device Simulation**: Mock GPU behavior for automated testing
- **Error Simulation**: Simulated device error conditions
- **Performance Modeling**: Mock performance characteristics

## Test Attributes and Categories

### Hardware Requirements
- `[Trait("Hardware", "CUDA")]` - Requires CUDA-capable GPU
- `[Trait("Hardware", "RTX2000")]` - Requires RTX 2000 series GPU
- `[Trait("Category", "Mock")]` - Runs without hardware (CI/CD safe)

### Performance Categories
- `[Trait("Category", "Performance")]` - Performance-focused tests
- `[Trait("Category", "Stress")]` - Stress and load tests
- `[Trait("Category", "Benchmark")]` - Detailed benchmarking

### Test Isolation
- `[Trait("Category", "Unit")]` - Isolated unit tests
- `[Trait("Category", "Integration")]` - Integration tests
- `[Trait("Category", "EdgeCase")]` - Edge case and error condition tests

## Running Tests

### Prerequisites
- NVIDIA GPU with CUDA Compute Capability 3.5+
- CUDA Toolkit 11.0+ installed
- NVRTC libraries available
- .NET 9.0+ Runtime

### Test Execution Commands

```bash
# Run all tests (requires CUDA hardware)
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only mock tests (CI/CD safe)
dotnet test --filter "Category=Mock"

# Run RTX 2000 specific tests
dotnet test --filter "Hardware=RTX2000"

# Run performance benchmarks
dotnet test --filter "Category=Performance"

# Run with detailed logging
dotnet test --logger "console;verbosity=detailed"
```

### CI/CD Configuration

For continuous integration without CUDA hardware:
```bash
# Run only mock tests in CI
dotnet test --filter "Category=Mock" --logger trx
```

## Test Data and Utilities

### CudaTestUtilities
Provides common functionality for all CUDA tests:
- Hardware detection and validation
- Test environment setup
- Device capability checking
- Logger creation and configuration
- Test data generation

### Test Data Generators
- **Kernels**: Pre-defined kernel source code for various scenarios
- **Arrays**: Generated test data for different numeric types and sizes
- **Mock Data**: Simulated GPU properties and behavior

## Expected Test Coverage

The test suite aims for **90%+ code coverage** across:

### Core Components (Target: 95%)
- CudaAccelerator class
- CudaMemoryManager class
- CudaKernelCompiler class
- CudaContext class

### Memory Management (Target: 90%)
- Buffer allocation and deallocation
- Host-device transfers
- Memory statistics and monitoring
- Error handling and recovery

### Kernel Compilation (Target: 85%)
- NVRTC compilation pipeline
- Optimization levels and flags
- Compilation caching
- Error handling and validation

## Performance Validation

### Memory Bandwidth Targets
- Host-to-Device: >10 GB/s (varies by hardware)
- Device-to-Host: >10 GB/s (varies by hardware)
- Device-to-Device: >100 GB/s (on-chip transfers)

### Compilation Performance
- Simple kernels: <2 seconds
- Complex kernels: <10 seconds
- Cache hits: <100ms

### Memory Management
- Small allocations (<1MB): <1ms
- Large allocations (>100MB): <100ms
- Statistics queries: <1ms

## Hardware Compatibility Matrix

| GPU Series | Compute Capability | Test Support | Notes |
|-------------|-------------------|---------------|-------|
| GTX 1000    | 6.1               | Basic         | Pascal architecture |
| RTX 2000    | 7.5               | Full          | Turing + Tensor Cores |
| RTX 3000    | 8.6               | Full          | Ampere architecture |
| RTX 4000    | 8.9               | Full          | Ada Lovelace |
| Tesla V100  | 7.0               | Full          | Data center validation |

## Troubleshooting

### Common Issues

1. **CUDA Not Found**
   - Ensure CUDA toolkit is installed
   - Check PATH and environment variables
   - Verify GPU driver version

2. **NVRTC Compilation Failures**
   - Update CUDA toolkit to 11.0+
   - Check NVRTC library availability
   - Verify compute capability support

3. **Memory Allocation Failures**
   - Check available GPU memory
   - Reduce test data sizes
   - Close other GPU applications

4. **Performance Test Failures**
   - Ensure GPU is not throttling
   - Check system thermal conditions
   - Verify no background GPU usage

### Debug Configuration

```xml
<RunSettings>
  <TestRunParameters>
    <Parameter name="CudaDebugMode" value="true" />
    <Parameter name="LogLevel" value="Debug" />
    <Parameter name="MemoryValidation" value="true" />
  </TestRunParameters>
</RunSettings>
```

## Contributing

When adding new tests:

1. Follow the established naming conventions
2. Use appropriate test categories and traits
3. Include both positive and negative test cases
4. Add mock variants for CI/CD compatibility
5. Document hardware requirements clearly
6. Update this README with new test descriptions

## Test Results and Reporting

Test results include:
- Execution times and performance metrics
- Memory usage statistics
- Hardware capability validation
- Coverage reports with detailed breakdowns
- Performance regression detection

The comprehensive test suite ensures robust validation of the CUDA backend across development, testing, and production environments.