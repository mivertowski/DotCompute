// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA;

/// <summary>
/// CUDA accelerator implementation for GPU compute operations
/// </summary>
public sealed class CudaAccelerator : IAccelerator, IDisposable
{
    private readonly ILogger<CudaAccelerator> _logger;
    private readonly CudaDevice _device;
    private readonly CudaContext _context;
    private readonly CudaMemoryManager _memoryManager;
    private readonly CudaAsyncMemoryManagerAdapter _memoryAdapter;
    private readonly CudaKernelCompiler _kernelCompiler;
    private readonly AcceleratorInfo _info;
    private bool _disposed;

    /// <inheritdoc/>
    public AcceleratorType Type => AcceleratorType.CUDA;
    
    /// <inheritdoc/>
    public AcceleratorInfo Info => _info;

    /// <inheritdoc/>
    public IMemoryManager Memory => _memoryAdapter;

    /// <inheritdoc/>
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    /// <summary>
    /// Gets the underlying CUDA device.
    /// </summary>
    public CudaDevice Device => _device;

    /// <summary>
    /// Gets the device ID.
    /// </summary>
    public int DeviceId => _device.DeviceId;

    public CudaAccelerator(int deviceId = 0, ILogger<CudaAccelerator>? logger = null)
    {
        _logger = logger ?? new NullLogger<CudaAccelerator>();

        try
        {
            _logger.LogInformation("Initializing CUDA accelerator for device {DeviceId}", deviceId);

            // Create and validate device
            _device = new CudaDevice(deviceId, _logger);
            _logger.LogInformation("Detected {DeviceName} (Compute Capability {ComputeCapability})", 
                _device.Name, _device.ComputeCapability);

            // Initialize CUDA context
            _context = new CudaContext(deviceId);

            // Create memory manager
            _memoryManager = new CudaMemoryManager(_context, _logger);
            _memoryAdapter = new CudaAsyncMemoryManagerAdapter(_memoryManager);

            // Create kernel compiler
#pragma warning disable IL2026, IL3050 // CudaKernelCompiler uses runtime code generation which is not trimming/AOT compatible
            _kernelCompiler = new CudaKernelCompiler(_context, _logger);
#pragma warning restore IL2026, IL3050

            // Build accelerator info from device
            _info = _device.ToAcceleratorInfo();

            _logger.LogInformation("CUDA accelerator initialized successfully. Device: {DeviceName} ({IsRTX2000Ada})",
                _info.Name, _device.IsRTX2000Ada ? "RTX 2000 Ada Generation" : _device.ArchitectureGeneration);
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
        ThrowIfDisposed();

        return await AcceleratorUtilities.CompileKernelWithLoggingAsync(
            definition,
            options,
            _logger,
            "CUDA",
            _kernelCompiler.CompileAsync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await AcceleratorUtilities.SynchronizeWithLoggingAsync(
            _logger,
            "CUDA",
            async (ct) => await Task.Run(() =>
            {
                var result = CudaRuntime.cudaDeviceSynchronize();
                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"CUDA synchronization failed: {CudaRuntime.GetErrorString(result)}");
                }
            }, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Gets detailed device information including RTX 2000 Ada detection.
    /// </summary>
    /// <returns>Device information and capabilities.</returns>
    public DeviceInfo GetDeviceInfo()
    {
        ThrowIfDisposed();

        var (freeMemory, totalMemory) = _device.GetMemoryInfo();

        return new DeviceInfo
        {
            Name = _device.Name,
            DeviceId = _device.DeviceId,
            ComputeCapability = _device.ComputeCapability,
            ArchitectureGeneration = _device.ArchitectureGeneration,
            IsRTX2000Ada = _device.IsRTX2000Ada,
            StreamingMultiprocessors = _device.StreamingMultiprocessorCount,
            EstimatedCudaCores = _device.GetEstimatedCudaCores(),
            TotalMemory = totalMemory,
            AvailableMemory = freeMemory,
            MemoryBandwidthGBps = _device.MemoryBandwidthGBps,
            MaxThreadsPerBlock = _device.MaxThreadsPerBlock,
            WarpSize = _device.WarpSize,
            SharedMemoryPerBlock = _device.SharedMemoryPerBlock,
            L2CacheSize = _device.L2CacheSize,
            ClockRate = _device.ClockRate,
            MemoryClockRate = _device.MemoryClockRate,
            SupportsUnifiedAddressing = _device.SupportsUnifiedAddressing,
            SupportsManagedMemory = _device.SupportsManagedMemory,
            SupportsConcurrentKernels = _device.SupportsConcurrentKernels,
            IsECCEnabled = _device.IsECCEnabled
        };
    }

    private void ThrowIfDisposed()
    {
        AcceleratorUtilities.ThrowIfDisposed(_disposed, this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await AcceleratorUtilities.DisposeWithSynchronizationAsync(
            _logger,
            "CUDA",
            SynchronizeAsync,
            _kernelCompiler, _memoryManager, _context, _device);

        _disposed = true;
    }

    /// <summary>
    /// Synchronous dispose method for IDisposable compatibility
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            AcceleratorUtilities.DisposeWithSynchronization(
                _logger,
                "CUDA",
                () => _context?.Synchronize(),
                _kernelCompiler, _memoryManager, _context, _device);
        }

        _disposed = true;
    }
}
