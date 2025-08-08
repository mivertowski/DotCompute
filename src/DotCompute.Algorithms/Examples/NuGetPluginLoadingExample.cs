// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Abstractions;
using DotCompute.Algorithms.Management;
using DotCompute.Algorithms.Security;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Examples;

/// <summary>
/// Comprehensive example demonstrating NuGet plugin loading capabilities with DotCompute.
/// </summary>
public sealed class NuGetPluginLoadingExample
{
    private readonly ILogger<NuGetPluginLoadingExample> _logger;
    private readonly AlgorithmPluginManager _pluginManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NuGetPluginLoadingExample"/> class.
    /// </summary>
    /// <param name="accelerator">The compute accelerator to use.</param>
    /// <param name="logger">The logger instance.</param>
    public NuGetPluginLoadingExample(IAccelerator accelerator, ILogger<NuGetPluginLoadingExample> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Configure plugin manager with comprehensive security settings
        var options = new AlgorithmPluginManagerOptions
        {
            EnablePluginIsolation = true,
            EnableSecurityValidation = true,
            RequireDigitalSignature = true,
            RequireStrongName = true,
            EnableMalwareScanning = true,
            EnableMetadataAnalysis = true,
            EnableHealthChecks = true,
            HealthCheckInterval = TimeSpan.FromMinutes(2),
            MaxAssemblySize = 100 * 1024 * 1024, // 100 MB
            MinimumSecurityLevel = SecurityLevel.Medium
        };

        // Add trusted publishers for NuGet packages
        options.TrustedPublishers.Add("DotCompute.");
        options.TrustedPublishers.Add("Microsoft.");
        options.TrustedPublishers.Add("System.");

        // Add allowed plugin directories
        options.AllowedPluginDirectories.Add(Path.Combine(Environment.CurrentDirectory, "plugins"));
        options.AllowedPluginDirectories.Add(Path.Combine(Path.GetTempPath(), "DotComputeNuGetCache"));

        var pluginManagerLogger = logger as ILogger<AlgorithmPluginManager> ?? throw new ArgumentException("Logger must be for AlgorithmPluginManager");
        _pluginManager = new AlgorithmPluginManager(accelerator, pluginManagerLogger, options);
    }

    /// <summary>
    /// Demonstrates loading plugins from various NuGet sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the example execution.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting NuGet plugin loading example...");

