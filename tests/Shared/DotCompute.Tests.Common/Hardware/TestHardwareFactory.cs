// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Tests.Common.Hardware;

/// <summary>
/// Factory for creating test hardware instances with preset configurations.
/// Provides easy access to commonly used hardware setups for testing.
/// </summary>
[ExcludeFromCodeCoverage]
public static class TestHardwareFactory
{
    /// <summary>
    /// Creates a hardware provider with the default configuration.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider.</returns>
    public static MockHardwareProvider CreateDefault(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.Default);
        return provider;
    }

    /// <summary>
    /// Creates a hardware provider for high-performance gaming scenarios.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with high-end gaming hardware.</returns>
    public static MockHardwareProvider CreateGamingSetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.HighEndGaming);
        return provider;
    }

    /// <summary>
    /// Creates a hardware provider for data center scenarios.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with data center hardware.</returns>
    public static MockHardwareProvider CreateDataCenterSetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.DataCenter);
        return provider;
    }

    /// <summary>
    /// Creates a hardware provider for laptop/mobile scenarios.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with laptop hardware.</returns>
    public static MockHardwareProvider CreateLaptopSetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.LaptopIntegrated);
        return provider;
    }

    /// <summary>
    /// Creates a hardware provider with only CPU devices.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with CPU-only hardware.</returns>
    public static MockHardwareProvider CreateCpuOnlySetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.CpuOnly);
        return provider;
    }

    /// <summary>
    /// Creates a minimal hardware provider for basic testing.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with minimal hardware.</returns>
    public static MockHardwareProvider CreateMinimalSetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.Minimal);
        return provider;
    }

    /// <summary>
    /// Creates a hardware provider with mixed vendor devices.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with mixed vendor hardware.</returns>
    public static MockHardwareProvider CreateMixedVendorSetup(ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.CreateConfiguration(HardwareConfiguration.Mixed);
        return provider;
    }

    /// <summary>
    /// Creates a custom hardware provider with specific devices.
    /// </summary>
    /// <param name="devices">The devices to add to the provider.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured MockHardwareProvider with the specified devices.</returns>
    public static MockHardwareProvider CreateCustom(IEnumerable<MockHardwareDevice> devices, ILogger? logger = null)
    {
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        
        foreach (var device in devices)
        {
            provider.AddDevice(device);
        }
        
        return provider;
    }

    /// <summary>
    /// Creates a hardware simulator with the specified configuration.
    /// </summary>
    /// <param name="configuration">The hardware configuration to simulate.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured HardwareSimulator.</returns>
    public static HardwareSimulator CreateSimulator(
        HardwareConfiguration configuration = HardwareConfiguration.Default,
        ILogger? logger = null)
    {
        var simulator = new HardwareSimulator(logger as ILogger<HardwareSimulator>);
        simulator.Start(configuration);
        return simulator;
    }

    /// <summary>
    /// Creates a stress testing simulator.
    /// </summary>
    /// <param name="duration">How long to run the stress test.</param>
    /// <param name="targetDeviceTypes">Device types to include in stress testing.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A configured HardwareSimulator running a stress test.</returns>
    public static HardwareSimulator CreateStressTestSimulator(
        TimeSpan duration,
        AcceleratorType[] targetDeviceTypes,
        ILogger? logger = null)
    {
        var simulator = new HardwareSimulator(logger as ILogger<HardwareSimulator>);
        simulator.Start(HardwareConfiguration.DataCenter);
        simulator.RunStressTest(duration, targetDeviceTypes);
        return simulator;
    }

    /// <summary>
    /// Creates an accelerator for testing with the specified type and configuration.
    /// </summary>
    /// <param name="type">The accelerator type to create.</param>
    /// <param name="configuration">Optional device configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A mock accelerator instance.</returns>
    public static IAccelerator CreateTestAccelerator(
        AcceleratorType type,
        TestDeviceConfiguration? configuration = null,
        ILogger? logger = null)
    {
        var device = CreateTestDevice(type, configuration, logger);
        var provider = new MockHardwareProvider(logger as ILogger<MockHardwareProvider>);
        provider.AddDevice((MockHardwareDevice)device);
        return provider.CreateAccelerator(device);
    }

    /// <summary>
    /// Creates a test device with the specified configuration.
    /// </summary>
    /// <param name="type">The device type to create.</param>
    /// <param name="configuration">Optional device configuration.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A mock hardware device.</returns>
    public static IHardwareDevice CreateTestDevice(
        AcceleratorType type,
        TestDeviceConfiguration? configuration = null,
        ILogger? logger = null)
    {
        configuration ??= TestDeviceConfiguration.Default;

        return type switch
        {
            AcceleratorType.CUDA => CreateCudaDevice(configuration, logger),
            AcceleratorType.CPU => CreateCpuDevice(configuration, logger),
            AcceleratorType.Metal => CreateMetalDevice(configuration, logger),
            AcceleratorType.OpenCL => CreateOpenClDevice(configuration, logger),
            AcceleratorType.GPU => CreateGenericGpuDevice(configuration, logger),
            _ => throw new NotSupportedException($"Device type {type} is not supported")
        };
    }

    /// <summary>
    /// Creates a collection of devices representing a typical development machine.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A collection of mock hardware devices.</returns>
    public static IEnumerable<MockHardwareDevice> CreateDevelopmentMachineDevices(ILogger? logger = null)
    {
        yield return MockCpuDevice.CreateIntelCore(logger);
        yield return MockCudaDevice.CreateRTX4070(logger);
    }

    /// <summary>
    /// Creates a collection of devices representing a CI/CD server.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A collection of mock hardware devices.</returns>
    public static IEnumerable<MockHardwareDevice> CreateCiServerDevices(ILogger? logger = null)
    {
        yield return MockCpuDevice.CreateXeonServer(logger);
        // CI servers typically don't have GPUs
    }

    /// <summary>
    /// Creates a collection of devices for ML/AI workloads.
    /// </summary>
    /// <param name="logger">Optional logger instance.</param>
    /// <returns>A collection of mock hardware devices optimized for ML.</returns>
    public static IEnumerable<MockHardwareDevice> CreateMachineLearningDevices(ILogger? logger = null)
    {
        yield return MockCudaDevice.CreateA100(logger);
        yield return MockCudaDevice.CreateH100(logger);
        yield return MockCpuDevice.CreateEpycServer(logger);
    }

    private static MockCudaDevice CreateCudaDevice(TestDeviceConfiguration config, ILogger? logger)
    {
        return config.Performance switch
        {
            DevicePerformance.High => MockCudaDevice.CreateRTX4090(logger),
            DevicePerformance.Medium => MockCudaDevice.CreateRTX4070(logger),
            DevicePerformance.Low => MockCudaDevice.CreateRTX3060Laptop(logger),
            _ => MockCudaDevice.CreateRTX4080(logger)
        };
    }

    private static MockCpuDevice CreateCpuDevice(TestDeviceConfiguration config, ILogger? logger)
    {
        return config.Performance switch
        {
            DevicePerformance.High => MockCpuDevice.CreateEpycServer(logger),
            DevicePerformance.Medium => MockCpuDevice.CreateIntelCore(logger),
            DevicePerformance.Low => MockCpuDevice.CreateBasic(logger),
            _ => MockCpuDevice.CreateIntelCore(logger)
        };
    }

    private static MockMetalDevice CreateMetalDevice(TestDeviceConfiguration config, ILogger? logger)
    {
        return config.Performance switch
        {
            DevicePerformance.High => MockMetalDevice.CreateM2Max(logger),
            DevicePerformance.Medium => MockMetalDevice.CreateM2(logger),
            DevicePerformance.Low => MockMetalDevice.CreateM1(logger),
            _ => MockMetalDevice.CreateM2(logger)
        };
    }

    private static MockHardwareDevice CreateOpenClDevice(TestDeviceConfiguration config, ILogger? logger)
    {
        return new MockGenericDevice(
            "opencl_device_0",
            "Generic OpenCL Device",
            AcceleratorType.OpenCL,
            "Generic Vendor",
            config.Performance switch
            {
                DevicePerformance.High => 16L * 1024 * 1024 * 1024,
                DevicePerformance.Medium => 8L * 1024 * 1024 * 1024,
                DevicePerformance.Low => 4L * 1024 * 1024 * 1024,
                _ => 8L * 1024 * 1024 * 1024
            },
            logger);
    }

    private static MockHardwareDevice CreateGenericGpuDevice(TestDeviceConfiguration config, ILogger? logger)
    {
        return new MockGenericDevice(
            "gpu_device_0",
            "Generic GPU Device",
            AcceleratorType.GPU,
            "Generic Vendor",
            config.Performance switch
            {
                DevicePerformance.High => 24L * 1024 * 1024 * 1024,
                DevicePerformance.Medium => 12L * 1024 * 1024 * 1024,
                DevicePerformance.Low => 6L * 1024 * 1024 * 1024,
                _ => 12L * 1024 * 1024 * 1024
            },
            logger);
    }

    /// <summary>
    /// Generic mock device for unsupported or generic device types.
    /// </summary>
    private sealed class MockGenericDevice : MockHardwareDevice
    {
        public MockGenericDevice(string id, string name, AcceleratorType type, string vendor, 
                                long totalMemory, ILogger? logger)
            : base(id, name, type, vendor, "1.0", totalMemory, 1024, 16, logger)
        {
        }
    }
}

