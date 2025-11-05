# DotCompute.Abstractions

Core abstractions and interfaces for the DotCompute compute acceleration framework.

## Overview

DotCompute.Abstractions defines the foundational contracts and types that enable compute acceleration across heterogeneous hardware backends. This library provides interface definitions, enumerations, and model types used by all DotCompute components, ensuring consistent abstraction across CPU, GPU, and accelerator implementations.

## Key Components

### Core Interfaces

#### Accelerator Abstraction
- **IAccelerator**: Defines compute accelerator capabilities (CPU, CUDA, Metal, OpenCL)
- **IAcceleratorManager**: Manages accelerator discovery and lifecycle
- **IUnifiedAcceleratorFactory**: Factory for creating accelerators with workload profiles

#### Kernel Abstractions
- **IKernel**: Represents an executable compute kernel
- **ICompiledKernel**: Compiled kernel ready for execution
- **IUnifiedKernelCompiler**: Backend-agnostic kernel compilation
- **IKernelExecutor**: Manages kernel execution and scheduling
- **IKernelGenerator**: Generates kernel code for specific backends
- **IKernelManager**: Handles kernel lifecycle and caching

#### Orchestration
- **IComputeOrchestrator**: High-level interface for unified kernel execution
- **IComputeEngine**: Core compute engine abstraction
- **ICompilationMetadata**: Metadata for kernel compilation

### Memory Management

#### Unified Memory
- **IUnifiedMemoryManager**: Backend-agnostic memory allocation and management
- **IUnifiedMemoryBuffer**: Unified buffer interface for cross-device memory
- **ISyncMemoryManager**: Synchronous memory operations
- **ISyncMemoryBuffer**: Synchronous buffer interface
- **IDeviceMemory**: Device-specific memory representation

#### Memory Operations
- **MemoryOptions**: Configuration for memory allocation strategies
- **DeviceMemory**: Device memory abstraction
- **IMemoryTransferStats**: Statistics for memory transfer operations

### Pipeline System

#### Pipeline Interfaces
- **IPipeline**: Generic pipeline execution
- **IKernelPipeline**: Kernel-specific pipeline abstraction
- **IKernelChainBuilder**: Fluent API for building kernel chains
- **IParallelStageBuilder**: Parallel execution stage construction
- **IKernelStageBuilder**: Individual kernel stage builder

#### Pipeline Management
- **IPipelineOptimizer**: Optimizes pipeline execution graphs
- **IPipelineProfiler**: Profiles pipeline performance
- **IPipelineMemoryManager**: Manages memory in pipeline contexts
- **IPipelineMetrics**: Pipeline execution metrics
- **IStageMetrics**: Per-stage performance metrics

### Device Abstraction

#### Device Interfaces
- **IComputeDevice**: Represents physical or virtual compute device
- **IDeviceCapabilities**: Device capability detection
- **IDeviceMetrics**: Runtime device performance metrics
- **ICommandQueue**: Command queue abstraction
- **ICacheSizes**: Device cache hierarchy information
- **IDeviceMemoryInfo**: Device memory information

#### Device Models
- **DeviceCapabilities**: Detailed device capability description
- **DeviceFeatures**: Feature flags for device capabilities
- **DeviceStatus**: Current device operational status
- **ComputeDeviceType**: Enumeration of device types
- **CommandQueueOptions**: Configuration for command queues

### Debugging and Telemetry

#### Debugging
- **IKernelDebugService**: Cross-backend kernel validation and debugging
- **DebugProfile**: Debugging configuration profiles
- **DebugValidationResult**: Results from debug validation

#### Telemetry
- **ITelemetryProvider**: Telemetry data collection
- **ITelemetryCollector**: Telemetry event aggregation
- **TelemetryContext**: Contextual telemetry information
- **TelemetryOptions**: Telemetry configuration
- **MetricType**: Types of metrics collected

### Recovery and Resilience

- **IKernelExecutionMonitor**: Monitors kernel execution health
- **RecoveryCapability**: Defines recovery strategies
- **IFailureDetector**: Detects execution failures
- **IRetryPolicy**: Retry logic abstraction

### Configuration and Options

#### Compilation Options
- **CompilationOptions**: Kernel compilation configuration
- **OptimizationLevel**: Code optimization levels (None, O1, O2, O3, Aggressive)
- **CompilationOptionsExtensions**: Extension methods for compilation options

