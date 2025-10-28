using System.Globalization;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Core.Aot;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Logging;

/// <summary>
/// High-performance asynchronous log buffer with batching, compression, and multiple sink support.
/// Designed to minimize performance impact on the main application thread while ensuring reliable log delivery.
/// </summary>
public sealed partial class LogBuffer : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 9001, Level = MsLogLevel.Trace, Message = "Processed batch of {Count} log entries across {SinkCount} sinks")]
    private static partial void LogBatchProcessed(ILogger logger, int count, int sinkCount);

    [LoggerMessage(EventId = 9002, Level = MsLogLevel.Trace, Message = "Failed to write individual entry to sink: {SinkType}")]
    private static partial void LogSinkWriteFailed(ILogger logger, Exception ex, string sinkType);

    #endregion

    private readonly ILogger<LogBuffer> _logger;
    private readonly LogBufferOptions _options;
    private readonly Channel<StructuredLogEntry> _logChannel;
    private readonly ChannelWriter<StructuredLogEntry> _writer;
    private readonly ChannelReader<StructuredLogEntry> _reader;
    private readonly List<ILogSink> _sinks;
    private readonly SemaphoreSlim _flushSemaphore;
    private readonly Timer _batchTimer;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    private volatile bool _disposed;

    // Metrics

    private long _totalEnqueued;
    private long _totalProcessed;
    private long _totalDropped;
    private long _batchesProcessed;
    private DateTimeOffset _lastFlush = DateTimeOffset.UtcNow;
    /// <summary>
    /// Initializes a new instance of the LogBuffer class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The options.</param>
    /// <param name="sinks">The sinks.</param>

    public LogBuffer(ILogger<LogBuffer> logger, IOptions<LogBufferOptions> options, IEnumerable<ILogSink> sinks)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new LogBufferOptions();
        _sinks = sinks?.ToList() ?? [];

        // Create bounded channel for backpressure handling

        var channelOptions = new BoundedChannelOptions(_options.MaxBufferSize)
        {
            FullMode = _options.DropOnFull ? BoundedChannelFullMode.DropOldest : BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };


        _logChannel = Channel.CreateBounded<StructuredLogEntry>(channelOptions);
        _writer = _logChannel.Writer;
        _reader = _logChannel.Reader;


        _flushSemaphore = new SemaphoreSlim(1, 1);
        _cancellationTokenSource = new CancellationTokenSource();

        // Start background processing task

        _processingTask = Task.Run(ProcessLogEntriesAsync, _cancellationTokenSource.Token);

        // Start batch processing timer

        _batchTimer = new Timer(TriggerBatchFlush, null,

            TimeSpan.FromMilliseconds(_options.BatchIntervalMs),
            TimeSpan.FromMilliseconds(_options.BatchIntervalMs));

        // Initialize sinks

        InitializeSinks();
    }

    /// <summary>
    /// Adds a log entry to the buffer for asynchronous processing.
    /// </summary>
    /// <param name="logEntry">The log entry to add</param>
    /// <returns>True if the entry was added successfully, false if the buffer is full and dropping is enabled</returns>
    public bool AddLogEntry(StructuredLogEntry logEntry)
    {
        ThrowIfDisposed();


        if (logEntry == null)
        {
            return false;
        }

        // Apply filtering if configured

        if (!ShouldProcessLogEntry(logEntry))
        {
            return true; // Entry was filtered, but not an error
        }


        try
        {
            var success = _writer.TryWrite(logEntry);


            if (success)
            {
                _ = Interlocked.Increment(ref _totalEnqueued);
            }
            else
            {
                _ = Interlocked.Increment(ref _totalDropped);


                if (_options.LogDroppedEntries)
                {
                    _logger.LogWarningMessage($"Log entry dropped due to full buffer: {logEntry.Category} - {logEntry.Message}");
                }
            }


            return success;
        }
        catch (InvalidOperationException)
        {
            // Writer is closed
            return false;
        }
    }

    /// <summary>
    /// Forces an immediate flush of all buffered log entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            var entries = await CollectPendingEntriesAsync(cancellationToken);
            if (entries.Count != 0)
            {
                await ProcessBatchAsync(entries, cancellationToken);
            }

            // Flush all sinks

            await FlushSinksAsync(cancellationToken);


            _lastFlush = DateTimeOffset.UtcNow;
        }
        finally
        {
            _ = _flushSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets current buffer statistics for monitoring.
    /// </summary>
    /// <returns>Buffer statistics</returns>
    public LogBufferStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new LogBufferStatistics
        {
            TotalEnqueued = Interlocked.Read(ref _totalEnqueued),
            TotalProcessed = Interlocked.Read(ref _totalProcessed),
            TotalDropped = Interlocked.Read(ref _totalDropped),
            BatchesProcessed = Interlocked.Read(ref _batchesProcessed),
            CurrentBufferSize = _logChannel.Reader.CanCount ? _logChannel.Reader.Count : -1,
            MaxBufferSize = _options.MaxBufferSize,
            LastFlushTime = _lastFlush,
            ActiveSinks = _sinks.Count,
            IsHealthy = IsHealthy()
        };
    }

    /// <summary>
    /// Adds a new log sink to the buffer.
    /// </summary>
    /// <param name="sink">The log sink to add</param>
    public void AddSink(ILogSink sink)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(sink);

        _sinks.Add(sink);

        try
        {
            sink.Initialize();
            _logger.LogInfoMessage($"Added log sink: {sink.GetType().Name}");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to initialize log sink: {sink.GetType().Name}");
        }
    }

    /// <summary>
    /// Removes a log sink from the buffer.
    /// </summary>
    /// <param name="sink">The log sink to remove</param>
    /// <returns>True if the sink was removed, false if it wasn't found</returns>
    public bool RemoveSink(ILogSink sink)
    {
        ThrowIfDisposed();


        if (sink == null)
        {
            return false;
        }


        var removed = _sinks.Remove(sink);
        if (removed)
        {
            try
            {
                sink.Dispose();
                _logger.LogInfoMessage($"Removed log sink: {sink.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Error disposing log sink: {sink.GetType().Name}");
            }
        }


        return removed;
    }

    private async Task ProcessLogEntriesAsync()
    {
        var batch = new List<StructuredLogEntry>(_options.BatchSize);
        var batchTimer = new Timer(_ => _ = Task.Run(ProcessCurrentBatchAsync), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);


        try
        {
            await foreach (var logEntry in _reader.ReadAllAsync(_cancellationTokenSource.Token))
            {
                batch.Add(logEntry);

                // Process batch when it reaches the configured size

                if (batch.Count >= _options.BatchSize)
                {
                    await ProcessBatchAsync(batch, _cancellationTokenSource.Token);
                    batch.Clear();
                }

                // Reset the batch timer

                _ = batchTimer.Change(TimeSpan.FromMilliseconds(_options.BatchIntervalMs), Timeout.InfiniteTimeSpan);
            }

            // Process any remaining entries

            if (batch.Count != 0)
            {
                await ProcessBatchAsync(batch, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error in log processing task");
        }
        finally
        {
            await batchTimer.DisposeAsync();
        }


        async Task ProcessCurrentBatchAsync()
        {
            if (batch.Count != 0)
            {
                await ProcessBatchAsync(batch, _cancellationTokenSource.Token);
                batch.Clear();
            }
        }
    }

    private async Task ProcessBatchAsync(IReadOnlyList<StructuredLogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }


        try
        {
            // Apply batch-level transformations
            var processedBatch = await PreprocessBatchAsync(batch.ToList(), cancellationToken);

            // Write to all configured sinks in parallel

            var sinkTasks = _sinks.Select(sink => WriteBatchToSinkAsync(sink, processedBatch, cancellationToken));
            await Task.WhenAll(sinkTasks);


            _ = Interlocked.Add(ref _totalProcessed, batch.Count);
            _ = Interlocked.Increment(ref _batchesProcessed);

            LogBatchProcessed(_logger, batch.Count, _sinks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to process log batch of {batch.Count} entries");

            // Attempt individual entry processing for resilience

            await ProcessIndividualEntriesAsync(batch.ToList(), cancellationToken);
        }
    }

    private async Task<List<StructuredLogEntry>> PreprocessBatchAsync(List<StructuredLogEntry> batch,

        CancellationToken cancellationToken)
    {
        // Add batch-level metadata
        var batchId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];
        var batchTimestamp = DateTimeOffset.UtcNow;


        foreach (var entry in batch)
        {
            entry.Properties["_batchId"] = batchId;
            entry.Properties["_batchTimestamp"] = batchTimestamp;
            entry.Properties["_batchSize"] = batch.Count;
        }

        // Apply compression if enabled and batch is large enough

        if (_options.EnableCompression && batch.Count >= _options.CompressionThreshold)
        {
            foreach (var entry in batch)
            {
                entry.Properties["_compressed"] = true;
                // Actual compression would happen in the sink
            }
        }


        await Task.Yield(); // Allow other operations to continue
        return batch;
    }

    private async Task WriteBatchToSinkAsync(ILogSink sink, List<StructuredLogEntry> batch,

        CancellationToken cancellationToken)
    {
        try
        {
            await sink.WriteBatchAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to write batch to sink: {sink.GetType().Name}");

            // Mark sink as unhealthy

            if (sink is IHealthCheckable healthCheckable)
            {
                healthCheckable.MarkUnhealthy(ex);
            }
        }
    }

    private async Task ProcessIndividualEntriesAsync(List<StructuredLogEntry> batch,

        CancellationToken cancellationToken)
    {
        foreach (var entry in batch)
        {
            foreach (var sink in _sinks)
            {
                try
                {
                    await sink.WriteAsync(entry, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogSinkWriteFailed(_logger, ex, sink.GetType().Name);
                }
            }
        }
    }

    private async Task<List<StructuredLogEntry>> CollectPendingEntriesAsync(CancellationToken cancellationToken)
    {
        var entries = new List<StructuredLogEntry>();

        // Collect all currently available entries without blocking

        while (_reader.TryRead(out var entry) && entries.Count < _options.MaxFlushBatchSize)
        {
            entries.Add(entry);
        }


        await Task.Yield(); // Allow other operations to continue
        return entries;
    }

    private async Task FlushSinksAsync(CancellationToken cancellationToken)
    {
        var flushTasks = _sinks.Select(sink => FlushSinkAsync(sink, cancellationToken));
        await Task.WhenAll(flushTasks);
    }

    private async Task FlushSinkAsync(ILogSink sink, CancellationToken cancellationToken)
    {
        try
        {
            await sink.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to flush sink: {sink.GetType().Name}");
        }
    }

    private void InitializeSinks()
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Initialize();
                _logger.LogInfoMessage($"Initialized log sink: {sink.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to initialize log sink: {sink.GetType().Name}");
            }
        }
    }

    private bool ShouldProcessLogEntry(StructuredLogEntry logEntry)
    {
        // Apply log level filtering
        if (logEntry.LogLevel < _options.MinimumLogLevel)
        {
            return false;
        }

        // Apply category filtering

        if (_options.ExcludedCategories.Any(cat =>

            logEntry.Category.Contains(cat, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Apply custom filters

        foreach (var filter in _options.CustomFilters)
        {
            if (!filter(logEntry))
            {
                return false;
            }
        }


        return true;
    }

    private bool IsHealthy()
    {
        // Check if processing is keeping up
        var currentBufferSize = _logChannel.Reader.CanCount ? _logChannel.Reader.Count : 0;
        var bufferUtilization = (double)currentBufferSize / _options.MaxBufferSize;


        if (bufferUtilization > 0.8) // Buffer is >80% full
        {
            return false;
        }

        // Check if we haven't flushed recently

        var timeSinceLastFlush = DateTimeOffset.UtcNow - _lastFlush;
        if (timeSinceLastFlush > TimeSpan.FromMinutes(5))
        {
            return false;
        }

        // Check sink health

        var healthyScenarios = _sinks.Count(s => s is IHealthCheckable hc ? hc.IsHealthy : true);
        return healthyScenarios > 0; // At least one sink is healthy
    }

    private void TriggerBatchFlush(object? state)
    {
        if (_disposed)
        {
            return;
        }


        _ = Task.Run(async () =>
        {
            try
            {
                await _flushSemaphore.WaitAsync(_cancellationTokenSource.Token);
                try
                {
                    var entries = await CollectPendingEntriesAsync(_cancellationTokenSource.Token);
                    if (entries.Count != 0)
                    {
                        await ProcessBatchAsync(entries, _cancellationTokenSource.Token);
                    }
                }
                finally
                {
                    _ = _flushSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during timer-triggered batch flush");
            }
        }, _cancellationTokenSource.Token);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;


        try
        {
            // Stop accepting new entries
            _writer.Complete();

            // Cancel background processing

            _cancellationTokenSource.Cancel();

            // Wait for processing to complete (with timeout)
            // VSTHRD002: Synchronous wait is necessary here because IDisposable.Dispose() cannot be async.
            // Using a timeout to prevent indefinite blocking during disposal.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            if (!_processingTask.Wait(TimeSpan.FromSeconds(30)))
            {
                _logger.LogWarningMessage("Log processing task did not complete within timeout");
            }

            // Final flush
            FlushAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002

            // Dispose sinks

            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, $"Error disposing sink: {sink.GetType().Name}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during LogBuffer disposal");
        }
        finally
        {
            _batchTimer?.Dispose();
            _flushSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
/// <summary>
/// An i log sink interface.
/// </summary>

// Supporting interfaces and data structures
public interface ILogSink : IDisposable
{
    /// <summary>
    /// Initializes the .
    /// </summary>
    public void Initialize();
    /// <summary>
    /// Gets write asynchronously.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets write batch asynchronously.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task WriteBatchAsync(IReadOnlyList<StructuredLogEntry> entries, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets flush asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public Task FlushAsync(CancellationToken cancellationToken = default);
}
/// <summary>
/// An i health checkable interface.
/// </summary>

public interface IHealthCheckable
{
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool IsHealthy { get; }
    /// <summary>
    /// Performs mark unhealthy.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void MarkUnhealthy(Exception? exception = null);
    /// <summary>
    /// Performs mark healthy.
    /// </summary>
    public void MarkHealthy();
}
/// <summary>
/// A class that represents log buffer options.
/// </summary>

public sealed class LogBufferOptions
{
    /// <summary>
    /// Gets or sets the max buffer size.
    /// </summary>
    /// <value>The max buffer size.</value>
    public int MaxBufferSize { get; set; } = 10000;
    /// <summary>
    /// Gets or sets the batch size.
    /// </summary>
    /// <value>The batch size.</value>
    public int BatchSize { get; set; } = 100;
    /// <summary>
    /// Gets or sets the batch interval ms.
    /// </summary>
    /// <value>The batch interval ms.</value>
    public int BatchIntervalMs { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the max flush batch size.
    /// </summary>
    /// <value>The max flush batch size.</value>
    public int MaxFlushBatchSize { get; set; } = 1000;
    /// <summary>
    /// Gets or sets the drop on full.
    /// </summary>
    /// <value>The drop on full.</value>
    public bool DropOnFull { get; set; } = true;
    /// <summary>
    /// Gets or sets the log dropped entries.
    /// </summary>
    /// <value>The log dropped entries.</value>
    public bool LogDroppedEntries { get; set; } = true;
    /// <summary>
    /// Gets or sets the enable compression.
    /// </summary>
    /// <value>The enable compression.</value>
    public bool EnableCompression { get; set; }
    /// <summary>
    /// Gets or sets the compression threshold.
    /// </summary>
    /// <value>The compression threshold.</value>

    public int CompressionThreshold { get; set; } = 50;
    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    /// <value>The minimum log level.</value>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;
    /// <summary>
    /// Gets or sets the excluded categories.
    /// </summary>
    /// <value>The excluded categories.</value>
    public IList<string> ExcludedCategories { get; init; } = [];
    /// <summary>
    /// Gets or sets the custom filters.
    /// </summary>
    /// <value>The custom filters.</value>
    public ICollection<Func<StructuredLogEntry, bool>> CustomFilters { get; } = [];
}
/// <summary>
/// A class that represents log buffer statistics.
/// </summary>

public sealed class LogBufferStatistics
{
    /// <summary>
    /// Gets or sets the total enqueued.
    /// </summary>
    /// <value>The total enqueued.</value>
    public long TotalEnqueued { get; set; }
    /// <summary>
    /// Gets or sets the total processed.
    /// </summary>
    /// <value>The total processed.</value>
    public long TotalProcessed { get; set; }
    /// <summary>
    /// Gets or sets the total dropped.
    /// </summary>
    /// <value>The total dropped.</value>
    public long TotalDropped { get; set; }
    /// <summary>
    /// Gets or sets the batches processed.
    /// </summary>
    /// <value>The batches processed.</value>
    public long BatchesProcessed { get; set; }
    /// <summary>
    /// Gets or sets the current buffer size.
    /// </summary>
    /// <value>The current buffer size.</value>
    public int CurrentBufferSize { get; set; }
    /// <summary>
    /// Gets or sets the max buffer size.
    /// </summary>
    /// <value>The max buffer size.</value>
    public int MaxBufferSize { get; set; }
    /// <summary>
    /// Gets or sets the last flush time.
    /// </summary>
    /// <value>The last flush time.</value>
    public DateTimeOffset LastFlushTime { get; set; }
    /// <summary>
    /// Gets or sets the active sinks.
    /// </summary>
    /// <value>The active sinks.</value>
    public int ActiveSinks { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool IsHealthy { get; set; }
    /// <summary>
    /// Gets or sets the drop rate.
    /// </summary>
    /// <value>The drop rate.</value>


    public double DropRate => TotalEnqueued > 0 ? (double)TotalDropped / TotalEnqueued : 0;
    /// <summary>
    /// Gets or sets the processing rate.
    /// </summary>
    /// <value>The processing rate.</value>
    public double ProcessingRate => TotalEnqueued > 0 ? (double)TotalProcessed / TotalEnqueued : 0;
    /// <summary>
    /// Gets or sets the buffer utilization.
    /// </summary>
    /// <value>The buffer utilization.</value>
    public double BufferUtilization => MaxBufferSize > 0 ? (double)CurrentBufferSize / MaxBufferSize : 0;
}
/// <summary>
/// A class that represents console sink.
/// </summary>

// Built-in sink implementations
public sealed class ConsoleSink : ILogSink, IHealthCheckable
{
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>
    public bool IsHealthy { get; private set; } = true;
    /// <summary>
    /// Initializes the .
    /// </summary>


    public void Initialize() { }
    /// <summary>
    /// Gets write asynchronously.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {entry.LogLevel}: {entry.FormattedMessage}");
            return Task.CompletedTask;
        }
        catch (Exception)
        {
            IsHealthy = false;
            throw;
        }
    }
    /// <summary>
    /// Gets write batch asynchronously.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async Task WriteBatchAsync(IReadOnlyList<StructuredLogEntry> entries, CancellationToken cancellationToken = default)
    {
        foreach (var entry in entries)
        {
            await WriteAsync(entry, cancellationToken);
        }
    }
    /// <summary>
    /// Gets flush asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Console.Out.Flush();
        return Task.CompletedTask;
    }
    /// <summary>
    /// Performs mark unhealthy.
    /// </summary>
    /// <param name="exception">The exception.</param>


    public void MarkUnhealthy(Exception? exception = null) => IsHealthy = false;
    /// <summary>
    /// Performs mark healthy.
    /// </summary>
    public void MarkHealthy() => IsHealthy = true;
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose() { }
}
/// <summary>
/// A class that represents file sink.
/// </summary>

public sealed class FileSink(string filePath) : ILogSink, IHealthCheckable
{
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);
    /// <summary>
    /// Gets or sets a value indicating whether healthy.
    /// </summary>
    /// <value>The is healthy.</value>


    public bool IsHealthy { get; private set; } = true;
    /// <summary>
    /// Initializes the .
    /// </summary>

    public void Initialize()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }
    /// <summary>
    /// Gets write asynchronously.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default) => await WriteBatchAsync([entry], cancellationToken);
    /// <summary>
    /// Gets write batch asynchronously.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public async Task WriteBatchAsync(IReadOnlyList<StructuredLogEntry> entries, CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var lines = entries.Select(entry => JsonSerializer.Serialize(entry, DotComputeCompactJsonContext.Default.StructuredLogEntry));
            await File.AppendAllLinesAsync(_filePath, lines, cancellationToken);
            IsHealthy = true;
        }
        catch (Exception)
        {
            IsHealthy = false;
            throw;
        }
        finally
        {
            _ = _writeSemaphore.Release();
        }
    }
    /// <summary>
    /// Gets flush asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>


    public Task FlushAsync(CancellationToken cancellationToken = default)
        // File writes are immediately flushed




        => Task.CompletedTask;
    /// <summary>
    /// Performs mark unhealthy.
    /// </summary>
    /// <param name="exception">The exception.</param>


    public void MarkUnhealthy(Exception? exception = null) => IsHealthy = false;
    /// <summary>
    /// Performs mark healthy.
    /// </summary>
    public void MarkHealthy() => IsHealthy = true;
    /// <summary>
    /// Performs dispose.
    /// </summary>


    public void Dispose() => _writeSemaphore?.Dispose();
}
