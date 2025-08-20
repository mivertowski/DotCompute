// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution
{

/// <summary>
/// Advanced CUDA stream manager with RTX 2000 optimizations, priority scheduling, and graph-like execution patterns
/// </summary>
public sealed class CudaStreamManager : IDisposable
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly CudaStreamPool _streamPool;
    private readonly ConcurrentDictionary<StreamId, CudaStreamInfo> _activeStreams;
    private readonly ConcurrentDictionary<string, CudaStreamGroup> _streamGroups;
    private readonly SemaphoreSlim _streamCreationSemaphore;
    private readonly Timer _maintenanceTimer;
    private readonly object _lockObject = new();
    
    // RTX 2000 optimization constants
    private const int RTX_2000_SM_COUNT = 24;
    private const int STREAMS_PER_SM_GROUP = 6; // 24 SMs / 4 streams = 6 SMs per stream
    private const int OPTIMAL_CONCURRENT_STREAMS = 4;
    private const int MAX_CONCURRENT_STREAMS = 32;
    private const int INITIAL_POOL_SIZE = 8;
    
    // Stream priority ranges
    private int _leastPriority;
    private int _greatestPriority;
    
    // Special streams
    private IntPtr _defaultStream = IntPtr.Zero;
    private readonly IntPtr[] _rtxOptimizedStreams = new IntPtr[OPTIMAL_CONCURRENT_STREAMS];
    private readonly CudaStreamDependencyTracker _dependencyTracker;
    
    private volatile bool _disposed;

    public CudaStreamManager(CudaContext context, ILogger<CudaStreamManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeStreams = new ConcurrentDictionary<StreamId, CudaStreamInfo>();
        _streamGroups = new ConcurrentDictionary<string, CudaStreamGroup>();
        _streamCreationSemaphore = new SemaphoreSlim(MAX_CONCURRENT_STREAMS, MAX_CONCURRENT_STREAMS);
        _dependencyTracker = new CudaStreamDependencyTracker();

        Initialize();

        _streamPool = new CudaStreamPool(context, logger, _leastPriority, _greatestPriority);

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null, 
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

        _logger.LogInformation(
            "CUDA Stream Manager initialized for RTX optimization: {OptimalStreams} optimal streams, " +
            "priority range [{Least}, {Greatest}], max concurrent: {MaxStreams}",
            OPTIMAL_CONCURRENT_STREAMS, _leastPriority, _greatestPriority, MAX_CONCURRENT_STREAMS);
    }

    /// <summary>
    /// Gets the default CUDA stream (stream 0)
    /// </summary>
    public IntPtr DefaultStream => _defaultStream;

    /// <summary>
    /// Gets RTX 2000 optimized streams for maximum performance
    /// </summary>
    public IReadOnlyList<IntPtr> RtxOptimizedStreams => _rtxOptimizedStreams.AsReadOnly();

    /// <summary>
    /// Gets the high priority stream for critical operations (backward compatibility)
    /// </summary>
    public IntPtr HighPriorityStream => _rtxOptimizedStreams.Length > 0 ? _rtxOptimizedStreams[0] : _defaultStream;

    /// <summary>
    /// Gets the low priority stream for background operations (backward compatibility)
    /// </summary>
    public IntPtr LowPriorityStream => _rtxOptimizedStreams.Length > 1 ? _rtxOptimizedStreams[1] : _defaultStream;

    /// <summary>
    /// Creates a high-performance stream group optimized for RTX 2000
    /// </summary>
    public async Task<CudaStreamGroup> CreateRtxOptimizedGroupAsync(
        string groupName,
        CudaStreamPriority priority = CudaStreamPriority.High,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var group = new CudaStreamGroup(groupName, OPTIMAL_CONCURRENT_STREAMS);
        
        for (int i = 0; i < OPTIMAL_CONCURRENT_STREAMS; i++)
        {
            var streamHandle = await CreateStreamAsync(
                CudaStreamFlags.NonBlocking, 
                priority, 
                cancellationToken).ConfigureAwait(false);
            
            group.AddStream(streamHandle.StreamId, streamHandle.Stream);
        }

        _streamGroups[groupName] = group;
        
        _logger.LogDebug("Created RTX-optimized stream group '{GroupName}' with {StreamCount} streams", 
            groupName, OPTIMAL_CONCURRENT_STREAMS);

        return group;
    }

    /// <summary>
    /// Creates a new CUDA stream with advanced options
    /// </summary>
    public async Task<CudaStreamHandle> CreateStreamAsync(
        CudaStreamFlags flags = CudaStreamFlags.NonBlocking,
        CudaStreamPriority priority = CudaStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        await _streamCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        
        try
        {
            _context.MakeCurrent();

            var stream = IntPtr.Zero;
            var streamId = StreamId.New();
            var cudaPriority = ConvertToCudaPriority(priority);
            var cudaFlags = ConvertToCudaFlags(flags);

            CudaError result;
            if (priority != CudaStreamPriority.Normal)
            {
                result = Native.CudaRuntime.cudaStreamCreateWithPriority(ref stream, cudaFlags, cudaPriority);
            }
            else
            {
                result = Native.CudaRuntime.cudaStreamCreateWithFlags(ref stream, cudaFlags);
            }

            Native.CudaRuntime.CheckError(result, "creating CUDA stream");

            var streamInfo = new CudaStreamInfo
            {
                StreamId = streamId,
                Handle = stream,
                Flags = flags,
                Priority = priority,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsed = DateTimeOffset.UtcNow
            };

            _activeStreams[streamId] = streamInfo;
            
            _logger.LogDebug("Created CUDA stream {StreamId} (handle={Handle}) with priority={Priority}, flags={Flags}", 
                streamId, stream, priority, flags);

            return new CudaStreamHandle(streamId, stream, this);
        }
        catch
        {
            _streamCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Gets or creates a stream from the pool for temporary use
    /// </summary>
    public async Task<CudaStreamHandle> GetPooledStreamAsync(
        CudaStreamPriority priority = CudaStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var pooledStream = await _streamPool.AcquireAsync(priority, cancellationToken).ConfigureAwait(false);
        return pooledStream;
    }

    /// <summary>
    /// Creates a batch of streams for parallel execution
    /// </summary>
    public async Task<CudaStreamHandle[]> CreateStreamBatchAsync(
        int count,
        CudaStreamFlags flags = CudaStreamFlags.NonBlocking,
        CudaStreamPriority priority = CudaStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var streams = new CudaStreamHandle[count];
        var tasks = new Task<CudaStreamHandle>[count];

        for (int i = 0; i < count; i++)
        {
            tasks[i] = CreateStreamAsync(flags, priority, cancellationToken);
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Array.Copy(results, streams, count);

        _logger.LogDebug("Created batch of {Count} streams with priority={Priority}", count, priority);
        return streams;
    }

    /// <summary>
    /// Synchronizes a stream asynchronously with advanced options
    /// </summary>
    public async Task SynchronizeStreamAsync(
        StreamId streamId, 
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(streamId, out var streamInfo))
        {
            throw new ArgumentException($"Stream {streamId} not found", nameof(streamId));
        }

        _context.MakeCurrent();

        if (timeout.HasValue)
        {
            using var timeoutCts = new CancellationTokenSource(timeout.Value);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            try
            {
                await Task.Run(() =>
                {
                    var result = Native.CudaRuntime.cudaStreamSynchronize(streamInfo.Handle);
                    Native.CudaRuntime.CheckError(result, $"synchronizing stream {streamId}");
                }, combinedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                throw new TimeoutException($"Stream {streamId} synchronization timed out after {timeout}");
            }
        }
        else
        {
            await Task.Run(() =>
            {
                var result = Native.CudaRuntime.cudaStreamSynchronize(streamInfo.Handle);
                Native.CudaRuntime.CheckError(result, $"synchronizing stream {streamId}");
            }, cancellationToken).ConfigureAwait(false);
        }

        streamInfo.LastUsed = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Implements event-based synchronization between streams
    /// </summary>
    public async Task SynchronizeStreamsAsync(
        StreamId waitingStream, 
        StreamId signalStream, 
        IntPtr eventHandle,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(waitingStream, out var waitingStreamInfo))
            {
                throw new ArgumentException($"Waiting stream {waitingStream} not found");
            }

            if (!_activeStreams.TryGetValue(signalStream, out var signalStreamInfo))
            {
                throw new ArgumentException($"Signal stream {signalStream} not found");
            }

            _context.MakeCurrent();

        // Record event on signal stream
        var recordResult = Native.CudaRuntime.cudaEventRecord(eventHandle, signalStreamInfo.Handle);
        Native.CudaRuntime.CheckError(recordResult, $"recording event on stream {signalStream}");

        // Make waiting stream wait for the event
        var waitResult = Native.CudaRuntime.cudaStreamWaitEvent(waitingStreamInfo.Handle, eventHandle, 0);
        Native.CudaRuntime.CheckError(waitResult, $"making stream {waitingStream} wait for event");

        _dependencyTracker.AddDependency(waitingStream, signalStream);

        _logger.LogTrace("Synchronized stream {WaitingStream} to wait for stream {SignalStream} via event {Event}",
            waitingStream, signalStream, eventHandle);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Implements graph-like execution pattern with dependencies
    /// </summary>
    public async Task ExecuteGraphAsync(
        CudaExecutionGraph graph,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var executionPlan = graph.BuildExecutionPlan();
        var completedNodes = new ConcurrentDictionary<string, bool>();
        var nodeTasks = new ConcurrentDictionary<string, Task>();

        foreach (var level in executionPlan.Levels)
        {
            var levelTasks = new List<Task>();

            foreach (var node in level.Nodes)
            {
                var task = Task.Run(async () =>
                {
                    // Wait for dependencies
                    foreach (var dependency in node.Dependencies)
                    {
                        if (nodeTasks.TryGetValue(dependency, out var depTask))
                        {
                            await depTask.ConfigureAwait(false);
                        }
                    }

                    // Execute node operation
                    var streamHandle = await GetPooledStreamAsync(node.Priority, cancellationToken).ConfigureAwait(false);
                    
                    try
                    {
                        await node.Operation(streamHandle.Stream).ConfigureAwait(false);
                        await SynchronizeStreamAsync(streamHandle.StreamId, cancellationToken: cancellationToken).ConfigureAwait(false);
                        
                        completedNodes[node.Id] = true;
                        
                        _logger.LogTrace("Completed execution graph node {NodeId} on stream {StreamId}", 
                            node.Id, streamHandle.StreamId);
                    }
                    finally
                    {
                        streamHandle.Dispose();
                    }
                }, cancellationToken);

                nodeTasks[node.Id] = task;
                levelTasks.Add(task);
            }

            // Wait for all nodes in this level to complete
            await Task.WhenAll(levelTasks).ConfigureAwait(false);
        }

        _logger.LogDebug("Completed execution graph with {NodeCount} nodes in {LevelCount} levels",
            executionPlan.TotalNodes, executionPlan.Levels.Count);
    }

    /// <summary>
    /// Checks if a stream is ready (all operations completed)
    /// </summary>
    public bool IsStreamReady(StreamId streamId)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(streamId, out var streamInfo))
            {
                return false;
            }

            _context.MakeCurrent();
        var result = Native.CudaRuntime.cudaStreamQuery(streamInfo.Handle);
        return result == CudaError.Success;
    }

    /// <summary>
    /// Gets comprehensive stream statistics for monitoring
    /// </summary>
    public CudaStreamStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var activeCount = 0;
        var busyCount = 0;
        var totalAge = 0.0;
        var now = DateTimeOffset.UtcNow;

        foreach (var streamInfo in _activeStreams.Values)
        {
            activeCount++;
            totalAge += (now - streamInfo.CreatedAt).TotalSeconds;
            
            if (!IsStreamReady(streamInfo.StreamId))
            {
                busyCount++;
            }
        }

        return new CudaStreamStatistics
        {
            ActiveStreams = activeCount,
            BusyStreams = busyCount,
            IdleStreams = activeCount - busyCount,
            StreamGroups = _streamGroups.Count,
            AverageStreamAge = activeCount > 0 ? totalAge / activeCount : 0,
            PoolStatistics = _streamPool.GetStatistics(),
            DependencyCount = _dependencyTracker.GetDependencyCount(),
            OptimalConcurrentStreams = OPTIMAL_CONCURRENT_STREAMS,
            MaxConcurrentStreams = MAX_CONCURRENT_STREAMS
        };
    }

    /// <summary>
    /// Adds a callback to be executed when stream operations complete
    /// </summary>
    public void AddStreamCallback(StreamId streamId, Func<StreamId, Task> callback)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(streamId, out var streamInfo))
            {
                throw new ArgumentException($"Stream {streamId} not found");
            }

            _ = Task.Run(async () =>
        {
            try
            {
                await SynchronizeStreamAsync(streamId).ConfigureAwait(false);
                await callback(streamId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stream callback for stream {StreamId}", streamId);
            }
        });
    }

        /// <summary>
        /// Optimizes stream usage for RTX 2000 architecture
        /// </summary>
        public void OptimizeForRtx2000() => OptimizeStreamUsage();

        /// <summary>
        /// Optimizes stream usage (backward compatibility)
        /// </summary>
        public void OptimizeStreamUsage()
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            // Rebalance streams based on SM utilization
            var activeStreamCount = _activeStreams.Values.Count(s => !IsStreamReady(s.StreamId));
            var optimalCount = Math.Min(activeStreamCount, OPTIMAL_CONCURRENT_STREAMS);

            // Prefer high-priority streams for active work
            var highPriorityStreams = _activeStreams.Values
                .Where(s => s.Priority == CudaStreamPriority.High && !IsStreamReady(s.StreamId))
                .Take(optimalCount)
                .ToList();

            _logger.LogDebug("RTX 2000 optimization: {ActiveStreams} active streams, {HighPriorityActive} high-priority active",
                activeStreamCount, highPriorityStreams.Count);

            // Additional optimization: cleanup old idle streams
            CleanupIdleStreams(TimeSpan.FromMinutes(5));
        }
    }

    internal void ReturnStreamToPool(StreamId streamId)
    {
        if (_activeStreams.TryRemove(streamId, out var streamInfo))
        {
            _streamPool.Return(streamInfo.Handle, streamInfo.Priority);
            _streamCreationSemaphore.Release();
        }
    }

    private void Initialize()
    {
        _context.MakeCurrent();

        // Get priority ranges
        var priorityResult = Native.CudaRuntime.cudaDeviceGetStreamPriorityRange(out _leastPriority, out _greatestPriority);
        if (priorityResult != CudaError.Success)
        {
            _logger.LogWarning("Failed to get stream priority range, using defaults: {Error}",
                Native.CudaRuntime.GetErrorString(priorityResult));
            _leastPriority = 0;
            _greatestPriority = -1;
        }

        // Default stream is always stream 0
        _defaultStream = IntPtr.Zero;

        // Create RTX-optimized streams
        for (int i = 0; i < OPTIMAL_CONCURRENT_STREAMS; i++)
        {
            var stream = IntPtr.Zero;
            var result = Native.CudaRuntime.cudaStreamCreateWithPriority(
                ref stream, 
                ConvertToCudaFlags(CudaStreamFlags.NonBlocking),
                ConvertToCudaPriority(CudaStreamPriority.High));

            if (result == CudaError.Success)
            {
                _rtxOptimizedStreams[i] = stream;
            }
            else
            {
                _logger.LogWarning("Failed to create RTX-optimized stream {Index}: {Error}", 
                    i, Native.CudaRuntime.GetErrorString(result));
                break;
            }
        }

        _logger.LogDebug("Initialized {Count} RTX-optimized streams with priority range [{Least}, {Greatest}]",
            _rtxOptimizedStreams.Count(s => s != IntPtr.Zero), _leastPriority, _greatestPriority);
    }

    private void CleanupIdleStreams(TimeSpan maxIdleTime)
    {
        var cutoffTime = DateTimeOffset.UtcNow - maxIdleTime;
        var idleStreams = _activeStreams
            .Where(kvp => IsStreamReady(kvp.Key) && kvp.Value.LastUsed < cutoffTime)
            .Take(10) // Limit cleanup batch size
            .ToList();

        foreach (var (streamId, streamInfo) in idleStreams)
        {
            if (_activeStreams.TryRemove(streamId, out _))
            {
                DestroyStream(streamInfo.Handle);
                _streamCreationSemaphore.Release();
                
                _logger.LogTrace("Cleaned up idle stream {StreamId} (idle for {IdleTime})", 
                    streamId, DateTimeOffset.UtcNow - streamInfo.LastUsed);
            }
        }

        if (idleStreams.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} idle streams", idleStreams.Count);
        }
    }

    private void DestroyStream(IntPtr stream)
    {
        if (stream == IntPtr.Zero || Array.IndexOf(_rtxOptimizedStreams, stream) >= 0)
            {
                return; // Don't destroy default or RTX-optimized streams
            }

            try
        {
            _context.MakeCurrent();
            var result = Native.CudaRuntime.cudaStreamDestroy(stream);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to destroy CUDA stream {Stream}: {Error}", 
                    stream, Native.CudaRuntime.GetErrorString(result));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while destroying stream {Stream}", stream);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ConvertToCudaFlags(CudaStreamFlags flags) => flags switch
    {
        CudaStreamFlags.Default => 0x00,
        CudaStreamFlags.NonBlocking => 0x01,
        _ => 0x00
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ConvertToCudaPriority(CudaStreamPriority priority) => priority switch
    {
        CudaStreamPriority.High => _greatestPriority,
        CudaStreamPriority.Low => _leastPriority,
        _ => (_leastPriority + _greatestPriority) / 2
    };

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
            {
                return;
            }

            try
        {
            OptimizeForRtx2000();
            _streamPool.PerformMaintenance();
            _dependencyTracker.Cleanup();
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
            _disposed = true;

            _maintenanceTimer?.Dispose();

            try
            {
                // Synchronize all active streams
                var syncTasks = _activeStreams.Values
                    .Select(s => SynchronizeStreamAsync(s.StreamId, TimeSpan.FromSeconds(5)))
                    .ToArray();

                Task.WaitAll(syncTasks, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error synchronizing streams during disposal");
            }

            // Destroy all active streams
            foreach (var streamInfo in _activeStreams.Values)
            {
                DestroyStream(streamInfo.Handle);
            }

            // Destroy RTX-optimized streams
            foreach (var stream in _rtxOptimizedStreams)
            {
                if (stream != IntPtr.Zero)
                {
                    DestroyStream(stream);
                }
            }

            _streamPool?.Dispose();
            _streamCreationSemaphore?.Dispose();
            _dependencyTracker?.Dispose();

            _logger.LogInformation("CUDA Stream Manager disposed");
        }
    }
}

// Supporting types and classes follow...

/// <summary>
/// Unique identifier for CUDA streams
/// </summary>
public readonly struct StreamId : IEquatable<StreamId>
{
    private readonly Guid _id;

    private StreamId(Guid id) => _id = id;

    public static StreamId New() => new(Guid.NewGuid());

    public bool Equals(StreamId other) => _id.Equals(other._id);
    public override bool Equals(object? obj) => obj is StreamId other && Equals(other);
    public override int GetHashCode() => _id.GetHashCode();
    public override string ToString() => _id.ToString("N")[..8];

    public static bool operator ==(StreamId left, StreamId right) => left.Equals(right);
    public static bool operator !=(StreamId left, StreamId right) => !left.Equals(right);
}

/// <summary>
/// Information about an active CUDA stream
/// </summary>
internal sealed class CudaStreamInfo
{
    public StreamId StreamId { get; set; }
    public IntPtr Handle { get; set; }
    public CudaStreamFlags Flags { get; set; }
    public CudaStreamPriority Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsed { get; set; }
    public long OperationCount { get; set; }
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
/// Handle for managed CUDA streams with automatic cleanup
/// </summary>
public class CudaStreamHandle : IDisposable
{
    private readonly object? _manager;
    private volatile bool _disposed;

    internal CudaStreamHandle(StreamId streamId, IntPtr stream, object manager)
    {
        StreamId = streamId;
        Stream = stream;
        _manager = manager;
    }

    public StreamId StreamId { get; }
    public IntPtr Stream { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            ReturnToManager();
        }
    }

    protected virtual void ReturnToManager()
    {
        if (_manager is CudaStreamManager streamManager)
        {
            streamManager.ReturnStreamToPool(StreamId);
        }
        else if (_manager is IStreamReturnManager returnManager)
        {
            returnManager.ReturnStreamToPool(StreamId);
        }
    }
}

/// <summary>
/// Group of streams working together
/// </summary>
public sealed class CudaStreamGroup : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, IntPtr> _streams;
    private volatile bool _disposed;

    public CudaStreamGroup(string name, int capacity = 4)
    {
        Name = name;
        _streams = new ConcurrentDictionary<StreamId, IntPtr>();
    }

    public string Name { get; }
    public IReadOnlyDictionary<StreamId, IntPtr> Streams => _streams;

        internal void AddStream(StreamId streamId, IntPtr stream) => _streams[streamId] = stream;

        public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // Stream cleanup is handled by the manager
        }
    }
}

