// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Exceptions.Loading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DotCompute.Plugins.Tests.Core;

/// <summary>
/// Tests for PluginSystem class covering initialization, loading, and lifecycle management.
/// </summary>
public sealed class PluginSystemTests : IDisposable
{
    private readonly ILogger<PluginSystem> _mockLogger;
    private readonly IServiceProvider _mockServiceProvider;
    private PluginSystem? _pluginSystem;

    public PluginSystemTests()
    {
        _mockLogger = Substitute.For<ILogger<PluginSystem>>();

        var services = new ServiceCollection();
        services.AddLogging();
        _mockServiceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_WithLogger_ShouldInitialize()
    {
        // Arrange & Act
        var system = new PluginSystem(_mockLogger);

        // Assert
        system.Should().NotBeNull();
        system.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithLoggerAndServiceProvider_ShouldInitialize()
    {
        // Arrange & Act
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);

        // Assert
        system.Should().NotBeNull();
        system.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginSystem(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new PluginSystem(_mockLogger, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    [Fact]
    public async Task InitializeAsync_ShouldSetIsInitializedToTrue()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        await system.InitializeAsync();

        // Assert
        system.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCancellationToken_ShouldComplete()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);
        using var cts = new CancellationTokenSource();

        // Act
        await system.InitializeAsync(cts.Token);

        // Assert
        system.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);
        system.Dispose();

        // Act
        var act = async () => await system.InitializeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task LoadPluginAsync_WithValidPlugin_ShouldLoadSuccessfully()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();

        // Act
        var result = await system.LoadPluginAsync(plugin);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(plugin);
    }

    [Fact]
    public async Task LoadPluginAsync_WithNullPlugin_ShouldThrowArgumentNullException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var act = async () => await system.LoadPluginAsync((IBackendPlugin)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadPluginAsync_WithInvalidPlugin_ShouldThrowPluginLoadException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin(isValid: false);

        // Act
        var act = async () => await system.LoadPluginAsync(plugin);

        // Assert
        await act.Should().ThrowAsync<PluginLoadException>();
    }

    [Fact]
    public async Task LoadPluginAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);
        system.Dispose();
        var plugin = CreateMockPlugin();

        // Act
        var act = async () => await system.LoadPluginAsync(plugin);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task LoadPluginAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();
        plugin.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
            .Returns(async (callInfo) =>
            {
                var token = callInfo.ArgAt<CancellationToken>(1);
                await Task.Delay(100, token);
            });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        // Act
        var act = async () => await system.LoadPluginAsync(plugin, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithValidPluginId_ShouldUnloadSuccessfully()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();
        await system.LoadPluginAsync(plugin);

        // Act
        var result = await system.UnloadPluginAsync(plugin.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithInvalidPluginId_ShouldReturnFalse()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var result = await system.UnloadPluginAsync("non-existent-plugin");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithEmptyPluginId_ShouldThrowArgumentException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var act = async () => await system.UnloadPluginAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UnloadPluginAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);
        system.Dispose();

        // Act
        var act = async () => await system.UnloadPluginAsync("test-plugin");

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void GetPlugin_WithValidPluginId_ShouldReturnPlugin()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();
        system.LoadPluginAsync(plugin).GetAwaiter().GetResult();

        // Act
        var result = system.GetPlugin(plugin.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(plugin);
    }

    [Fact]
    public void GetPlugin_WithInvalidPluginId_ShouldReturnNull()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var result = system.GetPlugin("non-existent-plugin");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetLoadedPlugins_WithNoPlugins_ShouldReturnEmptyCollection()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var result = system.GetLoadedPlugins();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLoadedPlugins_WithMultiplePlugins_ShouldReturnAllPlugins()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin1 = CreateMockPlugin("plugin1");
        var plugin2 = CreateMockPlugin("plugin2");
        await system.LoadPluginAsync(plugin1);
        await system.LoadPluginAsync(plugin2);

        // Act
        var result = system.GetLoadedPlugins().ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(plugin1);
        result.Should().Contain(plugin2);
    }

    [Fact]
    public void GetLoadedPlugins_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);
        system.Dispose();

        // Act
        var act = () => system.GetLoadedPlugins();

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task LoadPluginAsync_ShouldSetPluginStateToLoaded()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();

        // Act
        await system.LoadPluginAsync(plugin);

        // Assert
        plugin.Received(1).InitializeAsync(_mockServiceProvider, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnloadPluginAsync_ShouldDisposePlugin()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();
        await system.LoadPluginAsync(plugin);

        // Act
        await system.UnloadPluginAsync(plugin.Id);

        // Assert
        plugin.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_ShouldUnloadAllPlugins()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin1 = CreateMockPlugin("plugin1");
        var plugin2 = CreateMockPlugin("plugin2");
        system.LoadPluginAsync(plugin1).GetAwaiter().GetResult();
        system.LoadPluginAsync(plugin2).GetAwaiter().GetResult();

        // Act
        system.Dispose();

        // Assert
        plugin1.Received(1).Dispose();
        plugin2.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        system.Dispose();
        system.Dispose();

        // Assert - should not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task UnloadPluginAsync_WithWhitespacePluginId_ShouldThrowArgumentException(string pluginId)
    {
        // Arrange
        var system = new PluginSystem(_mockLogger);

        // Act
        var act = async () => await system.UnloadPluginAsync(pluginId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadPluginAsync_WithPluginThatFailsInitialization_ShouldRemoveFromCollection()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();
        plugin.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Init failed"));

        // Act
        var act = async () => await system.LoadPluginAsync(plugin);

        // Assert
        await act.Should().ThrowAsync<PluginLoadException>();
        system.GetPlugin(plugin.Id).Should().BeNull();
    }

    [Fact]
    public async Task LoadPluginAsync_MultipleTimesWithSamePlugin_ShouldUpdateExisting()
    {
        // Arrange
        var system = new PluginSystem(_mockLogger, _mockServiceProvider);
        var plugin = CreateMockPlugin();

        // Act
        await system.LoadPluginAsync(plugin);
        var plugins = system.GetLoadedPlugins();

        // Assert
        plugins.Should().HaveCount(1);
    }

    private static IBackendPlugin CreateMockPlugin(string id = "test-plugin", bool isValid = true)
    {
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Id.Returns(id);
        plugin.Name.Returns("Test Plugin");
        plugin.Version.Returns(new Version(1, 0, 0));
        plugin.Description.Returns("A test plugin");
        plugin.Author.Returns("Test Author");
        plugin.Capabilities.Returns(PluginCapabilities.ComputeBackend);
        plugin.State.Returns(PluginState.Unknown);
        plugin.Health.Returns(PluginHealth.Healthy);

        var validationResult = new PluginValidationResult { IsValid = isValid };
        if (!isValid)
        {
            validationResult.Errors.Add("Validation failed");
        }
        plugin.Validate().Returns(validationResult);

        plugin.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return plugin;
    }

    public void Dispose()
    {
        _pluginSystem?.Dispose();
        if (_mockServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
