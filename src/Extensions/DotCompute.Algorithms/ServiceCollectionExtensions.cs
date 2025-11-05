// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Algorithms.LinearAlgebra.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotCompute.Algorithms;

/// <summary>
/// Extension methods for registering DotCompute.Algorithms services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds DotCompute Algorithms services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddDotComputeAlgorithms(this IServiceCollection services)
    {
        // Register Linear Algebra components
        services.TryAddTransient<GpuMatrixOperations>();

        // Note: Additional algorithm services can be registered here as they are developed
        // - Plugin loader services
        // - Algorithm validators
        // - Performance profilers

        return services;
    }

    /// <summary>
    /// Adds Linear Algebra components to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddLinearAlgebra(this IServiceCollection services)
    {
        services.TryAddTransient<GpuMatrixOperations>();

        return services;
    }
}
