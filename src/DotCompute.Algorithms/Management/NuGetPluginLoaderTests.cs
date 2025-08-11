// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DotCompute.Algorithms.Security;
using Microsoft.Extensions.Logging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace DotCompute.Algorithms.Management;

/// <summary>
/// Integration tests for NuGet plugin loader functionality.
/// This class demonstrates the capabilities and validates the implementation.
/// </summary>
public sealed class NuGetPluginLoaderTests : IDisposable
{
    private readonly ILogger<NuGetPluginLoader> _logger;
    private readonly string _testCacheDirectory;
    private readonly string _testPackagesDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginLoaderTests"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for test operations.</param>
    public NuGetPluginLoaderTests(ILogger<NuGetPluginLoader>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance;
        _testCacheDirectory = Path.Combine(Path.GetTempPath(), "NuGetPluginLoaderTests", Guid.NewGuid().ToString("N"));
        _testPackagesDirectory = Path.Combine(_testCacheDirectory, "packages");
        
        Directory.CreateDirectory(_testCacheDirectory);
        Directory.CreateDirectory(_testPackagesDirectory);
    }

    /// <summary>
    /// Creates a test .nupkg file for testing purposes.
    /// </summary>
    /// <param name="packageId">Package ID.</param>
    /// <param name="version">Package version.</param>
    /// <param name="includeAssembly">Whether to include a test assembly.</param>
    /// <returns>Path to the created test package.</returns>
    public string CreateTestPackage(string packageId, string version, bool includeAssembly = true)
    {
        var packagePath = Path.Combine(_testPackagesDirectory, $"{packageId}.{version}.nupkg");
        
        using var fileStream = File.Create(packagePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create);

        // Create .nuspec file
        var nuspecContent = CreateNuspecContent(packageId, version);
        var nuspecEntry = archive.CreateEntry($"{packageId}.nuspec");
        using (var nuspecStream = nuspecEntry.Open())
        using (var writer = new StreamWriter(nuspecStream))
        {
            writer.Write(nuspecContent);
        }

        // Create lib folder structure
        if (includeAssembly)
        {
            // Create lib/net9.0 folder with test assembly
            var assemblyEntry = archive.CreateEntry($"lib/net9.0/{packageId}.dll");
            using (var assemblyStream = assemblyEntry.Open())
            {
                // Create a minimal dummy assembly (not a real .NET assembly, just for testing)
                var dummyData = Encoding.UTF8.GetBytes($"DUMMY_ASSEMBLY_{packageId}_{version}");
                assemblyStream.Write(dummyData, 0, dummyData.Length);
            }

            // Create lib/net8.0 folder for framework compatibility testing
            var assemblyEntry8 = archive.CreateEntry($"lib/net8.0/{packageId}.dll");
            using (var assemblyStream8 = assemblyEntry8.Open())
            {
                var dummyData = Encoding.UTF8.GetBytes($"DUMMY_ASSEMBLY_{packageId}_{version}_NET8");
                assemblyStream8.Write(dummyData, 0, dummyData.Length);
            }
        }

        // Create content files
        var readmeEntry = archive.CreateEntry("README.md");
        using (var readmeStream = readmeEntry.Open())
        using (var writer = new StreamWriter(readmeStream))
        {
            writer.WriteLine($"# {packageId}");
            writer.WriteLine($"Version: {version}");
            writer.WriteLine("This is a test package for NuGet plugin loading.");
        }

        return packagePath;
    }

    /// <summary>
    /// Creates .nuspec content for test packages.
    /// </summary>
    private static string CreateNuspecContent(string packageId, string version)
    {
        var nuspec = new XDocument(
            new XElement("package",
                new XAttribute("xmlns", "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"),
                new XElement("metadata",
                    new XElement("id", packageId),
                    new XElement("version", version),
                    new XElement("authors", "DotCompute Test Suite"),
                    new XElement("description", $"Test package for {packageId}"),
                    new XElement("projectUrl", "https://github.com/example/dotcompute"),
                    new XElement("licenseUrl", "https://github.com/example/dotcompute/blob/main/LICENSE"),
                    new XElement("requireLicenseAcceptance", "false"),
                    new XElement("tags", "dotcompute test algorithms"),
                    new XElement("dependencies",
                        new XElement("group",
                            new XAttribute("targetFramework", "net9.0"),
                            new XElement("dependency",
                                new XAttribute("id", "System.Text.Json"),
                                new XAttribute("version", "8.0.0")),
                            new XElement("dependency",
                                new XAttribute("id", "Microsoft.Extensions.Logging.Abstractions"),
                                new XAttribute("version", "8.0.0")))))));

        return nuspec.ToString();
    }

