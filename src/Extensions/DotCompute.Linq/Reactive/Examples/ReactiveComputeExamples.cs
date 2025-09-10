using System.Reactive.Linq;
using System.Reactive.Subjects;
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
    {
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
    }

    /// <summary>
    /// Example 2: Real-time filtering with GPU acceleration
    /// Shows ComputeWhere usage for parallel filtering
    /// </summary>
    public async Task Example2_StreamFiltering()
    {
        _logger.LogInformation("Starting Example 2: Stream Filtering");

        // Generate noisy signal data
        var noisySignal = Observable.Interval(TimeSpan.FromMilliseconds(5))
            .Take(5000)
            .Select(i => new SignalPoint
            {
                Timestamp = DateTime.UtcNow.AddMilliseconds(i * 5),
                Value = (float)(Math.Sin(i * 0.05) + Random.Shared.NextGaussian() * 0.3),
                Quality = Random.Shared.NextSingle()
            });

        var config = new ReactiveComputeConfig
        {
            MaxBatchSize = 256,
            BackpressureStrategy = BackpressureStrategy.DropOldest,
            EnableAdaptiveBatching = true
        };

        // Filter out low-quality data points using GPU
        var filteredSignal = noisySignal
            .ComputeWhere(
                point => Math.Abs(point.Value) < 2.0f && point.Quality > 0.7f,
                _orchestrator,
                config)
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
    }

    #endregion

    #region Windowing and Aggregation Examples

    /// <summary>
    /// Example 3: Real-time windowed aggregations
    /// Demonstrates sliding window operations with GPU acceleration
    /// </summary>
    public async Task Example3_WindowedAggregation()
    {
        _logger.LogInformation("Starting Example 3: Windowed Aggregation");

        // Generate financial market data
        var marketData = Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Take(1000)
            .Select(i => new MarketTick
            {
                Price = 100.0f + (float)(Math.Sin(i * 0.01) * 10 + Random.Shared.NextGaussian() * 2),
                Volume = Random.Shared.Next(100, 1000),
                Timestamp = DateTime.UtcNow
            });

        var windowConfig = new WindowConfig
        {
            Count = 20,        // 20-tick windows
            Skip = 10,         // Slide by 10 ticks
            IsTumbling = false // Overlapping windows
        };

        // Calculate moving averages and volatility
        var movingStats = marketData
            .SlidingWindow(windowConfig, _orchestrator)
            .Select(window => new MarketStatistics
            {
                AveragePrice = window.Average(tick => tick.Price),
                TotalVolume = window.Sum(tick => tick.Volume),
                Volatility = CalculateVolatility(window.Select(t => t.Price).ToArray()),
                WindowEnd = DateTime.UtcNow
            });

        var statistics = new List<MarketStatistics>();
        using var subscription = movingStats.Subscribe(
            statistics.Add,
            error => _logger.LogError(error, "Aggregation error"),
            () => _logger.LogInformation("Market analysis completed"));

        await Task.Delay(5000);

        _logger.LogInformation(
            "Example 3 completed. Generated {Count} statistical windows",
            statistics.Count);
    }

    /// <summary>
    /// Example 4: Real-time pattern detection
    /// Shows pattern matching in streaming data
    /// </summary>
    public async Task Example4_PatternDetection()
    {
        _logger.LogInformation("Starting Example 4: Pattern Detection");

        // Generate sequence data
        var sequenceData = Observable.Interval(TimeSpan.FromMilliseconds(50))
            .Take(2000)
            .Select(i => (int)(i % 10)); // Repeating pattern 0-9

        // Define pattern to detect (ascending sequence)
        var pattern = new[] { 1, 2, 3, 4, 5 };

        // Detect patterns in the stream
        var patternMatches = sequenceData
            .DetectPatterns(pattern, _orchestrator)
            .WithPerformanceMonitoring(metrics =>
            {
                _logger.LogDebug("Pattern detection rate: {Rate:F1}/sec", metrics.ElementsPerSecond);
            });

        var matches = new List<PatternMatch<int>>();
        using var subscription = patternMatches.Subscribe(
            match =>
            {
                matches.Add(match);
                _logger.LogInformation(
                    "Pattern detected at index {Index} with confidence {Confidence:F2}",
                    match.StartIndex, match.Confidence);
            },
            error => _logger.LogError(error, "Pattern detection error"),
            () => _logger.LogInformation("Pattern detection completed"));

        await Task.Delay(4000);

        _logger.LogInformation(
            "Example 4 completed. Found {Count} pattern matches",
            matches.Count);
    }

    #endregion

    #region Advanced Pipeline Examples

    /// <summary>
    /// Example 5: Complex multi-stage streaming pipeline
    /// Demonstrates end-to-end pipeline with multiple processing stages
    /// </summary>
    public async Task Example5_ComplexPipeline()
    {
        _logger.LogInformation("Starting Example 5: Complex Streaming Pipeline");

        var pipelineConfig = new StreamingPipelineConfig
        {
            Name = "SensorProcessingPipeline",
            MaxCapacity = 10000,
            EnableAutoRecovery = true,
            EnableMetrics = true,
            HealthCheckInterval = TimeSpan.FromSeconds(5)
        };

        // Create a complex processing pipeline
        using var pipeline = StreamingPipelineBuilder
            .Create<RawSensorData>(_orchestrator, pipelineConfig)
            .AddFilterStage("QualityFilter", data => data.Quality > 0.8f)
            .AddComputeStage("Calibration", data => new CalibratedSensorData
            {
                Value = data.Value * data.CalibrationFactor + data.Offset,
                Timestamp = data.Timestamp,
                SensorId = data.SensorId
            })
            .AddWindowAggregationStage("MovingAverage", 
                new WindowConfig { Count = 10 },
                window => new AggregatedSensorData
                {
                    AverageValue = window.Average(d => d.Value),
                    MaxValue = window.Max(d => d.Value),
                    MinValue = window.Min(d => d.Value),
                    WindowEnd = DateTime.UtcNow,
                    SampleCount = window.Count
                });

        // Monitor pipeline metrics
        var metricsSubscription = pipeline.Metrics.Subscribe(metrics =>
        {
            _logger.LogInformation(
                "Pipeline '{Name}': {Throughput:F1} elem/sec, Health: {Health}, Memory: {Memory:F1}MB",
                pipelineConfig.Name, metrics.Throughput, metrics.Health, 
                metrics.MemoryUsage / (1024.0 * 1024.0));
        });

        // Collect pipeline output
        var aggregatedData = new List<AggregatedSensorData>();
        var outputSubscription = pipeline.Output.Subscribe(
            aggregatedData.Add,
            error => _logger.LogError(error, "Pipeline processing error"),
            () => _logger.LogInformation("Pipeline processing completed"));

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

        _logger.LogInformation(
            "Example 5 completed. Pipeline processed {Count} aggregated results",
            aggregatedData.Count);
    }

    /// <summary>
    /// Example 6: Time-series analysis with out-of-order handling
    /// Shows advanced time-series processing capabilities
    /// </summary>
    public async Task Example6_TimeSeriesAnalysis()
    {
        _logger.LogInformation("Starting Example 6: Time-Series Analysis");

        var timeSeriesConfig = new TimeSeriesConfig
        {
            TimestampSelector = data => ((TimeSeriesPoint)data).Timestamp,
            MaxOutOfOrderDelay = TimeSpan.FromSeconds(5),
            WatermarkInterval = TimeSpan.FromSeconds(1),
            LateDataStrategy = LateDataStrategy.Recompute
        };

        // Generate out-of-order time series data
        var timeSeriesData = GenerateOutOfOrderTimeSeriesData();

        // Handle out-of-order events and compute statistics
        var orderedData = timeSeriesData
            .HandleOutOfOrder(timeSeriesConfig)
            .Cast<TimeSeriesPoint>();

        var windowConfig = new WindowConfig
        {
            Duration = TimeSpan.FromSeconds(10),
            Hop = TimeSpan.FromSeconds(5)
        };

        // Compute real-time statistics
        var statistics = orderedData
            .ComputeSum(windowConfig, _orchestrator)
            .Zip(orderedData.ComputeAverage(windowConfig, _orchestrator),
                 orderedData.ComputeStandardDeviation(windowConfig, _orchestrator),
                 (sum, avg, stdDev) => new TimeSeriesStatistics
                 {
                     Sum = sum,
                     Average = avg,
                     StandardDeviation = stdDev,
                     Timestamp = DateTime.UtcNow
                 });

        var results = new List<TimeSeriesStatistics>();
        using var subscription = statistics.Subscribe(
            results.Add,
            error => _logger.LogError(error, "Time series analysis error"),
            () => _logger.LogInformation("Time series analysis completed"));

        await Task.Delay(15000);

        _logger.LogInformation(
            "Example 6 completed. Generated {Count} statistical summaries",
            results.Count);
    }

    #endregion

    #region Helper Methods and Data Structures

    private static float CalculateVolatility(float[] prices)
    {
        if (prices.Length < 2) return 0;
        
        var mean = prices.Average();
        var variance = prices.Average(p => Math.Pow(p - mean, 2));
        return (float)Math.Sqrt(variance);
    }

    private IObservable<RawSensorData> GenerateSensorData()
    {
        return Observable.Interval(TimeSpan.FromMilliseconds(10))
            .Take(5000)
            .Select(i => new RawSensorData
            {
                SensorId = $"SENSOR_{i % 10:D3}",
                Value = (float)(50 + Math.Sin(i * 0.01) * 20 + Random.Shared.NextGaussian() * 5),
                Quality = 0.7f + Random.Shared.NextSingle() * 0.3f,
                CalibrationFactor = 1.0f + Random.Shared.NextSingle() * 0.1f,
                Offset = Random.Shared.NextSingle() * 2 - 1,
                Timestamp = DateTime.UtcNow
            });
    }

    private IObservable<TimeSeriesPoint> GenerateOutOfOrderTimeSeriesData()
    {
        var baseTime = DateTime.UtcNow;
        var random = new Random();
        
        return Observable.Interval(TimeSpan.FromMilliseconds(100))
            .Take(1000)
            .Select(i =>
            {
                // Occasionally generate out-of-order data
                var timeOffset = random.NextDouble() < 0.1 
                    ? TimeSpan.FromMilliseconds(-random.Next(1000, 5000))
                    : TimeSpan.Zero;
                
                return new TimeSeriesPoint
                {
                    Value = (float)(Math.Sin(i * 0.02) * 10 + random.NextGaussian() * 2),
                    Timestamp = baseTime.AddMilliseconds(i * 100).Add(timeOffset)
                };
            });
    }

    #endregion
}

