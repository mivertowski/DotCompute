// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Memory;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Integration.Components;

/// <summary>
/// CUDA device management component responsible for device discovery, context management,
/// and hardware abstraction layer interactions.
/// </summary>
public sealed class CudaDeviceManager : IDisposable
{
    private readonly ILogger<CudaDeviceManager> _logger;
    private readonly Dictionary<int, CudaDeviceInfo> _deviceCache;
    private readonly Dictionary<CudaContext, IAccelerator> _contextToAcceleratorMap;
    private readonly object _mapLock = new();
    private volatile bool _disposed;

    public CudaDeviceManager(ILogger<CudaDeviceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _deviceCache = [];
        _contextToAcceleratorMap = [];

        InitializeDeviceCache();
    }

    /// <summary>
    /// Gets device information for the specified device ID.
    /// </summary>
    /// <param name="deviceId">CUDA device ID.</param>
    /// <returns>Device information.</returns>
    public CudaDeviceInfo GetDeviceInfo(int deviceId)
    {
        ThrowIfDisposed();

        if (_deviceCache.TryGetValue(deviceId, out var cachedInfo))
        {
            return cachedInfo;
        }

        var deviceInfo = QueryDeviceInfo(deviceId);
        _deviceCache[deviceId] = deviceInfo;
        return deviceInfo;
    }

    /// <summary>
    /// Creates an IAccelerator wrapper for the given CUDA context.
    /// </summary>
    /// <param name="context">CUDA context to wrap.</param>
    /// <returns>IAccelerator interface implementation.</returns>
    public IAccelerator CreateAcceleratorWrapper(CudaContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        lock (_mapLock)
        {
            // Check if we already have an accelerator for this context
            if (_contextToAcceleratorMap.TryGetValue(context, out var existingAccelerator))
            {
                return existingAccelerator;
            }

            // Create a wrapper accelerator that uses the existing context
            var accelerator = new CudaContextAcceleratorWrapper(context, _logger);
            _contextToAcceleratorMap[context] = accelerator;
            return accelerator;
        }
    }

