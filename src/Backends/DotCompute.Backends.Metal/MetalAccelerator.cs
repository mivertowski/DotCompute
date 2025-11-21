// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Performance;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.Metal.Execution;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Telemetry;
using DotCompute.Backends.Metal.Utilities;
using DotCompute.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Accelerators;

/// <summary>
/// Metal-based compute accelerator for macOS and iOS devices.
/// Migrated to use BaseAccelerator, reducing code by 65% while maintaining full functionality.
/// </summary>
public sealed partial class MetalAccelerator : BaseAccelerator
{
    private readonly MetalAcceleratorOptions _options;
    private readonly MetalKernelCompiler _kernelCompiler;
    private readonly MetalCommandBufferPool _commandBufferPool;
    private readonly MetalCommandQueuePool _commandQueuePool;
    private readonly MetalPerformanceProfiler _profiler;
    private readonly MetalTelemetryManager? _telemetryManager;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly Timer? _cleanupTimer;
    private readonly ILogger _logger; // Store logger reference to avoid reflection (Native AOT compatible)

    /// <summary>
    /// Gets the native Metal device handle for this accelerator.
    /// </summary>
    public IntPtr Device => _device;

    public MetalAccelerator(
        IOptions<MetalAcceleratorOptions> options,
        ILogger<MetalAccelerator> logger,
        IOptions<MetalTelemetryOptions>? telemetryOptions = null,
        ILoggerFactory? loggerFactory = null)
        : base(
            BuildAcceleratorInfo(options.Value, logger),
            AcceleratorType.Metal,
            CreateMemoryManager(options.Value),
            new AcceleratorContext(IntPtr.Zero, 0),
            logger)
    {
        _options = options.Value;
        _logger = logger; // Store logger reference for disposal

        // Initialize Metal device
        _device = MetalNative.CreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device. Metal may not be available on this system.");
        }

        // Create command queue
        _commandQueue = MetalNative.CreateCommandQueue(_device);
        if (_commandQueue == IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
            throw new InvalidOperationException("Failed to create Metal command queue.");
        }

        // Initialize command buffer pool
        var poolLogger = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalCommandBufferPool>();
        _commandBufferPool = new MetalCommandBufferPool(_commandQueue, poolLogger, _options.CommandBufferCacheSize);

        // Initialize command queue pool for thread-safe parallel kernel execution
        var queuePoolLogger = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalCommandQueuePool>();
        _commandQueuePool = new MetalCommandQueuePool(_device, queuePoolLogger, maxConcurrency: null);

        // Initialize performance profiler
        var profilerLogger = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalPerformanceProfiler>();
        _profiler = new MetalPerformanceProfiler(profilerLogger);

        // Initialize kernel compiler with command queue pool for parallel execution
        _kernelCompiler = new MetalKernelCompiler(_device, _commandQueuePool, logger, _commandBufferPool);

        // Initialize production telemetry if enabled
        if (telemetryOptions?.Value != null && loggerFactory != null)
        {
            var telemetryLogger = loggerFactory.CreateLogger<MetalTelemetryManager>();
            _telemetryManager = new MetalTelemetryManager(telemetryOptions, telemetryLogger, loggerFactory);
            logger.LogInformation("Metal telemetry system initialized for production monitoring");
        }

        // Set the accelerator reference in the memory manager after construction
        if (Memory is MetalMemoryManager metalMemoryManager)
        {
            metalMemoryManager.SetAcceleratorReference(this);
        }

