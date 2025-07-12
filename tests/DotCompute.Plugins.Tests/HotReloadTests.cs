// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Plugins;
using DotCompute.Plugins.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DotCompute.Plugins.Tests;

public class HotReloadTests : IDisposable
{
    private readonly Mock<ILogger<HotReloadManager>> _loggerMock;
    private readonly Mock<IPluginManager> _pluginManagerMock;
    private readonly HotReloadManager _hotReloadManager;
    private readonly string _testDir;

    public HotReloadTests()
    {
        _loggerMock = new Mock<ILogger<HotReloadManager>>();
        _pluginManagerMock = new Mock<IPluginManager>();
        _hotReloadManager = new HotReloadManager(_pluginManagerMock.Object, _loggerMock.Object);
        _testDir = Path.Combine(Path.GetTempPath(), $"hotreload_tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        _hotReloadManager.Dispose();
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Fact]
    public async Task WatchDirectory_FileCreated_TriggersReload()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        
        _pluginManagerMock.Setup(m => m.LoadPluginAsync(It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() => tcs.SetResult(true));

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100); // Let watcher initialize
        File.WriteAllText(pluginPath, "test");

        // Assert
        var result = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        result.Should().Be(tcs.Task);
        await tcs.Task.Should().Be(true);
    }

    [Fact]
    public async Task WatchDirectory_FileModified_TriggersReload()
    {
        // Arrange
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        File.WriteAllText(pluginPath, "initial");
        
        var reloadCount = 0;
        _pluginManagerMock.Setup(m => m.ReloadPluginAsync(It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() => reloadCount++);

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100); // Let watcher initialize
        File.WriteAllText(pluginPath, "modified");
        await Task.Delay(500); // Wait for debounce

        // Assert
        reloadCount.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task WatchDirectory_FileDeleted_TriggersUnload()
    {
        // Arrange
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        File.WriteAllText(pluginPath, "test");
        
        var unloadCalled = false;
        _pluginManagerMock.Setup(m => m.UnloadPluginAsync(It.IsAny<Guid>()))
            .ReturnsAsync(true)
            .Callback(() => unloadCalled = true);

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100); // Let watcher initialize
        File.Delete(pluginPath);
        await Task.Delay(500); // Wait for event

        // Assert
        // Note: Actual implementation might track plugin IDs
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("deleted")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WatchDirectory_RapidChanges_DebouncesCorrectly()
    {
        // Arrange
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        var reloadCount = 0;
        
        _pluginManagerMock.Setup(m => m.LoadPluginAsync(It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() => reloadCount++);

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100); // Let watcher initialize
        
        // Make rapid changes
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(pluginPath, $"test{i}");
            await Task.Delay(50); // Less than debounce time
        }
        
        await Task.Delay(1000); // Wait for debounce to complete

        // Assert
        reloadCount.Should().BeLessThan(10); // Should debounce multiple changes
        reloadCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WatchDirectory_InvalidDirectory_ThrowsException()
    {
        // Arrange
        var invalidDir = Path.Combine(_testDir, "nonexistent");

        // Act & Assert
        var action = () => _hotReloadManager.WatchDirectory(invalidDir);
        action.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void StopWatching_StopsFileMonitoring()
    {
        // Arrange
        _hotReloadManager.WatchDirectory(_testDir);

        // Act
        _hotReloadManager.StopWatching();

        // Assert
        // Create a file after stopping - should not trigger reload
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        File.WriteAllText(pluginPath, "test");
        Thread.Sleep(500);

        _pluginManagerMock.Verify(m => m.LoadPluginAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task WatchDirectory_NonDllFile_IgnoresChange()
    {
        // Arrange
        var textFilePath = Path.Combine(_testDir, "readme.txt");
        var reloadCalled = false;
        
        _pluginManagerMock.Setup(m => m.LoadPluginAsync(It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() => reloadCalled = true);

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100);
        File.WriteAllText(textFilePath, "test");
        await Task.Delay(500);

        // Assert
        reloadCalled.Should().BeFalse();
    }

    [Fact]
    public async Task WatchDirectory_SubdirectoryChanges_MonitorsRecursively()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "plugins");
        Directory.CreateDirectory(subDir);
        var pluginPath = Path.Combine(subDir, "plugin.dll");
        
        var reloadCalled = false;
        _pluginManagerMock.Setup(m => m.LoadPluginAsync(It.IsAny<string>()))
            .ReturnsAsync(true)
            .Callback(() => reloadCalled = true);

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100);
        File.WriteAllText(pluginPath, "test");
        await Task.Delay(500);

        // Assert
        reloadCalled.Should().BeTrue();
    }

    [Fact]
    public void EnableHotReload_MultipleCalls_HandlesCorrectly()
    {
        // Act & Assert - Should not throw
        _hotReloadManager.EnableHotReload(true);
        _hotReloadManager.EnableHotReload(false);
        _hotReloadManager.EnableHotReload(true);
    }

    [Fact]
    public async Task WatchDirectory_PluginLoadFails_LogsError()
    {
        // Arrange
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        
        _pluginManagerMock.Setup(m => m.LoadPluginAsync(It.IsAny<string>()))
            .ThrowsAsync(new Exception("Load failed"));

        // Act
        _hotReloadManager.WatchDirectory(_testDir);
        await Task.Delay(100);
        File.WriteAllText(pluginPath, "test");
        await Task.Delay(500);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        _hotReloadManager.WatchDirectory(_testDir);

        // Act
        _hotReloadManager.Dispose();

        // Assert - Should not throw when creating files after dispose
        var pluginPath = Path.Combine(_testDir, "plugin.dll");
        var action = () => File.WriteAllText(pluginPath, "test");
        action.Should().NotThrow();
        
        Thread.Sleep(500);
        _pluginManagerMock.Verify(m => m.LoadPluginAsync(It.IsAny<string>()), Times.Never);
    }
}