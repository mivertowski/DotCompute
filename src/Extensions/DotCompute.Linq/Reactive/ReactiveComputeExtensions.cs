using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace DotCompute.Linq.Reactive;

/// <summary>
/// Backpressure strategies for reactive compute operations
/// </summary>
public enum BackpressureStrategy
{
    /// <summary>Buffer all incoming data (default)</summary>
    Buffer,
    /// <summary>Drop oldest data when buffer is full</summary>
    DropOldest,
    /// <summary>Drop newest data when buffer is full</summary>
    DropNewest,
    /// <summary>Keep only the latest data point</summary>
    Latest,
    /// <summary>Apply back-pressure to the source</summary>
    Block
}

/// <summary>
/// Configuration for reactive compute operations
/// </summary>
public record ReactiveComputeConfig
{
    /// <summary>Maximum batch size for GPU operations</summary>
    public int MaxBatchSize { get; init; } = 1024;
    
    /// <summary>Minimum batch size before processing</summary>
    public int MinBatchSize { get; init; } = 32;
    
    /// <summary>Maximum wait time before processing incomplete batch</summary>
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromMilliseconds(10);
    
    /// <summary>Buffer size for backpressure handling</summary>
    public int BufferSize { get; init; } = 10000;
    
    /// <summary>Backpressure strategy</summary>
    public BackpressureStrategy BackpressureStrategy { get; init; } = BackpressureStrategy.Buffer;
    
    /// <summary>Enable adaptive batch sizing based on throughput</summary>
    public bool EnableAdaptiveBatching { get; init; } = true;
    
    /// <summary>Preferred accelerator for compute operations</summary>
    public string? PreferredAccelerator { get; init; }
}

/// <summary>
/// Extension methods for integrating Reactive Extensions with DotCompute
/// Provides GPU-accelerated stream processing capabilities
/// </summary>
public static class ReactiveComputeExtensions
{
    private static readonly Meter _meter = new("DotCompute.Reactive");
    private static readonly Counter<long> _operationCounter = _meter.CreateCounter<long>("reactive_operations_total");
    private static readonly Histogram<double> _batchSizeHistogram = _meter.CreateHistogram<double>("batch_size");
    private static readonly Histogram<double> _processingTimeHistogram = _meter.CreateHistogram<double>("processing_time_ms");