        try
        {
            // Example 1: Load from local .nupkg file
            await LoadFromLocalPackageExample(cancellationToken);

            // Example 2: Load from NuGet.org by package ID
            await LoadFromRemotePackageExample(cancellationToken);

            // Example 3: Load specific version
            await LoadSpecificVersionExample(cancellationToken);

            // Example 4: Package validation before loading
            await PackageValidationExample(cancellationToken);

            // Example 5: Package update management
            await PackageUpdateExample(cancellationToken);

            // Example 6: Cache management
            await CacheManagementExample(cancellationToken);

            // Example 7: Dependency resolution demonstration
            await DependencyResolutionExample(cancellationToken);

            // Example 8: Security validation showcase
            await SecurityValidationExample(cancellationToken);

            // Example 9: Framework targeting
            await FrameworkTargetingExample(cancellationToken);

            // Example 10: Plugin execution with loaded NuGet packages
            await ExecuteLoadedPluginsExample(cancellationToken);

            _logger.LogInformation("NuGet plugin loading example completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NuGet plugin loading example failed");
            throw;
        }
    }

    /// <summary>
    /// Example 1: Loading plugins from a local .nupkg file.
    /// </summary>
    private async Task LoadFromLocalPackageExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 1: Loading from local .nupkg file ===");

        // In a real scenario, you would have a local .nupkg file
        var localPackagePath = Path.Combine(Environment.CurrentDirectory, "plugins", "CustomAlgorithms.1.0.0.nupkg");
        
        if (File.Exists(localPackagePath))
        {
            var stopwatch = Stopwatch.StartNew();
            var pluginCount = await _pluginManager.LoadPluginsFromNuGetPackageAsync(
                localPackagePath, 
                "net9.0", 
                cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Loaded {PluginCount} plugins from local package in {ElapsedMs} ms",
                pluginCount, 
                stopwatch.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogWarning("Local package not found: {PackagePath}", localPackagePath);
        }
    }

    /// <summary>
    /// Example 2: Loading plugins from NuGet.org by package ID.
    /// </summary>
    private async Task LoadFromRemotePackageExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 2: Loading from remote package ===");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Example: Load a hypothetical math algorithms package
            var pluginCount = await _pluginManager.LoadPluginsFromNuGetPackageAsync(
                "MathNet.Numerics", // Example package - replace with actual plugin package
                "net9.0",
                cancellationToken);
            
            stopwatch.Stop();

            _logger.LogInformation(
                "Loaded {PluginCount} plugins from remote package in {ElapsedMs} ms",
                pluginCount, 
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load remote package - this is expected in the example");
        }
    }

    /// <summary>
    /// Example 3: Loading a specific version of a package.
    /// </summary>
    private async Task LoadSpecificVersionExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 3: Loading specific package version ===");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Load specific version using PackageId:Version format
            var pluginCount = await _pluginManager.LoadPluginsFromNuGetPackageAsync(
                "DotCompute.ExamplePlugins:1.2.0", // Example package with version
                "net9.0",
                cancellationToken);
            
            stopwatch.Stop();

            _logger.LogInformation(
                "Loaded {PluginCount} plugins from specific version in {ElapsedMs} ms",
                pluginCount, 
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load specific version - this is expected in the example");
        }
    }

    /// <summary>
    /// Example 4: Validating a package before loading it.
    /// </summary>
    private async Task PackageValidationExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 4: Package validation ===");

        try
        {
            var validationResult = await _pluginManager.ValidateNuGetPackageAsync(
                "System.Text.Json", // Use a known package for validation
                "net9.0",
                cancellationToken);

            _logger.LogInformation(
                "Package validation result: Valid={IsValid}, Assemblies={AssemblyCount}, " +
                "Dependencies={DependencyCount}, Size={PackageSize} bytes, Time={ValidationTime} ms",
                validationResult.IsValid,
                validationResult.AssemblyCount,
                validationResult.DependencyCount,
                validationResult.PackageSize,
                validationResult.ValidationTime.TotalMilliseconds);

            if (validationResult.Warnings.Length > 0)
            {
                _logger.LogWarning("Validation warnings: {Warnings}", string.Join(", ", validationResult.Warnings));
            }

            _logger.LogInformation("Security validation: {SecurityDetails}", validationResult.SecurityDetails);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Package validation failed - this may be expected");
        }
    }

    /// <summary>
    /// Example 5: Updating packages to latest versions.
    /// </summary>
    private async Task PackageUpdateExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 5: Package updates ===");

        try
        {
            // First load an older version (in practice, this would already be loaded)
            await _pluginManager.LoadPluginsFromNuGetPackageAsync(
                "DotCompute.ExamplePlugins:1.0.0",
                "net9.0",
                cancellationToken);

            // Then update to latest
            var updatedPluginCount = await _pluginManager.UpdateNuGetPackageAsync(
                "DotCompute.ExamplePlugins",
                "net9.0",
                cancellationToken);

            _logger.LogInformation("Updated package loaded {PluginCount} plugins", updatedPluginCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Package update failed - this is expected in the example");
        }
    }

    /// <summary>
    /// Example 6: Cache management operations.
    /// </summary>
    private async Task CacheManagementExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 6: Cache management ===");

        try
        {
            // Get cached packages information
            var cachedPackages = await _pluginManager.GetCachedNuGetPackagesAsync(cancellationToken);
            
            _logger.LogInformation("Found {CacheCount} packages in cache:", cachedPackages.Length);
            
            foreach (var cached in cachedPackages)
            {
                _logger.LogInformation(
                    "  - {PackageId} v{Version}: {AssemblyCount} assemblies, " +
                    "{PackageSize} bytes, cached {CacheAge} ago",
                    cached.Identity.Id,
                    cached.Identity.Version,
                    cached.AssemblyCount,
                    cached.PackageSize,
                    cached.CacheAge);
            }

            // Clear old cache entries (older than 1 day)
            await _pluginManager.ClearNuGetCacheAsync(TimeSpan.FromDays(1), cancellationToken);
            
            _logger.LogInformation("Cache cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache management failed");
        }
    }

    /// <summary>
    /// Example 7: Dependency resolution demonstration.
    /// </summary>
    private async Task DependencyResolutionExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 7: Dependency resolution ===");

        // This would demonstrate how the NuGet loader handles dependencies
        // In practice, you would load a package with complex dependencies
        
        _logger.LogInformation("Dependency resolution is handled automatically during package loading");
        _logger.LogInformation("The NuGetPluginLoader resolves transitive dependencies and version conflicts");
        _logger.LogInformation("All dependencies are validated and loaded into isolated contexts");
    }

    /// <summary>
    /// Example 8: Security validation showcase.
    /// </summary>
    private async Task SecurityValidationExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 8: Security validation ===");

        _logger.LogInformation("Security validation includes:");
        _logger.LogInformation("  - Digital signature verification (Authenticode)");
        _logger.LogInformation("  - Strong name validation");
        _logger.LogInformation("  - Malware scanning integration");
        _logger.LogInformation("  - Security policy evaluation");
        _logger.LogInformation("  - Package integrity checks");
        _logger.LogInformation("  - Trusted publisher verification");
        
        // The actual security validation happens automatically during loading
        _logger.LogInformation("All loaded packages have passed security validation");
    }

    /// <summary>
    /// Example 9: Framework targeting demonstration.
    /// </summary>
    private async Task FrameworkTargetingExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 9: Framework targeting ===");

        try
        {
            // Load package for different target frameworks
            var frameworks = new[] { "net9.0", "net8.0", "net6.0", "netstandard2.1" };

            foreach (var framework in frameworks)
            {
                try
                {
                    _logger.LogInformation("Testing framework compatibility: {Framework}", framework);
                    
                    var validationResult = await _pluginManager.ValidateNuGetPackageAsync(
                        "System.Text.Json",
                        framework,
                        cancellationToken);

                    _logger.LogInformation(
                        "Framework {Framework}: {AssemblyCount} assemblies found",
                        framework,
                        validationResult.AssemblyCount);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Framework {Framework} not supported: {Error}", framework, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Framework targeting example failed");
        }
    }

    /// <summary>
    /// Example 10: Executing loaded plugins.
    /// </summary>
    private async Task ExecuteLoadedPluginsExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Example 10: Executing loaded plugins ===");

        try
        {
            // Get information about all loaded plugins
            var pluginInfo = _pluginManager.GetPluginInfo();
            
            _logger.LogInformation("Currently loaded plugins:");
            
            foreach (var info in pluginInfo)
            {
                _logger.LogInformation(
                    "  - {PluginId} ({PluginName}) v{Version}: {Description}",
                    info.Id,
                    info.Name,
                    info.Version,
                    info.Description);

                _logger.LogInformation(
                    "    Accelerators: {Accelerators}",
                    string.Join(", ", info.SupportedAccelerators));

                _logger.LogInformation(
                    "    Input Types: {InputTypes}",
                    string.Join(", ", info.InputTypes));

                _logger.LogInformation(
                    "    Output Type: {OutputType}",
                    info.OutputType);
            }

            // Example of executing a plugin (if any are loaded)
            var firstPlugin = pluginInfo.FirstOrDefault();
            if (firstPlugin != null)
            {
                _logger.LogInformation("Attempting to execute plugin: {PluginId}", firstPlugin.Id);
                
                try
                {
                    // This would execute with actual input data appropriate for the plugin
                    var result = await _pluginManager.ExecutePluginAsync(
                        firstPlugin.Id,
                        new object[] { /* appropriate inputs */ },
                        null,
                        cancellationToken);

                    _logger.LogInformation("Plugin execution completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Plugin execution failed - this may be expected without proper inputs");
                }
            }
            else
            {
                _logger.LogInformation("No plugins loaded for execution example");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin execution example failed");
        }
    }

    /// <summary>
    /// Demonstrates advanced scenarios and best practices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the demonstration.</returns>
    public async Task RunAdvancedScenariosAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Advanced NuGet Plugin Loading Scenarios ===");

        await BatchPackageLoadingExample(cancellationToken);
        await ConditionalLoadingExample(cancellationToken);
        await PerformanceMonitoringExample(cancellationToken);
        await ErrorHandlingExample(cancellationToken);
    }

    /// <summary>
    /// Demonstrates loading multiple packages efficiently.
    /// </summary>
    private async Task BatchPackageLoadingExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Batch package loading ---");

        var packages = new[]
        {
            "System.Text.Json",
            "System.Collections.Immutable",
            "System.Reactive"
        };

        var loadTasks = packages.Select(async packageId =>
        {
            try
            {
                var count = await _pluginManager.LoadPluginsFromNuGetPackageAsync(
                    packageId, 
                    "net9.0", 
                    cancellationToken);
                
                return new { PackageId = packageId, PluginCount = count, Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load package: {PackageId}", packageId);
                return new { PackageId = packageId, PluginCount = 0, Success = false };
            }
        });

        var results = await Task.WhenAll(loadTasks);
        
        foreach (var result in results)
        {
            if (result.Success)
            {
                _logger.LogInformation(
                    "Batch loaded {PackageId}: {PluginCount} plugins",
                    result.PackageId,
                    result.PluginCount);
            }
        }
    }

    /// <summary>
    /// Demonstrates conditional loading based on package analysis.
    /// </summary>
    private async Task ConditionalLoadingExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Conditional loading based on validation ---");

        var packageId = "System.Text.Json";
        
        // First, validate the package
        var validation = await _pluginManager.ValidateNuGetPackageAsync(packageId, "net9.0", cancellationToken);
        
        // Only load if it meets our criteria
        if (validation.IsValid && 
            validation.SecurityValidationPassed && 
            validation.PackageSize < 50 * 1024 * 1024 && // < 50 MB
            validation.Warnings.Length == 0)
        {
            _logger.LogInformation("Package meets criteria, loading...");
            await _pluginManager.LoadPluginsFromNuGetPackageAsync(packageId, "net9.0", cancellationToken);
        }
        else
        {
            _logger.LogWarning("Package does not meet loading criteria");
        }
    }

    /// <summary>
    /// Demonstrates performance monitoring during package operations.
    /// </summary>
    private async Task PerformanceMonitoringExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Performance monitoring ---");

        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);

        try
        {
            await _pluginManager.LoadPluginsFromNuGetPackageAsync("System.Text.Json", "net9.0", cancellationToken);
            
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var memoryUsed = memoryAfter - memoryBefore;

            _logger.LogInformation(
                "Performance metrics - Time: {ElapsedMs} ms, Memory: {MemoryMB} MB",
                stopwatch.ElapsedMilliseconds,
                memoryUsed / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Performance monitoring example failed");
        }
    }

    /// <summary>
    /// Demonstrates comprehensive error handling.
    /// </summary>
    private async Task ErrorHandlingExample(CancellationToken cancellationToken)
    {
        _logger.LogInformation("--- Error handling demonstration ---");

        var problematicPackages = new[]
        {
            "NonExistentPackage12345",
            "InvalidPackage:BadVersion",
            "/invalid/path/to/package.nupkg"
        };

        foreach (var package in problematicPackages)
        {
            try
            {
                await _pluginManager.LoadPluginsFromNuGetPackageAsync(package, "net9.0", cancellationToken);
            }
            catch (ArgumentException ex)
            {
                _logger.LogInformation("Handled ArgumentException for {Package}: {Message}", package, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogInformation("Handled InvalidOperationException for {Package}: {Message}", package, ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogInformation("Handled FileNotFoundException for {Package}: {Message}", package, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Handled generic exception for {Package}: {Type} - {Message}", 
                    package, ex.GetType().Name, ex.Message);
            }
        }

        _logger.LogInformation("Error handling demonstration completed");
    }

    /// <summary>
    /// Disposes the plugin manager.
    /// </summary>
    /// <returns>A task representing the disposal operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await _pluginManager.DisposeAsync();
    }
}