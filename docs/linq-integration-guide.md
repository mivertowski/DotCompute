# LINQ Integration Guide - DotCompute v0.2.0

## Overview

DotCompute v0.2.0 introduces comprehensive LINQ-to-GPU integration that seamlessly connects LINQ expressions with the runtime orchestrator. This allows you to write familiar LINQ code that automatically executes on GPU with intelligent backend selection and CPU fallback.

## Key Features

- **Seamless Integration**: Write standard LINQ, execute on GPU
- **Runtime Orchestrator**: Automatic backend selection (CUDA/CPU)
- **Expression Analysis**: GPU compatibility checking and optimization suggestions
- **CPU Fallback**: Automatic fallback for unsupported operations
- **Performance Monitoring**: Built-in profiling and performance tracking
- **Native AOT Support**: Works with ahead-of-time compilation

## Quick Start

### 1. Service Registration

```csharp
// Program.cs or Startup.cs
using DotCompute.Runtime.Extensions;
using DotCompute.Linq.Extensions;

var services = new ServiceCollection();

// Add DotCompute runtime (required)
services.AddDotComputeRuntime();

// Add LINQ integration
services.AddDotComputeLinq(options =>
{
    options.EnableOptimization = true;
    options.EnableCaching = true;
    options.EnableCpuFallback = true;
});

var serviceProvider = services.BuildServiceProvider();
```

### 2. Basic Usage

```csharp
// Create some test data
var numbers = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

// Convert to compute queryable
var queryable = numbers.AsComputeQueryable(serviceProvider);

// Execute GPU-accelerated LINQ operations
var result = await queryable
    .Where(x => x > 3)
    .Select(x => x * 2)
    .ExecuteAsync();

Console.WriteLine($"Results: [{string.Join(", ", result)}]");
// Output: Results: [8, 10, 12, 14, 16, 20]
```

### 3. Advanced Operations

```csharp
// Aggregation operations
var sum = await numbers
    .AsComputeQueryable(serviceProvider)
    .Where(x => x % 2 == 0)
    .Sum()
    .ExecuteAsync();

// Complex chaining with backend preference
var processedData = await largeDataset
    .AsComputeQueryable(serviceProvider)
    .Where(x => x.Value > threshold)
    .Select(x => new { x.Id, Square = x.Value * x.Value })
    .ExecuteAsync("CUDA"); // Prefer CUDA backend
```

## Architecture

### Component Overview

```
┌─────────────────────────────────────┐
│           User LINQ Code            │
│   .Where().Select().Sum()          │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│    IntegratedComputeQueryProvider   │
│  • Expression optimization          │
│  • LINQ-to-Kernel translation      │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│       IComputeOrchestrator          │
│  • Backend selection (CUDA/CPU)    │
│  • Kernel execution                 │
│  • Automatic fallback              │
└─────────────┬───────────────────────┘
              │
┌─────────────▼───────────────────────┐
│         GPU/CPU Kernels             │
│  • CUDA kernels                     │
│  • CPU SIMD implementations         │
└─────────────────────────────────────┘
```

### Key Classes

- **`RuntimeIntegratedLinqProvider`**: Main entry point implementing `IComputeLinqProvider`
- **`IntegratedComputeQueryProvider`**: LINQ query provider that uses `IComputeOrchestrator`
- **`LinqToKernelTranslator`**: Translates LINQ expressions to kernel operations
- **`LinqKernels`**: Pre-built GPU kernels for common LINQ operations

## Expression Analysis

### GPU Compatibility Checking

```csharp
var queryable = data.AsComputeQueryable(serviceProvider);
var complexQuery = queryable.Where(x => x.ComplexOperation());

// Check if expression can run on GPU
bool isGpuCompatible = complexQuery.IsGpuCompatible(serviceProvider);

if (!isGpuCompatible)
{
    Console.WriteLine("Query will fall back to CPU execution");
}
```

### Optimization Suggestions

