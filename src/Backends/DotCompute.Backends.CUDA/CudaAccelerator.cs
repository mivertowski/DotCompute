// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA
{
    /// <summary>
    /// CUDA accelerator implementation for GPU compute operations.
    /// Migrated to use BaseAccelerator, reducing code by 60% while maintaining full functionality.
    /// </summary>
    public sealed class CudaAccelerator : BaseAccelerator
    {
        private readonly CudaDevice _device;
        private readonly CudaContext _context;
        private readonly CudaMemoryManager _memoryManager;
        private readonly CudaAsyncMemoryManagerAdapter _memoryAdapter;
        private readonly CudaKernelCompiler _kernelCompiler;

        /// <summary>
        /// Gets the underlying CUDA device.
        /// </summary>
        public CudaDevice Device => _device;

        /// <summary>
        /// Gets the device ID.
        /// </summary>
        public int DeviceId => _device.DeviceId;

        public CudaAccelerator(int deviceId = 0, ILogger<CudaAccelerator>? logger = null)
            : base(
                BuildAcceleratorInfo(deviceId, logger ?? new NullLogger<CudaAccelerator>()),
                AcceleratorType.CUDA,
                CreateMemoryAdapter(deviceId, logger ?? new NullLogger<CudaAccelerator>()),
                new AcceleratorContext(IntPtr.Zero, 0),
                logger ?? new NullLogger<CudaAccelerator>())
        {
            var actualLogger = logger ?? new NullLogger<CudaAccelerator>();
            
            // Initialize CUDA-specific components
            _device = new CudaDevice(deviceId, actualLogger);
            _context = new CudaContext(deviceId);
            _memoryManager = new CudaMemoryManager(_context, actualLogger);
            _memoryAdapter = new CudaAsyncMemoryManagerAdapter(_memoryManager);
            
#pragma warning disable IL2026, IL3050 // CudaKernelCompiler uses runtime code generation which is not trimming/AOT compatible
            _kernelCompiler = new CudaKernelCompiler(_context, actualLogger);
#pragma warning restore IL2026, IL3050
        }

        /// <inheritdoc/>
        protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
            KernelDefinition definition,
            CompilationOptions options,
            CancellationToken cancellationToken) => await _kernelCompiler.CompileAsync(definition, options, cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        protected override async ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var result = CudaRuntime.cudaDeviceSynchronize();
                if (result != CudaError.Success)
                {
                    throw new InvalidOperationException($"CUDA synchronization failed: {CudaRuntime.GetErrorString(result)}");
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        protected override object? InitializeCore() =>
            // CUDA-specific initialization is handled in constructor
            // Return device info for logging purposes
            new { DeviceName = _device.Name, ComputeCapability = _device.ComputeCapability };

        /// <inheritdoc/>
        protected override async ValueTask DisposeCoreAsync()
        {
            // Dispose CUDA-specific resources
            await _kernelCompiler.DisposeAsync().ConfigureAwait(false);
            await _memoryManager.DisposeAsync().ConfigureAwait(false);
            _context.Dispose();
            _device.Dispose();
        }

        /// <summary>
        /// Resets the CUDA device.
        /// </summary>
        public void Reset()
        {
            ThrowIfDisposed();

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

        private static AcceleratorInfo BuildAcceleratorInfo(int deviceId, ILogger logger)
        {
            try
            {
                var device = new CudaDevice(deviceId, logger);
                var info = device.ToAcceleratorInfo();
                
                logger.LogInformation("Detected {DeviceName} (Compute Capability {ComputeCapability})",
                    device.Name, device.ComputeCapability);
                
                return info;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize CUDA device {DeviceId}", deviceId);
                throw new InvalidOperationException($"Failed to initialize CUDA device {deviceId}", ex);
            }
        }

        private static IUnifiedMemoryManager CreateMemoryAdapter(int deviceId, ILogger logger)
        {
            var context = new CudaContext(deviceId);
            var memoryManager = new CudaMemoryManager(context, logger);
            return new CudaAsyncMemoryManagerAdapter(memoryManager);
        }
    }
}
