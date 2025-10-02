// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Integration;

/// <summary>
/// Manages CUDA device discovery, selection, and health monitoring
/// </summary>
public sealed class CudaDeviceManager : IDisposable
{
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

        _logger.LogInfoMessage($"CUDA Device Manager initialized with {_availableDevices.Count} devices");
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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaDeviceManager));
        }

        if (_availableDevices.Count == 0)
        {
            throw new InvalidOperationException("No CUDA devices available");
        }

        // Score devices based on workload requirements
        var bestDevice = _availableDevices
            .Select(kvp => new { DeviceId = kvp.Key, Device = kvp.Value, Score = CalculateDeviceScore(kvp.Value, profile) })
            .OrderByDescending(x => x.Score)
            .First();

        _logger.LogDebugMessage($"Selected optimal device {bestDevice.DeviceId} with score {bestDevice.Score:F2}");

        return bestDevice.DeviceId;
    }

    /// <summary>
    /// Gets detailed device information for a specific device
    /// </summary>
    public CudaDeviceInfo GetDeviceInfo(int deviceId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaDeviceManager));
        }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaDeviceManager));
        }

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
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaDeviceManager));
        }

        await Task.Run(() =>
        {
            foreach (var deviceId in _availableDevices.Keys)
            {
                try
                {
                    OptimizeDeviceForWorkload(deviceId, profile);
                    _logger.LogDebugMessage($"Device {deviceId} optimized for workload");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to optimize device {DeviceId}", deviceId);
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

            _logger.LogDebugMessage("Device maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during device maintenance");
        }
    }

    private void DiscoverDevices()
    {
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("Failed to get device count: {Result}", result);
                return;
            }

            _availableDevices.Clear();

            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var deviceInfo = QueryDeviceInfo(i);
                    _availableDevices[i] = deviceInfo;
                    _logger.LogDebugMessage($"Discovered device {i}: {deviceInfo.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query device {DeviceId}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during device discovery");
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
                    _logger.LogWarning("Device {DeviceId} health degraded: {HealthScore:F2}", device.DeviceId, device.HealthScore);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during device monitoring");
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
            _logger.LogDebugMessage("CUDA Device Manager disposed");
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