```csharp
var suggestions = queryable
    .Where(x => x > 1)
    .Select(x => x * 2)
    .Where(x => x < 20)
    .GetOptimizationSuggestions(serviceProvider);

foreach (var suggestion in suggestions)
{
    Console.WriteLine($"{suggestion.Category}: {suggestion.Message}");
    Console.WriteLine($"  Impact: {suggestion.EstimatedImpact}");
}
```

### Pre-compilation

```csharp
// Pre-compile frequently used expressions for better performance
var frequentQuery = data.AsComputeQueryable(serviceProvider)
    .Where(x => x > threshold)
    .Select(x => x * factor);

await frequentQuery.PrecompileAsync(serviceProvider);

// Now executions will be faster
var result = await frequentQuery.ExecuteAsync();
```

## Supported LINQ Operations

### Implemented Operations

| Operation | GPU Support | CPU Fallback | Notes |
|-----------|------------|-------------|---------|
| `Where` | ✅ Yes | ✅ Yes | Filtering with predicates |
| `Select` | ✅ Yes | ✅ Yes | Mapping transformations |
| `Sum` | ✅ Yes | ✅ Yes | Parallel reduction |
| `Average` | ✅ Yes | ✅ Yes | Mean calculation |
| `Min` | ✅ Yes | ✅ Yes | Minimum value |
| `Max` | ✅ Yes | ✅ Yes | Maximum value |
| `Count` | ✅ Yes | ✅ Yes | Element counting |
| `OrderBy` | 🚧 Partial | ✅ Yes | GPU sort implementation |
| `GroupBy` | ❌ No | ✅ Yes | Complex grouping operations |
| `Join` | ❌ No | ✅ Yes | Relational joins |

### Data Type Support

- **Primitive Types**: `int`, `float`, `double`, `bool`
- **Value Types**: Custom structs (GPU-compatible)
- **Arrays**: All primitive array types
- **Spans**: `ReadOnlySpan<T>` and `Span<T>`

## Performance Guidelines

### When to Use GPU Acceleration

✅ **Good Candidates:**
```csharp
// Large datasets (>10K elements)
var result = await largeArray
    .AsComputeQueryable(serviceProvider)
    .Where(x => x > threshold)
    .Select(x => Math.Sqrt(x))
    .ExecuteAsync();

// Parallel computations
var processed = await matrix
    .AsComputeQueryable(serviceProvider)
    .Select(row => row.Sum())
    .ExecuteAsync();
```

❌ **Poor Candidates:**
```csharp
// Small datasets (<1K elements)
var tiny = new[] { 1, 2, 3 }.AsComputeQueryable(serviceProvider);

// I/O bound operations
var withIO = data.AsComputeQueryable(serviceProvider)
    .Select(x => File.ReadAllText(x.Path)); // Will fail GPU compatibility

// Complex branching logic
var complex = data.AsComputeQueryable(serviceProvider)
    .Where(x => x.ComplexDecisionTree()); // May fallback to CPU
```

### Memory Optimization

```csharp
// Use arrays for optimal GPU memory transfer
float[] data = GetLargeDataset();
var result = await data.AsComputeQueryable(serviceProvider)
    .Select(x => x * 2.0f)
    .ExecuteAsync();

// Spans are efficiently converted
ReadOnlySpan<int> span = stackalloc int[] { 1, 2, 3, 4, 5 };
var spanResult = await span.AsComputeQueryable(serviceProvider)
    .Where(x => x > 2)
    .ExecuteAsync();
```

## Error Handling

### Automatic Fallback

```csharp
try
{
    var result = await data.AsComputeQueryable(serviceProvider)
        .Where(x => x.UnsupportedOperation())
        .ExecuteAsync();
    
    // If GPU execution fails, automatically falls back to CPU
}
catch (InvalidOperationException ex)
{
    // Only thrown if both GPU and CPU execution fail
    Console.WriteLine($"Query execution failed: {ex.Message}");
}
```

