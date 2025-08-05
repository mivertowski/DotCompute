// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

#pragma warning disable CA1848 // Use the LoggerMessage delegates - CUDA backend has dynamic logging requirements

namespace DotCompute.Backends.CUDA;

/// <summary>
/// Plugin implementation for the CUDA backend.
/// </summary>
public sealed class CudaBackendPlugin : BackendPluginBase
{
    /// <inheritdoc/>
    public override string Id => "dotcompute.backends.cuda";

    /// <inheritdoc/>
    public override string Name => "DotCompute CUDA Backend";

    /// <inheritdoc/>
    public override Version Version => new(1, 0, 0);

    /// <inheritdoc/>
    public override string Description => "NVIDIA CUDA GPU backend for high-performance parallel computing";

    /// <inheritdoc/>
    public override string Author => "Michael Ivertowski";

    /// <inheritdoc/>
    public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable | PluginCapabilities.HotReloadable;

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // Register the CUDA backend factory
        services.TryAddSingleton<CudaBackendFactory>();

        // Register multiple CUDA accelerators (one per device)
        services.AddSingleton<IEnumerable<IAccelerator>>(provider =>
        {
            var factory = provider.GetRequiredService<CudaBackendFactory>();
            return factory.CreateAccelerators();
        });

        // Register a default CUDA accelerator
        services.AddSingleton<IAccelerator>(provider =>
        {
            var factory = provider.GetRequiredService<CudaBackendFactory>();
            var defaultAccelerator = factory.CreateDefaultAccelerator();

            if (defaultAccelerator == null)
            {
                throw new InvalidOperationException("Failed to create default CUDA accelerator");
            }

            return new NamedAcceleratorWrapper("cuda", defaultAccelerator);
        });
    }

    /// <inheritdoc/>
    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Initializing CUDA backend plugin");
        await base.OnInitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnStartAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Starting CUDA backend plugin");
        await base.OnStartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task OnStopAsync(CancellationToken cancellationToken)
    {
        Logger?.LogInformation("Stopping CUDA backend plugin");
        await base.OnStopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void OnValidate(PluginValidationResult result)
    {
        base.OnValidate(result);

        // Check CUDA runtime availability
        try
        {
            var cudaResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);

            if (cudaResult != CudaError.Success)
            {
                result.IsValid = false;
                result.Errors.Add($"CUDA runtime error: {CudaRuntime.GetErrorString(cudaResult)}");
            }
            else if (deviceCount == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No CUDA-capable devices found");
            }
            else
            {
                result.Metadata["CudaDeviceCount"] = deviceCount;

                // Check device capabilities
                for (var i = 0; i < deviceCount; i++)
                {
                    var props = new CudaDeviceProperties();
                    var propResult = CudaRuntime.cudaGetDeviceProperties(ref props, i);

                    if (propResult == CudaError.Success)
                    {
                        result.Metadata[$"Device{i}Name"] = props.Name;
                        result.Metadata[$"Device{i}ComputeCapability"] = $"{props.Major}.{props.Minor}";
                        result.Metadata[$"Device{i}GlobalMemory"] = props.TotalGlobalMem;

                        // Check minimum compute capability
                        if (props.Major < 3)
                        {
                            result.Warnings.Add($"Device {i} has compute capability {props.Major}.{props.Minor} which may have limited support");
                        }
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
            result.IsValid = false;
            result.Errors.Add("CUDA runtime library not found. Please install CUDA toolkit.");
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Failed to validate CUDA environment: {ex.Message}");
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
    ""CudaBackend"": {
      ""type"": ""object"",
      ""properties"": {
        ""DeviceSelection"": {
          ""type"": ""object"",
          ""properties"": {
            ""PreferredDeviceId"": { ""type"": ""integer"", ""minimum"": 0, ""default"": 0 },
            ""DeviceSelectionStrategy"": { ""type"": ""string"", ""enum"": [""Default"", ""HighestComputeCapability"", ""MostMemory"", ""FastestClock""], ""default"": ""Default"" },
            ""EnableMultiDevice"": { ""type"": ""boolean"", ""default"": false }
          }
        },
        ""Performance"": {
          ""type"": ""object"",
          ""properties"": {
            ""EnableUnifiedMemory"": { ""type"": ""boolean"", ""default"": false },
            ""EnableCudaGraphs"": { ""type"": ""boolean"", ""default"": true },
            ""EnableStreamCapture"": { ""type"": ""boolean"", ""default"": true },
            ""DefaultStreamPriority"": { ""type"": ""integer"", ""minimum"": -2, ""maximum"": 0, ""default"": 0 },
            ""EnableMemoryPooling"": { ""type"": ""boolean"", ""default"": true }
          }
        },
        ""Debug"": {
          ""type"": ""object"",
          ""properties"": {
            ""EnableSynchronousExecution"": { ""type"": ""boolean"", ""default"": false },
            ""EnableErrorChecking"": { ""type"": ""boolean"", ""default"": true },
            ""LogKernelLaunches"": { ""type"": ""boolean"", ""default"": false }
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

        // Add CUDA-specific metrics
        try
        {
            var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (result == CudaError.Success)
            {
                metrics.CustomMetrics["CudaDeviceCount"] = deviceCount;

                // Get runtime version
                if (CudaRuntime.cudaRuntimeGetVersion(out var runtimeVersion) == CudaError.Success)
                {
                    metrics.CustomMetrics["CudaRuntimeVersion"] = runtimeVersion;
                }

                // Get driver version
                if (CudaRuntime.cudaDriverGetVersion(out var driverVersion) == CudaError.Success)
                {
                    metrics.CustomMetrics["CudaDriverVersion"] = driverVersion;
                }

                // Get memory info for default device
                if (deviceCount > 0)
                {
                    var memResult = CudaRuntime.cudaMemGetInfo(out var free, out var total);
                    if (memResult == CudaError.Success)
                    {
                        metrics.CustomMetrics["CudaFreeMemory"] = free;
                        metrics.CustomMetrics["CudaTotalMemory"] = total;
                        metrics.CustomMetrics["CudaMemoryUtilization"] = (double)(total - free) / total * 100.0;
                    }
                }
            }
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
public static class CudaBackendPluginExtensions
{
    /// <summary>
    /// Adds the CUDA backend to the service collection.
    /// </summary>
    public static IServiceCollection AddCudaBackend(this IServiceCollection services)
    {
        // Register the CUDA backend factory
        services.TryAddSingleton<CudaBackendFactory>();

        // Register multiple CUDA accelerators (one per device)
        services.AddSingleton<IEnumerable<IAccelerator>>(provider =>
        {
            var factory = provider.GetRequiredService<CudaBackendFactory>();
            return factory.CreateAccelerators();
        });

        // Register a default CUDA accelerator
        services.AddSingleton<IAccelerator>(provider =>
        {
            var factory = provider.GetRequiredService<CudaBackendFactory>();
            var defaultAccelerator = factory.CreateDefaultAccelerator();

            if (defaultAccelerator == null)
            {
                throw new InvalidOperationException("Failed to create default CUDA accelerator");
            }

            return new NamedAcceleratorWrapper("cuda", defaultAccelerator);
        });

        return services;
    }

    /// <summary>
    /// Adds the CUDA backend with a specific device ID.
    /// </summary>
    public static IServiceCollection AddCudaBackend(this IServiceCollection services, int deviceId)
    {
        services.TryAddSingleton<CudaBackendFactory>();

        services.AddSingleton<IAccelerator>(provider =>
        {
            var logger = provider.GetService<ILogger<CudaAccelerator>>();
            var accelerator = new CudaAccelerator(deviceId, logger);
            return new NamedAcceleratorWrapper($"cuda-{deviceId}", accelerator);
        });

        return services;
    }
}

/// <summary>
/// Wrapper to provide named accelerator support.
/// </summary>
internal sealed class NamedAcceleratorWrapper(string name, IAccelerator accelerator) : IAccelerator
{
    private readonly string _name = name ?? throw new ArgumentNullException(nameof(name));
    private readonly IAccelerator _accelerator = accelerator ?? throw new ArgumentNullException(nameof(accelerator));

    public string Name => _name;

    public AcceleratorInfo Info => _accelerator.Info;

    public IMemoryManager Memory => _accelerator.Memory;

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = default,
        CancellationToken cancellationToken = default) => _accelerator.CompileKernelAsync(definition, options, cancellationToken);

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => _accelerator.SynchronizeAsync(cancellationToken);

    public ValueTask DisposeAsync() => _accelerator.DisposeAsync();
}
