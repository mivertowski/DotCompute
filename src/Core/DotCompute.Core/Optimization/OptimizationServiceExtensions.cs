// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Telemetry;
using DotCompute.Core.Optimization.Configuration;
using DotCompute.Core.Optimization.Performance;

namespace DotCompute.Core.Optimization;

/// <summary>
/// Extension methods for registering performance optimization services.
/// </summary>
public static class OptimizationServiceExtensions
{
    /// <summary>
    /// Adds performance optimization services to the service collection.
    /// This includes adaptive backend selection and performance profiling.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Optional configuration for optimization options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPerformanceOptimization(
        this IServiceCollection services,
        Action<PerformanceOptimizationOptions>? configureOptions = null)
    {
        // Register optimization options
        if (configureOptions != null)
        {
            services.Configure<PerformanceOptimizationOptions>(configureOptions);
        }

        // Register adaptive selection options
        services.Configure<AdaptiveSelectionOptions>(options =>
        {
            options.EnableLearning = true;
            options.MinConfidenceThreshold = 0.6f;
            options.MaxHistoryEntries = 1000;
            options.MinHistoryForLearning = 5;
            options.PerformanceUpdateIntervalSeconds = 10;
        });

        // Register performance profiler (if not already registered)
        services.TryAddSingleton<PerformanceProfiler>();

        // Register adaptive backend selector
        services.TryAddSingleton<AdaptiveBackendSelector>();

        return services;
    }

    /// <summary>
    /// Adds the performance-optimized orchestrator as a decorator for the existing orchestrator.
    /// This should be called after AddDotComputeRuntime().
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Optional configuration for optimization options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddPerformanceOptimizedOrchestrator(
        this IServiceCollection services,
        Action<PerformanceOptimizationOptions>? configureOptions = null)
    {
        // Add performance optimization services
        services.AddPerformanceOptimization(configureOptions);

        // Decorate the existing orchestrator with performance optimization
        services.Decorate<IComputeOrchestrator>((inner, provider) =>
        {
            var backendSelector = provider.GetRequiredService<AdaptiveBackendSelector>();
            var performanceProfiler = provider.GetRequiredService<PerformanceProfiler>();
            var logger = provider.GetRequiredService<ILogger<PerformanceOptimizedOrchestrator>>();
            var options = provider.GetService<IOptions<PerformanceOptimizationOptions>>()?.Value;

            return new PerformanceOptimizedOrchestrator(
                inner, backendSelector, performanceProfiler, logger, options);
        });

        return services;
    }

    /// <summary>
    /// Adds comprehensive performance optimization with all features enabled.
    /// This is a convenience method that combines all optimization services.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptimization">Optional configuration for optimization</param>
    /// <param name="configureSelection">Optional configuration for backend selection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddComprehensiveOptimization(
        this IServiceCollection services,
        Action<PerformanceOptimizationOptions>? configureOptimization = null,
        Action<AdaptiveSelectionOptions>? configureSelection = null)
    {
        // Configure adaptive selection options
        if (configureSelection != null)
        {
            services.Configure<AdaptiveSelectionOptions>(configureSelection);
        }

        return services.AddPerformanceOptimizedOrchestrator(configureOptimization);
    }

    /// <summary>
    /// Adds production-optimized performance settings.
    /// Balances performance gains with stability and resource usage.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddProductionOptimization(this IServiceCollection services)
    {
        return services.AddComprehensiveOptimization(
            optimization =>
            {
                optimization.OptimizationStrategy = OptimizationStrategy.Balanced;
                optimization.EnableLearning = true;
                optimization.EnableConstraints = true;
                optimization.EnablePreExecutionOptimizations = true;
                optimization.EnableMemoryOptimization = true;
                optimization.EnableKernelOptimization = false; // Disable for stability
                optimization.EnableWarmupOptimization = false; // Disable for faster startup
                optimization.EnableDetailedProfiling = false; // Disable for performance
                optimization.MaxCpuUtilizationThreshold = 0.8;
                optimization.MaxMemoryUtilizationThreshold = 0.7;
            },
            selection =>
            {
                selection.EnableLearning = true;
                selection.MinConfidenceThreshold = 0.7f; // Higher confidence for production
                selection.MaxHistoryEntries = 500; // Moderate history
                selection.MinHistoryForLearning = 10; // More samples for production decisions
                selection.PerformanceUpdateIntervalSeconds = 30; // Less frequent updates
            });
    }

    /// <summary>
    /// Adds development-optimized performance settings.
    /// Prioritizes learning and detailed monitoring over performance.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDevelopmentOptimization(this IServiceCollection services)
    {
        return services.AddComprehensiveOptimization(
            optimization =>
            {
                optimization.OptimizationStrategy = OptimizationStrategy.Adaptive;
                optimization.EnableLearning = true;
                optimization.EnableConstraints = false; // Allow all backends for testing
                optimization.EnablePreExecutionOptimizations = true;
                optimization.EnableMemoryOptimization = true;
                optimization.EnableKernelOptimization = true;
                optimization.EnableWarmupOptimization = true;
                optimization.EnableDetailedProfiling = true; // Enable for development insights
                optimization.ProfilingSampleIntervalMs = 50; // More frequent sampling
                optimization.MaxCpuUtilizationThreshold = 0.95;
                optimization.MaxMemoryUtilizationThreshold = 0.9;
            },
            selection =>
            {
                selection.EnableLearning = true;
                selection.MinConfidenceThreshold = 0.5f; // Lower threshold for experimentation
                selection.MaxHistoryEntries = 2000; // More history for learning
                selection.MinHistoryForLearning = 3; // Faster learning
                selection.PerformanceUpdateIntervalSeconds = 5; // Frequent updates
            });
    }

    /// <summary>
    /// Adds high-performance optimization settings.
    /// Maximizes performance at the cost of some stability and resource usage.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHighPerformanceOptimization(this IServiceCollection services)
    {
        return services.AddComprehensiveOptimization(
            optimization =>
            {
                optimization.OptimizationStrategy = OptimizationStrategy.Aggressive;
                optimization.EnableLearning = true;
                optimization.EnableConstraints = true;
                optimization.EnablePreExecutionOptimizations = true;
                optimization.EnableMemoryOptimization = true;
                optimization.EnableKernelOptimization = true;
                optimization.EnableWarmupOptimization = true;
                optimization.EnableDetailedProfiling = false; // Disable for maximum performance
                optimization.MaxCpuUtilizationThreshold = 0.95;
                optimization.MaxMemoryUtilizationThreshold = 0.9;
                optimization.PreferredBackends = new List<string> { "CUDA", "Metal", "OpenCL" }; // Prefer GPU
            },
            selection =>
            {
                selection.EnableLearning = true;
                selection.MinConfidenceThreshold = 0.5f; // Lower threshold for more aggressive selection
                selection.MaxHistoryEntries = 1000;
                selection.MinHistoryForLearning = 5;
                selection.PerformanceUpdateIntervalSeconds = 5; // Frequent updates for quick adaptation
            });
    }

    /// <summary>
    /// Adds conservative optimization settings.
    /// Prioritizes stability and predictability over maximum performance.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddConservativeOptimization(this IServiceCollection services)
    {
        return services.AddComprehensiveOptimization(
            optimization =>
            {
                optimization.OptimizationStrategy = OptimizationStrategy.Conservative;
                optimization.EnableLearning = true;
                optimization.EnableConstraints = true;
                optimization.EnablePreExecutionOptimizations = false; // Disable for predictability
                optimization.EnableMemoryOptimization = false; // Disable for predictability
                optimization.EnableKernelOptimization = false; // Disable for stability
                optimization.EnableWarmupOptimization = false; // Disable for predictable startup
                optimization.EnableDetailedProfiling = false;
                optimization.MaxCpuUtilizationThreshold = 0.6;
                optimization.MaxMemoryUtilizationThreshold = 0.5;
                optimization.PreferredBackends = new List<string> { "CPU" }; // Prefer stable CPU backend
            },
            selection =>
            {
                selection.EnableLearning = true;
                selection.MinConfidenceThreshold = 0.8f; // High confidence requirement
                selection.MaxHistoryEntries = 100; // Limited history for stability
                selection.MinHistoryForLearning = 20; // More samples required
                selection.PerformanceUpdateIntervalSeconds = 60; // Less frequent updates
            });
    }

    /// <summary>
    /// Adds machine learning workload optimization.
    /// Optimized for ML/AI workloads with long-running kernels and large data sets.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMLWorkloadOptimization(this IServiceCollection services)
    {
        return services.AddComprehensiveOptimization(
            optimization =>
            {
                optimization.OptimizationStrategy = OptimizationStrategy.Adaptive;
                optimization.EnableLearning = true;
                optimization.EnableConstraints = true;
                optimization.EnablePreExecutionOptimizations = true;
                optimization.EnableMemoryOptimization = true; // Important for large datasets
                optimization.EnableKernelOptimization = true; // Important for compute-intensive kernels
                optimization.EnableWarmupOptimization = true; // Amortize over long runs
                optimization.EnableDetailedProfiling = true; // Important for ML optimization
                optimization.ProfilingSampleIntervalMs = 200; // Less frequent for long-running kernels
                optimization.MaxCpuUtilizationThreshold = 0.9;
                optimization.MaxMemoryUtilizationThreshold = 0.85; // Allow higher memory usage
                optimization.PreferredBackends = new List<string> { "CUDA", "Metal" }; // Prefer GPU for ML
            },
            selection =>
            {
                selection.EnableLearning = true;
                selection.MinConfidenceThreshold = 0.6f;
                selection.MaxHistoryEntries = 5000; // Large history for complex ML workloads
                selection.MinHistoryForLearning = 3; // Quick learning for iterative ML
                selection.PerformanceUpdateIntervalSeconds = 15;
            });
    }

    /// <summary>
    /// Gets performance insights from the adaptive backend selector.
    /// Useful for monitoring and debugging performance optimization.
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>Current performance insights</returns>
    public static PerformanceInsights GetPerformanceInsights(this IServiceProvider serviceProvider)
    {
        var backendSelector = serviceProvider.GetService<AdaptiveBackendSelector>();
        return backendSelector?.GetPerformanceInsights() ?? new PerformanceInsights
        {
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Simple service decorator helper for dependency injection.
/// </summary>
public static class ServiceCollectionDecoratorExtensions
{
    /// <summary>
    /// Decorates an existing service registration with a wrapper implementation.
    /// </summary>
    /// <typeparam name="TService">The service interface type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="decorator">Factory function that creates the decorator</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<TService, IServiceProvider, TService> decorator)
        where TService : class
    {
        // Find the existing service registration
        var existingDescriptor = services.LastOrDefault(x => x.ServiceType == typeof(TService));
        if (existingDescriptor == null)
        {
            throw new InvalidOperationException($"Service of type {typeof(TService).Name} is not registered.");
        }

        // Create a new descriptor that wraps the original
        var decoratedDescriptor = ServiceDescriptor.Describe(
            typeof(TService),
            provider =>
            {
                // Create the original service instance
                TService originalService;


                if (existingDescriptor.ImplementationInstance != null)
                {
                    originalService = (TService)existingDescriptor.ImplementationInstance;
                }
                else if (existingDescriptor.ImplementationFactory != null)
                {
                    originalService = (TService)existingDescriptor.ImplementationFactory(provider);
                }
                else if (existingDescriptor.ImplementationType != null)
                {
                    originalService = (TService)ActivatorUtilities.CreateInstance(provider, existingDescriptor.ImplementationType);
                }
                else
                {
                    throw new InvalidOperationException("Invalid service descriptor");
                }

                // Apply the decorator
                return decorator(originalService, provider);
            },
            existingDescriptor.Lifetime);

        // Replace the existing registration
        services.Remove(existingDescriptor);
        services.Add(decoratedDescriptor);

        return services;
    }
}