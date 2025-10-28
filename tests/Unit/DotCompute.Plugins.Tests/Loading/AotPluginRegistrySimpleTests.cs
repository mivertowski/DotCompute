// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Aot.Registration;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Loading;

/// <summary>
/// Simple tests for PluginRegistrationService (AOT registry) covering available public APIs.
/// </summary>
public sealed class AotPluginRegistrySimpleTests : IDisposable
{
    private readonly ILogger<PluginRegistrationService> _mockLogger;
    private readonly PluginRegistrationService _registry;

    public AotPluginRegistrySimpleTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginRegistrationService>>();
        _registry = new PluginRegistrationService(_mockLogger);
    }

    [Fact]
    public void Constructor_WithValidLogger_ShouldInitialize()
    {
        // Arrange & Act
        var registry = new PluginRegistrationService(_mockLogger);

        // Assert
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginRegistrationService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void RegisterPluginFactory_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var pluginTypeName = "TestPlugin";
        Func<IBackendPlugin> factory = () => CreateMockPlugin();

        // Act
        var act = () => _registry.RegisterPluginFactory(pluginTypeName, factory);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void IsPluginTypeRegistered_ForRegisteredPlugin_ShouldReturnTrue()
    {
        // Arrange
        var pluginTypeName = "TestPlugin";
        Func<IBackendPlugin> factory = () => CreateMockPlugin();
        _registry.RegisterPluginFactory(pluginTypeName, factory);

        // Act
        var result = _registry.IsPluginTypeRegistered(pluginTypeName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void UnregisterPluginFactory_ForRegisteredPlugin_ShouldReturnTrue()
    {
        // Arrange
        var pluginTypeName = "TestPlugin";
        _registry.RegisterPluginFactory(pluginTypeName, () => CreateMockPlugin());

        // Act
        var result = _registry.UnregisterPluginFactory(pluginTypeName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void UnregisterPluginFactory_ForUnregisteredPlugin_ShouldReturnFalse()
    {
        // Arrange, Act
        var result = _registry.UnregisterPluginFactory("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var act = () => _registry.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        _registry.Dispose();

        // Act & Assert
        var act = () => _registry.Dispose();
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
        _registry?.Dispose();
    }
}
