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
        _ = Enum.IsDefined(typeof(PluginCapabilities), "None").Should().BeTrue();
        _ = Enum.IsDefined(typeof(PluginCapabilities), "ComputeBackend").Should().BeTrue();
        _ = Enum.IsDefined(typeof(PluginCapabilities), "StorageProvider").Should().BeTrue();
        _ = Enum.IsDefined(typeof(PluginCapabilities), "NetworkProvider").Should().BeTrue();
        _ = Enum.IsDefined(typeof(PluginCapabilities), "SecurityProvider").Should().BeTrue();
        _ = Enum.IsDefined(typeof(PluginCapabilities), "MonitoringProvider").Should().BeTrue();

        // Verify it's a flags enum
        _ = typeof(PluginCapabilities).Should().BeDecoratedWith<FlagsAttribute>();
    }

    [Fact]
    public void PluginCapabilities_FlagsWork()
    {
        // Arrange
        var combined = PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;

        // Assert
        _ = combined.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
        _ = combined.HasFlag(PluginCapabilities.Scalable).Should().BeTrue();
        _ = combined.HasFlag(PluginCapabilities.NetworkProvider).Should().BeFalse();
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
        _ = Enum.IsDefined(state).Should().BeTrue();
        _ = state.ToString().Should().NotBeNullOrEmpty();
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
        _ = Enum.IsDefined(health).Should().BeTrue();
        _ = health.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void PluginValidationResult_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var result = new PluginValidationResult();

        // Assert
        _ = result.IsValid.Should().BeFalse(); // Default is false
        _ = result.Errors.Should().NotBeNull();
        _ = result.Errors.Should().BeEmpty();
        _ = result.Warnings.Should().NotBeNull();
        _ = result.Warnings.Should().BeEmpty();
        _ = result.Metadata.Should().NotBeNull();
        _ = result.Metadata.Should().BeEmpty();
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
        _ = result.Errors.Count.Should().Be(2);
        _ = result.Errors.Should().Contain("Error 1");
        _ = result.Errors.Should().Contain("Error 2");
        _ = result.Warnings.Count.Should().Be(1);
        _ = result.Warnings.Should().Contain("Warning 1");
        _ = result.Metadata.Count.Should().Be(2);
        _ = result.Metadata["Key1"].Should().Be("Value1");
        _ = result.Metadata["Key2"].Should().Be(42);
    }

    [Fact]
    public void PluginMetrics_DefaultConstructor_InitializesCorrectly()
    {
        // Act
        var metrics = new PluginMetrics();

        // Assert
        _ = metrics.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        _ = metrics.Uptime.Should().Be(TimeSpan.Zero);
        _ = metrics.RequestCount.Should().Be(0);
        _ = metrics.ErrorCount.Should().Be(0);
        _ = metrics.AverageResponseTime.Should().Be(0);
        _ = metrics.MemoryUsage.Should().Be(0);
        _ = metrics.CpuUsage.Should().Be(0);
        _ = metrics.CustomMetrics.Should().NotBeNull();
        _ = metrics.CustomMetrics.Should().BeEmpty();
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
        _ = metrics.Timestamp.Should().Be(timestamp);
        _ = metrics.Uptime.Should().Be(uptime);
        _ = metrics.RequestCount.Should().Be(100);
        _ = metrics.ErrorCount.Should().Be(5);
        _ = metrics.AverageResponseTime.Should().Be(250.5);
        _ = metrics.MemoryUsage.Should().Be(1024 * 1024);
        _ = metrics.CpuUsage.Should().Be(75.8);
        _ = metrics.CustomMetrics.Count.Should().Be(2);
        _ = metrics.CustomMetrics["ThreadCount"].Should().Be(8);
        _ = metrics.CustomMetrics["QueueSize"].Should().Be(25);
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
        _ = eventArgs.OldState.Should().Be(oldState);
        _ = eventArgs.NewState.Should().Be(newState);
        _ = eventArgs.Reason.Should().Be(reason);
        _ = eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
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
        _ = eventArgs.Exception.Should().BeSameAs(exception);
        _ = eventArgs.Context.Should().Be(context);
        _ = eventArgs.AdditionalData.Should().BeSameAs(additionalData);
        _ = eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
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
        _ = eventArgs.AdditionalData.Should().NotBeNull();
        _ = eventArgs.AdditionalData.Should().BeEmpty();
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
        _ = eventArgs.OldHealth.Should().Be(oldHealth);
        _ = eventArgs.NewHealth.Should().Be(newHealth);
        _ = eventArgs.Reason.Should().Be(reason);
        _ = eventArgs.HealthData.Should().BeSameAs(healthData);
        _ = eventArgs.Timestamp.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PluginHealthChangedEventArgs_WithMinimalParameters_SetsDefaults()
    {
        // Act
        var eventArgs = new PluginHealthChangedEventArgs(PluginHealth.Healthy, PluginHealth.Degraded);

        // Assert
        Assert.Null(eventArgs.Reason);
        _ = eventArgs.HealthData.Should().NotBeNull();
        _ = eventArgs.HealthData.Should().BeEmpty();
    }

    [Fact]
    public void IBackendPlugin_InterfaceDefinition_HasAllRequiredMembers()
    {
        // Arrange
        var interfaceType = typeof(IBackendPlugin);

        // Assert - verify interface has all required properties
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Id)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Name)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Version)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Description)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Author)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Capabilities)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.State)).Should().NotBeNull();
        _ = interfaceType.GetProperty(nameof(IBackendPlugin.Health)).Should().NotBeNull();

        // Assert - verify interface has all required methods
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.ConfigureServices)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.InitializeAsync)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.StartAsync)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.StopAsync)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.Validate)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.GetConfigurationSchema)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.OnConfigurationChangedAsync)).Should().NotBeNull();
        _ = interfaceType.GetMethod(nameof(IBackendPlugin.GetMetrics)).Should().NotBeNull();

        // Assert - verify interface has all required events
        _ = interfaceType.GetEvent(nameof(IBackendPlugin.StateChanged)).Should().NotBeNull();
        _ = interfaceType.GetEvent(nameof(IBackendPlugin.ErrorOccurred)).Should().NotBeNull();
        _ = interfaceType.GetEvent(nameof(IBackendPlugin.HealthChanged)).Should().NotBeNull();

        // Assert - verify interface inherits from IDisposable
        _ = typeof(IDisposable).IsAssignableFrom(interfaceType).Should().BeTrue();
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
        _ = plugin.Id.Returns("mock-plugin");
        _ = plugin.Name.Returns("Mock Plugin");
        _ = plugin.Version.Returns(new Version(1, 0, 0));
        _ = plugin.Description.Returns("Mock plugin for testing");
        _ = plugin.Author.Returns("Test Author");
        _ = plugin.Capabilities.Returns(PluginCapabilities.ComputeBackend);
        _ = plugin.State.Returns(PluginState.Loaded);
        _ = plugin.Health.Returns(PluginHealth.Healthy);
        _ = plugin.Validate().Returns(new PluginValidationResult { IsValid = true });
        _ = plugin.GetConfigurationSchema().Returns("{}");
        _ = plugin.GetMetrics().Returns(new PluginMetrics());

        // Act & Assert - verify all properties work
        _ = plugin.Id.Should().Be("mock-plugin");
        _ = plugin.Name.Should().Be("Mock Plugin");
        _ = plugin.Version.Should().Be(new Version(1, 0, 0));
        _ = plugin.Description.Should().Be("Mock plugin for testing");
        _ = plugin.Author.Should().Be("Test Author");
        _ = plugin.Capabilities.Should().Be(PluginCapabilities.ComputeBackend);
        _ = plugin.State.Should().Be(PluginState.Loaded);
        _ = plugin.Health.Should().Be(PluginHealth.Healthy);

        // Act & Assert - verify all methods work
        Action configureServices = () => plugin.ConfigureServices(serviceCollection, configuration);
        configureServices(); // Should not throw

        Func<Task> initialize = () => plugin.InitializeAsync(serviceProvider);
        _ = initialize(); // Should not throw

        Func<Task> start = () => plugin.StartAsync();
        _ = start(); // Should not throw

        Func<Task> stop = () => plugin.StopAsync();
        _ = stop(); // Should not throw

        var validationResult = plugin.Validate();
        Assert.NotNull(validationResult);
        _ = validationResult.IsValid.Should().BeTrue();

        var schema = plugin.GetConfigurationSchema();
        Assert.Equal("{}", schema);

        Func<Task> configChanged = () => plugin.OnConfigurationChangedAsync(configuration);
        _ = configChanged(); // Should not throw

        var metrics = plugin.GetMetrics();
        Assert.NotNull(metrics);

        Action dispose = plugin.Dispose;
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
        _ = multiCapability.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
        _ = multiCapability.HasFlag(PluginCapabilities.StorageProvider).Should().BeTrue();
        _ = multiCapability.HasFlag(PluginCapabilities.MonitoringProvider).Should().BeTrue();
        _ = multiCapability.HasFlag(PluginCapabilities.Scalable).Should().BeTrue();
        _ = multiCapability.HasFlag(PluginCapabilities.NetworkProvider).Should().BeFalse();
        _ = multiCapability.HasFlag(PluginCapabilities.SecurityProvider).Should().BeFalse();

        // Test removing flags
        var withoutStorage = multiCapability & ~PluginCapabilities.StorageProvider;
        _ = withoutStorage.HasFlag(PluginCapabilities.StorageProvider).Should().BeFalse();
        _ = withoutStorage.HasFlag(PluginCapabilities.ComputeBackend).Should().BeTrue();
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
        _ = metrics.MemoryUsage.Should().Be(memoryUsage);
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
        _ = metrics.CpuUsage.Should().Be(cpuUsage);
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
        _ = metrics.CustomMetrics.Count.Should().Be(8);
        _ = metrics.CustomMetrics["String"].Should().Be("test");
        _ = metrics.CustomMetrics["Int"].Should().Be(42);
        _ = metrics.CustomMetrics["Double"].Should().Be(3.14);
        _ = metrics.CustomMetrics["Bool"].Should().Be(true);
        _ = metrics.CustomMetrics["DateTime"].Should().BeOfType<DateTime>();
        _ = metrics.CustomMetrics["TimeSpan"].Should().BeOfType<TimeSpan>();
        _ = metrics.CustomMetrics["Array"].Should().BeEquivalentTo(new[] { 1, 2, 3 });
        _ = metrics.CustomMetrics["Null"].Should().BeNull();
    }
}
