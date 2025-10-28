# Metal Backend Performance Benchmarks

Comprehensive benchmarks to **VALIDATE** all performance claims made by the Metal backend.

## Claims Under Test

| Category | Claim | Target | Benchmark |
|----------|-------|--------|-----------|
| **Unified Memory** | 2-3x speedup | Optimized should be 33-50% of baseline | `UnifiedMemory_*` |
| **MPS Operations** | 3-4x speedup | Optimized should be 25-33% of baseline | `MPS_*` |
| **Memory Pooling** | 90% allocation reduction | ≥85% reduction | `MemoryPool_*` |
| **Startup Time** | Sub-10ms cold start | <10ms | `Backend_ColdStart_*` |
| **Kernel Compilation** | <1ms cache hits | <1ms | `Kernel_Compilation_CacheHit` |
| **Command Queue** | <100μs latency | <100μs | `CommandQueue_AcquisitionLatency` |
| **Command Queue** | >80% reuse rate | >80% | `CommandQueue_ReuseRate` |
| **Graph Execution** | >1.5x parallel speedup | Optimized <67% of baseline | `Graph_*` |

## Requirements

- **Platform**: macOS 12.0+ (Monterey or newer)
- **Hardware**: Apple Silicon (M1/M2/M3) for unified memory tests
- **Runtime**: .NET 9.0 SDK
- **Tools**: BenchmarkDotNet 0.13.12+

## Running Benchmarks

### Run All Benchmarks
```bash
cd tests/Performance/DotCompute.Backends.Metal.Benchmarks
dotnet run -c Release
```

### Run Specific Categories
```bash
# Unified memory tests only
dotnet run -c Release -- --filter *UnifiedMemory*

# MPS performance tests only
dotnet run -c Release -- --filter *MPS*

# Memory pooling tests only
dotnet run -c Release -- --filter *MemoryPooling*

# Startup time test only
dotnet run -c Release -- --filter *Startup*

# Kernel compilation tests only
dotnet run -c Release -- --filter *Compilation*

# Command queue tests only
dotnet run -c Release -- --filter *CommandQueue*

# Graph execution tests only
dotnet run -c Release -- --filter *GraphExecution*
```

### Advanced Options
```bash
# Run with memory profiling
dotnet run -c Release -- --memory

# Run with detailed diagnostics
dotnet run -c Release -- --disasm --profiler ETW

# Export results to JSON
dotnet run -c Release -- --exporters json

# Run quick validation (fewer iterations)
dotnet run -c Release -- --job short
```

## Benchmark Details

### 1. Unified Memory Performance (2 benchmarks)

#### `UnifiedMemory_DiscreteMemory_Baseline`
- **Tests**: MTLStorageModeManaged (discrete GPU memory)
- **Operation**: 1M float array, copy to device and back
- **Baseline**: Establishes discrete memory performance

#### `UnifiedMemory_ZeroCopy_Optimized`
- **Tests**: MTLStorageModeShared (unified memory with zero-copy)
- **Target**: 2-3x faster than baseline (33-50% of baseline time)
- **Validation**: On Apple Silicon, should use zero-copy optimization

### 2. MPS Performance (2 benchmarks)

#### `MPS_CustomMatMul_Baseline`
- **Tests**: Custom Metal kernel for 512×512 matrix multiplication
- **Implementation**: Basic unoptimized matmul kernel
- **Baseline**: Shows custom kernel performance

#### `MPS_AcceleratedMatMul_Optimized`
- **Tests**: Metal Performance Shaders (MPS) matrix multiply
- **Target**: 3-4x faster than baseline (25-33% of baseline time)
- **Validation**: Apple's optimized MPS implementation

### 3. Memory Pooling (2 benchmarks)

#### `MemoryPool_DirectAllocation_Baseline`
- **Tests**: 100 allocate/free cycles without pooling
- **Measurement**: Counts actual allocations (should be 100)
- **Baseline**: Shows allocation overhead without pooling

#### `MemoryPool_PooledAllocation_Optimized`
- **Tests**: 100 allocate/free cycles with pooling
- **Target**: ≥85% allocation reduction (≤15 actual allocations)
- **Validation**: Checks `MemoryPoolStatistics.AllocationReductionPercentage`
- **Performance**: <5% time overhead vs baseline

### 4. Startup Time (1 benchmark)

#### `Backend_ColdStart_Initialization`
- **Tests**: MetalAccelerator initialization from scratch
- **Target**: <10ms total startup time
- **Validation**: Uses Stopwatch to measure initialization
- **Includes**: Device creation, command queue setup, compiler init

### 5. Kernel Compilation (2 benchmarks)

