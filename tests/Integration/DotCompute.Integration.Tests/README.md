# DotCompute Integration Tests

Comprehensive integration tests for DotCompute's core functionality, ensuring reliable end-to-end operation across different backends and scenarios.

## Test Categories

### 1. Core Orchestrator Integration Tests (`CoreOrchestratorIntegrationTests.cs`)
- End-to-end kernel execution pipeline
- Backend selection and fallback mechanisms
- Cross-backend validation
- Performance optimization testing
- Concurrent execution safety

### 2. Memory Management Integration Tests (`MemoryManagementIntegrationTests.cs`)
- Unified buffer allocation and transfer
- Memory pooling efficiency
- Cross-device memory operations
- Memory leak detection
- Thread-safe memory access

### 3. Kernel Compilation Integration Tests (`KernelCompilationIntegrationTests.cs`)
- Source generation with [Kernel] attribute
- CUDA kernel compilation
- CPU fallback compilation
- Compilation caching and reuse
- Different data type support

### 4. Multi-Backend Integration Tests (`MultiBackendIntegrationTests.cs`)
- CPU to GPU migration
- Automatic backend selection
- Heterogeneous computing scenarios
- Load balancing across backends
- Backend capability detection

### 5. Real-World Scenarios Tests (`RealWorldScenariosTests.cs`)
- Image processing pipelines (Gaussian blur, convolution)
- Scientific computation (FFT, N-body simulation)
- Financial calculations (Monte Carlo, risk analysis)
- Machine learning operations (matrix multiplication, CNN)

### 6. CI/CD Compatibility Tests (`CiCdCompatibilityTests.cs`)
- Headless execution compatibility
- Resource constraint handling
- Timeout management
- Deterministic behavior
- Error recovery mechanisms

## Configuration

Tests are configured via `testsettings.json`:

```json
{
  "TestSettings": {
    "TimeoutSeconds": 300,
    "MaxRetries": 3,
    "EnableCudaTests": true,
    "EnablePerformanceTests": true,
    "PerformanceTolerancePercent": 20,
    "MemoryLeakThresholdMB": 100,
    "ConcurrencyTestThreads": 10,
    "LargeDatasetSizeMB": 100
  }
}
```

## Running Tests

### All Integration Tests
```bash
dotnet test tests/Integration/DotCompute.Integration.Tests/
```

### Specific Test Category
```bash
dotnet test tests/Integration/DotCompute.Integration.Tests/ --filter "FullyQualifiedName~CoreOrchestrator"
```

### GPU Tests Only (requires CUDA)
```bash
dotnet test tests/Integration/DotCompute.Integration.Tests/ --filter "Category=Hardware"
```

### Performance Tests
```bash
dotnet test tests/Integration/DotCompute.Integration.Tests/ --filter "TestCategory=Performance"
```

## Test Infrastructure

### Base Classes
- `IntegrationTestBase` - Common test infrastructure with DI, logging, performance measurement
- `TestDataGenerator` - Generates various types of test data
- `MockAcceleratorProvider` - Provides mock accelerators for testing

### Utilities
- Performance measurement and assertions
- Memory leak detection
- Concurrent execution helpers
- CUDA availability detection

## CI/CD Integration

Tests are designed to run reliably in CI/CD environments:

- **Headless Operation**: No GUI dependencies
- **Resource Constraints**: Graceful handling of limited memory/GPU
- **Timeout Management**: Configurable timeouts for long operations
- **Deterministic Behavior**: Reproducible results across runs
- **Error Recovery**: Resilient to transient failures

### Environment Variables

Tests respect these environment variables:

- `CI=true` - Enables CI-specific behavior
- `DOTCOMPUTE_BACKEND` - Forces specific backend usage
- `DOTCOMPUTE_LOG_LEVEL` - Controls logging verbosity
- `DOTCOMPUTE_CACHE_DISABLED` - Disables compilation caching
- `CUDA_VISIBLE_DEVICES` - Controls CUDA device visibility

## Performance Expectations

Tests include performance assertions to ensure DotCompute meets baseline requirements:

- **Vector Operations**: >1000 elements/ms
- **Matrix Operations**: >0.1 GFLOPS
- **Memory Allocation**: <10s for large buffers
- **Kernel Compilation**: <60s for complex kernels
- **FFT**: >100 samples/ms
- **Monte Carlo**: >50 simulations/ms

## Debugging Test Failures

### Memory Issues
1. Check `MemoryLeakThresholdMB` setting
2. Enable detailed memory logging
3. Use memory profilers (dotMemory, PerfView)

### Performance Issues
1. Check system resources during test execution
2. Adjust `PerformanceTolerancePercent` if needed
3. Profile specific operations

### CUDA Issues
1. Verify CUDA installation: `/usr/local/cuda/bin/nvcc --version`
2. Check GPU availability: `nvidia-smi`
3. Review CUDA error logs in test output

### Compilation Issues
1. Check kernel source code generation
2. Verify compiler toolchain availability
3. Review compilation error messages

## Contributing

When adding new integration tests:

1. Inherit from `IntegrationTestBase`
2. Use dependency injection for services
3. Include performance measurements
4. Add proper error handling and cleanup
5. Document test scenarios and expected behavior
6. Consider CI/CD compatibility

## Test Data

Test data is located in `TestData/` directory and includes:
- Sample kernels and datasets
- Configuration files
- Reference data for validation

Large datasets are generated at runtime to avoid repository bloat.