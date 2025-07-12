# DotCompute Performance Benchmarks

This comprehensive performance benchmarking suite provides detailed analysis of memory allocation, vectorization, data transfer, and thread pool performance for the DotCompute project.

## Overview

The benchmark suite consists of:
- **Core Benchmarks**: Memory allocation, vectorization, data transfer, and thread pool performance
- **Stress Tests**: 24-hour memory leak detection, concurrent allocation stress, and high-frequency transfer stress
- **Performance Analysis**: Bottleneck identification, memory profiling, and optimization recommendations

## Quick Start

### Running All Benchmarks
```bash
cd tests/DotCompute.Performance.Benchmarks
dotnet run --configuration Release
```

### Running Specific Benchmark Categories
```bash
# Memory allocation benchmarks
dotnet run --configuration Release -- MemoryAllocationBenchmarks

# Vectorization benchmarks  
dotnet run --configuration Release -- VectorizationBenchmarks

# Transfer benchmarks
dotnet run --configuration Release -- TransferBenchmarks

# Thread pool benchmarks
dotnet run --configuration Release -- ThreadPoolBenchmarks
```

### Running Stress Tests
```bash
# Memory leak stress test (24 hours)
dotnet run --configuration Release -- MemoryLeakStressTest

# Concurrent allocation stress test
dotnet run --configuration Release -- ConcurrentAllocationStressTest

# High-frequency transfer stress test
dotnet run --configuration Release -- HighFrequencyTransferStressTest
```

## Performance Targets

### Memory Allocation
- **Managed Array**: < 1000 ns allocation time
- **Array Pool**: < 500 ns rent time
- **Native Memory**: < 100 ns allocation time
- **Small Allocations (1KB)**: > 1M ops/sec
- **Large Allocations (1MB)**: > 10K ops/sec

### Vectorization
- **Addition/Multiplication**: 4-8x speedup over scalar
- **Dot Product**: 8-16x speedup over scalar
- **Float Operations**: > 1B ops/sec

### Data Transfer
- **Memory-to-Memory**: > 10 GB/sec bandwidth
- **Host-to-Device**: > 1 GB/sec bandwidth
- **Small Transfers (1KB)**: < 100 ns latency
- **Large Transfers (1MB)**: < 1000 ns latency

### Thread Pool
- **Task Spawning**: < 1000 ns
- **Work Item Processing**: > 100K ops/sec
- **Queue Throughput**: > 1M ops/sec
- **Scalability**: Linear scaling up to CPU cores

## Benchmark Categories

### 1. Memory Allocation Benchmarks
- **Managed Array Allocation**: Standard .NET array allocation
- **Array Pool Allocation**: Using ArrayPool<T>.Shared
- **Native Memory Allocation**: Using NativeMemory APIs
- **Memory Pool Allocation**: Using MemoryPool<T>
- **Stack Allocation**: Using stackalloc for small arrays
- **Pinned Memory**: Using GC.AllocateUninitializedArray with pinned flag

### 2. Vectorization Benchmarks
- **Scalar vs Vector Operations**: Addition, multiplication, dot product
- **AVX2 Optimizations**: Platform-specific vectorization
- **Parallel Vector Operations**: Multi-threaded vectorization
- **Matrix Operations**: Vectorized matrix multiplication

### 3. Data Transfer Benchmarks
- **Array Copy**: Array.Copy, Buffer.BlockCopy
- **Span Copy**: Memory<T>.CopyTo, Span<T>.CopyTo
- **Unsafe Memory Copy**: Buffer.MemoryCopy, NativeMemory.Copy
- **Channel Transfer**: Using System.Threading.Channels
- **Stream Transfer**: MemoryStream operations
- **Parallel Transfer**: Multi-threaded data movement

### 4. Thread Pool Benchmarks
- **Task Scheduling**: Task.Run, Task.Factory.StartNew
- **Parallel Operations**: Parallel.For, Parallel.ForEach
- **Producer-Consumer**: Channel-based patterns
- **Custom Thread Pool**: Custom implementation comparison
- **Work Item Processing**: Different concurrency patterns

## Stress Tests

### 1. Memory Leak Stress Test (24 hours)
- **Continuous Allocation**: Steady allocation pattern
- **Burst Allocation**: Periodic high-allocation bursts
- **Large Object Allocation**: LOH stress testing
- **Array Pool Patterns**: Pool usage stress
- **Native Memory Patterns**: Unmanaged memory stress
- **Metrics**: Memory growth, GC pressure, leak detection

### 2. Concurrent Allocation Stress Test
- **Multi-threaded Allocation**: Concurrent allocation patterns
- **Allocation Types**: Managed, pooled, native, large objects
- **Failure Handling**: Out-of-memory scenarios
- **Performance Metrics**: Allocation rates, failure rates
- **Thread Safety**: Concurrent access patterns

### 3. High-Frequency Transfer Stress Test
- **Producer-Consumer Patterns**: Multiple transfer mechanisms
- **Transfer Types**: Memory-to-memory, channels, streams
- **Concurrency Levels**: Configurable producer/consumer counts
- **Sustained Load**: Long-duration high-frequency transfers
- **Latency Monitoring**: Real-time transfer latency tracking

## Performance Analysis

