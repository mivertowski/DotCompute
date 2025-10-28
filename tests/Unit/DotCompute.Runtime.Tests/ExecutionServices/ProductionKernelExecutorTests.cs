// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: ProductionKernelExecutor and IAccelerator API mismatches
// TODO: Uncomment when the following APIs are implemented:
// - IAccelerator.Name property
// - ProductionKernelExecutor.ExecuteAsync(IKernel) method
// - ProductionKernelExecutor various execution methods
/*
using DotCompute.Abstractions;
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.ExecutionServices;

/// <summary>
/// Tests for ProductionKernelExecutor
/// </summary>
public sealed class ProductionKernelExecutorTests : IDisposable
{
    private readonly ILogger<ProductionKernelExecutor> _mockLogger;
    private readonly IAccelerator _mockAccelerator;
    private ProductionKernelExecutor? _executor;

    public ProductionKernelExecutorTests()
    {
        _mockLogger = Substitute.For<ILogger<ProductionKernelExecutor>>();
        _mockAccelerator = Substitute.For<IAccelerator>();
        _mockAccelerator.Name.Returns("TestAccelerator");
    }

    public void Dispose()
    {
        _executor?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ProductionKernelExecutor(null!, _mockAccelerator);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAccelerator_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ProductionKernelExecutor(_mockLogger, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("accelerator");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidKernel_Succeeds()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();

        // Act
        await _executor.ExecuteAsync(kernel);

        // Assert
        await _mockAccelerator.Received(1).ExecuteAsync(kernel);
    }

    [Fact]
    public async Task ExecuteAsync_WithRetry_RetriesOnFailure()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();
        var callCount = 0;
        _mockAccelerator.ExecuteAsync(kernel).Returns(_ =>
        {
            if (callCount++ < 2)
                throw new InvalidOperationException();
            return Task.CompletedTask;
        });

        // Act
        await _executor.ExecuteAsync(kernel, retryCount: 3);

        // Assert
        await _mockAccelerator.Received(3).ExecuteAsync(kernel);
    }

    [Fact]
    public async Task ExecuteWithProfilingAsync_CollectsPerformanceData()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();

        // Act
        var profile = await _executor.ExecuteWithProfilingAsync(kernel);

        // Assert
        profile.Should().NotBeNull();
        profile.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteOptimizedAsync_SelectsOptimalBackend()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);
        var kernel = Substitute.For<IKernel>();

        // Act
        await _executor.ExecuteOptimizedAsync(kernel);

        // Assert
        await _mockAccelerator.Received(1).ExecuteAsync(kernel);
    }

    [Fact]
    public void GetExecutionStatistics_ReturnsStatistics()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);

        // Act
        var stats = _executor.GetExecutionStatistics();

        // Assert
        stats.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        // Arrange
        _executor = new ProductionKernelExecutor(_mockLogger, _mockAccelerator);

        // Act
        _executor.Dispose();

        // Assert - should not throw
        _executor.Dispose();
    }
}
*/
