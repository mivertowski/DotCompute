using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

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
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class MemoryOperationsBenchmarks : IDisposable
{
    private DefaultAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private readonly List<IMemoryBuffer> _buffers = [];

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
            {
                await buffer.DisposeAsync();
            }
        }
        _buffers.Clear();
        if (_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        foreach (var buffer in _buffers)
        {
            if (!buffer.IsDisposed)
            {
                await buffer.DisposeAsync();
            }
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

        for (var i = 0; i < ConcurrentBuffers; i++)
        {
            buffers[i] = _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers).AsTask();
        }

        var results = await Task.WhenAll(buffers);
        _buffers.AddRange(results);
    }

    [Benchmark]
    public async Task SequentialAllocationDeallocation()
    {
        for (var i = 0; i < ConcurrentBuffers; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(BufferSize / ConcurrentBuffers);
            await buffer.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task BufferWithDataAllocation()
    {
        var data = new byte[BufferSize];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        Random.Shared.NextBytes(data);
#pragma warning restore CA5394

        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task LargeBufferFragmentation()
    {
        var smallBuffers = new List<IMemoryBuffer>();

        // Allocate many small buffers to fragment memory
        for (var i = 0; i < 100; i++)
        {
            var buffer = await _memoryManager.AllocateAsync(1024);
            smallBuffers.Add(buffer);
        }

        // Free every other buffer
        for (var i = 1; i < smallBuffers.Count; i += 2)
        {
            await smallBuffers[i].DisposeAsync();
        }

        // Try to allocate a larger buffer in fragmented space
        var largeBuffer = await _memoryManager.AllocateAsync(BufferSize);
        _buffers.Add(largeBuffer);

        // Cleanup remaining small buffers
        for (var i = 0; i < smallBuffers.Count; i += 2)
        {
            if (!smallBuffers[i].IsDisposed)
            {
                await smallBuffers[i].DisposeAsync();
            }
        }
    }

    [Benchmark]
    public async Task MemoryPoolReuse()
    {
        var buffers = new List<IMemoryBuffer>();

        // Allocate buffers
        for (var i = 0; i < ConcurrentBuffers; i++)
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
        for (var i = 0; i < ConcurrentBuffers; i++)
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
        for (var i = 0; i < patternData.Length; i++)
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
        for (var i = 0; i < span.Length; i++)
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
        for (var i = 0; i < 8; i++)
        {
            var view = _memoryManager.CreateView(mainBuffer, i * viewSize, viewSize);
            views.Add(view);
        }

        _buffers.Add(mainBuffer);
        _buffers.AddRange(views);
    }

    public void Dispose()
    {
        try
        {
            Cleanup().GetAwaiter().GetResult();
            if (_accelerator != null)
            {
                var task = _accelerator.DisposeAsync();
                if (task.IsCompleted)
                {
                    task.GetAwaiter().GetResult();
                }
                else
                {
                    task.AsTask().Wait();
                }
            }
        }
        catch
        {
            // Ignore disposal errors
        }
        GC.SuppressFinalize(this);
    }
}
