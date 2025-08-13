// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Interface for hardware abstraction providers.
/// Allows tests to run with either real hardware or mock implementations.
/// </summary>
public interface IHardwareProvider : IDisposable
{
    /// <summary>
    /// Gets whether this provider represents real hardware or simulated hardware.
    /// </summary>
    bool IsRealHardware { get; }

    /// <summary>
    /// Gets all available devices of the specified type.
    /// </summary>
    /// <param name="type">The accelerator type to query for.</param>
    /// <returns>An enumerable of available devices.</returns>
    IEnumerable<IHardwareDevice> GetDevices(AcceleratorType type);

    /// <summary>
    /// Gets all available devices across all types.
    /// </summary>
    /// <returns>An enumerable of all available devices.</returns>
    IEnumerable<IHardwareDevice> GetAllDevices();

    /// <summary>
    /// Gets the first available device of the specified type.
    /// </summary>
    /// <param name="type">The accelerator type to find.</param>
    /// <returns>The first device of the specified type, or null if none found.</returns>
    IHardwareDevice? GetFirstDevice(AcceleratorType type);

    /// <summary>
    /// Creates an accelerator instance for the specified device.
    /// </summary>
    /// <param name="device">The hardware device to create an accelerator for.</param>
    /// <returns>An accelerator instance wrapping the device.</returns>
    IAccelerator CreateAccelerator(IHardwareDevice device);

    /// <summary>
    /// Checks if the specified accelerator type is supported.
    /// </summary>
    /// <param name="type">The accelerator type to check.</param>
    /// <returns>True if supported, false otherwise.</returns>
    bool IsSupported(AcceleratorType type);

    /// <summary>
    /// Gets hardware diagnostics information.
    /// </summary>
    /// <returns>A dictionary containing diagnostic information.</returns>
    Dictionary<string, object> GetDiagnostics();
}

/// <summary>
/// Interface representing a hardware device abstraction.
/// </summary>
public interface IHardwareDevice
{
    /// <summary>
    /// Gets the unique identifier for this device.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the friendly name of the device.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the accelerator type for this device.
    /// </summary>
    AcceleratorType Type { get; }

    /// <summary>
    /// Gets the device vendor.
    /// </summary>
    string Vendor { get; }

    /// <summary>
    /// Gets the driver version.
    /// </summary>
    string DriverVersion { get; }

    /// <summary>
    /// Gets the total memory size in bytes.
    /// </summary>
    long TotalMemory { get; }

    /// <summary>
    /// Gets the available memory size in bytes.
    /// </summary>
    long AvailableMemory { get; }

    /// <summary>
    /// Gets the maximum threads per block/work group.
    /// </summary>
    int MaxThreadsPerBlock { get; }

    /// <summary>
    /// Gets the number of compute units/streaming multiprocessors.
    /// </summary>
    int ComputeUnits { get; }

    /// <summary>
    /// Gets whether the device is currently available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets device-specific capabilities.
    /// </summary>
    Dictionary<string, object> Capabilities { get; }

    /// <summary>
    /// Converts this hardware device to an AcceleratorInfo instance.
    /// </summary>
    /// <returns>An AcceleratorInfo representing this device.</returns>
    AcceleratorInfo ToAcceleratorInfo();

    /// <summary>
    /// Gets detailed device properties.
    /// </summary>
    /// <returns>A dictionary of device properties.</returns>
    Dictionary<string, object> GetProperties();

    /// <summary>
    /// Performs a health check on the device.
    /// </summary>
    /// <returns>True if the device is healthy, false otherwise.</returns>
    bool HealthCheck();
}