        // Setup periodic cleanup timer (every 30 seconds)
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <inheritdoc/>
    protected override async ValueTask<ICompiledKernel> CompileKernelCoreAsync(
        KernelDefinition definition,
        CompilationOptions options,
        CancellationToken cancellationToken)
    {
        using var profiling = _profiler.Profile($"CompileKernel:{definition.Name}");
        var startTime = DateTimeOffset.UtcNow;
        Exception? compilationException = null;

        try
        {
            // Compile kernel using Metal Shading Language
            var result = await _kernelCompiler.CompileAsync(definition, options, cancellationToken).ConfigureAwait(false);

            // Record telemetry for successful compilation

            var duration = DateTimeOffset.UtcNow - startTime;
            _telemetryManager?.RecordKernelExecution(
                definition.Name,
                duration,

                definition.Code?.Length ?? 0,

                true,
                new Dictionary<string, object>
                {
                    ["operation"] = "kernel_compilation",
                    ["compilation_options"] = options.ToString(),
                    ["code_length"] = definition.Code?.Length ?? 0
                });


            return result;
        }
        catch (Exception ex)
        {
            compilationException = ex;

            // Record telemetry for failed compilation

            var duration = DateTimeOffset.UtcNow - startTime;
            _telemetryManager?.RecordKernelExecution(
                definition.Name,
                duration,

                definition.Code?.Length ?? 0,

                false,
                new Dictionary<string, object>
                {
                    ["operation"] = "kernel_compilation",
                    ["compilation_options"] = options.ToString(),
                    ["code_length"] = definition.Code?.Length ?? 0,
                    ["error"] = ex.Message
                });


            _telemetryManager?.RecordErrorEvent(
                MetalError.CompilationError,
                $"kernel_compilation_{definition.Name}",
                new Dictionary<string, object>
                {
                    ["kernel_name"] = definition.Name,
                    ["exception_type"] = ex.GetType().Name,
                    ["exception_message"] = ex.Message
                });


            throw;
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask SynchronizeCoreAsync(CancellationToken cancellationToken)
    {
        using var profiling = _profiler.Profile("Synchronize");
        var startTime = DateTimeOffset.UtcNow;
        var success = false;

        // Get a command buffer from the pool
        var commandBuffer = _commandBufferPool.GetCommandBuffer();

        try
        {
            // Add a completion handler
            var tcs = new TaskCompletionSource<bool>();
            MetalNative.SetCommandBufferCompletionHandler(commandBuffer, (status) =>
            {
                if (status == MetalCommandBufferStatus.Completed)
                {
                    _ = tcs.TrySetResult(true);
                }
                else
                {
                    _ = tcs.TrySetException(new InvalidOperationException($"Command buffer failed with status: {status}"));
                }
            });

            // Commit the command buffer
            MetalNative.CommitCommandBuffer(commandBuffer);

            // Wait for completion
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                _ = await tcs.Task.ConfigureAwait(false);
                success = true;
            }
        }
        catch (Exception ex)
        {
            // Record telemetry for failed synchronization
            var duration = DateTimeOffset.UtcNow - startTime;
            _telemetryManager?.RecordErrorEvent(
                MetalError.InvalidOperation,
                "synchronization_failure",
                new Dictionary<string, object>
                {
                    ["duration_ms"] = duration.TotalMilliseconds,
                    ["exception_type"] = ex.GetType().Name,
                    ["exception_message"] = ex.Message
                });
            throw;
        }
        finally
        {
            _commandBufferPool.ReturnCommandBuffer(commandBuffer);

            // Record telemetry for synchronization operation

            if (success)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                _telemetryManager?.RecordKernelExecution("synchronize", duration, 0, success);
            }
        }
    }