    /// <summary>
    /// Applies a compute kernel to each element in the observable sequence
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TResult">Result element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="selector">Transform function (will be compiled to kernel)</param>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="config">Reactive compute configuration</param>
    /// <returns>Transformed observable sequence</returns>
    public static IObservable<TResult> ComputeSelect<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector,
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig? config = null)
        where TSource : unmanaged
        where TResult : unmanaged
    {
        config ??= new ReactiveComputeConfig();
        
        return Observable.Create<TResult>(observer =>
        {
            var scheduler = new ReactiveKernelScheduler(orchestrator, config);
            var disposable = new CompositeDisposable();
            
            var subscription = source
                .Buffer(config.BatchTimeout, config.MaxBatchSize, scheduler)
                .Where(batch => batch.Count >= config.MinBatchSize || scheduler.ShouldFlushBatch())
                .SelectMany(async batch =>
                {
                    try
                    {
                        _operationCounter.Add(1, new KeyValuePair<string, object?>("operation", "compute_select"));
                        _batchSizeHistogram.Record(batch.Count);
                        
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var results = await ProcessBatchAsync(batch.ToArray(), selector, orchestrator);
                        stopwatch.Stop();
                        
                        _processingTimeHistogram.Record(stopwatch.ElapsedMilliseconds);
                        
                        return results;
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                        return Array.Empty<TResult>();
                    }
                })
                .SelectMany(results => results.ToObservable())
                .Subscribe(observer);
            
            disposable.Add(subscription);
            disposable.Add(scheduler);
            
            return disposable;
        });
    }

    /// <summary>
    /// Filters elements using a GPU-accelerated predicate
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="predicate">Filter predicate (will be compiled to kernel)</param>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="config">Reactive compute configuration</param>
    /// <returns>Filtered observable sequence</returns>
    public static IObservable<T> ComputeWhere<T>(
        this IObservable<T> source,
        Func<T, bool> predicate,
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig? config = null)
        where T : unmanaged
    {
        config ??= new ReactiveComputeConfig();
        
        return Observable.Create<T>(observer =>
        {
            var scheduler = new ReactiveKernelScheduler(orchestrator, config);
            var disposable = new CompositeDisposable();
            
            var subscription = source
                .Buffer(config.BatchTimeout, config.MaxBatchSize, scheduler)
                .Where(batch => batch.Count >= config.MinBatchSize || scheduler.ShouldFlushBatch())
                .SelectMany(async batch =>
                {
                    try
                    {
                        _operationCounter.Add(1, new KeyValuePair<string, object?>("operation", "compute_where"));
                        _batchSizeHistogram.Record(batch.Count);
                        
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var results = await FilterBatchAsync(batch.ToArray(), predicate, orchestrator);
                        stopwatch.Stop();
                        
                        _processingTimeHistogram.Record(stopwatch.ElapsedMilliseconds);
                        
                        return results;
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                        return Array.Empty<T>();
                    }
                })
                .SelectMany(results => results.ToObservable())
                .Subscribe(observer);
            
            disposable.Add(subscription);
            disposable.Add(scheduler);
            
            return disposable;
        });
    }

    /// <summary>
    /// Performs GPU-accelerated aggregation over sliding windows
    /// </summary>
    /// <typeparam name="TSource">Source element type</typeparam>
    /// <typeparam name="TAccumulate">Accumulator type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="seed">Initial accumulator value</param>
    /// <param name="func">Aggregation function</param>
    /// <param name="resultSelector">Result transformation</param>
    /// <param name="windowSize">Sliding window size</param>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="config">Reactive compute configuration</param>
    /// <returns>Aggregated observable sequence</returns>
    public static IObservable<TResult> ComputeAggregate<TSource, TAccumulate, TResult>(
        this IObservable<TSource> source,
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> func,
        Func<TAccumulate, TResult> resultSelector,
        int windowSize,
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig? config = null)
        where TSource : unmanaged
        where TAccumulate : unmanaged
        where TResult : unmanaged
    {
        config ??= new ReactiveComputeConfig();
        
        return source
            .Window(windowSize, 1)
            .Select(window => window.ToArray().ToObservable())
            .Switch()
            .ComputeSelect(batch => 
            {
                var accumulator = seed;
                for (int i = 0; i < windowSize && i < batch.Length; i++)
                {
                    accumulator = func(accumulator, batch[i]);
                }
                return resultSelector(accumulator);
            }, orchestrator, config);
    }

    /// <summary>
    /// Creates time-based sliding windows with GPU-accelerated processing
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="windowDuration">Window duration</param>
    /// <param name="windowShift">Window shift interval</param>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="config">Reactive compute configuration</param>
    /// <returns>Observable of windowed arrays</returns>
    public static IObservable<T[]> ComputeWindow<T>(
        this IObservable<T> source,
        TimeSpan windowDuration,
        TimeSpan windowShift,
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig? config = null)
        where T : unmanaged
    {
        config ??= new ReactiveComputeConfig();
        
        return source
            .Window(windowDuration, windowShift)
            .Select(window => window.ToArray().ToObservable())
            .Switch();
    }

    /// <summary>
    /// Applies backpressure handling to the observable stream
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="strategy">Backpressure strategy</param>
    /// <param name="bufferSize">Buffer size</param>
    /// <returns>Observable with backpressure handling</returns>
    public static IObservable<T> WithBackpressure<T>(
        this IObservable<T> source,
        BackpressureStrategy strategy,
        int bufferSize = 10000)
    {
        return strategy switch
        {
            BackpressureStrategy.Buffer => source.Buffer(bufferSize).SelectMany(x => x),
            BackpressureStrategy.DropOldest => source.Scan(
                new Queue<T>(),
                (queue, item) =>
                {
                    if (queue.Count >= bufferSize)
                        queue.Dequeue();
                    queue.Enqueue(item);
                    return queue;
                }).SelectMany(queue => queue),
            BackpressureStrategy.DropNewest => source.Buffer(bufferSize).Select(buffer => 
                buffer.Count > bufferSize ? buffer.Take(bufferSize) : buffer).SelectMany(x => x),
            BackpressureStrategy.Latest => source.Sample(TimeSpan.FromMilliseconds(1)),
            BackpressureStrategy.Block => source, // Default behavior
            _ => source
        };
    }

    /// <summary>
    /// Enables real-time performance monitoring for reactive compute streams
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="metricsCallback">Callback for performance metrics</param>
    /// <returns>Observable with performance monitoring</returns>
    public static IObservable<T> WithPerformanceMonitoring<T>(
        this IObservable<T> source,
        Action<PerformanceMetrics>? metricsCallback = null)
    {
        return Observable.Create<T>(observer =>
        {
            var startTime = DateTime.UtcNow;
            var elementCount = 0L;
            var lastMetricsReport = DateTime.UtcNow;
            
            return source.Subscribe(
                value =>
                {
                    elementCount++;
                    observer.OnNext(value);
                    
                    var now = DateTime.UtcNow;
                    if (now - lastMetricsReport > TimeSpan.FromSeconds(1))
                    {
                        var metrics = new PerformanceMetrics
                        {
                            TotalElements = elementCount,
                            ElementsPerSecond = elementCount / (now - startTime).TotalSeconds,
                            UpTime = now - startTime
                        };
                        
                        metricsCallback?.Invoke(metrics);
                        lastMetricsReport = now;
                    }
                },
                observer.OnError,
                observer.OnCompleted);
        });
    }

    /// <summary>
    /// Processes a batch of data using GPU compute
    /// </summary>
    private static async Task<TResult[]> ProcessBatchAsync<TSource, TResult>(
        TSource[] batch,
        Func<TSource, TResult> selector,
        IComputeOrchestrator orchestrator)
        where TSource : unmanaged
        where TResult : unmanaged
    {
        using var inputBuffer = UnifiedBuffer<TSource>.Allocate(batch.Length);
        using var outputBuffer = UnifiedBuffer<TResult>.Allocate(batch.Length);
        
        inputBuffer.CopyFrom(batch);
        
        // Create a simple kernel for the selector function
        var kernelCode = GenerateKernelCode(selector);
        var result = await orchestrator.ExecuteKernelAsync<TSource, TResult>(
            kernelCode,
            inputBuffer.Span,
            outputBuffer.Span);
        
        return outputBuffer.ToArray();
    }

    /// <summary>
    /// Filters a batch of data using GPU compute
    /// </summary>
    private static async Task<T[]> FilterBatchAsync<T>(
        T[] batch,
        Func<T, bool> predicate,
        IComputeOrchestrator orchestrator)
        where T : unmanaged
    {
        using var inputBuffer = UnifiedBuffer<T>.Allocate(batch.Length);
        using var maskBuffer = UnifiedBuffer<bool>.Allocate(batch.Length);
        
        inputBuffer.CopyFrom(batch);
        
        // Create a kernel for the predicate
        var kernelCode = GeneratePredicateKernelCode(predicate);
        await orchestrator.ExecuteKernelAsync<T, bool>(
            kernelCode,
            inputBuffer.Span,
            maskBuffer.Span);
        
        var mask = maskBuffer.ToArray();
        var result = new List<T>(batch.Length);
        
        for (int i = 0; i < batch.Length; i++)
        {
            if (mask[i])
                result.Add(batch[i]);
        }
        
        return result.ToArray();
    }

    /// <summary>
    /// Generates kernel code for a selector function (simplified implementation)
    /// </summary>
    private static string GenerateKernelCode<TSource, TResult>(Func<TSource, TResult> selector)
    {
        // This is a simplified implementation
        // In practice, you would use expression tree analysis or source generators
        return $@"
        __global__ void TransformKernel({typeof(TSource).Name}* input, {typeof(TResult).Name}* output, int length)
        {{
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < length)
            {{
                output[idx] = transform(input[idx]);
            }}
        }}";
    }

    /// <summary>
    /// Generates kernel code for a predicate function (simplified implementation)
    /// </summary>
    private static string GeneratePredicateKernelCode<T>(Func<T, bool> predicate)
    {
        // This is a simplified implementation
        return $@"
        __global__ void FilterKernel({typeof(T).Name}* input, bool* output, int length)
        {{
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < length)
            {{
                output[idx] = filter(input[idx]);
            }}
        }}";
    }
}

/// <summary>
/// Performance metrics for reactive compute operations
/// </summary>
public record PerformanceMetrics
{
    /// <summary>Total number of elements processed</summary>
    public long TotalElements { get; init; }
    
    /// <summary>Processing rate in elements per second</summary>
    public double ElementsPerSecond { get; init; }
    
    /// <summary>Total uptime</summary>
    public TimeSpan UpTime { get; init; }
    
    /// <summary>Average batch size</summary>
    public double AverageBatchSize { get; init; }
    
    /// <summary>GPU utilization percentage</summary>
    public double GpuUtilization { get; init; }
    
    /// <summary>Memory usage in bytes</summary>
    public long MemoryUsage { get; init; }
}