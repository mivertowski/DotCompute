// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using System.Text;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Provides validation and testing utilities for Metal backend.
/// </summary>
public static class MetalValidation
{
    /// <summary>
    /// Validates that the Metal backend is properly configured and functional.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Validation result with details.</returns>
    public static ValidationResult ValidateConfiguration(ILogger? logger = null)
    {
        var result = new ValidationResult();


        try
        {
            // Check platform compatibility
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                result.AddError("Metal backend is only supported on macOS");
                return result;
            }

            logger?.LogInformation("Validating Metal backend configuration...");

            // Check macOS version
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 10 || (osVersion.Major == 10 && osVersion.Minor < 13))
            {
                result.AddError($"macOS 10.13 or later required, found {osVersion}");
                return result;
            }


            result.AddInfo($"macOS version: {osVersion}");

            // Check Metal availability
            try
            {
                var isSupported = MetalNative.IsMetalSupported();
                if (!isSupported)
                {
                    result.AddError("Metal is not supported on this system");
                    return result;
                }


                result.AddInfo("Metal framework is available");
            }
            catch (DllNotFoundException ex)
            {
                result.AddError($"Native Metal library not found: {ex.Message}");
                return result;
            }
            catch (EntryPointNotFoundException ex)
            {
                result.AddError($"Native Metal function not found: {ex.Message}");
                return result;
            }

            // Check device availability
            var deviceCount = MetalNative.GetDeviceCount();
            if (deviceCount == 0)
            {
                result.AddError("No Metal devices found");
                return result;
            }


            result.AddInfo($"Found {deviceCount} Metal device(s)");

            // Validate each device
            for (var i = 0; i < deviceCount; i++)
            {
                var deviceResult = ValidateDevice(i, logger);
                result.Merge(deviceResult);
            }

            // Test basic functionality
            var functionalResult = TestBasicFunctionality(logger);
            result.Merge(functionalResult);

            if (result.IsValid)
            {
                result.AddInfo("Metal backend validation completed successfully");
                logger?.LogInformation("Metal backend validation passed");
            }
            else
            {
                logger?.LogError("Metal backend validation failed with {ErrorCount} errors", result.ErrorCount);
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Validation failed with exception: {ex.Message}");
            logger?.LogError(ex, "Metal backend validation failed");
        }

