# Metal Backend Integration Tests

Comprehensive integration test suite for end-to-end Metal backend workflows.

## Overview

This test suite provides 23 comprehensive integration tests covering all major aspects of the Metal backend implementation. Tests are organized into five categories that validate complete workflows, multi-kernel pipelines, real-world scenarios, cross-component integration, and performance characteristics.

## Test Categories

### 1. Complete Workflows (5 tests)
Tests that validate full end-to-end pipelines from allocation through execution to result verification.

- **EndToEnd_VectorAddition_Complete**: Full pipeline with vector addition (1M elements)
- **EndToEnd_MatrixMultiply_Complete**: Matrix multiplication workflow (256x256 matrices)
- **EndToEnd_ImageConvolution_Complete**: Image processing with box blur filter (512x512)
- **EndToEnd_ReductionSum_Complete**: Parallel reduction with atomic operations (1M elements)
- **EndToEnd_CustomKernel_Compile_Execute**: User-provided MSL kernel compilation and execution

### 2. Multi-Kernel Pipelines (5 tests)
Tests that exercise complex kernel execution patterns and data flow.

- **Pipeline_ThreeKernels_Sequential**: A → B → C kernel chain with data dependency
- **Pipeline_ParallelBranches_Diamond**: Diamond pattern A → (B, C) → D with parallel execution
- **Pipeline_ComplexGraph_10Kernels**: Complex dependency graph with 10 sequential kernels
- **Pipeline_DynamicGraph_BuildAtRuntime**: Runtime-constructed pipeline based on data size
- **Pipeline_MemoryReuse_Optimized**: 5-stage pipeline using only 3 buffers (ping-pong pattern)

### 3. Real-World Scenarios (5 tests)
Tests simulating actual production use cases.

- **Scenario_MLInference_ConvNet**: Simplified CNN inference (28x28 → conv → pooling)
- **Scenario_PhysicsSimulation_Particles**: Particle physics with 10,000 particles over 100 frames
- **Scenario_ImageProcessing_Filters**: Multi-filter image pipeline (brightness + contrast)
- **Scenario_ScientificComputing_FFT**: Discrete Fourier Transform (256 points)
- **Scenario_DataProcessing_MapReduce**: Map-reduce pattern (1M elements)

### 4. Cross-Component Integration (5 tests)
Tests validating seamless interaction between system components.

- **Integration_Compiler_Executor_Memory**: Compiler ↔ executor ↔ memory interaction
- **Integration_Cache_Performance_Telemetry**: Kernel caching and telemetry collection
- **Integration_Graph_Optimizer_Executor**: Optimization pipeline with maximum settings
- **Integration_ErrorHandling_Recovery**: Error recovery and graceful degradation
- **Integration_MultiGPU_Coordination**: Multi-device coordination (concurrent execution)

### 5. Performance Integration (3 tests)
Tests validating performance targets and characteristics.

- **Performance_EndToEnd_MeetsTargets**: Validates >5 GB/s memory bandwidth (10M elements)
- **Performance_Sustained_NoDegrade**: 100 iterations with <10% degradation tolerance
- **Performance_Scaling_MultipleKernels**: Tests scaling from 1 to 10 concurrent kernels

## Running the Tests

### Prerequisites

- macOS with Metal support (macOS 10.13+)
- .NET 9.0 SDK or later
- Metal-capable GPU (integrated or discrete)

### Run All Tests

```bash
dotnet test tests/Integration/DotCompute.Backends.Metal.IntegrationTests/DotCompute.Backends.Metal.IntegrationTests.csproj
```

### Run Specific Category

```bash
# Complete workflows only
dotnet test --filter "FullyQualifiedName~EndToEnd"

# Multi-kernel pipelines only
dotnet test --filter "FullyQualifiedName~Pipeline"

# Real-world scenarios only
dotnet test --filter "FullyQualifiedName~Scenario"

# Cross-component integration only
dotnet test --filter "FullyQualifiedName~Integration"

# Performance tests only
dotnet test --filter "FullyQualifiedName~Performance"
```

### Run Single Test

```bash
dotnet test --filter "FullyQualifiedName~EndToEnd_VectorAddition_Complete"
```

## Test Characteristics

### Data Sizes

- Small: 5,000 - 10,000 elements
- Medium: 100,000 - 1,000,000 elements
- Large: 10,000,000+ elements

### Performance Targets

- Memory Bandwidth: >5 GB/s on modern Apple Silicon
- Sustained Performance: <10% degradation over 100 iterations
- Kernel Compilation: First compile <100ms, cached <10ms
- Multi-kernel Scaling: <15x overhead for 10x kernels

### Validation Approach

All tests use:
- Floating-point comparison with tolerance (default 0.001f)
- Sample-based verification for large datasets (first 100-1000 elements)
- Range validation for physics simulations
- Mathematical correctness verification against CPU reference implementations

## Test Architecture

### Test Base Class

All tests inherit from `TestBase` and use:
- `ITestOutputHelper` for XUnit output
- `MetalBackendFactory` for accelerator creation
- Skippable tests that automatically skip on non-Metal systems

### Memory Management

- All buffers use `await using` for proper disposal
- Tests verify no memory leaks through buffer lifecycle tracking
- Large allocations test memory pooling effectiveness

### Error Handling

- Tests use `Skip.IfNot()` to gracefully skip on unsupported systems
- Invalid operations are expected to throw and be caught
- Recovery tests validate system resilience

## Test Data

### Generators

- `CreateRandomMatrix()`: Random matrices with seed for reproducibility
- `CreateRandomImage()`: Random image data in [0, 1] range
- `MultiplyMatricesCPU()`: CPU reference implementation for validation

### Patterns

- Linear sequences: 0, 1, 2, ..., N
- Random data: Seeded for reproducibility
- Sinusoidal: For FFT validation
- Constant: For boundary testing

## Integration with CI/CD

These tests are designed to:
- Run on macOS CI runners with Metal support
- Skip gracefully on non-Metal systems
- Provide detailed output for debugging failures
- Complete within reasonable time (<5 minutes total)

## Troubleshooting

### Tests Skip on macOS

Ensure Metal is available:
```bash
system_profiler SPDisplaysDataType | grep Metal
```

### Performance Tests Fail

- Check for thermal throttling
- Verify no background GPU workload
- Run tests individually to isolate issues

### Memory Tests Fail

- Check available system memory
- Verify no memory pressure warnings
- Review Metal memory budget

## Contributing

When adding new tests:
1. Follow the existing naming convention: `Category_Description_Expected`
2. Add to appropriate test category or create new category
3. Include detailed Output.WriteLine() for debugging
4. Use proper assertions with meaningful messages
5. Verify test cleanup (all resources disposed)

## Performance Baselines

Measured on Apple M1 Pro (2021):
- Vector Addition (1M): ~2ms
- Matrix Multiply (256x256): ~5ms
- Image Convolution (512x512): ~15ms
- FFT (256 points): ~8ms
- Memory Bandwidth: 35-45 GB/s

Results may vary based on hardware and system configuration.
