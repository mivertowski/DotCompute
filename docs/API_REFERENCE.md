# DotCompute API Reference v0.2.0

**Production-Ready Universal Compute Framework for .NET 9+**

Complete API reference for DotCompute's modern, attribute-based kernel development with real-time IDE integration, cross-backend validation, and enterprise-grade performance optimization.

## 🎯 **Modern Kernel Development**

### **[Kernel] Attribute - Primary API**

The modern approach uses C# `[Kernel]` attributes to define compute kernels. Source generators automatically create optimized wrappers for CPU and GPU execution.

```csharp
using DotCompute.Core;
using System;

public static class MyKernels
{
    [Kernel]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }

    [Kernel]
    public static void MatrixMultiply(ReadOnlySpan<float> matA, ReadOnlySpan<float> matB,
                                     Span<float> result, int width)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;

        if (row < width && col < width)
        {
            float sum = 0.0f;
            for (int k = 0; k < width; k++)
            {
                sum += matA[row * width + k] * matB[k * width + col];
            }
            result[row * width + col] = sum;
        }
    }
}
```

### **Supported Parameter Types**

| Type | Description | GPU Compatible | CPU Compatible |
|------|-------------|----------------|----------------|
| `Span<T>` | Mutable memory buffer | ✅ | ✅ |
| `ReadOnlySpan<T>` | Immutable memory buffer | ✅ | ✅ |
| `int`, `float`, `double` | Scalar parameters | ✅ | ✅ |
| `uint`, `long`, `ulong` | Integer parameters | ✅ | ✅ |
| `byte`, `sbyte`, `short`, `ushort` | Small integers | ✅ | ✅ |

**Supported Element Types**: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `bool`

### **Threading Model**

```csharp
[Kernel]
public static void ThreadingExample(Span<float> data)
{
    // Get current thread index
    int x = Kernel.ThreadId.X;        // 1D index
    int y = Kernel.ThreadId.Y;        // 2D row index
    int z = Kernel.ThreadId.Z;        // 3D depth index

    // Always add bounds checking
    if (x >= data.Length) return;

    // Process data at this thread's index
    data[x] = data[x] * 2.0f;
}
```

## 🚀 **Runtime Integration**

### **Service Registration**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;

var builder = Host.CreateApplicationBuilder(args);

// Essential services
builder.Services.AddDotComputeRuntime();

// Production-grade features
builder.Services.AddProductionOptimization();  // ML-powered backend selection
builder.Services.AddProductionDebugging();     // Cross-backend validation

// Optional advanced features
builder.Services.AddPerformanceProfiling();    // Hardware counter integration
builder.Services.AddTelemetryCollection();     // Metrics and monitoring

var app = builder.Build();
```

### **IComputeOrchestrator - Primary Execution Interface**

```csharp
namespace DotCompute.Core.Interfaces;

public interface IComputeOrchestrator
{
    /// <summary>Execute kernel with automatic backend selection</summary>
    Task<T> ExecuteAsync<T>(string kernelName, params object[] parameters);

    /// <summary>Execute with specific backend</summary>
    Task<T> ExecuteAsync<T>(string kernelName, string backend, params object[] parameters);

    /// <summary>Validate kernel across CPU/GPU backends</summary>
    Task<ValidationResult> ValidateKernelAsync(string kernelName, params object[] testData);

    /// <summary>Get performance metrics for kernel</summary>
    Task<PerformanceMetrics> GetPerformanceMetricsAsync(string kernelName);

    /// <summary>Get available backends for kernel</summary>
    Task<string[]> GetAvailableBackendsAsync(string kernelName);
}
```

### **Usage Examples**

```csharp
// Automatic backend selection (recommended)
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Simple execution - automatically selects optimal backend
await orchestrator.ExecuteAsync("VectorAdd", inputA, inputB, output);

// Explicit backend selection
await orchestrator.ExecuteAsync("MatrixMultiply", "CUDA", matA, matB, result, width);
await orchestrator.ExecuteAsync("ImageProcess", "CPU", image, filters, result);

// Validation and debugging
var validation = await orchestrator.ValidateKernelAsync("VectorAdd", testInputA, testInputB, testOutput);
if (validation.HasDifferences)
{
    Console.WriteLine($"CPU vs GPU differences detected: {validation.MaxDifference}");
}

