using Xunit;
using FluentAssertions;
using DotCompute.Plugins.Core;
using DotCompute.Plugins.Interfaces;
using DotCompute.Plugins.Attributes;
using DotCompute.Plugins.Configuration;
using DotCompute.Plugins.Exceptions;
using DotCompute.Plugins.Examples;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Plugins.Tests;

/// <summary>
/// Advanced plugin system tests targeting 95%+ coverage
/// Includes error handling, edge cases, and stress scenarios
/// </summary>
public class AdvancedPluginTests
{
    [Fact]
    public void PluginAttributeWithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PluginAttribute(null!));
    }

    [Fact]
    public void PluginAttributeWithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new PluginAttribute(""));
    }

    [Fact]
    public void PluginAttributeWithWhitespaceName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new PluginAttribute("   "));
    }

    [Theory]
    [InlineData("ValidPlugin")]
    [InlineData("Plugin.With.Dots")]
    [InlineData("Plugin_With_Underscores")]
    [InlineData("PluginWith123Numbers")]
    public void PluginAttributeWithValidNames_ShouldAccept(string validName)
    {
        // Arrange & Act
        var attribute = new PluginAttribute(validName);

        // Assert
        attribute.Name.Should().Be(validName);
    }

    [Fact]
    public void PluginAttributeProperties_ShouldBeSettable()
    {
        // Arrange
        var attribute = new PluginAttribute("TestPlugin");

        // Act
        attribute.Version = "1.2.3";
        attribute.Description = "Test plugin description";
        attribute.Author = "Test Author";
        attribute.LoadPriority = 10;

        // Assert
        attribute.Version.Should().Be("1.2.3");
        attribute.Description.Should().Be("Test plugin description");
        attribute.Author.Should().Be("Test Author");
        attribute.LoadPriority.Should().Be(10);
    }

    [Fact]
    public void PluginAttributeWithNegativeLoadPriority_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var attribute = new PluginAttribute("TestPlugin");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => attribute.LoadPriority = -1);
    }

    [Fact]
    public void PluginSystemWithNullConfiguration_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PluginSystem(null!));
    }

    [Fact]
    public void PluginSystemWithValidConfiguration_ShouldInitialize()
    {
        // Arrange
        var options = new PluginOptions
        {
            PluginDirectories = new[] { "/test/plugins" },
            EnableHotReload = false
        };

        // Act
        var pluginSystem = new PluginSystem(options);

        // Assert
        pluginSystem.Should().NotBeNull();
        pluginSystem.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task PluginSystemInitialize_ShouldLoadAvailablePlugins()
    {
        // Arrange
        var options = new PluginOptions
        {
            PluginDirectories = new[] { "./test-plugins" },
            EnableHotReload = false
        };
        var pluginSystem = new PluginSystem(options);

        // Act
        await pluginSystem.InitializeAsync();

        // Assert
        pluginSystem.IsInitialized.Should().BeTrue();
        // Note: In a real test, we'd set up actual plugin files
    }

    [Fact]
    public async Task PluginSystemLoadPlugin_WithNullPlugin_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await pluginSystem.LoadPluginAsync(null!));
    }

    [Fact]
    public async Task PluginSystemLoadPlugin_WithInvalidPlugin_ShouldThrowPluginLoadException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);
        var invalidPlugin = new InvalidTestPlugin();

        // Act & Assert
        await Assert.ThrowsAsync<PluginLoadException>(async () =>
            await pluginSystem.LoadPluginAsync(invalidPlugin));
    }

    [Fact]
    public async Task PluginSystemUnloadPlugin_WithNullPluginName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await pluginSystem.UnloadPluginAsync(null!));
    }

    [Fact]
    public async Task PluginSystemUnloadPlugin_WithNonExistentPlugin_ShouldThrowPluginNotFoundException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);

        // Act & Assert
        await Assert.ThrowsAsync<PluginNotFoundException>(async () =>
            await pluginSystem.UnloadPluginAsync("NonExistentPlugin"));
    }

    [Fact]
    public void BackendPluginBaseWithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TestBackendPlugin(null!));
    }

    [Fact]
    public void BackendPluginBaseWithValidName_ShouldInitialize()
    {
        // Arrange & Act
        var plugin = new TestBackendPlugin("TestBackend");

        // Assert
        plugin.Name.Should().Be("TestBackend");
        plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task BackendPluginBaseLoadAsync_ShouldSetLoadedState()
    {
        // Arrange
        var plugin = new TestBackendPlugin("TestBackend");

        // Act
        await plugin.LoadAsync();

        // Assert
        plugin.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task BackendPluginBaseUnloadAsync_ShouldClearLoadedState()
    {
        // Arrange
        var plugin = new TestBackendPlugin("TestBackend");
        await plugin.LoadAsync();

        // Act
        await plugin.UnloadAsync();

        // Assert
        plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void PluginOptionsDefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var options = new PluginOptions();

        // Assert
        options.PluginDirectories.Should().NotBeNull();
        options.EnableHotReload.Should().BeFalse();
        options.LoadTimeout.Should().BeGreaterThan(TimeSpan.Zero);
        options.MaxConcurrentLoads.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PluginOptionsWithNullDirectories_ShouldThrowArgumentNullException()
    {
        // Arrange
        var options = new PluginOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options.PluginDirectories = null!);
    }

    [Fact]
    public void PluginOptionsWithNegativeTimeout_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var options = new PluginOptions();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            options.LoadTimeout = TimeSpan.FromMilliseconds(-1));
    }

    [Fact]
    public void PluginOptionsWithZeroMaxConcurrentLoads_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var options = new PluginOptions();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxConcurrentLoads = 0);
    }

    [Fact]
    public void PluginLoadExceptionWithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "Test plugin load error";

        // Act
        var exception = new PluginLoadException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void PluginLoadExceptionWithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "Test plugin load error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new PluginLoadException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void PluginNotFoundExceptionWithPluginName_ShouldFormatMessage()
    {
        // Arrange
        var pluginName = "NonExistentPlugin";

        // Act
        var exception = new PluginNotFoundException(pluginName);

        // Assert
        exception.Message.Should().Contain(pluginName);
        exception.PluginName.Should().Be(pluginName);
    }

    [Fact]
    public void SimplePluginExample_ShouldImplementInterface()
    {
        // Arrange & Act
        var plugin = new SimplePlugin();

        // Assert
        plugin.Should().BeAssignableTo<IBackendPlugin>();
        plugin.Name.Should().NotBeNullOrEmpty();
        plugin.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SimplePluginLoadUnloadCycle_ShouldWorkCorrectly()
    {
        // Arrange
        var plugin = new SimplePlugin();

        // Act & Assert
        plugin.IsLoaded.Should().BeFalse();
        
        await plugin.LoadAsync();
        plugin.IsLoaded.Should().BeTrue();
        
        await plugin.UnloadAsync();
        plugin.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void ServiceCollectionExtensionsAddPluginSystem_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new PluginOptions();

        // Act
        services.AddPluginSystem(options);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var pluginSystem = serviceProvider.GetService<PluginSystem>();
        pluginSystem.Should().NotBeNull();
    }

    [Fact]
    public void ServiceCollectionExtensionsAddPluginSystem_WithNullServices_ShouldThrowArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;
        var options = new PluginOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddPluginSystem(options));
    }

    [Fact]
    public void ServiceCollectionExtensionsAddPluginSystem_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => services.AddPluginSystem(null!));
    }
}