/// <summary>
/// Configuration options for test devices.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestDeviceConfiguration
{
    /// <summary>
    /// Gets or sets the performance tier of the device.
    /// </summary>
    public DevicePerformance Performance { get; set; } = DevicePerformance.Medium;

    /// <summary>
    /// Gets or sets whether the device should simulate failures.
    /// </summary>
    public bool SimulateFailures { get; set; }

    /// <summary>
    /// Gets or sets the memory size override in bytes.
    /// </summary>
    public long? MemoryOverride { get; set; }

    /// <summary>
    /// Gets or sets custom capabilities to add to the device.
    /// </summary>
    public Dictionary<string, object> CustomCapabilities { get; set; } = new();

    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static TestDeviceConfiguration Default => new();

    /// <summary>
    /// Creates a high-performance device configuration.
    /// </summary>
    public static TestDeviceConfiguration HighPerformance => new() { Performance = DevicePerformance.High };

    /// <summary>
    /// Creates a low-performance device configuration.
    /// </summary>
    public static TestDeviceConfiguration LowPerformance => new() { Performance = DevicePerformance.Low };

    /// <summary>
    /// Creates a configuration that simulates device failures.
    /// </summary>
    public static TestDeviceConfiguration WithFailures => new() { SimulateFailures = true };
}

/// <summary>
/// Performance tiers for test devices.
/// </summary>
public enum DevicePerformance
{
    Low,
    Medium,
    High
}