// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Interfaces;
using DotCompute.Linq.Pipelines.Integration;
using DotCompute.Linq.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for reactive streaming performance in DotCompute LINQ system.
/// Tests throughput, latency, backpressure handling, and batch processing optimization.
/// </summary>
[Config(typeof(StreamingComputeConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class StreamingComputeBenchmark
{
    private IComputeLinqProvider _provider = null!;
    private IPipelineOrchestrationService _pipelineService = null!;
    private IAccelerator _gpuAccelerator = null!;
    private IAccelerator _cpuAccelerator = null!;
    
    // Streaming data sources
    private Subject<float> _floatStream = null!;
    private Subject<int> _intStream = null!;
    private Subject<StreamingData> _complexStream = null!;
    
    // Channel-based streaming
    private Channel<float> _floatChannel = null!;
    private Channel<int> _intChannel = null!;
    
    // Test data generators
    private Random _random = null!;
    private CancellationTokenSource _cancellationTokenSource = null!;
    
    // Performance counters
    private long _processedItems;
    private long _totalLatency;
    private readonly ConcurrentQueue<TimeSpan> _latencyMeasurements = new();
    
    [Params(1000, 10000, 100000)]
    public int StreamSize { get; set; }
    
    [Params(32, 128, 512, 2048)]
    public int BatchSize { get; set; }
    
    [Params(StreamingPattern.Continuous, StreamingPattern.Bursty, StreamingPattern.Random)]
    public StreamingPattern Pattern { get; set; }
    
    [Params(BackpressureStrategy.Drop, BackpressureStrategy.Buffer, BackpressureStrategy.Block)]
    public BackpressureStrategy BackpressureHandling { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Setup DotCompute services with pipeline integration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDotComputeRuntime();
        services.AddDotComputeLinq();
        services.AddPipelineLinqServices();
        
        var serviceProvider = services.BuildServiceProvider();
        _provider = serviceProvider.GetRequiredService<IComputeLinqProvider>();
        _pipelineService = serviceProvider.GetRequiredService<IPipelineOrchestrationService>();
        
        // Get accelerators
        var acceleratorService = serviceProvider.GetRequiredService<IAcceleratorService>();
        _cpuAccelerator = acceleratorService.GetAccelerators().First(a => a.Info.DeviceType == DeviceType.CPU);
        _gpuAccelerator = acceleratorService.GetAccelerators().FirstOrDefault(a => a.Info.DeviceType == DeviceType.CUDA) 
                          ?? _cpuAccelerator; // Fallback to CPU if no GPU available
        
        SetupStreams();
        _random = new Random(42);
        _cancellationTokenSource = new CancellationTokenSource();
    }
    
    private void SetupStreams()
    {
        // Setup reactive streams
        _floatStream = new Subject<float>();
        _intStream = new Subject<int>();
        _complexStream = new Subject<StreamingData>();
        
        // Setup channels with different backpressure strategies
        var channelOptions = new BoundedChannelOptions(BatchSize * 10)
        {
            FullMode = BackpressureHandling switch
            {
                BackpressureStrategy.Drop => BoundedChannelFullMode.DropOldest,
                BackpressureStrategy.Buffer => BoundedChannelFullMode.Wait,
                BackpressureStrategy.Block => BoundedChannelFullMode.Wait,
                _ => BoundedChannelFullMode.Wait
            },
            SingleReader = false,
            SingleWriter = true
        };
        
        _floatChannel = Channel.CreateBounded<float>(channelOptions);
        _intChannel = Channel.CreateBounded<int>(channelOptions);
    }

    #region Throughput Benchmarks
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Throughput", "StandardStream")]
    public async Task<long> StandardStream_Throughput()
    {
        var processed = 0L;
        var source = GenerateDataStream(StreamSize);
        
        var stopwatch = Stopwatch.StartNew();
        
        await foreach (var item in source)
        {
            // Simulate processing
            var result = item * 2.5f + 1.0f;
            if (result > 1000f) processed++;
        }
        
        stopwatch.Stop();
        
        // Return items per second
        return (long)(processed / stopwatch.Elapsed.TotalSeconds);
    }
    
    [Benchmark]
    [BenchmarkCategory("Throughput", "ReactiveStream")]
    public async Task<long> ReactiveStream_Throughput()
    {
        var processed = 0L;
        var completionSource = new TaskCompletionSource<long>();
        
        var subscription = _floatStream
            .Buffer(BatchSize)
            .Select(batch => batch.Select(x => x * 2.5f + 1.0f).Where(x => x > 1000f).Count())
            .Subscribe(
                count => Interlocked.Add(ref processed, count),
                ex => completionSource.SetException(ex),
                () => completionSource.SetResult(processed)
            );
        
        var stopwatch = Stopwatch.StartNew();
        
        // Generate data
        _ = Task.Run(async () =>
        {
            await GenerateStreamData(_floatStream, StreamSize);
            _floatStream.OnCompleted();
        });
        
        var totalProcessed = await completionSource.Task;
        stopwatch.Stop();
        
        subscription.Dispose();
        
        // Return items per second
        return (long)(totalProcessed / stopwatch.Elapsed.TotalSeconds);
    }
    
    [Benchmark]
    [BenchmarkCategory("Throughput", "GpuStream")]
    public async Task<long> GpuStream_Throughput()
    {
        var processed = 0L;
        var stopwatch = Stopwatch.StartNew();
        
        var batches = GenerateBatchedData(StreamSize, BatchSize);
        
        foreach (var batch in batches)
        {
            var queryable = _provider.CreateQueryable(batch, _gpuAccelerator);
            var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 1000f));
            processed += result.Length;
        }
        
        stopwatch.Stop();
        
        // Return items per second
        return (long)(processed / stopwatch.Elapsed.TotalSeconds);
    }
    
    [Benchmark]
    [BenchmarkCategory("Throughput", "PipelineStream")]
    public async Task<long> PipelineStream_Throughput()
    {
        var processed = 0L;
        var stopwatch = Stopwatch.StartNew();
        
        // Use pipeline-optimized provider for streaming
        var batches = GenerateBatchedData(StreamSize, BatchSize);
        
        var tasks = batches.Select(async batch =>
        {
            var queryable = _provider.CreateQueryable(batch);
            var result = await _pipelineService.ExecuteWithOrchestrationAsync(
                queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 1000f));
            return result.Count();
        });
        
        var results = await Task.WhenAll(tasks);
        processed = results.Sum();
        
        stopwatch.Stop();
        
        // Return items per second
        return (long)(processed / stopwatch.Elapsed.TotalSeconds);
    }
    
    #endregion

    #region Latency Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Latency", "SingleItem")]
    public TimeSpan SingleItem_Latency()
    {
        var data = new[] { _random.Next(1, 10000) };
        
        var stopwatch = Stopwatch.StartNew();
        var result = data.Select(x => x * 2 + 1).Where(x => x > 1000).ToArray();
        stopwatch.Stop();
        
        _ = result.Length; // Prevent optimization
        return stopwatch.Elapsed;
    }
    
    [Benchmark]
    [BenchmarkCategory("Latency", "SmallBatch")]
    public async Task<TimeSpan> SmallBatch_Latency()
    {
        var data = Enumerable.Range(0, 10).Select(_ => _random.Next(1, 10000)).ToArray();
        
        var stopwatch = Stopwatch.StartNew();
        var queryable = _provider.CreateQueryable(data, _gpuAccelerator);
        var result = await ExecuteStreamQuery(queryable.Select(x => x * 2 + 1).Where(x => x > 1000));
        stopwatch.Stop();
        
        _ = result.Length; // Prevent optimization
        return stopwatch.Elapsed;
    }
    
    [Benchmark]
    [BenchmarkCategory("Latency", "StreamLatency")]
    public async Task<TimeSpan> Stream_AverageLatency()
    {
        var latencies = new List<TimeSpan>();
        var source = GenerateTimestampedDataStream(1000);
        
        await foreach (var (timestamp, data) in source)
        {
            var latency = DateTime.UtcNow - timestamp;
            latencies.Add(latency);
        }
        
        return TimeSpan.FromTicks((long)latencies.Average(l => l.Ticks));
    }
    
    #endregion

    #region Backpressure Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("Backpressure", "BufferStrategy")]
    public async Task<BackpressureResult> BufferStrategy_Performance()
    {
        var processed = 0L;
        var dropped = 0L;
        var maxQueueSize = 0L;
        
        var buffer = new ConcurrentQueue<float>();
        var stopwatch = Stopwatch.StartNew();
        
        // Producer task - generates data faster than consumer can process
        var producer = Task.Run(async () =>
        {
            for (int i = 0; i < StreamSize; i++)
            {
                var value = (float)_random.NextDouble() * 1000f;
                
                if (BackpressureHandling == BackpressureStrategy.Drop && buffer.Count > BatchSize * 2)
                {
                    Interlocked.Increment(ref dropped);
                }
                else
                {
                    buffer.Enqueue(value);
                    maxQueueSize = Math.Max(maxQueueSize, buffer.Count);
                }
                
                // Simulate bursty pattern
                if (Pattern == StreamingPattern.Bursty && i % 100 == 0)
                {
                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
            }
        });
        
        // Consumer task - processes data in batches
        var consumer = Task.Run(async () =>
        {
            var batch = new List<float>();
            
            while (!producer.IsCompleted || !buffer.IsEmpty)
            {
                // Collect batch
                while (batch.Count < BatchSize && buffer.TryDequeue(out var item))
                {
                    batch.Add(item);
                }
                
                if (batch.Count > 0)
                {
                    // Process batch on GPU
                    var queryable = _provider.CreateQueryable(batch.ToArray(), _gpuAccelerator);
                    var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
                    Interlocked.Add(ref processed, result.Length);
                    
                    batch.Clear();
                }
                
                await Task.Delay(1, _cancellationTokenSource.Token); // Simulate processing time
            }
        });
        
        await Task.WhenAll(producer, consumer);
        stopwatch.Stop();
        
        return new BackpressureResult
        {
            ProcessedItems = processed,
            DroppedItems = dropped,
            MaxQueueSize = maxQueueSize,
            TotalTime = stopwatch.Elapsed,
            Throughput = processed / stopwatch.Elapsed.TotalSeconds
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("Backpressure", "ChannelStrategy")]
    public async Task<BackpressureResult> ChannelStrategy_Performance()
    {
        var processed = 0L;
        var stopwatch = Stopwatch.StartNew();
        
        // Producer task
        var producer = Task.Run(async () =>
        {
            var writer = _floatChannel.Writer;
            
            try
            {
                for (int i = 0; i < StreamSize; i++)
                {
                    var value = (float)_random.NextDouble() * 1000f;
                    await writer.WriteAsync(value, _cancellationTokenSource.Token);
                    
                    // Simulate different patterns
                    if (Pattern == StreamingPattern.Bursty && i % 100 == 0)
                    {
                        await Task.Delay(10, _cancellationTokenSource.Token);
                    }
                    else if (Pattern == StreamingPattern.Random)
                    {
                        await Task.Delay(_random.Next(0, 5), _cancellationTokenSource.Token);
                    }
                }
            }
            finally
            {
                writer.Complete();
            }
        });
        
        // Consumer task
        var consumer = Task.Run(async () =>
        {
            var reader = _floatChannel.Reader;
            var batch = new List<float>();
            
            await foreach (var item in reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                batch.Add(item);
                
                if (batch.Count >= BatchSize)
                {
                    var queryable = _provider.CreateQueryable(batch.ToArray(), _gpuAccelerator);
                    var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
                    Interlocked.Add(ref processed, result.Length);
                    
                    batch.Clear();
                }
            }
            
            // Process remaining items
            if (batch.Count > 0)
            {
                var queryable = _provider.CreateQueryable(batch.ToArray(), _gpuAccelerator);
                var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
                Interlocked.Add(ref processed, result.Length);
            }
        });
        
        await Task.WhenAll(producer, consumer);
        stopwatch.Stop();
        
        return new BackpressureResult
        {
            ProcessedItems = processed,
            DroppedItems = 0, // Channels don't drop by default
            MaxQueueSize = BatchSize * 10, // Channel capacity
            TotalTime = stopwatch.Elapsed,
            Throughput = processed / stopwatch.Elapsed.TotalSeconds
        };
    }
    
    #endregion

    #region Batch Size Optimization Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("BatchSize", "Optimization")]
    public async Task<BatchOptimizationResult> OptimalBatchSize_Analysis()
    {
        var results = new List<(int BatchSize, double Throughput, TimeSpan Latency)>();
        var testBatchSizes = new[] { 16, 32, 64, 128, 256, 512, 1024, 2048 };
        
        foreach (var testBatchSize in testBatchSizes)
        {
            var batches = GenerateBatchedData(10000, testBatchSize);
            var stopwatch = Stopwatch.StartNew();
            var totalProcessed = 0L;
            
            foreach (var batch in batches.Take(10)) // Limit for benchmark timing
            {
                var queryable = _provider.CreateQueryable(batch, _gpuAccelerator);
                var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
                totalProcessed += result.Length;
            }
            
            stopwatch.Stop();
            
            var throughput = totalProcessed / stopwatch.Elapsed.TotalSeconds;
            var avgLatency = TimeSpan.FromTicks(stopwatch.Elapsed.Ticks / 10);
            
            results.Add((testBatchSize, throughput, avgLatency));
        }
        
        var optimalBatch = results.OrderByDescending(r => r.Throughput).First();
        
        return new BatchOptimizationResult
        {
            OptimalBatchSize = optimalBatch.BatchSize,
            MaxThroughput = optimalBatch.Throughput,
            MinLatency = optimalBatch.Latency,
            AllResults = results
        };
    }
    
    [Benchmark]
    [BenchmarkCategory("BatchSize", "AdaptiveBatching")]
    public async Task<double> AdaptiveBatching_Performance()
    {
        var processed = 0L;
        var currentBatchSize = BatchSize;
        var batchPerformance = new Queue<double>();
        var stopwatch = Stopwatch.StartNew();
        
        var source = GenerateDataStream(StreamSize);
        var batch = new List<float>();
        
        await foreach (var item in source)
        {
            batch.Add(item);
            
            if (batch.Count >= currentBatchSize)
            {
                var batchStart = Stopwatch.StartNew();
                
                var queryable = _provider.CreateQueryable(batch.ToArray(), _gpuAccelerator);
                var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
                processed += result.Length;
                
                batchStart.Stop();
                
                // Track batch performance
                var batchThroughput = batch.Count / batchStart.Elapsed.TotalSeconds;
                batchPerformance.Enqueue(batchThroughput);
                
                // Adaptive batch size adjustment
                if (batchPerformance.Count >= 5)
                {
                    var avgThroughput = batchPerformance.Average();
                    var recentThroughput = batchPerformance.Skip(batchPerformance.Count - 3).Average();
                    
                    if (recentThroughput > avgThroughput * 1.1 && currentBatchSize < 2048)
                    {
                        currentBatchSize = Math.Min(currentBatchSize * 2, 2048);
                    }
                    else if (recentThroughput < avgThroughput * 0.9 && currentBatchSize > 16)
                    {
                        currentBatchSize = Math.Max(currentBatchSize / 2, 16);
                    }
                    
                    if (batchPerformance.Count > 10)
                        batchPerformance.Dequeue();
                }
                
                batch.Clear();
            }
        }
        
        // Process remaining batch
        if (batch.Count > 0)
        {
            var queryable = _provider.CreateQueryable(batch.ToArray(), _gpuAccelerator);
            var result = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f).Where(x => x > 500f));
            processed += result.Length;
        }
        
        stopwatch.Stop();
        
        return processed / stopwatch.Elapsed.TotalSeconds;
    }
    
    #endregion

    #region Real-Time Processing Benchmarks
    
    [Benchmark]
    [BenchmarkCategory("RealTime", "LowLatency")]
    public async Task<RealTimeMetrics> RealTime_LowLatencyProcessing()
    {
        var metrics = new RealTimeMetrics();
        var source = GenerateRealTimeDataStream(1000);
        
        await foreach (var dataPoint in source)
        {
            var processingStart = Stopwatch.StartNew();
            
            // Single-item processing for minimum latency
            var result = dataPoint.Value * 2.5f + 1.0f;
            if (result > dataPoint.Threshold)
            {
                metrics.TriggeredAlerts++;
            }
            
            processingStart.Stop();
            metrics.TotalLatency += processingStart.Elapsed;
            metrics.ProcessedItems++;
            
            // Real-time constraint: must process within 1ms
            if (processingStart.Elapsed.TotalMilliseconds > 1.0)
            {
                metrics.LatencyViolations++;
            }
        }
        
        metrics.AverageLatency = TimeSpan.FromTicks(metrics.TotalLatency.Ticks / metrics.ProcessedItems);
        
        return metrics;
    }
    
    [Benchmark]
    [BenchmarkCategory("RealTime", "HighThroughput")]
    public async Task<RealTimeMetrics> RealTime_HighThroughputProcessing()
    {
        var metrics = new RealTimeMetrics();
        var source = GenerateRealTimeDataStream(10000);
        var batch = new List<RealTimeData>();
        
        await foreach (var dataPoint in source)
        {
            batch.Add(dataPoint);
            
            if (batch.Count >= BatchSize)
            {
                var processingStart = Stopwatch.StartNew();
                
                // Batch processing for high throughput
                var values = batch.Select(d => d.Value).ToArray();
                var thresholds = batch.Select(d => d.Threshold).ToArray();
                
                var queryable = _provider.CreateQueryable(values, _gpuAccelerator);
                var results = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f));
                
                // Check alerts
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i] > thresholds[i])
                    {
                        metrics.TriggeredAlerts++;
                    }
                }
                
                processingStart.Stop();
                metrics.TotalLatency += processingStart.Stop();
                metrics.ProcessedItems += batch.Count;
                
                batch.Clear();
            }
        }
        
        // Process remaining batch
        if (batch.Count > 0)
        {
            var values = batch.Select(d => d.Value).ToArray();
            var queryable = _provider.CreateQueryable(values, _gpuAccelerator);
            var results = await ExecuteStreamQuery(queryable.Select(x => x * 2.5f + 1.0f));
            metrics.ProcessedItems += batch.Count;
        }
        
        if (metrics.ProcessedItems > 0)
        {
            metrics.AverageLatency = TimeSpan.FromTicks(metrics.TotalLatency.Ticks / metrics.ProcessedItems);
        }
        
        return metrics;
    }
    
    #endregion

    #region Helper Methods
    
    private async IAsyncEnumerable<float> GenerateDataStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return (float)_random.NextDouble() * 1000f;
            
            if (Pattern == StreamingPattern.Bursty && i % 100 == 0)
            {
                await Task.Delay(1);
            }
        }
    }
    
    private async IAsyncEnumerable<(DateTime Timestamp, float Data)> GenerateTimestampedDataStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return (DateTime.UtcNow, (float)_random.NextDouble() * 1000f);
            await Task.Delay(1); // Simulate 1ms intervals
        }
    }
    
    private async IAsyncEnumerable<RealTimeData> GenerateRealTimeDataStream(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new RealTimeData
            {
                Timestamp = DateTime.UtcNow,
                Value = (float)_random.NextDouble() * 1000f,
                Threshold = 500f + (float)_random.NextDouble() * 200f
            };
            
            await Task.Delay(1); // Simulate real-time data arrival
        }
    }
    
    private async Task GenerateStreamData(Subject<float> stream, int count)
    {
        for (int i = 0; i < count; i++)
        {
            stream.OnNext((float)_random.NextDouble() * 1000f);
            
            if (Pattern == StreamingPattern.Bursty && i % 100 == 0)
            {
                await Task.Delay(10);
            }
            else if (Pattern == StreamingPattern.Random)
            {
                await Task.Delay(_random.Next(0, 5));
            }
        }
    }
    
    private IEnumerable<float[]> GenerateBatchedData(int totalCount, int batchSize)
    {
        var batch = new List<float>();
        
        for (int i = 0; i < totalCount; i++)
        {
            batch.Add((float)_random.NextDouble() * 1000f);
            
            if (batch.Count >= batchSize)
            {
                yield return batch.ToArray();
                batch.Clear();
            }
        }
        
        if (batch.Count > 0)
        {
            yield return batch.ToArray();
        }
    }
    
    private async Task<T[]> ExecuteStreamQuery<T>(IQueryable<T> queryable)
    {
        if (queryable.Provider is IComputeQueryProvider provider)
        {
            var result = await provider.ExecuteAsync<IEnumerable<T>>(queryable.Expression);
            return result.ToArray();
        }
        
        return queryable.ToArray();
    }
    
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _cancellationTokenSource?.Cancel();
        _floatStream?.Dispose();
        _intStream?.Dispose();
        _complexStream?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
    
    #endregion
}

