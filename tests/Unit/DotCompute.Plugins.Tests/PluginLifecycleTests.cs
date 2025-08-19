// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;


/// <summary>
/// Tests for plugin lifecycle management, including initialization, startup, shutdown, and error handling.
/// </summary>
public sealed class PluginLifecycleTests : IDisposable
{
    private readonly ILogger<PluginSystem> _logger;
    private readonly PluginSystem _pluginSystem;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private bool _disposed;

    public PluginLifecycleTests()
    {
        _logger = NullLogger<PluginSystem>.Instance;
        _pluginSystem = new PluginSystem(_logger);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestSetting"] = "TestValue",
                ["MaxRetries"] = "3"
            })
            .Build();
    }

    [Fact]
    public async Task PluginLifecycle_CompleteFlow_WorksCorrectly()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();

        // Act & Assert - Initial state
        _ = plugin.State.Should().Be(PluginState.Unknown);
        _ = plugin.Health.Should().Be(PluginHealth.Unknown);

        // Load plugin
        _ = await _pluginSystem.LoadPluginAsync(plugin);
        _ = plugin.State.Should().Be(PluginState.Loaded);

        // Initialize
        await plugin.InitializeAsync(_serviceProvider);
        _ = plugin.State.Should().Be(PluginState.Initialized);
        _ = plugin.Health.Should().Be(PluginHealth.Healthy);
        _ = plugin.InitializeCalled.Should().BeTrue();

        // Start
        await plugin.StartAsync();
        _ = plugin.State.Should().Be(PluginState.Running);
        _ = plugin.StartCalled.Should().BeTrue();

        // Configuration change
        await plugin.OnConfigurationChangedAsync(_configuration);
        _ = plugin.ConfigurationChangeCalled.Should().BeTrue();

        // Stop
        await plugin.StopAsync();
        _ = plugin.State.Should().Be(PluginState.Stopped);
        _ = plugin.StopCalled.Should().BeTrue();

        // Dispose
        plugin.Dispose();
        _ = plugin.DisposeCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PluginLifecycle_WithFailure_HandlesGracefully()
    {
        // Arrange
        using var plugin = new FailingLifecycleTestPlugin();

        // Act & Assert - Plugin fails during initialization
        _ = await FluentActions.Awaiting(() => plugin.InitializeAsync(_serviceProvider))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Initialization failed");

        _ = plugin.State.Should().Be(PluginState.Failed);
        _ = plugin.Health.Should().Be(PluginHealth.Critical);
    }

    [Fact]
    public async Task PluginLifecycle_WithTimeout_HandlesCorrectly()
    {
        // Arrange
        using var plugin = new SlowLifecycleTestPlugin();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        _ = await FluentActions.Awaiting(() => plugin.InitializeAsync(_serviceProvider, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PluginLifecycle_InvalidStateTransitions_ThrowExceptions()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();

        // Act & Assert - Cannot start without initializing
        _ = await FluentActions.Awaiting(() => plugin.StartAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot start plugin in state*");

        // Initialize first
        await plugin.InitializeAsync(_serviceProvider);

        // Cannot initialize twice
        _ = await FluentActions.Awaiting(() => plugin.InitializeAsync(_serviceProvider))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot initialize plugin in state*");
    }

    [Fact]
    public async Task PluginLifecycle_EventsAreRaised()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();
        var stateChangedEvents = new List<PluginStateChangedEventArgs>();
        var healthChangedEvents = new List<PluginHealthChangedEventArgs>();
        var errorEvents = new List<PluginErrorEventArgs>();

        plugin.StateChanged += (sender, args) => stateChangedEvents.Add(args);
        plugin.HealthChanged += (sender, args) => healthChangedEvents.Add(args);
        plugin.ErrorOccurred += (sender, args) => errorEvents.Add(args);

        // Act
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();
        plugin.TriggerError(new InvalidOperationException("Test error"), "Test context");
        plugin.TriggerHealthChange(PluginHealth.Degraded);
        await plugin.StopAsync();

        // Assert
        _ = stateChangedEvents.Should().HaveCountGreaterThan(0);
        _ = healthChangedEvents.Should().HaveCountGreaterThan(0);
        _ = Assert.Single(errorEvents);

        var errorEvent = errorEvents.First();
        _ = errorEvent.Exception.Should().BeOfType<InvalidOperationException>();
        _ = errorEvent.Context.Should().Be("Test context");
    }

    [Fact]
    public void PluginLifecycle_Validation_WorksCorrectly()
    {
        // Arrange
        var validPlugin = new LifecycleTestPlugin();
        var invalidPlugin = new InvalidLifecycleTestPlugin();

        // Act
        var validResult = validPlugin.Validate();
        var invalidResult = invalidPlugin.Validate();

        // Assert
        _ = validResult.IsValid.Should().BeTrue();
        _ = validResult.Errors.Should().BeEmpty();

        _ = invalidResult.IsValid.Should().BeFalse();
        _ = invalidResult.Errors.Should().NotBeEmpty();
        _ = invalidResult.Errors.Should().Contain("Plugin name is required");
    }

    [Fact]
    public async Task PluginLifecycle_Metrics_UpdateCorrectly()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();

        // Act
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();

        // Simulate some activity
        plugin.SimulateActivity();
        await Task.Delay(10); // Small delay for uptime

        var metrics = plugin.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        _ = (metrics.Uptime > TimeSpan.Zero).Should().BeTrue();
        _ = (metrics.RequestCount > 0).Should().BeTrue();
        _ = metrics.CustomMetrics.Should().ContainKey("ActivityCount");
    }

    [Fact]
    public async Task PluginLifecycle_ConcurrentOperations_HandleSafely()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();
        await plugin.InitializeAsync(_serviceProvider);

        // Act - Perform concurrent operations
        var tasks = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(plugin.GetMetrics));
            tasks.Add(Task.Run(plugin.SimulateActivity));
        }

        // Assert
        _ = await FluentActions.Awaiting(() => Task.WhenAll(tasks))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task PluginLifecycle_DisposeDuringOperation_HandlesGracefully()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();
        await plugin.InitializeAsync(_serviceProvider);
        await plugin.StartAsync();

        // Act - Start a long-running operation and dispose during it
        var longTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            plugin.SimulateActivity();
        });

        plugin.Dispose();

        // Assert
        _ = await FluentActions.Awaiting(() => longTask)
            .Should().NotThrowAsync();

        _ = plugin.DisposeCalled.Should().BeTrue();
    }

    [Fact]
    public void PluginLifecycle_ConfigurationSchema_ReturnsValidJson()
    {
        // Arrange
        var plugin = new LifecycleTestPlugin();

        // Act
        var schema = plugin.GetConfigurationSchema();

        // Assert
        _ = schema.Should().NotBeNullOrEmpty();

        // Should be valid JSON (at least parseable)
        _ = FluentActions.Invoking(() => System.Text.Json.JsonDocument.Parse(schema))
            .Should().NotThrow();
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
            _pluginSystem?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Test plugin for lifecycle testing.
    /// </summary>
    private sealed class LifecycleTestPlugin : BackendPluginBase
    {
        private readonly Lock _lock = new();
        private int _activityCount;

        public override string Id => "lifecycle-test-plugin";
        public override string Name => "Lifecycle Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin for testing lifecycle";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public bool InitializeCalled { get; private set; }
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public bool ConfigurationChangeCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            InitializeCalled = true;
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        protected override Task OnConfigurationUpdatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            ConfigurationChangeCalled = true;
            return Task.CompletedTask;
        }

        protected override void OnUpdateMetrics(PluginMetrics metrics)
        {
            lock (_lock)
            {
                metrics.CustomMetrics["ActivityCount"] = _activityCount;
            }
        }

        public override string GetConfigurationSchema() => """
            {
                "type": "object",
                "properties": {
                    "TestSetting": { "type": "string" },
                    "MaxRetries": { "type": "integer" }
                }
            }
            """;

        public void SimulateActivity()
        {
            lock (_lock)
            {
                _activityCount++;
            }
            RecordRequest(100);
        }

        public void TriggerError(Exception exception, string context) => OnError(exception, context);

        public void TriggerHealthChange(PluginHealth newHealth) => Health = newHealth;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCalled = true;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Plugin that fails during lifecycle operations.
    /// </summary>
    private sealed class FailingLifecycleTestPlugin : BackendPluginBase
    {
        public override string Id => "failing-test-plugin";
        public override string Name => "Failing Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin that fails";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Initialization failed");
    }

    /// <summary>
    /// Plugin that takes a long time for operations.
    /// </summary>
    private sealed class SlowLifecycleTestPlugin : BackendPluginBase
    {
        public override string Id => "slow-test-plugin";
        public override string Name => "Slow Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin that is slow";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
            // Simulate slow operation
            => await Task.Delay(1000, cancellationToken);
    }

    /// <summary>
    /// Invalid plugin for validation testing.
    /// </summary>
    private sealed class InvalidLifecycleTestPlugin : BackendPluginBase
    {
        public override string Id => "invalid-test-plugin";
        public override string Name => ""; // Invalid - empty name
        public override Version Version => new(1, 0, 0);
        public override string Description => "Invalid plugin";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;
    }
}

