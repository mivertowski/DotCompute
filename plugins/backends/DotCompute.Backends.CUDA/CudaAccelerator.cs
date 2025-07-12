// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DotCompute.Core.Abstractions;
using DotCompute.Core.Abstractions.Backends;
using DotCompute.Core.Abstractions.Memory;
using DotCompute.Core.Abstractions.Compilation;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// CUDA accelerator implementation for GPU compute operations
/// </summary>
public class CudaAccelerator : IAccelerator
{
    private readonly ILogger<CudaAccelerator> _logger;
    private readonly CudaContext _context;
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaKernelCompiler _kernelCompiler;
    private readonly Dictionary<string, ComputeCapability> _capabilities;
    private readonly int _deviceId;
    private bool _disposed;

    public string Name => $"CUDA Device {_deviceId}";
    public AcceleratorType Type => AcceleratorType.Cuda;
    public IMemoryManager MemoryManager => _memoryManager;
    public IKernelCompiler KernelCompiler => _kernelCompiler;

    public CudaAccelerator(int deviceId = 0, ILogger<CudaAccelerator>? logger = null)
    {
        _logger = logger ?? new NullLogger<CudaAccelerator>();
        _deviceId = deviceId;
        
        try
        {
            _logger.LogInformation("Initializing CUDA accelerator for device {DeviceId}", deviceId);
            
            // Initialize CUDA context
            _context = new CudaContext(deviceId);
            
            // Create memory manager
            _memoryManager = new CudaMemoryManager(_context, _logger);
            
            // Create kernel compiler
            _kernelCompiler = new CudaKernelCompiler(_context, _logger);
            
            // Query device capabilities
            _capabilities = QueryDeviceCapabilities();
            
            _logger.LogInformation("CUDA accelerator initialized successfully. Device: {DeviceName}, Compute Capability: {ComputeCapability}",
                _capabilities["DeviceName"].Value,
                $"{_capabilities["ComputeCapabilityMajor"].Value}.{_capabilities["ComputeCapabilityMinor"].Value}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CUDA accelerator");
            throw new AcceleratorException("Failed to initialize CUDA accelerator", ex);
        }
    }

    public IEnumerable<ComputeCapability> GetCapabilities()
    {
        return _capabilities.Values;
    }

    public bool SupportsCapability(string capability)
    {
        return _capabilities.ContainsKey(capability);
    }

