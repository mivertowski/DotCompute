// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using Xunit;
using FluentAssertions;
using DotCompute.Core;
using DotCompute.Abstractions;

namespace DotCompute.Core.Tests;

/// <summary>
/// Tests for AcceleratorInfo structure.
/// </summary>
public class AcceleratorInfoTests
{
    [Fact]
    public void ConstructorWithValidParameters_CreatesInstance()
    {
        // Arrange
        var name = "Test Accelerator";
        var vendor = "Test Vendor";
        var driverVersion = "1.0.0";
        var type = AcceleratorType.CPU;
        var computeCapability = 1.0;
        var maxThreadsPerBlock = 1024;
        var maxSharedMemory = 48 * 1024;
        var totalMemory = 8L * 1024 * 1024 * 1024;
        var availableMemory = 6L * 1024 * 1024 * 1024;

        // Act
        var info = new AcceleratorInfo(
            name,
            vendor,
            driverVersion,
            type,
            computeCapability,
            maxThreadsPerBlock,
            maxSharedMemory,
            totalMemory,
            availableMemory);

        // Assert
        info.Name.Should().Be(name);
        info.Vendor.Should().Be(vendor);
        info.DriverVersion.Should().Be(driverVersion);
        info.Type.Should().Be(type);
        info.ComputeCapability.Should().Be(computeCapability);
        info.MaxThreadsPerBlock.Should().Be(maxThreadsPerBlock);
        info.MaxSharedMemoryPerBlock.Should().Be(maxSharedMemory);
        info.TotalMemory.Should().Be(totalMemory);
        info.AvailableMemory.Should().Be(availableMemory);
    }

    [Theory]
    [InlineData(null, "vendor", "1.0")]
    [InlineData("", "vendor", "1.0")]
    [InlineData("name", null, "1.0")]
    [InlineData("name", "", "1.0")]
    [InlineData("name", "vendor", null)]
    [InlineData("name", "vendor", "")]
    public void ConstructorWithNullOrEmptyStrings_ThrowsArgumentException(
        string name, string vendor, string driverVersion)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(
            name,
            vendor,
            driverVersion,
            AcceleratorType.CPU,
            1.0,
            1024,
            48 * 1024,
            8L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ConstructorWithInvalidComputeCapability_ThrowsArgumentException(double capability)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(
            "Test",
            "Vendor",
            "1.0",
            AcceleratorType.CPU,
            capability,
            1024,
            48 * 1024,
            8L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("computeCapability");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorWithInvalidMaxThreadsPerBlock_ThrowsArgumentException(int maxThreads)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(
            "Test",
            "Vendor",
            "1.0",
            AcceleratorType.CPU,
            1.0,
            maxThreads,
            48 * 1024,
            8L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxThreadsPerBlock");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1024)]
    public void ConstructorWithNegativeSharedMemory_ThrowsArgumentException(int sharedMemory)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(
            "Test",
            "Vendor",
            "1.0",
            AcceleratorType.CPU,
            1.0,
            1024,
            sharedMemory,
            8L * 1024 * 1024 * 1024,
            6L * 1024 * 1024 * 1024);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxSharedMemoryPerBlock");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(1024, 2048)]
    public void ConstructorWithInvalidMemorySizes_ThrowsArgumentException(long totalMemory, long availableMemory)
    {
        // Act & Assert
        var act = () => new AcceleratorInfo(
            "Test",
            "Vendor",
            "1.0",
            AcceleratorType.CPU,
            1.0,
            1024,
            48 * 1024,
            totalMemory,
            availableMemory);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EqualsWithSameValues_ReturnsTrue()
    {
        // Arrange
        var info1 = CreateTestInfo();
        var info2 = CreateTestInfo();

        // Act & Assert
        info1.Should().Be(info2);
        info1.Equals(info2).Should().BeTrue();
        (info1 == info2).Should().BeTrue();
        (info1 != info2).Should().BeFalse();
    }

    [Fact]
    public void EqualsWithDifferentValues_ReturnsFalse()
    {
        // Arrange
        var info1 = CreateTestInfo();
        var info2 = CreateTestInfo(name: "Different");

        // Act & Assert
        info1.Should().NotBe(info2);
        info1.Equals(info2).Should().BeFalse();
        (info1 == info2).Should().BeFalse();
        (info1 != info2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCodeWithSameValues_ReturnsSameHash()
    {
        // Arrange
        var info1 = CreateTestInfo();
        var info2 = CreateTestInfo();

        // Act & Assert
        info1.GetHashCode().Should().Be(info2.GetHashCode());
    }

    [Fact]
    public void ToStringReturnsReadableRepresentation()
    {
        // Arrange
        var info = CreateTestInfo();

        // Act
        var result = info.ToString();

        // Assert
        result.Should().Contain(info.Name);
        result.Should().Contain(info.Type.ToString());
        result.Should().Contain(info.Vendor);
    }

    private static AcceleratorInfo CreateTestInfo(
        string name = "Test Accelerator",
        string vendor = "Test Vendor",
        string driverVersion = "1.0.0",
        AcceleratorType type = AcceleratorType.CPU,
        double computeCapability = 1.0,
        int maxThreadsPerBlock = 1024,
        int maxSharedMemory = 48 * 1024,
        long totalMemory = 8L * 1024 * 1024 * 1024,
        long availableMemory = 6L * 1024 * 1024 * 1024)
    {
        return new AcceleratorInfo(
            name,
            vendor,
            driverVersion,
            type,
            computeCapability,
            maxThreadsPerBlock,
            maxSharedMemory,
            totalMemory,
            availableMemory);
    }
}