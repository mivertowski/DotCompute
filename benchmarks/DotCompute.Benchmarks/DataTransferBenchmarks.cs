using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;

namespace DotCompute.Benchmarks;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
public class DataTransferBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private byte[] _hostData = null!;
    private float[] _floatData = null!;
    private IMemoryBuffer _deviceBuffer = null!;

    [Params(1024, 1024 * 1024, 16 * 1024 * 1024)] // 1KB, 1MB, 16MB
    public int DataSize { get; set; }

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
        
        // Prepare test data
        _hostData = new byte[DataSize];
        Random.Shared.NextBytes(_hostData);
        
        _floatData = new float[DataSize / sizeof(float)];
        for (int i = 0; i < _floatData.Length; i++)
        {
            _floatData[i] = Random.Shared.NextSingle() * 100;
        }
        
        _deviceBuffer = await _memoryManager.AllocateAsync(DataSize);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _deviceBuffer.DisposeAsync();
        await _acceleratorManager.DisposeAsync();
    }

    [Benchmark(Baseline = true)]
    public async Task HostToDevice_Bytes()
    {
        await _deviceBuffer.CopyFromHostAsync<byte>(_hostData);
    }

    [Benchmark]
    public async Task DeviceToHost_Bytes()
    {
        var result = new byte[DataSize];
        await _deviceBuffer.CopyToHostAsync<byte>(result);
    }

    [Benchmark]
    public async Task HostToDevice_Floats()
    {
        var buffer = await _memoryManager.AllocateAsync(_floatData.Length * sizeof(float));
        await buffer.CopyFromHostAsync<float>(_floatData);
        await buffer.DisposeAsync();
    }

    [Benchmark]
    public async Task DeviceToHost_Floats()
    {
        var buffer = await _memoryManager.AllocateAndCopyAsync<float>(_floatData);
        var result = new float[_floatData.Length];
        await buffer.CopyToHostAsync<float>(result);
        await buffer.DisposeAsync();
    }

    [Benchmark]
    public async Task BidirectionalTransfer()
    {
        // Host to Device
        await _deviceBuffer.CopyFromHostAsync<byte>(_hostData);
        
        // Process (simulated)
        await Task.Yield();
        
        // Device to Host
        var result = new byte[DataSize];
        await _deviceBuffer.CopyToHostAsync<byte>(result);
    }

    [Benchmark]
    public async Task MultipleSmallTransfers()
    {
        const int transferCount = 100;
        int chunkSize = DataSize / transferCount;
        
        for (int i = 0; i < transferCount; i++)
        {
            var chunk = _hostData.AsMemory(i * chunkSize, Math.Min(chunkSize, _hostData.Length - i * chunkSize));
            await _deviceBuffer.CopyFromHostAsync<byte>(chunk, i * chunkSize);
        }
    }

    [Benchmark]
    public async Task PinnedMemoryTransfer()
    {
        var pinnedData = GCHandle.Alloc(_hostData, GCHandleType.Pinned);
        try
        {
            await _deviceBuffer.CopyFromHostAsync<byte>(_hostData);
        }
        finally
        {
            pinnedData.Free();
        }
    }
}