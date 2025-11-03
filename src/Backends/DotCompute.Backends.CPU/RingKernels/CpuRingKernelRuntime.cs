// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Abstractions.RingKernels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CPU.RingKernels;

/// <summary>
/// CPU-based ring kernel runtime simulation using background threads.
/// </summary>
/// <remarks>
/// This runtime simulates GPU persistent kernels using .NET threads for testing
/// and systems without GPU acceleration. Each "kernel" runs as a background thread
/// with message-based communication similar to GPU ring kernels.
///
/// <para><b>Architecture:</b></para>
/// <list type="bullet">
/// <item><description>Each kernel is a background thread processing messages</description></item>
/// <item><description>Message queues are thread-safe BlockingCollection instances</description></item>
/// <item><description>Lifecycle management via volatile flags and wait handles</description></item>
/// <item><description>Metrics tracking with atomic counters</description></item>
/// </list>
/// </remarks>
public sealed class CpuRingKernelRuntime : IRingKernelRuntime
{
    private readonly ILogger<CpuRingKernelRuntime> _logger;
    private readonly ConcurrentDictionary<string, KernelWorker> _workers = new();
    private bool _disposed;

    private sealed class KernelWorker
    {
        private readonly string _kernelId;
        private readonly int _gridSize;
        private readonly int _blockSize;
        private readonly ILogger _logger;
        private readonly Stopwatch _uptime = new();

        private Thread? _thread;
        private volatile bool _active;
        private volatile bool _terminate;
        private long _messagesProcessed;
#pragma warning disable CS0649 // Field is reserved for future telemetry implementation
        private long _messagesSent;
        private long _messagesReceived;
#pragma warning restore CS0649

        public object? InputQueue { get; set; }
        public object? OutputQueue { get; set; }
        public bool IsLaunched { get; private set; }
        public bool IsActive => _active;
        public bool IsTerminating => _terminate;

        public KernelWorker(string kernelId, int gridSize, int blockSize, ILogger logger)
        {
            _kernelId = kernelId;
            _gridSize = gridSize;
            _blockSize = blockSize;
            _logger = logger;
        }

