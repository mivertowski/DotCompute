// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Core;

/// <summary>
/// Tests for BackendPluginBase covering lifecycle, state management, and events.
/// </summary>
public sealed class BackendPluginBaseTests
{
    private readonly IServiceProvider _serviceProvider;

    public BackendPluginBaseTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_ShouldInitializeWithUnknownState()
    {
        // Arrange & Act
        var plugin = new TestPlugin();

        // Assert
        plugin.State.Should().Be(PluginState.Unknown);
        plugin.Health.Should().Be(PluginHealth.Unknown);
        plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_ShouldTransitionToInitialized()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        await plugin.InitializeAsync(_serviceProvider);

        // Assert
        plugin.State.Should().Be(PluginState.Initialized);
        plugin.Health.Should().Be(PluginHealth.Healthy);
    }

    [Fact]
    public async Task InitializeAsync_WhenAlreadyInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var plugin = new TestPlugin();
        await plugin.InitializeAsync(_serviceProvider);

        // Act
        var act = async () => await plugin.InitializeAsync(_serviceProvider);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InitializeAsync_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var act = async () => await plugin.InitializeAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeAsync_ShouldRaiseStateChangedEvent()
    {
        // Arrange
        var plugin = new TestPlugin();
        PluginStateChangedEventArgs? eventArgs = null;
        plugin.StateChanged += (sender, args) => eventArgs = args;

        // Act
        await plugin.InitializeAsync(_serviceProvider);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.OldState.Should().Be(PluginState.Unknown);
        eventArgs.NewState.Should().Be(PluginState.Initializing);
    }

    [Fact]
    public async Task StartAsync_ShouldTransitionToRunning()
    {
        // Arrange
        var plugin = new TestPlugin();
        await plugin.InitializeAsync(_serviceProvider);

        // Act
        await plugin.StartAsync();

        // Assert
        plugin.State.Should().Be(PluginState.Running);
        plugin.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WithoutInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var act = async () => await plugin.StartAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StopAsync_ShouldTransitionToStopped()
    {
        // Arrange
        var plugin = new TestPlugin();
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();

        // Act
        await plugin.StopAsync();

        // Assert
        plugin.State.Should().Be(PluginState.Stopped);
        plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ShouldNotThrow()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act & Assert
        await plugin.StopAsync(); // Should not throw
    }

    [Fact]
    public void Validate_WithValidPlugin_ShouldReturnSuccessResult()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var result = plugin.Validate();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidPlugin_ShouldReturnFailureResult()
    {
        // Arrange
        var plugin = new InvalidTestPlugin();

        // Act
        var result = plugin.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task OnConfigurationChangedAsync_ShouldUpdateConfiguration()
    {
        // Arrange
        var plugin = new TestPlugin();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        await plugin.OnConfigurationChangedAsync(configuration);

        // Assert - no exception thrown
    }

    [Fact]
    public void GetMetrics_ShouldReturnCurrentMetrics()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var metrics = plugin.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.RequestCount.Should().Be(0);
        metrics.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMetrics_WhenRunning_ShouldIncludeUptime()
    {
        // Arrange
        var plugin = new TestPlugin();
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();
        await Task.Delay(100);

        // Act
        var metrics = plugin.GetMetrics();

        // Assert
        metrics.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ErrorOccurred_ShouldBeRaisedOnError()
    {
        // Arrange
        var plugin = new TestPlugin();
        PluginErrorEventArgs? eventArgs = null;
        plugin.ErrorOccurred += (sender, args) => eventArgs = args;

        // Act
        await plugin.InitializeAsync(_serviceProvider);
        plugin.TriggerError(new InvalidOperationException("Test error"));

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task HealthChanged_ShouldBeRaisedOnHealthChange()
    {
        // Arrange
        var plugin = new TestPlugin();
        PluginHealthChangedEventArgs? eventArgs = null;
        plugin.HealthChanged += (sender, args) => eventArgs = args;

        // Act
        await plugin.InitializeAsync(_serviceProvider);
        plugin.UpdateHealth(PluginHealth.Degraded);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.OldHealth.Should().Be(PluginHealth.Healthy);
        eventArgs.NewHealth.Should().Be(PluginHealth.Degraded);
    }

    [Fact]
    public async Task LoadAsync_ShouldInitializeAndStart()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        await plugin.LoadAsync(_serviceProvider);

        // Assert
        plugin.State.Should().Be(PluginState.Running);
        plugin.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task UnloadAsync_ShouldStop()
    {
        // Arrange
        var plugin = new TestPlugin();
        await plugin.LoadAsync(_serviceProvider);

        // Act
        await plugin.UnloadAsync();

        // Assert
        plugin.State.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public void Dispose_WhenRunning_ShouldStopPlugin()
    {
        // Arrange
        var plugin = new TestPlugin();
        plugin.LoadAsync(_serviceProvider).GetAwaiter().GetResult();

        // Act
        plugin.Dispose();

        // Assert
        plugin.State.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        plugin.Dispose();
        plugin.Dispose();

        // Assert - should not throw
    }

    [Fact]
    public async Task InitializeAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var plugin = new TestPlugin();
        plugin.Dispose();

        // Act
        var act = async () => await plugin.InitializeAsync(_serviceProvider);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // Test Plugin Implementation
    private sealed class TestPlugin : BackendPluginBase
    {
        public override string Id => "test-plugin";
        public override string Name => "Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "A test plugin";
        public override string Author => "Test Author";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public void TriggerError(Exception exception)
        {
            OnError(exception, "Test");
        }

        public void UpdateHealth(PluginHealth newHealth)
        {
            Health = newHealth;
        }
    }

    private sealed class InvalidTestPlugin : BackendPluginBase
    {
        public override string Id => "";  // Invalid
        public override string Name => "";  // Invalid
        public override Version Version => null!;  // Invalid
        public override string Description => "Invalid Plugin";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.None;
    }
}
