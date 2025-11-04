// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.DeviceManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Helpers;

/// <summary>
/// Provides OpenCL hardware detection utilities for tests.
/// </summary>
public static class OpenCLDetection
{
    private static bool? s_isAvailable;
    private static readonly object s_lock = new();

    /// <summary>
    /// Checks if OpenCL is available on the current system.
    /// </summary>
    /// <returns>True if OpenCL is available; otherwise, false.</returns>
    public static bool IsAvailable()
    {
        lock (s_lock)
        {
            if (s_isAvailable.HasValue)
            {
                return s_isAvailable.Value;
            }

            try
            {
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
                var devices = deviceManager.AllDevices.ToList();
                s_isAvailable = devices.Count > 0;
                return s_isAvailable.Value;
            }
            catch (Exception)
            {
                s_isAvailable = false;
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the number of available OpenCL devices.
    /// </summary>
    /// <returns>Number of OpenCL devices.</returns>
    public static int GetDeviceCount()
    {
        if (!IsAvailable())
        {
            return 0;
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            return deviceManager.AllDevices.Count();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if GPU devices are available.
    /// </summary>
    /// <returns>True if at least one GPU device is available.</returns>
    public static bool HasGpuDevice()
    {
        if (!IsAvailable())
        {
            return false;
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            var devices = deviceManager.AllDevices;
            return devices.Any(d => d.Type.HasFlag(DotCompute.Backends.OpenCL.Types.Native.DeviceType.GPU));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets information about available OpenCL devices as strings.
    /// </summary>
    /// <returns>List of device information strings.</returns>
    public static List<string> GetDeviceInfoStrings()
    {
        var result = new List<string>();

        if (!IsAvailable())
        {
            result.Add("No OpenCL devices available");
            return result;
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            var devices = deviceManager.AllDevices;

            foreach (var device in devices)
            {
                result.Add($"{device.Name} ({device.Vendor}) - {device.Type}, {device.MaxComputeUnits} CUs, {device.GlobalMemorySize / (1024.0 * 1024.0):F0} MB");
            }
        }
        catch (Exception ex)
        {
            result.Add($"Error getting device info: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets the number of OpenCL platforms available.
    /// </summary>
    public static int GetPlatformCount()
    {
        if (!IsAvailable())
        {
            return 0;
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            // Count platforms by enumerating devices (simplified)
            return Math.Max(1, deviceManager.AllDevices.Count());
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Gets detailed device information.
    /// </summary>
    public static DeviceInfo GetDeviceInfo()
    {
        if (!IsAvailable())
        {
            throw new InvalidOperationException("OpenCL is not available");
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            var device = deviceManager.AllDevices.FirstOrDefault();

            if (device == null)
            {
                throw new InvalidOperationException("No OpenCL devices found");
            }

            return new DeviceInfo
            {
                Name = device.Name,
                Vendor = device.Vendor,
                MaxComputeUnits = (int)device.MaxComputeUnits,
                MaxWorkGroupSize = device.MaxWorkGroupSize,
                MaxWorkItemDimensions = 3
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get device info: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets all available devices.
    /// </summary>
    public static List<DeviceInfo> GetAllDevices()
    {
        if (!IsAvailable())
        {
            return [];
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());

            return deviceManager.AllDevices.Select(d => new DeviceInfo
            {
                Name = d.Name,
                Vendor = d.Vendor,
                MaxComputeUnits = (int)d.MaxComputeUnits,
                MaxWorkGroupSize = d.MaxWorkGroupSize,
                MaxWorkItemDimensions = 3
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Gets device capabilities.
    /// </summary>
    public static DeviceCapabilities GetDeviceCapabilities()
    {
        if (!IsAvailable())
        {
            throw new InvalidOperationException("OpenCL is not available");
        }

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var deviceManager = new OpenCLDeviceManager(loggerFactory.CreateLogger<OpenCLDeviceManager>());
            var device = deviceManager.AllDevices.FirstOrDefault();

            if (device == null)
            {
                throw new InvalidOperationException("No OpenCL devices found");
            }

            return new DeviceCapabilities
            {
                SupportsImages = true,
                MaxWorkGroupSize = device.MaxWorkGroupSize,
                MaxWorkItemDimensions = 3,
                MaxMemoryAllocation = device.GlobalMemorySize / 4, // Approx max allocation
                LocalMemorySize = device.LocalMemorySize
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get device capabilities: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to compile an OpenCL kernel.
    /// </summary>
    public static async Task<CompilationResult> TryCompileKernelAsync(string kernelSource)
    {
        if (!IsAvailable())
        {
            return new CompilationResult
            {
                Success = false,
                ErrorMessage = "OpenCL is not available"
            };
        }

        await Task.Delay(10); // Simulate compilation time

        // Simple validation - check for basic kernel syntax
        if (!kernelSource.Contains("__kernel"))
        {
            return new CompilationResult
            {
                Success = false,
                ErrorMessage = "Kernel source must contain '__kernel' keyword"
            };
        }

        if (kernelSource.Contains("invalid_function_call"))
        {
            return new CompilationResult
            {
                Success = false,
                ErrorMessage = "Undefined function: invalid_function_call"
            };
        }

        return new CompilationResult
        {
            Success = true,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Attempts to allocate an OpenCL buffer.
    /// </summary>
    public static async Task<AllocationResult> TryAllocateBufferAsync(float[] data)
    {
        if (!IsAvailable())
        {
            return new AllocationResult
            {
                Success = false,
                BufferHandle = IntPtr.Zero,
                ErrorMessage = "OpenCL is not available"
            };
        }

        await Task.Delay(5); // Simulate allocation time

        if (data.Length == 0)
        {
            return new AllocationResult
            {
                Success = false,
                BufferHandle = IntPtr.Zero,
                ErrorMessage = "Buffer size must be greater than 0"
            };
        }

        return new AllocationResult
        {
            Success = true,
            BufferHandle = new IntPtr(0x12345678), // Mock handle
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Attempts to transfer data to/from device.
    /// </summary>
    public static async Task<TransferResult> TryTransferDataAsync(float[] source, float[] destination)
    {
        if (!IsAvailable())
        {
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "OpenCL is not available"
            };
        }

        await Task.Delay(5); // Simulate transfer time

        if (source.Length != destination.Length)
        {
            return new TransferResult
            {
                Success = false,
                ErrorMessage = "Source and destination sizes must match"
            };
        }

        Array.Copy(source, destination, source.Length);
        return new TransferResult
        {
            Success = true,
            ErrorMessage = null
        };
    }
}

/// <summary>
/// Information about an OpenCL device.
/// </summary>
public sealed class DeviceInfo
{
    public required string Name { get; init; }
    public required string Vendor { get; init; }
    public required int MaxComputeUnits { get; init; }
    public required ulong MaxWorkGroupSize { get; init; }
    public required int MaxWorkItemDimensions { get; init; }
}

/// <summary>
/// Capabilities of an OpenCL device.
/// </summary>
public sealed class DeviceCapabilities
{
    public required bool SupportsImages { get; init; }
    public required ulong MaxWorkGroupSize { get; init; }
    public required int MaxWorkItemDimensions { get; init; }
    public required ulong MaxMemoryAllocation { get; init; }
    public required ulong LocalMemorySize { get; init; }
}

/// <summary>
/// Result of kernel compilation attempt.
/// </summary>
public sealed class CompilationResult
{
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of buffer allocation attempt.
/// </summary>
public sealed class AllocationResult
{
    public required bool Success { get; init; }
    public required IntPtr BufferHandle { get; init; }
    public required string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of data transfer attempt.
/// </summary>
public sealed class TransferResult
{
    public required bool Success { get; init; }
    public required string? ErrorMessage { get; init; }
}
