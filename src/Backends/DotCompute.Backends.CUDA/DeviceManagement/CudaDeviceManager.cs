// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using global::System.Runtime.CompilerServices;
using global::System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.DeviceManagement;

/// <summary>
/// Production-grade CUDA device manager with comprehensive device enumeration,
/// capability detection, multi-GPU coordination, and P2P support.
/// </summary>
public sealed class CudaDeviceManager : IDisposable
{
    private readonly ILogger<CudaDeviceManager> _logger;
    private readonly ConcurrentDictionary<int, CudaDeviceInfo> _devices;
    private readonly ConcurrentDictionary<(int, int), bool> _p2pCapabilities;
    private readonly Lock _deviceLock = new();
    private int _currentDevice = -1;
    private bool _disposed;

    public CudaDeviceManager(ILogger<CudaDeviceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _devices = new ConcurrentDictionary<int, CudaDeviceInfo>();
        _p2pCapabilities = new ConcurrentDictionary<(int, int), bool>();
        
        EnumerateDevices();
    }

    /// <summary>
    /// Gets the number of available CUDA devices.
    /// </summary>
    public int DeviceCount => _devices.Count;

    /// <summary>
    /// Gets information about all available devices.
    /// </summary>
    public IReadOnlyList<CudaDeviceInfo> Devices => _devices.Values.ToList();

    /// <summary>
    /// Gets or sets the current device for CUDA operations.
    /// </summary>
    public int CurrentDevice
    {
        get => _currentDevice;
        set => SetDevice(value);
    }

