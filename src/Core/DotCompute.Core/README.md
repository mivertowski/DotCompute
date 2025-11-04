# DotCompute.Core

Core runtime and orchestration engine for the DotCompute compute acceleration framework.

## Status: ✅ Production Ready (v0.2.0)

The Core runtime provides comprehensive infrastructure for compute acceleration:
- **Kernel Execution Management**: Complete kernel compilation and execution pipeline
- **Accelerator Discovery**: Automatic detection and lifecycle management
- **Service Orchestration**: Dependency injection integration
- **Performance Monitoring**: OpenTelemetry-based telemetry infrastructure
- **Debugging Services**: Cross-backend validation and profiling
- **Optimization Engine**: ML-powered backend selection and workload optimization
- **Pipeline System**: Execution pipeline management with optimization
- **Recovery System**: Fault tolerance and error recovery
- **Native AOT**: Full Native AOT compatibility

## Key Components

### Compute Orchestration

#### IComputeOrchestrator
Universal kernel execution interface providing:
- Backend-agnostic kernel execution
- Automatic accelerator selection
- Type-safe parameter binding
- Asynchronous execution model
- Error handling and recovery

#### Kernel Execution Service
Runtime kernel orchestration:
- Generated kernel discovery
- Automatic kernel registration
- Execution context management
- Performance profiling
- Result materialization

### Kernel Management

#### Kernel Definition System
- **KernelDefinition**: Metadata and source code representation
- **KernelSource**: Source code abstraction with language detection
- **Kernel Compilation**: Multi-backend compilation pipeline
- **Kernel Validation**: Static analysis and validation
- **Kernel Caching**: Compiled kernel caching with TTL

#### Compiled Kernel Execution
- **ICompiledKernel**: Compiled kernel interface
- **Parameter Binding**: Type-safe argument binding
- **Launch Configuration**: Grid/block dimension specification
- **Memory Management Integration**: Automatic buffer handling
- **Synchronization**: Explicit and implicit synchronization

### Accelerator Management

#### Accelerator Discovery
Automatic backend detection:
- Platform capability detection
- Hardware enumeration
- Driver version checking
- Compute capability validation
- Multi-GPU/device support

#### Accelerator Lifecycle
- **Initialization**: Device context creation
- **Resource Management**: Memory and compute resources
- **Synchronization**: Cross-device synchronization
- **Cleanup**: Proper resource disposal

### Debugging and Validation

#### Cross-Backend Debugging
Production-ready debugging system with 8 methods:
- **CompareBackends**: CPU vs GPU result validation
- **ValidateKernelExecution**: Output correctness verification
- **AnalyzePerformance**: Performance profiling
- **InspectMemory**: Memory pattern analysis
- **TestDeterminism**: Reproducibility testing
- **FindOptimalConfig**: Parameter tuning
- **SimulateFailures**: Fault injection testing
- **GenerateDiagnostics**: Comprehensive diagnostics

#### Debug Profiles
- **Development**: Extensive validation and logging
- **Testing**: Balance of validation and performance
- **Production**: Minimal overhead with targeted checks
- **Custom**: User-defined debugging strategies

### Optimization Engine

#### Adaptive Backend Selection
ML-powered backend selection with 4 optimization levels:
- **Conservative**: Stable, proven configurations
- **Balanced**: Performance vs reliability tradeoff
- **Aggressive**: Maximum performance optimization
- **ML-Optimized**: Machine learning-based selection

#### Workload Analysis
- **Workload Characteristics**: Pattern recognition
- **Performance Prediction**: Execution time estimation
- **Resource Utilization**: Memory and compute requirements
- **Backend Affinity**: Optimal backend recommendation

#### Optimization Strategies
- **Hardware Profiling**: Capability-based selection
- **Cost-Based**: Execution cost minimization
- **ML-Based**: Learning from execution patterns
- **Hybrid**: Combination of multiple strategies

### Pipeline System

#### Execution Pipelines
Comprehensive pipeline management:
- **Pipeline Definition**: DAG-based execution graphs
- **Stage Composition**: Kernel chains and parallel stages
- **Pipeline Optimization**: Automatic graph optimization
- **Memory Management**: Inter-stage buffer management
- **Error Handling**: Pipeline-wide error recovery

