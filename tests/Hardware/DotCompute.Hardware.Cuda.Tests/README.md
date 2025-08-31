# CUDA Hardware Tests

This directory contains comprehensive hardware tests for the CUDA backend of DotCompute. These tests are designed to run on actual CUDA hardware and verify real-world performance and functionality.

## Test Classes

### CudaAcceleratorTests.cs
- Device initialization with RTX 2000 series GPUs
- Compute capability 8.9 verification  
- Memory allocation and transfers
- Stream management
- Error handling
- Hardware property validation

### CudaKernelExecutionTests.cs
- Simple kernel execution (vector add, matrix multiply)
- Grid/block configuration optimization
- Shared memory usage
- Dynamic parallelism (if supported)
- Performance measurements

### CudaMemoryTests.cs
- Device memory allocation and management
- Host-to-device transfers
- Device-to-host transfers  
- Unified memory operations
- Memory bandwidth tests
- Pinned memory performance

### CudaGraphTests.cs
- CUDA graph creation and execution
- Graph capture functionality
- Multi-kernel graphs with dependencies
- Performance comparisons vs individual launches
- Graph updates (if supported)

### CudaPerformanceBenchmarkTests.cs
- Memory bandwidth benchmarking
- Compute performance testing
- Matrix multiply optimization
- Stream concurrency analysis
- Real-world performance validation

### CudaTestBase.cs
- Common base class with hardware detection
- Performance measurement utilities
- Test data generators
- Memory tracking helpers
- Hardware capability logging

## Requirements

- **CUDA Runtime**: CUDA 11.0 or later
- **Hardware**: NVIDIA GPU with compute capability 3.0+
- **RTX 2000 Tests**: RTX 2000 Ada generation GPU (compute capability 8.9)
- **Memory**: Sufficient GPU memory for test allocations
- **Driver**: Compatible NVIDIA driver

## Running Tests

### Prerequisites Check
```bash
# Verify CUDA installation
nvidia-smi

# Check compute capability
nvidia-ml-py3 or similar tools
```

### Execution
```bash
# Run all CUDA tests
dotnet test --filter "Category=RequiresCUDA"

# Run specific test class
dotnet test --filter "ClassName~CudaAcceleratorTests"

# Run performance benchmarks only
dotnet test --filter "Category=Performance"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed" --filter "Category=RequiresCUDA"
```

### Test Categories

- **RequiresCUDA**: All tests that need CUDA hardware
- **Performance**: Performance benchmark tests
- **RTX2000**: Tests specific to RTX 2000 Ada GPUs
- **Graph**: CUDA graph functionality tests
- **Memory**: Memory-related tests
- **Kernel**: Kernel execution tests

## Test Behavior

### Automatic Skipping
Tests use `[SkippableFact]` and automatically skip when:
- CUDA runtime is not available
- No compatible GPU is detected
- Insufficient memory for test requirements
- Required compute capability not met
- Driver issues prevent CUDA initialization

### Hardware Detection
Tests perform comprehensive hardware detection:
- CUDA runtime library presence
- Device count and accessibility
- Compute capability verification
- Memory availability checks
- Feature support validation

### Performance Validation
Performance tests include realistic expectations:
- Memory bandwidth > 50 GB/s for modern GPUs
- Compute throughput validation
- Transfer rate expectations based on PCIe generation
- Kernel execution time bounds

## Troubleshooting

### Common Issues

**"CUDA hardware not available"**
- Verify `nvidia-smi` works
- Check CUDA runtime installation
- Ensure compatible driver version

**"Compute capability insufficient"**
- Some tests require specific compute capabilities
- Check GPU specifications
- Update GPU drivers if needed

**Memory allocation failures**
- Reduce test data sizes for limited memory GPUs
- Close other GPU applications
- Check available GPU memory

**Driver compatibility issues**
- Update NVIDIA drivers
- Verify CUDA toolkit compatibility
- Check for conflicting installations

### Debugging
Enable verbose logging by setting environment variables:
```bash
export DOTCOMPUTE_LOG_LEVEL=Debug
export CUDA_LAUNCH_BLOCKING=1
```

## Performance Expectations

### RTX 2000 Ada (Compute Capability 8.9)
- Memory Bandwidth: 288 GB/s
- CUDA Cores: 2816
- Tensor Cores: 88 (4th gen)
- Memory: 12GB GDDR6

### General Modern GPU Expectations
- Memory bandwidth: 100-800 GB/s
- Matrix multiply: 500+ GFLOPS
- Memory transfers: 5-15 GB/s (PCIe dependent)
- Kernel launch overhead: < 10μs

## Contributing

When adding new tests:
1. Use `CudaTestBase` as base class
2. Apply appropriate `[Trait]` attributes
3. Use `[SkippableFact]` with hardware checks
4. Include performance validations
5. Add comprehensive logging
6. Document expected behavior
7. Test on multiple GPU generations

## Architecture Notes

Tests are designed to work with the DotCompute CUDA backend architecture:
- Factory pattern for accelerator creation
- Abstraction layer for cross-backend compatibility
- Advanced features: graphs, streams, unified memory
- Production-ready error handling
- Performance monitoring integration