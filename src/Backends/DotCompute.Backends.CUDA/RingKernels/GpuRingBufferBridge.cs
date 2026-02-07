// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.RingKernels;
// Disable threading analyzers - DMA transfer tasks are intentionally synchronous in disposal
#pragma warning disable VSTHRD103 // Call async methods when in an async method
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
#pragma warning disable VSTHRD200 // Use "Async" suffix for async methods

/// <summary>
/// Direction of data transfer for a GPU ring buffer bridge.
/// </summary>
/// <remarks>
/// <para>
/// This determines which DMA transfer loop runs:
/// <list type="bullet">
/// <item><description><b>HostToDevice</b>: Input bridges - host writes messages, GPU kernel reads</description></item>
/// <item><description><b>DeviceToHost</b>: Output bridges - GPU kernel writes messages, host reads</description></item>
/// <item><description><b>Bidirectional</b>: Both directions (for debugging, not recommended for production)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Critical</b>: Using wrong direction causes race conditions where the bridge
/// consumes messages before the kernel can process them.
/// </para>
/// </remarks>
public enum GpuBridgeDirection
{
    /// <summary>
    /// Host → GPU: Host writes to GPU buffer (input bridges).
    /// The bridge advances the GPU tail pointer after writing messages.
    /// The GPU kernel is the consumer and advances the head pointer.
    /// </summary>
    HostToDevice,

    /// <summary>
    /// GPU → Host: GPU writes to GPU buffer (output bridges).
    /// The GPU kernel advances the GPU tail pointer after writing messages.
    /// The bridge is the consumer and advances the head pointer.
    /// </summary>
    DeviceToHost,

    /// <summary>
    /// Both directions (for debugging/testing only).
    /// </summary>
    Bidirectional
}

/// <summary>
/// Non-generic interface for GPU ring buffer bridges, enabling polymorphic access
/// when the message type is only known at runtime.
/// </summary>
public interface IGpuRingBufferBridge : IDisposable
{
    /// <summary>
    /// Gets the associated GPU ring buffer.
    /// </summary>
    public IGpuRingBuffer GpuRingBuffer { get; }

    /// <summary>
    /// Gets whether explicit DMA transfer is enabled.
    /// </summary>
    public bool IsDmaTransferEnabled { get; }

    /// <summary>
    /// Gets the number of messages transferred from host to GPU.
    /// </summary>
    public long HostToGpuTransferCount { get; }

    /// <summary>
    /// Gets the number of messages transferred from GPU to host.
    /// </summary>
    public long GpuToHostTransferCount { get; }

    /// <summary>
    /// Starts the bridge's background DMA transfer tasks (if enabled).
    /// </summary>
    public void Start();

    /// <summary>
    /// Stops the bridge's background DMA transfer tasks.
    /// </summary>
    public void StopTransfers();
}

