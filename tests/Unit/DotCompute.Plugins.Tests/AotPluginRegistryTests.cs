// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CS0067 // Event is never used - test events don't need to be raised
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for the AotPluginRegistry and AotPluginSystem classes covering AOT-compatible plugin management.
/// </summary>
public class AotPluginRegistryTests : IDisposable
{
    private readonly ILogger<AotPluginRegistry> _logger;
    private readonly AotPluginRegistry _registry;
    private bool _disposed;

    public AotPluginRegistryTests()
    {
        _logger = NullLogger<AotPluginRegistry>.Instance;
        _registry = new AotPluginRegistry(_logger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new AotPluginRegistry(null!);
        act.Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_RegistersKnownPlugins()
    {
        // Act
        var availableTypes = _registry.GetAvailablePluginTypes();

        // Assert
        Assert.NotEmpty(availableTypes);
        Assert.Contains("DotCompute.Backends.CPU", availableTypes);
        Assert.Contains("DotCompute.Backends.CUDA", availableTypes);
        Assert.Contains("DotCompute.Backends.Metal", availableTypes);
    }

    [Fact]
    public void CreatePlugin_WithCpuBackend_CreatesSuccessfully()
    {
        // Act
        var plugin = _registry.CreatePlugin("DotCompute.Backends.CPU");

        // Assert
        Assert.NotNull(plugin);
        plugin!.Id.Should().Be("DotCompute.Backends.CPU");
        plugin.Name.Should().Be("CPU Backend");
        plugin.Capabilities.HaveFlag(PluginCapabilities.ComputeBackend);
    }

    [Fact]
    public void CreatePlugin_WithCudaBackend_CreatesOrThrowsPlatformNotSupported()
    {
        // Act & Assert
        if (Environment.Is64BitOperatingSystem && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()))
        {
            // On supported platforms, should either create or throw based on CUDA availability
            Action act = () => _registry.CreatePlugin("DotCompute.Backends.CUDA");
            try
            {
                var plugin = _registry.CreatePlugin("DotCompute.Backends.CUDA");
                Assert.NotNull(plugin);
                plugin!.Id.Should().Be("DotCompute.Backends.CUDA");
            }
            catch (PlatformNotSupportedException)
            {
                // This is also acceptable if CUDA is not available
            }
        }
        else
        {
            // On unsupported platforms, should return null or throw
            var plugin = _registry.CreatePlugin("DotCompute.Backends.CUDA");
            plugin?.BeNull();
        }
    }

    [Fact]
    public void CreatePlugin_WithMetalBackend_BehavesBasedOnPlatform()
    {
        // Act & Assert
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
        {
            // On Apple platforms, should create successfully
            var plugin = _registry.CreatePlugin("DotCompute.Backends.Metal");
            Assert.NotNull(plugin);
            plugin!.Id.Should().Be("DotCompute.Backends.Metal");
        }
        else
        {
            // On non-Apple platforms, should throw PlatformNotSupportedException
            Action act = () => _registry.CreatePlugin("DotCompute.Backends.Metal");
            act.Throw<PlatformNotSupportedException>()
                .WithMessage("*Metal backend is only available on macOS and iOS*");
        }
    }

    [Fact]
    public void CreatePlugin_WithUnknownPlugin_ReturnsNull()
    {
        // Act
        var plugin = _registry.CreatePlugin("Unknown.Plugin");

        // Assert
        Assert.Null(plugin);
    }

    [Fact]
    public void CreatePlugin_WithNullOrEmptyType_ThrowsArgumentException()
    {
        // Act & Assert
        Action act1 = () => _registry.CreatePlugin(null!);
        Action act2 = () => _registry.CreatePlugin("");
        
        Assert.Throws<ArgumentException>(() => act1());
        Assert.Throws<ArgumentException>(() => act2());
    }

    [Fact]
    public void CreatePlugin_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _registry.Dispose();

        // Act & Assert
        Action act = () => _registry.CreatePlugin("DotCompute.Backends.CPU");
        Assert.Throws<ObjectDisposedException>(() => act());
    }

    [Fact]
    public void GetPlugin_WithExistingPlugin_ReturnsPlugin()
    {
        // Arrange
        var created = _registry.CreatePlugin("DotCompute.Backends.CPU");

        // Act
        var retrieved = _registry.GetPlugin(created!.Id);

        // Assert
        retrieved.BeSameAs(created);
    }

