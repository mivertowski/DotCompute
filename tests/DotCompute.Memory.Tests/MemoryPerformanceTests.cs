using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotCompute.Memory;

namespace DotCompute.Memory.Tests;

/// <summary>
/// Performance benchmarks for the unified memory system.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class MemoryPerformanceBenchmarks
{
    private TestMemoryManager _memoryManager = null!;
    private UnifiedMemoryManager _unifiedManager = null!;
    private MemoryPool<float> _floatPool = null!;
    private MemoryAllocator _allocator = null!;
    
    [Params(1024, 16384, 262144)] // 4KB, 64KB, 1MB
    public int BufferSize { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        _memoryManager = new TestMemoryManager();
        _unifiedManager = new UnifiedMemoryManager(_memoryManager);
        _floatPool = _unifiedManager.GetPool<float>();
        _allocator = new MemoryAllocator();
    }
    
    [GlobalCleanup]
    public void Cleanup()
    {
        _unifiedManager?.Dispose();
        _memoryManager?.Dispose();
        _allocator?.Dispose();
    }
    
    [Benchmark]
    public async Task<UnifiedBuffer<float>> CreateUnifiedBuffer()
    {
        var buffer = await _unifiedManager.CreateUnifiedBufferAsync<float>(BufferSize / sizeof(float));
        buffer.Dispose();
        return buffer;
    }
    
    [Benchmark]
    public void RentReturnFromPool()
    {
        var buffer = _floatPool.Rent(BufferSize / sizeof(float));
        buffer.Dispose(); // Returns to pool
    }
    
    [Benchmark]
    public void HostToDeviceTransfer()
    {
        using var buffer = new UnifiedBuffer<float>(_memoryManager, BufferSize / sizeof(float));
        
        // Write to host
        var span = buffer.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = i;
        }
        
        // Transfer to device
        buffer.EnsureOnDevice();
    }
    
    [Benchmark]
    public void DeviceToHostTransfer()
    {
        using var buffer = new UnifiedBuffer<float>(_memoryManager, BufferSize / sizeof(float));
        
        // Ensure on device first
        buffer.EnsureOnDevice();
        buffer.MarkDeviceDirty();
        
        // Transfer to host
        buffer.EnsureOnHost();
    }
    
    [Benchmark]
    public void BidirectionalTransfer()
    {
        using var buffer = new UnifiedBuffer<float>(_memoryManager, BufferSize / sizeof(float));
        
        // Host to device
        var span = buffer.AsSpan();
        span[0] = 42.0f;
        buffer.EnsureOnDevice();
        
        // Device to host
        buffer.MarkDeviceDirty();
        buffer.EnsureOnHost();
        var result = buffer.AsReadOnlySpan()[0];
    }
    
    [Benchmark]
    public void AlignedAllocation()
    {
        using var aligned = _allocator.AllocateAligned<float>(BufferSize / sizeof(float), 64);
        var span = aligned.Memory.Span;
        span[0] = 1.0f;
    }
    
    [Benchmark]
    public void UnsafeCopyOperations()
    {
        var source = new float[BufferSize / sizeof(float)];
        var dest = new float[BufferSize / sizeof(float)];
        
        UnsafeMemoryOperations.CopyMemory<float>(source, dest);
    }
    
    [Benchmark]
    public void UnsafeFillOperations()
    {
        var buffer = new byte[BufferSize];
        UnsafeMemoryOperations.FillMemory<byte>(buffer, 0xFF);
    }
    
    [Benchmark]
    public async Task ConcurrentPoolAccess()
    {
        const int concurrency = 8;
        var tasks = new Task[concurrency];
        
        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                var buffer = _floatPool.Rent(BufferSize / sizeof(float) / concurrency);
                buffer.AsSpan()[0] = 1.0f;
                buffer.Dispose();
            });
        }
        
        await Task.WhenAll(tasks);
    }
    
    [Benchmark]
    public void MemoryPressureHandling()
    {
        // Allocate many buffers
        var buffers = new IMemoryBuffer<float>[10];
        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = _floatPool.Rent(BufferSize / sizeof(float) / 10);
        }
        
        // Return half
        for (int i = 0; i < 5; i++)
        {
            buffers[i].Dispose();
        }
        
        // Apply pressure
        _floatPool.HandleMemoryPressure(0.8);
        
        // Cleanup
        for (int i = 5; i < buffers.Length; i++)
        {
            buffers[i].Dispose();
        }
    }
}

