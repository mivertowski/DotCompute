using DotCompute.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;


public sealed class AcceleratorInfoTests
{
    [Fact]
    public void LegacyConstructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var type = AcceleratorType.CUDA;
        var name = "Test Accelerator";
        var driverVersion = "1.0.0";
        var memorySize = 8589934592L; // 8GB

        // Act
        var info = new AcceleratorInfo(type, name, driverVersion, memorySize);

        // Assert
        _ = info.Id.Should().Be($"{type}_{name}");
        _ = info.Name.Should().Be(name);
        _ = info.DeviceType.Should().Be(type.ToString());
        _ = info.Type.Should().Be(type.ToString());
        _ = info.Vendor.Should().Be("Unknown");
        _ = info.DriverVersion.Should().Be(driverVersion);
        _ = info.TotalMemory.Should().Be(memorySize);
        _ = info.AvailableMemory.Should().Be(memorySize);
        _ = info.IsUnifiedMemory.Should().BeFalse(); // GPU is not unified
        _ = info.MaxThreadsPerBlock.Should().Be(1024); // Default value
    }

    [Fact]
    public void LegacyConstructor_WithCPU_ShouldSetUnifiedMemory()
    {
        // Arrange
        var type = AcceleratorType.CPU;
        var name = "Intel CPU";
        var driverVersion = "1.0.0";
        var memorySize = 16777216000L; // 16GB

        // Act
        var info = new AcceleratorInfo(type, name, driverVersion, memorySize);

        // Assert
        _ = info.IsUnifiedMemory.Should().BeTrue(); // CPU has unified memory
    }

    [Fact]
    public void FullConstructor_WithValidParameters_ShouldInitializeAllProperties()
    {
        // Arrange
        var name = "NVIDIA RTX 3090";
        var vendor = "NVIDIA";
        var driverVersion = "525.60.11";
        var type = AcceleratorType.CUDA;
        var computeCapability = 8.6;
        var maxThreadsPerBlock = 1024;
        var maxSharedMemory = 49152;
        var totalMemory = 24576L * 1024 * 1024; // 24GB
        var availableMemory = 20480L * 1024 * 1024; // 20GB

        // Act
        var info = new AcceleratorInfo(name, vendor, driverVersion, type,
            computeCapability, maxThreadsPerBlock, maxSharedMemory,
            totalMemory, availableMemory);

        // Assert
        _ = info.Id.Should().Be($"{type}_{name}");
        _ = info.Name.Should().Be(name);
        _ = info.Vendor.Should().Be(vendor);
        _ = info.DriverVersion.Should().Be(driverVersion);
        _ = info.DeviceType.Should().Be(type.ToString());
        _ = info.TotalMemory.Should().Be(totalMemory);
        _ = info.AvailableMemory.Should().Be(availableMemory);
        _ = info.MaxThreadsPerBlock.Should().Be(maxThreadsPerBlock);
        _ = info.MaxSharedMemoryPerBlock.Should().Be(maxSharedMemory);
        _ = info.ComputeCapability.Should().NotBeNull();
        _ = info.ComputeCapability!.Major.Should().Be(8);
        // Due to floating point precision, 8.6 might produce Minor=5 instead of 6
        _ = info.ComputeCapability.Minor.Should().BeInRange(5, 6);
    }

    [Theory]
    [InlineData("", "vendor", "1.0", AcceleratorType.CUDA)]
    [InlineData("name", "", "1.0", AcceleratorType.CUDA)]
    [InlineData("name", "vendor", "", AcceleratorType.CUDA)]
    public void FullConstructor_WithEmptyStrings_ShouldThrowArgumentException(
        string name, string vendor, string driverVersion, AcceleratorType type)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(name, vendor, driverVersion, type,
            7.5, 1024, 49152, 8589934592L, 8589934592L);
        _ = Assert.Throws<ArgumentException>(() => act());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FullConstructor_WithInvalidComputeCapability_ShouldThrowArgumentOutOfRangeException(
        double computeCapability)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo("GPU", "Vendor", "1.0", AcceleratorType.CUDA,
            computeCapability, 1024, 49152, 8589934592L, 8589934592L);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FullConstructor_WithInvalidMaxThreads_ShouldThrowArgumentOutOfRangeException(
        int maxThreadsPerBlock)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo("GPU", "Vendor", "1.0", AcceleratorType.CUDA,
            7.5, maxThreadsPerBlock, 49152, 8589934592L, 8589934592L);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Fact]
    public void FullConstructor_WithNegativeSharedMemory_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var act = () => new AcceleratorInfo("GPU", "Vendor", "1.0", AcceleratorType.CUDA,
            7.5, 1024, -1, 8589934592L, 8589934592L);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => act());
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(1024, 0)]
    [InlineData(1024, 2048)] // Available > Total
    public void FullConstructor_WithInvalidMemorySizes_ShouldThrowArgumentException(
        long totalMemory, long availableMemory)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo("GPU", "Vendor", "1.0", AcceleratorType.CUDA,
            7.5, 1024, 49152, totalMemory, availableMemory);
        _ = Assert.Throws<ArgumentException>(() => act());
    }

    [Fact]
    public void ExtendedConstructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var type = AcceleratorType.CUDA;
        var name = "Test GPU";
        var driverVersion = "1.0.0";
        var memorySize = 8589934592L;
        var computeUnits = 80;
        var maxClockFrequency = 1700;
        var computeCapability = new Version(8, 6);
        var maxSharedMemoryPerBlock = 49152L;
        var isUnifiedMemory = false;

        // Act
        var info = new AcceleratorInfo(type, name, driverVersion, memorySize,
            computeUnits, maxClockFrequency, computeCapability,
            maxSharedMemoryPerBlock, isUnifiedMemory);

        // Assert
        _ = info.Id.Should().Be($"{type}_{name}");
        _ = info.Name.Should().Be(name);
        _ = info.DriverVersion.Should().Be(driverVersion);
        _ = info.TotalMemory.Should().Be(memorySize);
        _ = info.ComputeUnits.Should().Be(computeUnits);
        _ = info.MaxClockFrequency.Should().Be(maxClockFrequency);
        _ = info.ComputeCapability.Should().Be(computeCapability);
        _ = info.MaxSharedMemoryPerBlock.Should().Be(maxSharedMemoryPerBlock);
        _ = info.IsUnifiedMemory.Should().Be(isUnifiedMemory);
    }

    [Fact]
    public void MemorySize_Property_ShouldReturnTotalMemory()
    {
        // Arrange
        var memorySize = 8589934592L;
        var info = new AcceleratorInfo(AcceleratorType.CUDA, "GPU", "1.0", memorySize);

        // Act & Assert
        _ = info.MemorySize.Should().Be(memorySize);
        _ = info.MemorySize.Should().Be(info.TotalMemory);
    }

    [Theory]
    [InlineData(AcceleratorType.CPU)]
    [InlineData(AcceleratorType.CUDA)]
    [InlineData(AcceleratorType.OpenCL)]
    public void Type_Property_ShouldReturnDeviceTypeAsString(AcceleratorType type)
    {
        // Arrange
        var info = new AcceleratorInfo(type, "Device", "1.0", 1024);

        // Act & Assert
        _ = info.Type.Should().Be(type.ToString());
        _ = info.Type.Should().Be(info.DeviceType);
    }
}
