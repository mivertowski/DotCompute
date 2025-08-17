// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for plugin interfaces, enums, and data structures.
/// </summary>
public sealed class PluginInterfaceTests
{
    private static readonly int[] _testArray = [1, 2, 3];
    [Fact]
    public void PluginCapabilities_HasCorrectValues()
    {
        // Assert - verify the enum has expected values
        Enum.IsDefined(typeof(PluginCapabilities), "None").Should().BeTrue();
        Enum.IsDefined(typeof(PluginCapabilities), "ComputeBackend").Should().BeTrue();
        Enum.IsDefined(typeof(PluginCapabilities), "StorageProvider").Should().BeTrue();
        Enum.IsDefined(typeof(PluginCapabilities), "NetworkProvider").Should().BeTrue();
        Enum.IsDefined(typeof(PluginCapabilities), "SecurityProvider").Should().BeTrue();
        Enum.IsDefined(typeof(PluginCapabilities), "MonitoringProvider").Should().BeTrue();

        // Verify it's a flags enum
        typeof(PluginCapabilities).Should().BeDecoratedWith<FlagsAttribute>();
    }

    [Fact]
    public void PluginCapabilities_FlagsWork()
    {
        // Arrange
        var combined = PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;

        // Assert
        combined.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
        combined.HasFlag(PluginCapabilities.Scalable).Should().BeTrue();
        combined.HasFlag(PluginCapabilities.NetworkProvider).Should().BeFalse();
    }

    [Theory]
    [InlineData(PluginState.Unknown)]
    [InlineData(PluginState.Loading)]
    [InlineData(PluginState.Loaded)]
    [InlineData(PluginState.Initializing)]
    [InlineData(PluginState.Initialized)]
    [InlineData(PluginState.Starting)]
    [InlineData(PluginState.Running)]
    [InlineData(PluginState.Stopping)]
    [InlineData(PluginState.Stopped)]
    [InlineData(PluginState.Failed)]
    [InlineData(PluginState.Unloading)]
    [InlineData(PluginState.Unloaded)]
    public void PluginState_AllValuesAreDefined(PluginState state)
    {
        // Assert
        Enum.IsDefined(state).Should().BeTrue();
        state.ToString().Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(PluginHealth.Unknown)]
    [InlineData(PluginHealth.Healthy)]
    [InlineData(PluginHealth.Degraded)]
    [InlineData(PluginHealth.Unhealthy)]
    [InlineData(PluginHealth.Critical)]
    public void PluginHealth_AllValuesAreDefined(PluginHealth health)
    {
        // Assert
        Enum.IsDefined(health).Should().BeTrue();
        health.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginValidationResult_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var result = new PluginValidationResult();

        // Assert
        result.IsValid.Should().BeFalse(); // Default is false
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().NotBeNull();
        result.Warnings.Should().BeEmpty();
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void PluginValidationResult_CanAddErrorsAndWarnings()
    {
        // Arrange
        var result = new PluginValidationResult();

        // Act
        result.Errors.Add("Error 1");
        result.Errors.Add("Error 2");
        result.Warnings.Add("Warning 1");
        result.Metadata["Key1"] = "Value1";
        result.Metadata["Key2"] = 42;

        // Assert
        result.Errors.Count.Should().Be(2);
        result.Errors.Should().Contain("Error 1");
        result.Errors.Should().Contain("Error 2");
        result.Warnings.Count.Should().Be(1);
        result.Warnings.Should().Contain("Warning 1");
        result.Metadata.Count.Should().Be(2);
        result.Metadata["Key1"].Should().Be("Value1");
        result.Metadata["Key2"].Should().Be(42);
    }

    [Fact]
    public void PluginMetrics_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var metrics = new PluginMetrics();

        // Assert
        metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        metrics.Uptime.Should().Be(TimeSpan.Zero);
        metrics.RequestCount.Should().Be(0);
        metrics.ErrorCount.Should().Be(0);
        metrics.AverageResponseTime.Should().Be(0);
        metrics.MemoryUsage.Should().Be(0);
        metrics.CpuUsage.Should().Be(0);
        metrics.CustomMetrics.Should().NotBeNull();
        metrics.CustomMetrics.Should().BeEmpty();
    }