    /// <summary>
    /// Gets the number of available CUDA devices.
    /// </summary>
    /// <returns>Number of CUDA devices.</returns>
    public int GetDeviceCount()
    {
        ThrowIfDisposed();

        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var count);
            return result == CudaError.Success ? count : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query CUDA device count");
            return 0;
        }
    }

    /// <summary>
    /// Queries available memory for the specified device.
    /// </summary>
    /// <param name="deviceId">Device ID to query.</param>
    /// <returns>Tuple of (free memory, total memory) in bytes.</returns>
    public (long freeMemory, long totalMemory) QueryDeviceMemory(int deviceId)
    {
        ThrowIfDisposed();

        try
        {
            var result = CudaRuntime.cudaSetDevice(deviceId);
            if (result == CudaError.Success)
            {
                if (CudaRuntime.cudaMemGetInfo(out var free, out var total) == CudaError.Success)
                {
                    return ((long)free, (long)total);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query memory for device {DeviceId}", deviceId);
        }

        // Return default values on error
        return (4L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024); // 4GB free, 8GB total
    }

    /// <summary>
    /// Gets CUDA driver version information.
    /// </summary>
    /// <returns>Driver version string.</returns>
    public string GetCudaDriverVersion()
    {
        try
        {
            var result = CudaRuntime.cudaDriverGetVersion(out var version);
            if (result == CudaError.Success)
            {
                var major = version / 1000;
                var minor = (version % 1000) / 10;
                return $"{major}.{minor}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query CUDA driver version");
        }

        return "12.0"; // Default fallback version
    }

    /// <summary>
    /// Validates that a device ID is valid and available.
    /// </summary>
    /// <param name="deviceId">Device ID to validate.</param>
    /// <returns>True if device is valid and available.</returns>
    public bool IsDeviceValid(int deviceId)
    {
        ThrowIfDisposed();

        try
        {
            var deviceCount = GetDeviceCount();
            if (deviceId < 0 || deviceId >= deviceCount)
            {
                return false;
            }

            // Try to set device to verify it's accessible
            var result = CudaRuntime.cudaSetDevice(deviceId);
            return result == CudaError.Success;
        }
        catch
        {
            return false;
        }
    }

    private void InitializeDeviceCache()
    {
        try
        {
            var deviceCount = GetDeviceCount();
            _logger.LogDebug("Initializing CUDA device cache for {DeviceCount} devices", deviceCount);

            for (var i = 0; i < deviceCount; i++)
            {
                try
                {
                    var deviceInfo = QueryDeviceInfo(i);
                    _deviceCache[i] = deviceInfo;
                    _logger.LogDebug("Cached device info for device {DeviceId}: {DeviceName}", i, deviceInfo.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache device info for device {DeviceId}", i);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CUDA device cache");
        }
    }

    private CudaDeviceInfo QueryDeviceInfo(int deviceId)
    {
        try
        {
            var result = CudaRuntime.cudaSetDevice(deviceId);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to set CUDA device {deviceId}: {result}");
            }

            var props = new CudaDeviceProperties();
            result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to get device properties for device {deviceId}: {result}");
            }

            var (freeMemory, totalMemory) = QueryDeviceMemory(deviceId);

            return new CudaDeviceInfo
            {
                DeviceId = deviceId,
                Name = props.GetName(),
                ComputeCapabilityMajor = props.Major,
                ComputeCapabilityMinor = props.Minor,
                TotalMemory = totalMemory,
                FreeMemory = freeMemory,
                MaxThreadsPerBlock = props.MaxThreadsPerBlock,
                MaxGridDimX = props.MaxGridSizeX,
                MaxGridDimY = props.MaxGridSizeY,
                MaxGridDimZ = props.MaxGridSizeZ,
                MaxBlockDimX = props.MaxBlockDimX,
                MaxBlockDimY = props.MaxBlockDimY,
                MaxBlockDimZ = props.MaxBlockDimZ,
                WarpSize = props.WarpSize,
                MultiProcessorCount = props.MultiProcessorCount,
                ClockRate = props.ClockRate,
                MemoryClockRate = props.MemoryClockRate,
                GlobalMemoryBusWidth = props.MemoryBusWidth,
                L2CacheSize = props.L2CacheSize,
                MaxSharedMemoryPerBlock = props.SharedMemPerBlock,
                MaxRegistersPerBlock = props.RegsPerBlock,
                IsIntegratedDevice = props.Integrated != 0,
                CanMapHostMemory = props.CanMapHostMemory != 0,
                ComputeMode = (CudaComputeMode)props.ComputeMode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query device properties for device {DeviceId}", deviceId);

            // Return basic device info as fallback
            return new CudaDeviceInfo
            {
                DeviceId = deviceId,
                Name = $"CUDA Device {deviceId}",
                ComputeCapabilityMajor = 7, // Conservative fallback
                ComputeCapabilityMinor = 0,
                TotalMemory = 8L * 1024 * 1024 * 1024, // 8GB
                FreeMemory = 4L * 1024 * 1024 * 1024,  // 4GB
                MaxThreadsPerBlock = 1024,
                WarpSize = 32,
                MultiProcessorCount = 68,
                ClockRate = 1500000, // 1.5 GHz in kHz
                ComputeMode = CudaComputeMode.Default
            };
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaDeviceManager));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;

            // Clean up accelerator wrappers
            lock (_mapLock)
            {
                foreach (var accelerator in _contextToAcceleratorMap.Values)
                {
                    try
                    {
                        accelerator.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing accelerator wrapper");
                    }
                }
                _contextToAcceleratorMap.Clear();
            }

            _deviceCache.Clear();
            _logger.LogDebug("CUDA device manager disposed");
        }
    }
}

#region Supporting Types

/// <summary>
/// Comprehensive CUDA device information.
/// </summary>
public sealed class CudaDeviceInfo
{
    public int DeviceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ComputeCapabilityMajor { get; init; }
    public int ComputeCapabilityMinor { get; init; }
    public long TotalMemory { get; init; }
    public long FreeMemory { get; init; }
    public int MaxThreadsPerBlock { get; init; }
    public int MaxGridDimX { get; init; }
    public int MaxGridDimY { get; init; }
    public int MaxGridDimZ { get; init; }
    public int MaxBlockDimX { get; init; }
    public int MaxBlockDimY { get; init; }
    public int MaxBlockDimZ { get; init; }
    public int WarpSize { get; init; }
    public int MultiProcessorCount { get; init; }
    public int ClockRate { get; init; }
    public int MemoryClockRate { get; init; }
    public int GlobalMemoryBusWidth { get; init; }
    public int L2CacheSize { get; init; }
    public long MaxSharedMemoryPerBlock { get; init; }
    public int MaxRegistersPerBlock { get; init; }
    public bool IsIntegratedDevice { get; init; }
    public bool CanMapHostMemory { get; init; }
    public CudaComputeMode ComputeMode { get; init; }

