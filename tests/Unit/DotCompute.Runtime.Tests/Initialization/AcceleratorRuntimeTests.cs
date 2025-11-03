// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: AcceleratorRuntime constructor signature mismatch and IAccelerator API issues
// TODO: Uncomment when the following are fixed:
// - AcceleratorRuntime constructor expects (IServiceProvider, ILogger) not (ILogger, IAccelerator)
// - IAccelerator.Name property is missing
/*
using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Initialization;

/// <summary>
/// Tests for AcceleratorRuntime
/// </summary>
public sealed class AcceleratorRuntimeTests : IDisposable
{
    private readonly ILogger<AcceleratorRuntime> _mockLogger;
    private readonly IAccelerator _mockAccelerator;
    private AcceleratorRuntime? _runtime;

    public AcceleratorRuntimeTests()
    {
        _mockLogger = Substitute.For<ILogger<AcceleratorRuntime>>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Name.Returns("TestAccelerator");
    }

    public void Dispose()
    {
        _runtime?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new AcceleratorRuntime(null!, _mockAccelerator);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new AcceleratorRuntime(_mockLogger, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("accelerator");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);

        // Assert
        _runtime.Should().NotBeNull();
    }

    [Fact]
    public void GetAccelerator_ReturnsConfiguredAccelerator()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);

        // Act
        var accelerator = _runtime.GetAccelerator();

        // Assert
        accelerator.Should().BeSameAs(_mockAccelerator);
    }

    [Fact]
    public async Task InitializeAsync_Succeeds()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);

        // Act
        await _runtime.InitializeAsync();

        // Assert
        _runtime.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_OnlyInitializesOnce()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);

        // Act
        await _runtime.InitializeAsync();
        await _runtime.InitializeAsync();

        // Assert
        _runtime.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task GetAcceleratorInfo_ReturnsInfo()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);
        await _runtime.InitializeAsync();

        // Act
        var info = _runtime.GetAcceleratorInfo();

        // Assert
        info.Should().NotBeNull();
        info.Name.Should().Be("TestAccelerator");
    }

    [Fact]
    public async Task IsAcceleratorAvailable_AfterInitialization_ReturnsTrue()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);
        await _runtime.InitializeAsync();

        // Act
        var available = _runtime.IsAcceleratorAvailable();

        // Assert
        available.Should().BeTrue();
    }

    [Fact]
    public async Task GetMemoryInfo_ReturnsMemoryInformation()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);
        await _runtime.InitializeAsync();

        // Act
        var memInfo = _runtime.GetMemoryInfo();

        // Assert
        memInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispose_CleansUpResources()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);
        await _runtime.InitializeAsync();

        // Act
        _runtime.Dispose();

        // Assert
        _mockAccelerator.Received().Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);

        // Act
        _runtime.Dispose();
        _runtime.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task ExecuteKernelAsync_WithValidKernel_Succeeds()
    {
        // Arrange
        _runtime = new AcceleratorRuntime(_mockLogger, _mockAccelerator);
        await _runtime.InitializeAsync();
        var kernel = Substitute.For<IKernel>();

        // Act
        await _runtime.ExecuteKernelAsync(kernel);

        // Assert
        await _mockAccelerator.Received().ExecuteAsync(kernel);
    }
}
*/