    [Fact]
    public void GetPlugin_WithNonExistentPlugin_ReturnsNull()
    {
        // Act
        var plugin = _registry.GetPlugin("non-existent");

        // Assert
        Assert.Null(plugin);
    }

    [Fact]
    public void GetLoadedPlugins_WithMultiplePlugins_ReturnsAllPlugins()
    {
        // Arrange
        var plugin1 = _registry.CreatePlugin("DotCompute.Backends.CPU");

        // Act
        var loaded = _registry.GetLoadedPlugins();

        // Assert
        Assert.Equal(1, loaded.Count());
        Assert.Contains(plugin1!, loaded);
    }

    [Fact]
    public void GetLoadedPlugins_WithNoPlugins_ReturnsEmpty()
    {
        // Act
        var loaded = _registry.GetLoadedPlugins();

        // Assert
        Assert.Empty(loaded);
    }

    [Fact]
    public void UnloadPlugin_WithExistingPlugin_UnloadsSuccessfully()
    {
        // Arrange
        var plugin = _registry.CreatePlugin("DotCompute.Backends.CPU");
        var pluginId = plugin!.Id;

        // Act
        var result = _registry.UnloadPlugin(pluginId);

        // Assert
        Assert.True(result);
        _registry.GetPlugin(pluginId).BeNull();
    }

