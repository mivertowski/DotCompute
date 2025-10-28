// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: DefaultAcceleratorFactory class does not exist in Runtime module yet
// TODO: Uncomment when DefaultAcceleratorFactory is implemented in DotCompute.Runtime.Services
/*
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Initialization;

/// <summary>
/// Tests for DefaultAcceleratorFactory
/// </summary>
public sealed class DefaultAcceleratorFactoryTests
{
    private readonly ILogger<DefaultAcceleratorFactory> _mockLogger;
    private readonly DefaultAcceleratorFactory _factory;

    public DefaultAcceleratorFactoryTests()
    {
        _mockLogger = Substitute.For<ILogger<DefaultAcceleratorFactory>>();
        _factory = new DefaultAcceleratorFactory(_mockLogger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new DefaultAcceleratorFactory(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void CreateAccelerator_WithCpuType_ReturnsCpuAccelerator()
    {
        // Arrange
        var type = AcceleratorType.Cpu;

        // Act
        var accelerator = _factory.CreateAccelerator(type);

        // Assert
        accelerator.Should().NotBeNull();
        accelerator.Name.Should().Contain("CPU");
    }

    [Fact]
    public void CreateAccelerator_WithGpuType_ReturnsGpuAccelerator()
    {
        // Arrange
        var type = AcceleratorType.Gpu;

        // Act
        var accelerator = _factory.CreateAccelerator(type);

        // Assert
        accelerator.Should().NotBeNull();
    }

    [Fact]
    public void CreateAccelerator_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var type = (AcceleratorType)999;

        // Act
        var action = () => _factory.CreateAccelerator(type);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetAvailableAccelerators_ReturnsNonEmptyList()
    {
        // Act
        var accelerators = _factory.GetAvailableAccelerators();

        // Assert
        accelerators.Should().NotBeEmpty();
    }

    [Fact]
    public void IsAcceleratorAvailable_WithCpu_ReturnsTrue()
    {
        // Arrange
        var type = AcceleratorType.Cpu;

        // Act
        var available = _factory.IsAcceleratorAvailable(type);

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public void CreateDefaultAccelerator_ReturnsAccelerator()
    {
        // Act
        var accelerator = _factory.CreateDefaultAccelerator();

        // Assert
        accelerator.Should().NotBeNull();
    }

    [Fact]
    public void CreateAccelerator_WithOptions_AppliesOptions()
    {
        // Arrange
        var type = AcceleratorType.Cpu;
        var options = new AcceleratorOptions { EnableProfiling = true };

        // Act
        var accelerator = _factory.CreateAccelerator(type, options);

        // Assert
        accelerator.Should().NotBeNull();
    }

    // Helper enums and classes for testing
    private enum AcceleratorType
    {
        Cpu,
        Gpu,
        Tpu
    }

    private class AcceleratorOptions
    {
        public bool EnableProfiling { get; set; }
    }
}
*/