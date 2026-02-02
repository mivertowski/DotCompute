# DOTCOMPUTE0004: LINQ Optimization Interfaces - Experimental

## Summary

| Property | Value |
|----------|-------|
| **Diagnostic ID** | DOTCOMPUTE0004 |
| **Severity** | Warning |
| **Category** | Experimental |
| **Affected APIs** | `IOptimizationPipeline`, `IOptimizer`, `IKernelFusionOptimizer`, `IMemoryOptimizer`, `IPerformanceProfiler`, `IOptimizationEngine` |

## Description

The LINQ optimization interfaces are marked as experimental because they represent Phase 2 of the LINQ GPU acceleration work. While functional implementations exist, the API surface may change based on performance tuning and user feedback. Using these APIs in production may result in:

- **API Changes**: Interface signatures may evolve
- **Optimization Behavior**: Optimization strategies may change between versions
- **Performance Variance**: Results may vary as heuristics are tuned

## Current Implementation Status

| Interface | Implementation | Status |
|-----------|---------------|--------|
| `IOptimizationPipeline` | `OptimizationPipeline` | Functional |
| `IOptimizer` | Multiple implementations | Functional |
| `IKernelFusionOptimizer` | `KernelFusionOptimizer` | Functional |
| `IMemoryOptimizer` | `MemoryOptimizer` | Functional |
| `IPerformanceProfiler` | `PerformanceProfiler` | Functional |
| `IOptimizationEngine` | `AdaptiveOptimizer` | Functional |

## Supported Optimization Patterns

### Kernel Fusion

| Pattern | Support | Performance Gain |
|---------|---------|-----------------|
| Map + Map | Yes | 20-40% |
| Map + Filter | Yes | 30-50% |
| Filter + Map | Yes | 30-50% |
| Filter + Filter | Yes | 25-45% |
| Reduce + Reduce | Yes | 15-30% |

### Memory Optimization

| Optimization | Support | Notes |
|--------------|---------|-------|
| Access Pattern Analysis | Yes | Detects sequential/strided/random |
| Coalescing Recommendations | Yes | GPU memory optimization |
| Buffer Reuse | Partial | Basic lifetime analysis |
| Prefetching Hints | Partial | For sequential patterns |

## When to Suppress

You may suppress this warning if:

1. You are building custom optimization pipelines
2. You understand the API may change and can adapt
3. You are implementing custom optimizers
4. You need fine-grained control over GPU compilation

## How to Suppress

### In Code

```csharp
#pragma warning disable DOTCOMPUTE0004
var pipeline = new OptimizationPipeline();
pipeline.AddOptimizer(new KernelFusionOptimizer(logger));
#pragma warning restore DOTCOMPUTE0004
```

### In Project File

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DOTCOMPUTE0004</NoWarn>
</PropertyGroup>
```

### Using SuppressMessage Attribute

```csharp
[SuppressMessage("Experimental", "DOTCOMPUTE0004:LINQ optimization interfaces are experimental")]
public IOptimizationPipeline CreatePipeline()
{
    return new OptimizationPipeline()
        .AddOptimizer(fusionOptimizer)
        .AddOptimizer(memoryOptimizer);
}
```

## Recommended Usage

For stable LINQ GPU acceleration, use the high-level APIs:

```csharp
// Preferred: High-level LINQ extensions (stable)
var result = await data
    .AsGpuQueryable()
    .Where(x => x > 0)
    .Select(x => x * 2)
    .ToArrayAsync();

// Advanced: Custom optimization (experimental)
#pragma warning disable DOTCOMPUTE0004
var pipeline = new OptimizationPipeline();
pipeline.AddOptimizer(new KernelFusionOptimizer(logger));
var optimizedGraph = pipeline.Optimize(operationGraph);
#pragma warning restore DOTCOMPUTE0004
```

## Custom Optimizer Implementation

To implement a custom optimizer:

```csharp
#pragma warning disable DOTCOMPUTE0004
public class MyCustomOptimizer : IOptimizer
{
    public string Name => "MyCustomOptimizer";
    public int Priority => 100; // Higher = runs earlier

    public OperationGraph Optimize(OperationGraph graph)
    {
        // Your optimization logic
        return optimizedGraph;
    }

    public bool CanOptimize(OperationGraph graph)
    {
        // Check if this optimizer applies
        return graph.Operations.Any(op => op.Type == OperationType.Map);
    }
}
#pragma warning restore DOTCOMPUTE0004
```

## Advanced Operation Support (v0.5.6+)

### Join Operations

| Feature | Support | Notes |
|---------|---------|-------|
| Inner Join | Yes | Hash-based with linear probing |
| Left Outer Join | Yes | Returns all left rows |
| Semi Join | Yes | Existence check |
| Anti Join | Yes | Non-matching rows |
| **Key Selector Expressions** | **Yes** | Custom key extraction via lambda |
| Multi-table Join | Partial | Chain multiple joins |

```csharp
// Join with custom key selectors
var config = new JoinConfiguration
{
    OuterKeySelector = (Expression<Func<Customer, int>>)(c => c.Id),
    InnerKeySelector = (Expression<Func<Order, int>>)(o => o.CustomerId),
    JoinType = JoinType.Inner
};
```

### GroupBy Operations

| Aggregation | Support | Notes |
|-------------|---------|-------|
| Count | Yes | Element count per group |
| Sum | **Yes** | Atomic floating-point sum |
| Min | **Yes** | CAS-based atomic minimum |
| Max | **Yes** | CAS-based atomic maximum |
| Average | **Yes** | Computed as Sum/Count |
| **Key Selector** | **Yes** | Custom grouping key |
| **Value Selector** | **Yes** | Custom aggregation value |

```csharp
// GroupBy with aggregation
var config = new GroupByConfiguration
{
    KeySelector = (Expression<Func<Order, int>>)(o => o.CustomerId),
    ValueSelector = (Expression<Func<Order, decimal>>)(o => o.Amount),
    Aggregations = AggregationFunction.Sum | AggregationFunction.Average
};
```

### OrderBy Operations

| Feature | Support | Notes |
|---------|---------|-------|
| Ascending Sort | Yes | Default bitonic sort |
| Descending Sort | Yes | Inverted comparisons |
| **Multi-Block Sort** | **Yes** | Arrays up to 16M elements |
| Key Selector | **Yes** | Sort by extracted field |
| Stable Sort | Partial | Index-based tie breaking |

```csharp
// OrderBy with key selector
var config = new OrderByConfiguration
{
    KeySelector = (Expression<Func<Order, DateTime>>)(o => o.OrderDate),
    Descending = true
};
```

## Known Limitations

1. **Complex Predicates**: May not fuse correctly with advanced operations
2. **Cross-Backend Optimization**: Limited support for OpenCL/Metal advanced ops
3. **Dynamic Workloads**: Adaptive optimization still learning
4. **OpenCL Float Atomics**: Requires OpenCL 2.0+ for full GroupBy support

## Roadmap

- **v0.5.6**: ✅ Join/GroupBy/OrderBy kernel generators with key selectors
- **v0.6.0**: Stabilize IOptimizationPipeline interface
- **v0.7.0**: Cross-backend optimization improvements
- **v0.8.0**: Improve adaptive optimization
- **v1.0.0**: Promote to stable API

## Related Resources

- [LINQ GPU Acceleration Guide](../guides/linq-gpu.md)
- [Query Optimization](../articles/query-optimization.md)
- [Performance Tuning](../articles/performance-tuning.md)
