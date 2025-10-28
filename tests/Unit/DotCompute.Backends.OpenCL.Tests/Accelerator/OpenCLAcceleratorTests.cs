// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Types;
using DotCompute.Backends.OpenCL.DeviceManagement;
using DotCompute.Backends.OpenCL.Models;
using DotCompute.Backends.OpenCL.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.OpenCL.Tests.Accelerator;

/// <summary>
/// Tests for <see cref="OpenCLAccelerator"/> class.
/// </summary>
public sealed class OpenCLAcceleratorTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<OpenCLAccelerator> _logger;

    public OpenCLAcceleratorTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _logger = Substitute.For<ILogger<OpenCLAccelerator>>();
        _loggerFactory.CreateLogger<OpenCLAccelerator>().Returns(_logger);
        _loggerFactory.CreateLogger<OpenCLDeviceManager>().Returns(Substitute.For<ILogger<OpenCLDeviceManager>>());
        _loggerFactory.CreateLogger<OpenCLContext>().Returns(Substitute.For<ILogger<OpenCLContext>>());
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithLoggerFactory_InitializesSuccessfully()
    {
        // Arrange & Act
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Id.Should().NotBeEmpty();
        accelerator.Type.Should().Be(AcceleratorType.OpenCL);
        accelerator.DeviceType.Should().Be("OpenCL");
    }

    [Fact]
    public void Constructor_WithLogger_InitializesSuccessfully()
    {
        // Arrange & Act
        var accelerator = new OpenCLAccelerator(_logger);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Id.Should().NotBeEmpty();
        accelerator.Type.Should().Be(AcceleratorType.OpenCL);
    }

    [Fact]
    public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new OpenCLAccelerator((ILoggerFactory)null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("loggerFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new OpenCLAccelerator((ILogger<OpenCLAccelerator>)null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithDevice_InitializesWithProvidedDevice()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");

        // Act
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.DeviceInfo.Should().Be(deviceInfo);
        accelerator.Name.Should().Contain("Test Device");
    }

    [Fact]
    public void Constructor_WithDeviceAndLogger_InitializesWithProvidedDevice()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");

        // Act
        var accelerator = new OpenCLAccelerator(deviceInfo, _logger);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.DeviceInfo.Should().Be(deviceInfo);
    }

    [Fact]
    public void Constructor_WithNullDevice_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new OpenCLAccelerator(null!, _loggerFactory);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("device");
    }

    [Fact]
    public void Id_ReturnsUniqueGuid()
    {
        // Arrange
        var accelerator1 = new OpenCLAccelerator(_loggerFactory);
        var accelerator2 = new OpenCLAccelerator(_loggerFactory);

        // Act & Assert
        accelerator1.Id.Should().NotBeEmpty();
        accelerator2.Id.Should().NotBeEmpty();
        accelerator1.Id.Should().NotBe(accelerator2.Id);
    }

    [Fact]
    public void Name_WhenNotInitialized_ReturnsNotInitializedMessage()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var name = accelerator.Name;

        // Assert
        name.Should().Contain("Not Initialized");
    }

    [Fact]
    public void Name_WhenInitializedWithDevice_ReturnsDeviceName()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("NVIDIA GeForce RTX");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Act
        var name = accelerator.Name;

        // Assert
        name.Should().Contain("NVIDIA GeForce RTX");
    }

    [Fact]
    public void Type_ReturnsOpenCL()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var type = accelerator.Type;

        // Assert
        type.Should().Be(AcceleratorType.OpenCL);
    }

    [Fact]
    public void DeviceType_ReturnsOpenCLString()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var deviceType = accelerator.DeviceType;

        // Assert
        deviceType.Should().Be("OpenCL");
    }

    [Fact]
    public void IsAvailable_WhenNotInitialized_ReturnsFalse()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var isAvailable = accelerator.IsAvailable;

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);
        accelerator.Dispose();

        // Act
        var isAvailable = accelerator.IsAvailable;

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public void DeviceInfo_WhenNotInitializedWithDevice_ReturnsNull()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var deviceInfo = accelerator.DeviceInfo;

        // Assert
        deviceInfo.Should().BeNull();
    }

    [Fact]
    public void DeviceInfo_WhenInitializedWithDevice_ReturnsDeviceInfo()
    {
        // Arrange
        var expectedDevice = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(expectedDevice, _loggerFactory);

        // Act
        var deviceInfo = accelerator.DeviceInfo;

        // Assert
        deviceInfo.Should().Be(expectedDevice);
    }

    [Fact]
    public void IsDisposed_WhenNotDisposed_ReturnsFalse()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var isDisposed = accelerator.IsDisposed;

        // Assert
        isDisposed.Should().BeFalse();
    }

    [Fact]
    public void IsDisposed_WhenDisposed_ReturnsTrue()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);
        accelerator.Dispose();

        // Act
        var isDisposed = accelerator.IsDisposed;

        // Assert
        isDisposed.Should().BeTrue();
    }

    [Fact]
    public void Info_WhenNotInitialized_ReturnsDefaultInfo()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var info = accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Id.Should().Be(accelerator.Id.ToString());
        info.DeviceType.Should().Be("OpenCL");
        info.Vendor.Should().Be("Unknown");
    }

    [Fact]
    public void Info_WhenInitializedWithDevice_ReturnsDeviceInfo()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Act
        var info = accelerator.Info;

        // Assert
        info.Should().NotBeNull();
        info.Name.Should().Contain("Test Device");
        info.Vendor.Should().Be("Test Vendor");
        info.TotalMemory.Should().Be(1024L * 1024 * 1024);
    }

    [Fact]
    public void Memory_WhenNotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var act = () => accelerator.Memory;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void Memory_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);
        accelerator.Dispose();

        // Act
        var act = () => accelerator.Memory;

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void MemoryManager_ReturnsMemoryProperty()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Act & Assert - should throw before initialization
        var act = () => accelerator.MemoryManager;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Context_ReturnsAcceleratorContext()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var context = accelerator.Context;

        // Assert
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task AllocateAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);
        accelerator.Dispose();

        // Act
        var act = async () => await accelerator.AllocateAsync<float>(100);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task AllocateAsync_WithZeroCount_ThrowsArgumentException()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Act
        var act = async () => await accelerator.AllocateAsync<float>(0);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("elementCount");
    }

    [Fact]
    public async Task SynchronizeAsync_WhenNotInitialized_CompletesSuccessfully()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var act = async () => await accelerator.SynchronizeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SynchronizeAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);
        accelerator.Dispose();

        // Act
        var act = async () => await accelerator.SynchronizeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        accelerator.Dispose();
        var act = () => accelerator.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        var act = async () => await accelerator.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
        accelerator.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var accelerator = new OpenCLAccelerator(_loggerFactory);

        // Act
        await accelerator.DisposeAsync();
        var act = async () => await accelerator.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CompileKernelAsync_WithNullDefinition_ThrowsArgumentException()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);

        // Act
        var act = async () => await accelerator.CompileKernelAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompileKernelAsync_WithEmptySource_ThrowsArgumentException()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);
        var definition = new KernelDefinition { Source = "", EntryPoint = "test" };

        // Act
        var act = async () => await accelerator.CompileKernelAsync(definition);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*source*");
    }

    [Fact]
    public async Task CompileKernelAsync_WithEmptyEntryPoint_ThrowsArgumentException()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);
        var definition = new KernelDefinition { Source = "kernel void test() {}", EntryPoint = "" };

        // Act
        var act = async () => await accelerator.CompileKernelAsync(definition);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*entry point*");
    }

    [Fact]
    public async Task CompileKernelAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var deviceInfo = CreateMockDeviceInfo("Test Device");
        var accelerator = new OpenCLAccelerator(deviceInfo, _loggerFactory);
        accelerator.Dispose();
        var definition = new KernelDefinition { Source = "kernel void test() {}", EntryPoint = "test" };

        // Act
        var act = async () => await accelerator.CompileKernelAsync(definition);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    private static OpenCLDeviceInfo CreateMockDeviceInfo(string name)
    {
        return new OpenCLDeviceInfo
        {
            DeviceId = new OpenCLTypes.DeviceId(new IntPtr(1)),
            Name = name,
            Vendor = "Test Vendor",
            Type = OpenCLTypes.DeviceType.GPU,
            Available = true,
            CompilerAvailable = true,
            GlobalMemorySize = 1024UL * 1024 * 1024, // 1 GB
            MaxMemoryAllocationSize = 256UL * 1024 * 1024, // 256 MB
            LocalMemorySize = 64UL * 1024, // 64 KB
            MaxWorkGroupSize = 1024,
            MaxComputeUnits = 16,
            MaxClockFrequency = 1500,
            DriverVersion = "1.0.0",
            OpenCLVersion = "OpenCL 3.0"
        };
    }
}
