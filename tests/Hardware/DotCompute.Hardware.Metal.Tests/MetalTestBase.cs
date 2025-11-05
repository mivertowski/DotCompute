// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Abstractions;
using DotCompute.Backends.Metal.Factory;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Base class for Metal hardware tests.
/// Provides common infrastructure for Metal test classes including device detection and factory access.
/// </summary>
public abstract class MetalTestBase : IDisposable
{
    /// <summary>
    /// Test output helper for logging test results
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Metal backend factory for creating accelerators
    /// </summary>
    protected MetalBackendFactory Factory { get; }

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MetalTestBase class
    /// </summary>
    /// <param name="output">Test output helper</param>
    protected MetalTestBase(ITestOutputHelper output)
    {
        Output = output ?? throw new ArgumentNullException(nameof(output));
        Factory = new MetalBackendFactory();
    }

    /// <summary>
    /// Checks if Metal hardware is available on this system
    /// </summary>
    /// <returns>True if Metal is available, false otherwise</returns>
    protected bool IsMetalAvailable()
    {
        try
        {
            // Metal is only available on macOS
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Output.WriteLine("Metal not available: Not running on macOS");
                return false;
            }

            // Check if we can detect Metal devices
            var deviceCount = Factory.GetAvailableDeviceCount();
            if (deviceCount == 0)
            {
                Output.WriteLine("Metal not available: No Metal devices found");
                return false;
            }

            Output.WriteLine($"Metal available: {deviceCount} device(s) detected");
            return true;
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Metal not available: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if the system is running on Apple Silicon (ARM64)
    /// </summary>
    /// <returns>True if running on Apple Silicon, false otherwise</returns>
    protected bool IsAppleSilicon()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
               RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
    }

    /// <summary>
    /// Checks if the system is running on Intel Mac (x64)
    /// </summary>
    /// <returns>True if running on Intel Mac, false otherwise</returns>
    protected bool IsIntelMac()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
               RuntimeInformation.ProcessArchitecture == Architecture.X64;
    }

    /// <summary>
    /// Gets the macOS version
    /// </summary>
    /// <returns>macOS version string or empty if not running on macOS</returns>
    protected string GetMacOSVersion()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Unknown";
        }

        return Environment.OSVersion.Version.ToString();
    }

    /// <summary>
    /// Gets a description of the system architecture
    /// </summary>
    /// <returns>System architecture description</returns>
    protected string GetSystemArchitecture()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSDescription;
        }

        var arch = IsAppleSilicon() ? "Apple Silicon (ARM64)" :
                   IsIntelMac() ? "Intel (x64)" :
                   RuntimeInformation.ProcessArchitecture.ToString();

        return $"macOS {GetMacOSVersion()} on {arch}";
    }

    /// <summary>
    /// Gets a string description of the Metal device information
    /// </summary>
    /// <returns>Metal device info string or empty if not available</returns>
    protected async Task<string> GetMetalDeviceInfoStringAsync()
    {
        try
        {
            if (!IsMetalAvailable())
            {
                return "Metal not available";
            }

            await using var accelerator = Factory.CreateProductionAccelerator();
            if (accelerator == null)
            {
                return "Unable to create Metal accelerator";
            }

            return accelerator.Info.Name ?? "Unknown Metal Device";
        }
        catch (Exception ex)
        {
            return $"Error detecting Metal device: {ex.Message}";
        }
    }

    /// <summary>
    /// Logs Metal device capabilities to test output
    /// </summary>
    protected async Task LogMetalDeviceCapabilitiesAsync()
    {
        try
        {
            if (!IsMetalAvailable())
            {
                Output.WriteLine("Metal not available - cannot log capabilities");
                return;
            }

            await using var accelerator = Factory.CreateProductionAccelerator();
            if (accelerator == null)
            {
                Output.WriteLine("Unable to create Metal accelerator");
                return;
            }

            Output.WriteLine("=== Metal Device Capabilities ===");
            Output.WriteLine($"Name: {accelerator.Name}");
            Output.WriteLine($"Type: {accelerator.AcceleratorType}");
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Error logging Metal capabilities: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes of resources used by the test base
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Factory doesn't implement IDisposable, no cleanup needed
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Disposes of resources used by the test base
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
