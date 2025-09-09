// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Services;
using DotCompute.Runtime.Services.Interfaces;
using DotCompute.Runtime.Services.Implementation;
using DotCompute.Abstractions;
using DotCompute.Runtime.Services.Performance.Metrics;
using DotCompute.Runtime.Services.Performance.Results;
using DotCompute.Runtime.Services.Performance.Types;
using DotCompute.Core.Pipelines;
using DotCompute.Core.Pipelines.Services;
using DotCompute.Core.Pipelines.Services.Implementation;
using DotCompute.Core.Pipelines.Configuration;
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
        
        // Add production kernel services
        services.AddSingleton<IKernelCompiler, DefaultKernelCompiler>();
        services.AddSingleton<IKernelCache, MemoryKernelCache>();
        services.AddSingleton<IKernelProfiler, DefaultKernelProfiler>();
        
        // Register the production kernel execution service
        services.AddSingleton<KernelExecutionService>();
        services.AddSingleton<DotCompute.Abstractions.Interfaces.IComputeOrchestrator>(provider => 
            provider.GetRequiredService<KernelExecutionService>());
        
        // Keep simplified version available for backward compatibility
        services.AddSingleton<KernelExecutionServiceSimplified>();

        return services;
    }

    /// <summary>
    /// Adds DotCompute runtime services with custom options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure runtime options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntimeWithOptions(
        this IServiceCollection services,
        Action<DotComputeRuntimeOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddDotComputeRuntime(configuration: null);
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
    public static IServiceCollection AddDotComputeRuntimeAdvanced(
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

        return services.AddDotComputeRuntime(configuration: null);
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
        var kernelExecution = serviceProvider.GetRequiredService<KernelExecutionService>();
        
        var kernelCount = await kernelDiscovery.DiscoverAndRegisterKernelsAsync(kernelExecution);
        
        // Log successful initialization
        var logger = serviceProvider.GetService<Microsoft.Extensions.Logging.ILogger<AcceleratorRuntime>>();
        logger?.LogInformation("DotCompute runtime initialized successfully with {KernelCount} kernels", kernelCount);
    }

    /// <summary>
    /// Adds fluent kernel chaining services to the service collection.
    /// This enables the KernelChain fluent API for intuitive kernel operation chaining.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKernelChaining(this IServiceCollection services)
    {
        // Add memory caching for kernel chain cache service
        services.AddMemoryCache();
        
        // Add kernel chaining services
        services.AddSingleton<IKernelResolver, DefaultKernelResolver>();
        services.AddSingleton<IKernelChainValidator, DefaultKernelChainValidator>();
        services.AddSingleton<IKernelChainProfiler, DefaultKernelChainProfiler>();
        services.AddSingleton<IKernelChainCacheService, DefaultKernelChainCacheService>();

        // Register factory for creating kernel chain builders
        services.AddTransient<IKernelChainBuilder>(provider => 
        {
            var orchestrator = provider.GetRequiredService<DotCompute.Abstractions.Interfaces.IComputeOrchestrator>();
            var kernelResolver = provider.GetService<IKernelResolver>();
            var profiler = provider.GetService<IKernelChainProfiler>();
            var validator = provider.GetService<IKernelChainValidator>();
            var cacheService = provider.GetService<IKernelChainCacheService>();
            var logger = provider.GetService<ILogger<KernelChainBuilder>>();

            return new KernelChainBuilder(
                orchestrator,
                kernelResolver,
                profiler,
                validator,
                cacheService,
                logger);
        });

        return services;
    }

    /// <summary>
    /// Adds fluent kernel chaining services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure kernel chaining options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKernelChaining(
        this IServiceCollection services,
        Action<KernelChainingOptions> configureOptions)
    {
        services.Configure(configureOptions);
        return services.AddKernelChaining();
    }

    /// <summary>
    /// Adds the complete DotCompute runtime with kernel chaining support.
    /// This is a convenience method that adds both runtime and chaining services.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration for runtime options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeWithKernelChaining(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddDotComputeRuntime(configuration);
        services.AddKernelChaining();
        return services;
    }

    /// <summary>
    /// Initializes the DotCompute runtime with kernel chaining support.
    /// This configures the KernelChain static class with the service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the initialization operation</returns>
    public static async Task InitializeDotComputeWithKernelChainingAsync(this IServiceProvider serviceProvider)
    {
        // Initialize the standard runtime
        await serviceProvider.InitializeDotComputeRuntimeAsync();

        // Configure the KernelChain static class
        KernelChain.Configure(serviceProvider);

        // Log kernel chaining initialization
        var logger = serviceProvider.GetService<ILogger<KernelChainBuilder>>();
        logger?.LogInformation("Kernel chaining initialized successfully");
    }
}

// Note: Service implementations should be provided by backend-specific projects
// The integration layer provides the orchestration, while backends provide the actual services