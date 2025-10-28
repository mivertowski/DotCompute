// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Discovery;
using DotCompute.Plugins.Aot.Registration;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Loading;

/// <summary>
/// Simple tests for PluginDiscoveryService covering available public APIs.
/// </summary>
public sealed class PluginDiscoveryServiceSimpleTests : IDisposable
{
    private readonly ILogger<PluginDiscoveryService> _mockLogger;
    private readonly ILogger<PluginRegistrationService> _mockRegLogger;
    private readonly PluginRegistrationService _mockRegistrationService;
    private readonly PluginDiscoveryService _discoveryService;

    public PluginDiscoveryServiceSimpleTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginDiscoveryService>>();
        _mockRegLogger = Substitute.For<ILogger<PluginRegistrationService>>();
        _mockRegistrationService = new PluginRegistrationService(_mockRegLogger);
        _discoveryService = new PluginDiscoveryService(_mockLogger, _mockRegistrationService);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitialize()
    {
        // Arrange & Act
        var service = new PluginDiscoveryService(_mockLogger, _mockRegistrationService);

        // Assert
        service.Should().NotBeNull();
        service.LoadedPluginCount.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginDiscoveryService(null!, _mockRegistrationService);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullRegistrationService_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginDiscoveryService(_mockLogger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("registrationService");
    }

    [Fact]
    public void LoadedPluginCount_Initially_ShouldBeZero()
    {
        // Arrange, Act & Assert
        _discoveryService.LoadedPluginCount.Should().Be(0);
    }

    [Fact]
    public void CreatePlugin_WithNullPluginTypeName_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        var act = () => _discoveryService.CreatePlugin(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePlugin_WithEmptyPluginTypeName_ShouldThrowArgumentException()
    {
        // Arrange, Act & Assert
        var act = () => _discoveryService.CreatePlugin("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePlugin_WhenNoFactoryFound_ShouldReturnNull()
    {
        // Arrange & Act
        var result = _discoveryService.CreatePlugin("NonExistentPlugin");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CreatePlugin_WithValidFactory_ShouldCreatePlugin()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        _mockRegistrationService.RegisterPluginFactory("TestPlugin", () => mockPlugin);

        // Act
        var result = _discoveryService.CreatePlugin("TestPlugin");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(mockPlugin);
        _discoveryService.LoadedPluginCount.Should().Be(1);
    }

    [Fact]
    public void LoadedPluginCount_AfterCreatingPlugin_ShouldIncrease()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        _mockRegistrationService.RegisterPluginFactory("TestPlugin", () => mockPlugin);

        // Act
        _discoveryService.CreatePlugin("TestPlugin");

        // Assert
        _discoveryService.LoadedPluginCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var act = () => _discoveryService.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        _discoveryService.Dispose();

        // Act & Assert
        var act = () => _discoveryService.Dispose();
        act.Should().NotThrow();
    }

    private static IBackendPlugin CreateMockPlugin(string id = "test-plugin")
    {
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Id.Returns(id);
        plugin.Name.Returns($"Test Plugin {id}");
        plugin.Version.Returns(new Version(1, 0, 0));
        plugin.Description.Returns("Test plugin description");
        plugin.Author.Returns("Test Author");
        plugin.Capabilities.Returns(PluginCapabilities.ComputeBackend);
        plugin.State.Returns(PluginState.Unknown);
        plugin.Health.Returns(PluginHealth.Healthy);
        return plugin;
    }

    public void Dispose()
    {
        _discoveryService?.Dispose();
        _mockRegistrationService?.Dispose();
    }
}
