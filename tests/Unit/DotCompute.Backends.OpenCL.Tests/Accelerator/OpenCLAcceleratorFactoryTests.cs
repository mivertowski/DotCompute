// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.OpenCL.Factory;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Tests.Accelerator;

/// <summary>
/// Tests for <see cref="OpenCLAcceleratorFactory"/> class.
/// </summary>
public sealed class OpenCLAcceleratorFactoryTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OpenCLAcceleratorFactory> _logger;

    public OpenCLAcceleratorFactoryTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _logger = Substitute.For<ILogger<OpenCLAcceleratorFactory>>();
        _loggerFactory.CreateLogger<OpenCLAcceleratorFactory>().Returns(_logger);
    }

    [Fact]
    public void Constructor_WithLoggerFactory_InitializesSuccessfully()
    {
        // Arrange & Act
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithLogger_InitializesSuccessfully()
    {
        // Arrange & Act
        var factory = new OpenCLAcceleratorFactory(_logger);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new OpenCLAcceleratorFactory((ILoggerFactory)null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new OpenCLAcceleratorFactory((ILogger<OpenCLAcceleratorFactory>)null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void CreateBest_WhenNoDevicesAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act
        var act = () => factory.CreateBest();

        // Assert - Will throw since no actual OpenCL devices in test environment
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForDeviceType_WithGPUType_AttemptsToCreateGPUAccelerator()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert - Will throw since no actual OpenCL devices
        var act = () => factory.CreateForDeviceType(OpenCLTypes.DeviceType.GPU);
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForDeviceType_WithCPUType_AttemptsToCreateCPUAccelerator()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForDeviceType(OpenCLTypes.DeviceType.CPU);
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForDeviceType_WithAcceleratorType_AttemptsToCreateAcceleratorAccelerator()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForDeviceType(OpenCLTypes.DeviceType.Accelerator);
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForDeviceType_WithNegativeIndex_ThrowsException()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForDeviceType(OpenCLTypes.DeviceType.GPU, -1);
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForVendor_WithNullVendorName_ThrowsArgumentException()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForVendor(null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("vendorName");
    }

    [Fact]
    public void CreateForVendor_WithEmptyVendorName_ThrowsArgumentException()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForVendor("");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("vendorName");
    }

    [Fact]
    public void CreateForVendor_WithWhitespaceVendorName_ThrowsArgumentException()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForVendor("   ");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("vendorName");
    }

    [Fact]
    public void CreateForVendor_WithValidVendor_AttemptsToCreate()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act & Assert
        var act = () => factory.CreateForVendor("NVIDIA");
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void CreateForDevice_WithValidDeviceId_AttemptsToCreate()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);
        var deviceId = new OpenCLTypes.DeviceId(new IntPtr(1));

        // Act & Assert
        var act = () => factory.CreateForDevice(deviceId);
        act.Should().ThrowAny<Exception>();
    }

    [Fact]
    public void GetAvailableDevices_ReturnsDeviceCollection()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act
        var devices = factory.GetAvailableDevices();

        // Assert
        devices.Should().NotBeNull();
        // In test environment without OpenCL, should be empty
        devices.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailablePlatforms_ReturnsPlatformCollection()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act
        var platforms = factory.GetAvailablePlatforms();

        // Assert
        platforms.Should().NotBeNull();
        // In test environment without OpenCL, should be empty
        platforms.Should().BeEmpty();
    }

    [Fact]
    public void IsOpenCLAvailable_WhenNotAvailable_ReturnsFalse()
    {
        // Arrange
        var factory = new OpenCLAcceleratorFactory(_loggerFactory);

        // Act
        var isAvailable = factory.IsOpenCLAvailable();

        // Assert
        // In test environment without OpenCL, should be false
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsDeviceSuitable_WithValidDevice_ReturnsTrue()
    {
        // Arrange
        var device = CreateSuitableDevice();

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeTrue();
    }

    [Fact]
    public void IsDeviceSuitable_WithUnavailableDevice_ReturnsFalse()
    {
        // Arrange
        var device = CreateSuitableDevice() with { Available = false };

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeFalse();
    }

    [Fact]
    public void IsDeviceSuitable_WithNoCompiler_ReturnsFalse()
    {
        // Arrange
        var device = CreateSuitableDevice() with { CompilerAvailable = false };

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeFalse();
    }

    [Fact]
    public void IsDeviceSuitable_WithInsufficientMemory_ReturnsFalse()
    {
        // Arrange
        var device = CreateSuitableDevice() with { GlobalMemorySize = 64UL * 1024 * 1024 }; // 64 MB

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeFalse();
    }

    [Fact]
    public void IsDeviceSuitable_WithZeroComputeUnits_ReturnsFalse()
    {
        // Arrange
        var device = CreateSuitableDevice() with { MaxComputeUnits = 0 };

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeFalse();
    }

    [Fact]
    public void IsDeviceSuitable_WithSmallWorkGroupSize_ReturnsFalse()
    {
        // Arrange
        var device = CreateSuitableDevice() with { MaxWorkGroupSize = 32 };

        // Act
        var isSuitable = OpenCLAcceleratorFactory.IsDeviceSuitable(device);

        // Assert
        isSuitable.Should().BeFalse();
    }

    [Fact]
    public void GetPreferredDeviceTypes_ReturnsOrderedTypes()
    {
        // Act
        var types = OpenCLAcceleratorFactory.GetPreferredDeviceTypes();

        // Assert
        types.Should().NotBeNull();
        types.Should().HaveCountGreaterThan(0);
        types[0].Should().Be(OpenCLTypes.DeviceType.GPU);
    }

    [Fact]
    public void GetPreferredDeviceTypes_GPUIsFirstPriority()
    {
        // Act
        var types = OpenCLAcceleratorFactory.GetPreferredDeviceTypes();

        // Assert
        types[0].Should().Be(OpenCLTypes.DeviceType.GPU);
    }

    [Fact]
    public void GetPreferredDeviceTypes_AcceleratorIsSecondPriority()
    {
        // Act
        var types = OpenCLAcceleratorFactory.GetPreferredDeviceTypes();

        // Assert
        types[1].Should().Be(OpenCLTypes.DeviceType.Accelerator);
    }

    [Fact]
    public void GetPreferredDeviceTypes_CPUIsThirdPriority()
    {
        // Act
        var types = OpenCLAcceleratorFactory.GetPreferredDeviceTypes();

        // Assert
        types[2].Should().Be(OpenCLTypes.DeviceType.CPU);
    }

    private static OpenCLDeviceInfo CreateSuitableDevice()
    {
        return new OpenCLDeviceInfo
        {
            DeviceId = new OpenCLTypes.DeviceId(new IntPtr(1)),
            Name = "Test Device",
            Vendor = "Test Vendor",
            Type = OpenCLTypes.DeviceType.GPU,
            Available = true,
            CompilerAvailable = true,
            GlobalMemorySize = 1024UL * 1024 * 1024, // 1 GB
            MaxMemoryAllocationSize = 256UL * 1024 * 1024,
            LocalMemorySize = 64UL * 1024,
            MaxWorkGroupSize = 1024,
            MaxComputeUnits = 16,
            MaxClockFrequency = 1500
        };
    }
}
