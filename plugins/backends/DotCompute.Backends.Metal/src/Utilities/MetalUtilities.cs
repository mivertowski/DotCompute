// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using DotCompute.Backends.Metal.Native;

namespace DotCompute.Backends.Metal.Utilities;

/// <summary>
/// Utility functions for Metal backend operations.
/// </summary>
public static class MetalUtilities
{
    /// <summary>
    /// Checks if Metal is available on the current system.
    /// </summary>
    public static bool IsMetalAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return false;
        }

        try
        {
            var device = MetalNative.CreateSystemDefaultDevice();
            if (device != IntPtr.Zero)
            {
                MetalNative.ReleaseDevice(device);
                return true;
            }
        }
        catch
        {
            // Metal is not available
        }

        return false;
    }

    /// <summary>
    /// Gets the number of available Metal devices.
    /// </summary>
    public static int GetDeviceCount()
    {
        if (!IsMetalAvailable())
        {
            return 0;
        }

        return MetalNative.GetDeviceCount();
    }

    /// <summary>
    /// Gets information about all available Metal devices.
    /// </summary>
    public static MetalDeviceInfoWrapper[] GetAllDevices()
    {
        var count = GetDeviceCount();
        var devices = new MetalDeviceInfoWrapper[count];

        for (int i = 0; i < count; i++)
        {
            var device = MetalNative.CreateDeviceAtIndex(i);
            if (device != IntPtr.Zero)
            {
                try
                {
                    var info = MetalNative.GetDeviceInfo(device);
                    devices[i] = new MetalDeviceInfoWrapper
                    {
                        Index = i,
                        Name = Marshal.PtrToStringAnsi(info.Name) ?? "Unknown",
                        RegistryID = info.RegistryID,
                        HasUnifiedMemory = info.HasUnifiedMemory,
                        IsLowPower = info.IsLowPower,
                        IsRemovable = info.IsRemovable,
                        MaxThreadgroupSize = (long)info.MaxThreadgroupSize,
                        MaxBufferLength = (long)info.MaxBufferLength,
                        RecommendedMaxWorkingSetSize = (long)info.RecommendedMaxWorkingSetSize
                    };
                }
                finally
                {
                    MetalNative.ReleaseDevice(device);
                }
            }
        }

        return devices;
    }

    /// <summary>
    /// Formats a byte size into a human-readable string.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// Wrapper for Metal device information.
/// </summary>
public class MetalDeviceInfoWrapper
{
    /// <summary>
    /// Device index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Device name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Registry ID.
    /// </summary>
    public ulong RegistryID { get; set; }

    /// <summary>
    /// Whether the device has unified memory.
    /// </summary>
    public bool HasUnifiedMemory { get; set; }

    /// <summary>
    /// Whether the device is low power.
    /// </summary>
    public bool IsLowPower { get; set; }

    /// <summary>
    /// Whether the device is removable.
    /// </summary>
    public bool IsRemovable { get; set; }

    /// <summary>
    /// Maximum threadgroup size.
    /// </summary>
    public long MaxThreadgroupSize { get; set; }

    /// <summary>
    /// Maximum buffer length.
    /// </summary>
    public long MaxBufferLength { get; set; }

    /// <summary>
    /// Recommended maximum working set size.
    /// </summary>
    public long RecommendedMaxWorkingSetSize { get; set; }

    /// <summary>
    /// Gets a string representation of the device info.
    /// </summary>
    public override string ToString()
    {
        return $"{Name} (ID: {RegistryID:X})" +
               $"\n  Type: {(IsLowPower ? "Integrated" : IsRemovable ? "External" : "Discrete")}" +
               $"\n  Memory: {(HasUnifiedMemory ? "Unified" : "Dedicated")} - {MetalUtilities.FormatBytes(MaxBufferLength)}" +
               $"\n  Max Threadgroup: {MaxThreadgroupSize}";
    }
}
