// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution;

/// <summary>
/// Advanced Metal command stream manager for asynchronous execution pipeline,
/// following CUDA stream patterns for maximum performance and thread safety.
/// </summary>
public sealed class MetalCommandStream : IDisposable
{
    private readonly IntPtr _device;
    private readonly ILogger<MetalCommandStream> _logger;
    private readonly MetalCommandBufferPool _commandBufferPool;
    private readonly ConcurrentDictionary<StreamId, MetalStreamInfo> _activeStreams;
    private readonly ConcurrentDictionary<string, MetalStreamGroup> _streamGroups;
    private readonly SemaphoreSlim _streamCreationSemaphore;
    private readonly Timer _maintenanceTimer;
    private readonly object _lockObject = new();

    // Apple Silicon optimization constants
    private const int APPLE_SILICON_OPTIMAL_STREAMS = 6;
    private const int INTEL_MAC_OPTIMAL_STREAMS = 4;
    private const int MAX_CONCURRENT_STREAMS = 32;

    // Special streams
    private IntPtr _defaultCommandQueue;
    private readonly IntPtr[] _optimizedCommandQueues;
    private readonly MetalStreamDependencyTracker _dependencyTracker;
    private readonly bool _isAppleSilicon;

    private volatile bool _disposed;
    private long _totalStreamsCreated;
    private long _totalCommandsExecuted;

    public MetalCommandStream(IntPtr device, ILogger<MetalCommandStream> logger)
    {
        _device = device != IntPtr.Zero ? device : throw new ArgumentNullException(nameof(device));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activeStreams = new ConcurrentDictionary<StreamId, MetalStreamInfo>();
        _streamGroups = new ConcurrentDictionary<string, MetalStreamGroup>();
        _streamCreationSemaphore = new SemaphoreSlim(MAX_CONCURRENT_STREAMS, MAX_CONCURRENT_STREAMS);
        _dependencyTracker = new MetalStreamDependencyTracker();


        _isAppleSilicon = DetectAppleSilicon();
        var optimalStreams = _isAppleSilicon ? APPLE_SILICON_OPTIMAL_STREAMS : INTEL_MAC_OPTIMAL_STREAMS;

        Initialize();

        // Create command buffer pool
        var poolLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalCommandBufferPool>.Instance;
        _commandBufferPool = new MetalCommandBufferPool(_defaultCommandQueue, poolLogger);

        // Create optimized command queues
        _optimizedCommandQueues = new IntPtr[optimalStreams];
        for (var i = 0; i < optimalStreams; i++)
        {
            _optimizedCommandQueues[i] = CreateOptimizedCommandQueue();
        }

        // Setup maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null,
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

        _logger.LogInformation(
            "Metal Command Stream initialized for {Architecture} optimization: {OptimalStreams} optimal streams, " +
            "max concurrent: {MaxStreams}",
            _isAppleSilicon ? "Apple Silicon" : "Intel Mac", optimalStreams, MAX_CONCURRENT_STREAMS);
    }

    /// <summary>
    /// Gets the default Metal command queue (primary queue)
    /// </summary>
    public IntPtr DefaultCommandQueue => _defaultCommandQueue;

    /// <summary>
    /// Gets optimized command queues for maximum performance
    /// </summary>
    public IReadOnlyList<IntPtr> OptimizedCommandQueues => _optimizedCommandQueues.AsReadOnly();

    /// <summary>
    /// Gets the high priority stream for critical operations
    /// </summary>
    public IntPtr HighPriorityStream => _optimizedCommandQueues.Length > 0 ? _optimizedCommandQueues[0] : _defaultCommandQueue;

    /// <summary>
    /// Gets the low priority stream for background operations
    /// </summary>
    public IntPtr LowPriorityStream => _optimizedCommandQueues.Length > 1 ? _optimizedCommandQueues[1] : _defaultCommandQueue;

    /// <summary>
    /// Creates a high-performance stream group optimized for Apple Silicon or Intel Mac
    /// </summary>
    public async Task<MetalStreamGroup> CreateOptimizedGroupAsync(
        string groupName,
        MetalStreamPriority priority = MetalStreamPriority.High,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var optimalStreams = _isAppleSilicon ? APPLE_SILICON_OPTIMAL_STREAMS : INTEL_MAC_OPTIMAL_STREAMS;
        var group = new MetalStreamGroup(groupName, optimalStreams);

        for (var i = 0; i < optimalStreams; i++)
        {
            var streamHandle = await CreateStreamAsync(
                MetalStreamFlags.Concurrent,
                priority,
                cancellationToken).ConfigureAwait(false);

            group.AddStream(streamHandle.StreamId, streamHandle.CommandQueue);
        }

        _streamGroups[groupName] = group;

        _logger.LogDebug("Created optimized stream group '{GroupName}' with {StreamCount} streams", groupName, optimalStreams);

        return group;
    }

