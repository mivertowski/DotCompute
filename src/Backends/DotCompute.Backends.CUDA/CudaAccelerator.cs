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

namespace DotCompute.Backends.CUDA
{
    /// <summary>
    /// CUDA accelerator implementation providing high-performance GPU compute operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The CUDA accelerator is the primary interface for executing compute kernels on NVIDIA GPUs.
    /// It provides automatic kernel compilation, memory management, and execution optimization.
    /// </para>
    /// <para>
    /// <strong>Supported GPU Architectures:</strong>
    /// <list type="bullet">
    /// <item>Compute Capability 5.0+ (Maxwell through Ada Lovelace/Hopper)</item>
    /// <item>RTX 2000 Ada Generation (Compute Capability 8.9) with optimizations</item>
    /// <item>Automatic detection and optimization for specific GPU models</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Key Features:</strong>
    /// <list type="bullet">
    /// <item>Zero-copy unified memory support</item>
    /// <item>CUDA graph execution for optimized kernel launches (CC 10.0+)</item>
    /// <item>Automatic kernel compilation with NVRTC</item>
    /// <item>Device memory pooling for reduced allocation overhead</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create accelerator for default GPU device
    /// using var accelerator = new CudaAccelerator();
    ///
    /// // Get detailed device information
    /// var deviceInfo = accelerator.GetDeviceInfo();
    /// Console.WriteLine($"GPU: {deviceInfo.Name}, CC: {deviceInfo.ComputeCapability}");
    ///
    /// // Compile and execute kernel
    /// var kernel = await accelerator.CompileKernelAsync(kernelDef, options);
    /// await accelerator.ExecuteKernelAsync(kernel, args);
    /// await accelerator.SynchronizeAsync();
    /// </code>
    /// </example>
    public sealed partial class CudaAccelerator : BaseAccelerator
    {
        #region LoggerMessage Delegates

        [LoggerMessage(
            EventId = 6500,
            Level = LogLevel.Information,
            Message = "Detected {DeviceName} (Compute Capability {ComputeCapability})")]
        private static partial void LogDeviceDetected(ILogger logger, string deviceName, string computeCapability);

        [LoggerMessage(
            EventId = 6501,
            Level = LogLevel.Error,
            Message = "Failed to initialize CUDA device {DeviceId}")]
        private static partial void LogDeviceInitFailed(ILogger logger, Exception exception, int deviceId);

        #endregion
        private readonly CudaDevice _device;
        private readonly CudaContext _context;
        private readonly CudaMemoryManager _memoryManager;
        private readonly CudaKernelCompiler _kernelCompiler;
        private readonly CudaGraphManager? _graphManager;

        /// <summary>
        /// Gets the underlying CUDA device providing hardware information and capabilities.
        /// </summary>
        /// <value>The CUDA device instance for this accelerator.</value>
        public CudaDevice Device => _device;

        /// <summary>
        /// Gets the CUDA device identifier used for multi-GPU scenarios.
        /// </summary>
        /// <value>Zero-based device index (0 = first GPU, 1 = second GPU, etc.).</value>
        public int DeviceId => _device.DeviceId;


        /// <summary>
        /// Gets the CUDA execution context managing device state and streams.
        /// </summary>
        /// <value>Internal CUDA context for device operations.</value>
        internal CudaContext CudaContext => _context;


        /// <summary>
        /// Gets the CUDA graph manager for optimized repeated kernel execution patterns.
        /// </summary>
        /// <value>
        /// Graph manager instance if supported (Compute Capability 10.0+), otherwise <c>null</c>.
        /// CUDA graphs capture kernel launch sequences for faster replay with reduced CPU overhead.
        /// </value>
        /// <remarks>
        /// Available only on GPUs with compute capability 10.0 or higher (Hopper architecture and newer).
        /// Use graphs for repetitive execution patterns to reduce launch overhead by up to 50%.
        /// </remarks>
        public CudaGraphManager? GraphManager => _graphManager;

