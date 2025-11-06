# Quick Start Guide

Get up and running with DotCompute in minutes. This guide shows you how to write your first GPU-accelerated computation using **correct API patterns** for v0.4.1-rc2.

> 📖 **See Also**: [Working Reference Example](examples/WORKING_REFERENCE.md) for comprehensive examples and patterns.

## Prerequisites

- .NET 9.0 SDK or later
- Visual Studio 2022 17.8+ or VS Code with C# extension
- (Optional) NVIDIA GPU with Compute Capability 5.0+ for CUDA support
- (Optional) macOS with Apple Silicon for Metal support

## Installation

Install DotCompute v0.4.1-rc2 via NuGet:

```bash
# Core packages (required)
dotnet add package DotCompute.Core --version 0.4.1-rc2
dotnet add package DotCompute.Abstractions --version 0.4.1-rc2
dotnet add package DotCompute.Runtime --version 0.4.1-rc2

# CPU backend (always recommended)
dotnet add package DotCompute.Backends.CPU --version 0.4.1-rc2

# GPU backends (optional)
dotnet add package DotCompute.Backends.CUDA --version 0.4.1-rc2   # NVIDIA GPUs
dotnet add package DotCompute.Backends.OpenCL --version 0.4.1-rc2 # Cross-platform GPU
dotnet add package DotCompute.Backends.Metal --version 0.4.1-rc2  # Apple Silicon

# Source generators (required for [Kernel] attribute)
dotnet add package DotCompute.Generators --version 0.4.1-rc2
```

## Your First Kernel

The simplest way to create a compute kernel is using the `[Kernel]` attribute:

```csharp
using DotCompute;
using DotCompute.Abstractions;

public static class MyKernels
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
```

## Executing with DotCompute Runtime

Use the unified `AddDotComputeRuntime()` method for complete setup:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Factories;

// Setup host with DotCompute services
var host = Host.CreateApplicationBuilder(args);
host.Services.AddLogging();

// ✅ Single method registers ALL necessary services!
host.Services.AddDotComputeRuntime();

var app = host.Build();

// Prepare data
var a = new float[] { 1, 2, 3, 4, 5 };
var b = new float[] { 10, 20, 30, 40, 50 };
var result = new float[5];

// Execute kernel with automatic backend selection
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { a, b, result }
);

// Read results
Console.WriteLine(string.Join(", ", result)); // Output: 11, 22, 33, 44, 55
```

## Device Discovery and Selection

Device enumeration works with the manual registration pattern:

```csharp
// Factory is already obtained in the example above
var devices = await factory.GetAvailableDevicesAsync();

Console.WriteLine($"Found {devices.Count} device(s):");
foreach (var device in devices)
{
    Console.WriteLine($"  - {device.Name} ({device.DeviceType})");
    Console.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
}

// Find and use a specific device (e.g., CUDA)
var cudaDevice = devices.FirstOrDefault(d => d.DeviceType == "CUDA");
if (cudaDevice != null)
{
    // Create accelerator for this device
    using var accelerator = await factory.CreateAsync(cudaDevice);

    Console.WriteLine($"Using GPU: {cudaDevice.Name}");

    // Now use the accelerator for kernel compilation and execution
    // (See backend-specific documentation for details)
}
```

## Backend Selection

With the manual registration pattern, you explicitly select which device to use:

```csharp
// Get factory
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

// Select best available device (priority: CUDA > OpenCL > CPU)
var device = devices.FirstOrDefault(d => d.DeviceType == "CUDA")
          ?? devices.FirstOrDefault(d => d.DeviceType == "OpenCL")
          ?? devices.First();

// Create accelerator
using var accelerator = await factory.CreateAsync(device);
Console.WriteLine($"Using device: {device.Name} ({device.DeviceType})");

// Now compile and execute kernels on this accelerator
// (See backend-specific documentation for compilation details)
```

## Matrix Operations Example

Define a matrix multiplication kernel:

```csharp
public static class MatrixKernels
{
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
}

