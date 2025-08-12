using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using DotCompute.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
public class MemoryAllocationBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
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
        await _acceleratorManager.DisposeAsync();
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
        Random.Shared.NextBytes(data);
        
        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
        await buffer.DisposeAsync();
        return buffer;
    }

    [Benchmark]
    public async Task AllocateMultipleBuffers()
    {
        const int bufferCount = 10;
        var buffers = new IMemoryBuffer[bufferCount];
        
        for (int i = 0; i < bufferCount; i++)
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
        Random.Shared.NextBytes(data);
        
        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(data);
        
        var result = new byte[BufferSize];
        await buffer.CopyToHostAsync<byte>(result);
        
        await buffer.DisposeAsync();
    }
}