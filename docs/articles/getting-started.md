# Getting Started with DotCompute

This guide will help you install DotCompute and write your first GPU-accelerated kernel in under 10 minutes using **correct API patterns** for v0.4.2-rc2.

> 📖 **See Also**: [Working Reference Example](examples/WORKING_REFERENCE.md) for tested, working code examples.

## Prerequisites

### Required
- **.NET 9.0 SDK** or later ([download](https://dotnet.microsoft.com/download))
- **C# 13** compatible IDE (Visual Studio 2022 17.8+, VS Code, or Rider)

### Optional (for GPU acceleration)
- **NVIDIA GPU** with Compute Capability 5.0+ for CUDA support
  - CUDA Toolkit 12.0+ ([download](https://developer.nvidia.com/cuda-downloads))
- **Apple Silicon Mac** (M1/M2/M3) for Metal support
- **AMD GPU** for OpenCL support (foundation complete)

### Verification

Check your setup:

```bash
# Verify .NET version
dotnet --version
# Should show 9.0.0 or later

# Check for NVIDIA GPU (optional)
nvidia-smi
# Should show GPU details if CUDA available

# Check CUDA version (optional)
nvcc --version
# Should show CUDA 12.0+ if installed
```

## Installation

### 1. Create a New Project

```bash
# Create console application
dotnet new console -n MyFirstKernel
cd MyFirstKernel
```

### 2. Install DotCompute Packages

```bash
# Core runtime (required)
dotnet add package DotCompute.Core
dotnet add package DotCompute.Abstractions
dotnet add package DotCompute.Memory

# CPU backend (always available)
dotnet add package DotCompute.Backends.CPU

# GPU backends (optional, install what you need)
dotnet add package DotCompute.Backends.CUDA  # For NVIDIA GPUs
dotnet add package DotCompute.Backends.Metal # For Apple Silicon

# Source generators for [Kernel] attribute support
dotnet add package DotCompute.Generators

# Runtime services
dotnet add package DotCompute.Runtime
```

### 3. Enable C# 13 (if not already)

Edit your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <LangVersion>13.0</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- DotCompute packages -->
  <ItemGroup>
    <PackageReference Include="DotCompute.Core" Version="0.4.2-rc2" />
    <PackageReference Include="DotCompute.Abstractions" Version="0.4.2-rc2" />
    <PackageReference Include="DotCompute.Backends.CPU" Version="0.4.2-rc2" />
    <PackageReference Include="DotCompute.Generators" Version="0.4.2-rc2" />
    <PackageReference Include="DotCompute.Runtime" Version="0.4.2-rc2" />
  </ItemGroup>
</Project>
```

## Your First Kernel

Let's write a simple vector addition kernel that runs on both CPU and GPU.

### Step 1: Write the Kernel

Create a file `Kernels.cs`:

```csharp
using DotCompute;
using System;

namespace MyFirstKernel;

public static class Kernels
{
    /// <summary>
    /// Adds two vectors element-wise: result[i] = a[i] + b[i]
    /// </summary>
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result)
    {
        // Get thread ID (works on both CPU and GPU)
        int idx = Kernel.ThreadId.X;

        // Bounds check (important for safety)
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

**Key Points**:
- `[Kernel]` attribute marks the method for GPU acceleration
- Must be `static` and return `void`
- Use `ReadOnlySpan<T>` for inputs, `Span<T>` for outputs
- `Kernel.ThreadId.X` provides thread indexing
- Always include bounds checking

### Step 2: Set Up Dependency Injection

Edit `Program.cs`:

```csharp
using DotCompute;
using DotCompute.Abstractions;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;

namespace MyFirstKernel;

class Program
{
    static async Task Main(string[] args)
    {
        // Build host with DotCompute services
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add DotCompute runtime (includes orchestration, memory, backends)
                services.AddDotComputeRuntime();
            })
            .Build();

        // Get orchestrator from DI container
        var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();

        // Create input data
        const int size = 1_000_000;
        var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
        var result = new float[size];

        Console.WriteLine($"Adding {size:N0} elements...");

        // Execute kernel (automatic backend selection)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await orchestrator.ExecuteKernelAsync(
            kernelName: "VectorAdd",
            args: new object[] { a, b, result }
        );

        stopwatch.Stop();

        // Verify results (first 5 elements)
        Console.WriteLine("\nResults (first 5 elements):");
        for (int i = 0; i < 5; i++)
        {
            Console.WriteLine($"  result[{i}] = {result[i]} (expected: {a[i] + b[i]})");
        }

        Console.WriteLine($"\nExecution time: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Throughput: {size / stopwatch.Elapsed.TotalSeconds / 1_000_000:F2}M elements/sec");
    }
}
```

### Step 3: Build and Run

```bash
# Build project (source generator runs automatically)
dotnet build

# Run application
dotnet run
```

**Expected Output**:
```
Adding 1,000,000 elements...

Results (first 5 elements):
  result[0] = 0 (expected: 0)
  result[1] = 3 (expected: 3)
  result[2] = 6 (expected: 6)
  result[3] = 9 (expected: 9)
  result[4] = 12 (expected: 12)

Execution time: 2.34ms
Throughput: 427.35M elements/sec
```

## Understanding What Happened

### 1. Source Generator Created Code

When you built the project, `DotCompute.Generators` automatically generated:

- **CPU SIMD implementation** using `Vector<T>` for hardware acceleration
- **CUDA kernel** (if CUDA backend installed)
- **Metal shader** (if Metal backend installed)
- **Registration code** to make the kernel discoverable at runtime

You can see the generated code in your IDE's solution explorer under "Dependencies → Analyzers → DotCompute.Generators".

### 2. Automatic Backend Selection

The orchestrator automatically selected the best backend:

- **Small data (< 10,000 elements)**: CPU (no transfer overhead)
- **Large data + GPU available**: CUDA or Metal
- **Large data + no GPU**: CPU with SIMD

### 3. Zero-Copy on CPU

When running on CPU, `Span<T>` enables zero-copy access:
- No intermediate arrays allocated
- Direct memory access
- Minimal overhead (< 50μs)

## Next Steps

### Add More Complex Kernels

Try a matrix multiplication:

```csharp
[Kernel]
public static void MatrixMultiply(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result,
    int width)
{
    int row = Kernel.ThreadId.Y;
    int col = Kernel.ThreadId.X;

    if (row < width && col < width)
    {
        float sum = 0;
        for (int k = 0; k < width; k++)
        {
            sum += a[row * width + k] * b[k * width + col];
        }
        result[row * width + col] = sum;
    }
}
```

### Force Specific Backend

```csharp
// Force CUDA execution
await orchestrator.ExecuteAsync<object>(
    kernelName: "VectorAdd",
    preferredBackend: "CUDA",
    args: new object[] { a, b, result }
);
```

### Enable Performance Profiling

```csharp
services.AddDotComputeRuntime(options =>
{
    options.EnableTelemetry = true;
});
```

### Enable Debug Validation

```csharp
#if DEBUG
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.ValidateAllExecutions = true;
});
#endif
```

## Troubleshooting

### "Kernel not found" Error

**Problem**: `ExecuteKernelAsync` throws "Kernel 'VectorAdd' not found"

**Solution**: Ensure the source generator ran:
1. Clean and rebuild: `dotnet clean && dotnet build`
2. Check that `DotCompute.Generators` package is installed
3. Verify kernel method is `static` with `[Kernel]` attribute

### CUDA "Device not found" Error

**Problem**: CUDA backend fails to initialize

**Solution**:
1. Verify GPU: `nvidia-smi`
2. Check CUDA version: `nvcc --version`
3. Install CUDA Toolkit 12.0+
4. Restart IDE after CUDA installation

### Slow Performance on First Run

**Observation**: First execution is slower than subsequent runs

**Explanation**: This is normal:
- First run: Kernel compilation (~50-200ms)
- Subsequent runs: Cached kernel (< 1ms overhead)
- Memory pool warm-up (first allocation)

### "Assembly could not be found" Error

**Problem**: Runtime error about missing DotCompute assemblies

**Solution**: Ensure all required packages are installed:
```bash
dotnet add package DotCompute.Core
dotnet add package DotCompute.Runtime
dotnet add package DotCompute.Backends.CPU
```

## Common Patterns

### Pattern 1: Async Kernel Execution

```csharp
// Execute multiple kernels concurrently
var task1 = orchestrator.ExecuteKernelAsync("Kernel1", params1);
var task2 = orchestrator.ExecuteKernelAsync("Kernel2", params2);

await Task.WhenAll(task1, task2);
```

### Pattern 2: Device-Specific Execution

```csharp
var factory = host.Services.GetRequiredService<IUnifiedAcceleratorFactory>();

// Get specific device (e.g., CUDA)
var devices = await factory.GetAvailableDevicesAsync();
var cudaDevice = devices.FirstOrDefault(d => d.DeviceType == "CUDA");

if (cudaDevice != null)
{
    // Create accelerator for this device
    using var accelerator = await factory.CreateAsync(cudaDevice);

    // Use orchestrator with specific accelerator
    await orchestrator.ExecuteAsync<object>(
        "MyKernel",
        accelerator,
        new object[] { inputData, outputData }
    );
}
```

### Pattern 3: Runtime Configuration

```csharp
using DotCompute.Runtime.Configuration;

services.Configure<DotComputeRuntimeOptions>(options =>
{
    options.ValidateCapabilities = true;
    options.AcceleratorLifetime = ServiceLifetime.Transient;
});

services.AddDotComputeRuntime();
services.AddProductionOptimization();  // Enable ML-based backend selection
services.AddProductionDebugging();     // Enable cross-backend validation
```

## Performance Tips

### 1. Use Appropriate Data Types
- **`Span<T>`** for zero-copy CPU operations
- **`float`** for most GPU operations (single-precision is faster)
- **`double`** only when precision is critical

### 2. Batch Operations
```csharp
// ❌ Bad: Multiple small kernel calls
for (int i = 0; i < 1000; i++)
{
    await orchestrator.ExecuteKernelAsync("SmallKernel", smallData[i]);
}

// ✅ Good: Single large kernel call
await orchestrator.ExecuteKernelAsync("BatchKernel", allData);
```

### 3. Reuse Buffers
```csharp
// Allocate once, reuse many times
var buffer = await memoryManager.AllocateAsync<float>(1_000_000);

for (int i = 0; i < iterations; i++)
{
    await buffer.CopyFromAsync(inputData[i]);
    await orchestrator.ExecuteKernelAsync("MyKernel", new { buffer });
    await buffer.CopyToAsync(outputData[i]);
}

await buffer.DisposeAsync(); // Return to pool
```

## IDE Integration

### Visual Studio

DotCompute analyzers provide real-time feedback:

- **Error Squiggles**: DC001-DC012 diagnostics
- **Quick Fixes**: Lightbulb actions for common issues
- **IntelliSense**: Full support for generated code

### VS Code

Install C# Dev Kit for full analyzer support:

```bash
# Install C# Dev Kit extension
code --install-extension ms-dotnettools.csdevkit
```

## Example Projects

### Console Application (Simple)

```
MyKernelApp/
├── MyKernelApp.csproj
├── Program.cs
└── Kernels.cs
```

### Web API with Compute (Advanced)

```
ComputeApi/
├── ComputeApi.csproj
├── Program.cs
├── Controllers/
│   └── ComputeController.cs
├── Kernels/
│   ├── ImageProcessing.cs
│   └── DataAnalysis.cs
└── appsettings.json
```

## Further Reading

- [Kernel Development Guide](guides/kernel-development.md) - Write efficient kernels
- [Backend Selection Guide](guides/backend-selection.md) - Choose optimal backends
- [Performance Tuning](guides/performance-tuning.md) - Optimize execution
- [Architecture Overview](architecture/overview.md) - Understand the system design
- [API Reference](../api/index.md) - Complete API documentation

## Getting Help

- **Documentation**: [https://dotcompute.dev](https://dotcompute.dev)
- **GitHub Issues**: [Report bugs or request features](https://github.com/DotCompute/DotCompute/issues)
- **Discussions**: [Ask questions](https://github.com/DotCompute/DotCompute/discussions)

---

**Welcome to DotCompute! • Write Once • Run Anywhere • Run Fast**
