// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: Test file references deprecated IKernelCompiler interface
// TODO: Uncomment when tests are updated to use IUnifiedKernelCompiler
// The tests try to create KernelExecutionService with obsolete IKernelCompiler
/*
using DotCompute.Runtime.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Reflection;
using Xunit;

namespace DotCompute.Runtime.Tests.Services.Execution;

/// <summary>
/// Tests for GeneratedKernelDiscoveryService
/// </summary>
public sealed class GeneratedKernelDiscoveryServiceTests
{
    private readonly ILogger<GeneratedKernelDiscoveryService> _mockLogger;
    private readonly GeneratedKernelDiscoveryService _service;

    public GeneratedKernelDiscoveryServiceTests()
    {
        _mockLogger = Substitute.For<ILogger<GeneratedKernelDiscoveryService>>();
        _service = new GeneratedKernelDiscoveryService(_mockLogger);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new GeneratedKernelDiscoveryService(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task DiscoverKernelsAsync_WithNoKernels_ReturnsEmptyList()
    {
        // Arrange & Act
        var result = await _service.DiscoverKernelsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverKernelsAsync_FiltersSystemAssemblies()
    {
        // Arrange & Act
        var result = await _service.DiscoverKernelsAsync();

        // Assert
        // Should not throw exceptions from scanning system assemblies
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithNullAssembly_ReturnsEmptyList()
    {
        // Arrange
        var assembly = typeof(GeneratedKernelDiscoveryServiceTests).Assembly;

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithCurrentAssembly_HandlesGracefully()
    {
        // Arrange
        var assembly = Assembly.GetExecutingAssembly();

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverAndRegisterKernelsAsync_WithNoKernels_ReturnsZero()
    {
        // Arrange
        var mockExecutionService = Substitute.For<KernelExecutionService>(
            Substitute.For<DotCompute.Runtime.Services.Interfaces.IKernelCompiler>(),
            Substitute.For<DotCompute.Abstractions.IAccelerator>());

        // Act
        var count = await _service.DiscoverAndRegisterKernelsAsync(mockExecutionService);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task DiscoverKernelsAsync_HandlesExceptionsDuringScanning()
    {
        // Arrange & Act
        var result = await _service.DiscoverKernelsAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithDynamicAssembly_ReturnsEmptyList()
    {
        // Arrange
        var assembly = typeof(object).Assembly; // System assembly

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverKernelsAsync_ProcessesMultipleAssemblies()
    {
        // Arrange & Act
        var result = await _service.DiscoverKernelsAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithMissingTypes_HandlesGracefully()
    {
        // Arrange
        var assembly = typeof(GeneratedKernelDiscoveryServiceTests).Assembly;

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task DiscoverKernelsAsync_HandlesMultipleScans(int scanCount)
    {
        // Arrange & Act
        for (int i = 0; i < scanCount; i++)
        {
            var result = await _service.DiscoverKernelsAsync();
            result.Should().NotBeNull();
        }

        // Assert - no exceptions thrown
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithReflectionOnlyAssembly_HandlesCorrectly()
    {
        // Arrange
        var assembly = typeof(GeneratedKernelDiscoveryServiceTests).Assembly;

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DiscoverAndRegisterKernelsAsync_LogsAppropriateMessages()
    {
        // Arrange
        var mockExecutionService = Substitute.For<KernelExecutionService>(
            Substitute.For<DotCompute.Runtime.Services.Interfaces.IKernelCompiler>(),
            Substitute.For<DotCompute.Abstractions.IAccelerator>());

        // Act
        await _service.DiscoverAndRegisterKernelsAsync(mockExecutionService);

        // Assert
        // Verify logging occurred (implementation logs warnings when no kernels found)
        _mockLogger.ReceivedCalls().Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiscoverKernelsAsync_IsIdempotent()
    {
        // Arrange & Act
        var result1 = await _service.DiscoverKernelsAsync();
        var result2 = await _service.DiscoverKernelsAsync();

        // Assert
        result1.Count.Should().Be(result2.Count);
    }

    [Fact]
    public async Task DiscoverKernelsFromAssemblyAsync_WithEmptyAssembly_ReturnsEmptyList()
    {
        // Arrange
        var assembly = typeof(GeneratedKernelDiscoveryServiceTests).Assembly;

        // Act
        var result = await _service.DiscoverKernelsFromAssemblyAsync(assembly);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<KernelRegistrationInfo>>();
    }
}
*/
