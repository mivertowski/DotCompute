# DotCompute.Linq

LINQ provider for GPU-accelerated query execution with expression compilation to compute kernels.

## Status: 🚧 In Development

The LINQ module currently provides minimal foundation infrastructure with planned comprehensive features:
- **Basic LINQ Extensions**: Simple compute-enabled query operations (current)
- **Expression Compilation Pipeline**: Direct LINQ-to-kernel compilation (planned Phase 5)
- **Reactive Extensions Integration**: GPU-accelerated streaming compute (planned)
- **Advanced Optimization**: ML-based optimization and kernel fusion (planned)
- **Multi-Backend Support**: CPU, CUDA, Metal kernel generation (planned)

## Current Features (v0.2.0-alpha)

### Basic LINQ Extensions

#### ComputeQueryable<T>
Minimal LINQ query provider for compute operations:
- Expression tree representation
- Basic query provider implementation
- Integration with standard LINQ

#### Extension Methods
- **AsComputeQueryable()**: Convert IQueryable to compute-enabled queryable
- **ToComputeArray()**: Execute query and materialize results
- **ComputeSelect()**: Map operation with compute acceleration intent
- **ComputeWhere()**: Filter operation with compute acceleration intent

### Service Integration

#### Dependency Injection Support
- **AddDotComputeLinq()**: Register LINQ services
- **IComputeLinqProvider**: Service interface for compute LINQ
- **ComputeLinqProvider**: Default provider implementation

## Current Usage

### Basic Query Operations

```csharp
using DotCompute.Linq;

// Convert queryable to compute-enabled
var data = Enumerable.Range(0, 1000).AsQueryable();
var computeData = data.AsComputeQueryable();

// Use compute-enabled select
var results = computeData
    .ComputeSelect(x => x * 2)
    .ComputeWhere(x => x > 100)
    .ToComputeArray();
```

### Service Registration

```csharp
using DotCompute.Linq.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add DotCompute LINQ services
services.AddDotComputeLinq();

var provider = services.BuildServiceProvider();
var linqProvider = provider.GetRequiredService<IComputeLinqProvider>();

// Create compute queryable
var queryable = linqProvider.CreateComputeQueryable(data);
```

## Planned Features (Phase 5)

### Expression Compilation Pipeline

The planned expression compilation system will provide:

#### LINQ-to-Kernel Compiler
- **Expression Tree Analysis**: Decompose LINQ expressions into kernel operations
- **Type Inference**: Automatic type resolution for complex expressions
- **Dependency Detection**: Identify data dependencies for optimization
- **Multi-Backend Code Generation**: Generate CPU SIMD, CUDA GPU, and Metal kernels
- **Kernel Caching**: Cache compiled kernels with TTL and invalidation
- **Parallel Compilation**: Batch compilation for multiple queries

#### Supported LINQ Operations
```csharp
// Map operations
data.Select(x => x * 2)
    .Select(x => Math.Sqrt(x))
    .Select(x => x + 1);

// Reduce operations
data.Sum()
data.Average()
data.Min() / Max()
data.Aggregate((a, b) => a + b);

// Filter operations
data.Where(x => x > 100)
    .Where(x => x % 2 == 0);

// Join operations
data1.Join(data2, x => x.Id, y => y.Id, (x, y) => new { x, y });

// Group operations
data.GroupBy(x => x.Category)
    .Select(g => new { Category = g.Key, Sum = g.Sum() });

// Order operations
data.OrderBy(x => x.Value)
    .ThenBy(x => x.Name);
```

### Reactive Extensions Integration

GPU-accelerated streaming compute with Rx.NET:

#### Observable Compute Operations
```csharp
using System.Reactive.Linq;
using DotCompute.Linq.Reactive;

// Create observable compute stream
var dataStream = Observable.Interval(TimeSpan.FromMilliseconds(10))
    .Select(i => GenerateData())
    .AsComputeObservable(accelerator);

// GPU-accelerated transformations
var results = dataStream
    .ComputeSelect(x => x * 2)          // GPU kernel
    .ComputeWhere(x => x > threshold)    // GPU kernel
    .ComputeAggregate((a, b) => a + b)  // GPU reduction
    .Subscribe(result => Console.WriteLine(result));
```

#### Streaming Features
- **Adaptive Batching**: Automatically batch operations for GPU efficiency
- **Backpressure Handling**: Multiple strategies for flow control
- **Windowing Operations**: Tumbling, sliding, and time-based windows
- **Stream Fusion**: Combine multiple operations into single kernel

### Advanced Optimization Strategies