// Performance monitoring
var metrics = await orchestrator.GetPerformanceMetricsAsync("MatrixMultiply");
Console.WriteLine($"Average execution time: {metrics.AverageExecutionTime}ms");
Console.WriteLine($"Recommended backend: {metrics.OptimalBackend}");
Console.WriteLine($"Expected speedup: {metrics.ExpectedSpeedup:F1}x");
```

## 🛠️ **Developer Experience APIs**

### **Real-Time Analysis - Roslyn Analyzer**

The analyzer provides 12 diagnostic rules with automated fixes:

#### **Critical Diagnostics (Errors)**
- **DC001**: Kernel methods must be static
- **DC002**: Invalid parameter types (must use Span<T>, primitives)
- **DC003**: Unsupported constructs (exceptions, dynamic allocation)

#### **Performance Diagnostics (Warnings)**
- **DC004**: Vectorization opportunities detected
- **DC005**: Suboptimal memory access patterns
- **DC006**: Register spilling risk (too many variables)

#### **Code Quality Diagnostics (Info/Suggestions)**
- **DC007**: Missing [Kernel] attribute on potential kernels
- **DC008**: Unnecessary complexity in kernel logic
- **DC009**: Thread safety warnings
- **DC010**: Incorrect threading model (use Kernel.ThreadId)
- **DC011**: Missing bounds checking
- **DC012**: Suboptimal backend selection hints

### **Automated Code Fixes**

The analyzer provides 5 automated code fixes available in Visual Studio and VS Code:

1. **Add Static Modifier** (DC001): `public void Method()` → `public static void Method()`
2. **Convert to Span** (DC002): `float[] data` → `Span<float> data`
3. **Add [Kernel] Attribute** (DC007): Suggests adding `[Kernel]` to GPU-suitable methods
4. **Fix Threading Model** (DC010): `for (int i = 0; ...)` → `int index = Kernel.ThreadId.X;`
5. **Add Bounds Check** (DC011): Automatically inserts `if (index >= data.Length) return;`

### **Cross-Backend Debugging APIs**

```csharp
using DotCompute.Core.Debugging;

public interface IKernelDebugService
{
    /// <summary>Compare CPU vs GPU execution results</summary>
    Task<ValidationResult> CompareBackendResultsAsync<T>(
        string kernelName, T[] testData, double tolerance = 1e-6);

    /// <summary>Analyze kernel performance characteristics</summary>
    Task<PerformanceAnalysis> AnalyzeKernelPerformanceAsync(
        string kernelName, params object[] parameters);

    /// <summary>Validate memory access patterns</summary>
    Task<MemoryAnalysis> AnalyzeMemoryPatternsAsync(
        string kernelName, params object[] parameters);

    /// <summary>Test kernel determinism across runs</summary>
    Task<DeterminismAnalysis> TestDeterminismAsync(
        string kernelName, int iterations = 10, params object[] parameters);
}
```

## 📊 **Performance & Optimization APIs**

### **Adaptive Backend Selection**

```csharp
using DotCompute.Core.Optimization;

public interface IAdaptiveBackendSelector
{
    /// <summary>Get optimal backend for workload</summary>
    Task<string> SelectOptimalBackendAsync(WorkloadCharacteristics workload);

    /// <summary>Learn from execution results</summary>
    Task UpdatePerformanceModelAsync(string kernelName, string backend,
                                   TimeSpan executionTime, int dataSize);

    /// <summary>Get backend recommendations with reasoning</summary>
    Task<BackendRecommendation[]> GetBackendRecommendationsAsync(
        string kernelName, int estimatedDataSize);
}

// Usage
var selector = app.Services.GetRequiredService<IAdaptiveBackendSelector>();
var optimal = await selector.SelectOptimalBackendAsync(new WorkloadCharacteristics
{
    DataSize = 1_000_000,
    ComputeIntensity = ComputeIntensity.High,
    MemoryPattern = MemoryPattern.Sequential,
    ParallelizationPotential = 0.95f
});
```

### **Performance Profiling**

```csharp
using DotCompute.Core.Telemetry;

public interface IPerformanceProfiler
{
    /// <summary>Start performance monitoring session</summary>
    Task<ProfilingSession> StartProfilingAsync(string kernelName);

    /// <summary>Collect detailed hardware metrics</summary>
    Task<HardwareMetrics> CollectHardwareMetricsAsync(TimeSpan duration);

    /// <summary>Generate performance report</summary>
    Task<PerformanceReport> GenerateReportAsync(string kernelName, TimeSpan timeRange);
}

// Hardware metrics available
public class HardwareMetrics
{
    public double CpuUtilization { get; set; }
    public double GpuUtilization { get; set; }
    public long MemoryBandwidth { get; set; }
    public double CacheHitRatio { get; set; }
    public int VectorizationEfficiency { get; set; }
    public TimeSpan KernelExecutionTime { get; set; }
    public TimeSpan MemoryTransferTime { get; set; }
}
```

## 🔧 **Backend-Specific APIs**

### **CPU Backend**

```csharp
using DotCompute.Backends.CPU;

