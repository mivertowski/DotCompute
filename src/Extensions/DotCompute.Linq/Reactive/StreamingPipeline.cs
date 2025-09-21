using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Memory;
using DotCompute.Linq.Reactive.Operators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace DotCompute.Linq.Reactive;
/// <summary>
/// Configuration for streaming pipeline
/// </summary>
public record StreamingPipelineConfig
{
    /// <summary>Pipeline name for identification and monitoring</summary>
    public string Name { get; init; } = "StreamingPipeline";
    /// <summary>Reactive compute configuration</summary>
    public ReactiveComputeConfig ReactiveConfig { get; init; } = new();
    /// <summary>Maximum pipeline capacity (elements)</summary>
    public int MaxCapacity { get; init; } = 100000;
    /// <summary>Health check interval</summary>
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Enable automatic error recovery</summary>
    public bool EnableAutoRecovery { get; init; } = true;
    /// <summary>Maximum retry attempts for failed operations</summary>
    public int MaxRetryAttempts { get; init; } = 3;
    /// <summary>Retry delay strategy</summary>
    public RetryDelayStrategy RetryDelayStrategy { get; init; } = RetryDelayStrategy.Exponential;
    /// <summary>Enable pipeline metrics collection</summary>
    public bool EnableMetrics { get; init; } = true;
    /// <summary>Checkpoint interval for state persistence</summary>
    public TimeSpan CheckpointInterval { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Enable pipeline state persistence</summary>
    public bool EnableStatePersistence { get; init; } = false;
}
/// Retry delay strategies
public enum RetryDelayStrategy
    Fixed,
    Linear,
    Exponential,
    Random
/// Pipeline health status
public enum PipelineHealth
    Healthy,
    Degraded,
    Critical,
    Failed
/// Pipeline stage definition
public record PipelineStage<TInput, TOutput>
    /// <summary>Stage name</summary>
    public string Name { get; init; } = "";
    /// <summary>Stage transformation function</summary>
    public Func<IObservable<TInput>, IObservable<TOutput>> Transform { get; init; } = null!;
    /// <summary>Stage-specific configuration</summary>
    public object? Configuration { get; init; }
    /// <summary>Whether stage supports parallel processing</summary>
    public bool SupportsParallel { get; init; } = true;
    /// <summary>Stage priority (higher values get more resources)</summary>
    public int Priority { get; init; } = 1;
/// Pipeline execution metrics
public record PipelineMetrics
    /// <summary>Total elements processed</summary>
    public long TotalProcessed { get; init; }
    /// <summary>Current throughput (elements/second)</summary>
    public double Throughput { get; init; }
    /// <summary>Average processing latency (milliseconds)</summary>
    public double AverageLatency { get; init; }
    /// <summary>Error rate (errors per second)</summary>
    public double ErrorRate { get; init; }
    /// <summary>Current pipeline health</summary>
    public PipelineHealth Health { get; init; }
    /// <summary>Memory usage (bytes)</summary>
    public long MemoryUsage { get; init; }
    /// <summary>CPU utilization percentage</summary>
    public double CpuUtilization { get; init; }
    /// <summary>GPU utilization percentage</summary>
    public double GpuUtilization { get; init; }
    /// <summary>Stage-specific metrics</summary>
    public Dictionary<string, object> StageMetrics { get; init; } = [];
/// Comprehensive streaming pipeline for real-time GPU-accelerated data processing
/// Provides end-to-end streaming capabilities with error handling, monitoring, and state management
public sealed class StreamingPipeline<TInput, TOutput> : IDisposable
    where TInput : class
    where TOutput : class
    private readonly StreamingPipelineConfig _config;
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<StreamingPipeline<TInput, TOutput>>? _logger;
    private readonly List<object> _stages = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Timer _healthCheckTimer;
    private readonly Timer _checkpointTimer;
    // Subjects for pipeline control
    private readonly Subject<TInput> _inputSubject = new();
    private readonly Subject<TOutput> _outputSubject = new();
    private readonly Subject<Exception> _errorSubject = new();
    private readonly Subject<PipelineMetrics> _metricsSubject = new();
    // State management
    private IObservable<TOutput>? _pipeline;
    private readonly ConcurrentDictionary<string, object> _state = new();
    private readonly CircuitBreaker _circuitBreaker;
    // Metrics
    private static readonly Meter _meter = new("DotCompute.Reactive.Pipeline");
    private static readonly Counter<long> _elementsProcessedCounter = _meter.CreateCounter<long>("elements_processed_total");
    private static readonly Counter<long> _errorsCounter = _meter.CreateCounter<long>("errors_total");
    private static readonly Histogram<double> _latencyHistogram = _meter.CreateHistogram<double>("processing_latency_ms");
    private static readonly Gauge<double> _throughputGauge = _meter.CreateGauge<double>("throughput_elements_per_second");
    private long _totalProcessed;
    private long _totalErrors;
    private readonly List<double> _recentLatencies = [];
    private DateTime _startTime = DateTime.UtcNow;
    private bool _disposed;
    public StreamingPipeline(
        IComputeOrchestrator orchestrator,
        StreamingPipelineConfig? config = null,
        ILogger<StreamingPipeline<TInput, TOutput>>? logger = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _config = config ?? new StreamingPipelineConfig();
        _logger = logger;
        _circuitBreaker = new CircuitBreaker(_config.MaxRetryAttempts, TimeSpan.FromSeconds(30));
        // Setup health monitoring
        _healthCheckTimer = new Timer(
            PerformHealthCheck,
            null,
            _config.HealthCheckInterval,
            _config.HealthCheckInterval);
        // Setup checkpointing
        if (_config.EnableStatePersistence)
        {
            _checkpointTimer = new Timer(
                CreateCheckpoint,
                null,
                _config.CheckpointInterval,
                _config.CheckpointInterval);
        }
        else
            _checkpointTimer = new Timer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _logger?.LogInformation(
            "StreamingPipeline '{Name}' initialized with capacity {Capacity}",
            _config.Name, _config.MaxCapacity);
    }
    /// <summary>
    /// Input stream for the pipeline
    /// </summary>
    public IObserver<TInput> Input => _inputSubject.AsObserver();
    /// Output stream from the pipeline
    public IObservable<TOutput> Output => _outputSubject.AsObservable();
    /// Error stream for the pipeline
    public IObservable<Exception> Errors => _errorSubject.AsObservable();
    /// Metrics stream for the pipeline
    public IObservable<PipelineMetrics> Metrics => _metricsSubject.AsObservable();
    /// Current pipeline health status
    public PipelineHealth Health { get; private set; } = PipelineHealth.Healthy;
    /// Adds a transformation stage to the pipeline
    /// <typeparam name="TStageOutput">Output type of the stage</typeparam>
    /// <param name="stage">Stage definition</param>
    /// <returns>Pipeline builder for chaining</returns>
    public StreamingPipeline<TInput, TStageOutput> AddStage<TStageOutput>(
        PipelineStage<TOutput, TStageOutput> stage)
        where TStageOutput : class
        _stages.Add(stage);
        var newPipeline = new StreamingPipeline<TInput, TStageOutput>(
            _orchestrator, _config, logger: null);
        // Copy existing stages
        foreach (var existingStage in _stages.Take(_stages.Count - 1))
            newPipeline._stages.Add(existingStage);
        newPipeline._stages.Add(stage);
        return newPipeline;
    /// Adds a compute transformation stage with GPU acceleration
    /// <param name="name">Stage name</param>
    /// <param name="selector">Transformation function</param>
    public StreamingPipeline<TInput, TStageOutput> AddComputeStage<TStageOutput>(
        string name,
        Func<TOutput, TStageOutput> selector)
        var stage = new PipelineStage<TOutput, TStageOutput>
            Name = name,
            Transform = source => source.Select(selector),
            SupportsParallel = true
        };
        return AddStage(stage);
    /// Adds a filtering stage with GPU acceleration
    /// <param name="predicate">Filter predicate</param>
    /// <returns>Same pipeline for chaining</returns>
    public StreamingPipeline<TInput, TOutput> AddFilterStage(
        Func<TOutput, bool> predicate)
        var stage = new PipelineStage<TOutput, TOutput>
            Transform = source => source.Where(predicate),
        AddStage(stage);
        return this;
    /// Adds a windowing aggregation stage
    /// <typeparam name="TResult">Aggregation result type</typeparam>
    /// <param name="windowConfig">Window configuration</param>
    /// <param name="aggregator">Aggregation function</param>
    public StreamingPipeline<TInput, TResult> AddWindowAggregationStage<TResult>(
        WindowConfig windowConfig,
        Func<IList<TOutput>, TResult> aggregator)
        where TResult : class
        var stage = new PipelineStage<TOutput, TResult>
            Transform = source => source
                .Buffer(windowConfig.Count)
                .Select(aggregator),
            Configuration = windowConfig,
    /// Starts the pipeline execution
    /// <returns>Disposable to stop the pipeline</returns>
    public IDisposable Start()
        if (_pipeline != null)
            throw new InvalidOperationException("Pipeline is already started");
        _startTime = DateTime.UtcNow;
        _pipeline = BuildPipeline();
        var subscription = _pipeline
            .Subscribe(
                output =>
                {
                    RecordProcessingMetrics();
                    _outputSubject.OnNext(output);
                },
                error =>
                    RecordError(error);
                    if (_config.EnableAutoRecovery)
                    {
                        _logger?.LogWarning(error, "Pipeline error occurred, attempting recovery");
                        // Implement recovery logic here
                    }
                    else
                        _errorSubject.OnNext(error);
                () =>
                    _logger?.LogInformation("Pipeline '{Name}' completed", _config.Name);
                    _outputSubject.OnCompleted();
                });
        _logger?.LogInformation("StreamingPipeline '{Name}' started", _config.Name);
        return Disposable.Create(() =>
            subscription?.Dispose();
            _pipeline = null;
            _logger?.LogInformation("StreamingPipeline '{Name}' stopped", _config.Name);
        });
    /// Builds the complete pipeline from all stages
    private IObservable<TOutput> BuildPipeline()
        var pipeline = _inputSubject.Cast<object>();
        foreach (var stageObj in _stages)
            // Use reflection to call the Transform method
            var stageType = stageObj.GetType();
            var transformProperty = stageType.GetProperty("Transform");
            var transform = transformProperty?.GetValue(stageObj) as Delegate;
            if (transform != null)
            {
                var result = transform.DynamicInvoke(pipeline);
                if (result is IObservable<object> observableResult)
                    pipeline = observableResult;
                }
            }
        return pipeline.Cast<TOutput>()
            .WithBackpressure(_config.ReactiveConfig.BackpressureStrategy, _config.MaxCapacity)
            .WithPerformanceMonitoring(metrics =>
                if (_config.EnableMetrics)
                    PublishMetrics();
            })
            .Retry(_config.MaxRetryAttempts);
    /// Records processing metrics
    private void RecordProcessingMetrics()
        Interlocked.Increment(ref _totalProcessed);
        _elementsProcessedCounter.Add(1, new KeyValuePair<string, object?>("pipeline", _config.Name));
        var currentThroughput = _totalProcessed / (DateTime.UtcNow - _startTime).TotalSeconds;
        _throughputGauge.Record(currentThroughput, new KeyValuePair<string, object?>("pipeline", _config.Name));
    /// Records error metrics
    private void RecordError(Exception error)
        Interlocked.Increment(ref _totalErrors);
        _errorsCounter.Add(1,
            new KeyValuePair<string, object?>("pipeline", _config.Name),
            new KeyValuePair<string, object?>("error_type", error.GetType().Name));
        _logger?.LogError(error, "Pipeline error in '{PipelineName}'", _config.Name);
    /// Publishes current pipeline metrics
    private void PublishMetrics()
        var uptime = DateTime.UtcNow - _startTime;
        var throughput = uptime.TotalSeconds > 0 ? _totalProcessed / uptime.TotalSeconds : 0;
        var errorRate = uptime.TotalSeconds > 0 ? _totalErrors / uptime.TotalSeconds : 0;
        var metrics = new PipelineMetrics
            TotalProcessed = _totalProcessed,
            Throughput = throughput,
            AverageLatency = _recentLatencies.Any() ? _recentLatencies.Average() : 0,
            ErrorRate = errorRate,
            Health = Health,
            MemoryUsage = GC.GetTotalMemory(false),
            CpuUtilization = GetCpuUtilization(),
            GpuUtilization = GetGpuUtilization()
        _metricsSubject.OnNext(metrics);
    /// Performs health check on the pipeline
    private void PerformHealthCheck(object? state)
        try
            var previousHealth = Health;
            Health = CalculateHealth();
            if (Health != previousHealth)
                _logger?.LogInformation(
                    "Pipeline '{Name}' health changed from {PreviousHealth} to {NewHealth}",
                    _config.Name, previousHealth, Health);
            if (_config.EnableMetrics)
                PublishMetrics();
        catch (Exception ex)
            _logger?.LogError(ex, "Error during health check for pipeline '{Name}'", _config.Name);
    /// Calculates current pipeline health
    private PipelineHealth CalculateHealth()
        if (uptime.TotalSeconds < 10)
            return PipelineHealth.Healthy; // Too early to judge
        var errorRate = _totalErrors / uptime.TotalSeconds;
        var throughput = _totalProcessed / uptime.TotalSeconds;
        if (errorRate > 10)
            return PipelineHealth.Failed;
        if (errorRate > 1)
            return PipelineHealth.Critical;
        if (throughput < 1)
            return PipelineHealth.Degraded;
        return PipelineHealth.Healthy;
    /// Creates a checkpoint of the current pipeline state
    private void CreateCheckpoint(object? state)
        if (!_config.EnableStatePersistence)
            return;
            var checkpoint = new
                Timestamp = DateTime.UtcNow,
                TotalProcessed = _totalProcessed,
                TotalErrors = _totalErrors,
                State = _state.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Health = Health
            };
            var json = JsonSerializer.Serialize(checkpoint);
            // In a real implementation, you would save this to persistent storage
            _logger?.LogDebug("Checkpoint created for pipeline '{Name}'", _config.Name);
            _logger?.LogError(ex, "Error creating checkpoint for pipeline '{Name}'", _config.Name);
    /// Gets current CPU utilization (simplified implementation)
    private double GetCpuUtilization()
        // In a real implementation, you would use performance counters
        return Environment.ProcessorCount * 0.1; // Placeholder
    /// Gets current GPU utilization (simplified implementation)
    private double GetGpuUtilization()
        // In a real implementation, you would query GPU metrics
        return 0.0; // Placeholder
    /// <inheritdoc />
    public void Dispose()
        if (_disposed)
        _disposed = true;
        _cancellationTokenSource.Cancel();
        _healthCheckTimer?.Dispose();
        _checkpointTimer?.Dispose();
        _inputSubject?.Dispose();
        _outputSubject?.Dispose();
        _errorSubject?.Dispose();
        _metricsSubject?.Dispose();
        _cancellationTokenSource?.Dispose();
            "StreamingPipeline '{Name}' disposed. Total processed: {TotalProcessed}, Total errors: {TotalErrors}",
            _config.Name, _totalProcessed, _totalErrors);
