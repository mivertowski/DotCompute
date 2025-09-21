// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Pipelines.Examples.Services;
using DotCompute.Core.Pipelines.Examples.Models;

namespace DotCompute.Core.Pipelines.Examples.Configuration;

/// <summary>
/// Extension methods for configuring kernel chain example services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures services for kernel chain examples in dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    public static void ConfigureKernelChainExampleServices(this IServiceCollection services)
    {
        // Add DotCompute runtime with kernel chaining
        // services.AddDotComputeWithKernelChaining();
        // This would be called in the application's Startup/Program.cs file
        // where DotCompute.Runtime.Extensions is available

        // Add application-specific services that use kernel chains
        services.AddScoped<ImageProcessingService>();
        services.AddScoped<DataAnalysisService>();
        services.AddSingleton<MLInferenceService>();
    }

    /// <summary>
    /// Configures the application with kernel chain support.
    /// </summary>
    /// <param name="app">The host application</param>
    /// <returns>Async task for configuration completion</returns>
    public static Task ConfigureKernelChainApplication(this IHost app)
    {
        // Initialize DotCompute runtime with kernel chaining support
        // await app.Services.InitializeDotComputeWithKernelChainingAsync();
        // This would be called in the application's startup where DotCompute.Runtime.Extensions is available

        // Verify kernel chain system is working
        var diagnostics = KernelChain.GetDiagnostics();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("Kernel chain diagnostics: {@Diagnostics}", diagnostics);

        if (diagnostics.ContainsKey("IsConfigured") && (bool)diagnostics["IsConfigured"])
        {
            logger.LogInformation("Kernel chaining system initialized successfully");
        }
        else
        {
            logger.LogError("Kernel chaining system initialization failed");
        }

        return Task.CompletedTask;
    }
}
