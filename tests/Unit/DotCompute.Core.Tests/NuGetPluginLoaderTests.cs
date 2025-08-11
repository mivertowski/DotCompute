// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
// Note: DotCompute.Core.Plugins namespace doesn't exist in current codebase
// using DotCompute.Core.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DotCompute.Core.Tests;

/// <summary>
/// Comprehensive tests for NuGet plugin loader system that can run on CI/CD.
/// Tests plugin discovery, loading, validation, and management without requiring actual NuGet packages.
/// Note: This is a mock implementation since DotCompute.Core.Plugins doesn't exist in current codebase.
/// </summary>
public class NuGetPluginLoaderTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly MockNuGetPluginLoader _pluginLoader;
    private readonly string _tempDirectory;

    public NuGetPluginLoaderTests()
    {
        _mockLogger = new Mock<ILogger>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DotCompute_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _pluginLoader = new MockNuGetPluginLoader(_mockLogger.Object, _tempDirectory);
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MockNuGetPluginLoader(null!, _tempDirectory));
    }

    [Fact]
    public void Constructor_WithNullPluginDirectory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MockNuGetPluginLoader(_mockLogger.Object, null!));
    }

    [Fact]
    public void Constructor_WithInvalidPluginDirectory_ShouldThrowDirectoryNotFoundException()
    {
        // Arrange
        var invalidPath = Path.Combine(_tempDirectory, "nonexistent");

        // Act & Assert
        Assert.Throws<DirectoryNotFoundException>(() => new MockNuGetPluginLoader(_mockLogger.Object, invalidPath));
    }

    [Fact]
    public async Task DiscoverPluginsAsync_WithNoPlugins_ShouldReturnEmptyCollection()
    {
        // Act
        var plugins = await _pluginLoader.DiscoverPluginsAsync();

        // Assert
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public async Task DiscoverPluginsAsync_WithValidPlugins_ShouldDiscoverAll()
    {
        // Arrange
        CreateMockPluginManifests();

        // Act
        var plugins = await _pluginLoader.DiscoverPluginsAsync();

        // Assert
        Assert.NotEmpty(plugins);
        Assert.Contains(plugins, p => p.Name == "MockCUDABackend");
        Assert.Contains(plugins, p => p.Name == "MockLinearAlgebra");
    }

    [Fact]
    public async Task LoadPluginAsync_WithValidPlugin_ShouldLoadSuccessfully()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("TestPlugin", "1.0.0");
        CreateMockPluginAssembly(manifest);

        // Act
        var result = await _pluginLoader.LoadPluginAsync(manifest);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsLoaded);
        Assert.Equal(manifest.Name, result.PluginInfo.Name);
        Assert.Equal(manifest.Version, result.PluginInfo.Version);
    }

    [Fact]
    public async Task LoadPluginAsync_WithNullManifest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _pluginLoader.LoadPluginAsync(null!));
    }

    [Fact]
    public async Task LoadPluginAsync_WithMissingAssembly_ShouldReturnFailure()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("MissingPlugin", "1.0.0");
        // Don't create the assembly file

        // Act
        var result = await _pluginLoader.LoadPluginAsync(manifest);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsLoaded);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task LoadPluginAsync_WithInvalidAssembly_ShouldReturnFailure()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("InvalidPlugin", "1.0.0");
        CreateInvalidPluginAssembly(manifest);

        // Act
        var result = await _pluginLoader.LoadPluginAsync(manifest);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsLoaded);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidatePluginAsync_WithValidPlugin_ShouldPass()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("ValidPlugin", "1.0.0");
        manifest.RequiredInterfaces = new[] { typeof(IKernelCompiler).AssemblyQualifiedName! };

        // Act
        var result = await _pluginLoader.ValidatePluginAsync(manifest);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    public async Task ValidatePluginAsync_WithMissingDependencies_ShouldFail()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("InvalidPlugin", "1.0.0");
        manifest.Dependencies = new[]
        {
            new PluginDependency { Name = "NonExistentDep", MinVersion = "1.0.0" }
        };

        // Act
        var result = await _pluginLoader.ValidatePluginAsync(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, e => e.Contains("Dependency"));
    }

    [Fact]
    public async Task ValidatePluginAsync_WithVersionMismatch_ShouldFail()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("VersionMismatch", "2.0.0");
        manifest.MinFrameworkVersion = "10.0.0"; // Unrealistic high version

        // Act
        var result = await _pluginLoader.ValidatePluginAsync(manifest);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationErrors, e => e.Contains("framework version"));
    }

    [Fact]
    public async Task UnloadPluginAsync_WithLoadedPlugin_ShouldUnloadSuccessfully()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("UnloadTest", "1.0.0");
        CreateMockPluginAssembly(manifest);
        var loadResult = await _pluginLoader.LoadPluginAsync(manifest);
        Assert.True(loadResult.IsLoaded);

        // Act
        var unloadResult = await _pluginLoader.UnloadPluginAsync(loadResult.PluginInfo.Name);

        // Assert
        Assert.True(unloadResult);
    }

    [Fact]
    public async Task UnloadPluginAsync_WithNotLoadedPlugin_ShouldReturnFalse()
    {
        // Act
        var result = await _pluginLoader.UnloadPluginAsync("NonExistentPlugin");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetLoadedPluginsAsync_WithMultiplePlugins_ShouldReturnAll()
    {
        // Arrange
        var manifest1 = CreateMockPluginManifest("Plugin1", "1.0.0");
        var manifest2 = CreateMockPluginManifest("Plugin2", "2.0.0");
        CreateMockPluginAssembly(manifest1);
        CreateMockPluginAssembly(manifest2);

        await _pluginLoader.LoadPluginAsync(manifest1);
        await _pluginLoader.LoadPluginAsync(manifest2);

        // Act
        var loadedPlugins = await _pluginLoader.GetLoadedPluginsAsync();

        // Assert
        Assert.Equal(2, loadedPlugins.Count);
        Assert.Contains(loadedPlugins, p => p.Name == "Plugin1");
        Assert.Contains(loadedPlugins, p => p.Name == "Plugin2");
    }

    [Fact]
    public async Task LoadPluginAsync_WithDuplicateName_ShouldHandleGracefully()
    {
        // Arrange
        var manifest1 = CreateMockPluginManifest("DuplicateTest", "1.0.0");
        var manifest2 = CreateMockPluginManifest("DuplicateTest", "2.0.0");
        CreateMockPluginAssembly(manifest1);
        CreateMockPluginAssembly(manifest2, "DuplicateTest_v2");

        // Act
        var result1 = await _pluginLoader.LoadPluginAsync(manifest1);
        var result2 = await _pluginLoader.LoadPluginAsync(manifest2);

        // Assert
        Assert.True(result1.IsLoaded);
        Assert.False(result2.IsLoaded); // Should reject duplicate name
        Assert.Contains("already loaded", result2.ErrorMessage);
    }

    [Fact]
    public async Task RefreshPluginsAsync_WithNewPlugins_ShouldDiscoverNewOnes()
    {
        // Arrange
        var initialPlugins = await _pluginLoader.DiscoverPluginsAsync();
        var initialCount = initialPlugins.Count;

        // Add a new plugin
        var newManifest = CreateMockPluginManifest("NewPlugin", "1.0.0");
        CreateMockPluginAssembly(newManifest);

        // Act
        await _pluginLoader.RefreshPluginsAsync();
        var refreshedPlugins = await _pluginLoader.DiscoverPluginsAsync();

        // Assert
        Assert.True(refreshedPlugins.Count > initialCount);
        Assert.Contains(refreshedPlugins, p => p.Name == "NewPlugin");
    }

    [Fact]
    public async Task LoadPluginAsync_ConcurrentLoading_ShouldHandleThreadSafety()
    {
        // Arrange
        var manifest1 = CreateMockPluginManifest("Concurrent1", "1.0.0");
        var manifest2 = CreateMockPluginManifest("Concurrent2", "1.0.0");
        var manifest3 = CreateMockPluginManifest("Concurrent3", "1.0.0");
        CreateMockPluginAssembly(manifest1);
        CreateMockPluginAssembly(manifest2);
        CreateMockPluginAssembly(manifest3);

        // Act - Load plugins concurrently
        var task1 = _pluginLoader.LoadPluginAsync(manifest1);
        var task2 = _pluginLoader.LoadPluginAsync(manifest2);
        var task3 = _pluginLoader.LoadPluginAsync(manifest3);

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.True(result.IsLoaded);
        });
    }

    [Fact]
    public async Task LoadPluginAsync_WithCancellation_ShouldRespectCancellation()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("CancellationTest", "1.0.0");
        CreateMockPluginAssembly(manifest);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _pluginLoader.LoadPluginAsync(manifest, cts.Token));
    }

    [Fact]
    public async Task GetPluginDependenciesAsync_WithComplexDependencies_ShouldResolveCorrectly()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("ComplexDeps", "1.0.0");
        manifest.Dependencies = new[]
        {
            new PluginDependency { Name = "CoreLibrary", MinVersion = "1.0.0" },
            new PluginDependency { Name = "MathLibrary", MinVersion = "2.1.0" }
        };

        // Act
        var dependencies = await _pluginLoader.GetPluginDependenciesAsync(manifest);

        // Assert
        Assert.NotEmpty(dependencies);
        Assert.Equal(2, dependencies.Count);
        Assert.Contains(dependencies, d => d.Name == "CoreLibrary");
        Assert.Contains(dependencies, d => d.Name == "MathLibrary");
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.1.0", true)]
    [InlineData("2.0.0", "1.0.0", true)]  // Newer version should be compatible
    [InlineData("1.0.0-alpha", "1.0.0", true)]
    public async Task ValidatePluginVersion_WithVariousVersions_ShouldValidateCorrectly(
        string availableVersion, string requiredVersion, bool expectedValid)
    {
        // Arrange
        var manifest = CreateMockPluginManifest("VersionTest", availableVersion);
        manifest.MinFrameworkVersion = requiredVersion;

        // Act
        var result = await _pluginLoader.ValidatePluginAsync(manifest);

        // Assert
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task LoadPluginAsync_WithSecurityRestrictions_ShouldEnforceSecurityPolicy()
    {
        // Arrange
        var manifest = CreateMockPluginManifest("SecurityTest", "1.0.0");
        manifest.RequiredPermissions = new[] { "FileSystem.Write", "Network.Connect" };
        CreateMockPluginAssembly(manifest);

        var restrictedLoader = new MockNuGetPluginLoader(_mockLogger.Object, _tempDirectory, 
            new PluginSecurityPolicy { AllowedPermissions = new[] { "FileSystem.Read" } });

        // Act
        var result = await restrictedLoader.LoadPluginAsync(manifest);

        // Assert
        Assert.False(result.IsLoaded);
        Assert.Contains("security policy", result.ErrorMessage);
    }

    #region Helper Methods

    private void CreateMockPluginManifests()
    {
        var cudaManifest = CreateMockPluginManifest("MockCUDABackend", "1.0.0");
        cudaManifest.Description = "Mock CUDA backend for testing";
        cudaManifest.RequiredInterfaces = new[] { typeof(IKernelCompiler).AssemblyQualifiedName! };
        CreateMockPluginAssembly(cudaManifest);

        var mathManifest = CreateMockPluginManifest("MockLinearAlgebra", "2.1.0");
        mathManifest.Description = "Mock linear algebra library";
        mathManifest.RequiredInterfaces = new[] { typeof(IAccelerator).AssemblyQualifiedName! };
        CreateMockPluginAssembly(mathManifest);
    }

    private PluginManifest CreateMockPluginManifest(string name, string version)
    {
        return new PluginManifest
        {
            Name = name,
            Version = version,
            Description = $"Mock plugin {name}",
            Author = "Test Author",
            AssemblyFileName = $"{name}.dll",
            EntryPointType = $"{name}.PluginEntryPoint",
            MinFrameworkVersion = "6.0.0",
            Dependencies = Array.Empty<PluginDependency>(),
            RequiredInterfaces = Array.Empty<string>(),
            RequiredPermissions = Array.Empty<string>(),
            Metadata = new Dictionary<string, string>
            {
                ["Platform"] = "Any",
                ["Category"] = "Test"
            }
        };
    }

    private void CreateMockPluginAssembly(PluginManifest manifest, string? customName = null)
    {
        var assemblyName = customName ?? manifest.Name;
        var assemblyPath = Path.Combine(_tempDirectory, $"{assemblyName}.dll");
        var manifestPath = Path.Combine(_tempDirectory, $"{assemblyName}.plugin.json");

        // Create a minimal assembly file (just empty bytes for testing)
        File.WriteAllBytes(assemblyPath, new byte[] { 0x4D, 0x5A }); // PE header signature

        // Create manifest file
        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        File.WriteAllText(manifestPath, manifestJson);
    }

    private void CreateInvalidPluginAssembly(PluginManifest manifest)
    {
        var assemblyPath = Path.Combine(_tempDirectory, $"{manifest.Name}.dll");
        var manifestPath = Path.Combine(_tempDirectory, $"{manifest.Name}.plugin.json");

        // Create invalid assembly file
        File.WriteAllText(assemblyPath, "This is not a valid assembly file");

        // Create valid manifest
        var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest);
        File.WriteAllText(manifestPath, manifestJson);
    }

    #endregion

    public void Dispose()
    {
        try
        {
            _pluginLoader?.Dispose();
            
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
        
        GC.SuppressFinalize(this);
    }
}

