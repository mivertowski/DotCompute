// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.DependencyInjection;
using DotCompute.Runtime.DependencyInjection.Core;
using DotCompute.Runtime.Factories;
using DotCompute.Runtime.Services;
using DotCompute.Runtime.Services.Implementation;
using DotCompute.Runtime.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime;


/// <summary>
/// Extension methods for registering DotCompute Runtime services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add comprehensive DotCompute Runtime services to the service collection.
    /// This is the ONLY AddDotComputeRuntime() method you should use - it registers ALL necessary services.
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
        // ===== CONFIGURATION SETUP =====
        // Configure all runtime options (merged from both implementations)
        if (configuration != null)
        {
            _ = services.Configure<DotComputeRuntimeOptions>(options =>
            {
#pragma warning disable IL2026, IL3050 // Members annotated with trimming attributes
                configuration.GetSection("DotCompute").Bind(options);
#pragma warning restore IL2026, IL3050
                configureOptions?.Invoke(options);
            });

            // Additional configuration sections (from Extensions version)
            _ = services.Configure<DotComputePluginOptions>(
                configuration.GetSection(DotComputePluginOptions.SectionName));
            _ = services.Configure<AdvancedMemoryOptions>(
                configuration.GetSection("DotCompute:Memory"));
            _ = services.Configure<PerformanceMonitoringOptions>(
                configuration.GetSection("DotCompute:Performance"));
        }
        else
        {
            // Add default options if no configuration provided
            if (configureOptions != null)
            {
                _ = services.Configure(configureOptions);
            }
            else
            {
                _ = services.Configure<DotComputeRuntimeOptions>(options => { });
            }

            _ = services.Configure<DotComputePluginOptions>(options => { });
            _ = services.Configure<AdvancedMemoryOptions>(options => { });
            _ = services.Configure<PerformanceMonitoringOptions>(options => { });
        }

        // ===== CORE SERVICES =====
        _ = services.AddLogging();

        // ===== ACCELERATOR FACTORY AND RUNTIME =====
        // Register accelerator factory for proper DI-based creation
        services.TryAddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

        // Register the main runtime service
        services.TryAddSingleton<AcceleratorRuntime>();

        // ===== MEMORY SERVICES =====
        services.TryAddTransient<DotCompute.Runtime.Services.IMemoryPoolService, MemoryPoolService>();
        services.TryAddTransient<DotCompute.Runtime.Services.IUnifiedMemoryService, UnifiedMemoryService>();

        // ===== KERNEL SERVICES (from Extensions version - CRITICAL!) =====
        // Register kernel discovery service (required for [Kernel] attribute support)
        _ = services.AddSingleton<GeneratedKernelDiscoveryService>();

        // Register kernel compiler, cache, and profiler
        _ = services.AddSingleton<IUnifiedKernelCompiler, DefaultKernelCompiler>();
        _ = services.AddSingleton<IKernelCache, MemoryKernelCache>();
        _ = services.AddSingleton<IKernelProfiler, DefaultKernelProfiler>();

        // Register kernel execution service (implements IComputeOrchestrator)
        _ = services.AddSingleton<KernelExecutionService>();

        // ===== COMPUTE ORCHESTRATOR (THE KEY SERVICE!) =====
        _ = services.AddSingleton<Abstractions.Interfaces.IComputeOrchestrator>(provider =>
            provider.GetRequiredService<KernelExecutionService>());

        // ===== CONFIGURATION VALIDATORS =====
        services.TryAddSingleton<IValidateOptions<DotComputeRuntimeOptions>, RuntimeOptionsValidator>();

        // ===== ACCELERATOR PROVIDERS (CRITICAL FOR ACCELERATOR CREATION!) =====
        // Register providers for each backend to enable accelerator instantiation
        // Without these, the factory can discover devices but cannot create accelerators!
        services.TryAddSingleton<IAcceleratorProvider, Providers.CudaAcceleratorProvider>();
        services.TryAddSingleton<IAcceleratorProvider, Providers.CpuAcceleratorProvider>();
        services.TryAddSingleton<IAcceleratorProvider, Providers.OpenCLAcceleratorProvider>();
        services.TryAddSingleton<IAcceleratorProvider, Providers.MetalAcceleratorProvider>();

        // ===== INITIALIZATION SERVICE =====
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
