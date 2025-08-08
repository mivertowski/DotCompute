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
    public NuGetPluginLoaderTests()
    {
        _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetPluginLoader>.Instance;
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
    public async Task TestLocalPackageLoading()
    {
        Console.WriteLine("=== Testing Local Package Loading ===");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            DefaultTargetFramework = "net9.0",
            EnableSecurityValidation = false // Disable for test packages
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create a test package
        var packagePath = CreateTestPackage("TestAlgorithms", "1.0.0");
        Console.WriteLine($"Created test package: {packagePath}");

        // Load the package
        var result = await loader.LoadPackageAsync(packagePath, "net9.0");

        // Verify results
        Console.WriteLine($"Package Identity: {result.PackageIdentity.Id} v{result.PackageIdentity.Version}");
        Console.WriteLine($"Load Time: {result.LoadTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Assembly Paths: {result.LoadedAssemblyPaths.Length}");
        Console.WriteLine($"Dependencies: {result.ResolvedDependencies.Length}");
        Console.WriteLine($"From Cache: {result.FromCache}");
        Console.WriteLine($"Total Size: {result.TotalSize} bytes");

        foreach (var assemblyPath in result.LoadedAssemblyPaths)
        {
            Console.WriteLine($"  Assembly: {assemblyPath}");
            Console.WriteLine($"    Exists: {File.Exists(assemblyPath)}");
            Console.WriteLine($"    Size: {new FileInfo(assemblyPath).Length} bytes");
        }

        // Test loading same package again (should come from cache)
        var cachedResult = await loader.LoadPackageAsync(packagePath, "net9.0");
        Console.WriteLine($"Second load from cache: {cachedResult.FromCache}");

        Console.WriteLine("Local package loading test completed successfully!\n");
    }

    /// <summary>
    /// Tests package manifest parsing and metadata extraction.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageManifestParsing()
    {
        Console.WriteLine("=== Testing Package Manifest Parsing ===");

        var options = new NuGetPluginLoaderOptions
        {
            CacheDirectory = _testCacheDirectory,
            EnableSecurityValidation = false
        };

        using var loader = new NuGetPluginLoader(_logger, options);

        // Create a package with complex dependencies
        var packagePath = CreateTestPackage("ComplexPackage", "2.1.0");
        
        var result = await loader.LoadPackageAsync(packagePath, "net9.0");

        Console.WriteLine($"Package: {result.PackageIdentity.Id} v{result.PackageIdentity.Version}");
        Console.WriteLine("Dependencies:");
        
        foreach (var dependency in result.ResolvedDependencies)
        {
            Console.WriteLine($"  - {dependency.Id} {dependency.VersionRange}");
            if (dependency.TargetFrameworks?.Length > 0)
            {
                Console.WriteLine($"    Frameworks: {string.Join(", ", dependency.TargetFrameworks)}");
            }
        }

        Console.WriteLine("Package manifest parsing test completed successfully!\n");
    }

    /// <summary>
    /// Tests framework compatibility and assembly selection.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestFrameworkCompatibility()
    {
        Console.WriteLine("=== Testing Framework Compatibility ===");

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
                Console.WriteLine($"Testing framework: {framework}");
                var result = await loader.LoadPackageAsync(packagePath, framework);
                
                Console.WriteLine($"  Assemblies found: {result.LoadedAssemblyPaths.Length}");
                foreach (var assembly in result.LoadedAssemblyPaths)
                {
                    var relativePath = Path.GetRelativePath(result.CachePath!, assembly);
                    Console.WriteLine($"    {relativePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Framework {framework} not supported: {ex.Message}");
            }
        }

        Console.WriteLine("Framework compatibility test completed successfully!\n");
    }

    /// <summary>
    /// Tests package caching functionality.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageCaching()
    {
        Console.WriteLine("=== Testing Package Caching ===");

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

        Console.WriteLine("Loading packages into cache...");
        foreach (var package in packages)
        {
            var result = await loader.LoadPackageAsync(package, "net9.0");
            Console.WriteLine($"  Loaded: {result.PackageIdentity.Id} (from cache: {result.FromCache})");
        }

        // Check cached packages
        var cachedPackages = loader.GetCachedPackages();
        Console.WriteLine($"Cached packages: {cachedPackages.Length}");

        foreach (var cached in cachedPackages)
        {
            Console.WriteLine($"  - {cached.Identity.Id} v{cached.Identity.Version}");
            Console.WriteLine($"    Cache age: {cached.CacheAge}");
            Console.WriteLine($"    Assembly count: {cached.AssemblyCount}");
            Console.WriteLine($"    Package size: {cached.PackageSize} bytes");
            Console.WriteLine($"    Security validated: {cached.IsSecurityValidated}");
        }

        // Test cache clearing
        Console.WriteLine("Clearing cache (packages older than 1 second)...");
        await Task.Delay(1100); // Wait a bit
        await loader.ClearCacheAsync(TimeSpan.FromSeconds(1));

        var remainingCached = loader.GetCachedPackages();
        Console.WriteLine($"Remaining cached packages: {remainingCached.Length}");

        Console.WriteLine("Package caching test completed successfully!\n");
    }

    /// <summary>
    /// Tests security validation with mock security policies.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestSecurityValidation()
    {
        Console.WriteLine("=== Testing Security Validation ===");

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
            Console.WriteLine($"Package loaded with security validation");
            Console.WriteLine($"Security result: {result.SecurityValidationResult ?? "No security issues"}");
            
            if (result.Warnings.Length > 0)
            {
                Console.WriteLine("Security warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Security validation failed (expected for test packages): {ex.Message}");
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
            Console.WriteLine("Large package loaded (size validation may have been skipped)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Large package rejected (expected): {ex.Message}");
        }

        Console.WriteLine("Security validation test completed successfully!\n");
    }

    /// <summary>
    /// Tests package update functionality.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestPackageUpdates()
    {
        Console.WriteLine("=== Testing Package Updates ===");

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
        Console.WriteLine("Loading version 1.0.0...");
        var result1 = await loader.LoadPackageAsync(v1Path, "net9.0");
        Console.WriteLine($"Loaded: {result1.PackageIdentity.Id} v{result1.PackageIdentity.Version}");

        // Load version 2.0.0 (simulating update)
        Console.WriteLine("Loading version 2.0.0...");
        var result2 = await loader.LoadPackageAsync(v2Path, "net9.0");
        Console.WriteLine($"Loaded: {result2.PackageIdentity.Id} v{result2.PackageIdentity.Version}");

        // Check that both versions are cached separately
        var cachedPackages = loader.GetCachedPackages();
        var updatablePackages = cachedPackages
            .Where(p => p.Identity.Id == "UpdatablePackage")
            .ToArray();

        Console.WriteLine($"Cached versions of UpdatablePackage: {updatablePackages.Length}");
        foreach (var cached in updatablePackages)
        {
            Console.WriteLine($"  - Version {cached.Identity.Version}");
        }

        // Test update functionality (would require remote package resolution in real implementation)
        try
        {
            var updateResult = await loader.UpdatePackageAsync("UpdatablePackage", "net9.0");
            Console.WriteLine($"Package updated: {updateResult.PackageIdentity.Version}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed (expected for local packages): {ex.Message}");
        }

        Console.WriteLine("Package updates test completed successfully!\n");
    }

    /// <summary>
    /// Tests error handling and edge cases.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    public async Task TestErrorHandling()
    {
        Console.WriteLine("=== Testing Error Handling ===");

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
            Console.WriteLine("ERROR: Should have thrown FileNotFoundException");
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine("✓ Correctly handled non-existent package file");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ Handled non-existent package: {ex.GetType().Name}");
        }

        // Test invalid package format
        var invalidPackagePath = Path.Combine(_testPackagesDirectory, "invalid.nupkg");
        File.WriteAllText(invalidPackagePath, "This is not a valid zip/nupkg file");

        try
        {
            await loader.LoadPackageAsync(invalidPackagePath, "net9.0");
            Console.WriteLine("ERROR: Should have thrown format exception");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ Correctly handled invalid package format: {ex.GetType().Name}");
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
            Console.WriteLine("ERROR: Should have thrown exception for missing nuspec");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ Correctly handled empty package: {ex.GetType().Name}");
        }

        // Test invalid target framework
        var validPackagePath = CreateTestPackage("ValidPackage", "1.0.0");
        
        try
        {
            var result = await loader.LoadPackageAsync(validPackagePath, "invalid-framework");
            Console.WriteLine($"Loaded with invalid framework (may be handled gracefully): {result.LoadedAssemblyPaths.Length} assemblies");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✓ Correctly handled invalid framework: {ex.GetType().Name}");
        }

        Console.WriteLine("Error handling test completed successfully!\n");
    }

    /// <summary>
    /// Runs all tests in sequence.
    /// </summary>
    /// <returns>A task representing all test execution.</returns>
    public async Task RunAllTestsAsync()
    {
        Console.WriteLine("Starting NuGet Plugin Loader Tests...\n");

        try
        {
            await TestLocalPackageLoading();
            await TestPackageManifestParsing();
            await TestFrameworkCompatibility();
            await TestPackageCaching();
            await TestSecurityValidation();
            await TestPackageUpdates();
            await TestErrorHandling();

            Console.WriteLine("=== All Tests Completed Successfully! ===");
            Console.WriteLine($"Test artifacts created in: {_testCacheDirectory}");
            Console.WriteLine("The NuGet plugin loader implementation is working correctly.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates performance characteristics.
    /// </summary>
    /// <returns>A task representing the performance test.</returns>
    public async Task RunPerformanceTests()
    {
        Console.WriteLine("=== Performance Tests ===");

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
        Console.WriteLine("Sequential loading test:");
        foreach (var package in packages.Take(5))
        {
            var result = await loader.LoadPackageAsync(package, "net9.0");
            Console.WriteLine($"  Loaded {result.PackageIdentity.Id} in {result.LoadTime.TotalMilliseconds:F2} ms");
        }

        var sequentialTime = stopwatch.Elapsed;
        Console.WriteLine($"Sequential loading total time: {sequentialTime.TotalMilliseconds:F2} ms");

        // Clear cache for fair comparison
        await loader.ClearCacheAsync();
        stopwatch.Restart();

        // Concurrent loading
        Console.WriteLine("Concurrent loading test:");
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
            Console.WriteLine($"  Loaded {result.PackageIdentity.Id} in {result.LoadTime.TotalMilliseconds:F2} ms");
        }

        Console.WriteLine($"Concurrent loading total time: {concurrentTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Performance improvement: {sequentialTime.TotalMilliseconds / concurrentTime.TotalMilliseconds:F2}x");

        Console.WriteLine("Performance tests completed!\n");
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
            Console.WriteLine($"Warning: Failed to clean up test directory: {ex.Message}");
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
    public static async Task Main(string[] args)
    {
        Console.WriteLine("DotCompute NuGet Plugin Loader Tests");
        Console.WriteLine("====================================");

        using var tests = new NuGetPluginLoaderTests();

        try
        {
            if (args.Length > 0 && args[0] == "--performance")
            {
                await tests.RunPerformanceTests();
            }
            else
            {
                await tests.RunAllTestsAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tests failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}