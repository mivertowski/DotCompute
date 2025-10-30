// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines.Examples.Models;
using DotCompute.Core.Pipelines.Examples.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Pipelines.Examples.Configuration;

/// <summary>
/// Extension methods for configuring kernel chain example services.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 15001, Level = MsLogLevel.Information, Message = "Kernel chain diagnostics: {@Diagnostics}")]
    private static partial void LogKernelChainDiagnostics(ILogger logger, object diagnostics);

    [LoggerMessage(EventId = 15002, Level = MsLogLevel.Information, Message = "Kernel chaining system initialized successfully")]
    private static partial void LogKernelChainInitialized(ILogger logger);

    [LoggerMessage(EventId = 15003, Level = MsLogLevel.Error, Message = "Kernel chaining system initialization failed")]
    private static partial void LogKernelChainInitFailed(ILogger logger);

    #endregion

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
        _ = services.AddScoped<ImageProcessingService>();
        _ = services.AddScoped<DataAnalysisService>();
        _ = services.AddSingleton<MLInferenceService>();
    }

    /// <summary>
    /// Configures the application with kernel chain support.
    /// </summary>
    /// <param name="app">The host application</param>
    /// <returns>Async task for configuration completion</returns>
    public static Task ConfigureKernelChainApplicationAsync(this IHost app)
    {
        // Initialize DotCompute runtime with kernel chaining support
        // await app.Services.InitializeDotComputeWithKernelChainingAsync();
        // This would be called in the application's startup where DotCompute.Runtime.Extensions is available

        // Verify kernel chain system is working
        var diagnostics = KernelChain.GetDiagnostics();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        LogKernelChainDiagnostics(logger, diagnostics);

        if (diagnostics.ContainsKey("IsConfigured") && (bool)diagnostics["IsConfigured"])
        {
            LogKernelChainInitialized(logger);
        }
        else
        {
            LogKernelChainInitFailed(logger);
        }

        return Task.CompletedTask;
    }
}