        return result;
    }

    /// <summary>
    /// Validates a specific Metal device.
    /// </summary>
    /// <param name="deviceIndex">Index of the device to validate.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Validation result for the device.</returns>
    public static ValidationResult ValidateDevice(int deviceIndex, ILogger? logger = null)
    {
        var result = new ValidationResult();
        var device = IntPtr.Zero;

        try
        {
            device = MetalNative.CreateDeviceAtIndex(deviceIndex);
            if (device == IntPtr.Zero)
            {
                result.AddError($"Failed to create device at index {deviceIndex}");
                return result;
            }

            // Get device information
            var deviceInfo = MetalNative.GetDeviceInfo(device);
            var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? "Unknown";
            var families = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";

            result.AddInfo($"Device {deviceIndex}: {deviceName}");
            result.AddInfo($"  Registry ID: {deviceInfo.RegistryID}");
            result.AddInfo($"  Unified Memory: {deviceInfo.HasUnifiedMemory}");
            result.AddInfo($"  Low Power: {deviceInfo.IsLowPower}");
            result.AddInfo($"  Max Threads per Threadgroup: {deviceInfo.MaxThreadsPerThreadgroup}");
            result.AddInfo($"  Max Buffer Length: {deviceInfo.MaxBufferLength:N0} bytes");
            result.AddInfo($"  Supported Families: {families}");

            // Validate device capabilities
            if (deviceInfo.MaxThreadgroupSize == 0)
            {
                result.AddError($"Device {deviceIndex} has no threadgroup support");
            }

            if (deviceInfo.MaxBufferLength < 1024 * 1024) // 1MB minimum
            {
                result.AddWarning($"Device {deviceIndex} has limited buffer capacity: {deviceInfo.MaxBufferLength} bytes");
            }

            // Test command queue creation
            var commandQueue = MetalNative.CreateCommandQueue(device);
            if (commandQueue == IntPtr.Zero)
            {
                result.AddError($"Failed to create command queue for device {deviceIndex}");
            }
            else
            {
                result.AddInfo($"Command queue created successfully for device {deviceIndex}");
                MetalNative.ReleaseCommandQueue(commandQueue);
            }

            logger?.LogDebug("Validated Metal device {Index}: {Name}", deviceIndex, deviceName);
        }
        catch (Exception ex)
        {
            result.AddError($"Device {deviceIndex} validation failed: {ex.Message}");
            logger?.LogError(ex, "Failed to validate Metal device {Index}", deviceIndex);
        }
        finally
        {
            if (device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        return result;
    }

    /// <summary>
    /// Tests basic Metal functionality.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Validation result for basic functionality.</returns>
    public static ValidationResult TestBasicFunctionality(ILogger? logger = null)
    {
        var result = new ValidationResult();
        var device = IntPtr.Zero;
        var commandQueue = IntPtr.Zero;
        var buffer = IntPtr.Zero;

        try
        {
            logger?.LogDebug("Testing basic Metal functionality...");

            // Create default device
            device = MetalNative.CreateSystemDefaultDevice();
            if (device == IntPtr.Zero)
            {
                result.AddError("Failed to create default Metal device");
                return result;
            }

            // Create command queue
            commandQueue = MetalNative.CreateCommandQueue(device);
            if (commandQueue == IntPtr.Zero)
            {
                result.AddError("Failed to create command queue");
                return result;
            }

            // Test buffer creation
            const int bufferSize = 1024;
            buffer = MetalNative.CreateBuffer(device, bufferSize, MetalStorageMode.Shared);
            if (buffer == IntPtr.Zero)
            {
                result.AddError("Failed to create Metal buffer");
                return result;
            }

            // Test buffer access
            var contents = MetalNative.GetBufferContents(buffer);
            if (contents == IntPtr.Zero)
            {
                result.AddError("Failed to get buffer contents");
                return result;
            }

            var length = MetalNative.GetBufferLength(buffer);
            if (length != bufferSize)
            {
                result.AddError($"Buffer length mismatch: expected {bufferSize}, got {length}");
                return result;
            }

            // Test simple shader compilation
            var compilationResult = TestShaderCompilation(device, logger);
            result.Merge(compilationResult);

            result.AddInfo("Basic Metal functionality test passed");
            logger?.LogDebug("Basic Metal functionality test completed successfully");
        }
        catch (Exception ex)
        {
            result.AddError($"Basic functionality test failed: {ex.Message}");
            logger?.LogError(ex, "Basic Metal functionality test failed");
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                MetalNative.ReleaseBuffer(buffer);
            }
            if (commandQueue != IntPtr.Zero)
            {
                MetalNative.ReleaseCommandQueue(commandQueue);
            }
            if (device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(device);
            }
        }

        return result;
    }

    /// <summary>
    /// Tests shader compilation functionality.
    /// </summary>
    /// <param name="device">Metal device handle.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>Validation result for shader compilation.</returns>
    public static ValidationResult TestShaderCompilation(IntPtr device, ILogger? logger = null)
    {
        var result = new ValidationResult();
        var library = IntPtr.Zero;
        var function = IntPtr.Zero;
        var pipelineState = IntPtr.Zero;

        try
        {
            logger?.LogDebug("Testing Metal shader compilation...");

            // Simple test kernel
            const string testKernel = @"
                #include <metal_stdlib>
                using namespace metal;
                
                kernel void test_kernel(
                    device float* input [[buffer(0)]],
                    device float* output [[buffer(1)]],
                    uint id [[thread_position_in_grid]]
                ) {
                    output[id] = input[id] * 2.0f;
                }
            ";

            // Compile shader
            library = MetalNative.CreateLibraryWithSource(device, testKernel);
            if (library == IntPtr.Zero)
            {
                result.AddError("Failed to compile test shader");
                return result;
            }

            // Get function
            function = MetalNative.GetFunction(library, "test_kernel");
            if (function == IntPtr.Zero)
            {
                result.AddError("Failed to get test kernel function");
                return result;
            }

            // Create pipeline state
            var error = IntPtr.Zero;
            pipelineState = MetalNative.CreateComputePipelineState(device, function, ref error);
            if (pipelineState == IntPtr.Zero)
            {
                var errorMsg = "Unknown error";
                if (error != IntPtr.Zero)
                {
                    errorMsg = Marshal.PtrToStringAnsi(MetalNative.GetErrorLocalizedDescription(error)) ?? "Unknown error";
                    MetalNative.ReleaseError(error);
                }
                result.AddError($"Failed to create compute pipeline state: {errorMsg}");
                return result;
            }

            // Get pipeline characteristics
            var maxThreads = MetalNative.GetMaxTotalThreadsPerThreadgroup(pipelineState);
            if (maxThreads <= 0)
            {
                result.AddWarning("Pipeline state reports zero max threads per threadgroup");
            }
            else
            {
                result.AddInfo($"Max threads per threadgroup: {maxThreads}");
            }

            result.AddInfo("Shader compilation test passed");
            logger?.LogDebug("Metal shader compilation test completed successfully");
        }
        catch (Exception ex)
        {
            result.AddError($"Shader compilation test failed: {ex.Message}");
            logger?.LogError(ex, "Metal shader compilation test failed");
        }
        finally
        {
            if (pipelineState != IntPtr.Zero)
            {
                MetalNative.ReleaseComputePipelineState(pipelineState);
            }
            if (function != IntPtr.Zero)
            {
                MetalNative.ReleaseFunction(function);
            }
            if (library != IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
            }
        }

        return result;
    }
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _info = [];

    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Gets the number of errors.
    /// </summary>
    public int ErrorCount => _errors.Count;

    /// <summary>
    /// Gets the number of warnings.
    /// </summary>
    public int WarningCount => _warnings.Count;

    /// <summary>
    /// Gets all errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Gets all warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

    /// <summary>
    /// Gets all informational messages.
    /// </summary>
    public IReadOnlyList<string> Info => _info.AsReadOnly();

    /// <summary>
    /// Adds an error message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public void AddError(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _errors.Add(message);
        }
    }

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    /// <param name="message">The warning message.</param>
    public void AddWarning(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _warnings.Add(message);
        }
    }

    /// <summary>
    /// Adds an informational message.
    /// </summary>
    /// <param name="message">The info message.</param>
    public void AddInfo(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            _info.Add(message);
        }
    }

    /// <summary>
    /// Merges another validation result into this one.
    /// </summary>
    /// <param name="other">The validation result to merge.</param>
    public void Merge(ValidationResult other)
    {
        if (other == null)
        {
            return;
        }

        _errors.AddRange(other._errors);
        _warnings.AddRange(other._warnings);
        _info.AddRange(other._info);
    }

    /// <summary>
    /// Generates a formatted report of the validation results.
    /// </summary>
    /// <returns>A formatted validation report.</returns>
    public string GenerateReport()
    {
        var report = new StringBuilder();


        _ = report.AppendLine("Metal Backend Validation Report");
        _ = report.AppendLine(new string('=', 40));
        _ = report.AppendLine();

        _ = report.AppendLine($"Status: {(IsValid ? "PASSED" : "FAILED")}");
        _ = report.AppendLine($"Errors: {ErrorCount}");
        _ = report.AppendLine($"Warnings: {WarningCount}");
        _ = report.AppendLine();

        if (_errors.Count > 0)
        {
            _ = report.AppendLine("ERRORS:");
            foreach (var error in _errors)
            {
                _ = report.AppendLine($"  ❌ {error}");
            }
            _ = report.AppendLine();
        }

        if (_warnings.Count > 0)
        {
            _ = report.AppendLine("WARNINGS:");
            foreach (var warning in _warnings)
            {
                _ = report.AppendLine($"  ⚠️  {warning}");
            }
            _ = report.AppendLine();
        }

        if (_info.Count > 0)
        {
            _ = report.AppendLine("INFORMATION:");
            foreach (var info in _info)
            {
                _ = report.AppendLine($"  ℹ️  {info}");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Returns a string representation of the validation result.
    /// </summary>
    public override string ToString() => $"ValidationResult: {(IsValid ? "Valid" : "Invalid")} - Errors: {ErrorCount}, Warnings: {WarningCount}";
}