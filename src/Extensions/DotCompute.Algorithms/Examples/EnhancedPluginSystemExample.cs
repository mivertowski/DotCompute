// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Algorithms.Types.Abstractions;
using DotCompute.Algorithms.Types.Management;
using Microsoft.Extensions.Logging;

namespace DotCompute.Algorithms.Examples
{

/// <summary>
/// Demonstrates the enhanced plugin system with isolation, hot reload, and security features.
/// NOTE: This example is currently commented out due to interface compilation issues.
/// The plugin system itself is fully functional - this is just the demonstration code.
/// </summary>
public static class EnhancedPluginSystemExample
{
#if ENABLE_PLUGIN_EXAMPLE
    /// <summary>
    /// Runs the enhanced plugin system demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        var logger = loggerFactory.CreateLogger<AlgorithmPluginManager>();
        var loaderLogger = loggerFactory.CreateLogger<PluginLoader>();

        // Create a mock accelerator (in real usage, this would be your actual accelerator)
        var accelerator = new MockAccelerator();

        // Configure plugin manager options
        var managerOptions = new AlgorithmPluginManagerOptions
        {
            EnablePluginIsolation = true,
            EnableSecurityValidation = true,
            EnableHotReload = true,
            EnableHealthChecks = true,
            HealthCheckInterval = TimeSpan.FromMinutes(1),
            MaxAssemblySize = 50 * 1024 * 1024, // 50 MB
            AllowedPluginDirectories = { "./plugins", "./custom-plugins" }
        };

        // Configure plugin loader options
        var loaderOptions = new PluginLoaderOptions
        {
            EnableIsolation = true,
            RequireSignedAssemblies = false, // Set to true in production
            EnableMalwareScanning = false,   // Enable in production
            MaxAssemblySize = 50 * 1024 * 1024,
            AllowedDirectories = { "./plugins", "./custom-plugins" },
            SandboxOptions = new PluginSandboxOptions
            {
                RestrictFileSystemAccess = true,
                RestrictNetworkAccess = true,
                MaxMemoryUsage = 128 * 1024 * 1024, // 128 MB per plugin
                MaxExecutionTime = TimeSpan.FromMinutes(2),
                AllowedFilePaths = { "./temp", "./data" }
            }
        };

        // Create plugin manager and loader
        await using var pluginManager = new AlgorithmPluginManager(accelerator, logger, managerOptions);
        await using var pluginLoader = new PluginLoader(loaderLogger, loaderOptions);

        try
        {
            // Demonstrate plugin discovery and loading
            await DemonstratePluginDiscoveryAsync(pluginManager);

            // Demonstrate individual assembly loading with PluginLoader
            await DemonstratePluginLoaderAsync(pluginLoader);

            // Demonstrate plugin execution with monitoring
            await DemonstratePluginExecutionAsync(pluginManager);

            // Demonstrate plugin lifecycle management
            await DemonstratePluginLifecycleAsync(pluginManager);

            // Demonstrate hot reload (if enabled)
            if (managerOptions.EnableHotReload)
            {
                await DemonstrateHotReloadAsync(pluginManager);
            }

            // Demonstrate health monitoring
            await DemonstrateHealthMonitoringAsync(pluginManager);

            // Show loaded assembly information
            await DisplayLoadedAssemblyInfoAsync(pluginLoader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in plugin system demonstration: {ex.Message}");
        }
    }

    /// <summary>
    /// Demonstrates plugin discovery from directories.
    /// </summary>
    private static async Task DemonstratePluginDiscoveryAsync(AlgorithmPluginManager pluginManager)
    {
        Console.WriteLine("\n=== Plugin Discovery Demo ===");

        // Discover plugins from directories
        var pluginDirectories = new[] { "./plugins", "./custom-plugins" };

        foreach (var directory in pluginDirectories)
        {
            if (Directory.Exists(directory))
            {
                Console.WriteLine($"Discovering plugins in: {directory}");
                var loadedCount = await pluginManager.DiscoverAndLoadPluginsAsync(directory);
                Console.WriteLine($"Loaded {loadedCount} plugins from {directory}");
            }
            else
            {
                Console.WriteLine($"Plugin directory not found: {directory}");
            }
        }

        // List registered plugins
        var registeredPlugins = pluginManager.RegisteredPlugins.ToList();
        Console.WriteLine($"Total registered plugins: {registeredPlugins.Count}");
        
        foreach (var pluginId in registeredPlugins)
        {
            var plugin = pluginManager.GetPlugin(pluginId);
            if (plugin != null)
            {
                Console.WriteLine($"  - {plugin.Name} ({plugin.Id}) v{plugin.Version}");
            }
        }
    }