/// <summary>
/// Additional performance tests that don't fit the benchmark pattern.
/// </summary>
public class MemoryPerformanceTests
{
    public static async Task RunThroughputTest()
    {
        Console.WriteLine("=== Memory System Throughput Test ===");
        
        using var memoryManager = new TestMemoryManager();
        using var unifiedManager = new UnifiedMemoryManager(memoryManager);
        
        var sizes = new[] { 1024, 16384, 262144, 1048576 }; // 1KB, 16KB, 256KB, 1MB
        var iterations = 1000;
        
        foreach (var size in sizes)
        {
            var sw = Stopwatch.StartNew();
            long totalBytes = 0;
            
            for (int i = 0; i < iterations; i++)
            {
                var buffer = await unifiedManager.CreateUnifiedBufferAsync<byte>(size);
                
                // Write
                var span = buffer.AsSpan();
                span[0] = 1;
                
                // Transfer to device
                buffer.EnsureOnDevice();
                
                // Transfer back
                buffer.MarkDeviceDirty();
                buffer.EnsureOnHost();
                
                buffer.Dispose();
                totalBytes += size * 2; // Count both directions
            }
            
            sw.Stop();
            var throughputMBps = (totalBytes / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
            
            Console.WriteLine($"Size: {size / 1024}KB - Throughput: {throughputMBps:F2} MB/s");
        }
    }
    
    public static async Task RunPoolEfficiencyTest()
    {
        Console.WriteLine("\n=== Memory Pool Efficiency Test ===");
        
        using var memoryManager = new TestMemoryManager();
        using var unifiedManager = new UnifiedMemoryManager(memoryManager);
        var pool = unifiedManager.GetPool<int>();
        
        const int warmupIterations = 100;
        const int testIterations = 10000;
        const int bufferSize = 1024;
        
        // Warmup
        for (int i = 0; i < warmupIterations; i++)
        {
            var buffer = pool.Rent(bufferSize);
            buffer.Dispose();
        }
        
        // Test with pool
        var swWithPool = Stopwatch.StartNew();
        for (int i = 0; i < testIterations; i++)
        {
            var buffer = pool.Rent(bufferSize);
            buffer.AsSpan()[0] = i;
            buffer.Dispose();
        }
        swWithPool.Stop();
        
        // Test without pool (direct allocation)
        var swWithoutPool = Stopwatch.StartNew();
        for (int i = 0; i < testIterations; i++)
        {
            var buffer = await unifiedManager.CreateUnifiedBufferAsync<int>(bufferSize);
            buffer.AsSpan()[0] = i;
            buffer.Dispose();
        }
        swWithoutPool.Stop();
        
        var poolStats = pool.GetStatistics();
        var efficiency = (double)poolStats.TotalReturnedBuffers / poolStats.TotalAllocatedBuffers;
        
        Console.WriteLine($"With Pool: {swWithPool.ElapsedMilliseconds}ms");
        Console.WriteLine($"Without Pool: {swWithoutPool.ElapsedMilliseconds}ms");
        Console.WriteLine($"Speedup: {(double)swWithoutPool.ElapsedMilliseconds / swWithPool.ElapsedMilliseconds:F2}x");
        Console.WriteLine($"Pool Reuse Efficiency: {efficiency:P}");
    }
    
    public static void RunAlignmentPerformanceTest()
    {
        Console.WriteLine("\n=== Memory Alignment Performance Test ===");
        
        using var allocator = new MemoryAllocator();
        const int iterations = 100000;
        const int size = 1024;
        
        // Test unaligned
        var swUnaligned = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var mem = allocator.Allocate<float>(size);
            mem.Memory.Span[0] = 1.0f;
        }
        swUnaligned.Stop();
        
        // Test 16-byte aligned
        var sw16 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var mem = allocator.AllocateAligned<float>(size, 16);
            mem.Memory.Span[0] = 1.0f;
        }
        sw16.Stop();
        
        // Test 32-byte aligned (AVX)
        var sw32 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var mem = allocator.AllocateAligned<float>(size, 32);
            mem.Memory.Span[0] = 1.0f;
        }
        sw32.Stop();
        
        // Test 64-byte aligned (cache line)
        var sw64 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            using var mem = allocator.AllocateAligned<float>(size, 64);
            mem.Memory.Span[0] = 1.0f;
        }
        sw64.Stop();
        
        Console.WriteLine($"Unaligned: {swUnaligned.ElapsedMilliseconds}ms");
        Console.WriteLine($"16-byte aligned: {sw16.ElapsedMilliseconds}ms");
        Console.WriteLine($"32-byte aligned: {sw32.ElapsedMilliseconds}ms");
        Console.WriteLine($"64-byte aligned: {sw64.ElapsedMilliseconds}ms");
    }
    
    public static async Task RunAllPerformanceTests()
    {
        await RunThroughputTest();
        await RunPoolEfficiencyTest();
        RunAlignmentPerformanceTest();
        
        // Run BenchmarkDotNet benchmarks if in Release mode
        #if RELEASE
        BenchmarkRunner.Run<MemoryPerformanceBenchmarks>();
        #endif
    }
}