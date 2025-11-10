// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;

namespace DotCompute.Backends.CUDA.Execution.Types;

/// <summary>
/// Internal information about an active CUDA stream.
/// </summary>
internal sealed class CudaStreamInfo
{
    public required StreamId StreamId { get; init; }
    public required IntPtr CudaStream { get; init; }
    public required int Priority { get; init; }
    public required CudaStreamFlags Flags { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public long CommandsExecuted { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public bool IsFromPool { get; init; }
}

/// <summary>
/// Handle for managed CUDA streams with automatic cleanup.
/// </summary>
/// <remarks>
/// Implements RAII pattern for CUDA stream lifecycle management with pool return support.
/// </remarks>
public class CudaStreamHandle : IDisposable
{
    private readonly IntPtr _cudaStream;
    private readonly Action<StreamId>? _onDispose;
    private readonly Action<IntPtr>? _returnToPool;
    private readonly bool _isFromPool;
    private bool _disposed;

    public CudaStreamHandle(
        StreamId streamId,
        IntPtr cudaStream,
        bool isFromPool = false,
        Action<StreamId>? onDispose = null,
        Action<IntPtr>? returnToPool = null)
    {
        StreamId = streamId;
        _cudaStream = cudaStream;
        _isFromPool = isFromPool;
        _onDispose = onDispose;
        _returnToPool = returnToPool;
    }

    public StreamId StreamId { get; }
    public IntPtr CudaStream => _cudaStream;
    public bool IsFromPool => _isFromPool;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isFromPool && _returnToPool != null)
            _returnToPool(_cudaStream);
        else
            _onDispose?.Invoke(StreamId);

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Group of related CUDA streams for coordinated execution.
/// </summary>
public sealed class CudaStreamGroup(string name, int capacity = 4) : IDisposable
{
    private readonly List<CudaStreamHandle> _streams = new(capacity);
    private bool _disposed;

    public string Name { get; } = name;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<CudaStreamHandle> Streams => _streams;

    public void AddStream(CudaStreamHandle stream) => _streams.Add(stream);

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
/// Internal dependency tracker for CUDA stream synchronization.
/// </summary>
internal sealed class CudaStreamDependencyTracker : IDisposable
{
    private readonly ConcurrentDictionary<StreamId, HashSet<StreamId>> _dependencies = new();
    private readonly ConcurrentDictionary<StreamId, List<IntPtr>> _cudaEvents = new();
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

    public void AddCudaEvent(StreamId streamId, IntPtr cudaEvent)
    {
        var events = _cudaEvents.GetOrAdd(streamId, _ => []);
        lock (_lockObject)
        {
            events.Add(cudaEvent);
        }
    }

    public bool HasDependencies(StreamId streamId) =>
        _dependencies.TryGetValue(streamId, out var deps) && deps.Count > 0;

    public IEnumerable<IntPtr> GetCudaEvents(StreamId streamId) =>
        _cudaEvents.TryGetValue(streamId, out var events) ? events : [];

    public void ClearDependencies(StreamId streamId)
    {
        _dependencies.TryRemove(streamId, out _);
        _cudaEvents.TryRemove(streamId, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dependencies.Clear();
        _cudaEvents.Clear();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Statistics for CUDA stream operations.
/// </summary>
public sealed class CudaStreamStatistics
{
    public long TotalStreamsCreated { get; set; }
    public long TotalStreamPoolHits { get; set; }
    public long TotalStreamPoolMisses { get; set; }
    public long TotalCommandsExecuted { get; set; }
    public int ActiveStreams { get; set; }
    public int PooledStreams { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public DateTimeOffset LastOperationTime { get; set; }

    public double StreamPoolHitRate =>
        (TotalStreamPoolHits + TotalStreamPoolMisses) > 0
            ? (double)TotalStreamPoolHits / (TotalStreamPoolHits + TotalStreamPoolMisses)
            : 0.0;
}
