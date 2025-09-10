// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Linq.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotCompute.Examples;

/// <summary>
/// Example demonstrating how to configure DotCompute LINQ pipeline services
/// in ASP.NET Core or generic host applications using dependency injection.
/// </summary>
public class PipelineServiceCollectionExample
{
    /// <summary>
    /// Example: Basic pipeline services registration for ASP.NET Core applications.
    /// </summary>
    public static void ConfigureBasicPipelineServices(IServiceCollection services)
    {
        // Add basic DotCompute LINQ services first
        services.AddDotComputeLinq();

        // Add pipeline services with default configuration
        services.AddPipelineServices();
    }

    /// <summary>
    /// Example: Advanced pipeline services configuration with custom options.
    /// </summary>
    public static void ConfigureAdvancedPipelineServices(IServiceCollection services)
    {
        // Add basic LINQ services
        services.AddDotComputeLinq(options =>
        {
            options.EnableCaching = true;
            options.EnableOptimization = true;
            options.EnableProfiling = true;
        });

        // Add pipeline services with custom configuration
        services.AddPipelineServices(options =>
        {
            options.EnableAdvancedOptimization = true;
            options.EnableTelemetry = true;
            options.MaxPipelineStages = 15;
            options.DefaultTimeoutSeconds = 600;
            options.MaxMemoryUsageMB = 2048;
        });

        // Add pipeline optimization services
        services.AddPipelineOptimization(options =>
        {
            options.Strategy = OptimizationStrategy.Aggressive;
            options.EnableMachineLearning = false; // Disable ML for production stability
            options.EnableKernelFusion = true;
            options.EnableMemoryOptimization = true;
            options.CachePolicy = CachePolicy.Memory;
        });

        // Add telemetry for monitoring
        services.AddPipelineTelemetry(options =>
        {
            options.EnableDetailedMetrics = true;
            options.EnableMemoryTracking = true;
            options.EnableBackendMonitoring = true;
            options.EnableBottleneckIdentification = true;
            options.CollectionIntervalSeconds = 15;
        });
    }

    /// <summary>
    /// Example: Complete pipeline services configuration with all features.
    /// </summary>
    public static void ConfigureCompletePipelineServices(IServiceCollection services)
    {
        // Add basic LINQ services
        services.AddDotComputeLinq();

        // Add all pipeline services in one call
        services.AddCompletePipelineServices(options =>
        {
            // Pipeline service options
            options.Pipelines.EnableAdvancedOptimization = true;
            options.Pipelines.EnableTelemetry = true;
            options.Pipelines.MaxPipelineStages = 20;
            options.Pipelines.DefaultTimeoutSeconds = 300;

            // LINQ provider options
            options.Provider.EnableExpressionOptimization = true;
            options.Provider.EnableIntelligentBackendSelection = true;
            options.Provider.EnableAutomaticKernelFusion = true;
            options.Provider.DefaultStrategy = OptimizationStrategy.Balanced;
            options.Provider.GpuComplexityThreshold = 8;

            // Optimization options
            options.Optimization.Strategy = OptimizationStrategy.Adaptive;
            options.Optimization.EnableMachineLearning = true;
            options.Optimization.EnableKernelFusion = true;
            options.Optimization.EnableQueryPlanOptimization = true;

            // Telemetry options
            options.Telemetry.EnableDetailedMetrics = true;
            options.Telemetry.EnableExecutionTraces = false; // Disabled for performance
            options.Telemetry.EnableMemoryTracking = true;
            options.Telemetry.EnableQueryPatternAnalysis = true;
        });
    }

    /// <summary>
    /// Example: Production-ready configuration for high-performance applications.
    /// </summary>
    public static void ConfigureProductionPipelineServices(IServiceCollection services)
    {
        // Add LINQ services optimized for production
        services.AddDotComputeLinq(options =>
        {
            options.EnableCaching = true;
            options.CacheMaxEntries = 5000;
            options.EnableOptimization = true;
            options.EnableProfiling = false; // Disabled in production
            options.EnableCpuFallback = true;
        });

        // Add production-optimized pipeline services
        services.AddPipelineServices(options =>
        {
            options.EnableAdvancedOptimization = true;
            options.EnableTelemetry = false; // Minimal telemetry in production
            options.MaxPipelineStages = 25;
            options.DefaultTimeoutSeconds = 120;
            options.MaxMemoryUsageMB = 4096;
        });

        // Conservative optimization for stability
        services.AddPipelineOptimization(options =>
        {
            options.Strategy = OptimizationStrategy.Balanced;
            options.EnableMachineLearning = false; // Disabled for predictability
            options.EnableKernelFusion = true;
            options.EnableMemoryOptimization = true;
            options.CachePolicy = CachePolicy.Memory;
            options.ExecutionPriority = ExecutionPriority.High;
        });

        // Minimal telemetry for performance monitoring
        services.AddPipelineTelemetry(options =>
        {
            options.EnableDetailedMetrics = false;
            options.EnableExecutionTraces = false;
            options.EnableMemoryTracking = true;
            options.EnableBackendMonitoring = true;
            options.EnableQueryPatternAnalysis = false;
            options.EnableBottleneckIdentification = true;
            options.CollectionIntervalSeconds = 60;
            options.MaxBufferSize = 1000;
        });
    }

