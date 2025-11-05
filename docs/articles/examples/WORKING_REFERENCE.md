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

## ⚠️ IMPORTANT: AddDotComputeRuntime() Namespace Conflict

**CRITICAL ISSUE**: There are TWO different `AddDotComputeRuntime()` methods in DotCompute v0.4.0-rc2:

1. `DotCompute.Runtime.AddDotComputeRuntime()` - Registers `IUnifiedAcceleratorFactory` only
2. `DotCompute.Runtime.Extensions.AddDotComputeRuntime()` - Registers `IComputeOrchestrator` only

**Neither registers both services!** This is a known API design issue that will be fixed in a future release.

## ✅ RECOMMENDED PATTERN: Manual Service Registration

The **most reliable** approach is manual registration:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;

// Using Host builder
var host = Host.CreateApplicationBuilder(args);

// Add logging (optional but recommended)
host.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Configure DotCompute runtime options
host.Services.Configure<DotComputeRuntimeOptions>(options =>
{
    options.ValidateCapabilities = false; // Set to true for strict validation
    options.AcceleratorLifetime = ServiceLifetime.Transient;
});

// Manually register the accelerator factory (this works reliably)
host.Services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

var app = host.Build();

// Get factory - this works!
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

**IMPORTANT**: `IComputeOrchestrator` requires additional setup beyond just the factory. For most use cases, direct accelerator usage is simpler:

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

**For IComputeOrchestrator usage** (requires `DotCompute.Runtime.Extensions` namespace):

```csharp
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Runtime.Extensions; // ⚠️ Required for orchestrator registration

// Additional registration needed
host.Services.AddDotComputeRuntime(); // From Extensions namespace

var app = host.Build();
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

## ❌ INCORRECT Patterns (Do Not Use)

### ❌ Wrong: Using AddDotComputeRuntime() expecting both services
```csharp
using DotCompute.Runtime; // ❌ WRONG - only registers IUnifiedAcceleratorFactory

host.Services.AddDotComputeRuntime();
var app = host.Build();

// This works:
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>(); // ✅

// This FAILS:
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>(); // ❌ Not registered!
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

## ✅ Complete Working Example (Manual Registration - RECOMMENDED)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Build host with manual DotCompute service registration
        var host = Host.CreateApplicationBuilder(args);

        host.Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Configure DotCompute runtime options
        host.Services.Configure<DotComputeRuntimeOptions>(options =>
        {
            options.ValidateCapabilities = false; // Set to true for strict validation
            options.AcceleratorLifetime = ServiceLifetime.Transient;
        });

        // Manually register accelerator factory (RELIABLE PATTERN)
        host.Services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

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
