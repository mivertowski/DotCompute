// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CS0067 // Event is never used - test events don't need to be raised
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Exceptions;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Reflection;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for the PluginSystem class covering plugin loading, unloading, and lifecycle management.
/// </summary>
public class PluginSystemTests : IDisposable
{
    private readonly ILogger<PluginSystem> _logger;
    private readonly PluginSystem _pluginSystem;
    private readonly TestPlugin _testPlugin;
    private bool _disposed;

    public PluginSystemTests()
    {
        _logger = NullLogger<PluginSystem>.Instance;
        
        // Create a minimal service provider for plugin initialization
        var services = new ServiceCollection();
        services.AddSingleton(_logger);
        var serviceProvider = services.BuildServiceProvider();
        
        _pluginSystem = new PluginSystem(_logger, serviceProvider);
        _testPlugin = new TestPlugin();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new PluginSystem((ILogger<PluginSystem>)null!);
        act.Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithOptions_CreatesSuccessfully()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        var pluginSystem = new PluginSystem(options);

        // Assert
        Assert.NotNull(pluginSystem);
        pluginSystem.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsInitializedToTrue()
    {
        // Act
        await _pluginSystem.InitializeAsync();

        // Assert
        _pluginSystem.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCancellation_CompletesSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await _pluginSystem.InitializeAsync(cts.Token);

        // Assert
        _pluginSystem.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task LoadPluginAsync_WithValidPlugin_LoadsSuccessfully()
    {
        // Act
        var result = await _pluginSystem.LoadPluginAsync(_testPlugin);

        // Assert
        Assert.NotNull(result);
        result.BeSameAs(_testPlugin);
        _pluginSystem.GetPlugin(_testPlugin.Id).BeSameAs(_testPlugin);
    }

    [Fact]
    public async Task LoadPluginAsync_WithNullPlugin_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => FluentActions.MethodCall().AsTask());
    }

    [Fact]
    public async Task LoadPluginAsync_WithInvalidPlugin_ThrowsPluginLoadException()
    {
        // Arrange
        var invalidPlugin = new InvalidTestPlugin();

        // Act & Assert
        await Assert.ThrowsAsync<PluginLoadException>(() => FluentActions.MethodCall().AsTask())
            .WithMessage("Plugin validation failed*");
    }