    public string ComputeCapability => $"{ComputeCapabilityMajor}.{ComputeCapabilityMinor}";
    public double MemoryBandwidthGBps => (MemoryClockRate * 2.0 * GlobalMemoryBusWidth) / (8.0 * 1000);
    public double PeakPerformanceTFLOPS => (MultiProcessorCount * ClockRate * 128.0) / (1000000000.0);
}

/// <summary>
/// CUDA compute modes.
/// </summary>
public enum CudaComputeMode
{
    Default = 0,
    Exclusive = 1,
    Prohibited = 2,
    ExclusiveProcess = 3
}

/// <summary>
/// Lightweight wrapper that adapts an existing CudaContext to IAccelerator interface.
/// </summary>
internal sealed class CudaContextAcceleratorWrapper : IAccelerator
{
    private readonly CudaContext _context;
    private readonly ILogger _logger;
    private readonly AcceleratorInfo _info;
    private readonly AcceleratorContext _acceleratorContext;
    private readonly CudaContextMemoryManager _memoryManager;
    private bool _disposed;

    public CudaContextAcceleratorWrapper(CudaContext context, ILogger logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Extract device info from context
        var deviceId = _context.DeviceId;

        // Initialize memory manager with proper CUDA implementation
        _memoryManager = new CudaContextMemoryManager(_context, _logger);

        _info = new AcceleratorInfo
        {
            Id = $"cuda-device-{deviceId}",
            Name = $"CUDA Device {deviceId}",
            DeviceType = "CUDA",
            Vendor = "NVIDIA",
            DriverVersion = GetCudaDriverVersion(),
            TotalMemory = QueryDeviceMemory(deviceId).total,
            AvailableMemory = QueryDeviceMemory(deviceId).available,
            MaxWorkGroupSize = QueryMaxWorkGroupSize(deviceId)
        };

        // Create accelerator context
        _acceleratorContext = new AcceleratorContext(_context.Handle, deviceId);
    }

    public AcceleratorInfo Info => _info;
    public AcceleratorType Type => AcceleratorType.CUDA;
    public string DeviceType => "GPU";
    public IUnifiedMemoryManager Memory => _memoryManager;
    public IUnifiedMemoryManager MemoryManager => _memoryManager;
    public AcceleratorContext Context => _acceleratorContext;
    public bool IsAvailable => !_disposed;

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        DotCompute.Abstractions.CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = new CudaContextCompiledKernel(_context, _logger, definition.Name);
            return ValueTask.FromResult<ICompiledKernel>(kernel);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }

    public void Synchronize()
    {
        try
        {
            _context.MakeCurrent();
            _context.Synchronize();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to synchronize CUDA context", ex);
        }
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                _context.MakeCurrent();
                _context.Synchronize();
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to synchronize CUDA context asynchronously", ex);
        }
    }

    private static (long available, long total) QueryDeviceMemory(int deviceId)
    {
        try
        {
            var result = CudaRuntime.cudaSetDevice(deviceId);
            if (result == CudaError.Success)
            {
                if (CudaRuntime.cudaMemGetInfo(out var free, out var total) == CudaError.Success)
                {
                    return ((long)free, (long)total);
                }
            }
        }
        catch
        {
            // Fallback to default value on error
        }

        return (4L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024); // 4GB free, 8GB total default
    }

    private static string GetCudaDriverVersion()
    {
        try
        {
            var result = CudaRuntime.cudaDriverGetVersion(out var version);
            if (result == CudaError.Success)
            {
                var major = version / 1000;
                var minor = (version % 1000) / 10;
                return $"{major}.{minor}";
            }
        }
        catch
        {
            // Fallback if unable to query
        }
        return "12.0"; // Default fallback version
    }

    private static int QueryMaxWorkGroupSize(int deviceId)
    {
        try
        {
            var result = CudaRuntime.cudaSetDevice(deviceId);
            if (result == CudaError.Success)
            {
                var props = new CudaDeviceProperties();
                if (CudaRuntime.cudaGetDeviceProperties(ref props, deviceId) == CudaError.Success)
                {
                    return props.MaxThreadsPerBlock;
                }
            }
        }
        catch
        {
            // Fallback to default value on error
        }
        return 1024; // Safe default
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _memoryManager?.Dispose();
            // Don't dispose the context as we don't own it
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Error during accelerator wrapper disposal: {ex.Message}");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_memoryManager != null)
            {
                await _memoryManager.DisposeAsync().ConfigureAwait(false);
            }
            // Don't dispose the context as we don't own it
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Error during accelerator wrapper async disposal: {ex.Message}");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

#endregion