#### Execution Configuration
- **KernelExecutionContext**: Context for kernel execution
- **ExecutionPriority**: Execution priority levels
- **StreamFlags**: Stream configuration flags
- **KernelLaunchParameters**: Launch parameters for kernels

### Models and Types

#### Core Types
- **KernelDefinition**: Defines kernel metadata and source
- **AcceleratorType**: Enumeration of accelerator types
- **AcceleratorFeature**: Feature flags for accelerators
- **AcceleratorContext**: Runtime context for accelerators
- **Dim3**: 3D dimension specification (grid/block dimensions)

#### Performance Types
- **PerformanceProfile**: Performance characteristics profile
- **WorkloadProfile**: Workload classification profile
- **StealingStatistics**: Work-stealing thread pool statistics

#### Result Types
- **PipelineExecutionResult**: Results from pipeline execution
- **PipelineValidationResult**: Pipeline validation results
- **StageExecutionResult**: Individual stage execution results

### Exceptions and Validation

#### Exceptions
- **ComputeException**: Base exception for compute operations
- **CompilationException**: Kernel compilation failures
- **DeviceException**: Device-related errors
- **MemoryException**: Memory operation failures

#### Validation
- **IValidator**: Validation interface
- **ValidationResult**: Validation outcome

### Utilities

- **AcceleratorUtilities**: Helper methods for accelerator operations
- **DisposalUtilities**: Resource disposal helpers
- **TypeConverters**: Type conversion utilities (e.g., Dim3TypeConverter)

## Installation

```bash
dotnet add package DotCompute.Abstractions --version 0.3.0-rc1
```

## Usage

### Defining a Compute Backend

```csharp
using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;

public class CustomAccelerator : IAccelerator
{
    public AcceleratorInfo Info { get; }
    public AcceleratorType Type => AcceleratorType.Custom;
    public string DeviceType => "CustomDevice";
    public IUnifiedMemoryManager Memory { get; }
    public IUnifiedMemoryManager MemoryManager => Memory;
    public AcceleratorContext Context { get; }
    public bool IsAvailable { get; private set; }

    public async ValueTask<ICompiledKernel> CompileKernelAsync(
        KernelDefinition definition,
        CompilationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Backend-specific compilation logic
        return await CompileKernelInternalAsync(definition, options, cancellationToken);
    }

    public async ValueTask SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        // Synchronization logic
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup logic
        await Task.CompletedTask;
    }
}
```

### Using Kernel Definitions

```csharp
using DotCompute.Abstractions.Kernels;

var kernelDef = new KernelDefinition
{
    Name = "VectorAdd",
    Source = @"
        kernel void vector_add(global float* a, global float* b, global float* result, int n)
        {
            int idx = get_global_id(0);
            if (idx < n) {
                result[idx] = a[idx] + b[idx];
            }
        }
    ",
    EntryPoint = "vector_add",
    Backend = AcceleratorType.CUDA
};
```

### Configuring Compilation Options

```csharp
using DotCompute.Abstractions;

var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.O3,
    GenerateDebugInfo = false,
    TargetArchitecture = "sm_89",
    CustomOptions = new[] { "--use_fast_math" }
};
```

### Building Kernel Pipelines

```csharp
using DotCompute.Abstractions.Interfaces.Pipelines;

var pipeline = await pipelineBuilder
    .AddStage("Preprocessing", preprocessKernel)
    .AddStage("MainComputation", computeKernel)
    .AddStage("Postprocessing", postprocessKernel)
    .WithOptimization()
    .BuildAsync();

var result = await pipeline.ExecuteAsync(inputData);
```

### Memory Management

```csharp
using DotCompute.Abstractions;

// Allocate unified memory buffer
var buffer = await accelerator.Memory.AllocateAsync<float>(1_000_000);

// Copy data to device
await buffer.CopyFromAsync(hostData);

// Use in kernel execution
await kernel.ExecuteAsync(buffer);

// Retrieve results
await buffer.CopyToAsync(resultData);

// Dispose when done
await buffer.DisposeAsync();
```

## Architecture Patterns

### Interface Segregation
The abstractions follow the Interface Segregation Principle (ISP), providing focused interfaces for specific capabilities rather than monolithic contracts.

