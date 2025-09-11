# DotCompute Metal Backend Hardware Tests

A comprehensive, production-ready test suite for the DotCompute Metal backend, providing extensive validation of Metal compute functionality on Apple Silicon and Intel Macs with discrete GPUs. This test suite follows CUDA testing patterns and provides real-world scenario validation.

## 🚀 Test Suite Overview

The Metal test suite has been significantly expanded to include **comprehensive hardware tests** covering:

### 📋 New Comprehensive Test Classes (Added)

| Test Class | Purpose | Test Count | Features |
|------------|---------|------------|----------|
| **MetalHardwareDetectionTests** | Hardware Discovery | 8 tests | Metal availability, Apple Silicon detection, device capabilities, shader compilation |
| **MetalPerformanceBenchmarkTests** | Performance Validation | 4 tests | Memory bandwidth (100+ GB/s), compute performance (10+ GFLOPS), matrix operations |
| **MetalMemoryStressTests** | Memory Management | 4 tests | Large buffers (up to 8GB), memory pressure, access patterns, lifecycle testing |
| **MetalConcurrencyTests** | Multi-threading Safety | 4 tests | Concurrent queues, thread safety, workload balancing, atomic operations |
| **MetalErrorHandlingTests** | Error Scenarios | 6 tests | Invalid shaders, parameter errors, resource cleanup, edge cases |

### 🔧 Enhanced Existing Test Categories

## ⚡ Key Features of the New Test Suite

### 🌟 Real-World Scenarios
- **Image Processing Pipelines**: Gaussian blur → Edge detection → Enhancement
- **Scientific Simulations**: 2D Heat equation with finite difference methods  
- **Matrix Operation Chains**: Complex GEMM sequences with dependency management
- **Signal Processing**: DFT computation and frequency analysis
- **Multi-buffer Dependencies**: Complex data flow patterns with feedback loops

### 🚀 Apple Silicon Optimizations
- **Unified Memory Testing**: Zero-copy access patterns and latency measurements
- **Expected Performance**: 150-250 GB/s bandwidth on M-series chips
- **Apple Silicon Detection**: Automatic detection and optimization paths
- **Native Metal API**: Direct Metal compute shader validation

### 🛡️ Production-Grade Validation
- **Error Handling**: Graceful handling of invalid shaders, memory limits, edge cases
- **Thread Safety**: Concurrent execution with 8+ threads, atomic operations
- **Memory Management**: Large buffer allocation (up to device limits), rapid lifecycle testing
- **Performance Baselines**: Comprehensive benchmarks with automatic regression detection

### MetalPerformanceTests (Original)
- **Memory bandwidth benchmarks** - Tests sustained memory throughput with different data sizes
- **Compute performance benchmarks** - Tests computational throughput (GFLOPS) for various operations
- **Data transfer benchmarks** - Tests Host-Device transfer performance
- **Matrix multiplication benchmarks** - Tests optimized matrix operations with shared memory
- **Concurrent kernel execution** - Tests parallel kernel execution capabilities

### MetalStressTests
- **Memory pool stress testing** - High-concurrency allocation/deallocation scenarios
- **Memory pressure testing** - Tests behavior under memory exhaustion
- **Long-running kernel stability** - Extended computation stability validation
- **Concurrent memory operations** - Multi-threaded memory access stress testing
- **Mixed workload stress testing** - Combined memory, compute, and transfer stress

### MetalIntegrationTests
- **Image processing pipeline** - End-to-end multi-kernel image processing workflow
- **Matrix operation chains** - Complex matrix computation dependency chains
- **Scientific simulation workflows** - Heat equation simulation with time-stepping
- **Multi-buffer dependency chains** - Complex data flow patterns with feedback loops

### MetalRegressionTests
- **Memory allocation performance** - Ensures allocation speed doesn't regress
- **Kernel compilation performance** - Validates compilation time stability
- **Data transfer bandwidth** - Prevents transfer performance degradation
- **Compute performance baselines** - Maintains minimum GFLOPS thresholds
- **API compatibility validation** - Ensures API behavior consistency

### MetalComparisonTests
- **Vector operations vs CUDA patterns** - Compares fundamental operations to CUDA benchmarks
- **Matrix scaling behavior** - Validates GPU-like scaling characteristics
- **Memory access patterns** - Tests coalesced vs strided access performance
- **Concurrent execution patterns** - Compares parallelism efficiency
- **Occupancy optimization** - Tests threadgroup size optimization patterns

## Hardware Support

### Supported Platforms
- **Apple Silicon** (M1, M2, M3) - Primary target with unified memory architecture
- **Intel Mac** with discrete/integrated GPUs - Legacy support
- **macOS 10.13+** - Minimum OS requirement for Metal compute shaders

### Performance Expectations

