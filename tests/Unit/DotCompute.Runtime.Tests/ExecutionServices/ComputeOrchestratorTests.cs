// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: ComputeOrchestrator class does not exist in Runtime module yet
// TODO: Uncomment when ComputeOrchestrator is implemented in DotCompute.Runtime.Services
/*
using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.ExecutionServices;

/// <summary>
/// Tests for ComputeOrchestrator
/// </summary>
public sealed class ComputeOrchestratorTests : IDisposable
{
    private readonly ILogger<ComputeOrchestrator> _mockLogger;
    private readonly IAccelerator _mockAccelerator;
    private ComputeOrchestrator? _orchestrator;

    public ComputeOrchestratorTests()
    {
        _mockLogger = Substitute.For<ILogger<ComputeOrchestrator>>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Name.Returns("TestAccelerator");
    }

    public void Dispose()
    {
        _orchestrator?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ComputeOrchestrator(null!, _mockAccelerator);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ComputeOrchestrator(_mockLogger, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("accelerator");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_Succeeds()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();

        // Act
        await _orchestrator.ExecuteAsync(kernel);

        // Assert
        await _mockAccelerator.Received(1).ExecuteAsync(kernel);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullKernel_ThrowsArgumentNullException()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);

        // Act
        var action = async () => await _orchestrator.ExecuteAsync(null!);

        // Assert
        await action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteBatchAsync_WithMultipleKernels_ExecutesAll()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);
        var kernels = new[]
        {
            Substitute.For<IKernel>(),
            Substitute.For<IKernel>()
        };

        // Act
        await _orchestrator.ExecuteBatchAsync(kernels);

        // Assert
        await _mockAccelerator.Received(2).ExecuteAsync(Arg.Any<IKernel>());
    }

    [Fact]
    public async Task ExecuteWithFallbackAsync_WithPrimaryFailure_UsesFallback()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var fallbackAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.ExecuteAsync(kernel).Throws(new InvalidOperationException());

        // Act
        await _orchestrator.ExecuteWithFallbackAsync(kernel, fallbackAccelerator);

        // Assert
        await fallbackAccelerator.Received(1).ExecuteAsync(kernel);
    }

    [Fact]
    public async Task GetExecutionMetricsAsync_ReturnsMetrics()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        await _orchestrator.ExecuteAsync(kernel);

        // Act
        var metrics = await _orchestrator.GetExecutionMetricsAsync();

        // Assert
        metrics.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        _orchestrator = new ComputeOrchestrator(_mockLogger, _mockAccelerator);

        // Act
        _orchestrator.Dispose();

        // Assert - should not throw
        _orchestrator.Dispose();
    }
}
*/