# DotCompute.Memory

High-performance unified memory management system for DotCompute with zero-copy operations and cross-device memory transfers.

## Status: ✅ Production Ready

The Memory module provides comprehensive memory management capabilities:
- **Memory Pooling**: 90% allocation reduction through object pooling
- **Zero-Copy Operations**: Span<T> and Memory<T> throughout
- **Cross-Device Transfers**: Unified abstraction for CPU/GPU memory
- **Thread Safety**: Lock-free operations and concurrent access
- **Native AOT**: Full compatibility with Native AOT compilation

## Key Features

### Unified Memory Management
- **UnifiedMemoryManager**: Central memory management authority for DotCompute
- **Cross-Backend Support**: Works with CPU, CUDA, Metal, OpenCL backends
- **Automatic Cleanup**: Background defragmentation and resource reclamation
- **Statistics and Monitoring**: Comprehensive metrics for memory usage

### Buffer Abstractions

#### OptimizedUnifiedBuffer<T>
Performance-optimized unified buffer with:
- Object pooling for frequent allocations (90% reduction target)
- Lazy initialization for expensive operations
- Zero-copy operations using Span<T> and Memory<T>
- Async-first design with optimized synchronization
- Memory prefetching for improved cache performance
- NUMA-aware memory allocation

#### Buffer States
Buffers track their state across devices:
- **HostOnly**: Data exists only in system memory
- **DeviceOnly**: Data exists only in device memory
- **Synchronized**: Data is consistent across host and device
- **HostDirty**: Host copy modified, needs sync to device
- **DeviceDirty**: Device copy modified, needs sync to host

### Memory Pooling

#### HighPerformanceObjectPool<T>
High-performance object pool optimized for compute workloads:
- Lock-free operations using ConcurrentStack
- Automatic pool size management
- Thread-local storage for hot paths
- Performance metrics and monitoring
- Configurable eviction policies
- NUMA-aware allocation when available

#### MemoryPool
Specialized memory pool for buffer management:
- Buffer size categorization for optimal reuse
- Automatic cleanup of unused buffers
- Configurable retention policies
- Cross-backend buffer pooling

### Advanced Transfer Engine

#### AdvancedMemoryTransferEngine
Sophisticated memory transfer system:
- **Concurrent Transfers**: Parallel host-device data movement
- **Transfer Pipelining**: Overlap compute and transfer operations
- **Adaptive Batching**: Automatic batch size optimization
- **Transfer Statistics**: Detailed performance metrics
- **Error Recovery**: Automatic retry with exponential backoff

#### Transfer Options
- **Asynchronous Transfers**: Non-blocking memory operations
- **Pinned Memory**: Use page-locked memory for faster transfers
- **Streaming Transfers**: Support for chunked data movement
- **Priority Scheduling**: Prioritize critical transfers

### Zero-Copy Operations

#### ZeroCopyOperations
Optimized operations that avoid unnecessary data copies:
- Direct Span<T> manipulation
- Memory-mapped operations
- Pointer-based transformations
- SIMD-accelerated operations when available

#### UnsafeMemoryOperations
Low-level unsafe memory operations:
- Pointer arithmetic utilities
- Unmanaged memory marshalling
- Platform-specific optimizations
- Bounds-checked unsafe operations

### Memory Utilities

#### MemoryAllocator
Centralized memory allocation with:
- Aligned memory allocation
- Platform-specific allocators
- Huge page support (when available)
- Memory tracking and diagnostics

#### ArrayPoolWrapper<T>
Wrapper around ArrayPool<T> with:
- Automatic size normalization
- Return tracking to prevent double-free
- Usage statistics
- Integration with DotCompute pooling

### Performance Metrics

#### MemoryStatistics
Comprehensive memory usage statistics:
- Total bytes allocated/freed
- Active allocation count
- Peak memory usage
- Allocation/deallocation rates
- Pool efficiency metrics

#### BufferPerformanceMetrics
Per-buffer performance tracking:
- Transfer count and total time
- Average transfer bandwidth
- Last access timestamp
- Cache hit/miss ratio

#### TransferStatistics
Memory transfer performance data:
- Bytes transferred (host-to-device, device-to-host)
- Transfer count and average time
- Bandwidth utilization
- Transfer efficiency metrics

## Installation

```bash
dotnet add package DotCompute.Memory --version 0.5.3
```