ML-powered query optimization:

#### Cost-Based Optimizer
```csharp
var optimizer = new QueryOptimizer(accelerator);

// Automatically selects best execution strategy
var optimized = optimizer.Optimize(query, new OptimizationOptions
{
    Strategy = OptimizationStrategy.Balanced,
    EnableKernelFusion = true,
    EnableMemoryOptimization = true,
    UseMachineLearning = true
});
```

#### Optimization Features
- **Kernel Fusion**: Merge multiple operations to reduce memory transfers
- **Memory Access Optimization**: Coalesce and reorder memory operations
- **Dynamic Parallelization**: Adaptive thread/block sizing
- **Load Balancing**: Distribute work across compute units
- **ML-Based Selection**: Learn from execution patterns

### GPU Kernel Generation

Direct kernel generation from LINQ expressions:

#### CUDA Kernel Templates
```cuda
// Generated from: data.Select(x => x * 2).Sum()
__global__ void map_multiply_kernel(float* input, float* output, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        output[idx] = input[idx] * 2.0f;
    }
}

__global__ void reduce_sum_kernel(float* input, float* output, int n) {
    extern __shared__ float sdata[];
    int tid = threadIdx.x;
    int idx = blockIdx.x * blockDim.x + threadIdx.x;

    sdata[tid] = (idx < n) ? input[idx] : 0.0f;
    __syncthreads();

    // Warp-level reduction
    for (int s = blockDim.x/2; s > 0; s >>= 1) {
        if (tid < s) sdata[tid] += sdata[tid + s];
        __syncthreads();
    }

    if (tid == 0) output[blockIdx.x] = sdata[0];
}
```

#### Kernel Features
- **8 Kernel Templates**: Map, reduce, filter, scan, join, group, sort, scatter/gather
- **Compute Capability Support**: CC 5.0-8.9 (Maxwell to Ada Lovelace)
- **Warp-Level Primitives**: Shuffle, vote, ballot operations
- **Shared Memory Optimization**: Automatic shared memory usage
- **Memory Coalescing**: Optimized global memory access patterns
- **Memory Pooling**: 90% allocation reduction through pooling

### Expression Compilation Architecture

```
LINQ Expression
    ↓
Expression Tree Analysis
    ↓
Operation Decomposition
    ↓
Type Inference & Validation
    ↓
Backend Selection (CPU/CUDA/Metal)
    ↓
Code Generation
    ├── CPU: SIMD intrinsics
    ├── CUDA: PTX/CUBIN
    └── Metal: MSL shaders
    ↓
Kernel Compilation & Caching
    ↓
Execution with Memory Management
    ↓
Result Materialization
```

## Planned Usage Examples (Phase 5)

### Complex Query with Optimization

```csharp
using DotCompute.Linq;

var accelerator = await CudaAccelerator.CreateAsync();

// Complex LINQ query automatically compiled to GPU kernels
var result = data
    .AsComputeQueryable(accelerator)
    .Where(x => x.Temperature > 20.0f)
    .Select(x => new { x.Id, Celsius = x.Temperature, Fahrenheit = x.Temperature * 9/5 + 32 })
    .GroupBy(x => x.Id / 100)
    .Select(g => new {
        GroupId = g.Key,
        AvgCelsius = g.Average(x => x.Celsius),
        MaxFahrenheit = g.Max(x => x.Fahrenheit)
    })
    .OrderBy(x => x.GroupId)
    .ToComputeArrayAsync(); // Execute on GPU

// Kernel fusion automatically combines operations
```

### Streaming Analytics

```csharp
using DotCompute.Linq.Reactive;

var sensorStream = Observable
    .Interval(TimeSpan.FromMilliseconds(1))
    .Select(_ => ReadSensor())
    .AsComputeObservable(accelerator);

// Real-time GPU analytics
var alerts = sensorStream
    .Window(TimeSpan.FromSeconds(1))  // 1-second windows
    .ComputeSelect(window => window.Average())  // GPU average per window
    .ComputeWhere(avg => avg > threshold)       // GPU filter
    .Subscribe(avg => SendAlert(avg));
```

### Custom Reduction Operations

```csharp
// Custom aggregation compiled to GPU reduction kernel
var customSum = data
    .AsComputeQueryable(accelerator)
    .Aggregate(
        seed: 0.0,
        func: (acc, x) => acc + x * x,  // Sum of squares
        resultSelector: x => Math.Sqrt(x)  // Root mean square
    );
```

## Planned Performance Characteristics

Based on design specifications and benchmarks from similar systems:

