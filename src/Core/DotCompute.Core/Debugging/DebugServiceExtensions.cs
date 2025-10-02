// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Extension methods for registering debugging services with dependency injection.
/// </summary>
public static class DebugServiceExtensions
{
    /// <summary>
    /// Adds kernel debugging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Optional configuration for debugging options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddKernelDebugging(
        this IServiceCollection services,
        Action<DebugServiceOptions>? configureOptions = null)
    {
        // Register the debug service
        services.TryAddSingleton<IKernelDebugService, KernelDebugService>();

        // Configure debug service options
        if (configureOptions != null)
        {
            _ = services.Configure<DebugServiceOptions>(configureOptions);
        }

        return services;
    }

    /// <summary>
    /// Wraps the existing compute orchestrator with debugging capabilities.
    /// This should be called after AddDotComputeRuntime() to enhance the orchestrator.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureOptions">Optional configuration for debug execution options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDebugIntegratedOrchestrator(
        this IServiceCollection services,
        Action<DebugExecutionOptions>? configureOptions = null)
    {
        // Create execution options
        var options = new DebugExecutionOptions();
        configureOptions?.Invoke(options);

        // Wrap the existing orchestrator with debug capabilities
        _ = services.Decorate<IComputeOrchestrator>((inner, provider) =>
        {
            var debugService = provider.GetRequiredService<IKernelDebugService>();
            var logger = provider.GetRequiredService<ILogger<DebugIntegratedOrchestrator>>();
            return new DebugIntegratedOrchestrator(inner, debugService, logger, options);
        });

        return services;
    }

    /// <summary>
    /// Adds comprehensive kernel debugging with all features enabled.
    /// This is a convenience method that combines AddKernelDebugging() and AddDebugIntegratedOrchestrator().
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configureDebugService">Optional configuration for debug service</param>
    /// <param name="configureExecution">Optional configuration for debug execution</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddComprehensiveDebugging(
        this IServiceCollection services,
        Action<DebugServiceOptions>? configureDebugService = null,
        Action<DebugExecutionOptions>? configureExecution = null)
    {
        return services
            .AddKernelDebugging(configureDebugService)
            .AddDebugIntegratedOrchestrator(configureExecution);
    }

    /// <summary>
    /// Adds production-safe debugging with conservative settings.
    /// Enables performance monitoring and error analysis without expensive validations.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddProductionDebugging(this IServiceCollection services)
    {
        return services.AddComprehensiveDebugging(
            debugService =>

            {
                debugService.VerbosityLevel = (DotCompute.Abstractions.Debugging.Types.LogLevel)LogLevel.Warning;
                debugService.EnableProfiling = true;
                debugService.EnableMemoryAnalysis = false;
                debugService.SaveExecutionLogs = false;
                debugService.ExecutionTimeout = TimeSpan.FromSeconds(10);
            },
            execution =>
            {
                execution.EnableDebugHooks = true;
                execution.ValidateBeforeExecution = false;
                execution.ValidateAfterExecution = false;
                execution.EnableCrossBackendValidation = false;
                execution.EnablePerformanceMonitoring = true;
                execution.TestDeterminism = false;
                execution.AnalyzeErrorsOnFailure = true;
                execution.StorePerformanceHistory = true;
            });
    }

    /// <summary>
    /// Adds development debugging with comprehensive validation and analysis.
    /// Includes cross-backend validation, determinism testing, and detailed logging.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDevelopmentDebugging(this IServiceCollection services)
    {
        return services.AddComprehensiveDebugging(
            debugService =>
            {
                debugService.VerbosityLevel = (DotCompute.Abstractions.Debugging.Types.LogLevel)LogLevel.Debug;
                debugService.EnableProfiling = true;
                debugService.EnableMemoryAnalysis = true;
                debugService.SaveExecutionLogs = true;
                debugService.ExecutionTimeout = TimeSpan.FromMinutes(2);
            },
            execution =>
            {
                execution.EnableDebugHooks = true;
                execution.ValidateBeforeExecution = true;
                execution.ValidateAfterExecution = true;
                execution.EnableCrossBackendValidation = true;
                execution.CrossValidationProbability = 0.2; // 20% of executions
                execution.ValidationTolerance = 1e-6f;
                execution.EnablePerformanceMonitoring = true;
                execution.TestDeterminism = true;
                execution.AnalyzeErrorsOnFailure = true;
                execution.StorePerformanceHistory = true;
            });
    }

    /// <summary>
    /// Adds testing debugging with maximum validation and analysis.
    /// Validates every execution across all backends for comprehensive testing.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddTestingDebugging(this IServiceCollection services)
    {
        return services.AddComprehensiveDebugging(
            debugService =>
            {
                debugService.VerbosityLevel = (DotCompute.Abstractions.Debugging.Types.LogLevel)LogLevel.Trace;
                debugService.EnableProfiling = true;
                debugService.EnableMemoryAnalysis = true;
                debugService.SaveExecutionLogs = true;
                debugService.ExecutionTimeout = TimeSpan.FromMinutes(5);
                debugService.MaxConcurrentExecutions = 1; // Sequential for debugging
            },
            execution =>
            {
                execution.EnableDebugHooks = true;
                execution.ValidateBeforeExecution = true;
                execution.ValidateAfterExecution = true;
                execution.EnableCrossBackendValidation = true;
                execution.CrossValidationProbability = 1.0; // 100% validation
                execution.ValidationTolerance = 1e-8f; // Strict tolerance
                execution.EnablePerformanceMonitoring = true;
                execution.TestDeterminism = true;
                execution.AnalyzeErrorsOnFailure = true;
                execution.StorePerformanceHistory = true;
                execution.FailOnValidationErrors = true; // Fail fast for testing
            });
    }
}

// Note: The Decorate extension method used above is from Scrutor package
// If not available, here's a simple implementation:

/// <summary>
/// Simple service decorator extensions for dependency injection.
/// </summary>
public static class ServiceDecoratorExtensions
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
        _ = services.Remove(existingDescriptor);
        services.Add(decoratedDescriptor);

        return services;
    }
}