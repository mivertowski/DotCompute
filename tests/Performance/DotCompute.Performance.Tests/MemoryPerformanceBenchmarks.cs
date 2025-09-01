using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Performance.Tests;

/// <summary>
/// Performance benchmarks for memory allocation and transfer operations.
/// Tests various scenarios including host-device transfers, buffer allocations, and memory patterns.
/// </summary>
[Trait("Category", "Performance")]
public class MemoryPerformanceBenchmarks : PerformanceBenchmarkBase, IDisposable
{
    private readonly ITestOutputHelper _output;
    private IAccelerator _accelerator;
    private IUnifiedMemoryBuffer<float> _deviceBuffer;
    private IUnifiedMemoryBuffer<int> _intBuffer;
    private IUnifiedMemoryBuffer<double> _doubleBuffer;
    private float[] _hostData;
    private int[] _intHostData;
    private double[] _doubleHostData;
    
    [Params(1024, 4096, 16384, 65536, 262144, 1048576, 4194304)]
    public int BufferSize { get; set; }
    
    [Params(MemoryLocation.Device, MemoryLocation.Host, MemoryLocation.Unified)]
    public MemoryLocation Location { get; set; }
    
    [Params(typeof(float), typeof(int), typeof(double))]
    public Type ElementType { get; set; }
    
