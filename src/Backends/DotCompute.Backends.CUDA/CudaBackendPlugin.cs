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

namespace DotCompute.Backends.CUDA
{

    /// <summary>
    /// Plugin implementation for the CUDA backend.
    /// Consolidates duplicate registration patterns using BaseBackendPlugin.
    /// </summary>
    public sealed class CudaBackendPlugin : BaseBackendPlugin<CudaAccelerator, CudaBackendOptions>
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
        protected override string AcceleratorName => "cuda";

        /// <inheritdoc/>
        protected override string ConfigurationSectionName => "CudaBackend";

        /// <inheritdoc/>
        protected override void RegisterAccelerator(IServiceCollection services, IConfiguration configuration)
        {
            // Register the CUDA backend factory
            services.TryAddSingleton<CudaBackendFactory>();

            // Register multiple CUDA accelerators (one per device)
            _ = services.AddSingleton<IEnumerable<IAccelerator>>(provider =>
            {
                var factory = provider.GetRequiredService<CudaBackendFactory>();
                return factory.CreateAccelerators();
            });

            // Register a default CUDA accelerator as the primary CudaAccelerator
            _ = services.AddSingleton<CudaAccelerator>(provider =>
            {
                var factory = provider.GetRequiredService<CudaBackendFactory>();
                var defaultAccelerator = factory.CreateDefaultAccelerator();

                return defaultAccelerator == null
                    ? throw new InvalidOperationException("Failed to create default CUDA accelerator")
                    : (CudaAccelerator)defaultAccelerator;
            });
        }


