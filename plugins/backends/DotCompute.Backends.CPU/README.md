# DotCompute CPU Backend

High-performance CPU backend for DotCompute with SIMD vectorization support.

## Features

- **SIMD Detection**: Automatic detection of CPU capabilities (SSE, AVX, AVX2, AVX512, NEON)
- **Thread Pool**: Optimized thread pool with work-stealing and thread affinity
- **Vectorization**: Automatic vectorization for compatible kernels
- **NUMA Support**: NUMA-aware memory allocation (when available)
- **AOT Compatible**: Fully compatible with Native AOT compilation

## Supported Instruction Sets

### x86/x64
- SSE, SSE2, SSE3, SSSE3, SSE4.1, SSE4.2
- AVX, AVX2
- AVX512 (F, BW, CD, DQ, VBMI)
- FMA, BMI1, BMI2
- POPCNT, LZCNT

### ARM
- NEON (Advanced SIMD)
- ARM64 extensions
- CRC32, AES, SHA1, SHA256
- Dot Product (DP)
- Rounding Double Multiply (RDM)

## Usage

```csharp
// Add CPU backend to services
services.AddCpuBackend(options =>
{
    options.MaxWorkGroupSize = 1024;
    options.EnableAutoVectorization = true;
    options.EnableNumaAwareAllocation = true;
});

// Configure thread pool
services.AddCpuBackend(
    configureAccelerator: options =>
    {
        options.MaxMemoryAllocation = 4L * 1024 * 1024 * 1024; // 4GB
    },
    configureThreadPool: options =>
    {
        options.WorkerThreads = Environment.ProcessorCount;
        options.UseThreadAffinity = true;
        options.EnableWorkStealing = true;
    });
```

## Architecture

The CPU backend is designed for maximum performance:

1. **SIMD Capabilities Detection**: Automatically detects and utilizes available SIMD instructions
2. **Thread Pool Management**: Custom thread pool optimized for compute workloads
3. **Memory Management**: Efficient memory allocation with support for large buffers
4. **Kernel Compilation**: JIT compilation of kernels to vectorized native code with SIMD optimization

## Performance Considerations

- Uses `ArrayPool<T>` for memory allocations up to 2GB
- Thread affinity improves cache locality
- Work-stealing balances load across cores
- NUMA-aware allocation reduces memory latency

## Limitations

- Large allocations (>2GB) not yet supported
- Advanced kernel optimizations are being continuously improved
- Thread affinity only implemented on Windows

## Future Enhancements

- Full kernel JIT compilation with auto-vectorization
- Support for very large memory allocations
- Cross-platform thread affinity
- Advanced NUMA optimizations
- Custom SIMD intrinsics for specific operations