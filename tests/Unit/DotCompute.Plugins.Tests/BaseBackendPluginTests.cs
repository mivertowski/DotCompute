// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

using DotCompute.Abstractions.Kernels;
namespace DotCompute.Plugins.Tests;


/// <summary>
/// Tests for the BaseBackendPlugin abstract class.
/// </summary>
public sealed class BaseBackendPluginTests
{
    /// <summary>
    /// Test accelerator implementation for testing purposes.
    /// </summary>
    private sealed class TestAccelerator : IAccelerator
    {
        public static string Name => "TestAccelerator";
        public AcceleratorType Type => AcceleratorType.CPU;
        public AcceleratorInfo Info => Mock.Of<AcceleratorInfo>();
        public IMemoryManager Memory => Mock.Of<IMemoryManager>();
        public AcceleratorContext Context => default;

        public ValueTask<ICompiledKernel> CompileKernelAsync(KernelDefinition definition, CompilationOptions? options = default, CancellationToken cancellationToken = default) => ValueTask.FromResult(Mock.Of<ICompiledKernel>());

        public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Test implementation of BaseBackendPlugin for testing purposes.
    /// </summary>
    private sealed class TestBackendPlugin : BaseBackendPlugin<TestAccelerator, TestBackendOptions>
    {
        public override string Id => "test.backend";
        public override string Name => "Test Backend";
        public override Version Version => new(1, 0, 0);
        public override string Description => "Test backend for unit testing";
        public override string Author => "Test Author";
        public override PluginCapabilities Capabilities => PluginCapabilities.ComputeBackend;

        protected override string AcceleratorName => "test";
        protected override string ConfigurationSectionName => "TestBackend";

        protected override void RegisterAccelerator(IServiceCollection services, IConfiguration configuration)
            // Register a test accelerator for testing

            => services.AddSingleton<TestAccelerator>();
    }

    /// <summary>
    /// Test configuration options class.
    /// </summary>
    private sealed class TestBackendOptions
    {
        public bool EnableTestFeature { get; set; } = true;
        public int MaxTestItems { get; set; } = 100;
    }

    [Fact]
    public void Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange & Act
        using var plugin = new TestBackendPlugin();

        // Assert
        Assert.Equal("test.backend", plugin.Id);
        Assert.Equal("Test Backend", plugin.Name);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
        Assert.Equal("Test backend for unit testing", plugin.Description);
        Assert.Equal("Test Author", plugin.Author);
        Assert.Equal(PluginCapabilities.ComputeBackend, plugin.Capabilities);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterAcceleratorAndWrapper()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify the test accelerator is registered
        var testAccelerator = serviceProvider.GetService<TestAccelerator>();
        Assert.NotNull(testAccelerator);

        // Verify the named wrapper is registered
        var namedAccelerator = serviceProvider.GetService<IAccelerator>();
        Assert.NotNull(namedAccelerator);
        _ = Assert.IsType<NamedAcceleratorWrapper>(namedAccelerator);
        var wrapper = (NamedAcceleratorWrapper)namedAccelerator;
        Assert.Equal("test", wrapper.Name);
    }

    [Fact]
    public void ConfigureBackendOptions_ShouldBindConfigurationCorrectly()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string>
    {
        {"TestBackend:Options:EnableTestFeature", "false"},
        {"TestBackend:Options:MaxTestItems", "250"}
    };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<Microsoft.Extensions.Options.IOptions<TestBackendOptions>>();
        Assert.NotNull(options);
        Assert.False(options.Value.EnableTestFeature);
        Assert.Equal(250, options.Value.MaxTestItems);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCallOnBackendInitializeAsync()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        await plugin.InitializeAsync(serviceProvider);

        // Assert
        Assert.Equal(PluginState.Initialized, plugin.State);
        Assert.Equal(PluginHealth.Healthy, plugin.Health);
    }

    [Fact]
    public async Task StartAsync_ShouldUpdateStateToRunning()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        await plugin.InitializeAsync(serviceProvider);

        // Act
        await plugin.StartAsync();

        // Assert
        Assert.Equal(PluginState.Running, plugin.State);
    }

    [Fact]
    public async Task StopAsync_ShouldUpdateStateToStopped()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();

        await plugin.InitializeAsync(serviceProvider);
        await plugin.StartAsync();

        // Act
        await plugin.StopAsync();

        // Assert
        Assert.Equal(PluginState.Stopped, plugin.State);
    }

    [Fact]
    public void Validate_ShouldReturnValidResult()
    {
        // Arrange
        using var plugin = new TestBackendPlugin();

        // Act
        var result = plugin.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}

/// <summary>
/// Tests for the NamedAcceleratorWrapper class.
/// </summary>
public sealed class NamedAcceleratorWrapperTests
{
    [Fact]
    public void Constructor_ShouldSetNameAndDelegate()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        _ = mockAccelerator.SetupGet(x => x.Type).Returns(AcceleratorType.GPU);
        _ = mockAccelerator.SetupGet(x => x.Info).Returns(Mock.Of<AcceleratorInfo>());

        // Act
        var wrapper = new NamedAcceleratorWrapper("test-accelerator", mockAccelerator.Object);

        // Assert
        Assert.Equal("test-accelerator", wrapper.Name);
        Assert.Equal(AcceleratorType.GPU, wrapper.Type);
        Assert.NotNull(wrapper.Info);
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullName()
    {
        // Arrange
        var mockAccelerator = Mock.Of<IAccelerator>();

        // Act & Assert
        _ = Assert.Throws<ArgumentNullException>(() => new NamedAcceleratorWrapper(null!, mockAccelerator));
    }

    [Fact]
    public void Constructor_ShouldThrowOnNullAccelerator()
        // Act & Assert

        => Assert.Throws<ArgumentNullException>(() => new NamedAcceleratorWrapper("test", null!));

    [Fact]
    public async Task CompileKernelAsync_ShouldDelegateToUnderlyingAccelerator()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var mockCompiledKernel = Mock.Of<ICompiledKernel>();
        var kernelDefinition = Mock.Of<KernelDefinition>();

        _ = mockAccelerator
            .Setup(x => x.CompileKernelAsync(It.IsAny<KernelDefinition>(), It.IsAny<CompilationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockCompiledKernel);

        var wrapper = new NamedAcceleratorWrapper("test", mockAccelerator.Object);

        // Act
        var result = await wrapper.CompileKernelAsync(kernelDefinition);

        // Assert
        Assert.Equal(mockCompiledKernel, result);
        mockAccelerator.Verify(x => x.CompileKernelAsync(kernelDefinition, null, default), Times.Once);
    }

    [Fact]
    public async Task SynchronizeAsync_ShouldDelegateToUnderlyingAccelerator()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var wrapper = new NamedAcceleratorWrapper("test", mockAccelerator.Object);

        // Act
        await wrapper.SynchronizeAsync();

        // Assert
        mockAccelerator.Verify(x => x.SynchronizeAsync(default), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldDelegateToUnderlyingAccelerator()
    {
        // Arrange
        var mockAccelerator = new Mock<IAccelerator>();
        var wrapper = new NamedAcceleratorWrapper("test", mockAccelerator.Object);

        // Act
        await wrapper.DisposeAsync();

        // Assert
        mockAccelerator.Verify(x => x.DisposeAsync(), Times.Once);
    }
}