    /// <summary>
    /// Example: Development configuration with extensive debugging and telemetry.
    /// </summary>
    public static void ConfigureDevelopmentPipelineServices(IServiceCollection services)
    {
        // Add LINQ services with debugging enabled
        services.AddDotComputeLinq(options =>
        {
            options.EnableCaching = true;
            options.EnableOptimization = true;
            options.EnableProfiling = true; // Enabled for debugging
        });

        // Development pipeline configuration
        services.AddPipelineServices(options =>
        {
            options.EnableAdvancedOptimization = true;
            options.EnableTelemetry = true;
            options.MaxPipelineStages = 10;
            options.DefaultTimeoutSeconds = 600; // Higher timeout for debugging
        });

        // Aggressive optimization for testing
        services.AddPipelineOptimization(options =>
        {
            options.Strategy = OptimizationStrategy.Aggressive;
            options.EnableMachineLearning = true; // Enabled for testing
            options.EnableKernelFusion = true;
            options.EnableMemoryOptimization = true;
            options.EnableQueryPlanOptimization = true;
        });

        // Comprehensive telemetry for debugging
        services.AddPipelineTelemetry(options =>
        {
            options.EnableDetailedMetrics = true;
            options.EnableExecutionTraces = true;
            options.EnableMemoryTracking = true;
            options.EnableBackendMonitoring = true;
            options.EnableQueryPatternAnalysis = true;
            options.EnableBottleneckIdentification = true;
            options.CollectionIntervalSeconds = 5; // Frequent collection
            options.MaxBufferSize = 50000;
        });
    }
}

/// <summary>
/// Example host application demonstrating pipeline service usage.
/// </summary>
public class PipelineServiceHost
{
    /// <summary>
    /// Creates a host builder with pipeline services configured.
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add DotCompute runtime services
                // services.AddDotComputeRuntime(); // Requires runtime setup

                // Configure pipeline services based on environment
                if (context.HostingEnvironment.IsDevelopment())
                {
                    PipelineServiceCollectionExample.ConfigureDevelopmentPipelineServices(services);
                }
                else
                {
                    PipelineServiceCollectionExample.ConfigureProductionPipelineServices(services);
                }
            });

    /// <summary>
    /// Example service that uses the pipeline services.
    /// </summary>
    public class ExamplePipelineService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExamplePipelineService> _logger;

        public ExamplePipelineService(
            IServiceProvider serviceProvider,
            ILogger<ExamplePipelineService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Example method demonstrating pipeline service usage.
        /// </summary>
        public async Task<double[]> ProcessDataAsync(float[] inputData)
        {
            _logger.LogInformation("Processing {Count} data points with pipeline services", inputData.Length);

            try
            {
                // Get the LINQ provider from DI
                var linqProvider = _serviceProvider.GetComputeLinqProvider();

                // Create a compute queryable
                var queryable = linqProvider.CreateQueryable(inputData);

                // Execute a complex LINQ query using pipeline optimization
                var results = await queryable
                    .Where(x => x > 0)
                    .Select(x => Math.Sqrt(x))
                    .Where(x => x < 100)
                    .Select(x => x * 2.0)
                    .ToArrayAsync();

                _logger.LogInformation("Successfully processed data, got {Count} results", results.Length);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process data with pipeline services");
                throw;
            }
        }
    }
}

/// <summary>
/// Example ASP.NET Core startup configuration.
/// </summary>
public class PipelineStartupExample
{
    /// <summary>
    /// Configure services for ASP.NET Core application.
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // Add framework services
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        // Add DotCompute pipeline services
        PipelineServiceCollectionExample.ConfigureAdvancedPipelineServices(services);

        // Add application services
        services.AddScoped<PipelineServiceHost.ExamplePipelineService>();
    }

    /// <summary>
    /// Example controller demonstrating pipeline service usage.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    public class ComputeController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly PipelineServiceHost.ExamplePipelineService _pipelineService;

        public ComputeController(PipelineServiceHost.ExamplePipelineService pipelineService)
        {
            _pipelineService = pipelineService;
        }

        [Microsoft.AspNetCore.Mvc.HttpPost("process")]
        public async Task<Microsoft.AspNetCore.Mvc.ActionResult<double[]>> ProcessData([Microsoft.AspNetCore.Mvc.FromBody] float[] data)
        {
            var results = await _pipelineService.ProcessDataAsync(data);
            return Ok(results);
        }
    }
}