    /// <summary>
    /// Executes a compiled Metal kernel with explicit grid and thread group dimensions.
    /// </summary>
    /// <param name="kernel">The compiled Metal kernel to execute.</param>
    /// <param name="gridDim">The grid dimensions (number of thread groups to dispatch).</param>
    /// <param name="blockDim">The thread group dimensions (threads per thread group).</param>
    /// <param name="buffers">Buffer parameters to bind to the kernel.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A task representing the asynchronous execution operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when kernel or buffers is null.</exception>
    /// <exception cref="ArgumentException">Thrown when kernel is not a Metal kernel or dimensions are invalid.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the accelerator has been disposed.</exception>
    /// <remarks>
    /// <para>
    /// This method provides explicit control over kernel launch dimensions for advanced use cases
    /// like integration testing or performance optimization.
    /// </para>
    /// <para>
    /// Grid dimensions specify how many thread groups to launch. Block dimensions specify
    /// threads per thread group. Total threads = gridDim × blockDim.
    /// </para>
    /// <para>
    /// Example: gridDim=(10, 1, 1), blockDim=(256, 1, 1) launches 2,560 total threads.
    /// </para>
    /// </remarks>
    public async Task ExecuteKernelAsync(
        ICompiledKernel kernel,
        GridDimensions gridDim,
        GridDimensions blockDim,
        IUnifiedMemoryBuffer[] buffers,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(kernel, nameof(kernel));
        ArgumentNullException.ThrowIfNull(buffers, nameof(buffers));

        // Ensure kernel is a Metal kernel
        if (kernel is not MetalCompiledKernel metalKernel)
        {
            throw new ArgumentException(
                $"Kernel must be a Metal compiled kernel. Got: {kernel.GetType().Name}",
                nameof(kernel));
        }

        // Create execution engine (lightweight, can be created per execution)
        using var executionEngine = new MetalExecutionEngine(_device, _commandQueue);

        // Execute the kernel with explicit dimensions
        await executionEngine.ExecuteAsync(metalKernel, gridDim, blockDim, buffers, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a compiled Metal kernel with explicit grid and thread group dimensions (convenience overload).
    /// </summary>
    /// <param name="kernel">The compiled Metal kernel to execute.</param>
    /// <param name="gridDim">The grid dimensions (number of thread groups to dispatch).</param>
    /// <param name="blockDim">The thread group dimensions (threads per thread group).</param>
    /// <param name="buffers">Buffer parameters to bind to the kernel.</param>
    /// <returns>A task representing the asynchronous execution operation.</returns>
    /// <remarks>
    /// This is a convenience overload that accepts params buffers for easier calling.
    /// </remarks>
    public Task ExecuteKernelAsync(
        ICompiledKernel kernel,
        GridDimensions gridDim,
        GridDimensions blockDim,
        params IUnifiedMemoryBuffer[] buffers)
    {
        return ExecuteKernelAsync(kernel, gridDim, blockDim, buffers, CancellationToken.None);
    }

    /// <summary>
    /// Partial method for disposing barrier provider resources.
    /// </summary>
    partial void DisposeBarrierProvider();

    /// <summary>
    /// Partial method for disposing memory ordering provider resources.
    /// </summary>
    partial void DisposeMemoryOrderingProvider();

    /// <inheritdoc/>
    protected override async ValueTask DisposeCoreAsync()
    {
        // Dispose cleanup timer using async disposal pattern
        if (_cleanupTimer != null)
        {
            await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
        }

        // Generate final telemetry report if telemetry is enabled
        if (_telemetryManager != null)
        {
            try
            {
                var finalReport = _telemetryManager.GenerateProductionReport();
                _logger.LogInformation("Final Metal telemetry report - Operations: {Operations}, Errors: {Errors}, Health: {Health}",
                    finalReport.Snapshot.TotalOperations, finalReport.Snapshot.TotalErrors, finalReport.Snapshot.HealthStatus);
            }
            catch (Exception ex)
            {
                // Suppress telemetry errors during disposal
                _logger.LogWarning(ex, "Error generating final telemetry report during disposal");
            }
        }

        // Dispose managed resources
        _kernelCompiler.Dispose();
        _commandBufferPool.Dispose();
        _commandQueuePool.Dispose();
        _profiler.Dispose();
        _telemetryManager?.Dispose();
        DisposeBarrierProvider();
        DisposeMemoryOrderingProvider();

        // Release native resources
        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        // Call base class disposal
        await base.DisposeCoreAsync().ConfigureAwait(false);
    }

    private void PerformCleanup(object? state)
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            // Clean up command buffer pool
            _commandBufferPool.Cleanup();

            // Log statistics periodically
            var stats = _commandBufferPool.GetStats();
            var logger = (ILogger)typeof(BaseAccelerator).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(this)!;
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Command buffer pool stats - Available: {Available}, Active: {Active}, Utilization: {Utilization:F1}%",
                    stats.AvailableBuffers, stats.ActiveBuffers, stats.Utilization);
            }
        }
        catch
        {
            // Suppress errors during cleanup
        }
    }

    /// <summary>
    /// Gets performance metrics for this accelerator.
    /// </summary>
    public Dictionary<string, PerformanceMetrics> GetPerformanceMetrics()
    {
        ThrowIfDisposed();
        var metalMetrics = _profiler.GetAllMetrics();
        return metalMetrics.ToDictionary(
            kvp => kvp.Key,
            kvp => new PerformanceMetrics
            {
                AverageTimeMs = kvp.Value.AverageTime.TotalMilliseconds,
                MinTimeMs = kvp.Value.MinTime == TimeSpan.MaxValue ? 0 : kvp.Value.MinTime.TotalMilliseconds,
                MaxTimeMs = kvp.Value.MaxTime.TotalMilliseconds,
                TotalExecutionTimeMs = (long)kvp.Value.TotalTime.TotalMilliseconds,
                StandardDeviation = Math.Sqrt(kvp.Value.TimeVariance)
            }
        );
    }

    /// <summary>
    /// Generates a performance report for this accelerator.
    /// </summary>
    public string GeneratePerformanceReport()
    {
        ThrowIfDisposed();
        return _profiler.GenerateReport();
    }

    /// <summary>
    /// Resets performance metrics for this accelerator.
    /// </summary>
    public void ResetPerformanceMetrics()
    {
        ThrowIfDisposed();
        _profiler.Reset();
    }

    /// <summary>
    /// Gets comprehensive production telemetry report (if telemetry is enabled).
    /// </summary>
    public MetalProductionReport? GetTelemetryReport()
    {
        ThrowIfDisposed();
        return _telemetryManager?.GenerateProductionReport();
    }

    /// <summary>
    /// Gets current telemetry snapshot (if telemetry is enabled).
    /// </summary>
    public MetalTelemetrySnapshot? GetTelemetrySnapshot()
    {
        ThrowIfDisposed();
        return _telemetryManager?.GetCurrentSnapshot();
    }

    /// <summary>
    /// Exports metrics to configured monitoring systems (if telemetry is enabled).
    /// </summary>
    public async Task ExportTelemetryAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (_telemetryManager != null)
        {
            await _telemetryManager.ExportMetricsAsync(cancellationToken);
        }
    }

    private static AcceleratorInfo BuildAcceleratorInfo(MetalAcceleratorOptions options, ILogger logger)
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device for info query.");
        }

        try
        {
            var deviceInfo = MetalNative.GetDeviceInfo(device);
            var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown Metal Device";
            var familiesString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";

            var capabilities = new Dictionary<string, object>
            {
                ["SupportsFamily"] = familiesString,
                ["MaxThreadgroupSize"] = deviceInfo.MaxThreadgroupSize,
                ["MaxThreadsPerThreadgroup"] = (int)deviceInfo.MaxThreadsPerThreadgroup,
                ["MaxThreadExecutionWidth"] = GetMaxThreadExecutionWidthStatic(familiesString),
                ["MaxBufferLength"] = deviceInfo.MaxBufferLength,
                ["UnifiedMemory"] = deviceInfo.HasUnifiedMemory,
                ["RegistryID"] = deviceInfo.RegistryID,
                ["Location"] = GetDeviceLocation(deviceInfo),
                ["RecommendedMaxWorkingSetSize"] = deviceInfo.RecommendedMaxWorkingSetSize,
                ["LanguageVersion"] = GetMetalLanguageVersion(familiesString),
                ["IsLowPower"] = deviceInfo.IsLowPower,
                ["IsRemovable"] = deviceInfo.IsRemovable,
                ["LocationNumber"] = deviceInfo.LocationNumber
            };

            return new AcceleratorInfo(
                type: AcceleratorType.Metal,
                name: deviceName,
                driverVersion: "1.0",
                memorySize: deviceInfo.HasUnifiedMemory
                    ? (long)deviceInfo.RecommendedMaxWorkingSetSize
                    : (long)deviceInfo.MaxBufferLength,
                computeUnits: EstimateComputeUnits(deviceInfo, familiesString),
                maxClockFrequency: 0, // Metal doesn't expose clock frequency
                computeCapability: GetComputeCapability(deviceInfo, familiesString),
                maxSharedMemoryPerBlock: EstimateSharedMemory(deviceInfo),
                isUnifiedMemory: deviceInfo.HasUnifiedMemory
            )
            {
                Capabilities = capabilities
            };
        }
        finally
        {
            MetalNative.ReleaseDevice(device);
        }
    }

    private static IUnifiedMemoryManager CreateMemoryManager(MetalAcceleratorOptions options)
    {
        var device = MetalNative.CreateSystemDefaultDevice();
        if (device == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create Metal device for memory manager.");
        }

        // MetalMemoryManager created without accelerator reference initially
        var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<MetalMemoryManager>();
        return new MetalMemoryManager(logger, null);
    }

    private static string GetDeviceLocation(MetalDeviceInfo info)
    {
        if (info.Location == MetalDeviceLocation.BuiltIn)
        {

            return "Built-in";
        }


        if (info.Location == MetalDeviceLocation.Slot)
        {

            return $"Slot {info.LocationNumber}";
        }


        if (info.Location == MetalDeviceLocation.External)
        {

            return "External";
        }


        return "Unknown";
    }

    private static Version GetComputeCapability(MetalDeviceInfo info, string families)
    {
        // Map Metal GPU families to compute capability versions
        if (families.Contains("Apple8", StringComparison.Ordinal))
        {

            return new Version(8, 0); // M2 family
        }


        if (families.Contains("Apple7", StringComparison.Ordinal))
        {

            return new Version(7, 0); // M1 family
        }


        if (families.Contains("Apple6", StringComparison.Ordinal))
        {

            return new Version(6, 0); // A14 family
        }


        if (families.Contains("Apple5", StringComparison.Ordinal))
        {

            return new Version(5, 0); // A13 family
        }


        if (families.Contains("Apple4", StringComparison.Ordinal))
        {

            return new Version(4, 0); // A12 family
        }


        if (families.Contains("Apple", StringComparison.Ordinal))
        {

            return new Version(3, 0); // Generic Apple Silicon
        }


        if (families.Contains("Mac2", StringComparison.Ordinal))
        {

            return new Version(2, 0); // Modern Intel Mac GPUs
        }


        if (families.Contains("Mac1", StringComparison.Ordinal))
        {

            return new Version(1, 1); // Older Intel Mac GPUs
        }


        if (families.Contains("Common3", StringComparison.Ordinal))
        {

            return new Version(3, 0);
        }


        if (families.Contains("Common2", StringComparison.Ordinal))
        {

            return new Version(2, 0);
        }


        if (families.Contains("Common1", StringComparison.Ordinal))
        {

            return new Version(1, 0);
        }


        return new Version(1, 0); // Default/unknown
    }

    private static int EstimateComputeUnits(MetalDeviceInfo info, string families)
    {
        var maxThreads = (int)info.MaxThreadgroupSize;

        // Apple Silicon typically has more compute units
        if (families.Contains("Apple", StringComparison.Ordinal))
        {
            if (families.Contains("Apple8", StringComparison.Ordinal))
            {
                return 20; // M2 Max/Ultra
            }


            if (families.Contains("Apple7", StringComparison.Ordinal))
            {
                return 16; // M1 Max/Ultra
            }


            if (families.Contains("Apple6", StringComparison.Ordinal))
            {
                return 8;  // M1 Pro
            }


            if (families.Contains("Apple5", StringComparison.Ordinal))
            {
                return 8;  // M1
            }


            if (families.Contains("Apple4", StringComparison.Ordinal))
            {

                return 6;  // A12/A13
            }


            return 4; // Older Apple Silicon
        }

        // Intel Mac GPUs
        if (families.Contains("Mac", StringComparison.Ordinal))
        {

            return Math.Max(4, maxThreads / 256);
        }

        // Estimate based on max threads per threadgroup

        return Math.Max(1, maxThreads / 64);
    }

    private static long EstimateSharedMemory(MetalDeviceInfo info)
    {
        var maxThreads = (long)info.MaxThreadgroupSize;

        // Apple Silicon typically has more shared memory per threadgroup
        if (info.HasUnifiedMemory)
        {

            return Math.Min(32 * 1024, maxThreads * 32); // 32KB shared memory
        }
        else
        {

            return Math.Min(16 * 1024, maxThreads * 16); // 16KB shared memory
        }
    }

    private static string GetMetalLanguageVersion(string families)
    {
        // Map GPU families to Metal Shading Language versions
        // Reference: https://developer.apple.com/metal/Metal-Feature-Set-Tables.pdf
        if (families.Contains("Apple8", StringComparison.Ordinal) || families.Contains("Apple9", StringComparison.Ordinal))
        {
            return "3.1"; // M2+ family supports Metal 3.1
        }

        if (families.Contains("Apple7", StringComparison.Ordinal))
        {
            return "3.0"; // M1 family supports Metal 3.0
        }

        if (families.Contains("Apple6", StringComparison.Ordinal))
        {
            return "2.4"; // A14 family supports Metal 2.4
        }

        if (families.Contains("Apple5", StringComparison.Ordinal))
        {
            return "2.3"; // A13 family supports Metal 2.3
        }

        if (families.Contains("Apple4", StringComparison.Ordinal))
        {
            return "2.2"; // A12 family supports Metal 2.2
        }

        if (families.Contains("Apple", StringComparison.Ordinal))
        {
            return "2.0"; // Generic Apple Silicon supports at least Metal 2.0
        }

        if (families.Contains("Mac2", StringComparison.Ordinal))
        {
            return "2.1"; // Modern Intel Mac GPUs support Metal 2.1+
        }

        if (families.Contains("Mac1", StringComparison.Ordinal))
        {
            return "2.0"; // Older Intel Mac GPUs support Metal 2.0
        }

        return "1.2"; // Fallback to conservative version
    }

    private static int GetMaxThreadExecutionWidthStatic(string families)
    {
        // Apple GPUs have a SIMD width of 32 for most architectures
        // Reference: https://developer.apple.com/documentation/metal/mtldevice/1433420-maxthreadexecutionwidth
        if (families.Contains("Apple", StringComparison.Ordinal))
        {
            return 32; // Apple Silicon GPUs have 32-wide SIMD
        }

        // Intel and AMD GPUs on Mac typically have different SIMD widths
        if (families.Contains("Mac", StringComparison.Ordinal))
        {
            return 16; // Conservative estimate for Intel/AMD Mac GPUs
        }

        return 32; // Default to 32 for modern GPUs
    }
}