/// <summary>
/// Bidirectional bridge between host <see cref="IMessageQueue{T}"/> and GPU ring buffer.
/// </summary>
/// <typeparam name="T">Message type implementing <see cref="IRingKernelMessage"/>.</typeparam>
/// <remarks>
/// <para>
/// This bridge manages the data flow between:
/// <list type="bullet">
/// <item><description>Host-side: <see cref="IMessageQueue{T}"/> (managed memory)</description></item>
/// <item><description>GPU-side: <see cref="GpuRingBuffer{T}"/> (device memory)</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Two Transfer Modes:</b>
/// <list type="number">
/// <item><description>
/// <b>Unified Memory Mode</b> (non-WSL2): GPU kernel directly accesses host queue via unified memory.
/// Bridge is passive and only provides pointer translation.
/// </description></item>
/// <item><description>
/// <b>Explicit DMA Mode</b> (WSL2): Background tasks periodically copy messages between
/// host queue and GPU buffer using cudaMemcpy. Required for WSL2 due to unified memory limitations.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Message Flow:</b>
/// <code>
/// Host → HostQueue.Enqueue() → [Bridge: Host→GPU DMA] → GPU Buffer → GPU Kernel
/// GPU Kernel → GPU Buffer → [Bridge: GPU→Host DMA] → HostQueue.Dequeue() → Host
/// </code>
/// </para>
/// </remarks>
public sealed class GpuRingBufferBridge<T> : IGpuRingBufferBridge
    where T : IRingKernelMessage
{
    private readonly IMessageQueue<T> _hostQueue;
    private readonly GpuRingBuffer<T> _gpuBuffer;
    private readonly ILogger? _logger;
    private readonly bool _enableDmaTransfer;
    private readonly GpuBridgeDirection _direction;

    private Task? _hostToGpuTask;
    private Task? _gpuToHostTask;
    private CancellationTokenSource? _cts;

    private bool _disposed;

    // Diagnostic counters
    private long _hostToGpuTransferCount;
    private long _gpuToHostTransferCount;
    private DateTime _lastHeartbeatTime;

    // High-performance polling configuration
    // SpinWait iterations before yielding (tune based on CPU)
    private const int SpinIterationsBeforeYield = 100;
    // Number of yields before falling back to Task.Yield for async friendliness
    private const int YieldsBeforeSleep = 10;

    /// <summary>
    /// Gets the host-side message queue.
    /// </summary>
    public IMessageQueue<T> HostQueue => _hostQueue;

    /// <summary>
    /// Gets the GPU-side ring buffer (typed).
    /// </summary>
    public GpuRingBuffer<T> GpuBuffer => _gpuBuffer;

    /// <summary>
    /// Gets the GPU-side ring buffer (interface access for non-generic scenarios).
    /// </summary>
    public IGpuRingBuffer GpuRingBuffer => _gpuBuffer;

    /// <summary>
    /// Gets whether explicit DMA transfer is enabled.
    /// </summary>
    public bool IsDmaTransferEnabled => _enableDmaTransfer;

    /// <summary>
    /// Gets the count of messages transferred from host to GPU.
    /// </summary>
    public long HostToGpuTransferCount => Interlocked.Read(ref _hostToGpuTransferCount);

    /// <summary>
    /// Gets the count of messages transferred from GPU to host.
    /// </summary>
    public long GpuToHostTransferCount => Interlocked.Read(ref _gpuToHostTransferCount);

    /// <summary>
    /// Initializes a new instance of the <see cref="GpuRingBufferBridge{T}"/> class.
    /// </summary>
    /// <param name="hostQueue">Host-side message queue.</param>
    /// <param name="gpuBuffer">GPU-side ring buffer.</param>
    /// <param name="enableDmaTransfer">
    /// True to enable background DMA transfer tasks (WSL2 mode), false for unified memory mode.
    /// </param>
    /// <param name="direction">
    /// Direction of data flow. Use <see cref="GpuBridgeDirection.HostToDevice"/> for input bridges
    /// and <see cref="GpuBridgeDirection.DeviceToHost"/> for output bridges.
    /// </param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public GpuRingBufferBridge(
        IMessageQueue<T> hostQueue,
        GpuRingBuffer<T> gpuBuffer,
        bool enableDmaTransfer,
        GpuBridgeDirection direction = GpuBridgeDirection.Bidirectional,
        ILogger? logger = null)
    {
        _hostQueue = hostQueue ?? throw new ArgumentNullException(nameof(hostQueue));
        _gpuBuffer = gpuBuffer ?? throw new ArgumentNullException(nameof(gpuBuffer));
        _logger = logger;
        _enableDmaTransfer = enableDmaTransfer;
        _direction = direction;

        // Validate capacity matches
        if (_hostQueue.Capacity != _gpuBuffer.Capacity)
        {
            throw new ArgumentException(
                $"Host queue capacity ({_hostQueue.Capacity}) must match GPU buffer capacity ({_gpuBuffer.Capacity})");
        }

        _logger?.LogInformation(
            "[Bridge:{MessageType}] Created GPU ring buffer bridge - Capacity={Capacity}, MessageSize={MessageSize}, " +
            "DmaEnabled={DmaEnabled}, UnifiedMem={UnifiedMem}, Direction={Direction}, " +
            "HeadPtr=0x{HeadPtr:X}, TailPtr=0x{TailPtr:X}, BufferPtr=0x{BufferPtr:X}",
            typeof(T).Name, _gpuBuffer.Capacity, _gpuBuffer.MessageSize,
            _enableDmaTransfer, _gpuBuffer.IsUnifiedMemory, _direction,
            _gpuBuffer.DeviceHeadPtr.ToInt64(), _gpuBuffer.DeviceTailPtr.ToInt64(), _gpuBuffer.DeviceBufferPtr.ToInt64());
    }

    /// <summary>
    /// Starts the bridge's background DMA transfer tasks (if enabled).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only starts the transfer loop(s) appropriate for the configured <see cref="_direction"/>:
    /// <list type="bullet">
    /// <item><description><b>HostToDevice</b>: Only Host→GPU loop (input bridges)</description></item>
    /// <item><description><b>DeviceToHost</b>: Only GPU→Host loop (output bridges)</description></item>
    /// <item><description><b>Bidirectional</b>: Both loops (debugging only)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_enableDmaTransfer)
        {
            _logger?.LogDebug("DMA transfer disabled (unified memory mode) - bridge is passive");
            return;
        }

        if (_cts != null)
        {
            _logger?.LogWarning("Bridge already started");
            return;
        }

        _cts = new CancellationTokenSource();
        _lastHeartbeatTime = DateTime.UtcNow;

        // Start transfer loops based on direction
        // CRITICAL: Running wrong direction causes bridge to consume messages before kernel can process them!
        if (_direction is GpuBridgeDirection.HostToDevice or GpuBridgeDirection.Bidirectional)
        {
            _hostToGpuTask = Task.Run(() => HostToGpuTransferLoop(_cts.Token), _cts.Token);
        }

        if (_direction is GpuBridgeDirection.DeviceToHost or GpuBridgeDirection.Bidirectional)
        {
            _gpuToHostTask = Task.Run(() => GpuToHostTransferLoop(_cts.Token), _cts.Token);
        }

        _logger?.LogInformation(
            "[Bridge:{MessageType}] Started DMA transfer tasks (Direction={Direction}) - Host→GPU Task={HostToGpuStatus}, GPU→Host Task={GpuToHostStatus}",
            typeof(T).Name, _direction, _hostToGpuTask?.Status.ToString() ?? "NotStarted", _gpuToHostTask?.Status.ToString() ?? "NotStarted");
    }

    /// <summary>
    /// Stops the bridge's background DMA transfer tasks.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts == null || _disposed)
        {
            return;
        }

        _logger?.LogDebug("Stopping GPU ring buffer bridge...");

        // Signal cancellation
        _cts.Cancel();

        // Wait for tasks to complete (with timeout)
        var tasks = new List<Task>();
        if (_hostToGpuTask != null)
        {
            tasks.Add(_hostToGpuTask);
        }

        if (_gpuToHostTask != null)
        {
            tasks.Add(_gpuToHostTask);
        }

        if (tasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning("Bridge tasks did not complete within timeout");
            }
        }

        _cts.Dispose();
        _cts = null;
        _hostToGpuTask = null;
        _gpuToHostTask = null;

        _logger?.LogInformation("GPU ring buffer bridge stopped");
    }

    /// <summary>
    /// Stops the bridge's background DMA transfer tasks (synchronous wrapper).
    /// </summary>
    public void StopTransfers()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Stop background tasks synchronously
        if (_cts != null)
        {
            StopAsync().GetAwaiter().GetResult();
        }

        _gpuBuffer.Dispose();
        _hostQueue.Dispose();

        _disposed = true;
    }

    /// <summary>
    /// Background loop that transfers messages from host queue to GPU buffer.
    /// </summary>
    /// <remarks>
    /// Uses a high-performance polling strategy for sub-millisecond latency:
    /// 1. SpinWait (microseconds) - tightest loop, catches messages immediately
    /// 2. Thread.Yield() - lets other threads run but stays responsive
    /// 3. Short sleep (optional) - reduces CPU when truly idle
    ///
    /// This replaces Task.Delay(1) which has ~15ms minimum resolution on Windows.
    /// </remarks>
    private async Task HostToGpuTransferLoop(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[Bridge:{MessageType}] Host→GPU transfer loop started (high-perf mode)", typeof(T).Name);
        _logger?.LogDebug("[Bridge:{MessageType}] Host→GPU transfer loop STARTED - DmaEnabled={DmaEnabled}, Direction={Direction}, HighPerfMode=true",
            typeof(T).Name, _enableDmaTransfer, _direction);

        try
        {
            var loopIterations = 0L;
            var consecutiveEmptyPolls = 0;
            var spinWait = new SpinWait();

            while (!cancellationToken.IsCancellationRequested)
            {
                loopIterations++;

                // Periodic heartbeat logging (every 100000 iterations for high-perf mode)
                if (loopIterations % 100000 == 0)
                {
                    var elapsed = DateTime.UtcNow - _lastHeartbeatTime;
                    _logger?.LogDebug(
                        "[Bridge:{MessageType}] Host→GPU heartbeat - Transfers={TransferCount}, Iterations={Iterations}, Elapsed={Elapsed:F1}s",
                        typeof(T).Name, Interlocked.Read(ref _hostToGpuTransferCount), loopIterations, elapsed.TotalSeconds);
                }

                // Try to dequeue from host
                if (_hostQueue.TryDequeue(out var message) && message != null)
                {
                    consecutiveEmptyPolls = 0; // Reset idle counter
                    spinWait.Reset(); // Reset spin state for next idle period

                    // Get GPU tail position
                    var tail = _gpuBuffer.ReadTail();
                    var nextTail = (tail + 1) & (uint)(_gpuBuffer.Capacity - 1);
                    var head = _gpuBuffer.ReadHead();

                    // Check if GPU buffer has space
                    if (nextTail != head)
                    {
                        // Write message to GPU at tail position
                        _gpuBuffer.WriteMessage(message, (int)tail, cancellationToken);

                        // Advance tail
                        _gpuBuffer.WriteTail(nextTail);

                        var count = Interlocked.Increment(ref _hostToGpuTransferCount);
                        _logger?.LogDebug(
                            "[Bridge:{MessageType}] Host→GPU transfer #{Count}: tail={Tail}→{NextTail}, msgId={MessageId}",
                            typeof(T).Name, count, tail, nextTail, message.MessageId);
                    }
                    else
                    {
                        // GPU buffer full - re-enqueue to host (backpressure)
                        _ = _hostQueue.TryEnqueue(message, cancellationToken);

                        _logger?.LogDebug(
                            "[Bridge:{MessageType}] Host→GPU backpressure: GPU buffer full (head={Head}, tail={Tail}), re-enqueued msgId={MessageId}",
                            typeof(T).Name, head, tail, message.MessageId);

                        // Back off slightly when buffer is full
                        spinWait.SpinOnce();
                    }
                }
                else
                {
                    // No messages in host queue - use adaptive polling
                    consecutiveEmptyPolls++;

                    if (consecutiveEmptyPolls < SpinIterationsBeforeYield)
                    {
                        // Phase 1: Tight spin-wait (sub-microsecond response)
                        Thread.SpinWait(10);
                    }
                    else if (consecutiveEmptyPolls < SpinIterationsBeforeYield + YieldsBeforeSleep)
                    {
                        // Phase 2: Yield to other threads but stay responsive
                        Thread.Yield();
                    }
                    else
                    {
                        // Phase 3: Longer idle - use Task.Yield for async friendliness
                        // This allows other async work to proceed without blocking
                        await Task.Yield();

                        // Optional: Reset to avoid starvation of other threads
                        if (consecutiveEmptyPolls > 1000)
                        {
                            consecutiveEmptyPolls = SpinIterationsBeforeYield; // Stay in yield phase
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Bridge:{MessageType}] Host→GPU transfer loop failed", typeof(T).Name);
        }

        _logger?.LogInformation(
            "[Bridge:{MessageType}] Host→GPU transfer loop stopped - TotalTransfers={TransferCount}",
            typeof(T).Name, Interlocked.Read(ref _hostToGpuTransferCount));
    }

    /// <summary>
    /// Background loop that transfers messages from GPU buffer to host queue.
    /// </summary>
    /// <remarks>
    /// Uses a high-performance polling strategy for sub-millisecond latency.
    /// See <see cref="HostToGpuTransferLoop"/> for details on the adaptive polling approach.
    /// </remarks>
    private async Task GpuToHostTransferLoop(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[Bridge:{MessageType}] GPU→Host transfer loop started (high-perf mode)", typeof(T).Name);

        try
        {
            var loopIterations = 0L;
            var consecutiveEmptyPolls = 0;
            var spinWait = new SpinWait();

            while (!cancellationToken.IsCancellationRequested)
            {
                loopIterations++;

                // Periodic heartbeat logging (every 100000 iterations for high-perf mode)
                if (loopIterations % 100000 == 0)
                {
                    var head = _gpuBuffer.ReadHead();
                    var tail = _gpuBuffer.ReadTail();
                    var elapsed = DateTime.UtcNow - _lastHeartbeatTime;
                    _logger?.LogDebug(
                        "[Bridge:{MessageType}] GPU→Host heartbeat - Transfers={TransferCount}, Head={Head}, Tail={Tail}, Iterations={Iterations}, Elapsed={Elapsed:F1}s",
                        typeof(T).Name, Interlocked.Read(ref _gpuToHostTransferCount), head, tail, loopIterations, elapsed.TotalSeconds);
                }

                // Get GPU head/tail positions
                var gpuHead = _gpuBuffer.ReadHead();
                var gpuTail = _gpuBuffer.ReadTail();

                // Check if GPU buffer has messages
                if (gpuHead != gpuTail)
                {
                    consecutiveEmptyPolls = 0; // Reset idle counter
                    spinWait.Reset();

                    // Read message from GPU at head position
                    var message = _gpuBuffer.ReadMessage((int)gpuHead, cancellationToken);

                    // Try to enqueue to host
                    if (_hostQueue.TryEnqueue(message, cancellationToken))
                    {
                        // Advance head
                        var nextHead = (gpuHead + 1) & (uint)(_gpuBuffer.Capacity - 1);
                        _gpuBuffer.WriteHead(nextHead);

                        var count = Interlocked.Increment(ref _gpuToHostTransferCount);
                        _logger?.LogDebug(
                            "[Bridge:{MessageType}] GPU→Host transfer #{Count}: head={Head}→{NextHead}, msgId={MessageId}",
                            typeof(T).Name, count, gpuHead, nextHead, message.MessageId);
                    }
                    else
                    {
                        // Host queue full - back off slightly
                        _logger?.LogDebug(
                            "[Bridge:{MessageType}] GPU→Host backpressure: Host queue full, retrying msgId={MessageId}",
                            typeof(T).Name, message.MessageId);
                        spinWait.SpinOnce();
                    }
                }
                else
                {
                    // No messages in GPU buffer - use adaptive polling
                    consecutiveEmptyPolls++;

                    if (consecutiveEmptyPolls < SpinIterationsBeforeYield)
                    {
                        // Phase 1: Tight spin-wait (sub-microsecond response)
                        Thread.SpinWait(10);
                    }
                    else if (consecutiveEmptyPolls < SpinIterationsBeforeYield + YieldsBeforeSleep)
                    {
                        // Phase 2: Yield to other threads but stay responsive
                        Thread.Yield();
                    }
                    else
                    {
                        // Phase 3: Longer idle - use Task.Yield for async friendliness
                        await Task.Yield();

                        if (consecutiveEmptyPolls > 1000)
                        {
                            consecutiveEmptyPolls = SpinIterationsBeforeYield;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[Bridge:{MessageType}] GPU→Host transfer loop failed", typeof(T).Name);
        }

        _logger?.LogInformation(
            "[Bridge:{MessageType}] GPU→Host transfer loop stopped - TotalTransfers={TransferCount}",
            typeof(T).Name, Interlocked.Read(ref _gpuToHostTransferCount));
    }
}