    /// <summary>
    /// Enumerates all available CUDA devices and their capabilities.
    /// </summary>
    private void EnumerateDevices()
    {
        _logger.LogInformation("Enumerating CUDA devices...");
        
        try
        {
            // Get device count
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result != CudaError.Success)
            {
                _logger.LogWarning("No CUDA devices found or CUDA not available: {Error}", result);
                return;
            }

            _logger.LogInformation("Found {Count} CUDA device(s)", deviceCount);

            // Enumerate each device
            for (var deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                try
                {
                    var deviceInfo = GetDeviceInfo(deviceId);
                    _devices.TryAdd(deviceId, deviceInfo);
                    
                    _logger.LogInformation(
                        "Device {Id}: {Name} - Compute {Major}.{Minor}, {Memory:N0} MB, {Cores} SMs",
                        deviceId, deviceInfo.Name, deviceInfo.ComputeCapabilityMajor,
                        deviceInfo.ComputeCapabilityMinor, deviceInfo.TotalMemory / (1024 * 1024),
                        deviceInfo.MultiProcessorCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enumerate device {DeviceId}", deviceId);
                }
            }

            // Detect P2P capabilities between devices
            if (deviceCount > 1)
            {
                DetectP2PCapabilities();
            }

            // Set default device
            if (deviceCount > 0 && _currentDevice < 0)
            {
                SetDevice(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate CUDA devices");
            throw new InvalidOperationException("CUDA device enumeration failed", ex);
        }
    }

    /// <summary>
    /// Gets detailed information about a specific device.
    /// </summary>
    private static CudaDeviceInfo GetDeviceInfo(int deviceId)
    {
        var props = new CudaDeviceProperties();
        var result = CudaRuntime.cudaGetDeviceProperties(ref props, deviceId);
        CudaRuntime.CheckError(result, $"getting properties for device {deviceId}");

        return new CudaDeviceInfo
        {
            DeviceId = deviceId,
            Name = GetDeviceName(props),
            ComputeCapabilityMajor = props.Major,
            ComputeCapabilityMinor = props.Minor,
            TotalMemory = (long)props.TotalGlobalMem,
            SharedMemoryPerBlock = (int)props.SharedMemPerBlock,
            RegistersPerBlock = props.RegsPerBlock,
            WarpSize = props.WarpSize,
            MaxThreadsPerBlock = props.MaxThreadsPerBlock,
            MaxBlockDimX = props.MaxThreadsDimX,
            MaxBlockDimY = props.MaxThreadsDimY,
            MaxBlockDimZ = props.MaxThreadsDimZ,
            MaxGridDimX = props.MaxGridSizeX,
            MaxGridDimY = props.MaxGridSizeY,
            MaxGridDimZ = props.MaxGridSizeZ,
            ClockRate = props.ClockRate,
            MemoryClockRate = props.MemClockRate,
            MemoryBusWidth = props.MemBusWidth,
            L2CacheSize = props.L2CacheSize,
            MaxTexture1DSize = props.MaxTexture1D,
            MaxTexture2DWidth = props.MaxTexture2DWidth,
            MaxTexture2DHeight = props.MaxTexture2DHeight,
            MaxTexture3DWidth = props.MaxTexture3DWidth,
            MaxTexture3DHeight = props.MaxTexture3DHeight,
            MaxTexture3DDepth = props.MaxTexture3DDepth,
            MultiProcessorCount = props.MultiProcessorCount,
            KernelExecutionTimeout = props.KernelExecTimeoutEnabled != 0,
            IntegratedGpu = props.Integrated != 0,
            CanMapHostMemory = props.CanMapHostMemory != 0,
            ConcurrentKernels = props.ConcurrentKernels != 0,
            EccEnabled = props.ECCEnabled != 0,
            PciDeviceId = props.PciDeviceID,
            PciBusId = props.PciBusID,
            PciDomainId = props.PciDomainID,
            TccDriver = props.TccDriver != 0,
            AsyncEngineCount = props.AsyncEngineCount,
            UnifiedAddressing = props.UnifiedAddressing != 0,
            MaxThreadsPerMultiProcessor = props.MaxThreadsPerMultiProcessor,
            StreamPrioritiesSupported = props.StreamPrioritiesSupported != 0,
            GlobalL1CacheSupported = props.GlobalL1CacheSupported != 0,
            LocalL1CacheSupported = props.LocalL1CacheSupported != 0,
            ManagedMemory = props.ManagedMemory != 0,
            SupportsManagedMemory = props.ManagedMemory != 0,  // Set both properties for compatibility
            IsMultiGpuBoard = props.IsMultiGpuBoard != 0,
            MultiGpuBoardGroupId = props.MultiGpuBoardGroupID,
            HostNativeAtomicSupported = props.HostNativeAtomicSupported != 0,
            SingleToDoublePrecisionPerfRatio = props.SingleToDoublePrecisionPerfRatio,
            PageableMemoryAccess = props.PageableMemoryAccess != 0,
            ConcurrentManagedAccess = props.ConcurrentManagedAccess != 0,
            ComputePreemptionSupported = props.ComputePreemptionSupported != 0,
            CanUseHostPointerForRegisteredMem = props.CanUseHostPointerForRegisteredMem != 0,
            CooperativeLaunch = props.CooperativeLaunch != 0,
            CooperativeMultiDeviceLaunch = props.CooperativeMultiDeviceLaunch != 0,
            MaxSharedMemoryPerMultiprocessor = (int)props.SharedMemPerMultiprocessor,
            PageableMemoryAccessUsesHostPageTables = props.PageableMemoryAccessUsesHostPageTables != 0,
            DirectManagedMemAccessFromHost = props.DirectManagedMemAccessFromHost != 0
        };
    }

    /// <summary>
    /// Extracts device name from properties.
    /// </summary>
    private static string GetDeviceName(CudaDeviceProperties props) => props.DeviceName;

    /// <summary>
    /// Detects P2P capabilities between all device pairs.
    /// </summary>
    private void DetectP2PCapabilities()
    {
        _logger.LogInformation("Detecting P2P capabilities between devices...");

        foreach (var device1 in _devices.Keys)
        {
            foreach (var device2 in _devices.Keys)
            {
                if (device1 == device2)
                {
                    continue;
                }


                try
                {
                    var canAccess = 0;
                    var result = CudaRuntime.cudaDeviceCanAccessPeer(ref canAccess, device1, device2);
                    if (result == CudaError.Success)
                    {
                        var canAccessP2P = canAccess != 0;
                        _p2pCapabilities.TryAdd((device1, device2), canAccessP2P);
                        
                        if (canAccessP2P)
                        {
                            _logger.LogInformation("P2P access available: Device {From} -> Device {To}", 
                                device1, device2);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check P2P capability between device {From} and {To}", 
                        device1, device2);
                }
            }
        }
    }

    /// <summary>
    /// Sets the current CUDA device for operations.
    /// </summary>
    public void SetDevice(int deviceId)
    {
        ThrowIfDisposed();
        
        if (!_devices.ContainsKey(deviceId))
        {
            throw new ArgumentException($"Device {deviceId} not found. Available devices: 0-{DeviceCount - 1}", 
                nameof(deviceId));
        }

        lock (_deviceLock)
        {
            if (_currentDevice == deviceId)
            {
                return;
            }


            var result = CudaRuntime.cudaSetDevice(deviceId);
            CudaRuntime.CheckError(result, $"setting device to {deviceId}");
            
            _currentDevice = deviceId;
            _logger.LogDebug("Set current device to {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Gets device information by ID.
    /// </summary>
    public CudaDeviceInfo GetDevice(int deviceId)
    {
        ThrowIfDisposed();
        
        if (_devices.TryGetValue(deviceId, out var device))
        {
            return device;
        }
        
        throw new ArgumentException($"Device {deviceId} not found", nameof(deviceId));
    }

    /// <summary>
    /// Checks if P2P access is available between two devices.
    /// </summary>
    public bool CanAccessPeer(int fromDevice, int toDevice)
    {
        ThrowIfDisposed();
        
        if (fromDevice == toDevice)
        {

            return true;
        }


        return _p2pCapabilities.TryGetValue((fromDevice, toDevice), out var canAccess) && canAccess;
    }

    /// <summary>
    /// Enables P2P access between two devices if available.
    /// </summary>
    public void EnablePeerAccess(int fromDevice, int toDevice)
    {
        ThrowIfDisposed();
        
        if (fromDevice == toDevice)
        {
            return;
        }


        if (!CanAccessPeer(fromDevice, toDevice))
        {
            throw new InvalidOperationException(
                $"P2P access not available between device {fromDevice} and device {toDevice}");
        }

        // Save current device
        var savedDevice = _currentDevice;
        
        try
        {
            // Set source device
            SetDevice(fromDevice);
            
            // Enable peer access
            var result = CudaRuntime.cudaDeviceEnablePeerAccess(toDevice, 0);
            if (result != CudaError.Success && result != CudaError.PeerAccessAlreadyEnabled)
            {
                CudaRuntime.CheckError(result, $"enabling peer access from device {fromDevice} to {toDevice}");
            }
            
            _logger.LogInformation("Enabled P2P access: Device {From} -> Device {To}", fromDevice, toDevice);
        }
        finally
        {
            // Restore device
            if (savedDevice >= 0)
            {
                SetDevice(savedDevice);
            }
        }
    }

    /// <summary>
    /// Disables P2P access between two devices.
    /// </summary>
    public void DisablePeerAccess(int fromDevice, int toDevice)
    {
        ThrowIfDisposed();
        
        if (fromDevice == toDevice)
        {
            return;
        }


        var savedDevice = _currentDevice;
        
        try
        {
            SetDevice(fromDevice);
            
            var result = CudaRuntime.cudaDeviceDisablePeerAccess(toDevice);
            if (result != CudaError.Success && result != CudaError.PeerAccessNotEnabled)
            {
                _logger.LogWarning("Failed to disable peer access from device {From} to {To}: {Error}", 
                    fromDevice, toDevice, result);
            }
        }
        finally
        {
            if (savedDevice >= 0)
            {
                SetDevice(savedDevice);
            }
        }
    }

    /// <summary>
    /// Synchronizes all devices.
    /// </summary>
    public void SynchronizeAllDevices()
    {
        ThrowIfDisposed();
        
        var savedDevice = _currentDevice;
        
        try
        {
            foreach (var deviceId in _devices.Keys)
            {
                SetDevice(deviceId);
                var result = CudaRuntime.cudaDeviceSynchronize();
                CudaRuntime.CheckError(result, $"synchronizing device {deviceId}");
            }
        }
        finally
        {
            if (savedDevice >= 0)
            {
                SetDevice(savedDevice);
            }
        }
    }

    /// <summary>
    /// Resets all devices.
    /// </summary>
    public void ResetAllDevices()
    {
        ThrowIfDisposed();
        
        foreach (var deviceId in _devices.Keys)
        {
            try
            {
                SetDevice(deviceId);
                var result = CudaRuntime.cudaDeviceReset();
                if (result != CudaError.Success)
                {
                    _logger.LogWarning("Failed to reset device {DeviceId}: {Error}", deviceId, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting device {DeviceId}", deviceId);
            }
        }
        
        _currentDevice = -1;
    }

    /// <summary>
    /// Selects the best device based on criteria.
    /// </summary>
    public int SelectBestDevice(DeviceSelectionCriteria criteria)
    {
        ThrowIfDisposed();
        
        if (_devices.IsEmpty)
        {

            throw new InvalidOperationException("No CUDA devices available");
        }


        var scoredDevices = _devices.Values
            .Select(d => new { Device = d, Score = CalculateDeviceScore(d, criteria) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var bestDevice = scoredDevices.First();
        
        _logger.LogInformation("Selected device {DeviceId} ({Name}) with score {Score:F2}",
            bestDevice.Device.DeviceId, bestDevice.Device.Name, bestDevice.Score);
        
        return bestDevice.Device.DeviceId;
    }

    /// <summary>
    /// Calculates a score for device selection.
    /// </summary>
    private static double CalculateDeviceScore(CudaDeviceInfo device, DeviceSelectionCriteria criteria)
    {
        double score = 0;

        // Memory score (normalized to 0-100)
        if (criteria.PreferLargeMemory)
        {
            score += (device.TotalMemory / (32.0 * 1024 * 1024 * 1024)) * 30; // Normalize to 32GB
        }

        // Compute capability score
        score += (device.ComputeCapabilityMajor * 10 + device.ComputeCapabilityMinor) * 2;

        // SM count score
        score += Math.Min(device.MultiProcessorCount / 100.0, 1.0) * 20;

        // Clock rate score
        score += (device.ClockRate / 2000000.0) * 10; // Normalize to 2GHz

        // Feature scores
        if (criteria.RequireTensorCores && device.ComputeCapabilityMajor >= 7)
        {
            score += 20;
        }
        
        // Additional tensor core preference (softer requirement)
        if (criteria.PreferTensorCores && device.ComputeCapabilityMajor >= 7)
        {
            score += 10;
        }
        
        // Check minimum compute capability
        var deviceComputeCapability = device.ComputeCapabilityMajor + (device.ComputeCapabilityMinor / 10.0);
        if (deviceComputeCapability < criteria.MinComputeCapability)
        {
            score *= 0.1; // Heavy penalty for not meeting minimum requirement
        }
        
        // Prefer largest memory if requested
        if (criteria.PreferLargestMemory)
        {
            score += (device.TotalMemory / (64.0 * 1024 * 1024 * 1024)) * 25; // Normalize to 64GB
        }

        if (criteria.RequireUnifiedMemory && device.ManagedMemory)
        {
            score += 10;
        }

        if (criteria.RequireP2P && device.UnifiedAddressing)
        {
            score += 10;
        }

        // Penalty for integrated GPUs if discrete preferred
        if (criteria.PreferDiscrete && device.IntegratedGpu)
        {
            score *= 0.5;
        }


        return score;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, GetType());

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // Disable all P2P connections
                foreach (var ((from, to), _) in _p2pCapabilities.Where(kvp => kvp.Value))
                {
                    try
                    {
                        DisablePeerAccess(from, to);
                    }
                    catch { /* Best effort */ }
                }

                // Reset devices if requested
                if (Environment.GetEnvironmentVariable("DOTCOMPUTE_RESET_ON_EXIT") == "1")
                {
                    ResetAllDevices();
                }
            }
            finally
            {
                _disposed = true;
                // Note: Lock doesn't implement IDisposable in .NET 9
            }
        }
    }
}

/// <summary>
/// Criteria for selecting the best CUDA device.
/// </summary>
public sealed class DeviceSelectionCriteria
{
    public bool PreferLargeMemory { get; init; } = true;
    public bool PreferDiscrete { get; init; } = true;
    public bool RequireTensorCores { get; init; }
    public bool RequireUnifiedMemory { get; init; }
    public bool RequireP2P { get; init; }
    public int MinComputeCapabilityMajor { get; init; } = 3;
    public int MinComputeCapabilityMinor { get; init; } = 5;
    public long MinMemoryBytes { get; init; }
    
    /// <summary>
    /// Gets or sets whether to prefer devices with Tensor Cores.
    /// </summary>
    public bool PreferTensorCores { get; init; }
    
    /// <summary>
    /// Gets or sets the minimum compute capability (major.minor) as a decimal.
    /// For example, 7.5 for compute capability 7.5.
    /// </summary>
    public double MinComputeCapability { get; init; } = 3.5;
    
    /// <summary>
    /// Gets or sets whether to prefer the device with the largest memory.
    /// </summary>
    public bool PreferLargestMemory { get; init; } = true;
}