// Usage
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

var matrixA = new float[9] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
var matrixB = new float[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }; // Identity matrix
var result = new float[9];

await orchestrator.ExecuteKernelAsync(
    "MatrixMultiply",
    new object[] { matrixA, matrixB, result, 3 }
);
```

## Debugging Cross-Backend

Enable debugging services for cross-backend validation:

```csharp
using DotCompute.Core.Debugging;
using Microsoft.Extensions.DependencyInjection;

// Add debugging services during setup
host.Services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.ValidateAllExecutions = true; // Validate CPU vs GPU results
});

// Debugging happens automatically during kernel execution
// Any discrepancies will be logged with detailed diagnostics
await orchestrator.ExecuteKernelAsync("VectorAdd", new object[] { a, b, result });

// Check logs for validation results
// If results don't match, detailed difference reports will be shown
```

## Performance Optimization

### Enable Adaptive Backend Selection

```csharp
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;

// During host setup
host.Services.AddDotComputeRuntime();
host.Services.AddProductionOptimization(); // ML-based backend selection

var app = host.Build();
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Orchestrator now uses machine learning to select optimal backend
await orchestrator.ExecuteKernelAsync("VectorAdd", new object[] { a, b, result });
```

### Memory Pooling (Automatic)

```csharp
// Memory pooling is automatic in DotCompute v0.4.0-rc2
// The runtime manages buffers efficiently, reducing allocations by 90%+

// Just use normal arrays - pooling happens automatically
var data = new float[1_000_000];
await orchestrator.ExecuteKernelAsync("ProcessData", new object[] { data });

// No manual pool management required!
```

### Batch Multiple Kernel Calls

```csharp
// Execute multiple kernels efficiently
var tasks = new[]
{
    orchestrator.ExecuteKernelAsync("Kernel1", new object[] { data1 }),
    orchestrator.ExecuteKernelAsync("Kernel2", new object[] { data2 }),
    orchestrator.ExecuteKernelAsync("Kernel3", new object[] { data3 })
};

await Task.WhenAll(tasks); // Parallel execution
```

## Advanced: Writing Raw MSL (Metal)

For Apple Silicon, you can write Metal Shading Language directly:

```csharp
var mslCode = @"
#include <metal_stdlib>
using namespace metal;

kernel void vector_add(
    const device float* a [[buffer(0)]],
    const device float* b [[buffer(1)]],
    device float* result [[buffer(2)]],
    uint id [[thread_position_in_grid]])
{
    result[id] = a[id] + b[id];
}
";

var kernel = await accelerator.CompileKernelAsync(
    new KernelDefinition
    {
        Name = "vector_add",
        Code = mslCode,
        EntryPoint = "vector_add"
    }
);
```

## Native AOT Support

DotCompute is fully Native AOT compatible:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

Startup time: **< 10ms** with Native AOT

## Next Steps

- **[Kernel Attribute Reference](reference/kernel-attribute.md)** - Learn all kernel features
- **[Performance Guide](performance/characteristics.md)** - Optimize your kernels
- **[CUDA Programming](advanced/cuda-programming.md)** - Advanced CUDA features
- **[Algorithm Library](api/DotCompute.Algorithms.html)** - Pre-built operations

## Example Projects

Check out complete examples in the repository:

- **Vector Addition** - `samples/VectorAdd/`
- **Matrix Multiplication** - `samples/MatrixMultiply/`
- **Image Processing** - `samples/ImageFilters/`
- **Signal Processing** - `samples/FFT/`

## Getting Help

- **Documentation**: https://mivertowski.github.io/DotCompute/
- **Issues**: https://github.com/mivertowski/DotCompute/issues
- **Discussions**: https://github.com/mivertowski/DotCompute/discussions

---

**Performance Tip**: Start with CPU backend for development and debugging, then switch to GPU for production performance. DotCompute makes this transition seamless!