    [Fact]
    public void PluginMetrics_CanUpdateAllProperties()
    {
        // Arrange
        var metrics = new PluginMetrics();
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var uptime = TimeSpan.FromMinutes(10);

        // Act
        metrics.Timestamp = timestamp;
        metrics.Uptime = uptime;
        metrics.RequestCount = 100;
        metrics.ErrorCount = 5;
        metrics.AverageResponseTime = 250.5;
        metrics.MemoryUsage = 1024 * 1024; // 1MB
        metrics.CpuUsage = 75.8;
        metrics.CustomMetrics["ThreadCount"] = 8;
        metrics.CustomMetrics["QueueSize"] = 25;

        // Assert
        metrics.Timestamp.Should().Be(timestamp);
        metrics.Uptime.Should().Be(uptime);
        metrics.RequestCount.Should().Be(100);
        metrics.ErrorCount.Should().Be(5);
        metrics.AverageResponseTime.Should().Be(250.5);
        metrics.MemoryUsage.Should().Be(1024 * 1024);
        metrics.CpuUsage.Should().Be(75.8);
        metrics.CustomMetrics.Count.Should().Be(2);
        metrics.CustomMetrics["ThreadCount"].Should().Be(8);
        metrics.CustomMetrics["QueueSize"].Should().Be(25);
    }

    [Fact]
    public void PluginStateChangedEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var oldState = PluginState.Loaded;
        var newState = PluginState.Running;
        var reason = "Successfully started";

        // Act
        var eventArgs = new PluginStateChangedEventArgs(oldState, newState, reason);

        // Assert
        eventArgs.OldState.Should().Be(oldState);
        eventArgs.NewState.Should().Be(newState);
        eventArgs.Reason.Should().Be(reason);
        eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PluginStateChangedEventArgs_WithoutReason_SetsReasonToNull()
    {
        // Act
        var eventArgs = new PluginStateChangedEventArgs(PluginState.Loaded, PluginState.Running);

        // Assert
        Assert.Null(eventArgs.Reason);
    }

    [Fact]
    public void PluginErrorEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var context = "During initialization";
        var additionalData = new Dictionary<string, object>
        {
            ["PluginVersion"] = "1.0.0",
            ["ErrorCode"] = 500
        };

        // Act
        var eventArgs = new PluginErrorEventArgs(exception, context, additionalData);

        // Assert
        eventArgs.Exception.Should().BeSameAs(exception);
        eventArgs.Context.Should().Be(context);
        eventArgs.AdditionalData.Should().BeSameAs(additionalData);
        eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PluginErrorEventArgs_WithoutAdditionalData_InitializesEmptyDictionary()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var context = "During initialization";

        // Act
        var eventArgs = new PluginErrorEventArgs(exception, context);