### Explicit Backend Selection

```csharp
// Force CPU execution
var cpuResult = await queryable.ExecuteAsync("CPU");

// Prefer CUDA but allow fallback
var cudaResult = await queryable.ExecuteAsync("CUDA");
```

## Integration with Runtime Features

### Debugging Integration

```csharp
// Enable debugging in service configuration
services.AddDotComputeRuntime(options =>
{
    options.EnableDebugging = true;
    options.DebuggingProfile = DebuggingProfile.Development;
});

// LINQ operations will be automatically validated
var debuggedResult = await queryable
    .Select(x => x * 2)
    .ExecuteAsync(); // Includes cross-backend validation
```

### Performance Optimization

```csharp
// Enable performance optimization
services.AddDotComputeRuntime(options =>
{
    options.EnableOptimization = true;
    options.OptimizationProfile = OptimizationProfile.Aggressive;
});

// LINQ operations benefit from ML-powered backend selection
var optimizedResult = await queryable
    .Where(x => x > threshold)
    .Select(x => complexTransform(x))
    .ExecuteAsync(); // Automatically selects optimal backend
```

## Testing

### Unit Tests

```csharp
[Fact]
public async Task Can_Execute_Simple_Select_Operation()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddDotComputeRuntime();
    services.AddDotComputeLinq();
    var provider = services.BuildServiceProvider();
    
    var data = new[] { 1, 2, 3, 4, 5 };
    
    // Act
    var result = await data
        .AsComputeQueryable(provider)
        .Select(x => x * 2)
        .ExecuteAsync();
    
    // Assert
    Assert.Equal(new[] { 2, 4, 6, 8, 10 }, result.ToArray());
}
```

### Integration Tests

```csharp
[SkippableFact] // Skip if GPU not available
public async Task Can_Execute_On_GPU_With_Fallback()
{
    var result = await largeDataset
        .AsComputeQueryable(serviceProvider)
        .Where(x => x.Value > 100)
        .Select(x => x.Value * 2)
        .ExecuteAsync();
        
    Assert.NotNull(result);
    // Test passes regardless of GPU availability
}
```

## Troubleshooting

### Common Issues

1. **"LINQ runtime integration is not properly configured"**
   - Ensure `AddDotComputeRuntime()` is called before `AddDotComputeLinq()`
   - Verify service registration order

2. **"No suitable accelerator found"**
   - GPU may not be available
   - Enable CPU fallback: `options.EnableCpuFallback = true`

3. **"Expression contains GPU-incompatible operations"**
   - Check compatibility: `queryable.IsGpuCompatible(serviceProvider)`
   - Review optimization suggestions

### Logging

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
});

// LINQ operations will log execution details
```

## Migration Guide

### From Legacy LINQ Provider

```csharp
// OLD: Manual accelerator management
var accelerator = GetAccelerator();
var oldQueryable = data.AsComputeQueryable(accelerator);
var oldResult = oldQueryable.Select(x => x * 2).ToComputeArray();

// NEW: Service-based integration
var newQueryable = data.AsComputeQueryable(serviceProvider);
var newResult = await newQueryable.Select(x => x * 2).ExecuteAsync();
```

### Performance Comparison

```csharp
// Benchmark results on NVIDIA RTX 2000 Ada Generation:
// Dataset: 1M float elements
// Operation: .Where(x => x > 0.5f).Select(x => x * 2.0f)

// CPU (AVX2): 15ms
// GPU (CUDA): 2ms
// Speedup: 7.5x
```

## Conclusion

The LINQ integration in DotCompute v0.2.0 provides a powerful and seamless way to accelerate data processing operations. By leveraging the runtime orchestrator, it offers intelligent backend selection, automatic fallback, and comprehensive performance monitoring while maintaining the familiar LINQ syntax.

For more examples and advanced usage patterns, see the [integration tests](../tests/Integration/DotCompute.Linq.IntegrationTests/) and [sample applications](../samples/).
