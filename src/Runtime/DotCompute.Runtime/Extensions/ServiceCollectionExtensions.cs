// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Services;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Runtime.Services.Performance.Metrics;
using DotCompute.Runtime.Services.Performance.Results;
using DotCompute.Runtime.Services.Performance.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Extensions;

/// <summary>
/// Extension methods for configuring DotCompute runtime services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DotCompute runtime services to the service collection with automatic kernel discovery.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration for runtime options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntime(
        this IServiceCollection services, 
        IConfiguration? configuration = null)
    {
        // Add configuration
        if (configuration != null)
        {
            services.Configure<DotComputeRuntimeOptions>(
                configuration.GetSection(DotComputeRuntimeOptions.SectionName));
            services.Configure<DotComputePluginOptions>(
                configuration.GetSection(DotComputePluginOptions.SectionName));
            services.Configure<AdvancedMemoryOptions>(
                configuration.GetSection("DotCompute:Memory"));
            services.Configure<PerformanceMonitoringOptions>(
                configuration.GetSection("DotCompute:Performance"));
        }
        else
        {
            // Add default options
            services.Configure<DotComputeRuntimeOptions>(options => { });
            services.Configure<DotComputePluginOptions>(options => { });
            services.Configure<AdvancedMemoryOptions>(options => { });
            services.Configure<PerformanceMonitoringOptions>(options => { });
        }

        // Add options validation
        services.AddSingleton<IValidateOptions<DotComputeRuntimeOptions>, RuntimeOptionsValidator>();

        // Add core runtime services
        services.AddSingleton<AcceleratorRuntime>();
        
        // Add the integration services (core bridge between generator and runtime)
        services.AddSingleton<GeneratedKernelDiscoveryService>();
        services.AddSingleton<KernelExecutionServiceSimplified>();
        services.AddSingleton<IComputeOrchestrator>(provider => 
            provider.GetRequiredService<KernelExecutionServiceSimplified>());

        // Note: Actual kernel compiler, cache, profiler, and plugin services 
        // should be registered by the specific backend implementations

        return services;
    }

    /// <summary>
    /// Adds DotCompute runtime services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure runtime options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntime(
        this IServiceCollection services,
        Action<DotComputeRuntimeOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddDotComputeRuntime();
    }

    /// <summary>
    /// Adds DotCompute runtime services with advanced configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureRuntime">Action to configure runtime options</param>
    /// <param name="configurePlugins">Action to configure plugin options</param>
    /// <param name="configureMemory">Action to configure memory options</param>
    /// <param name="configurePerformance">Action to configure performance options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntime(
        this IServiceCollection services,
        Action<DotComputeRuntimeOptions>? configureRuntime = null,
        Action<DotComputePluginOptions>? configurePlugins = null,
        Action<AdvancedMemoryOptions>? configureMemory = null,
        Action<PerformanceMonitoringOptions>? configurePerformance = null)
    {
        if (configureRuntime != null)
            services.Configure(configureRuntime);
        if (configurePlugins != null)
            services.Configure(configurePlugins);
        if (configureMemory != null)
            services.Configure(configureMemory);
        if (configurePerformance != null)
            services.Configure(configurePerformance);

        return services.AddDotComputeRuntime();
    }

    /// <summary>
    /// Initializes the DotCompute runtime and discovers kernels from loaded assemblies.
    /// This should be called after building the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the initialization operation</returns>
    public static async Task InitializeDotComputeRuntimeAsync(this IServiceProvider serviceProvider)
    {
        // Initialize the accelerator runtime
        var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
        await runtime.InitializeAsync();

        // Discover and register kernels
        var kernelDiscovery = serviceProvider.GetRequiredService<GeneratedKernelDiscoveryService>();
        var kernelExecution = serviceProvider.GetRequiredService<KernelExecutionServiceSimplified>();
        
        var kernelCount = await kernelDiscovery.DiscoverAndRegisterKernelsAsync(kernelExecution);
        
        // Log successful initialization
        var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<AcceleratorRuntime>>();
        logger?.LogInformation("DotCompute runtime initialized successfully with {KernelCount} kernels", kernelCount);
    }
}

// Note: Service implementations should be provided by backend-specific projects
// The integration layer provides the orchestration, while backends provide the actual services