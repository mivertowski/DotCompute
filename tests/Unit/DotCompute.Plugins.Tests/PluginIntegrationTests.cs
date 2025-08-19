// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Configuration;
using DotCompute.Plugins.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit
{

/// <summary>
/// Integration tests for the plugin system covering end-to-end scenarios, dependency resolution,
/// and complex plugin interactions.
/// </summary>
public sealed class PluginIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginSystem> _logger;
    private bool _disposed;

    public PluginIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        var configData = new Dictionary<string, string?>
        {
            ["PluginSystem:MaxConcurrentLoads"] = "4",
            ["PluginSystem:LoadTimeout"] = "00:00:30",
            ["PluginSystem:EnableHotReload"] = "true",
            ["CpuBackend:MaxThreads"] = Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["CudaBackend:DeviceId"] = "0",
            ["MetalBackend:EnableProfiling"] = "false"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _logger = NullLogger<PluginSystem>.Instance;
    }

    [Fact]
    public async Task PluginSystem_EndToEndScenario_WorksCorrectly()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        await pluginSystem.InitializeAsync();

        using var cpuPlugin = new IntegrationCpuPlugin();
        using var mockBackend = new IntegrationMockBackend();

        // Act - Load multiple plugins
        var cpuResult = await pluginSystem.LoadPluginAsync(cpuPlugin);
        var mockResult = await pluginSystem.LoadPluginAsync(mockBackend);

        // Initialize and start plugins
        await cpuResult!.InitializeAsync(_serviceProvider);
        await cpuResult.StartAsync();

        await mockResult!.InitializeAsync(_serviceProvider);
        await mockResult.StartAsync();

        // Configure plugins
        await cpuResult.OnConfigurationChangedAsync(_configuration);
        await mockResult.OnConfigurationChangedAsync(_configuration);

        // Assert
        pluginSystem.GetLoadedPlugins().Should().HaveCount(2);

        cpuResult.State.Should().Be(PluginState.Running);
        cpuResult.Health.Should().Be(PluginHealth.Healthy);

        mockResult.State.Should().Be(PluginState.Running);
        mockResult.Health.Should().Be(PluginHealth.Healthy);

        var cpuMetrics = cpuResult.GetMetrics();
        Assert.NotNull(cpuMetrics);
        cpuMetrics.RequestCount.Should().BeGreaterThanOrEqualTo(0);

        // Test unloading
        var unloadResult = await pluginSystem.UnloadPluginAsync(cpuResult.Id);
        Assert.True(unloadResult);
        pluginSystem.GetPlugin(cpuResult.Id).Should().BeNull();
    }

    [Fact]
    public async Task PluginSystem_WithPluginOptions_ConfiguresCorrectly()
    {
        // Arrange
        var options = new PluginOptions
        {
            MaxConcurrentLoads = 2,
            LoadTimeout = TimeSpan.FromSeconds(10),
            EnableHotReload = true,
            IsolatePlugins = true
        };

        options.Plugins["test-plugin"] = new PluginConfig
        {
            AssemblyPath = "/fake/path.dll",
            TypeName = "Test.Plugin",
            Enabled = true,
            Settings =
            {
                ["MaxConnections"] = 100,
                ["EnableCaching"] = true
            }
        };

        using var pluginSystem = new PluginSystem(options, _logger);

        // Act
        await pluginSystem.InitializeAsync();

        // Assert
        pluginSystem.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task PluginSystem_DependencyResolution_WorksWithServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITestService, TestService>();

        using var serviceProvider = services.BuildServiceProvider();
        using var pluginSystem = new PluginSystem(_logger);

        using var plugin = new DependentPlugin();

        // Act
        await pluginSystem.LoadPluginAsync(plugin);
        await plugin.InitializeAsync(serviceProvider);

        // Assert
        plugin.TestService.Should().NotBeNull();
        plugin.TestService.Should().BeOfType<TestService>();
    }

    [Fact]
    public async Task PluginSystem_ErrorRecovery_HandlesPluginFailures()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        using var goodPlugin = new IntegrationCpuPlugin();
        using var badPlugin = new FailingPlugin();

        // Act - Load good plugin first
        await pluginSystem.LoadPluginAsync(goodPlugin);
        await goodPlugin.InitializeAsync(_serviceProvider);
        await goodPlugin.StartAsync();

        // Try to load bad plugin
        await FluentActions.Awaiting(() => pluginSystem.LoadPluginAsync(badPlugin))
            .Should().ThrowAsync<PluginLoadException>();

        // Assert - Good plugin should still be working
        pluginSystem.GetLoadedPlugins().Should().HaveCount(1);
        goodPlugin.State.Should().Be(PluginState.Running);
        goodPlugin.Health.Should().Be(PluginHealth.Healthy);
    }

    [Fact]
    public async Task PluginSystem_ConcurrentOperations_ThreadSafe()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        await pluginSystem.InitializeAsync();

        var plugins = Enumerable.Range(0, 10)
            .Select(i => new IntegrationCpuPlugin($"cpu-plugin-{i}"))
            .ToList();

        // Act - Load plugins concurrently
        var loadTasks = plugins.Select(p => pluginSystem.LoadPluginAsync(p));
        var loadResults = await Task.WhenAll(loadTasks);

        // Initialize concurrently
        var initTasks = loadResults.Where(r => r != null).Select(p => p!.InitializeAsync(_serviceProvider));
        await Task.WhenAll(initTasks);

        // Start concurrently
        var startTasks = loadResults.Where(r => r != null).Select(p => p!.StartAsync());
        await Task.WhenAll(startTasks);

        // Assert
        pluginSystem.GetLoadedPlugins().Should().HaveCount(10);
        foreach (var plugin in plugins)
        {
            plugin.State.Should().Be(PluginState.Running);
        }
    }

    [Fact]
    public async Task PluginSystem_MemoryManagement_DisposesCorrectly()
    {
        // Arrange
        var pluginSystem = new PluginSystem(_logger);
        var plugin = new IntegrationCpuPlugin();

        // Act
        await pluginSystem.LoadPluginAsync(plugin);
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();

        // Dispose system
        pluginSystem.Dispose();

        // Assert
        plugin.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task PluginSystem_ValidationErrors_AreHandledProperly()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        var invalidPlugin = new InvalidPlugin();

        // Act & Assert
        await FluentActions.Awaiting(() => pluginSystem.LoadPluginAsync(invalidPlugin))
            .Should().ThrowAsync<PluginLoadException>();
    }

    [Fact]
    public void AotPluginHelpers_CreatesCorrectSystemType()
    {
        // Act
        using var system = AotPluginHelpers.CreatePluginSystem(_logger);

        // Assert
        Assert.NotNull(system);
        Assert.IsAssignableFrom<IDisposable>(system);
    }

    [Fact]
    public async Task PluginSystem_ConfigurationChanges_PropagateToPlugins()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        var plugin = new ConfigurationAwarePlugin();

        await pluginSystem.LoadPluginAsync(plugin);
        await plugin.InitializeAsync(_serviceProvider);

        // Act
        var newConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NewSetting"] = "NewValue"
            })
            .Build();

        await plugin.OnConfigurationChangedAsync(newConfig);

        // Assert
        plugin.LastConfigurationValue.Should().Be("NewValue");
    }

    [Fact]
    public async Task PluginSystem_PluginEvents_AreRaisedCorrectly()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        var plugin = new EventTestPlugin();

        var stateEvents = new List<PluginStateChangedEventArgs>();
        var healthEvents = new List<PluginHealthChangedEventArgs>();
        var errorEvents = new List<PluginErrorEventArgs>();

        plugin.StateChanged += (s, e) => stateEvents.Add(e);
        plugin.HealthChanged += (s, e) => healthEvents.Add(e);
        plugin.ErrorOccurred += (s, e) => errorEvents.Add(e);

        // Act
        await pluginSystem.LoadPluginAsync(plugin);
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();
        plugin.SimulateError();
        plugin.SimulateHealthChange();
        await plugin.StopAsync();

        // Assert
        stateEvents.Should().HaveCountGreaterThan(0);
        healthEvents.Should().HaveCountGreaterThan(0);
        Assert.Single(errorEvents);
    }

    [Fact]
    public async Task PluginSystem_MetricsCollection_WorksAcrossPlugins()
    {
        // Arrange
        using var pluginSystem = new PluginSystem(_logger);
        using var plugin1 = new MetricsTestPlugin("plugin1");
        using var plugin2 = new MetricsTestPlugin("plugin2");

        await pluginSystem.LoadPluginAsync(plugin1);
        await pluginSystem.LoadPluginAsync(plugin2);

        await plugin1.InitializeAsync(_serviceProvider);
        await plugin2.InitializeAsync(_serviceProvider);

        await plugin1.StartAsync();
        await plugin2.StartAsync();

        // Act
        plugin1.SimulateWork();
        plugin2.SimulateWork();

        var metrics1 = plugin1.GetMetrics();
        var metrics2 = plugin2.GetMetrics();

        // Assert
        (metrics1.RequestCount > 0).Should().BeTrue();
        (metrics2.RequestCount > 0).Should().BeTrue();
        metrics1.CustomMetrics.Should().ContainKey("WorkCount");
        metrics2.CustomMetrics.Should().ContainKey("WorkCount");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    #region Test Support Classes

    public interface ITestService
    {
        public string GetData();
    }

    internal sealed class TestService : ITestService
    {
        public string GetData() => "Test data";
    }

    private sealed class IntegrationCpuPlugin : BackendPluginBase
    {
        private readonly string _id;

        public IntegrationCpuPlugin(string? id = null)
        {
            _id = id ?? "integration-cpu-plugin";
        }

        public override string Id => _id;
        public override string Name => "Integration CPU Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "CPU plugin for integration tests";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend | PluginCapabilities.Scalable;

        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }

    private sealed class IntegrationMockBackend : BackendPluginBase
    {
        public override string Id => "integration-mock-backend";
        public override string Name => "Integration Mock Backend";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Mock backend for integration tests";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;
    }

    private sealed class DependentPlugin : BackendPluginBase
    {
        public override string Id => "dependent-plugin";
        public override string Name => "Dependent Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin with dependencies";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public ITestService? TestService { get; private set; }

        public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            await base.InitializeAsync(serviceProvider, cancellationToken);
            TestService = serviceProvider.GetService<ITestService>();
        }
    }

    private sealed class FailingPlugin : BackendPluginBase
    {
        public override string Id => "failing-plugin";
        public override string Name => "Failing Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin that always fails";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public override PluginValidationResult Validate()
        {
            return new PluginValidationResult
            {
                IsValid = false,
                Errors = { "This plugin always fails validation" }
            };
        }
    }

    private sealed class InvalidPlugin : BackendPluginBase
    {
        public override string Id => "";  // Invalid - empty ID
        public override string Name => "";  // Invalid - empty name
        public override Version Version => new(1, 0, 0);
        public override string Description => "Invalid plugin";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.None;
    }

    private sealed class ConfigurationAwarePlugin : BackendPluginBase
    {
        public override string Id => "config-aware-plugin";
        public override string Name => "Configuration Aware Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin that responds to configuration";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public string? LastConfigurationValue { get; private set; }

        protected override Task OnConfigurationUpdatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            LastConfigurationValue = configuration["NewSetting"];
            return Task.CompletedTask;
        }
    }

    private sealed class EventTestPlugin : BackendPluginBase
    {
        public override string Id => "event-test-plugin";
        public override string Name => "Event Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin for testing events";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public void SimulateError() => OnError(new InvalidOperationException("Simulated error"), "Test context");

        public void SimulateHealthChange() => Health = PluginHealth.Degraded;
    }

    private sealed class MetricsTestPlugin : BackendPluginBase
    {
        private readonly string _id;
        private int _workCount;

        public MetricsTestPlugin(string id)
        {
            _id = id;
        }

        public override string Id => _id;
        public override string Name => "Metrics Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin for testing metrics";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public void SimulateWork()
        {
            _workCount++;
            RecordRequest(50.0);
        }

        protected override void OnUpdateMetrics(PluginMetrics metrics) => metrics.CustomMetrics["WorkCount"] = _workCount;
    }

    #endregion
}
}
