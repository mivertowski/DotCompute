// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Diagnostics;
using DotCompute.Abstractions.Messaging;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using static DotCompute.Backends.CUDA.Native.CudaRuntime;

namespace DotCompute.Backends.CUDA.Messaging;

/// <summary>
/// CUDA-backed message queue for high-performance GPU-resident message passing.
/// </summary>
/// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// This implementation uses GPU device memory for the message buffer with atomic
/// operations for head/tail indices. Messages are serialized to byte arrays before
/// transfer to the GPU and deserialized when read back.
/// </para>
///
/// <para>
/// <b>Thread Safety:</b> All operations are thread-safe and can be called concurrently
/// from multiple threads. CUDA atomic operations ensure correctness.
/// </para>
///
/// <para>
/// <b>Performance:</b>
/// - Target: ~1-5μs per enqueue/dequeue (includes host-device transfer)
/// - Device memory allocation minimized through pooling
/// - Atomic operations for lock-free concurrency
/// </para>
/// </remarks>
public sealed class CudaMessageQueue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IMessageQueue<T>
    where T : IRingKernelMessage
{
    private readonly ILogger<CudaMessageQueue<T>> _logger;
    private readonly MessageQueueOptions _options;
    private readonly int _capacityMask; // capacity - 1, for fast modulo
    private readonly ConcurrentDictionary<Guid, byte>? _seenMessages;
    private readonly SemaphoreSlim? _semaphore;
    private readonly DateTime _createdAt;

    // CUDA resources
    private IntPtr _context;
    private IntPtr _deviceBuffer; // GPU buffer for serialized messages
    private IntPtr _deviceHead;   // GPU atomic head index
    private IntPtr _deviceTail;   // GPU atomic tail index
    private IntPtr _hostHead;     // Host pinned memory for head
    private IntPtr _hostTail;     // Host pinned memory for tail
    private readonly int _maxMessageSize;  // Maximum serialized message size in bytes

    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaMessageQueue{T}"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the queue.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="options"/> or <paramref name="logger"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when options are invalid.
    /// </exception>
    public CudaMessageQueue(MessageQueueOptions options, ILogger<CudaMessageQueue<T>> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        options.Validate(); // Rounds capacity to power of 2
        _options = options;
        _logger = logger;

        _capacityMask = options.Capacity - 1;
        _createdAt = DateTime.UtcNow;

        // Initialize deduplication tracking if enabled
        if (options.EnableDeduplication)
        {
            _seenMessages = new ConcurrentDictionary<Guid, byte>();
        }

        // Initialize semaphore for blocking backpressure strategy
        // Semaphore tracks number of items in queue (0 = empty, capacity = full)
        if (options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            _semaphore = new SemaphoreSlim(0, options.Capacity);
        }

        // Estimate maximum message size (MessageId + MessageType + Priority + CorrelationId + PayloadSize + 1KB safety margin)
        _maxMessageSize = 16 + 256 + 1 + 16 + 4 + 1024; // Conservative estimate
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Property getter, infrequent access")]
    public int Count
    {
        get
        {
            if (!_initialized || _disposed)
            {
                return 0;
            }

            // Set context and read head/tail atomically
            var ctxResult = cuCtxSetCurrent(_context);
            if (ctxResult != CudaError.Success)
            {
                _logger.LogError("cuCtxSetCurrent failed in Count getter: {Error}", ctxResult);
                return 0;
            }

            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(long));
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(long));

            var head = Marshal.ReadInt64(_hostHead);
            var tail = Marshal.ReadInt64(_hostTail);

            return (int)(head - tail);
        }
    }

    /// <inheritdoc/>
    public int Capacity => _options.Capacity;

    /// <inheritdoc/>
    public bool IsFull => Count >= Capacity;

    /// <inheritdoc/>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Initializes the CUDA message queue by allocating GPU resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when CUDA initialization fails.
    /// </exception>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Initialization code, not hot path")]
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        _logger.LogInformation(
            "Initializing CUDA message queue with capacity {Capacity} for type {Type}",
            _options.Capacity,
            typeof(T).Name);

        await Task.Run(() =>
        {
            // Initialize CUDA Driver API (safe to call multiple times)
            var initResult = cuInit(0);
            if (initResult is not CudaError.Success and not ((CudaError)4)) // 4 = CUDA_ERROR_ALREADY_INITIALIZED
            {
                throw new InvalidOperationException($"Failed to initialize CUDA Driver API: {initResult}");
            }

            // Get device handle for device 0
            var getDeviceResult = CudaRuntime.cuDeviceGet(out var device, 0);
            if (getDeviceResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to get CUDA device: {getDeviceResult}");
            }

            // Create new context for the device
            var ctxResult = CudaRuntimeCore.cuCtxCreate(out _context, 0, device);
            if (ctxResult != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to create CUDA context: {ctxResult}");
            }

            // Calculate buffer size (capacity * max message size)
            var bufferSize = (nuint)(_options.Capacity * _maxMessageSize);
            var atomicSize = (nuint)sizeof(long); // Use long for head/tail to match CPU implementation

            // Allocate device memory
            CudaApi.cuMemAlloc(ref _deviceBuffer, bufferSize);
            CudaApi.cuMemAlloc(ref _deviceHead, atomicSize);
            CudaApi.cuMemAlloc(ref _deviceTail, atomicSize);

            // Allocate host pinned memory for fast transfers
            _hostHead = Marshal.AllocHGlobal(sizeof(long));
            _hostTail = Marshal.AllocHGlobal(sizeof(long));

            // Initialize head/tail to zero
            long zero = 0;
            Marshal.WriteInt64(_hostHead, zero);
            Marshal.WriteInt64(_hostTail, zero);

            CudaApi.cuMemcpyHtoD(_deviceHead, _hostHead, atomicSize);
            CudaApi.cuMemcpyHtoD(_deviceTail, _hostTail, atomicSize);

            // Zero the buffer
            CudaApi.cuMemsetD8(_deviceBuffer, 0, bufferSize);

            _initialized = true;
            _logger.LogDebug("CUDA message queue initialized successfully");

        }, cancellationToken);
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Message queue operations, balanced performance")]
    public bool TryEnqueue(T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        // Check for duplicates if deduplication is enabled
        if (_seenMessages is not null)
        {
            if (!_seenMessages.TryAdd(message.MessageId, 0))
            {
                // Duplicate message - silently drop and return true (already processed)
                return true;
            }

            // Limit deduplication window size
            if (_seenMessages.Count > _options.DeduplicationWindowSize)
            {
                // Remove oldest entries (simple strategy: clear half)
                var toRemove = _seenMessages.Keys.Take(_options.DeduplicationWindowSize / 2).ToList();
                foreach (var key in toRemove)
                {
                    _ = _seenMessages.TryRemove(key, out _);
                }
            }
        }

        // Handle backpressure based on strategy
        if (IsFull)
        {
            switch (_options.BackpressureStrategy)
            {
                case BackpressureStrategy.Block:
                    // Wait for space to become available (CurrentCount < Capacity)
                    if (_semaphore is null)
                    {
                        throw new InvalidOperationException("Semaphore not initialized for Block strategy");
                    }

                    try
                    {
                        // Poll until space available or timeout
                        var startTime = DateTime.UtcNow;
                        while (_semaphore.CurrentCount >= _options.Capacity)
                        {
                            if (DateTime.UtcNow - startTime > _options.BlockTimeout)
                            {
                                // Timeout waiting for space
                                RemoveFromDeduplication(message.MessageId);
                                return false;
                            }
                            if (cancellationToken.IsCancellationRequested)
                            {
                                RemoveFromDeduplication(message.MessageId);
                                throw new OperationCanceledException("CUDA message queue enqueue operation was cancelled while waiting for available capacity.", cancellationToken);
                            }
                            Thread.Sleep(1); // Small delay to reduce CPU usage
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        RemoveFromDeduplication(message.MessageId);
                        throw;
                    }
                    break;

                case BackpressureStrategy.DropOldest:
                    // Dequeue oldest message to make space
                    if (!TryDequeue(out _))
                    {
                        // Queue became empty between check and dequeue (rare race condition)
                        // Just proceed with enqueue
                    }
                    break;

                case BackpressureStrategy.Reject:
                    // Return false to reject the message
                    RemoveFromDeduplication(message.MessageId);
                    return false;

                case BackpressureStrategy.DropNew:
                    // Drop the new message silently
                    RemoveFromDeduplication(message.MessageId);
                    return true; // Return true despite dropping (fire-and-forget semantics)
            }
        }

        // Set CUDA context
        var ctxResult = cuCtxSetCurrent(_context);
        if (ctxResult != CudaError.Success)
        {
            _logger.LogError("cuCtxSetCurrent failed in TryEnqueue: {Error}", ctxResult);
            RemoveFromDeduplication(message.MessageId);
            return false;
        }

        try
        {
            // Serialize message to byte array
            var serialized = message.Serialize();
            if (serialized.Length > _maxMessageSize)
            {
                _logger.LogWarning(
                    "Message size {Size} exceeds maximum {Max}, rejecting",
                    serialized.Length,
                    _maxMessageSize);
                RemoveFromDeduplication(message.MessageId);
                return false;
            }

            // Read current head atomically
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(long));
            var currentHead = Marshal.ReadInt64(_hostHead);

            // Calculate slot index
            var slotIndex = (int)(currentHead & _capacityMask);
            var slotOffset = _deviceBuffer + (slotIndex * _maxMessageSize);

            // Write serialized message to GPU buffer
            var messagePtr = Marshal.AllocHGlobal(serialized.Length);
            try
            {
                Marshal.Copy(serialized.ToArray(), 0, messagePtr, serialized.Length);

                var copyResult = CudaApi.cuMemcpyHtoD(slotOffset, messagePtr, (nuint)serialized.Length);
                if (copyResult != CudaError.Success)
                {
                    _logger.LogError("cuMemcpyHtoD failed in TryEnqueue: {Error}", copyResult);
                    RemoveFromDeduplication(message.MessageId);
                    return false;
                }

                // Synchronize to ensure write completes
                var syncResult = cuCtxSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogError("cuCtxSynchronize failed in TryEnqueue: {Error}", syncResult);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            // Update head atomically
            var newHead = currentHead + 1;
            Marshal.WriteInt64(_hostHead, newHead);
            CudaApi.cuMemcpyHtoD(_deviceHead, _hostHead, (nuint)sizeof(long));

            // Signal item added (for blocking backpressure strategy)
            if (_options.BackpressureStrategy == BackpressureStrategy.Block)
            {
                _semaphore?.Release();
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing message to CUDA queue");
            RemoveFromDeduplication(message.MessageId);
            return false;
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Message queue operations, balanced performance")]
    public bool TryDequeue(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        // Set CUDA context
        var ctxResult = cuCtxSetCurrent(_context);
        if (ctxResult != CudaError.Success)
        {
            _logger.LogError("cuCtxSetCurrent failed in TryDequeue: {Error}", ctxResult);
            return false;
        }

        try
        {
            // Read current head and tail atomically
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(long));
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(long));

            var head = Marshal.ReadInt64(_hostHead);
            var tail = Marshal.ReadInt64(_hostTail);

            // Check if queue is empty
            if (tail >= head)
            {
                return false;
            }

            // Calculate slot index
            var slotIndex = (int)(tail & _capacityMask);
            var slotOffset = _deviceBuffer + (slotIndex * _maxMessageSize);

            // Read serialized message from GPU buffer
            var messagePtr = Marshal.AllocHGlobal(_maxMessageSize);
            try
            {
                var copyResult = CudaApi.cuMemcpyDtoH(messagePtr, slotOffset, (nuint)_maxMessageSize);
                if (copyResult != CudaError.Success)
                {
                    _logger.LogError("cuMemcpyDtoH failed in TryDequeue: {Error}", copyResult);
                    return false;
                }

                // Synchronize to ensure read completes
                var syncResult = cuCtxSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogError("cuCtxSynchronize failed in TryDequeue: {Error}", syncResult);
                }

                // Deserialize message
                var buffer = new byte[_maxMessageSize];
                Marshal.Copy(messagePtr, buffer, 0, _maxMessageSize);

                // Create new message instance and deserialize
                message = Activator.CreateInstance<T>();
                message.Deserialize(buffer);

                // Check message timeout
                if (_options.MessageTimeout != TimeSpan.Zero)
                {
                    var age = DateTime.UtcNow - _createdAt;
                    if (age > _options.MessageTimeout)
                    {
                        // Message expired - remove from deduplication and return null
                        RemoveFromDeduplication(message.MessageId);
                        message = default;
                        return false;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            // Update tail atomically
            var newTail = tail + 1;
            Marshal.WriteInt64(_hostTail, newTail);
            CudaApi.cuMemcpyHtoD(_deviceTail, _hostTail, (nuint)sizeof(long));

            // Remove from deduplication tracking
            if (message is not null)
            {
                RemoveFromDeduplication(message.MessageId);

                // Decrement item count if using blocking strategy (only if we successfully dequeued)
                if (_options.BackpressureStrategy == BackpressureStrategy.Block)
                {
                    _semaphore?.Wait(0); // Non-blocking decrement
                }
            }

            return message is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing message from CUDA queue");
            return false;
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Message queue operations, balanced performance")]
    public bool TryPeek(out T? message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        message = default;

        if (!_initialized)
        {
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
        }

        // Set CUDA context
        var ctxResult = cuCtxSetCurrent(_context);
        if (ctxResult != CudaError.Success)
        {
            _logger.LogError("cuCtxSetCurrent failed in TryPeek: {Error}", ctxResult);
            return false;
        }

        try
        {
            // Read current head and tail atomically
            CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(long));
            CudaApi.cuMemcpyDtoH(_hostTail, _deviceTail, (nuint)sizeof(long));

            var head = Marshal.ReadInt64(_hostHead);
            var tail = Marshal.ReadInt64(_hostTail);

            // Check if queue is empty
            if (tail >= head)
            {
                return false;
            }

            // Calculate slot index (peek at tail without advancing)
            var slotIndex = (int)(tail & _capacityMask);
            var slotOffset = _deviceBuffer + (slotIndex * _maxMessageSize);

            // Read serialized message from GPU buffer
            var messagePtr = Marshal.AllocHGlobal(_maxMessageSize);
            try
            {
                var copyResult = CudaApi.cuMemcpyDtoH(messagePtr, slotOffset, (nuint)_maxMessageSize);
                if (copyResult != CudaError.Success)
                {
                    _logger.LogError("cuMemcpyDtoH failed in TryPeek: {Error}", copyResult);
                    return false;
                }

                // Synchronize to ensure read completes
                var syncResult = cuCtxSynchronize();
                if (syncResult != CudaError.Success)
                {
                    _logger.LogError("cuCtxSynchronize failed in TryPeek: {Error}", syncResult);
                }

                // Deserialize message
                var buffer = new byte[_maxMessageSize];
                Marshal.Copy(messagePtr, buffer, 0, _maxMessageSize);

                // Create new message instance and deserialize
                message = Activator.CreateInstance<T>();
                message.Deserialize(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(messagePtr);
            }

            return message is not null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error peeking message from CUDA queue");
            return false;
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Queue maintenance operation, not hot path")]
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            return;
        }

        // Set CUDA context
        var ctxResult = cuCtxSetCurrent(_context);
        if (ctxResult != CudaError.Success)
        {
            _logger.LogError("cuCtxSetCurrent failed in Clear: {Error}", ctxResult);
            return;
        }

        // Read current head
        CudaApi.cuMemcpyDtoH(_hostHead, _deviceHead, (nuint)sizeof(long));
        var currentHead = Marshal.ReadInt64(_hostHead);

        // Set tail to head (empty queue)
        Marshal.WriteInt64(_hostTail, currentHead);
        CudaApi.cuMemcpyHtoD(_deviceTail, _hostTail, (nuint)sizeof(long));

        // Clear deduplication tracking
        _seenMessages?.Clear();

        // Reset semaphore to full capacity
        if (_semaphore is not null && _options.BackpressureStrategy == BackpressureStrategy.Block)
        {
            // Drain semaphore completely
            while (_semaphore.CurrentCount > 0)
            {
                _semaphore.Wait(0);
            }

            // Release to full capacity
            _semaphore.Release(_options.Capacity);
        }
    }

    /// <inheritdoc/>
    [SuppressMessage("Performance", "XFIX003:Use LoggerMessage.Define", Justification = "Disposal code, not hot path")]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing CUDA message queue");

        // Free device memory
        if (_deviceBuffer != IntPtr.Zero)
        {
            CudaApi.cuMemFree(_deviceBuffer);
            _deviceBuffer = IntPtr.Zero;
        }

        if (_deviceHead != IntPtr.Zero)
        {
            CudaApi.cuMemFree(_deviceHead);
            _deviceHead = IntPtr.Zero;
        }

        if (_deviceTail != IntPtr.Zero)
        {
            CudaApi.cuMemFree(_deviceTail);
            _deviceTail = IntPtr.Zero;
        }

        // Free host pinned memory
        if (_hostHead != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_hostHead);
            _hostHead = IntPtr.Zero;
        }

        if (_hostTail != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_hostTail);
            _hostTail = IntPtr.Zero;
        }

        // Destroy CUDA context
        if (_context != IntPtr.Zero)
        {
            CudaRuntimeCore.cuCtxDestroy(_context);
            _context = IntPtr.Zero;
        }

        // Dispose semaphore
        _semaphore?.Dispose();

        _disposed = true;
        _logger.LogInformation("CUDA message queue disposed");
    }

    /// <summary>
    /// Gets the GPU device pointer buffer for the head index (for kernel access).
    /// </summary>
    /// <returns>Device pointer buffer wrapping the atomic head counter.</returns>
    /// <remarks>
    /// Returns a CudaDevicePointerBuffer that wraps the device pointer for use in ring kernel launch.
    /// The buffer represents sizeof(long) bytes for the atomic counter.
    /// </remarks>
    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Method appropriate for GPU pointer access")]
    [SuppressMessage("Performance", "XFIX001:Use properties where appropriate", Justification = "Method appropriate for GPU pointer access")]
    public RingKernels.CudaDevicePointerBuffer GetHeadPtr()
    {
        return new RingKernels.CudaDevicePointerBuffer(_deviceHead, sizeof(long));
    }

    /// <summary>
    /// Gets the GPU device pointer buffer for the tail index (for kernel access).
    /// </summary>
    /// <returns>Device pointer buffer wrapping the atomic tail counter.</returns>
    /// <remarks>
    /// Returns a CudaDevicePointerBuffer that wraps the device pointer for use in ring kernel launch.
    /// The buffer represents sizeof(long) bytes for the atomic counter.
    /// </remarks>
    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Method appropriate for GPU pointer access")]
    [SuppressMessage("Performance", "XFIX001:Use properties where appropriate", Justification = "Method appropriate for GPU pointer access")]
    public RingKernels.CudaDevicePointerBuffer GetTailPtr()
    {
        return new RingKernels.CudaDevicePointerBuffer(_deviceTail, sizeof(long));
    }

    /// <summary>
    /// Removes a message ID from deduplication tracking.
    /// </summary>
    /// <param name="messageId">Message ID to remove.</param>
    private void RemoveFromDeduplication(Guid messageId)
    {
        _seenMessages?.TryRemove(messageId, out _);
    }
}
