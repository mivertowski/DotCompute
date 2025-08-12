// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution;

/// <summary>
/// Advanced CUDA stream manager with multi-stream support, priority queues, and automatic optimization
/// </summary>
public sealed class CudaStreamManager : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<object, IntPtr> _streams;
    private readonly ConcurrentQueue<IntPtr> _availableStreams;
    private readonly SemaphoreSlim _streamSemaphore;
    private readonly CudaStreamPool _streamPool;
    private readonly Timer _maintenanceTimer;
    private readonly object _lockObject = new();
    private IntPtr _defaultStream;
    private IntPtr _highPriorityStream;
    private IntPtr _lowPriorityStream;
    private bool _disposed;

    // Stream configuration
    private const int MaxConcurrentStreams = 32;
    private const int InitialStreamPoolSize = 8;
    private const uint CudaStreamNonBlocking = 0x01;
    private const uint CudaStreamDefault = 0x00;

    public CudaStreamManager(CudaContext context, ILogger<CudaStreamManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _streams = new ConcurrentDictionary<object, IntPtr>();
        _availableStreams = new ConcurrentQueue<IntPtr>();
        _streamSemaphore = new SemaphoreSlim(MaxConcurrentStreams, MaxConcurrentStreams);
        _streamPool = new CudaStreamPool(context, logger);

        Initialize();

        // Set up maintenance timer to clean up unused streams
        _maintenanceTimer = new Timer(PerformMaintenance, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("CUDA Stream Manager initialized with {MaxStreams} max concurrent streams", 
            MaxConcurrentStreams);
    }

    /// <summary>
    /// Gets the default CUDA stream
    /// </summary>
    public IntPtr DefaultStream => _defaultStream;

    /// <summary>
    /// Gets the high priority stream for critical operations
    /// </summary>
    public IntPtr HighPriorityStream => _highPriorityStream;

    /// <summary>
    /// Gets the low priority stream for background operations
    /// </summary>
    public IntPtr LowPriorityStream => _lowPriorityStream;

    /// <summary>
    /// Gets or creates a stream for the specified identifier
    /// </summary>
    public IntPtr GetOrCreateStream(object? identifier = null)
    {
        ThrowIfDisposed();

        if (identifier == null)
        {
            return _defaultStream;
        }

        return _streams.GetOrAdd(identifier, _ => CreateNewStream());
    }

    /// <summary>
    /// Gets a stream from the pool for temporary use
    /// </summary>
    public async Task<CudaStreamHandle> GetPooledStreamAsync(
        CudaStreamPriority priority = CudaStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _streamSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            var stream = _streamPool.AcquireStream(priority);
            return new CudaStreamHandle(stream, this);
        }
        catch
        {
            _streamSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a new CUDA stream with specified flags
    /// </summary>
    public IntPtr CreateStream(CudaStreamFlags flags = CudaStreamFlags.NonBlocking)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var stream = IntPtr.Zero;
        var cudaFlags = ConvertToCudaFlags(flags);
        var result = CudaRuntime.cudaStreamCreateWithFlags(ref stream, cudaFlags);
        CudaRuntime.CheckError(result, "creating CUDA stream");

        _logger.LogDebug("Created new CUDA stream {Stream} with flags {Flags}", stream, flags);
        return stream;
    }

    /// <summary>
    /// Destroys a CUDA stream
    /// </summary>
    public void DestroyStream(IntPtr stream)
    {
        if (stream == IntPtr.Zero || stream == _defaultStream || 
            stream == _highPriorityStream || stream == _lowPriorityStream)
        {
            return; // Don't destroy special streams
        }

        _context.MakeCurrent();
        var result = CudaRuntime.cudaStreamDestroy(stream);
        if (result != CudaError.Success)
        {
            _logger.LogWarning("Failed to destroy CUDA stream {Stream}: {Error}", 
                stream, CudaRuntime.GetErrorString(result));
        }
        else
        {
            _logger.LogDebug("Destroyed CUDA stream {Stream}", stream);
        }
    }

    /// <summary>
    /// Synchronizes a stream asynchronously
    /// </summary>
    public async Task SynchronizeStreamAsync(IntPtr stream, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        await Task.Run(() =>
        {
            var result = CudaRuntime.cudaStreamSynchronize(stream);
            CudaRuntime.CheckError(result, $"synchronizing stream {stream}");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a stream has completed all operations
    /// </summary>
    public bool IsStreamReady(IntPtr stream)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var result = CudaRuntime.cudaStreamQuery(stream);
        return result == CudaError.Success;
    }

    /// <summary>
    /// Waits for a stream to complete with timeout
    /// </summary>
    public async Task<bool> WaitForStreamAsync(IntPtr stream, TimeSpan timeout, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            while (!IsStreamReady(stream))
            {
                await Task.Delay(1, combinedCts.Token).ConfigureAwait(false);
            }
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return false; // Timeout
        }
    }

    /// <summary>
    /// Synchronizes all streams
    /// </summary>
    public async Task SynchronizeAllStreamsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _context.MakeCurrent();

        var tasks = new List<Task>();

        // Synchronize all managed streams
        foreach (var stream in _streams.Values)
        {
            tasks.Add(SynchronizeStreamAsync(stream, cancellationToken));
        }

        // Synchronize special streams
        tasks.Add(SynchronizeStreamAsync(_defaultStream, cancellationToken));
        tasks.Add(SynchronizeStreamAsync(_highPriorityStream, cancellationToken));
        tasks.Add(SynchronizeStreamAsync(_lowPriorityStream, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets stream statistics for monitoring
    /// </summary>
    public CudaStreamStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new CudaStreamStatistics
        {
            TotalStreams = _streams.Count + 3, // Include special streams
            ActiveStreams = _streams.Values.Count(stream => !IsStreamReady(stream)) + 
                           (!IsStreamReady(_defaultStream) ? 1 : 0) +
                           (!IsStreamReady(_highPriorityStream) ? 1 : 0) +
                           (!IsStreamReady(_lowPriorityStream) ? 1 : 0),
            AvailablePooledStreams = _availableStreams.Count,
            MaxConcurrentStreams = MaxConcurrentStreams,
            PoolStatistics = _streamPool.GetStatistics()
        };
    }

    /// <summary>
    /// Adds a callback to be executed when the stream completes
    /// </summary>
    public void AddStreamCallback(IntPtr stream, Action callback)
    {
        ThrowIfDisposed();

        // CUDA doesn't have direct callback support, so we use async monitoring
        _ = Task.Run(async () =>
        {
            try
            {
                await SynchronizeStreamAsync(stream).ConfigureAwait(false);
                callback();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream callback for stream {Stream}", stream);
            }
        });
    }

    /// <summary>
    /// Optimizes stream usage by rebalancing load
    /// </summary>
    public void OptimizeStreamUsage()
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            // Clean up unused streams
            var unusedStreams = _streams
                .Where(kvp => IsStreamReady(kvp.Value))
                .Take(_streams.Count / 2) // Keep at least half
                .ToList();

            foreach (var (key, stream) in unusedStreams)
            {
                if (_streams.TryRemove(key, out var removedStream))
                {
                    _availableStreams.Enqueue(removedStream);
                }
            }

            _logger.LogDebug("Optimized stream usage: moved {Count} streams to pool", unusedStreams.Count);
        }
    }

    internal void ReturnPooledStream(IntPtr stream)
    {
        if (!_disposed)
        {
            _streamPool.ReleaseStream(stream);
            _streamSemaphore.Release();
        }
    }

    private void Initialize()
    {
        _context.MakeCurrent();

        // Create default stream (stream 0)
        _defaultStream = IntPtr.Zero; // CUDA default stream

        // Create priority streams
        _highPriorityStream = CreateStream(CudaStreamFlags.NonBlocking);
        _lowPriorityStream = CreateStream(CudaStreamFlags.NonBlocking);

        // Pre-allocate stream pool
        for (int i = 0; i < InitialStreamPoolSize; i++)
        {
            var stream = CreateNewStream();
            _availableStreams.Enqueue(stream);
        }

        _logger.LogDebug("Initialized CUDA streams: default={Default}, high={High}, low={Low}, pool={Pool}",
            _defaultStream, _highPriorityStream, _lowPriorityStream, InitialStreamPoolSize);
    }

    private IntPtr CreateNewStream()
    {
        if (_availableStreams.TryDequeue(out var availableStream))
        {
            return availableStream;
        }

        return CreateStream(CudaStreamFlags.NonBlocking);
    }

    private static uint ConvertToCudaFlags(CudaStreamFlags flags)
    {
        return flags switch
        {
            CudaStreamFlags.Default => CudaStreamDefault,
            CudaStreamFlags.NonBlocking => CudaStreamNonBlocking,
            _ => CudaStreamDefault
        };
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
            return;

        try
        {
            OptimizeStreamUsage();
            _streamPool.PerformMaintenance();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during stream manager maintenance");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _maintenanceTimer?.Dispose();

            try
            {
                // Synchronize all streams before cleanup
                SynchronizeAllStreamsAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error synchronizing streams during disposal");
            }

            // Destroy managed streams
            foreach (var stream in _streams.Values)
            {
                DestroyStream(stream);
            }

            // Destroy pooled streams
            while (_availableStreams.TryDequeue(out var stream))
            {
                DestroyStream(stream);
            }

            // Destroy special streams (except default which is null)
            DestroyStream(_highPriorityStream);
            DestroyStream(_lowPriorityStream);

            _streamPool?.Dispose();
            _streamSemaphore?.Dispose();

            _disposed = true;
            
            _logger.LogInformation("CUDA Stream Manager disposed");
        }
    }
}