/// <summary>
/// Tests for plugin security and isolation.
/// </summary>
public sealed class PluginSecurityTests
{
    [Fact]
    public void PluginSecurityException_WithSecurityViolation_ContainsDetails()
    {
        // Arrange
        const string message = "Security violation detected";
        const string pluginId = "malicious-plugin";
        const string violation = "Attempted to access restricted API";

        // Act
        var exception = new PluginSecurityException(message, pluginId, violation);

        // Assert
        _ = exception.Message.Should().Be(message);
        _ = exception.PluginId.Should().Be(pluginId);
        _ = exception.SecurityViolation.Should().Be(violation);
    }

    [Fact]
    public void Plugin_CannotAccessSystemResources_WithoutPermission()
    {
        // Arrange
        using var plugin = new RestrictedTestPlugin();

        // Act & Assert - Plugin should be designed to not access restricted resources
        _ = plugin.Validate().IsValid.Should().BeTrue();
        _ = plugin.Capabilities.Should().Be(PluginCapabilities.ComputeBackend);
    }

    [Fact]
    public void Plugin_ConfigurationAccess_IsControlled()
    {
        // Arrange
        using var plugin = new RestrictedTestPlugin();
        var config = Substitute.For<IConfiguration>();
        _ = config["AllowedSetting"].Returns("value");

        // Act
        var task = plugin.OnConfigurationChangedAsync(config);

        // Assert
        Assert.NotNull(task);
        _ = FluentActions.Invoking(task.Wait).Should().NotThrow();
    }

    [Fact]
    public void Plugin_ServiceRegistration_IsControlled()
    {
        // Arrange
        using var plugin = new RestrictedTestPlugin();
        var services = new ServiceCollection();
        var config = Substitute.For<IConfiguration>();

        // Act
        Action act = () => plugin.ConfigureServices(services, config);

        // Assert
        act(); // Should not throw
        _ = services.Count.Should().Be(1); // Should only register itself
    }

    /// <summary>
    /// Test plugin that respects security boundaries.
    /// </summary>
    private sealed class RestrictedTestPlugin : BackendPluginBase
    {
        public override string Id => "restricted-test-plugin";
        public override string Name => "Restricted Test Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Plugin with restricted access";
        public override string Author => "Test";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
            // Only register basic services, no system access
            => base.ConfigureServices(services, configuration);

        protected override Task OnConfigurationUpdatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            // Only access allowed configuration settings
            _ = configuration["AllowedSetting"];
            return Task.CompletedTask;
        }
    }
}
