# DotCompute Runtime - Dependency Injection System

This document describes the dependency injection (DI) container integration for DotCompute, including both implemented features and stub implementations that enable proper service registration and testing.

## Overview

The DotCompute Runtime provides dependency injection infrastructure with:
- **✅ Accelerator Creation**: DI-based accelerator factory implementation
- **✅ Plugin Support**: Plugin service provider and lifecycle management
- **✅ Service Registration**: Core service registration with proper lifetimes
- **✅ Configuration System**: Options pattern with validation
- **🚧 Service Implementation**: Many services are stub implementations for DI compatibility

## Installation

```bash
dotnet add package DotCompute.Runtime --version 0.5.3
```

## Key Features

### 1. Accelerator Factory with DI Support
- `IAcceleratorFactory` and `DefaultAcceleratorFactory` for DI-based accelerator creation
- Support for multiple accelerator providers (CPU, CUDA, OpenCL)
- Service lifetime management (Singleton, Scoped, Transient)
- Accelerator validation and performance metrics

### 2. Plugin Dependency Injection
- `IPluginServiceProvider` for plugin-specific service scopes
- `IPluginDependencyResolver` for constructor and property injection
- `IPluginLifecycleManager` for managing plugin lifecycles with DI
- Support for plugin-specific services and isolated contexts

### 3. Advanced Service Registration
- Memory management services (`IMemoryPoolService`, `IUnifiedMemoryService`)
- Kernel compilation services (`IKernelCompilerService`, `IKernelCacheService`)
- Performance monitoring (`IPerformanceProfiler`, `IDeviceMetricsCollector`)
- Plugin management (`IAlgorithmPluginManager`)

### 4. Configuration System
- `DotComputeRuntimeOptions` for runtime configuration
- `DotComputePluginOptions` for plugin-specific settings
- `AdvancedMemoryOptions` for memory management configuration
- `PerformanceMonitoringOptions` for profiling configuration
- Configuration validation with `IValidateOptions<T>`

## Usage Examples

### Basic Setup
```csharp
var services = new ServiceCollection();

// Add DotCompute with basic configuration
services.AddDotComputeRuntime(configureOptions: options =>
{
    options.EnableAutoDiscovery = true;
    options.PreferredAcceleratorType = AcceleratorType.CPU;
    options.EnableKernelCaching = true;
    options.EnableMemoryPooling = true;
});

var serviceProvider = services.BuildServiceProvider();
var runtime = serviceProvider.GetRequiredService<AcceleratorRuntime>();
```

### Advanced Setup with Configuration
```csharp
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DotCompute:EnableAutoDiscovery"] = "true",
        ["DotCompute:PreferredAcceleratorType"] = "CPU",
        ["DotCompute:EnableKernelCaching"] = "true",
        ["DotCompute:Plugins:EnablePlugins"] = "true",
        ["DotCompute:Plugins:EnableDependencyInjection"] = "true"
    })
    .Build();

var services = new ServiceCollection();
services.AddDotComputeComplete(configuration);
```

### Plugin with DI Support
```csharp
public class SampleAlgorithmPlugin : IAlgorithmPlugin
{
    private readonly ICustomDataProvider _dataProvider;
    private readonly IComputationLogger _computationLogger;

    // Constructor injection
    public SampleAlgorithmPlugin(
        ICustomDataProvider dataProvider,
        IComputationLogger computationLogger)
    {
        _dataProvider = dataProvider;
        _computationLogger = computationLogger;
    }

    // Property injection
    [Inject(Required = false)]
    public IPluginSpecificService? PluginService { get; set; }

    // ... implementation
}
```

### Hosted Service Integration
```csharp
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        // DotCompute will be initialized automatically as a hosted service
        services.AddDotComputeRuntime(context.Configuration);
        services.AddHostedService<ComputeBackgroundService>();
    })
    .Build();

await host.RunAsync();
```

## File Structure

```
src/DotCompute.Runtime/
├── Configuration/
│   └── DotComputeRuntimeOptions.cs       # Configuration classes and validation
├── DependencyInjection/
│   ├── IPluginServiceProvider.cs          # Plugin DI interfaces
│   ├── PluginServiceProvider.cs           # Plugin DI implementations
│   └── PluginLifecycleManager.cs          # Plugin lifecycle with DI
├── Factories/
│   ├── IAcceleratorFactory.cs             # Accelerator factory interface
│   └── DefaultAcceleratorFactory.cs       # DI-based accelerator factory
├── Services/
│   ├── IMemoryServices.cs                 # Memory management interfaces
│   ├── IKernelServices.cs                 # Kernel compilation interfaces
│   ├── IPerformanceServices.cs            # Performance monitoring interfaces
│   ├── MemoryPoolService.cs               # Memory pool implementation
│   ├── RuntimeInitializationService.cs    # Hosted service for initialization
│   └── StubImplementations.cs             # Stub services for compilation
├── Examples/
│   └── DIIntegrationExample.cs            # Comprehensive usage examples
├── ServiceCollectionExtensions.cs         # Main DI registration methods
└── README.md                              # This documentation
```

