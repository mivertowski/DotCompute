// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Extensions;

/// <summary>
/// Extension methods for registering DotCompute Runtime services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add DotCompute Runtime services to the service collection
    /// </summary>
    public static IServiceCollection AddDotComputeRuntime(this IServiceCollection services)
    {
        services.AddSingleton<AcceleratorRuntime>();
        services.AddLogging();
        
        return services;
    }
}