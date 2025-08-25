// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution;

/// <summary>
/// Production-grade CUDA stream manager with stream pools, priorities, callbacks, and graph capture.
/// </summary>
public sealed class CudaStreamManagerProduction : IDisposable
{
    private readonly ILogger<CudaStreamManagerProduction> _logger;
    private readonly CudaContext _context;
    private readonly ConcurrentDictionary<string, StreamInfo> _namedStreams;
    private readonly ConcurrentBag<IntPtr> _streamPool;
    private readonly ConcurrentDictionary<IntPtr, StreamCallbackInfo> _streamCallbacks;
    private readonly Lock _streamLock = new();
    private readonly int _maxPoolSize;
    private volatile bool _disposed;

    // Stream priorities (CUDA supports priority range, typically -1 to 0)
    private int _lowestPriority;
    private int _highestPriority;

    // Graph capture state
    private IntPtr _captureStream = IntPtr.Zero;
    private IntPtr _currentGraph = IntPtr.Zero;
    private bool _isCapturing;

    public CudaStreamManagerProduction(
        CudaContext context,
        ILogger<CudaStreamManagerProduction> logger,
        int maxPoolSize = 32)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPoolSize = maxPoolSize;
        _namedStreams = new ConcurrentDictionary<string, StreamInfo>();
        _streamPool = new ConcurrentBag<IntPtr>();
        _streamCallbacks = new ConcurrentDictionary<IntPtr, StreamCallbackInfo>();