    /// <summary>
    /// Creates a new Metal stream with advanced options
    /// </summary>
    public async Task<MetalStreamHandle> CreateStreamAsync(
        MetalStreamFlags flags = MetalStreamFlags.Concurrent,
        MetalStreamPriority priority = MetalStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _streamCreationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var commandQueue = CreateCommandQueueWithPriority(priority);
            var streamId = StreamId.New();

            var streamInfo = new MetalStreamInfo
            {
                StreamId = streamId,
                CommandQueue = commandQueue,
                Flags = flags,
                Priority = priority,
                CreatedAt = DateTimeOffset.UtcNow,
                LastUsed = DateTimeOffset.UtcNow
            };

            _activeStreams[streamId] = streamInfo;
            _ = Interlocked.Increment(ref _totalStreamsCreated);

            _logger.LogDebug("Created Metal stream {StreamId} with priority={Priority}, flags={Flags}", streamId, priority, flags);

            return new MetalStreamHandle(streamId, commandQueue, this);
        }
        catch
        {
            _ = _streamCreationSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Creates a batch of streams for parallel execution
    /// </summary>
    public async Task<MetalStreamHandle[]> CreateStreamBatchAsync(
        int count,
        MetalStreamFlags flags = MetalStreamFlags.Concurrent,
        MetalStreamPriority priority = MetalStreamPriority.Normal,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var streams = new MetalStreamHandle[count];
        var tasks = new Task<MetalStreamHandle>[count];

        for (var i = 0; i < count; i++)
        {
            tasks[i] = CreateStreamAsync(flags, priority, cancellationToken);
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Array.Copy(results, streams, count);

        _logger.LogDebug("Created batch of {Count} Metal streams", count);
        return streams;
    }

    /// <summary>
    /// Executes a command asynchronously on the specified stream
    /// </summary>
    public async Task<MetalCommandExecutionResult> ExecuteCommandAsync(
        StreamId streamId,
        Func<IntPtr, IntPtr, Task> commandOperation,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(streamId, out var streamInfo))
        {
            throw new ArgumentException($"Stream {streamId} not found", nameof(streamId));
        }

        var startTime = DateTimeOffset.UtcNow;
        var commandBuffer = _commandBufferPool.GetCommandBuffer();

        try
        {
            // Execute the command operation
            await commandOperation(streamInfo.CommandQueue, commandBuffer).ConfigureAwait(false);

            // Commit and wait for completion
            var completionTask = await CommitAndWaitAsync(commandBuffer, cancellationToken).ConfigureAwait(false);

            var endTime = DateTimeOffset.UtcNow;
            var executionTime = endTime - startTime;

            streamInfo.LastUsed = endTime;
            streamInfo.OperationCount++;
            _ = Interlocked.Increment(ref _totalCommandsExecuted);

            _logger.LogTrace("Executed command '{Operation}' on stream {StreamId} in {Duration}ms",
                operationName ?? "Unknown", streamId, executionTime.TotalMilliseconds);

            return new MetalCommandExecutionResult
            {
                StreamId = streamId,
                OperationName = operationName ?? "Unknown",
                ExecutionTime = executionTime,
                Success = completionTask,
                StartTime = startTime,
                EndTime = endTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Command execution failed on stream {StreamId}", streamId);


            return new MetalCommandExecutionResult
            {
                StreamId = streamId,
                OperationName = operationName ?? "Unknown",
                ExecutionTime = DateTimeOffset.UtcNow - startTime,
                Success = false,
                Error = ex,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            _commandBufferPool.ReturnCommandBuffer(commandBuffer);
        }
    }

    /// <summary>
    /// Synchronizes a stream asynchronously with timeout support
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

        var commandBuffer = _commandBufferPool.GetCommandBuffer();


        try
        {
            if (timeout.HasValue)
            {
                using var timeoutCts = new CancellationTokenSource(timeout.Value);
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                try
                {
                    _ = await CommitAndWaitAsync(commandBuffer, combinedCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
                {
                    throw new TimeoutException($"Stream {streamId} synchronization timed out after {timeout}");
                }
            }
            else
            {
                _ = await CommitAndWaitAsync(commandBuffer, cancellationToken).ConfigureAwait(false);
            }

            streamInfo.LastUsed = DateTimeOffset.UtcNow;
        }
        finally
        {
            _commandBufferPool.ReturnCommandBuffer(commandBuffer);
        }
    }

    /// <summary>
    /// Implements event-based synchronization between streams
    /// </summary>
    public async Task SynchronizeStreamsAsync(
        StreamId waitingStream,
        StreamId signalStream,
        MetalEvent metalEvent,
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

        // Record event on signal stream
        await metalEvent.RecordAsync(signalStreamInfo.CommandQueue, cancellationToken).ConfigureAwait(false);

        // Make waiting stream wait for the event
        await metalEvent.WaitAsync(waitingStreamInfo.CommandQueue, cancellationToken).ConfigureAwait(false);

        _dependencyTracker.AddDependency(waitingStream, signalStream);

        _logger.LogTrace("Synchronized stream {WaitingStream} to wait for stream {SignalStream} via event",
            waitingStream, signalStream);
    }

    /// <summary>
    /// Implements graph-like execution pattern with dependencies
    /// </summary>
    public async Task ExecuteGraphAsync(
        MetalExecutionGraph graph,
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
                    var streamHandle = await CreateStreamAsync(MetalStreamFlags.Concurrent, node.Priority, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        _ = await ExecuteCommandAsync(streamHandle.StreamId, node.Operation, node.Id, cancellationToken).ConfigureAwait(false);
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

        _logger.LogDebug("Completed execution graph with {TotalNodes} nodes in {LevelCount} levels", executionPlan.TotalNodes, executionPlan.Levels.Count);
    }

    /// <summary>
    /// Checks if a stream is ready (all operations completed)
    /// </summary>
    public bool IsStreamReady(StreamId streamId)
    {
        ThrowIfDisposed();

        if (!_activeStreams.TryGetValue(streamId, out _))
        {
            return false;
        }

        // For Metal, we check if the command queue has pending work
        // This would require native Metal calls to check queue status
        // For now, return true as a placeholder
        return true;
    }

    /// <summary>
    /// Gets comprehensive stream statistics for monitoring
    /// </summary>
    public MetalStreamStatistics GetStatistics()
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

        var optimalStreams = _isAppleSilicon ? APPLE_SILICON_OPTIMAL_STREAMS : INTEL_MAC_OPTIMAL_STREAMS;

        return new MetalStreamStatistics
        {
            ActiveStreams = activeCount,
            BusyStreams = busyCount,
            IdleStreams = activeCount - busyCount,
            StreamGroups = _streamGroups.Count,
            AverageStreamAge = activeCount > 0 ? totalAge / activeCount : 0,
            DependencyCount = _dependencyTracker.GetDependencyCount(),
            OptimalConcurrentStreams = optimalStreams,
            MaxConcurrentStreams = MAX_CONCURRENT_STREAMS,
            TotalStreamsCreated = _totalStreamsCreated,
            TotalCommandsExecuted = _totalCommandsExecuted,
            IsAppleSilicon = _isAppleSilicon
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
                _logger.LogError(ex, "Error in stream callback execution for stream {StreamId}", streamId);
            }
        });
    }

    /// <summary>
    /// Optimizes stream usage for current hardware
    /// </summary>
    public void OptimizeStreamUsage()
    {
        ThrowIfDisposed();

        lock (_lockObject)
        {
            // Rebalance streams based on hardware utilization
            var activeStreamCount = _activeStreams.Values.Count(s => !IsStreamReady(s.StreamId));
            var optimalCount = Math.Min(activeStreamCount,

                _isAppleSilicon ? APPLE_SILICON_OPTIMAL_STREAMS : INTEL_MAC_OPTIMAL_STREAMS);

            // Prefer high-priority streams for active work
            var highPriorityStreams = _activeStreams.Values
                .Where(s => s.Priority == MetalStreamPriority.High && !IsStreamReady(s.StreamId))
                .Take(optimalCount)
                .ToList();

            _logger.LogDebug("{Architecture} optimization: {ActiveStreams} active streams, {HighPriorityStreams} high-priority active", _isAppleSilicon ? "Apple Silicon" : "Intel Mac", activeStreamCount, highPriorityStreams.Count);

            // Additional optimization: cleanup old idle streams
            CleanupIdleStreams(TimeSpan.FromMinutes(5));
        }
    }

    internal void ReturnStreamToPool(StreamId streamId)
    {
        if (_activeStreams.TryRemove(streamId, out var streamInfo))
        {
            // Release the command queue
            if (streamInfo.CommandQueue != IntPtr.Zero &&

                streamInfo.CommandQueue != _defaultCommandQueue &&
                !_optimizedCommandQueues.Contains(streamInfo.CommandQueue))
            {
                MetalNative.ReleaseCommandQueue(streamInfo.CommandQueue);
            }
            _ = _streamCreationSemaphore.Release();
        }
    }

    private void Initialize()
    {
        // Create default command queue
        _defaultCommandQueue = MetalNative.CreateCommandQueue(_device);
        if (_defaultCommandQueue == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create default Metal command queue");
        }

        _logger.LogDebug("Initialized Metal command stream manager for {Architecture}", _isAppleSilicon ? "Apple Silicon" : "Intel Mac");
    }

    private IntPtr CreateOptimizedCommandQueue()
    {
        var commandQueue = MetalNative.CreateCommandQueue(_device);
        if (commandQueue == IntPtr.Zero)
        {
            _logger.LogWarning("Failed to create optimized command queue");
        }
        return commandQueue;
    }

    private IntPtr CreateCommandQueueWithPriority(MetalStreamPriority priority)
        // For now, create a standard command queue
        // In a full implementation, this would set queue priority if supported
        => MetalNative.CreateCommandQueue(_device);

    private static async Task<bool> CommitAndWaitAsync(IntPtr commandBuffer, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        // Set completion handler
        MetalNative.SetCommandBufferCompletionHandler(commandBuffer, (status) =>
        {
            if (status == MetalCommandBufferStatus.Completed)
            {
                _ = tcs.TrySetResult(true);
            }
            else
            {
                _ = tcs.TrySetException(new InvalidOperationException($"Command buffer failed with status: {status}"));
            }
        });

        // Commit the command buffer
        MetalNative.CommitCommandBuffer(commandBuffer);

        // Wait for completion with cancellation support
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
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
                DestroyStream(streamInfo.CommandQueue);
                _ = _streamCreationSemaphore.Release();

                _logger.LogTrace("Cleaned up idle stream {StreamId} (idle for {IdleTime})",
                    streamId, DateTimeOffset.UtcNow - streamInfo.LastUsed);
            }
        }

        if (idleStreams.Count > 0)
        {
            _logger.LogDebug("Cleaned up {IdleStreamCount} idle streams", idleStreams.Count);
        }
    }

