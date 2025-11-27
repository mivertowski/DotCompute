# Memory Optimization

This module covers advanced memory management techniques to minimize allocation overhead and maximize throughput.

## Memory Allocation Overhead

GPU memory allocation is expensive:

| Operation | Typical Latency |
|-----------|-----------------|
| `malloc` (CPU) | 50-200 ns |
| GPU buffer allocation | 1-10 ms |
| GPU buffer deallocation | 0.5-5 ms |

For high-frequency operations, allocation overhead can dominate execution time.

## Memory Pooling

### Enabling the Memory Pool

Memory pooling is automatically enabled with `AddDotComputeRuntime()`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;

var host = Host.CreateApplicationBuilder(args);

// Memory pooling is enabled by default
host.Services.AddDotComputeRuntime();

// For advanced memory features, add memory management services
host.Services.AddAdvancedMemoryManagement();

var app = host.Build();
```

### Pool Behavior

```
First allocation:  Pool allocates from GPU memory
Buffer release:    Returns to pool (not freed)
Next allocation:   Reuses pooled buffer if size matches
Pool trim:         Frees unused buffers periodically
```

### Pool Statistics

```csharp
var poolStats = orchestrator.GetMemoryPoolStatistics();

Console.WriteLine($"Pool size: {poolStats.TotalPoolSize / (1024*1024)} MB");
Console.WriteLine($"Allocated: {poolStats.AllocatedBytes / (1024*1024)} MB");
Console.WriteLine($"Available: {poolStats.AvailableBytes / (1024*1024)} MB");
Console.WriteLine($"Hit rate: {poolStats.HitRate:P1}");
Console.WriteLine($"Allocations saved: {poolStats.PooledAllocations}");
```

### Performance Impact

Without pooling (100 iterations, 1MB buffers):
```
Total time: 2,450 ms
Per iteration: 24.5 ms
```

With pooling:
```
Total time: 125 ms (19.6x faster)
Per iteration: 1.25 ms
```

## Allocation Strategies

### Strategy 1: Pre-allocation

Allocate buffers at startup:

```csharp
public class ImageProcessor : IDisposable
{
    private readonly IBuffer<byte> _inputBuffer;
    private readonly IBuffer<byte> _outputBuffer;
    private readonly IBuffer<float> _tempBuffer;

    public ImageProcessor(IComputeOrchestrator orchestrator, int maxImageSize)
    {
        // Pre-allocate at construction
        _inputBuffer = orchestrator.CreateBuffer<byte>(maxImageSize);
        _outputBuffer = orchestrator.CreateBuffer<byte>(maxImageSize);
        _tempBuffer = orchestrator.CreateBuffer<float>(maxImageSize / 4);
    }

    public async Task ProcessAsync(byte[] image)
    {
        // Reuse pre-allocated buffers
        await _inputBuffer.CopyFromAsync(image.AsSpan(0, image.Length));
        // ... process ...
    }

    public void Dispose()
    {
        _inputBuffer.Dispose();
        _outputBuffer.Dispose();
        _tempBuffer.Dispose();
    }
}
```

### Strategy 2: Buffer Rental

For variable-size allocations:

```csharp
public class BufferRentalPool
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ConcurrentBag<IBuffer<float>>[] _pools;

    public BufferRentalPool(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        // Pools for different size classes: 1KB, 4KB, 16KB, 64KB, 256KB, 1MB
        _pools = new ConcurrentBag<IBuffer<float>>[6];
        for (int i = 0; i < _pools.Length; i++)
            _pools[i] = new ConcurrentBag<IBuffer<float>>();
    }

    public IBuffer<float> Rent(int minSize)
    {
        int sizeClass = GetSizeClass(minSize);
        int actualSize = GetActualSize(sizeClass);

        if (_pools[sizeClass].TryTake(out var buffer))
            return buffer;

        return _orchestrator.CreateBuffer<float>(actualSize);
    }

    public void Return(IBuffer<float> buffer)
    {
        int sizeClass = GetSizeClass(buffer.Length);
        _pools[sizeClass].Add(buffer);
    }
}
```

### Strategy 3: Unified Memory with Hints

```csharp
// Create unified buffer with access hints
using var buffer = orchestrator.CreateUnifiedBuffer<float>(size,
    new UnifiedMemoryOptions
    {
        PreferredLocation = MemoryLocation.Device,  // Keep on GPU
        AccessedBy = AccessPattern.GpuMostly,       // Optimize for GPU access
        ReadOnly = false
    });

// Provide migration hints
buffer.PrefetchToDevice();  // Migrate to GPU before kernel
await orchestrator.ExecuteKernelAsync(kernel, config, buffer);
buffer.PrefetchToHost();    // Migrate back for CPU access
```

## Transfer Optimization

### Asynchronous Transfers

```csharp
// Overlap transfer with computation
var transferTask = inputBuffer.CopyFromAsync(nextBatchData);
await orchestrator.ExecuteKernelAsync(kernel, config, currentBuffer);
await transferTask; // Ensure transfer complete before next iteration
```

### Pinned Memory Transfers

```csharp
// Regular transfer
var sw = Stopwatch.StartNew();
await gpuBuffer.CopyFromAsync(hostArray);
Console.WriteLine($"Regular: {sw.ElapsedMilliseconds} ms");

