# DotCompute.Linq API Reference Guide

This comprehensive guide covers all public APIs in the DotCompute.Linq pipeline system, including detailed usage patterns, performance considerations, and best practices.

## Table of Contents

- [Core Extensions](#core-extensions)
- [Pipeline Extensions](#pipeline-extensions)
- [Interfaces](#interfaces)
- [Configuration](#configuration)
- [Performance Analysis](#performance-analysis)
- [Error Handling](#error-handling)
- [Advanced Features](#advanced-features)

## Core Extensions

### ComputeQueryableExtensions

The primary entry point for GPU-accelerated LINQ operations.

#### AsComputeQueryable Methods

##### AsComputeQueryable\<T>(IEnumerable\<T>, IServiceProvider, IAccelerator?)

```csharp
public static IQueryable<T> AsComputeQueryable<T>(
    this IEnumerable<T> source,
    IServiceProvider serviceProvider,
    IAccelerator? accelerator = null)
```

**Purpose**: Converts any enumerable to a GPU-accelerated queryable.

**Parameters**:

- `source`: The data to process (will be materialized if not already an array)
- `serviceProvider`: DI container with DotCompute services
- `accelerator`: Optional specific accelerator (null = automatic selection)

**Returns**: GPU-accelerated queryable supporting standard LINQ operations

**Performance Notes**:

- IEnumerable sources are materialized into arrays for GPU transfer
- Zero-copy for arrays and compatible memory types
- Automatic backend selection based on workload analysis

**Example**:

```csharp
var data = GetLargeDataset();
var results = await data.AsComputeQueryable(services)
    .Where(x => x.Value > threshold)
    .Select(x => x.ProcessedValue)
    .ExecuteAsync();
```

##### AsComputeQueryable\<T>(T[], IServiceProvider, IAccelerator?)

```csharp
public static IQueryable<T> AsComputeQueryable<T>(
    this T[] source,
    IServiceProvider serviceProvider,
    IAccelerator? accelerator = null)
```

**Purpose**: Optimized overload for arrays with zero-copy data transfer.

**Performance Benefits**:

- No data copying for arrays
- Direct GPU memory mapping when possible
- Faster initialization than IEnumerable overload

##### AsComputeQueryable\<T>(ReadOnlySpan\<T>, IServiceProvider, IAccelerator?)

```csharp
public static IQueryable<T> AsComputeQueryable<T>(
    this ReadOnlySpan<T> source,
    IServiceProvider serviceProvider,
    IAccelerator? accelerator = null) where T : unmanaged
```

**Purpose**: High-performance overload for span-based data.

**Constraints**: T must be unmanaged for GPU compatibility

**Performance Benefits**:

- Optimal for stack-allocated data
- Minimal memory overhead
- Direct memory access patterns

#### Execution Methods

##### ExecuteAsync\<T>(IQueryable\<T>, CancellationToken)

```csharp
public static async Task<IEnumerable<T>> ExecuteAsync<T>(
    this IQueryable<T> queryable,
    CancellationToken cancellationToken = default)
```

**Purpose**: Executes the query with automatic backend selection and optimization.

**Execution Flow**:

1. Expression analysis and optimization
2. Backend selection (GPU → CPU SIMD → Standard)
3. Kernel compilation and caching
4. Memory transfer and execution
5. Result retrieval and cleanup

**Error Handling**: Automatic fallback on GPU failures

**Example**:

```csharp
try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var results = await queryable.ExecuteAsync(cts.Token);
    ProcessResults(results);
}
catch (OperationCanceledException)
{
    // Handle timeout
}
```

##### ExecuteAsync\<T>(IQueryable\<T>, string, CancellationToken)

```csharp
public static async Task<IEnumerable<T>> ExecuteAsync<T>(
    this IQueryable<T> queryable, 
    string preferredBackend,
    CancellationToken cancellationToken = default)
```

**Purpose**: Executes with a specific backend preference.

**Backend Options**:

- `"CUDA"` - NVIDIA GPU acceleration
- `"CPU"` - CPU with SIMD optimization
- `"Metal"` - Apple GPU (future support)
- `"ROCm"` - AMD GPU (future support)

#### GPU-Accelerated Aggregations

##### ComputeSum Methods

```csharp
public static int ComputeSum(this IQueryable<int> source)
public static float ComputeSum(this IQueryable<float> source)
public static double ComputeSum(this IQueryable<double> source)
```

**Performance**: Uses parallel tree reduction for O(log n) complexity

**GPU Optimization**: Optimized kernel with shared memory utilization

**Example**:

```csharp
var numbers = Enumerable.Range(1, 10_000_000).AsComputeQueryable(services);
var sum = numbers.ComputeSum(); // ~50x faster than standard LINQ
```

##### ComputeAverage Methods

```csharp
public static double ComputeAverage(this IQueryable<int> source)
public static float ComputeAverage(this IQueryable<float> source)
public static double ComputeAverage(this IQueryable<double> source)
```

**Implementation**: Single-pass algorithm combining sum and count operations

#### Analysis and Optimization Methods

##### GetOptimizationSuggestions

```csharp
public static IEnumerable<OptimizationSuggestion> GetOptimizationSuggestions(
    this IQueryable queryable,
    IServiceProvider serviceProvider)
```

**Analysis Areas**:

- Operation reordering opportunities
- Kernel fusion potential
- Memory access pattern optimization
- Backend compatibility assessment

**Suggestion Categories**:

- Performance (execution speed improvements)
- Memory (memory usage optimization)
- Compatibility (GPU/backend compatibility)
- Architecture (structural improvements)

**Example**:

```csharp
var suggestions = query.GetOptimizationSuggestions(services);
foreach (var suggestion in suggestions.OrderByDescending(s => s.EstimatedImpact))
{
    Console.WriteLine($"{suggestion.Severity}: {suggestion.Message}");
    Console.WriteLine($"Estimated improvement: {suggestion.EstimatedImpact:P}");
}
```

##### IsGpuCompatible

```csharp
public static bool IsGpuCompatible(
    this IQueryable queryable,
    IServiceProvider serviceProvider)
```

**Compatibility Checks**:

- Data type compatibility (value types preferred)
- Operation support (mathematical, logical, comparison)
- Memory access patterns
- Backend feature requirements

##### PrecompileAsync

```csharp
public static async Task PrecompileAsync(
    this IQueryable queryable,
    IServiceProvider serviceProvider)
```

**Benefits**:

- Eliminates first-execution compilation overhead
- Validates expression compatibility at build time
- Enables warm startup scenarios

**Best Practices**:

- Pre-compile during application startup
- Cache compiled kernels across application sessions
- Use for frequently executed query patterns

## Pipeline Extensions

### PipelineQueryableExtensions

Advanced pipeline operations for complex data processing workflows.

#### Pipeline Creation

##### AsKernelPipeline\<T>

```csharp
public static object AsKernelPipeline<T>(
    this IQueryable<T> source, 
    IServiceProvider services) where T : unmanaged
```

**Purpose**: Converts LINQ expressions to kernel pipeline representation.

**Capabilities**:

- Multi-stage optimization
- Kernel fusion opportunities
- Advanced caching strategies
- Performance monitoring integration

##### AsComputePipeline\<T>

```csharp
public static object AsComputePipeline<T>(
    this IEnumerable<T> source,
    IServiceProvider services) where T : unmanaged
```

**Purpose**: Creates pipeline from enumerable data source.

#### Pipeline Operations

##### ThenSelect\<TIn, TOut>

```csharp
public static object ThenSelect<TIn, TOut>(
    this object chain,
    Expression<Func<TIn, TOut>> selector,
    PipelineStageOptions? options = null) where TIn : unmanaged where TOut : unmanaged
```

**Optimization**: Automatically fused with adjacent operations when possible

##### ThenWhere\<T>

```csharp
public static object ThenWhere<T>(
    this object chain,
    Expression<Func<T, bool>> predicate,
    PipelineStageOptions? options = null) where T : unmanaged
```

**Performance**: Early filtering reduces downstream processing

##### ThenAggregate\<T>

```csharp
public static object ThenAggregate<T>(
    this object chain,
    Expression<Func<T, T, T>> aggregator,
    PipelineStageOptions? options = null) where T : unmanaged
```

**Implementation**: Uses parallel reduction trees for optimal performance

#### Streaming Operations

##### AsStreamingPipeline\<T>

```csharp
public static IAsyncEnumerable<T> AsStreamingPipeline<T>(
    this IAsyncEnumerable<T> source,
    IServiceProvider services,
    int batchSize = 1000,
    TimeSpan? windowSize = null) where T : unmanaged
```

**Features**:

- Micro-batching for optimal GPU utilization
- Backpressure handling for slow consumers
- Sliding window operations
- Real-time processing capabilities

**Configuration Options**:

```csharp
var options = new StreamPipelineOptions
{
    BatchSize = 5000,                    // Larger batches for GPU efficiency
    BatchTimeout = TimeSpan.FromMilliseconds(100),
    EnableBackpressure = true,
    MaxBufferSize = 50000
};
```

#### Advanced Optimization

##### ExecutePipelineAsync\<TResult>

```csharp
public static async Task<TResult> ExecutePipelineAsync<TResult>(
    this object pipeline,
    OptimizationLevel optimizationLevel = OptimizationLevel.Aggressive,
    CancellationToken cancellationToken = default) where TResult : unmanaged
```

**Optimization Levels**:

- **None**: Direct execution, fastest compilation
- **Conservative**: Safe optimizations only, stable performance
- **Balanced**: Performance/compilation time balance
- **Aggressive**: Maximum performance, longer compilation
- **Adaptive**: ML-powered optimization selection

##### WithIntelligentCaching\<T>

```csharp
public static object WithIntelligentCaching<T>(
    this object pipeline,
    CachePolicy? policy = null) where T : unmanaged
```

**Caching Strategies**:

- Automatic cache key generation from expression trees
- LRU eviction for memory management
- Performance-based cache decisions
- Cross-session persistence options

##### OptimizeQueryPlanAsync

```csharp
public static async Task<object> OptimizeQueryPlanAsync(
    this object pipeline,
    IServiceProvider services)
```

**Optimizations Applied**:

- Predicate pushdown
- Kernel fusion analysis
- Memory layout optimization
- Parallel execution planning

## Interfaces

### IComputeLinqProvider

Core abstraction for LINQ-to-GPU execution.

#### Key Methods

##### CreateQueryable\<T>

```csharp
IQueryable<T> CreateQueryable<T>(IEnumerable<T> source, IAccelerator? accelerator = null);
```

**Implementation Requirements**:

- Thread-safe execution
- Resource cleanup handling
- Error propagation and logging

##### ExecuteAsync\<T>

```csharp
Task<T> ExecuteAsync<T>(Expression expression);
Task<T> ExecuteAsync<T>(Expression expression, IAccelerator preferredAccelerator);
```

**Execution Pipeline**:

1. Expression tree analysis
2. Kernel generation
3. Backend selection
4. Memory management
5. Result processing

### IPipelineExpressionAnalyzer

Expression analysis for pipeline conversion.

#### AnalyzeExpression\<T>

```csharp
PipelineExecutionPlan AnalyzeExpression<T>(Expression expression);
```

**Analysis Output**:

- Pipeline stage decomposition
- Optimization opportunities
- Resource requirements
- Performance estimates

### IPipelinePerformanceAnalyzer

Performance analysis and recommendations.

#### Key Analysis Methods

##### AnalyzePipelineAsync

```csharp
Task<PipelinePerformanceReport> AnalyzePipelineAsync(Expression expression);
```

**Report Contents**:

- Execution time estimates
- Memory usage projections
- Bottleneck identification
- Alternative strategy suggestions

##### RecommendOptimalBackendAsync

```csharp
Task<BackendRecommendation> RecommendOptimalBackendAsync(IQueryable queryable);
```

**Recommendation Factors**:

- Operation type compatibility
- Data size considerations
- Available hardware capabilities
- Historical performance data

## Configuration

### Service Registration

#### Basic Configuration

```csharp
services.AddDotComputeLinq(options =>
{
    options.PreferredBackend = "CUDA";
    options.EnableCaching = true;
    options.OptimizationLevel = OptimizationLevel.Balanced;
});
```

#### Production Configuration

```csharp
services.AddDotComputeLinq(options =>
{
    // Performance settings
    options.OptimizationLevel = OptimizationLevel.Aggressive;
    options.EnableKernelFusion = true;
    options.DefaultWorkGroupSize = 256;
    
    // Memory management
    options.MaxMemoryUsage = 2L * 1024 * 1024 * 1024; // 2GB
    options.EnableMemoryOptimization = true;
    
    // Caching and persistence
    options.EnableCaching = true;
    options.CacheSize = 10000;
    options.EnableCachePersistence = true;
    
    // Error handling
    options.EnableAutoFallback = true;
    options.FallbackTimeout = TimeSpan.FromSeconds(60);
    
    // Monitoring
    options.EnableProfiling = true;
    options.EnableDetailedProfiling = true;
});

// Add specific backends
services.AddDotComputeCuda();
services.AddDotComputeCpu();

// Add advanced features
services.AddDotComputeProfiling();
services.AddPipelineErrorHandling();
```

### ComputeQueryOptions

Comprehensive configuration options for query execution.

```csharp
public class ComputeQueryOptions
{
    // Basic execution settings
    public string? PreferredBackend { get; set; }
    public TimeSpan? DefaultTimeout { get; set; }
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Balanced;
    
    // Performance settings
    public int DefaultWorkGroupSize { get; set; } = 256;
    public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
    public bool EnableKernelFusion { get; set; } = true;
    public bool EnableMemoryOptimization { get; set; } = true;
    
    // Caching configuration
    public bool EnableCaching { get; set; } = true;
    public int CacheSize { get; set; } = 1000;
    public bool EnableCachePersistence { get; set; } = false;
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromHours(1);
    
    // Error handling
    public bool EnableAutoFallback { get; set; } = true;
    public TimeSpan FallbackTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    // Monitoring and profiling
    public bool EnableProfiling { get; set; } = false;
    public bool EnableDetailedProfiling { get; set; } = false;
    public ILoggerFactory? LoggerFactory { get; set; }
}
```

## Performance Analysis

### Optimization Suggestions

#### OptimizationSuggestion Class
```csharp
public class OptimizationSuggestion
{
    public string Category { get; set; }           // Performance, Memory, Compatibility
    public string Message { get; set; }            // Human-readable suggestion
    public SuggestionSeverity Severity { get; set; } // Info, Warning, High, Critical
    public double EstimatedImpact { get; set; }    // 0.0 to 1.0 improvement estimate
}
```

#### Common Suggestion Categories

**Performance Suggestions**:

- Operation reordering for better data locality
- Kernel fusion opportunities
- Parallel execution recommendations
- Algorithm complexity improvements

**Memory Suggestions**:

- Memory layout optimizations
- Data type recommendations
- Streaming suggestions for large datasets
- Memory pool utilization

**Compatibility Suggestions**:

- GPU-incompatible operation identification
- Alternative implementation recommendations
- Backend-specific optimizations

### Performance Monitoring

#### Built-in Metrics

```csharp
// Enable profiling in configuration
options.EnableDetailedProfiling = true;

// Access metrics after execution
var profiler = services.GetRequiredService<IQueryProfiler>();
var metrics = profiler.GetLastExecutionMetrics();

Console.WriteLine($"Total Time: {metrics.TotalTime}");
Console.WriteLine($"Compilation Time: {metrics.CompilationTime}");
Console.WriteLine($"Data Transfer Time: {metrics.TransferTime}");
Console.WriteLine($"Execution Time: {metrics.ExecutionTime}");
Console.WriteLine($"Memory Usage: {metrics.PeakMemoryUsage} bytes");
```

## Error Handling

### Error Types and Recovery

#### Common Error Scenarios

**OutOfMemoryException**:

```csharp
try
{
    var results = await largeQuery.ExecuteAsync();
}
catch (OutOfMemoryException)
{
    // Automatic fallback to streaming execution
    var streamingResults = await ProcessWithStreamingAsync(largeQuery);
}
```

**NotSupportedException**:

```csharp
// Check compatibility before execution
if (!query.IsGpuCompatible(services))
{
    // Use CPU fallback or alternative implementation
    var cpuResults = await query.ExecuteAsync("CPU");
}
```

**InvalidOperationException**:

```csharp
try
{
    await query.PrecompileAsync(services);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("compilation"))
{
    // Handle kernel compilation failures
    LogCompilationIssue(ex);
    // Use interpreted execution or simpler operations
}
```

### Comprehensive Error Handling Pattern

```csharp
public async Task<IEnumerable<T>> ExecuteWithErrorHandlingAsync<T>(
    IQueryable<T> query,
    IServiceProvider services,
    ILogger logger)
{
    try
    {
        // Primary execution attempt
        return await query.ExecuteAsync();
    }
    catch (OutOfMemoryException ex)
    {
        logger.LogWarning("GPU memory exhausted, using streaming execution");
        return await ExecuteWithStreamingAsync(query, services);
    }
    catch (NotSupportedException ex) when (ex.Message.Contains("GPU"))
    {
        logger.LogWarning("GPU incompatible operation, falling back to CPU");
        return await query.ExecuteAsync("CPU");
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError("Execution failed: {Error}", ex.Message);
        
        // Final fallback to standard LINQ
        if (query.Expression is ConstantExpression constant &&
            constant.Value is IEnumerable<T> source)
        {
            logger.LogInformation("Using standard LINQ fallback");
            return ExecuteStandardLinq(source, query.Expression);
        }
        
        throw; // Re-throw if no fallback possible
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Operation was canceled");
        throw; // Don't handle cancellation
    }
}
```

## Advanced Features

### Custom Backend Integration

#### IAccelerator Implementation
```csharp
public class CustomAccelerator : IAccelerator
{
    public string Name => "Custom";
    public AcceleratorType Type => AcceleratorType.Custom;
    
    public async Task<T[]> ExecuteKernelAsync<T>(
        ICompiledKernel kernel, 
        T[] input, 
        CancellationToken cancellationToken = default)
    {
        // Custom backend implementation
        return await ExecuteOnCustomHardware(kernel, input);
    }
}

// Registration
services.AddSingleton<IAccelerator, CustomAccelerator>();
```

### Performance Profiling Integration

#### Custom Profiler
```csharp
public class CustomQueryProfiler : IQueryProfiler
{
    public void StartProfiling(string operationId) { /* Implementation */ }
    public void StopProfiling(string operationId) { /* Implementation */ }
    public QueryMetrics GetMetrics(string operationId) { /* Implementation */ }
}

// Registration
services.AddSingleton<IQueryProfiler, CustomQueryProfiler>();
```

### Memory Management Integration

#### Custom Memory Manager
```csharp
public class CustomMemoryManager : IUnifiedMemoryManager
{
    public UnifiedBuffer<T> CreateBuffer<T>(int size) where T : unmanaged
    {
        // Custom memory allocation logic
        return new CustomUnifiedBuffer<T>(size);
    }
    
    public async Task TransferToDeviceAsync<T>(
        UnifiedBuffer<T> buffer, 
        IAccelerator target) where T : unmanaged
    {
        // Custom memory transfer implementation
    }
}
```

### Extensibility Points

#### Custom Expression Handlers
```csharp
public class CustomExpressionHandler : IExpressionHandler
{
    public bool CanHandle(Expression expression) =>
        expression is MethodCallExpression call &&
        call.Method.Name == "CustomOperation";
        
    public ICompiledKernel CompileExpression(Expression expression, CompilationContext context)
    {
        // Custom expression compilation logic
        return new CustomCompiledKernel(expression);
    }
}

// Registration
services.AddSingleton<IExpressionHandler, CustomExpressionHandler>();
```

This API guide provides comprehensive coverage of the DotCompute.Linq pipeline system. For additional examples and advanced scenarios, refer to the examples in the Examples/ directory and the comprehensive demo applications.