// SIMD optimization control
public class CpuExecutionOptions
{
    public SIMDLevel SimdLevel { get; set; } = SIMDLevel.Auto; // Auto, AVX512, AVX2, SSE
    public int ThreadCount { get; set; } = -1; // -1 = auto-detect
    public bool EnableVectorization { get; set; } = true;
    public CacheOptimization CacheStrategy { get; set; } = CacheOptimization.Optimized;
}

// Direct CPU accelerator access
var cpuAccelerator = app.Services.GetRequiredService<CpuAccelerator>();
await cpuAccelerator.SetExecutionOptionsAsync(new CpuExecutionOptions
{
    SimdLevel = SIMDLevel.AVX512,
    ThreadCount = Environment.ProcessorCount,
    EnableVectorization = true
});
```

### **CUDA Backend**

```csharp
using DotCompute.Backends.CUDA;

// CUDA-specific configuration
public class CudaExecutionOptions
{
    public int DeviceId { get; set; } = 0;
    public ComputeCapability TargetCapability { get; set; }
    public bool EnableP2PTransfers { get; set; } = true;
    public CudaMemoryModel MemoryModel { get; set; } = CudaMemoryModel.Unified;
    public int MaxBlockSize { get; set; } = 1024;
}

// CUDA capability detection
public static class CudaCapabilityManager
{
    /// <summary>Get target compute capability for current hardware</summary>
    public static ComputeCapability GetTargetComputeCapability();

    /// <summary>Check if feature is supported on current hardware</summary>
    public static bool IsFeatureSupported(CudaFeature feature);

    /// <summary>Get optimal block size for kernel</summary>
    public static int GetOptimalBlockSize(string kernelName, int dataSize);
}
```

## 🔍 **Memory Management APIs**

### **Unified Buffer System**

```csharp
using DotCompute.Memory;

public interface IUnifiedMemoryManager
{
    /// <summary>Allocate cross-device buffer</summary>
    Task<UnifiedBuffer<T>> AllocateAsync<T>(int elementCount, MemoryLocation location = MemoryLocation.Auto)
        where T : unmanaged;

    /// <summary>Copy data between devices</summary>
    Task CopyAsync<T>(UnifiedBuffer<T> source, UnifiedBuffer<T> destination)
        where T : unmanaged;

    /// <summary>Enable peer-to-peer transfers</summary>
    Task EnableP2PAsync(int deviceA, int deviceB);

    /// <summary>Get memory pool statistics</summary>
    MemoryPoolStatistics GetPoolStatistics();
}

// Usage
var memoryManager = app.Services.GetRequiredService<IUnifiedMemoryManager>();

// Automatic memory location selection
var buffer = await memoryManager.AllocateAsync<float>(1_000_000);

// Explicit GPU memory allocation
var gpuBuffer = await memoryManager.AllocateAsync<float>(1_000_000, MemoryLocation.GPU);

// Memory pool benefits: 90%+ allocation reduction
var stats = memoryManager.GetPoolStatistics();
Console.WriteLine($"Pool efficiency: {stats.ReuseRatio:P0}");
Console.WriteLine($"Memory saved: {stats.SavedAllocations} allocations");
```

## 🌟 **Advanced Features**

### **LINQ Integration**

```csharp
using DotCompute.Linq;

// LINQ-to-GPU with automatic optimization
var result = await data
    .AsComputeQueryable(serviceProvider)
    .Where(x => x > threshold)
    .Select(x => x * x)
    .Where(x => x < maxValue)
    .ExecuteAsync(); // Automatically uses GPU if beneficial, CPU otherwise

// Performance analysis
var analysis = await data
    .AsComputeQueryable(serviceProvider)
    .AnalyzePerformanceAsync();

Console.WriteLine($"Estimated speedup: {analysis.ExpectedSpeedup:F1}x");
Console.WriteLine($"Recommended backend: {analysis.OptimalBackend}");
```

### **Plugin System**

```csharp
using DotCompute.Plugins;

// Custom backend plugin
public class CustomBackendPlugin : IAcceleratorPlugin
{
    public string Name => "CustomAccelerator";
    public AcceleratorType Type => AcceleratorType.Custom;

    public async Task<IAccelerator> CreateAcceleratorAsync(IServiceProvider services)
    {
        return new CustomAccelerator();
    }
}

// Registration
builder.Services.AddAcceleratorPlugin<CustomBackendPlugin>();
```

### **Telemetry & Monitoring**

```csharp
using DotCompute.Telemetry;

