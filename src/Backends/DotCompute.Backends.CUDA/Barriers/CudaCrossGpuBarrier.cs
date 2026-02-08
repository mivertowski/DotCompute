// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable XFIX003 // Use LoggerMessage.Define - will be refactored with proper implementation
#pragma warning disable CA1822 // Mark members as static - stub methods will access instance data when implemented

using System.Diagnostics;
using System.Runtime.CompilerServices;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Temporal;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Barriers;

/// <summary>
/// CUDA implementation of cross-GPU barrier synchronization.
/// Supports P2P memory, CUDA events, and CPU fallback modes.
/// </summary>
/// <remarks>
/// <para><b>Architecture:</b></para>
/// <list type="bullet">
/// <item>Uses atomic operations in pinned host memory for state tracking</item>
/// <item>Leverages P2P memory access when available for GPU-GPU signaling</item>
/// <item>Falls back to CUDA events or CPU polling when P2P unavailable</item>
/// <item>Integrates with HLC for causal barrier ordering</item>
/// </list>
///
/// <para><b>Thread Safety:</b></para>
/// <para>All operations are thread-safe using atomic operations and locks where necessary.</para>
/// </remarks>
public sealed class CudaCrossGpuBarrier : ICrossGpuBarrier
{
    private readonly ILogger<CudaCrossGpuBarrier> _logger;
    private readonly string _barrierId;
    private readonly int _participantCount;
    private readonly CrossGpuBarrierMode _mode;
    private readonly int[] _gpuIds;
    private readonly IHybridLogicalClock _hlc;

    // Barrier state in pinned host memory for atomic access
    private int _generation;
    private int _arrivedCount;
    private readonly HlcTimestamp?[] _arrivalTimestamps;
    private readonly Stopwatch[] _arrivalStopwatches;
    private readonly object _stateLock = new();
    private bool _disposed;

    // P2P memory pointers (if P2P mode)
    private readonly IntPtr[]? _p2pBarrierPointers;

    // P2P host memory pointer (for cleanup)
    private IntPtr _p2pHostPointer;

    // CUDA event handles (if Event mode)
    private readonly IntPtr[]? _cudaEvents;

    /// <summary>
    /// Initializes a new instance of the <see cref="CudaCrossGpuBarrier"/> class.
    /// </summary>
    /// <param name="barrierId">Unique identifier for this barrier.</param>
    /// <param name="gpuIds">Array of participating GPU device IDs.</param>
    /// <param name="mode">Synchronization mode to use.</param>
    /// <param name="hlc">Hybrid Logical Clock for timestamping.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown if required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown if participant count is invalid.</exception>
    public CudaCrossGpuBarrier(
        string barrierId,
        int[] gpuIds,
        CrossGpuBarrierMode mode,
        IHybridLogicalClock hlc,
        ILogger<CudaCrossGpuBarrier>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barrierId);
        ArgumentNullException.ThrowIfNull(gpuIds);
        ArgumentNullException.ThrowIfNull(hlc);

        if (gpuIds.Length < 2)
        {
            throw new ArgumentException(
                "Cross-GPU barrier requires at least 2 participants",
                nameof(gpuIds));
        }

        if (gpuIds.Length > 256)
        {
            throw new ArgumentException(
                "Cross-GPU barrier supports maximum 256 participants",
                nameof(gpuIds));
        }

        _logger = logger ?? NullLogger<CudaCrossGpuBarrier>.Instance;
        _barrierId = barrierId;
        _gpuIds = gpuIds.ToArray(); // Defensive copy
        _participantCount = gpuIds.Length;
        _hlc = hlc;
        _generation = 0;
        _arrivedCount = 0;
        _arrivalTimestamps = new HlcTimestamp?[_participantCount];
        _arrivalStopwatches = new Stopwatch[_participantCount];

        // Initialize stopwatches
        for (var i = 0; i < _participantCount; i++)
        {
            _arrivalStopwatches[i] = new Stopwatch();
        }

