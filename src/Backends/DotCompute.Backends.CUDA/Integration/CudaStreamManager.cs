// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Execution;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Manages CUDA streams for concurrent kernel execution and optimization
/// </summary>
public sealed class CudaStreamManager : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly Dictionary<string, CudaStream> _namedStreams;
    private readonly Queue<CudaStream> _streamPool;
    private readonly object _streamLock = new();
    private readonly Timer _optimizationTimer;
    private volatile bool _disposed;

    // Pre-defined streams for different priorities
    private CudaStream? _defaultStream;
    private CudaStream? _highPriorityStream;
    private CudaStream? _lowPriorityStream;
    private CudaStream? _memoryStream;

    public CudaStreamManager(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _namedStreams = new Dictionary<string, CudaStream>();
        _streamPool = new Queue<CudaStream>();

        // Initialize predefined streams
        InitializePredefinedStreams();

        // Set up stream optimization timer
        _optimizationTimer = new Timer(OptimizeStreams, null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5));

        _logger.LogInfoMessage($"CUDA Stream Manager initialized for device {context.DeviceId}");
    }

    /// <summary>
    /// Gets the default stream for general operations
    /// </summary>
    public CudaStream DefaultStream => _defaultStream ?? throw new InvalidOperationException("Default stream not initialized");

    /// <summary>
    /// Gets the high priority stream for critical operations
    /// </summary>
    public CudaStream HighPriorityStream => _highPriorityStream ?? throw new InvalidOperationException("High priority stream not initialized");

    /// <summary>
    /// Gets the low priority stream for background operations
    /// </summary>
    public CudaStream LowPriorityStream => _lowPriorityStream ?? throw new InvalidOperationException("Low priority stream not initialized");

    /// <summary>
    /// Gets the dedicated stream for memory operations
    /// </summary>
    public CudaStream MemoryStream => _memoryStream ?? throw new InvalidOperationException("Memory stream not initialized");

    /// <summary>
    /// Creates a new named stream with specified priority
    /// </summary>
    public CudaStream CreateNamedStream(string name, CudaStreamPriority priority = CudaStreamPriority.Normal)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_streamLock)
        {
            if (_namedStreams.ContainsKey(name))
            {
                throw new ArgumentException($"Stream with name '{name}' already exists", nameof(name));
            }

            try
            {
                var stream = CreateStreamWithPriority(priority);
                _namedStreams[name] = stream;
                _logger.LogDebugMessage($"Created named stream '{name}' with priority {priority}");
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, $"Failed to create named stream '{name}'");
                throw;
            }
        }
    }

    /// <summary>
    /// Gets a named stream or creates it if it doesn't exist
    /// </summary>
    public CudaStream GetOrCreateNamedStream(string name, CudaStreamPriority priority = CudaStreamPriority.Normal)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_streamLock)
        {
            if (_namedStreams.TryGetValue(name, out var existingStream))
            {
                return existingStream;
            }

            return CreateNamedStream(name, priority);
        }
    }

    /// <summary>
    /// Gets a temporary stream from the pool
    /// </summary>
    public CudaStream GetTemporaryStream()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }

        lock (_streamLock)
        {
            if (_streamPool.Count > 0)
            {
                var stream = _streamPool.Dequeue();
                _logger.LogDebugMessage("Retrieved temporary stream from pool");
                return stream;
            }

            try
            {
                var stream = CreateStreamWithPriority(CudaStreamPriority.Normal);
                _logger.LogDebugMessage("Created new temporary stream");
                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Failed to create temporary stream");
                throw;
            }
        }
    }

    /// <summary>
    /// Returns a temporary stream to the pool
    /// </summary>
    public void ReturnTemporaryStream(CudaStream stream)
    {
        if (_disposed || stream == null)
        {
            return;
        }

        // Don't return predefined streams to the pool
        if (stream == _defaultStream || stream == _highPriorityStream || 
            stream == _lowPriorityStream || stream == _memoryStream)
        {
            return;
        }

        lock (_streamLock)
        {
            try
            {
                // Synchronize stream before returning to pool
                stream.Synchronize();
                
                if (_streamPool.Count < 10) // Limit pool size
                {
                    _streamPool.Enqueue(stream);
                    _logger.LogDebugMessage("Returned temporary stream to pool");
                }
                else
                {
                    stream.Dispose();
                    _logger.LogDebugMessage("Disposed excess temporary stream");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error returning stream to pool");
                stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Synchronizes all managed streams
    /// </summary>
    public void SynchronizeAllStreams()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }

        lock (_streamLock)
        {
            try
            {
                // Synchronize predefined streams
                _defaultStream?.Synchronize();
                _highPriorityStream?.Synchronize();
                _lowPriorityStream?.Synchronize();
                _memoryStream?.Synchronize();

                // Synchronize named streams
                foreach (var stream in _namedStreams.Values)
                {
                    stream.Synchronize();
                }

                // Synchronize pooled streams
                foreach (var stream in _streamPool)
                {
                    stream.Synchronize();
                }

                _logger.LogDebugMessage("All streams synchronized");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error synchronizing streams");
                throw;
            }
        }
    }

    /// <summary>
    /// Gets stream usage statistics
    /// </summary>
    public CudaStreamStatistics GetStatistics()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaStreamManager));
        }

        lock (_streamLock)
        {
            return new CudaStreamStatistics
            {
                ActiveStreams = _namedStreams.Count + 4, // Named + 4 predefined
                PooledStreams = _streamPool.Count,
                MaxConcurrentStreams = 32, // Typical limit for most CUDA devices
                TotalStreamsCreated = _namedStreams.Count + _streamPool.Count + 4,
                LastUpdated = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>
    /// Gets stream health status
    /// </summary>
    public double GetStreamHealth()
    {
        if (_disposed)
        {
            return 0.0;
        }

        try
        {
            var stats = GetStatistics();
            
            // Health is based on resource utilization
            var utilizationRatio = (double)stats.ActiveStreams / stats.MaxConcurrentStreams;
            
            // Healthy if utilization is below 80%
            return utilizationRatio < 0.8 ? 1.0 : Math.Max(0.0, (1.0 - utilizationRatio) / 0.2);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating stream health");
            return 0.0;
        }
    }

    /// <summary>
    /// Optimizes stream usage and cleanup
    /// </summary>
    public void OptimizeStreamUsage()
    {
        if (_disposed)
        {
            return;
        }

        lock (_streamLock)
        {
            try
            {
                // Clean up excess pooled streams
                while (_streamPool.Count > 5)
                {
                    var stream = _streamPool.Dequeue();
                    stream.Dispose();
                }

                // Synchronize all active streams to clear any pending operations
                foreach (var stream in _namedStreams.Values)
                {
                    try
                    {
                        stream.Synchronize();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to synchronize stream during optimization");
                    }
                }

                _logger.LogDebugMessage("Stream usage optimized");
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage(ex, "Error during stream optimization");
            }
        }
    }

    /// <summary>
    /// Removes a named stream
    /// </summary>
    public bool RemoveNamedStream(string name)
    {
        if (_disposed)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (_streamLock)
        {
            if (_namedStreams.TryGetValue(name, out var stream))
            {
                try
                {
                    stream.Synchronize();
                    stream.Dispose();
                    _namedStreams.Remove(name);
                    _logger.LogDebugMessage($"Removed named stream '{name}'");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error removing named stream '{Name}'", name);
                    return false;
                }
            }

            return false;
        }
    }

    private void InitializePredefinedStreams()
    {
        try
        {
            _context.MakeCurrent();
            
            _defaultStream = CreateStreamWithPriority(CudaStreamPriority.Normal);
            _highPriorityStream = CreateStreamWithPriority(CudaStreamPriority.High);
            _lowPriorityStream = CreateStreamWithPriority(CudaStreamPriority.Low);
            _memoryStream = CreateStreamWithPriority(CudaStreamPriority.Normal);
            
            _logger.LogDebugMessage("Predefined streams initialized");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to initialize predefined streams");
            throw;
        }
    }

    private static CudaStream CreateStreamWithPriority(CudaStreamPriority priority)
    {
        var flags = CudaStreamFlags.NonBlocking;
        
        // Create stream with specified priority
        return new CudaStream(flags, (int)priority);
    }

    private void OptimizeStreams(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            OptimizeStreamUsage();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during periodic stream optimization");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _optimizationTimer?.Dispose();
            
            lock (_streamLock)
            {
                // Dispose predefined streams
                _defaultStream?.Dispose();
                _highPriorityStream?.Dispose();
                _lowPriorityStream?.Dispose();
                _memoryStream?.Dispose();

                // Dispose named streams
                foreach (var stream in _namedStreams.Values)
                {
                    stream.Dispose();
                }
                _namedStreams.Clear();

                // Dispose pooled streams
                while (_streamPool.Count > 0)
                {
                    var stream = _streamPool.Dequeue();
                    stream.Dispose();
                }
            }

            _disposed = true;
            _logger.LogDebugMessage("CUDA Stream Manager disposed");
        }
    }
}

/// <summary>
/// CUDA stream priority levels
/// </summary>
public enum CudaStreamPriority
{
    Low = 1,
    Normal = 0,
    High = -1
}

/// <summary>
/// CUDA stream creation flags
/// </summary>
[Flags]
public enum CudaStreamFlags
{
    Default = 0,
    NonBlocking = 1
}

/// <summary>
/// CUDA stream usage statistics
/// </summary>
public sealed class CudaStreamStatistics
{
    public int ActiveStreams { get; init; }
    public int PooledStreams { get; init; }
    public int MaxConcurrentStreams { get; init; }
    public int TotalStreamsCreated { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}
