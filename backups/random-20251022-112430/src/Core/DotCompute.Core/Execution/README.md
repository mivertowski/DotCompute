# DotCompute Execution Planning System

This directory contains the comprehensive execution planning system for DotCompute, providing advanced parallel execution capabilities with dependency analysis, resource scheduling, and performance optimization.

## Architecture Overview

The execution planning system consists of several key components working together to provide optimal parallel execution:

### Core Components

1. **ExecutionPlans.cs** (~4,100 lines) - Core data structures and planning algorithms
2. **ExecutionPlanExecutor.cs** (~1,200 lines) - Execution engine with synchronization
3. **Supporting Infrastructure** (~4,400 lines total)

### Key Features

#### 1. Execution Plan Generation
- **Data Parallel Plans**: Distribute work across multiple devices with load balancing
- **Model Parallel Plans**: Partition large models across devices with communication scheduling  
- **Pipeline Plans**: Stream processing with microbatch scheduling and buffer management
- **Work Stealing Plans**: Dynamic load balancing for irregular workloads

#### 2. Dependency Analysis
- **Data Dependencies**: Read-after-write and write-after-read hazard detection
- **Control Dependencies**: Execution order constraints
- **Topological Sorting**: Optimal execution ordering
- **Circular Dependency Detection**: Prevention of deadlock scenarios

#### 3. Resource Scheduling
- **Device Selection**: Performance-based optimal device selection
- **Workload Distribution**: Multiple strategies (round-robin, weighted, adaptive, dynamic)
- **Memory Management**: Efficient buffer allocation and pooling
- **Load Balancing**: Runtime workload distribution optimization

#### 4. Parallel Execution Engine
- **Synchronization Primitives**: Events, barriers, and coordination
- **Resource Tracking**: Memory usage and device utilization monitoring
- **Performance Profiling**: Detailed execution metrics and bottleneck analysis
- **Error Handling**: Robust failure recovery and reporting

#### 5. Performance Optimization
- **Memory Optimization**: Buffer reuse, prefetching, double buffering
- **Synchronization Optimization**: Minimal coordination overhead
- **Device-Specific Optimizations**: GPU, CPU, and TPU-specific tuning
- **Communication Scheduling**: Overlapping computation and data transfer

## Usage Examples

### Basic Data Parallel Execution

```csharp
// Create execution plan generator
var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("ExecutionPlanning");
var performanceMonitor = new PerformanceMonitor(logger);
var planGenerator = new ExecutionPlanGenerator(logger, performanceMonitor);

// Set up data parallel workload
var devices = await acceleratorManager.GetAvailableAcceleratorsAsync();
var inputBuffers = new IBuffer<float>[] { /* input data */ };
var outputBuffers = new IBuffer<float>[] { /* output buffers */ };

var options = new DataParallelismOptions
{
    MaxDevices = 4,
    LoadBalancing = LoadBalancingStrategy.Adaptive,
    EnablePeerToPeer = true
};

// Generate optimized execution plan
var plan = await planGenerator.GenerateDataParallelPlanAsync(
    "VectorAdd", devices, inputBuffers, outputBuffers, options);

// Execute the plan
var executor = new ExecutionPlanExecutor(logger, performanceMonitor);
var result = await executor.ExecuteDataParallelPlanAsync(plan);

Console.WriteLine($"Execution completed in {result.TotalExecutionTimeMs:F2}ms");
Console.WriteLine($"Parallel efficiency: {result.EfficiencyPercentage:F1}%");
Console.WriteLine($"Total throughput: {result.ThroughputGFLOPS:F2} GFLOPS");
```

### Model Parallel Execution

```csharp
// Define model layers
var layers = new List<ModelLayer<float>>
{
    new ModelLayer<float>
    {
        LayerId = 0,
        Name = "Input",
        InputTensors = new[] { inputTensor },
        OutputTensors = new[] { hiddenTensor1 },
        MemoryRequirementBytes = 1024 * 1024 * 100, // 100MB
        ComputeRequirementFLOPS = 1000000000 // 1 GFLOP
    },
    // ... more layers
};

var workload = new ModelParallelWorkload<float>
{
    ModelLayers = layers,
    InputTensors = new[] { inputTensor },
    OutputTensors = new[] { outputTensor }
};

var modelOptions = new ModelParallelismOptions
{
    LayerAssignment = LayerAssignmentStrategy.Automatic,
    CommunicationBackend = CommunicationBackend.P2P,
    MemoryOptimization = MemoryOptimizationLevel.Balanced
};

// Generate and execute model parallel plan
var modelPlan = await planGenerator.GenerateModelParallelPlanAsync(
    workload, devices, modelOptions);
var modelResult = await executor.ExecuteModelParallelPlanAsync(modelPlan);
```

### Pipeline Execution

```csharp
// Define pipeline stages
var stages = new List<PipelineStageDefinition>
{
    new PipelineStageDefinition
    {
        Name = "Preprocessing",
        KernelName = "PreprocessKernel",
        Dependencies = new List<string>()
    },
    new PipelineStageDefinition
    {
        Name = "Processing", 
        KernelName = "ProcessKernel",
        Dependencies = new List<string> { "Preprocessing" }
    },
    new PipelineStageDefinition
    {
        Name = "Postprocessing",
        KernelName = "PostprocessKernel", 
        Dependencies = new List<string> { "Processing" }
    }
};

var pipelineDefinition = new PipelineDefinition<float>
{
    Stages = stages,
    InputSpec = new PipelineInputSpec<float>
    {
        Tensors = new[] { inputTensor }
    },
    OutputSpec = new PipelineOutputSpec<float>
    {
        Tensors = new[] { outputTensor }
    }
};

var pipelineOptions = new PipelineParallelismOptions
{
    MicrobatchSize = 32,
    BufferDepth = 3,
    SchedulingStrategy = PipelineSchedulingStrategy.OneForwardOneBackward
};

// Generate and execute pipeline plan
var pipelinePlan = await planGenerator.GeneratePipelinePlanAsync(
    pipelineDefinition, devices, pipelineOptions);
var pipelineResult = await executor.ExecutePipelinePlanAsync(pipelinePlan);
```

