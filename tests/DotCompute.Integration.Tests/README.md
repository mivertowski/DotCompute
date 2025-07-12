# DotCompute Integration Tests

This project contains comprehensive integration tests for the DotCompute system, designed to test end-to-end scenarios, system integration, and real-world use cases.

## Overview

The integration tests validate the complete system functionality including:

- **Memory Management Integration**: Tests for unified memory buffers, cross-backend transfers, and memory optimization
- **Multi-Backend Pipeline Testing**: CPU→CUDA workflows, performance comparisons, and memory transfer patterns
- **Plugin System Integration**: Plugin loading, hot-reload functionality, and multi-plugin coordination
- **Source Generator Integration**: Generated code execution, complex data types, and runtime validation
- **Real-World Scenarios**: Image processing pipelines, scientific computing (Monte Carlo), machine learning workflows

## Test Structure

### Test Fixtures
- **IntegrationTestFixture**: Core test infrastructure providing memory management, logging, and mock kernel execution
- **MockCompiledKernel**: Simplified kernel representation for testing without actual GPU compilation

### Test Scenarios

#### 1. Memory Integration Tests (`MemoryIntegrationTests`)
- ✅ **UnifiedBuffer Basic Operations**: Create, write, read operations
- ✅ **Multiple Buffer Types**: Float, int, and other data type handling
- ✅ **Large Allocations**: Performance with 1M+ element datasets
- ✅ **Memory Statistics**: Pool utilization and allocation tracking
- ✅ **Concurrent Operations**: Thread-safe buffer management
- ✅ **Memory Pressure**: Automatic cleanup under memory constraints
- ✅ **Compaction**: Memory defragmentation and optimization
- ✅ **Pool Management**: Type-specific memory pools
- ✅ **Benchmarking**: Performance measurement and reporting

#### 2. Simple Pipeline Tests (`SimplePipelineTests`)
- ✅ **Basic Data Processing**: Single-stage kernel execution simulation
- ✅ **Multi-Stage Pipelines**: Coordinated multi-kernel workflows
- ✅ **Error Handling**: Robust pipeline error recovery
- ✅ **Large Dataset Processing**: Memory-efficient handling of large data
- ✅ **Concurrent Pipelines**: Multiple parallel pipeline execution
- ✅ **Resource Cleanup**: Proper disposal and memory management

#### 3. Advanced Integration Tests (Planned)

**Multi-Backend Pipeline Tests**:
- CPU→CUDA data transfer validation
- Performance comparison across backends
- Memory transfer pattern optimization
- Cross-platform compatibility verification

**Plugin System Integration Tests**:
- Dynamic plugin loading and execution
- Hot-reload with running kernels
- Multi-plugin coordination and resource sharing
- Plugin isolation and safety

**Source Generator Integration Tests**:
- Generated code compilation and execution
- Complex data type marshaling
- Attribute-driven code generation validation
- Performance comparison: generated vs. manual code

**Real-World Scenario Tests**:
- **Image Processing Pipeline**: RGB→Grayscale→Edge Detection→Blur
- **Monte Carlo Simulation**: π estimation with parallel random sampling
- **Machine Learning Workflow**: Matrix multiplication, activation functions, softmax
- **Scientific Computing**: FFT, convolution, statistical analysis

## Key Features Tested

### Memory Management
- **Unified Buffer System**: Host/device memory coordination
- **Pool Efficiency**: Automatic buffer reuse and optimization
- **Memory Pressure Handling**: Graceful degradation under constraints
- **Cross-Backend Transfers**: Efficient CPU↔GPU memory operations
- **Statistics and Monitoring**: Real-time memory usage tracking

### Pipeline Coordination
- **Stage Synchronization**: Proper inter-stage data flow
- **Error Propagation**: Comprehensive error handling
- **Resource Management**: Automatic cleanup and optimization
- **Performance Monitoring**: Execution time and throughput tracking

### Plugin Architecture
- **Dynamic Loading**: Runtime plugin discovery and loading
- **Hot Reload**: Live plugin updates without system restart
- **Isolation**: Safe plugin execution with proper boundaries
- **Resource Sharing**: Efficient shared resource management

### Source Generation
- **Compile-Time Generation**: Automatic kernel code generation
- **Type Safety**: Strong typing across generated interfaces
- **Performance Optimization**: Generated code performance validation
- **Complex Types**: Support for structures, arrays, and custom types

