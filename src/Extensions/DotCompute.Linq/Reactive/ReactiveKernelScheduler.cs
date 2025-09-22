using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Collections.Concurrent;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions;
using DotCompute.Memory;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace DotCompute.Linq.Reactive;
{
/// <summary>
/// Adaptive batch sizing metrics
/// </summary>
public record BatchMetrics
{
    public int BatchSize { get; init; }
    public double ProcessingTimeMs { get; init; }
    public double ThroughputElementsPerSecond { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public double GpuUtilization { get; init; }
    public long MemoryUsed { get; init; }
}
/// Custom scheduler optimized for GPU kernel execution with adaptive batching
/// Handles batch processing, resource management, and performance optimization
public sealed class ReactiveKernelScheduler : IScheduler, IDisposable
    {
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ReactiveComputeConfig _config;
    private readonly ILogger? _logger;
    private readonly Timer _batchTimer;
    private readonly ConcurrentQueue<BatchMetrics> _performanceHistory;
    private readonly SemaphoreSlim _resourceSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    // Adaptive batching state
    private volatile int _currentOptimalBatchSize;
    private double _lastThroughput;
    private DateTime _lastBatchFlush = DateTime.UtcNow;
    private volatile bool _shouldFlush;
    // Metrics
    private static readonly Meter _meter = new("DotCompute.Reactive.Scheduler");
    private static readonly Counter<long> _batchCounter = _meter.CreateCounter<long>("batches_processed_total");
    private static readonly Histogram<double> _batchLatencyHistogram = _meter.CreateHistogram<double>("batch_latency_ms");
    private static readonly Gauge<int> _currentBatchSizeGauge = _meter.CreateGauge<int>("current_batch_size");
    private static readonly Gauge<double> _throughputGauge = _meter.CreateGauge<double>("throughput_elements_per_second");
    // Resource tracking
    private readonly ConcurrentDictionary<object, UnifiedBuffer<byte>> _bufferPool;
    private long _totalMemoryAllocated;
    private volatile bool _disposed;
    public ReactiveKernelScheduler(
        {
        IComputeOrchestrator orchestrator,
        ReactiveComputeConfig config,
        ILogger? logger = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _currentOptimalBatchSize = config.MinBatchSize;
        _performanceHistory = new ConcurrentQueue<BatchMetrics>();
        _resourceSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
        _cancellationTokenSource = new CancellationTokenSource();
        _bufferPool = new ConcurrentDictionary<object, UnifiedBuffer<byte>>();
        // Timer for periodic batch flushing
        _batchTimer = new Timer(
            FlushPendingBatches,
            null,
            config.BatchTimeout,
            config.BatchTimeout);
        // Start adaptive optimization if enabled
        if (config.EnableAdaptiveBatching)
        {
            _ = Task.Run(AdaptiveBatchOptimizationLoop, _cancellationTokenSource.Token);
        }
        _logger?.LogInformation(
            "ReactiveKernelScheduler initialized with batch size {MinSize}-{MaxSize}, timeout {Timeout}ms",
            config.MinBatchSize, config.MaxBatchSize, config.BatchTimeout.TotalMilliseconds);
    }
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    /// <summary>
    /// Indicates whether pending batches should be flushed
    /// </summary>
    public bool ShouldFlushBatch() => _shouldFlush ||
        (DateTime.UtcNow - _lastBatchFlush) > _config.BatchTimeout;
    /// Gets the current optimal batch size for performance
    public int CurrentOptimalBatchSize => _currentOptimalBatchSize;
    /// Gets current scheduler performance metrics
    public SchedulerPerformanceMetrics GetPerformanceMetrics()
        var recentMetrics = _performanceHistory
            .Where(m => m.Timestamp > DateTime.UtcNow.AddSeconds(-10))
            .ToArray();
        return new SchedulerPerformanceMetrics
            AverageThroughput = recentMetrics.Any() ? recentMetrics.Average(m => m.ThroughputElementsPerSecond) : 0,
            AverageLatency = recentMetrics.Any() ? recentMetrics.Average(m => m.ProcessingTimeMs) : 0,
            OptimalBatchSize = _currentOptimalBatchSize,
            TotalMemoryAllocated = _totalMemoryAllocated,
            ActiveBuffers = _bufferPool.Count
        };
    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        return Schedule(state, TimeSpan.Zero, action);
    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
        if (_disposed)
            return Disposable.Empty;
        return Schedule(state, Now.Add(dueTime), action);
    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
        var delay = dueTime - Now;
        if (delay <= TimeSpan.Zero)
            return ScheduleImmediate(state, action);
        var timer = new Timer(_ =>
            try
            {
                action(this, state);
            }
            catch (Exception ex)
                _logger?.LogError(ex, "Error executing scheduled action");
        }, null, delay, Timeout.InfiniteTimeSpan);
        return Disposable.Create(() => timer?.Dispose());
    /// Schedules an action for immediate execution with resource management
    private IDisposable ScheduleImmediate<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
        var cancellationToken = _cancellationTokenSource.Token;
        _ = Task.Run(async () =>
            await _resourceSemaphore.WaitAsync(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    var stopwatch = Stopwatch.StartNew();
                    action(this, state);
                    stopwatch.Stop();
                    _batchLatencyHistogram.Record(stopwatch.ElapsedMilliseconds);
                }
            catch (OperationCanceledException)
                // Expected during shutdown
                _logger?.LogError(ex, "Error in scheduled kernel operation");
            finally
                _resourceSemaphore.Release();
        }, cancellationToken);
        return Disposable.Create(() => { });
    /// Records batch performance metrics for adaptive optimization
    public void RecordBatchPerformance(BatchMetrics metrics)
        {
        _performanceHistory.Enqueue(metrics);
        _batchCounter.Add(1);
        _currentBatchSizeGauge.Record(metrics.BatchSize);
        _throughputGauge.Record(metrics.ThroughputElementsPerSecond);
        // Keep only recent history (last 100 batches)
        while (_performanceHistory.Count > 100)
            _performanceHistory.TryDequeue(out _);
        _lastThroughput = metrics.ThroughputElementsPerSecond;
        _lastBatchFlush = DateTime.UtcNow;
        _shouldFlush = false;
        _logger?.LogDebug(
            "Batch processed: Size={BatchSize}, Time={ProcessingTime}ms, Throughput={Throughput:F1} elements/sec",
            metrics.BatchSize, metrics.ProcessingTimeMs, metrics.ThroughputElementsPerSecond);
    /// Allocates a buffer from the pool or creates a new one
    public async Task<IUnifiedMemoryBuffer<T>> AllocateBufferAsync<T>(int size) where T : unmanaged
        var bufferSize = size * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        var key = (typeof(T), size);
        if (_bufferPool.TryRemove(key, out var pooledBuffer))
            // Try to reuse the pooled buffer if possible
            pooledBuffer.Dispose(); // Clean up the old buffer
        // Create a new buffer - don't dispose the memory manager here since we return the buffer
        var memoryManager = new UnifiedMemoryManager();
        var buffer = await memoryManager.AllocateAsync<T>(size);
        Interlocked.Add(ref _totalMemoryAllocated, bufferSize);
        return buffer;
    /// Returns a buffer to the pool for reuse
    public void ReturnBuffer<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
        var key = (typeof(T), buffer.Length);
        try
            // Create a basic memory manager for buffer pooling
            var memoryManager = new UnifiedMemoryManager();
            var pooledBuffer = memoryManager.AllocateAsync<byte>(buffer.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>()).Result;
            // Only store if the buffer is the expected concrete type
            if (pooledBuffer is UnifiedBuffer<byte> concreteBuffer)
                _bufferPool.TryAdd(key, concreteBuffer);
            else
                pooledBuffer.Dispose();
        catch (Exception ex)
            _logger?.LogWarning(ex, "Failed to return buffer to pool");
    /// Forces flush of pending batches
    private void FlushPendingBatches(object? state)
        _shouldFlush = true;
        _logger?.LogDebug("Triggering batch flush due to timeout");
    /// Adaptive batch size optimization loop
    private async Task AdaptiveBatchOptimizationLoop()
        var lastOptimization = DateTime.UtcNow;
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(5), _cancellationTokenSource.Token);
                if (DateTime.UtcNow - lastOptimization < TimeSpan.FromSeconds(10))
                    continue;
                OptimizeBatchSize();
                lastOptimization = DateTime.UtcNow;
                break;
                _logger?.LogError(ex, "Error in adaptive batch optimization loop");
    /// Optimizes batch size based on performance history
    private void OptimizeBatchSize()
            .Where(m => m.Timestamp > DateTime.UtcNow.AddSeconds(-30))
        if (recentMetrics.Length < 3)
            return;
        // Find optimal batch size based on throughput
        var optimalMetric = recentMetrics
            .OrderByDescending(m => m.ThroughputElementsPerSecond)
            .First();
        var newOptimalSize = Math.Clamp(
            optimalMetric.BatchSize,
            _config.MinBatchSize,
            _config.MaxBatchSize);
        if (Math.Abs(newOptimalSize - _currentOptimalBatchSize) > _config.MinBatchSize * 0.1)
            var oldSize = _currentOptimalBatchSize;
            _currentOptimalBatchSize = newOptimalSize;
            _logger?.LogInformation(
                "Adaptive batching: Optimal batch size changed from {OldSize} to {NewSize} " +
                "(throughput: {Throughput:F1} elements/sec)",
                oldSize, newOptimalSize, optimalMetric.ThroughputElementsPerSecond);
    public void Dispose()
        _disposed = true;
        _cancellationTokenSource.Cancel();
        _batchTimer?.Dispose();
        _resourceSemaphore?.Dispose();
        // Dispose all pooled buffers
        foreach (var buffer in _bufferPool.Values)
            buffer.Dispose();
        _bufferPool.Clear();
        _cancellationTokenSource.Dispose();
            "ReactiveKernelScheduler disposed. Total memory allocated: {MemoryMB:F1} MB",
            _totalMemoryAllocated / (1024.0 * 1024.0));
/// Performance metrics for the reactive kernel scheduler
public record SchedulerPerformanceMetrics
    /// <summary>Average throughput in elements per second</summary>
    public double AverageThroughput { get; init; }
    /// <summary>Average processing latency in milliseconds</summary>
    public double AverageLatency { get; init; }
    /// <summary>Current optimal batch size</summary>
    public int OptimalBatchSize { get; init; }
    /// <summary>Total memory allocated in bytes</summary>
    public long TotalMemoryAllocated { get; init; }
    /// <summary>Number of active buffers in pool</summary>
    public int ActiveBuffers { get; init; }
