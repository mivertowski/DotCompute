// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.DependencyInjection;
using DotCompute.Runtime.Examples.DependencyInjection.Factories;
using DotCompute.Runtime.Examples.DependencyInjection.Logging;
using DotCompute.Runtime.Examples.DependencyInjection.Providers;
using DotCompute.Runtime.Examples.DependencyInjection.Services;
using DotCompute.Runtime.Factories;
using DotCompute.Runtime.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection;

/// <summary>
/// Comprehensive example demonstrating DotCompute DI integration
/// </summary>
public static class DIIntegrationExample
{
    /// <summary>
    /// Example showing basic DI setup for DotCompute
    /// </summary>
    public static async Task BasicDISetupExample()
    {
        var services = new ServiceCollection();

        // Add logging
        _ = services.AddLogging(builder => builder.AddConsole());

        // Add DotCompute with basic configuration
        _ = services.AddDotComputeRuntime(configureOptions: options =>
        {
            options.EnableAutoDiscovery = true;
            options.PreferredAcceleratorType = AcceleratorType.CPU;
            options.EnableKernelCaching = true;
            options.EnableMemoryPooling = true;
        });

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Get runtime and use it
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
        await runtime.InitializeAsync();

        // Get accelerator through DI
        var acceleratorManager = serviceProvider.GetRequiredService<IAcceleratorManager>();
        var defaultAccelerator = acceleratorManager.Default;

        Console.WriteLine($"Default accelerator: {defaultAccelerator.Info.Name} ({defaultAccelerator.Info.DeviceType})");

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }

    /// <summary>
    /// Example showing advanced DI setup with configuration
    /// </summary>
    public static async Task AdvancedDISetupExample()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DotCompute:EnableAutoDiscovery"] = "true",
                ["DotCompute:PreferredAcceleratorType"] = "CPU",
                ["DotCompute:MaxAccelerators"] = "8",
                ["DotCompute:EnableKernelCaching"] = "true",
                ["DotCompute:KernelCacheDirectory"] = "./cache/kernels",
                ["DotCompute:MaxCacheSizeMB"] = "512",
                ["DotCompute:EnableMemoryPooling"] = "true",
                ["DotCompute:InitialPoolSizeMB"] = "128",
                ["DotCompute:EnableProfiling"] = "true",
                ["DotCompute:Plugins:EnablePlugins"] = "true",
                ["DotCompute:Plugins:PluginDirectories:0"] = "./plugins",
                ["DotCompute:Plugins:EnableDependencyInjection"] = "true"
            })
            .Build();

        var services = new ServiceCollection();

        // Add configuration
        _ = services.AddSingleton<IConfiguration>(configuration);

        // Add logging with more detailed configuration
        _ = services.AddLogging(builder =>
        {
            _ = builder.AddConsole();
            _ = builder.AddDebug();
            _ = builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add complete DotCompute stack
        _ = services.AddDotComputeComplete(configuration);

        // Add custom services
        _ = services.AddTransient<ICustomComputeService, CustomComputeService>();

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Use the enhanced setup
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
        // var pluginManager = serviceProvider.GetRequiredService<IAlgorithmPluginManager>();
        var memoryPool = serviceProvider.GetRequiredService<IMemoryPoolService>();
        var customService = serviceProvider.GetRequiredService<ICustomComputeService>();

        await runtime.InitializeAsync();
        // await pluginManager.LoadPluginsAsync();

        // Use memory pool
        var stats = memoryPool.GetUsageStatistics();
        Console.WriteLine($"Memory pools: {stats.PerAcceleratorStats.Count}");

        // Use custom service
        await customService.PerformComputationAsync();

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }

    /// <summary>
    /// Example showing plugin DI integration
    /// </summary>
    public static Task PluginDIExample()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(builder => builder.AddConsole());

        // Add DotCompute with plugin support
        _ = services.AddDotComputeRuntime();
        _ = services.AddDotComputePlugins(configureOptions: options =>
        {
            options.EnablePlugins = true;
            options.EnableDependencyInjection = true;
            options.PluginLifetime = DotCompute.Runtime.Configuration.ServiceLifetime.Scoped;
        });

        // Register services that plugins might depend on
        _ = services.AddSingleton<ICustomDataProvider, CustomDataProvider>();
        _ = services.AddTransient<IComputationLogger, ComputationLogger>();

        var serviceProvider = services.BuildServiceProvider();

        // Get plugin services
        var pluginServiceProvider = serviceProvider.GetRequiredService<IPluginServiceProvider>();
        var pluginLifecycleManager = serviceProvider.GetRequiredService<IPluginLifecycleManager>();
        var pluginFactory = serviceProvider.GetRequiredService<IPluginFactory>();

        // Register plugin-specific services
        pluginServiceProvider.RegisterPluginServices("MyPlugin", pluginServices =>
        {
            _ = pluginServices.AddSingleton<IPluginSpecificService, PluginSpecificService>();
        });

        // Create plugin with DI
        var pluginScope = pluginServiceProvider.CreatePluginScope("MyPlugin");
        // var plugin = await pluginFactory.CreateAsync<IAlgorithmPlugin>(
        //     typeof(SampleAlgorithmPlugin), pluginScope);

        // Console.WriteLine($"Created plugin: {plugin.Name} v{plugin.Version}");

        pluginScope.Dispose();
        serviceProvider.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Example showing hosted service integration
    /// </summary>
    public static Task HostedServiceExample()
    {
        // This example requires Microsoft.Extensions.Hosting package
        // Uncomment and install the package to enable this functionality

        Console.WriteLine("HostedServiceExample: Microsoft.Extensions.Hosting package required");
        Console.WriteLine("Install with: dotnet add package Microsoft.Extensions.Hosting");

        return Task.CompletedTask;
        /*
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Add DotCompute as hosted service
                services.AddDotComputeRuntime(context.Configuration);

                // Add custom background service
                services.AddHostedService<ComputeBackgroundService>();
            })
            .Build();

        // Start host (will initialize DotCompute automatically)
        await host.StartAsync();

        // Host is running with DotCompute initialized
        Console.WriteLine("Host started with DotCompute runtime");

        // Simulate some work
        await Task.Delay(1000);

        await host.StopAsync();
        host.Dispose();
        */
    }

    /// <summary>
    /// Example showing custom accelerator factory
    /// </summary>
    public static async Task CustomFactoryExample()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging(builder => builder.AddConsole());

        // Use custom accelerator factory
        _ = services.AddDotComputeRuntimeWithFactory<CustomAcceleratorFactory>();

        var serviceProvider = services.BuildServiceProvider();
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();

        await runtime.InitializeAsync();

        var accelerators = runtime.GetAccelerators();
        Console.WriteLine($"Found {accelerators.Count} accelerators using custom factory");

        await runtime.DisposeAsync();
        serviceProvider.Dispose();
    }
}