        /// <inheritdoc/>
        protected override void OnValidate(PluginValidationResult result)
        {
            base.OnValidate(result);

            // Check CUDA runtime availability using enhanced device detection
            try
            {
                var cudaResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);

                if (cudaResult != CudaError.Success)
                {
                    result.IsValid = false;
                    result.Errors.Add($"CUDA runtime error: {CudaRuntime.GetErrorString(cudaResult)}");
                    return;
                }

                if (deviceCount == 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("No CUDA-capable devices found");
                    return;
                }

                result.Metadata["CudaDeviceCount"] = deviceCount;

                // Add CUDA runtime and driver version info
                var runtimeVersion = CudaRuntime.GetRuntimeVersion();
                var driverVersion = CudaRuntime.GetDriverVersion();
                result.Metadata["CudaRuntimeVersion"] = runtimeVersion.ToString();
                result.Metadata["CudaDriverVersion"] = driverVersion.ToString();

                // Use enhanced device detection to get detailed device information
                var devices = CudaDevice.DetectAll(Logger).ToList();
                var rtx2000AdaCount = 0;
                var adaLovelaceCount = 0;

                for (var i = 0; i < devices.Count; i++)
                {
                    var device = devices[i];

                    result.Metadata[$"Device{i}Name"] = device.Name;
                    result.Metadata[$"Device{i}ComputeCapability"] = device.ComputeCapability.ToString();
                    result.Metadata[$"Device{i}GlobalMemory"] = device.GlobalMemorySize;
                    result.Metadata[$"Device{i}Architecture"] = device.ArchitectureGeneration;
                    result.Metadata[$"Device{i}IsRTX2000Ada"] = device.IsRTX2000Ada;
                    result.Metadata[$"Device{i}StreamingMultiprocessors"] = device.StreamingMultiprocessorCount;
                    result.Metadata[$"Device{i}EstimatedCudaCores"] = device.GetEstimatedCudaCores();
                    result.Metadata[$"Device{i}MemoryBandwidth"] = device.MemoryBandwidthGBps;

                    // Track special GPU types
                    if (device.IsRTX2000Ada)
                    {
                        rtx2000AdaCount++;
                    }

                    if (device.ArchitectureGeneration == "Ada Lovelace")
                    {
                        adaLovelaceCount++;
                    }

                    // Check minimum compute capability
                    if (device.ComputeCapabilityMajor < 3)
                    {
                        result.Warnings.Add($"Device {i} ({device.Name}) has compute capability {device.ComputeCapability} which may have limited support");
                    }

                    // Check for legacy architectures
                    if (device.ComputeCapabilityMajor < 6)
                    {
                        result.Warnings.Add($"Device {i} ({device.Name}) uses legacy {device.ArchitectureGeneration} architecture");
                    }

                    // Provide RTX 2000 Ada specific information
                    if (device.IsRTX2000Ada)
                    {
                        result.Metadata[$"Device{i}RTX2000AdaFeatures"] = "Tensor Cores, RT Cores, DLSS 3, AV1 Encoding";
                    }
                }

                // Summary metadata
                result.Metadata["RTX2000AdaDeviceCount"] = rtx2000AdaCount;
                result.Metadata["AdaLovelaceDeviceCount"] = adaLovelaceCount;

                if (rtx2000AdaCount > 0)
                {
                    result.Metadata["HasRTX2000AdaSupport"] = true;
                    Logger?.LogInformation("Detected {RTX2000AdaCount} RTX 2000 Ada Generation GPU(s)", rtx2000AdaCount);
                }

                if (adaLovelaceCount > 0)
                {
                    result.Metadata["HasAdaLovelaceSupport"] = true;
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
                Logger?.LogError(ex, "CUDA validation failed");
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

            // Add CUDA-specific metrics with enhanced device detection
            try
            {
                var result = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
                if (result == CudaError.Success)
                {
                    metrics.CustomMetrics["CudaDeviceCount"] = deviceCount;

                    // Get runtime and driver versions using enhanced methods
                    var runtimeVersion = CudaRuntime.GetRuntimeVersion();
                    var driverVersion = CudaRuntime.GetDriverVersion();
                    metrics.CustomMetrics["CudaRuntimeVersion"] = runtimeVersion.ToString();
                    metrics.CustomMetrics["CudaDriverVersion"] = driverVersion.ToString();

                    // Get detailed device metrics
                    if (deviceCount > 0)
                    {
                        try
                        {
                            var devices = CudaDevice.DetectAll(Logger).ToList();
                            var rtx2000AdaCount = devices.Count(d => d.IsRTX2000Ada);
                            var adaLovelaceCount = devices.Count(d => d.ArchitectureGeneration == "Ada Lovelace");
                            var totalCudaCores = devices.Sum(d => d.GetEstimatedCudaCores());
                            var totalStreamingMultiprocessors = devices.Sum(d => d.StreamingMultiprocessorCount);

                            metrics.CustomMetrics["CudaRTX2000AdaDeviceCount"] = rtx2000AdaCount;
                            metrics.CustomMetrics["CudaAdaLovelaceDeviceCount"] = adaLovelaceCount;
                            metrics.CustomMetrics["CudaTotalEstimatedCores"] = totalCudaCores;
                            metrics.CustomMetrics["CudaTotalStreamingMultiprocessors"] = totalStreamingMultiprocessors;

                            // Get memory info for default device (device 0)
                            if (devices.Count > 0)
                            {
                                var defaultDevice = devices[0];
                                var (freeMemory, totalMemory) = defaultDevice.GetMemoryInfo();

                                metrics.CustomMetrics["CudaFreeMemory"] = freeMemory;
                                metrics.CustomMetrics["CudaTotalMemory"] = totalMemory;
                                metrics.CustomMetrics["CudaMemoryUtilization"] = (double)(totalMemory - freeMemory) / totalMemory * 100.0;
                                metrics.CustomMetrics["CudaDefaultDeviceName"] = defaultDevice.Name;
                                metrics.CustomMetrics["CudaDefaultDeviceArchitecture"] = defaultDevice.ArchitectureGeneration;
                                metrics.CustomMetrics["CudaDefaultDeviceComputeCapability"] = defaultDevice.ComputeCapability.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger?.LogDebug(ex, "Failed to collect detailed CUDA device metrics");

                            // Fallback to basic memory info
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
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "Failed to collect CUDA metrics");
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
            _ = services.AddSingleton<IEnumerable<IAccelerator>>(provider =>
            {
                var factory = provider.GetRequiredService<CudaBackendFactory>();
                return factory.CreateAccelerators();
            });

            // Register a default CUDA accelerator
            _ = services.AddSingleton<IAccelerator>(provider =>
            {
                var factory = provider.GetRequiredService<CudaBackendFactory>();
                var defaultAccelerator = factory.CreateDefaultAccelerator();

                return defaultAccelerator == null
                    ? throw new InvalidOperationException("Failed to create default CUDA accelerator")
                    : (IAccelerator)new Plugins.Core.NamedAcceleratorWrapper("cuda", defaultAccelerator);
            });

            return services;
        }

        /// <summary>
        /// Adds the CUDA backend with a specific device ID.
        /// </summary>
        public static IServiceCollection AddCudaBackend(this IServiceCollection services, int deviceId)
        {
            services.TryAddSingleton<CudaBackendFactory>();

            _ = services.AddSingleton<IAccelerator>(provider =>
            {
                var logger = provider.GetService<ILogger<CudaAccelerator>>();
                var accelerator = new CudaAccelerator(deviceId, logger);
                return new Plugins.Core.NamedAcceleratorWrapper($"cuda-{deviceId}", accelerator);
            });

            return services;
        }
    }
}
