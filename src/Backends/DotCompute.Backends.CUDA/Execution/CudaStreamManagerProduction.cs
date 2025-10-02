// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Execution;

/// <summary>
/// Delegate for CUDA stream callbacks.
/// </summary>
/// <param name="stream">The CUDA stream handle.</param>
/// <param name="status">The CUDA error status.</param>
/// <param name="userData">User data pointer.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void StreamCallbackDelegate(IntPtr stream, CudaError status, IntPtr userData);

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
    private readonly int _maxPoolSize;
    private volatile bool _disposed;

    // Stream priorities (CUDA supports priority range, typically -1 to 0)
    private int _lowestPriority;
    private int _highestPriority;

    // Graph capture state
    private IntPtr _captureStream = IntPtr.Zero;
    private IntPtr _currentGraph = IntPtr.Zero;
    private bool _isCapturing;
    /// <summary>
    /// Initializes a new instance of the CudaStreamManagerProduction class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="maxPoolSize">The max pool size.</param>

    public CudaStreamManagerProduction(
        CudaContext context,
        ILogger<CudaStreamManagerProduction> logger,
        int maxPoolSize = 32)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPoolSize = maxPoolSize;
        _namedStreams = new ConcurrentDictionary<string, StreamInfo>();
        _streamPool = [];
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
            _logger.LogInfoMessage($"Stream priorities supported: [{_lowestPriority}, {_highestPriority}]");
        }
        else
        {
            _logger.LogInfoMessage("Stream priorities not supported on this device");
            _lowestPriority = 0;
            _highestPriority = 0;
        }
    }

    /// <summary>
    /// Initializes the stream pool with pre-allocated streams.
    /// </summary>
    private void InitializeStreamPool()
    {
        _logger.LogDebugMessage(" streams");

        // Pre-allocate half the max pool size

        for (var i = 0; i < _maxPoolSize / 2; i++)
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
            _logger.LogDebugMessage("Reusing stream from pool");
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
        var stream = IntPtr.Zero;
        CudaError result;

        if (PrioritiesSupported && priority != StreamPriority.Normal)
        {
            var cudaPriority = MapPriorityToCuda(priority);
            result = CudaRuntime.cudaStreamCreateWithPriority(
                ref stream, (uint)flags, cudaPriority);
        }
        else
        {
            result = CudaRuntime.cudaStreamCreateWithFlags(
                ref stream, (uint)flags);
        }

        CudaRuntime.CheckError(result, "creating stream");


        _logger.LogDebugMessage($"Created new stream with priority {priority}, flags {flags}");


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
        {
            return;
        }

        // Don't pool named streams

        if (_namedStreams.Values.Any(s => s.Stream == stream))
        {
            return;
        }

        // Clean up callbacks

        _ = _streamCallbacks.TryRemove(stream, out _);

        // Return to pool if not full
        if (_streamPool.Count < _maxPoolSize)
        {
            _streamPool.Add(stream);
            _logger.LogDebugMessage(")");
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
    /// Asynchronously synchronizes all active streams.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task that completes when all streams are synchronized.</returns>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();


        await Task.Run(() =>
        {
            // Synchronize all named streams
            foreach (var streamInfo in _namedStreams.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();


                try
                {
                    var result = CudaRuntime.cudaStreamSynchronize(streamInfo.Stream);
                    CudaRuntime.CheckError(result, $"synchronizing named stream '{streamInfo.Name}'");


                    _logger.LogDebugMessage("'");
                }
                catch (Exception)
                {
                    _logger.LogErrorMessage("Failed to execute kernel launch");
                    throw;
                }
            }

            // Synchronize all pooled streams

            var pooledStreams = _streamPool.ToArray();
            foreach (var stream in pooledStreams)
            {
                cancellationToken.ThrowIfCancellationRequested();


                try
                {
                    var result = CudaRuntime.cudaStreamSynchronize(stream);
                    CudaRuntime.CheckError(result, "synchronizing pooled stream");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to synchronize pooled stream");
                    // Continue with other streams - don't fail the entire operation
                }
            }

            // If graph capture is active, synchronize the capture stream

            if (_isCapturing && _captureStream != IntPtr.Zero)
            {
                cancellationToken.ThrowIfCancellationRequested();


                try
                {
                    var result = CudaRuntime.cudaStreamSynchronize(_captureStream);
                    CudaRuntime.CheckError(result, "synchronizing graph capture stream");


                    _logger.LogDebugMessage("Synchronized graph capture stream");
                }
                catch (Exception ex)
                {
                    _logger.LogErrorMessage(ex, "Failed to synchronize graph capture stream");
                    throw;
                }
            }

            // Finally, synchronize the device to ensure all operations complete

            var deviceResult = CudaRuntime.cudaDeviceSynchronize();
            CudaRuntime.CheckError(deviceResult, "synchronizing device");


            _logger.LogDebugMessage("Successfully synchronized all streams and device");


        }, cancellationToken).ConfigureAwait(false);
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

        _ = _streamCallbacks.TryAdd(stream, callbackInfo);

        // Create GC handle to pass to native callback
        var handle = GCHandle.Alloc(callbackInfo);


        try
        {
            // Add callback to stream
            var callbackPtr = Marshal.GetFunctionPointerForDelegate(StreamCallbackThunk);
            var result = CudaRuntime.cudaStreamAddCallback(
                stream,
                callbackPtr,
                GCHandle.ToIntPtr(handle),
                0); // flags must be 0


            CudaRuntime.CheckError(result, "adding stream callback");


            _logger.LogDebugMessage("");
        }
        catch
        {
            handle.Free();
            _ = _streamCallbacks.TryRemove(stream, out _);
            throw;
        }
    }

    /// <summary>
    /// Native callback thunk that marshals to managed callback.
    /// </summary>
    private static readonly StreamCallbackDelegate StreamCallbackThunk = (IntPtr stream, CudaError status, IntPtr userData) =>
    {
        if (userData == IntPtr.Zero)
        {
            return;
        }

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
                    // Log error but don't throw in callback - use proper logging in production
                    // Note: Cannot use ILogger in static callback, consider event-based error reporting
                    System.Diagnostics.Debug.WriteLine($"Stream callback error: {status}");
                }
            }
        }
        finally
        {
            handle.Free();
        }
    };

    /// <summary>
    /// Begins graph capture on a stream.
    /// </summary>
    public void BeginCapture(IntPtr stream, CudaGraphCaptureMode mode = CudaGraphCaptureMode.Global)
    {
        ThrowIfDisposed();


        if (_isCapturing)
        {

            throw new InvalidOperationException("Graph capture already in progress");
        }


        var result = CudaRuntime.cudaStreamBeginCapture(stream, (uint)mode);
        CudaRuntime.CheckError(result, "beginning graph capture");


        _captureStream = stream;
        _isCapturing = true;


        _logger.LogInfoMessage("");
    }

    /// <summary>
    /// Ends graph capture and returns the captured graph.
    /// </summary>
    public IntPtr EndCapture()
    {
        ThrowIfDisposed();


        if (!_isCapturing)
        {

            throw new InvalidOperationException("No graph capture in progress");
        }


        var result = CudaRuntime.cudaStreamEndCapture(_captureStream, ref _currentGraph);
        var graph = _currentGraph;
        CudaRuntime.CheckError(result, "ending graph capture");


        _currentGraph = graph;
        _isCapturing = false;
        _captureStream = IntPtr.Zero;


        _logger.LogInfoMessage("Completed graph capture");


        return graph;
    }

    /// <summary>
    /// Creates an executable graph instance from a captured graph.
    /// </summary>
    public IntPtr InstantiateGraph(IntPtr graph)
    {
        ThrowIfDisposed();


        var graphExec = IntPtr.Zero;
        var result = CudaRuntime.cudaGraphInstantiate(
            ref graphExec, graph, IntPtr.Zero, IntPtr.Zero, 0);
        CudaRuntime.CheckError(result, "instantiating graph");


        _logger.LogDebugMessage("Created executable graph instance");


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


        _logger.LogDebugMessage("Launched graph on stream");
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
        {
            return true;
        }


        if (result == CudaError.NotReady)
        {
            return false;
        }


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
        {
            return;
        }


        try
        {
            var result = CudaRuntime.cudaStreamDestroy(stream);
            if (result != CudaError.Success)
            {
                _logger.LogWarningMessage("");
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
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                    _ = CudaRuntime.cudaGraphDestroy(_currentGraph);
                }

                // Clean up streams
                CleanupNamedStreams();
                CleanupStreamPool();

                _streamCallbacks.Clear();
            }
            finally
            {
                // Note: Lock (_streamLock) does not require disposal in .NET 9
            }
        }
    }

    /// <summary>
    /// Stream information.
    /// </summary>
    private sealed class StreamInfo
    {
        /// <summary>
        /// Gets or sets the stream.
        /// </summary>
        /// <value>The stream.</value>
        public IntPtr Stream { get; init; }
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        /// <value>The priority.</value>
        public StreamPriority Priority { get; init; }
        /// <summary>
        /// Gets or sets the flags.
        /// </summary>
        /// <value>The flags.</value>
        public StreamFlags Flags { get; init; }
        /// <summary>
        /// Gets or sets the created at.
        /// </summary>
        /// <value>The created at.</value>
        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// Stream callback information.
    /// </summary>
    private sealed class StreamCallbackInfo
    {
        /// <summary>
        /// Gets or sets the callback.
        /// </summary>
        /// <value>The callback.</value>
        public Action Callback { get; init; } = () => { };
        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        /// <value>The description.</value>
        public string? Description { get; init; }
        /// <summary>
        /// Gets or sets the added at.
        /// </summary>
        /// <value>The added at.</value>
        public DateTimeOffset AddedAt { get; init; }
    }
}
/// <summary>
/// An stream priority enumeration.
/// </summary>

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
/// An stream flags enumeration.
/// </summary>

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
    /// <summary>
    /// Gets or sets the total streams created.
    /// </summary>
    /// <value>The total streams created.</value>
    public int TotalStreamsCreated { get; init; }
    /// <summary>
    /// Gets or sets the named streams.
    /// </summary>
    /// <value>The named streams.</value>
    public int NamedStreams { get; init; }
    /// <summary>
    /// Gets or sets the pooled streams.
    /// </summary>
    /// <value>The pooled streams.</value>
    public int PooledStreams { get; init; }
    /// <summary>
    /// Gets or sets the active callbacks.
    /// </summary>
    /// <value>The active callbacks.</value>
    public int ActiveCallbacks { get; init; }
    /// <summary>
    /// Gets or sets the priorities supported.
    /// </summary>
    /// <value>The priorities supported.</value>
    public bool PrioritiesSupported { get; init; }
    /// <summary>
    /// Gets or sets the graph capture active.
    /// </summary>
    /// <value>The graph capture active.</value>
    public bool GraphCaptureActive { get; init; }
}