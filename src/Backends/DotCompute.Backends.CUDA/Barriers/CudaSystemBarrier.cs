// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Barriers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Barriers;

/// <summary>
/// System-wide barrier implementation for multi-GPU synchronization.
/// </summary>
/// <remarks>
/// <para>
/// CudaSystemBarrier provides synchronization across multiple CUDA devices and the host CPU.
/// This enables system-wide barriers spanning all GPUs in the system, coordinated through
/// the host.
/// </para>
/// <para>
/// <strong>Architecture:</strong>
/// System barriers operate in three distinct phases:
/// <list type="number">
/// <item><description><strong>Phase 1 - Device-Local:</strong> Each GPU executes device-local barrier via __threadfence_system()</description></item>
/// <item><description><strong>Phase 2 - Cross-GPU Sync:</strong> Host CPU waits for all GPU completion events</description></item>
/// <item><description><strong>Phase 3 - Resume:</strong> Host signals all GPUs to continue via mapped memory</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Characteristics:</strong>
/// <list type="bullet">
/// <item><description>Latency: ~1-10ms depending on GPU count and PCIe topology</description></item>
/// <item><description>Overhead scales linearly with number of GPUs</description></item>
/// <item><description>Use sparingly - typically once per iteration in multi-GPU algorithms</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe for multi-threaded CPU access.
/// GPU threads must follow barrier semantics (all threads in all devices must call Sync()).
/// </para>
/// </remarks>
public sealed class CudaSystemBarrier : IBarrierHandle
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Debug,
        Message = "CudaSystemBarrier created: ID={BarrierId}, Devices={DeviceCount}, Capacity={Capacity}")]
    private static partial void LogBarrierCreated(ILogger logger, int barrierId, int deviceCount, int capacity);

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Debug,
        Message = "Device {DeviceId} starting barrier sync for barrier {BarrierId}")]
    private static partial void LogDeviceSyncStarted(ILogger logger, int deviceId, int barrierId);

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Information,
        Message = "System barrier {BarrierId} completed successfully. Duration: {DurationMs}ms")]
    private static partial void LogBarrierCompleted(ILogger logger, int barrierId, double durationMs);

    [LoggerMessage(
        EventId = 8203,
        Level = LogLevel.Warning,
        Message = "System barrier {BarrierId} timed out after {TimeoutMs}ms")]
    private static partial void LogBarrierTimeout(ILogger logger, int barrierId, double timeoutMs);

    [LoggerMessage(
        EventId = 8204,
        Level = LogLevel.Error,
        Message = "System barrier {BarrierId} failed: {Error}")]
    private static partial void LogBarrierFailed(ILogger logger, int barrierId, string error);

    [LoggerMessage(
        EventId = 8205,
        Level = LogLevel.Debug,
        Message = "Resetting system barrier {BarrierId}")]
    private static partial void LogBarrierReset(ILogger logger, int barrierId);

    #endregion

    private readonly ILogger _logger;
    private readonly CudaBarrierProvider? _provider;
    private readonly MultiGpuSynchronizer _synchronizer;
    private readonly List<CudaContext> _contexts;
    private readonly List<int> _deviceIds;
    private readonly TimeSpan _defaultTimeout;
    private readonly object _syncLock = new();
    private int _threadsWaiting;
    private bool _disposed;

    /// <summary>
    /// Initializes a new system-wide barrier.
    /// </summary>
    /// <param name="provider">The barrier provider for tracking (optional).</param>
    /// <param name="synchronizer">Multi-GPU synchronizer for cross-device coordination.</param>
    /// <param name="contexts">CUDA contexts for all participating devices.</param>
    /// <param name="deviceIds">Device IDs for all participating devices.</param>
    /// <param name="barrierId">Unique barrier identifier.</param>
    /// <param name="capacity">Maximum threads that will synchronize.</param>
    /// <param name="name">Optional barrier name for debugging.</param>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    /// <param name="timeout">Default timeout for barrier operations (default: 30 seconds).</param>
    internal CudaSystemBarrier(
        CudaBarrierProvider? provider,
        MultiGpuSynchronizer synchronizer,
        IEnumerable<CudaContext> contexts,
        IEnumerable<int> deviceIds,
        int barrierId,
        int capacity,
        string? name = null,
        ILogger? logger = null,
        TimeSpan? timeout = null)
    {
        _provider = provider;
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        _contexts = contexts?.ToList() ?? throw new ArgumentNullException(nameof(contexts));
        _deviceIds = deviceIds?.ToList() ?? throw new ArgumentNullException(nameof(deviceIds));
        _logger = logger ?? NullLogger.Instance;
        _defaultTimeout = timeout ?? TimeSpan.FromSeconds(30);

        if (_contexts.Count == 0)
        {
            throw new ArgumentException("At least one device context is required.", nameof(contexts));
        }

        if (_contexts.Count != _deviceIds.Count)
        {
            throw new ArgumentException(
                "Number of contexts must match number of device IDs.",
                nameof(deviceIds));
        }

        BarrierId = barrierId;
        Capacity = capacity;
        Name = name;

        // Register all devices with the synchronizer
        for (int i = 0; i < _contexts.Count; i++)
        {
            _synchronizer.RegisterDevice(_deviceIds[i], _contexts[i]);
        }

        LogBarrierCreated(_logger, barrierId, _contexts.Count, capacity);
    }

    /// <inheritdoc />
    public int BarrierId { get; }

    /// <inheritdoc />
    public BarrierScope Scope => BarrierScope.System;

    /// <inheritdoc />
    public int Capacity { get; }

    /// <summary>
    /// Gets the optional barrier name.
    /// </summary>
    public string? Name { get; }

    /// <inheritdoc />
    public int ThreadsWaiting
    {
        get
        {
            lock (_syncLock)
            {
                return _threadsWaiting;
            }
        }
    }

    /// <inheritdoc />
    public bool IsActive => ThreadsWaiting > 0 && ThreadsWaiting < Capacity;

    /// <summary>
    /// Gets the number of devices participating in this barrier.
    /// </summary>
    public int DeviceCount => _contexts.Count;

    /// <summary>
    /// Gets the device IDs participating in this barrier.
    /// </summary>
    public IReadOnlyList<int> ParticipatingDevices => _deviceIds.AsReadOnly();

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <strong>IMPORTANT:</strong> For system barriers, this method coordinates synchronization
    /// across multiple GPUs via the host CPU. The actual GPU-side synchronization happens in
    /// the kernel code.
    /// </para>
    /// <para>
    /// <strong>Three-Phase Operation:</strong>
    /// <list type="number">
    /// <item><description>Device-local barriers complete on each GPU</description></item>
    /// <item><description>Host waits for all GPU completion events</description></item>
    /// <item><description>Host signals all GPUs to resume execution</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public void Sync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // This is the CPU-side tracking, actual GPU sync happens via SyncAsync
        lock (_syncLock)
        {
            _threadsWaiting++;

            if (_threadsWaiting >= Capacity)
            {
                _threadsWaiting = 0;
            }
        }
    }

    /// <summary>
    /// Asynchronously synchronizes all devices at the barrier.
    /// </summary>
    /// <param name="deviceId">The device ID calling this sync operation.</param>
    /// <param name="timeout">Optional timeout override (default: 30 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if barrier completed, false on timeout.</returns>
    /// <remarks>
    /// <para>
    /// This method implements the three-phase system barrier protocol:
    /// <list type="number">
    /// <item><description>Record device arrival at barrier</description></item>
    /// <item><description>Wait for all devices to arrive (host-side coordination)</description></item>
    /// <item><description>Signal completion when all devices are synchronized</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public async Task<bool> SyncAsync(
        int deviceId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_deviceIds.Contains(deviceId))
        {
            throw new ArgumentException(
                $"Device {deviceId} is not participating in this barrier.", nameof(deviceId));
        }

        var effectiveTimeout = timeout ?? _defaultTimeout;
        var startTime = DateTimeOffset.UtcNow;

        LogDeviceSyncStarted(_logger, deviceId, BarrierId);

        try
        {
            // Phase 1 & 2: Device arrives and waits for all others
            var success = await _synchronizer.ArriveAndWaitAsync(
                BarrierId,
                deviceId,
                DeviceCount,
                effectiveTimeout,
                cancellationToken);

            if (!success)
            {
                var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                LogBarrierTimeout(_logger, BarrierId, duration);
                return false;
            }

            // Phase 3: Wait for all device events to complete
            success = await _synchronizer.WaitForAllDeviceEventsAsync(BarrierId, cancellationToken);

            if (!success)
            {
                LogBarrierFailed(_logger, BarrierId, "One or more device events failed");
                return false;
            }

            var completionDuration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            LogBarrierCompleted(_logger, BarrierId, completionDuration);

            return true;
        }
        catch (Exception ex)
        {
            LogBarrierFailed(_logger, BarrierId, ex.Message);
            throw;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncLock)
        {
            if (_threadsWaiting > 0)
            {
                throw new InvalidOperationException(
                    "Cannot reset an active barrier. Wait for all threads to complete sync.");
            }

            _threadsWaiting = 0;
            _synchronizer.ResetBarrier(BarrierId);

            LogBarrierReset(_logger, BarrierId);
        }
    }

    /// <summary>
    /// Disposes the system barrier and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_syncLock)
        {
            if (_disposed) return;

            // Unregister all devices from synchronizer
            foreach (var deviceId in _deviceIds)
            {
                _synchronizer.UnregisterDevice(deviceId);
            }

            // Notify provider to remove from tracking
            _provider?.RemoveBarrier(BarrierId, Name);

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure resources are released.
    /// </summary>
    ~CudaSystemBarrier()
    {
        Dispose();
    }

    /// <summary>
    /// Returns a string representation of this system barrier.
    /// </summary>
    public override string ToString()
    {
        var nameStr = string.IsNullOrEmpty(Name) ? "" : $" '{Name}'";
        return $"CudaSystemBarrier{nameStr}[ID={BarrierId}, Scope={Scope}, " +
               $"Devices={DeviceCount}, Capacity={Capacity}, Waiting={ThreadsWaiting}]";
    }
}
