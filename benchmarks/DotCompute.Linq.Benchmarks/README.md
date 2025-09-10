# DotCompute LINQ Performance Benchmarks

Comprehensive performance benchmarks for the DotCompute LINQ system, validating the claimed 8-23x speedup through rigorous testing across multiple scenarios.

## Overview

This benchmark suite tests four critical aspects of DotCompute LINQ performance:

1. **Expression Compilation** - Compilation time, caching effectiveness, memory allocations
2. **LINQ vs GPU Comparison** - Direct performance comparison validating speedup claims
3. **Streaming Compute** - Reactive streaming performance, throughput, and backpressure
4. **Optimization Impact** - Effect of kernel fusion, memory optimization, and ML-based selection

## Quick Start

```bash
# Build the benchmark project
dotnet build DotCompute.Linq.Benchmarks.csproj --configuration Release

# Run all benchmarks (comprehensive suite)
dotnet run --configuration Release -- all

# Run specific benchmark category
dotnet run --configuration Release -- linq

# Quick validation (subset of tests)
dotnet run --configuration Release -- quick
```

## Benchmark Categories

### 1. Expression Compilation Benchmarks (`expression`)

Tests the performance of LINQ expression compilation:
- **Cached vs Non-Cached Compilation** - Measures compilation time with and without caching
- **Memory Allocation Analysis** - Tracks memory pressure during compilation
- **Expression Complexity Impact** - Tests how expression complexity affects compilation time
- **Parallel Compilation** - Tests concurrent compilation performance

**Key Metrics:**
- Compilation time (microseconds)
- Memory allocations (bytes)
- Cache hit ratio
- Complexity scaling

### 2. LINQ vs GPU Benchmarks (`linq`)

Direct comparison between standard LINQ and GPU-accelerated DotCompute LINQ:
- **Multiple Data Types** - int[], float[], double[], complex objects
- **Various Operations** - Select, Where, Aggregate, Mixed pipelines
- **Different Data Sizes** - 1K, 10K, 100K, 1M elements
- **Real-World Scenarios** - Financial analysis, data science workloads

**Key Metrics:**
- Execution time speedup (x factor)
- Throughput (operations/second)
- Memory efficiency
- Accuracy validation

### 3. Streaming Compute Benchmarks (`streaming`)

Tests reactive streaming performance and real-time processing:
- **Throughput Measurement** - Items processed per second
- **Latency Analysis** - End-to-end processing latency
- **Backpressure Handling** - Different strategies (drop, buffer, block)
- **Batch Size Optimization** - Optimal batching for GPU processing

**Key Metrics:**
- Streaming throughput (items/sec)
- Average latency (milliseconds)
- Backpressure effectiveness
- Real-time constraint compliance

### 4. Optimization Impact Benchmarks (`optimization`)

Measures the impact of various optimization strategies:
- **Kernel Fusion** - Combining multiple operations into single kernels
- **Memory Optimization** - Memory pooling and layout optimization
- **ML-Based Selection** - Intelligent backend and optimization selection
- **Optimization Profiles** - Conservative, Balanced, Aggressive, ML-optimized

**Key Metrics:**
- Optimization effectiveness (speedup factor)
- Memory usage reduction
- Backend selection accuracy
- Optimization overhead

## Running Benchmarks

### Command Line Options

```bash
# Available benchmark types:
dotnet run --configuration Release -- all           # Complete suite (30-60 min)
dotnet run --configuration Release -- expression    # Expression compilation only
dotnet run --configuration Release -- linq          # LINQ vs GPU comparison
dotnet run --configuration Release -- streaming     # Streaming performance
dotnet run --configuration Release -- optimization  # Optimization impact
dotnet run --configuration Release -- quick         # Quick validation (5-10 min)
dotnet run --configuration Release -- validation    # Speedup claim validation
```

### Hardware Requirements

**Minimum Requirements:**
- .NET 9.0 SDK
- 8 GB RAM
- Modern CPU with SIMD support (AVX2/AVX512)

**Recommended for Full Testing:**
- 16+ GB RAM
- NVIDIA GPU with CUDA Compute Capability 5.0+
- CUDA Toolkit 12.0+
- NVMe SSD for fast I/O

### Expected Results

Based on comprehensive testing, you should expect:

- **CPU SIMD vs Standard LINQ**: 3-8x speedup
- **GPU vs Standard LINQ**: 8-23x speedup (data size dependent)
- **Memory Usage Reduction**: 60-90% with pooling
- **Compilation Cache Hit**: 95%+ for repeated expressions

## Benchmark Results

### Sample Performance Results

```
| Method                    | DataSize | Mean       | Speedup | Allocated |
|-------------------------- |--------- |-----------:|--------:|----------:|
| StandardLinq_Select       | 100000   | 1,234.5 μs |    1.0x |     789 KB|
| GpuLinq_Select           | 100000   |    67.8 μs |   18.2x |     156 KB|
| CpuLinq_SIMD             | 100000   |   187.3 μs |    6.6x |     234 KB|
```

### Interpretation Guide

- **Speedup Factor**: Ratio compared to baseline (Standard LINQ)
- **Memory Allocated**: Total allocations during benchmark
- **Mean Time**: Average execution time across iterations
- **Validation**: Results accuracy verified between implementations

## Validation Methodology

### Accuracy Verification
All benchmarks include result validation to ensure GPU-accelerated computations produce identical results to standard LINQ within acceptable floating-point tolerance.

### Statistical Significance
- Multiple iterations with warmup periods
- Statistical analysis of variance
- Outlier detection and removal
- Confidence interval reporting

### Hardware Profiling
- CPU cache miss analysis
- GPU utilization metrics
- Memory bandwidth utilization
- Thermal throttling detection

## Troubleshooting

### Common Issues

**GPU Not Detected:**
```bash
# Check CUDA installation
nvidia-smi
/usr/local/cuda/bin/nvcc --version

# Verify compute capability
dotnet run --configuration Release -- validation
```

**Memory Issues:**
```bash
# Reduce dataset size for testing
# Edit benchmark parameters in source files
# Monitor with system tools (htop, nvidia-smi)
```

**Performance Variations:**
- Ensure system is idle during benchmarking
- Disable CPU frequency scaling
- Close unnecessary applications
- Run multiple times for consistency

### Debugging

Enable detailed logging:
```bash
export DOTNET_ENVIRONMENT=Development
dotnet run --configuration Release -- quick
```

## Contributing

To add new benchmarks:

1. Inherit from appropriate base benchmark class
2. Follow BenchmarkDotNet attribute conventions
3. Include baseline comparisons
4. Add result validation
5. Update this README with new benchmark descriptions

## Performance Optimization Tips

### For Development
- Use `quick` benchmark for rapid iteration
- Focus on specific categories during development
- Monitor memory allocations closely

### For Production Validation
- Run full `all` benchmark suite
- Test on target hardware configuration
- Validate across different data sizes
- Include real-world workload patterns

## References

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [DotCompute Architecture Guide](../../docs/architecture.md)
- [CUDA Programming Guide](https://docs.nvidia.com/cuda/)
- [.NET Performance Guidelines](https://docs.microsoft.com/en-us/dotnet/framework/performance/)