## Advanced Features

### Performance Monitoring and Analysis

```csharp
// Get comprehensive performance analysis
var analysis = performanceMonitor.GetPerformanceAnalysis();
Console.WriteLine($"Overall performance rating: {analysis.OverallRating:F1}/10");

foreach (var bottleneck in analysis.Bottlenecks)
{
    Console.WriteLine($"Bottleneck: {bottleneck.Type} (severity: {bottleneck.Severity:F2})");
    Console.WriteLine($"Details: {bottleneck.Details}");
}

// Get optimization recommendations
foreach (var recommendation in analysis.OptimizationRecommendations)
{
    Console.WriteLine($"Recommendation: {recommendation}");
}

// Get performance trends
var trends = performanceMonitor.GetPerformanceTrends(TimeSpan.FromHours(1));
Console.WriteLine($"Throughput trend: {trends.ThroughputTrend}");
Console.WriteLine($"Efficiency trend: {trends.EfficiencyTrend}");
```

### Resource Management

```csharp
// Monitor memory usage
var memoryStats = ExecutionMemoryManager.GetMemoryStatistics();
Console.WriteLine($"Total allocated memory: {memoryStats.TotalAllocatedBytes / (1024*1024):F1} MB");
Console.WriteLine($"Memory utilization: {memoryStats.UtilizationPercentage:F1}%");

foreach (var deviceStats in memoryStats.DeviceStatistics)
{
    Console.WriteLine($"Device {deviceStats.Key}:");
    Console.WriteLine($"  Allocated: {deviceStats.Value.AllocatedBytes / (1024*1024):F1} MB");
    Console.WriteLine($"  Fragmentation: {deviceStats.Value.FragmentationIndex:F2}");
}

// Device utilization analysis
var deviceAnalysis = performanceMonitor.GetDeviceUtilizationAnalysis();
foreach (var analysis in deviceAnalysis)
{
    Console.WriteLine($"Device {analysis.Key} utilization: {analysis.Value.AverageUtilizationPercentage:F1}%");
}
```

### Execution Plan Factory

```csharp
// Create optimal plans automatically
var factory = new ExecutionPlanFactory(logger, performanceMonitor);

var workload = new DataParallelWorkload<float>
{
    KernelName = "MatrixMultiply",
    InputBuffers = inputBuffers,
    OutputBuffers = outputBuffers
};

var constraints = new ExecutionConstraints
{
    MaxDevices = 8,
    MaxExecutionTime = TimeSpan.FromSeconds(10),
    LoadBalancingStrategy = LoadBalancingStrategy.Adaptive
};

// Get optimal plan
var optimalPlan = await factory.CreateOptimalPlanAsync(workload, devices, constraints);

// Or get multiple alternatives for comparison
var alternatives = await factory.CreatePlanAlternativesAsync(workload, devices, constraints);
Console.WriteLine($"Recommended: {alternatives.RecommendedPlan?.StrategyType}");
Console.WriteLine($"Alternatives: {string.Join(", ", alternatives.AllAlternatives.Select(p => p.StrategyType))}");
```

## Performance Characteristics

### Execution Plan Generation
- **Dependency Analysis**: O(V + E) where V is vertices (tasks) and E is edges (dependencies)
- **Topological Sorting**: O(V + E) for optimal execution ordering
- **Resource Scheduling**: O(D × T) where D is devices and T is tasks
- **Memory Optimization**: O(B) where B is number of buffers

### Parallel Execution
- **Synchronization Overhead**: Minimal with event-based coordination
- **Memory Bandwidth**: Optimized with buffer pooling and prefetching
- **Load Balancing**: Adaptive algorithms with sub-linear complexity
- **Communication Scheduling**: Overlapped with computation for near-zero overhead

### Scalability
- **Device Count**: Tested up to 16 devices with linear speedup
- **Data Size**: Scales to multi-GB workloads
- **Model Complexity**: Supports models with 1000+ layers
- **Pipeline Depth**: Handles pipelines with 50+ stages

## Integration with DotCompute Core

The execution planning system integrates seamlessly with other DotCompute components:

- **Kernel Management**: Automatic kernel compilation and caching
- **Memory Management**: Unified buffer management across devices
- **Device Abstraction**: Works with any IAccelerator implementation
- **Performance Monitoring**: Deep integration with execution metrics

## Thread Safety and Resource Management

- **Thread-Safe**: All components are thread-safe and support concurrent operations
- **Resource Cleanup**: Proper disposal patterns with IAsyncDisposable
- **Memory Management**: Automatic buffer pooling and leak prevention
- **Error Handling**: Comprehensive error recovery and reporting

## Testing and Validation

The execution planning system includes comprehensive test coverage:

- **Unit Tests**: Individual component testing with mocks
- **Integration Tests**: End-to-end execution scenarios
- **Performance Tests**: Benchmarking and regression testing
- **Stress Tests**: High-load and error condition testing

For more detailed information, see the individual class documentation and the test suite in the `tests/` directory.