        /// <summary>
        /// Initializes a new CUDA accelerator instance for GPU compute operations.
        /// </summary>
        /// <param name="deviceId">
        /// Zero-based index of the CUDA device to use (default: 0 for first GPU).
        /// Use <see cref="CudaRuntime.cudaGetDeviceCount"/> to enumerate available devices.
        /// </param>
        /// <param name="logger">
        /// Optional logger for diagnostics and performance monitoring.
        /// If <c>null</c>, a null logger is used (no logging output).
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when CUDA initialization fails, device doesn't exist, or driver is incompatible.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The accelerator automatically:
        /// <list type="bullet">
        /// <item>Detects GPU compute capability and optimizes accordingly</item>
        /// <item>Initializes memory pools for efficient allocation</item>
        /// <item>Creates CUDA graph manager if CC 10.0+ is detected</item>
        /// <item>Sets up NVRTC compiler for runtime kernel compilation</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Multi-GPU Systems:</strong><br/>
        /// Create separate accelerator instances for each GPU device to enable parallel execution.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Use default GPU (device 0)
        /// using var gpu0 = new CudaAccelerator();
        ///
        /// // Use second GPU with logging
        /// var logger = loggerFactory.CreateLogger&lt;CudaAccelerator&gt;();
        /// using var gpu1 = new CudaAccelerator(deviceId: 1, logger: logger);
        /// </code>
        /// </example>

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

            // Set accelerator reference in memory manager
            InitializeMemoryManagerReference();
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
        /// Resets the CUDA device to a clean state, clearing all memory allocations and reinitializing the context.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation:
        /// <list type="bullet">
        /// <item>Frees all device memory allocations managed by this accelerator</item>
        /// <item>Destroys all CUDA contexts on this device</item>
        /// <item>Resets the device to its initial state</item>
        /// <item>Reinitializes the accelerator context</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>Warning:</strong> This is a heavyweight operation that affects all CUDA contexts
        /// on the device, not just this accelerator instance. Use sparingly, primarily for:
        /// <list type="bullet">
        /// <item>Recovering from device errors</item>
        /// <item>Cleaning up after memory leaks during development</item>
        /// <item>Benchmarking scenarios requiring pristine device state</item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the accelerator has been disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if device reset fails due to driver error or pending operations.
        /// </exception>
        /// <example>
        /// <code>
        /// // Reset device after encountering errors
        /// try
        /// {
        ///     await accelerator.ExecuteKernelAsync(kernel, args);
        /// }
        /// catch (CudaException ex) when (ex.Error == CudaError.IllegalAddress)
        /// {
        ///     accelerator.Reset(); // Clean slate
        ///     // Retry operation...
        /// }
        /// </code>
        /// </example>
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
        /// Retrieves comprehensive device information including hardware specifications and capabilities.
        /// </summary>
        /// <returns>
        /// Detailed device information including compute capability, memory configuration,
        /// multiprocessor count, and architecture-specific features like RTX Ada detection.
        /// </returns>
        /// <exception cref="ObjectDisposedException">Thrown if the accelerator has been disposed.</exception>
        /// <remarks>
        /// <para>
        /// The returned <see cref="DeviceInfo"/> includes:
        /// <list type="bullet">
        /// <item><strong>Hardware:</strong> GPU name, compute capability, SM count, CUDA core estimate</item>
        /// <item><strong>Memory:</strong> Total/available memory, bandwidth, L2 cache size</item>
        /// <item><strong>Capabilities:</strong> Unified memory, concurrent kernels, ECC support</item>
        /// <item><strong>Limits:</strong> Max threads per block, shared memory, warp size</item>
        /// <item><strong>Architecture:</strong> Generation detection (Ampere, Ada, Hopper, etc.)</item>
        /// </list>
        /// </para>
        /// <para>
        /// <strong>RTX 2000 Ada Detection:</strong><br/>
        /// The <c>IsRTX2000Ada</c> property specifically identifies the RTX 2000 Ada Generation
        /// (Compute Capability 8.9) for architecture-specific optimizations.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var info = accelerator.GetDeviceInfo();
        /// Console.WriteLine($"GPU: {info.Name}");
        /// Console.WriteLine($"Compute Capability: {info.ComputeCapability}");
        /// Console.WriteLine($"Memory: {info.GlobalMemoryBytes / (1024 * 1024 * 1024)} GB");
        /// Console.WriteLine($"CUDA Cores: ~{info.EstimatedCudaCores}");
        /// Console.WriteLine($"Memory Bandwidth: {info.MemoryBandwidthGBps:F1} GB/s");
        ///
        /// if (info.IsRTX2000Ada)
        /// {
        ///     Console.WriteLine("Detected RTX 2000 Ada - enabling Ada optimizations");
        /// }
        /// </code>
        /// </example>
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


                LogDeviceDetected(logger, device.Name, device.ComputeCapability);


                return info;
            }
            catch (Exception ex)
            {
                LogDeviceInitFailed(logger, ex, deviceId);
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

        /// <summary>
        /// Sets the accelerator reference in the memory manager after initialization.
        /// </summary>
        private void InitializeMemoryManagerReference()
        {
            if (Memory is CudaAsyncMemoryManagerAdapter adapter)
            {
                adapter.SetAccelerator(this);
            }
        }
    }
}
