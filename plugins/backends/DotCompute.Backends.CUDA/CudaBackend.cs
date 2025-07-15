// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Main entry point for CUDA compute backend
/// </summary>
public class CudaBackend : IDisposable
{
    private readonly ILogger<CudaBackend> _logger;
    private readonly List<CudaAccelerator> _accelerators = new();
    private bool _disposed;

    public CudaBackend(ILogger<CudaBackend> logger)
    {
        _logger = logger;
        DiscoverAccelerators();
    }

    /// <summary>
    /// Check if CUDA is available on this platform
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            // This would call CUDA runtime detection
            return CudaRuntime.IsCudaSupported();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all available CUDA accelerators
    /// </summary>
    public IReadOnlyList<CudaAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get default CUDA accelerator
    /// </summary>
    public CudaAccelerator? GetDefaultAccelerator() => _accelerators.FirstOrDefault();

    private void DiscoverAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("CUDA is not available on this platform");
            return;
        }

        try
        {
            _logger.LogInformation("Discovering CUDA devices...");
            
            // 1. Enumerate CUDA devices using cuDeviceGet
            var deviceCountResult = CudaRuntime.cudaGetDeviceCount(out int deviceCount);
            if (deviceCountResult != CudaError.Success)
            {
                _logger.LogError("Failed to get CUDA device count: {Error}", CudaRuntime.GetErrorString(deviceCountResult));
                return;
            }

            if (deviceCount == 0)
            {
                _logger.LogInformation("No CUDA devices found");
                return;
            }

            _logger.LogInformation("Found {DeviceCount} CUDA device(s)", deviceCount);

            // 2. Query device properties for each device
            for (int deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                try
                {
                    if (ValidateDeviceAccessibility(deviceId))
                    {
                        var accelerator = CreateAccelerator(deviceId);
                        if (accelerator != null)
                        {
                            _accelerators.Add(accelerator);
                            LogDeviceCapabilities(accelerator);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize CUDA device {DeviceId}", deviceId);
                }
            }

            _logger.LogInformation("CUDA device discovery completed - {AcceleratorCount} accelerators available", _accelerators.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover CUDA accelerators");
        }
    }

    private bool ValidateDeviceAccessibility(int deviceId)
    {
        try
        {
            // 3. Check CUDA runtime version compatibility
            var runtimeVersionResult = CudaRuntime.cudaRuntimeGetVersion(out int runtimeVersion);
            if (runtimeVersionResult != CudaError.Success)
            {
                _logger.LogWarning("Could not determine CUDA runtime version for device {DeviceId}", deviceId);
                return false;
            }

            var driverVersionResult = CudaRuntime.cudaDriverGetVersion(out int driverVersion);
            if (driverVersionResult != CudaError.Success)
            {
                _logger.LogWarning("Could not determine CUDA driver version for device {DeviceId}", deviceId);
                return false;
            }

            // Check minimum version requirements (CUDA 11.0+)
            if (runtimeVersion < 11000 || driverVersion < 11000)
            {
                _logger.LogWarning("CUDA device {DeviceId} requires CUDA 11.0 or higher. Runtime: {Runtime}, Driver: {Driver}",
                    deviceId, runtimeVersion, driverVersion);
                return false;
            }

            // 4. Validate device accessibility
            var setDeviceResult = CudaRuntime.cudaSetDevice(deviceId);
            if (setDeviceResult != CudaError.Success)
            {
                _logger.LogWarning("Cannot access CUDA device {DeviceId}: {Error}", 
                    deviceId, CudaRuntime.GetErrorString(setDeviceResult));
                return false;
            }

            // Test basic device operation
            var syncResult = CudaRuntime.cudaDeviceSynchronize();
            if (syncResult != CudaError.Success)
            {
                _logger.LogWarning("CUDA device {DeviceId} failed synchronization test: {Error}", 
                    deviceId, CudaRuntime.GetErrorString(syncResult));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating CUDA device {DeviceId} accessibility", deviceId);
            return false;
        }
    }

    private CudaAccelerator? CreateAccelerator(int deviceId)
    {
        try
        {
            var deviceProps = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, deviceId);
            
            if (result != CudaError.Success)
            {
                _logger.LogError("Failed to get properties for CUDA device {DeviceId}: {Error}", 
                    deviceId, CudaRuntime.GetErrorString(result));
                return null;
            }

            // Check compute capability (require 5.0+)
            if (deviceProps.Major < 5)
            {
                _logger.LogInformation("Skipping CUDA device {DeviceId} ({Name}) - compute capability {Major}.{Minor} is below minimum 5.0",
                    deviceId, deviceProps.Name, deviceProps.Major, deviceProps.Minor);
                return null;
            }

            var acceleratorLogger = _logger is ILoggerFactory loggerFactory
                ? loggerFactory.CreateLogger<CudaAccelerator>()
                : new NullLogger<CudaAccelerator>();
            
            return new CudaAccelerator(deviceId, acceleratorLogger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create accelerator for CUDA device {DeviceId}", deviceId);
            return null;
        }
    }

    private void LogDeviceCapabilities(CudaAccelerator accelerator)
    {
        var info = accelerator.Info;
        var capabilities = info.Capabilities;
        
        _logger.LogInformation("CUDA Device: {Name} (ID: {Id})", info.Name, info.Id);
        _logger.LogInformation("  Compute Capability: {ComputeCapability}", info.ComputeCapability);
        _logger.LogInformation("  Total Memory: {TotalMemory:N0} bytes ({MemoryGB:F1} GB)", 
            info.TotalMemory, info.TotalMemory / (1024.0 * 1024 * 1024));
        _logger.LogInformation("  Multiprocessors: {ComputeUnits}", info.ComputeUnits);
        _logger.LogInformation("  Clock Rate: {ClockRate} MHz", info.MaxClockFrequency);
        
        if (capabilities.TryGetValue("SharedMemoryPerBlock", out var sharedMem))
            _logger.LogInformation("  Shared Memory per Block: {SharedMem:N0} bytes", sharedMem);
        
        if (capabilities.TryGetValue("MaxThreadsPerBlock", out var maxThreads))
            _logger.LogInformation("  Max Threads per Block: {MaxThreads}", maxThreads);
            
        if (capabilities.TryGetValue("WarpSize", out var warpSize))
            _logger.LogInformation("  Warp Size: {WarpSize}", warpSize);
            
        if (capabilities.TryGetValue("ECCEnabled", out var ecc) && (bool)ecc)
            _logger.LogInformation("  ECC Memory: Enabled");
            
        if (capabilities.TryGetValue("UnifiedAddressing", out var unified) && (bool)unified)
            _logger.LogInformation("  Unified Virtual Addressing: Supported");
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var accelerator in _accelerators)
        {
            accelerator?.Dispose();
        }
        
        _accelerators.Clear();
        _disposed = true;
    }
}