    private void DestroyStream(IntPtr commandQueue)
    {
        if (commandQueue == IntPtr.Zero ||

            commandQueue == _defaultCommandQueue ||

            _optimizedCommandQueues.Contains(commandQueue))
        {
            return; // Don't destroy default or optimized command queues
        }

        try
        {
            MetalNative.ReleaseCommandQueue(commandQueue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while destroying command queue {CommandQueue}", commandQueue);
        }
    }

    private void PerformMaintenance(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            OptimizeStreamUsage();
            _commandBufferPool.Cleanup();
            _dependencyTracker.Cleanup();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during stream manager maintenance");
        }
    }

    private static bool DetectAppleSilicon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }


        try
        {
            return System.Runtime.InteropServices.RuntimeInformation.OSArchitecture ==

                   System.Runtime.InteropServices.Architecture.Arm64;
        }
        catch
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MetalCommandStream));
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

                _ = Task.WaitAll(syncTasks, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error synchronizing streams during disposal");
            }

            // Destroy all active streams
            foreach (var streamInfo in _activeStreams.Values)
            {
                DestroyStream(streamInfo.CommandQueue);
            }

            // Destroy optimized command queues
            foreach (var commandQueue in _optimizedCommandQueues)
            {
                if (commandQueue != IntPtr.Zero)
                {
                    try
                    {
                        MetalNative.ReleaseCommandQueue(commandQueue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error releasing optimized command queue during disposal");
                    }
                }
            }

            // Destroy default command queue
            if (_defaultCommandQueue != IntPtr.Zero)
            {
                try
                {
                    MetalNative.ReleaseCommandQueue(_defaultCommandQueue);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error releasing default command queue during disposal");
                }
            }

            _commandBufferPool?.Dispose();
            _streamCreationSemaphore?.Dispose();
            _dependencyTracker?.Dispose();

            _logger.LogInformation("Metal Command Stream disposed: created {TotalStreams} streams, executed {TotalCommands} commands", _totalStreamsCreated, _totalCommandsExecuted);
        }
    }
}