## Performance Characteristics

### Benchmarked Scenarios
- **Memory Throughput**: Sequential read/write performance
- **Random Access Latency**: Memory access pattern efficiency
- **Pipeline Execution**: End-to-end processing performance
- **Cross-Backend Transfer**: Host↔Device transfer rates
- **Concurrent Operations**: Multi-threaded performance scaling

### Expected Performance Targets
- Memory operations: > 10 GB/s sequential throughput
- Random access: < 100ns average latency
- Pipeline coordination: < 5ms overhead per stage
- Cross-backend transfers: > 80% theoretical bandwidth utilization

## Configuration

### Test Environment Setup
```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <IsTestProject>true</IsTestProject>
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

### Package Dependencies
- **xUnit**: Testing framework
- **FluentAssertions**: Fluent assertion library
- **BenchmarkDotNet**: Performance benchmarking
- **Microsoft.Extensions.Hosting**: Dependency injection and logging

### Memory Configuration
- Default pool sizes: Adaptive based on available system memory
- Memory pressure thresholds: 80% for cleanup, 90% for aggressive cleanup
- Buffer alignment: Platform-optimized (64-byte on x64)

## Running Tests

### Full Test Suite
```bash
dotnet test
```

### Specific Test Categories
```bash
# Memory tests only
dotnet test --filter "FullyQualifiedName~MemoryIntegrationTests"

# Pipeline tests only
dotnet test --filter "FullyQualifiedName~SimplePipelineTests"

# Performance benchmarks
dotnet test --filter "Category=Benchmark"
```

### Continuous Integration
Tests are designed to run in CI environments with:
- No GPU requirements (CPU-only by default)
- Configurable memory limits
- Parallel execution support
- Detailed performance reporting

## Test Data

### Data Generators (`TestDataGenerator`)
- **Random Data**: Seeded random generation for reproducible tests
- **Sequential Data**: Linear sequences for predictable validation
- **Sinusoidal Data**: Periodic patterns for signal processing tests
- **Matrix Data**: 2D array generation with various patterns
- **Complex Data**: Real/imaginary pairs for FFT testing
- **Image Data**: Synthetic image generation for processing tests

### Sample Kernels (`sample_kernels.cl`)
- Basic vector operations (add, multiply, scale)
- Matrix operations (multiply, transpose)
- Image processing (grayscale, blur, edge detection)
- Mathematical functions (polynomial evaluation, FFT)
- Reduction operations (sum, statistics)
- Neural network operations (ReLU, sigmoid, batch normalization)

## Architecture Integration

### System Components Tested
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Memory        │    │   Pipelines     │    │   Plugins       │
│   Management    │◄──►│   & Workflows   │◄──►│   & Extensions  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 ▼
                    ┌─────────────────┐
                    │   Core Engine   │
                    │   & Backends    │
                    └─────────────────┘
```

### Integration Points
1. **Memory ↔ Pipelines**: Buffer lifecycle management
2. **Pipelines ↔ Plugins**: Dynamic kernel loading
3. **Memory ↔ Plugins**: Resource allocation and sharing
4. **Core ↔ All**: Central coordination and scheduling

## Future Enhancements

### Planned Test Scenarios
- **GPU Validation**: CUDA/OpenCL backend testing when available
- **Network Integration**: Distributed computing scenarios
- **Security Testing**: Plugin isolation and safety validation
- **Stress Testing**: Long-running stability and memory leak detection

### Performance Optimization
- **SIMD Integration**: Vector instruction utilization
- **Cache Optimization**: Memory access pattern improvement
- **Parallel Scaling**: Multi-core performance validation
- **Backend Selection**: Automatic optimal backend selection

## Contributing

### Adding New Tests
1. Follow the established naming conventions
2. Use FluentAssertions for readable test assertions
3. Include performance benchmarks where appropriate
4. Document expected behavior and performance characteristics
5. Add corresponding test data generators if needed

### Performance Testing
- Use BenchmarkDotNet for accurate measurements
- Include both throughput and latency metrics
- Test with realistic data sizes
- Validate against expected performance baselines

### Documentation
- Update this README for new test categories
- Document any new configuration requirements
- Include performance expectations for new scenarios
- Provide clear failure troubleshooting steps