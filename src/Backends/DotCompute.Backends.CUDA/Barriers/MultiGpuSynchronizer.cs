// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Barriers;

/// <summary>
/// Cross-GPU synchronization primitive for system-wide barriers.
/// </summary>
/// <remarks>
/// <para>
/// MultiGpuSynchronizer coordinates barrier synchronization across multiple CUDA devices
/// using a combination of CUDA events and host-side CPU synchronization. This enables
/// system-wide barriers spanning all GPUs in the system.
/// </para>
/// <para>
/// <strong>Architecture:</strong>
/// <list type="bullet">
/// <item><description>Phase 1: Device-local barriers complete on each GPU</description></item>
/// <item><description>Phase 2: Each GPU records a CUDA event when barrier arrives</description></item>
/// <item><description>Phase 3: Host CPU waits for all GPU events (cross-GPU sync)</description></item>
/// <item><description>Phase 4: Host signals all GPUs via mapped memory to resume</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance:</strong> System barriers have ~1-10ms latency due to PCIe roundtrip.
/// Use sparingly in tight loops. Overhead scales linearly with GPU count.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe for concurrent barrier operations
/// across multiple CPU threads coordinating different GPU operations.
/// </para>
/// </remarks>
public sealed partial class MultiGpuSynchronizer : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 8100,
        Level = LogLevel.Debug,
        Message = "MultiGpuSynchronizer initialized with {DeviceCount} devices")]
    private static partial void LogSynchronizerInitialized(ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 8101,
        Level = LogLevel.Debug,
        Message = "Registering device {DeviceId} with context {Context}")]
    private static partial void LogDeviceRegistered(ILogger logger, int deviceId, IntPtr context);

    [LoggerMessage(
        EventId = 8102,
        Level = LogLevel.Debug,
        Message = "Starting barrier sync for barrier {BarrierId}, capacity {Capacity}")]
    private static partial void LogBarrierSyncStarted(ILogger logger, int barrierId, int capacity);

    [LoggerMessage(
        EventId = 8103,
        Level = LogLevel.Debug,
        Message = "Device {DeviceId} arrived at barrier {BarrierId} ({Current}/{Total})")]
    private static partial void LogDeviceArrived(ILogger logger, int deviceId, int barrierId, int current, int total);

    [LoggerMessage(
        EventId = 8104,
        Level = LogLevel.Information,
        Message = "All devices synchronized for barrier {BarrierId}. Duration: {DurationMs}ms")]
    private static partial void LogBarrierCompleted(ILogger logger, int barrierId, double durationMs);

    [LoggerMessage(
        EventId = 8105,
        Level = LogLevel.Warning,
        Message = "Barrier {BarrierId} timeout after {TimeoutMs}ms. {ArrivedCount}/{TotalCount} devices arrived")]
    private static partial void LogBarrierTimeout(ILogger logger, int barrierId, double timeoutMs, int arrivedCount, int totalCount);

    [LoggerMessage(
        EventId = 8106,
        Level = LogLevel.Error,
        Message = "Failed to wait for device {DeviceId} event: {Error}")]
    private static partial void LogEventWaitFailed(ILogger logger, int deviceId, string error);

    [LoggerMessage(
        EventId = 8107,
        Level = LogLevel.Debug,
        Message = "MultiGpuSynchronizer: CUDA detected but allocation failed ({Error}), using fallback memory")]
    private static partial void LogCudaAllocationFailed(ILogger logger, string error);

    [LoggerMessage(
        EventId = 8108,
        Level = LogLevel.Debug,
        Message = "MultiGpuSynchronizer initialized without CUDA (using fallback memory for testing)")]
    private static partial void LogFallbackMemoryInitialized(ILogger logger);

    #endregion

    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, DeviceState> _deviceStates;
    private readonly ConcurrentDictionary<int, BarrierState> _barrierStates;
    private readonly IntPtr _hostSignalMemory; // Pinned host memory for cross-GPU signaling
    private readonly bool _usedCudaMemory; // Flag to track if CUDA memory or fallback was used
    private readonly object _syncLock = new();
    private bool _disposed;

    /// <summary>
    /// Represents the synchronization state for a single device.
    /// </summary>
    private sealed class DeviceState : IDisposable
    {
        public int DeviceId { get; init; }
        public CudaContext Context { get; init; } = null!;
        public IntPtr BarrierEvent { get; set; }
        public IntPtr SignalMemory { get; set; } // Device pointer to host signal memory
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (BarrierEvent != IntPtr.Zero)
            {
                CudaRuntime.cudaEventDestroy(BarrierEvent);
                BarrierEvent = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Represents the synchronization state for a barrier operation.
    /// </summary>
    private sealed class BarrierState
    {
        public int BarrierId { get; init; }
        public int Capacity { get; init; }
        public int ArrivedCount { get; set; }
        public HashSet<int> ArrivedDevices { get; } = new();
        public TaskCompletionSource<bool> CompletionSource { get; } = new();
        public CancellationTokenSource TimeoutCancellation { get; } = new();
    }

    /// <summary>
    /// Initializes a new multi-GPU synchronizer.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic messages.</param>
    public MultiGpuSynchronizer(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _deviceStates = new ConcurrentDictionary<int, DeviceState>();
        _barrierStates = new ConcurrentDictionary<int, BarrierState>();

        // Check if CUDA is available before allocating
        var deviceCheckResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);

        if (deviceCheckResult == CudaError.Success && deviceCount > 0)
        {
            // Try to allocate pinned host memory for cross-GPU signaling (4 bytes = 1 int)
            var tempPtr = IntPtr.Zero;
            var result = CudaRuntime.cudaHostAlloc(ref tempPtr, 4,
                (uint)(CudaHostAllocFlags.Portable | CudaHostAllocFlags.Mapped));

            if (result == CudaError.Success)
            {
                // CUDA allocation succeeded
                _hostSignalMemory = tempPtr;
                _usedCudaMemory = true;

                // Initialize signal to 0
                Marshal.WriteInt32(_hostSignalMemory, 0);

                LogSynchronizerInitialized(_logger, deviceCount);
            }
            else
            {
                // CUDA detected but allocation failed (likely unit test environment) - use fallback
                _hostSignalMemory = Marshal.AllocHGlobal(4);
                _usedCudaMemory = false;
                Marshal.WriteInt32(_hostSignalMemory, 0);
                LogCudaAllocationFailed(_logger, CudaRuntime.GetErrorString(result));
            }
        }
        else
        {
            // No CUDA devices or CUDA not initialized - allocate fallback memory for unit tests
            _hostSignalMemory = Marshal.AllocHGlobal(4);
            _usedCudaMemory = false;
            Marshal.WriteInt32(_hostSignalMemory, 0);
            LogFallbackMemoryInitialized(_logger);
        }
    }

    /// <summary>
    /// Registers a CUDA device for participation in system-wide barriers.
    /// </summary>
    /// <param name="deviceId">Device identifier.</param>
    /// <param name="context">CUDA context for the device.</param>
    public void RegisterDevice(int deviceId, CudaContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(context);

        lock (_syncLock)
        {
            if (_deviceStates.ContainsKey(deviceId))
            {
                throw new InvalidOperationException($"Device {deviceId} is already registered.");
            }

            // Set device as current
            var result = CudaRuntime.cudaSetDevice(deviceId);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to set device {deviceId}: {CudaRuntime.GetErrorString(result)}");
            }

            // Create event for this device
            var eventPtr = IntPtr.Zero;
            result = CudaRuntime.cudaEventCreate(ref eventPtr);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to create event for device {deviceId}: {CudaRuntime.GetErrorString(result)}");
            }

            // Get device pointer to host signal memory
            var deviceSignalPtr = IntPtr.Zero;
            result = CudaRuntime.cudaHostGetDevicePointer(ref deviceSignalPtr, _hostSignalMemory, 0);
            if (result != CudaError.Success)
            {
                CudaRuntime.cudaEventDestroy(eventPtr);
                throw new InvalidOperationException(
                    $"Failed to get device pointer for device {deviceId}: {CudaRuntime.GetErrorString(result)}");
            }

            var state = new DeviceState
            {
                DeviceId = deviceId,
                Context = context,
                BarrierEvent = eventPtr,
                SignalMemory = deviceSignalPtr
            };

            if (!_deviceStates.TryAdd(deviceId, state))
            {
                state.Dispose();
                throw new InvalidOperationException($"Failed to register device {deviceId}.");
            }

            LogDeviceRegistered(_logger, deviceId, context.Handle);
        }
    }

    /// <summary>
    /// Unregisters a device from barrier participation.
    /// </summary>
    /// <param name="deviceId">Device identifier to unregister.</param>
    public void UnregisterDevice(int deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_syncLock)
        {
            if (_deviceStates.TryRemove(deviceId, out var state))
            {
                state.Dispose();
            }
        }
    }

    /// <summary>
    /// Signals that a device has arrived at the barrier and waits for all devices.
    /// </summary>
    /// <param name="barrierId">Unique barrier identifier.</param>
    /// <param name="deviceId">Device that is arriving at the barrier.</param>
    /// <param name="capacity">Total number of devices expected.</param>
    /// <param name="timeout">Maximum time to wait for all devices.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if barrier completed successfully, false on timeout.</returns>
    public async Task<bool> ArriveAndWaitAsync(
        int barrierId,
        int deviceId,
        int capacity,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_deviceStates.TryGetValue(deviceId, out var deviceState))
        {
            throw new InvalidOperationException($"Device {deviceId} is not registered.");
        }

        var startTime = DateTimeOffset.UtcNow;

        // Get or create barrier state
        var barrierState = _barrierStates.GetOrAdd(barrierId, _ =>
        {
            LogBarrierSyncStarted(_logger, barrierId, capacity);
            return new BarrierState
            {
                BarrierId = barrierId,
                Capacity = capacity
            };
        });

        // Record arrival
        int arrivedCount;
        lock (barrierState)
        {
            if (barrierState.ArrivedDevices.Contains(deviceId))
            {
                throw new InvalidOperationException(
                    $"Device {deviceId} has already arrived at barrier {barrierId}.");
            }

            barrierState.ArrivedDevices.Add(deviceId);
            barrierState.ArrivedCount++;
            arrivedCount = barrierState.ArrivedCount;

            LogDeviceArrived(_logger, deviceId, barrierId, arrivedCount, capacity);

            // If this is the last device, complete the barrier
            if (arrivedCount >= capacity)
            {
                var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
                LogBarrierCompleted(_logger, barrierId, duration);

                // Signal all devices via host memory
                Marshal.WriteInt32(_hostSignalMemory, 1);

                barrierState.CompletionSource.TrySetResult(true);
                return true;
            }
        }

        // Wait for barrier completion with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, barrierState.TimeoutCancellation.Token);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await barrierState.CompletionSource.Task.WaitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            LogBarrierTimeout(_logger, barrierId, duration, arrivedCount, capacity);
            return false;
        }
    }

    /// <summary>
    /// Waits for all device events to complete (Phase 2 of system barrier).
    /// </summary>
    /// <param name="barrierId">Barrier identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all events completed, false on error.</returns>
    public async Task<bool> WaitForAllDeviceEventsAsync(
        int barrierId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tasks = new List<Task>();

        foreach (var deviceState in _deviceStates.Values)
        {
            var task = Task.Run(() =>
            {
                // Set device context
                var result = CudaRuntime.cudaSetDevice(deviceState.DeviceId);
                if (result != CudaError.Success)
                {
                    LogEventWaitFailed(_logger, deviceState.DeviceId,
                        CudaRuntime.GetErrorString(result));
                    return false;
                }

                // Wait for device event
                result = CudaRuntime.cudaEventSynchronize(deviceState.BarrierEvent);
                if (result != CudaError.Success)
                {
                    LogEventWaitFailed(_logger, deviceState.DeviceId,
                        CudaRuntime.GetErrorString(result));
                    return false;
                }

                return true;
            }, cancellationToken);

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        return tasks.All(t => ((Task<bool>)t).Result);
    }

    /// <summary>
    /// Resets a barrier state after completion.
    /// </summary>
    /// <param name="barrierId">Barrier identifier to reset.</param>
    public void ResetBarrier(int barrierId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_barrierStates.TryRemove(barrierId, out var barrierState))
        {
            barrierState.TimeoutCancellation.Dispose();
        }

        // Reset host signal memory
        Marshal.WriteInt32(_hostSignalMemory, 0);
    }

    /// <summary>
    /// Gets the number of registered devices.
    /// </summary>
    public int RegisteredDeviceCount => _deviceStates.Count;

    /// <summary>
    /// Gets the device IDs of all registered devices.
    /// </summary>
    public IReadOnlyCollection<int> RegisteredDevices => (IReadOnlyCollection<int>)_deviceStates.Keys;

    /// <summary>
    /// Disposes the synchronizer and all associated resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            // Dispose all device states
            foreach (var state in _deviceStates.Values)
            {
                state.Dispose();
            }
            _deviceStates.Clear();

            // Cancel and dispose all barrier states
            foreach (var barrierState in _barrierStates.Values)
            {
                barrierState.TimeoutCancellation.Cancel();
                barrierState.TimeoutCancellation.Dispose();
            }
            _barrierStates.Clear();

            // Free host memory
            if (_hostSignalMemory != IntPtr.Zero)
            {
                if (_usedCudaMemory)
                {
                    CudaRuntime.cudaFreeHost(_hostSignalMemory);
                }
                else
                {
                    Marshal.FreeHGlobal(_hostSignalMemory);
                }
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizer to ensure resources are released.
    /// </summary>
    ~MultiGpuSynchronizer()
    {
        Dispose();
    }
}