        // Determine actual mode to use
        _mode = mode == CrossGpuBarrierMode.Auto
            ? DetermineOptimalMode(gpuIds)
            : mode;

        _logger.LogInformation(
            "Creating cross-GPU barrier '{BarrierId}' with {ParticipantCount} GPUs using {Mode} mode",
            _barrierId, _participantCount, _mode);

        // Initialize mode-specific resources
        try
        {
            switch (_mode)
            {
                case CrossGpuBarrierMode.P2PMemory:
                    _p2pBarrierPointers = InitializeP2PMemory(gpuIds);
                    break;

                case CrossGpuBarrierMode.CudaEvent:
                    _cudaEvents = InitializeCudaEvents(gpuIds);
                    break;

                case CrossGpuBarrierMode.CpuFallback:
                    // No special initialization needed for CPU mode
                    break;

                default:
                    throw new NotSupportedException($"Barrier mode {_mode} not supported");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to initialize barrier '{BarrierId}' in {Mode} mode, falling back to CPU",
                _barrierId, _mode);

            // Fall back to CPU mode on initialization failure
            _mode = CrossGpuBarrierMode.CpuFallback;
            _p2pBarrierPointers = null;
            _cudaEvents = null;
        }

        _logger.LogInformation(
            "Cross-GPU barrier '{BarrierId}' initialized successfully with {Mode} mode",
            _barrierId, _mode);
    }

    /// <inheritdoc/>
    public string BarrierId => _barrierId;

    /// <inheritdoc/>
    public int ParticipantCount => _participantCount;

    /// <inheritdoc/>
    public CrossGpuBarrierMode Mode => _mode;

    /// <inheritdoc/>
    public async Task<CrossGpuBarrierResult> ArriveAndWaitAsync(
        int gpuId,
        HlcTimestamp arrivalTimestamp,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var phase = await ArriveAsync(gpuId, arrivalTimestamp).ConfigureAwait(false);
        return await WaitAsync(phase, timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<CrossGpuBarrierPhase> ArriveAsync(int gpuId, HlcTimestamp arrivalTimestamp)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var gpuIndex = GetGpuIndex(gpuId);
        int currentGeneration;
        int arrivedCount;

        lock (_stateLock)
        {
            currentGeneration = _generation;

            // Record arrival timestamp
            _arrivalTimestamps[gpuIndex] = arrivalTimestamp;
            _arrivalStopwatches[gpuIndex].Restart();

            // Increment arrival counter
            arrivedCount = ++_arrivedCount;

            _logger.LogDebug(
                "GPU {GpuId} arrived at barrier '{BarrierId}' (generation {Generation}, {ArrivedCount}/{ParticipantCount})",
                gpuId, _barrierId, currentGeneration, arrivedCount, _participantCount);

            // Signal arrival in mode-specific way
            switch (_mode)
            {
                case CrossGpuBarrierMode.P2PMemory:
                    SignalArrivalP2P(gpuIndex);
                    break;

                case CrossGpuBarrierMode.CudaEvent:
                    SignalArrivalEvent(gpuIndex);
                    break;

                case CrossGpuBarrierMode.CpuFallback:
                    // State already updated in shared memory
                    break;
            }
        }

        return Task.FromResult(new CrossGpuBarrierPhase
        {
            Generation = currentGeneration,
            GpuId = gpuId,
            PhaseTimestamp = arrivalTimestamp
        });
    }

    /// <inheritdoc/>
    public async Task<CrossGpuBarrierResult> WaitAsync(
        CrossGpuBarrierPhase phase,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var gpuIndex = GetGpuIndex(phase.GpuId);
        var startTime = _arrivalStopwatches[gpuIndex].Elapsed;
        var deadline = DateTime.UtcNow + timeout;

        _logger.LogDebug(
            "GPU {GpuId} waiting at barrier '{BarrierId}' (generation {Generation}, timeout {TimeoutMs}ms)",
            phase.GpuId, _barrierId, phase.Generation, timeout.TotalMilliseconds);

        // Wait for all participants based on mode
        var completed = _mode switch
        {
            CrossGpuBarrierMode.P2PMemory => await WaitP2PAsync(phase, deadline, cancellationToken).ConfigureAwait(false),
            CrossGpuBarrierMode.CudaEvent => await WaitEventAsync(phase, deadline, cancellationToken).ConfigureAwait(false),
            CrossGpuBarrierMode.CpuFallback => await WaitCpuAsync(phase, deadline, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Wait mode {_mode} not supported")
        };

        if (!completed)
        {
            int arrivedCount;
            lock (_stateLock)
            {
                arrivedCount = _arrivedCount;
            }

            _logger.LogWarning(
                "Barrier '{BarrierId}' timed out on GPU {GpuId} (generation {Generation}, {ArrivedCount}/{ParticipantCount})",
                _barrierId, phase.GpuId, phase.Generation, arrivedCount, _participantCount);

            throw new BarrierTimeoutException(
                phase.GpuId,
                phase.Generation,
                arrivedCount,
                _participantCount);
        }

        // Barrier completed - collect results
        HlcTimestamp[] arrivals;
        HlcTimestamp releaseTimestamp;
        TimeSpan waitTime;

        lock (_stateLock)
        {
            arrivals = _arrivalTimestamps
                .Select(t => t ?? default)
                .ToArray();

            // Release timestamp is max of all arrivals (total ordering)
            releaseTimestamp = arrivals
                .OrderByDescending(t => t)
                .First();

            waitTime = _arrivalStopwatches[gpuIndex].Elapsed - startTime;
        }

        _logger.LogDebug(
            "GPU {GpuId} released from barrier '{BarrierId}' (generation {Generation}, wait time {WaitTimeUs}μs)",
            phase.GpuId, _barrierId, phase.Generation, waitTime.TotalMicroseconds);

        return new CrossGpuBarrierResult
        {
            Success = true,
            ReleaseTimestamp = releaseTimestamp,
            ArrivalTimestamps = arrivals,
            WaitTime = waitTime,
            ErrorMessage = null
        };
    }

    /// <inheritdoc/>
    public CrossGpuBarrierStatus GetStatus()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_stateLock)
        {
            return new CrossGpuBarrierStatus
            {
                Generation = _generation,
                ArrivedCount = _arrivedCount,
                TotalCount = _participantCount,
                ArrivalTimestamps = _arrivalTimestamps.ToArray(),
                Mode = _mode
            };
        }
    }

    /// <inheritdoc/>
    public Task ResetAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_stateLock)
        {
            if (_arrivedCount != 0 && _arrivedCount != _participantCount)
            {
                throw new InvalidOperationException(
                    $"Cannot reset barrier '{_barrierId}' while in use ({_arrivedCount}/{_participantCount} arrived)");
            }

            _generation++;
            _arrivedCount = 0;
            Array.Clear(_arrivalTimestamps);

            for (var i = 0; i < _participantCount; i++)
            {
                _arrivalStopwatches[i].Reset();
            }

            _logger.LogDebug(
                "Barrier '{BarrierId}' reset to generation {Generation}",
                _barrierId, _generation);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing cross-GPU barrier '{BarrierId}'", _barrierId);

        // Clean up mode-specific resources
        try
        {
            switch (_mode)
            {
                case CrossGpuBarrierMode.P2PMemory:
                    CleanupP2PMemory();
                    break;

                case CrossGpuBarrierMode.CudaEvent:
                    CleanupCudaEvents();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during barrier cleanup");
        }

        _disposed = true;
        _logger.LogInformation("Cross-GPU barrier '{BarrierId}' disposed", _barrierId);
    }

    // --- Private Helper Methods ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetGpuIndex(int gpuId)
    {
        var index = Array.IndexOf(_gpuIds, gpuId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gpuId),
                gpuId,
                $"GPU {gpuId} is not a participant in barrier '{_barrierId}'");
        }

        return index;
    }

    private static CrossGpuBarrierMode DetermineOptimalMode(int[] gpuIds)
    {
        // Check if P2P access is available between all GPU pairs
        var p2pAvailable = CheckP2PAccess(gpuIds);

        return p2pAvailable
            ? CrossGpuBarrierMode.P2PMemory
            : CrossGpuBarrierMode.CudaEvent;
    }

    private static bool CheckP2PAccess(int[] gpuIds)
    {
        // Check if P2P access is available between all GPU pairs
        for (var i = 0; i < gpuIds.Length; i++)
        {
            for (var j = i + 1; j < gpuIds.Length; j++)
            {
                var canAccess = 0;
                var result = CudaRuntime.cudaDeviceCanAccessPeer(ref canAccess, gpuIds[i], gpuIds[j]);
                if (result != Types.Native.CudaError.Success || canAccess == 0)
                {
                    return false;
                }

                // Check reverse direction as well
                result = CudaRuntime.cudaDeviceCanAccessPeer(ref canAccess, gpuIds[j], gpuIds[i]);
                if (result != Types.Native.CudaError.Success || canAccess == 0)
                {
                    return false;
                }
            }
        }
        return gpuIds.Length >= 2;
    }

    private IntPtr[] InitializeP2PMemory(int[] gpuIds)
    {
        _logger.LogDebug("Initializing P2P memory for barrier '{BarrierId}' with {GpuCount} GPUs", _barrierId, gpuIds.Length);

        var pointers = new IntPtr[gpuIds.Length];
        var hostPtr = IntPtr.Zero;

        try
        {
            // Allocate shared pinned host memory that is accessible from all GPUs
            // Size: 4 bytes per GPU for arrival flags (int per GPU)
            var bufferSize = (ulong)(gpuIds.Length * sizeof(int));

            // Use Mapped | Portable flags for cross-GPU accessibility
            const uint flags = (uint)(Types.Native.CudaHostAllocFlags.Mapped | Types.Native.CudaHostAllocFlags.Portable);
            var allocResult = CudaRuntime.cudaHostAlloc(ref hostPtr, bufferSize, flags);
            CudaRuntime.CheckError(allocResult, "allocating P2P barrier memory");

            // Initialize flags to zero
            unsafe
            {
                var flagPtr = (int*)hostPtr.ToPointer();
                for (var i = 0; i < gpuIds.Length; i++)
                {
                    flagPtr[i] = 0;
                }
            }

            // Enable P2P access between all GPU pairs and get device pointers
            for (var i = 0; i < gpuIds.Length; i++)
            {
                // Set device context
                var setDeviceResult = CudaRuntime.cudaSetDevice(gpuIds[i]);
                CudaRuntime.CheckError(setDeviceResult, $"setting device {gpuIds[i]} for P2P initialization");

                // Enable P2P access to all other GPUs
                for (var j = 0; j < gpuIds.Length; j++)
                {
                    if (i != j)
                    {
                        var enableResult = CudaRuntime.cudaDeviceEnablePeerAccess(gpuIds[j], 0);
                        // Ignore error if already enabled
                        if (enableResult != Types.Native.CudaError.Success && enableResult != Types.Native.CudaError.PeerAccessAlreadyEnabled)
                        {
                            _logger.LogWarning("Failed to enable P2P access from GPU {From} to GPU {To}: {Error}",
                                gpuIds[i], gpuIds[j], enableResult);
                        }
                    }
                }

                // Get device pointer for this GPU
                var devicePtr = IntPtr.Zero;
                var getDevicePtrResult = CudaRuntime.cudaHostGetDevicePointer(ref devicePtr, hostPtr, 0);
                CudaRuntime.CheckError(getDevicePtrResult, $"getting device pointer for GPU {gpuIds[i]}");

                pointers[i] = devicePtr;
                _logger.LogDebug("P2P memory initialized for GPU {GpuId} at device pointer 0x{Pointer:X}", gpuIds[i], devicePtr);
            }

            // Store host pointer for later cleanup
            _p2pHostPointer = hostPtr;

            _logger.LogInformation("P2P barrier memory initialized successfully for {GpuCount} GPUs", gpuIds.Length);
            return pointers;
        }
        catch (Exception ex)
        {
            // Cleanup on failure
            if (hostPtr != IntPtr.Zero)
            {
                CudaRuntime.cudaFreeHost(hostPtr);
            }
            _logger.LogError(ex, "Failed to initialize P2P memory for barrier '{BarrierId}'", _barrierId);
            throw;
        }
    }

    private IntPtr[] InitializeCudaEvents(int[] gpuIds)
    {
        _logger.LogDebug("Initializing CUDA events for barrier '{BarrierId}' with {GpuCount} GPUs", _barrierId, gpuIds.Length);

        var events = new IntPtr[gpuIds.Length];
        var initializedCount = 0;

        try
        {
            // cudaEventDisableTiming = 0x2, cudaEventBlockingSync = 0x1
            // Use DisableTiming for lower overhead synchronization events
            const uint eventFlags = 0x2; // cudaEventDisableTiming

            for (var i = 0; i < gpuIds.Length; i++)
            {
                // Set device context for this GPU
                var setDeviceResult = CudaRuntime.cudaSetDevice(gpuIds[i]);
                CudaRuntime.CheckError(setDeviceResult, $"setting device {gpuIds[i]} for event creation");

                // Create event with DisableTiming flag for lower overhead
                var eventPtr = IntPtr.Zero;
                var createResult = CudaRuntime.cudaEventCreateWithFlags(ref eventPtr, eventFlags);
                CudaRuntime.CheckError(createResult, $"creating CUDA event for GPU {gpuIds[i]}");

                events[i] = eventPtr;
                initializedCount++;
                _logger.LogDebug("CUDA event created for GPU {GpuId} at handle 0x{Handle:X}", gpuIds[i], eventPtr);
            }

            _logger.LogInformation("CUDA events initialized successfully for {GpuCount} GPUs", gpuIds.Length);
            return events;
        }
        catch (Exception ex)
        {
            // Cleanup any events that were created before failure
            for (var i = 0; i < initializedCount; i++)
            {
                if (events[i] != IntPtr.Zero)
                {
                    CudaRuntime.cudaEventDestroy(events[i]);
                }
            }
            _logger.LogError(ex, "Failed to initialize CUDA events for barrier '{BarrierId}'", _barrierId);
            throw;
        }
    }

    private void SignalArrivalP2P(int gpuIndex)
    {
        if (_p2pHostPointer == IntPtr.Zero)
        {
            _logger.LogWarning("P2P host pointer is null for barrier '{BarrierId}'", _barrierId);
            return;
        }

        // Calculate offset for this GPU's arrival flag
        var offset = gpuIndex * sizeof(int);

        // Use Volatile.Write for proper memory ordering (release semantics)
        // This ensures all prior writes are visible to other threads/GPUs
        unsafe
        {
            var flagPtr = (int*)(_p2pHostPointer + offset).ToPointer();
            Volatile.Write(ref *flagPtr, _generation + 1); // Write generation+1 as "arrived" marker
        }

        _logger.LogDebug("GPU index {GpuIndex} signaled arrival via P2P memory for barrier '{BarrierId}'",
            gpuIndex, _barrierId);
    }

    private void SignalArrivalEvent(int gpuIndex)
    {
        if (_cudaEvents == null || gpuIndex >= _cudaEvents.Length)
        {
            _logger.LogWarning("CUDA events not initialized for barrier '{BarrierId}'", _barrierId);
            return;
        }

        var eventHandle = _cudaEvents[gpuIndex];
        if (eventHandle == IntPtr.Zero)
        {
            _logger.LogWarning("CUDA event handle is null for GPU index {GpuIndex} in barrier '{BarrierId}'",
                gpuIndex, _barrierId);
            return;
        }

        // Set device context and record event on default stream
        var gpuId = _gpuIds[gpuIndex];
        var setDeviceResult = CudaRuntime.cudaSetDevice(gpuId);
        if (setDeviceResult != Types.Native.CudaError.Success)
        {
            _logger.LogWarning("Failed to set device {GpuId} for event recording: {Error}",
                gpuId, setDeviceResult);
            return;
        }

        // Record event on default stream (IntPtr.Zero)
        var recordResult = CudaRuntime.cudaEventRecord(eventHandle, IntPtr.Zero);
        if (recordResult != Types.Native.CudaError.Success)
        {
            _logger.LogWarning("Failed to record CUDA event for GPU {GpuId}: {Error}",
                gpuId, recordResult);
            return;
        }

        _logger.LogDebug("GPU {GpuId} signaled arrival via CUDA event for barrier '{BarrierId}'",
            gpuId, _barrierId);
    }

    private async Task<bool> WaitP2PAsync(
        CrossGpuBarrierPhase phase,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        if (_p2pHostPointer == IntPtr.Zero)
        {
            _logger.LogWarning("P2P host pointer is null for wait in barrier '{BarrierId}'", _barrierId);
            return false;
        }

        var expectedMarker = phase.Generation + 1; // Arrival marker for this generation
        var spinWait = new SpinWait();

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check all arrival flags using acquire semantics
            var allArrived = true;
            unsafe
            {
                for (var i = 0; i < _participantCount; i++)
                {
                    var offset = i * sizeof(int);
                    var flagPtr = (int*)(_p2pHostPointer + offset).ToPointer();
                    var arrivedMarker = Volatile.Read(ref *flagPtr);

                    if (arrivedMarker < expectedMarker)
                    {
                        allArrived = false;
                        break;
                    }
                }
            }

            if (allArrived)
            {
                _logger.LogDebug("All participants arrived at P2P barrier '{BarrierId}' (generation {Generation})",
                    _barrierId, phase.Generation);
                return true;
            }

            // Adaptive spin-wait to reduce CPU usage
            if (spinWait.NextSpinWillYield)
            {
                // After several spins, yield to other threads
                await Task.Delay(TimeSpan.FromMicroseconds(50), cancellationToken).ConfigureAwait(false);
                spinWait.Reset();
            }
            else
            {
                spinWait.SpinOnce();
            }
        }

        return false;
    }

    private async Task<bool> WaitEventAsync(
        CrossGpuBarrierPhase phase,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        if (_cudaEvents == null)
        {
            _logger.LogWarning("CUDA events not initialized for wait in barrier '{BarrierId}'", _barrierId);
            return false;
        }

        // Wait on all participant events
        var tasks = new Task<bool>[_participantCount];

        for (var i = 0; i < _participantCount; i++)
        {
            var eventHandle = _cudaEvents[i];
            var gpuIndex = i;

            if (eventHandle == IntPtr.Zero)
            {
                // Event not recorded yet, skip
                tasks[i] = Task.FromResult(false);
                continue;
            }

            tasks[i] = Task.Run(() =>
            {
                try
                {
                    // Poll event until ready or timeout
                    var spinWait = new SpinWait();
                    while (DateTime.UtcNow < deadline)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var queryResult = CudaRuntime.cudaEventQuery(eventHandle);
                        if (queryResult == Types.Native.CudaError.Success)
                        {
                            // Event completed
                            return true;
                        }

                        if (queryResult != Types.Native.CudaError.NotReady)
                        {
                            // Unexpected error
                            _logger.LogWarning("CUDA event query failed for GPU index {GpuIndex}: {Error}",
                                gpuIndex, queryResult);
                            return false;
                        }

                        // Event not ready yet, spin/yield
                        if (spinWait.NextSpinWillYield)
                        {
                            Thread.Sleep(0);
                            spinWait.Reset();
                        }
                        else
                        {
                            spinWait.SpinOnce();
                        }
                    }

                    return false; // Timeout
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }, cancellationToken);
        }

        try
        {
            var remainingTime = deadline - DateTime.UtcNow;
            if (remainingTime <= TimeSpan.Zero)
            {
                return false;
            }

            var completedInTime = await Task.WhenAll(tasks).WaitAsync(remainingTime, cancellationToken)
                .ConfigureAwait(false);

            var allCompleted = completedInTime.All(r => r);
            if (allCompleted)
            {
                _logger.LogDebug("All CUDA events completed for barrier '{BarrierId}' (generation {Generation})",
                    _barrierId, phase.Generation);
            }

            return allCompleted;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private async Task<bool> WaitCpuAsync(
        CrossGpuBarrierPhase phase,
        DateTime deadline,
        CancellationToken cancellationToken)
    {
        // CPU fallback: Poll _arrivedCount until all participants arrive
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int currentArrived;
            int currentGeneration;

            lock (_stateLock)
            {
                currentArrived = _arrivedCount;
                currentGeneration = _generation;
            }

            // Check if generation changed (barrier reset)
            if (currentGeneration != phase.Generation)
            {
                return false;
            }

            // Check if all participants arrived
            if (currentArrived == _participantCount)
            {
                return true;
            }

            // Brief sleep to avoid spinning CPU
            await Task.Delay(TimeSpan.FromMicroseconds(100), cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }

    private void CleanupP2PMemory()
    {
        // Free the pinned host memory (device pointers are derived from host, no separate free needed)
        if (_p2pHostPointer != IntPtr.Zero)
        {
            var result = CudaRuntime.cudaFreeHost(_p2pHostPointer);
            if (result != Types.Native.CudaError.Success)
            {
                _logger.LogWarning("Failed to free P2P host memory for barrier '{BarrierId}': {Error}",
                    _barrierId, result);
            }
            else
            {
                _logger.LogDebug("P2P host memory freed for barrier '{BarrierId}'", _barrierId);
            }
            _p2pHostPointer = IntPtr.Zero;
        }

        // Disable P2P access between GPU pairs
        for (var i = 0; i < _gpuIds.Length; i++)
        {
            var setResult = CudaRuntime.cudaSetDevice(_gpuIds[i]);
            if (setResult != Types.Native.CudaError.Success)
            {
                continue;
            }

            for (var j = 0; j < _gpuIds.Length; j++)
            {
                if (i != j)
                {
                    // Best effort - ignore errors during cleanup
                    _ = CudaRuntime.cudaDeviceDisablePeerAccess(_gpuIds[j]);
                }
            }
        }
    }

    private void CleanupCudaEvents()
    {
        if (_cudaEvents == null)
        {
            return;
        }

        for (var i = 0; i < _cudaEvents.Length; i++)
        {
            var eventHandle = _cudaEvents[i];
            if (eventHandle == IntPtr.Zero)
            {
                continue;
            }

            // Set device context for cleanup (events must be destroyed on their creation device)
            var setResult = CudaRuntime.cudaSetDevice(_gpuIds[i]);
            if (setResult != Types.Native.CudaError.Success)
            {
                _logger.LogWarning("Failed to set device {GpuId} for event cleanup: {Error}",
                    _gpuIds[i], setResult);
                // Try to destroy anyway
            }

            var destroyResult = CudaRuntime.cudaEventDestroy(eventHandle);
            if (destroyResult != Types.Native.CudaError.Success)
            {
                _logger.LogWarning("Failed to destroy CUDA event for GPU {GpuId}: {Error}",
                    _gpuIds[i], destroyResult);
            }
            else
            {
                _logger.LogDebug("CUDA event destroyed for GPU {GpuId} in barrier '{BarrierId}'",
                    _gpuIds[i], _barrierId);
            }
        }
    }
}