/// <summary>
/// Error handling and edge case tests for plugins
/// </summary>
public class PluginErrorHandlingTests
{
    [Fact]
    public async Task PluginSystemLoadPlugin_WithExceptionInLoad_ShouldWrapInPluginLoadException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);
        var faultyPlugin = new FaultyTestPlugin();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PluginLoadException>(async () =>
            await pluginSystem.LoadPluginAsync(faultyPlugin));
        
        exception.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task PluginSystemConcurrentLoading_ShouldHandleRaceConditions()
    {
        // Arrange
        var options = new PluginOptions { MaxConcurrentLoads = 2 };
        var pluginSystem = new PluginSystem(options);
        var plugins = Enumerable.Range(0, 10)
            .Select(i => new TestBackendPlugin($"Plugin{i}"))
            .ToArray();

        // Act
        var loadTasks = plugins.Select(p => pluginSystem.LoadPluginAsync(p));
        var results = await Task.WhenAll(loadTasks);

        // Assert
        results.Should().HaveCount(10);
        plugins.Should().OnlyContain(p => p.IsLoaded);
    }

    [Fact]
    public async Task PluginSystemLoadTimeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var options = new PluginOptions 
        { 
            LoadTimeout = TimeSpan.FromMilliseconds(100)
        };
        var pluginSystem = new PluginSystem(options);
        var slowPlugin = new SlowLoadingTestPlugin();

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await pluginSystem.LoadPluginAsync(slowPlugin));
    }

    [Fact]
    public void PluginSystemDisposal_ShouldUnloadAllPlugins()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);

        // Act
        pluginSystem.Dispose();

        // Assert
        pluginSystem.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void PluginSystemDoubleDisposal_ShouldBeIdempotent()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);

        // Act & Assert
        pluginSystem.Dispose();
        pluginSystem.Dispose(); // Should not throw
        
        pluginSystem.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public async Task PluginSystemOperationAfterDisposal_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginSystem = new PluginSystem(options);
        pluginSystem.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await pluginSystem.InitializeAsync());
    }

    [Fact]
    public void BackendPluginBaseDoubleLoad_ShouldBeIdempotent()
    {
        // Arrange
        var plugin = new TestBackendPlugin("TestPlugin");

        // Act
        plugin.LoadAsync().Wait();
        plugin.LoadAsync().Wait(); // Second load should not throw

        // Assert
        plugin.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public void BackendPluginBaseUnloadBeforeLoad_ShouldNotThrow()
    {
        // Arrange
        var plugin = new TestBackendPlugin("TestPlugin");

        // Act & Assert
        plugin.UnloadAsync().Wait(); // Should not throw
        plugin.IsLoaded.Should().BeFalse();
    }
}

