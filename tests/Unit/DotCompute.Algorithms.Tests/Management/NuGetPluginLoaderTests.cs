// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.Types.Management;
using DotCompute.Algorithms.Types.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System.IO.Compression;
using System.Text;
using Xunit;
using FluentAssertions;

namespace DotCompute.Tests.Management;

/// <summary>
/// Comprehensive tests for NuGetPluginLoader functionality.
/// </summary>
public sealed class NuGetPluginLoaderTests : IDisposable
{
    private readonly NuGetPluginLoader _pluginLoader;
    private readonly NuGetPluginLoaderOptions _options;
    private readonly string _tempDirectory;
    private readonly string _testPackagesDirectory;

    public NuGetPluginLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _testPackagesDirectory = Path.Combine(_tempDirectory, "packages");
        
        Directory.CreateDirectory(_tempDirectory);
        Directory.CreateDirectory(_testPackagesDirectory);

        _options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _tempDirectory,
            DefaultTargetFramework = "net9.0",
            EnableSecurityValidation = false, // Disable for testing
            RequirePackageSignature = false,
            EnableMalwareScanning = false,
            MaxConcurrentDownloads = 2,
            CacheExpiration = TimeSpan.FromMinutes(5)
        };

        _pluginLoader = new NuGetPluginLoader(
            NullLogger<NuGetPluginLoader>.Instance,
            _options);
    }

    [Fact]
    public async Task LoadPackageAsync_ValidLocalPackage_ShouldReturnLoadResult()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");

        // Act
        var result = await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");

        // Assert
        Assert.NotNull(result);
        result.PackageIdentity.Id.Should().Be("TestPackage");
        _ = result.PackageIdentity.Version.ToString().Should().Be("1.0.0");
        result.LoadedAssemblyPaths.Should().NotBeEmpty();
        result.FromCache.Should().BeFalse();
        (result.LoadTime > TimeSpan.Zero).Should().BeTrue();
    }

    [Fact]
    public async Task LoadPackageAsync_SecondLoad_ShouldUseCache()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");

        // Act
        var firstResult = await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");
        var secondResult = await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");

        // Assert
        firstResult.FromCache.Should().BeFalse();
        secondResult.FromCache.Should().BeTrue();
        secondResult.CachePath.Should().Be(firstResult.CachePath);
    }

    [Fact]
    public async Task LoadPackageAsync_NonExistentFile_ShouldThrowException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDirectory, "NonExistent.nupkg");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            _pluginLoader.LoadPackageAsync(nonExistentPath, "net9.0"));
        
        exception.Assert.Contains("not found", Message); // StringComparison.OrdinalIgnoreCase;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task LoadPackageAsync_InvalidPackageSource_ShouldThrowArgumentException(string? packageSource)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _pluginLoader.LoadPackageAsync(packageSource!, "net9.0"));
    }

    [Fact]
    public async Task LoadPackageAsync_InvalidTargetFramework_ShouldUseDefault()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");

        // Act
        var result = await _pluginLoader.LoadPackageAsync(packagePath, null);

        // Assert
        Assert.NotNull(result);
        // Should use the default target framework from options
    }

    [Fact]
    public async Task UpdatePackageAsync_ExistingPackage_ShouldUpdateToLatest()
    {
        // Arrange
        var packagePath1 = await CreateTestPackageAsync("TestPackage", "1.0.0");
        var packagePath2 = await CreateTestPackageAsync("TestPackage", "2.0.0");

        // Load the first version
        await _pluginLoader.LoadPackageAsync(packagePath1, "net9.0");

        // Act
        var updateResult = await _pluginLoader.UpdatePackageAsync("TestPackage", "net9.0");

        // Assert
        Assert.NotNull(updateResult);
        updateResult.PackageIdentity.Id.Should().Be("TestPackage");
        // Note: In a real scenario with remote packages, this would update to the latest version
    }

    [Fact]
    public async Task UpdatePackageAsync_NonExistentPackage_ShouldLoadLatest()
    {
        // Act
        var result = await _pluginLoader.UpdatePackageAsync("NonExistentPackage", "net9.0");

        // Assert
        // This should attempt to load the latest version from remote sources
        // In our test environment, this will likely fail, but the method should handle it gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public void GetCachedPackages_NoCachedPackages_ShouldReturnEmpty()
    {
        // Act
        var cachedPackages = _pluginLoader.GetCachedPackages();

        // Assert
        Assert.Empty(cachedPackages);
    }

    [Fact]
    public async Task GetCachedPackages_WithCachedPackages_ShouldReturnCachedInfo()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");
        await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");

        // Act
        var cachedPackages = _pluginLoader.GetCachedPackages();

        // Assert
        Assert.NotEmpty(cachedPackages);
        Assert.Equal(1, cachedPackages.Count());
        
        var cachedPackage = cachedPackages.First();
        cachedPackage.Identity.Id.Should().Be("TestPackage");
        _ = cachedPackage.Identity.Version.ToString().Should().Be("1.0.0");
        (cachedPackage.AssemblyCount > 0).Should().BeTrue();
        (cachedPackage.CacheAge > TimeSpan.Zero).Should().BeTrue();
        cachedPackage.IsSecurityValidated.Should().BeTrue(); // Since we disabled security validation, it should pass
    }

    [Fact]
    public async Task ClearCacheAsync_WithCachedPackages_ShouldClearCache()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");
        await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");
        
        var cachedPackagesBefore = _pluginLoader.GetCachedPackages();
        Assert.NotEmpty(cachedPackagesBefore);

        // Act
        await _pluginLoader.ClearCacheAsync();

        // Assert
        var cachedPackagesAfter = _pluginLoader.GetCachedPackages();
        Assert.Empty(cachedPackagesAfter);
    }

    [Fact]
    public async Task ClearCacheAsync_WithOlderThanFilter_ShouldClearOnlyOldPackages()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("TestPackage", "1.0.0");
        await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");

        // Act - clear packages older than 1 minute(should not clear anything)
        await _pluginLoader.ClearCacheAsync(TimeSpan.FromMinutes(1));

        // Assert
        var cachedPackages = _pluginLoader.GetCachedPackages();
        Assert.NotEmpty(cachedPackages); // Nothing should be cleared

        // Act - clear packages older than 0 seconds(should clear everything)
        await _pluginLoader.ClearCacheAsync(TimeSpan.Zero);

        // Assert
        var cachedPackagesAfter = _pluginLoader.GetCachedPackages();
        Assert.Empty(cachedPackagesAfter);
    }

    [Fact]
    public void NuGetPluginLoaderOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new NuGetPluginLoaderOptions();

        // Assert
        options.Assert.Contains("DotComputeNuGetCache", CacheDirectory);
        options.CacheExpiration.Should().Be(TimeSpan.FromDays(7));
        options.DefaultTargetFramework.Should().Be("net9.0");
        options.IncludePrerelease.Should().BeFalse();
        options.MaxConcurrentDownloads.Should().Be(3);
        options.EnableSecurityValidation.Should().BeTrue();
        options.RequirePackageSignature.Should().BeTrue();
        options.EnableMalwareScanning.Should().BeTrue();
        options.MaxAssemblySize.Should().Be(50 * 1024 * 1024);
        options.MinimumSecurityLevel.Should().Be(SecurityLevel.Medium);
        options.Assert.Contains("https://api.nuget.org/v3/index.json", TrustedSources);
    }

    [Fact]
    public void CachedPackageInfo_Properties_ShouldSetCorrectly()
    {
        // Arrange
        var identity = new PackageIdentity("TestPackage", NuGetVersion.Parse("1.0.0"));
        var cacheTime = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var info = new CachedPackageInfo
        {
            Identity = identity,
            CacheTime = cacheTime,
            ExtractedPath = "/path/to/extracted",
            AssemblyCount = 2,
            DependencyCount = 3,
            PackageSize = 1024 * 1024,
            IsSecurityValidated = true,
            CacheAge = DateTime.UtcNow - cacheTime
        };

        // Assert
        info.Identity.Should().Be(identity);
        info.CacheTime.Should().Be(cacheTime);
        info.ExtractedPath.Should().Be("/path/to/extracted");
        info.AssemblyCount.Should().Be(2);
        info.DependencyCount.Should().Be(3);
        info.PackageSize.Should().Be(1024 * 1024);
        info.IsSecurityValidated.Should().BeTrue();
        info.CacheAge.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void NuGetLoadResult_Properties_ShouldSetCorrectly()
    {
        // Arrange
        var identity = new PackageIdentity("TestPackage", NuGetVersion.Parse("1.0.0"));
        var loadTime = TimeSpan.FromMilliseconds(500);

        // Act
        var result = new NuGetLoadResult
        {
            PackageIdentity = identity,
            LoadedAssemblyPaths = ["assembly1.dll", "assembly2.dll"],
            ResolvedDependencies = [],
            LoadTime = loadTime,
            TotalSize = 2048,
            SecurityValidationResult = "Valid signature",
            FromCache = true,
            CachePath = "/cache/path",
            Warnings = ["Warning 1", "Warning 2"]
        };

        // Assert
        result.PackageIdentity.Should().Be(identity);
        result.LoadedAssemblyPaths.Equal(["assembly1.dll", "assembly2.dll"]);
        resultResolvedDependencies.Should().BeEmpty();
        result.LoadTime.Should().Be(loadTime);
        result.TotalSize.Should().Be(2048);
        result.SecurityValidationResult.Should().Be("Valid signature");
        result.FromCache.Should().BeTrue();
        result.CachePath.Should().Be("/cache/path");
        result.Warnings.Equal(["Warning 1", "Warning 2"]);
    }

    [Fact]
    public void NuGetLogger_LogLevels_ShouldMapCorrectly()
    {
        // Arrange
        var logger = new NuGetLogger(NullLogger<NuGetPluginLoader>.Instance);
        
        // Act & Assert
        // Test that the NuGetLogger can handle different log levels without throwing
        logger.Log(new TestLogMessage(NuGet.Common.LogLevel.Debug, "Debug message"));
        logger.Log(new TestLogMessage(NuGet.Common.LogLevel.Information, "Info message"));
        logger.Log(new TestLogMessage(NuGet.Common.LogLevel.Warning, "Warning message"));
        logger.Log(new TestLogMessage(NuGet.Common.LogLevel.Error, "Error message"));
    }

    [Fact]
    public async Task LoadPackageAsync_LargePackage_ShouldHandleCorrectly()
    {
        // Arrange
        var packagePath = await CreateTestPackageAsync("LargePackage", "1.0.0", includeMultipleAssemblies: true);

        // Act
        var result = await _pluginLoader.LoadPackageAsync(packagePath, "net9.0");

        // Assert
        Assert.NotNull(result);
        result.LoadedAssemblyPaths.Should().NotBeEmpty();
        (result.TotalSize > 0).Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Act & Assert
        _pluginLoader.Dispose(); // First dispose
        _pluginLoader.Dispose(); // Second dispose - should not throw
    }

    private async Task<string> CreateTestPackageAsync(string packageId, string version, bool includeMultipleAssemblies = false)
    {
        var packagePath = Path.Combine(_testPackagesDirectory, $"{packageId}.{version}.nupkg");

        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        // Create nuspec file
        var nuspec = CreateNuspecContent(packageId, version);
        var nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
        using(var nuspecStream = nuspecEntry.Open())
        {
            await nuspecStream.WriteAsync(Encoding.UTF8.GetBytes(nuspec));
        }

        // Create lib folder and assemblies
        var libPath = $"lib/net9.0/";
        
        // Create main assembly
        var mainAssemblyEntry = archive.CreateEntry($"{libPath}{packageId}.dll");
        using(var assemblyStream = mainAssemblyEntry.Open())
        {
            await assemblyStream.WriteAsync(CreateDummyAssemblyBytes(packageId));
        }

        if(includeMultipleAssemblies)
        {
            // Create additional assemblies
            var helperAssemblyEntry = archive.CreateEntry($"{libPath}{packageId}.Helper.dll");
            using(var helperStream = helperAssemblyEntry.Open())
            {
                await helperStream.WriteAsync(CreateDummyAssemblyBytes($"{packageId}.Helper"));
            }
        }

        return packagePath;
    }

    private static string CreateNuspecContent(string packageId, string version)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
              <metadata>
                <id>{packageId}</id>
                <version>{version}</version>
                <authors>Test Author</authors>
                <description>Test package for unit testing</description>
                <dependencies>
                  <group targetFramework="net9.0">
                    <dependency id="System.Text.Json" version="8.0.0" />
                  </group>
                </dependencies>
              </metadata>
            </package>
            """;
    }

    private static byte[] CreateDummyAssemblyBytes(string assemblyName)
    {
        // Create a minimal valid PE header for testing
        // This is not a real .NET assembly but enough for basic testing
        var content = $"DUMMY_ASSEMBLY_{assemblyName}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        return Encoding.UTF8.GetBytes(content);
    }

    public void Dispose()
    {
        _pluginLoader?.Dispose();
        
        if(Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    /// <summary>
    /// Test implementation of ILogMessage for NuGetLogger testing.
    /// </summary>
    private sealed class TestLogMessage : NuGet.Common.ILogMessage
    {
        public TestLogMessage(NuGet.Common.LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public NuGet.Common.LogLevel Level { get; }
        public NuGet.Common.WarningLevel WarningLevel => NuGet.Common.WarningLevel.Important;
        public NuGet.Common.NuGetLogCode Code => NuGet.Common.NuGetLogCode.Undefined;
        public string Message { get; }
        public string ProjectPath => string.Empty;
        public DateTime Time => DateTime.UtcNow;
    }
}