// Supporting types and classes follow...

/// <summary>
/// Unique identifier for Metal streams
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
/// Information about an active Metal stream
/// </summary>
internal sealed class MetalStreamInfo
{
    public StreamId StreamId { get; set; }
    public IntPtr CommandQueue { get; set; }
    public MetalStreamFlags Flags { get; set; }
    public MetalStreamPriority Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastUsed { get; set; }
    public long OperationCount { get; set; }
}

/// <summary>
/// Metal stream flags
/// </summary>
public enum MetalStreamFlags
{
    Default,
    Concurrent,
    Serial
}

/// <summary>
/// Metal stream priority levels
/// </summary>
public enum MetalStreamPriority
{
    Low,
    Normal,
    High
}

/// <summary>
/// Handle for managed Metal streams with automatic cleanup
/// </summary>
public sealed class MetalStreamHandle : IDisposable
{
    private readonly MetalCommandStream _manager;
    private volatile bool _disposed;

    internal MetalStreamHandle(StreamId streamId, IntPtr commandQueue, MetalCommandStream manager)
    {
        StreamId = streamId;
        CommandQueue = commandQueue;
        _manager = manager;
    }

    public StreamId StreamId { get; }
    public IntPtr CommandQueue { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _manager.ReturnStreamToPool(StreamId);
        }
    }
}

