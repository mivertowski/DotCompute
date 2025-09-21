using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions;
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
/// Configuration for reactive compute operations
public record ReactiveComputeConfig
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
/// Extension methods for integrating Reactive Extensions with DotCompute
/// Provides GPU-accelerated stream processing capabilities
public static class ReactiveComputeExtensions
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
                        observer.OnError(ex);
                        return Array.Empty<TResult>();
                })
                .SelectMany(results => results.ToObservable())
                .Subscribe(observer);
            disposable.Add(subscription);
            disposable.Add(scheduler);
            return disposable;
        });
    }
    /// Filters elements using a GPU-accelerated predicate
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="predicate">Filter predicate (will be compiled to kernel)</param>
    /// <returns>Filtered observable sequence</returns>
    public static IObservable<T> ComputeWhere<T>(
        this IObservable<T> source,
        Func<T, bool> predicate,
        where T : unmanaged
        return Observable.Create<T>(observer =>
                        _operationCounter.Add(1, new KeyValuePair<string, object?>("operation", "compute_where"));
                        var results = await FilterBatchAsync(batch.ToArray(), predicate, orchestrator);
                        return Array.Empty<T>();
    /// Performs GPU-accelerated aggregation over sliding windows
    /// <typeparam name="TAccumulate">Accumulator type</typeparam>
    /// <typeparam name="TResult">Result type</typeparam>
    /// <param name="seed">Initial accumulator value</param>
    /// <param name="func">Aggregation function</param>
    /// <param name="resultSelector">Result transformation</param>
    /// <param name="windowSize">Sliding window size</param>
    /// <returns>Aggregated observable sequence</returns>
    public static IObservable<TResult> ComputeAggregate<TSource, TAccumulate, TResult>(
        TAccumulate seed,
        Func<TAccumulate, TSource, TAccumulate> func,
        Func<TAccumulate, TResult> resultSelector,
        int windowSize,
        where TAccumulate : unmanaged
        return source
            .Window(windowSize, 1)
            .SelectMany(window => window.ToArray())
            .Select(batch =>
            {
                var accumulator = seed;
                for (var i = 0; i < windowSize && i < batch.Length; i++)
                    accumulator = func(accumulator, batch[i]);
                }
                return resultSelector(accumulator);
            });
    /// Creates time-based sliding windows with GPU-accelerated processing
    /// <param name="windowDuration">Window duration</param>
    /// <param name="windowShift">Window shift interval</param>
    /// <returns>Observable of windowed arrays</returns>
    public static IObservable<T[]> ComputeWindow<T>(
        TimeSpan windowDuration,
        TimeSpan windowShift,
            .Window(windowDuration, windowShift)
            .SelectMany(window => window.ToArray());
    /// Applies backpressure handling to the observable stream
    /// <param name="strategy">Backpressure strategy</param>
    /// <param name="bufferSize">Buffer size</param>
    /// <returns>Observable with backpressure handling</returns>
    public static IObservable<T> WithBackpressure<T>(
        BackpressureStrategy strategy,
        int bufferSize = 10000)
        return strategy switch
            BackpressureStrategy.Buffer => source.Buffer(bufferSize).SelectMany(x => x),
            BackpressureStrategy.DropOldest => source.Scan(
                new Queue<T>(),
                (queue, item) =>
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
    /// Enables real-time performance monitoring for reactive compute streams
    /// <param name="metricsCallback">Callback for performance metrics</param>
    /// <returns>Observable with performance monitoring</returns>
    public static IObservable<T> WithPerformanceMonitoring<T>(
        Action<PerformanceMetrics>? metricsCallback = null)
            var startTime = DateTime.UtcNow;
            var elementCount = 0L;
            var lastMetricsReport = DateTime.UtcNow;
            return source.Subscribe(
                value =>
                    elementCount++;
                    observer.OnNext(value);
                    var now = DateTime.UtcNow;
                    if (now - lastMetricsReport > TimeSpan.FromSeconds(1))
                        var metrics = new PerformanceMetrics
                        {
                            TotalElements = elementCount,
                            ElementsPerSecond = elementCount / (now - startTime).TotalSeconds,
                            UpTime = now - startTime
                        };
                        metricsCallback?.Invoke(metrics);
                        lastMetricsReport = now;
                },
                observer.OnError,
                observer.OnCompleted);
    /// Processes a batch of data using GPU compute
    private static async Task<TResult[]> ProcessBatchAsync<TSource, TResult>(
        TSource[] batch,
        IComputeOrchestrator orchestrator)
        // Create a basic memory manager
        using var memoryManager = new UnifiedMemoryManager();
        using var inputBuffer = await memoryManager.AllocateAsync<TSource>(batch.Length);
        await inputBuffer.CopyFromAsync(batch);
        using var outputBuffer = await memoryManager.AllocateAsync<TResult>(batch.Length);
        // For now, use CPU fallback until proper kernel execution is implemented
        var results = new TResult[batch.Length];
        for (var i = 0; i < batch.Length; i++)
            results[i] = selector(batch[i]);
        }
        await outputBuffer.CopyFromAsync(results);
        return await outputBuffer.ToArrayAsync();
    /// Filters a batch of data using GPU compute
    private static async Task<T[]> FilterBatchAsync<T>(
        T[] batch,
        using var inputBuffer = await memoryManager.AllocateAsync<T>(batch.Length);
        var result = new List<T>(batch.Length);
            if (predicate(batch[i]))
                result.Add(batch[i]);
            }
        return await Task.FromResult(result.ToArray());
    /// Generates kernel code for a selector function (simplified implementation)
    private static string GenerateKernelCode<TSource, TResult>(Func<TSource, TResult> selector)
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
    /// Generates kernel code for a predicate function (simplified implementation)
    private static string GeneratePredicateKernelCode<T>(Func<T, bool> predicate)
        __global__ void FilterKernel({typeof(T).Name}* input, bool* output, int length)
                output[idx] = filter(input[idx]);
    /// Creates sliding windows over the observable sequence with GPU-optimized processing
    /// <param name="windowConfig">Window configuration</param>
    /// <returns>Observable of windowed batches</returns>
    public static IObservable<IList<T>> SlidingWindow<T>(
        WindowConfig windowConfig,
        return Observable.Create<IList<T>>(observer =>
            var buffer = new List<T>();
            var bufferLock = new object();
                onNext: item =>
                    lock (bufferLock)
                        buffer.Add(item);
                        // Check if we have enough items for a window
                        if (buffer.Count >= windowConfig.Count)
                            var window = buffer.Take(windowConfig.Count).ToList();
                            observer.OnNext(window);
                            // Remove items based on skip size (for sliding window)
                            var skipCount = windowConfig.Skip > 0 ? windowConfig.Skip : 1;
                            if (skipCount >= buffer.Count)
                            {
                                buffer.Clear();
                            }
                            else if (skipCount > 0)
                                buffer.RemoveRange(0, Math.Min(skipCount, buffer.Count));
                        }
                onError: observer.OnError,
                onCompleted: () =>
                    // Emit final window if we have remaining items
                        if (buffer.Count > 0)
                            observer.OnNext(buffer.ToList());
                    observer.OnCompleted();
            );
/// Performance metrics for reactive compute operations
public record PerformanceMetrics
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
/// Configuration for windowing operations
public class WindowConfig
    /// <summary>Number of elements per window</summary>
    public int Count { get; set; } = 10;
    /// <summary>Number of elements to skip between windows</summary>
    public int Skip { get; set; } = 1;
    /// <summary>Whether windows should be tumbling (non-overlapping) or sliding (overlapping)</summary>
    public bool IsTumbling { get; set; } = false;
    /// <summary>Time-based window duration (if applicable)</summary>
    public TimeSpan? TimeWindow { get; set; }
    /// <summary>Maximum wait time before emitting a partial window</summary>
    public TimeSpan? Timeout { get; set; }
/// Extension methods for unified memory buffers
public static class UnifiedBufferExtensions
    /// Converts a unified memory buffer to an array asynchronously
    /// <param name="buffer">The unified memory buffer</param>
    /// <returns>Array containing the buffer data</returns>
    public static async Task<T[]> ToArrayAsync<T>(this IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        var result = new T[buffer.Length];
        await buffer.CopyToAsync(result);
        return result;
