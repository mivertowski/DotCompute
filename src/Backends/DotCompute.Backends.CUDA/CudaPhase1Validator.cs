// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA
{

/// <summary>
/// Validates Phase 1 implementation of the CUDA backend.
/// </summary>
public static class CudaPhase1Validator
{
    /// <summary>
    /// Performs comprehensive validation of Phase 1 CUDA backend implementation.
    /// </summary>
    /// <param name="logger">Optional logger for validation output.</param>
    /// <returns>Validation results including any issues found.</returns>
    public static ValidationResult ValidatePhase1Implementation(ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        var result = new ValidationResult();

        logger.LogInformation("Starting CUDA Phase 1 validation");

        try
        {
            // 1. Validate CUDA runtime availability
            ValidateCudaRuntimeAvailability(result, logger);

            // 2. Validate device detection capabilities
            ValidateDeviceDetection(result, logger);

            // 3. Validate CudaDevice class functionality
            ValidateCudaDeviceClass(result, logger);

            // 4. Validate CudaAccelerator initialization
            ValidateCudaAcceleratorInitialization(result, logger);

            // 5. Validate CudaBackendPlugin registration
            ValidateCudaBackendPluginRegistration(result, logger);

            // 6. Validate error handling and resource disposal
            ValidateErrorHandlingAndResourceDisposal(result, logger);

            result.Success = result.Errors.Count == 0;
            
            logger.LogInformation("CUDA Phase 1 validation completed: {Success} (Errors: {ErrorCount}, Warnings: {WarningCount})",
                result.Success, result.Errors.Count, result.Warnings.Count);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Validation failed with exception: {ex.Message}");
            logger.LogError(ex, "CUDA Phase 1 validation failed with exception");
        }

        return result;
    }

    private static void ValidateCudaRuntimeAvailability(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating CUDA runtime availability");

        try
        {
            // Test CUDA availability
            var isSupported = CudaRuntime.IsCudaSupported();
            logger.LogInformation("CUDA runtime supported: {IsSupported}", isSupported);

            if (!isSupported)
            {
                result.Warnings.Add("CUDA runtime is not available on this system");
                return;
            }

            // Test runtime version detection
            var runtimeVersion = CudaRuntime.GetRuntimeVersion();
            var driverVersion = CudaRuntime.GetDriverVersion();
            
            logger.LogInformation("CUDA Runtime Version: {RuntimeVersion}", runtimeVersion);
            logger.LogInformation("CUDA Driver Version: {DriverVersion}", driverVersion);

            result.Details["CudaRuntimeVersion"] = runtimeVersion.ToString();
            result.Details["CudaDriverVersion"] = driverVersion.ToString();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to validate CUDA runtime availability: {ex.Message}");
            logger.LogError(ex, "CUDA runtime validation failed");
        }
    }

    private static void ValidateDeviceDetection(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating device detection");

        try
        {
            if (!CudaRuntime.IsCudaSupported())
            {
                result.Warnings.Add("Skipping device detection - CUDA not available");
                return;
            }

            var cudaResult = CudaRuntime.cudaGetDeviceCount(out var deviceCount);
            if (cudaResult != CudaError.Success)
            {
                result.Errors.Add($"Failed to get device count: {CudaRuntime.GetErrorString(cudaResult)}");
                return;
            }

            logger.LogInformation("Found {DeviceCount} CUDA device(s)", deviceCount);
            result.Details["DeviceCount"] = deviceCount;

            if (deviceCount == 0)
            {
                result.Warnings.Add("No CUDA devices found");
                return;
            }

            // Test basic device property query
            for (var i = 0; i < Math.Min(deviceCount, 4); i++) // Test up to 4 devices
            {
                var props = new CudaDeviceProperties();
                var propResult = CudaRuntime.cudaGetDeviceProperties(ref props, i);
                
                if (propResult == CudaError.Success)
                {
                    logger.LogInformation("Device {DeviceId}: {DeviceName} (CC {Major}.{Minor})",
                        i, props.Name, props.Major, props.Minor);
                    
                    result.Details[$"Device{i}Name"] = props.Name ?? $"Unknown Device {i}";
                    result.Details[$"Device{i}ComputeCapability"] = $"{props.Major}.{props.Minor}";
                }
                else
                {
                    result.Warnings.Add($"Failed to get properties for device {i}: {CudaRuntime.GetErrorString(propResult)}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Device detection validation failed: {ex.Message}");
            logger.LogError(ex, "Device detection validation failed");
        }
    }

    private static void ValidateCudaDeviceClass(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating CudaDevice class");

        try
        {
            if (!CudaRuntime.IsCudaSupported())
            {
                result.Warnings.Add("Skipping CudaDevice validation - CUDA not available");
                return;
            }

            // Test device detection
            var devices = CudaDevice.DetectAll(logger).ToList();
            logger.LogInformation("CudaDevice.DetectAll found {DeviceCount} devices", devices.Count);

            if (devices.Count == 0)
            {
                result.Warnings.Add("CudaDevice.DetectAll returned no devices");
                return;
            }

            var device = devices[0];
            logger.LogInformation("Testing device 0: {DeviceName}", device.Name);

            // Validate RTX 2000 Ada detection
            logger.LogInformation("RTX 2000 Ada Detection: {IsRTX2000Ada}", device.IsRTX2000Ada);
            logger.LogInformation("Architecture Generation: {ArchitectureGeneration}", device.ArchitectureGeneration);

            result.Details["HasRTX2000Ada"] = device.IsRTX2000Ada;
            result.Details["FirstDeviceArchitecture"] = device.ArchitectureGeneration;

            // Test memory info retrieval
            var (freeMemory, totalMemory) = device.GetMemoryInfo();
            logger.LogInformation("Memory: {FreeMemory:N0} / {TotalMemory:N0} bytes", freeMemory, totalMemory);

            result.Details["DeviceMemoryTotal"] = totalMemory;
            result.Details["DeviceMemoryFree"] = freeMemory;

            // Test CUDA cores estimation
            var cudaCores = device.GetEstimatedCudaCores();
            logger.LogInformation("Estimated CUDA Cores: {CudaCores}", cudaCores);

            result.Details["EstimatedCudaCores"] = cudaCores;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CudaDevice class validation failed: {ex.Message}");
            logger.LogError(ex, "CudaDevice class validation failed");
        }
    }

    private static void ValidateCudaAcceleratorInitialization(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating CudaAccelerator initialization");

        try
        {
            if (!CudaRuntime.IsCudaSupported())
            {
                result.Warnings.Add("Skipping CudaAccelerator validation - CUDA not available");
                return;
            }

            // Test accelerator creation
            using var accelerator = new CudaAccelerator(0, null);
            logger.LogInformation("CudaAccelerator created successfully for device 0");

            // Validate accelerator info
            var info = accelerator.Info;
            logger.LogInformation("Accelerator Info - Name: {Name}, Type: {Type}", info.Name, info.DeviceType);

            result.Details["AcceleratorName"] = info.Name;
            result.Details["AcceleratorType"] = info.DeviceType;

            // Test device info method
            var deviceInfo = accelerator.GetDeviceInfo();
            logger.LogInformation("Device Info - RTX2000Ada: {IsRTX2000Ada}, Cores: {CudaCores}",
                deviceInfo.IsRTX2000Ada, deviceInfo.EstimatedCudaCores);

            result.Details["AcceleratorRTX2000Ada"] = deviceInfo.IsRTX2000Ada;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CudaAccelerator initialization validation failed: {ex.Message}");
            logger.LogError(ex, "CudaAccelerator initialization validation failed");
        }
    }

    private static void ValidateCudaBackendPluginRegistration(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating CudaBackendPlugin registration");

        try
        {
            // Test plugin creation
            var plugin = new CudaBackendPlugin();
            logger.LogInformation("CudaBackendPlugin created - ID: {Id}, Name: {Name}", plugin.Id, plugin.Name);

            result.Details["PluginId"] = plugin.Id;
            result.Details["PluginName"] = plugin.Name;
            result.Details["PluginVersion"] = plugin.Version.ToString();

            // Test backend factory
            var factory = new CudaBackendFactory(null);
            var isAvailable = factory.IsAvailable();
            logger.LogInformation("Backend factory availability: {IsAvailable}", isAvailable);

            result.Details["BackendFactoryAvailable"] = isAvailable;

            if (isAvailable)
            {
                var capabilities = factory.GetCapabilities();
                logger.LogInformation("Backend capabilities - Devices: {MaxDevices}, UnifiedMemory: {SupportsUnifiedMemory}",
                    capabilities.MaxDevices, capabilities.SupportsUnifiedMemory);

                result.Details["MaxDevices"] = capabilities.MaxDevices;
                result.Details["SupportsUnifiedMemory"] = capabilities.SupportsUnifiedMemory;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CudaBackendPlugin validation failed: {ex.Message}");
            logger.LogError(ex, "CudaBackendPlugin validation failed");
        }
    }

    private static void ValidateErrorHandlingAndResourceDisposal(ValidationResult result, ILogger logger)
    {
        logger.LogDebug("Validating error handling and resource disposal");

        try
        {
            // Test invalid device ID handling
            try
            {
                var invalidDevice = CudaDevice.Detect(999, logger as ILogger<CudaDevice>);
                if (invalidDevice != null)
                {
                    result.Warnings.Add("Expected CudaDevice.Detect to return null for invalid device ID");
                }
                else
                {
                    logger.LogInformation("Invalid device ID correctly handled");
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Invalid device ID properly threw exception: {Message}", ex.Message);
            }

            // Test proper disposal
            if (CudaRuntime.IsCudaSupported())
            {
                CudaAccelerator? accelerator = null;
                try
                {
                    accelerator = new CudaAccelerator(0, logger as ILogger<CudaAccelerator>);
                    logger.LogInformation("Accelerator created for disposal test");
                }
                finally
                {
                    accelerator?.Dispose();
                    logger.LogInformation("Accelerator disposed successfully");
                }
            }

            result.Details["ErrorHandlingValidated"] = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error handling and resource disposal validation failed: {ex.Message}");
            logger.LogError(ex, "Error handling validation failed");
        }
    }
}

/// <summary>
/// Results of Phase 1 validation.
/// </summary>
public class ValidationResult
{
    /// <summary>Gets or sets whether validation was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Gets the list of validation errors.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Gets the list of validation warnings.</summary>
    public List<string> Warnings { get; } = new();

    /// <summary>Gets validation details and metrics.</summary>
    public Dictionary<string, object> Details { get; } = new();

    /// <summary>
    /// Gets a summary of the validation results.
    /// </summary>
    /// <returns>A formatted summary string.</returns>
    public string GetSummary()
    {
        var summary = $"CUDA Phase 1 Validation: {(Success ? "PASSED" : "FAILED")}\n";
        summary += $"Errors: {Errors.Count}, Warnings: {Warnings.Count}\n";

        if (Errors.Count > 0)
        {
            summary += "\nErrors:\n";
            foreach (var error in Errors)
            {
                summary += $"  - {error}\n";
            }
        }

        if (Warnings.Count > 0)
        {
            summary += "\nWarnings:\n";
            foreach (var warning in Warnings)
            {
                summary += $"  - {warning}\n";
            }
        }

        if (Details.Count > 0)
        {
            summary += "\nDetails:\n";
            foreach (var detail in Details)
            {
                summary += $"  {detail.Key}: {detail.Value}\n";
            }
        }

        return summary;
    }
}}
