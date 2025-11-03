# Dependency Injection Guide

DotCompute integrates with Microsoft.Extensions.DependencyInjection for flexible service configuration and testability.

## Overview

DotCompute uses dependency injection for:

- **Service Configuration**: Register compute services and backends
- **Lifetime Management**: Control service scopes and disposal
- **Testability**: Replace implementations for testing
- **Extensibility**: Add custom services and plugins

All DotCompute services follow standard DI patterns from `Microsoft.Extensions.DependencyInjection`.

## Basic Setup

### Minimal Configuration

```csharp
using DotCompute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDotComputeRuntime();
    })
    .Build();

var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();
```

**What's Registered**:
- `IComputeOrchestrator` (singleton)
- `IUnifiedMemoryManager` (singleton)
- `IKernelDiscoveryService` (singleton)
- Available backends (CPU, CUDA, Metal, etc.)
- Memory pool
- Plugin system

### With Options

```csharp
services.AddDotComputeRuntime(options =>
{
    // Memory pooling
    options.MemoryPooling.Enabled = true;
    options.MemoryPooling.MaxPoolSizeBytes = 4L * 1024 * 1024 * 1024;  // 4GB

    // Backend preferences
    options.PreferredBackend = BackendType.CUDA;
    options.EnableCpuFallback = true;

    // Telemetry
    options.Telemetry.Enabled = true;
    options.Telemetry.EnableDetailedMetrics = false;

    // Debug mode
    options.Debug.Enabled = false;
});
```

## Service Registration

### Core Services

DotCompute registers these core services:

```csharp
public static class DotComputeServiceCollectionExtensions
{
    public static IServiceCollection AddDotComputeRuntime(
        this IServiceCollection services,
        Action<DotComputeOptions>? configure = null)
    {
        // Core orchestration (singleton)
        services.AddSingleton<IComputeOrchestrator, ComputeOrchestrator>();

        // Memory management (singleton)
        services.AddSingleton<IUnifiedMemoryManager, UnifiedMemoryManager>();

        // Kernel discovery (singleton)
        services.AddSingleton<IKernelDiscoveryService, GeneratedKernelDiscoveryService>();

        // Backend registration
        services.AddSingleton<ICpuAccelerator, CpuAccelerator>();
        services.TryAddSingleton<ICudaAccelerator, CudaAccelerator>();
        services.TryAddSingleton<IMetalAccelerator, MetalAccelerator>();

        // Apply options
        if (configure != null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
```

### Service Lifetimes

**Singleton** (entire application):
- `IComputeOrchestrator`
- `IUnifiedMemoryManager`
- `IKernelDiscoveryService`
- Backend accelerators

**Scoped** (per request/operation):
- None by default (all singletons)

**Transient** (per injection):
- None by default

**Why Singletons?**
- GPU contexts expensive to create
- Memory pools benefit from reuse
- Kernel compilation results cached
- Minimal GC pressure

## Optional Services

### Debugging Services

```csharp
services.AddDotComputeRuntime()
    .AddProductionDebugging();  // < 5% overhead

// Or development debugging (2-5x overhead)
services.AddDotComputeRuntime()
    .AddDevelopmentDebugging();
```

**Registers**:
- `IKernelDebugService` (singleton)
- `DebugIntegratedOrchestrator` (wraps `IComputeOrchestrator`)
- Cross-backend validation
- Performance profiling

### Optimization Services

```csharp
services.AddDotComputeRuntime()
    .AddProductionOptimization();

// Or with custom profile
services.AddDotComputeRuntime()
    .AddOptimization(options =>
    {
        options.Profile = OptimizationProfile.Aggressive;
        options.EnableMachineLearning = true;
        options.LearningRate = 0.1;
    });
```

**Registers**:
- `IBackendSelector` (singleton)
- `IWorkloadAnalyzer` (singleton)
- `PerformanceOptimizedOrchestrator` (wraps `IComputeOrchestrator`)
- ML-powered backend selection

