using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Benchmarks;

/// <summary>
/// Enhanced benchmarks for data transfer throughput between host and device.
/// Includes P2P transfers, streaming, and various data patterns.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
internal class EnhancedDataTransferBenchmarks
{
    private IAcceleratorManager _acceleratorManager = null!;
    private IAccelerator _accelerator = null!;
    private IMemoryManager _memoryManager = null!;
    private readonly List<IMemoryBuffer> _buffers = [];

    [Params(1024, 64 * 1024, 1024 * 1024, 16 * 1024 * 1024, 128 * 1024 * 1024)]
    public int DataSize { get; set; }

    [Params("Sequential", "Random", "Zeros", "Pattern")]
    public string DataPattern { get; set; } = "Sequential";

    private byte[] _hostData = null!;
    private byte[] _hostResult = null!;

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

        SetupTestData();
    }

    private void SetupTestData()
    {
        _hostData = new byte[DataSize];
        _hostResult = new byte[DataSize];

        switch (DataPattern)
        {
            case "Sequential":
                for (var i = 0; i < DataSize; i++)
                {
                    _hostData[i] = (byte)(i % 256);
                }

                break;
            case "Random":
                Random.Shared.NextBytes(_hostData);
                break;
            case "Zeros":
                Array.Fill(_hostData, (byte)0);
                break;
            case "Pattern":
                var pattern = new byte[] { 0xAA, 0x55, 0xFF, 0x00 };
                for (var i = 0; i < DataSize; i++)
                {
                    _hostData[i] = pattern[i % pattern.Length];
                }

                break;
        }
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
        await _acceleratorManager.DisposeAsync();
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
    public async Task HostToDeviceTransfer()
    {
        var buffer = await _memoryManager.AllocateAsync(DataSize);
        await buffer.CopyFromHostAsync<byte>(_hostData);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task DeviceToHostTransfer()
    {
        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(_hostData);
        await buffer.CopyToHostAsync<byte>(_hostResult);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task BidirectionalTransfer()
    {
        var buffer = await _memoryManager.AllocateAsync(DataSize);

        // Host to device
        await buffer.CopyFromHostAsync<byte>(_hostData);

        // Device to host
        await buffer.CopyToHostAsync<byte>(_hostResult);

        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task StreamedTransfer()
    {
        const int chunkSize = 64 * 1024; // 64KB chunks
        var buffer = await _memoryManager.AllocateAsync(DataSize);

        var tasks = new List<Task>();
        for (var offset = 0; offset < DataSize; offset += chunkSize)
        {
            var currentChunkSize = Math.Min(chunkSize, DataSize - offset);
            var chunk = _hostData.AsMemory(offset, currentChunkSize);

            tasks.Add(buffer.CopyFromHostAsync<byte>(chunk, offset).AsTask());
        }

        await Task.WhenAll(tasks);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task PinnedMemoryTransfer()
    {
        // Simulate pinned memory transfer (actual implementation would use pinned arrays)
        var pinnedData = GC.AllocateUninitializedArray<byte>(DataSize, pinned: true);
        _hostData.CopyTo(pinnedData, 0);

        var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(pinnedData);
        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task MultipleBufferTransfer()
    {
        const int bufferCount = 4;
        var bufferSize = DataSize / bufferCount;
        var tasks = new List<Task<IMemoryBuffer>>();

        for (var i = 0; i < bufferCount; i++)
        {
            var chunk = _hostData.AsMemory(i * bufferSize, bufferSize);
            tasks.Add(_memoryManager.AllocateAndCopyAsync<byte>(chunk).AsTask());
        }

        var results = await Task.WhenAll(tasks);
        _buffers.AddRange(results);
    }

    [Benchmark]
    public async Task AsyncTransferOverlap()
    {
        var buffer1 = await _memoryManager.AllocateAsync(DataSize / 2);
        var buffer2 = await _memoryManager.AllocateAsync(DataSize / 2);

        var data1 = _hostData.AsMemory(0, DataSize / 2);
        var data2 = _hostData.AsMemory(DataSize / 2, DataSize / 2);

        // Overlap transfers
        var task1 = buffer1.CopyFromHostAsync<byte>(data1);
        var task2 = buffer2.CopyFromHostAsync<byte>(data2);

        await Task.WhenAll(task1.AsTask(), task2.AsTask());

        _buffers.Add(buffer1);
        _buffers.Add(buffer2);
    }

    [Benchmark]
    public async Task TransferWithOffset()
    {
        var buffer = await _memoryManager.AllocateAsync(DataSize + 1024); // Extra space
        const int offset = 512;

        var dataToTransfer = _hostData.AsMemory(0, DataSize - offset);
        await buffer.CopyFromHostAsync<byte>(dataToTransfer, offset);

        _buffers.Add(buffer);
    }

    [Benchmark]
    public async Task P2PDeviceToDeviceTransfer()
    {
        // Simulate P2P transfer by creating two buffers and copying between them
        var sourceBuffer = await _memoryManager.AllocateAndCopyAsync<byte>(_hostData);
        var destBuffer = await _memoryManager.AllocateAsync(DataSize);

        // Read from source and write to destination (simulating P2P)
        await sourceBuffer.CopyToHostAsync<byte>(_hostResult);
        await destBuffer.CopyFromHostAsync<byte>(_hostResult);

        _buffers.Add(sourceBuffer);
        _buffers.Add(destBuffer);
    }

    [Benchmark]
    public double ThroughputCalculation()
    {
        // Calculate theoretical throughput
        var transferTime = 1.0; // Simulated 1 second
        var bytesTransferred = DataSize * 2; // Bidirectional

        return (bytesTransferred / transferTime) / (1024.0 * 1024.0); // MB/s
    }

    [Benchmark]
    public async Task MemoryBandwidthSaturation()
    {
        // Create multiple large buffers to test memory bandwidth limits
        var bufferCount = Environment.ProcessorCount;
        var bufferSize = DataSize / bufferCount;
        var tasks = new List<Task>();

        for (var i = 0; i < bufferCount; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var chunk = new byte[bufferSize];
                Random.Shared.NextBytes(chunk);
                var buffer = await _memoryManager.AllocateAndCopyAsync<byte>(chunk);
                lock (_buffers)
                {
                    _buffers.Add(buffer);
                }
            }));
        }

        await Task.WhenAll(tasks);
    }
}