#region Data Models

public record SignalPoint
{
    public DateTime Timestamp { get; init; }
    public float Value { get; init; }
    public float Quality { get; init; }
}

public record MarketTick
{
    public float Price { get; init; }
    public int Volume { get; init; }
    public DateTime Timestamp { get; init; }
}

public record MarketStatistics
{
    public float AveragePrice { get; init; }
    public int TotalVolume { get; init; }
    public float Volatility { get; init; }
    public DateTime WindowEnd { get; init; }
}

public record RawSensorData
{
    public string SensorId { get; init; } = "";
    public float Value { get; init; }
    public float Quality { get; init; }
    public float CalibrationFactor { get; init; }
    public float Offset { get; init; }
    public DateTime Timestamp { get; init; }
}

public record CalibratedSensorData
{
    public string SensorId { get; init; } = "";
    public float Value { get; init; }
    public DateTime Timestamp { get; init; }
}

public record AggregatedSensorData
{
    public float AverageValue { get; init; }
    public float MaxValue { get; init; }
    public float MinValue { get; init; }
    public DateTime WindowEnd { get; init; }
    public int SampleCount { get; init; }
}

public record TimeSeriesPoint
{
    public float Value { get; init; }
    public DateTime Timestamp { get; init; }
}

public record TimeSeriesStatistics
{
    public float Sum { get; init; }
    public double Average { get; init; }
    public double StandardDeviation { get; init; }
    public DateTime Timestamp { get; init; }
}

#endregion

/// <summary>
/// Extension for generating Gaussian random numbers
/// </summary>
public static class RandomExtensions
{
    public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
    {
        // Box-Muller transform
        static double Sample(Random rng)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = 1.0 - rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
        
        return mean + stdDev * Sample(random);
    }
}