        InitializePriorityRange();
        InitializeStreamPool();
    }

    /// <summary>
    /// Gets whether stream priorities are supported.
    /// </summary>
    public bool PrioritiesSupported => _lowestPriority != _highestPriority;

    /// <summary>
    /// Gets whether graph capture is currently active.
    /// </summary>
    public bool IsCapturing => _isCapturing;

    /// <summary>
    /// Initializes the priority range for streams.
    /// </summary>
    private void InitializePriorityRange()
    {
        var result = CudaRuntime.cudaDeviceGetStreamPriorityRange(
            out _lowestPriority, out _highestPriority);
        
        if (result == CudaError.Success && _lowestPriority != _highestPriority)
        {
            _logger.LogInformation("Stream priorities supported: [{Lowest}, {Highest}]",
                _lowestPriority, _highestPriority);
        }
        else
        {
            _logger.LogInformation("Stream priorities not supported on this device");
            _lowestPriority = 0;
            _highestPriority = 0;
        }
    }

    /// <summary>
    /// Initializes the stream pool with pre-allocated streams.
    /// </summary>
    private void InitializeStreamPool()
    {
        _logger.LogDebug("Initializing stream pool with {Size} streams", _maxPoolSize / 2);
        
        // Pre-allocate half the max pool size
        for (int i = 0; i < _maxPoolSize / 2; i++)
        {
            try
            {
                var stream = CreateStreamInternal(StreamPriority.Normal, StreamFlags.NonBlocking);
                _streamPool.Add(stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pre-allocate stream {Index}", i);
                break;
            }
        }
    }

    /// <summary>
    /// Creates or gets a stream with the specified priority.
    /// </summary>
    public IntPtr CreateStream(
        string? name = null,
        StreamPriority priority = StreamPriority.Normal,
        StreamFlags flags = StreamFlags.NonBlocking)
    {
        ThrowIfDisposed();

        // Check for named stream
        if (!string.IsNullOrEmpty(name))
        {
            return _namedStreams.GetOrAdd(name, _ => 
            {
                var stream = GetOrCreateStream(priority, flags);
                return new StreamInfo
                {
                    Stream = stream,
                    Name = name,
                    Priority = priority,
                    Flags = flags,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }).Stream;
        }

        return GetOrCreateStream(priority, flags);
    }

    /// <summary>
    /// Gets or creates a stream from the pool.
    /// </summary>
    private IntPtr GetOrCreateStream(StreamPriority priority, StreamFlags flags)
    {
        // Try to get from pool first
        if (priority == StreamPriority.Normal && _streamPool.TryTake(out var pooledStream))
        {
            _logger.LogDebug("Reusing stream from pool");
            return pooledStream;
        }

        // Create new stream
        return CreateStreamInternal(priority, flags);
    }

    /// <summary>
    /// Creates a new CUDA stream internally.
    /// </summary>
    private IntPtr CreateStreamInternal(StreamPriority priority, StreamFlags flags)
    {
        IntPtr stream;
        CudaError result;

        if (PrioritiesSupported && priority != StreamPriority.Normal)
        {
            int cudaPriority = MapPriorityToCuda(priority);
            result = CudaRuntime.cudaStreamCreateWithPriority(
                out stream, (uint)flags, cudaPriority);
        }
        else
        {
            result = CudaRuntime.cudaStreamCreateWithFlags(
                out stream, (uint)flags);
        }

        CudaRuntime.CheckError(result, "creating stream");
        
        _logger.LogDebug("Created new stream with priority {Priority}, flags {Flags}",
            priority, flags);
        
        return stream;
    }

    /// <summary>
    /// Maps StreamPriority enum to CUDA priority value.
    /// </summary>
    private int MapPriorityToCuda(StreamPriority priority)
    {
        return priority switch
        {
            StreamPriority.Highest => _highestPriority,
            StreamPriority.High => (_highestPriority + _lowestPriority * 3) / 4,
            StreamPriority.Normal => (_highestPriority + _lowestPriority) / 2,
            StreamPriority.Low => (_highestPriority * 3 + _lowestPriority) / 4,
            StreamPriority.Lowest => _lowestPriority,
            _ => (_highestPriority + _lowestPriority) / 2
        };
    }

    /// <summary>
    /// Returns a stream to the pool.
    /// </summary>
    public void ReturnStream(IntPtr stream)
    {
        ThrowIfDisposed();
        
        if (stream == IntPtr.Zero)
            return;

        // Don't pool named streams
        if (_namedStreams.Values.Any(s => s.Stream == stream))
            return;

        // Clean up callbacks
        _streamCallbacks.TryRemove(stream, out _);

        // Return to pool if not full
        if (_streamPool.Count < _maxPoolSize)
        {
            _streamPool.Add(stream);
            _logger.LogDebug("Returned stream to pool (pool size: {Size})", _streamPool.Count);
        }
        else
        {
            // Destroy excess stream
            DestroyStream(stream);
        }
    }

    /// <summary>
    /// Synchronizes a stream.
    /// </summary>
    public void SynchronizeStream(IntPtr stream)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaStreamSynchronize(stream);
        CudaRuntime.CheckError(result, "synchronizing stream");
    }

    /// <summary>
    /// Adds a callback to a stream.
    /// </summary>
    public void AddCallback(IntPtr stream, Action callback, string? description = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);

        // Store callback info
        var callbackInfo = new StreamCallbackInfo
        {
            Callback = callback,
            Description = description,
            AddedAt = DateTimeOffset.UtcNow
        };
        
        _streamCallbacks.TryAdd(stream, callbackInfo);

        // Create GC handle to pass to native callback
        var handle = GCHandle.Alloc(callbackInfo);
        
        try
        {
            // Add callback to stream
            var result = CudaRuntime.cudaStreamAddCallback(
                stream,
                StreamCallbackThunk,
                GCHandle.ToIntPtr(handle),
                0); // flags must be 0
                
            CudaRuntime.CheckError(result, "adding stream callback");
            
            _logger.LogDebug("Added callback to stream: {Description}", description);
        }
        catch
        {
            handle.Free();
            _streamCallbacks.TryRemove(stream, out _);
            throw;
        }
    }

    /// <summary>
    /// Native callback thunk that marshals to managed callback.
    /// </summary>
    [UnmanagedCallersOnly]
    private static void StreamCallbackThunk(IntPtr stream, CudaError status, IntPtr userData)
    {
        if (userData == IntPtr.Zero)
            return;

        var handle = GCHandle.FromIntPtr(userData);
        try
        {
            if (handle.Target is StreamCallbackInfo callbackInfo)
            {
                if (status == CudaError.Success)
                {
                    callbackInfo.Callback.Invoke();
                }
                else
                {
                    // Log error but don't throw in callback
                    Console.WriteLine($"Stream callback error: {status}");
                }
            }
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Begins graph capture on a stream.
    /// </summary>
    public void BeginCapture(IntPtr stream, CudaGraphCaptureMode mode = CudaGraphCaptureMode.Global)
    {
        ThrowIfDisposed();
        
        if (_isCapturing)
            throw new InvalidOperationException("Graph capture already in progress");

        var result = CudaRuntime.cudaStreamBeginCapture(stream, (uint)mode);
        CudaRuntime.CheckError(result, "beginning graph capture");
        
        _captureStream = stream;
        _isCapturing = true;
        
        _logger.LogInformation("Started graph capture on stream with mode {Mode}", mode);
    }

    /// <summary>
    /// Ends graph capture and returns the captured graph.
    /// </summary>
    public IntPtr EndCapture()
    {
        ThrowIfDisposed();
        
        if (!_isCapturing)
            throw new InvalidOperationException("No graph capture in progress");

        var result = CudaRuntime.cudaStreamEndCapture(_captureStream, out IntPtr graph);
        CudaRuntime.CheckError(result, "ending graph capture");
        
        _currentGraph = graph;
        _isCapturing = false;
        _captureStream = IntPtr.Zero;
        
        _logger.LogInformation("Completed graph capture");
        
        return graph;
    }

    /// <summary>
    /// Creates an executable graph instance from a captured graph.
    /// </summary>
    public IntPtr InstantiateGraph(IntPtr graph)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaGraphInstantiate(
            out IntPtr graphExec, graph, IntPtr.Zero, IntPtr.Zero, 0);
        CudaRuntime.CheckError(result, "instantiating graph");
        
        _logger.LogDebug("Created executable graph instance");
        
        return graphExec;
    }

    /// <summary>
    /// Launches a graph on a stream.
    /// </summary>
    public void LaunchGraph(IntPtr graphExec, IntPtr stream)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaGraphLaunch(graphExec, stream);
        CudaRuntime.CheckError(result, "launching graph");
        
        _logger.LogDebug("Launched graph on stream");
    }

    /// <summary>
    /// Waits for an event on a stream.
    /// </summary>
    public void WaitEvent(IntPtr stream, IntPtr eventHandle)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaStreamWaitEvent(stream, eventHandle, 0);
        CudaRuntime.CheckError(result, "stream waiting for event");
    }

    /// <summary>
    /// Records an event on a stream.
    /// </summary>
    public void RecordEvent(IntPtr eventHandle, IntPtr stream)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaEventRecord(eventHandle, stream);
        CudaRuntime.CheckError(result, "recording event on stream");
    }

    /// <summary>
    /// Queries if a stream has completed all operations.
    /// </summary>
    public bool IsStreamComplete(IntPtr stream)
    {
        ThrowIfDisposed();
        
        var result = CudaRuntime.cudaStreamQuery(stream);
        
        if (result == CudaError.Success)
            return true;
        
        if (result == CudaError.NotReady)
            return false;
            
        CudaRuntime.CheckError(result, "querying stream status");
        return false;
    }

    /// <summary>
    /// Gets stream statistics.
    /// </summary>
    public StreamStatistics GetStatistics()
    {
        return new StreamStatistics
        {
            TotalStreamsCreated = _namedStreams.Count + _streamPool.Count,
            NamedStreams = _namedStreams.Count,
            PooledStreams = _streamPool.Count,
            ActiveCallbacks = _streamCallbacks.Count,
            PrioritiesSupported = PrioritiesSupported,
            GraphCaptureActive = _isCapturing
        };
    }

    /// <summary>
    /// Destroys a stream.
    /// </summary>
    private void DestroyStream(IntPtr stream)
    {
        if (stream == IntPtr.Zero)
            return;

        try
        {
            var result = CudaRuntime.cudaStreamDestroy(stream);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to destroy stream: {Error}", result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error destroying stream");
        }
    }

    /// <summary>
    /// Cleans up the stream pool.
    /// </summary>
    private void CleanupStreamPool()
    {
        while (_streamPool.TryTake(out var stream))
        {
            DestroyStream(stream);
        }
    }

    /// <summary>
    /// Cleans up named streams.
    /// </summary>
    private void CleanupNamedStreams()
    {
        foreach (var streamInfo in _namedStreams.Values)
        {
            DestroyStream(streamInfo.Stream);
        }
        _namedStreams.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            try
            {
                // Clean up graph resources
                if (_currentGraph != IntPtr.Zero)
                {
                    CudaRuntime.cudaGraphDestroy(_currentGraph);
                }

                // Clean up streams
                CleanupNamedStreams();
                CleanupStreamPool();

                _streamCallbacks.Clear();
            }
            finally
            {
                _lock?.Dispose();
            }
        }
    }

    /// <summary>
    /// Stream information.
    /// </summary>
    private sealed class StreamInfo
    {
        public IntPtr Stream { get; init; }
        public string Name { get; init; } = string.Empty;
        public StreamPriority Priority { get; init; }
        public StreamFlags Flags { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// Stream callback information.
    /// </summary>
    private sealed class StreamCallbackInfo
    {
        public Action Callback { get; init; } = () => { };
        public string? Description { get; init; }
        public DateTimeOffset AddedAt { get; init; }
    }
}

/// <summary>
/// Stream priority levels.
/// </summary>
public enum StreamPriority
{
    Lowest,
    Low,
    Normal,
    High,
    Highest
}

/// <summary>
/// Stream creation flags.
/// </summary>
[Flags]
public enum StreamFlags : uint
{
    Default = 0x00,
    NonBlocking = 0x01
}

/// <summary>
/// Stream statistics.
/// </summary>
public sealed class StreamStatistics
{
    public int TotalStreamsCreated { get; init; }
    public int NamedStreams { get; init; }
    public int PooledStreams { get; init; }
    public int ActiveCallbacks { get; init; }
    public bool PrioritiesSupported { get; init; }
    public bool GraphCaptureActive { get; init; }
}