/// <summary>
/// Tracks dependencies between streams
/// </summary>
internal sealed class CudaStreamDependencyTracker : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, HashSet<StreamId>> _dependencies;
    private readonly object _lockObject = new();

    public CudaStreamDependencyTracker()
    {
        _dependencies = new ConcurrentDictionary<StreamId, HashSet<StreamId>>();
    }

    public void AddDependency(StreamId dependent, StreamId dependency)
    {
        lock (_lockObject)
        {
            _dependencies.GetOrAdd(dependent, _ => new HashSet<StreamId>()).Add(dependency);
        }
    }

    public int GetDependencyCount()
    {
        lock (_lockObject)
        {
            return _dependencies.Values.Sum(deps => deps.Count);
        }
    }

    public void Cleanup()
    {
        lock (_lockObject)
        {
            // Remove old dependency tracking (implementation depends on specific needs)
            var keysToRemove = _dependencies
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _dependencies.TryRemove(key, out _);
            }
        }
    }

    public void Dispose()
    {
        lock (_lockObject)
        {
            _dependencies.Clear();
        }
    }
}

/// <summary>
/// Statistics for CUDA stream usage
/// </summary>
public sealed class CudaStreamStatistics
{
    public int ActiveStreams { get; set; }
    public int BusyStreams { get; set; }
    public int IdleStreams { get; set; }
    public int StreamGroups { get; set; }
    public double AverageStreamAge { get; set; }
    public CudaStreamPoolStatistics? PoolStatistics { get; set; }
    public int DependencyCount { get; set; }
    public int OptimalConcurrentStreams { get; set; }
    public int MaxConcurrentStreams { get; set; }
}