### Backend Independence
All interfaces are designed to be backend-agnostic, enabling implementations for diverse hardware (CPUs, NVIDIA GPUs, AMD GPUs, Apple GPUs, TPUs).

### Memory Abstraction Layers
Three levels of memory abstraction:
1. **Unified Memory** (IUnifiedMemoryManager): Cross-device abstraction
2. **Device Memory** (IDeviceMemory): Device-specific memory
3. **Synchronous Memory** (ISyncMemoryManager): Synchronous operations

### Factory Pattern
Factory interfaces (IUnifiedAcceleratorFactory) enable workload-aware accelerator selection.

## Type System

### Enumerations

#### AcceleratorType
```csharp
public enum AcceleratorType
{
    CPU,
    CUDA,
    Metal,
    OpenCL,
    DirectCompute,
    Vulkan,
    Auto
}
```

#### OptimizationLevel
```csharp
public enum OptimizationLevel
{
    None,
    O1,      // Basic optimizations
    O2,      // Standard optimizations
    O3,      // Aggressive optimizations
    Aggressive
}
```

#### ExecutionPriority
```csharp
public enum ExecutionPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

## Native AOT Compatibility

All types in DotCompute.Abstractions are designed for Native AOT compatibility:
- No runtime code generation dependencies
- Trimming-safe attribute usage
- AOT analyzer verification enabled
- No reflection-based operations

## Dependencies

- **Microsoft.Extensions.Logging.Abstractions**: Logging infrastructure
- **Microsoft.Extensions.DependencyInjection.Abstractions**: Dependency injection
- **Microsoft.Extensions.Configuration.Abstractions**: Configuration support
- **Microsoft.Extensions.Options**: Options pattern
- **System.Memory**: Span and Memory types
- **System.Runtime.CompilerServices.Unsafe**: Unsafe memory operations

## System Requirements

- .NET 9.0 or later
- C# 13 language features
- Native AOT compatible runtime (optional)

## Design Principles

1. **Zero-cost abstractions**: Interfaces compile to optimal code
2. **Memory safety**: Span<T> and Memory<T> usage throughout
3. **Async-first**: ValueTask<T> for performance-critical paths
4. **Disposable resources**: IAsyncDisposable for proper cleanup
5. **Nullable reference types**: Full nullable annotations
6. **Performance**: Server GC configuration enabled

## Target Scenarios

This library supports multiple usage scenarios:

- **Backend Implementers**: Create new compute backends (e.g., DirectX, Vulkan)
- **Framework Consumers**: Use DotCompute for compute acceleration
- **Extension Developers**: Build on top of DotCompute abstractions
- **Testing**: Mock implementations for unit testing

## Documentation & Resources

Comprehensive documentation is available for DotCompute:

### Architecture Documentation
- **[System Overview](../../../docs/articles/architecture/overview.md)** - Architecture and design principles
- **[Core Orchestration](../../../docs/articles/architecture/core-orchestration.md)** - IComputeOrchestrator and execution pipeline
- **[Backend Integration](../../../docs/articles/architecture/backend-integration.md)** - IAccelerator plugin system
- **[Memory Management](../../../docs/articles/architecture/memory-management.md)** - Unified memory architecture

### Developer Guides
- **[Getting Started](../../../docs/articles/getting-started.md)** - Installation and first kernel
- **[Kernel Development](../../../docs/articles/guides/kernel-development.md)** - Writing efficient compute kernels
- **[Backend Selection](../../../docs/articles/guides/backend-selection.md)** - Choosing optimal execution backend
- **[Dependency Injection](../../../docs/articles/guides/dependency-injection.md)** - DI integration and testing

### API Documentation
- **[API Reference](../../../api/index.md)** - Complete API documentation
- **[Interface Catalog](../../../api/DotCompute.Abstractions.html)** - All abstractions and interfaces

## Support

- **Documentation**: [Comprehensive Guides](../../../docs/index.md)
- **Issues**: [GitHub Issues](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)

## Contributing

Contributions are welcome. When adding new abstractions:

1. Follow existing interface naming conventions
2. Ensure Native AOT compatibility
3. Add XML documentation comments
4. Maintain backward compatibility where possible
5. Include unit tests for models and utilities

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for detailed guidelines.

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