/// <summary>
/// Result of a Metal command execution
/// </summary>
public sealed class MetalCommandExecutionResult
{
    public StreamId StreamId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public TimeSpan ExecutionTime { get; set; }
    public bool Success { get; set; }
    public Exception? Error { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
}

/// <summary>
/// Group of streams working together
/// </summary>
public sealed class MetalStreamGroup(string name, int capacity = 4) : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, IntPtr> _streams = new();
    private volatile bool _disposed;

    public string Name { get; } = name;
    public IReadOnlyDictionary<StreamId, IntPtr> Streams => _streams;

    internal void AddStream(StreamId streamId, IntPtr commandQueue) => _streams[streamId] = commandQueue;

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
internal sealed class MetalStreamDependencyTracker : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, HashSet<StreamId>> _dependencies;
    private readonly object _lockObject = new();

    public MetalStreamDependencyTracker()
    {
        _dependencies = new ConcurrentDictionary<StreamId, HashSet<StreamId>>();
    }

    public void AddDependency(StreamId dependent, StreamId dependency)
    {
        lock (_lockObject)
        {
            _ = _dependencies.GetOrAdd(dependent, _ => []).Add(dependency);
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
            // Remove old dependency tracking
            var keysToRemove = _dependencies
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _ = _dependencies.TryRemove(key, out _);
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
/// Statistics for Metal stream usage
/// </summary>
public sealed class MetalStreamStatistics
{
    public int ActiveStreams { get; set; }
    public int BusyStreams { get; set; }
    public int IdleStreams { get; set; }
    public int StreamGroups { get; set; }
    public double AverageStreamAge { get; set; }
    public int DependencyCount { get; set; }
    public int OptimalConcurrentStreams { get; set; }
    public int MaxConcurrentStreams { get; set; }
    public long TotalStreamsCreated { get; set; }
    public long TotalCommandsExecuted { get; set; }
    public bool IsAppleSilicon { get; set; }
}

/// <summary>
/// Execution graph for coordinated stream operations
/// </summary>
public sealed class MetalExecutionGraph
{
    private readonly List<MetalExecutionNode> _nodes = [];

    public void AddNode(string id, Func<IntPtr, IntPtr, Task> operation,
                       MetalStreamPriority priority = MetalStreamPriority.Normal,
                       params string[] dependencies)
    {
        _nodes.Add(new MetalExecutionNode
        {
            Id = id,
            Operation = operation,
            Priority = priority,
            Dependencies = [.. dependencies]
        });
    }

    internal MetalExecutionPlan BuildExecutionPlan()
    {
        // Topological sort to determine execution levels
        var levels = new List<MetalExecutionLevel>();
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

            levels.Add(new MetalExecutionLevel { Nodes = readyNodes });

            foreach (var node in readyNodes)
            {
                _ = completed.Add(node.Id);
                _ = remaining.Remove(node);
            }
        }

        return new MetalExecutionPlan
        {
            Levels = levels,
            TotalNodes = _nodes.Count
        };
    }
}

/// <summary>
/// Node in the Metal execution graph
/// </summary>
public sealed class MetalExecutionNode
{
    public string Id { get; set; } = string.Empty;
    public Func<IntPtr, IntPtr, Task> Operation { get; set; } = null!;
    public MetalStreamPriority Priority { get; set; }
    public List<string> Dependencies { get; set; } = [];
}

/// <summary>
/// Level in the Metal execution plan
/// </summary>
public sealed class MetalExecutionLevel
{
    public List<MetalExecutionNode> Nodes { get; set; } = [];
}

/// <summary>
/// Complete Metal execution plan
/// </summary>
public sealed class MetalExecutionPlan
{
    public List<MetalExecutionLevel> Levels { get; set; } = [];
    public int TotalNodes { get; set; }
}