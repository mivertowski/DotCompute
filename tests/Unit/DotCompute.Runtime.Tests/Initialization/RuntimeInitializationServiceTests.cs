// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: RuntimeInitializationService API mismatch - missing methods and properties
// TODO: Uncomment when RuntimeInitializationService implements:
// - Dispose() method
// - InitializeAsync() method
// - IsInitialized property
// - Constructor signature: (AcceleratorRuntime, IOptions<DotComputeRuntimeOptions>, ILogger)
/*
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.Initialization;

/// <summary>
/// Tests for RuntimeInitializationService
/// </summary>
public sealed class RuntimeInitializationServiceTests : IDisposable
{
    private readonly ILogger<RuntimeInitializationService> _mockLogger;
    private RuntimeInitializationService? _service;

    public RuntimeInitializationServiceTests()
    {
        _mockLogger = Substitute.For<ILogger<RuntimeInitializationService>>();
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new RuntimeInitializationService(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task InitializeAsync_WithValidConfiguration_Succeeds()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);

        // Act
        await _service.InitializeAsync();

        // Assert
        _service.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_CalledMultipleTimes_OnlyInitializesOnce()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);

        // Act
        await _service.InitializeAsync();
        await _service.InitializeAsync();

        // Assert
        _service.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var action = async () => await _service.InitializeAsync(cts.Token);

        // Assert
        await action.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public async Task ShutdownAsync_AfterInitialization_Succeeds()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        await _service.InitializeAsync();

        // Act
        await _service.ShutdownAsync();

        // Assert
        _service.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task ShutdownAsync_WithoutInitialization_DoesNotThrow()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);

        // Act
        await _service.ShutdownAsync();

        // Assert - no exception thrown
    }

    [Fact]
    public async Task GetInitializationStatus_BeforeInitialization_ReturnsNotInitialized()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);

        // Act
        var status = _service.GetInitializationStatus();

        // Assert
        status.Should().Be(InitializationStatus.NotInitialized);
    }

    [Fact]
    public async Task GetInitializationStatus_AfterInitialization_ReturnsInitialized()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        await _service.InitializeAsync();

        // Act
        var status = _service.GetInitializationStatus();

        // Assert
        status.Should().Be(InitializationStatus.Initialized);
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_AppliesOptions()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        var options = new RuntimeInitializationOptions
        {
            EnableMetrics = true,
            EnableLogging = true
        };

        // Act
        await _service.InitializeAsync(options);

        // Assert
        _service.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterInitializationCallback_CallsCallback()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        bool callbackCalled = false;
        _service.RegisterInitializationCallback(() => callbackCalled = true);

        // Act
        await _service.InitializeAsync();

        // Assert
        callbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterShutdownCallback_CallsCallback()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        bool callbackCalled = false;
        _service.RegisterShutdownCallback(() => callbackCalled = true);
        await _service.InitializeAsync();

        // Act
        await _service.ShutdownAsync();

        // Assert
        callbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetInitializationTime_AfterInitialization_ReturnsPositiveValue()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        await _service.InitializeAsync();

        // Act
        var time = _service.GetInitializationTime();

        // Assert
        time.Should().BePositive();
    }

    [Fact]
    public async Task IsHealthy_AfterSuccessfulInitialization_ReturnsTrue()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        await _service.InitializeAsync();

        // Act
        var healthy = _service.IsHealthy();

        // Assert
        healthy.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_WithInitializedService_CleansUpResources()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        await _service.InitializeAsync();

        // Act
        _service.Dispose();

        // Assert
        _service.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithTimeout_CompletesWithinTimeout()
    {
        // Arrange
        _service = new RuntimeInitializationService(_mockLogger);
        var timeout = TimeSpan.FromSeconds(5);

        // Act
        var task = _service.InitializeAsync();
        var completed = await Task.WhenAny(task, Task.Delay(timeout)) == task;

        // Assert
        completed.Should().BeTrue();
    }

    // Helper enums and classes for testing
    private enum InitializationStatus
    {
        NotInitialized,
        Initializing,
        Initialized,
        Failed
    }

    private class RuntimeInitializationOptions
    {
        public bool EnableMetrics { get; set; }
        public bool EnableLogging { get; set; }
    }
}
*/
