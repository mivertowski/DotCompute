// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Compilation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA;

/// <summary>
/// CUDA accelerator implementation for GPU compute operations
/// </summary>
public class CudaAccelerator : IAccelerator, IDisposable
{
    private readonly ILogger<CudaAccelerator> _logger;
    private readonly CudaContext _context;
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaAsyncMemoryManagerAdapter _memoryAdapter;
    private readonly CudaKernelCompiler _kernelCompiler;
    private readonly AcceleratorInfo _info;
    private readonly int _deviceId;
    private bool _disposed;

    /// <inheritdoc/>
    public AcceleratorInfo Info => _info;

    /// <inheritdoc/>
    public IMemoryManager Memory => _memoryAdapter;

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
        _memoryAdapter = new CudaAsyncMemoryManagerAdapter(_memoryManager);
            
            // Create kernel compiler
            _kernelCompiler = new CudaKernelCompiler(_context, _logger);
            
            // Build accelerator info
            _info = BuildAcceleratorInfo();
            
            _logger.LogInformation("CUDA accelerator initialized successfully. Device: {DeviceName}",
                _info.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CUDA accelerator");
            throw new InvalidOperationException("Failed to initialize CUDA accelerator", ex);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        _logger.LogDebug("Compiling kernel '{KernelName}' for CUDA device {DeviceId}", definition.Name, _deviceId);

        try
        {
            var compiledKernel = await _kernelCompiler.CompileAsync(definition, options, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Successfully compiled kernel '{KernelName}'", definition.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile kernel '{KernelName}'", definition.Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        try
        {
            _logger.LogTrace("Synchronizing CUDA device {DeviceId}", _deviceId);
            
            // Run synchronization on a background thread to avoid blocking
            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaDeviceSynchronize();
                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"CUDA synchronization failed: {CudaRuntime.GetErrorString(result)}");
                }
            }, cancellationToken).ConfigureAwait(false);
            
            _logger.LogTrace("CUDA device {DeviceId} synchronized", _deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize CUDA device {DeviceId}", _deviceId);
            throw;
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
                throw new InvalidOperationException($"CUDA device reset failed: {CudaRuntime.GetErrorString(result)}");
            }
            
            // Reinitialize context
            _context.Reinitialize();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CUDA device reset");
            throw new InvalidOperationException("Failed to reset CUDA device", ex);
        }
    }

    private AcceleratorInfo BuildAcceleratorInfo()
    {
        try
        {
            var deviceProps = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref deviceProps, _deviceId);
            
            if (result != CudaError.Success)
            {
                throw new InvalidOperationException($"Failed to query device properties: {CudaRuntime.GetErrorString(result)}");
            }
            
            var capabilities = new Dictionary<string, object>
            {
                ["ComputeCapabilityMajor"] = deviceProps.Major,
                ["ComputeCapabilityMinor"] = deviceProps.Minor,
                ["SharedMemoryPerBlock"] = deviceProps.SharedMemPerBlock,
                ["ConstantMemory"] = deviceProps.TotalConstMem,
                ["L2CacheSize"] = deviceProps.L2CacheSize,
                ["MultiprocessorCount"] = deviceProps.MultiProcessorCount,
                ["MaxThreadsPerBlock"] = deviceProps.MaxThreadsPerBlock,
                ["MaxThreadsPerMultiprocessor"] = deviceProps.MaxThreadsPerMultiProcessor,
                ["WarpSize"] = deviceProps.WarpSize,
                ["AsyncEngineCount"] = deviceProps.AsyncEngineCount,
                ["UnifiedAddressing"] = deviceProps.UnifiedAddressing > 0,
                ["ManagedMemory"] = deviceProps.ManagedMemory > 0,
                ["ConcurrentKernels"] = deviceProps.ConcurrentKernels > 0,
                ["ECCEnabled"] = deviceProps.ECCEnabled > 0,
                ["ClockRate"] = deviceProps.ClockRate,
                ["MemoryClockRate"] = deviceProps.MemoryClockRate,
                ["MemoryBusWidth"] = deviceProps.MemoryBusWidth,
                ["MemoryBandwidth"] = 2.0 * deviceProps.MemoryClockRate * (deviceProps.MemoryBusWidth / 8) / 1.0e6
            };
            
            return new AcceleratorInfo(
                type: AcceleratorType.CUDA,
                name: deviceProps.Name,
                driverVersion: $"{deviceProps.Major}.{deviceProps.Minor}",
                memorySize: (long)deviceProps.TotalGlobalMem,
                computeUnits: deviceProps.MultiProcessorCount,
                maxClockFrequency: deviceProps.ClockRate / 1000, // Convert kHz to MHz
                computeCapability: new Version(deviceProps.Major, deviceProps.Minor),
                maxSharedMemoryPerBlock: (long)deviceProps.SharedMemPerBlock,
                isUnifiedMemory: false
            )
            {
                Capabilities = capabilities
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build accelerator info");
            throw new InvalidOperationException("Failed to build CUDA accelerator info", ex);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CudaAccelerator));
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        try
        {
            _logger.LogInformation("Disposing CUDA accelerator");
            
            // Synchronize before disposal
            await SynchronizeAsync().ConfigureAwait(false);
            
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

    /// <summary>
    /// Synchronous dispose method for IDisposable compatibility
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            _logger.LogInformation("Disposing CUDA accelerator");
            
            // Synchronize before disposal
            _context?.Synchronize();
            
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