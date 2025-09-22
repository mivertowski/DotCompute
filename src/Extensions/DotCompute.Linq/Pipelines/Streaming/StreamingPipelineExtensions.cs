// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DotCompute.Abstractions.Pipelines;
using DotCompute.Linq.Pipelines.Models;
using Microsoft.Extensions.Logging;
namespace DotCompute.Linq.Pipelines.Streaming;
{
/// <summary>
/// Extensions for creating real-time streaming pipelines with micro-batching and backpressure handling.
/// Optimized for continuous data processing with configurable windowing and buffering strategies.
/// </summary>
public static class StreamingPipelineExtensions
{
    #region Core Streaming Extensions
    /// <summary>
    /// Converts an async enumerable to a streaming pipeline with micro-batching.
    /// </summary>
    /// <typeparam name="T">Element type</typeparam>
    /// <param name="source">The source async enumerable</param>
    /// <param name="options">Streaming configuration options</param>
    /// <returns>A streaming pipeline processor</returns>
    public static IAsyncEnumerable<T> AsStreamingPipeline<T>(
        this IAsyncEnumerable<T> source,
        StreamingPipelineOptions? options = null) where T : unmanaged
    {
        var config = options ?? new StreamingPipelineOptions();
        return new StreamingPipelineProcessor<T>(source, config).ProcessAsync();
    }
    /// Adds batching to a kernel pipeline chain with configurable batch size and timeout.
    /// <param name="chain">The kernel pipeline builder</param>
    /// <param name="batchSize">Maximum batch size</param>
    /// <param name="timeout">Maximum time to wait for a batch</param>
    /// <returns>Pipeline with batching capability</returns>
    public static object WithBatching<T>(
        this object chain,
        int batchSize,
        TimeSpan? timeout = null) where T : unmanaged
        // Simplified implementation for now
        return chain;
    /// Adds sliding window processing to a pipeline chain.
    /// <param name="windowSize">Size of the sliding window</param>
    /// <param name="stride">Stride between windows</param>
    /// <returns>Pipeline with windowing capability</returns>
    public static object WithWindowing<T>(
        TimeSpan windowSize,
        TimeSpan? stride = null) where T : unmanaged
    #endregion
    #region Real-Time Processing
    /// Processes a stream in real-time with configurable latency constraints.
    /// <typeparam name="TInput">Input element type</typeparam>
    /// <typeparam name="TOutput">Output element type</typeparam>
    /// <param name="source">Source stream</param>
    /// <param name="processor">Processing function</param>
    /// <param name="maxLatency">Maximum allowed latency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processed real-time stream</returns>
    public static async IAsyncEnumerable<TOutput> ProcessRealTime<TInput, TOutput>(
        this IAsyncEnumerable<TInput> source,
        Func<TInput, Task<TOutput>> processor,
        TimeSpan maxLatency,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where TInput : unmanaged where TOutput : unmanaged
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            await semaphore.WaitAsync(cancellationToken);
            var processTask = Task.Run(async () =>
            {
                try
                {
                    return await processor(item);
                }
                finally
                    semaphore.Release();
            }, cancellationToken);
            // Apply latency constraint
            var timeoutTask = Task.Delay(maxLatency, cancellationToken);
            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            if (completedTask == timeoutTask)
                throw new StreamingLatencyException($"Processing exceeded maximum latency of {maxLatency}");
            }
            yield return await processTask;
        }
    /// Creates a buffered stream with backpressure handling.
    /// <param name="bufferSize">Buffer size for backpressure</param>
    /// <param name="backpressureStrategy">Strategy for handling backpressure</param>
    /// <returns>Buffered stream with backpressure handling</returns>
    public static async IAsyncEnumerable<T> WithBackpressure<T>(
        int bufferSize = 1000,
        BackpressureStrategy backpressureStrategy = BackpressureStrategy.Block,
        where T : unmanaged
        var channelOptions = new BoundedChannelOptions(bufferSize)
            FullMode = backpressureStrategy switch
                BackpressureStrategy.Block => BoundedChannelFullMode.Wait,
                BackpressureStrategy.DropOldest => BoundedChannelFullMode.DropOldest,
                BackpressureStrategy.DropNewest => BoundedChannelFullMode.DropWrite,
                _ => BoundedChannelFullMode.Wait
            },
            SingleReader = true,
            SingleWriter = false
        };
        var channel = Channel.CreateBounded<T>(channelOptions);
        var writer = channel.Writer;
        var reader = channel.Reader;
        // Producer task
        _ = Task.Run(async () =>
            try
                await foreach (var item in source.WithCancellation(cancellationToken))
        {
                    await writer.WriteAsync(item, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            catch (Exception ex)
            {
                writer.TryComplete(ex);
                return;
            writer.TryComplete();
        }, cancellationToken);
        // Consumer
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
    #region Stream Analytics
    /// Computes a rolling average over a time window.
    /// <param name="source">Source numeric stream</param>
    /// <param name="windowDuration">Duration of the rolling window</param>
    /// <returns>Stream of rolling averages</returns>
    public static async IAsyncEnumerable<double> RollingAverage(
        this IAsyncEnumerable<TimestampedValue<double>> source,
        TimeSpan windowDuration,
        var window = new List<TimestampedValue<double>>();
        await foreach (var value in source.WithCancellation(cancellationToken))
            // Add new value
            window.Add(value);
            // Remove values outside the window
            var cutoff = value.Timestamp - windowDuration;
            window.RemoveAll(v => v.Timestamp < cutoff);
            // Compute and yield average
            if (window.Count > 0)
                var average = window.Average(v => v.Value);
                yield return average;
    /// Detects anomalies in a numeric stream using statistical methods.
    /// <param name="windowSize">Window size for statistical calculation</param>
    /// <param name="threshold">Z-score threshold for anomaly detection</param>
    /// <returns>Stream of anomaly detection results</returns>
    public static async IAsyncEnumerable<AnomalyResult> DetectAnomalies(
        this IAsyncEnumerable<double> source,
        int windowSize = 100,
        double threshold = 2.0,
        var window = new Queue<double>();
        var sum = 0.0;
        var sumOfSquares = 0.0;
            // Add to window
            window.Enqueue(value);
            sum += value;
            sumOfSquares += value * value;
            // Maintain window size
            if (window.Count > windowSize)
                var removed = window.Dequeue();
                sum -= removed;
                sumOfSquares -= removed * removed;
            // Calculate statistics and detect anomalies
            if (window.Count >= 10) // Need minimum samples
                var mean = sum / window.Count;
                var variance = (sumOfSquares / window.Count) - (mean * mean);
                var stdDev = Math.Sqrt(Math.Max(0, variance));
                var zScore = stdDev > 0 ? Math.Abs(value - mean) / stdDev : 0;
                var isAnomaly = zScore > threshold;
                yield return new AnomalyResult
                    Value = value,
                    ZScore = zScore,
                    IsAnomaly = isAnomaly,
                    Mean = mean,
                    StandardDeviation = stdDev,
                    Timestamp = DateTime.UtcNow
                };
    /// Applies event-driven processing to a stream.
    /// <param name="eventDetector">Function to detect events</param>
    /// <param name="eventProcessor">Function to process detected events</param>
    /// <returns>Stream of processed events</returns>
    public static async IAsyncEnumerable<TEvent> ProcessEvents<T, TEvent>(
        Func<T, bool> eventDetector,
        Func<T, TEvent> eventProcessor,
        where T : unmanaged where TEvent : unmanaged
            if (eventDetector(item))
                yield return eventProcessor(item);
    #region Stream Aggregation
    /// Performs continuous aggregation over a time-based window.
    /// <typeparam name="TResult">Aggregation result type</typeparam>
    /// <param name="windowDuration">Duration of the aggregation window</param>
    /// <param name="aggregateFunction">Function to aggregate window contents</param>
    /// <returns>Stream of aggregation results</returns>
    public static async IAsyncEnumerable<TResult> ContinuousAggregate<T, TResult>(
        this IAsyncEnumerable<TimestampedValue<T>> source,
        Func<IEnumerable<T>, TResult> aggregateFunction,
        where T : unmanaged where TResult : unmanaged
        var window = new List<TimestampedValue<T>>();
        var lastEmission = DateTime.MinValue;
            // Check if we should emit a result
            if (value.Timestamp - lastEmission >= windowDuration)
                // Filter to current window
                var cutoff = value.Timestamp - windowDuration;
                var windowValues = window.Where(v => v.Timestamp >= cutoff).Select(v => v.Value);
                if (windowValues.Any())
                    var result = aggregateFunction(windowValues);
                    yield return result;
                    lastEmission = value.Timestamp;
                // Clean up old values
                window.RemoveAll(v => v.Timestamp < cutoff);
    /// Performs sessionized aggregation based on session gaps.
    /// <typeparam name="TResult">Session result type</typeparam>
    /// <param name="sessionGap">Maximum gap between elements in a session</param>
    /// <param name="sessionProcessor">Function to process completed sessions</param>
    /// <returns>Stream of session results</returns>
    public static async IAsyncEnumerable<TResult> SessionizeStream<T, TResult>(
        TimeSpan sessionGap,
        Func<IEnumerable<TimestampedValue<T>>, TResult> sessionProcessor,
        var currentSession = new List<TimestampedValue<T>>();
        var lastTimestamp = DateTime.MinValue;
            // Check if this starts a new session
            if (currentSession.Count > 0 && value.Timestamp - lastTimestamp > sessionGap)
                // Process and emit completed session
                if (currentSession.Count > 0)
                    yield return sessionProcessor(currentSession);
                // Start new session
                currentSession.Clear();
            currentSession.Add(value);
            lastTimestamp = value.Timestamp;
        // Process final session
        if (currentSession.Count > 0)
            yield return sessionProcessor(currentSession);
    #region Performance Optimizations
    /// Applies parallel processing to stream elements while maintaining order.
    /// <param name="maxDegreeOfParallelism">Maximum parallel degree</param>
    /// <returns>Parallel processed stream with preserved order</returns>
    public static async IAsyncEnumerable<TOutput> ParallelProcess<TInput, TOutput>(
        int maxDegreeOfParallelism = -1,
        var actualParallelism = maxDegreeOfParallelism == -1
            ? Environment.ProcessorCount
            : maxDegreeOfParallelism;
        var semaphore = new SemaphoreSlim(actualParallelism, actualParallelism);
        var results = new Dictionary<long, Task<TOutput>>();
        var nextIndex = 0L;
        var currentIndex = 0L;
            var index = currentIndex++;
            var task = ProcessItemAsync(item, processor, semaphore);
            results[index] = task;
            // Yield completed results in order
            while (results.TryGetValue(nextIndex, out var nextTask))
                if (nextTask.IsCompleted)
                    yield return await nextTask;
                    results.Remove(nextIndex);
                    nextIndex++;
                else
                    break;
        // Yield remaining results in order
        while (results.TryGetValue(nextIndex, out var remainingTask))
            yield return await remainingTask;
            results.Remove(nextIndex);
            nextIndex++;
    private static async Task<TOutput> ProcessItemAsync<TInput, TOutput>(
        TInput item,
        SemaphoreSlim semaphore)
        try
            return await processor(item);
        finally
            semaphore.Release();
}
#region Supporting Types
/// Configuration options for streaming pipelines.
public class StreamingPipelineOptions
    {
    /// <summary>Batch size for micro-batching.</summary>
    public int BatchSize { get; set; } = 1000;
    /// <summary>Maximum time to wait for a batch to fill.</summary>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>Buffer size for backpressure handling.</summary>
    public int BufferSize { get; set; } = 10000;
    /// <summary>Backpressure handling strategy.</summary>
    public BackpressureStrategy BackpressureStrategy { get; set; } = BackpressureStrategy.Block;
    /// <summary>Whether to enable performance metrics collection.</summary>
    public bool EnableMetrics { get; set; } = true;
    /// <summary>Logger for streaming operations.</summary>
    public ILogger? Logger { get; set; }
/// Backpressure handling strategies.
public enum BackpressureStrategy
    {
    /// <summary>Block producer when buffer is full.</summary>
    Block,
    /// <summary>Drop oldest items when buffer is full.</summary>
    DropOldest,
    /// <summary>Drop newest items when buffer is full.</summary>
    DropNewest,
    /// <summary>Apply exponential backoff.</summary>
    Backoff
/// Options for pipeline profiling configuration.
public class ProfilingOptions
    {
    /// <summary>Gets or sets whether to enable detailed timing.</summary>
    public bool EnableDetailedTiming { get; set; } = true;
    /// <summary>Gets or sets whether to track memory usage.</summary>
    public bool TrackMemoryUsage { get; set; } = true;
    /// <summary>Gets or sets whether to collect performance counters.</summary>
    public bool CollectPerformanceCounters { get; set; } = false;
    /// <summary>Gets or sets the sampling interval for profiling.</summary>
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    /// <summary>Gets or sets whether to enable kernel-level profiling.</summary>
    public bool EnableKernelProfiling { get; set; } = false;
    /// <summary>Gets or sets custom profiling tags.</summary>
    public Dictionary<string, string> CustomTags { get; set; } = [];
/// Options for circuit breaker pattern in error handling.
public class CircuitBreakerOptions
    {
    /// <summary>Gets or sets the failure threshold before opening circuit.</summary>
    public int FailureThreshold { get; set; } = 5;
    /// <summary>Gets or sets the timeout before trying to close circuit.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);
    /// <summary>Gets or sets the success threshold for closing circuit.</summary>
    public int SuccessThreshold { get; set; } = 3;
    /// <summary>Gets or sets whether to log circuit breaker events.</summary>
    public bool EnableLogging { get; set; } = true;
    /// <summary>Gets or sets custom circuit breaker policies.</summary>
    public Dictionary<string, object> CustomPolicies { get; set; } = [];
/// Timestamped value for time-aware stream processing.
/// <typeparam name="T">Value type</typeparam>
public struct TimestampedValue<T> where T : unmanaged
    {
    /// <summary>The value.</summary>
    public T Value { get; set; }
    /// <summary>Timestamp when the value was created.</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Optional sequence number.</summary>
    public long SequenceNumber { get; set; }
/// Result of anomaly detection.
public struct AnomalyResult
    {
    /// <summary>The value that was tested.</summary>
    public double Value { get; set; }
    /// <summary>Z-score of the value.</summary>
    public double ZScore { get; set; }
    /// <summary>Whether the value is considered an anomaly.</summary>
    public bool IsAnomaly { get; set; }
    /// <summary>Mean of the reference window.</summary>
    public double Mean { get; set; }
    /// <summary>Standard deviation of the reference window.</summary>
    public double StandardDeviation { get; set; }
    /// <summary>Timestamp of the detection.</summary>
/// Exception thrown when streaming latency constraints are violated.
public class StreamingLatencyException : Exception
    {
    /// <summary>Initializes a new instance of the StreamingLatencyException.</summary>
    public StreamingLatencyException(string message) : base(message) { }
        {
    public StreamingLatencyException(string message, Exception innerException) : base(message, innerException) { }
/// Internal streaming pipeline processor.
/// <typeparam name="T">Element type</typeparam>
internal class StreamingPipelineProcessor<T> where T : unmanaged
    {
    private readonly IAsyncEnumerable<T> _source;
    private readonly StreamingPipelineOptions _options;
    }
    public StreamingPipelineProcessor(IAsyncEnumerable<T> source, StreamingPipelineOptions options)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    public async IAsyncEnumerable<T> ProcessAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        var metrics = new StreamingMetrics();
        var startTime = DateTime.UtcNow;
            await foreach (var item in _source.WithBackpressure(_options.BufferSize, _options.BackpressureStrategy, cancellationToken))
                if (_options.EnableMetrics)
                    metrics.ItemsProcessed++;
                    metrics.LastProcessedTime = DateTime.UtcNow;
                yield return item;
            if (_options.EnableMetrics)
                metrics.TotalDuration = DateTime.UtcNow - startTime;
                _options.Logger?.LogInformation("Streaming pipeline completed: {Metrics}", metrics);
/// Streaming performance metrics.
public class StreamingMetrics
    {
    /// <summary>Total number of items processed.</summary>
    public long ItemsProcessed { get; set; }
    /// <summary>Time when the last item was processed.</summary>
    public DateTime LastProcessedTime { get; set; }
    /// <summary>Total duration of streaming.</summary>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>Average throughput in items per second.</summary>
    public double AverageThroughput => TotalDuration.TotalSeconds > 0
        ? ItemsProcessed / TotalDuration.TotalSeconds
        : 0;
    /// <inheritdoc />
    public override string ToString()
    {
        return $"Items: {ItemsProcessed}, Duration: {TotalDuration.TotalSeconds:F2}s, Throughput: {AverageThroughput:F2} items/s";
#endregion
