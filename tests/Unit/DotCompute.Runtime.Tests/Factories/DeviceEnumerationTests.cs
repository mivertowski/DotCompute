// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Runtime.Tests.Factories;

/// <summary>
/// Unit tests for device enumeration functionality.
/// Tests the fix for the zero devices bug.
/// </summary>
public class DeviceEnumerationTests
{
    private readonly ITestOutputHelper _output;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUnifiedAcceleratorFactory _factory;

    public DeviceEnumerationTests(ITestOutputHelper output)
    {
        _output = output;

        // Setup DI container with logging
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.Configure<DotComputeRuntimeOptions>(options =>
        {
            options.ValidateCapabilities = false;
            options.AcceleratorLifetime = Configuration.ServiceLifetime.Transient;
        });
        services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = _serviceProvider.GetRequiredService<IUnifiedAcceleratorFactory>();
    }

    [Fact(DisplayName = "GetAvailableDevicesAsync returns non-empty list")]
    public async Task GetAvailableDevicesAsync_ReturnsNonEmptyList()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        Assert.NotNull(devices);
        Assert.NotEmpty(devices);

        _output.WriteLine($"Found {devices.Count} device(s):");
        foreach (var device in devices)
        {
            _output.WriteLine($"  - {device.Name} ({device.DeviceType})");
        }
    }

    [Fact(DisplayName = "CPU device is always present")]
    public async Task GetAvailableDevicesAsync_AlwaysReturnsCpuDevice()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        var cpuDevice = devices.FirstOrDefault(d => d.DeviceType == "CPU");
        Assert.NotNull(cpuDevice);
        Assert.Contains("CPU", cpuDevice.Name);
        Assert.True(cpuDevice.IsUnifiedMemory, "CPU should have unified memory");
        Assert.True(cpuDevice.SupportsFloat64, "CPU should support double precision");
        Assert.True(cpuDevice.SupportsInt64, "CPU should support 64-bit integers");

        _output.WriteLine($"CPU Device: {cpuDevice.Name}");
        _output.WriteLine($"  Cores: {cpuDevice.ComputeUnits}");
        _output.WriteLine($"  Memory: {cpuDevice.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
    }

    [Fact(DisplayName = "CUDA devices detected when available", Skip = "Requires NVIDIA GPU")]
    public async Task GetAvailableDevicesAsync_DetectsCudaDevices_WhenAvailable()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        var cudaDevices = devices.Where(d => d.DeviceType == "CUDA").ToList();
        if (cudaDevices.Any())
        {
            _output.WriteLine($"Found {cudaDevices.Count} CUDA device(s):");
            foreach (var device in cudaDevices)
            {
                _output.WriteLine($"  - {device.Name}");
                _output.WriteLine($"    Compute Capability: {device.ComputeCapability}");
                _output.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
                _output.WriteLine($"    SMs: {device.ComputeUnits}");

                Assert.Equal("NVIDIA", device.Vendor);
                Assert.NotNull(device.ComputeCapability);
                Assert.True(device.TotalMemory > 0);
                Assert.True(device.MaxThreadsPerBlock > 0);
            }
        }
        else
        {
            _output.WriteLine("No CUDA devices found (expected on macOS without eGPU)");
        }
    }

    [Fact(DisplayName = "OpenCL devices detected when available")]
    public async Task GetAvailableDevicesAsync_DetectsOpenCLDevices_WhenAvailable()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        var openclDevices = devices.Where(d => d.DeviceType == "OpenCL").ToList();
        _output.WriteLine($"Found {openclDevices.Count} OpenCL device(s):");

        foreach (var device in openclDevices)
        {
            _output.WriteLine($"  - {device.Name} ({device.Vendor})");
            _output.WriteLine($"    Compute Units: {device.MaxComputeUnits}");
            _output.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
            _output.WriteLine($"    Max Work Group Size: {device.MaxThreadsPerBlock}");

            Assert.NotNull(device.Name);
            Assert.NotNull(device.Vendor);
            Assert.True(device.TotalMemory > 0);
            Assert.True(device.MaxComputeUnits > 0);
        }
    }

    [Fact(DisplayName = "Metal devices detected on macOS")]
    public async Task GetAvailableDevicesAsync_DetectsMetalDevices_OnMacOS()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        var metalDevices = devices.Where(d => d.DeviceType == "Metal").ToList();

        if (OperatingSystem.IsMacOS())
        {
            _output.WriteLine($"Found {metalDevices.Count} Metal device(s):");
            foreach (var device in metalDevices)
            {
                _output.WriteLine($"  - {device.Name}");
                _output.WriteLine($"    Vendor: {device.Vendor}");
                _output.WriteLine($"    Unified Memory: {device.IsUnifiedMemory}");
                _output.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
                _output.WriteLine($"    Max Threads: {device.MaxThreadsPerBlock}");

                Assert.Equal("Apple", device.Vendor);
                Assert.NotNull(device.Name);
                Assert.True(device.TotalMemory > 0);
            }

            // macOS should have at least one Metal device
            Assert.NotEmpty(metalDevices);
        }
        else
        {
            _output.WriteLine("Not on macOS, Metal devices not expected");
            Assert.Empty(metalDevices);
        }
    }

    [Fact(DisplayName = "All devices have valid IDs")]
    public async Task GetAvailableDevicesAsync_AllDevicesHaveValidIds()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        foreach (var device in devices)
        {
            Assert.NotNull(device.Id);
            Assert.NotEmpty(device.Id);
            _output.WriteLine($"Device ID: {device.Id} -> {device.Name}");
        }

        // Verify IDs are unique
        var uniqueIds = devices.Select(d => d.Id).Distinct().Count();
        Assert.Equal(devices.Count, uniqueIds);
    }

    [Fact(DisplayName = "All devices have complete information")]
    public async Task GetAvailableDevicesAsync_AllDevicesHaveCompleteInfo()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        foreach (var device in devices)
        {
            _output.WriteLine($"\nValidating device: {device.Name}");

            Assert.NotNull(device.Id);
            Assert.NotEmpty(device.Name);
            Assert.NotEmpty(device.DeviceType);
            Assert.NotEmpty(device.Vendor);
            Assert.True(device.TotalMemory > 0, $"{device.Name}: TotalMemory must be > 0");
            Assert.True(device.MaxComputeUnits > 0, $"{device.Name}: MaxComputeUnits must be > 0");
            Assert.True(device.MaxThreadsPerBlock > 0, $"{device.Name}: MaxThreadsPerBlock must be > 0");

            _output.WriteLine($"  ✓ ID: {device.Id}");
            _output.WriteLine($"  ✓ Name: {device.Name}");
            _output.WriteLine($"  ✓ Type: {device.DeviceType}");
            _output.WriteLine($"  ✓ Vendor: {device.Vendor}");
            _output.WriteLine($"  ✓ Memory: {device.TotalMemory / (1024.0 * 1024):F2} MB");
            _output.WriteLine($"  ✓ Compute Units: {device.MaxComputeUnits}");
        }
    }

    [Fact(DisplayName = "Device enumeration is consistent across calls")]
    public async Task GetAvailableDevicesAsync_IsConsistentAcrossCalls()
    {
        // Act
        var devices1 = await _factory.GetAvailableDevicesAsync();
        var devices2 = await _factory.GetAvailableDevicesAsync();

        // Assert
        Assert.Equal(devices1.Count, devices2.Count);

        _output.WriteLine($"First call: {devices1.Count} devices");
        _output.WriteLine($"Second call: {devices2.Count} devices");
        _output.WriteLine("Device enumeration is consistent ✓");
    }

    [Fact(DisplayName = "Performance: Device enumeration completes in reasonable time")]
    public async Task GetAvailableDevicesAsync_CompletesInReasonableTime()
    {
        // Warm-up
        await _factory.GetAvailableDevicesAsync();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var devices = await _factory.GetAvailableDevicesAsync();
        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Device enumeration should complete in less than 1 second (actual: {stopwatch.ElapsedMilliseconds}ms)");

        _output.WriteLine($"Device enumeration time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Devices found: {devices.Count}");
        _output.WriteLine($"Average per device: {stopwatch.ElapsedMilliseconds / (double)devices.Count:F2}ms");
    }

    [Fact(DisplayName = "Device capabilities are populated")]
    public async Task GetAvailableDevicesAsync_DeviceCapabilitiesArePopulated()
    {
        // Act
        var devices = await _factory.GetAvailableDevicesAsync();

        // Assert
        foreach (var device in devices)
        {
            _output.WriteLine($"\nDevice: {device.Name} ({device.DeviceType})");

            if (device.Capabilities != null && device.Capabilities.Count > 0)
            {
                _output.WriteLine($"  Capabilities ({device.Capabilities.Count}):");
                foreach (var cap in device.Capabilities.Take(10)) // Show first 10
                {
                    _output.WriteLine($"    - {cap.Key}: {cap.Value}");
                }
                if (device.Capabilities.Count > 10)
                {
                    _output.WriteLine($"    ... and {device.Capabilities.Count - 10} more");
                }
            }
            else
            {
                _output.WriteLine("  No additional capabilities stored (may be a CPU device)");
            }
        }
    }
}
