// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.DependencyInjection.Scopes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Runtime.Tests.DependencyInjection;

/// <summary>
/// Tests for IsolatedPluginServiceProvider ensuring proper plugin service isolation.
/// </summary>
public sealed class IsolatedPluginServiceProviderTests : IDisposable
{
    private const string TestPluginId = "test-plugin";
    private readonly IServiceCollection _pluginServices;
    private readonly ServiceProvider _parentServiceProvider;
    private readonly ILogger _mockLogger;
    private IsolatedPluginServiceProvider? _provider;

    public IsolatedPluginServiceProviderTests()
    {
        _pluginServices = new ServiceCollection();
        _parentServiceProvider = new ServiceCollection()
            .AddSingleton<IParentService, ParentService>()
            .AddSingleton<ISharedService, SharedService>()
            .BuildServiceProvider();
        _mockLogger = Substitute.For<ILogger>();
    }

    public void Dispose()
    {
        _provider?.Dispose();
        _parentServiceProvider.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Assert
        _provider.Should().NotBeNull();
        _provider.AllowParentFallback.Should().BeTrue();
        _provider.BlockedParentServices.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidPluginId_ThrowsArgumentException(string? pluginId)
    {
        // Arrange & Act
        var action = () => new IsolatedPluginServiceProvider(
            pluginId!,
            _pluginServices,
            _parentServiceProvider);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullPluginServices_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new IsolatedPluginServiceProvider(
            TestPluginId,
            null!,
            _parentServiceProvider);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pluginServices");
    }

    [Fact]
    public void Constructor_WithNullParentProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var action = () => new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("parentServiceProvider");
    }

    [Fact]
    public void Constructor_WithBlockedServices_SetsBlockedServicesList()
    {
        // Arrange
        var blockedServices = new[] { typeof(IParentService), typeof(ISharedService) };

        // Act
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            blockedParentServices: blockedServices,
            logger: _mockLogger);