#### Pipeline Profiling
- **Metrics Collection**: Per-stage performance metrics
- **Bottleneck Analysis**: Critical path identification
- **Resource Tracking**: Memory and compute utilization
- **Optimization Recommendations**: Auto-tuning suggestions

### Telemetry and Observability

#### OpenTelemetry Integration
Production-grade observability:
- **Distributed Tracing**: Execution flow tracking
- **Metrics Collection**: Performance counters
- **Logging Integration**: Structured logging
- **Prometheus Export**: Metrics endpoint
- **OTLP Support**: OpenTelemetry Protocol

#### Performance Metrics
- **Kernel Execution Time**: Per-kernel timing
- **Memory Transfer Bandwidth**: Host-device transfer rates
- **Throughput**: Operations per second
- **Resource Utilization**: CPU/GPU usage
- **Queue Depth**: Command queue metrics

### Recovery and Fault Tolerance

#### Fault Detection
- **Compilation Failures**: Kernel compilation errors
- **Execution Failures**: Runtime kernel failures
- **Memory Errors**: Out-of-memory, allocation failures
- **Device Errors**: GPU hangs, driver issues

#### Recovery Strategies
- **Retry with Backoff**: Automatic retry logic
- **Fallback Backends**: CPU fallback for GPU failures
- **Parameter Adjustment**: Reduce resource requirements
- **Graceful Degradation**: Continue with reduced capability

### Security Features

#### Kernel Validation
- **Source Code Scanning**: Detect unsafe patterns
- **Resource Limits**: Prevent resource exhaustion
- **Sandboxing**: Isolated execution environments
- **Access Control**: Permission-based execution

## Installation

```bash
dotnet add package DotCompute.Core --version 0.2.0-alpha
```

## Usage

### Basic Service Configuration

```csharp
using DotCompute.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure services
var builder = Host.CreateApplicationBuilder(args);

// Add DotCompute runtime
builder.Services.AddDotComputeRuntime(options =>
{
    options.EnableTelemetry = true;
    options.DefaultAccelerator = AcceleratorType.Auto;
    options.EnableDebugValidation = false; // Production
});

var host = builder.Build();

// Get orchestrator service
var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();
```

### Kernel Execution

```csharp
using DotCompute.Core;

// Execute kernel using orchestrator
var result = await orchestrator.ExecuteKernelAsync<float[], float[]>(
    "VectorAdd",
    new { a = dataA, b = dataB, length = 1_000_000 }
);

// Result is automatically materialized
Console.WriteLine($"First result: {result[0]}");
```

### Debug-Enabled Orchestration

```csharp
using DotCompute.Core.Debugging;

// Enable debugging for development
builder.Services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.EnableCrossBackendValidation = true;
    options.ValidateAllExecutions = true;
    options.CollectPerformanceMetrics = true;
});

// Orchestrator automatically validates all executions
var result = await orchestrator.ExecuteKernelAsync<float[], float[]>(
    "MyKernel",
    parameters
);

// Debug service provides detailed diagnostics
var debugService = host.Services.GetRequiredService<IKernelDebugService>();
var diagnostics = await debugService.GenerateDiagnosticsAsync("MyKernel", parameters);
Console.WriteLine(diagnostics.Summary);
```

### Performance Optimization

```csharp
using DotCompute.Core.Optimization;

// Enable ML-based optimization
builder.Services.AddProductionOptimization(options =>
{
    options.OptimizationStrategy = OptimizationStrategy.Aggressive;
    options.EnableMachineLearning = true;
    options.EnableAdaptiveSelection = true;
});

// Orchestrator automatically selects optimal backend
var result = await orchestrator.ExecuteKernelAsync<float[], float[]>(
    "ComplexKernel",
    largeDataset
);

// Get optimization insights
var optimizer = host.Services.GetRequiredService<IAdaptiveBackendSelector>();
var recommendation = await optimizer.SelectBackendAsync(
    new WorkloadCharacteristics
    {
        DataSize = largeDataset.Length,
        ComputeIntensity = ComputeIntensity.High,
        MemoryIntensive = true
    }
);

Console.WriteLine($"Recommended: {recommendation.Backend}");
Console.WriteLine($"Confidence: {recommendation.Confidence:P2}");
```