#region Supporting Types

/// <summary>
/// Streaming patterns for benchmark testing.
/// </summary>
public enum StreamingPattern
{
    Continuous,  // Steady data flow
    Bursty,      // Periodic bursts with quiet periods
    Random       // Random intervals
}

/// <summary>
/// Backpressure handling strategies.
/// </summary>
public enum BackpressureStrategy
{
    Drop,    // Drop oldest items when buffer is full
    Buffer,  // Buffer items (may cause memory pressure)
    Block    // Block producer when buffer is full
}

/// <summary>
/// Streaming data structure for complex scenarios.
/// </summary>
public class StreamingData
{
    public DateTime Timestamp { get; set; }
    public float Value { get; set; }
    public string Category { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Real-time data point with timing constraints.
/// </summary>
public class RealTimeData
{
    public DateTime Timestamp { get; set; }
    public float Value { get; set; }
    public float Threshold { get; set; }
}

/// <summary>
/// Result structure for backpressure performance analysis.
/// </summary>
public class BackpressureResult
{
    public long ProcessedItems { get; set; }
    public long DroppedItems { get; set; }
    public long MaxQueueSize { get; set; }
    public TimeSpan TotalTime { get; set; }
    public double Throughput { get; set; }
    
    public double DropRate => ProcessedItems > 0 ? (double)DroppedItems / (ProcessedItems + DroppedItems) : 0.0;
}

/// <summary>
/// Result structure for batch size optimization analysis.
/// </summary>
public class BatchOptimizationResult
{
    public int OptimalBatchSize { get; set; }
    public double MaxThroughput { get; set; }
    public TimeSpan MinLatency { get; set; }
    public List<(int BatchSize, double Throughput, TimeSpan Latency)> AllResults { get; set; } = new();
}

/// <summary>
/// Real-time processing metrics.
/// </summary>
public class RealTimeMetrics
{
    public long ProcessedItems { get; set; }
    public long TriggeredAlerts { get; set; }
    public long LatencyViolations { get; set; }
    public TimeSpan TotalLatency { get; set; }
    public TimeSpan AverageLatency { get; set; }
    
    public double AlertRate => ProcessedItems > 0 ? (double)TriggeredAlerts / ProcessedItems : 0.0;
    public double ViolationRate => ProcessedItems > 0 ? (double)LatencyViolations / ProcessedItems : 0.0;
}

/// <summary>
/// Custom benchmark configuration for streaming compute tests.
/// </summary>
public class StreamingComputeConfig : ManualConfig
{
    public StreamingComputeConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithJit(Jit.RyuJit)
            .WithPlatform(Platform.X64)
            .WithWarmupCount(2)
            .WithIterationCount(5)
            .WithInvocationCount(1)
            .WithStrategy(RunStrategy.Throughput));
            
        AddDiagnoser(MemoryDiagnoser.Default);
        AddDiagnoser(ThreadingDiagnoser.Default);
        
        // Add columns for streaming-specific metrics
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(BaselineRatioColumn.RatioMean);
        AddColumn(RankColumn.Arabic);
        
        AddOrderer(DefaultOrderer.Instance);
        
        WithOptions(ConfigOptions.DisableOptimizationsValidator);
    }
}

#endregion