        // Assert
        _provider.BlockedParentServices.Should().Contain(typeof(IParentService));
        _provider.BlockedParentServices.Should().Contain(typeof(ISharedService));
    }

    [Fact]
    public void Constructor_WithAllowParentFallbackFalse_DisablesParentFallback()
    {
        // Arrange & Act
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: false,
            logger: _mockLogger);

        // Assert
        _provider.AllowParentFallback.Should().BeFalse();
    }

    #endregion

    #region Service Resolution Tests

    [Fact]
    public void GetService_PluginService_ReturnsPluginInstance()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IPluginService));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<PluginService>();
    }

    [Fact]
    public void GetService_ParentService_WithFallbackEnabled_ReturnsParentInstance()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IParentService));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<ParentService>();
    }

    [Fact]
    public void GetService_ParentService_WithFallbackDisabled_ReturnsNull()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: false,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IParentService));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_BlockedParentService_ReturnsNull()
    {
        // Arrange
        var blockedServices = new[] { typeof(IParentService) };
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            blockedParentServices: blockedServices,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IParentService));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_NonBlockedParentService_ReturnsParentInstance()
    {
        // Arrange - block only IParentService, not ISharedService
        var blockedServices = new[] { typeof(IParentService) };
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            blockedParentServices: blockedServices,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(ISharedService));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<SharedService>();
    }

    [Fact]
    public void GetService_PluginOverridesParent_ReturnsPluginInstance()
    {
        // Arrange - plugin registers same interface as parent
        _pluginServices.AddSingleton<ISharedService, PluginSharedService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(ISharedService));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<PluginSharedService>(); // Plugin takes precedence
    }

    [Fact]
    public void GetService_UnregisteredService_ReturnsNull()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IUnregisteredService));

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void GetService_WithNullServiceType_ThrowsArgumentNullException()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var action = () => _provider.GetService(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceType");
    }

    #endregion

    #region Plugin Singleton Isolation Tests

    [Fact]
    public void GetService_PluginSingleton_ReturnsSameInstanceWithinPlugin()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var service1 = _provider.GetService(typeof(IPluginService));
        var service2 = _provider.GetService(typeof(IPluginService));

        // Assert
        service1.Should().BeSameAs(service2);
    }

    [Fact]
    public void GetService_DifferentPlugins_HaveDifferentSingletonInstances()
    {
        // Arrange
        var pluginServices1 = new ServiceCollection();
        pluginServices1.AddSingleton<IPluginService, PluginService>();

        var pluginServices2 = new ServiceCollection();
        pluginServices2.AddSingleton<IPluginService, PluginService>();

        var provider1 = new IsolatedPluginServiceProvider(
            "plugin-1",
            pluginServices1,
            _parentServiceProvider,
            logger: _mockLogger);

        var provider2 = new IsolatedPluginServiceProvider(
            "plugin-2",
            pluginServices2,
            _parentServiceProvider,
            logger: _mockLogger);

        try
        {
            // Act
            var service1 = provider1.GetService(typeof(IPluginService));
            var service2 = provider2.GetService(typeof(IPluginService));

            // Assert - Each plugin has its own singleton
            service1.Should().NotBeSameAs(service2);
        }
        finally
        {
            provider1.Dispose();
            provider2.Dispose();
        }
    }

    #endregion

    #region GetRequiredService Tests

    [Fact]
    public void GetRequiredService_RegisteredService_ReturnsService()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var service = _provider.GetRequiredService<IPluginService>();

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void GetRequiredService_UnregisteredService_ThrowsInvalidOperationException()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: false,
            logger: _mockLogger);

        // Act
        var action = () => _provider.GetRequiredService<IUnregisteredService>();

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{nameof(IUnregisteredService)}'*");
    }

    #endregion

    #region GetServices Tests

    [Fact]
    public void GetServices_WithPluginServices_ReturnsPluginServices()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _pluginServices.AddSingleton<IPluginService, AnotherPluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: false,
            logger: _mockLogger);

        // Act
        var services = _provider.GetServices<IPluginService>().ToList();

        // Assert
        services.Should().HaveCount(2);
    }

    [Fact]
    public void GetServices_WithParentFallback_CombinesPluginAndParentServices()
    {
        // Arrange
        _pluginServices.AddSingleton<ISharedService, PluginSharedService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            logger: _mockLogger);

        // Act
        var services = _provider.GetServices<ISharedService>().ToList();

        // Assert - Plugin service should be included, parent should not duplicate
        services.Should().Contain(s => s is PluginSharedService);
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _pluginServices.AddTransient<IAnotherService, AnotherService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            allowParentFallback: true,
            blockedParentServices: [typeof(IParentService)],
            logger: _mockLogger);

        // Act
        var stats = _provider.GetStatistics();

        // Assert
        stats.PluginId.Should().Be(TestPluginId);
        stats.RegisteredServiceCount.Should().Be(2);
        stats.BlockedServiceCount.Should().Be(1);
        stats.AllowsParentFallback.Should().BeTrue();
    }

    #endregion

    #region IsPluginRegisteredService Tests

    [Fact]
    public void IsPluginRegisteredService_RegisteredService_ReturnsTrue()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var result = _provider.IsPluginRegisteredService(typeof(IPluginService));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPluginRegisteredService_UnregisteredService_ReturnsFalse()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var result = _provider.IsPluginRegisteredService(typeof(IUnregisteredService));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsBlockedFromParent Tests

    [Fact]
    public void IsBlockedFromParent_BlockedService_ReturnsTrue()
    {
        // Arrange
        var blockedServices = new[] { typeof(IParentService) };
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            blockedParentServices: blockedServices,
            logger: _mockLogger);

        // Act
        var result = _provider.IsBlockedFromParent(typeof(IParentService));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsBlockedFromParent_NonBlockedService_ReturnsFalse()
    {
        // Arrange
        var blockedServices = new[] { typeof(IParentService) };
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            blockedParentServices: blockedServices,
            logger: _mockLogger);

        // Act
        var result = _provider.IsBlockedFromParent(typeof(ISharedService));

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        _provider.Dispose();

        // Assert - no exception thrown
        _provider = null; // Prevent double dispose in Dispose method
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        _provider.Dispose();
        var action = () => _provider.Dispose();

        // Assert
        action.Should().NotThrow();
        _provider = null; // Prevent double dispose in Dispose method
    }

    [Fact]
    public void GetService_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);
        _provider.Dispose();

        // Act
        var action = () => _provider.GetService(typeof(IPluginService));

        // Assert
        action.Should().Throw<ObjectDisposedException>();
        _provider = null; // Prevent double dispose in Dispose method
    }

    [Fact]
    public async Task DisposeAsync_DisposesResources()
    {
        // Arrange
        _pluginServices.AddSingleton<IPluginService, PluginService>();
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        await _provider.DisposeAsync();

        // Assert - should throw on subsequent use
        var action = () => _provider.GetService(typeof(IPluginService));
        action.Should().Throw<ObjectDisposedException>();
        _provider = null; // Prevent double dispose in Dispose method
    }

    #endregion

    #region Generic Type Resolution Tests

    [Fact]
    public void GetService_OpenGenericType_ReturnsInstance()
    {
        // Arrange
        _pluginServices.AddSingleton(typeof(IGenericService<>), typeof(GenericService<>));
        _provider = new IsolatedPluginServiceProvider(
            TestPluginId,
            _pluginServices,
            _parentServiceProvider,
            logger: _mockLogger);

        // Act
        var service = _provider.GetService(typeof(IGenericService<string>));

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<GenericService<string>>();
    }

    #endregion

    #region Test Helpers

    private interface IPluginService { }
    private sealed class PluginService : IPluginService { }
    private sealed class AnotherPluginService : IPluginService { }

    private interface IParentService { }
    private sealed class ParentService : IParentService { }

    private interface ISharedService { }
    private sealed class SharedService : ISharedService { }
    private sealed class PluginSharedService : ISharedService { }

    private interface IAnotherService { }
    private sealed class AnotherService : IAnotherService { }

    private interface IUnregisteredService { }

    private interface IGenericService<T>
    {
        public T? Value { get; }
    }

    private sealed class GenericService<T> : IGenericService<T>
    {
        public T? Value { get; }
    }

    #endregion
}
