# DotCompute LINQ Pipeline Services - Dependency Injection Guide

This guide demonstrates how to configure and use DotCompute LINQ pipeline services with Microsoft.Extensions.DependencyInjection in ASP.NET Core and generic host applications.

## Overview

The DotCompute LINQ pipeline system provides advanced query optimization, intelligent backend selection, and comprehensive telemetry for high-performance compute workloads. The service collection extensions make it easy to integrate these capabilities into any .NET application using dependency injection.

## Quick Start

### Basic Configuration

```csharp
using DotCompute.Linq.Extensions;
using Microsoft.Extensions.DependencyInjection;

// Add basic LINQ and pipeline services
services.AddDotComputeLinq()
        .AddPipelineServices();
```

### ASP.NET Core Integration

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // Framework services
    services.AddControllers();
    
    // DotCompute services
    services.AddDotComputeLinq(options =>
    {
        options.EnableCaching = true;
        options.EnableOptimization = true;
    })
    .AddCompletePipelineServices(options =>
    {
        options.Pipelines.EnableAdvancedOptimization = true;
        options.Optimization.Strategy = OptimizationStrategy.Balanced;
        options.Telemetry.EnableDetailedMetrics = true;
    });
}
```

## Service Collection Extension Methods

### Core Methods

#### `AddDotComputeLinq()`
Registers basic LINQ services for query compilation and execution.

```csharp
services.AddDotComputeLinq(options =>
{
    options.EnableCaching = true;
    options.CacheMaxEntries = 1000;
    options.EnableOptimization = true;
    options.EnableProfiling = false;
    options.EnableCpuFallback = true;
    options.DefaultTimeout = TimeSpan.FromMinutes(5);
});
```

#### `AddPipelineServices()`
Adds core pipeline services including expression analysis and optimization.

```csharp
services.AddPipelineServices(options =>
{
    options.EnableAdvancedOptimization = true;
    options.EnableTelemetry = false;
    options.MaxPipelineStages = 20;
    options.DefaultTimeoutSeconds = 300;
    options.EnablePipelineCaching = true;
    options.MaxMemoryUsageMB = 1024;
});
```

#### `AddLinqProvider()`
Registers pipeline-optimized LINQ provider with intelligent backend selection.

```csharp
services.AddLinqProvider(options =>
{
    options.EnableExpressionOptimization = true;
    options.EnableIntelligentBackendSelection = true;
    options.EnablePerformanceMonitoring = false;
    options.EnableAutomaticKernelFusion = true;
    options.DefaultStrategy = OptimizationStrategy.Balanced;
    options.GpuComplexityThreshold = 10;
});
```

#### `AddPipelineOptimization()`
Adds advanced optimization services including adaptive backend selection.

```csharp
services.AddPipelineOptimization(options =>
{
    options.Strategy = OptimizationStrategy.Aggressive;
    options.EnableMachineLearning = false;
    options.EnableKernelFusion = true;
    options.EnableMemoryOptimization = true;
    options.EnableQueryPlanOptimization = true;
    options.CachePolicy = CachePolicy.Memory;
    options.ExecutionPriority = ExecutionPriority.Normal;
});
```

#### `AddPipelineTelemetry()`
Registers telemetry and monitoring services for performance insights.

```csharp
services.AddPipelineTelemetry(options =>
{
    options.EnableDetailedMetrics = false;
    options.EnableExecutionTraces = false;
    options.EnableMemoryTracking = true;
    options.EnableBackendMonitoring = true;
    options.EnableQueryPatternAnalysis = false;
    options.EnableBottleneckIdentification = true;
    options.CollectionIntervalSeconds = 30;
    options.MaxBufferSize = 10000;
});
```

#### `AddCompletePipelineServices()`
Convenience method that adds all pipeline services with comprehensive configuration.

```csharp
services.AddCompletePipelineServices(options =>
{
    // Pipeline options
    options.Pipelines.EnableAdvancedOptimization = true;
    options.Pipelines.MaxPipelineStages = 20;
    
    // Provider options
    options.Provider.DefaultStrategy = OptimizationStrategy.Balanced;
    options.Provider.GpuComplexityThreshold = 8;
    
    // Optimization options
    options.Optimization.EnableMachineLearning = true;
    options.Optimization.EnableKernelFusion = true;
    
    // Telemetry options
    options.Telemetry.EnableDetailedMetrics = true;
    options.Telemetry.EnableMemoryTracking = true;
});
```

## Configuration Patterns

### Development Environment

```csharp
public static void ConfigureDevelopmentServices(IServiceCollection services)
{
    services.AddDotComputeLinq(options =>
    {
        options.EnableProfiling = true; // Enable for debugging
    })
    .AddPipelineServices(options =>
    {
        options.EnableTelemetry = true;
        options.DefaultTimeoutSeconds = 600; // Higher timeout
    })
    .AddPipelineOptimization(options =>
    {
        options.Strategy = OptimizationStrategy.Aggressive;
        options.EnableMachineLearning = true; // Test ML features
    })
    .AddPipelineTelemetry(options =>
    {
        options.EnableDetailedMetrics = true;
        options.EnableExecutionTraces = true;
        options.CollectionIntervalSeconds = 5; // Frequent collection
    });
}
```

### Production Environment

```csharp
public static void ConfigureProductionServices(IServiceCollection services)
{
    services.AddDotComputeLinq(options =>
    {
        options.EnableProfiling = false; // Disabled for performance
        options.CacheMaxEntries = 5000;
    })
    .AddPipelineServices(options =>
    {
        options.EnableTelemetry = false; // Minimal telemetry
        options.MaxMemoryUsageMB = 4096;
    })
    .AddPipelineOptimization(options =>
    {
        options.Strategy = OptimizationStrategy.Balanced;
        options.EnableMachineLearning = false; // Predictable behavior
        options.ExecutionPriority = ExecutionPriority.High;
    })
    .AddPipelineTelemetry(options =>
    {
        options.EnableDetailedMetrics = false;
        options.EnableBottleneckIdentification = true;
        options.CollectionIntervalSeconds = 60;
    });
}
```

## Usage Examples

### Basic Usage

```csharp
public class DataProcessingService
{
    private readonly IServiceProvider _serviceProvider;