    [Fact]
    public async Task LoadPluginAsync_WithDisposedSystem_ThrowsObjectDisposedException()
    {
        // Arrange
        _pluginSystem.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());
    }

    [Fact]
    public async Task UnloadPluginAsync_WithValidPlugin_UnloadsSuccessfully()
    {
        // Arrange
        await _pluginSystem.LoadPluginAsync(_testPlugin);

        // Act
        var result = await _pluginSystem.UnloadPluginAsync(_testPlugin.Id);

        // Assert
        Assert.True(result);
        _pluginSystem.GetPlugin(_testPlugin.Id).BeNull();
    }

    [Fact]
    public async Task UnloadPluginAsync_WithNonExistentPlugin_ReturnsFalse()
    {
        // Act
        var result = await _pluginSystem.UnloadPluginAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task UnloadPluginAsync_WithDisposedSystem_ThrowsObjectDisposedException()
    {
        // Arrange
        _pluginSystem.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());
    }

    [Fact]
    public void GetPlugin_WithExistingPlugin_ReturnsPlugin()
    {
        // Arrange
        _pluginSystem.LoadPluginAsync(_testPlugin).Wait();

        // Act
        var result = _pluginSystem.GetPlugin(_testPlugin.Id);

        // Assert
        result.BeSameAs(_testPlugin);
    }

    [Fact]
    public void GetPlugin_WithNonExistentPlugin_ReturnsNull()
    {
        // Act
        var result = _pluginSystem.GetPlugin("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetLoadedPlugins_WithLoadedPlugins_ReturnsAllPlugins()
    {
        // Arrange
        _pluginSystem.LoadPluginAsync(_testPlugin).Wait();

        // Act
        var result = _pluginSystem.GetLoadedPlugins();

        // Assert
        Assert.Equal(1, result.Count());
        Assert.Contains(_testPlugin, result);
    }

    [Fact]
    public void GetLoadedPlugins_WithNoPlugins_ReturnsEmpty()
    {
        // Act
        var result = _pluginSystem.GetLoadedPlugins();

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("TestPlugin")]
    [InlineData("AnotherPlugin")]
    [InlineData("ComplexPluginName.With.Dots")]
    public async Task LoadPluginAsync_WithAssemblyPath_HandlesPluginTypeNames(string pluginTypeName)
    {
        // Act
        var result = await _pluginSystem.LoadPluginAsync("/fake/path.dll", pluginTypeName);

        // Assert
        // Should return null since we don't have a real assembly at the path
        Assert.Null(result);
    }

    [Fact]
    public void DiscoverPluginTypes_WithValidAssembly_ReturnsPluginTypes()
    {
        // Arrange
        var assembly = typeof(TestPlugin).Assembly;

        // Act
        var result = PluginSystem.DiscoverPluginTypes(assembly);

        // Assert
        Assert.NotEmpty(result);
        result.Contain(typeof(TestPlugin));
    }

    [Fact]
    public async Task LoadPluginAsync_ConcurrentLoading_HandlesThreadSafety()
    {
        // Arrange
        var plugins = Enumerable.Range(0, 10).Select(i => new TestPlugin($"test-{i}")).ToArray();

        // Act
        var loadTasks = plugins.Select(p => _pluginSystem.LoadPluginAsync(p));
        var results = await Task.WhenAll(loadTasks);

        // Assert
        Assert.Equal(10, results.Count());
        results.OnlyContain(r => r != null);
        _pluginSystem.GetLoadedPlugins().Should().HaveCount(10);
    }

    [Fact]
    public async Task LoadPluginAsync_WithPluginThatThrowsOnValidation_HandlesGracefully()
    {
        // Arrange
        var plugin = Substitute.For<IBackendPlugin>();
        plugin.Id.Returns("throwing-plugin");
        plugin.Validate().Returns(callInfo => throw new InvalidOperationException("Validation failed"));

        // Act & Assert
        await Assert.ThrowsAsync<PluginLoadException>(() => FluentActions.MethodCall().AsTask())
            .WithInnerException(typeof(InvalidOperationException));
    }

    [Fact]
    public void Dispose_WithLoadedPlugins_DisposesAllPlugins()
    {
        // Arrange
        var plugin1 = new TestPlugin("plugin1");
        var plugin2 = new TestPlugin("plugin2");
        _pluginSystem.LoadPluginAsync(plugin1).Wait();
        _pluginSystem.LoadPluginAsync(plugin2).Wait();

        // Act
        _pluginSystem.Dispose();

        // Assert
        plugin1.IsDisposed.Should().BeTrue();
        plugin2.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesShould().NotThrow()
    {
        // Act & Assert
        Action act = () =>
        {
            _pluginSystem.Dispose();
            _pluginSystem.Dispose(); // Second call should not throw
        };
        act(); // Should not throw
    }

    public void Dispose()
    {
        if (_disposed) return;

        _pluginSystem?.Dispose();
        _testPlugin?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Test plugin implementation for testing purposes.
    /// </summary>
    private sealed class TestPlugin : IBackendPlugin
    {
        private PluginState _state = PluginState.Loaded;
        private readonly PluginHealth _health = PluginHealth.Healthy;

        public TestPlugin(string? id = null)
        {
            Id = id ?? "test-plugin";
        }

        public string Id { get; }
        public string Name => "Test Plugin";
        public Version Version => new(1, 0, 0);
        public string Description => "Test plugin for unit tests";
        public string Author => "Test Author";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;
        public PluginState State => _state;
        public PluginHealth Health => _health;
        public bool IsDisposed { get; private set; }

#pragma warning disable CS0067 // The event is never used
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            _state = PluginState.Initialized;
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _state = PluginState.Running;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _state = PluginState.Stopped;
            return Task.CompletedTask;
        }

        public PluginValidationResult Validate() => new() { IsValid = true };

        public string GetConfigurationSchema() => "{}";

        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public PluginMetrics GetMetrics() => new();

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    /// <summary>
    /// Invalid test plugin that fails validation.
    /// </summary>
    private sealed class InvalidTestPlugin : IBackendPlugin
    {
        public string Id => "invalid-plugin";
        public string Name => "";  // Invalid - empty name
        public Version Version => new(1, 0, 0);
        public string Description => "Invalid plugin";
        public string Author => "Test";
        public PluginCapabilities Capabilities => PluginCapabilities.None;
        public PluginState State => PluginState.Unknown;
        public PluginHealth Health => PluginHealth.Unknown;

#pragma warning disable CS0067 // The event is never used
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public PluginValidationResult Validate()
        {
            return new PluginValidationResult
            {
                IsValid = false,
                Errors = { "Plugin name is empty" }
            };
        }

        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();
        public void Dispose() { }
    }
}
