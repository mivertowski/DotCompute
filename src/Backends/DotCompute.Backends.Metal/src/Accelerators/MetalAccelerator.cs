// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using DotCompute.Backends.Metal.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using DotCompute.Abstractions.Kernels;
#pragma warning disable CA1848 // Use the LoggerMessage delegates - Metal backend has dynamic logging requirements

namespace DotCompute.Backends.Metal.Accelerators;


/// <summary>
/// Metal-based compute accelerator for macOS and iOS devices.
/// </summary>
public sealed class MetalAccelerator : IAccelerator
{
    private readonly ILogger<MetalAccelerator> _logger;
    private readonly MetalAcceleratorOptions _options;
    private readonly MetalMemoryManager _memoryManager;
    private readonly MetalKernelCompiler _kernelCompiler;
    private readonly MetalCommandBufferPool _commandBufferPool;
    private readonly MetalPerformanceProfiler _profiler;
    private readonly AcceleratorInfo _info;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
    private readonly Timer? _cleanupTimer;
    private int _disposed;

    public MetalAccelerator(
        IOptions<MetalAcceleratorOptions> options,
        ILogger<MetalAccelerator> logger)
    {
        _options = options.Value;
        _logger = logger;

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

        // Initialize memory manager
        _memoryManager = new MetalMemoryManager(_device, _options);

        // Initialize command buffer pool first
        var poolLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalCommandBufferPool>();
        _commandBufferPool = new MetalCommandBufferPool(_commandQueue, poolLogger, _options.CommandBufferCacheSize);

        // Initialize performance profiler
        var profilerLogger = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(logger.IsEnabled(LogLevel.Trace) ? LogLevel.Trace : LogLevel.Information))
            .CreateLogger<MetalPerformanceProfiler>();
        _profiler = new MetalPerformanceProfiler(profilerLogger);

        // Initialize kernel compiler with command buffer pool
        _kernelCompiler = new MetalKernelCompiler(_device, _commandQueue, _logger, _commandBufferPool);

        // Setup periodic cleanup timer (every 30 seconds)
        _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        // Build accelerator info
        var deviceInfo = MetalNative.GetDeviceInfo(_device);
        var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown Metal Device";
        var familiesString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";

        var capabilities = new Dictionary<string, object>
        {
            ["SupportsFamily"] = familiesString,
            ["MaxThreadgroupSize"] = deviceInfo.MaxThreadgroupSize,
            ["MaxThreadsPerThreadgroup"] = deviceInfo.MaxThreadsPerThreadgroup,
            ["MaxBufferLength"] = deviceInfo.MaxBufferLength,
            ["UnifiedMemory"] = deviceInfo.HasUnifiedMemory,
            ["RegistryID"] = deviceInfo.RegistryID,
            ["Location"] = GetDeviceLocation(deviceInfo),
            ["RecommendedMaxWorkingSetSize"] = deviceInfo.RecommendedMaxWorkingSetSize,
            ["IsLowPower"] = deviceInfo.IsLowPower,
            ["IsRemovable"] = deviceInfo.IsRemovable,
            ["LocationNumber"] = deviceInfo.LocationNumber
        };

        _info = new AcceleratorInfo(
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

        _logger.LogInformation(
            "Initialized Metal accelerator: {Name} ({Type}) with {Memory:N0} bytes memory",
            _info.Name, _info.DeviceType, _info.TotalMemory);
    }

    /// <inheritdoc/>
    public AcceleratorInfo Info => _info;

    /// <inheritdoc/>
    public AcceleratorType Type => AcceleratorType.Metal;

    /// <inheritdoc/>
    public IMemoryManager Memory => _memoryManager;

    /// <inheritdoc/>
    public AcceleratorContext Context { get; } = new(IntPtr.Zero, 0);

    /// <inheritdoc/>
    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        options ??= new CompilationOptions();

        _logger.LogDebug("Compiling kernel: {Name}", definition.Name);

        using var profiling = _profiler.Profile($"CompileKernel:{definition.Name}");

        try
        {
            // Compile kernel using Metal Shading Language
            var compiledKernel = await _kernelCompiler.CompileAsync(
                definition,
                options,
                cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Successfully compiled kernel: {Name}", definition.Name);
            return compiledKernel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile kernel: {Name}", definition.Name);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);

        _logger.LogTrace("Synchronizing Metal device");

        using var profiling = _profiler.Profile("Synchronize");

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
            }
        }
        finally
        {
            _commandBufferPool.ReturnCommandBuffer(commandBuffer);
        }