## Usage

### Basic Memory Management

```csharp
using DotCompute.Memory;
using DotCompute.Abstractions;

// Create memory manager (typically done by accelerator)
var memoryManager = new UnifiedMemoryManager(accelerator, logger);

// Allocate a buffer
var buffer = await memoryManager.AllocateAsync<float>(1_000_000);

// Write data to buffer
var data = new float[1_000_000];
await buffer.CopyFromAsync(data);

// Use buffer in kernel execution
await kernel.ExecuteAsync(buffer);

// Read results back
var results = new float[1_000_000];
await buffer.CopyToAsync(results);

// Dispose when done (returns to pool if eligible)
await buffer.DisposeAsync();
```

### CPU-Only Memory Manager

```csharp
using DotCompute.Memory;

// Create CPU-only memory manager
var memoryManager = new UnifiedMemoryManager(logger);

// Allocate CPU buffers
var buffer = await memoryManager.AllocateAsync<double>(10_000);

// All operations work on CPU memory
await buffer.CopyFromAsync(cpuData);
```

### Zero-Copy Operations with Span<T>

```csharp
using DotCompute.Memory;

// Allocate buffer
var buffer = await memoryManager.AllocateAsync<float>(1000);

// Get host span for zero-copy access
var span = buffer.AsSpan();

// Direct manipulation without copying
for (int i = 0; i < span.Length; i++)
{
    span[i] = i * 2.0f;
}

// Mark buffer as dirty to sync to device
await buffer.SynchronizeAsync();
```

### Memory Pooling

```csharp
using DotCompute.Memory;

// High-performance object pool
var pool = new HighPerformanceObjectPool<MyObject>(
    createFunc: () => new MyObject(),
    resetAction: obj => obj.Reset(),
    validateFunc: obj => obj.IsValid(),
    config: new PoolConfiguration
    {
        MaxPoolSize = 1000,
        MinPoolSize = 10,
        EvictionPolicy = EvictionPolicy.LRU
    },
    logger: logger
);

// Get object from pool
var obj = pool.Get();

// Use object
obj.DoWork();

// Return to pool for reuse
pool.Return(obj);

// Get pool statistics
var stats = pool.GetStatistics();
Console.WriteLine($"Pool hits: {stats.PoolHitRate:P2}");
Console.WriteLine($"Allocation reduction: {stats.AllocationReduction:P2}");
```

### Advanced Transfer Engine

```csharp
using DotCompute.Memory;
using DotCompute.Memory.Types;

var transferEngine = new AdvancedMemoryTransferEngine(accelerator, logger);

// Concurrent transfers with options
var options = new ConcurrentTransferOptions
{
    MaxConcurrency = 4,
    UsePinnedMemory = true,
    EnablePipelining = true,
    ChunkSize = 1024 * 1024 // 1MB chunks
};

var transfers = new[]
{
    (source: data1, destination: deviceBuffer1),
    (source: data2, destination: deviceBuffer2),
    (source: data3, destination: deviceBuffer3)
};

var result = await transferEngine.ExecuteConcurrentTransfersAsync(transfers, options);

Console.WriteLine($"Total transferred: {result.TotalBytesTransferred:N0} bytes");
Console.WriteLine($"Transfer rate: {result.AverageBandwidth:F2} MB/s");
Console.WriteLine($"Failed transfers: {result.FailedTransfers}");
```

### Memory Statistics and Monitoring

```csharp
using DotCompute.Memory;

// Get memory statistics
var stats = memoryManager.Statistics;

Console.WriteLine($"Total allocated: {stats.TotalBytesAllocated:N0} bytes");
Console.WriteLine($"Active allocations: {stats.ActiveAllocationCount}");
Console.WriteLine($"Peak usage: {stats.PeakMemoryUsage:N0} bytes");
Console.WriteLine($"Pool efficiency: {stats.PoolEfficiency:P2}");

// Get detailed buffer metrics
var bufferMetrics = buffer.GetPerformanceMetrics();
Console.WriteLine($"Transfer count: {bufferMetrics.TransferCount}");
Console.WriteLine($"Avg bandwidth: {bufferMetrics.AverageBandwidth:F2} MB/s");
Console.WriteLine($"Last accessed: {bufferMetrics.LastAccessTime}");
```

### Buffer Slicing

