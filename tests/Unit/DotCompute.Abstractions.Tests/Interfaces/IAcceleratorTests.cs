// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces.Kernels;
using DotCompute.Abstractions.Kernels;

namespace DotCompute.Abstractions.Tests.Interfaces;

/// <summary>
/// Comprehensive tests for IAccelerator interface and AcceleratorInfo class.
/// </summary>
public class IAcceleratorTests
{
    #region IAccelerator Interface Tests

    [Fact]
    public void IAccelerator_ShouldHaveInfo()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var info = new AcceleratorInfo();
        accelerator.Info.Returns(info);

        // Act
        var result = accelerator.Info;

        // Assert
        result.Should().Be(info);
    }

    [Fact]
    public void IAccelerator_ShouldHaveType()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.Type.Returns(AcceleratorType.GPU);

        // Act
        var type = accelerator.Type;

        // Assert
        type.Should().Be(AcceleratorType.GPU);
    }

    [Fact]
    public void IAccelerator_ShouldHaveDeviceType()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.DeviceType.Returns("CUDA");

        // Act
        var deviceType = accelerator.DeviceType;

        // Assert
        deviceType.Should().Be("CUDA");
    }

    [Fact]
    public void IAccelerator_ShouldHaveMemory()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var memory = Substitute.For<IUnifiedMemoryManager>();
        accelerator.Memory.Returns(memory);

        // Act
        var result = accelerator.Memory;

        // Assert
        result.Should().Be(memory);
    }

    [Fact]
    public void IAccelerator_MemoryManager_ShouldAliasMemory()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var memory = Substitute.For<IUnifiedMemoryManager>();
        accelerator.MemoryManager.Returns(memory);

        // Act
        var result = accelerator.MemoryManager;

        // Assert
        result.Should().Be(memory);
    }

    [Fact]
    public void IAccelerator_ShouldHaveContext()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var context = new AcceleratorContext();
        accelerator.Context.Returns(context);

        // Act
        var result = accelerator.Context;

        // Assert
        result.Should().Be(context);
    }

    [Fact]
    public void IAccelerator_IsAvailable_ShouldReturnStatus()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.IsAvailable.Returns(true);

        // Act
        var isAvailable = accelerator.IsAvailable;

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task CompileKernelAsync_ShouldReturnCompiledKernel()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var definition = new KernelDefinition
        {
            Name = "test_kernel"
        };
        var compiledKernel = Substitute.For<ICompiledKernel>();
        accelerator.CompileKernelAsync(definition, null, Arg.Any<CancellationToken>())
            .Returns(compiledKernel);

        // Act
        var result = await accelerator.CompileKernelAsync(definition);

        // Assert
        result.Should().Be(compiledKernel);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldComplete()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        accelerator.SynchronizeAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await accelerator.SynchronizeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region AcceleratorInfo Constructor Tests

    [Fact]
    public void AcceleratorInfo_DefaultConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var info = new AcceleratorInfo();

        // Assert
        info.Id.Should().Be("test_device");
        info.Name.Should().Be("Test Device");
        info.DeviceType.Should().Be("Test");
        info.Vendor.Should().Be("Test Vendor");
    }

    [Fact]
    public void AcceleratorInfo_LegacyConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var info = new AcceleratorInfo(
            AcceleratorType.GPU,
            "RTX 4090",
            "535.104.05",
            24L * 1024 * 1024 * 1024);

        // Assert
        info.Name.Should().Be("RTX 4090");
        info.DeviceType.Should().Be("GPU");
        info.DriverVersion.Should().Be("535.104.05");
        info.TotalMemory.Should().Be(24L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void AcceleratorInfo_FullConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var info = new AcceleratorInfo(
            "RTX 4090",
            "NVIDIA",
            "535.104.05",
            AcceleratorType.GPU,
            8.9,
            1024,
            48 * 1024,
            24L * 1024 * 1024 * 1024,
            23L * 1024 * 1024 * 1024);

        // Assert
        info.Name.Should().Be("RTX 4090");
        info.Vendor.Should().Be("NVIDIA");
        info.MaxThreadsPerBlock.Should().Be(1024);
        info.MaxSharedMemoryPerBlock.Should().Be(48 * 1024);
    }

    [Fact]
    public void AcceleratorInfo_FullConstructor_WithInvalidName_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new AcceleratorInfo(
            "",
            "NVIDIA",
            "535.104.05",
            AcceleratorType.GPU,
            8.9,
            1024,
            48 * 1024,
            24L * 1024 * 1024 * 1024,
            23L * 1024 * 1024 * 1024);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void AcceleratorInfo_FullConstructor_WithInvalidMemory_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new AcceleratorInfo(
            "GPU",
            "NVIDIA",
            "535.104.05",
            AcceleratorType.GPU,
            8.9,
            1024,
            48 * 1024,
            1000,
            2000); // Available > Total

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid memory sizes*");
    }

    [Fact]
    public void AcceleratorInfo_ExtendedConstructor_ShouldInitialize()
    {
        // Arrange & Act
        var computeCapability = new Version(8, 9);
        var info = new AcceleratorInfo(
            AcceleratorType.GPU,
            "RTX 4090",
            "535.104.05",
            24L * 1024 * 1024 * 1024,
            128,
            2520,
            computeCapability,
            48 * 1024,
            false);

        // Assert
        info.ComputeUnits.Should().Be(128);
        info.MaxClockFrequency.Should().Be(2520);
        info.ComputeCapability.Should().Be(computeCapability);
        info.IsUnifiedMemory.Should().BeFalse();
    }

    #endregion

    #region AcceleratorInfo Property Tests

    [Fact]
    public void AcceleratorInfo_Type_ShouldAliasDeviceType()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "Custom",
            Vendor = "TestVendor"
        };

        // Act
        var type = info.Type;

        // Assert
        type.Should().Be("Custom");
    }

    [Fact]
    public void AcceleratorInfo_MemorySize_ShouldAliasTotalMemory()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            TotalMemory = 8L * 1024 * 1024 * 1024
        };

        // Act
        var memorySize = info.MemorySize;

        // Assert
        memorySize.Should().Be(8L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void AcceleratorInfo_ComputeCapabilityVersionSafe_WithNull_ShouldReturnDefault()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            ComputeCapability = null
        };

        // Act
        var version = info.ComputeCapabilityVersionSafe;

        // Assert
        version.Should().Be(new Version(6, 0));
    }

    [Fact]
    public void AcceleratorInfo_ComputeCapabilityVersionSafe_WithValue_ShouldReturnValue()
    {
        // Arrange
        var expectedVersion = new Version(8, 9);
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            ComputeCapability = expectedVersion
        };

        // Act
        var version = info.ComputeCapabilityVersionSafe;

        // Assert
        version.Should().Be(expectedVersion);
    }

    [Fact]
    public void AcceleratorInfo_MaxWorkGroupSize_ShouldAliasMaxThreadsPerBlock()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            MaxThreadsPerBlock = 1024
        };

        // Act
        var workGroupSize = info.MaxWorkGroupSize;

        // Assert
        workGroupSize.Should().Be(1024);
    }

    [Fact]
    public void AcceleratorInfo_MaxComputeUnits_ShouldBeSet()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            MaxComputeUnits = 128
        };

        // Act
        var computeUnits = info.MaxComputeUnits;

        // Assert
        computeUnits.Should().Be(128);
    }

    [Fact]
    public void AcceleratorInfo_GlobalMemorySize_ShouldBeSet()
    {
        // Arrange
        var memorySize = 16L * 1024 * 1024 * 1024;
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            GlobalMemorySize = memorySize
        };

        // Act
        var globalMemory = info.GlobalMemorySize;

        // Assert
        globalMemory.Should().Be(memorySize);
    }

    [Fact]
    public void AcceleratorInfo_SupportsFloat64_ShouldBeSet()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            SupportsFloat64 = true
        };

        // Act
        var supportsFloat64 = info.SupportsFloat64;

        // Assert
        supportsFloat64.Should().BeTrue();
    }

    [Fact]
    public void AcceleratorInfo_SupportsInt64_ShouldBeSet()
    {
        // Arrange
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            SupportsInt64 = true
        };

        // Act
        var supportsInt64 = info.SupportsInt64;

        // Assert
        supportsInt64.Should().BeTrue();
    }

    [Fact]
    public void AcceleratorInfo_Capabilities_ShouldBeSettable()
    {
        // Arrange
        var capabilities = new Dictionary<string, object>
        {
            ["AsyncCopy"] = true,
            ["PeerAccess"] = false,
            ["UnifiedAddressing"] = true
        };

        // Act
        var info = new AcceleratorInfo
        {
            Id = "test",
            Name = "Test",
            DeviceType = "GPU",
            Vendor = "TestVendor",
            Capabilities = capabilities
        };

        // Assert
        info.Capabilities.Should().ContainKey("AsyncCopy");
        info.Capabilities!["AsyncCopy"].Should().Be(true);
    }

    #endregion

    #region ICompiledKernel Tests

    [Fact]
    public void ICompiledKernel_ShouldHaveId()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        var id = Guid.NewGuid();
        kernel.Id.Returns(id);

        // Act
        var result = kernel.Id;

        // Assert
        result.Should().Be(id);
    }

    [Fact]
    public void ICompiledKernel_ShouldHaveName()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        kernel.Name.Returns("vector_add");

        // Act
        var name = kernel.Name;

        // Assert
        name.Should().Be("vector_add");
    }

    [Fact]
    public async Task ICompiledKernel_ExecuteAsync_ShouldComplete()
    {
        // Arrange
        var kernel = Substitute.For<ICompiledKernel>();
        var arguments = new KernelArguments();
        kernel.ExecuteAsync(arguments, Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);

        // Act
        var act = async () => await kernel.ExecuteAsync(arguments);

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void AcceleratorInfo_CPUDefaults_ShouldBeCorrect()
    {
        // Arrange & Act
        var info = new AcceleratorInfo(
            AcceleratorType.CPU,
            "Intel i9-13900K",
            "1.0",
            32L * 1024 * 1024 * 1024);

        // Assert
        info.DeviceType.Should().Be("CPU");
        info.IsUnifiedMemory.Should().BeTrue();
        info.MaxComputeUnits.Should().Be(Environment.ProcessorCount);
        info.SupportsFloat64.Should().BeTrue();
    }

    [Fact]
    public void AcceleratorInfo_GPUDefaults_ShouldBeCorrect()
    {
        // Arrange & Act
        var info = new AcceleratorInfo(
            AcceleratorType.GPU,
            "RTX 4090",
            "535.104.05",
            24L * 1024 * 1024 * 1024);

        // Assert
        info.DeviceType.Should().Be("GPU");
        info.IsUnifiedMemory.Should().BeFalse();
        info.MaxComputeUnits.Should().Be(16);
    }

    [Fact]
    public async Task IAccelerator_FullWorkflow_ShouldWork()
    {
        // Arrange
        var accelerator = Substitute.For<IAccelerator>();
        var memory = Substitute.For<IUnifiedMemoryManager>();
        var kernel = Substitute.For<ICompiledKernel>();
        var definition = new KernelDefinition { Name = "test" };

        accelerator.IsAvailable.Returns(true);
        accelerator.Memory.Returns(memory);
        accelerator.CompileKernelAsync(Arg.Any<KernelDefinition>(), null, Arg.Any<CancellationToken>())
            .Returns(kernel);

        // Act
        var isAvailable = accelerator.IsAvailable;
        var compiledKernel = await accelerator.CompileKernelAsync(definition);
        await accelerator.SynchronizeAsync();

        // Assert
        isAvailable.Should().BeTrue();
        compiledKernel.Should().Be(kernel);
    }

    #endregion
}