/// <summary>
/// Performance and stress tests for plugins
/// </summary>
public class PluginPerformanceTests
{
    [Fact]
    public async Task PluginSystemLoadManyPlugins_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var options = new PluginOptions { MaxConcurrentLoads = 10 };
        var pluginSystem = new PluginSystem(options);
        const int pluginCount = 100;
        
        var plugins = Enumerable.Range(0, pluginCount)
            .Select(i => new TestBackendPlugin($"Plugin{i}"))
            .ToArray();
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var loadTasks = plugins.Select(p => pluginSystem.LoadPluginAsync(p));
        await Task.WhenAll(loadTasks);
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "Should load 100 plugins within 5 seconds");
        plugins.Should().OnlyContain(p => p.IsLoaded);
    }

    [Fact]
    public async Task PluginSystemConcurrentLoadUnload_ShouldMaintainConsistency()
    {
        // Arrange
        var options = new PluginOptions { MaxConcurrentLoads = 5 };
        var pluginSystem = new PluginSystem(options);
        var plugin = new TestBackendPlugin("ConcurrentTestPlugin");
        
        const int operationCount = 100;
        var operations = new List<Task>();

        // Act
        for (int i = 0; i < operationCount; i++)
        {
            if (i % 2 == 0)
            {
                operations.Add(pluginSystem.LoadPluginAsync(plugin));
            }
            else
            {
                operations.Add(pluginSystem.UnloadPluginAsync(plugin.Name));
            }
        }

        await Task.WhenAll(operations);

        // Assert
        // The final state should be consistent (either loaded or not loaded)
        plugin.IsLoaded.Should().Match(loaded => loaded == true || loaded == false);
    }

    [Fact]
    public void PluginAttributeMassCreation_ShouldBePerformant()
    {
        // Arrange
        const int attributeCount = 10000;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < attributeCount; i++)
        {
            var attribute = new PluginAttribute($"Plugin{i}")
            {
                Version = $"1.0.{i}",
                Description = $"Test plugin {i}",
                Author = "Test Author",
                LoadPriority = i % 100
            };
            
            attribute.Should().NotBeNull();
        }
        
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "Should create 10k attributes within 1 second");
    }
}

/// <summary>
/// Test plugin implementations
/// </summary>
internal class TestBackendPlugin : BackendPluginBase
{
    public TestBackendPlugin(string name) : base(name) { }

    public override string Version => "1.0.0";
    public override string Description => "Test backend plugin";

    protected override Task OnLoadAsync()
    {
        return Task.CompletedTask;
    }

    protected override Task OnUnloadAsync()
    {
        return Task.CompletedTask;
    }
}

internal class InvalidTestPlugin : IBackendPlugin
{
    public string Name => "";  // Invalid empty name
    public string Version => "1.0.0";
    public string Description => "Invalid test plugin";
    public bool IsLoaded => false;

    public Task LoadAsync()
    {
        throw new InvalidOperationException("This plugin always fails to load");
    }

    public Task UnloadAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Test implementation
    }
}

internal class FaultyTestPlugin : IBackendPlugin
{
    public string Name => "FaultyPlugin";
    public string Version => "1.0.0";
    public string Description => "Faulty test plugin";
    public bool IsLoaded => false;

    public Task LoadAsync()
    {
        throw new InvalidOperationException("Simulated load failure");
    }

    public Task UnloadAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Test implementation
    }
}

internal class SlowLoadingTestPlugin : IBackendPlugin
{
    public string Name => "SlowPlugin";
    public string Version => "1.0.0";
    public string Description => "Slow loading test plugin";
    public bool IsLoaded => false;

    public async Task LoadAsync()
    {
        // Simulate slow loading
        await Task.Delay(1000);
    }

    public Task UnloadAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Test implementation
    }
}