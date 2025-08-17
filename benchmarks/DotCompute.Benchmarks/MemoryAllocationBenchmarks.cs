using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class MemoryAllocationBenchmarks : IDisposable
{
    private DefaultAcceleratorManager? _acceleratorManager;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;

    [Params(1024, 1024 * 1024, 16 * 1024 * 1024)] // 1KB, 1MB, 16MB
    public int BufferSize { get; set; }

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
        if (_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
    }

    [Benchmark(Baseline = true)]
    public async Task<IMemoryBuffer> AllocateBuffer()
    {
        var buffer = await _memoryManager.AllocateAsync(BufferSize);
        await buffer.DisposeAsync();
        return buffer;
    }

    [Benchmark]
    public async Task<IMemoryBuffer> AllocateAndCopyBuffer()
    {
        var data = new byte[BufferSize];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        Random.Shared.NextBytes(data);
#pragma warning restore CA5394

        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
        await buffer.DisposeAsync();
        return buffer;
    }

    [Benchmark]
    public async Task AllocateMultipleBuffers()
    {
        const int bufferCount = 10;
        var buffers = new IMemoryBuffer[bufferCount];

        for (var i = 0; i < bufferCount; i++)
        {
            buffers[i] = await _memoryManager.AllocateAsync(BufferSize / bufferCount);
        }

        foreach (var buffer in buffers)
        {
            await buffer.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task AllocateCopyAndReadBack()
    {
        var data = new byte[BufferSize];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        Random.Shared.NextBytes(data);
#pragma warning restore CA5394

        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);

        var result = new byte[BufferSize];
        await buffer.CopyToHostAsync<byte>(result);

        await buffer.DisposeAsync();
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