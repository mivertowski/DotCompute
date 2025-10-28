// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: PluginLifecycleManager class does not exist in Runtime module yet
// TODO: Uncomment when PluginLifecycleManager is implemented in DotCompute.Runtime.DependencyInjection.Lifecycle
/*
using DotCompute.Runtime.DependencyInjection.Lifecycle;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for PluginLifecycleManager
/// </summary>
public sealed class PluginLifecycleManagerTests : IDisposable
{
    private readonly ILogger<PluginLifecycleManager> _mockLogger;
    private PluginLifecycleManager? _manager;

    public PluginLifecycleManagerTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginLifecycleManager>>();
    }

    public void Dispose()
    {
        _manager?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new PluginLifecycleManager(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task StartAsync_WithValidPlugin_StartsPlugin()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();

        // Act
        await _manager.StartAsync("test-plugin", plugin);

        // Assert
        plugin.IsStarted.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithNullPluginId_ThrowsArgumentException()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();

        // Act
        var action = async () => await _manager.StartAsync(null!, plugin);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StartAsync_WithNullPlugin_ThrowsArgumentNullException()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);

        // Act
        var action = async () => await _manager.StartAsync("test-plugin", null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StopAsync_WithStartedPlugin_StopsPlugin()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();
        await _manager.StartAsync("test-plugin", plugin);

        // Act
        await _manager.StopAsync("test-plugin");

        // Assert
        plugin.IsStopped.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_WithNonExistentPlugin_DoesNotThrow()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);

        // Act
        await _manager.StopAsync("non-existent");

        // Assert - no exception thrown
    }

    [Fact]
    public async Task GetPluginState_WithStartedPlugin_ReturnsRunning()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();
        await _manager.StartAsync("test-plugin", plugin);

        // Act
        var state = _manager.GetPluginState("test-plugin");

        // Assert
        state.Should().Be(PluginState.Running);
    }

    [Fact]
    public async Task GetPluginState_WithStoppedPlugin_ReturnsStopped()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();
        await _manager.StartAsync("test-plugin", plugin);
        await _manager.StopAsync("test-plugin");

        // Act
        var state = _manager.GetPluginState("test-plugin");

        // Assert
        state.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public async Task RestartAsync_WithRunningPlugin_RestartsPlugin()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();
        await _manager.StartAsync("test-plugin", plugin);

        // Act
        await _manager.RestartAsync("test-plugin");

        // Assert
        plugin.IsStopped.Should().BeTrue();
        plugin.IsStarted.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WithRunningPlugins_StopsAllPlugins()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin = new TestPlugin();
        _manager.StartAsync("test-plugin", plugin).Wait();

        // Act
        _manager.Dispose();

        // Assert
        plugin.IsStopped.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllPluginStates_ReturnsAllStates()
    {
        // Arrange
        _manager = new PluginLifecycleManager(_mockLogger);
        var plugin1 = new TestPlugin();
        var plugin2 = new TestPlugin();
        await _manager.StartAsync("plugin1", plugin1);
        await _manager.StartAsync("plugin2", plugin2);

        // Act
        var states = _manager.GetAllPluginStates();

        // Assert
        states.Should().HaveCount(2);
    }

    // Helper classes for testing
    private class TestPlugin : IPlugin
    {
        public bool IsStarted { get; private set; }
        public bool IsStopped { get; private set; }

        public Task StartAsync()
        {
            IsStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsStopped = true;
            return Task.CompletedTask;
        }
    }

    private interface IPlugin
    {
        Task StartAsync();
        Task StopAsync();
    }

    private enum PluginState
    {
        Stopped,
        Running,
        Failed
    }
}
*/