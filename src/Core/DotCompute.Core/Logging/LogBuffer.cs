using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Core.Logging;

/// <summary>
/// High-performance asynchronous log buffer with batching, compression, and multiple sink support.
/// Designed to minimize performance impact on the main application thread while ensuring reliable log delivery.
/// </summary>
public sealed class LogBuffer : IDisposable
{
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
                    _logger.LogWarning("Log entry dropped due to full buffer: {Category} - {Message}",
                        logEntry.Category, logEntry.Message);
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
            _logger.LogInformation("Added log sink: {SinkType}", sink.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize log sink: {SinkType}", sink.GetType().Name);
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
                _logger.LogInformation("Removed log sink: {SinkType}", sink.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing log sink: {SinkType}", sink.GetType().Name);
            }
        }


        return removed;
    }

    private async Task ProcessLogEntriesAsync()
    {
        var batch = new List<StructuredLogEntry>(_options.BatchSize);
        var batchTimer = new Timer(async _ => await ProcessCurrentBatchAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);


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
            _logger.LogError(ex, "Error in log processing task");
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

    private async Task ProcessBatchAsync(List<StructuredLogEntry> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }


        try
        {
            // Apply batch-level transformations
            var processedBatch = await PreprocessBatchAsync(batch, cancellationToken);

            // Write to all configured sinks in parallel

            var sinkTasks = _sinks.Select(sink => WriteBatchToSinkAsync(sink, processedBatch, cancellationToken));
            await Task.WhenAll(sinkTasks);


            _ = Interlocked.Add(ref _totalProcessed, batch.Count);
            _ = Interlocked.Increment(ref _batchesProcessed);


            _logger.LogTrace("Processed batch of {Count} log entries across {SinkCount} sinks",

                batch.Count, _sinks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process log batch of {Count} entries", batch.Count);

            // Attempt individual entry processing for resilience

            await ProcessIndividualEntriesAsync(batch, cancellationToken);
        }
    }

    private async Task<List<StructuredLogEntry>> PreprocessBatchAsync(List<StructuredLogEntry> batch,

        CancellationToken cancellationToken)
    {
        // Add batch-level metadata
        var batchId = Guid.NewGuid().ToString("N")[..8];
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
            _logger.LogError(ex, "Failed to write batch to sink: {SinkType}", sink.GetType().Name);

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
                    _logger.LogTrace(ex, "Failed to write individual entry to sink: {SinkType}",

                        sink.GetType().Name);
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
            _logger.LogError(ex, "Failed to flush sink: {SinkType}", sink.GetType().Name);
        }
    }

    private void InitializeSinks()
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Initialize();
                _logger.LogInformation("Initialized log sink: {SinkType}", sink.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize log sink: {SinkType}", sink.GetType().Name);
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
                _logger.LogError(ex, "Error during timer-triggered batch flush");
            }
        }, _cancellationTokenSource.Token);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(LogBuffer));
        }
    }

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

            if (!_processingTask.Wait(TimeSpan.FromSeconds(30)))
            {
                _logger.LogWarning("Log processing task did not complete within timeout");
            }

            // Final flush

            FlushAsync().GetAwaiter().GetResult();

            // Dispose sinks

            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing sink: {SinkType}", sink.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LogBuffer disposal");
        }
        finally
        {
            _batchTimer?.Dispose();
            _flushSemaphore?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}

// Supporting interfaces and data structures
public interface ILogSink : IDisposable
{
    public void Initialize();
    public Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default);
    public Task WriteBatchAsync(List<StructuredLogEntry> entries, CancellationToken cancellationToken = default);
    public Task FlushAsync(CancellationToken cancellationToken = default);
}

public interface IHealthCheckable
{
    public bool IsHealthy { get; }
    public void MarkUnhealthy(Exception? exception = null);
    public void MarkHealthy();
}

public sealed class LogBufferOptions
{
    public int MaxBufferSize { get; set; } = 10000;
    public int BatchSize { get; set; } = 100;
    public int BatchIntervalMs { get; set; } = 1000;
    public int MaxFlushBatchSize { get; set; } = 1000;
    public bool DropOnFull { get; set; } = true;
    public bool LogDroppedEntries { get; set; } = true;
    public bool EnableCompression { get; set; }

    public int CompressionThreshold { get; set; } = 50;
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;
    public List<string> ExcludedCategories { get; set; } = [];
    public List<Func<StructuredLogEntry, bool>> CustomFilters { get; set; } = [];
}

public sealed class LogBufferStatistics
{
    public long TotalEnqueued { get; set; }
    public long TotalProcessed { get; set; }
    public long TotalDropped { get; set; }
    public long BatchesProcessed { get; set; }
    public int CurrentBufferSize { get; set; }
    public int MaxBufferSize { get; set; }
    public DateTimeOffset LastFlushTime { get; set; }
    public int ActiveSinks { get; set; }
    public bool IsHealthy { get; set; }


    public double DropRate => TotalEnqueued > 0 ? (double)TotalDropped / TotalEnqueued : 0;
    public double ProcessingRate => TotalEnqueued > 0 ? (double)TotalProcessed / TotalEnqueued : 0;
    public double BufferUtilization => MaxBufferSize > 0 ? (double)CurrentBufferSize / MaxBufferSize : 0;
}

// Built-in sink implementations
public sealed class ConsoleSink : ILogSink, IHealthCheckable
{
    public bool IsHealthy { get; private set; } = true;


    public void Initialize() { }


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


    public async Task WriteBatchAsync(List<StructuredLogEntry> entries, CancellationToken cancellationToken = default)
    {
        foreach (var entry in entries)
        {
            await WriteAsync(entry, cancellationToken);
        }
    }


    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Console.Out.Flush();
        return Task.CompletedTask;
    }


    public void MarkUnhealthy(Exception? exception = null) => IsHealthy = false;
    public void MarkHealthy() => IsHealthy = true;


    public void Dispose() { }
}

public sealed class FileSink : ILogSink, IHealthCheckable
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _writeSemaphore = new(1, 1);


    public bool IsHealthy { get; private set; } = true;


    public FileSink(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }


    public void Initialize()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }


    public async Task WriteAsync(StructuredLogEntry entry, CancellationToken cancellationToken = default) => await WriteBatchAsync([entry], cancellationToken);


    public async Task WriteBatchAsync(List<StructuredLogEntry> entries, CancellationToken cancellationToken = default)
    {
        await _writeSemaphore.WaitAsync(cancellationToken);
        try
        {
            var lines = entries.Select(entry => JsonSerializer.Serialize(entry));
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


    public Task FlushAsync(CancellationToken cancellationToken = default)
        // File writes are immediately flushed

        => Task.CompletedTask;


    public void MarkUnhealthy(Exception? exception = null) => IsHealthy = false;
    public void MarkHealthy() => IsHealthy = true;


    public void Dispose() => _writeSemaphore?.Dispose();
}