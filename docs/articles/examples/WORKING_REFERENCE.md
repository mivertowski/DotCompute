# DotCompute - Working Reference Example

This document shows the **correct** API patterns for using DotCompute v0.4.0-rc2.

## ✅ Correct Pattern: Device Discovery and Enumeration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;

// 1. Setup Dependency Injection
var services = new ServiceCollection();

// Add logging (optional but recommended)
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Configure DotCompute runtime options
services.Configure<DotComputeRuntimeOptions>(options =>
{
    options.ValidateCapabilities = false; // Set to true for strict validation
    options.AcceleratorLifetime = DotCompute.Runtime.Configuration.ServiceLifetime.Transient;
});

// Register the accelerator factory
services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

var serviceProvider = services.BuildServiceProvider();

// 2. Get the factory from DI
var factory = serviceProvider.GetRequiredService<IUnifiedAcceleratorFactory>();

// 3. Enumerate available devices
var devices = await factory.GetAvailableDevicesAsync();

Console.WriteLine($"Found {devices.Count} device(s):");
foreach (var device in devices)
{
    Console.WriteLine($"  - {device.Name} ({device.DeviceType})");
    Console.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
    Console.WriteLine($"    Compute Units: {device.MaxComputeUnits}");
}
```

## ✅ Correct Pattern: Using AddDotComputeRuntime Extension

The recommended approach uses the extension method:

```csharp
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;

// Using Host builder (recommended for most applications)
var host = Host.CreateApplicationBuilder(args);

// Add DotCompute with all defaults
host.Services.AddDotComputeRuntime();

var app = host.Build();

// Get factory
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();
```

## ✅ Correct Pattern: Creating Specific Accelerators

```csharp
// After enumerating devices, create an accelerator for a specific device
var devices = await factory.GetAvailableDevicesAsync();

// Find CUDA device
var cudaDevice = devices.FirstOrDefault(d => d.DeviceType == "CUDA");
if (cudaDevice != null)
{
    // Create accelerator for this specific device
    var accelerator = await factory.CreateAsync(cudaDevice);

    // Use the accelerator...

    // Dispose when done
    accelerator.Dispose();
}
```

## ✅ Correct Pattern: Kernel Execution with IComputeOrchestrator

```csharp
using DotCompute.Abstractions.Interfaces;

// Get orchestrator from DI
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Prepare data
var a = new float[] { 1, 2, 3, 4, 5 };
var b = new float[] { 10, 20, 30, 40, 50 };
var result = new float[5];

// Execute kernel with automatic backend selection
await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { a, b, result }
);

Console.WriteLine($"Result: {string.Join(", ", result)}");
```

## ❌ INCORRECT Patterns (Do Not Use)

### ❌ Wrong: Direct Instantiation
```csharp
// DON'T DO THIS - bypasses DI and configuration
using var accelerator = new CudaAccelerator(); // ❌ WRONG
using var accelerator = new CpuAccelerator();  // ❌ WRONG
```

### ❌ Wrong: ComputeOrchestrator.CreateDefault()
```csharp
// DON'T DO THIS - this method doesn't exist
var orchestrator = ComputeOrchestrator.CreateDefault(); // ❌ WRONG
```

### ❌ Wrong: Using UnifiedBuffer Directly Without Factory
```csharp
// DON'T DO THIS - requires proper accelerator from factory
var buffer = new UnifiedBuffer<float>(data, accelerator); // ❌ May not work correctly
```

## ✅ Complete Working Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Build host with DotCompute services
        var host = Host.CreateApplicationBuilder(args);

        host.Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        host.Services.AddDotComputeRuntime();

        var app = host.Build();

        // 2. Get factory and enumerate devices
        var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
        var devices = await factory.GetAvailableDevicesAsync();

        Console.WriteLine($"\\n=== Available Devices ===");
        Console.WriteLine($"Found {devices.Count} device(s):\\n");

        foreach (var device in devices)
        {
            Console.WriteLine($"📱 {device.Name}");
            Console.WriteLine($"   Type: {device.DeviceType}");
            Console.WriteLine($"   Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
            Console.WriteLine($"   Compute Units: {device.MaxComputeUnits}");
            Console.WriteLine();
        }

        // 3. Use orchestrator for kernel execution
        var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

        // Example: Vector addition
        var a = new float[] { 1, 2, 3, 4, 5 };
        var b = new float[] { 10, 20, 30, 40, 50 };
        var result = new float[5];

        await orchestrator.ExecuteKernelAsync(
            "VectorAdd",
            new object[] { a, b, result }
        );

        Console.WriteLine($"Vector addition result: {string.Join(", ", result)}");
    }
}

// Kernel definition
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

## Package Installation

```bash
# Core packages (required)
dotnet add package DotCompute.Core --version 0.4.0-rc2
dotnet add package DotCompute.Abstractions --version 0.4.0-rc2
dotnet add package DotCompute.Runtime --version 0.4.0-rc2
dotnet add package DotCompute.Memory --version 0.4.0-rc2

# Backend packages (install what you need)
dotnet add package DotCompute.Backends.CPU --version 0.4.0-rc2     # Always recommended
dotnet add package DotCompute.Backends.CUDA --version 0.4.0-rc2    # NVIDIA GPUs
dotnet add package DotCompute.Backends.OpenCL --version 0.4.0-rc2  # Cross-platform GPU
dotnet add package DotCompute.Backends.Metal --version 0.4.0-rc2   # Apple Silicon

# Source generators (required for [Kernel] attribute)
dotnet add package DotCompute.Generators --version 0.4.0-rc2
```

## Key Takeaways

1. **Always use Dependency Injection** - Don't instantiate accelerators directly
2. **Use `IUnifiedAcceleratorFactory`** - For device discovery and accelerator creation
3. **Use `IComputeOrchestrator`** - For kernel execution with automatic backend selection
4. **Call `AddDotComputeRuntime()`** - This sets up all necessary services
5. **Dispose properly** - Accelerators implement `IDisposable`
6. **Check device capabilities** - Not all devices support all operations

## Additional Resources

- [Getting Started Guide](../getting-started.md)
- [Kernel Development Guide](../guides/kernel-development.md)
- [Dependency Injection Guide](../guides/dependency-injection.md)
- [API Reference](../../api/index.md)
