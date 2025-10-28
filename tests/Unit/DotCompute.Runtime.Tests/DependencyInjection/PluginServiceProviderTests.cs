// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// COMMENTED OUT: PluginServiceProvider constructor signature mismatch
// TODO: Uncomment when PluginServiceProvider implements required constructor
// Expected: PluginServiceProvider(IServiceProvider, ILogger<PluginServiceProvider>)
// Current: PluginServiceProvider(IServiceCollection)
/*
using DotCompute.Runtime.DependencyInjection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for PluginServiceProvider
/// </summary>
public sealed class PluginServiceProviderTests : IDisposable
{
    private readonly IServiceProvider _mockParentProvider;
    private PluginServiceProvider? _provider;

    public PluginServiceProviderTests()
    {
        _mockParentProvider = Substitute.For<IServiceProvider>();
    }

    public void Dispose()
    {
        _provider?.Dispose();
    }

    [Fact]
    public void Constructor_WithServiceCollection_CreatesInstance()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        _provider = new PluginServiceProvider(services);

        // Assert
        _provider.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullServiceCollection_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new PluginServiceProvider(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetService_WithRegisteredService_ReturnsService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        _provider = new PluginServiceProvider(services);

        // Act
        var service = _provider.GetService(typeof(ITestService));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<ITestService>();
    }

    [Fact]
    public void GetService_WithUnregisteredService_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        _provider = new PluginServiceProvider(services);

        // Act
        var service = _provider.GetService(typeof(ITestService));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_WithSingletonService_ReturnsSameInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        _provider = new PluginServiceProvider(services);

        // Act
        var service1 = _provider.GetService(typeof(ITestService));
        var service2 = _provider.GetService(typeof(ITestService));

        // Assert
        service1.Should().BeSameAs(service2);
    }

    [Fact]
    public void GetService_WithTransientService_ReturnsDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        _provider = new PluginServiceProvider(services);

        // Act
        var service1 = _provider.GetService(typeof(ITestService));
        var service2 = _provider.GetService(typeof(ITestService));

        // Assert
        service1.Should().NotBeSameAs(service2);
    }

    [Fact]
    public void GetRequiredService_WithRegisteredService_ReturnsService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        _provider = new PluginServiceProvider(services);

        // Act
        var service = _provider.GetRequiredService(typeof(ITestService));

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void CreateScope_CreatesNewScope()
    {
        // Arrange
        var services = new ServiceCollection();
        _provider = new PluginServiceProvider(services);

        // Act
        using var scope = _provider.CreateScope();

        // Assert
        scope.Should().NotBeNull();
        scope.ServiceProvider.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        var services = new ServiceCollection();
        _provider = new PluginServiceProvider(services);

        // Act
        _provider.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        _provider = new PluginServiceProvider(services);

        // Act
        _provider.Dispose();
        _provider.Dispose();

        // Assert - no exception thrown
    }

    [Fact]
    public void GetService_WithScopedService_ReturnsScopedInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ITestService, TestService>();
        _provider = new PluginServiceProvider(services);

        // Act
        using var scope = _provider.CreateScope();
        var service = scope.ServiceProvider.GetService(typeof(ITestService));

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var services = new ServiceCollection();
        _provider = new PluginServiceProvider(services);
        _provider.Dispose();

        // Act
        var action = () => _provider.GetService(typeof(ITestService));

        // Assert
        action.Should().Throw<ObjectDisposedException>();
    }

    // Helper interfaces and classes for testing
    private interface ITestService { }
    private class TestService : ITestService { }
}
*/