## Extension Methods

### Core Registration
- `AddDotComputeRuntime()` - Registers core runtime services
- `AddDotComputeRuntimeWithProviders()` - Registers with specific providers
- `AddDotComputeRuntimeWithFactory<T>()` - Registers with custom factory

### Feature Registration
- `AddDotComputePlugins()` - Enables plugin support with DI
- `AddAdvancedMemoryManagement()` - Enables advanced memory features
- `AddPerformanceMonitoring()` - Enables performance profiling
- `AddDotComputeComplete()` - Registers all features

## Service Lifetimes

### Singleton Services
- `AcceleratorRuntime` - Main runtime instance
- `IAcceleratorManager` - Accelerator management
- `IAcceleratorFactory` - Accelerator factory
- `IMemoryPoolService` - Memory pool management

### Scoped Services
- Plugin instances (configurable)
- Performance profiling sessions

### Transient Services
- Kernel compilation services
- Memory buffers
- Performance metrics collectors

## Configuration Validation

The system includes comprehensive configuration validation:

```csharp
public class RuntimeOptionsValidator : IValidateOptions<DotComputeRuntimeOptions>
{
    public ValidateOptionsResult Validate(string? name, DotComputeRuntimeOptions options)
    {
        // Validates ranges, paths, and dependencies
        // Returns detailed error messages for invalid configurations
    }
}
```

## Plugin Dependency Resolution

Plugins can use both constructor injection and property injection:

```csharp
// Constructor injection (required dependencies)
public MyPlugin(IRequiredService service) { }

// Property injection (optional dependencies)
[Inject(Required = false)]
public IOptionalService? OptionalService { get; set; }
```

## Integration with Existing Code

The DI system is designed to integrate seamlessly with existing DotCompute code:

1. **Non-breaking**: Existing code continues to work unchanged
2. **Opt-in**: DI features are enabled through configuration
3. **Gradual adoption**: Services can be migrated to DI incrementally
4. **Fallback support**: Graceful degradation when dependencies are missing

## Performance Considerations

- Service resolution is optimized for frequent access patterns
- Memory pools reduce allocation overhead
- Lazy initialization minimizes startup time
- Caching reduces repeated service creation costs

## Future Enhancements

- Integration with .NET Generic Host for full application lifecycle management
- Support for multiple DI containers (Autofac, Unity, etc.)
- Advanced plugin isolation with AppDomains or AssemblyLoadContext
- Distributed computing support with remote service resolution
- Integration with OpenTelemetry for comprehensive observability

## Dependencies

The DI system requires these NuGet packages:
- `Microsoft.Extensions.DependencyInjection` - Core DI container
- `Microsoft.Extensions.Configuration` - Configuration system
- `Microsoft.Extensions.Options` - Options pattern support
- `Microsoft.Extensions.Hosting` - Hosted service support
- `Microsoft.Extensions.Logging` - Logging infrastructure

## Implementation Notes

### Stub Services
Many services in the Runtime project are implemented as stubs that:
- Enable proper DI container configuration and compilation
- Throw `NotImplementedException` for actual functionality
- Allow concrete implementations to be provided by backend packages
- Support testing scenarios with mock implementations

Key stub services include:
- `KernelCompilerService` - Actual implementation in backend packages
- `AlgorithmPluginManager` - Full implementation in Algorithms package  
- `PerformanceProfiler` - Metrics collection stubs
- Memory management services - Integrated with Memory package

### Architecture Benefits
This DI system provides:
- **✅ Modular architecture** with proper service lifetimes
- **✅ Testable code** through dependency injection
- **✅ Flexible configuration** for different environments
- **✅ Plugin ecosystem** with DI support
- **🚧 Service implementations** completed by concrete backend packages

The implementation provides the foundation for a fully-featured compute runtime while allowing individual components to be developed and integrated independently.

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[Core Orchestration](../../../docs/articles/architecture/core-orchestration.md)** - Runtime orchestration and execution
- **[System Overview](../../../docs/articles/architecture/overview.md)** - Service layer architecture

### Developer Guides
- **[Getting Started](../../../docs/articles/getting-started.md)** - Installation and setup
- **[Dependency Injection](../../../docs/articles/guides/dependency-injection.md)** - DI integration patterns and testing

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)