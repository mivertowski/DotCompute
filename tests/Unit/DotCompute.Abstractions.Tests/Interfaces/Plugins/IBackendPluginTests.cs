// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Abstractions.Tests.Interfaces.Plugins;

/// <summary>
/// Comprehensive tests for IBackendPlugin interface and related types.
/// </summary>
public class IBackendPluginTests
{
    #region Interface Contract Tests

    [Fact]
    public void IBackendPlugin_ShouldHaveRequiredProperties()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Id.Returns("test-plugin");
        plugin.Name.Returns("Test Plugin");
        plugin.Version.Returns(new Version(1, 0, 0));
        plugin.Description.Returns("A test plugin");
        plugin.Author.Returns("Test Author");

        // Act & Assert
        plugin.Id.Should().Be("test-plugin");
        plugin.Name.Should().Be("Test Plugin");
        plugin.Version.Should().Be(new Version(1, 0, 0));
        plugin.Description.Should().Be("A test plugin");
        plugin.Author.Should().Be("Test Author");
    }

    [Fact]
    public void IBackendPlugin_ShouldHaveState()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.State.Returns(PluginState.Initialized);

        // Act
        var state = plugin.State;

        // Assert
        state.Should().Be(PluginState.Initialized);
    }

    [Fact]
    public void IBackendPlugin_ShouldHaveHealth()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Health.Returns(PluginHealth.Healthy);

        // Act
        var health = plugin.Health;

        // Assert
        health.Should().Be(PluginHealth.Healthy);
    }

    [Fact]
    public void IBackendPlugin_ShouldHaveCapabilities()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Capabilities.Returns(PluginCapabilities.ComputeBackend | PluginCapabilities.HotReloadable);

        // Act
        var capabilities = plugin.Capabilities;

        // Assert
        capabilities.Should().HaveFlag(PluginCapabilities.ComputeBackend);
        capabilities.Should().HaveFlag(PluginCapabilities.HotReloadable);
    }

    [Fact]
    public void ConfigureServices_ShouldBeCallable()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var services = new ServiceCollection();
        var configuration = Substitute.For<IConfiguration>();

        // Act
        var act = () => plugin.ConfigureServices(services, configuration);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task InitializeAsync_ShouldReturnTask()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        plugin.InitializeAsync(serviceProvider, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await plugin.InitializeAsync(serviceProvider);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldReturnTask()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        var act = async () => await plugin.StartAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldReturnTask()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        var act = async () => await plugin.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Validate_ShouldReturnValidationResult()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var validationResult = new PluginValidationResult { IsValid = true };
        plugin.Validate().Returns(validationResult);

        // Act
        var result = plugin.Validate();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetConfigurationSchema_ShouldReturnSchema()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var schema = "{ \"type\": \"object\" }";
        plugin.GetConfigurationSchema().Returns(schema);

        // Act
        var result = plugin.GetConfigurationSchema();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("object");
    }

    [Fact]
    public async Task OnConfigurationChangedAsync_ShouldHandleChanges()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var configuration = Substitute.For<IConfiguration>();
        plugin.OnConfigurationChangedAsync(configuration, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var act = async () => await plugin.OnConfigurationChangedAsync(configuration);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetMetrics_ShouldReturnPluginMetrics()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var metrics = new PluginMetrics
        {
            RequestCount = 100,
            ErrorCount = 2,
            AverageResponseTime = 15.5
        };
        plugin.GetMetrics().Returns(metrics);

        // Act
        var result = plugin.GetMetrics();

        // Assert
        result.Should().NotBeNull();
        result.RequestCount.Should().Be(100);
    }

    #endregion

    #region PluginCapabilities Tests

    [Fact]
    public void PluginCapabilities_None_ShouldBeZero()
    {
        // Arrange & Act
        var capabilities = PluginCapabilities.None;

        // Assert
        ((int)capabilities).Should().Be(0);
    }

    [Fact]
    public void PluginCapabilities_ComputeBackend_ShouldBeSet()
    {
        // Arrange & Act
        var capabilities = PluginCapabilities.ComputeBackend;

        // Assert
        capabilities.Should().HaveFlag(PluginCapabilities.ComputeBackend);
    }

    [Fact]
    public void PluginCapabilities_CanCombineFlags()
    {
        // Arrange & Act
        var capabilities = PluginCapabilities.ComputeBackend |
                          PluginCapabilities.HotReloadable |
                          PluginCapabilities.CrossPlatform;

        // Assert
        capabilities.Should().HaveFlag(PluginCapabilities.ComputeBackend);
        capabilities.Should().HaveFlag(PluginCapabilities.HotReloadable);
        capabilities.Should().HaveFlag(PluginCapabilities.CrossPlatform);
    }

    #endregion

    #region PluginState Tests

    [Fact]
    public void PluginState_AllStates_ShouldBeDefined()
    {
        // Arrange & Act & Assert
        Enum.IsDefined(typeof(PluginState), PluginState.Unknown).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Loading).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Loaded).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Initializing).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Initialized).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Starting).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Running).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Stopping).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Stopped).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Failed).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Unloading).Should().BeTrue();
        Enum.IsDefined(typeof(PluginState), PluginState.Unloaded).Should().BeTrue();
    }

    #endregion

    #region PluginHealth Tests

    [Fact]
    public void PluginHealth_AllStates_ShouldBeDefined()
    {
        // Arrange & Act & Assert
        Enum.IsDefined(typeof(PluginHealth), PluginHealth.Unknown).Should().BeTrue();
        Enum.IsDefined(typeof(PluginHealth), PluginHealth.Healthy).Should().BeTrue();
        Enum.IsDefined(typeof(PluginHealth), PluginHealth.Degraded).Should().BeTrue();
        Enum.IsDefined(typeof(PluginHealth), PluginHealth.Unhealthy).Should().BeTrue();
        Enum.IsDefined(typeof(PluginHealth), PluginHealth.Critical).Should().BeTrue();
    }

    #endregion

    #region PluginValidationResult Tests

    [Fact]
    public void PluginValidationResult_IsValid_DefaultsToFalse()
    {
        // Arrange & Act
        var result = new PluginValidationResult();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PluginValidationResult_CanAddErrors()
    {
        // Arrange
        var result = new PluginValidationResult();

        // Act
        result.Errors.Add("Missing dependency");
        result.Errors.Add("Invalid configuration");

        // Assert
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Missing dependency");
    }

    [Fact]
    public void PluginValidationResult_CanAddWarnings()
    {
        // Arrange
        var result = new PluginValidationResult();

        // Act
        result.Warnings.Add("Deprecated API usage");
        result.Warnings.Add("Performance concern");

        // Assert
        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("Deprecated API usage");
    }

    [Fact]
    public void PluginValidationResult_CanAddMetadata()
    {
        // Arrange
        var result = new PluginValidationResult();

        // Act
        result.Metadata["version"] = "1.0.0";
        result.Metadata["checkTime"] = DateTime.UtcNow;

        // Assert
        result.Metadata.Should().ContainKey("version");
        result.Metadata.Should().ContainKey("checkTime");
    }

    #endregion

    #region PluginMetrics Tests

    [Fact]
    public void PluginMetrics_DefaultTimestamp_ShouldBeRecent()
    {
        // Arrange & Act
        var metrics = new PluginMetrics();

        // Assert
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PluginMetrics_CanSetProperties()
    {
        // Arrange & Act
        var metrics = new PluginMetrics
        {
            Uptime = TimeSpan.FromHours(24),
            RequestCount = 10000,
            ErrorCount = 50,
            AverageResponseTime = 25.5,
            MemoryUsage = 1024 * 1024 * 100, // 100 MB
            CpuUsage = 45.0
        };

        // Assert
        metrics.Uptime.Should().Be(TimeSpan.FromHours(24));
        metrics.RequestCount.Should().Be(10000);
        metrics.ErrorCount.Should().Be(50);
        metrics.AverageResponseTime.Should().Be(25.5);
        metrics.MemoryUsage.Should().Be(1024 * 1024 * 100);
        metrics.CpuUsage.Should().Be(45.0);
    }

    [Fact]
    public void PluginMetrics_CanAddCustomMetrics()
    {
        // Arrange
        var metrics = new PluginMetrics();

        // Act
        metrics.CustomMetrics["QueueDepth"] = 25;
        metrics.CustomMetrics["CacheHitRate"] = 0.95;

        // Assert
        metrics.CustomMetrics.Should().ContainKey("QueueDepth");
        metrics.CustomMetrics.Should().ContainKey("CacheHitRate");
    }

    #endregion

    #region PluginStateChangedEventArgs Tests

    [Fact]
    public void PluginStateChangedEventArgs_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var args = new PluginStateChangedEventArgs(
            PluginState.Loading,
            PluginState.Loaded,
            "Initialization complete");

        // Assert
        args.OldState.Should().Be(PluginState.Loading);
        args.NewState.Should().Be(PluginState.Loaded);
        args.Reason.Should().Be("Initialization complete");
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PluginStateChangedEventArgs_ReasonCanBeNull()
    {
        // Arrange & Act
        var args = new PluginStateChangedEventArgs(
            PluginState.Running,
            PluginState.Stopped);

        // Assert
        args.Reason.Should().BeNull();
    }

    #endregion

    #region PluginErrorEventArgs Tests

    [Fact]
    public void PluginErrorEventArgs_ShouldInitializeCorrectly()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var args = new PluginErrorEventArgs(exception, "Plugin startup");

        // Assert
        args.Exception.Should().Be(exception);
        args.Context.Should().Be("Plugin startup");
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        args.AdditionalData.Should().NotBeNull();
    }

    [Fact]
    public void PluginErrorEventArgs_CanIncludeAdditionalData()
    {
        // Arrange
        var exception = new Exception("Error");
        var additionalData = new Dictionary<string, object>
        {
            ["RetryCount"] = 3,
            ["LastAttempt"] = DateTime.UtcNow
        };

        // Act
        var args = new PluginErrorEventArgs(exception, "Operation", additionalData);

        // Assert
        args.AdditionalData.Should().ContainKey("RetryCount");
        args.AdditionalData.Should().ContainKey("LastAttempt");
    }

    #endregion

    #region PluginHealthChangedEventArgs Tests

    [Fact]
    public void PluginHealthChangedEventArgs_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var args = new PluginHealthChangedEventArgs(
            PluginHealth.Healthy,
            PluginHealth.Degraded,
            "High memory usage");

        // Assert
        args.OldHealth.Should().Be(PluginHealth.Healthy);
        args.NewHealth.Should().Be(PluginHealth.Degraded);
        args.Reason.Should().Be("High memory usage");
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PluginHealthChangedEventArgs_CanIncludeHealthData()
    {
        // Arrange
        var healthData = new Dictionary<string, object>
        {
            ["MemoryUsage"] = 95.0,
            ["ResponseTime"] = 500.0
        };

        // Act
        var args = new PluginHealthChangedEventArgs(
            PluginHealth.Healthy,
            PluginHealth.Degraded,
            "Performance degradation",
            healthData);

        // Assert
        args.HealthData.Should().ContainKey("MemoryUsage");
        args.HealthData.Should().ContainKey("ResponseTime");
    }

    #endregion

    #region Event Tests

    [Fact]
    public void IBackendPlugin_StateChangedEvent_ShouldBeInvokable()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var eventRaised = false;
        plugin.StateChanged += (sender, args) => eventRaised = true;

        // Act
        plugin.StateChanged += Raise.Event<EventHandler<PluginStateChangedEventArgs>>(
            plugin,
            new PluginStateChangedEventArgs(PluginState.Loading, PluginState.Loaded));

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void IBackendPlugin_ErrorOccurredEvent_ShouldBeInvokable()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var eventRaised = false;
        plugin.ErrorOccurred += (sender, args) => eventRaised = true;

        // Act
        plugin.ErrorOccurred += Raise.Event<EventHandler<PluginErrorEventArgs>>(
            plugin,
            new PluginErrorEventArgs(new Exception(), "Test"));

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public void IBackendPlugin_HealthChangedEvent_ShouldBeInvokable()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var eventRaised = false;
        plugin.HealthChanged += (sender, args) => eventRaised = true;

        // Act
        plugin.HealthChanged += Raise.Event<EventHandler<PluginHealthChangedEventArgs>>(
            plugin,
            new PluginHealthChangedEventArgs(PluginHealth.Healthy, PluginHealth.Degraded));

        // Assert
        eventRaised.Should().BeTrue();
    }

    #endregion
}
