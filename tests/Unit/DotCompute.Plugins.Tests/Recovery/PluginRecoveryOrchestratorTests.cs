// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces.Recovery;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Recovery;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests.Recovery;

/// <summary>
/// Tests for PluginRecoveryOrchestrator covering recovery strategies and plugin health monitoring.
/// </summary>
public sealed class PluginRecoveryOrchestratorTests : IDisposable
{
    private readonly ILogger<PluginRecoveryOrchestrator> _mockLogger;
    private PluginRecoveryOrchestrator? _orchestrator;

    public PluginRecoveryOrchestratorTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginRecoveryOrchestrator>>();
    }

    [Fact]
    public void Constructor_WithLogger_ShouldInitialize()
    {
        // Arrange & Act
        var orchestrator = new PluginRecoveryOrchestrator(_mockLogger);

        // Assert
        orchestrator.Should().NotBeNull();
        orchestrator.Capability.Should().Be(RecoveryCapability.PluginErrors);
        orchestrator.Priority.Should().Be(80);
    }

    [Fact]
    public void Constructor_WithLoggerAndConfig_ShouldInitialize()
    {
        // Arrange
        var config = new PluginRecoveryConfiguration
        {
            MaxRestarts = 5
        };

        // Act
        var orchestrator = new PluginRecoveryOrchestrator(_mockLogger, config);

        // Assert
        orchestrator.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginRecoveryOrchestrator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CanHandle_WithPluginException_ShouldReturnTrue()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new ArgumentException("Plugin error", "plugin");
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = _orchestrator.CanHandle(error, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithReflectionTypeLoadException_ShouldReturnTrue()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new System.Reflection.ReflectionTypeLoadException(Array.Empty<Type>(), Array.Empty<Exception>());
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = _orchestrator.CanHandle(error, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithFileLoadException_ShouldReturnTrue()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new FileLoadException("Failed to load file");
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = _orchestrator.CanHandle(error, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithBadImageFormatException_ShouldReturnTrue()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new BadImageFormatException("Invalid image format");
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = _orchestrator.CanHandle(error, context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanHandle_WithNonPluginException_ShouldReturnFalse()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new InvalidOperationException("Not a plugin error");
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = _orchestrator.CanHandle(error, context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecoverAsync_WithValidContext_ShouldAttemptRecovery()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new FileLoadException("Plugin load failed");
        var context = new PluginRecoveryContext("test-plugin");
        var options = new RecoveryOptions();

        // Act
        var result = await _orchestrator.RecoverAsync(error, context, options);

        // Assert
        result.Should().NotBeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RecoverAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var error = new FileLoadException("Plugin load failed");
        var context = new PluginRecoveryContext("test-plugin");
        var options = new RecoveryOptions();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _orchestrator.RecoverAsync(error, context, options, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteRecoveryAsync_WithRestartStrategy_ShouldExecute()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var context = new PluginRecoveryContext("test-plugin");

        // Act
        var result = await _orchestrator.ExecuteRecoveryAsync(context);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task IsolatePluginAsync_WithValidPlugin_ShouldIsolate()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Id.Returns("test-plugin");

        // Act
        var container = await _orchestrator.IsolatePluginAsync("test-plugin", plugin);

        // Assert
        container.Should().NotBeNull();
        container.PluginId.Should().Be("test-plugin");
    }

    [Fact]
    public void GetHealthReport_Initially_ShouldReturnEmptyReport()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);

        // Act
        var report = _orchestrator.GetHealthReport();

        // Assert
        report.Should().NotBeNull();
    }

    [Fact]
    public async Task EmergencyShutdownAsync_WithValidPluginId_ShouldShutdown()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var pluginId = "test-plugin";

        // Act
        var result = await _orchestrator.EmergencyShutdownAsync(pluginId, "Test shutdown");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmergencyShutdownAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _orchestrator.EmergencyShutdownAsync(
            "test-plugin", "Test shutdown", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void CheckPluginCompatibility_WithValidAssembly_ShouldAnalyze()
    {
        // Arrange
        _orchestrator = new PluginRecoveryOrchestrator(_mockLogger);
        var assembly = typeof(PluginRecoveryOrchestratorTests).Assembly;

        // Act
        var result = _orchestrator.CheckPluginCompatibility("test-plugin", assembly);

        // Assert
        result.Should().NotBeNull();
        result.PluginId.Should().Be("test-plugin");
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        var orchestrator = new PluginRecoveryOrchestrator(_mockLogger);

        // Act
        orchestrator.Dispose();

        // Assert - should not throw
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var orchestrator = new PluginRecoveryOrchestrator(_mockLogger);

        // Act
        orchestrator.Dispose();
        orchestrator.Dispose();

        // Assert - should not throw
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();
    }
}

/// <summary>
/// Tests for PluginRecoveryContext class.
/// </summary>
public sealed class PluginRecoveryContextTests
{
    [Fact]
    public void Constructor_WithPluginId_ShouldInitializeProperties()
    {
        // Arrange & Act
        var context = new PluginRecoveryContext("test-plugin");

        // Assert
        context.Should().NotBeNull();
        context.PluginId.Should().Be("test-plugin");
    }

    [Fact]
    public void Constructor_WithPluginIdAndPlugin_ShouldInitializeBoth()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();

        // Act
        var context = new PluginRecoveryContext("test-plugin", plugin);

        // Assert
        context.PluginId.Should().Be("test-plugin");
        context.Plugin.Should().BeSameAs(plugin);
    }

    [Fact]
    public void PluginId_ShouldBeSettable()
    {
        // Arrange
        var context = new PluginRecoveryContext("initial-id");
        var pluginId = "test-plugin";

        // Act
        context.PluginId = pluginId;

        // Assert
        context.PluginId.Should().Be(pluginId);
    }

    [Fact]
    public void Plugin_ShouldBeSettable()
    {
        // Arrange
        var context = new PluginRecoveryContext("test-plugin");
        var plugin = Substitute.For<IBackendPlugin>();

        // Act
        context.Plugin = plugin;

        // Assert
        context.Plugin.Should().BeSameAs(plugin);
    }

    [Fact]
    public void FromException_ShouldCreateContextWithException()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var context = PluginRecoveryContext.FromException("test-plugin", exception);

        // Assert
        context.PluginId.Should().Be("test-plugin");
        context.Exception.Should().BeSameAs(exception);
        context.OperationType.Should().Be("InvalidOperationException");
    }
}

/// <summary>
/// Tests for PluginRecoveryConfiguration class.
/// </summary>
public sealed class PluginRecoveryConfigurationTests
{
    [Fact]
    public void Default_ShouldReturnConfiguration()
    {
        // Arrange & Act
        var config = PluginRecoveryConfiguration.Default;

        // Assert
        config.Should().NotBeNull();
        config.MaxRestarts.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MaxRestarts_ShouldBeSettable()
    {
        // Arrange
        var config = new PluginRecoveryConfiguration();

        // Act
        config.MaxRestarts = 10;

        // Assert
        config.MaxRestarts.Should().Be(10);
    }

    [Fact]
    public void EnableAutoRestart_ShouldBeSettable()
    {
        // Arrange
        var config = new PluginRecoveryConfiguration();

        // Act
        config.EnableAutoRestart = false;

        // Assert
        config.EnableAutoRestart.Should().BeFalse();
    }

    [Fact]
    public void EnablePluginIsolation_ShouldBeSettable()
    {
        // Arrange
        var config = new PluginRecoveryConfiguration();

        // Act
        config.EnablePluginIsolation = true;

        // Assert
        config.EnablePluginIsolation.Should().BeTrue();
    }
}

/// <summary>
/// Tests for PluginCompatibilityResult class.
/// </summary>
public sealed class PluginCompatibilityResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeCollections()
    {
        // Arrange & Act
        var result = new PluginCompatibilityResult();

        // Assert
        result.DependencyConflicts.Should().NotBeNull();
        result.SecurityIssues.Should().NotBeNull();
    }

    [Fact]
    public void IsCompatible_ShouldBeSettable()
    {
        // Arrange
        var result = new PluginCompatibilityResult();

        // Act
        result.IsCompatible = true;

        // Assert
        result.IsCompatible.Should().BeTrue();
    }

    [Fact]
    public void FrameworkCompatible_ShouldBeSettable()
    {
        // Arrange
        var result = new PluginCompatibilityResult();

        // Act
        result.FrameworkCompatible = true;

        // Assert
        result.FrameworkCompatible.Should().BeTrue();
    }
}
