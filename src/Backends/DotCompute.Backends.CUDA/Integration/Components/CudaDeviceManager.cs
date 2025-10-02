// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Compilation;
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
    /// <summary>
    /// Initializes a new instance of the CudaDeviceManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>

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
                Name = props.DeviceName,
                ComputeCapabilityMajor = props.Major,
                ComputeCapabilityMinor = props.Minor,
                TotalMemory = totalMemory,
                FreeMemory = freeMemory,
                MaxThreadsPerBlock = props.MaxThreadsPerBlock,
                MaxGridDimX = props.MaxGridSizeX,
                MaxGridDimY = props.MaxGridSizeY,
                MaxGridDimZ = props.MaxGridSizeZ,
                MaxBlockDimX = props.MaxThreadsDimX,
                MaxBlockDimY = props.MaxThreadsDimY,
                MaxBlockDimZ = props.MaxThreadsDimZ,
                WarpSize = props.WarpSize,
                MultiProcessorCount = props.MultiProcessorCount,
                ClockRate = props.ClockRate,
                MemoryClockRate = props.MemoryClockRate,
                GlobalMemoryBusWidth = props.MemoryBusWidth,
                L2CacheSize = props.L2CacheSize,
                MaxSharedMemoryPerBlock = (long)props.SharedMemPerBlock,
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
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                        (accelerator as IDisposable)?.Dispose();
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
    public long TotalMemory { get; init; }
    /// <summary>
    /// Gets or sets the free memory.
    /// </summary>
    /// <value>The free memory.</value>
    public long FreeMemory { get; init; }
    /// <summary>
    /// Gets or sets the max threads per block.
    /// </summary>
    /// <value>The max threads per block.</value>
    public int MaxThreadsPerBlock { get; init; }
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
    /// Gets or sets the warp size.
    /// </summary>
    /// <value>The warp size.</value>
    public int WarpSize { get; init; }
    /// <summary>
    /// Gets or sets the multi processor count.
    /// </summary>
    /// <value>The multi processor count.</value>
    public int MultiProcessorCount { get; init; }
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
    /// Gets or sets the global memory bus width.
    /// </summary>
    /// <value>The global memory bus width.</value>
    public int GlobalMemoryBusWidth { get; init; }
    /// <summary>
    /// Gets or sets the l2 cache size.
    /// </summary>
    /// <value>The l2 cache size.</value>
    public int L2CacheSize { get; init; }
    /// <summary>
    /// Gets or sets the max shared memory per block.
    /// </summary>
    /// <value>The max shared memory per block.</value>
    public long MaxSharedMemoryPerBlock { get; init; }
    /// <summary>
    /// Gets or sets the max registers per block.
    /// </summary>
    /// <value>The max registers per block.</value>
    public int MaxRegistersPerBlock { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether integrated device.
    /// </summary>
    /// <value>The is integrated device.</value>
    public bool IsIntegratedDevice { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether map host memory.
    /// </summary>
    /// <value>The can map host memory.</value>
    public bool CanMapHostMemory { get; init; }
    /// <summary>
    /// Gets or sets the compute mode.
    /// </summary>
    /// <value>The compute mode.</value>
    public CudaComputeMode ComputeMode { get; init; }
    /// <summary>
    /// Gets or sets the compute capability.
    /// </summary>
    /// <value>The compute capability.</value>

    public string ComputeCapability => $"{ComputeCapabilityMajor}.{ComputeCapabilityMinor}";
    /// <summary>
    /// Gets or sets the memory bandwidth g bps.
    /// </summary>
    /// <value>The memory bandwidth g bps.</value>
    public double MemoryBandwidthGBps => (MemoryClockRate * 2.0 * GlobalMemoryBusWidth) / (8.0 * 1000);
    /// <summary>
    /// Gets or sets the peak performance t f l o p s.
    /// </summary>
    /// <value>The peak performance t f l o p s.</value>
    public double PeakPerformanceTFLOPS => (MultiProcessorCount * ClockRate * 128.0) / (1000000000.0);
}
/// <summary>
/// An cuda compute mode enumeration.
/// </summary>

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
    /// <summary>
    /// Initializes a new instance of the CudaContextAcceleratorWrapper class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="logger">The logger.</param>

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
    /// <summary>
    /// Gets or sets the info.
    /// </summary>
    /// <value>The info.</value>

    public AcceleratorInfo Info => _info;
    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    /// <value>The type.</value>
    public AcceleratorType Type => AcceleratorType.CUDA;
    /// <summary>
    /// Gets or sets the device type.
    /// </summary>
    /// <value>The device type.</value>
    public string DeviceType => "GPU";
    /// <summary>
    /// Gets or sets the memory.
    /// </summary>
    /// <value>The memory.</value>
    public IUnifiedMemoryManager Memory => _memoryManager.UnderlyingManager;
    /// <summary>
    /// Gets or sets the memory manager.
    /// </summary>
    /// <value>The memory manager.</value>
    public IUnifiedMemoryManager MemoryManager => _memoryManager.UnderlyingManager;
    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    public AcceleratorContext Context => _acceleratorContext;
    /// <summary>
    /// Gets or sets a value indicating whether available.
    /// </summary>
    /// <value>The is available.</value>
    public bool IsAvailable => !_disposed;
    /// <summary>
    /// Gets compile kernel asynchronously.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Return a minimal stub - actual compilation happens via the pipeline
            var emptyPtx = Array.Empty<byte>();
            var kernel = new CudaCompiledKernel(_context, definition.Name, definition.Name, emptyPtx, options, _logger);
            return ValueTask.FromResult<ICompiledKernel>(kernel);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compile kernel '{definition.Name}'", ex);
        }
    }
    /// <summary>
    /// Performs synchronize.
    /// </summary>

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
    /// <summary>
    /// Gets synchronize asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>

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
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
    /// <summary>
    /// Gets dispose asynchronously.
    /// </summary>
    /// <returns>The result of the operation.</returns>

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