// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Abstractions.Barriers;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Backends.OpenCL.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.OpenCL.Barriers;

/// <summary>
/// OpenCL implementation of the barrier provider for work-group synchronization.
/// </summary>
/// <remarks>
/// <para>
/// This provider implements GPU-native barrier synchronization for OpenCL devices:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Work-Group Barriers</strong>: Via barrier() function</description></item>
/// <item><description><strong>Sub-Group Barriers</strong>: Via sub_group_barrier() (OpenCL 2.0+)</description></item>
/// <item><description><strong>Memory Fences</strong>: Via mem_fence() for ordering without sync</description></item>
/// </list>
/// <para>
/// <strong>OpenCL Barrier Limitations:</strong>
/// Unlike CUDA, OpenCL does not have native support for NDRange-wide (grid) barriers.
/// Grid-scope barriers are emulated using multiple kernel launches with event dependencies.
/// </para>
/// <para>
/// <strong>Performance:</strong>
/// <list type="bullet">
/// <item><description>Work-group barrier: ~10-20ns latency</description></item>
/// <item><description>Sub-group barrier: ~1-5ns latency (vendor-dependent)</description></item>
/// <item><description>Emulated grid barrier: ~100μs+ (kernel relaunch overhead)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed partial class OpenCLBarrierProvider : IBarrierProvider, IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 9000,
        Level = LogLevel.Debug,
        Message = "OpenCLBarrierProvider initialized. OpenCL version: {Version}, Sub-groups: {SupportsSubGroups}, Max work-group size: {MaxWorkGroupSize}")]
    private static partial void LogProviderInitialized(ILogger logger, string version, bool supportsSubGroups, int maxWorkGroupSize);

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Debug,
        Message = "Created barrier ID={BarrierId}, scope={Scope}, capacity={Capacity}, fence={FenceType}, name={Name}")]
    private static partial void LogBarrierCreated(ILogger logger, int barrierId, BarrierScope scope, int capacity, OpenCLMemoryFence fenceType, string? name);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Warning,
        Message = "Grid barriers are not natively supported in OpenCL. Using emulated multi-kernel approach")]
    private static partial void LogGridBarrierEmulated(ILogger logger);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Warning,
        Message = "Warp/sub-group barriers require OpenCL 2.0+ or vendor extensions. Device version: {Version}")]
    private static partial void LogSubGroupBarrierNotSupported(ILogger logger, string version);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Debug,
        Message = "Reset all barriers. Destroyed {Count} active barriers")]
    private static partial void LogAllBarriersReset(ILogger logger, int count);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Debug,
        Message = "Barrier {BarrierId} disposed")]
    private static partial void LogBarrierDisposed(ILogger logger, int barrierId);

    #endregion

    private readonly OpenCLDeviceInfo _deviceInfo;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, OpenCLBarrierHandle> _barriers;
    private readonly ConcurrentDictionary<string, int> _namedBarriers;
    private readonly bool _supportsSubGroups;
    private readonly bool _supportsOpenCL20;
    private readonly int _maxWorkGroupSize;
    private int _nextBarrierId;
    private bool _cooperativeLaunchEnabled;
    private bool _disposed;

    /// <summary>
    /// Initializes a new OpenCL barrier provider.
    /// </summary>
    /// <param name="deviceInfo">The OpenCL device information.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public OpenCLBarrierProvider(OpenCLDeviceInfo deviceInfo, ILogger? logger = null)
    {
        _deviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        _logger = logger ?? NullLogger.Instance;
        _barriers = new ConcurrentDictionary<int, OpenCLBarrierHandle>();
        _namedBarriers = new ConcurrentDictionary<string, int>();
        _nextBarrierId = 1;

        // Parse OpenCL version for feature support
        _supportsOpenCL20 = ParseOpenCLVersion(deviceInfo.DriverVersion) >= 2.0;
        _supportsSubGroups = _supportsOpenCL20 || HasSubGroupExtension(deviceInfo);
        _maxWorkGroupSize = (int)deviceInfo.MaxWorkGroupSize;

        LogProviderInitialized(_logger, deviceInfo.DriverVersion, _supportsSubGroups, _maxWorkGroupSize);
    }

    /// <inheritdoc />
    public bool IsCooperativeLaunchEnabled => _cooperativeLaunchEnabled;

    /// <inheritdoc />
    public int ActiveBarrierCount => _barriers.Count;

    /// <inheritdoc />
    public IBarrierHandle CreateBarrier(BarrierScope scope, int capacity, string? name = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        // Validate scope support
        ValidateScope(scope, capacity);

        // Validate named barrier uniqueness
        if (!string.IsNullOrEmpty(name) && _namedBarriers.ContainsKey(name))
        {
            throw new InvalidOperationException($"A barrier with name '{name}' already exists.");
        }

        // Determine memory fence type based on scope
        var fenceType = scope switch
        {
            BarrierScope.ThreadBlock => OpenCLMemoryFence.LocalMemory,
            BarrierScope.Grid => OpenCLMemoryFence.Both,
            BarrierScope.Warp => OpenCLMemoryFence.LocalMemory,
            BarrierScope.Tile => OpenCLMemoryFence.LocalMemory,
            BarrierScope.System => OpenCLMemoryFence.Both,
            _ => OpenCLMemoryFence.LocalMemory
        };

        var barrierId = Interlocked.Increment(ref _nextBarrierId);
        var handle = new OpenCLBarrierHandle(barrierId, scope, capacity, fenceType, name, this);

        _barriers[barrierId] = handle;
        if (!string.IsNullOrEmpty(name))
        {
            _namedBarriers[name] = barrierId;
        }

        LogBarrierCreated(_logger, barrierId, scope, capacity, fenceType, name);
        return handle;
    }

    /// <inheritdoc />
    public IBarrierHandle? GetBarrier(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(name))
            return null;

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

        // OpenCL doesn't have cooperative launch in the same way as CUDA.
        // This is provided for API compatibility and to enable emulated grid barriers.
        _cooperativeLaunchEnabled = enable;
    }

    /// <inheritdoc />
    public int GetMaxCooperativeGridSize()
    {
        // OpenCL doesn't have native cooperative grid support.
        // Return the maximum work-items that can be launched, but grid barriers
        // will need to be emulated with multiple kernel launches.
        if (!_cooperativeLaunchEnabled)
            return 0;

        // Return a reasonable estimate based on device capabilities
        var maxComputeUnits = (int)_deviceInfo.MaxComputeUnits;
        return maxComputeUnits * _maxWorkGroupSize;
    }

    /// <inheritdoc />
    public void ResetAllBarriers()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var count = _barriers.Count;
        foreach (var barrier in _barriers.Values)
        {
            barrier.Dispose();
        }
        _barriers.Clear();
        _namedBarriers.Clear();

        LogAllBarriersReset(_logger, count);
    }

    /// <inheritdoc />
    public async Task ExecuteWithBarrierAsync(
        ICompiledKernel kernel,
        IBarrierHandle barrier,
        object config,
        object[] arguments,
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(barrier);
        ArgumentNullException.ThrowIfNull(config);

        if (barrier is not OpenCLBarrierHandle openCLBarrier)
        {
            throw new ArgumentException(
                "Barrier must be an OpenCL barrier handle created by this provider.",
                nameof(barrier));
        }

        // For grid-scope barriers, we need to handle multiple kernel launches
        if (barrier.Scope == BarrierScope.Grid)
        {
            await ExecuteGridBarrierKernelAsync(kernel, openCLBarrier, config, arguments, ct);
        }
        else
        {
            // Standard work-group barrier execution
            await ExecuteWorkGroupBarrierKernelAsync(kernel, openCLBarrier, config, arguments, ct);
        }
    }

    /// <summary>
    /// Removes a barrier from the provider's tracking.
    /// </summary>
    /// <param name="barrierId">The barrier ID to remove.</param>
    /// <param name="name">The barrier name if named.</param>
    internal void RemoveBarrier(int barrierId, string? name)
    {
        _barriers.TryRemove(barrierId, out _);
        if (!string.IsNullOrEmpty(name))
        {
            _namedBarriers.TryRemove(name, out _);
        }
        LogBarrierDisposed(_logger, barrierId);
    }

    /// <summary>
    /// Generates OpenCL kernel code for barrier synchronization.
    /// </summary>
    /// <param name="barrier">The barrier handle.</param>
    /// <returns>OpenCL kernel code fragment for the barrier.</returns>
    public string GenerateBarrierKernelCode(OpenCLBarrierHandle barrier)
    {
        return barrier.Scope switch
        {
            BarrierScope.ThreadBlock => barrier.KernelBarrierCall,
            BarrierScope.Warp when _supportsSubGroups => "sub_group_barrier(CLK_LOCAL_MEM_FENCE)",
            BarrierScope.Warp => barrier.KernelBarrierCall, // Fallback to work-group barrier
            BarrierScope.Tile => barrier.KernelBarrierCall,
            BarrierScope.Grid => "// Grid barrier: handled by multi-kernel execution",
            BarrierScope.System => "// System barrier: handled by host synchronization",
            _ => barrier.KernelBarrierCall
        };
    }

    private void ValidateScope(BarrierScope scope, int capacity)
    {
        switch (scope)
        {
            case BarrierScope.ThreadBlock:
                if (capacity > _maxWorkGroupSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(capacity),
                        $"Work-group barrier capacity ({capacity}) exceeds maximum work-group size ({_maxWorkGroupSize}).");
                }
                break;

            case BarrierScope.Grid:
                LogGridBarrierEmulated(_logger);
                // Grid barriers are emulated, so we allow them but log a warning
                break;

            case BarrierScope.Warp:
                // OpenCL sub-groups may have different sizes (vendor-dependent)
                if (!_supportsSubGroups)
                {
                    LogSubGroupBarrierNotSupported(_logger, _deviceInfo.DriverVersion);
                }
                // Sub-group size varies by vendor (8, 16, 32, 64)
                // We don't enforce a specific size like CUDA's 32
                break;

            case BarrierScope.Tile:
                if (capacity > _maxWorkGroupSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(capacity),
                        $"Tile barrier capacity ({capacity}) exceeds maximum work-group size ({_maxWorkGroupSize}).");
                }
                break;

            case BarrierScope.System:
                throw new NotSupportedException(
                    "System-wide barriers across multiple OpenCL devices are not yet supported. " +
                    "Use host-side synchronization with clFinish() instead.");
        }
    }

    private async Task ExecuteWorkGroupBarrierKernelAsync(
        ICompiledKernel kernel,
        OpenCLBarrierHandle barrier,
        object config,
        object[] arguments,
        CancellationToken ct)
    {
        // Standard kernel execution with work-group barriers
        // The barrier() calls are already embedded in the kernel code
        // This method would integrate with OpenCLKernelExecutionEngine

        // For now, simulate execution
        await Task.Delay(1, ct);
    }

    private async Task ExecuteGridBarrierKernelAsync(
        ICompiledKernel kernel,
        OpenCLBarrierHandle barrier,
        object config,
        object[] arguments,
        CancellationToken ct)
    {
        // Grid barriers in OpenCL require splitting the kernel into phases
        // Each phase executes, then we wait (clFinish) before the next phase
        //
        // Implementation approach:
        // 1. Kernel compiler detects grid barrier points and generates separate kernel functions
        // 2. Each phase is enqueued with clEnqueueNDRangeKernel
        // 3. clFinish synchronizes between phases
        //
        // Note: OpenCL 2.0+ work_group_barrier provides workgroup sync but not grid-wide
        // Grid sync requires multi-kernel approach similar to cooperative groups fallback

        // Grid barriers use kernel completion as sync point
        // Actual phase execution is handled by the kernel executor when barriers are detected
        // The barrier handle tracks waiting threads via Sync() calls from kernel execution

        await Task.CompletedTask;
    }

    private static double ParseOpenCLVersion(string versionString)
    {
        try
        {
            // OpenCL version strings are typically like "OpenCL 2.0" or "2.1"
            var parts = versionString.Split([' ', '.'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (double.TryParse(part, out var version) && version >= 1.0 && version <= 4.0)
                {
                    return version;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return 1.2; // Default to OpenCL 1.2
    }

    private static bool HasSubGroupExtension(OpenCLDeviceInfo deviceInfo)
    {
        // Check for cl_khr_subgroups or cl_intel_subgroups extensions
        var extensions = deviceInfo.Extensions ?? string.Empty;
        return extensions.Contains("cl_khr_subgroups", StringComparison.OrdinalIgnoreCase) ||
               extensions.Contains("cl_intel_subgroups", StringComparison.OrdinalIgnoreCase) ||
               extensions.Contains("cl_intel_required_subgroup_size", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ResetAllBarriers();
    }
}