/// <summary>
/// CUDA stream flags
/// </summary>
public enum CudaStreamFlags
{
    Default,
    NonBlocking
}

/// <summary>
/// CUDA stream priority levels
/// </summary>
public enum CudaStreamPriority
{
    Low,
    Normal,
    High
}

/// <summary>
/// Handle for pooled CUDA streams with automatic cleanup
/// </summary>
public sealed class CudaStreamHandle : IDisposable
{
    private readonly CudaStreamManager _manager;
    private bool _disposed;

    internal CudaStreamHandle(IntPtr stream, CudaStreamManager manager)
    {
        Stream = stream;
        _manager = manager;
    }

    public IntPtr Stream { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _manager.ReturnPooledStream(Stream);
            _disposed = true;
        }
    }
}

/// <summary>
/// Statistics for CUDA stream usage
/// </summary>
public sealed class CudaStreamStatistics
{
    public int TotalStreams { get; set; }
    public int ActiveStreams { get; set; }
    public int AvailablePooledStreams { get; set; }
    public int MaxConcurrentStreams { get; set; }
    public CudaStreamPoolStatistics? PoolStatistics { get; set; }
}

/// <summary>
/// Internal stream pool for efficient stream reuse
/// </summary>
internal sealed class CudaStreamPool : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly ConcurrentQueue<IntPtr> _normalPriorityStreams;
    private readonly ConcurrentQueue<IntPtr> _highPriorityStreams;
    private readonly ConcurrentQueue<IntPtr> _lowPriorityStreams;
    private readonly object _lockObject = new();
    private bool _disposed;

    public CudaStreamPool(CudaContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
        _normalPriorityStreams = new ConcurrentQueue<IntPtr>();
        _highPriorityStreams = new ConcurrentQueue<IntPtr>();
        _lowPriorityStreams = new ConcurrentQueue<IntPtr>();
    }

    public IntPtr AcquireStream(CudaStreamPriority priority)
    {
        var queue = GetQueueForPriority(priority);
        
        if (queue.TryDequeue(out var stream))
        {
            return stream;
        }

        // Create new stream if none available
        return CreateStreamForPriority(priority);
    }

    public void ReleaseStream(IntPtr stream)
    {
        if (_disposed || stream == IntPtr.Zero)
            return;

        // Return to normal priority queue by default
        _normalPriorityStreams.Enqueue(stream);
    }

    public void PerformMaintenance()
    {
        if (_disposed)
            return;

        lock (_lockObject)
        {
            // Clean up excess streams to prevent memory leaks
            CleanupExcessStreams(_normalPriorityStreams, 8);
            CleanupExcessStreams(_highPriorityStreams, 4);
            CleanupExcessStreams(_lowPriorityStreams, 4);
        }
    }

    public CudaStreamPoolStatistics GetStatistics()
    {
        return new CudaStreamPoolStatistics
        {
            NormalPriorityStreams = _normalPriorityStreams.Count,
            HighPriorityStreams = _highPriorityStreams.Count,
            LowPriorityStreams = _lowPriorityStreams.Count
        };
    }

    private ConcurrentQueue<IntPtr> GetQueueForPriority(CudaStreamPriority priority)
    {
        return priority switch
        {
            CudaStreamPriority.High => _highPriorityStreams,
            CudaStreamPriority.Low => _lowPriorityStreams,
            _ => _normalPriorityStreams
        };
    }

    private IntPtr CreateStreamForPriority(CudaStreamPriority priority)
    {
        _context.MakeCurrent();
        
        var stream = IntPtr.Zero;
        var result = CudaRuntime.cudaStreamCreateWithFlags(ref stream, 0x01); // NonBlocking
        CudaRuntime.CheckError(result, $"creating {priority} priority stream");
        
        return stream;
    }

    private void CleanupExcessStreams(ConcurrentQueue<IntPtr> queue, int maxCount)
    {
        while (queue.Count > maxCount && queue.TryDequeue(out var stream))
        {
            _context.MakeCurrent();
            var result = CudaRuntime.cudaStreamDestroy(stream);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to destroy excess stream: {Error}", 
                    CudaRuntime.GetErrorString(result));
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lockObject)
            {
                DestroyAllStreamsInQueue(_normalPriorityStreams);
                DestroyAllStreamsInQueue(_highPriorityStreams);
                DestroyAllStreamsInQueue(_lowPriorityStreams);
            }
            
            _disposed = true;
        }
    }

    private void DestroyAllStreamsInQueue(ConcurrentQueue<IntPtr> queue)
    {
        while (queue.TryDequeue(out var stream))
        {
            try
            {
                _context.MakeCurrent();
                CudaRuntime.cudaStreamDestroy(stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error destroying stream during pool cleanup");
            }
        }
    }
}

/// <summary>
/// Statistics for the stream pool
/// </summary>
public sealed class CudaStreamPoolStatistics
{
    public int NormalPriorityStreams { get; set; }
    public int HighPriorityStreams { get; set; }
    public int LowPriorityStreams { get; set; }
    public int TotalPooledStreams => NormalPriorityStreams + HighPriorityStreams + LowPriorityStreams;
}