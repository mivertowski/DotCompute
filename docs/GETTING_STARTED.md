# Getting Started with DotCompute

This guide will help you get up and running with DotCompute quickly, from installation to running your first GPU-accelerated compute kernel.

## 📋 Prerequisites

Before starting, ensure you have the following installed:

### Required
- **.NET 9.0 SDK** or later ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Visual Studio 2022 17.8+** or **VS Code** with C# extension

### Optional (for GPU acceleration)
- **For NVIDIA GPUs**: CUDA Toolkit 12.0+ ([Download](https://developer.nvidia.com/cuda-downloads))
  - NVIDIA GPU with Compute Capability 5.0+ (GTX 9xx series or newer)
- **For Apple Silicon**: macOS 12+ (Metal backend - coming soon)
- **For AMD GPUs**: ROCm 5.0+ (ROCm backend - planned)

## 🚀 Quick Installation

### 1. Create a New Project

```bash
# Create a new console application
dotnet new console -n MyComputeApp
cd MyComputeApp
```

### 2. Add DotCompute Packages

```bash
# Core packages (required)
dotnet add package DotCompute.Core --version 0.1.0-alpha.1
dotnet add package DotCompute.Backends.CPU --version 0.1.0-alpha.1

# GPU acceleration (optional but recommended)
dotnet add package DotCompute.Backends.CUDA --version 0.1.0-alpha.1

# Memory management (recommended)
dotnet add package DotCompute.Memory --version 0.1.0-alpha.1

# Additional features (optional)
dotnet add package DotCompute.Algorithms --version 0.1.0-alpha.1
dotnet add package DotCompute.Plugins --version 0.1.0-alpha.1
```

### 3. Update Program.cs

Replace the contents of `Program.cs` with:

```csharp
using DotCompute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure dependency injection
var builder = Host.CreateApplicationBuilder(args);

// Add DotCompute services
builder.Services.AddDotCompute();
builder.Services.AddCpuBackend();      // Always available
builder.Services.AddCudaBackend();     // Requires NVIDIA GPU

var host = builder.Build();

// Get compute service
var compute = host.Services.GetRequiredService<IComputeService>();

// Your first DotCompute program
await RunVectorAddExample(compute);

static async Task RunVectorAddExample(IComputeService compute)
{
    Console.WriteLine("DotCompute Vector Addition Example");
    
    // Create input data
    var size = 1024;
    var a = Enumerable.Range(0, size).Select(x => (float)x).ToArray();
    var b = Enumerable.Range(0, size).Select(x => (float)x * 2).ToArray();
    var result = new float[size];
    
    Console.WriteLine($"Adding {size} elements...");
    
    // Execute on best available backend
    await compute.ExecuteAsync("VectorAdd", new
    {
        a = a.AsSpan(),
        b = b.AsSpan(),
        result = result.AsSpan(),
        length = size
    });
    
    // Verify results
    var isCorrect = result.Take(10).All(x => Math.Abs(x - (a[Array.IndexOf(result, x)] + b[Array.IndexOf(result, x)])) < 0.001f);
    Console.WriteLine($"Result: {(isCorrect ? "✅ Correct" : "❌ Incorrect")}");
    Console.WriteLine($"First 5 results: [{string.Join(", ", result.Take(5))}]");
}

// Define the vector addition kernel
[Kernel("VectorAdd")]
public static void VectorAddKernel(
    KernelContext ctx,
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result,
    int length)
{
    var i = ctx.GlobalId.X;
    if (i < length)
    {
        result[i] = a[i] + b[i];
    }
}
```

### 4. Run Your First Program

```bash
dotnet run
```

Expected output:
```
DotCompute Vector Addition Example
Adding 1024 elements...
Result: ✅ Correct
First 5 results: [0, 3, 6, 9, 12]
```

## 🎯 Understanding the Basics

### Kernel Definition

Kernels are the compute functions that run on accelerators:

```csharp
[Kernel("KernelName")]
public static void MyKernel(
    KernelContext ctx,          // Execution context
    ReadOnlySpan<float> input,  // Input parameters
    Span<float> output)         // Output parameters
{
    var i = ctx.GlobalId.X;     // Thread index
    if (i < output.Length)
    {
        output[i] = input[i] * 2.0f;  // Your computation
    }
}
```

**Key Points:**
- Must be `static` methods
- Decorated with `[Kernel("name")]` attribute
- First parameter is always `KernelContext`
- Use `Span<T>` and `ReadOnlySpan<T>` for data
- Check bounds using thread index

### Service Configuration

DotCompute uses dependency injection for configuration:

```csharp
var builder = Host.CreateApplicationBuilder();

builder.Services.AddDotCompute(options =>
{
    // Configure memory pool
    options.Memory.PoolSize = 1_000_000_000;  // 1GB
    options.Memory.EnableNUMA = true;
    
    // Performance settings
    options.Performance.OptimizationLevel = OptimizationLevel.Aggressive;
    options.Performance.EnableProfiling = true;
});

// Add backends
builder.Services.AddCpuBackend();
builder.Services.AddCudaBackend();

var host = builder.Build();
```

### Execution Patterns

#### Simple Execution
```csharp
await compute.ExecuteAsync("KernelName", new { param1, param2, result });
```

#### With Accelerator Selection
```csharp
// Get specific accelerator
var gpuAccelerator = await compute.GetAcceleratorAsync("CUDA");
using var context = gpuAccelerator.CreateContext();

// Execute on selected accelerator
await context.ExecuteAsync("KernelName", parameters);
```

## 🔧 Advanced Examples

### Matrix Multiplication

```csharp
[Kernel("MatrixMultiply")]
public static void MatrixMultiplyKernel(
    KernelContext ctx,
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> c,
    int width)
{
    var row = ctx.GlobalId.Y;
    var col = ctx.GlobalId.X;
    
    if (row < width && col < width)
    {
        float sum = 0.0f;
        for (int k = 0; k < width; k++)
        {
            sum += a[row * width + k] * b[k * width + col];
        }
        c[row * width + col] = sum;
    }
}

// Usage
static async Task MatrixMultiplyExample(IComputeService compute)
{
    const int size = 512;
    var a = CreateRandomMatrix(size, size);
    var b = CreateRandomMatrix(size, size);
    var c = new float[size * size];
    
    var stopwatch = Stopwatch.StartNew();
    
    await compute.ExecuteAsync("MatrixMultiply", new
    {
        a = a.AsSpan(),
        b = b.AsSpan(),
        c = c.AsSpan(),
        width = size
    });
    
    stopwatch.Stop();
    Console.WriteLine($"Matrix multiplication completed in {stopwatch.ElapsedMilliseconds}ms");
}
```

### Memory Management with UnifiedBuffer

```csharp
static async Task UnifiedBufferExample(IComputeService compute)
{
    // Get accelerator for advanced memory operations
    var accelerator = await compute.GetAcceleratorAsync();
    
    // Create unified buffer (automatic synchronization)
    var inputData = Enumerable.Range(0, 10000).Select(x => (float)x).ToArray();
    var unifiedBuffer = await accelerator.CreateUnifiedBufferAsync(inputData);
    
    // Execute kernel - data automatically transfers to GPU if needed
    await compute.ExecuteAsync("ProcessData", new { data = unifiedBuffer });
    
    // Access data - automatically synchronizes from GPU if needed
    var results = unifiedBuffer.AsSpan();
    Console.WriteLine($"First result: {results[0]}");
    
    // Manual synchronization if needed
    await unifiedBuffer.SynchronizeAsync();
}

[Kernel("ProcessData")]
public static void ProcessDataKernel(KernelContext ctx, Span<float> data)
{
    var i = ctx.GlobalId.X;
    if (i < data.Length)
    {
        data[i] = data[i] * data[i]; // Square each element
    }
}
```

### Performance Comparison

```csharp
static async Task PerformanceComparison(IComputeService compute)
{
    const int size = 1_000_000;
    var data = new float[size];
    new Random().NextSingle(data);
    
    // CPU version
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < size; i++)
    {
        data[i] = MathF.Sin(data[i]) * MathF.Cos(data[i]);
    }
    stopwatch.Stop();
    var cpuTime = stopwatch.ElapsedMilliseconds;
    
    // Reset data
    new Random().NextSingle(data);
    
    // DotCompute version
    stopwatch.Restart();
    await compute.ExecuteAsync("TrigCompute", new { data = data.AsSpan() });
    stopwatch.Stop();
    var dotcomputeTime = stopwatch.ElapsedMilliseconds;
    
    Console.WriteLine($"CPU Time: {cpuTime}ms");
    Console.WriteLine($"DotCompute Time: {dotcomputeTime}ms");
    Console.WriteLine($"Speedup: {(double)cpuTime / dotcomputeTime:F1}x");
}

[Kernel("TrigCompute")]
public static void TrigComputeKernel(KernelContext ctx, Span<float> data)
{
    var i = ctx.GlobalId.X;
    if (i < data.Length)
    {
        data[i] = MathF.Sin(data[i]) * MathF.Cos(data[i]);
    }
}
```

## 🔍 Debugging and Troubleshooting

### Enable Detailed Logging

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

builder.Services.AddDotCompute(options =>
{
    options.Diagnostics.EnableVerboseLogging = true;
    options.Diagnostics.LogKernelCompilation = true;
});
```

### Check Available Accelerators

```csharp
static async Task DiagnoseSystem(IComputeService compute)
{
    Console.WriteLine("=== DotCompute System Diagnostics ===");
    
    var accelerators = await compute.GetAvailableAcceleratorsAsync();
    Console.WriteLine($"Available accelerators: {accelerators.Count}");
    
    foreach (var accelerator in accelerators)
    {
        Console.WriteLine($"  - {accelerator.Name} ({accelerator.Type})");
        Console.WriteLine($"    Memory: {accelerator.Info.TotalMemory / (1024*1024)}MB");
        Console.WriteLine($"    Compute Units: {accelerator.Info.ComputeUnits}");
    }
    
    // Test each accelerator
    foreach (var accelerator in accelerators)
    {
        try
        {
            using var context = accelerator.CreateContext();
            Console.WriteLine($"✅ {accelerator.Name}: Working");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {accelerator.Name}: {ex.Message}");
        }
    }
}
```

### Common Issues and Solutions

#### CUDA Not Available
```
Error: No CUDA accelerators found
```
**Solutions:**
1. Install NVIDIA GPU drivers
2. Install CUDA Toolkit 12.0+
3. Verify GPU compute capability (5.0+)
4. Check `nvidia-smi` command works

#### Compilation Errors
```csharp
// Enable compilation diagnostics
builder.Services.AddDotCompute(options =>
{
    options.Compilation.EnableDebugging = true;
    options.Compilation.GenerateDebugInfo = true;
});
```

#### Memory Issues
```csharp
// Configure memory limits
builder.Services.AddDotCompute(options =>
{
    options.Memory.MaxPoolSize = 2_000_000_000; // 2GB limit
    options.Memory.EnableMemoryProfiling = true;
});
```

## 📊 Performance Optimization Tips

### 1. **Use Appropriate Data Types**
```csharp
// Prefer float over double for GPU compatibility
[Kernel("OptimizedKernel")]
public static void OptimizedKernel(KernelContext ctx, Span<float> data) // ✅
public static void SlowKernel(KernelContext ctx, Span<double> data)     // ❌
```

### 2. **Memory Access Patterns**
```csharp
[Kernel("CoalescedAccess")]
public static void CoalescedAccessKernel(
    KernelContext ctx,
    ReadOnlySpan<float> input,
    Span<float> output)
{
    var i = ctx.GlobalId.X; // Sequential access pattern ✅
    if (i < output.Length)
    {
        output[i] = input[i] * 2.0f;
    }
}
```

### 3. **Minimize Host-Device Transfers**
```csharp
// Good: Batch operations
await accelerator.ExecuteAsync("Process1", data);
await accelerator.ExecuteAsync("Process2", data);
await accelerator.ExecuteAsync("Process3", data);
await accelerator.SynchronizeAsync(); // Single sync

// Bad: Sync after each operation
await accelerator.ExecuteAsync("Process1", data);
await accelerator.SynchronizeAsync(); // ❌
await accelerator.ExecuteAsync("Process2", data);
await accelerator.SynchronizeAsync(); // ❌
```

## 📚 Next Steps

Now that you have DotCompute running, explore these advanced topics:

1. **[API Reference](API.md)** - Complete API documentation
2. **[Architecture](ARCHITECTURE.md)** - Understanding DotCompute's design
3. **[Performance Guide](PERFORMANCE.md)** - Optimization strategies
4. **[Development Guide](DEVELOPMENT.md)** - Contributing to DotCompute

### Example Projects

Check out complete example projects in the repository:
- `samples/GettingStarted/` - Basic usage patterns
- `samples/KernelExample/` - Advanced kernel techniques
- `benchmarks/DotCompute.Benchmarks/` - Performance benchmarking

### Community Resources

- **GitHub Discussions**: Ask questions and share projects
- **GitHub Issues**: Report bugs and request features
- **NuGet Packages**: Latest releases and prerelease versions

---

🎉 **Congratulations!** You now have DotCompute set up and running. Start building high-performance compute applications with the power of GPU acceleration!