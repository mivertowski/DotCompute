// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL;
using DotCompute.Backends.OpenCL.DeviceManagement;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace DotCompute.Hardware.OpenCL.Tests.Helpers;

/// <summary>
/// Base class for OpenCL hardware tests providing common utilities and device detection.
/// </summary>
public abstract class OpenCLTestBase : IDisposable
{
    /// <summary>
    /// Gets the test output helper.
    /// </summary>
    protected ITestOutputHelper Output { get; }

    /// <summary>
    /// Gets the logger factory.
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Gets a value indicating whether OpenCL is available on this system.
    /// </summary>
    protected static bool IsOpenCLAvailable => OpenCLDetection.IsAvailable();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenCLTestBase"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    protected OpenCLTestBase(ITestOutputHelper output)
    {
        Output = output;
        LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
    }

    /// <summary>
    /// Creates an OpenCL accelerator for testing.
    /// </summary>
    /// <param name="deviceIndex">The device index to use (default: 0).</param>
    /// <returns>A new OpenCL accelerator instance.</returns>
    protected OpenCLAccelerator CreateAccelerator(int deviceIndex = 0)
    {
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.GetAvailableDevices();

        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No OpenCL devices available");
        }

        if (deviceIndex >= devices.Count)
        {
            deviceIndex = 0;
            Output.WriteLine($"Device index out of range, using device 0 instead");
        }

        var device = devices[deviceIndex];
        Output.WriteLine($"Using OpenCL device: {device.Name} ({device.Vendor})");
        Output.WriteLine($"  Type: {device.Type}");
        Output.WriteLine($"  Compute Units: {device.MaxComputeUnits}");
        Output.WriteLine($"  Global Memory: {device.GlobalMemorySize / (1024.0 * 1024.0):F0} MB");
        Output.WriteLine($"  Max Work Group Size: {device.MaxWorkGroupSize}");

        return new OpenCLAccelerator(device, LoggerFactory);
    }

    /// <summary>
    /// Gets the first available GPU device or null if not available.
    /// </summary>
    /// <returns>GPU accelerator or null.</returns>
    protected OpenCLAccelerator? TryCreateGpuAccelerator()
    {
        var deviceManager = new OpenCLDeviceManager(LoggerFactory.CreateLogger<OpenCLDeviceManager>());
        var devices = deviceManager.GetAvailableDevices();

        var gpuDevice = devices.FirstOrDefault(d => d.Type == Backends.OpenCL.Types.Native.OpenCLTypes.DeviceType.GPU);
        if (gpuDevice == null)
        {
            Output.WriteLine("No GPU device available");
            return null;
        }

        Output.WriteLine($"Using GPU device: {gpuDevice.Name} ({gpuDevice.Vendor})");
        return new OpenCLAccelerator(gpuDevice, LoggerFactory);
    }

    /// <summary>
    /// Disposes resources used by the test base.
    /// </summary>
    public virtual void Dispose()
    {
        LoggerFactory?.Dispose();
        GC.SuppressFinalize(this);
    }
}
