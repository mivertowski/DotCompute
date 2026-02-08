// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotCompute.Core.Observability;

/// <summary>
/// Extension methods for registering kernel execution instrumentation.
/// </summary>
public static class KernelExecutionInstrumentationExtensions
{
    /// <summary>
    /// Adds kernel execution instrumentation to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelExecutionInstrumentation(
        this IServiceCollection services,
        Action<KernelExecutionInstrumentationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Configure<KernelExecutionInstrumentationOptions>(options =>
        {
            configure?.Invoke(options);
        });

        services.TryAddSingleton<KernelExecutionInstrumentation>();

        return services;
    }

    /// <summary>
    /// Adds kernel execution instrumentation with specific options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The instrumentation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelExecutionInstrumentation(
        this IServiceCollection services,
        KernelExecutionInstrumentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.Configure<KernelExecutionInstrumentationOptions>(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.ServiceName = options.ServiceName;
            opt.ServiceVersion = options.ServiceVersion;
            opt.EnableStructuredLogging = options.EnableStructuredLogging;
            opt.TraceSamplingRate = options.TraceSamplingRate;
            opt.RecordMemoryTransfers = options.RecordMemoryTransfers;
            opt.RecordProblemSizes = options.RecordProblemSizes;
        });

        services.TryAddSingleton<KernelExecutionInstrumentation>();

        return services;
    }

    /// <summary>
    /// Adds kernel execution instrumentation with default production settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelExecutionInstrumentationProduction(
        this IServiceCollection services)
    {
        return services.AddKernelExecutionInstrumentation(options =>
        {
            options.Enabled = true;
            options.TraceSamplingRate = 0.1; // Sample 10% of traces in production
            options.RecordMemoryTransfers = true;
            options.RecordProblemSizes = true;
        });
    }
}
