# DotCompute Performance Test Suite

This directory contains comprehensive performance benchmarks for the DotCompute accelerated computing framework. The test suite evaluates kernel compilation, memory operations, scalability, and comparative performance across different backends and optimization levels.

## Test Categories

### 1. KernelPerformanceTests.cs
**Purpose**: Measures kernel compilation time, execution latency, and throughput performance.

**Key Features**:
- Compilation time benchmarks across optimization levels
- Execution latency profiling with statistical analysis
- Throughput measurements under continuous load
- Latency distribution analysis with outlier detection

**Test Traits**: `Performance`, `Benchmark`, `KernelCompilation`

### 2. MemoryPerformanceTests.cs
**Purpose**: Evaluates memory allocation speed, transfer bandwidth, and concurrent memory operations.

**Key Features**:
- Memory allocation performance scaling
- Host-to-Device (H2D) transfer bandwidth measurement  
- Device-to-Host (D2H) transfer bandwidth measurement
- Device-to-Device (D2D) internal transfer performance
- Memory pool efficiency comparisons
- Concurrent allocation and transfer performance

**Test Traits**: `Performance`, `MemoryIntensive`, `Concurrency`

### 3. ScalabilityTests.cs
**Purpose**: Analyzes performance scaling with workload size and thread count, resource utilization monitoring.

**Key Features**:
- Workload size scalability analysis (1K to 50M elements)
- Multi-threading efficiency measurements
- Resource contention analysis under high concurrency
- Performance breakpoint identification
- Resource utilization monitoring (CPU, Memory, GPU)

**Test Traits**: `Performance`, `LongRunning`, `Concurrency`

### 4. ComparisonBenchmarks.cs
**Purpose**: Comprehensive performance comparisons between different backends, algorithms, and optimization levels.

**Key Features**:
- CPU vs GPU speedup analysis
- Optimization level impact measurement
- Algorithm implementation comparisons
- Backend performance comparisons (CUDA vs OpenCL)
- Statistical significance testing

**Test Traits**: `Performance`, `Benchmark`

## Usage

### Running All Performance Tests
```bash
dotnet test --filter "Category=Performance"
```

### Running Specific Test Categories
```bash
# Memory-intensive tests only
dotnet test --filter "Category=MemoryIntensive"

# Benchmarking comparisons
dotnet test --filter "Category=Benchmark"

# Long-running scalability tests
dotnet test --filter "Category=LongRunning"

# Concurrency tests
dotnet test --filter "Category=Concurrency"
```

### Running Individual Test Classes
```bash
# Kernel performance tests
dotnet test --filter "FullyQualifiedName~KernelPerformanceTests"

# Memory performance tests
dotnet test --filter "FullyQualifiedName~MemoryPerformanceTests"

# Scalability tests
dotnet test --filter "FullyQualifiedName~ScalabilityTests"

# Comparison benchmarks
dotnet test --filter "FullyQualifiedName~ComparisonBenchmarks"
```

## Test Configuration

### Environment Requirements
- .NET 9.0 or later
- Sufficient system memory for large workload tests (recommend 16GB+)
- Hardware accelerators (GPU/CUDA) for realistic performance comparisons
- Extended test timeouts configured for long-running tests

### Performance Baselines
The tests include baseline measurements and performance assertions:

- **Kernel Compilation**: < 1000ms average for standard kernels
- **Memory Allocation**: Consistent timing with < 20% coefficient of variation
- **Transfer Bandwidth**: Efficiency targets based on theoretical hardware limits
- **Thread Scaling**: Efficiency > 50% for reasonable thread counts
- **CPU vs GPU Speedup**: Algorithm-specific expectations (2x-50x range)

### Test Data Sizes
Scalable test data sizes for comprehensive coverage:
- Small: 1K - 10K elements
- Medium: 100K elements  
- Large: 1M elements
- Very Large: 10M elements
- Extreme: 50M elements

## Output and Analysis

### Test Metrics Collected
- **Timing**: Average, min, max, standard deviation
- **Throughput**: Operations per second/millisecond
- **Memory Usage**: Allocation overhead, transfer bandwidth
- **Resource Utilization**: CPU, memory, GPU utilization percentages  
- **Scaling Efficiency**: Performance scaling ratios
- **Statistical Significance**: T-test results for comparisons

### Performance Reports
Tests generate detailed performance reports including:
- Execution time distributions with percentile analysis
- Scaling trend analysis and breakpoint identification
- Resource utilization patterns
- Comparative performance rankings
- Statistical significance assessments

## Integration with BenchmarkDotNet

The test suite is designed for compatibility with BenchmarkDotNet for advanced benchmarking:

```bash
# Run with BenchmarkDotNet integration
dotnet run --project DotCompute.Performance.Tests -- --benchmarkdotnet
```

**Note**: BenchmarkDotNet attributes are included but may require additional configuration for full integration.

## Hardware Compatibility

### Supported Accelerators
- **CPU**: Multi-core CPU testing with SIMD support
- **NVIDIA GPU**: CUDA-based acceleration testing  
- **AMD GPU**: OpenCL-based acceleration testing
- **Intel GPU**: Integrated graphics testing

### Hardware Detection
Tests automatically detect available hardware and skip unsupported scenarios:
- CUDA availability detection
- OpenCL runtime detection  
- GPU memory capacity validation
- Compute capability requirements

## Troubleshooting

### Common Issues
1. **Test Timeouts**: Increase timeout values for slower hardware
2. **Memory Issues**: Reduce workload sizes for systems with limited memory
3. **Hardware Missing**: Tests will skip gracefully if required hardware is unavailable
4. **Compilation Failures**: Check accelerator driver installations

### Performance Expectations
- Tests include realistic performance expectations based on hardware capabilities
- Baseline measurements are established during test initialization
- Performance regressions are detected through assertion validation

## Contributing

When adding new performance tests:
1. Follow the established naming conventions
2. Include appropriate test traits for categorization
3. Implement proper warmup and measurement phases
4. Add statistical analysis for result validation
5. Include hardware requirement specifications
6. Document expected performance characteristics

## License

Copyright (c) 2025 Michael Ivertowski  
Licensed under the MIT License. See LICENSE file in the project root for license information.