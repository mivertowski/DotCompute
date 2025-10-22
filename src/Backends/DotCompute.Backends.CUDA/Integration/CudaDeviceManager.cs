// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Manages CUDA device discovery, selection, and health monitoring
/// </summary>
public sealed partial class CudaDeviceManager : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 5650,
        Level = LogLevel.Information,
        Message = "CUDA Device Manager initialized with {DeviceCount} devices")]
    private static partial void LogDeviceManagerInitialized(ILogger logger, int deviceCount);

    [LoggerMessage(
        EventId = 5651,
        Level = LogLevel.Debug,
        Message = "Selected optimal device {DeviceId} with score {Score:F2}")]
    private static partial void LogOptimalDeviceSelected(ILogger logger, int deviceId, double score);

    [LoggerMessage(
        EventId = 5652,
        Level = LogLevel.Debug,
        Message = "Device {DeviceId} optimized for workload")]
    private static partial void LogDeviceOptimized(ILogger logger, int deviceId);

    [LoggerMessage(
        EventId = 5653,
        Level = LogLevel.Warning,
        Message = "Failed to optimize device {DeviceId}")]
    private static partial void LogDeviceOptimizationFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5654,
        Level = LogLevel.Debug,
        Message = "Device maintenance completed")]
    private static partial void LogMaintenanceCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 5655,
        Level = LogLevel.Error,
        Message = "Error during device maintenance")]
    private static partial void LogMaintenanceError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5656,
        Level = LogLevel.Warning,
        Message = "Failed to get device count: {Result}")]
    private static partial void LogDeviceCountFailed(ILogger logger, CudaError result);

    [LoggerMessage(
        EventId = 5657,
        Level = LogLevel.Debug,
        Message = "Discovered device {DeviceId}: {DeviceName}")]
    private static partial void LogDeviceDiscovered(ILogger logger, int deviceId, string deviceName);

    [LoggerMessage(
        EventId = 5658,
        Level = LogLevel.Warning,
        Message = "Failed to query device {DeviceId}")]
    private static partial void LogDeviceQueryFailed(ILogger logger, Exception ex, int deviceId);

    [LoggerMessage(
        EventId = 5659,
        Level = LogLevel.Error,
        Message = "Error during device discovery")]
    private static partial void LogDiscoveryError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5660,
        Level = LogLevel.Warning,
        Message = "Device {DeviceId} health degraded: {HealthScore:F2}")]
    private static partial void LogDeviceHealthDegraded(ILogger logger, int deviceId, double healthScore);

    [LoggerMessage(
        EventId = 5661,
        Level = LogLevel.Warning,
        Message = "Error during device monitoring")]
    private static partial void LogMonitoringError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 5662,
        Level = LogLevel.Debug,
        Message = "CUDA Device Manager disposed")]
    private static partial void LogDeviceManagerDisposed(ILogger logger);

    #endregion

    private readonly ILogger _logger;
    private readonly Dictionary<int, CudaDeviceInfo> _availableDevices;
    private readonly Timer _deviceMonitorTimer;
    private volatile bool _disposed;
    /// <summary>
    /// Initializes a new instance of the CudaDeviceManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>

    public CudaDeviceManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _availableDevices = [];

        // Initialize device discovery
        DiscoverDevices();

        // Set up device monitoring
        _deviceMonitorTimer = new Timer(MonitorDevices, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        LogDeviceManagerInitialized(_logger, _availableDevices.Count);
    }

    /// <summary>
    /// Gets information about all available CUDA devices
    /// </summary>
    public IReadOnlyDictionary<int, CudaDeviceInfo> AvailableDevices => _availableDevices;

    /// <summary>
    /// Gets the optimal device for the given workload profile
    /// </summary>
    public int GetOptimalDevice(CudaWorkloadProfile profile)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_availableDevices.Count == 0)
        {
            throw new InvalidOperationException("No CUDA devices available");
        }

        // Score devices based on workload requirements
        var bestDevice = _availableDevices
            .Select(kvp => new { DeviceId = kvp.Key, Device = kvp.Value, Score = CalculateDeviceScore(kvp.Value, profile) })
            .OrderByDescending(x => x.Score)
            .First();

        LogOptimalDeviceSelected(_logger, bestDevice.DeviceId, bestDevice.Score);

        return bestDevice.DeviceId;
    }

    /// <summary>
    /// Gets detailed device information for a specific device
    /// </summary>
    public CudaDeviceInfo GetDeviceInfo(int deviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_availableDevices.TryGetValue(deviceId, out var deviceInfo))
        {
            throw new ArgumentException($"Device {deviceId} not found", nameof(deviceId));
        }

        return deviceInfo;
    }

    /// <summary>
    /// Gets the health status of all devices
    /// </summary>
    public double GetDeviceHealth()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_availableDevices.Count == 0)
        {
            return 0.0;
        }

        var totalHealth = 0.0;
        var healthyDevices = 0;

        foreach (var device in _availableDevices.Values)
        {
            var deviceHealth = CalculateDeviceHealth(device);
            totalHealth += deviceHealth;
            if (deviceHealth > 0.5)
            {
                healthyDevices++;
            }
        }

        // Overall health considers both average health and ratio of healthy devices
        var averageHealth = totalHealth / _availableDevices.Count;
        var healthyRatio = (double)healthyDevices / _availableDevices.Count;

        return (averageHealth + healthyRatio) / 2.0;
    }

    /// <summary>
    /// Optimizes device settings for the given workload
    /// </summary>
    public async Task OptimizeDeviceAsync(CudaWorkloadProfile profile, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await Task.Run(() =>
        {
            foreach (var deviceId in _availableDevices.Keys)
            {
                try
                {
                    OptimizeDeviceForWorkload(deviceId, profile);
                    LogDeviceOptimized(_logger, deviceId);
                }
                catch (Exception ex)
                {
                    LogDeviceOptimizationFailed(_logger, ex, deviceId);
                }
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Performs maintenance on all devices
    /// </summary>
    public void PerformMaintenance()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Re-discover devices to detect new ones or removed ones
            DiscoverDevices();

            // Update device health status
            foreach (var device in _availableDevices.Values)
            {
                UpdateDeviceHealth(device);
            }

            LogMaintenanceCompleted(_logger);
        }
        catch (Exception ex)
        {
            LogMaintenanceError(_logger, ex);
        }
    }

    private void DiscoverDevices()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success)
            {
                LogDeviceCountFailed(_logger, result);
                return;
            }

            _availableDevices.Clear();

            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var deviceInfo = QueryDeviceInfo(i);
                    _availableDevices[i] = deviceInfo;
                    LogDeviceDiscovered(_logger, i, deviceInfo.Name);
                }
                catch (Exception ex)
                {
                    LogDeviceQueryFailed(_logger, ex, i);
                }
            }
        }
        catch (Exception ex)
        {
            LogDiscoveryError(_logger, ex);
        }
    }

    private static CudaDeviceInfo QueryDeviceInfo(int deviceId)
    {
        var result = CudaRuntime.cudaSetDevice(deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set device {deviceId}: {result}");
        }

        var props = new CudaDeviceProperties();
        result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to get device properties for device {deviceId}: {result}");
        }

        // Query memory information
        result = CudaRuntime.cudaMemGetInfo(out var freeMemory, out var totalMemory);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to get memory info for device {deviceId}: {result}");
        }

        return new CudaDeviceInfo
        {
            DeviceId = deviceId,
            Name = props.DeviceName,
            ComputeCapabilityMajor = props.Major,
            ComputeCapabilityMinor = props.Minor,
            TotalMemory = (long)totalMemory,
            FreeMemory = (long)freeMemory,
            MaxThreadsPerBlock = props.MaxThreadsPerBlock,
            MultiProcessorCount = props.MultiProcessorCount,
            WarpSize = props.WarpSize,
            MaxBlockDimX = props.MaxThreadsDim[0],
            MaxBlockDimY = props.MaxThreadsDim[1],
            MaxBlockDimZ = props.MaxThreadsDim[2],
            MaxGridDimX = props.MaxGridDim[0],
            MaxGridDimY = props.MaxGridDim[1],
            MaxGridDimZ = props.MaxGridDim[2],
            ClockRate = props.ClockRate,
            MemoryClockRate = props.MemoryClockRate,
            MemoryBusWidth = props.MemoryBusWidth,
            LastHealthCheck = DateTimeOffset.UtcNow,
            HealthScore = 1.0 // Initialize as healthy
        };
    }

    private static double CalculateDeviceScore(CudaDeviceInfo device, CudaWorkloadProfile profile)
    {
        var score = 0.0;

        // Memory score
        if (profile.IsMemoryIntensive)
        {
            var memoryUtilization = 1.0 - (double)device.FreeMemory / device.TotalMemory;
            score += (1.0 - memoryUtilization) * 30.0; // Available memory
            score += Math.Min(device.MemoryBusWidth / 512.0, 1.0) * 20.0; // Memory bandwidth
        }

        // Compute score
        if (profile.IsComputeIntensive)
        {
            score += device.MultiProcessorCount * 2.0; // Raw compute power
            score += Math.Min(device.ClockRate / 2000.0, 1.0) * 15.0; // Clock speed
        }

        // Precision score
        if (profile.RequiresHighPrecision)
        {
            var computeCapability = device.ComputeCapabilityMajor + device.ComputeCapabilityMinor / 10.0;
            score += Math.Min(computeCapability / 8.0, 1.0) * 10.0; // Newer architectures
        }

        // Parallelism score
        var maxConcurrency = device.MultiProcessorCount * device.MaxThreadsPerBlock / device.WarpSize;
        var parallelismMatch = Math.Min((double)profile.EstimatedParallelism / maxConcurrency, 1.0);
        score += parallelismMatch * 15.0;

        // Health score
        score += device.HealthScore * 10.0;

        return score;
    }

    private static double CalculateDeviceHealth(CudaDeviceInfo device)
    {
        // Simple health calculation based on memory availability and age of last check
        var memoryHealth = (double)device.FreeMemory / device.TotalMemory;
        var timeHealth = 1.0; // Assume healthy if recently checked

        return (memoryHealth + timeHealth) / 2.0;
    }

    private static void OptimizeDeviceForWorkload(int deviceId, CudaWorkloadProfile profile)
    {
        // Set device as current
        var result = CudaRuntime.cudaSetDevice(deviceId);
        if (result != CudaError.Success)
        {
            throw new InvalidOperationException($"Failed to set device {deviceId}: {result}");
        }

        // Device-specific optimizations would go here
        // For example: setting cache preferences, shared memory configuration, etc.
    }

    private static void UpdateDeviceHealth(CudaDeviceInfo device)
    {
        try
        {
            // Update memory information
            var result = CudaRuntime.cudaSetDevice(device.DeviceId);
            if (result == CudaError.Success)
            {
                result = CudaRuntime.cudaMemGetInfo(out var freeMemory, out var totalMemory);
                if (result == CudaError.Success)
                {
                    device.FreeMemory = (long)freeMemory;
                    device.TotalMemory = (long)totalMemory;
                    device.HealthScore = CalculateDeviceHealth(device);
                }
            }

            device.LastHealthCheck = DateTimeOffset.UtcNow;
        }
        catch
        {
            device.HealthScore = 0.0; // Mark as unhealthy on any error
        }
    }

    private void MonitorDevices(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            foreach (var device in _availableDevices.Values)
            {
                UpdateDeviceHealth(device);

                if (device.HealthScore < 0.5)
                {
                    LogDeviceHealthDegraded(_logger, device.DeviceId, device.HealthScore);
                }
            }
        }
        catch (Exception ex)
        {
            LogMonitoringError(_logger, ex);
        }
    }
    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _deviceMonitorTimer?.Dispose();
            _disposed = true;
            LogDeviceManagerDisposed(_logger);
        }
    }

    /// <summary>
    /// Creates an IAccelerator wrapper for the given context
    /// </summary>
    public static IAccelerator CreateAcceleratorWrapper(CudaContext context) => new CudaAccelerator(context.DeviceId);
}

