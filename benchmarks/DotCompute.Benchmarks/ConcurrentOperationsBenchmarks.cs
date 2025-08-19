using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DotCompute.Abstractions;
using DotCompute.Core.Compute;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Benchmarks;


/// <summary>
/// Benchmarks for concurrent operations performance.
/// Tests thread safety, contention, and scaling characteristics under concurrent load.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RPlotExporter]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by BenchmarkDotNet framework")]
internal sealed class ConcurrentOperationsBenchmarks : IDisposable
{
    private DefaultAcceleratorManager? _acceleratorManager;
    private IAccelerator _accelerator = null!;
    private readonly ConcurrentBag<IMemoryBuffer> _buffers = [];
    // Removed unused _lockObject field

    [Params(1, 2, 4, 8, 16)]
    public int ConcurrentThreads { get; set; }

    [Params(1024, 16384, 65536, 262144)]
    public int DataSize { get; set; }

    [Params("MemoryOperations", "KernelExecution", "Mixed", "HighContention")]
    public string ConcurrencyType { get; set; } = "MemoryOperations";

    private float[][] _threadData = null!;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConcurrentQueue<float[]> _workQueue = new();

    [GlobalSetup]
    public async Task Setup()
    {
        var logger = new NullLogger<DefaultAcceleratorManager>();
        _acceleratorManager = new DefaultAcceleratorManager(logger);

        var cpuProvider = new CpuAcceleratorProvider(new NullLogger<CpuAcceleratorProvider>());
        _acceleratorManager.RegisterProvider(cpuProvider);
        await _acceleratorManager.InitializeAsync();

        _accelerator = _acceleratorManager.Default;

        SetupTestData();
    }

    private void SetupTestData()
    {
        _threadData = new float[ConcurrentThreads][];
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
        var random = new Random(42);

        for (var t = 0; t < ConcurrentThreads; t++)
        {
            _threadData[t] = new float[DataSize];
            for (var i = 0; i < DataSize; i++)
            {
                _threadData[t][i] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
        }
#pragma warning restore CA5394

        // Fill work queue for producer-consumer tests
        for (var i = 0; i < ConcurrentThreads * 10; i++)
        {
            var workItem = new float[DataSize / 10];
            for (var j = 0; j < workItem.Length; j++)
            {
#pragma warning disable CA5394 // Random is acceptable for benchmark data generation
                workItem[j] = (float)random.NextDouble();
#pragma warning restore CA5394
            }
            _workQueue.Enqueue(workItem);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        while (_buffers.TryTake(out var buffer))
        {
            if (!buffer.IsDisposed)
            {
                await buffer.DisposeAsync();
            }
        }

        if (_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
        _semaphore.Dispose();
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