```csharp
using DotCompute.Memory;

// Create buffer
var buffer = await memoryManager.AllocateAsync<int>(1000);

// Create slice (view) without copying data
var slice = buffer.Slice(100, 200); // Elements 100-299

// Operate on slice
await kernel.ExecuteAsync(slice);

// Slices share underlying memory with parent buffer
```

### NUMA-Aware Allocation

```csharp
using DotCompute.Memory;

// Allocator with NUMA awareness
var allocator = new MemoryAllocator(logger)
{
    UseNumaAllocation = true,
    PreferredNumaNode = 0
};

// Allocate on specific NUMA node for optimal performance
var buffer = allocator.AllocateAligned<float>(1_000_000, alignment: 64);
```

## Architecture

### Memory Hierarchy

```
UnifiedMemoryManager (Top-level coordinator)
    ├── MemoryPool (Buffer pooling and reuse)
    ├── AdvancedMemoryTransferEngine (Transfer orchestration)
    ├── MemoryAllocator (Low-level allocation)
    └── Buffer Registry (Active buffer tracking)

Buffers:
    ├── OptimizedUnifiedBuffer<T> (General-purpose unified buffer)
    ├── UnifiedBufferView (Non-owning view of buffer data)
    └── UnifiedBufferSlice (Slice/window into buffer)
```

### Buffer Lifecycle

1. **Allocation**: Request buffer from memory manager
2. **Initialization**: Initialize host or device memory
3. **Data Transfer**: Move data between host and device
4. **Execution**: Use in kernel operations
5. **Synchronization**: Ensure consistency across devices
6. **Disposal**: Return to pool or free memory

### Pooling Strategy

The memory system uses tiered pooling:

1. **Thread-Local Pools**: Fast path for frequent allocations
2. **Global Pool**: Shared across threads with lock-free access
3. **Size-Based Buckets**: Categorize buffers by size for optimal reuse
4. **Automatic Eviction**: Remove unused buffers based on LRU policy

Target metrics:
- 90%+ reduction in allocation calls
- Sub-microsecond pool access latency
- 95%+ pool hit rate for common sizes

### Transfer Optimization

Memory transfers are optimized through:

1. **Pinned Memory**: Page-locked memory for DMA transfers
2. **Asynchronous Transfers**: Non-blocking operations
3. **Transfer Pipelining**: Overlap compute and data movement
4. **Batching**: Combine small transfers into larger operations
5. **Concurrent Streams**: Parallel transfers when possible

## Performance Benchmarks

Tested on RTX 2000 Ada with 16GB RAM:

| Operation | Standard | Optimized | Improvement |
|-----------|----------|-----------|-------------|
| Buffer Allocation | 45μs | 4μs | **11.2x** |
| Host-Device Copy (10MB) | 2.8ms | 2.4ms | **1.2x** |
| Device-Host Copy (10MB) | 2.9ms | 2.5ms | **1.2x** |
| Buffer Pool Hit | N/A | 120ns | **N/A** |
| Zero-Copy Access | 450ns | 45ns | **10x** |

Memory usage:
- **Standard allocation**: 100% allocation overhead
- **Pooled allocation**: 10% allocation overhead (90% reduction)

## System Requirements

- .NET 9.0 or later
- 4GB+ RAM recommended (8GB+ for large datasets)
- Native AOT compatible

### Optional Features
- **NUMA Support**: Requires NUMA-aware OS (Windows Server, Linux with NUMA)
- **Huge Pages**: Requires OS configuration (Linux: `hugetlbfs`, Windows: Large Pages privilege)
- **GPU Acceleration**: Requires compatible accelerator (CUDA, Metal, OpenCL)

## Configuration

### Memory Manager Options

Configure memory manager behavior:

```csharp
var options = new MemoryManagerOptions
{
    EnablePooling = true,
    MaxPoolSize = 1024 * 1024 * 1024, // 1GB
    MinPoolSize = 64 * 1024 * 1024,    // 64MB
    EnableAutoDefragmentation = true,
    DefragmentationInterval = TimeSpan.FromMinutes(5),
    EnableStatistics = true
};

var memoryManager = new UnifiedMemoryManager(accelerator, options, logger);
```

### Pool Configuration