// Pinned memory transfer
using var pinnedBuffer = orchestrator.CreatePinnedBuffer<float>(size);
hostArray.AsSpan().CopyTo(pinnedBuffer.AsSpan());

sw.Restart();
await gpuBuffer.CopyFromPinnedAsync(pinnedBuffer);
Console.WriteLine($"Pinned: {sw.ElapsedMilliseconds} ms");
```

Typical improvement: 1.5-3x faster transfers.

### Compression for Large Transfers

For sparse or compressible data:

```csharp
// Compress before transfer
var compressed = CompressData(originalData);

// Transfer compressed data
using var compressedBuffer = orchestrator.CreateBuffer<byte>(compressed.Length);
await compressedBuffer.CopyFromAsync(compressed);

// Decompress on GPU (custom kernel)
using var decompressedBuffer = orchestrator.CreateBuffer<float>(originalSize);
await orchestrator.ExecuteKernelAsync(
    DecompressKernel, config, compressedBuffer, decompressedBuffer);
```

## Memory Layout Optimization

### Structure of Arrays (SoA) vs Array of Structures (AoS)

```csharp
// AoS: Poor GPU memory access pattern
struct ParticleAoS
{
    public float X, Y, Z;
    public float VelX, VelY, VelZ;
}
ParticleAoS[] particles = new ParticleAoS[count];

// SoA: Better GPU memory coalescing
struct ParticlesSoA
{
    public float[] X, Y, Z;
    public float[] VelX, VelY, VelZ;
}

[Kernel]
public static void UpdateParticlesSoA(
    Span<float> x, Span<float> y, Span<float> z,
    ReadOnlySpan<float> velX, ReadOnlySpan<float> velY, ReadOnlySpan<float> velZ,
    float dt)
{
    int idx = Kernel.ThreadId.X;
    if (idx < x.Length)
    {
        // Coalesced access: adjacent threads read adjacent memory
        x[idx] += velX[idx] * dt;
        y[idx] += velY[idx] * dt;
        z[idx] += velZ[idx] * dt;
    }
}
```

### Padding for Alignment

```csharp
// Avoid: Misaligned structure
struct BadLayout // 13 bytes, poor alignment
{
    public float Value;     // 4 bytes
    public byte Flag;       // 1 byte
    public double Result;   // 8 bytes
}

// Better: Aligned structure
struct GoodLayout // 16 bytes, aligned
{
    public float Value;     // 4 bytes
    public float Padding;   // 4 bytes (explicit padding)
    public double Result;   // 8 bytes
}
```

## Monitoring Memory Usage

### Real-time Monitoring

```csharp
public class MemoryMonitor
{
    private readonly IComputeOrchestrator _orchestrator;
    private Timer _timer;

    public void StartMonitoring(TimeSpan interval)
    {
        _timer = new Timer(_ =>
        {
            var backend = _orchestrator.ActiveBackend;
            var free = backend.GetFreeMemory();
            var total = backend.GetTotalMemory();
            var used = total - free;

            Console.WriteLine($"GPU Memory: {used/(1024*1024)} / {total/(1024*1024)} MB ({used*100.0/total:F1}%)");

            if (used > total * 0.9)
            {
                Console.WriteLine("WARNING: GPU memory usage exceeds 90%");
            }
        }, null, TimeSpan.Zero, interval);
    }
}
```

### Memory Leak Detection

```csharp
#if DEBUG
public static class MemoryLeakDetector
{
    private static long _allocations;
    private static long _deallocations;

    public static void TrackAllocation() => Interlocked.Increment(ref _allocations);
    public static void TrackDeallocation() => Interlocked.Increment(ref _deallocations);

    public static void Report()
    {
        var leaked = _allocations - _deallocations;
        if (leaked > 0)
        {
            Console.WriteLine($"LEAK DETECTED: {leaked} buffers not disposed");
        }
    }
}
#endif
```

## Exercises

### Exercise 1: Pool Configuration

Experiment with different pool sizes and measure hit rates for your workload.

### Exercise 2: Transfer Benchmark

Compare regular, pinned, and asynchronous transfer performance.

### Exercise 3: SoA Conversion

Convert an AoS particle system to SoA and measure the performance improvement.

## Key Takeaways

1. **GPU allocation is expensive** - pool or pre-allocate buffers
2. **Pinned memory accelerates transfers** by enabling DMA
3. **SoA layout improves coalescing** for GPU memory access
4. **Monitor memory usage** to catch leaks and pressure
5. **Overlap transfers with computation** for maximum throughput

## Next Module

[Kernel Performance →](kernel-performance.md)

Learn to profile and optimize kernel execution.
