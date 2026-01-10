// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.CUDA.Configuration;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;

namespace DotCompute.Backends.CUDA.Barriers;

/// <summary>
/// CUDA-specific implementation of barrier provider using Cooperative Groups.
/// </summary>
/// <remarks>
/// <para>
/// This provider implements GPU-native barrier synchronization with hardware acceleration:
/// <list type="bullet">
/// <item><description><strong>Thread-Block Barriers</strong>: Via __syncthreads() (CC 1.0+)</description></item>
/// <item><description><strong>Grid-Wide Barriers</strong>: Via Cooperative Groups (CC 6.0+)</description></item>
/// <item><description><strong>Named Barriers</strong>: Up to 16 per block (CC 7.0+)</description></item>
/// <item><description><strong>Warp Barriers</strong>: Via __syncwarp() (CC 7.0+)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Cooperative Launch Requirements:</strong>
/// Grid-wide barriers require kernels launched with <c>cudaLaunchCooperativeKernel</c>,
/// which guarantees all threads execute concurrently. Query <c>GetMaxCooperativeGridSize()</c>
/// to determine device limits.
/// </para>
/// <para>
/// <strong>Performance:</strong>
/// <list type="bullet">
/// <item><description>Barrier creation: ~1μs overhead</description></item>
/// <item><description>Thread-block sync: ~10ns hardware latency</description></item>
/// <item><description>Grid-wide sync: ~1-10μs depending on size</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class CudaBarrierProvider : IBarrierProvider, IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 8000,
        Level = LogLevel.Debug,
        Message = "CudaBarrierProvider initialized. Grid barriers: {SupportsGrid}, Named barriers: {SupportsNamed}, Max cooperative size: {MaxCoopSize}")]
    private static partial void LogProviderInitialized(ILogger logger, bool supportsGrid, bool supportsNamed, int maxCoopSize);

    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Debug,
        Message = "Created barrier ID={BarrierId}, scope={Scope}, capacity={Capacity}, name={Name}")]
    private static partial void LogBarrierCreated(ILogger logger, int barrierId, BarrierScope scope, int capacity, string? name);

    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "Cooperative launch {Status}. Grid barriers now {State}")]
    private static partial void LogCooperativeLaunchChanged(ILogger logger, string status, string state);

    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Warning,
        Message = "Cannot create grid barrier: device does not support cooperative launch (CC < 6.0)")]
    private static partial void LogGridBarrierNotSupported(ILogger logger);

    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Warning,
        Message = "Named barrier limit reached ({Current}/{Max}). Consider reusing existing barriers")]
    private static partial void LogNamedBarrierLimitReached(ILogger logger, int current, int max);

    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Debug,
        Message = "Reset all barriers. Destroyed {Count} active barriers")]
    private static partial void LogAllBarriersReset(ILogger logger, int count);

    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Warning,
        Message = "Failed to query cooperative grid size, assuming 0")]
    private static partial void LogCooperativeQueryFailed(ILogger logger, Exception exception);

    #endregion

    private readonly CudaContext _context;
    private readonly CudaDevice _device;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, IBarrierHandle> _barriers;
    private readonly ConcurrentDictionary<string, int> _namedBarriers;
    private readonly bool _supportsGridBarriers;
    private readonly bool _supportsNamedBarriers;
    private readonly int _maxCooperativeGridSize;
    private readonly int _maxNamedBarriersPerBlock;
    private readonly MultiGpuSynchronizer? _multiGpuSynchronizer;
    private readonly List<(int deviceId, CudaContext context)>? _registeredDevices;
    private int _nextBarrierId;
    private bool _cooperativeLaunchEnabled;
    private bool _disposed;

    /// <summary>
    /// Initializes a new CUDA barrier provider.
    /// </summary>
    /// <param name="context">The CUDA context for barrier operations.</param>
    /// <param name="device">The CUDA device for capability queries.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public CudaBarrierProvider(CudaContext context, CudaDevice device, ILogger? logger = null)
        : this(context, device, null, logger)
    {
    }

    /// <summary>
    /// Initializes a new CUDA barrier provider with multi-GPU support.
    /// </summary>
    /// <param name="context">The primary CUDA context for barrier operations.</param>
    /// <param name="device">The CUDA device for capability queries.</param>
    /// <param name="additionalDevices">Additional devices for system-wide barriers (optional).</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public CudaBarrierProvider(
        CudaContext context,
        CudaDevice device,
        IEnumerable<(int deviceId, CudaContext context)>? additionalDevices,
        ILogger? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _logger = logger ?? NullLogger.Instance;
        _barriers = new ConcurrentDictionary<int, IBarrierHandle>();
        _namedBarriers = new ConcurrentDictionary<string, int>();
        _nextBarrierId = 1;

        // Query device capabilities
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        _supportsGridBarriers = major >= 6; // Pascal (CC 6.0) and newer
        _supportsNamedBarriers = major >= 7; // Volta (CC 7.0) and newer
        _maxNamedBarriersPerBlock = _supportsNamedBarriers ? 16 : 1;

        // Query maximum cooperative grid size
        _maxCooperativeGridSize = QueryMaxCooperativeGridSize();

        // Initialize multi-GPU support if additional devices provided
        if (additionalDevices != null)
        {
            _registeredDevices = new List<(int, CudaContext)>
            {
                (device.DeviceId, context)
            };
            _registeredDevices.AddRange(additionalDevices);

            _multiGpuSynchronizer = new MultiGpuSynchronizer(_logger);
        }

        LogProviderInitialized(_logger, _supportsGridBarriers, _supportsNamedBarriers, _maxCooperativeGridSize);
    }

    /// <inheritdoc />
    public IBarrierHandle CreateBarrier(BarrierScope scope, int capacity, string? name = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        // Validate scope support
        if (scope == BarrierScope.Grid && !_supportsGridBarriers)
        {
            LogGridBarrierNotSupported(_logger);
            throw new NotSupportedException(
                "Grid barriers require Compute Capability 6.0+ (Pascal or newer).");
        }

        // Validate named barrier limit
        if (!string.IsNullOrEmpty(name))
        {
            if (!_supportsNamedBarriers)
            {
                throw new NotSupportedException(
                    "Named barriers require Compute Capability 11.0+ or device with named barrier support.");
            }

            if (_namedBarriers.Count >= _maxNamedBarriersPerBlock)
            {
                LogNamedBarrierLimitReached(_logger, _namedBarriers.Count, _maxNamedBarriersPerBlock);
                throw new InvalidOperationException(
                    $"Maximum named barriers ({_maxNamedBarriersPerBlock}) reached. " +
                    "Consider reusing existing barriers or using anonymous barriers.");
            }
        }

        // Validate warp scope
        if (scope == BarrierScope.Warp && capacity != 32)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity),
                "Warp barriers must have capacity of exactly 32 threads (warp size).");
        }

        // Validate system scope
        if (scope == BarrierScope.System)
        {
            if (_multiGpuSynchronizer == null || _registeredDevices == null)
            {
                throw new NotSupportedException(
                    "System barriers require multi-GPU configuration. " +
                    "Initialize CudaBarrierProvider with additional devices.");
            }

            if (_registeredDevices.Count < 2)
            {
                throw new InvalidOperationException(
                    "System barriers require at least 2 devices. " +
                    $"Currently registered: {_registeredDevices.Count}");
            }

            if (_registeredDevices.Count > 8)
            {
                throw new InvalidOperationException(
                    "System barriers support maximum 8 devices. " +
                    $"Currently registered: {_registeredDevices.Count}");
            }
        }

        // Allocate barrier ID
        var barrierId = Interlocked.Increment(ref _nextBarrierId);

        // Create handle based on scope
        IBarrierHandle handle;
        if (scope == BarrierScope.System)
        {
            // Create system-wide barrier
            var contexts = _registeredDevices!.Select(d => d.context);
            var deviceIds = _registeredDevices!.Select(d => d.deviceId);

            handle = new CudaSystemBarrier(
                this,
                _multiGpuSynchronizer!,
                contexts,
                deviceIds,
                barrierId,
                capacity,
                name,
                _logger);
        }
        else
        {
            // Create standard device-local barrier
            handle = new CudaBarrierHandle(_context, this, barrierId, scope, capacity, name);
        }

        if (!_barriers.TryAdd(barrierId, handle))
        {
            handle.Dispose();
            throw new InvalidOperationException($"Barrier ID {barrierId} already exists.");
        }

        // Register named barrier
        if (!string.IsNullOrEmpty(name))
        {
            if (!_namedBarriers.TryAdd(name, barrierId))
            {
                _barriers.TryRemove(barrierId, out _);
                handle.Dispose();
                throw new InvalidOperationException($"Barrier with name '{name}' already exists.");
            }
        }

        LogBarrierCreated(_logger, barrierId, scope, capacity, name);
        return handle;
    }

    /// <inheritdoc />
    public IBarrierHandle? GetBarrier(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (_namedBarriers.TryGetValue(name, out var barrierId) &&
            _barriers.TryGetValue(barrierId, out var handle))
        {
            return handle;
        }

        return null;
    }

    /// <inheritdoc />
    public void EnableCooperativeLaunch(bool enable = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (enable && !_supportsGridBarriers)
        {
            throw new NotSupportedException(
                "Cooperative launch requires Compute Capability 6.0+ (Pascal or newer).");
        }

        _cooperativeLaunchEnabled = enable;
        LogCooperativeLaunchChanged(_logger,
            enable ? "enabled" : "disabled",
            enable ? "available" : "disabled");
    }

    /// <inheritdoc />
    public bool IsCooperativeLaunchEnabled => _cooperativeLaunchEnabled;

    /// <inheritdoc />
    public int GetMaxCooperativeGridSize() => _maxCooperativeGridSize;

    /// <inheritdoc />
    public int ActiveBarrierCount => _barriers.Count;

    /// <inheritdoc />
    public void ResetAllBarriers()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var count = _barriers.Count;

        // Dispose all barriers
        foreach (var kvp in _barriers)
        {
            kvp.Value.Dispose();
        }

        _barriers.Clear();
        _namedBarriers.Clear();

        LogAllBarriersReset(_logger, count);
    }

    /// <summary>
    /// Queries the maximum cooperative grid size from the device.
    /// </summary>
    private int QueryMaxCooperativeGridSize()
    {
        if (!_supportsGridBarriers)
        {
            return 0;
        }

        try
        {
            // Query device properties directly
            var smCount = _device.StreamingMultiprocessorCount;
            var maxThreadsPerBlock = _device.MaxThreadsPerBlock;

            // Maximum cooperative grid = SM count * max threads per block
            return smCount * maxThreadsPerBlock;
        }
        catch (Exception ex)
        {
            LogCooperativeQueryFailed(_logger, ex);
            return 0;
        }
    }

    /// <summary>
    /// Removes a barrier from tracking when it's disposed.
    /// </summary>
    /// <param name="barrierId">The barrier ID to remove.</param>
    /// <param name="name">Optional barrier name to remove from named collection.</param>
    internal void RemoveBarrier(int barrierId, string? name = null)
    {
        _barriers.TryRemove(barrierId, out _);

        if (!string.IsNullOrEmpty(name))
        {
            _namedBarriers.TryRemove(name, out _);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteWithBarrierAsync(
        DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel kernel,
        IBarrierHandle barrier,
        object config,
        object[] arguments,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(barrier);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(arguments);

        // Cast to CUDA-specific types
        if (config is not Configuration.LaunchConfiguration launchConfig)
        {
            throw new ArgumentException(
                $"Expected LaunchConfiguration, got {config.GetType().Name}",
                nameof(config));
        }

        if (barrier is not CudaBarrierHandle cudaBarrier)
        {
            throw new ArgumentException(
                $"Expected CudaBarrierHandle, got {barrier.GetType().Name}",
                nameof(barrier));
        }

        // Validate barrier capacity against launch configuration
        var totalThreads = launchConfig.GridSize.X * launchConfig.GridSize.Y * launchConfig.GridSize.Z *
                          launchConfig.BlockSize.X * launchConfig.BlockSize.Y * launchConfig.BlockSize.Z;
        var blockSize = launchConfig.BlockSize.X * launchConfig.BlockSize.Y * launchConfig.BlockSize.Z;

        switch (barrier.Scope)
        {
            case BarrierScope.ThreadBlock:
                if (barrier.Capacity > blockSize)
                {
                    throw new InvalidOperationException(
                        $"Thread-block barrier capacity ({barrier.Capacity}) exceeds block size ({blockSize})");
                }
                break;

            case BarrierScope.Grid:
                if (barrier.Capacity != totalThreads)
                {
                    throw new InvalidOperationException(
                        $"Grid barrier capacity ({barrier.Capacity}) must equal total thread count ({totalThreads})");
                }

                // Enable cooperative launch for grid barriers
                if (!_cooperativeLaunchEnabled)
                {
                    LogEnablingCooperativeLaunch(_logger);
                    EnableCooperativeLaunch(true);
                }

                // Validate grid size doesn't exceed cooperative limit
                if (totalThreads > _maxCooperativeGridSize)
                {
                    throw new InvalidOperationException(
                        $"Grid size ({totalThreads}) exceeds maximum cooperative grid size ({_maxCooperativeGridSize})");
                }
                break;

            case BarrierScope.Warp:
                if (barrier.Capacity != 32)
                {
                    throw new InvalidOperationException(
                        $"Warp barrier capacity must be 32, got {barrier.Capacity}");
                }
                break;

            case BarrierScope.Tile:
                if (barrier.Capacity > blockSize)
                {
                    throw new InvalidOperationException(
                        $"Tile barrier capacity ({barrier.Capacity}) exceeds block size ({blockSize})");
                }
                break;

            case BarrierScope.System:
                // System barriers coordinate across multiple GPUs
                // They don't have the same thread count constraints as device-local barriers
                if (_multiGpuSynchronizer == null)
                {
                    throw new InvalidOperationException(
                        "System barriers require multi-GPU configuration");
                }
                break;
        }

        // Prepend barrier handle to arguments
        // For GPU kernels, we pass the barrier ID and device pointer (if grid barrier)
        var barrierArgs = barrier.Scope == BarrierScope.Grid
            ? new object[] { barrier.BarrierId, cudaBarrier.DeviceBarrierPtr }
            : new object[] { barrier.BarrierId };

        var allArgs = barrierArgs.Concat(arguments).ToArray();

        LogExecutingKernelWithBarrier(_logger, kernel.Name, barrier.Scope.ToString(), barrier.Capacity);

        // Execute kernel via ICompiledKernel.ExecuteAsync
        await kernel.ExecuteAsync(allArgs, ct).ConfigureAwait(false);

        LogKernelExecutionComplete(_logger, kernel.Name);
    }

    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Debug,
        Message = "Executing kernel '{KernelName}' with {BarrierScope} barrier (capacity: {Capacity})")]
    private static partial void LogExecutingKernelWithBarrier(ILogger logger, string kernelName, string barrierScope, int capacity);

    [LoggerMessage(
        EventId = 8008,
        Level = LogLevel.Debug,
        Message = "Kernel '{KernelName}' execution complete")]
    private static partial void LogKernelExecutionComplete(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 8009,
        Level = LogLevel.Information,
        Message = "Automatically enabling cooperative launch for grid barrier")]
    private static partial void LogEnablingCooperativeLaunch(ILogger logger);

    /// <summary>
    /// Disposes the barrier provider and all active barriers.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResetAllBarriers();

        // Dispose multi-GPU synchronizer if present
        _multiGpuSynchronizer?.Dispose();

        _disposed = true;

        GC.SuppressFinalize(this);
    }
}
