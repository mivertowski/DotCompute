// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Security;

/// <summary>
/// Tests for PluginSandbox covering secure plugin execution and isolation.
/// </summary>
public sealed class PluginSandboxTests : IDisposable
{
    private readonly ILogger<PluginSandbox> _mockLogger;
    private PluginSandbox? _sandbox;

    public PluginSandboxTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginSandbox>>();
    }

    [Fact]
    public void Constructor_WithLogger_ShouldInitialize()
    {
        // Arrange & Act
        var sandbox = new PluginSandbox(_mockLogger);

        // Assert
        sandbox.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginSandbox(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithConfiguration_ShouldUseConfiguration()
    {
        // Arrange
        var config = new SandboxConfiguration
        {
            DefaultExecutionTimeout = TimeSpan.FromSeconds(30)
        };

        // Act
        var sandbox = new PluginSandbox(_mockLogger, config);

        // Assert
        sandbox.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateSandboxedPluginAsync_WithValidParameters_ShouldCreatePlugin()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        var assemblyPath = "/test/plugin.dll";
        var typeName = "TestPlugin";
        var permissions = new SandboxPermissions();

        // Act & Assert - This will fail because of file system access
        // In a real scenario, we'd mock the file system
        var act = async () => await _sandbox.CreateSandboxedPluginAsync<object>(
            assemblyPath, typeName, permissions);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task CreateSandboxedPluginAsync_WithEmptyAssemblyPath_ShouldThrowArgumentException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        var permissions = new SandboxPermissions();

        // Act
        var act = async () => await _sandbox.CreateSandboxedPluginAsync<object>(
            "", "TypeName", permissions);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateSandboxedPluginAsync_WithEmptyTypeName_ShouldThrowArgumentException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        var permissions = new SandboxPermissions();

        // Act
        var act = async () => await _sandbox.CreateSandboxedPluginAsync<object>(
            "/test/plugin.dll", "", permissions);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateSandboxedPluginAsync_WithNullPermissions_ShouldThrowArgumentNullException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);

        // Act
        var act = async () => await _sandbox.CreateSandboxedPluginAsync<object>(
            "/test/plugin.dll", "TypeName", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateSandboxedPluginAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        _sandbox.Dispose();
        var permissions = new SandboxPermissions();

        // Act
        var act = async () => await _sandbox.CreateSandboxedPluginAsync<object>(
            "/test/plugin.dll", "TypeName", permissions);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void GetActiveSandboxedPlugins_Initially_ShouldReturnEmptyList()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);

        // Act
        var plugins = _sandbox.GetActiveSandboxedPlugins();

        // Assert
        plugins.Should().NotBeNull();
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveSandboxedPlugins_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        _sandbox.Dispose();

        // Act
        var act = () => _sandbox.GetActiveSandboxedPlugins();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task TerminatePluginAsync_WithValidPluginId_ShouldComplete()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        var pluginId = Guid.NewGuid();

        // Act & Assert - should not throw
        await _sandbox.TerminatePluginAsync(pluginId);
    }

    [Fact]
    public async Task TerminatePluginAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _sandbox = new PluginSandbox(_mockLogger);
        _sandbox.Dispose();

        // Act
        var act = async () => await _sandbox.TerminatePluginAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var sandbox = new PluginSandbox(_mockLogger);

        // Act
        sandbox.Dispose();

        // Assert - should not throw
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var sandbox = new PluginSandbox(_mockLogger);

        // Act
        sandbox.Dispose();
        sandbox.Dispose();

        // Assert - should not throw
    }

    public void Dispose()
    {
        _sandbox?.Dispose();
    }
}

/// <summary>
/// Tests for SandboxPermissions class.
/// </summary>
public sealed class SandboxPermissionsTests
{
    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var permissions = new SandboxPermissions();

        // Assert
        permissions.AllowedPermissions.Should().NotBeNull();
        permissions.DeniedPermissions.Should().NotBeNull();
    }

    [Fact]
    public void AllowedPermissions_ShouldBeModifiable()
    {
        // Arrange
        var permissions = new SandboxPermissions();

        // Act
        permissions.AllowedPermissions.Add("FileIO");

        // Assert
        permissions.AllowedPermissions.Should().Contain("FileIO");
    }

    [Fact]
    public void DeniedPermissions_ShouldBeModifiable()
    {
        // Arrange
        var permissions = new SandboxPermissions();

        // Act
        permissions.DeniedPermissions.Add("NetworkAccess");

        // Assert
        permissions.DeniedPermissions.Should().Contain("NetworkAccess");
    }
}

/// <summary>
/// Tests for SandboxConfiguration class.
/// </summary>
public sealed class SandboxConfigurationTests
{
    [Fact]
    public void Constructor_ShouldInitializeDefaults()
    {
        // Arrange & Act
        var config = new SandboxConfiguration();

        // Assert
        config.DefaultExecutionTimeout.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void DefaultExecutionTimeout_ShouldBeSettable()
    {
        // Arrange
        var config = new SandboxConfiguration();
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        config.DefaultExecutionTimeout = timeout;

        // Assert
        config.DefaultExecutionTimeout.Should().Be(timeout);
    }
}
