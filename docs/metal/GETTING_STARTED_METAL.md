# Getting Started with DotCompute Metal Backend

Welcome to the DotCompute Metal backend! This guide will help you set up and start using GPU acceleration on macOS with Apple Silicon or Intel Macs.

**Prerequisites:**
- macOS 12.0+ (Monterey or later)
- .NET 9.0 SDK or later
- Xcode 14+ (for native library compilation)
- Metal-capable GPU (Apple Silicon or Intel Mac 2016+)

---

## Table of Contents

1. [Installation](#installation)
2. [Hello World: Your First Metal Kernel](#hello-world-your-first-metal-kernel)
3. [Understanding [Kernel] Attributes](#understanding-kernel-attributes)
4. [Memory Management](#memory-management)
5. [Performance Optimization](#performance-optimization)
6. [Common Patterns](#common-patterns)
7. [Debugging and Profiling](#debugging-and-profiling)
8. [Next Steps](#next-steps)

---

## Installation

### Step 1: Verify Metal Support

First, check if your system supports Metal:

```bash
# Check Metal support
system_profiler SPDisplaysDataType | grep Metal

# Check macOS version
sw_vers

# Expected output:
# macOS 12.0 or later
# Metal: Supported
```

### Step 2: Install .NET 9 SDK

```bash
# Download from https://dotnet.microsoft.com/download
# Or use Homebrew
brew install dotnet-sdk

# Verify installation
dotnet --version
# Expected: 9.0.0 or later
```

### Step 3: Create a New Project

```bash
# Create console application
dotnet new console -n MyMetalApp
cd MyMetalApp

# Add DotCompute packages (when published)
dotnet add package DotCompute.Core
dotnet add package DotCompute.Backends.Metal
dotnet add package DotCompute.Runtime

# Or reference from source
dotnet add reference ../../DotCompute/src/Core/DotCompute.Core/DotCompute.Core.csproj
dotnet add reference ../../DotCompute/src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj
```

### Step 4: Build Native Metal Library

```bash
# Navigate to Metal backend native directory
cd path/to/DotCompute/src/Backends/DotCompute.Backends.Metal/native

# Build with CMake
mkdir -p build && cd build
cmake ..
make

# Verify library was created
ls -l ../libDotComputeMetal.dylib

# Expected output:
# -rwxr-xr-x  libDotComputeMetal.dylib
```

---

## Hello World: Your First Metal Kernel

Let's create a simple vector addition kernel to get started.

### Program.cs

```csharp
using DotCompute.Abstractions;
using DotCompute.Backends.Metal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Step 1: Define your kernel using [Kernel] attribute
public static class Kernels
{
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // Step 2: Setup dependency injection
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });

        // Add Metal backend
        services.AddDotComputeMetalBackend(options =>
        {
            options.PreferredDeviceIndex = 0;
            options.EnableUnifiedMemory = true;
            options.EnableProfiling = true;
        });

        // Add runtime services
        services.AddDotComputeRuntime();

        var provider = services.BuildServiceProvider();

        // Step 3: Get accelerator and initialize
        var accelerator = provider.GetRequiredService<IAccelerator>();
        await accelerator.InitializeAsync();

        Console.WriteLine("=== DotCompute Metal Backend - Hello World ===");
        Console.WriteLine($"Device: {accelerator.Info.Name}");
        Console.WriteLine($"GPU Family: {accelerator.Info.GpuFamily}");
        Console.WriteLine($"Memory: {accelerator.Info.GlobalMemorySize / (1024 * 1024 * 1024)} GB");
        Console.WriteLine();

        // Step 4: Prepare data
        const int size = 1_000_000;
        var a = new float[size];
        var b = new float[size];
        var result = new float[size];

        // Initialize with sample data
        for (int i = 0; i < size; i++)
        {
            a[i] = i;
            b[i] = i * 2;
        }

        Console.WriteLine($"Computing vector addition of {size:N0} elements...");

        // Step 5: Allocate GPU memory
        var bufferA = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferB = await accelerator.Memory.AllocateAsync<float>(size);
        var bufferResult = await accelerator.Memory.AllocateAsync<float>(size);

        // Copy data to GPU
        a.CopyTo(bufferA.AsSpan());
        b.CopyTo(bufferB.AsSpan());

        // Step 6: Get orchestrator and execute kernel
        var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await orchestrator.ExecuteAsync<float[]>(
            nameof(Kernels.VectorAdd),
            bufferA,
            bufferB,
            bufferResult);
        sw.Stop();

        // Step 7: Copy results back
        bufferResult.AsSpan().CopyTo(result);

        Console.WriteLine($"Execution time: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine();

        // Verify results
        bool correct = true;
        for (int i = 0; i < Math.Min(10, size); i++)
        {
            float expected = a[i] + b[i];
            if (Math.Abs(result[i] - expected) > 0.0001f)
            {
                correct = false;
                Console.WriteLine($"Error at index {i}: expected {expected}, got {result[i]}");
                break;
            }
        }

        if (correct)
        {
            Console.WriteLine("✓ Results verified correctly!");
            Console.WriteLine($"Sample results: {result[0]}, {result[1]}, {result[2]}, ...");
        }

        // Step 8: Cleanup
        await bufferA.DisposeAsync();
        await bufferB.DisposeAsync();
        await bufferResult.DisposeAsync();
        await accelerator.DisposeAsync();

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
```

### Run the Program

```bash
dotnet run

# Expected output:
# === DotCompute Metal Backend - Hello World ===
# Device: Apple M2
# GPU Family: Apple8
# Memory: 24 GB
#
# Computing vector addition of 1,000,000 elements...
# Execution time: 1ms
#
# ✓ Results verified correctly!
# Sample results: 0, 3, 6, ...
```

---

## Understanding [Kernel] Attributes

The `[Kernel]` attribute enables automatic translation from C# to Metal Shading Language.

### Basic Kernel Structure

```csharp
[Kernel]
public static void MyKernel(
    ReadOnlySpan<float> input,   // Read-only buffer
    Span<float> output,          // Writable buffer
    int parameter)               // Scalar parameter
{
    // Get thread ID
    int idx = Kernel.ThreadId.X;

    // Bounds checking (important!)
    if (idx < output.Length)
    {
        // Your computation
        output[idx] = input[idx] * parameter;
    }
}
```

### Threading Models

**1D Grid (Linear):**
```csharp
[Kernel]
public static void Linear1D(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
        data[idx] *= 2.0f;
}
```

**2D Grid:**
```csharp
[Kernel]
public static void Grid2D(Span<float> matrix, int width, int height)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;

    if (x < width && y < height)
    {
        int idx = y * width + x;
        matrix[idx] = x + y;
    }
}
```

**3D Grid:**
```csharp
[Kernel]
public static void Grid3D(Span<float> volume, int width, int height, int depth)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;
    int z = Kernel.ThreadId.Z;

    if (x < width && y < height && z < depth)
    {
        int idx = z * width * height + y * width + x;
        volume[idx] = x + y + z;
    }
}
```

### Supported Operations

```csharp
[Kernel]
public static void SupportedOperations(
    ReadOnlySpan<float> input,
    Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx >= output.Length) return;

    float value = input[idx];

    // Arithmetic
    float result = value + 1.0f;
    result = result * 2.0f;
    result = result - 0.5f;
    result = result / 3.0f;

    // Math functions
    result = MathF.Sqrt(result);
    result = MathF.Sin(result);
    result = MathF.Cos(result);
    result = MathF.Abs(result);
    result = MathF.Pow(result, 2.0f);

    // Comparisons
    if (result > 10.0f)
        result = 10.0f;

    // Loops
    float sum = 0.0f;
    for (int i = 0; i < 10; i++)
        sum += i;

    output[idx] = result + sum;
}
```

---

## Memory Management

### Unified Memory Buffers

DotCompute provides unified memory that works seamlessly across CPU and GPU:

```csharp
// Allocate unified buffer
var buffer = await accelerator.Memory.AllocateAsync<float>(1_000_000);

// Use on CPU (zero-copy on Apple Silicon)
var cpuSpan = buffer.AsSpan();
cpuSpan[0] = 42.0f;

// Use on GPU
await orchestrator.ExecuteAsync<float[]>(
    nameof(MyKernel),
    buffer);  // Automatically transferred if needed

// Cleanup
await buffer.DisposeAsync();
```

### Memory Pooling

Enable memory pooling for frequent allocations:

```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.MemoryPoolSizeClasses = 21;  // 21 size classes
    options.EnableUnifiedMemory = true;
});

// Allocations now use pool (90% reduction in allocation time)
for (int i = 0; i < 1000; i++)
{
    using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
    // Buffer returned to pool on disposal
}
```

### Memory Pressure Monitoring

```csharp
var memoryManager = (MetalMemoryManager)accelerator.Memory;
var monitor = memoryManager.PressureMonitor;

monitor.PressureChanged += (sender, args) =>
{
    if (args.Level >= MemoryPressureLevel.High)
    {
        Console.WriteLine($"High memory pressure: {args.Level}");
        // Reduce allocations or free buffers
    }
};
```

---

## Performance Optimization

### 1. Enable Unified Memory (Apple Silicon)

```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.EnableUnifiedMemory = true;  // 2-3x speedup on Apple Silicon
});
```

### 2. Use Optimal Threadgroup Sizes

```csharp
var options = new CompilationOptions
{
    EnableAutoTuning = true,  // Automatically selects optimal size
    OptimizationLevel = OptimizationLevel.Maximum
};

var compiled = await accelerator.CompileKernelAsync(definition, options);
```

### 3. Batch Operations with Compute Graphs

```csharp
var graph = new MetalComputeGraph("pipeline", logger);

// Add nodes with dependencies
var node1 = graph.AddKernelNode(kernel1, grid1, threadgroup1, args1);
var node2 = graph.AddKernelNode(kernel2, grid2, threadgroup2, args2, new[] { node1 });
var node3 = graph.AddKernelNode(kernel3, grid3, threadgroup3, args3, new[] { node1 });

graph.Build();

var executor = new MetalGraphExecutor(logger);
var result = await executor.ExecuteAsync(graph, commandQueue);

Console.WriteLine($"Executed {result.NodesExecuted} nodes in {result.TotalExecutionTimeMs}ms");
```

### 4. Enable Kernel Caching

```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.MaxCachedKernels = 1000;
    options.CacheDirectory = "./metal_cache";
});

// First compilation: 20-50ms
var compiled = await accelerator.CompileKernelAsync(definition);

// Subsequent compilations: <1ms (cache hit)
var cached = await accelerator.CompileKernelAsync(definition);
```

### 5. Use Metal Performance Shaders (MPS)

```csharp
// Standard matrix multiply: 8-12ms
var result = await MatrixMultiplyKernel(a, b, ...);

// MPS accelerated: 2-3ms (3-4x faster)
var resultMPS = await MetalMPSIntegration.MatrixMultiplyMPSAsync(
    a, b, rowsA, colsA, colsB, device, commandQueue);
```

---

## Common Patterns

### Pattern 1: Image Processing

```csharp
[Kernel]
public static void GrayscaleFilter(
    ReadOnlySpan<byte> inputRGBA,
    Span<byte> outputRGBA,
    int width,
    int height)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;

    if (x >= width || y >= height) return;

    int idx = (y * width + x) * 4;  // 4 bytes per pixel (RGBA)

    byte r = inputRGBA[idx];
    byte g = inputRGBA[idx + 1];
    byte b = inputRGBA[idx + 2];

    // Grayscale conversion
    byte gray = (byte)(0.299f * r + 0.587f * g + 0.114f * b);

    outputRGBA[idx] = gray;
    outputRGBA[idx + 1] = gray;
    outputRGBA[idx + 2] = gray;
    outputRGBA[idx + 3] = inputRGBA[idx + 3];  // Preserve alpha
}
```

### Pattern 2: Reduction (Sum)

```csharp
[Kernel]
public static void ReduceSum(
    ReadOnlySpan<float> input,
    Span<float> partialSums,
    int inputSize)
{
    int idx = Kernel.ThreadId.X;
    int stride = 256;  // Threadgroup size

    float sum = 0.0f;

    // Each thread sums multiple elements
    for (int i = idx; i < inputSize; i += stride)
    {
        sum += input[i];
    }

    // Store partial sum
    if (idx < partialSums.Length)
    {
        partialSums[idx] = sum;
    }
}

// Host code
var partialSums = await ReduceSumKernel(input, ...);
float totalSum = partialSums.Sum();  // Final sum on CPU
```

### Pattern 3: Matrix Transpose

```csharp
[Kernel]
public static void MatrixTranspose(
    ReadOnlySpan<float> input,
    Span<float> output,
    int rows,
    int cols)
{
    int col = Kernel.ThreadId.X;
    int row = Kernel.ThreadId.Y;

    if (row < rows && col < cols)
    {
        int inputIdx = row * cols + col;
        int outputIdx = col * rows + row;
        output[outputIdx] = input[inputIdx];
    }
}
```

---

## Debugging and Profiling

### Enable Debug Logging

```csharp
services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
    builder.AddFilter("DotCompute.Backends.Metal", LogLevel.Trace);
});
```

### Use Performance Profiler

```csharp
var profiler = ((MetalAccelerator)accelerator).Profiler;

profiler.StartProfiling("my_session");

// Execute kernels
await orchestrator.ExecuteAsync<float[]>(nameof(Kernel1), ...);
await orchestrator.ExecuteAsync<float[]>(nameof(Kernel2), ...);

var report = profiler.EndProfiling();

Console.WriteLine($"Total GPU time: {report.TotalGpuTimeMs}ms");
Console.WriteLine($"Total CPU time: {report.TotalCpuTimeMs}ms");

foreach (var (name, profile) in report.KernelProfiles)
{
    Console.WriteLine($"{name}: {profile.AverageExecutionMs}ms");
}
```

### Enable Debug Markers

```csharp
services.AddDotComputeMetalBackend(options =>
{
    options.EnableDebugMarkers = true;
});

// View in Xcode Instruments (Metal System Trace)
```

### Cross-Backend Validation

```csharp
services.AddProductionDebugging();  // Adds debug orchestrator

// Automatically validates GPU results against CPU
var result = await orchestrator.ExecuteAsync<float[]>(
    nameof(MyKernel),
    inputBuffer,
    outputBuffer);

// Throws if GPU and CPU results differ
```

---

## Common Pitfalls

### 1. Missing Bounds Checking

```csharp
// ❌ BAD: No bounds check
[Kernel]
public static void Unsafe(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    data[idx] *= 2.0f;  // May access out of bounds!
}

// ✅ GOOD: Always check bounds
[Kernel]
public static void Safe(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
        data[idx] *= 2.0f;
}
```

### 2. Forgetting to Synchronize

```csharp
// ❌ BAD: Reading before synchronization
await orchestrator.ExecuteAsync<float[]>(nameof(MyKernel), buffer);
var value = buffer.AsSpan()[0];  // May read stale data!

// ✅ GOOD: Explicit synchronization
await orchestrator.ExecuteAsync<float[]>(nameof(MyKernel), buffer);
await buffer.SynchronizeAsync();  // Ensure GPU->CPU transfer complete
var value = buffer.AsSpan()[0];
```

### 3. Memory Leaks

```csharp
// ❌ BAD: Not disposing buffers
for (int i = 0; i < 1000; i++)
{
    var buffer = await accelerator.Memory.AllocateAsync<float>(1000);
    // Memory leak!
}

// ✅ GOOD: Always dispose
for (int i = 0; i < 1000; i++)
{
    using var buffer = await accelerator.Memory.AllocateAsync<float>(1000);
    // Automatic cleanup
}
```

---

## Next Steps

Now that you've learned the basics, explore these advanced topics:

1. **[Metal API Reference](METAL_API_REFERENCE.md)** - Complete API documentation
2. **[Architecture Guide](../ARCHITECTURE.md)** - Deep dive into Metal backend architecture
3. **[Performance Guide](UnifiedMemoryOptimization.md)** - Advanced optimization techniques
4. **[Troubleshooting Guide](TROUBLESHOOTING.md)** - Common issues and solutions
5. **[Real-World Examples](../../tests/Integration/)** - Production-ready code samples

### Example Projects

- **Image Processing Pipeline**: See `tests/Integration/RealWorldComputeTests.cs`
- **Audio DSP**: Real-time audio processing at 44.1kHz
- **Machine Learning**: Neural network inference on GPU
- **Scientific Computing**: Large-scale numerical simulations

---

## Community and Support

- **GitHub Issues**: [DotCompute/DotCompute/issues](https://github.com/DotCompute/DotCompute/issues)
- **Discussions**: [GitHub Discussions](https://github.com/DotCompute/DotCompute/discussions)
- **Documentation**: [docs.dotcompute.io](https://docs.dotcompute.io)

Tag Metal-specific questions with `backend:metal` for faster responses.

---

**Happy Computing with Metal! 🚀**

*Built with ❤️ for the .NET community on macOS*
