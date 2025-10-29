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
                var devices = deviceManager.GetAvailableDevices();
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
            return deviceManager.GetAvailableDevices().Count;
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
            var devices = deviceManager.GetAvailableDevices();
            return devices.Any(d => d.Type == Backends.OpenCL.Types.Native.OpenCLTypes.DeviceType.GPU);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets information about available OpenCL devices.
    /// </summary>
    /// <returns>List of device information strings.</returns>
    public static List<string> GetDeviceInfo()
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
            var devices = deviceManager.GetAvailableDevices();

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
}