    public DataProcessingService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<double[]> ProcessAsync(float[] data)
    {
        var linqProvider = _serviceProvider.GetComputeLinqProvider();
        var queryable = linqProvider.CreateQueryable(data);

        return await queryable
            .Where(x => x > 0)
            .Select(x => Math.Sqrt(x))
            .ToArrayAsync();
    }
}
```

### Advanced Usage with Manual Provider

```csharp
public class AdvancedProcessingService
{
    private readonly PipelineOptimizedProvider _provider;
    private readonly ILogger<AdvancedProcessingService> _logger;

    public AdvancedProcessingService(
        PipelineOptimizedProvider provider,
        ILogger<AdvancedProcessingService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task<T> ExecuteOptimizedAsync<T>(Expression expression)
    {
        _logger.LogInformation("Executing optimized pipeline expression");
        return await _provider.ExecuteAsync<T>(expression);
    }
}
```

## Service Lifetimes

The service collection extensions register services with appropriate lifetimes:

- **Singletons**: Configuration options, optimizers, analyzers, cache managers
- **Scoped**: Query providers, pipeline builders, execution services
- **Transient**: Individual query instances, temporary objects

## Performance Considerations

### Memory Usage
- Configure `MaxMemoryUsageMB` based on available system memory
- Enable memory tracking in development, disable in production
- Use memory caching for frequently executed queries

### Optimization Strategy
- **Conservative**: Minimal risk, good for production stability
- **Balanced**: Good performance with reasonable safety (recommended)
- **Aggressive**: Maximum performance, higher resource usage
- **Adaptive**: ML-based optimization (experimental)

### Telemetry Impact
- Detailed metrics can impact performance by 5-15%
- Execution traces have higher overhead (10-25%)
- Memory tracking has minimal impact (<2%)
- Consider disabling telemetry in high-throughput scenarios

## Integration with ASP.NET Core

### Controller Example

```csharp
[ApiController]
[Route("api/[controller]")]
public class ComputeController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public ComputeController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [HttpPost("process")]
    public async Task<ActionResult<double[]>> Process([FromBody] float[] data)
    {
        var queryable = _serviceProvider.CreateComputeQueryable(data);
        var results = await queryable
            .Where(x => x > 0)
            .Select(x => x * 2.0)
            .ToArrayAsync();
        
        return Ok(results);
    }
}
```

### Minimal API Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDotComputeLinq()
                .AddCompletePipelineServices();

var app = builder.Build();

// Map endpoint
app.MapPost("/compute", async (float[] data, IServiceProvider serviceProvider) =>
{
    var queryable = serviceProvider.CreateComputeQueryable(data);
    return await queryable.Where(x => x > 0).ToArrayAsync();
});

app.Run();
```

## Troubleshooting

### Common Issues

1. **Missing Dependencies**: Ensure DotCompute runtime services are registered
2. **Memory Errors**: Reduce `MaxMemoryUsageMB` or disable memory-intensive features
3. **Timeout Issues**: Increase `DefaultTimeoutSeconds` for complex queries
4. **Performance Issues**: Disable telemetry and profiling in production

### Debugging

Enable detailed logging and telemetry in development:

```csharp
services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
services.AddPipelineTelemetry(options =>
{
    options.EnableDetailedMetrics = true;
    options.EnableExecutionTraces = true;
});
```

## See Also

- [Pipeline Architecture Documentation](../architecture/pipelines.md)
- [Performance Optimization Guide](../performance/optimization.md)
- [LINQ Provider Reference](../api/linq-provider.md)
- [Telemetry and Monitoring](../monitoring/telemetry.md)