        // Assert
        eventArgs.AdditionalData.Should().NotBeNull();
        eventArgs.AdditionalData.Should().BeEmpty();
    }

    [Fact]
    public void PluginHealthChangedEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var oldHealth = PluginHealth.Healthy;
        var newHealth = PluginHealth.Degraded;
        var reason = "High memory usage detected";
        var healthData = new Dictionary<string, object>
        {
            ["MemoryUsagePercent"] = 85.5,
            ["CpuUsagePercent"] = 45.2
        };

        // Act
        var eventArgs = new PluginHealthChangedEventArgs(oldHealth, newHealth, reason, healthData);

        // Assert
        eventArgs.OldHealth.Should().Be(oldHealth);
        eventArgs.NewHealth.Should().Be(newHealth);
        eventArgs.Reason.Should().Be(reason);
        eventArgs.HealthData.Should().BeSameAs(healthData);
        eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PluginHealthChangedEventArgs_WithMinimalParameters_SetsDefaults()
    {
        // Act
        var eventArgs = new PluginHealthChangedEventArgs(PluginHealth.Healthy, PluginHealth.Degraded);

        // Assert
        Assert.Null(eventArgs.Reason);
        eventArgs.HealthData.Should().NotBeNull();
        eventArgs.HealthData.Should().BeEmpty();
    }

    [Fact]
    public void IBackendPlugin_InterfaceDefinition_HasAllRequiredMembers()
    {
        // Arrange
        var interfaceType = typeof(IBackendPlugin);

        // Assert - verify interface has all required properties
        interfaceType.GetProperty(nameof(IBackendPlugin.Id)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Name)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Version)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Description)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Author)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Capabilities)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.State)).Should().NotBeNull();
        interfaceType.GetProperty(nameof(IBackendPlugin.Health)).Should().NotBeNull();

        // Assert - verify interface has all required methods
        interfaceType.GetMethod(nameof(IBackendPlugin.ConfigureServices)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.InitializeAsync)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.StartAsync)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.StopAsync)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.Validate)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.GetConfigurationSchema)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.OnConfigurationChangedAsync)).Should().NotBeNull();
        interfaceType.GetMethod(nameof(IBackendPlugin.GetMetrics)).Should().NotBeNull();

        // Assert - verify interface has all required events
        interfaceType.GetEvent(nameof(IBackendPlugin.StateChanged)).Should().NotBeNull();
        interfaceType.GetEvent(nameof(IBackendPlugin.ErrorOccurred)).Should().NotBeNull();
        interfaceType.GetEvent(nameof(IBackendPlugin.HealthChanged)).Should().NotBeNull();

        // Assert - verify interface inherits from IDisposable
        typeof(IDisposable).IsAssignableFrom(interfaceType).Should().BeTrue();
    }

    [Fact]
    public void MockPlugin_ImplementsInterface_CorrectlyFollowsContract()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        var serviceCollection = new ServiceCollection();
        var configuration = Substitute.For<IConfiguration>();
        var serviceProvider = Substitute.For<IServiceProvider>();

        // Setup mock behavior
        plugin.Id.Returns("mock-plugin");
        plugin.Name.Returns("Mock Plugin");
        plugin.Version.Returns(new Version(1, 0, 0));
        plugin.Description.Returns("Mock plugin for testing");
        plugin.Author.Returns("Test Author");
        plugin.Capabilities.Returns(PluginCapabilities.ComputeBackend);
        plugin.State.Returns(PluginState.Loaded);
        plugin.Health.Returns(PluginHealth.Healthy);
        plugin.Validate().Returns(new PluginValidationResult { IsValid = true });
        plugin.GetConfigurationSchema().Returns("{}");
        plugin.GetMetrics().Returns(new PluginMetrics());

        // Act & Assert - verify all properties work
        plugin.Id.Should().Be("mock-plugin");
        plugin.Name.Should().Be("Mock Plugin");
        plugin.Version.Should().Be(new Version(1, 0, 0));
        plugin.Description.Should().Be("Mock plugin for testing");
        plugin.Author.Should().Be("Test Author");
        plugin.Capabilities.Should().Be(PluginCapabilities.ComputeBackend);
        plugin.State.Should().Be(PluginState.Loaded);
        plugin.Health.Should().Be(PluginHealth.Healthy);

        // Act & Assert - verify all methods work
        Action configureServices = () => plugin.ConfigureServices(serviceCollection, configuration);
        configureServices(); // Should not throw

        Func<Task> initialize = () => plugin.InitializeAsync(serviceProvider);
        initialize(); // Should not throw

        Func<Task> start = () => plugin.StartAsync();
        start(); // Should not throw

        Func<Task> stop = () => plugin.StopAsync();
        stop(); // Should not throw

        var validationResult = plugin.Validate();
        Assert.NotNull(validationResult);
        validationResult.IsValid.Should().BeTrue();

        var schema = plugin.GetConfigurationSchema();
        Assert.Equal("{}", schema);

        Func<Task> configChanged = () => plugin.OnConfigurationChangedAsync(configuration);
        configChanged(); // Should not throw

        var metrics = plugin.GetMetrics();
        Assert.NotNull(metrics);

        Action dispose = () => plugin.Dispose();
        dispose(); // Should not throw
    }

    [Fact]
    public void PluginCapabilities_CombinationFlags_WorkCorrectly()
    {
        // Arrange
        var multiCapability = PluginCapabilities.ComputeBackend |
                             PluginCapabilities.StorageProvider |
                             PluginCapabilities.MonitoringProvider |
                             PluginCapabilities.Scalable;

        // Assert
        multiCapability.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
        multiCapability.HasFlag(PluginCapabilities.StorageProvider).Should().BeTrue();
        multiCapability.HasFlag(PluginCapabilities.MonitoringProvider).Should().BeTrue();
        multiCapability.HasFlag(PluginCapabilities.Scalable).Should().BeTrue();
        multiCapability.HasFlag(PluginCapabilities.NetworkProvider).Should().BeFalse();
        multiCapability.HasFlag(PluginCapabilities.SecurityProvider).Should().BeFalse();

        // Test removing flags
        var withoutStorage = multiCapability & ~PluginCapabilities.StorageProvider;
        withoutStorage.HasFlag(PluginCapabilities.StorageProvider).Should().BeFalse();
        withoutStorage.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1024L)]
    [InlineData(1024L * 1024L)]
    [InlineData(long.MaxValue)]
    public void PluginMetrics_MemoryUsage_AcceptsValidValues(long memoryUsage)
    {
        // Arrange
        var metrics = new PluginMetrics();

        // Act
        metrics.MemoryUsage = memoryUsage;

        // Assert
        metrics.MemoryUsage.Should().Be(memoryUsage);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.5)]
    [InlineData(100.0)]
    public void PluginMetrics_CpuUsage_AcceptsValidValues(double cpuUsage)
    {
        // Arrange
        var metrics = new PluginMetrics();

        // Act
        metrics.CpuUsage = cpuUsage;

        // Assert
        metrics.CpuUsage.Should().Be(cpuUsage);
    }

    [Fact]
    public void PluginMetrics_CustomMetrics_SupportsVariousTypes()
    {
        // Arrange
        var metrics = new PluginMetrics();

        // Act
        metrics.CustomMetrics["String"] = "test";
        metrics.CustomMetrics["Int"] = 42;
        metrics.CustomMetrics["Double"] = 3.14;
        metrics.CustomMetrics["Bool"] = true;
        metrics.CustomMetrics["DateTime"] = DateTime.UtcNow;
        metrics.CustomMetrics["TimeSpan"] = TimeSpan.FromMinutes(5);
        metrics.CustomMetrics["Array"] = _testArray;
        metrics.CustomMetrics["Null"] = null!;

        // Assert
        metrics.CustomMetrics.Count.Should().Be(8);
        metrics.CustomMetrics["String"].Should().Be("test");
        metrics.CustomMetrics["Int"].Should().Be(42);
        metrics.CustomMetrics["Double"].Should().Be(3.14);
        metrics.CustomMetrics["Bool"].Should().Be(true);
        metrics.CustomMetrics["DateTime"].Should().BeOfType<DateTime>();
        metrics.CustomMetrics["TimeSpan"].Should().BeOfType<TimeSpan>();
        metrics.CustomMetrics["Array"].Should().BeEquivalentTo(new[] { 1, 2, 3 });
        metrics.CustomMetrics["Null"].Should().BeNull();
    }
}
