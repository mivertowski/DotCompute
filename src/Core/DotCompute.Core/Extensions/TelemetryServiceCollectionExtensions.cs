// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Abstractions.Telemetry;
using DotCompute.Core.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotCompute.Core.Extensions;

/// <summary>
/// Extension methods for registering kernel telemetry services with dependency injection.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers kernel telemetry services as a singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Registers <see cref="IKernelTelemetryProvider"/> with the production
    /// implementation <see cref="KernelTelemetryCollector"/>.
    /// The telemetry collector is registered as a singleton to maintain
    /// metrics across the application lifetime.
    /// </remarks>
    public static IServiceCollection AddKernelTelemetry(this IServiceCollection services)
    {
        services.TryAddSingleton<IKernelTelemetryProvider, KernelTelemetryCollector>();
        return services;
    }

    /// <summary>
    /// Registers a custom kernel telemetry provider implementation.
    /// </summary>
    /// <typeparam name="TProvider">The custom telemetry provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Use this method to register a custom telemetry provider implementation
    /// that implements <see cref="IKernelTelemetryProvider"/>.
    /// The custom provider will replace the default <see cref="KernelTelemetryCollector"/>.
    /// </remarks>
    public static IServiceCollection AddKernelTelemetry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services)
        where TProvider : class, IKernelTelemetryProvider
    {
        services.Replace(ServiceDescriptor.Singleton<IKernelTelemetryProvider, TProvider>());
        return services;
    }

    /// <summary>
    /// Registers a custom kernel telemetry provider using a factory function.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="implementationFactory">The factory function to create the telemetry provider.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Use this method when you need to provide custom initialization logic
    /// for the telemetry provider, such as configuring storage backends or
    /// integration with external telemetry systems.
    /// </remarks>
    public static IServiceCollection AddKernelTelemetry(
        this IServiceCollection services,
        Func<IServiceProvider, IKernelTelemetryProvider> implementationFactory)
    {
        services.Replace(ServiceDescriptor.Singleton(implementationFactory));
        return services;
    }
}
