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
    public DateTimeOffset CreatedAt { get; init; }
    public long CommandsExecuted { get; set; }
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
        if (_disposed) return;
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
    public required bool IsSuccessful { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public DateTimeOffset CompletionTime { get; init; }
}

/// <summary>
/// Group of related Metal streams for batch operations and coordinated execution.
/// </summary>
public sealed class MetalStreamGroup : IDisposable
{
    private readonly List<MetalStreamHandle> _streams = [];
    private bool _disposed;

    public MetalStreamGroup(string name)
    {
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<MetalStreamHandle> Streams => _streams;

    public void AddStream(MetalStreamHandle stream) => _streams.Add(stream);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var stream in _streams)
            stream.Dispose();
        _streams.Clear();
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

    public void Dispose()
    {
        if (_disposed) return;
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
    public TimeSpan TotalExecutionTime { get; set; }
    public DateTimeOffset LastOperationTime { get; set; }
}
