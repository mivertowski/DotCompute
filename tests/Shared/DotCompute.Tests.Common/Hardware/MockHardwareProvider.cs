// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using DotCompute.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Common.Hardware;


/// <summary>
/// Mock hardware provider for testing without actual hardware dependencies.
/// Simulates various hardware configurations and failure scenarios.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MockHardwareProvider : IHardwareProvider
{
    private readonly ILogger<MockHardwareProvider> _logger;
    private readonly List<IHardwareDevice> _devices;
    private readonly Dictionary<AcceleratorType, List<IHardwareDevice>> _devicesByType;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the MockHardwareProvider with default devices.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    public MockHardwareProvider(ILogger<MockHardwareProvider>? logger = null)
    {
        _logger = logger ?? NullLogger<MockHardwareProvider>.Instance;
        _devices = [];
        _devicesByType = [];

        InitializeDefaultDevices();
        _logger.LogInformation("Initialized MockHardwareProvider with {DeviceCount} devices", _devices.Count);
    }

    /// <inheritdoc/>
    public bool IsRealHardware => false;

    /// <inheritdoc/>
    public IEnumerable<IHardwareDevice> GetDevices(AcceleratorType type)
    {
        ThrowIfDisposed();
        return _devicesByType.TryGetValue(type, out var devices)
            ? devices.Where(d => d.IsAvailable)
            : Enumerable.Empty<IHardwareDevice>();
    }

    /// <inheritdoc/>
    public IEnumerable<IHardwareDevice> GetAllDevices()
    {
        ThrowIfDisposed();
        return _devices.Where(d => d.IsAvailable);
    }

    /// <inheritdoc/>
    public IHardwareDevice? GetFirstDevice(AcceleratorType type) => GetDevices(type).FirstOrDefault();

    /// <inheritdoc/>
    public IAccelerator CreateAccelerator(IHardwareDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device);

        if (device is not MockHardwareDevice mockDevice)
        {
            throw new ArgumentException("Device must be a MockHardwareDevice", nameof(device));
        }

        _logger.LogDebug("Creating mock accelerator for device {DeviceId}", device.Id);
        return new MockAccelerator(
            name: mockDevice.Name ?? mockDevice.Id,
            type: mockDevice.Type,
            totalMemory: 8L * 1024 * 1024 * 1024, // 8GB default
            logger: _logger);
    }

    /// <inheritdoc/>
    public bool IsSupported(AcceleratorType type) => _devicesByType.ContainsKey(type) && _devicesByType[type].Any(d => d.IsAvailable);

    /// <inheritdoc/>
    public Dictionary<string, object> GetDiagnostics()
    {
        ThrowIfDisposed();

        var diagnostics = new Dictionary<string, object>
        {
            ["IsRealHardware"] = IsRealHardware,
            ["TotalDevices"] = _devices.Count,
            ["AvailableDevices"] = _devices.Count(d => d.IsAvailable),
            ["DevicesByType"] = _devicesByType.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value.Count(d => d.IsAvailable)),
            ["Timestamp"] = DateTime.UtcNow
        };

        return diagnostics;
    }

    /// <summary>
    /// Adds a mock device to the provider.
    /// </summary>
    /// <param name="device">The device to add.</param>
    public void AddDevice(MockHardwareDevice device)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(device);

        _devices.Add(device);

        if (!_devicesByType.ContainsKey(device.Type))
        {
            _devicesByType[device.Type] = [];
        }
        _devicesByType[device.Type].Add(device);

        _logger.LogDebug("Added mock device: {DeviceId} ({DeviceType})", device.Id, device.Type);
    }

    /// <summary>
    /// Removes a device from the provider.
    /// </summary>
    /// <param name="deviceId">The ID of the device to remove.</param>
    /// <returns>True if the device was found and removed, false otherwise.</returns>
    public bool RemoveDevice(string deviceId)
    {
        ThrowIfDisposed();

        var device = _devices.FirstOrDefault(d => d.Id == deviceId);
        if (device == null)
        {
            return false;
        }

        _ = _devices.Remove(device);
        _ = _devicesByType[device.Type].Remove(device);

        _logger.LogDebug("Removed mock device: {DeviceId}", deviceId);
        return true;
    }

    /// <summary>
    /// Simulates a device failure.
    /// </summary>
    /// <param name="deviceId">The ID of the device to fail.</param>
    /// <param name="errorMessage">Optional error message.</param>
    public void SimulateDeviceFailure(string deviceId, string? errorMessage = null)
    {
        ThrowIfDisposed();

        if (_devices.FirstOrDefault(d => d.Id == deviceId) is MockHardwareDevice device)
        {
            device.SimulateFailure(errorMessage ?? "Simulated device failure");
            _logger.LogWarning("Simulated failure for device {DeviceId}: {Error}", deviceId, errorMessage);
        }
    }

    /// <summary>
    /// Resets all device failures.
    /// </summary>
    public void ResetAllFailures()
    {
        ThrowIfDisposed();

        foreach (var device in _devices.OfType<MockHardwareDevice>())
        {
            device.ResetFailure();
        }

        _logger.LogInformation("Reset all simulated failures");
    }

    /// <summary>
    /// Creates a preset configuration of devices for testing.
    /// </summary>
    /// <param name="configuration">The configuration to create.</param>
    public void CreateConfiguration(HardwareConfiguration configuration)
    {
        ThrowIfDisposed();

        // Clear existing devices
        _devices.Clear();
        _devicesByType.Clear();

        switch (configuration)
        {
            case HardwareConfiguration.HighEndGaming:
                CreateHighEndGamingSetup();
                break;
            case HardwareConfiguration.DataCenter:
                CreateDataCenterSetup();
                break;
            case HardwareConfiguration.LaptopIntegrated:
                CreateLaptopSetup();
                break;
            case HardwareConfiguration.CpuOnly:
                CreateCpuOnlySetup();
                break;
            case HardwareConfiguration.Mixed:
                CreateMixedSetup();
                break;
            case HardwareConfiguration.Minimal:
                CreateMinimalSetup();
                break;
            default:
                InitializeDefaultDevices();
                break;
        }

        _logger.LogInformation("Created {Configuration} hardware configuration with {DeviceCount} devices",
            configuration, _devices.Count);
    }

    private void InitializeDefaultDevices()
    {
        // Create a balanced set of devices for general testing
        AddDevice(MockCudaDevice.CreateRTX4090());
        AddDevice(MockCudaDevice.CreateRTX3080());
        AddDevice(MockCpuDevice.CreateIntelCore());
        AddDevice(MockCpuDevice.CreateAmdRyzen());
        AddDevice(MockMetalDevice.CreateM2Max());
    }

    private void CreateHighEndGamingSetup()
    {
        AddDevice(MockCudaDevice.CreateRTX4090());
        AddDevice(MockCudaDevice.CreateRTX4080());
        AddDevice(MockCpuDevice.CreateIntelCore());
    }

    private void CreateDataCenterSetup()
    {
        AddDevice(MockCudaDevice.CreateA100());
        AddDevice(MockCudaDevice.CreateV100());
        AddDevice(MockCudaDevice.CreateH100());
        AddDevice(MockCpuDevice.CreateEpycServer());
        AddDevice(MockCpuDevice.CreateXeonServer());
    }

    private void CreateLaptopSetup()
    {
        AddDevice(MockCudaDevice.CreateRTX3060Laptop());
        AddDevice(MockCpuDevice.CreateIntelCoreLaptop());
        AddDevice(MockMetalDevice.CreateM1Pro());
    }

    private void CreateCpuOnlySetup()
    {
        AddDevice(MockCpuDevice.CreateIntelCore());
        AddDevice(MockCpuDevice.CreateAmdRyzen());
    }

    private void CreateMixedSetup()
    {
        AddDevice(MockCudaDevice.CreateRTX4070());
        AddDevice(MockCpuDevice.CreateIntelCore());
        AddDevice(MockMetalDevice.CreateM2());
    }

    private void CreateMinimalSetup() => AddDevice(MockCpuDevice.CreateBasic());

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var device in _devices.OfType<IDisposable>())
        {
            try
            {
                device.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing device during cleanup");
            }
        }

        _devices.Clear();
        _devicesByType.Clear();
        _disposed = true;
        _logger.LogDebug("MockHardwareProvider disposed");
    }
}

/// <summary>
/// Predefined hardware configurations for testing scenarios.
/// </summary>
public enum HardwareConfiguration
{
    /// <summary>Default balanced configuration.</summary>
    Default,

    /// <summary>High-end gaming setup with latest GPUs.</summary>
    HighEndGaming,

    /// <summary>Data center setup with professional cards.</summary>
    DataCenter,

    /// <summary>Laptop with integrated/mobile GPUs.</summary>
    LaptopIntegrated,

    /// <summary>CPU-only configuration.</summary>
    CpuOnly,

    /// <summary>Mixed vendor configuration.</summary>
    Mixed,

    /// <summary>Minimal single device configuration.</summary>
    Minimal
}
