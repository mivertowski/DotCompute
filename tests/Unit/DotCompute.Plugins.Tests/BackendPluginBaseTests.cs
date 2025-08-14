// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for the BackendPluginBase class covering plugin lifecycle, state management, and metrics.
/// </summary>
public class BackendPluginBaseTests : IDisposable
{
    private readonly TestBackendPlugin _plugin;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private bool _disposed;

    public BackendPluginBaseTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestSetting"] = "TestValue"
            })
            .Build();

        _plugin = new TestBackendPlugin();
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        // Assert
        _plugin.State.Should().Be(PluginState.Unknown);
        _plugin.Health.Should().Be(PluginHealth.Unknown);
        _plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_SetsStateToInitialized()
    {
        // Act
        await _plugin.InitializeAsync(_serviceProvider);

        // Assert
        _plugin.State.Should().Be(PluginState.Initialized);
        _plugin.Health.Should().Be(PluginHealth.Healthy);
    }

    [Fact]
    public async Task InitializeAsync_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => FluentActions.MethodCall().AsTask())
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public async Task InitializeAsync_FromInvalidState_ThrowsInvalidOperationException()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => FluentActions.MethodCall().AsTask())
            .WithMessage("*Cannot initialize plugin in state*");
    }

    [Fact]
    public async Task InitializeAsync_WhenOnInitializeThrows_SetsStateToFailed()
    {
        // Arrange
        _plugin.ThrowOnInitialize = true;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => FluentActions.MethodCall().AsTask());

        _plugin.State.Should().Be(PluginState.Failed);
        _plugin.Health.Should().Be(PluginHealth.Critical);
    }

    [Fact]
    public async Task StartAsync_SetsStateToRunning()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);

        // Act
        await _plugin.StartAsync();

        // Assert
        _plugin.State.Should().Be(PluginState.Running);
        _plugin.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_FromInvalidState_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => FluentActions.MethodCall().AsTask())
            .WithMessage("*Cannot start plugin in state*");
    }

    [Fact]
    public async Task StartAsync_WhenOnStartThrows_SetsStateToFailed()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);
        _plugin.ThrowOnStart = true;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => FluentActions.MethodCall().AsTask());

        _plugin.State.Should().Be(PluginState.Failed);
        _plugin.Health.Should().Be(PluginHealth.Critical);
    }

    [Fact]
    public async Task StopAsync_SetsStateToStopped()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);
        await _plugin.StartAsync();

        // Act
        await _plugin.StopAsync();

        // Assert
        _plugin.State.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_DoesShould().NotThrow()
    {
        // Act & Assert
        await FluentActions.Invoking(() => _plugin.StopAsync())
            .NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WhenOnStopThrows_SetsStateToFailed()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);
        await _plugin.StartAsync();
        _plugin.ThrowOnStop = true;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => FluentActions.MethodCall().AsTask());

        _plugin.State.Should().Be(PluginState.Failed);
    }

    [Fact]
    public void Validate_WithValidPlugin_ReturnsValid()
    {
        // Act
        var result = _plugin.Validate();

        // Assert
        Assert.NotNull(result);
        result.IsValid.Should().BeTrue();
        result.Assert.Empty(Errors);
    }

    [Fact]
    public void Validate_WithEmptyId_ReturnsInvalid()
    {
        // Arrange
        var plugin = new TestBackendPlugin("");

        // Act
        var result = plugin.Validate();

        // Assert
        result.IsValid.Should().BeFalse();
        result.Assert.Contains("Plugin ID is required", Errors);
    }

    [Fact]
    public async Task OnConfigurationChangedAsync_UpdatesConfiguration()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);

        // Act
        await _plugin.OnConfigurationChangedAsync(_configuration);

        // Assert
        _plugin.LastConfiguration.BeSameAs(_configuration);
    }

    [Fact]
    public async Task OnConfigurationChangedAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => FluentActions.MethodCall().AsTask())
            .WithParameterName("configuration");
    }

    [Fact]
    public void GetConfigurationSchema_ReturnsDefaultSchema()
    {
        // Act
        var schema = _plugin.GetConfigurationSchema();

        // Assert
        Assert.Equal("{}", schema);
    }

    [Fact]
    public void GetMetrics_ReturnsBasicMetrics()
    {
        // Act
        var metrics = _plugin.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        metrics.Timestamp.BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        metrics.Uptime.Should().Be(TimeSpan.Zero); // Not running
    }

    [Fact]
    public async Task GetMetrics_WhenRunning_IncludesUptime()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);
        await _plugin.StartAsync();
        await Task.Delay(10); // Small delay to ensure uptime > 0

        // Act
        var metrics = _plugin.GetMetrics();

        // Assert
        metrics.Uptime > TimeSpan.Zero.Should().BeTrue();
    }

    [Fact]
    public void RecordRequest_UpdatesMetrics()
    {
        // Act
        _plugin.TestRecordRequest(100.0);

        // Assert
        var metrics = _plugin.GetMetrics();
        metrics.RequestCount.Should().Be(1);
        metrics.AverageResponseTime.Should().Be(100.0);
    }

    [Fact]
    public void RecordError_UpdatesMetrics()
    {
        // Act
        _plugin.TestRecordError();

        // Assert
        var metrics = _plugin.GetMetrics();
        metrics.ErrorCount.Should().Be(1);
    }

    [Fact]
    public void UpdateMemoryUsage_UpdatesMetrics()
    {
        // Act
        _plugin.TestUpdateMemoryUsage(1024 * 1024); // 1MB

        // Assert
        var metrics = _plugin.GetMetrics();
        metrics.MemoryUsage.Should().Be(1024 * 1024);
    }

    [Fact]
    public void UpdateCpuUsage_UpdatesMetrics()
    {
        // Act
        _plugin.TestUpdateCpuUsage(75.5);

        // Assert
        var metrics = _plugin.GetMetrics();
        metrics.CpuUsage.Should().Be(75.5);
    }

    [Fact]
    public void UpdateCpuUsage_ClampsToValidRange()
    {
        // Act & Assert
        _plugin.TestUpdateCpuUsage(-10);
        _plugin.GetMetrics().CpuUsage.Should().Be(0);

        _plugin.TestUpdateCpuUsage(150);
        _plugin.GetMetrics().CpuUsage.Should().Be(100);
    }

    [Fact]
    public void SetCustomMetric_UpdatesMetrics()
    {
        // Act
        _plugin.TestSetCustomMetric("CustomValue", 42);

        // Assert
        var metrics = _plugin.GetMetrics();
        metrics.CustomMetrics.Should().ContainKey("CustomValue");
        metrics.CustomMetrics["CustomValue"].Should().Be(42);
    }

    [Fact]
    public async Task LoadAsync_InitializesAndStarts()
    {
        // Act
        await _plugin.LoadAsync(_serviceProvider);

        // Assert
        _plugin.State.Should().Be(PluginState.Running);
    }

    [Fact]
    public async Task UnloadAsync_StopsPlugin()
    {
        // Arrange
        await _plugin.LoadAsync(_serviceProvider);

        // Act
        await _plugin.UnloadAsync();

        // Assert
        _plugin.State.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public void StateChanged_RaisesEvent()
    {
        // Arrange
        var eventRaised = false;
        PluginStateChangedEventArgs? eventArgs = null;
        _plugin.StateChanged += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act
        _plugin.TestChangeState(PluginState.Initializing);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(eventArgs);
        eventArgs!.NewState.Should().Be(PluginState.Initializing);
        eventArgs.OldState.Should().Be(PluginState.Unknown);
    }

    [Fact]
    public void ErrorOccurred_RaisesEvent()
    {
        // Arrange
        var eventRaised = false;
        PluginErrorEventArgs? eventArgs = null;
        _plugin.ErrorOccurred += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        var exception = new InvalidOperationException("Test error");

        // Act
        _plugin.TestRaiseError(exception, "Test context");

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(eventArgs);
        eventArgs!.Exception.BeSameAs(exception);
        eventArgs.Context.Should().Be("Test context");
    }

    [Fact]
    public void HealthChanged_RaisesEvent()
    {
        // Arrange
        var eventRaised = false;
        PluginHealthChangedEventArgs? eventArgs = null;
        _plugin.HealthChanged += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act
        _plugin.TestChangeHealth(PluginHealth.Degraded);

        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(eventArgs);
        eventArgs!.NewHealth.Should().Be(PluginHealth.Degraded);
        eventArgs.OldHealth.Should().Be(PluginHealth.Unknown);
    }

    [Fact]
    public async Task Dispose_StopsRunningPlugin()
    {
        // Arrange
        await _plugin.InitializeAsync(_serviceProvider);
        await _plugin.StartAsync();

        // Act
        _plugin.Dispose();

        // Assert
        _plugin.State.Should().Be(PluginState.Unloaded);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesShould().NotThrow()
    {
        // Act & Assert
        Action act = () =>
        {
            _plugin.Dispose();
            _plugin.Dispose(); // Should not throw
        };
        act(); // Should not throw
    }

    [Fact]
    public async Task OperationsAfterDispose_ThrowObjectDisposedException()
    {
        // Arrange
        _plugin.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());

        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());

        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());

        await Assert.ThrowsAsync<ObjectDisposedException>(() => FluentActions.MethodCall().AsTask());
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _plugin?.Dispose();
            (_serviceProvider as IDisposable)?.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Test implementation of BackendPluginBase for testing purposes.
    /// </summary>
    private sealed class TestBackendPlugin : BackendPluginBase
    {
        private readonly string _id;

        public TestBackendPlugin(string? id = null)
        {
            _id = id ?? "test-backend-plugin";
        }

        public override string Id => _id;
        public override string Name => "Test Backend Plugin";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Test backend plugin for unit tests";
        public override string Author => "Test Author";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        public bool ThrowOnInitialize { get; set; }
        public bool ThrowOnStart { get; set; }
        public bool ThrowOnStop { get; set; }
        public IConfiguration? LastConfiguration { get; private set; }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("Initialization failed");
            }
            return Task.CompletedTask;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("Start failed");
            }
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnStop)
            {
                throw new InvalidOperationException("Stop failed");
            }
            return Task.CompletedTask;
        }

        protected override Task OnConfigurationUpdatedAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            LastConfiguration = configuration;
            return Task.CompletedTask;
        }

        // Test methods to expose protected methods
        public void TestRecordRequest(double responseTime = 0) => RecordRequest(responseTime);
        public void TestRecordError() => RecordError();
        public void TestUpdateMemoryUsage(long bytes) => UpdateMemoryUsage(bytes);
        public void TestUpdateCpuUsage(double percentage) => UpdateCpuUsage(percentage);
        public void TestSetCustomMetric(string name, object value) => SetCustomMetric(name, value);
        public void TestChangeState(PluginState newState) => State = newState;
        public void TestChangeHealth(PluginHealth newHealth) => Health = newHealth;
        public void TestRaiseError(Exception exception, string context) => OnError(exception, context);
    }
}