```csharp
var poolConfig = new PoolConfiguration
{
    MaxPoolSize = 1000,
    MinPoolSize = 10,
    MaxObjectSize = 100 * 1024 * 1024, // 100MB
    EvictionPolicy = EvictionPolicy.LRU,
    MaintenanceInterval = TimeSpan.FromMinutes(1),
    EnableMetrics = true
};
```

## Troubleshooting

### Out of Memory Errors

1. **Check Statistics**: Review `memoryManager.Statistics` for usage patterns
2. **Enable Cleanup**: Ensure automatic cleanup is running
3. **Manual Cleanup**: Call `memoryManager.TrimExcessAsync()`
4. **Adjust Pool Size**: Reduce `MaxPoolSize` if memory constrained

### Performance Issues

1. **Pool Efficiency**: Check `PoolEfficiency` metric (target: 90%+)
2. **Transfer Bandwidth**: Monitor transfer statistics for bottlenecks
3. **Alignment**: Ensure buffers are properly aligned (64-byte for SIMD)
4. **Pinned Memory**: Enable pinned memory for large transfers

### Memory Leaks

1. **Dispose Buffers**: Always dispose buffers explicitly or use `using`
2. **Registry Check**: Review `_activeBuffers` count in memory manager
3. **Weak References**: Check `_bufferRegistry` for leaked references
4. **Enable Diagnostics**: Use `UnifiedBufferDiagnostics` for leak detection

## Advanced Topics

### Custom Buffer Types

Implement IUnifiedMemoryBuffer<T> for custom buffer behavior:

```csharp
public class CustomBuffer<T> : IUnifiedMemoryBuffer<T> where T : unmanaged
{
    public int Length { get; }
    public long SizeInBytes { get; }

    public async ValueTask CopyFromAsync(ReadOnlyMemory<T> source)
    {
        // Custom implementation
    }

    // Implement other interface members...
}
```

### Integration with External Libraries

```csharp
// Wrap external library buffers
var externalPointer = ExternalLibrary.AllocateBuffer(size);
var wrappedBuffer = MemoryAllocator.WrapUnmanagedPointer<float>(
    externalPointer,
    size,
    ownsMemory: false
);
```

### Cross-Process Memory Sharing

```csharp
// Create shared memory buffer
var sharedBuffer = await memoryManager.AllocateSharedAsync<float>(
    size,
    name: "SharedComputeBuffer"
);

// Other process can open the same buffer
var remoteBuffer = await memoryManager.OpenSharedAsync<float>(
    "SharedComputeBuffer"
);
```

## Dependencies

- **DotCompute.Abstractions**: Core abstractions
- **DotCompute.Core**: Core runtime components
- **System.IO.Pipelines**: High-performance I/O
- **Microsoft.Toolkit.HighPerformance**: Performance utilities
- **System.Runtime.InteropServices**: Platform invoke support

## Design Principles

1. **Zero-Copy First**: Minimize data copying through Span<T> and Memory<T>
2. **Pool Everything**: Reduce allocations through aggressive pooling
3. **Async by Default**: Non-blocking operations for scalability
4. **Monitor Everything**: Comprehensive statistics for diagnostics
5. **Fail Fast**: Immediate validation and error reporting
6. **Thread-Safe**: All operations safe for concurrent access

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[Memory Management Architecture](../../../docs/articles/architecture/memory-management.md)** - Unified buffer abstraction and pooling (90% allocation reduction)
- **[Backend Integration](../../../docs/articles/architecture/backend-integration.md)** - P2P memory transfer architecture

### Developer Guides
- **[Memory Management Guide](../../../docs/articles/guides/memory-management.md)** - Memory pooling best practices and zero-copy techniques
- **[Performance Tuning](../../../docs/articles/guides/performance-tuning.md)** - Memory optimization strategies (11.2x speedup)
- **[Multi-GPU Programming](../../../docs/articles/guides/multi-gpu.md)** - P2P transfers (12 GB/s measured)

### Examples
- **[Basic Vector Operations](../../../docs/articles/examples/basic-vector-operations.md)** - Buffer management examples
- **[Image Processing](../../../docs/articles/examples/image-processing.md)** - Memory-efficient image operations

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation
- **[UnifiedBuffer Documentation](../../../api/DotCompute.Memory.UnifiedBuffer.html)** - Buffer API reference

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)

## Contributing

Contributions are welcome, particularly in:
- Platform-specific memory optimizations
- Additional pooling strategies
- Transfer optimization techniques
- Memory usage profiling tools

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
