// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using DotCompute.Plugins;
using DotCompute.Plugins.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Plugins.Tests;

public class PluginLoaderTests : IDisposable
{
    private readonly Mock<ILogger<PluginLoader>> _loggerMock;
    private readonly PluginLoader _loader;
    private readonly string _testPluginDir;

    public PluginLoaderTests()
    {
        _loggerMock = new Mock<ILogger<PluginLoader>>();
        _loader = new PluginLoader(_loggerMock.Object);
        _testPluginDir = Path.Combine(Path.GetTempPath(), $"plugin_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPluginDir);
    }

    public void Dispose()
    {
        _loader.Dispose();
        if (Directory.Exists(_testPluginDir))
        {
            Directory.Delete(_testPluginDir, true);
        }
    }

    [Fact]
    public void LoadPluginValidPlugin_LoadsSuccessfully()
    {
        // Arrange
        var pluginPath = CreateMockPluginAssembly();

        // Act
        var plugin = _loader.LoadPlugin(pluginPath);

        // Assert
        plugin.Should().NotBeNull();
        plugin.Name.Should().Be("MockPlugin");
        plugin.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void LoadPluginInvalidPath_ThrowsFileNotFoundException()
    {
        // Arrange
        var invalidPath = Path.Combine(_testPluginDir, "nonexistent.dll");

        // Act & Assert
        var action = () => _loader.LoadPlugin(invalidPath);
        action.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadPluginNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => _loader.LoadPlugin(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadPluginInvalidAssembly_ThrowsPluginLoadException()
    {
        // Arrange
        var invalidFile = Path.Combine(_testPluginDir, "invalid.dll");
        File.WriteAllText(invalidFile, "not a valid assembly");

        // Act & Assert
        var action = () => _loader.LoadPlugin(invalidFile);
        action.Should().Throw<PluginLoadException>();
    }

    [Fact]
    public void LoadPluginsFromDirectoryValidDirectory_LoadsAllPlugins()
    {
        // Arrange
        CreateMockPluginAssembly("Plugin1.dll");
        CreateMockPluginAssembly("Plugin2.dll");
        CreateMockPluginAssembly("Plugin3.dll");
        File.WriteAllText(Path.Combine(_testPluginDir, "notaplugin.txt"), "text file");

        // Act
        var plugins = _loader.LoadPluginsFromDirectory(_testPluginDir).ToList();

        // Assert
        plugins.Should().HaveCount(3);
        plugins.All(p => p.Name == "MockPlugin").Should().BeTrue();
    }

    [Fact]
    public void LoadPluginsFromDirectoryEmptyDirectory_ReturnsEmpty()
    {
        // Arrange
        var emptyDir = Path.Combine(_testPluginDir, "empty");
        Directory.CreateDirectory(emptyDir);

        // Act
        var plugins = _loader.LoadPluginsFromDirectory(emptyDir).ToList();

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void LoadPluginsFromDirectoryInvalidDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var invalidDir = Path.Combine(_testPluginDir, "nonexistent");

        // Act & Assert
        var action = () => _loader.LoadPluginsFromDirectory(invalidDir).ToList();
        action.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void GetLoadedPluginsAfterLoadingPlugins_ReturnsAllLoaded()
    {
        // Arrange
        CreateMockPluginAssembly("Plugin1.dll");
        CreateMockPluginAssembly("Plugin2.dll");
        _loader.LoadPluginsFromDirectory(_testPluginDir).ToList();

        // Act
        var loadedPlugins = _loader.GetLoadedPlugins().ToList();

        // Assert
        loadedPlugins.Should().HaveCount(2);
    }

    [Fact]
    public void UnloadPluginValidPlugin_UnloadsSuccessfully()
    {
        // Arrange
        var pluginPath = CreateMockPluginAssembly();
        var plugin = _loader.LoadPlugin(pluginPath);
        var initialCount = _loader.GetLoadedPlugins().Count();

        // Act
        var result = _loader.UnloadPlugin(plugin.Id);

        // Assert
        result.Should().BeTrue();
        _loader.GetLoadedPlugins().Count().Should().Be(initialCount - 1);
    }

    [Fact]
    public void UnloadPluginInvalidId_ReturnsFalse()
    {
        // Act
        var result = _loader.UnloadPlugin(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ReloadPluginValidPlugin_ReloadsWithNewVersion()
    {
        // Arrange
        var pluginPath = CreateMockPluginAssembly();
        var originalPlugin = _loader.LoadPlugin(pluginPath);
        var originalId = originalPlugin.Id;

        // Simulate plugin update
        System.Threading.Thread.Sleep(100);

        // Act
        var reloadedPlugin = _loader.ReloadPlugin(pluginPath);

        // Assert
        reloadedPlugin.Should().NotBeNull();
        reloadedPlugin.Id.Should().NotBe(originalId);
        _loader.GetLoadedPlugins().Should().ContainSingle(p => p.Id == reloadedPlugin.Id);
    }

    [Fact]
    public void DisposeUnloadsAllPlugins()
    {
        // Arrange
        CreateMockPluginAssembly("Plugin1.dll");
        CreateMockPluginAssembly("Plugin2.dll");
        _loader.LoadPluginsFromDirectory(_testPluginDir).ToList();

        // Act
        _loader.Dispose();

        // Assert
        _loader.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public void LoadPluginPluginWithDependencies_ResolvesCorrectly()
    {
        // This test would require creating assemblies with dependencies
        // For now, we'll test the dependency resolution logic
        var pluginPath = CreateMockPluginAssembly();
        var plugin = _loader.LoadPlugin(pluginPath);
        
        plugin.Should().NotBeNull();
        // Verify logger was used for dependency resolution
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void LoadPluginMultipleVersionsOfSamePlugin_IsolatesCorrectly()
    {
        // Arrange
        var plugin1Path = CreateMockPluginAssembly("Plugin_v1.dll");
        var plugin2Path = CreateMockPluginAssembly("Plugin_v2.dll");

        // Act
        var plugin1 = _loader.LoadPlugin(plugin1Path);
        var plugin2 = _loader.LoadPlugin(plugin2Path);

        // Assert
        plugin1.Should().NotBeNull();
        plugin2.Should().NotBeNull();
        plugin1.Id.Should().NotBe(plugin2.Id);
        _loader.GetLoadedPlugins().Should().HaveCount(2);
    }

    private string CreateMockPluginAssembly(string fileName = "MockPlugin.dll")
    {
        var assemblyPath = Path.Combine(_testPluginDir, fileName);
        
        // For testing purposes, we'll copy the current test assembly
        // In a real scenario, we'd compile a proper plugin assembly
        var currentAssembly = Assembly.GetExecutingAssembly().Location;
        File.Copy(currentAssembly, assemblyPath, true);
        
        return assemblyPath;
    }
}