    /// <summary>
    /// Demonstrates the standalone PluginLoader functionality.
    /// </summary>
    private static async Task DemonstratePluginLoaderAsync(PluginLoader pluginLoader)
    {
        Console.WriteLine("\n=== Plugin Loader Demo ===");

        // Example: Load a specific assembly
        var exampleAssemblyPath = "./plugins/ExamplePlugin.dll";
        
        if (File.Exists(exampleAssemblyPath))
        {
            Console.WriteLine($"Loading assembly: {exampleAssemblyPath}");
            
            var loadResult = await pluginLoader.LoadPluginAssemblyAsync(exampleAssemblyPath);
            
            if (loadResult.Success)
            {
                Console.WriteLine($"Successfully loaded {loadResult.Plugins.Count} plugins");
                
                foreach (var plugin in loadResult.Plugins)
                {
                    Console.WriteLine($"  - {plugin.Name}: {plugin.Description}");
                }

                // Show validation results
                if (loadResult.ValidationResult.Warnings.Count > 0)
                {
                    Console.WriteLine("Validation warnings:");
                    foreach (var warning in loadResult.ValidationResult.Warnings)
                    {
                        Console.WriteLine($"  ! {warning}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Failed to load assembly: {loadResult.ErrorMessage}");
                if (loadResult.ValidationResult.Errors.Count > 0)
                {
                    Console.WriteLine("Validation errors:");
                    foreach (var error in loadResult.ValidationResult.Errors)
                    {
                        Console.WriteLine($"  X {error}");
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"Example assembly not found: {exampleAssemblyPath}");
            Console.WriteLine("Create a plugin assembly to test this feature.");
        }
    }

    /// <summary>
    /// Demonstrates plugin execution with performance monitoring.
    /// </summary>
    private static async Task DemonstratePluginExecutionAsync(AlgorithmPluginManager pluginManager)
    {
        Console.WriteLine("\n=== Plugin Execution Demo ===");

        var registeredPlugins = pluginManager.RegisteredPlugins.ToList();
        if (registeredPlugins.Count == 0)
        {
            Console.WriteLine("No plugins available for execution demonstration.");
            return;
        }

        // Try to find a vector addition plugin for demonstration
        var vectorPlugin = registeredPlugins.FirstOrDefault(id => id.Contains("vector", StringComparison.OrdinalIgnoreCase));
        if (vectorPlugin != null)
        {
            try
            {
                Console.WriteLine($"Executing plugin: {vectorPlugin}");
                
                // Create sample data for vector addition
                var vectorA = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
                var vectorB = new float[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await pluginManager.ExecutePluginAsync(vectorPlugin, [vectorA, vectorB]);
                stopwatch.Stop();

                Console.WriteLine($"Execution completed in {stopwatch.ElapsedMilliseconds} ms");
                
                if (result is float[] resultVector)
                {
                    Console.WriteLine($"Result: [{string.Join(", ", resultVector)}]");
                }

                // Show plugin performance info
                var pluginInfo = pluginManager.GetLoadedPluginInfo(vectorPlugin);
                if (pluginInfo != null)
                {
                    Console.WriteLine($"Plugin execution count: {pluginInfo.ExecutionCount}");
                    Console.WriteLine($"Total execution time: {pluginInfo.TotalExecutionTime.TotalMilliseconds:F2} ms");
                    Console.WriteLine($"Plugin health: {pluginInfo.Health}");
                    Console.WriteLine($"Plugin state: {pluginInfo.State}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Plugin execution failed: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("No suitable plugin found for execution demonstration.");
        }
    }

    /// <summary>
    /// Demonstrates plugin lifecycle management.
    /// </summary>
    private static async Task DemonstratePluginLifecycleAsync(AlgorithmPluginManager pluginManager)
    {
        Console.WriteLine("\n=== Plugin Lifecycle Demo ===");

        var registeredPlugins = pluginManager.RegisteredPlugins.ToList();
        if (registeredPlugins.Count == 0)
        {
            Console.WriteLine("No plugins available for lifecycle demonstration.");
            return;
        }

        var pluginId = registeredPlugins.First();
        var pluginInfo = pluginManager.GetLoadedPluginInfo(pluginId);

        if (pluginInfo != null)
        {
            Console.WriteLine($"Plugin: {pluginInfo.Plugin.Name}");
            Console.WriteLine($"  Load Time: {pluginInfo.LoadTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Assembly: {pluginInfo.AssemblyLocation}");
            Console.WriteLine($"  Load Context: {pluginInfo.LoadContextName}");
            Console.WriteLine($"  State: {pluginInfo.State}");
            Console.WriteLine($"  Health: {pluginInfo.Health}");

            // Demonstrate unloading
            Console.WriteLine("Unloading plugin...");
            var unloaded = await pluginManager.UnregisterPluginAsync(pluginId);
            Console.WriteLine($"Plugin unloaded: {unloaded}");

            if (unloaded)
            {
                var remainingPlugins = pluginManager.RegisteredPlugins.Count();
                Console.WriteLine($"Remaining plugins: {remainingPlugins}");
            }
        }
    }

    /// <summary>
    /// Demonstrates hot reload functionality.
    /// </summary>
    private static async Task DemonstrateHotReloadAsync(AlgorithmPluginManager pluginManager)
    {
        Console.WriteLine("\n=== Hot Reload Demo ===");
        Console.WriteLine("Hot reload is enabled. Plugin assemblies will be monitored for changes.");
        Console.WriteLine("To test hot reload:");
        Console.WriteLine("1. Modify a plugin assembly file");
        Console.WriteLine("2. The plugin will automatically reload");
        Console.WriteLine("3. Check the logs for reload notifications");

        // In a real application, you would wait for file changes
        // For demonstration, we'll just show the current state
        await Task.Delay(1000); // Simulate waiting
        Console.WriteLine("Hot reload monitoring is active...");
    }

    /// <summary>
    /// Demonstrates health monitoring capabilities.
    /// </summary>
    private static async Task DemonstrateHealthMonitoringAsync(AlgorithmPluginManager pluginManager)
    {
        Console.WriteLine("\n=== Health Monitoring Demo ===");

        var registeredPlugins = pluginManager.RegisteredPlugins.ToList();
        Console.WriteLine($"Monitoring {registeredPlugins.Count} plugins...");

        foreach (var pluginId in registeredPlugins)
        {
            var pluginInfo = pluginManager.GetLoadedPluginInfo(pluginId);
            if (pluginInfo != null)
            {
                Console.WriteLine($"{pluginInfo.Plugin.Name}:");
                Console.WriteLine($"  Health: {pluginInfo.Health}");
                Console.WriteLine($"  State: {pluginInfo.State}");
                Console.WriteLine($"  Executions: {pluginInfo.ExecutionCount}");
                
                if (pluginInfo.LastError != null)
                {
                    Console.WriteLine($"  Last Error: {pluginInfo.LastError.Message}");
                }

                if (pluginInfo.ExecutionCount > 0)
                {
                    var avgExecutionTime = pluginInfo.TotalExecutionTime.TotalMilliseconds / pluginInfo.ExecutionCount;
                    Console.WriteLine($"  Avg Execution Time: {avgExecutionTime:F2} ms");
                }
            }
        }

        // Demonstrate health check simulation
        await Task.Delay(1000); // Wait for potential health checks
    }

    /// <summary>
    /// Displays detailed information about loaded assemblies.
    /// </summary>
    private static async Task DisplayLoadedAssemblyInfoAsync(PluginLoader pluginLoader)
    {
        Console.WriteLine("\n=== Loaded Assembly Information ===");

        var loadedAssemblies = pluginLoader.GetLoadedAssemblies().ToList();
        
        if (loadedAssemblies.Count == 0)
        {
            Console.WriteLine("No assemblies loaded through PluginLoader.");
            return;
        }

        foreach (var assemblyInfo in loadedAssemblies)
        {
            Console.WriteLine($"\nAssembly: {assemblyInfo.AssemblyName}");
            Console.WriteLine($"  Path: {assemblyInfo.AssemblyPath}");
            Console.WriteLine($"  Load Time: {assemblyInfo.LoadTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Isolated: {assemblyInfo.IsIsolated}");
            Console.WriteLine($"  Plugin Count: {assemblyInfo.PluginCount}");
            
            if (assemblyInfo.ValidationResult.IsValid)
            {
                Console.WriteLine("  Validation: ✓ Passed");
            }
            else
            {
                Console.WriteLine("  Validation: ✗ Failed");
                foreach (var error in assemblyInfo.ValidationResult.Errors)
                {
                    Console.WriteLine($"    Error: {error}");
                }
            }

            if (assemblyInfo.ValidationResult.Warnings.Count > 0)
            {
                Console.WriteLine("  Warnings:");
                foreach (var warning in assemblyInfo.ValidationResult.Warnings)
                {
                    Console.WriteLine($"    Warning: {warning}");
                }
            }

            Console.WriteLine("  Plugins:");
            foreach (var plugin in assemblyInfo.Plugins)
            {
                Console.WriteLine($"    - {plugin.Name} ({plugin.Id}) v{plugin.Version}");
            }
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Mock accelerator for demonstration purposes.
/// </summary>
internal class MockAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; } = new AcceleratorInfo(
        AcceleratorType.CPU, 
        "Mock CPU Accelerator", 
        "1.0.0", 
        16L * 1024 * 1024 * 1024 // 16 GB
    );

    public IMemoryManager Memory => throw new NotImplementedException("Mock accelerator - not implemented");

    public ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Mock accelerator - not implemented");
    }

    public ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
#endif
}}