### 1. Memory Profiling
- **Working Set Monitoring**: Process memory usage
- **GC Metrics**: Collection counts, memory pressure
- **Allocation Tracking**: Total allocated bytes
- **Memory Growth Rate**: Memory usage over time
- **Leak Detection**: Weak reference tracking

### 2. CPU Profiling
- **CPU Usage**: System and process CPU utilization
- **Thread Metrics**: Thread count, context switches
- **Processor Time**: User vs privileged time
- **Hot Path Analysis**: Performance bottleneck identification

### 3. Bottleneck Analysis
- **Memory Bottlenecks**: High allocation rates, GC pressure
- **CPU Bottlenecks**: High CPU usage, excessive threading
- **Thread Pool Bottlenecks**: Queue saturation, thread starvation
- **Recommendations**: Automated optimization suggestions

## Configuration

### Benchmark Settings
- **Warmup Iterations**: 3 (configurable)
- **Measurement Iterations**: 10 (configurable)
- **Platform**: x64 (optimized for 64-bit)
- **Runtime**: .NET 8.0 (latest LTS)
- **GC Configuration**: Server GC with concurrent collection

### Hardware Baselines
- **CPU Cores**: 8 (baseline)
- **Memory**: 16 GB (baseline)
- **Cache**: L1: 32KB, L2: 1MB, L3: 8MB
- **Memory Bandwidth**: 25.6 GB/s (baseline)

## Output and Reports

### Benchmark Results
- **Markdown Reports**: Human-readable results
- **JSON Reports**: Machine-readable detailed data
- **HTML Reports**: Interactive web-based results
- **CSV Reports**: Spreadsheet-compatible data

### Performance Profiles
- **Memory Usage**: Working set, GC metrics over time
- **CPU Usage**: Thread count, processor time over time
- **Bottleneck Analysis**: Identified performance issues
- **Optimization Recommendations**: Automated suggestions

### Stress Test Reports
- **Memory Leak Analysis**: Growth patterns, leak indicators
- **Concurrent Allocation**: Throughput, failure rates
- **Transfer Performance**: Latency, throughput analysis

## Integration

### Continuous Integration
```yaml
# Example GitHub Actions workflow
- name: Run Performance Benchmarks
  run: |
    cd tests/DotCompute.Performance.Benchmarks
    dotnet run --configuration Release
```

### Performance Monitoring
- **Baseline Establishment**: Initial performance metrics
- **Regression Detection**: Performance degradation alerts
- **Trend Analysis**: Performance over time
- **Automated Reporting**: CI/CD integration

## Troubleshooting

### Common Issues
1. **Performance Counter Access**: Requires elevated permissions on Windows
2. **Memory Allocation Failures**: Adjust system virtual memory
3. **Thread Pool Exhaustion**: Monitor concurrent operations
4. **Vectorization Not Applied**: Check CPU capabilities and compiler flags

### Debug Options
```bash
# Enable detailed logging
dotnet run --configuration Release -- --verbose

# Run specific benchmark with profiling
dotnet run --configuration Release -- MemoryAllocationBenchmarks --profile

# Generate disassembly for vectorization analysis
dotnet run --configuration Release -- VectorizationBenchmarks --disasm
```

## Hardware Recommendations

### Optimal Configuration
- **CPU**: 8+ cores with AVX2 support
- **Memory**: 16+ GB RAM
- **Storage**: SSD for fast I/O
- **OS**: Windows 10/11 or Linux with recent kernel

### Minimum Requirements
- **CPU**: 4+ cores
- **Memory**: 8+ GB RAM
- **Storage**: Any (benchmarks are CPU/memory intensive)
- **OS**: Any supported .NET 8.0 platform

## Performance Optimization Guidelines

### Memory Optimization
1. **Use ArrayPool<T>** for frequently allocated arrays
2. **Implement Object Pooling** for expensive objects
3. **Use stackalloc** for small, short-lived arrays
4. **Minimize Large Object allocations** (>85KB)

### Vectorization Optimization
1. **Use System.Numerics.Vector<T>** for portable vectorization
2. **Consider System.Runtime.Intrinsics** for platform-specific optimizations
3. **Ensure proper data alignment** for optimal performance
4. **Profile with disassembly** to verify vectorization

### Transfer Optimization
1. **Use Memory<T> and Span<T>** for efficient operations
2. **Implement batching** for small, frequent transfers
3. **Use async patterns** for I/O-bound operations
4. **Consider channels** for producer-consumer scenarios

### Thread Pool Optimization
1. **Use Task.Run** for CPU-bound work
2. **Use async/await** for I/O-bound operations
3. **Avoid blocking** on async operations
4. **Monitor thread pool metrics** in production

## Contributing

### Adding New Benchmarks
1. Create benchmark class in `Benchmarks/` directory
2. Inherit from appropriate base class
3. Add `[Benchmark]` attributes to methods
4. Update configuration in `Config/BenchmarkConfig.cs`

### Adding New Stress Tests
1. Create stress test class in `Stress/` directory
2. Implement comprehensive metrics collection
3. Add progress reporting and error handling
4. Update test runner configuration

### Performance Analysis
1. Extend `PerformanceAnalyzer` class
2. Add new metrics collection
3. Implement bottleneck detection logic
4. Generate actionable recommendations

## License

This performance benchmarking suite is part of the DotCompute project and follows the same license terms.