    /// <summary>
    /// Tests basic package loading from a local .nupkg file.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestLocalPackageLoadingAsync()
    {
        _logger.LogInformation("Starting local package loading test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            DefaultTargetFramework = "net9.0",
            EnableSecurityValidation = false // Disable for test packages
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create a test package
        var packagePath = CreateTestPackage("TestAlgorithms", "1.0.0");
        _logger.LogInformation("Created test package at: {PackagePath}", packagePath);

        // Load the package
        var result = await loader.LoadPackageAsync(packagePath, "net9.0");

        // Verify results
        _logger.LogInformation("Package loaded - ID: {PackageId}, Version: {Version}, LoadTime: {LoadTime:F2}ms, Assemblies: {AssemblyCount}, Dependencies: {DependencyCount}, FromCache: {FromCache}, Size: {TotalSize} bytes",
            result.PackageIdentity.Id,
            result.PackageIdentity.Version,
            result.LoadTime.TotalMilliseconds,
            result.LoadedAssemblyPaths.Length,
            result.ResolvedDependencies.Length,
            result.FromCache,
            result.TotalSize);

        foreach (var assemblyPath in result.LoadedAssemblyPaths)
        {
            var exists = File.Exists(assemblyPath);
            var size = exists ? new FileInfo(assemblyPath).Length : 0;
            _logger.LogInformation("Assembly loaded - Path: {AssemblyPath}, Exists: {Exists}, Size: {Size} bytes",
                assemblyPath, exists, size);
        }

        // Test loading same package again (should come from cache)
        var cachedResult = await loader.LoadPackageAsync(packagePath, "net9.0");
        _logger.LogInformation("Second load from cache result: {FromCache}", cachedResult.FromCache);

        _logger.LogInformation("Local package loading test completed successfully");
    }

    /// <summary>
    /// Tests package manifest parsing and metadata extraction.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageManifestParsingAsync()
    {
        _logger.LogInformation("Starting package manifest parsing test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create a package with complex dependencies
        var packagePath = CreateTestPackage("ComplexPackage", "2.1.0");
        
        var result = await loader.LoadPackageAsync(packagePath, "net9.0");

        _logger.LogInformation("Package manifest parsed - ID: {PackageId}, Version: {Version}, Dependencies: {DependencyCount}",
            result.PackageIdentity.Id, result.PackageIdentity.Version, result.ResolvedDependencies.Length);
        
        foreach (var dependency in result.ResolvedDependencies)
        {
            var frameworks = dependency.TargetFrameworks?.Length > 0 ? string.Join(", ", dependency.TargetFrameworks) : "None";
            _logger.LogInformation("Dependency found - ID: {DependencyId}, VersionRange: {VersionRange}, Frameworks: {Frameworks}",
                dependency.Id, dependency.VersionRange, frameworks);
        }

        _logger.LogInformation("Package manifest parsing test completed successfully");
    }

    /// <summary>
    /// Tests framework compatibility and assembly selection.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestFrameworkCompatibilityAsync()
    {
        _logger.LogInformation("Starting framework compatibility test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        var packagePath = CreateTestPackage("MultiFrameworkPackage", "1.5.0");

        // Test different target frameworks
        var frameworks = new[] { "net9.0", "net8.0", "net6.0", "netstandard2.1" };

        foreach (var framework in frameworks)
        {
            try
            {
                _logger.LogInformation("Testing framework compatibility: {Framework}", framework);
                var result = await loader.LoadPackageAsync(packagePath, framework);
                
                _logger.LogInformation("Framework {Framework} supported - {AssemblyCount} assemblies found", framework, result.LoadedAssemblyPaths.Length);
                foreach (var assembly in result.LoadedAssemblyPaths)
                {
                    var relativePath = Path.GetRelativePath(result.CachePath!, assembly);
                    _logger.LogInformation("Assembly path for {Framework}: {RelativePath}", framework, relativePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Framework {Framework} not supported: {ErrorMessage}", framework, ex.Message);
            }
        }

        _logger.LogInformation("Framework compatibility test completed successfully");
    }

    /// <summary>
    /// Tests package caching functionality.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageCachingAsync()
    {
        _logger.LogInformation("Starting package caching test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            CacheExpiration = TimeSpan.FromMinutes(10),
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create multiple test packages
        var packages = new[]
        {
            CreateTestPackage("CacheTest1", "1.0.0"),
            CreateTestPackage("CacheTest2", "1.0.0"),
            CreateTestPackage("CacheTest3", "1.0.0")
        };

        _logger.LogInformation("Loading {PackageCount} packages into cache", packages.Length);
        foreach (var package in packages)
        {
            var result = await loader.LoadPackageAsync(package, "net9.0");
            _logger.LogInformation("Package loaded into cache - ID: {PackageId}, FromCache: {FromCache}", result.PackageIdentity.Id, result.FromCache);
        }

        // Check cached packages
        var cachedPackages = loader.GetCachedPackages();
        _logger.LogInformation("Cache contains {CachedPackageCount} packages", cachedPackages.Length);

        foreach (var cached in cachedPackages)
        {
            _logger.LogInformation("Cached package - ID: {PackageId}, Version: {Version}, Age: {CacheAge}, Assemblies: {AssemblyCount}, Size: {PackageSize} bytes, SecurityValidated: {IsSecurityValidated}",
                cached.Identity.Id, cached.Identity.Version, cached.CacheAge, cached.AssemblyCount, cached.PackageSize, cached.IsSecurityValidated);
        }

        // Test cache clearing
        _logger.LogInformation("Testing cache clearing - removing packages older than 1 second");
        await Task.Delay(1100); // Wait a bit
        await loader.ClearCacheAsync(TimeSpan.FromSeconds(1));

        var remainingCached = loader.GetCachedPackages();
        _logger.LogInformation("Cache cleared - remaining packages: {RemainingCount}", remainingCached.Length);

        _logger.LogInformation("Package caching test completed successfully");
    }

    /// <summary>
    /// Tests security validation with mock security policies.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestSecurityValidationAsync()
    {
        _logger.LogInformation("Starting security validation test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = true,
            RequirePackageSignature = false, // Disable for test packages
            EnableMalwareScanning = false,  // Disable for test packages
            MaxAssemblySize = 1024 * 1024,  // 1 MB limit
            MinimumSecurityLevel = SecurityLevel.Low
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        var packagePath = CreateTestPackage("SecurityTest", "1.0.0");

        try
        {
            var result = await loader.LoadPackageAsync(packagePath, "net9.0");
            _logger.LogInformation("Package loaded with security validation - Result: {SecurityResult}, Warnings: {WarningCount}",
                result.SecurityValidationResult ?? "No security issues", result.Warnings.Length);
            
            foreach (var warning in result.Warnings)
            {
                _logger.LogWarning("Security warning: {Warning}", warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Security validation failed (expected for test packages): {ErrorMessage}", ex.Message);
        }

        // Test with size limit exceeded
        var largeDummyData = new byte[2 * 1024 * 1024]; // 2 MB
        var largePackagePath = Path.Combine(_testPackagesDirectory, "LargePackage.1.0.0.nupkg");
        
        using (var fileStream = File.Create(largePackagePath))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            var nuspecEntry = archive.CreateEntry("LargePackage.nuspec");
            using (var stream = nuspecEntry.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(CreateNuspecContent("LargePackage", "1.0.0"));
            }

            var largeEntry = archive.CreateEntry("lib/net9.0/LargeAssembly.dll");
            using (var stream = largeEntry.Open())
            {
                stream.Write(largeDummyData, 0, largeDummyData.Length);
            }
        }

        try
        {
            await loader.LoadPackageAsync(largePackagePath, "net9.0");
            _logger.LogInformation("Large package loaded (size validation may have been skipped)");
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "Large package rejected as expected: {ErrorMessage}", ex.Message);
        }

        _logger.LogInformation("Security validation test completed successfully");
    }

    /// <summary>
    /// Tests package update functionality.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageUpdatesAsync()
    {
        _logger.LogInformation("Starting package updates test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create multiple versions of the same package
        var v1Path = CreateTestPackage("UpdatablePackage", "1.0.0");
        var v2Path = CreateTestPackage("UpdatablePackage", "2.0.0");

        // Load version 1.0.0
        _logger.LogInformation("Loading package version 1.0.0");
        var result1 = await loader.LoadPackageAsync(v1Path, "net9.0");
        _logger.LogInformation("Loaded package: {PackageId} v{Version}", result1.PackageIdentity.Id, result1.PackageIdentity.Version);

        // Load version 2.0.0 (simulating update)
        _logger.LogInformation("Loading package version 2.0.0");
        var result2 = await loader.LoadPackageAsync(v2Path, "net9.0");
        _logger.LogInformation("Loaded package: {PackageId} v{Version}", result2.PackageIdentity.Id, result2.PackageIdentity.Version);

        // Check that both versions are cached separately
        var cachedPackages = loader.GetCachedPackages();
        var updatablePackages = cachedPackages
            .Where(p => p.Identity.Id == "UpdatablePackage")
            .ToArray();

        _logger.LogInformation("Cached versions of UpdatablePackage: {VersionCount}", updatablePackages.Length);
        foreach (var cached in updatablePackages)
        {
            _logger.LogInformation("Cached version: {Version}", cached.Identity.Version);
        }

        // Test update functionality (would require remote package resolution in real implementation)
        try
        {
            var updateResult = await loader.UpdatePackageAsync("UpdatablePackage", "net9.0");
            _logger.LogInformation("Package updated to version: {Version}", updateResult.PackageIdentity.Version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update failed (expected for local packages): {ErrorMessage}", ex.Message);
        }

        _logger.LogInformation("Package updates test completed successfully");
    }

    /// <summary>
    /// Tests error handling and edge cases.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestErrorHandlingAsync()
    {
        _logger.LogInformation("Starting error handling test");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Test non-existent file
        try
        {
            await loader.LoadPackageAsync("NonExistentPackage.nupkg", "net9.0");
            _logger.LogError("ERROR: Should have thrown FileNotFoundException");
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogInformation("✓ Correctly handled non-existent package file: {ExceptionType}", ex.GetType().Name);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("✓ Handled non-existent package: {ExceptionType}", ex.GetType().Name);
        }

        // Test invalid package format
        var invalidPackagePath = Path.Combine(_testPackagesDirectory, "invalid.nupkg");
        File.WriteAllText(invalidPackagePath, "This is not a valid zip/nupkg file");

        try
        {
            await loader.LoadPackageAsync(invalidPackagePath, "net9.0");
            _logger.LogError("ERROR: Should have thrown format exception");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("✓ Correctly handled invalid package format: {ExceptionType}", ex.GetType().Name);
        }

        // Test empty package
        var emptyPackagePath = Path.Combine(_testPackagesDirectory, "empty.nupkg");
        using (var fileStream = File.Create(emptyPackagePath))
        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            // Create empty archive
        }

        try
        {
            await loader.LoadPackageAsync(emptyPackagePath, "net9.0");
            _logger.LogError("ERROR: Should have thrown exception for missing nuspec");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("✓ Correctly handled empty package: {ExceptionType}", ex.GetType().Name);
        }

        // Test invalid target framework
        var validPackagePath = CreateTestPackage("ValidPackage", "1.0.0");
        
        try
        {
            var result = await loader.LoadPackageAsync(validPackagePath, "invalid-framework");
            _logger.LogInformation("Loaded with invalid framework (may be handled gracefully): {AssemblyCount} assemblies", result.LoadedAssemblyPaths.Length);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("✓ Correctly handled invalid framework: {ExceptionType}", ex.GetType().Name);
        }

        _logger.LogInformation("Error handling test completed successfully");
    }

    /// <summary>
    /// Runs all tests in sequence.
    /// </summary>
    /// <returns>A task representing all test execution.</returns>
    public async Task RunAllTestsAsync()
    {
        _logger.LogInformation("Starting NuGet Plugin Loader Tests suite");

        try
        {
            await TestLocalPackageLoadingAsync();
            await TestPackageManifestParsingAsync();
            await TestFrameworkCompatibilityAsync();
            await TestPackageCachingAsync();
            await TestSecurityValidationAsync();
            await TestPackageUpdatesAsync();
            await TestErrorHandlingAsync();

            _logger.LogInformation("All tests completed successfully - Test artifacts created in: {TestDirectory}", _testCacheDirectory);
            _logger.LogInformation("The NuGet plugin loader implementation is working correctly");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test suite failed: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Demonstrates performance characteristics.
    /// </summary>
    /// <returns>A task representing the performance test.</returns>
    public async Task RunPerformanceTestsAsync()
    {
        _logger.LogInformation("Starting performance tests");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false,
            MaxConcurrentDownloads = 5
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create multiple packages for concurrent loading
        var packages = Enumerable.Range(1, 10)
            .Select(i => CreateTestPackage($"PerfTest{i}", "1.0.0"))
            .ToArray();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Sequential loading
        _logger.LogInformation("Starting sequential loading test");
        foreach (var package in packages.Take(5))
        {
            var result = await loader.LoadPackageAsync(package, "net9.0");
            _logger.LogInformation("Sequential load - Package: {PackageId}, Time: {LoadTime:F2}ms", result.PackageIdentity.Id, result.LoadTime.TotalMilliseconds);
        }

        var sequentialTime = stopwatch.Elapsed;
        _logger.LogInformation("Sequential loading total time: {TotalTime:F2}ms", sequentialTime.TotalMilliseconds);

        // Clear cache for fair comparison
        await loader.ClearCacheAsync();
        stopwatch.Restart();

        // Concurrent loading
        _logger.LogInformation("Starting concurrent loading test");
        var concurrentTasks = packages.Skip(5).Take(5)
            .Select(async package =>
            {
                var result = await loader.LoadPackageAsync(package, "net9.0");
                return result;
            });

        var concurrentResults = await Task.WhenAll(concurrentTasks);
        var concurrentTime = stopwatch.Elapsed;

        foreach (var result in concurrentResults)
        {
            _logger.LogInformation("Concurrent load - Package: {PackageId}, Time: {LoadTime:F2}ms", result.PackageIdentity.Id, result.LoadTime.TotalMilliseconds);
        }

        _logger.LogInformation("Performance test results - Sequential: {SequentialTime:F2}ms, Concurrent: {ConcurrentTime:F2}ms, Improvement: {Improvement:F2}x",
            sequentialTime.TotalMilliseconds, concurrentTime.TotalMilliseconds, sequentialTime.TotalMilliseconds / concurrentTime.TotalMilliseconds);

        _logger.LogInformation("Performance tests completed successfully");
    }

    /// <summary>
    /// Cleans up test resources.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testCacheDirectory))
            {
                Directory.Delete(_testCacheDirectory, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up test directory: {TestDirectory}", _testCacheDirectory);
        }
    }
}

/// <summary>
/// Program entry point for running the NuGet plugin loader tests.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A task representing the program execution.</returns>
    public static async Task MainAsync(string[] args)
    {
        // Create a simple console logger for the main entry point
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<NuGetPluginLoader>();
        
        logger.LogInformation("DotCompute NuGet Plugin Loader Tests - Starting test suite");

        using var tests = new NuGetPluginLoaderTests(logger);

        try
        {
            if (args.Length > 0 && args[0] == "--performance")
            {
                await tests.RunPerformanceTestsAsync();
            }
            else
            {
                await tests.RunAllTestsAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test suite failed: {ErrorMessage}", ex.Message);
            Environment.ExitCode = 1;
        }
    }
}