### Pipeline Execution

```csharp
using DotCompute.Core.Pipelines;

// Build execution pipeline
var pipeline = await pipelineBuilder
    .AddStage("Load", loadKernel, new { path = inputPath })
    .AddStage("Transform", transformKernel)
    .AddStage("Reduce", reduceKernel)
    .AddStage("Save", saveKernel, new { path = outputPath })
    .WithOptimization()
    .WithProfiling()
    .BuildAsync();

// Execute pipeline
var result = await pipeline.ExecuteAsync();

// Get profiling results
var profiler = host.Services.GetRequiredService<IPipelineProfiler>();
var metrics = await profiler.GetMetricsAsync(pipeline.Id);

foreach (var stage in metrics.Stages)
{
    Console.WriteLine($"{stage.Name}: {stage.Duration.TotalMilliseconds}ms");
}
```

### Telemetry Configuration

```csharp
using DotCompute.Core.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddDotComputeInstrumentation();
        metrics.AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddDotComputeInstrumentation();
        tracing.AddOtlpExporter();
    });

// Metrics automatically collected:
// - dotcompute.kernel.executions (counter)
// - dotcompute.kernel.duration (histogram)
// - dotcompute.memory.allocated (counter)
// - dotcompute.memory.transferred (counter)
```

### Recovery Configuration

```csharp
using DotCompute.Core.Recovery;

builder.Services.Configure<RecoveryOptions>(options =>
{
    options.EnableAutoRecovery = true;
    options.MaxRetries = 3;
    options.RetryDelay = TimeSpan.FromMilliseconds(100);
    options.FallbackToCPU = true;
    options.CollectFailureStatistics = true;
});

// Automatic recovery on failures
try
{
    var result = await orchestrator.ExecuteKernelAsync(kernelName, parameters);
}
catch (ComputeException ex)
{
    // After exhausting retries and fallbacks
    Console.WriteLine($"Execution failed: {ex.Message}");
    Console.WriteLine($"Retries attempted: {ex.RetryCount}");
}
```

## Architecture

### Service Layer

```
Application
    ↓
IComputeOrchestrator (High-level API)
    ↓
KernelExecutionService (Orchestration)
    ↓
├── Accelerator Management (Device selection)
├── Kernel Discovery (Generated kernels)
├── Kernel Compilation (Backend-specific)
├── Memory Management (Buffer allocation)
├── Execution Pipeline (Kernel launch)
├── Telemetry Collection (Metrics)
└── Error Recovery (Fault handling)
```

### Component Integration

```
DotCompute.Core
├── Abstractions Layer (Interfaces)
├── Compute Engine (Execution)
├── Memory Management (Buffers)
├── Debugging Services (Validation)
├── Optimization Engine (Backend selection)
├── Pipeline System (Workflow)
├── Telemetry System (Observability)
├── Recovery System (Fault tolerance)
└── Security System (Validation)
```

## Configuration Options

### Runtime Options

```csharp
public class DotComputeRuntimeOptions
{
    public AcceleratorType DefaultAccelerator { get; set; } = AcceleratorType.Auto;
    public bool EnableTelemetry { get; set; } = true;
    public bool EnableDebugValidation { get; set; } = false;
    public bool EnableAutoOptimization { get; set; } = true;
    public bool EnableRecovery { get; set; } = true;
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
}
```

### Debug Options

```csharp
public class DebugOptions
{
    public DebugProfile Profile { get; set; } = DebugProfile.Production;
    public bool EnableCrossBackendValidation { get; set; } = false;
    public bool ValidateAllExecutions { get; set; } = false;
    public bool CollectPerformanceMetrics { get; set; } = true;
    public double ToleranceThreshold { get; set; } = 1e-5;
}
```

### Optimization Options

```csharp
public class OptimizationOptions
{
    public OptimizationStrategy Strategy { get; set; } = OptimizationStrategy.Balanced;
    public bool EnableMachineLearning { get; set; } = false;
    public bool EnableAdaptiveSelection { get; set; } = true;
    public bool CacheDecisions { get; set; } = true;
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);
}
```