        _logger.LogTrace("Metal device synchronized");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _logger.LogDebug("Disposing Metal accelerator");

        try
        {
            // Ensure all work is complete
            await SynchronizeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during synchronization before disposal");
        }

        // Dispose cleanup timer
        _cleanupTimer?.Dispose();

        // Dispose managed resources
        await _memoryManager.DisposeAsync().ConfigureAwait(false);
        _kernelCompiler.Dispose();
        _commandBufferPool.Dispose();
        _profiler.Dispose();

        // Release native resources
        if (_commandQueue != IntPtr.Zero)
        {
            MetalNative.ReleaseCommandQueue(_commandQueue);
        }

        if (_device != IntPtr.Zero)
        {
            MetalNative.ReleaseDevice(_device);
        }

        _logger.LogDebug("Metal accelerator disposed");
    }

    private void PerformCleanup(object? state)
    {
        if (_disposed > 0)
        {
            return;
        }

        try
        {
            // Clean up command buffer pool
            _commandBufferPool.Cleanup();

            // Log statistics periodically
            var stats = _commandBufferPool.GetStats();
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Command buffer pool stats - Available: {Available}, Active: {Active}, Utilization: {Utilization:F1}%",
                    stats.AvailableBuffers, stats.ActiveBuffers, stats.Utilization);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during periodic cleanup");
        }
    }

    /// <summary>
    /// Gets performance metrics for this accelerator.
    /// </summary>
    /// <returns>Dictionary of operation names to performance metrics.</returns>
    public Dictionary<string, PerformanceMetrics> GetPerformanceMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        return _profiler.GetAllMetrics();
    }

    /// <summary>
    /// Generates a performance report for this accelerator.
    /// </summary>
    /// <returns>A formatted performance report.</returns>
    public string GeneratePerformanceReport()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        return _profiler.GenerateReport();
    }

    /// <summary>
    /// Resets performance metrics for this accelerator.
    /// </summary>
    public void ResetPerformanceMetrics()
    {
        ObjectDisposedException.ThrowIf(_disposed > 0, this);
        _profiler.Reset();
    }

    private static string GetDeviceType(MetalDeviceInfo info)
    {
        if (info.IsLowPower)
        {
            return "IntegratedGPU";
        }

        if (info.IsRemovable)
        {
            return "ExternalGPU";
        }

        return "DiscreteGPU";
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
        // Apple Silicon families (higher capabilities)
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

        // Intel Mac families
        if (families.Contains("Mac2", StringComparison.Ordinal))
        {
            return new Version(2, 0); // Modern Intel Mac GPUs
        }
        if (families.Contains("Mac1", StringComparison.Ordinal))
        {
            return new Version(1, 1); // Older Intel Mac GPUs
        }

        // Common families (cross-platform)
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

        // Legacy support
        if (families.Contains("Legacy", StringComparison.Ordinal))
        {
            return new Version(1, 0);
        }

        return new Version(1, 0); // Default/unknown
    }

    private static int EstimateComputeUnits(MetalDeviceInfo info, string families)
    {
        // Estimate compute units based on GPU family and threadgroup size
        var maxThreads = (int)info.MaxThreadgroupSize;

        // Apple Silicon typically has more compute units
        if (families.Contains("Apple", StringComparison.Ordinal))
        {
            // Apple Silicon M-series chips
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
            // Intel integrated graphics typically have fewer compute units
            return Math.Max(4, maxThreads / 256);
        }

        // Estimate based on max threads per threadgroup
        return Math.Max(1, maxThreads / 64);
    }

    private static long EstimateSharedMemory(MetalDeviceInfo info)
    {
        // Estimate shared memory based on threadgroup capabilities
        var maxThreads = (long)info.MaxThreadgroupSize;

        // Apple Silicon typically has more shared memory per threadgroup
        if (info.HasUnifiedMemory)
        {
            // Apple Silicon: typically 32KB shared memory per threadgroup
            return Math.Min(32 * 1024, maxThreads * 32);
        }
        else
        {
            // Intel Mac: typically 16KB shared memory per threadgroup  
            return Math.Min(16 * 1024, maxThreads * 16);
        }
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
}