    public MemoryPerformanceBenchmarks(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _accelerator = AcceleratorFactory.CreateBest();
        await _accelerator.InitializeAsync();
        
        // Initialize host data arrays
        _hostData = new float[BufferSize];
        _intHostData = new int[BufferSize];
        _doubleHostData = new double[BufferSize];
        
        var random = new Random(42);
        for (var i = 0; i < BufferSize; i++)
        {
            _hostData[i] = (float)random.NextDouble();
            _intHostData[i] = random.Next();
            _doubleHostData[i] = random.NextDouble();
        }
        
        // Pre-allocate buffers for certain tests
        _deviceBuffer = _accelerator.AllocateBuffer<float>(BufferSize);
        _intBuffer = _accelerator.AllocateBuffer<int>(BufferSize);
        _doubleBuffer = _accelerator.AllocateBuffer<double>(BufferSize);
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkBufferAllocation()
    {
        DataSize = GetElementSize() * BufferSize;
        var metrics = await RunBufferAllocationBenchmarkAsync();
        LogMetrics(metrics, $"Buffer Allocation ({ElementType.Name})");

        // Memory allocation should be fast

        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 100.0,
            maxStandardDeviationMs: 50.0);
        
        _output.WriteLine($"Buffer Allocation Performance ({ElementType.Name}): {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunBufferAllocationBenchmarkAsync()
    {
        await WarmupBufferAllocation();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureBufferAllocationAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupBufferAllocation()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteBufferAllocationAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureBufferAllocationAsync()
    {
        _stopwatch.Restart();
        await ExecuteBufferAllocationAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }


    protected override async Task ExecuteOperationAsync() => await ExecuteBufferAllocationAsync();


    private async Task ExecuteBufferAllocationAsync()
    {
        IDisposable buffer = ElementType.Name switch
        {
            "Single" => _accelerator.AllocateBuffer<float>(BufferSize, Location),
            "Int32" => _accelerator.AllocateBuffer<int>(BufferSize, Location),
            "Double" => _accelerator.AllocateBuffer<double>(BufferSize, Location),
            _ => throw new ArgumentException($"Unsupported element type: {ElementType.Name}")
        };
        
        // Simulate some usage
        await Task.Delay(1);
        buffer.Dispose();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkHostToDeviceTransfer()
    {
        DataSize = GetElementSize() * BufferSize;
        var metrics = await RunHostToDeviceTransferBenchmarkAsync();
        LogMetrics(metrics, $"Host-to-Device Transfer ({ElementType.Name})");
        
        // Calculate expected minimum throughput (should achieve at least PCIe Gen3 x16 speeds)
        var expectedMinThroughput = 1000.0; // MB/s
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 1000.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Host-to-Device Transfer Performance ({ElementType.Name}): {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunHostToDeviceTransferBenchmarkAsync()
    {
        await WarmupHostToDeviceTransfer();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureHostToDeviceTransferAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupHostToDeviceTransfer()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteHostToDeviceTransferAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureHostToDeviceTransferAsync()
    {
        _stopwatch.Restart();
        await ExecuteHostToDeviceTransferAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteHostToDeviceTransferAsync()
    {
        switch (ElementType.Name)
        {
            case "Single":
                await _deviceBuffer.WriteAsync(_hostData);
                break;
            case "Int32":
                await _intBuffer.WriteAsync(_intHostData);
                break;
            case "Double":
                await _doubleBuffer.WriteAsync(_doubleHostData);
                break;
        }
        await _accelerator.SynchronizeAsync();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkDeviceToHostTransfer()
    {
        DataSize = GetElementSize() * BufferSize;
        var metrics = await RunDeviceToHostTransferBenchmarkAsync();
        LogMetrics(metrics, $"Device-to-Host Transfer ({ElementType.Name})");
        
        var expectedMinThroughput = 1000.0; // MB/s
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 1000.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Device-to-Host Transfer Performance ({ElementType.Name}): {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunDeviceToHostTransferBenchmarkAsync()
    {
        // First ensure device buffers have data
        await _deviceBuffer.WriteAsync(_hostData);
        await _intBuffer.WriteAsync(_intHostData);
        await _doubleBuffer.WriteAsync(_doubleHostData);
        
        await WarmupDeviceToHostTransfer();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureDeviceToHostTransferAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupDeviceToHostTransfer()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteDeviceToHostTransferAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureDeviceToHostTransferAsync()
    {
        _stopwatch.Restart();
        await ExecuteDeviceToHostTransferAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteDeviceToHostTransferAsync()
    {
        switch (ElementType.Name)
        {
            case "Single":
                var floatResult = await _deviceBuffer.ReadAsync();
                break;
            case "Int32":
                var intResult = await _intBuffer.ReadAsync();
                break;
            case "Double":
                var doubleResult = await _doubleBuffer.ReadAsync();
                break;
        }
        await _accelerator.SynchronizeAsync();
    }
    
    [Benchmark]
    [Fact]
    public async Task BenchmarkBidirectionalTransfer()
    {
        DataSize = GetElementSize() * BufferSize * 2; // Both directions
        var metrics = await RunBidirectionalTransferBenchmarkAsync();
        LogMetrics(metrics, $"Bidirectional Transfer ({ElementType.Name})");
        
        var expectedMinThroughput = 800.0; // Slightly lower due to bidirectional overhead
        AssertPerformanceExpectations(metrics,
            maxAverageTimeMs: 2000.0,
            minThroughputMBps: expectedMinThroughput);
        
        _output.WriteLine($"Bidirectional Transfer Performance ({ElementType.Name}): {metrics}");
    }
    
    private async Task<PerformanceMetrics> RunBidirectionalTransferBenchmarkAsync()
    {
        await WarmupBidirectionalTransfer();
        
        _measurements.Clear();
        var memoryBefore = GC.GetTotalMemory(true);
        
        for (var i = 0; i < MeasurementIterations; i++)
        {
            var time = await MeasureBidirectionalTransferAsync();
            _measurements.Add(time);
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        return CalculateMetrics(memoryAfter - memoryBefore);
    }
    
    private async Task WarmupBidirectionalTransfer()
    {
        for (var i = 0; i < WarmupIterations; i++)
        {
            await ExecuteBidirectionalTransferAsync();
        }
        GC.Collect();
    }
    
    private async Task<double> MeasureBidirectionalTransferAsync()
    {
        _stopwatch.Restart();
        await ExecuteBidirectionalTransferAsync();
        _stopwatch.Stop();
        return _stopwatch.Elapsed.TotalMilliseconds;
    }
    
    private async Task ExecuteBidirectionalTransferAsync()
    {
        // Host to device
        await ExecuteHostToDeviceTransferAsync();
        
        // Device to host
        await ExecuteDeviceToHostTransferAsync();
    }
    
    [Fact]
    public async Task BenchmarkMemoryBandwidth()
    {
        var sizes = new[] { 1024, 4096, 16384, 65536, 262144, 1048576, 4194304 };
        var bandwidths = new double[sizes.Length];
        
        for (var i = 0; i < sizes.Length; i++)
        {
            BufferSize = sizes[i];
            DataSize = sizeof(float) * BufferSize;
            
            // Re-initialize buffers for new size
            _deviceBuffer?.Dispose();
            _deviceBuffer = _accelerator.AllocateBuffer<float>(BufferSize);
            _hostData = new float[BufferSize];
            
            var metrics = await RunHostToDeviceTransferBenchmarkAsync();
            bandwidths[i] = metrics.Throughput;
        }
        
        // Find peak bandwidth
        var peakBandwidth = 0.0;
        var peakSize = 0;
        for (var i = 0; i < bandwidths.Length; i++)
        {
            if (bandwidths[i] > peakBandwidth)
            {
                peakBandwidth = bandwidths[i];
                peakSize = sizes[i];
            }
        }
        
        _output.WriteLine($"Peak Memory Bandwidth: {peakBandwidth:F2} MB/s at size {peakSize} elements");
        
        // Assert that we achieve reasonable bandwidth
        Assert.True(peakBandwidth > 500.0, 
            $"Peak bandwidth {peakBandwidth:F2} MB/s is below expected minimum of 500 MB/s");
    }
    
    [Fact]
    public async Task BenchmarkMemoryAccessPatterns()
    {
        var sequentialMetrics = await BenchmarkSequentialAccess();
        var randomMetrics = await BenchmarkRandomAccess();
        var stridedMetrics = await BenchmarkStridedAccess();
        
        LogMetrics(sequentialMetrics, "Sequential Memory Access");
        LogMetrics(randomMetrics, "Random Memory Access");
        LogMetrics(stridedMetrics, "Strided Memory Access");
        
        // Sequential should be fastest, random should be slowest
        Assert.True(sequentialMetrics.Throughput > randomMetrics.Throughput,
            "Sequential access should be faster than random access");
        Assert.True(stridedMetrics.Throughput > randomMetrics.Throughput,
            "Strided access should be faster than random access");
        
        _output.WriteLine($"Memory Access Pattern Performance Comparison:");
        _output.WriteLine($"  Sequential: {sequentialMetrics.Throughput:F2} MB/s");
        _output.WriteLine($"  Strided:    {stridedMetrics.Throughput:F2} MB/s");
        _output.WriteLine($"  Random:     {randomMetrics.Throughput:F2} MB/s");
    }
    
    private async Task<PerformanceMetrics> BenchmarkSequentialAccess()
    {
        DataSize = sizeof(float) * BufferSize;
        return await RunHostToDeviceTransferBenchmarkAsync();
    }
    
    private async Task<PerformanceMetrics> BenchmarkRandomAccess()
    {
        // Create randomly ordered data
        var random = new Random(42);
        var randomData = new float[BufferSize];
        var indices = new int[BufferSize];
        for (var i = 0; i < BufferSize; i++) indices[i] = i;
        
        // Shuffle indices
        for (var i = BufferSize - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        
        // Reorder data randomly
        for (var i = 0; i < BufferSize; i++)
        {
            randomData[i] = _hostData[indices[i]];
        }
        
        DataSize = sizeof(float) * BufferSize;
        _hostData = randomData;
        
        var metrics = await RunHostToDeviceTransferBenchmarkAsync();
        
        // Restore original data
        var originalRandom = new Random(42);
        for (var i = 0; i < BufferSize; i++)
        {
            _hostData[i] = (float)originalRandom.NextDouble();
        }
        
        return metrics;
    }
    
    private async Task<PerformanceMetrics> BenchmarkStridedAccess()
    {
        // Create strided access pattern (every 4th element)
        var stride = 4;
        var stridedData = new float[BufferSize];
        for (var i = 0; i < BufferSize; i++)
        {
            stridedData[i] = _hostData[(i * stride) % BufferSize];
        }
        
        DataSize = sizeof(float) * BufferSize;
        _hostData = stridedData;
        
        var metrics = await RunHostToDeviceTransferBenchmarkAsync();
        
        // Restore original data
        var random = new Random(42);
        for (var i = 0; i < BufferSize; i++)
        {
            _hostData[i] = (float)random.NextDouble();
        }
        
        return metrics;
    }
    
    private int GetElementSize()
    {
        return ElementType.Name switch
        {
            "Single" => sizeof(float),
            "Int32" => sizeof(int),
            "Double" => sizeof(double),
            _ => sizeof(float)
        };
    }


    [GlobalCleanup]
    public override void Cleanup() => base.Cleanup();


    public void Dispose()
    {
        _deviceBuffer?.Dispose();
        _intBuffer?.Dispose();
        _doubleBuffer?.Dispose();
        _accelerator?.Dispose();
    }
    
    private PerformanceMetrics CalculateMetrics(long allocations)
    {
        _measurements.Sort();
        
        var count = _measurements.Count;
        var sum = 0.0;
        foreach (var measurement in _measurements)
        {
            sum += measurement;
        }
        
        var average = sum / count;
        var median = count % 2 == 0 
            ? (_measurements[count / 2 - 1] + _measurements[count / 2]) / 2.0
            : _measurements[count / 2];
        
        var variance = 0.0;
        foreach (var measurement in _measurements)
        {
            variance += Math.Pow(measurement - average, 2);
        }
        var stdDev = Math.Sqrt(variance / count);
        
        var p95Index = (int)Math.Ceiling(0.95 * count) - 1;
        var p99Index = (int)Math.Ceiling(0.99 * count) - 1;
        
        var throughput = DataSize > 0 
            ? (DataSize / (1024.0 * 1024.0)) / (average / 1000.0)
            : 1000.0 / average;
        
        return new PerformanceMetrics
        {
            MinTime = _measurements[0],
            MaxTime = _measurements[count - 1],
            AverageTime = average,
            MedianTime = median,
            StandardDeviation = stdDev,
            P95Time = _measurements[p95Index],
            P99Time = _measurements[p99Index],
            Throughput = throughput,
            TotalAllocations = allocations,
            PeakMemoryUsage = GC.GetTotalMemory(false)
        };
    }
}