#### `Kernel_Compilation_CacheMiss`
- **Tests**: First compilation of unique kernel (cache miss)
- **Operation**: Compile simple Metal kernel from source
- **Baseline**: Shows full compilation cost

#### `Kernel_Compilation_CacheHit`
- **Tests**: Subsequent compilation of same kernel (cache hit)
- **Target**: <1ms retrieval from cache
- **Validation**: Should be >10x faster than cache miss
- **Measurement**: Uses Stopwatch for sub-millisecond precision

### 6. Command Queue (2 benchmarks)

#### `CommandQueue_AcquisitionLatency`
- **Tests**: Time to get command buffer from queue
- **Target**: <100μs (0.1ms) per acquisition
- **Validation**: Measures in microseconds using Stopwatch

#### `CommandQueue_ReuseRate`
- **Tests**: Command buffer pooling effectiveness
- **Target**: >80% reuse rate from pool
- **Operation**: Request 100 buffers, return, request 100 more
- **Validation**: Checks `CommandBufferPoolStats.CacheHits` ratio

### 7. Graph Execution (2 benchmarks)

#### `Graph_Sequential_Baseline`
- **Tests**: 4 independent kernels executed sequentially
- **Operation**: Each kernel processes 10K elements
- **Baseline**: Shows sequential execution cost

#### `Graph_Parallel_Optimized`
- **Tests**: 4 independent kernels executed in parallel
- **Target**: >1.5x speedup (<67% of baseline time)
- **Implementation**: Uses Task.WhenAll for parallel execution
- **Validation**: Metal should dispatch kernels concurrently

## Interpreting Results

### Success Criteria

Each benchmark includes `Debug.Assert` statements that validate claims:

```csharp
// Example assertions in benchmarks
Debug.Assert(sw.ElapsedMilliseconds < 10, "Startup <10ms");
Debug.Assert(reduction >= 85.0, "≥85% reduction");
Debug.Assert(microseconds < 100, "<100μs latency");
Debug.Assert(reuseRate > 0.80, ">80% reuse");
```

### Reading BenchmarkDotNet Output

```
|                      Method |      Mean |    StdDev | Ratio |
|---------------------------- |----------:|----------:|------:|
| UnifiedMemory_Discrete_Base | 10.50 ms  | 0.25 ms   |  1.00 |
| UnifiedMemory_ZeroCopy_Opt  |  4.20 ms  | 0.15 ms   |  0.40 | ✓ 2.5x speedup
```

**Key Metrics**:
- **Mean**: Average execution time
- **StdDev**: Consistency (lower is better)
- **Ratio**: Performance vs baseline (lower is better)

### Validation Checklist

- [ ] **Unified Memory**: Ratio 0.33-0.50 (2-3x speedup)
- [ ] **MPS**: Ratio 0.25-0.33 (3-4x speedup)
- [ ] **Memory Pooling**: ≥85% reduction, <5% time overhead
- [ ] **Startup**: Mean <10ms
- [ ] **Cache Hit**: Mean <1ms
- [ ] **Queue Latency**: Mean <0.1ms (100μs)
- [ ] **Queue Reuse**: >80% cache hit rate
- [ ] **Graph Parallel**: Ratio <0.67 (>1.5x speedup)

## Troubleshooting

### Issue: Benchmarks Fail on Intel Mac

**Cause**: Unified memory tests require Apple Silicon
**Solution**: Run only discrete GPU benchmarks:
```bash
dotnet run -c Release -- --filter *MPS* *Compilation* *CommandQueue*
```

### Issue: MPS Benchmarks Fail

**Cause**: MetalPerformanceShaders not available
**Solution**: Check macOS version (requires 12.0+) and ensure MPS is enabled in `MetalAcceleratorOptions`

### Issue: Pooling Tests Don't Show Reduction

**Cause**: Memory pool not properly initialized
**Solution**: Verify `enablePooling: true` in `MetalMemoryManager` constructor

### Issue: Startup Time >10ms

**Cause**: Debug build or cold system
**Solution**:
- Always run with `-c Release`
- Run benchmarks twice (first run warms up system)
- Close other applications to reduce system load

## Contributing

When adding new benchmarks:

1. **Add Category**: Use `[BenchmarkCategory("YourCategory")]`
2. **Add Baseline**: Mark one benchmark as `[Benchmark(Baseline = true)]`
3. **Add Validation**: Include `Debug.Assert` for performance targets
4. **Update Docs**: Add to this README with target metrics
5. **Update Claims**: Document in table above

## License

Copyright (c) 2025 Michael Ivertowski
Licensed under MIT License
