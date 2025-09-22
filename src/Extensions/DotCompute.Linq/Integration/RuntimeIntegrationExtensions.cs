// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Linq.Extensions;
using Microsoft.Extensions.DependencyInjection;
namespace DotCompute.Linq.Integration;
{
/// <summary>
/// Extension methods for integrating LINQ with the DotCompute runtime.
/// This class provides the bridge between LINQ and runtime services without circular dependencies.
/// </summary>
public static class RuntimeIntegrationExtensions
{
    /// <summary>
    /// Adds DotCompute LINQ services that require runtime integration.
    /// This method should be called after AddDotComputeRuntime().
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Optional action to configure LINQ options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotComputeLinqWithRuntime(
        {
        this IServiceCollection services,
        Action<LinqServiceOptions>? configureOptions = null)
    {
        // First add basic LINQ services
        services.AddDotComputeLinq(configureOptions);
        // The IComputeOrchestrator should already be registered by the runtime
        // This method just ensures the LINQ provider can access it
        return services;
    }
    /// Validates that all required services for LINQ runtime integration are available.
    /// <param name="serviceProvider">The service provider to validate</param>
    /// <returns>True if all required services are available</returns>
    public static bool ValidateLinqRuntimeIntegration(this IServiceProvider serviceProvider)
        try
        {
            var orchestrator = serviceProvider.GetService<IComputeOrchestrator>();
            var linqProvider = serviceProvider.GetService<DotCompute.Linq.Interfaces.IComputeLinqProvider>();
            return orchestrator != null && linqProvider != null;
        }
        catch
            return false;
    /// Gets the LINQ provider with runtime integration verification.
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The compute LINQ provider</returns>
    /// <exception cref="InvalidOperationException">Thrown when runtime integration is not properly configured</exception>
    public static DotCompute.Linq.Interfaces.IComputeLinqProvider GetComputeLinqProviderWithRuntime(
        this IServiceProvider serviceProvider)
        if (!serviceProvider.ValidateLinqRuntimeIntegration())
            throw new InvalidOperationException(
                "LINQ runtime integration is not properly configured. " +
                "Ensure AddDotComputeRuntime() is called before AddDotComputeLinqWithRuntime().");
        return serviceProvider.GetRequiredService<DotCompute.Linq.Interfaces.IComputeLinqProvider>();
}
