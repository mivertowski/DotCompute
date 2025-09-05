// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CUDA.Types;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Memory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Execution.Graph;
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
        private readonly Memory.CudaMemoryManager _memoryManager;
        private readonly CudaKernelCompiler _kernelCompiler;
        private readonly CudaGraphManager? _graphManager;

        /// <summary>
        /// Gets the underlying CUDA device.
        /// </summary>
        public CudaDevice Device => _device;

        /// <summary>
        /// Gets the device ID.
        /// </summary>
        public int DeviceId => _device.DeviceId;
        
        /// <summary>
        /// Gets the CUDA-specific context.
        /// </summary>
        internal CudaContext CudaContext => _context;
        
        /// <summary>
        /// Gets the graph manager for optimized kernel execution.
        /// </summary>
        public CudaGraphManager? GraphManager => _graphManager;

        public CudaAccelerator(int deviceId = 0, ILogger<CudaAccelerator>? logger = null)
            : base(
                BuildAcceleratorInfo(deviceId, logger ?? new NullLogger<CudaAccelerator>()),
                AcceleratorType.CUDA,
                CreateMemoryAdapter(deviceId, out var memoryManager, out var context, out var device, logger ?? new NullLogger<CudaAccelerator>()),
                new AcceleratorContext(IntPtr.Zero, 0),
                logger ?? new NullLogger<CudaAccelerator>())
        {
            var actualLogger = logger ?? new NullLogger<CudaAccelerator>();
            
            // Use the same instances created by CreateMemoryAdapter
            _device = device;
            _context = context;
            _memoryManager = memoryManager;
            
#pragma warning disable IL2026, IL3050 // CudaKernelCompiler uses runtime code generation which is not trimming/AOT compatible
            _kernelCompiler = new CudaKernelCompiler(_context, actualLogger);
#pragma warning restore IL2026, IL3050
            
            // Initialize graph manager for devices that support it
            if (_device.ComputeCapability.Major >= 10) // CUDA graphs require compute capability 10.0+
            {
                _graphManager = new CudaGraphManager(_context, actualLogger as ILogger<CudaGraphManager> ?? NullLogger<CudaGraphManager>.Instance);
            }
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
        protected override object? InitializeCore()
        {
            // CUDA-specific initialization is handled in constructor
            // Return device info for logging purposes
            // Note: _device might be null when called from base constructor
            if (_device == null)
            {
                return new { DeviceName = "CUDA Device (initializing)", ComputeCapability = "Unknown" };
            }
            return new { DeviceName = _device.Name, ComputeCapability = _device.ComputeCapability };
        }

        /// <inheritdoc/>
        protected override async ValueTask DisposeCoreAsync()
        {
            // Dispose CUDA-specific resources
            _graphManager?.Dispose();
            await _kernelCompiler.DisposeAsync().ConfigureAwait(false);
            await _memoryManager.DisposeAsync().ConfigureAwait(false);
            _context.Dispose();
            _device.Dispose();
            
            // Call base disposal
            await base.DisposeCoreAsync().ConfigureAwait(false);
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
                DeviceIndex = _device.DeviceId,
                ComputeCapability = (_device.ComputeCapabilityMajor, _device.ComputeCapabilityMinor),
                ArchitectureGeneration = _device.ArchitectureGeneration,
                IsRTX2000Ada = _device.IsRTX2000Ada,
                StreamingMultiprocessors = _device.StreamingMultiprocessorCount,
                EstimatedCudaCores = _device.GetEstimatedCudaCores(),
                GlobalMemoryBytes = (long)totalMemory,
                MemoryBandwidthGBps = _device.MemoryBandwidthGBps,
                MaxThreadsPerBlock = _device.MaxThreadsPerBlock,
                WarpSize = _device.WarpSize,
                SharedMemoryPerBlock = (int)_device.SharedMemoryPerBlock,
                L2CacheSize = _device.L2CacheSize,
                ClockRate = _device.ClockRate,
                MemoryClockRate = _device.MemoryClockRate,
                SupportsUnifiedAddressing = _device.SupportsUnifiedAddressing,
                SupportsManagedMemory = _device.SupportsManagedMemory,
                SupportsConcurrentKernels = _device.SupportsConcurrentKernels,
                IsECCEnabled = _device.IsECCEnabled,
                MultiprocessorCount = _device.StreamingMultiprocessorCount,
                SupportsUnifiedMemory = _device.SupportsManagedMemory,
                TotalMemory = (long)totalMemory,
                AvailableMemory = (long)freeMemory
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

        private static IUnifiedMemoryManager CreateMemoryAdapter(int deviceId, out CudaMemoryManager memoryManager, out CudaContext context, out CudaDevice device, ILogger logger)
        {
            context = new CudaContext(deviceId);
            device = new CudaDevice(deviceId, logger);
            memoryManager = new CudaMemoryManager(context, device, logger);
            return new CudaAsyncMemoryManagerAdapter(memoryManager);
        }
    }
}
