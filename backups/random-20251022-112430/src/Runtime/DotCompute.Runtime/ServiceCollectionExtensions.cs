// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.DependencyInjection;
using DotCompute.Runtime.Factories;
using DotCompute.Runtime.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotCompute.Runtime.DependencyInjection.Core;

namespace DotCompute.Runtime;


/// <summary>
/// Extension methods for registering DotCompute Runtime services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add comprehensive DotCompute Runtime services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Optional configuration for runtime settings</param>
    /// <param name="configureOptions">Optional action to configure runtime options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntime(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<DotComputeRuntimeOptions>? configureOptions = null)
    {
        // Configure runtime options
        if (configuration != null)
        {
            _ = services.Configure<DotComputeRuntimeOptions>(options =>
            {
#pragma warning disable IL2026, IL3050 // Members annotated with trimming attributes
                configuration.GetSection("DotCompute").Bind(options);
#pragma warning restore IL2026, IL3050
                configureOptions?.Invoke(options);
            });
        }
        else if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // Add core services
        _ = services.AddLogging();

        // Register accelerator providers with DI (Core project provides full implementation)
        // For now, we'll let the consuming project register providers
        // services.TryAddTransient<CpuAcceleratorProvider>();

        // Register accelerator manager (stub - full implementation in Core project)
        // services.TryAddSingleton<IAcceleratorManager, DefaultAcceleratorManager>();

        // Register accelerator factory for proper DI-based creation
        services.TryAddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

        // Register memory services
        services.TryAddTransient<IMemoryPoolService, MemoryPoolService>();
        services.TryAddTransient<IUnifiedMemoryService, UnifiedMemoryService>();

        // Register kernel services
        // Commented out - these interfaces don't exist in Abstractions
        // services.TryAddTransient<IKernelCompilerService, KernelCompilerService>();
        // services.TryAddTransient<IKernelCacheService, KernelCacheService>();

        // Register the main runtime service
        services.TryAddSingleton<AcceleratorRuntime>();

        // Register configuration validators
        services.TryAddSingleton<IValidateOptions<DotComputeRuntimeOptions>, RuntimeOptionsValidator>();

        // Register hosted service for runtime initialization
        _ = services.AddHostedService<RuntimeInitializationService>();

        return services;
    }

    /// <summary>
    /// Add DotCompute Runtime with specific accelerator providers
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="providerTypes">Types of accelerator providers to register</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntimeWithProviders(
        this IServiceCollection services,
        params Type[] providerTypes)
    {
        _ = services.AddDotComputeRuntime();

        foreach (var providerType in providerTypes)
        {
            if (!typeof(IAcceleratorProvider).IsAssignableFrom(providerType))
            {
                throw new ArgumentException($"Type {providerType.Name} does not implement IAcceleratorProvider");
            }

#pragma warning disable IL2072 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' - Plugin system requires runtime type registration
            services.TryAddTransient(typeof(IAcceleratorProvider), providerType);
#pragma warning restore IL2072
        }

        return services;
    }

    /// <summary>
    /// Add DotCompute Runtime with custom accelerator factory
    /// </summary>
    /// <typeparam name="TFactory">The factory type</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeRuntimeWithFactory<TFactory>(
        this IServiceCollection services)
        where TFactory : class, IUnifiedAcceleratorFactory
    {
        _ = services.AddDotComputeRuntime();
        _ = services.Replace(ServiceDescriptor.Singleton<IUnifiedAcceleratorFactory, TFactory>());
        return services;
    }

    /// <summary>
    /// Add plugin support to DotCompute Runtime
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Optional configuration for plugin settings</param>
    /// <param name="configureOptions">Optional action to configure plugin options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputePlugins(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<DotComputePluginOptions>? configureOptions = null)
    {
        // Configure plugin options
        if (configuration != null)
        {
            _ = services.Configure<DotComputePluginOptions>(options =>
            {
#pragma warning disable IL2026, IL3050 // Members annotated with trimming attributes
                configuration.GetSection("DotCompute:Plugins").Bind(options);
#pragma warning restore IL2026, IL3050
                configureOptions?.Invoke(options);
            });
        }
        else if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // Register plugin services - using consolidated implementation
        services.TryAddSingleton<IPluginServiceProvider, ConsolidatedPluginServiceProvider>();
        services.TryAddSingleton<IPluginDependencyResolver, PluginDependencyResolver>();
        services.TryAddSingleton<IPluginLifecycleManager, PluginLifecycleManager>();
        services.TryAddTransient<IPluginFactory, DefaultPluginFactory>();

        // Register algorithm plugin manager
        // Commented out - this interface doesn't exist in Abstractions
        // services.TryAddSingleton<IAlgorithmPluginManager, AlgorithmPluginManager>();

        return services;
    }

    /// <summary>
    /// Add advanced memory management features
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure memory options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAdvancedMemoryManagement(
        this IServiceCollection services,
        Action<AdvancedMemoryOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // These interfaces don't exist in Abstractions - commented out
        // services.TryAddSingleton<IMemoryCoherenceManager, MemoryCoherenceManager>();
        // services.TryAddSingleton<IDeviceBufferPoolManager, DeviceBufferPoolManager>();
        // services.TryAddSingleton<IP2PTransferService, P2PTransferService>();
        // services.TryAddTransient<IMemoryOptimizationService, MemoryOptimizationService>();

        return services;
    }

    /// <summary>
    /// Add performance monitoring and profiling services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure profiling options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPerformanceMonitoring(
        this IServiceCollection services,
        Action<PerformanceMonitoringOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            _ = services.Configure(configureOptions);
        }

        // These interfaces don't exist in Abstractions - commented out
        // services.TryAddSingleton<IPerformanceProfiler, PerformanceProfiler>();
        // services.TryAddSingleton<IDeviceMetricsCollector, DeviceMetricsCollector>();
        // services.TryAddTransient<IKernelProfiler, KernelProfiler>();
        // services.TryAddTransient<IBenchmarkRunner, BenchmarkRunner>();

        return services;
    }

    /// <summary>
    /// Add all DotCompute services with full feature set
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration for all services</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeComplete(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        _ = services.AddDotComputeRuntime(configuration);
        _ = services.AddDotComputePlugins(configuration);
        _ = services.AddAdvancedMemoryManagement();
        _ = services.AddPerformanceMonitoring();

        return services;
    }
}
