// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Kernels;
using DotCompute.Backends.Metal.Memory;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly AcceleratorInfo _info;
    private readonly IntPtr _device;
    private readonly IntPtr _commandQueue;
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

        // Initialize kernel compiler
        _kernelCompiler = new MetalKernelCompiler(_device, _commandQueue, _logger);

        // Build accelerator info
        var deviceInfo = MetalNative.GetDeviceInfo(_device);
        var capabilities = new Dictionary<string, object>
        {
            ["SupportsFamily"] = deviceInfo.SupportedFamilies,
            ["MaxThreadgroupSize"] = deviceInfo.MaxThreadgroupSize,
            ["MaxThreadsPerThreadgroup"] = deviceInfo.MaxThreadsPerThreadgroup,
            ["MaxBufferLength"] = deviceInfo.MaxBufferLength,
            ["UnifiedMemory"] = deviceInfo.HasUnifiedMemory,
            ["RegistryID"] = deviceInfo.RegistryID,
            ["Location"] = GetDeviceLocation(deviceInfo),
            ["RecommendedMaxWorkingSetSize"] = deviceInfo.RecommendedMaxWorkingSetSize
        };

        _info = new AcceleratorInfo(
            type: AcceleratorType.Metal,
            name: Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown Metal Device",
            driverVersion: "1.0",
            memorySize: deviceInfo.HasUnifiedMemory
                ? (long)deviceInfo.RecommendedMaxWorkingSetSize
                : (long)deviceInfo.MaxBufferLength,
            computeUnits: (int)deviceInfo.MaxThreadgroupSize,
            maxClockFrequency: 0, // Metal doesn't expose clock frequency
            computeCapability: GetComputeCapability(deviceInfo),
            maxSharedMemoryPerBlock: (long)deviceInfo.MaxThreadgroupSize * 1024, // Estimate
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

        // Create a command buffer and commit it to ensure all previous work is complete
        var commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
        if (commandBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create command buffer for synchronization");
        }

        try
        {
            // Add a completion handler
            var tcs = new TaskCompletionSource<bool>();
            MetalNative.SetCommandBufferCompletionHandler(commandBuffer, (status) =>
            {
                if (status == MetalCommandBufferStatus.Completed)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetException(new InvalidOperationException($"Command buffer failed with status: {status}"));
                }
            });

            // Commit the command buffer
            MetalNative.CommitCommandBuffer(commandBuffer);

            // Wait for completion
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            MetalNative.ReleaseCommandBuffer(commandBuffer);
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

        // Dispose managed resources
        await _memoryManager.DisposeAsync().ConfigureAwait(false);
        _kernelCompiler.Dispose();

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

    private static Version GetComputeCapability(MetalDeviceInfo info)
    {
        var families = Marshal.PtrToStringAnsi(info.SupportedFamilies) ?? "";

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

        if (families.Contains("Mac2", StringComparison.Ordinal))
        {
            return new Version(2, 0); // Intel Mac GPUs
        }

        return new Version(1, 0); // Default/unknown
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