#### Apple Silicon (M1/M2/M3)
- Memory bandwidth: 400-650 GB/s
- Matrix multiply (1024x1024): 1500-4000 GFLOPS
- Memory allocation: <0.1ms average
- Unified memory advantages for zero-copy operations

#### Intel Mac Systems
- Memory bandwidth: 150-350 GB/s
- Matrix multiply (1024x1024): 300-1000 GFLOPS
- Memory allocation: 0.3-1.0ms average
- Discrete GPU memory hierarchy

## Running the Tests

### Prerequisites
```bash
# Ensure Metal backend is built
dotnet build src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj

# Verify Metal hardware availability
system_profiler SPDisplaysDataType
```

### Test Execution
```bash
# Run all Metal tests
dotnet test tests/Hardware/DotCompute.Hardware.Metal.Tests/

# Run specific test categories
dotnet test --filter "Category=Performance"
dotnet test --filter "Category=Stress"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Regression"

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run performance tests only
dotnet test --filter "Category=Performance&Category=RequiresMetal"
```

### Configuration

#### Test Filtering
Tests are tagged with traits for selective execution:
- `RequiresMetal` - Requires Metal hardware
- `Performance` - Performance benchmarking tests
- `Stress` - High-load stress tests
- `Integration` - End-to-end workflow tests
- `Regression` - Performance regression tests
- `LongRunning` - Extended duration tests

#### Test Scaling
Tests automatically scale based on detected hardware:
- Memory-intensive tests scale down on lower-memory systems
- Compute tests adjust expectations based on GPU type
- Timeout values scale with system performance

## Performance Baselines

### Benchmark Data Structure
Performance thresholds are maintained in `MetalBenchmarkData.cs`:
- Device-specific performance expectations
- Memory bandwidth thresholds by data size
- Compute performance thresholds (GFLOPS)
- Memory allocation time limits
- Kernel compilation time limits

### Regression Detection
- Automatic performance trend analysis
- Historical performance data tracking
- Configurable tolerance thresholds (typically 20-30%)
- Cross-build performance comparison

### Performance Monitoring
- Real-time performance metric collection
- Performance report generation
- Trend analysis and alerting
- Export/import of benchmark data

## Test Infrastructure

### MetalTestUtilities
- **Test data generators** - Random, linear, sinusoidal, Gaussian data
- **Kernel templates** - Common compute kernels (add, multiply, matrix ops)
- **Performance measurement** - Timing, bandwidth calculation, statistics
- **Memory tracking** - GC memory usage monitoring
- **Result verification** - Floating-point comparison with tolerance

### MetalTestBase
- **Hardware detection** - Metal availability, Apple Silicon detection
- **Logging infrastructure** - XUnit test output integration
- **Resource management** - Automatic cleanup and disposal
- **Factory setup** - Metal backend factory initialization

## Troubleshooting

### Common Issues

#### Metal Not Available
```
Skip.IfNot(IsMetalAvailable(), "Metal hardware not available");
```
- Verify running on macOS
- Check macOS version (10.13+ required)
- Ensure Metal framework is available
- Verify Metal device enumeration

#### Insufficient Memory
```
if (accelerator.Info.TotalMemory < requiredMemory) {
    Output.WriteLine("Skipping test due to insufficient memory");
    return;
}
```
- Tests automatically scale down on low-memory systems
- Large matrix tests may be skipped
- Memory pressure tests adapt to available memory

#### Performance Variations
- GPU thermal throttling under sustained load
- System load affecting benchmark accuracy
- Background processes impacting memory bandwidth
- Power management affecting GPU clocks

### Debugging Performance Issues

#### Memory Bandwidth
1. Check memory access patterns (coalesced vs random)
2. Verify data size alignment
3. Monitor thermal throttling
4. Compare unified vs discrete memory performance

#### Compute Performance
1. Validate threadgroup size optimization
2. Check arithmetic intensity vs memory bandwidth
3. Monitor GPU utilization
4. Compare against theoretical peak performance

#### Compilation Performance
1. Check kernel complexity and size
2. Monitor shader compiler optimization levels
3. Verify Metal shader language version compatibility
4. Test with different GPU architectures

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Add appropriate test traits/categories
3. Include performance baselines for new benchmarks
4. Add hardware-specific scaling factors
5. Update documentation with new test descriptions
6. Ensure proper resource cleanup and error handling

### Performance Test Guidelines
- Use consistent iteration counts for benchmarks
- Include warm-up iterations to stabilize performance
- Measure multiple metrics (time, bandwidth, GFLOPS)
- Add appropriate tolerance for performance variations
- Scale tests based on detected hardware capabilities
- Include verification of computational correctness

### Code Style
- Use descriptive test names that explain what is being tested
- Include detailed output for debugging failed tests
- Implement proper async/await patterns
- Use `using` statements for resource cleanup
- Add comprehensive error handling and diagnostics