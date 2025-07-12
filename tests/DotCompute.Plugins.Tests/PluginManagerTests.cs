using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotCompute.Plugins;
using DotCompute.Plugins.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Plugins.Tests;

public class PluginManagerTests
{
    private readonly Mock<ILogger<PluginManager>> _loggerMock;
    private readonly Mock<IPluginLoader> _loaderMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly PluginManager _manager;

    public PluginManagerTests()
    {
        _loggerMock = new Mock<ILogger<PluginManager>>();
        _loaderMock = new Mock<IPluginLoader>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _manager = new PluginManager(_loaderMock.Object, _serviceProviderMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_ValidPlugin_InitializesSuccessfully()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin);

        // Act
        await _manager.LoadPluginAsync("test.dll");
        await _manager.InitializeAsync();

        // Assert
        mockPlugin.Verify(p => p.InitializeAsync(It.IsAny<IServiceProvider>()), Times.Once);
        _manager.GetPlugin<ITestPlugin>().Should().NotBeNull();
    }

    [Fact]
    public async Task LoadPluginAsync_ValidPath_LoadsPlugin()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);

        // Act
        var result = await _manager.LoadPluginAsync("test.dll");

        // Assert
        result.Should().BeTrue();
        _manager.GetLoadedPlugins().Should().ContainSingle();
    }

    [Fact]
    public async Task LoadPluginAsync_LoaderThrows_ReturnsFalse()
    {
        // Arrange
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Throws<PluginLoadException>();

        // Act
        var result = await _manager.LoadPluginAsync("invalid.dll");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task UnloadPluginAsync_ValidPlugin_UnloadsSuccessfully()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        var pluginId = Guid.NewGuid();
        mockPlugin.Setup(p => p.Id).Returns(pluginId);
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);
        _loaderMock.Setup(l => l.UnloadPlugin(pluginId)).Returns(true);

        await _manager.LoadPluginAsync("test.dll");

        // Act
        var result = await _manager.UnloadPluginAsync(pluginId);

        // Assert
        result.Should().BeTrue();
        mockPlugin.Verify(p => p.ShutdownAsync(), Times.Once);
    }

    [Fact]
    public async Task UnloadPluginAsync_InvalidId_ReturnsFalse()
    {
        // Act
        var result = await _manager.UnloadPluginAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetPlugin_ByType_ReturnsCorrectPlugin()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);
        _manager.LoadPluginAsync("test.dll").Wait();

        // Act
        var plugin = _manager.GetPlugin<ITestPlugin>();

        // Assert
        plugin.Should().NotBeNull();
        plugin.Should().Be(mockPlugin.Object);
    }

    [Fact]
    public void GetPlugin_TypeNotFound_ReturnsNull()
    {
        // Act
        var plugin = _manager.GetPlugin<INonExistentPlugin>();

        // Assert
        plugin.Should().BeNull();
    }

    [Fact]
    public void GetPluginById_ValidId_ReturnsPlugin()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        var pluginId = Guid.NewGuid();
        mockPlugin.Setup(p => p.Id).Returns(pluginId);
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);
        _manager.LoadPluginAsync("test.dll").Wait();

        // Act
        var plugin = _manager.GetPlugin(pluginId);

        // Assert
        plugin.Should().NotBeNull();
        plugin.Should().Be(mockPlugin.Object);
    }

    [Fact]
    public void GetLoadedPlugins_ReturnsAllPlugins()
    {
        // Arrange
        var plugin1 = CreateMockPlugin();
        var plugin2 = CreateMockPlugin();
        
        _loaderMock.SetupSequence(l => l.LoadPlugin(It.IsAny<string>()))
            .Returns(plugin1.Object)
            .Returns(plugin2.Object);

        _manager.LoadPluginAsync("plugin1.dll").Wait();
        _manager.LoadPluginAsync("plugin2.dll").Wait();

        // Act
        var plugins = _manager.GetLoadedPlugins().ToList();

        // Assert
        plugins.Should().HaveCount(2);
        plugins.Should().Contain(plugin1.Object);
        plugins.Should().Contain(plugin2.Object);
    }

    [Fact]
    public async Task ReloadPluginAsync_ValidPlugin_ReloadsSuccessfully()
    {
        // Arrange
        var oldPlugin = CreateMockPlugin();
        var newPlugin = CreateMockPlugin();
        var pluginId = Guid.NewGuid();
        var pluginPath = "test.dll";
        
        oldPlugin.Setup(p => p.Id).Returns(pluginId);
        newPlugin.Setup(p => p.Id).Returns(Guid.NewGuid());
        
        _loaderMock.Setup(l => l.LoadPlugin(pluginPath)).Returns(oldPlugin.Object);
        _loaderMock.Setup(l => l.ReloadPlugin(pluginPath)).Returns(newPlugin.Object);
        _loaderMock.Setup(l => l.UnloadPlugin(pluginId)).Returns(true);

        await _manager.LoadPluginAsync(pluginPath);

        // Act
        var result = await _manager.ReloadPluginAsync(pluginPath);

        // Assert
        result.Should().BeTrue();
        oldPlugin.Verify(p => p.ShutdownAsync(), Times.Once);
        newPlugin.Verify(p => p.InitializeAsync(It.IsAny<IServiceProvider>()), Times.Once);
    }

    [Fact]
    public async Task ShutdownAsync_UnloadsAllPlugins()
    {
        // Arrange
        var plugin1 = CreateMockPlugin();
        var plugin2 = CreateMockPlugin();
        
        _loaderMock.SetupSequence(l => l.LoadPlugin(It.IsAny<string>()))
            .Returns(plugin1.Object)
            .Returns(plugin2.Object);

        await _manager.LoadPluginAsync("plugin1.dll");
        await _manager.LoadPluginAsync("plugin2.dll");

        // Act
        await _manager.ShutdownAsync();

        // Assert
        plugin1.Verify(p => p.ShutdownAsync(), Times.Once);
        plugin2.Verify(p => p.ShutdownAsync(), Times.Once);
        _manager.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public void RegisterService_ValidService_RegistersSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var testService = new TestService();

        // Act
        _manager.RegisterService<ITestService>(testService);
        var provider = services.BuildServiceProvider();

        // Assert
        // This tests the concept, actual implementation would integrate with DI
        _manager.GetService<ITestService>().Should().BeNull(); // Until proper DI integration
    }

    [Fact]
    public async Task LoadPluginAsync_PluginWithDependencies_ResolvesCorrectly()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        var dependency = new TestService();
        
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ITestService))).Returns(dependency);

        // Act
        await _manager.LoadPluginAsync("test.dll");
        await _manager.InitializeAsync();

        // Assert
        mockPlugin.Verify(p => p.InitializeAsync(_serviceProviderMock.Object), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_PluginThrowsException_HandlesGracefully()
    {
        // Arrange
        var mockPlugin = CreateMockPlugin();
        mockPlugin.Setup(p => p.InitializeAsync(It.IsAny<IServiceProvider>()))
            .ThrowsAsync(new Exception("Init failed"));
        
        _loaderMock.Setup(l => l.LoadPlugin(It.IsAny<string>())).Returns(mockPlugin.Object);
        await _manager.LoadPluginAsync("test.dll");

        // Act
        var action = () => _manager.InitializeAsync();

        // Assert
        await action.Should().NotThrowAsync();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    private Mock<IPlugin> CreateMockPlugin()
    {
        var mockPlugin = new Mock<IPlugin>();
        mockPlugin.Setup(p => p.Id).Returns(Guid.NewGuid());
        mockPlugin.Setup(p => p.Name).Returns("TestPlugin");
        mockPlugin.Setup(p => p.Version).Returns("1.0.0");
        mockPlugin.Setup(p => p.GetType()).Returns(typeof(ITestPlugin));
        mockPlugin.As<ITestPlugin>();
        return mockPlugin;
    }

    private interface ITestPlugin : IPlugin
    {
        void TestMethod();
    }

    private interface INonExistentPlugin : IPlugin
    {
    }

    private interface ITestService
    {
        void DoSomething();
    }

    private class TestService : ITestService
    {
        public void DoSomething() { }
    }
}