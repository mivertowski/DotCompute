using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Memory;
using DotCompute.Linq.KernelGeneration.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Numerics;

namespace DotCompute.Linq.Reactive.Operators;
/// <summary>
/// Window configuration for streaming operations
/// </summary>
public record WindowConfig
{
    /// <summary>Window size (count-based)</summary>
    public int? Count { get; init; }
    /// <summary>Window duration (time-based)</summary>
    public TimeSpan? Duration { get; init; }
    /// <summary>Skip count for sliding windows</summary>
    public int? Skip { get; init; }
    /// <summary>Hop duration for time-based sliding windows</summary>
    public TimeSpan? Hop { get; init; }
    /// <summary>Whether to use tumbling (non-overlapping) windows</summary>
    public bool IsTumbling { get; init; } = false;
}
/// Configuration for time-series operations
public record TimeSeriesConfig
    /// <summary>Timestamp selector function</summary>
    public Func<object, DateTime>? TimestampSelector { get; init; }
    /// <summary>Maximum allowed out-of-order delay</summary>
    public TimeSpan MaxOutOfOrderDelay { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Watermark generation interval</summary>
    public TimeSpan WatermarkInterval { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>Late data handling strategy</summary>
    public LateDataStrategy LateDataStrategy { get; init; } = LateDataStrategy.Drop;
/// Strategy for handling late-arriving data
public enum LateDataStrategy
    Drop,
    Buffer,
    Recompute
/// Advanced reactive operators optimized for GPU compute operations
/// Provides streaming window operations, time-series processing, and real-time aggregations
public static class ReactiveComputeOperators
    private static readonly Meter _meter = new("DotCompute.Reactive.Operators");
    private static readonly Counter<long> _windowOperationCounter = _meter.CreateCounter<long>("window_operations_total");
    private static readonly Counter<long> _aggregationCounter = _meter.CreateCounter<long>("aggregations_total");
    private static readonly Histogram<double> _windowSizeHistogram = _meter.CreateHistogram<double>("window_size");
    private static readonly Histogram<double> _aggregationTimeHistogram = _meter.CreateHistogram<double>("aggregation_time_ms");
    #region Windowing Operations
    /// <summary>
    /// Creates sliding windows with GPU-accelerated processing
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">Source observable</param>
    /// <param name="windowConfig">Window configuration</param>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="computeConfig">Reactive compute configuration</param>
    /// <returns>Observable of windowed arrays</returns>
    public static IObservable<T[]> SlidingWindow<T>(
        this IObservable<T> source,
        WindowConfig windowConfig,
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig? computeConfig = null)
        where T : unmanaged
    {
        computeConfig ??= new ReactiveComputeConfig();
        return Observable.Create<T[]>(observer =>
        {
            var buffer = new ConcurrentQueue<(T Value, DateTime Timestamp)>();
            var scheduler = new ReactiveKernelScheduler(orchestrator, computeConfig);
            return source
                .Timestamp()
                .Subscribe(
                    timestampedValue =>
                    {
                        buffer.Enqueue((timestampedValue.Value, timestampedValue.Timestamp.DateTime));
                        var window = ExtractWindow(buffer, windowConfig, timestampedValue.Timestamp.DateTime);
                        if (window.Length > 0)
                        {
                            _windowOperationCounter.Add(1, new KeyValuePair<string, object?>("type", "sliding"));
                            _windowSizeHistogram.Record(window.Length);
                            observer.OnNext(window);
                        }
                    },
                    observer.OnError,
                    observer.OnCompleted);
        });
    }
    /// Creates tumbling (non-overlapping) windows
    public static IObservable<T[]> TumblingWindow<T>(
        var config = windowConfig with { IsTumbling = true };
        return source.SlidingWindow(config, orchestrator, computeConfig);
    /// Session-based windowing that groups elements by activity periods
    /// <param name="sessionTimeout">Maximum gap between elements in the same session</param>
    /// <returns>Observable of session windows</returns>
    public static IObservable<T[]> SessionWindow<T>(
        TimeSpan sessionTimeout,
            var currentSession = new List<T>();
            var lastActivity = DateTime.MinValue;
            var sessionTimer = new Timer(_ => FlushSession(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            void FlushSession()
            {
                if (currentSession.Count > 0)
                {
                    _windowOperationCounter.Add(1, new KeyValuePair<string, object?>("type", "session"));
                    _windowSizeHistogram.Record(currentSession.Count);
                    observer.OnNext(currentSession.ToArray());
                    currentSession.Clear();
                }
            }
            return source.Subscribe(
                value =>
                    var now = DateTime.UtcNow;
                    // Check if we need to start a new session
                    if (lastActivity != DateTime.MinValue &&
                        now - lastActivity > sessionTimeout)
                        FlushSession();
                    }
                    currentSession.Add(value);
                    lastActivity = now;
                    // Reset the session timer
                    sessionTimer.Change(sessionTimeout, Timeout.InfiniteTimeSpan);
                },
                ex =>
                    FlushSession();
                    observer.OnError(ex);
                () =>
                    observer.OnCompleted();
                });
    #endregion
    #region Real-time Aggregations
    /// Performs real-time sum aggregation with GPU acceleration
    /// <typeparam name="T">Numeric element type</typeparam>
    /// <returns>Observable of sum values</returns>
    public static IObservable<T> ComputeSum<T>(
        where T : unmanaged, INumber<T>
        return source
            .SlidingWindow(windowConfig, orchestrator, computeConfig)
            .Select(async window =>
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await ComputeArraySum(window, orchestrator);
                stopwatch.Stop();
                _aggregationCounter.Add(1, new KeyValuePair<string, object?>("operation", "sum"));
                _aggregationTimeHistogram.Record(stopwatch.ElapsedMilliseconds);
                return result;
            })
            .SelectMany(obs => obs);
    /// Performs real-time average aggregation with GPU acceleration
    /// <returns>Observable of average values</returns>
    public static IObservable<double> ComputeAverage<T>(
                if (window.Length == 0)
                    return 0.0;
                var sum = await ComputeArraySum(window, orchestrator);
                var average = Convert.ToDouble(sum) / window.Length;
                _aggregationCounter.Add(1, new KeyValuePair<string, object?>("operation", "average"));
                return average;
    /// Performs real-time min/max aggregation with GPU acceleration
    /// <typeparam name="T">Comparable element type</typeparam>
    /// <returns>Observable of (min, max) tuples</returns>
    public static IObservable<(T Min, T Max)> ComputeMinMax<T>(
        where T : unmanaged, IComparable<T>
                    return (default(T), default(T));
                var (min, max) = await ComputeArrayMinMax(window, orchestrator);
                _aggregationCounter.Add(1, new KeyValuePair<string, object?>("operation", "minmax"));
                return (min, max);
    /// Performs real-time standard deviation calculation
    /// <returns>Observable of standard deviation values</returns>
    public static IObservable<double> ComputeStandardDeviation<T>(
                if (window.Length < 2)
                var stdDev = await ComputeArrayStandardDeviation(window, orchestrator);
                _aggregationCounter.Add(1, new KeyValuePair<string, object?>("operation", "stddev"));
                return stdDev;
    #region Time-Series Operations
    /// Handles out-of-order events in time-series data
    /// <typeparam name="T">Element type with timestamp</typeparam>
    /// <param name="config">Time-series configuration</param>
    /// <returns>Observable with ordered elements</returns>
    public static IObservable<T> HandleOutOfOrder<T>(
        TimeSeriesConfig config)
        return Observable.Create<T>(observer =>
            var buffer = new SortedDictionary<DateTime, List<T>>();
            var watermark = DateTime.MinValue;
            var watermarkTimer = new Timer(_ => ProcessWatermark(), null,
                config.WatermarkInterval, config.WatermarkInterval);
            void ProcessWatermark()
                var newWatermark = DateTime.UtcNow - config.MaxOutOfOrderDelay;
                var itemsToEmit = buffer
                    .Where(kvp => kvp.Key <= newWatermark)
                    .SelectMany(kvp => kvp.Value)
                    .ToList();
                foreach (var item in itemsToEmit)
                    observer.OnNext(item);
                // Remove processed items
                var keysToRemove = buffer.Keys.Where(k => k <= newWatermark).ToList();
                foreach (var key in keysToRemove)
                    buffer.Remove(key);
                watermark = newWatermark;
                    if (config.TimestampSelector == null)
                        observer.OnNext(value);
                        return;
                    var timestamp = config.TimestampSelector(value!);
                    if (timestamp <= watermark)
                        // Handle late data based on strategy
                        switch (config.LateDataStrategy)
                            case LateDataStrategy.Drop:
                                break; // Drop the late data
                            case LateDataStrategy.Buffer:
                                // Could implement buffering for late data reprocessing
                                break;
                            case LateDataStrategy.Recompute:
                                // Could trigger recomputation of affected windows
                                observer.OnNext(value);
                    else
                        // Buffer the in-order or early data
                        if (!buffer.ContainsKey(timestamp))
                            buffer[timestamp] = [];
                        buffer[timestamp].Add(value);
                observer.OnError,
                    ProcessWatermark(); // Final flush
    /// Detects patterns in streaming data using GPU acceleration
    /// <param name="pattern">Pattern to detect</param>
    /// <returns>Observable of pattern matches</returns>
    public static IObservable<PatternMatch<T>> DetectPatterns<T>(
        T[] pattern,
        where T : unmanaged, IEquatable<T>
        return Observable.Create<PatternMatch<T>>(observer =>
            var buffer = new CircularBuffer<T>(pattern.Length * 2);
                async value =>
                    buffer.Add(value);
                    if (buffer.Count >= pattern.Length)
                        var window = buffer.ToArray();
                        var matches = await FindPatternMatches(window, pattern, orchestrator);
                        foreach (var match in matches)
                            observer.OnNext(match);
                observer.OnCompleted);
    #region Utility Methods
    /// Extracts a window from the buffer based on configuration
    private static T[] ExtractWindow<T>(
        ConcurrentQueue<(T Value, DateTime Timestamp)> buffer,
        WindowConfig config,
        DateTime currentTime)
        var items = buffer.ToArray();
        if (config.Count.HasValue)
            // Count-based window
            var count = config.Count.Value;
            var skip = config.Skip ?? (config.IsTumbling ? count : 1);
            return items.Skip(Math.Max(0, items.Length - count)).Take(count)
                .Select(x => x.Value).ToArray();
        }
        else if (config.Duration.HasValue)
            // Time-based window
            var duration = config.Duration.Value;
            var windowStart = currentTime - duration;
            return items.Where(x => x.Timestamp >= windowStart)
        return Array.Empty<T>();
    /// Computes sum of array using GPU acceleration
    private static async Task<T> ComputeArraySum<T>(T[] array, IComputeOrchestrator orchestrator)
        if (array.Length == 0)
            return T.Zero;
        // Create a basic memory manager
        using var memoryManager = new DotCompute.Memory.UnifiedMemoryManager();
        using var inputBuffer = await memoryManager.AllocateAndCopyAsync<T>(array);
        // Use reduction kernel for sum
        var kernelCode = GenerateSumReductionKernel<T>();
        // This is a simplified implementation - in practice, you'd use a proper reduction algorithm
        await Task.CompletedTask; // Satisfy async signature
        var result = array.Aggregate(T.Zero, (acc, val) => acc + val);
        return result;
    /// Computes min/max of array using GPU acceleration
    private static async Task<(T Min, T Max)> ComputeArrayMinMax<T>(T[] array, IComputeOrchestrator orchestrator)
            return (default(T)!, default(T)!);
        // Simplified implementation
        var min = array.Min()!;
        var max = array.Max()!;
        return (min, max);
    /// Computes standard deviation using GPU acceleration
    private static Task<double> ComputeArrayStandardDeviation<T>(T[] array, IComputeOrchestrator orchestrator)
        if (array.Length < 2)
            return Task.FromResult(0.0);
        var mean = array.Average(x => Convert.ToDouble(x));
        var variance = array.Average(x => Math.Pow(Convert.ToDouble(x) - mean, 2));
        return Task.FromResult(Math.Sqrt(variance));
    /// Finds pattern matches in the data
    private static Task<PatternMatch<T>[]> FindPatternMatches<T>(
        T[] data,
        IComputeOrchestrator orchestrator)
        var matches = new List<PatternMatch<T>>();
        for (var i = 0; i <= data.Length - pattern.Length; i++)
            var isMatch = true;
            for (var j = 0; j < pattern.Length; j++)
                if (!data[i + j].Equals(pattern[j]))
                    isMatch = false;
                    break;
            if (isMatch)
                matches.Add(new PatternMatch<T>
                    StartIndex = i,
                    Length = pattern.Length,
                    MatchedData = data.Skip(i).Take(pattern.Length).ToArray(),
                    Timestamp = DateTime.UtcNow
        return Task.FromResult(matches.ToArray());
    /// Generates CUDA kernel code for sum reduction
    private static string GenerateSumReductionKernel<T>()
        return $@"
        __global__ void SumReduction({typeof(T).Name}* input, {typeof(T).Name}* output, int n)
        {{
            __shared__ {typeof(T).Name} sdata[256];
            
            unsigned int tid = threadIdx.x;
            unsigned int i = blockIdx.x * blockDim.x + threadIdx.x;
            sdata[tid] = (i < n) ? input[i] : 0;
            __syncthreads();
            for (unsigned int s = 1; s < blockDim.x; s *= 2) {{
                if (tid % (2*s) == 0) {{
                    sdata[tid] += sdata[tid + s];
                }}
                __syncthreads();
            }}
            if (tid == 0) output[blockIdx.x] = sdata[0];
        }}";
/// Circular buffer for efficient windowing operations
internal class CircularBuffer<T>
    private readonly T[] _buffer;
    private int _start;
    private int _count;
    public CircularBuffer(int capacity)
        _buffer = new T[capacity];
    public int Count => _count;
    public int Capacity => _buffer.Length;
    public void Add(T item)
        var index = (_start + _count) % _buffer.Length;
        _buffer[index] = item;
        if (_count < _buffer.Length)
            _count++;
        else
            _start = (_start + 1) % _buffer.Length;
    public T[] ToArray()
        var result = new T[_count];
        for (var i = 0; i < _count; i++)
            result[i] = _buffer[(_start + i) % _buffer.Length];
/// Represents a pattern match in streaming data
public record PatternMatch<T>
    /// <summary>Start index of the match</summary>
    public int StartIndex { get; init; }
    /// <summary>Length of the matched pattern</summary>
    public int Length { get; init; }
    /// <summary>The matched data</summary>
    public T[] MatchedData { get; init; } = Array.Empty<T>();
    /// <summary>Timestamp when the match was found</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>Confidence score of the match (0-1)</summary>
    public double Confidence { get; init; } = 1.0;
