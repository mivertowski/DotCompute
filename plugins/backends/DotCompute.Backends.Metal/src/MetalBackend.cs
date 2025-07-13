// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Accelerators;
using DotCompute.Backends.Metal.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal;

/// <summary>
/// Main entry point for Metal compute backend
/// </summary>
public sealed class MetalBackend : IDisposable
{
    private readonly ILogger<MetalBackend> _logger;
    private readonly List<MetalAccelerator> _accelerators = new();
    private bool _disposed;

    public MetalBackend(ILogger<MetalBackend> logger)
    {
        _logger = logger;
        DiscoverAccelerators();
    }

    /// <summary>
    /// Check if Metal is available on this platform
    /// </summary>
    public static bool IsAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        try
        {
            // This would call native Metal detection
            return MetalNative.IsMetalSupported();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get all available Metal accelerators
    /// </summary>
    public IReadOnlyList<MetalAccelerator> GetAccelerators() => _accelerators.AsReadOnly();

    /// <summary>
    /// Get default Metal accelerator
    /// </summary>
    public MetalAccelerator? GetDefaultAccelerator() => _accelerators.FirstOrDefault();

    private void DiscoverAccelerators()
    {
        if (!IsAvailable())
        {
            _logger.LogWarning("Metal is not available on this platform");
            return;
        }

        try
        {
            _logger.LogInformation("Discovering Metal devices...");

            // 1. Enumerate Metal devices using MTLCopyAllDevices
            var deviceCount = MetalNative.GetDeviceCount();
            if (deviceCount == 0)
            {
                _logger.LogInformation("No Metal devices found");
                return;
            }

            _logger.LogInformation("Found {DeviceCount} Metal device(s)", deviceCount);

            // 2. Query GPU families and feature sets for each device
            for (int deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                try
                {
                    var device = MetalNative.CreateDeviceAtIndex(deviceIndex);
                    if (device == IntPtr.Zero)
                    {
                        _logger.LogWarning("Failed to create Metal device at index {DeviceIndex}", deviceIndex);
                        continue;
                    }

                    if (ValidateMetalDevice(device, deviceIndex))
                    {
                        var accelerator = CreateMetalAccelerator(device, deviceIndex);
                        if (accelerator != null)
                        {
                            _accelerators.Add(accelerator);
                            LogMetalDeviceCapabilities(accelerator);
                        }
                    }
                    else
                    {
                        MetalNative.ReleaseDevice(device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to initialize Metal device {DeviceIndex}", deviceIndex);
                }
            }

            _logger.LogInformation("Metal device discovery completed - {AcceleratorCount} accelerators available", _accelerators.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Metal accelerators");
        }
    }

    private bool ValidateMetalDevice(IntPtr device, int deviceIndex)
    {
        try
        {
            // 3. Check macOS version compatibility
            var osVersion = Environment.OSVersion.Version;
            if (osVersion.Major < 11) // macOS 11.0 (Big Sur) minimum for modern Metal
            {
                _logger.LogWarning("Metal device {DeviceIndex} requires macOS 11.0 or higher. Current: {OSVersion}",
                    deviceIndex, osVersion);
                return false;
            }

            // Get device info for validation
            var deviceInfo = MetalNative.GetDeviceInfo(device);
            
            // 4. Validate compute support
            if (!ValidateComputeSupport(deviceInfo, deviceIndex))
            {
                return false;
            }

            // 5. Test shader compilation capability
            if (!TestShaderCompilation(device, deviceIndex))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating Metal device {DeviceIndex}", deviceIndex);
            return false;
        }
    }

    private bool ValidateComputeSupport(MetalDeviceInfo deviceInfo, int deviceIndex)
    {
        try
        {
            // Check if device supports compute operations
            if (deviceInfo.MaxThreadgroupSize == 0)
            {
                _logger.LogWarning("Metal device {DeviceIndex} does not support compute operations", deviceIndex);
                return false;
            }

            // Verify minimum capability requirements
            var familyString = Marshal.PtrToStringAnsi(deviceInfo.SupportedFamilies) ?? "";
            
            // Require at least Mac2 (Intel) or Apple4 (Apple Silicon) family support
            bool hasMinimumCapability = familyString.Contains("Mac2") || 
                                      familyString.Contains("Apple") ||
                                      familyString.Contains("Common");

            if (!hasMinimumCapability)
            {
                _logger.LogWarning("Metal device {DeviceIndex} does not meet minimum GPU family requirements. Families: {Families}",
                    deviceIndex, familyString);
                return false;
            }

            // Check memory constraints
            if (deviceInfo.MaxBufferLength < 1024 * 1024) // At least 1MB
            {
                _logger.LogWarning("Metal device {DeviceIndex} has insufficient memory capacity: {MaxBuffer} bytes",
                    deviceIndex, deviceInfo.MaxBufferLength);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating compute support for Metal device {DeviceIndex}", deviceIndex);
            return false;
        }
    }

    private bool TestShaderCompilation(IntPtr device, int deviceIndex)
    {
        try
        {
            // Test basic compute shader compilation
            var testShaderSource = @"
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

            // Attempt to compile test shader
            var library = MetalNative.CreateLibraryWithSource(device, testShaderSource);
            if (library == IntPtr.Zero)
            {
                _logger.LogWarning("Metal device {DeviceIndex} failed shader compilation test", deviceIndex);
                return false;
            }

            // Check if we can create a compute pipeline
            var function = MetalNative.GetFunction(library, "test_kernel");
            if (function == IntPtr.Zero)
            {
                MetalNative.ReleaseLibrary(library);
                _logger.LogWarning("Metal device {DeviceIndex} failed to find test kernel function", deviceIndex);
                return false;
            }

            var pipeline = MetalNative.CreateComputePipelineState(device, function);
            var success = pipeline != IntPtr.Zero;

            // Cleanup
            if (pipeline != IntPtr.Zero)
                MetalNative.ReleaseComputePipelineState(pipeline);
            MetalNative.ReleaseFunction(function);
            MetalNative.ReleaseLibrary(library);

            if (!success)
            {
                _logger.LogWarning("Metal device {DeviceIndex} failed to create compute pipeline state", deviceIndex);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error testing shader compilation for Metal device {DeviceIndex}", deviceIndex);
            return false;
        }
    }

    private MetalAccelerator? CreateMetalAccelerator(IntPtr device, int deviceIndex)
    {
        try
        {
            var deviceInfo = MetalNative.GetDeviceInfo(device);
            var deviceName = Marshal.PtrToStringAnsi(deviceInfo.Name) ?? $"Metal Device {deviceIndex}";
            
            _logger.LogDebug("Creating Metal accelerator for device: {DeviceName}", deviceName);
            
            // Create accelerator with the device
            // Note: We'll need to modify MetalAccelerator constructor to accept an existing device
            // For now, we'll create using the default constructor and the system will pick it up
            var options = Microsoft.Extensions.Options.Options.Create(new MetalAcceleratorOptions());
            return new MetalAccelerator(options, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Metal accelerator for device {DeviceIndex}", deviceIndex);
            return null;
        }
    }

    private void LogMetalDeviceCapabilities(MetalAccelerator accelerator)
    {
        var info = accelerator.Info;
        var capabilities = info.Capabilities;
        
        _logger.LogInformation("Metal Device: {Name} (ID: {Id})", info.Name, info.Id);
        _logger.LogInformation("  Device Type: {DeviceType}", info.DeviceType);
        _logger.LogInformation("  Compute Capability: {ComputeCapability}", info.ComputeCapability);
        _logger.LogInformation("  Total Memory: {TotalMemory:N0} bytes ({MemoryGB:F1} GB)", 
            info.TotalMemory, info.TotalMemory / (1024.0 * 1024 * 1024));
        _logger.LogInformation("  Compute Units: {ComputeUnits}", info.ComputeUnits);
        
        if (capabilities.TryGetValue("MaxThreadgroupSize", out var maxThreadgroup))
            _logger.LogInformation("  Max Threadgroup Size: {MaxThreadgroup}", maxThreadgroup);
        
        if (capabilities.TryGetValue("MaxThreadsPerThreadgroup", out var maxThreadsPerGroup))
            _logger.LogInformation("  Max Threads per Threadgroup: {MaxThreads}", maxThreadsPerGroup);
            
        if (capabilities.TryGetValue("UnifiedMemory", out var unified) && (bool)unified)
            _logger.LogInformation("  Unified Memory: Supported");
            
        if (capabilities.TryGetValue("SupportsFamily", out var families))
            _logger.LogInformation("  GPU Families: {Families}", families);
            
        if (capabilities.TryGetValue("Location", out var location))
            _logger.LogInformation("  Location: {Location}", location);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var accelerator in _accelerators)
        {
            accelerator?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        
        _accelerators.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}