    public void Synchronize()
    {
        ThrowIfDisposed();
        
        try
        {
            _logger.LogDebug("Synchronizing CUDA device");
            var result = CudaRuntime.cudaDeviceSynchronize();
            
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"CUDA synchronization failed: {result}");
            }
        }
        catch (Exception ex) when (!(ex is AcceleratorException))
        {
            _logger.LogError(ex, "Unexpected error during CUDA synchronization");
            throw new AcceleratorException("Failed to synchronize CUDA device", ex);
        }
    }

    public void Reset()
    {
        ThrowIfDisposed();
        
        try
        {
            _logger.LogInformation("Resetting CUDA device");
            
            // Clear all allocations
            _memoryManager.Reset();
            
            // Reset the device
            var result = CudaRuntime.cudaDeviceReset();
            
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"CUDA device reset failed: {result}");
            }
            
            // Reinitialize context
            _context.Reinitialize();
        }
        catch (Exception ex) when (!(ex is AcceleratorException))
        {
            _logger.LogError(ex, "Unexpected error during CUDA device reset");
            throw new AcceleratorException("Failed to reset CUDA device", ex);
        }
    }

    private Dictionary<string, ComputeCapability> QueryDeviceCapabilities()
    {
        var capabilities = new Dictionary<string, ComputeCapability>();
        
        try
        {
            var deviceProps = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, _deviceId);
            
            if (result != CudaError.Success)
            {
                throw new AcceleratorException($"Failed to query device properties: {result}");
            }
            
            // Basic device info
            capabilities["DeviceName"] = new ComputeCapability("DeviceName", deviceProps.Name);
            capabilities["ComputeCapabilityMajor"] = new ComputeCapability("ComputeCapabilityMajor", deviceProps.Major);
            capabilities["ComputeCapabilityMinor"] = new ComputeCapability("ComputeCapabilityMinor", deviceProps.Minor);
            
            // Memory info
            capabilities["TotalGlobalMemory"] = new ComputeCapability("TotalGlobalMemory", deviceProps.TotalGlobalMem);
            capabilities["SharedMemoryPerBlock"] = new ComputeCapability("SharedMemoryPerBlock", deviceProps.SharedMemPerBlock);
            capabilities["ConstantMemory"] = new ComputeCapability("ConstantMemory", deviceProps.TotalConstMem);
            capabilities["L2CacheSize"] = new ComputeCapability("L2CacheSize", deviceProps.L2CacheSize);
            
            // Compute info
            capabilities["MultiprocessorCount"] = new ComputeCapability("MultiprocessorCount", deviceProps.MultiProcessorCount);
            capabilities["MaxThreadsPerBlock"] = new ComputeCapability("MaxThreadsPerBlock", deviceProps.MaxThreadsPerBlock);
            capabilities["MaxThreadsPerMultiprocessor"] = new ComputeCapability("MaxThreadsPerMultiprocessor", deviceProps.MaxThreadsPerMultiProcessor);
            capabilities["WarpSize"] = new ComputeCapability("WarpSize", deviceProps.WarpSize);
            capabilities["MaxGridSizeX"] = new ComputeCapability("MaxGridSizeX", deviceProps.MaxGridSize[0]);
            capabilities["MaxGridSizeY"] = new ComputeCapability("MaxGridSizeY", deviceProps.MaxGridSize[1]);
            capabilities["MaxGridSizeZ"] = new ComputeCapability("MaxGridSizeZ", deviceProps.MaxGridSize[2]);
            capabilities["MaxBlockSizeX"] = new ComputeCapability("MaxBlockSizeX", deviceProps.MaxThreadsDim[0]);
            capabilities["MaxBlockSizeY"] = new ComputeCapability("MaxBlockSizeY", deviceProps.MaxThreadsDim[1]);
            capabilities["MaxBlockSizeZ"] = new ComputeCapability("MaxBlockSizeZ", deviceProps.MaxThreadsDim[2]);
            
            // Features
            capabilities["AsyncEngineCount"] = new ComputeCapability("AsyncEngineCount", deviceProps.AsyncEngineCount);
            capabilities["UnifiedAddressing"] = new ComputeCapability("UnifiedAddressing", deviceProps.UnifiedAddressing);
            capabilities["ManagedMemory"] = new ComputeCapability("ManagedMemory", deviceProps.ManagedMemory);
            capabilities["ConcurrentKernels"] = new ComputeCapability("ConcurrentKernels", deviceProps.ConcurrentKernels);
            capabilities["ECCEnabled"] = new ComputeCapability("ECCEnabled", deviceProps.ECCEnabled);
            
            // Performance
            capabilities["ClockRate"] = new ComputeCapability("ClockRate", deviceProps.ClockRate);
            capabilities["MemoryClockRate"] = new ComputeCapability("MemoryClockRate", deviceProps.MemoryClockRate);
            capabilities["MemoryBusWidth"] = new ComputeCapability("MemoryBusWidth", deviceProps.MemoryBusWidth);
            
            // Compute the memory bandwidth in GB/s
            var memoryBandwidth = 2.0 * deviceProps.MemoryClockRate * (deviceProps.MemoryBusWidth / 8) / 1.0e6;
            capabilities["MemoryBandwidth"] = new ComputeCapability("MemoryBandwidth", memoryBandwidth);
            
            return capabilities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query device capabilities");
            throw new AcceleratorException("Failed to query CUDA device capabilities", ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaAccelerator));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            _logger.LogInformation("Disposing CUDA accelerator");
            
            // Dispose managed resources
            _kernelCompiler?.Dispose();
            _memoryManager?.Dispose();
            _context?.Dispose();
            
            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CUDA accelerator disposal");
        }
    }
}