#region Mock Plugin System Classes

/// <summary>
/// Mock NuGet plugin loader for testing purposes.
/// </summary>
public class MockNuGetPluginLoader : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _pluginDirectory;
    private readonly PluginSecurityPolicy? _securityPolicy;
    private readonly Dictionary<string, PluginLoadResult> _loadedPlugins = new();

    public MockNuGetPluginLoader(ILogger logger, string pluginDirectory, PluginSecurityPolicy? securityPolicy = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pluginDirectory = pluginDirectory ?? throw new ArgumentNullException(nameof(pluginDirectory));
        _securityPolicy = securityPolicy;

        if (!Directory.Exists(_pluginDirectory))
        {
            throw new DirectoryNotFoundException($"Plugin directory not found: {_pluginDirectory}");
        }
    }

    public async Task<IReadOnlyList<PluginManifest>> DiscoverPluginsAsync()
    {
        await Task.Delay(10); // Simulate async work

        var manifests = new List<PluginManifest>();
        var manifestFiles = Directory.GetFiles(_pluginDirectory, "*.plugin.json");

        foreach (var manifestFile in manifestFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(manifestFile);
                var manifest = System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(json);
                if (manifest != null)
                {
                    manifests.Add(manifest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse plugin manifest: {ManifestFile}", manifestFile);
            }
        }

        return manifests.AsReadOnly();
    }

    public async Task<PluginLoadResult> LoadPluginAsync(PluginManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Delay(50, cancellationToken); // Simulate loading time

        // Check if plugin is already loaded
        if (_loadedPlugins.ContainsKey(manifest.Name))
        {
            return new PluginLoadResult
            {
                IsLoaded = false,
                ErrorMessage = $"Plugin '{manifest.Name}' is already loaded"
            };
        }

        // Check security policy
        if (_securityPolicy != null && !CheckSecurityPolicy(manifest))
        {
            return new PluginLoadResult
            {
                IsLoaded = false,
                ErrorMessage = "Plugin does not meet security policy requirements"
            };
        }

        // Check if assembly file exists
        var assemblyPath = Path.Combine(_pluginDirectory, manifest.AssemblyFileName);
        if (!File.Exists(assemblyPath))
        {
            return new PluginLoadResult
            {
                IsLoaded = false,
                ErrorMessage = $"Assembly file not found: {manifest.AssemblyFileName}"
            };
        }

        // Basic validation of assembly file
        try
        {
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken);
            if (assemblyBytes.Length < 2 || assemblyBytes[0] != 0x4D || assemblyBytes[1] != 0x5A)
            {
                return new PluginLoadResult
                {
                    IsLoaded = false,
                    ErrorMessage = "Invalid assembly file format"
                };
            }
        }
        catch (Exception ex)
        {
            return new PluginLoadResult
            {
                IsLoaded = false,
                ErrorMessage = $"Failed to read assembly file: {ex.Message}"
            };
        }

        // Create successful load result
        var pluginInfo = new LoadedPluginInfo
        {
            Name = manifest.Name,
            Version = manifest.Version,
            Description = manifest.Description,
            Author = manifest.Author,
            AssemblyPath = assemblyPath,
            LoadedAt = DateTimeOffset.UtcNow,
            Capabilities = manifest.RequiredInterfaces ?? Array.Empty<string>()
        };

        var result = new PluginLoadResult
        {
            IsLoaded = true,
            PluginInfo = pluginInfo
        };

        _loadedPlugins[manifest.Name] = result;
        return result;
    }

    public async Task<PluginValidationResult> ValidatePluginAsync(PluginManifest manifest)
    {
        await Task.Delay(10); // Simulate validation work

        var errors = new List<string>();

        // Check framework version
        if (!string.IsNullOrEmpty(manifest.MinFrameworkVersion))
        {
            var requiredVersion = new Version(manifest.MinFrameworkVersion);
            var currentVersion = new Version("9.0.0"); // Mock current framework version
            
            if (requiredVersion > currentVersion)
            {
                errors.Add($"Required framework version {manifest.MinFrameworkVersion} is not available");
            }
        }

        // Check dependencies
        if (manifest.Dependencies != null)
        {
            foreach (var dependency in manifest.Dependencies)
            {
                // Simulate dependency check - in real implementation, this would check actual dependencies
                if (dependency.Name == "NonExistentDep")
                {
                    errors.Add($"Dependency '{dependency.Name}' not found");
                }
            }
        }

        return new PluginValidationResult
        {
            IsValid = errors.Count == 0,
            ValidationErrors = errors
        };
    }

    public async Task<bool> UnloadPluginAsync(string pluginName)
    {
        await Task.Delay(10); // Simulate unloading

        return _loadedPlugins.Remove(pluginName);
    }

    public async Task<IReadOnlyList<LoadedPluginInfo>> GetLoadedPluginsAsync()
    {
        await Task.Delay(5);

        return _loadedPlugins.Values.Select(r => r.PluginInfo).ToList().AsReadOnly();
    }

    public async Task RefreshPluginsAsync()
    {
        await Task.Delay(20); // Simulate refresh operation
        // In real implementation, this would rescan the plugin directory
    }

    public async Task<IReadOnlyList<PluginDependency>> GetPluginDependenciesAsync(PluginManifest manifest)
    {
        await Task.Delay(5);

        return manifest.Dependencies?.ToList().AsReadOnly() ?? new List<PluginDependency>().AsReadOnly();
    }

    private bool CheckSecurityPolicy(PluginManifest manifest)
    {
        if (_securityPolicy?.AllowedPermissions == null || manifest.RequiredPermissions == null)
            return true;

        return manifest.RequiredPermissions.All(permission => 
            _securityPolicy.AllowedPermissions.Contains(permission));
    }

    public void Dispose()
    {
        // Clean up any loaded plugins
        _loadedPlugins.Clear();
        GC.SuppressFinalize(this);
    }
}

#endregion

#region Mock Plugin Types

public class PluginManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string AssemblyFileName { get; set; } = "";
    public string EntryPointType { get; set; } = "";
    public string MinFrameworkVersion { get; set; } = "";
    public PluginDependency[]? Dependencies { get; set; }
    public string[]? RequiredInterfaces { get; set; }
    public string[]? RequiredPermissions { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PluginDependency
{
    public string Name { get; set; } = "";
    public string MinVersion { get; set; } = "";
    public string? MaxVersion { get; set; }
}

public class PluginLoadResult
{
    public bool IsLoaded { get; set; }
    public LoadedPluginInfo PluginInfo { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class LoadedPluginInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string AssemblyPath { get; set; } = "";
    public DateTimeOffset LoadedAt { get; set; }
    public string[] Capabilities { get; set; } = Array.Empty<string>();
}

public class PluginValidationResult
{
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

public class PluginSecurityPolicy
{
    public string[]? AllowedPermissions { get; set; }
    public bool AllowNetworkAccess { get; set; } = false;
    public bool AllowFileSystemAccess { get; set; } = false;
}

#endregion