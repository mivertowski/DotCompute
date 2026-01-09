// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotCompute.Core.Resilience;

/// <summary>
/// Extension methods for registering kernel circuit breaker services.
/// </summary>
public static class KernelCircuitBreakerServiceExtensions
{
    /// <summary>
    /// Adds the kernel circuit breaker to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurePolicy">Optional policy configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelCircuitBreaker(
        this IServiceCollection services,
        Action<KernelCircuitBreakerPolicy>? configurePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var policy = KernelCircuitBreakerPolicy.Default;
        configurePolicy?.Invoke(policy);

        services.TryAddSingleton(policy);
        services.TryAddSingleton<IKernelCircuitBreaker, KernelCircuitBreaker>();

        return services;
    }

    /// <summary>
    /// Adds the kernel circuit breaker with a specific policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The circuit breaker policy.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelCircuitBreaker(
        this IServiceCollection services,
        KernelCircuitBreakerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);

        services.TryAddSingleton(policy);
        services.TryAddSingleton<IKernelCircuitBreaker, KernelCircuitBreaker>();

        return services;
    }

    /// <summary>
    /// Adds the kernel circuit breaker with strict policy (lower failure thresholds).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelCircuitBreakerStrict(this IServiceCollection services)
    {
        return services.AddKernelCircuitBreaker(KernelCircuitBreakerPolicy.Strict);
    }

    /// <summary>
    /// Adds the kernel circuit breaker with lenient policy (higher failure thresholds).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKernelCircuitBreakerLenient(this IServiceCollection services)
    {
        return services.AddKernelCircuitBreaker(KernelCircuitBreakerPolicy.Lenient);
    }
}