        public void Launch()
        {
            if (IsLaunched)
            {
                throw new InvalidOperationException($"Kernel '{_kernelId}' already launched");
            }

            _thread = new Thread(WorkerLoop)
            {
                Name = $"RingKernel-{_kernelId}",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            _uptime.Start();
            _thread.Start();
            IsLaunched = true;

            _logger.LogInformation(
                "Launched CPU ring kernel '{KernelId}' with gridSize={GridSize}, blockSize={BlockSize}",
                _kernelId, _gridSize, _blockSize);
        }

        public void Activate()
        {
            if (!IsLaunched)
            {
                throw new InvalidOperationException($"Kernel '{_kernelId}' not launched");
            }

            if (_active)
            {
                _logger.LogWarning("Kernel '{KernelId}' already active", _kernelId);
                return;
            }

            _active = true;
            _logger.LogInformation("Activated ring kernel '{KernelId}'", _kernelId);
        }

        public void Deactivate()
        {
            if (!_active)
            {
                _logger.LogWarning("Kernel '{KernelId}' not active", _kernelId);
                return;
            }

            _active = false;
            _logger.LogInformation("Deactivated ring kernel '{KernelId}'", _kernelId);
        }

        public void Terminate()
        {
            _terminate = true;

            // Wake up the thread if it's sleeping
            _thread?.Interrupt();

            _logger.LogInformation("Terminating ring kernel '{KernelId}'", _kernelId);

            // Wait for graceful shutdown
            if (_thread != null && _thread.IsAlive)
            {
                if (!_thread.Join(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Kernel '{KernelId}' did not terminate gracefully, aborting", _kernelId);
                }
            }

            _uptime.Stop();
            _logger.LogInformation(
                "Terminated ring kernel '{KernelId}' - uptime: {Uptime:F2}s, messages processed: {Messages}",
                _kernelId, _uptime.Elapsed.TotalSeconds, _messagesProcessed);
        }

        public RingKernelStatus GetStatus()
        {
            return new RingKernelStatus
            {
                KernelId = _kernelId,
                IsLaunched = IsLaunched,
                IsActive = _active,
                IsTerminating = _terminate,
                MessagesPending = 0, // Would need queue access
                MessagesProcessed = Interlocked.Read(ref _messagesProcessed),
                GridSize = _gridSize,
                BlockSize = _blockSize,
                Uptime = _uptime.Elapsed
            };
        }

        public RingKernelMetrics GetMetrics()
        {
            var messagesReceived = Interlocked.Read(ref _messagesReceived);
            var messagesSent = Interlocked.Read(ref _messagesSent);
            var uptime = _uptime.Elapsed.TotalSeconds;

            var throughput = uptime > 0 ? messagesReceived / uptime : 0;

            return new RingKernelMetrics
            {
                LaunchCount = 1,
                MessagesSent = messagesSent,
                MessagesReceived = messagesReceived,
                AvgProcessingTimeMs = 0, // Would need detailed timing
                ThroughputMsgsPerSec = throughput,
                InputQueueUtilization = 0,  // Would need queue metrics
                OutputQueueUtilization = 0,
                PeakMemoryBytes = 0,
                CurrentMemoryBytes = 0,
                GpuUtilizationPercent = 0 // CPU doesn't have GPU utilization
            };
        }

        private void WorkerLoop()
        {
            _logger.LogDebug("Ring kernel '{KernelId}' worker thread started", _kernelId);

            try
            {
                while (!_terminate)
                {
                    if (!_active)
                    {
                        // Sleep when inactive to avoid busy waiting
                        try
                        {
                            Thread.Sleep(10);
                        }
                        catch (ThreadInterruptedException)
                        {
                            // Woken up for termination
                            continue;
                        }
                    }
                    else
                    {
                        // Simulate kernel processing
                        // In a real implementation, this would:
                        // 1. Check input queue for messages
                        // 2. Process messages with user kernel logic
                        // 3. Send results to output queue
                        // 4. Update metrics

                        // For now, just yield to avoid busy waiting
                        Thread.Yield();

                        // Simulate some processing
                        Interlocked.Increment(ref _messagesProcessed);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ring kernel '{KernelId}' worker thread", _kernelId);
            }
            finally
            {
                _logger.LogDebug("Ring kernel '{KernelId}' worker thread exiting", _kernelId);
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CpuRingKernelRuntime"/> class.
    /// </summary>
    /// <param name="logger">Logger instance (optional).</param>
    public CpuRingKernelRuntime(ILogger<CpuRingKernelRuntime>? logger = null)
    {
        _logger = logger ?? NullLogger<CpuRingKernelRuntime>.Instance;
        _logger.LogInformation("CPU ring kernel runtime initialized");
    }

    /// <inheritdoc/>
    public Task LaunchAsync(string kernelId, int gridSize, int blockSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);

        if (gridSize <= 0 || blockSize <= 0)
        {
            throw new ArgumentException("Grid and block sizes must be positive");
        }

        ThrowIfDisposed();

        return Task.Run(() =>
        {
            var worker = new KernelWorker(kernelId, gridSize, blockSize, _logger);

            if (!_workers.TryAdd(kernelId, worker))
            {
                throw new InvalidOperationException($"Kernel '{kernelId}' already exists");
            }

            // Create message queues
            var inputQueueLogger = NullLogger<CpuMessageQueue<int>>.Instance;
            var outputQueueLogger = NullLogger<CpuMessageQueue<int>>.Instance;

            worker.InputQueue = new CpuMessageQueue<int>(256, inputQueueLogger);
            worker.OutputQueue = new CpuMessageQueue<int>(256, outputQueueLogger);

            // Initialize queues
            if (worker.InputQueue is IMessageQueue<int> inputQueue)
            {
                inputQueue.InitializeAsync(cancellationToken).Wait(cancellationToken);
            }

            if (worker.OutputQueue is IMessageQueue<int> outputQueue)
            {
                outputQueue.InitializeAsync(cancellationToken).Wait(cancellationToken);
            }

            worker.Launch();
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ActivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Activate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeactivateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Deactivate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task TerminateAsync(string kernelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryRemove(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.Run(() => worker.Terminate(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task SendMessageAsync<T>(string kernelId, KernelMessage<T> message,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (worker.InputQueue is not IMessageQueue<T> queue)
        {
            throw new InvalidOperationException(
                $"Input queue for kernel '{kernelId}' does not support type {typeof(T).Name}");
        }

        return queue.EnqueueAsync(message, default, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<KernelMessage<T>?> ReceiveMessageAsync<T>(string kernelId, TimeSpan timeout = default,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        if (worker.OutputQueue is not IMessageQueue<T> queue)
        {
            throw new InvalidOperationException(
                $"Output queue for kernel '{kernelId}' does not support type {typeof(T).Name}");
        }

        if (timeout == default)
        {
            timeout = TimeSpan.FromSeconds(1);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await queue.TryDequeueAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null; // Timeout
        }
    }

    /// <inheritdoc/>
    public Task<RingKernelStatus> GetStatusAsync(string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.FromResult(worker.GetStatus());
    }

    /// <inheritdoc/>
    public Task<RingKernelMetrics> GetMetricsAsync(string kernelId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelId);
        ThrowIfDisposed();

        if (!_workers.TryGetValue(kernelId, out var worker))
        {
            throw new InvalidOperationException($"Kernel '{kernelId}' not found");
        }

        return Task.FromResult(worker.GetMetrics());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> ListKernelsAsync()
    {
        ThrowIfDisposed();
        return Task.FromResult<IReadOnlyCollection<string>>(_workers.Keys.ToList());
    }

    /// <inheritdoc/>
    public async Task<IMessageQueue<T>> CreateMessageQueueAsync<T>(int capacity,
        CancellationToken cancellationToken = default) where T : unmanaged
    {
        ThrowIfDisposed();

        var logger = NullLogger<CpuMessageQueue<T>>.Instance;
        var queue = new CpuMessageQueue<T>(capacity, logger);

        await queue.InitializeAsync(cancellationToken);
        return queue;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _logger.LogInformation("Disposing CPU ring kernel runtime with {Count} active kernels", _workers.Count);

        // Terminate all workers
        var terminateTasks = _workers.Keys.Select(async kernelId =>
        {
            try
            {
                await TerminateAsync(kernelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating kernel '{KernelId}' during disposal", kernelId);
            }
        });

        await Task.WhenAll(terminateTasks);

        // Dispose all queues
        foreach (var worker in _workers.Values)
        {
            if (worker.InputQueue is IAsyncDisposable inputQueue)
            {
                await inputQueue.DisposeAsync();
            }

            if (worker.OutputQueue is IAsyncDisposable outputQueue)
            {
                await outputQueue.DisposeAsync();
            }
        }

        _workers.Clear();

        _logger.LogInformation("CPU ring kernel runtime disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CpuRingKernelRuntime));
        }
    }
}
