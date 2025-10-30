// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Accelerators;
using DotCompute.Backends.CPU.Threading;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using IAccelerator = DotCompute.Abstractions.IAccelerator;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - Plugin lifecycle logging is minimal and not performance-critical

namespace DotCompute.Backends.CPU.Registration;


/// <summary>
/// Plugin implementation for the CPU backend.
/// Consolidates duplicate registration patterns using BaseBackendPlugin.
/// </summary>
public sealed class CpuBackendPlugin : BaseBackendPlugin<CpuAccelerator, CpuAcceleratorOptions>
{
    /// <inheritdoc/>
    public override string Id => "dotcompute.backends.cpu";

    /// <inheritdoc/>
    public override string Name => "DotCompute CPU Backend";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "High-performance CPU backend with SIMD vectorization support";

    /// <inheritdoc/>
    public override string Author => "Michael Ivertowski";

    /// <inheritdoc/>
    public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable | PluginCapabilities.HotReloadable;

    /// <inheritdoc/>
    protected override string AcceleratorName => "cpu";

    /// <inheritdoc/>
    protected override string ConfigurationSectionName => "CpuBackend";

    /// <inheritdoc/>
    protected override void ConfigureBackendOptions(IServiceCollection services, IConfiguration configuration)
    {
        // Configure CPU accelerator options
#pragma warning disable IL2026, IL3050 // Suppress AOT and trimming warnings for configuration binding
        _ = services.Configure<CpuAcceleratorOptions>(options =>
            configuration.GetSection("CpuBackend:Accelerator").Bind(options));

        // Also configure thread pool options specific to CPU backend
        _ = services.Configure<CpuThreadPoolOptions>(options =>
            configuration.GetSection("CpuBackend:ThreadPool").Bind(options));
#pragma warning restore IL2026, IL3050
    }

    /// <inheritdoc/>
    protected override void RegisterAccelerator(IServiceCollection services, IConfiguration configuration)
        // Register the CPU accelerator





        => services.TryAddSingleton<CpuAccelerator>();


    /// <inheritdoc/>
    protected override void OnValidate(PluginValidationResult result)
    {
        base.OnValidate(result);

        // Validate CPU availability
        if (Environment.ProcessorCount <= 0)
        {
            result.IsValid = false;
            result.Errors.Add("No CPU cores detected");
        }

        // Check SIMD support
        try
        {
            var simdInfo = Intrinsics.SimdCapabilities.GetSummary();
            if (simdInfo.SupportedInstructionSets.Count == 0)
            {
                result.Warnings.Add("No SIMD instruction sets detected - performance may be reduced");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Failed to detect SIMD capabilities: {ex.Message}");
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
    ""CpuBackend"": {
      ""type"": ""object"",
      ""properties"": {
        ""Accelerator"": {
          ""type"": ""object"",
          ""properties"": {
            ""MaxWorkGroupSize"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 65536, ""default"": 1024 },
            ""MaxMemoryAllocation"": { ""type"": ""integer"", ""minimum"": 1048576, ""default"": 2147483648 },
            ""EnableAutoVectorization"": { ""type"": ""boolean"", ""default"": true },
            ""EnableNumaAwareAllocation"": { ""type"": ""boolean"", ""default"": true },
            ""MinVectorizationWorkSize"": { ""type"": ""integer"", ""minimum"": 1, ""default"": 256 },
            ""EnableLoopUnrolling"": { ""type"": ""boolean"", ""default"": true },
            ""TargetVectorWidth"": { ""type"": ""integer"", ""minimum"": 0, ""default"": 0 }
          }
        },
        ""ThreadPool"": {
          ""type"": ""object"",
          ""properties"": {
            ""WorkerThreadCount"": { ""type"": ""integer"", ""minimum"": 1, ""default"": 0 },
            ""EnableWorkStealing"": { ""type"": ""boolean"", ""default"": true },
            ""EnableThreadAffinity"": { ""type"": ""boolean"", ""default"": false }
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

        // Add CPU-specific metrics
        metrics.CustomMetrics["ProcessorCount"] = Environment.ProcessorCount;
        metrics.CustomMetrics["WorkingSet"] = Environment.WorkingSet;
        metrics.CustomMetrics["Is64BitProcess"] = Environment.Is64BitProcess;

        // Get SIMD capabilities
        try
        {
            var simdInfo = Intrinsics.SimdCapabilities.GetSummary();
            metrics.CustomMetrics["SimdVectorWidth"] = Intrinsics.SimdCapabilities.PreferredVectorWidth;
            metrics.CustomMetrics["SimdInstructionSets"] = simdInfo.SupportedInstructionSets.Count;
        }
        catch
        {
            // Ignore errors in metrics collection
        }
    }
}

/// <summary>
/// Static extension methods for service registration (backward compatibility).
/// </summary>
public static class CpuBackendPluginExtensions
{
    /// <summary>
    /// Adds the CPU backend to the service collection.
    /// </summary>
    public static IServiceCollection AddCpuBackend(
        this IServiceCollection services,
        Action<CpuAcceleratorOptions>? configureAccelerator = null,
        Action<CpuThreadPoolOptions>? configureThreadPool = null)
    {
        // Register options
        if (configureAccelerator != null)
        {
            _ = services.Configure(configureAccelerator);
        }
        else
        {
            _ = services.Configure<CpuAcceleratorOptions>(options => { });
        }

        if (configureThreadPool != null)
        {
            _ = services.Configure(configureThreadPool);
        }
        else
        {
            _ = services.Configure<CpuThreadPoolOptions>(options => { });
        }

        // Register the CPU accelerator
        services.TryAddSingleton<CpuAccelerator>();

        // Register as IAccelerator with a factory that includes the backend name
        _ = services.AddSingleton<IAccelerator>(provider =>
        {
            var accelerator = provider.GetRequiredService<CpuAccelerator>();
            return new NamedAcceleratorWrapper("cpu", accelerator);
        });

        return services;
    }

    /// <summary>
    /// Adds the CPU backend with default configuration.
    /// </summary>
    public static IServiceCollection AddCpuBackend(this IServiceCollection services) => services.AddCpuBackend(null, null);
}


#pragma warning restore CA1848