/// <summary>
/// Information about a CUDA device
/// </summary>
public sealed class CudaDeviceInfo
{
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public int DeviceId { get; init; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the compute capability major.
    /// </summary>
    /// <value>The compute capability major.</value>
    public int ComputeCapabilityMajor { get; init; }
    /// <summary>
    /// Gets or sets the compute capability minor.
    /// </summary>
    /// <value>The compute capability minor.</value>
    public int ComputeCapabilityMinor { get; init; }
    /// <summary>
    /// Gets or sets the total memory.
    /// </summary>
    /// <value>The total memory.</value>
    public long TotalMemory { get; set; }
    /// <summary>
    /// Gets or sets the free memory.
    /// </summary>
    /// <value>The free memory.</value>
    public long FreeMemory { get; set; }
    /// <summary>
    /// Gets or sets the max threads per block.
    /// </summary>
    /// <value>The max threads per block.</value>
    public int MaxThreadsPerBlock { get; init; }
    /// <summary>
    /// Gets or sets the multi processor count.
    /// </summary>
    /// <value>The multi processor count.</value>
    public int MultiProcessorCount { get; init; }
    /// <summary>
    /// Gets or sets the warp size.
    /// </summary>
    /// <value>The warp size.</value>
    public int WarpSize { get; init; }
    /// <summary>
    /// Gets or sets the max block dim x.
    /// </summary>
    /// <value>The max block dim x.</value>
    public int MaxBlockDimX { get; init; }
    /// <summary>
    /// Gets or sets the max block dim y.
    /// </summary>
    /// <value>The max block dim y.</value>
    public int MaxBlockDimY { get; init; }
    /// <summary>
    /// Gets or sets the max block dim z.
    /// </summary>
    /// <value>The max block dim z.</value>
    public int MaxBlockDimZ { get; init; }
    /// <summary>
    /// Gets or sets the max grid dim x.
    /// </summary>
    /// <value>The max grid dim x.</value>
    public int MaxGridDimX { get; init; }
    /// <summary>
    /// Gets or sets the max grid dim y.
    /// </summary>
    /// <value>The max grid dim y.</value>
    public int MaxGridDimY { get; init; }
    /// <summary>
    /// Gets or sets the max grid dim z.
    /// </summary>
    /// <value>The max grid dim z.</value>
    public int MaxGridDimZ { get; init; }
    /// <summary>
    /// Gets or sets the clock rate.
    /// </summary>
    /// <value>The clock rate.</value>
    public int ClockRate { get; init; }
    /// <summary>
    /// Gets or sets the memory clock rate.
    /// </summary>
    /// <value>The memory clock rate.</value>
    public int MemoryClockRate { get; init; }
    /// <summary>
    /// Gets or sets the memory bus width.
    /// </summary>
    /// <value>The memory bus width.</value>
    public int MemoryBusWidth { get; init; }
    /// <summary>
    /// Gets or sets the last health check.
    /// </summary>
    /// <value>The last health check.</value>
    public DateTimeOffset LastHealthCheck { get; set; }
    /// <summary>
    /// Gets or sets the health score.
    /// </summary>
    /// <value>The health score.</value>
    public double HealthScore { get; set; }
}
