// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using DotCompute.Abstractions.FaultTolerance;
using DotCompute.Core.FaultTolerance;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotCompute.Core.Tests.FaultTolerance;

/// <summary>
/// Unit tests for the ChaosEngine class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChaosEngineTests : IAsyncDisposable
{
    private readonly ILogger<ChaosEngine> _logger;
    private readonly ChaosEngine _engine;

    public ChaosEngineTests()
    {
        _logger = Substitute.For<ILogger<ChaosEngine>>();
        _engine = new ChaosEngine(_logger, seed: 42); // Fixed seed for reproducibility
    }

    public async ValueTask DisposeAsync()
    {
        await _engine.DisposeAsync();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ChaosEngine(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesEngine()
    {
        // Assert
        Assert.NotNull(_engine);
        Assert.False(_engine.IsActive);
        Assert.Empty(_engine.ActiveFaults);
    }

    [Fact]
    public async Task Constructor_WithSeed_CreatesReproducibleEngine()
    {
        // Arrange
        await using var engine1 = new ChaosEngine(_logger, seed: 123);
        await using var engine2 = new ChaosEngine(_logger, seed: 123);

        // Assert - both engines should exist with same seed
        Assert.NotNull(engine1);
        Assert.NotNull(engine2);
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WithValidFaults_ActivatesEngine()
    {
        // Arrange
        var faults = new[]
        {
            FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)),
            FaultConfiguration.CreateTimeout()
        };

        // Act
        await _engine.StartAsync(faults);

        // Assert
        Assert.True(_engine.IsActive);
        Assert.Equal(2, _engine.ActiveFaults.Count);
    }

    [Fact]
    public async Task StartAsync_WithDisabledFaults_FiltersThemOut()
    {
        // Arrange
        var faults = new[]
        {
            FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)),
            new FaultConfiguration { FaultType = FaultType.Exception, Enabled = false }
        };

        // Act
        await _engine.StartAsync(faults);

        // Assert
        Assert.Single(_engine.ActiveFaults);
        Assert.Equal(FaultType.Latency, _engine.ActiveFaults[0].FaultType);
    }

    [Fact]
    public async Task StartAsync_WithNullFaults_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.StartAsync(null!));
    }

    [Fact]
    public async Task StartAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        await _engine.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            _engine.StartAsync(Array.Empty<FaultConfiguration>()));
    }

    [Fact]
    public async Task StartAsync_ResetsStatistics()
    {
        // Arrange - start with some activity
        var faults = new[] { FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 1.0) };
        await _engine.StartAsync(faults);
        await _engine.EvaluateFaultAsync("test-component");

        // Act - restart
        await _engine.StartAsync(faults);
        var stats = _engine.GetStatistics();

        // Assert
        Assert.Equal(0, stats.TotalOpportunities);
        Assert.Equal(0, stats.FaultsInjected);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_DeactivatesEngine()
    {
        // Arrange
        await _engine.StartAsync(new[] { FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)) });

        // Act
        await _engine.StopAsync();

        // Assert
        Assert.False(_engine.IsActive);
    }

    [Fact]
    public async Task StopAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        await _engine.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _engine.StopAsync());
    }

    #endregion

    #region AddFault Tests

    [Fact]
    public async Task AddFault_WhileRunning_AddsFault()
    {
        // Arrange
        await _engine.StartAsync(Array.Empty<FaultConfiguration>());
        var fault = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(50));

        // Act
        _engine.AddFault(fault);

        // Assert
        Assert.Single(_engine.ActiveFaults);
    }

    [Fact]
    public void AddFault_WithDisabledFault_DoesNotAdd()
    {
        // Arrange
        var fault = new FaultConfiguration { FaultType = FaultType.Exception, Enabled = false };

        // Act
        _engine.AddFault(fault);

        // Assert
        Assert.Empty(_engine.ActiveFaults);
    }

    [Fact]
    public void AddFault_WithNullFault_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _engine.AddFault(null!));
    }

    #endregion

    #region RemoveFault Tests

    [Fact]
    public async Task RemoveFault_ByType_RemovesMatchingFaults()
    {
        // Arrange
        var faults = new[]
        {
            FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)),
            FaultConfiguration.CreateTimeout()
        };
        await _engine.StartAsync(faults);

        // Act
        _engine.RemoveFault(FaultType.Latency);

        // Assert
        Assert.Single(_engine.ActiveFaults);
        Assert.Equal(FaultType.Timeout, _engine.ActiveFaults[0].FaultType);
    }

    [Fact]
    public async Task RemoveFault_ByTypeAndPattern_RemovesOnlyMatching()
    {
        // Arrange
        var faults = new[]
        {
            new FaultConfiguration { FaultType = FaultType.Latency, TargetPattern = "component-a", Latency = TimeSpan.FromMilliseconds(100) },
            new FaultConfiguration { FaultType = FaultType.Latency, TargetPattern = "component-b", Latency = TimeSpan.FromMilliseconds(100) }
        };
        await _engine.StartAsync(faults);

        // Act
        _engine.RemoveFault(FaultType.Latency, "component-a");

        // Assert
        Assert.Single(_engine.ActiveFaults);
        Assert.Equal("component-b", _engine.ActiveFaults[0].TargetPattern);
    }

    #endregion

    #region ClearFaults Tests

    [Fact]
    public async Task ClearFaults_RemovesAllFaults()
    {
        // Arrange
        var faults = new[]
        {
            FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)),
            FaultConfiguration.CreateTimeout(),
            FaultConfiguration.CreateException<InvalidOperationException>()
        };
        await _engine.StartAsync(faults);

        // Act
        _engine.ClearFaults();

        // Assert
        Assert.Empty(_engine.ActiveFaults);
    }

    #endregion

    #region EvaluateFaultAsync Tests

    [Fact]
    public async Task EvaluateFaultAsync_WhenNotActive_ReturnsNull()
    {
        // Arrange - engine not started

        // Act
        var fault = await _engine.EvaluateFaultAsync("test-component");

        // Assert
        Assert.Null(fault);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithMatchingFault_ReturnsFault()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100), probability: 1.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var fault = await _engine.EvaluateFaultAsync("test-component");

        // Assert
        Assert.NotNull(fault);
        Assert.Equal(FaultType.Latency, fault.FaultType);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithWildcardPattern_MatchesAllComponents()
    {
        // Arrange
        var faultConfig = new FaultConfiguration
        {
            FaultType = FaultType.Latency,
            TargetPattern = "*",
            Probability = 1.0,
            Latency = TimeSpan.FromMilliseconds(100)
        };
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var fault1 = await _engine.EvaluateFaultAsync("component-a");
        var fault2 = await _engine.EvaluateFaultAsync("component-b");

        // Assert
        Assert.NotNull(fault1);
        Assert.NotNull(fault2);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithSpecificPattern_MatchesOnlyTarget()
    {
        // Arrange
        var faultConfig = new FaultConfiguration
        {
            FaultType = FaultType.Latency,
            TargetPattern = "component-a",
            Probability = 1.0,
            Latency = TimeSpan.FromMilliseconds(100)
        };
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var fault1 = await _engine.EvaluateFaultAsync("component-a");
        var fault2 = await _engine.EvaluateFaultAsync("component-b");

        // Assert
        Assert.NotNull(fault1);
        Assert.Null(fault2);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithWildcardInPattern_MatchesPartial()
    {
        // Arrange
        var faultConfig = new FaultConfiguration
        {
            FaultType = FaultType.Latency,
            TargetPattern = "gpu-*",
            Probability = 1.0,
            Latency = TimeSpan.FromMilliseconds(100)
        };
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var fault1 = await _engine.EvaluateFaultAsync("gpu-0");
        var fault2 = await _engine.EvaluateFaultAsync("gpu-1");
        var fault3 = await _engine.EvaluateFaultAsync("cpu-0");

        // Assert
        Assert.NotNull(fault1);
        Assert.NotNull(fault2);
        Assert.Null(fault3);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithOperationPattern_MatchesComponentAndOperation()
    {
        // Arrange
        var faultConfig = new FaultConfiguration
        {
            FaultType = FaultType.Latency,
            TargetPattern = "component/execute",
            Probability = 1.0,
            Latency = TimeSpan.FromMilliseconds(100)
        };
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var fault1 = await _engine.EvaluateFaultAsync("component", "execute");
        var fault2 = await _engine.EvaluateFaultAsync("component", "other");

        // Assert
        Assert.NotNull(fault1);
        Assert.Null(fault2);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithZeroProbability_NeverReturns()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100), probability: 0.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act - try many times
        FaultConfiguration? fault = null;
        for (var i = 0; i < 100; i++)
        {
            fault = await _engine.EvaluateFaultAsync("test-component");
            if (fault != null) break;
        }

        // Assert
        Assert.Null(fault);
    }

    [Fact]
    public async Task EvaluateFaultAsync_IncrementsOpportunityCount()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100), probability: 0.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        await _engine.EvaluateFaultAsync("test-component");
        await _engine.EvaluateFaultAsync("test-component");
        await _engine.EvaluateFaultAsync("test-component");

        // Assert
        var stats = _engine.GetStatistics();
        Assert.Equal(3, stats.TotalOpportunities);
    }

    [Fact]
    public async Task EvaluateFaultAsync_WithNullComponentId_ThrowsArgumentNullException()
    {
        // Arrange
        await _engine.StartAsync(Array.Empty<FaultConfiguration>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.EvaluateFaultAsync(null!));
    }

    #endregion

    #region InjectFaultAsync Tests

    [Fact]
    public async Task InjectFaultAsync_WithLatency_DelaysExecution()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(50));
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");
        sw.Stop();

        // Assert - should have delayed at least 50ms (with some tolerance)
        Assert.True(sw.ElapsedMilliseconds >= 40, $"Expected delay >= 40ms, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InjectFaultAsync_WithRandomLatency_DelaysWithinRange()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateRandomLatency(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(100));
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");
        sw.Stop();

        // Assert - should be within range (with tolerance)
        Assert.True(sw.ElapsedMilliseconds >= 5, $"Expected delay >= 5ms, got {sw.ElapsedMilliseconds}ms");
        Assert.True(sw.ElapsedMilliseconds <= 150, $"Expected delay <= 150ms, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InjectFaultAsync_WithException_ThrowsConfiguredException()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateException<InvalidOperationException>("Test chaos exception");
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.InjectFaultAsync(faultConfig, "test-component", "execute"));
        Assert.Equal("Test chaos exception", ex.Message);
    }

    [Fact]
    public async Task InjectFaultAsync_WithExceptionFactory_UsesFactory()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateException(
            () => new ArgumentException("Factory created exception"));
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _engine.InjectFaultAsync(faultConfig, "test-component", "execute"));
        Assert.Equal("Factory created exception", ex.Message);
    }

    [Fact]
    public async Task InjectFaultAsync_WithTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateTimeout();
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            _engine.InjectFaultAsync(faultConfig, "test-component", "execute"));
    }

    [Fact]
    public async Task InjectFaultAsync_WithResourceExhaustion_ThrowsInsufficientMemoryException()
    {
        // Arrange
        var faultConfig = new FaultConfiguration { FaultType = FaultType.ResourceExhaustion };
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientMemoryException>(() =>
            _engine.InjectFaultAsync(faultConfig, "test-component", "execute"));
    }

    [Fact]
    public async Task InjectFaultAsync_WithGpuError_ThrowsInvalidOperationException()
    {
        // Arrange
        var faultConfig = new FaultConfiguration { FaultType = FaultType.GpuError };
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.InjectFaultAsync(faultConfig, "test-component", "execute"));
    }

    [Fact]
    public async Task InjectFaultAsync_RecordsInjection()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        // Act
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");

        // Assert
        var history = _engine.GetHistory();
        Assert.Single(history);
        Assert.Equal("test-component", history[0].TargetComponent);
        Assert.Equal("execute", history[0].Operation);
        Assert.True(history[0].WasApplied);
    }

    [Fact]
    public async Task InjectFaultAsync_RaisesFaultInjectingEvent()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        FaultInjectionEventArgs? eventArgs = null;
        _engine.FaultInjecting += (_, args) => eventArgs = args;

        // Act
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("test-component", eventArgs.ComponentId);
        Assert.Equal("execute", eventArgs.Operation);
    }

    [Fact]
    public async Task InjectFaultAsync_RaisesFaultInjectedEvent()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        FaultInjectionEventArgs? eventArgs = null;
        _engine.FaultInjected += (_, args) => eventArgs = args;

        // Act
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal("test-component", eventArgs.ComponentId);
    }

    [Fact]
    public async Task InjectFaultAsync_WhenEventCancelled_DoesNotInject()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100));
        await _engine.StartAsync(new[] { faultConfig });
        _engine.FaultInjecting += (_, args) => args.Cancel = true;

        // Act
        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        await _engine.InjectFaultAsync(faultConfig, "test-component", "execute");
        sw.Stop();

        // Assert - should not have delayed
        Assert.True(sw.ElapsedMilliseconds < 50, $"Expected no delay, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InjectFaultAsync_WithNullFault_ThrowsArgumentNullException()
    {
        // Arrange
        await _engine.StartAsync(Array.Empty<FaultConfiguration>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.InjectFaultAsync(null!, "test-component"));
    }

    [Fact]
    public async Task InjectFaultAsync_WithNullComponentId_ThrowsArgumentNullException()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.InjectFaultAsync(faultConfig, null!));
    }

    #endregion

    #region GetHistory Tests

    [Fact]
    public async Task GetHistory_ReturnsRecentInjections()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        await _engine.InjectFaultAsync(faultConfig, "component-1", "op1");
        await _engine.InjectFaultAsync(faultConfig, "component-2", "op2");
        await _engine.InjectFaultAsync(faultConfig, "component-3", "op3");

        // Act
        var history = _engine.GetHistory();

        // Assert
        Assert.Equal(3, history.Count);
        // Should be in reverse order (most recent first)
        Assert.Equal("component-3", history[0].TargetComponent);
        Assert.Equal("component-2", history[1].TargetComponent);
        Assert.Equal("component-1", history[2].TargetComponent);
    }

    [Fact]
    public async Task GetHistory_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1));
        await _engine.StartAsync(new[] { faultConfig });

        for (var i = 0; i < 10; i++)
        {
            await _engine.InjectFaultAsync(faultConfig, $"component-{i}", "op");
        }

        // Act
        var history = _engine.GetHistory(limit: 5);

        // Assert
        Assert.Equal(5, history.Count);
    }

    #endregion

    #region GetStatistics Tests

    [Fact]
    public async Task GetStatistics_ReturnsAccurateStats()
    {
        // Arrange
        var latencyFault1 = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 1.0);
        var latencyFault2 = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 1.0, targetPattern: "component-b");
        await _engine.StartAsync(new[] { latencyFault1, latencyFault2 });

        // Act
        await _engine.InjectFaultAsync(latencyFault1, "component-a", "op1");
        await _engine.InjectFaultAsync(latencyFault1, "component-a", "op2");
        await _engine.InjectFaultAsync(latencyFault2, "component-b", "op3");

        var stats = _engine.GetStatistics();

        // Assert
        Assert.Equal(3, stats.FaultsInjected);
        Assert.Equal(3, stats.ByFaultType[FaultType.Latency]);
        Assert.Equal(2, stats.ByComponent["component-a"]);
        Assert.Equal(1, stats.ByComponent["component-b"]);
    }

    [Fact]
    public async Task GetStatistics_TracksSkippedFaults()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 0.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act - evaluate multiple times (all should be skipped due to 0 probability)
        for (var i = 0; i < 5; i++)
        {
            await _engine.EvaluateFaultAsync("test-component");
        }

        var stats = _engine.GetStatistics();

        // Assert
        Assert.Equal(5, stats.TotalOpportunities);
        Assert.Equal(5, stats.FaultsSkipped);
        Assert.Equal(0, stats.FaultsInjected);
    }

    #endregion

    #region ExecuteWithChaosAsync Extension Tests

    [Fact]
    public async Task ExecuteWithChaosAsync_WhenNotActive_ExecutesNormally()
    {
        // Arrange - engine not started
        var executed = false;

        // Act
        var result = await _engine.ExecuteWithChaosAsync(
            "test-component",
            "execute",
            async () =>
            {
                executed = true;
                await Task.Yield();
                return 42;
            });

        // Assert
        Assert.True(executed);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteWithChaosAsync_WithActiveFault_InjectsAndExecutes()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(10), probability: 1.0);
        await _engine.StartAsync(new[] { faultConfig });
        var executed = false;

        // Act
        var sw = global::System.Diagnostics.Stopwatch.StartNew();
        var result = await _engine.ExecuteWithChaosAsync(
            "test-component",
            "execute",
            async () =>
            {
                executed = true;
                await Task.Yield();
                return 42;
            });
        sw.Stop();

        // Assert
        Assert.True(executed);
        Assert.Equal(42, result);
        Assert.True(sw.ElapsedMilliseconds >= 5, $"Expected delay, got {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task ExecuteWithChaosAsync_VoidVersion_Works()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(10), probability: 1.0);
        await _engine.StartAsync(new[] { faultConfig });
        var executed = false;

        // Act
        await _engine.ExecuteWithChaosAsync(
            "test-component",
            "execute",
            async () =>
            {
                executed = true;
                await Task.Yield();
            });

        // Assert
        Assert.True(executed);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task DisposeAsync_ClearsState()
    {
        // Arrange
        await _engine.StartAsync(new[] { FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(100)) });

        // Act
        await _engine.DisposeAsync();

        // Assert
        Assert.False(_engine.IsActive);
        Assert.Empty(_engine.ActiveFaults);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Act & Assert - should not throw
        await _engine.DisposeAsync();
        await _engine.DisposeAsync();
        await _engine.DisposeAsync();
    }

    #endregion

    #region FaultConfiguration Factory Tests

    [Fact]
    public void FaultConfiguration_Latency_CreatesCorrectConfig()
    {
        // Act
        var config = FaultConfiguration.CreateLatency(
            TimeSpan.FromMilliseconds(100),
            probability: 0.5,
            targetPattern: "gpu-*");

        // Assert
        Assert.Equal(FaultType.Latency, config.FaultType);
        Assert.Equal(TimeSpan.FromMilliseconds(100), config.Latency);
        Assert.Equal(0.5, config.Probability);
        Assert.Equal("gpu-*", config.TargetPattern);
        Assert.True(config.Enabled);
    }

    [Fact]
    public void FaultConfiguration_RandomLatency_CreatesCorrectConfig()
    {
        // Act
        var config = FaultConfiguration.CreateRandomLatency(
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal(FaultType.Latency, config.FaultType);
        Assert.Equal(TimeSpan.FromMilliseconds(10), config.MinLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(100), config.MaxLatency);
    }

    [Fact]
    public void FaultConfiguration_Exception_CreatesCorrectConfig()
    {
        // Act
        var config = FaultConfiguration.CreateException<ArgumentException>(
            message: "Test message",
            probability: 0.25);

        // Assert
        Assert.Equal(FaultType.Exception, config.FaultType);
        Assert.Equal(typeof(ArgumentException), config.ExceptionType);
        Assert.Equal("Test message", config.ExceptionMessage);
        Assert.Equal(0.25, config.Probability);
    }

    [Fact]
    public void FaultConfiguration_ExceptionWithFactory_CreatesCorrectConfig()
    {
        // Arrange
        Func<Exception> factory = () => new InvalidOperationException("From factory");

        // Act
        var config = FaultConfiguration.CreateException(factory, probability: 0.75);

        // Assert
        Assert.Equal(FaultType.Exception, config.FaultType);
        Assert.Equal(factory, config.ExceptionFactory);
        Assert.Equal(0.75, config.Probability);
    }

    [Fact]
    public void FaultConfiguration_Timeout_CreatesCorrectConfig()
    {
        // Act
        var config = FaultConfiguration.CreateTimeout(
            duration: TimeSpan.FromSeconds(30),
            probability: 0.1);

        // Assert
        Assert.Equal(FaultType.Timeout, config.FaultType);
        Assert.Equal(TimeSpan.FromSeconds(30), config.TimeoutDuration);
        Assert.Equal(0.1, config.Probability);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentEvaluations_AreThreadSafe()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 1.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act - run many concurrent evaluations
        var tasks = Enumerable.Range(0, 100)
            .Select(i => _engine.EvaluateFaultAsync($"component-{i}"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - all should have returned a fault
        Assert.Equal(100, results.Count(r => r != null));

        var stats = _engine.GetStatistics();
        Assert.Equal(100, stats.TotalOpportunities);
    }

    [Fact]
    public async Task ConcurrentInjections_AreThreadSafe()
    {
        // Arrange
        var faultConfig = FaultConfiguration.CreateLatency(TimeSpan.FromMilliseconds(1), probability: 1.0);
        await _engine.StartAsync(new[] { faultConfig });

        // Act - run many concurrent injections
        var tasks = Enumerable.Range(0, 50)
            .Select(i => _engine.InjectFaultAsync(faultConfig, $"component-{i}", "op"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert
        var stats = _engine.GetStatistics();
        Assert.Equal(50, stats.FaultsInjected);

        var history = _engine.GetHistory(limit: 100);
        Assert.Equal(50, history.Count);
    }

    #endregion
}
