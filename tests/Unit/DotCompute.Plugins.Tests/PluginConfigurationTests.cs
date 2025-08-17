// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Configuration;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Unit;

/// <summary>
/// Tests for plugin configuration classes covering options, settings, and directory management.
/// </summary>
public sealed class PluginOptionsTests
{
    [Fact]
    public void PluginOptions_DefaultConstructor_SetsDefaults()
    {
        // Act
        var options = new PluginOptions();

        // Assert
        Assert.Null(options.PluginsDirectory);
        options.EnableHotReload.Should().BeFalse();
        options.IsolatePlugins.Should().BeTrue();
        options.MaxConcurrentLoads.Should().Be(4);
        options.LoadTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.IsInitialized.Should().BeFalse();

        options.SharedAssemblies.Should().NotBeEmpty();
        options.SharedAssemblies.Should().Contain("DotCompute.Core");
        options.SharedAssemblies.Should().Contain("DotCompute.Plugins");
        options.SharedAssemblies.Should().Contain("Microsoft.Extensions.DependencyInjection.Abstractions");

        options.Plugins.Should().NotBeNull();
        options.Plugins.Should().BeEmpty();

        options.PluginDirectories.Should().NotBeNull();
        options.PluginDirectories.Should().BeEmpty();
    }

    [Fact]
    public void PluginOptions_SetPluginsDirectory_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();
        const string directory = "/path/to/plugins";

        // Act
        options.PluginsDirectory = directory;

        // Assert
        options.PluginsDirectory.Should().Be(directory);
    }

    [Fact]
    public void PluginOptions_EnableHotReload_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.EnableHotReload = true;

        // Assert
        options.EnableHotReload.Should().BeTrue();
    }

    [Fact]
    public void PluginOptions_IsolatePlugins_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.IsolatePlugins = false;

        // Assert
        options.IsolatePlugins.Should().BeFalse();
    }

    [Fact]
    public void PluginOptions_MaxConcurrentLoads_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.MaxConcurrentLoads = 8;

        // Assert
        options.MaxConcurrentLoads.Should().Be(8);
    }

    [Fact]
    public void PluginOptions_LoadTimeout_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();
        var timeout = TimeSpan.FromMinutes(2);

        // Act
        options.LoadTimeout = timeout;

        // Assert
        options.LoadTimeout.Should().Be(timeout);
    }

    [Fact]
    public void PluginOptions_IsInitialized_UpdatesProperty()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.IsInitialized = true;

        // Assert
        options.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void PluginOptions_SharedAssemblies_CanBeModified()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.SharedAssemblies.Add("CustomAssembly");
        options.SharedAssemblies.Remove("DotCompute.Core");

        // Assert
        options.SharedAssemblies.Should().Contain("CustomAssembly");
        options.SharedAssemblies.Should().NotContain("DotCompute.Core");
    }

    [Fact]
    public void PluginOptions_Plugins_CanBeModified()
    {
        // Arrange
        var options = new PluginOptions();
        var pluginConfig = new PluginConfig
        {
            AssemblyPath = "/path/to/plugin.dll",
            TypeName = "Test.Plugin",
            Enabled = true
        };

        // Act
        options.Plugins["test-plugin"] = pluginConfig;

        // Assert
        options.Plugins.Should().ContainKey("test-plugin");
        options.Plugins["test-plugin"].Should().BeSameAs(pluginConfig);
    }

    [Fact]
    public void PluginOptions_PluginDirectories_CanBeModified()
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.PluginDirectories.Add("/path/to/plugins1");
        options.PluginDirectories.Add("/path/to/plugins2");

        // Assert
        options.PluginDirectories.Count.Should().Be(2);
        options.PluginDirectories.Should().Contain("/path/to/plugins1");
        options.PluginDirectories.Should().Contain("/path/to/plugins2");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    public void PluginOptions_MaxConcurrentLoads_AcceptsValidValues(int maxLoads)
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.MaxConcurrentLoads = maxLoads;

        // Assert
        options.MaxConcurrentLoads.Should().Be(maxLoads);
    }

    [Theory]
    [InlineData(-1)]
    public void PluginOptions_MaxConcurrentLoads_AcceptsNegativeValues(int maxLoads)
    {
        // Arrange
        var options = new PluginOptions();

        // Act & Assert - No validation in the class, so negative values are allowed
        Action act = () => options.MaxConcurrentLoads = maxLoads;
        act(); // Should not throw
        options.MaxConcurrentLoads.Should().Be(maxLoads);
    }
}