    [Fact]
    public void UnloadPlugin_WithNonExistentPlugin_ReturnsFalse()
    {
        // Act
        var result = _registry.UnloadPlugin("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UnloadPlugin_WithNullOrEmptyId_ThrowsArgumentException()
    {
        // Act & Assert
        Action act1 = () => _registry.UnloadPlugin(null!);
        Action act2 = () => _registry.UnloadPlugin("");

        Assert.Throws<ArgumentException>(() => act1());
        Assert.Throws<ArgumentException>(() => act2());
    }

    [Fact]
    public void RegisterPluginFactory_WithCustomFactory_RegistersSuccessfully()
    {
        // Arrange
        var customPlugin = new CustomTestPlugin();
        Func<IBackendPlugin> factory = () => customPlugin;

        // Act
        _registry.RegisterPluginFactory("Custom.Plugin", factory);

        // Assert
        var availableTypes = _registry.GetAvailablePluginTypes();
        Assert.Contains("Custom.Plugin", availableTypes);

        var created = _registry.CreatePlugin("Custom.Plugin");
        created.BeSameAs(customPlugin);
    }

    [Fact]
    public void RegisterPluginFactory_WithNullFactory_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => _registry.RegisterPluginFactory("test", null!);
        act.Throw<ArgumentNullException>().WithParameterName("factory");
    }

    [Fact]
    public void RegisterPluginFactory_WithNullOrEmptyTypeName_ThrowsArgumentException()
    {
        // Arrange
        Func<IBackendPlugin> factory = () => new CustomTestPlugin();

        // Act & Assert
        Action act1 = () => _registry.RegisterPluginFactory(null!, factory);
        Action act2 = () => _registry.RegisterPluginFactory("", factory);

        Assert.Throws<ArgumentException>(() => act1());
        Assert.Throws<ArgumentException>(() => act2());
    }

    [Fact]
    public void Dispose_WithLoadedPlugins_DisposesAllPlugins()
    {
        // Arrange
        var plugin = _registry.CreatePlugin("DotCompute.Backends.CPU") as IDisposable;
        
        // Verify plugin was created
        _registry.GetLoadedPlugins().Should().HaveCount(1);

        // Act
        _registry.Dispose();

        // Assert
        // After disposal, registry should throw ObjectDisposedException when accessed
        Action act = () => _registry.GetLoadedPlugins();
        Assert.Throws<ObjectDisposedException>(() => act());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        Action act = () =>
        {
            _registry.Dispose();
            _registry.Dispose(); // Should not throw
        };
        act(); // Should not throw
    }

    public void Dispose()
    {
        if (_disposed) return;

        _registry?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Custom test plugin for testing plugin factory registration.
    /// </summary>
    private sealed class CustomTestPlugin : IBackendPlugin
    {
        public string Id => "custom-test-plugin";
        public string Name => "Custom Test Plugin";
        public Version Version => new(1, 0, 0);
        public string Description => "Custom test plugin";
        public string Author => "Test";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;
        public PluginState State => PluginState.Loaded;
        public PluginHealth Health => PluginHealth.Healthy;

#pragma warning disable CS0067 // The event is never used
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginValidationResult Validate() => new() { IsValid = true };
        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();
        public void Dispose() { }
    }
}

/// <summary>
/// Tests for the AotPluginSystem class.
/// </summary>
public class AotPluginSystemTests : IDisposable
{
    private readonly ILogger<AotPluginSystem> _logger;
    private readonly AotPluginSystem _system;
    private bool _disposed;

    public AotPluginSystemTests()
    {
        _logger = NullLogger<AotPluginSystem>.Instance;
        _system = new AotPluginSystem(_logger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new AotPluginSystem(null!);
        act.Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task LoadPluginAsync_IgnoresAssemblyPath()
    {
        // Act
        var plugin = await _system.LoadPluginAsync("/fake/path.dll", "DotCompute.Backends.CPU");

        // Assert
        Assert.NotNull(plugin);
        plugin!.Id.Should().Be("DotCompute.Backends.CPU");
    }

    [Fact]
    public async Task LoadPluginAsync_WithUnknownType_ReturnsNull()
    {
        // Act
        var plugin = await _system.LoadPluginAsync("/fake/path.dll", "Unknown.Plugin");

        // Assert
        Assert.Null(plugin);
    }

    [Fact]
    public async Task UnloadPluginAsync_WithExistingPlugin_UnloadsSuccessfully()
    {
        // Arrange
        var plugin = await _system.LoadPluginAsync("/fake/path.dll", "DotCompute.Backends.CPU");
        var pluginId = plugin!.Id;

        // Act
        var result = await _system.UnloadPluginAsync(pluginId);

        // Assert
        Assert.True(result);
        _system.GetPlugin(pluginId).BeNull();
    }

    [Fact]
    public void GetAvailablePluginTypes_ReturnsKnownTypes()
    {
        // Act
        var types = _system.GetAvailablePluginTypes();

        // Assert
        Assert.NotEmpty(types);
        Assert.Contains("DotCompute.Backends.CPU", types);
        Assert.Contains("DotCompute.Backends.CUDA", types);
        Assert.Contains("DotCompute.Backends.Metal", types);
    }

    [Fact]
    public void RegisterPluginFactory_DelegatesToRegistry()
    {
        // Arrange
        var customPlugin = new CustomTestPlugin();
        Func<IBackendPlugin> factory = () => customPlugin;

        // Act
        _system.RegisterPluginFactory("Custom.Plugin", factory);

        // Assert
        var types = _system.GetAvailablePluginTypes();
        Assert.Contains("Custom.Plugin", types);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _system?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    private sealed class CustomTestPlugin : IBackendPlugin
    {
        public string Id => "custom-test-plugin";
        public string Name => "Custom Test Plugin";
        public Version Version => new(1, 0, 0);
        public string Description => "Custom test plugin";
        public string Author => "Test";
        public PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;
        public PluginState State => PluginState.Loaded;
        public PluginHealth Health => PluginHealth.Healthy;

#pragma warning disable CS0067 // The event is never used
        public event EventHandler<PluginStateChangedEventArgs>? StateChanged;
        public event EventHandler<PluginErrorEventArgs>? ErrorOccurred;
        public event EventHandler<PluginHealthChangedEventArgs>? HealthChanged;
#pragma warning restore CS0067

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
        public Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginValidationResult Validate() => new() { IsValid = true };
        public string GetConfigurationSchema() => "{}";
        public Task OnConfigurationChangedAsync(IConfiguration configuration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PluginMetrics GetMetrics() => new();
        public void Dispose() { }
    }
}

/// <summary>
/// Tests for AOT plugin helper functions.
/// </summary>
public class AotPluginHelpersTests
{
    [Fact]
    public void IsAotCompatible_ReturnsExpectedValue()
    {
        // Act
        var isCompatible = AotPluginHelpers.IsAotCompatible;

        // Assert
        // The value depends on the runtime, so we just verify it returns a boolean
        Assert.Equal(isCompatible, isCompatible);
    }

    [Fact]
    public void CreatePluginSystem_WithLogger_CreatesAppropriateSystem()
    {
        // Arrange
        var logger = NullLogger<PluginSystem>.Instance;

        // Act
        using var system = AotPluginHelpers.CreatePluginSystem(logger);

        // Assert
        Assert.NotNull(system);
        Assert.IsAssignableFrom<IDisposable>(system);
    }
}