### Telemetry Services

```csharp
services.AddDotComputeRuntime(options =>
{
    options.Telemetry.Enabled = true;
})
.AddOpenTelemetry();  // OpenTelemetry integration

// Optional: Custom metrics
services.AddSingleton<IMetricsCollector, CustomMetricsCollector>();
```

**Registers**:
- OpenTelemetry meters and traces
- Performance counters
- Custom metrics exporters

## Configuration Patterns

### appsettings.json

```json
{
  "DotCompute": {
    "PreferredBackend": "CUDA",
    "EnableCpuFallback": true,
    "MemoryPooling": {
      "Enabled": true,
      "MaxPoolSizeBytes": 4294967296
    },
    "Telemetry": {
      "Enabled": true,
      "EnableDetailedMetrics": false
    },
    "Debug": {
      "Enabled": false,
      "Profile": "Production"
    },
    "Optimization": {
      "Enabled": true,
      "Profile": "Balanced",
      "EnableMachineLearning": true
    }
  }
}
```

**Bind Configuration**:
```csharp
services.AddDotComputeRuntime(options =>
{
    configuration.GetSection("DotCompute").Bind(options);
});
```

### Environment-Specific Configuration

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var env = context.HostingEnvironment;

        if (env.IsDevelopment())
        {
            services.AddDotComputeRuntime()
                .AddDevelopmentDebugging();
        }
        else if (env.IsProduction())
        {
            services.AddDotComputeRuntime(options =>
            {
                options.MemoryPooling.Enabled = true;
                options.Telemetry.Enabled = true;
            })
            .AddProductionOptimization();
        }
    });
```

### Feature Flags

```csharp
services.AddDotComputeRuntime(options =>
{
    var enableGpu = configuration.GetValue<bool>("Features:EnableGPU");
    options.PreferredBackend = enableGpu ? BackendType.CUDA : BackendType.CPU;

    var enableOptimization = configuration.GetValue<bool>("Features:EnableOptimization");
    if (enableOptimization)
    {
        services.AddProductionOptimization();
    }
});
```

## Testing

### Unit Tests

Replace implementations with mocks:

```csharp
using Moq;
using Xunit;