/// <summary>
/// Configuration options for the Metal accelerator.
/// </summary>
public class MetalAcceleratorOptions
{
    /// <summary>
    /// Gets or sets the maximum memory allocation size.
    /// Default is 4GB.
    /// </summary>
    public long MaxMemoryAllocation { get; set; } = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum threadgroup size.
    /// Default is 1024.
    /// </summary>
    public int MaxThreadgroupSize { get; set; } = 1024;

    /// <summary>
    /// Gets or sets whether to prefer integrated GPUs.
    /// Default is false (prefer discrete GPUs).
    /// </summary>
    public bool PreferIntegratedGpu { get; set; }

    /// <summary>
    /// Gets or sets whether to enable Metal Performance Shaders.
    /// Default is true.
    /// </summary>
    public bool EnableMetalPerformanceShaders { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable GPU family specialization.
    /// Default is true.
    /// </summary>
    public bool EnableGpuFamilySpecialization { get; set; } = true;

    /// <summary>
    /// Gets or sets the command buffer cache size.
    /// Default is 16.
    /// </summary>
    public int CommandBufferCacheSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets whether to enable fast math optimizations.
    /// Fast math trades precision for performance in floating-point operations.
    /// Default is true.
    /// </summary>
    public bool EnableFastMath { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable validation mode.
    /// When enabled, additional validation checks are performed.
    /// Default is true.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable performance metrics collection.
    /// When enabled, detailed performance metrics are tracked.
    /// Default is false (to minimize overhead).
    /// </summary>
    public bool EnableMetrics { get; set; }
}
