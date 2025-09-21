using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Reactive;
using ReactiveLinq = System.Reactive.Linq.Observable;
using DotCompute.Linq.Reactive;
using DotCompute.Linq.Reactive.Operators;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Linq.Reactive.Examples;
/// <summary>
/// Comprehensive examples demonstrating reactive compute capabilities
/// Shows real-world usage patterns for GPU-accelerated stream processing
/// </summary>
public class ReactiveComputeExamples
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<ReactiveComputeExamples> _logger;
    public ReactiveComputeExamples(
        IComputeOrchestrator orchestrator,
        ILogger<ReactiveComputeExamples> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }
    #region Basic Stream Processing Examples
    /// <summary>
    /// Example 1: Real-time data transformation with GPU acceleration
    /// Demonstrates basic ComputeSelect usage for parallel processing
    /// </summary>
    public async Task Example1_BasicTransformation()
        _logger.LogInformation("Starting Example 1: Basic Stream Transformation");
        // Create a stream of sensor data
        var sensorData = Observable.Interval(TimeSpan.FromMilliseconds(10))
            .Take(10000)
            .Select(i => (float)(Math.Sin(i * 0.1) * 100 + Random.Shared.NextDouble() * 10));
        var config = new ReactiveComputeConfig
        {
            MaxBatchSize = 512,
            MinBatchSize = 64,
            BatchTimeout = TimeSpan.FromMilliseconds(50),
            EnableAdaptiveBatching = true
        };
        // Apply GPU-accelerated transformation
        var processedData = sensorData
            .ComputeSelect(
                value => value * 2.0f + 10.0f, // Normalize and scale
                _orchestrator,
                config)
            .WithPerformanceMonitoring(metrics =>
            {
                _logger.LogInformation(
                    "Processing: {ElementsPerSecond:F1} elements/sec, {TotalElements} total",
                    metrics.ElementsPerSecond, metrics.TotalElements);
            });
        // Collect results
        var results = new List<float>();
        using var subscription = processedData.Subscribe(
            results.Add,
            error => _logger.LogError(error, "Processing error"),
            () => _logger.LogInformation("Processing completed. Processed {Count} elements", results.Count));
        // Wait for processing to complete
        await Task.Delay(2000);
        _logger.LogInformation("Example 1 completed with {Count} processed elements", results.Count);
    /// Example 2: Real-time filtering with GPU acceleration
    /// Shows ComputeWhere usage for parallel filtering
    public async Task Example2_StreamFiltering()
        _logger.LogInformation("Starting Example 2: Stream Filtering");
        // Generate noisy signal data
        var noisySignal = Observable.Interval(TimeSpan.FromMilliseconds(5))
            .Take(5000)
            .Select(i => new SignalPoint
                TimestampTicks = DateTime.UtcNow.AddMilliseconds(i * 5).Ticks,
                Value = (float)(Math.Sin(i * 0.05) + Random.Shared.NextGaussian() * 0.3),
                Quality = Random.Shared.NextSingle()
        var config = new DotCompute.Linq.Reactive.ReactiveComputeConfig
            MaxBatchSize = 256,
            BackpressureStrategy = DotCompute.Linq.Reactive.BackpressureStrategy.DropOldest,
        // Filter out low-quality data points using GPU
        var filteredSignal = noisySignal
            .ComputeWhere(
                point => Math.Abs(point.Value) < 2.0f && point.Quality > 0.7f,
            .WithBackpressure(BackpressureStrategy.Buffer, 1000);
        var filteredResults = new List<SignalPoint>();
        using var subscription = filteredSignal.Subscribe(
            filteredResults.Add,
            error => _logger.LogError(error, "Filtering error"),
            () => _logger.LogInformation("Filtering completed"));
        await Task.Delay(3000);
        _logger.LogInformation(
            "Example 2 completed. Filtered to {FilteredCount} high-quality points",
            filteredResults.Count);
    #endregion
    #region Windowing and Aggregation Examples
    /// Example 3: Real-time windowed aggregations
    /// Demonstrates sliding window operations with GPU acceleration
    public async Task Example3_WindowedAggregation()
        _logger.LogInformation("Starting Example 3: Windowed Aggregation");
        // Generate financial market data
        var marketData = Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Take(1000)
            .Select(i => new MarketTick
                Price = 100.0f + (float)(Math.Sin(i * 0.01) * 10 + Random.Shared.NextGaussian() * 2),
                Volume = Random.Shared.Next(100, 1000),
                Timestamp = DateTime.UtcNow
        var windowConfig = new WindowConfig
            Count = 20,        // 20-tick windows
            // Skip = 10,         // Slide by 10 ticks (commented out if not available)
            IsTumbling = false // Overlapping windows
        // Calculate moving averages and volatility (simplified for demo)
        var movingStats = marketData
            .Buffer(20, 10) // Use Buffer for windowing if SlidingWindow not available
            .Select(window => new MarketStatistics
                AveragePrice = window.Average(tick => tick.Price),
                TotalVolume = window.Sum(tick => tick.Volume),
                Volatility = CalculateVolatility(window.Select(t => t.Price).ToArray()),
                WindowEnd = DateTime.UtcNow
        var statistics = new List<MarketStatistics>();
        using var subscription = movingStats.Subscribe(
            statistics.Add,
            error => _logger.LogError(error, "Aggregation error"),
            () => _logger.LogInformation("Market analysis completed"));
        await Task.Delay(5000);
            "Example 3 completed. Generated {Count} statistical windows",
            statistics.Count);
    /// Example 4: Real-time pattern detection
    /// Shows pattern matching in streaming data
    public async Task Example4_PatternDetection()
        _logger.LogInformation("Starting Example 4: Pattern Detection");
        // Generate sequence data
        var sequenceData = Observable.Interval(TimeSpan.FromMilliseconds(50))
            .Take(2000)
            .Select(i => (int)(i % 10)); // Repeating pattern 0-9
        // Define pattern to detect (ascending sequence)
        var pattern = new[] { 1, 2, 3, 4, 5 };
        // Detect patterns in the stream (simplified pattern detection for demo)
        var patternMatches = sequenceData
            .Buffer(pattern.Length, 1) // Sliding window the size of pattern
            .Select((window, index) => new { Window = window, Index = index })
            .Where(x => x.Window.Count() == pattern.Length && x.Window.SequenceEqual(pattern))
            .Select(x => new PatternMatch<int>
                StartIndex = x.Index,
                Pattern = pattern,
                Confidence = 1.0f // Full confidence for exact matches
        var matches = new List<PatternMatch<int>>();
        using var subscription = patternMatches.Subscribe(
            match =>
                matches.Add(match);
                    "Pattern detected at index {Index} with confidence {Confidence:F2}",
                    match.StartIndex, match.Confidence);
            },
            error => _logger.LogError(error, "Pattern detection error"),
            () => _logger.LogInformation("Pattern detection completed"));
        await Task.Delay(4000);
            "Example 4 completed. Found {Count} pattern matches",
            matches.Count);
    #region Advanced Pipeline Examples
    /// Example 5: Complex multi-stage streaming pipeline
    /// Demonstrates end-to-end pipeline with multiple processing stages
    public async Task Example5_ComplexPipeline()
        _logger.LogInformation("Starting Example 5: Complex Streaming Pipeline");
        var pipelineConfig = new StreamingPipelineConfig
            Name = "SensorProcessingPipeline",
            MaxCapacity = 10000,
            EnableAutoRecovery = true,
            EnableMetrics = true,
            HealthCheckInterval = TimeSpan.FromSeconds(5)
        // Create a complex processing pipeline
        using var pipeline = StreamingPipelineBuilder
            .Create<RawSensorData>(_orchestrator, pipelineConfig)
            .AddFilterStage<RawSensorData>("QualityFilter", data => data.Quality > 0.8f)
            .AddComputeStage<RawSensorData, CalibratedSensorData>("Calibration", data => new CalibratedSensorData
                Value = data.Value * data.CalibrationFactor + data.Offset,
                Timestamp = data.Timestamp,
                SensorId = data.SensorId
            })
            .AddWindowAggregationStage<CalibratedSensorData, AggregatedSensorData>("MovingAverage",
                new WindowConfig { Count = 10 },
                window => new AggregatedSensorData
                {
                    AverageValue = window.Any() ? window.Average(d => d.Value) : 0f,
                    MaxValue = window.Any() ? window.Max(d => d.Value) : 0f,
                    MinValue = window.Any() ? window.Min(d => d.Value) : 0f,
                    WindowEnd = DateTime.UtcNow,
                    SampleCount = window.Count()
                });
        // Monitor pipeline metrics
        var metricsSubscription = pipeline.Metrics.Subscribe(metricsObj =>
            // Cast to expected metrics type or use dynamic
            if (metricsObj is IDictionary<string, object> metricsDict)
                var throughput = metricsDict.TryGetValue("Throughput", out var t) ? Convert.ToDouble(t) : 0.0;
                var health = metricsDict.TryGetValue("Health", out var h) ? h?.ToString() : "Unknown";
                var memoryUsage = metricsDict.TryGetValue("MemoryUsage", out var m) ? Convert.ToInt64(m) : 0L;
                    "Pipeline '{Name}': {Throughput:F1} elem/sec, Health: {Health}, Memory: {Memory:F1}MB",
                    pipelineConfig.Name,
                    throughput,
                    health,
                    memoryUsage / (1024.0 * 1024.0));
            }
            else
                    "Pipeline '{Name}': Metrics received",
                    pipelineConfig.Name);
        });
        // Collect pipeline output
        var aggregatedData = new List<AggregatedSensorData>();
        var outputSubscription = pipeline.Output.Subscribe(
            data =>
                if (data is AggregatedSensorData aggregated)
                    aggregatedData.Add(aggregated);
                }
            error => _logger.LogError(error, "Pipeline processing error"));
        // Start the pipeline
        using var pipelineExecution = pipeline.Start();
        // Generate and feed sensor data
        var dataGenerator = GenerateSensorData();
        var inputSubscription = dataGenerator.Subscribe(
            data => pipeline.Input.OnNext(data),
            error => pipeline.Input.OnError(error),
            () => pipeline.Input.OnCompleted());
        // Let the pipeline run
        await Task.Delay(10000);
        // Cleanup
        inputSubscription.Dispose();
        outputSubscription.Dispose();
        metricsSubscription.Dispose();
            "Example 5 completed. Pipeline processed {Count} aggregated results",
            aggregatedData.Count);
    /// Example 6: Time-series analysis with out-of-order handling
    /// Shows advanced time-series processing capabilities
    public async Task Example6_TimeSeriesAnalysis()
        _logger.LogInformation("Starting Example 6: Time-Series Analysis");
        var timeSeriesConfig = new TimeSeriesConfig
            TimestampSelector = data => ((TimeSeriesPoint)data).Timestamp,
            MaxOutOfOrderDelay = TimeSpan.FromSeconds(5),
            WatermarkInterval = TimeSpan.FromSeconds(1),
            LateDataStrategy = LateDataStrategy.Recompute
        // Generate out-of-order time series data
        var timeSeriesData = GenerateOutOfOrderTimeSeriesData();
        // Handle out-of-order events and compute statistics (simplified for demo)
        var orderedData = timeSeriesData
            .Buffer(100) // Buffer elements for sorting
            .SelectMany(buffer => buffer.OrderBy(point => point.TimestampTicks));
            Duration = TimeSpan.FromSeconds(10)
            // Hop = TimeSpan.FromSeconds(5) // Commented out if not available
        // Compute real-time statistics (simplified for demo)
        var statistics = orderedData
            .Buffer(100) // Window of 100 points
            .Select(window =>
                if (window.Count() == 0)
                    return new TimeSeriesStatistics
                    { Sum = 0, Average = 0, StandardDeviation = 0, Timestamp = DateTime.UtcNow };
                var sum = window.Sum(p => p.Value);
                var average = window.Average(p => p.Value);
                var stdDev = Math.Sqrt(window.Average(p => Math.Pow(p.Value - average, 2)));
                return new TimeSeriesStatistics
                    Sum = sum,
                    Average = average,
                    StandardDeviation = stdDev,
                    Timestamp = DateTime.UtcNow
                };
        var results = new List<TimeSeriesStatistics>();
        using var subscription = statistics.Subscribe(
            error => _logger.LogError(error, "Time series analysis error"),
            () => _logger.LogInformation("Time series analysis completed"));
        await Task.Delay(15000);
            "Example 6 completed. Generated {Count} statistical summaries",
            results.Count);
    #region Helper Methods and Data Structures
    private static float CalculateVolatility(float[] prices)
        if (prices.Length < 2)
            return 0;
        }
        var mean = prices.Average();
        var variance = prices.Average(p => Math.Pow(p - mean, 2));
        return (float)Math.Sqrt(variance);
    private IObservable<RawSensorData> GenerateSensorData()
        return Observable.Interval(TimeSpan.FromMilliseconds(10))
            .Select(i => new RawSensorData
                SensorId = $"SENSOR_{i % 10:D3}",
                Value = (float)(50 + Math.Sin(i * 0.01) * 20 + Random.Shared.NextGaussian() * 5),
                Quality = 0.7f + Random.Shared.NextSingle() * 0.3f,
                CalibrationFactor = 1.0f + Random.Shared.NextSingle() * 0.1f,
                Offset = Random.Shared.NextSingle() * 2 - 1,
    private IObservable<TimeSeriesPoint> GenerateOutOfOrderTimeSeriesData()
        var baseTime = DateTime.UtcNow;
        var random = new Random();
        return Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Select(i =>
                // Occasionally generate out-of-order data
                var timeOffset = random.NextDouble() < 0.1
                    ? TimeSpan.FromMilliseconds(-random.Next(1000, 5000))
                    : TimeSpan.Zero;
                return new TimeSeriesPoint
                    Value = (float)(Math.Sin(i * 0.02) * 10 + random.NextGaussian() * 2),
                    TimestampTicks = baseTime.AddMilliseconds(i * 100).Add(timeOffset).Ticks
}
#region Data Models
public struct SignalPoint
    public long TimestampTicks { get; init; }
    public float Value { get; init; }
    public float Quality { get; init; }
    public DateTime Timestamp => new(TimestampTicks);
public struct MarketTick
    public float Price { get; init; }
    public int Volume { get; init; }
    public DateTime Timestamp { get; init; }
    public long TimestampTicks => Timestamp.Ticks;
public record MarketStatistics
    public float AveragePrice { get; init; }
    public int TotalVolume { get; init; }
    public float Volatility { get; init; }
    public DateTime WindowEnd { get; init; }
public record RawSensorData
    public string SensorId { get; init; } = "";
    public float CalibrationFactor { get; init; }
    public float Offset { get; init; }
public record CalibratedSensorData
public record AggregatedSensorData
    public float AverageValue { get; init; }
    public float MaxValue { get; init; }
    public float MinValue { get; init; }
    public int SampleCount { get; init; }
public struct TimeSeriesPoint
public record TimeSeriesStatistics
    public float Sum { get; init; }
    public double Average { get; init; }
    public double StandardDeviation { get; init; }
public record PatternMatch<T>
    public int StartIndex { get; init; }
    public T[] Pattern { get; init; } = Array.Empty<T>();
    public float Confidence { get; init; }
#endregion
/// Extension for generating Gaussian random numbers
public static class RandomExtensions
    public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
        // Box-Muller transform
        static double Sample(Random rng)
            var u1 = 1.0 - rng.NextDouble();
            var u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * Sample(random);
/// Demo configuration and placeholder types for reactive compute examples
public record ReactiveComputeConfig
    public int MaxBatchSize { get; init; } = 512;
    public int MinBatchSize { get; init; } = 64;
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromMilliseconds(50);
    public bool EnableAdaptiveBatching { get; init; } = true;
public record WindowConfig
    public int Count { get; init; }
    public bool IsTumbling { get; init; }
    public TimeSpan Duration { get; init; }
public enum BackpressureStrategy
    DropOldest,
    Buffer,
    Block
public record StreamingPipelineConfig
    public string Name { get; init; } = "";
    public int MaxCapacity { get; init; }
    public bool EnableAutoRecovery { get; init; }
    public bool EnableMetrics { get; init; }
    public TimeSpan HealthCheckInterval { get; init; }
public record TimeSeriesConfig
    public Func<object, DateTime> TimestampSelector { get; init; } = _ => DateTime.UtcNow;
    public TimeSpan MaxOutOfOrderDelay { get; init; }
    public TimeSpan WatermarkInterval { get; init; }
    public LateDataStrategy LateDataStrategy { get; init; }
public enum LateDataStrategy
    Drop,
    Recompute,
    Buffer
/// Mock streaming pipeline builder for demo purposes
public static class StreamingPipelineBuilder
    public static MockStreamingPipeline<T> Create<T>(IComputeOrchestrator orchestrator, StreamingPipelineConfig config)
        return new MockStreamingPipeline<T>();
public class MockStreamingPipeline<T> : IDisposable
    public IObservable<object> Metrics => Observable.Empty<object>();
    public IObservable<object> Output => Observable.Empty<object>();
    public IObserver<T> Input => Observer.Create<T>(_ => { });
    public MockStreamingPipeline<T> AddFilterStage<TInput>(string name, Func<TInput, bool> filter)
        => this;
    public MockStreamingPipeline<T> AddComputeStage<TInput, TOutput>(string name, Func<TInput, TOutput> compute)
    public MockStreamingPipeline<T> AddWindowAggregationStage<TInput, TOutput>(
        string name,
        WindowConfig config,
        Func<IEnumerable<TInput>, TOutput> aggregator)
    public IDisposable Start() => Disposable.Empty;
    public void Dispose() { }
/// Extension methods for reactive compute demo
public static class ReactiveComputeDemoExtensions
    public static IObservable<T> ComputeSelect<T>(
        this IObservable<T> source,
        Func<T, T> selector,
        ReactiveComputeConfig config)
        return source.Select(selector); // Simplified for demo
    public static IObservable<T> ComputeWhere<T>(
        Func<T, bool> predicate,
        return source.Where(predicate); // Simplified for demo
    public static IObservable<T> WithPerformanceMonitoring<T>(
        Action<object> onMetrics)
        return source; // Simplified - no monitoring for demo
    public static IObservable<T> WithBackpressure<T>(
        BackpressureStrategy strategy,
        int bufferSize)
        return source; // Simplified - no backpressure handling for demo