### Expected Speedup (Phase 5)
- **Simple Map**: 10-50x over CPU LINQ
- **Reductions**: 20-100x over CPU LINQ
- **Complex Queries**: 5-30x over CPU LINQ (depends on data transfer overhead)
- **Streaming**: Real-time processing of 100K-1M events/second

### Memory Efficiency
- **Kernel Fusion**: 2-5x reduction in memory transfers
- **Memory Pooling**: 90% reduction in allocation overhead
- **Lazy Evaluation**: Minimal intermediate materialization

## Installation

```bash
dotnet add package DotCompute.Linq --version 0.2.0-alpha
```

## System Requirements

- .NET 9.0 or later
- System.Reactive 6.0+
- DotCompute.Core and dependencies

### For GPU Acceleration (Phase 5)
- **CUDA**: NVIDIA GPU with CC 5.0+
- **Metal**: Apple Silicon or AMD GPU on macOS
- **Minimum**: 4GB RAM, 2GB VRAM
- **Recommended**: 16GB RAM, 8GB+ VRAM

## Configuration

### Current Configuration

```csharp
var services = new ServiceCollection();
services.AddDotComputeLinq();

// Get LINQ provider
var provider = serviceProvider.GetRequiredService<IComputeLinqProvider>();
```

### Planned Configuration (Phase 5)

```csharp
var options = new ComputeLinqOptions
{
    DefaultBackend = AcceleratorType.CUDA,
    EnableKernelFusion = true,
    EnableCaching = true,
    CacheMaxSize = 100,
    CacheTTL = TimeSpan.FromMinutes(30),
    OptimizationStrategy = OptimizationStrategy.Aggressive,
    EnableMachineLearning = true
};

services.AddDotComputeLinq(options);
```

## Current Limitations

1. **No GPU Execution**: Current implementation executes on CPU
2. **No Kernel Generation**: LINQ expressions are not compiled to kernels
3. **Limited Operations**: Only basic Select/Where provided
4. **No Optimization**: No query optimization or kernel fusion
5. **No Reactive Support**: Rx.NET integration not implemented

## Roadmap to Phase 5

### Milestone 1: Expression Analysis (Planned)
- Expression tree visitor implementation
- Type inference system
- Dependency graph construction
- Operation categorization (map/reduce/filter/etc.)

### Milestone 2: Code Generation (Planned)
- CPU SIMD code generator
- CUDA kernel template system
- Metal shader language generator
- Kernel compilation pipeline

### Milestone 3: Optimization (Planned)
- Kernel fusion engine
- Memory access pattern optimization
- Cost-based query planning
- ML-based backend selection

### Milestone 4: Reactive Extensions (Planned)
- Observable compute wrapper
- Adaptive batching system
- Backpressure strategies
- Window operations

### Milestone 5: Production Hardening (Planned)
- Comprehensive testing suite
- Performance benchmarking
- Error handling and diagnostics
- Documentation and examples

## Design Principles (Planned)

1. **Transparent Acceleration**: GPU usage should be transparent to developers
2. **Fallback Support**: Always provide CPU fallback
3. **Composability**: All operations should be composable
4. **Type Safety**: Full compile-time type checking
5. **Performance**: Minimize overhead, maximize throughput
6. **Compatibility**: Work with existing LINQ code where possible

## Dependencies

- **DotCompute.Core**: Core runtime
- **DotCompute.Abstractions**: Interface definitions
- **DotCompute.Memory**: Memory management
- **System.Linq.Expressions**: Expression trees
- **System.Reactive**: Reactive extensions (for Phase 5)
- **Microsoft.Extensions.Caching.Memory**: Query cache (for Phase 5)

## Contributing

Contributions are welcome, particularly in:
- Expression tree analysis and compilation
- CUDA/Metal kernel generation
- Query optimization algorithms
- Performance benchmarking
- Documentation and examples

The Phase 5 implementation will be a significant undertaking. Early contributors will have substantial impact on the architecture and design.

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## References

### Similar Systems
- **LINQ to GPU**: Historical GPU LINQ implementations
- **TorchSharp**: ML tensor operations with LINQ-like API
- **Alea GPU**: F# GPU programming with quotations
- **ILGPU**: IL-based GPU kernel compilation

### Planned Technical Approach
Expression compilation follows proven patterns:
1. **Expression Tree Visitor Pattern**: Traverse and analyze LINQ expressions
2. **Template-Based Code Generation**: Generate backend-specific code from templates
3. **Just-In-Time Compilation**: Compile kernels on-demand with caching
4. **Cost-Based Optimization**: Use statistics to guide optimization decisions

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