public class DataProcessorTests
{
    [Fact]
    public async Task ProcessData_ReturnsExpectedResult()
    {
        // Arrange
        var mockOrchestrator = new Mock<IComputeOrchestrator>();
        mockOrchestrator
            .Setup(o => o.ExecuteKernelAsync<float[]>(
                "ProcessData",
                It.IsAny<object>(),
                It.IsAny<ExecutionOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 1.0f, 2.0f, 3.0f });

        var services = new ServiceCollection();
        services.AddSingleton(mockOrchestrator.Object);
        var provider = services.BuildServiceProvider();

        var processor = new DataProcessor(provider.GetRequiredService<IComputeOrchestrator>());

        // Act
        var result = await processor.ProcessAsync(new float[] { 1.0f, 2.0f, 3.0f });

        // Assert
        Assert.Equal(3, result.Length);
        mockOrchestrator.Verify(o => o.ExecuteKernelAsync<float[]>(
            "ProcessData",
            It.IsAny<object>(),
            It.IsAny<ExecutionOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Integration Tests

Use real implementations with CPU backend:

```csharp
public class IntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IComputeOrchestrator _orchestrator;

    public IntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime(options =>
        {
            options.PreferredBackend = BackendType.CPU;  // Reliable, no GPU required
            options.EnableCpuFallback = false;
        });

        _serviceProvider = services.BuildServiceProvider();
        _orchestrator = _serviceProvider.GetRequiredService<IComputeOrchestrator>();
    }

    [Fact]
    public async Task VectorAdd_ComputesCorrectResult()
    {
        // Arrange
        var a = new float[] { 1, 2, 3 };
        var b = new float[] { 4, 5, 6 };
        var result = new float[3];

        // Act
        await _orchestrator.ExecuteKernelAsync(
            "VectorAdd",
            new { a, b, result });

        // Assert
        Assert.Equal(new float[] { 5, 7, 9 }, result);
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
```

### Test Fixtures

Reuse service provider across tests:

```csharp
public class DotComputeFixture : IDisposable
{
    public ServiceProvider ServiceProvider { get; }
    public IComputeOrchestrator Orchestrator { get; }

    public DotComputeFixture()
    {
        var services = new ServiceCollection();
        services.AddDotComputeRuntime(options =>
        {
            options.PreferredBackend = BackendType.CPU;
        });

        ServiceProvider = services.BuildServiceProvider();
        Orchestrator = ServiceProvider.GetRequiredService<IComputeOrchestrator>();
    }

    public void Dispose()
    {
        ServiceProvider.Dispose();
    }
}

public class MyTests : IClassFixture<DotComputeFixture>
{
    private readonly DotComputeFixture _fixture;

    public MyTests(DotComputeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test1()
    {
        await _fixture.Orchestrator.ExecuteKernelAsync(...);
    }

    [Fact]
    public async Task Test2()
    {
        await _fixture.Orchestrator.ExecuteKernelAsync(...);
    }
}
```

## Advanced Patterns

### Factory Pattern

Create orchestrators with different configurations:

```csharp
public interface IComputeOrchestratorFactory
{
    IComputeOrchestrator Create(BackendType backend);
}

public class ComputeOrchestratorFactory : IComputeOrchestratorFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ComputeOrchestratorFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IComputeOrchestrator Create(BackendType backend)
    {
        var options = new ExecutionOptions
        {
            PreferredBackend = backend
        };

        return new ComputeOrchestrator(
            _serviceProvider.GetRequiredService<IUnifiedMemoryManager>(),
            _serviceProvider.GetRequiredService<IKernelDiscoveryService>(),
            _serviceProvider.GetServices<IAccelerator>(),
            options);
    }
}

// Register factory
services.AddSingleton<IComputeOrchestratorFactory, ComputeOrchestratorFactory>();

// Use factory
var factory = serviceProvider.GetRequiredService<IComputeOrchestratorFactory>();
var cudaOrchestrator = factory.Create(BackendType.CUDA);
var cpuOrchestrator = factory.Create(BackendType.CPU);
```

### Decorator Pattern

Add cross-cutting concerns:

```csharp
public class LoggingComputeOrchestrator : IComputeOrchestrator
{
    private readonly IComputeOrchestrator _inner;
    private readonly ILogger<LoggingComputeOrchestrator> _logger;

    public LoggingComputeOrchestrator(
        IComputeOrchestrator inner,
        ILogger<LoggingComputeOrchestrator> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<TResult> ExecuteKernelAsync<TResult>(
        string kernelName,
        object parameters,
        ExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing kernel: {KernelName}", kernelName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _inner.ExecuteKernelAsync<TResult>(
                kernelName, parameters, options, cancellationToken);

            _logger.LogInformation(
                "Kernel {KernelName} completed in {ElapsedMs}ms",
                kernelName,
                stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kernel {KernelName} failed", kernelName);
            throw;
        }
    }
}

// Register with decorator
services.AddSingleton<IComputeOrchestrator, ComputeOrchestrator>();
services.Decorate<IComputeOrchestrator, LoggingComputeOrchestrator>();
```

### Named Services

Multiple configurations:

```csharp
services.AddSingleton<IComputeOrchestrator>(sp =>
    new ComputeOrchestrator(
        sp.GetRequiredService<IUnifiedMemoryManager>(),
        sp.GetRequiredService<IKernelDiscoveryService>(),
        sp.GetServices<IAccelerator>(),
        new DotComputeOptions { PreferredBackend = BackendType.CUDA }));

services.AddSingleton<IComputeOrchestrator>(sp =>
    new ComputeOrchestrator(
        sp.GetRequiredService<IUnifiedMemoryManager>(),
        sp.GetRequiredService<IKernelDiscoveryService>(),
        sp.GetServices<IAccelerator>(),
        new DotComputeOptions { PreferredBackend = BackendType.CPU }));

// Resolve specific implementation
var allOrchestrators = serviceProvider.GetServices<IComputeOrchestrator>();
var cudaOrchestrator = allOrchestrators.First(o => o.PreferredBackend == BackendType.CUDA);
var cpuOrchestrator = allOrchestrators.First(o => o.PreferredBackend == BackendType.CPU);
```

### Plugin Registration

Dynamic backend loading:

```csharp
services.AddDotComputeRuntime(options =>
{
    options.Plugins.SearchPaths = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "plugins"),
        "/opt/dotcompute/plugins"
    };

    options.Plugins.AutoLoadEnabled = true;
    options.Plugins.EnableHotReload = true;
});

// Plugins automatically discovered and registered as IAccelerator
```

## Custom Services

### Custom Backend

```csharp
public class CustomAccelerator : IAccelerator
{
    public string Id => "custom";
    public AcceleratorType Type => AcceleratorType.Custom;

    // Implement interface...
}

// Register
services.AddSingleton<IAccelerator, CustomAccelerator>();
```

### Custom Memory Manager

```csharp
public class CustomMemoryManager : IUnifiedMemoryManager
{
    // Implement interface...
}

// Replace default
services.AddSingleton<IUnifiedMemoryManager, CustomMemoryManager>();
```

### Custom Kernel Discovery

```csharp
public class CustomKernelDiscoveryService : IKernelDiscoveryService
{
    public IEnumerable<KernelDefinition> DiscoverKernels()
    {
        // Custom kernel discovery logic
        yield return new KernelDefinition
        {
            Name = "CustomKernel",
            // ...
        };
    }
}

// Chain with default discovery
services.AddSingleton<IKernelDiscoveryService>(sp =>
{
    var defaultService = sp.GetRequiredService<GeneratedKernelDiscoveryService>();
    var customService = new CustomKernelDiscoveryService();

    return new CompositeKernelDiscoveryService(defaultService, customService);
});
```

## Common Pitfalls

### 1. Incorrect Lifetime

**❌ Transient for GPU Context**:
```csharp
// Don't do this
services.AddTransient<IComputeOrchestrator, ComputeOrchestrator>();
// Creates new GPU context on every injection - expensive!
```

**✅ Singleton**:
```csharp
services.AddSingleton<IComputeOrchestrator, ComputeOrchestrator>();
// Single GPU context, reused throughout application
```

### 2. Forgetting to Dispose

**❌ No Disposal**:
```csharp
var services = new ServiceCollection();
services.AddDotComputeRuntime();
var provider = services.BuildServiceProvider();
// Forgot to dispose - GPU context leaked
```

**✅ Proper Disposal**:
```csharp
await using var provider = services.BuildServiceProvider();
// Automatically disposes all singleton services
```

### 3. Circular Dependencies

**❌ Circular Reference**:
```csharp
public class ServiceA
{
    public ServiceA(ServiceB b) { }
}

public class ServiceB
{
    public ServiceB(ServiceA a) { }  // Circular!
}
// Throws InvalidOperationException at runtime
```

**✅ Break Cycle with Factory**:
```csharp
public class ServiceA
{
    public ServiceA(Func<ServiceB> bFactory) { }
}

services.AddSingleton<ServiceA>();
services.AddSingleton<ServiceB>();
services.AddSingleton<Func<ServiceB>>(sp => () => sp.GetRequiredService<ServiceB>());
```

### 4. Service Not Registered

**❌ Missing Registration**:
```csharp
var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();
// Throws InvalidOperationException: Service not registered
```

**✅ Check Registration**:
```csharp
var orchestrator = serviceProvider.GetService<IComputeOrchestrator>();
if (orchestrator == null)
{
    throw new InvalidOperationException("DotCompute runtime not registered. Call services.AddDotComputeRuntime()");
}
```

### 5. Configuration Timing

**❌ Configure After Build**:
```csharp
var provider = services.BuildServiceProvider();
services.AddDotComputeRuntime();  // Too late!
```

**✅ Configure Before Build**:
```csharp
services.AddDotComputeRuntime();
var provider = services.BuildServiceProvider();
```

## Best Practices

### 1. Use Extension Methods

**✅ Fluent Configuration**:
```csharp
services.AddDotComputeRuntime()
    .AddProductionDebugging()
    .AddProductionOptimization()
    .AddOpenTelemetry();
```

### 2. Validate Configuration

```csharp
services.AddDotComputeRuntime(options =>
{
    if (options.MemoryPooling.MaxPoolSizeBytes < 0)
    {
        throw new ArgumentException("MaxPoolSizeBytes must be non-negative");
    }

    if (options.PreferredBackend == BackendType.CUDA && !CudaRuntime.IsAvailable)
    {
        throw new InvalidOperationException("CUDA backend requested but not available");
    }
});
```

### 3. Environment-Specific Configurations

```csharp
services.AddDotComputeRuntime(options =>
{
    var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    if (isDevelopment)
    {
        options.Debug.Enabled = true;
        options.Telemetry.EnableDetailedMetrics = true;
    }
    else
    {
        options.Debug.Enabled = false;
        options.Optimization.EnableMachineLearning = true;
    }
});
```

### 4. Scoped Services for Request-Level State

```csharp
// If you need per-request state
services.AddScoped<IKernelExecutionContext, KernelExecutionContext>();

public class KernelExecutionContext : IKernelExecutionContext
{
    private readonly Dictionary<string, object> _state = new();

    public void SetState(string key, object value)
    {
        _state[key] = value;
    }

    public object? GetState(string key)
    {
        _state.TryGetValue(key, out var value);
        return value;
    }
}
```

### 5. Options Pattern

```csharp
services.AddOptions<DotComputeOptions>()
    .Bind(configuration.GetSection("DotCompute"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

## ASP.NET Core Integration

### WebAPI Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add DotCompute services
builder.Services.AddDotComputeRuntime(options =>
{
    builder.Configuration.GetSection("DotCompute").Bind(options);
})
.AddProductionOptimization();

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();
app.Run();
```

### Controller Usage

```csharp
[ApiController]
[Route("api/[controller]")]
public class ComputeController : ControllerBase
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<ComputeController> _logger;

    public ComputeController(
        IComputeOrchestrator orchestrator,
        ILogger<ComputeController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<ActionResult<float[]>> Process([FromBody] float[] data)
    {
        _logger.LogInformation("Processing {Count} elements", data.Length);

        var result = new float[data.Length];
        await _orchestrator.ExecuteKernelAsync(
            "ProcessData",
            new { input = data, output = result });

        return Ok(result);
    }
}
```

## Worker Service Integration

```csharp
public class ComputeWorker : BackgroundService
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<ComputeWorker> _logger;

    public ComputeWorker(
        IComputeOrchestrator orchestrator,
        ILogger<ComputeWorker> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Processing batch at {Time}", DateTime.UtcNow);

            var data = await LoadNextBatchAsync();
            await _orchestrator.ExecuteKernelAsync("ProcessBatch", new { data });

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}

// Register worker
builder.Services.AddHostedService<ComputeWorker>();
```

## Further Reading

- [Getting Started](../getting-started.md) - Basic setup
- [Testing Guide](testing.md) - Testing strategies
- [Debugging Guide](debugging-guide.md) - Debugging with DI
- [Performance Tuning](performance-tuning.md) - Optimization

---

**Dependency Injection • Configuration • Testability • Production Ready**