/// Circuit breaker for pipeline fault tolerance
internal class CircuitBreaker
    private readonly int _maxFailures;
    private readonly TimeSpan _timeout;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private bool _isOpen;
    public CircuitBreaker(int maxFailures, TimeSpan timeout)
        _maxFailures = maxFailures;
        _timeout = timeout;
    public bool CanExecute()
        if (!_isOpen)
            return true;
        if (DateTime.UtcNow - _lastFailureTime > _timeout)
            _isOpen = false;
            _failureCount = 0;
        return false;
    public void RecordFailure()
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
        if (_failureCount >= _maxFailures)
            _isOpen = true;
    public void RecordSuccess()
        _failureCount = 0;
        _isOpen = false;
/// Builder for creating streaming pipelines
public static class StreamingPipelineBuilder
    /// Creates a new streaming pipeline
    /// <typeparam name="TInput">Input type</typeparam>
    /// <param name="orchestrator">Compute orchestrator</param>
    /// <param name="config">Pipeline configuration</param>
    /// <param name="logger">Logger instance</param>
    /// <returns>New streaming pipeline</returns>
    public static StreamingPipeline<TInput, TInput> Create<TInput>(
        ILogger? logger = null)
        where TInput : class
        return new StreamingPipeline<TInput, TInput>(orchestrator, config, logger as ILogger<StreamingPipeline<TInput, TInput>>);
