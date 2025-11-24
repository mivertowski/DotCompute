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
public sealed class GpuRingBufferBridge<T> : IDisposable
    where T : IRingKernelMessage
{
    private readonly IMessageQueue<T> _hostQueue;
    private readonly GpuRingBuffer<T> _gpuBuffer;
    private readonly ILogger? _logger;
    private readonly bool _enableDmaTransfer;

    private Task? _hostToGpuTask;
    private Task? _gpuToHostTask;
    private CancellationTokenSource? _cts;

    private bool _disposed;

    // Diagnostic counters
    private long _hostToGpuTransferCount;
    private long _gpuToHostTransferCount;
    private DateTime _lastHeartbeatTime;

    /// <summary>
    /// Gets the host-side message queue.
    /// </summary>
    public IMessageQueue<T> HostQueue => _hostQueue;

    /// <summary>
    /// Gets the GPU-side ring buffer.
    /// </summary>
    public GpuRingBuffer<T> GpuBuffer => _gpuBuffer;

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
    /// <param name="logger">Optional logger for diagnostics.</param>
    public GpuRingBufferBridge(
        IMessageQueue<T> hostQueue,
        GpuRingBuffer<T> gpuBuffer,
        bool enableDmaTransfer,
        ILogger? logger = null)
    {
        _hostQueue = hostQueue ?? throw new ArgumentNullException(nameof(hostQueue));
        _gpuBuffer = gpuBuffer ?? throw new ArgumentNullException(nameof(gpuBuffer));
        _logger = logger;
        _enableDmaTransfer = enableDmaTransfer;

        // Validate capacity matches
        if (_hostQueue.Capacity != _gpuBuffer.Capacity)
        {
            throw new ArgumentException(
                $"Host queue capacity ({_hostQueue.Capacity}) must match GPU buffer capacity ({_gpuBuffer.Capacity})");
        }

        _logger?.LogInformation(
            "[Bridge:{MessageType}] Created GPU ring buffer bridge - Capacity={Capacity}, MessageSize={MessageSize}, " +
            "DmaEnabled={DmaEnabled}, UnifiedMem={UnifiedMem}, " +
            "HeadPtr=0x{HeadPtr:X}, TailPtr=0x{TailPtr:X}, BufferPtr=0x{BufferPtr:X}",
            typeof(T).Name, _gpuBuffer.Capacity, _gpuBuffer.MessageSize,
            _enableDmaTransfer, _gpuBuffer.IsUnifiedMemory,
            _gpuBuffer.DeviceHeadPtr.ToInt64(), _gpuBuffer.DeviceTailPtr.ToInt64(), _gpuBuffer.DeviceBufferPtr.ToInt64());
    }

    /// <summary>
    /// Starts the bridge's background DMA transfer tasks (if enabled).
    /// </summary>
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

        // Start Host→GPU transfer task
        _hostToGpuTask = Task.Run(() => HostToGpuTransferLoop(_cts.Token), _cts.Token);

        // Start GPU→Host transfer task
        _gpuToHostTask = Task.Run(() => GpuToHostTransferLoop(_cts.Token), _cts.Token);

        _logger?.LogInformation(
            "[Bridge:{MessageType}] Started DMA transfer tasks - Host→GPU Task={HostToGpuStatus}, GPU→Host Task={GpuToHostStatus}",
            typeof(T).Name, _hostToGpuTask.Status, _gpuToHostTask.Status);
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
    private async Task HostToGpuTransferLoop(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[Bridge:{MessageType}] Host→GPU transfer loop started", typeof(T).Name);

        try
        {
            var loopIterations = 0L;

            while (!cancellationToken.IsCancellationRequested)
            {
                loopIterations++;

                // Periodic heartbeat logging (every ~10 seconds at 1ms delay = 10000 iterations)
                if (loopIterations % 10000 == 0)
                {
                    var elapsed = DateTime.UtcNow - _lastHeartbeatTime;
                    _logger?.LogDebug(
                        "[Bridge:{MessageType}] Host→GPU heartbeat - Transfers={TransferCount}, Iterations={Iterations}, Elapsed={Elapsed:F1}s",
                        typeof(T).Name, Interlocked.Read(ref _hostToGpuTransferCount), loopIterations, elapsed.TotalSeconds);
                }

                // Try to dequeue from host
                if (_hostQueue.TryDequeue(out var message) && message != null)
                {
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

                        // Back off to avoid tight loop
                        await Task.Delay(1, cancellationToken);
                    }
                }
                else
                {
                    // No messages in host queue - yield CPU
                    await Task.Delay(1, cancellationToken);
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
    private async Task GpuToHostTransferLoop(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[Bridge:{MessageType}] GPU→Host transfer loop started", typeof(T).Name);

        try
        {
            var loopIterations = 0L;

            while (!cancellationToken.IsCancellationRequested)
            {
                loopIterations++;

                // Periodic heartbeat logging (every ~10 seconds at 1ms delay = 10000 iterations)
                if (loopIterations % 10000 == 0)
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
                        // Host queue full - back off
                        _logger?.LogDebug(
                            "[Bridge:{MessageType}] GPU→Host backpressure: Host queue full, retrying msgId={MessageId}",
                            typeof(T).Name, message.MessageId);
                        await Task.Delay(1, cancellationToken);
                    }
                }
                else
                {
                    // No messages in GPU buffer - yield CPU
                    await Task.Delay(1, cancellationToken);
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
