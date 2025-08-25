// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using global::System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Registration;


/// <summary>
/// Plugin implementation for the Metal backend.
/// Consolidates duplicate registration patterns using BaseBackendPlugin.
/// </summary>
public sealed partial class MetalBackendPlugin : BaseBackendPlugin<MetalAccelerator, MetalAcceleratorOptions>
{
    /// <inheritdoc/>
    public override string Id => "dotcompute.backends.metal";

    /// <inheritdoc/>
    public override string Name => "DotCompute Metal Backend";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "Apple Metal GPU backend for macOS and iOS platforms";

    /// <inheritdoc/>
    public override string Author => "Michael Ivertowski";

    /// <inheritdoc/>
    public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;

    /// <inheritdoc/>
    protected override string AcceleratorName => "metal";

    /// <inheritdoc/>
    protected override string ConfigurationSectionName => "MetalBackend";

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("AOT", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Configuration options are preserved")]
    [UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Configuration options are preserved")]
    protected override void ConfigureBackendOptions(IServiceCollection services, IConfiguration configuration)
        // Configure Metal backend options  

        => services.Configure<MetalAcceleratorOptions>(configuration.GetSection("MetalBackend:Accelerator"));

    /// <inheritdoc/>
    protected override void RegisterAccelerator(IServiceCollection services, IConfiguration configuration)
    {
        // Check platform support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("Metal backend is only supported on macOS and iOS platforms.");
        }

        // Register the Metal accelerator
        services.TryAddSingleton<MetalAccelerator>();
    }


    /// <inheritdoc/>
    protected override void OnValidate(PluginValidationResult result)
    {
        base.OnValidate(result);

        // Validate platform support
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            result.IsValid = false;
            result.Errors.Add("Metal backend requires macOS or iOS platform");
        }

        // Check Metal device availability
        try
        {
            var device = DotCompute.Backends.Metal.Native.MetalNative.CreateSystemDefaultDevice();
            if (device == IntPtr.Zero)
            {
                result.IsValid = false;
                result.Errors.Add("No Metal-capable devices found");
            }
            else
            {
                DotCompute.Backends.Metal.Native.MetalNative.ReleaseDevice(device);
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to initialize Metal device: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public override string GetConfigurationSchema()
    {
        return @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""type"": ""object"",
  ""properties"": {
    ""MetalBackend"": {
      ""type"": ""object"",
      ""properties"": {
        ""Accelerator"": {
          ""type"": ""object"",
          ""properties"": {
            ""MaxMemoryAllocation"": { ""type"": ""integer"", ""minimum"": 1048576, ""default"": 4294967296 },
            ""MaxThreadgroupSize"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 1024, ""default"": 1024 },
            ""PreferIntegratedGpu"": { ""type"": ""boolean"", ""default"": false },
            ""EnableMetalPerformanceShaders"": { ""type"": ""boolean"", ""default"": true },
            ""EnableGpuFamilySpecialization"": { ""type"": ""boolean"", ""default"": true },
            ""CommandBufferCacheSize"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 256, ""default"": 16 }
          }
        }
      }
    }
  }
}";
    }

    /// <inheritdoc/>
    protected override void OnUpdateMetrics(PluginMetrics metrics)
    {
        base.OnUpdateMetrics(metrics);

        // Add Metal-specific metrics
        metrics.CustomMetrics["Platform"] = "macOS";
        metrics.CustomMetrics["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString();

        // Get Metal device information if available
        try
        {
            var device = DotCompute.Backends.Metal.Native.MetalNative.CreateSystemDefaultDevice();
            if (device != IntPtr.Zero)
            {
                var deviceInfo = DotCompute.Backends.Metal.Native.MetalNative.GetDeviceInfo(device);
                metrics.CustomMetrics["MetalDeviceRegistryID"] = deviceInfo.RegistryID;
                metrics.CustomMetrics["MetalDeviceHasUnifiedMemory"] = deviceInfo.HasUnifiedMemory;
                metrics.CustomMetrics["MetalDeviceIsLowPower"] = deviceInfo.IsLowPower;
                metrics.CustomMetrics["MetalDeviceMaxThreadgroupSize"] = deviceInfo.MaxThreadgroupSize;

                DotCompute.Backends.Metal.Native.MetalNative.ReleaseDevice(device);
            }
        }
        catch
        {
            // Ignore errors in metrics collection
        }
    }

    #region Logger Message Delegates

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Initializing Metal backend plugin")]
    private static partial void LogInitializing(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Starting Metal backend plugin")]
    private static partial void LogStarting(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Stopping Metal backend plugin")]
    private static partial void LogStopping(ILogger logger);

    #endregion
}

/// <summary>
/// Static extension methods for service registration (backward compatibility).
/// </summary>
public static class MetalBackendPluginExtensions
{
    /// <summary>
    /// Adds the Metal backend to the service collection.
    /// </summary>
    public static IServiceCollection AddMetalBackend(
        this IServiceCollection services,
        Action<MetalAcceleratorOptions>? configureAccelerator = null)
    {
        // Check if we're on a supported platform
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("Metal backend is only supported on macOS and iOS platforms.");
        }

        // Register options
        if (configureAccelerator != null)
        {
            _ = services.Configure(configureAccelerator);
        }
        else
        {
            _ = services.Configure<MetalAcceleratorOptions>(options => { });
        }

        // Register the Metal accelerator
        services.TryAddSingleton<MetalAccelerator>();

        // Register as IAccelerator with a factory that includes the backend name
        _ = services.AddSingleton<IAccelerator>(provider =>
        {
            var accelerator = provider.GetRequiredService<MetalAccelerator>();
            return new Plugins.Core.NamedAcceleratorWrapper("metal", accelerator);
        });

        return services;
    }

    /// <summary>
    /// Adds the Metal backend with default configuration.
    /// </summary>
    public static IServiceCollection AddMetalBackend(this IServiceCollection services) => services.AddMetalBackend(null);

    /// <summary>
    /// Adds the Metal backend with device selection.
    /// </summary>
    public static IServiceCollection AddMetalBackend(
        this IServiceCollection services,
        MetalDeviceSelector deviceSelector,
        Action<MetalAcceleratorOptions>? configureAccelerator = null)
    {
        // Configure device selection
        _ = services.Configure<MetalAcceleratorOptions>(options =>
        {
            if (deviceSelector == MetalDeviceSelector.PreferIntegrated)
            {
                options.PreferIntegratedGpu = true;
            }
            else if (deviceSelector == MetalDeviceSelector.PreferDiscrete)
            {
                options.PreferIntegratedGpu = false;
            }
        });

        return services.AddMetalBackend(configureAccelerator);
    }
}

/// <summary>
/// Device selection preference for Metal backend.
/// </summary>
public enum MetalDeviceSelector
{
    /// <summary>
    /// Use the system default device.
    /// </summary>
    Default,

    /// <summary>
    /// Prefer integrated GPU (better for power efficiency).
    /// </summary>
    PreferIntegrated,

    /// <summary>
    /// Prefer discrete GPU (better for performance).
    /// </summary>
    PreferDiscrete
}
