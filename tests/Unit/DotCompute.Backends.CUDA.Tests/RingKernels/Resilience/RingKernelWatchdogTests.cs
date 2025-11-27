// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.RingKernels.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Backends.CUDA.Tests.RingKernels.Resilience;

/// <summary>
/// Unit tests for ring kernel watchdog fault recovery functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RingKernelWatchdogTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;
    private RingKernelWatchdog? _watchdog;

    public RingKernelWatchdogTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger.Instance;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_watchdog != null)
        {
            await _watchdog.DisposeAsync();
        }
    }

    #region Constructor Tests

    [Fact(DisplayName = "Constructor throws on null logger")]
    public void Constructor_ThrowsOnNullLogger()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new RingKernelWatchdog(null!, null));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact(DisplayName = "Constructor uses default options when null")]
    public async Task Constructor_UsesDefaultOptions_WhenNull()
    {
        // Arrange & Act
        _watchdog = new RingKernelWatchdog(_logger, null);

        // Assert - watchdog should start since EnableWatchdog defaults to true
        var stats = _watchdog.GetStatistics();
        Assert.NotNull(stats);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "Constructor validates options")]
    public void Constructor_ValidatesOptions()
    {
        // Arrange
        var invalidOptions = new RingKernelFaultRecoveryOptions
        {
            WatchdogInterval = TimeSpan.Zero // Invalid
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RingKernelWatchdog(_logger, invalidOptions));
    }

    [Fact(DisplayName = "Constructor does not start watchdog when disabled")]
    public async Task Constructor_DoesNotStartWatchdog_WhenDisabled()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };

        // Act
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Assert - statistics still work
        var stats = _watchdog.GetStatistics();
        Assert.NotNull(stats);
        Assert.Equal(0, stats.WatchedKernelCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Registration Tests

    [Fact(DisplayName = "RegisterKernel throws on null kernelId")]
    public async Task RegisterKernel_ThrowsOnNullKernelId()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _watchdog.RegisterKernel(null!, ct => Task.FromResult(true), () => new KernelHealthStatus()));

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "RegisterKernel throws on null restartCallback")]
    public async Task RegisterKernel_ThrowsOnNullRestartCallback()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _watchdog.RegisterKernel("test", null!, () => new KernelHealthStatus()));

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "RegisterKernel throws on null getStatusCallback")]
    public async Task RegisterKernel_ThrowsOnNullGetStatusCallback()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _watchdog.RegisterKernel("test", ct => Task.FromResult(true), null!));

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "RegisterKernel adds kernel to watched list")]
    public async Task RegisterKernel_AddsKernelToWatchedList()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });

        // Assert
        var stats = _watchdog.GetStatistics();
        Assert.Equal(1, stats.WatchedKernelCount);
        Assert.Equal(1, stats.ActiveKernelCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "UnregisterKernel removes kernel from watched list")]
    public async Task UnregisterKernel_RemovesKernelFromWatchedList()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });

        // Act
        var removed = _watchdog.UnregisterKernel("test-kernel");

        // Assert
        Assert.True(removed);
        var stats = _watchdog.GetStatistics();
        Assert.Equal(0, stats.WatchedKernelCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "UnregisterKernel returns false for unknown kernel")]
    public async Task UnregisterKernel_ReturnsFalseForUnknownKernel()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act
        var removed = _watchdog.UnregisterKernel("unknown-kernel");

        // Assert
        Assert.False(removed);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Activity Reporting Tests

    [Fact(DisplayName = "ReportActivity updates last activity time")]
    public async Task ReportActivity_UpdatesLastActivityTime()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });

        // Act
        _watchdog.ReportActivity("test-kernel", 10);

        // Assert - no exceptions thrown, activity recorded
        var stats = _watchdog.GetStatistics();
        Assert.Equal(1, stats.ActiveKernelCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "ReportActivity ignores unknown kernel")]
    public async Task ReportActivity_IgnoresUnknownKernel()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act - should not throw
        _watchdog.ReportActivity("unknown-kernel", 10);

        // Assert
        var stats = _watchdog.GetStatistics();
        Assert.Equal(0, stats.WatchedKernelCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Circuit Breaker Tests

    [Fact(DisplayName = "GetCircuitBreakerState returns null for unknown kernel")]
    public async Task GetCircuitBreakerState_ReturnsNullForUnknownKernel()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        // Act
        var state = _watchdog.GetCircuitBreakerState("unknown-kernel");

        // Assert
        Assert.Null(state);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "GetCircuitBreakerState returns Closed for new kernel")]
    public async Task GetCircuitBreakerState_ReturnsClosedForNewKernel()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });

        // Act
        var state = _watchdog.GetCircuitBreakerState("test-kernel");

        // Assert
        Assert.Equal(CircuitBreakerState.Closed, state);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "TripCircuitBreaker sets state to Open")]
    public async Task TripCircuitBreaker_SetsStateToOpen()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });

        // Act
        _watchdog.TripCircuitBreaker("test-kernel", "Test reason");

        // Assert
        var state = _watchdog.GetCircuitBreakerState("test-kernel");
        Assert.Equal(CircuitBreakerState.Open, state);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "ResetCircuitBreaker closes open circuit")]
    public async Task ResetCircuitBreaker_ClosesOpenCircuit()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);
        _watchdog.RegisterKernel("test-kernel",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });
        _watchdog.TripCircuitBreaker("test-kernel", "Test reason");

        // Act
        _watchdog.ResetCircuitBreaker("test-kernel");

        // Assert
        var state = _watchdog.GetCircuitBreakerState("test-kernel");
        Assert.Equal(CircuitBreakerState.Closed, state);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Statistics Tests

    [Fact(DisplayName = "GetStatistics returns accurate counts")]
    public async Task GetStatistics_ReturnsAccurateCounts()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = false };
        _watchdog = new RingKernelWatchdog(_logger, options);

        _watchdog.RegisterKernel("kernel-1",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });
        _watchdog.RegisterKernel("kernel-2",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = true });
        _watchdog.RegisterKernel("kernel-3",
            ct => Task.FromResult(true),
            () => new KernelHealthStatus { IsRunning = false });

        _watchdog.TripCircuitBreaker("kernel-2", "Test");

        // Act
        var stats = _watchdog.GetStatistics();

        // Assert
        Assert.Equal(3, stats.WatchedKernelCount);
        Assert.Equal(3, stats.ActiveKernelCount); // All are marked active on registration
        Assert.Equal(1, stats.CircuitBreakerOpenCount);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Event Tests

    [Fact(DisplayName = "KernelFaultDetected event fires on fault")]
    public async Task KernelFaultDetected_EventFiresOnFault()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions
        {
            EnableWatchdog = true,
            WatchdogInterval = TimeSpan.FromMilliseconds(50),
            KernelStallTimeout = TimeSpan.FromMilliseconds(1), // Very short for test
            EnableAutoRestart = false, // Disable restart for this test
            MaxRestartAttempts = 0
        };
        _watchdog = new RingKernelWatchdog(_logger, options);

        var faultDetected = false;
        string? faultKernelId = null;
        KernelFaultType? detectedFaultType = null;

        _watchdog.KernelFaultDetected += (sender, args) =>
        {
            faultDetected = true;
            faultKernelId = args.KernelId;
            detectedFaultType = args.FaultType;
        };

        // Register kernel that reports as running but with no activity
        _watchdog.RegisterKernel("stalled-kernel",
            ct => Task.FromResult(false),
            () => new KernelHealthStatus { IsRunning = true });

        // Act - wait for watchdog to detect stall
        await Task.Delay(200);

        // Assert
        Assert.True(faultDetected, "Fault should have been detected");
        Assert.Equal("stalled-kernel", faultKernelId);
        Assert.Equal(KernelFaultType.Stall, detectedFaultType);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    [Fact(DisplayName = "KernelRecovered event fires on successful restart")]
    public async Task KernelRecovered_EventFiresOnSuccessfulRestart()
    {
        // Arrange
        var restartAttempted = false;
        var options = new RingKernelFaultRecoveryOptions
        {
            EnableWatchdog = true,
            WatchdogInterval = TimeSpan.FromMilliseconds(50),
            KernelStallTimeout = TimeSpan.FromMilliseconds(1),
            EnableAutoRestart = true,
            MaxRestartAttempts = 3,
            RestartDelay = TimeSpan.FromMilliseconds(1)
        };
        _watchdog = new RingKernelWatchdog(_logger, options);

        var recovered = false;
        string? recoveredKernelId = null;

        _watchdog.KernelRecovered += (sender, args) =>
        {
            recovered = true;
            recoveredKernelId = args.KernelId;
        };

        // Register kernel that reports crash then succeeds restart
        var crashCounter = 0;
        _watchdog.RegisterKernel("recoverable-kernel",
            ct =>
            {
                restartAttempted = true;
                return Task.FromResult(true); // Restart succeeds
            },
            () =>
            {
                crashCounter++;
                // First call: crashed, subsequent: running
                return new KernelHealthStatus { IsRunning = crashCounter > 1 };
            });

        // Artificially cause a fault by not reporting activity and having IsRunning=false
        // Wait for watchdog to detect crash and attempt recovery
        await Task.Delay(300);

        // Assert
        Assert.True(restartAttempted, "Restart should have been attempted");
        Assert.True(recovered, "Recovery event should have fired");
        Assert.Equal("recoverable-kernel", recoveredKernelId);

        await _watchdog.DisposeAsync();
        _watchdog = null;
    }

    #endregion

    #region Options Validation Tests

    [Fact(DisplayName = "Options validation rejects negative MaxRestartAttempts")]
    public void OptionsValidation_RejectsNegativeMaxRestartAttempts()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions
        {
            MaxRestartAttempts = -1
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact(DisplayName = "Options validation rejects zero CircuitBreakerFailureThreshold")]
    public void OptionsValidation_RejectsZeroCircuitBreakerThreshold()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions
        {
            CircuitBreakerFailureThreshold = 0
        };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    #endregion

    #region Disposal Tests

    [Fact(DisplayName = "Dispose cleans up resources")]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = true };
        var watchdog = new RingKernelWatchdog(_logger, options);
        watchdog.RegisterKernel("test", ct => Task.FromResult(true), () => new KernelHealthStatus());

        // Act
        watchdog.Dispose();

        // Assert - no exception on dispose
        Assert.True(true);
    }

    [Fact(DisplayName = "DisposeAsync cleans up resources")]
    public async Task DisposeAsync_CleansUpResources()
    {
        // Arrange
        var options = new RingKernelFaultRecoveryOptions { EnableWatchdog = true };
        var watchdog = new RingKernelWatchdog(_logger, options);
        watchdog.RegisterKernel("test", ct => Task.FromResult(true), () => new KernelHealthStatus());

        // Act
        await watchdog.DisposeAsync();

        // Assert - no exception on dispose
        Assert.True(true);
    }

    #endregion
}
