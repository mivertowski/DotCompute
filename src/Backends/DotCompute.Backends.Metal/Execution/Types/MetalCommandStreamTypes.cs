// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Backends.Metal.Execution.Types;

/// <summary>
/// Internal information about an active Metal stream.
/// </summary>
internal sealed class MetalStreamInfo
{
    public required StreamId StreamId { get; init; }
    public required IntPtr CommandQueue { get; init; }
    public required MetalStreamPriority Priority { get; init; }
    public MetalStreamFlags Flags { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUsed { get; set; }
    public long CommandsExecuted { get; set; }
    public long OperationCount { get; set; }
}

/// <summary>
/// Handle for managed Metal streams with automatic cleanup and resource management.
/// </summary>
public sealed class MetalStreamHandle : IDisposable
{
    private readonly IntPtr _commandQueue;
    private readonly Action<StreamId>? _onDispose;
    private bool _disposed;

    public MetalStreamHandle(StreamId streamId, IntPtr commandQueue, Action<StreamId>? onDispose = null)
    {
        StreamId = streamId;
        _commandQueue = commandQueue;
        _onDispose = onDispose;
    }

    public StreamId StreamId { get; }
    public IntPtr CommandQueue => _commandQueue;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose?.Invoke(StreamId);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result of Metal command execution with timing and status information.
/// </summary>
public sealed class MetalCommandExecutionResult
{
    public StreamId StreamId { get; init; }
    public string OperationName { get; init; } = string.Empty;
    public bool IsSuccessful { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public DateTimeOffset CompletionTime { get; init; }
}

/// <summary>
/// Group of related Metal streams for batch operations and coordinated execution.
/// </summary>
public sealed class MetalStreamGroup : IDisposable
{
    private readonly List<MetalStreamHandle> _streams = [];
    private readonly List<StreamId> _streamIds = [];
    private readonly List<IntPtr> _commandQueues = [];
    private bool _disposed;

    public MetalStreamGroup(string name, int capacity = 0)
    {
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
        if (capacity > 0)
        {
            _streams.Capacity = capacity;
            _streamIds.Capacity = capacity;
            _commandQueues.Capacity = capacity;
        }
    }

    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<MetalStreamHandle> Streams => _streams;

    public void AddStream(MetalStreamHandle stream) => _streams.Add(stream);

    public void AddStream(StreamId streamId, IntPtr commandQueue)
    {
        _streamIds.Add(streamId);
        _commandQueues.Add(commandQueue);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var stream in _streams)
        {
            stream.Dispose();
        }

        _streams.Clear();
        _streamIds.Clear();
        _commandQueues.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal dependency tracker for Metal stream synchronization.
/// </summary>
internal sealed class MetalStreamDependencyTracker : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, HashSet<StreamId>> _dependencies = new();
    private readonly Lock _lockObject = new();
    private bool _disposed;

    public void AddDependency(StreamId dependentStream, StreamId prerequisiteStream)
    {
        lock (_lockObject)
        {
            var deps = _dependencies.GetOrAdd(dependentStream, _ => []);
            deps.Add(prerequisiteStream);
        }
    }

    public bool HasDependencies(StreamId streamId)
    {
        return _dependencies.TryGetValue(streamId, out var deps) && deps.Count > 0;
    }

    public void ClearDependencies(StreamId streamId)
    {
        _dependencies.TryRemove(streamId, out _);
    }

    public int GetDependencyCount()
    {
        return _dependencies.Sum(kvp => kvp.Value.Count);
    }

    public void Cleanup()
    {
        lock (_lockObject)
        {
            var toRemove = _dependencies
                .Where(kvp => kvp.Value.Count == 0)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _dependencies.TryRemove(key, out _);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dependencies.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Statistics for Metal command stream operations.
/// </summary>
public sealed class MetalStreamStatistics
{
    public long TotalStreamsCreated { get; set; }
    public long TotalCommandsExecuted { get; set; }
    public int ActiveStreams { get; set; }
    public int BusyStreams { get; set; }
    public int IdleStreams { get; set; }
    public int StreamGroups { get; set; }
    public TimeSpan AverageStreamAge { get; set; }
    public int DependencyCount { get; set; }
    public int OptimalConcurrentStreams { get; set; }
    public int MaxConcurrentStreams { get; set; }
    public bool IsAppleSilicon { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public DateTimeOffset LastOperationTime { get; set; }
}
