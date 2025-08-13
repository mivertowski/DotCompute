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
using AbstractionsAcceleratorInfo = DotCompute.Abstractions.AcceleratorInfo;
using AbstractionsCompilationOptions = DotCompute.Abstractions.CompilationOptions;
using AbstractionsIAccelerator = DotCompute.Abstractions.IAccelerator;
using AbstractionsICompiledKernel = DotCompute.Abstractions.ICompiledKernel;
using AbstractionsIMemoryManager = DotCompute.Abstractions.IMemoryManager;
using AcceleratorType = DotCompute.Abstractions.AcceleratorType;
using AbstractionsKernelDefinition = DotCompute.Abstractions.KernelDefinition;
using IAccelerator = DotCompute.Abstractions.IAccelerator;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - Plugin lifecycle logging is minimal and not performance-critical

namespace DotCompute.Backends.CPU.Registration;

/// <summary>
/// Plugin implementation for the CPU backend.
/// </summary>
public sealed class CpuBackendPlugin : BackendPluginBase
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
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // Configure CPU backend options
#pragma warning disable IL2026, IL3050 // Suppress AOT and trimming warnings for configuration binding
        services.Configure<CpuAcceleratorOptions>(options =>
            configuration.GetSection("CpuBackend:Accelerator").Bind(options));
        services.Configure<CpuThreadPoolOptions>(options =>
            configuration.GetSection("CpuBackend:ThreadPool").Bind(options));
#pragma warning restore IL2026, IL3050

        // Register the CPU accelerator
        services.TryAddSingleton<CpuAccelerator>();

        // Register as IAccelerator with a factory that includes the backend name
        services.AddSingleton<IAccelerator>(provider =>
        {
            var accelerator = provider.GetRequiredService<CpuAccelerator>();
            return new NamedAcceleratorWrapper("cpu", accelerator);
        });
    }

    /// <inheritdoc/>
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Initializing CPU backend plugin");
        await base.OnInitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Starting CPU backend plugin");
        await base.OnStartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Stopping CPU backend plugin");
        await base.OnStopAsync(cancellationToken).ConfigureAwait(false);
    }

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
            var simdInfo = DotCompute.Backends.CPU.Intrinsics.SimdCapabilities.GetSummary();
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
            var simdInfo = DotCompute.Backends.CPU.Intrinsics.SimdCapabilities.GetSummary();
            metrics.CustomMetrics["SimdVectorWidth"] = DotCompute.Backends.CPU.Intrinsics.SimdCapabilities.PreferredVectorWidth;
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
            services.Configure(configureAccelerator);
        }
        else
        {
            services.Configure<CpuAcceleratorOptions>(options => { });
        }

        if (configureThreadPool != null)
        {
            services.Configure(configureThreadPool);
        }
        else
        {
            services.Configure<CpuThreadPoolOptions>(options => { });
        }

        // Register the CPU accelerator
        services.TryAddSingleton<CpuAccelerator>();

        // Register as IAccelerator with a factory that includes the backend name
        services.AddSingleton<IAccelerator>(provider =>
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

/// <summary>
/// Wrapper to provide named accelerator support.
/// </summary>
internal sealed class NamedAcceleratorWrapper(string name, AbstractionsIAccelerator accelerator) : AbstractionsIAccelerator
{
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private readonly AbstractionsIAccelerator _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));

    public string Name => _name;

    public AcceleratorType Type => _accelerator.Type;

    public AbstractionsAcceleratorInfo Info => _accelerator.Info;

    public AbstractionsIMemoryManager Memory => _accelerator.Memory;

    public ValueTask<AbstractionsICompiledKernel> CompileKernelAsync(
        AbstractionsKernelDefinition definition,
        AbstractionsCompilationOptions? options = default,
        CancellationToken cancellationToken = default) => _accelerator.CompileKernelAsync(definition, options, cancellationToken);

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => _accelerator.SynchronizeAsync(cancellationToken);

    public ValueTask DisposeAsync() => _accelerator.DisposeAsync();
}

#pragma warning restore CA1848