/// <summary>
/// Tests for the PluginConfig class.
/// </summary>
public class PluginConfigTests
{
    [Fact]
    public void PluginConfig_DefaultConstructor_SetsDefaults()
    {
        // Act
        var config = new PluginConfig();

        // Assert
        config.AssemblyPath.Should().Be("");
        config.TypeName.Should().Be("");
        config.Enabled.Should().BeTrue();
        config.Settings.Should().NotBeNull();
        config.Settings.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfig_AssemblyPath_UpdatesProperty()
    {
        // Arrange
        var config = new PluginConfig();
        const string path = "/path/to/plugin.dll";

        // Act
        config.AssemblyPath = path;

        // Assert
        config.AssemblyPath.Should().Be(path);
    }

    [Fact]
    public void PluginConfig_TypeName_UpdatesProperty()
    {
        // Arrange
        var config = new PluginConfig();
        const string typeName = "Test.Plugin.Class";

        // Act
        config.TypeName = typeName;

        // Assert
        config.TypeName.Should().Be(typeName);
    }

    [Fact]
    public void PluginConfig_Enabled_UpdatesProperty()
    {
        // Arrange
        var config = new PluginConfig();

        // Act
        config.Enabled = false;

        // Assert
        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void PluginConfig_Settings_CanBeModified()
    {
        // Arrange
        var config = new PluginConfig();

        // Act
        config.Settings["StringSetting"] = "test value";
        config.Settings["IntSetting"] = 42;
        config.Settings["BoolSetting"] = true;
        config.Settings["DoubleSetting"] = 3.14;

        // Assert
        config.Settings.Count.Should().Be(4);
        config.Settings["StringSetting"].Should().Be("test value");
        config.Settings["IntSetting"].Should().Be(42);
        config.Settings["BoolSetting"].Should().Be(true);
        config.Settings["DoubleSetting"].Should().Be(3.14);
    }

    [Fact]
    public void PluginConfig_Settings_SupportsComplexObjects()
    {
        // Arrange
        var config = new PluginConfig();
        var complexObject = new { Name = "Test", Value = 123 };
        var arrayObject = new[] { 1, 2, 3 };

        // Act
        config.Settings["ComplexObject"] = complexObject;
        config.Settings["ArrayObject"] = arrayObject;

        // Assert
        config.Settings["ComplexObject"].Should().Be(complexObject);
        config.Settings["ArrayObject"].Should().Be(arrayObject);
    }

    [Fact]
    public void PluginConfig_Settings_CanBeCleared()
    {
        // Arrange
        var config = new PluginConfig();
        config.Settings["test"] = "value";

        // Act
        config.Settings.Clear();

        // Assert
        config.Settings.Should().BeEmpty();
    }

    [Fact]
    public void PluginConfig_Settings_SupportsNullValues()
    {
        // Arrange
        var config = new PluginConfig();

        // Act
        config.Settings["NullValue"] = null!;

        // Assert
        config.Settings.Should().ContainKey("NullValue");
        config.Settings["NullValue"].Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void PluginConfig_AssemblyPath_AcceptsNullOrEmpty(string? path)
    {
        // Arrange
        var config = new PluginConfig();

        // Act
        config.AssemblyPath = path!;

        // Assert
        config.AssemblyPath.Should().Be(path ?? "");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void PluginConfig_TypeName_AcceptsNullOrEmpty(string? typeName)
    {
        // Arrange
        var config = new PluginConfig();

        // Act
        config.TypeName = typeName!;

        // Assert
        config.TypeName.Should().Be(typeName ?? "");
    }

    [Fact]
    public void PluginConfig_WithValidConfiguration_CanBeCreated()
    {
        // Arrange & Act
        var config = new PluginConfig
        {
            AssemblyPath = "/path/to/MyPlugin.dll",
            TypeName = "MyCompany.MyPlugin.Backend",
            Enabled = true,
            Settings =
            {
                ["MaxThreads"] = 8,
                ["CacheSizeBytes"] = 1024 * 1024,
                ["EnableLogging"] = true,
                ["LogLevel"] = "Information"
            }
        };

        // Assert
        config.AssemblyPath.Should().Be("/path/to/MyPlugin.dll");
        config.TypeName.Should().Be("MyCompany.MyPlugin.Backend");
        config.Enabled.Should().BeTrue();
        config.Settings.Count.Should().Be(4);
        config.Settings["MaxThreads"].Should().Be(8);
        config.Settings["CacheSizeBytes"].Should().Be(1024 * 1024);
        config.Settings["EnableLogging"].Should().Be(true);
        config.Settings["LogLevel"].Should().Be("Information");
    }

    [Fact]
    public void PluginConfig_Settings_AreReferenceEqual()
    {
        // Arrange
        var config1 = new PluginConfig();
        var config2 = new PluginConfig();

        // Act
        var settings1 = config1.Settings;
        var settings1Again = config1.Settings;
        var settings2 = config2.Settings;

        // Assert
        settings1.Should().BeSameAs(settings1Again);
        settings1.Should().NotBeSameAs(settings2);
    }
}

/// <summary>
/// Integration tests for PluginOptions and PluginConfig working together.
/// </summary>
public class PluginConfigurationIntegrationTests
{
    [Fact]
    public void PluginOptions_WithMultiplePluginConfigs_WorksTogether()
    {
        // Arrange
        var options = new PluginOptions();

        var cpuConfig = new PluginConfig
        {
            AssemblyPath = "/plugins/DotCompute.Backends.CPU.dll",
            TypeName = "DotCompute.Backends.CPU.CpuBackendPlugin",
            Enabled = true,
            Settings =
            {
                ["MaxThreads"] = Environment.ProcessorCount,
                ["EnableSIMD"] = true
            }
        };

        var cudaConfig = new PluginConfig
        {
            AssemblyPath = "/plugins/DotCompute.Backends.CUDA.dll",
            TypeName = "DotCompute.Backends.CUDA.CudaBackendPlugin",
            Enabled = false, // Disabled by default
            Settings =
            {
                ["DeviceId"] = 0,
                ["MemoryPoolSize"] = 512 * 1024 * 1024 // 512MB
            }
        };

        // Act
        options.Plugins["cpu-backend"] = cpuConfig;
        options.Plugins["cuda-backend"] = cudaConfig;
        options.PluginDirectories.Add("/plugins");
        options.PluginDirectories.Add("/custom-plugins");
        options.EnableHotReload = true;
        options.MaxConcurrentLoads = 2;

        // Assert
        options.Plugins.Count.Should().Be(2);
        options.Plugins["cpu-backend"].Should().BeSameAs(cpuConfig);
        options.Plugins["cuda-backend"].Should().BeSameAs(cudaConfig);
        options.PluginDirectories.Count.Should().Be(2);
        options.EnableHotReload.Should().BeTrue();
        options.MaxConcurrentLoads.Should().Be(2);

        // Verify individual plugin configurations
        cpuConfig.Enabled.Should().BeTrue();
        cpuConfig.Settings["MaxThreads"].Should().Be(Environment.ProcessorCount);

        cudaConfig.Enabled.Should().BeFalse();
        cudaConfig.Settings["MemoryPoolSize"].Should().Be(512 * 1024 * 1024);
    }

    [Fact]
    public void PluginOptions_SharedAssemblies_DefaultsIncludeRequiredAssemblies()
    {
        // Arrange
        var options = new PluginOptions();

        // Act & Assert
        options.SharedAssemblies.Should().Contain("DotCompute.Core");
        options.SharedAssemblies.Should().Contain("DotCompute.Plugins");
        options.SharedAssemblies.Should().Contain("Microsoft.Extensions.DependencyInjection.Abstractions");
        options.SharedAssemblies.Should().Contain("Microsoft.Extensions.Logging.Abstractions");
        options.SharedAssemblies.Should().Contain("Microsoft.Extensions.Configuration.Abstractions");
    }

    [Fact]
    public void PluginOptions_ModifiedSharedAssemblies_PersistsChanges()
    {
        // Arrange
        var options = new PluginOptions();
        var originalCount = options.SharedAssemblies.Count;

        // Act
        options.SharedAssemblies.Add("Custom.Shared.Assembly");
        options.SharedAssemblies.Remove("Microsoft.Extensions.Configuration.Abstractions");

        // Assert
        options.SharedAssemblies.Count.Should().Be(originalCount); // +1 -1 = same count
        options.SharedAssemblies.Should().Contain("Custom.Shared.Assembly");
        options.SharedAssemblies.Should().NotContain("Microsoft.Extensions.Configuration.Abstractions");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    public void PluginOptions_MaxConcurrentLoads_WithDifferentValues_WorksCorrectly(int maxLoads)
    {
        // Arrange
        var options = new PluginOptions();

        // Act
        options.MaxConcurrentLoads = maxLoads;

        // Assert
        options.MaxConcurrentLoads.Should().Be(maxLoads);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(120)]
    public void PluginOptions_LoadTimeout_WithDifferentTimeouts_WorksCorrectly(int seconds)
    {
        // Arrange
        var options = new PluginOptions();
        var timeout = TimeSpan.FromSeconds(seconds);

        // Act
        options.LoadTimeout = timeout;

        // Assert
        options.LoadTimeout.Should().Be(timeout);
    }
}
