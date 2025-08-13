using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace DotCompute.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for memory allocation and deallocation performance.
/// Tests various allocation patterns, buffer sizes, and memory management scenarios.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class MemoryOperationsBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private readonly List<IMemoryBuffer> _buffers = new();

    [Params(1024, 64 * 1024, 1024 * 1024, 16 * 1024 * 1024, 64 * 1024 * 1024)] 
    public int BufferSize { get; set; }

    [Params(1, 4, 16, 64)]
    public int ConcurrentBuffers { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);
        
        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();
        
        _accelerator = _acceleratorManager.Default;
        _memoryManager = _accelerator.Memory;
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
                await buffer.DisposeAsync();
        }
        _buffers.Clear();
        await _acceleratorManager.DisposeAsync();
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
                await buffer.DisposeAsync();
        }
        _buffers.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task SingleBufferAllocation()
    {
        var buffer = await _memoryManager.AllocateAsync(BufferSize);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task MultipleBufferAllocations()
    {
        var buffers = new Task<IMemoryBuffer>[ConcurrentBuffers];
        
        for (int i = 0; i < ConcurrentBuffers; i++)
        {
            buffers[i] = _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers).AsTask();
        }
        
        var results = await Task.WhenAll(buffers);
        _buffers.AddRange(results);
    }

    [Benchmark]
    public async Task SequentialAllocationDeallocation()
    {
        for (int i = 0; i < ConcurrentBuffers; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers);
            await buffer.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task BufferWithDataAllocation()
    {
        var data = new byte[BufferSize];
        Random.Shared.NextBytes(data);
        
        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task LargeBufferFragmentation()
    {
        var smallBuffers = new List<IMemoryBuffer>();
        
        // Allocate many small buffers to fragment memory
        for (int i = 0; i < 100; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(1024);
            smallBuffers.Add(buffer);
        }
        
        // Free every other buffer
        for (int i = 1; i < smallBuffers.Count; i += 2)
        {
            await smallBuffers[i].DisposeAsync();
        }
        
        // Try to allocate a larger buffer in fragmented space
        var largeBuffer = await _memoryManager.AllocateAsync(BufferSize);
        _buffers.Add(largeBuffer);
        
        // Cleanup remaining small buffers
        for (int i = 0; i < smallBuffers.Count; i += 2)
        {
            if (!smallBuffers[i].IsDisposed)
                await smallBuffers[i].DisposeAsync();
        }
    }

    [Benchmark]
    public async Task MemoryPoolReuse()
    {
        var buffers = new List<IMemoryBuffer>();
        
        // Allocate buffers
        for (int i = 0; i < ConcurrentBuffers; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers);
            buffers.Add(buffer);
        }
        
        // Free all buffers (should return to pool)
        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
        
        // Reallocate (should reuse from pool)
        for (int i = 0; i < ConcurrentBuffers; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers);
            _buffers.Add(buffer);
        }
    }

    [Benchmark]
    public async Task ZeroInitialization()
    {
        var buffer = await _memoryManager.AllocateAsync(BufferSize);
        var zeroData = new byte[BufferSize]; // Already zero-initialized
        await buffer.CopyFromHostAsync<byte>(zeroData);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task PatternInitialization()
    {
        var buffer = await _memoryManager.AllocateAsync(BufferSize);
        var patternData = new byte[BufferSize];
        
        // Initialize with pattern
        for (int i = 0; i < patternData.Length; i++)
        {
            patternData[i] = (byte)(i % 256);
        }
        
        await buffer.CopyFromHostAsync<byte>(patternData);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public unsafe void UnsafeMemoryOperations()
    {
        // Simulate unsafe memory operations for comparison
        var ptr = Marshal.AllocHGlobal(BufferSize);
        var span = new Span<byte>(ptr.ToPointer(), BufferSize);
        
        // Fill with pattern
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = (byte)(i % 256);
        }
        
        Marshal.FreeHGlobal(ptr);
    }

    [Benchmark]
    public async Task BufferViewCreation()
    {
        var mainBuffer = await _memoryManager.AllocateAsync(BufferSize);
        var views = new List<IMemoryBuffer>();
        
        var viewSize = BufferSize / 8;
        for (int i = 0; i < 8; i++)
        {
            var view = _memoryManager.CreateView(mainBuffer, i * viewSize, viewSize);
            views.Add(view);
        }
        
        _buffers.Add(mainBuffer);
        _buffers.AddRange(views);
    }
}