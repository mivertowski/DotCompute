# DotCompute - Working Reference Example

This document shows the **correct** API patterns for using DotCompute v0.6.2.

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

## ✅ CORRECT PATTERN: Using AddDotComputeRuntime()

**FIXED IN LATEST BUILD**: The namespace conflict has been resolved. There is now ONE unified `AddDotComputeRuntime()` method that registers ALL necessary services.

The **recommended** approach is simple and clean:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime; // ✅ ONLY namespace needed now!

// Using Host builder
var host = Host.CreateApplicationBuilder(args);

// Add logging (optional but recommended)
host.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// ✅ Single unified method registers ALL services!
host.Services.AddDotComputeRuntime();

var app = host.Build();

// Get factory - now properly registered!
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

// Get orchestrator - also registered!
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();
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

**IMPORTANT**: `IComputeOrchestrator` is now automatically registered by `AddDotComputeRuntime()`. No additional setup needed!

```csharp
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime;

// Get orchestrator - automatically registered!
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
```

**For direct accelerator usage**:

```csharp
using DotCompute.Abstractions.Factories;

// Get factory from DI
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();

// Get devices and create accelerator
var devices = await factory.GetAvailableDevicesAsync();
var device = devices.FirstOrDefault(d => d.DeviceType == "CUDA") ?? devices.First();

using var accelerator = await factory.CreateAsync(device);

// Now use the accelerator for computations
// (See backend-specific documentation for kernel compilation and execution)
```

## ❌ INCORRECT Patterns (Do Not Use)

### ❌ Wrong: Manual Service Registration (Deprecated Pattern)
```csharp
// DON'T DO THIS - use AddDotComputeRuntime() instead!
host.Services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>(); // ❌ Incomplete
host.Services.Configure<DotComputeRuntimeOptions>(options => { }); // ❌ Manual

// ✅ CORRECT: Use the unified method
host.Services.AddDotComputeRuntime(); // Registers EVERYTHING!
```

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

## ✅ Complete Working Example (RECOMMENDED)

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

        // ✅ Single unified method registers ALL services!
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

        // 3. Create accelerator for specific device
        var cudaDevice = devices.FirstOrDefault(d => d.DeviceType == "CUDA");
        if (cudaDevice != null)
        {
            using var accelerator = await factory.CreateAsync(cudaDevice);
            Console.WriteLine($"Using accelerator: {cudaDevice.Name}");

            // Now use accelerator for kernel compilation and execution
            // (See backend-specific documentation for details)
        }
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
dotnet add package DotCompute.Core --version 0.6.2
dotnet add package DotCompute.Abstractions --version 0.6.2
dotnet add package DotCompute.Runtime --version 0.6.2
dotnet add package DotCompute.Memory --version 0.6.2

# Backend packages (install what you need)
dotnet add package DotCompute.Backends.CPU --version 0.6.2     # Always recommended (Production)
dotnet add package DotCompute.Backends.CUDA --version 0.6.2    # NVIDIA GPUs (Production)
dotnet add package DotCompute.Backends.OpenCL --version 0.6.2  # Cross-platform GPU (Experimental)
dotnet add package DotCompute.Backends.Metal --version 0.6.2   # Apple Silicon (Experimental)

# Source generators (required for [Kernel] attribute)
dotnet add package DotCompute.Generators --version 0.6.2
```

## Key Takeaways

1. **Always use Dependency Injection** - Don't instantiate accelerators directly
2. **Call `AddDotComputeRuntime()`** - Single method registers ALL services (factory, orchestrator, memory, kernels)
3. **Use `IUnifiedAcceleratorFactory`** - For device discovery and accelerator creation
4. **Use `IComputeOrchestrator`** - For kernel execution with automatic backend selection
5. **Dispose properly** - Accelerators implement `IDisposable`
6. **Check device capabilities** - Not all devices support all operations
7. **No manual registration needed** - Everything is configured automatically

## Additional Resources

- [Getting Started Guide](../getting-started.md)
- [Kernel Development Guide](../guides/kernel-development.md)
- [Dependency Injection Guide](../guides/dependency-injection.md)
- [API Reference](../../api/index.md)