/// <summary>
/// Execution graph for coordinated stream operations
/// </summary>
public sealed class CudaExecutionGraph
{
    private readonly List<CudaExecutionNode> _nodes = new();

    public void AddNode(string id, Func<IntPtr, Task> operation, 
                       CudaStreamPriority priority = CudaStreamPriority.Normal,
                       params string[] dependencies)
    {
        _nodes.Add(new CudaExecutionNode
        {
            Id = id,
            Operation = operation,
            Priority = priority,
            Dependencies = [.. dependencies]
        });
    }

    internal CudaExecutionPlan BuildExecutionPlan()
    {
        // Topological sort to determine execution levels
        var levels = new List<CudaExecutionLevel>();
        var completed = new HashSet<string>();
        var remaining = _nodes.ToList();

        while (remaining.Count > 0)
        {
            var readyNodes = remaining
                .Where(node => node.Dependencies.All(dep => completed.Contains(dep)))
                .ToList();

            if (readyNodes.Count == 0)
                {
                    throw new InvalidOperationException("Circular dependency detected in execution graph");
                }

                levels.Add(new CudaExecutionLevel { Nodes = readyNodes });

            foreach (var node in readyNodes)
            {
                completed.Add(node.Id);
                remaining.Remove(node);
            }
        }

        return new CudaExecutionPlan
        {
            Levels = levels,
            TotalNodes = _nodes.Count
        };
    }
}

/// <summary>
/// Node in the execution graph
/// </summary>
public sealed class CudaExecutionNode
{
    public string Id { get; set; } = string.Empty;
    public Func<IntPtr, Task> Operation { get; set; } = null!;
    public CudaStreamPriority Priority { get; set; }
    public List<string> Dependencies { get; set; } = new();
}

/// <summary>
/// Level in the execution plan
/// </summary>
public sealed class CudaExecutionLevel
{
    public List<CudaExecutionNode> Nodes { get; set; } = new();
}

/// <summary>
/// Complete execution plan
/// </summary>
public sealed class CudaExecutionPlan
{
    public List<CudaExecutionLevel> Levels { get; set; } = new();
    public int TotalNodes { get; set; }
}}