## System Requirements

- .NET 9.0 or later
- Native AOT compatible runtime (optional)
- 2GB+ RAM (4GB+ recommended)
- OpenTelemetry compatible monitoring (optional)

## Performance Characteristics

### Overhead
- Orchestration overhead: < 50μs per kernel
- Debugging overhead (Development): 2-5x execution time
- Debugging overhead (Production): < 5% execution time
- Telemetry overhead: < 1% execution time

### Scalability
- Concurrent kernel executions: Unlimited (backend-limited)
- Pipeline depth: No practical limit
- Telemetry throughput: 10K+ events/second

## Troubleshooting

### Accelerator Not Found

Check accelerator availability:
```csharp
var service = host.Services.GetRequiredService<IAcceleratorDiscoveryService>();
var accelerators = await service.DiscoverAsync();

if (!accelerators.Any())
{
    Console.WriteLine("No accelerators found. Ensure drivers installed.");
}
```

### Kernel Compilation Failures

Enable detailed logging:
```csharp
builder.Logging.SetMinimumLevel(LogLevel.Trace);
```

### Performance Issues

Use profiling:
```csharp
builder.Services.AddProductionDebugging(options =>
{
    options.CollectPerformanceMetrics = true;
});
```

## Advanced Topics

### Custom Accelerator Implementation

Implement IAccelerator and register:
```csharp
builder.Services.AddSingleton<IAccelerator, CustomAccelerator>();
```

### Custom Optimization Strategy

Implement IOptimizationStrategy:
```csharp
public class CustomStrategy : IOptimizationStrategy
{
    public Task<OptimizationDecision> DecideAsync(WorkloadCharacteristics workload)
    {
        // Custom logic
    }
}
```

### Pipeline Optimization

Custom pipeline optimizer:
```csharp
public class CustomPipelineOptimizer : IPipelineOptimizer
{
    public Task<PipelineExecutionPlan> OptimizeAsync(IPipeline pipeline)
    {
        // Custom optimization logic
    }
}
```

## Dependencies

- **DotCompute.Abstractions**: Core abstractions
- **Microsoft.Extensions.DependencyInjection**: DI infrastructure
- **Microsoft.Extensions.Logging**: Logging infrastructure
- **OpenTelemetry**: Observability infrastructure
- **System.Diagnostics.DiagnosticSource**: Metrics and tracing
- **prometheus-net**: Prometheus metrics export

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[Core Orchestration](../../../docs/articles/architecture/core-orchestration.md)** - Kernel execution pipeline (< 50μs overhead)
- **[Debugging System](../../../docs/articles/architecture/debugging-system.md)** - Cross-backend validation and profiling
- **[Optimization Engine](../../../docs/articles/architecture/optimization-engine.md)** - ML-powered backend selection
- **[Memory Management](../../../docs/articles/architecture/memory-management.md)** - Unified memory with pooling

### Developer Guides
- **[Getting Started](../../../docs/articles/getting-started.md)** - Installation and quick start
- **[Kernel Development](../../../docs/articles/guides/kernel-development.md)** - Writing efficient kernels
- **[Performance Tuning](../../../docs/articles/guides/performance-tuning.md)** - Optimization techniques
- **[Debugging Guide](../../../docs/articles/guides/debugging-guide.md)** - Cross-backend validation and troubleshooting
- **[Dependency Injection](../../../docs/articles/guides/dependency-injection.md)** - DI integration patterns

### Examples
- **[Basic Vector Operations](../../../docs/articles/examples/basic-vector-operations.md)** - Fundamental operations with benchmarks
- **[Multi-Kernel Pipelines](../../../docs/articles/examples/multi-kernel-pipelines.md)** - Chaining operations efficiently

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation
- **[IComputeOrchestrator](../../../api/DotCompute.Core.IComputeOrchestrator.html)** - Universal execution interface

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)

## Contributing

Contributions are welcome in:
- Additional optimization strategies
- Performance improvements
- Platform-specific enhancements
- Documentation and examples

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
