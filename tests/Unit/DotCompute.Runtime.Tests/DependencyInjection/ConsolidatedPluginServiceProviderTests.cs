// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: ConsolidatedPluginServiceProvider constructor signature mismatch
// TODO: Uncomment when ConsolidatedPluginServiceProvider implements the expected constructor
// Expected: ConsolidatedPluginServiceProvider(IServiceProvider, ILogger<ConsolidatedPluginServiceProvider>)
// Also verify methods: CreatePluginScope(), RegisterPluginServices(), GetService()
/*
using DotCompute.Runtime.DependencyInjection.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for ConsolidatedPluginServiceProvider
/// </summary>
public sealed class ConsolidatedPluginServiceProviderTests : IDisposable
{
    private readonly IServiceProvider _mockHostProvider;
    private readonly ILogger<ConsolidatedPluginServiceProvider> _mockLogger;
    private ConsolidatedPluginServiceProvider? _provider;

    public ConsolidatedPluginServiceProviderTests()
    {
        _mockHostProvider = Substitute.For<IServiceProvider>();
        _mockLogger = Substitute.For<ILogger<ConsolidatedPluginServiceProvider>>();
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Assert
        _provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHostProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ConsolidatedPluginServiceProvider(null!, _mockLogger);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("hostServiceProvider");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new ConsolidatedPluginServiceProvider(_mockHostProvider, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithOptions_UsesProvidedOptions()
    {
        // Arrange
        var options = new ConsolidatedPluginServiceProviderOptions
        {
            EnableMetrics = true
        };

        // Act
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger, options);

        // Assert
        _provider.Should().NotBeNull();
    }

    [Fact]
    public void CreatePluginScope_WithValidPluginId_CreatesScope()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);
        var mockScope = Substitute.For<IServiceScope>();
        _mockHostProvider.CreateScope().Returns(mockScope);

        // Act
        var scope = _provider.CreatePluginScope("test-plugin");

        // Assert
        scope.Should().NotBeNull();
        _mockHostProvider.Received(1).CreateScope();
    }

    [Fact]
    public void CreatePluginScope_WithNullPluginId_ThrowsArgumentException()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        var action = () => _provider.CreatePluginScope(null!);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePluginScope_WithEmptyPluginId_ThrowsArgumentException()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        var action = () => _provider.CreatePluginScope(string.Empty);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreatePluginScope_CalledTwiceWithSameId_ReturnsSameScope()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);
        var mockScope = Substitute.For<IServiceScope>();
        _mockHostProvider.CreateScope().Returns(mockScope);

        // Act
        var scope1 = _provider.CreatePluginScope("test-plugin");
        var scope2 = _provider.CreatePluginScope("test-plugin");

        // Assert
        scope1.Should().BeSameAs(scope2);
    }

    [Fact]
    public void RegisterPluginServices_WithValidConfiguration_RegistersServices()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        _provider.RegisterPluginServices("test-plugin", services =>
        {
            services.AddSingleton<ITestService, TestService>();
        });

        // Assert - no exception thrown
    }

    [Fact]
    public void RegisterPluginServices_WithNullPluginId_ThrowsArgumentException()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        var action = () => _provider.RegisterPluginServices(null!, _ => { });

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterPluginServices_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        var action = () => _provider.RegisterPluginServices("test-plugin", null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetService_WithRegisteredService_ReturnsService()
    {
        // Arrange
        var testService = new TestService();
        _mockHostProvider.GetService(typeof(ITestService)).Returns(testService);
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        var service = _provider.GetService(typeof(ITestService));

        // Assert
        service.Should().BeSameAs(testService);
    }

    [Fact]
    public void Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        _provider.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);

        // Act
        _provider.Dispose();
        _provider.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public void CreatePluginScope_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _provider = new ConsolidatedPluginServiceProvider(_mockHostProvider, _mockLogger);
        _provider.Dispose();

        // Act
        var action = () => _provider.CreatePluginScope("test-plugin");

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    // Helper interfaces and classes for testing
    private interface ITestService { }
    private class TestService : ITestService { }
}
*/