// Comprehensive monitoring
services.AddDotComputeTelemetry(options =>
{
    options.EnablePerformanceCounters = true;
    options.EnableMemoryTracking = true;
    options.EnableHardwareMetrics = true;
    options.ExportInterval = TimeSpan.FromSeconds(30);
});

// Custom metrics
var telemetry = app.Services.GetRequiredService<ITelemetryService>();
telemetry.RecordKernelExecution("VectorAdd", TimeSpan.FromMilliseconds(1.2), "CUDA");
telemetry.RecordMemoryUsage(1_000_000, MemoryLocation.GPU);
telemetry.RecordPerformanceMetric("Throughput", 850_000_000); // elements/sec
```

## 📚 **Integration Examples**

### **ASP.NET Core Integration**

```csharp
using Microsoft.AspNetCore.Mvc;
using DotCompute.Core.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class ComputeController : ControllerBase
{
    private readonly IComputeOrchestrator _orchestrator;

    public ComputeController(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("vector-add")]
    public async Task<IActionResult> VectorAdd([FromBody] VectorAddRequest request)
    {
        var result = new float[request.A.Length];
        await _orchestrator.ExecuteAsync("VectorAdd", request.A, request.B, result);
        return Ok(new { Result = result });
    }
}
```

### **Background Service Integration**

```csharp
public class ComputeBackgroundService : BackgroundService
{
    private readonly IComputeOrchestrator _orchestrator;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Process work queue with GPU acceleration
            var workItem = await GetNextWorkItemAsync();
            await _orchestrator.ExecuteAsync("ProcessWorkItem", workItem.Data);

            await Task.Delay(100, stoppingToken);
        }
    }
}
```

## 🚨 **Error Handling & Diagnostics**

### **Exception Types**

```csharp
// DotCompute-specific exceptions
try
{
    await orchestrator.ExecuteAsync("MyKernel", data);
}
catch (KernelCompilationException ex)
{
    // Kernel failed to compile for target backend
    Console.WriteLine($"Compilation failed: {ex.CompilerOutput}");
}
catch (BackendNotAvailableException ex)
{
    // Requested backend not available (e.g., no CUDA GPU)
    Console.WriteLine($"Backend unavailable: {ex.BackendName}");
}
catch (KernelExecutionException ex)
{
    // Runtime execution error
    Console.WriteLine($"Execution failed: {ex.Message}");
    Console.WriteLine($"Backend: {ex.Backend}");
}
catch (ValidationFailedException ex)
{
    // Cross-backend validation detected differences
    Console.WriteLine($"Validation failed: CPU vs GPU results differ by {ex.MaxDifference}");
}
```

### **Health Checks**

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

// Built-in health checks
services.AddHealthChecks()
    .AddDotComputeHealthChecks(options =>
    {
        options.CheckBackendAvailability = true;
        options.CheckMemoryPools = true;
        options.CheckKernelCompilation = true;
        options.ValidateKernelExecution = true;
    });
```

## 📋 **Migration Guide**

### **From Legacy KernelDefinition to [Kernel] Attributes**

```csharp
// Old approach (still supported but deprecated)
var kernelDef = new KernelDefinition
{
    Name = "VectorAdd",
    Source = @"
        kernel void VectorAdd(global float* a, global float* b, global float* result, int length)
        {
            int idx = get_global_id(0);
            if (idx < length) {
                result[idx] = a[idx] + b[idx];
            }
        }
    ",
    EntryPoint = "VectorAdd"
};

// New approach (recommended)
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

## 🎯 **Best Practices**

### **Kernel Development**

1. **Always use `[Kernel]` attribute** for new development
2. **Add bounds checking** with `if (index >= length) return;`
3. **Use `ReadOnlySpan<T>`** for input parameters
4. **Use `Span<T>`** for output parameters
5. **Keep kernels simple** - complex logic should be in separate methods
6. **Profile both CPU and GPU** - not all operations benefit from GPU acceleration

### **Performance Optimization**

1. **Let the system choose backends** - use automatic selection unless specific requirements
2. **Use production optimization services** for ML-powered backend selection
3. **Enable telemetry** to track performance regressions
4. **Validate kernels** during development with cross-backend debugging
5. **Monitor memory usage** - utilize memory pooling for frequent allocations

---

## 📊 **API Summary**

**Total APIs**: 150+ methods, properties, and interfaces
**Core Interfaces**: 12 primary interfaces for different use cases
**Backend Support**: CPU (Production), CUDA (Production), Metal (Development), ROCm (Planned)
**Testing**: 75-85% API coverage with comprehensive integration tests
**Documentation**: Complete XML documentation for IntelliSense support

**Production Status**: ✅ **Enterprise Ready** - Complete feature set with comprehensive testing and validation.
