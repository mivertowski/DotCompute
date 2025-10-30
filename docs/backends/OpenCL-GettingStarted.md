# Getting Started with OpenCL Backend

The OpenCL backend provides cross-vendor GPU and CPU acceleration for DotCompute, supporting NVIDIA, AMD, Intel, and other OpenCL-compatible devices.

## Table of Contents

- [Overview](#overview)
- [System Requirements](#system-requirements)
- [Installation](#installation)
- [Device Selection](#device-selection)
- [Basic Usage](#basic-usage)
- [Configuration](#configuration)
- [Platform Detection](#platform-detection)
- [Troubleshooting](#troubleshooting)

## Overview

The OpenCL backend enables compute acceleration across multiple vendors:

- **Cross-vendor support**: Works with NVIDIA, AMD, Intel GPUs and CPUs
- **Automatic optimization**: Vendor-specific optimizations for best performance
- **Memory pooling**: 90%+ reduction in allocation overhead
- **Compilation caching**: Multi-tier caching (memory + disk)
- **Event-based profiling**: Hardware counter integration
- **Production-ready**: Thread-safe, async-first design

## System Requirements

### Minimum Requirements

- **.NET 9.0 SDK** or later
- **OpenCL 1.2+** runtime installed
- **Compatible device** (GPU or CPU with OpenCL support)

### Supported Platforms

| Vendor | Devices | Minimum Version |
|--------|---------|----------------|
| **NVIDIA** | GeForce, Quadro, Tesla | OpenCL 1.2+ |
| **AMD** | Radeon, FirePro, Instinct | OpenCL 2.0+ |
| **Intel** | Integrated Graphics, Arc | OpenCL 2.1+ |
| **Apple** | Metal via OpenCL | OpenCL 1.2+ |

### Runtime Installation

#### Windows
```bash
# NVIDIA: Install CUDA Toolkit (includes OpenCL)
# AMD: Install Radeon Software Adrenalin
# Intel: Install Intel Graphics Driver
```

#### Linux
```bash
# NVIDIA
sudo apt install nvidia-opencl-dev

# AMD
sudo apt install rocm-opencl-dev

# Intel
sudo apt install intel-opencl-icd
```

#### macOS
```bash
# OpenCL included in macOS (deprecated in favor of Metal)
# Use Metal backend for macOS development
```

## Installation

### Package Installation

```bash
# Add OpenCL backend package
dotnet add package DotCompute.Backends.OpenCL

# Add core packages if not already installed
dotnet add package DotCompute.Core
dotnet add package DotCompute.Runtime
```

### Service Registration

```csharp
using DotCompute.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Register DotCompute with OpenCL backend
builder.Services.AddDotComputeRuntime(options =>
{
    options.EnableOpenCL = true;
    options.PreferGPU = true; // Prioritize GPU devices
});

var host = builder.Build();
```

## Device Selection

### Automatic Selection (Recommended)

The OpenCL backend automatically selects the best device:

```csharp
using DotCompute.Backends.OpenCL;
using Microsoft.Extensions.Logging;

// Create accelerator with automatic device selection
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var accelerator = new OpenCLAccelerator(loggerFactory);

await accelerator.InitializeAsync();

Console.WriteLine($"Selected: {accelerator.Name}");
Console.WriteLine($"Type: {accelerator.DeviceType}");
Console.WriteLine($"Memory: {accelerator.Info.TotalMemory / (1024 * 1024)} MB");
```

### Manual Device Selection

For fine-grained control, manually select a device:

```csharp
using DotCompute.Backends.OpenCL.DeviceManagement;

var deviceManager = new OpenCLDeviceManager(
    loggerFactory.CreateLogger<OpenCLDeviceManager>());

// List all available devices
foreach (var platform in deviceManager.Platforms)
{
    Console.WriteLine($"Platform: {platform.Name} ({platform.Vendor})");

    foreach (var device in platform.AvailableDevices)
    {
        Console.WriteLine($"  Device: {device.Name}");
        Console.WriteLine($"    Type: {device.Type}");
        Console.WriteLine($"    Compute Units: {device.MaxComputeUnits}");
        Console.WriteLine($"    Memory: {device.GlobalMemorySize / (1024 * 1024)} MB");
        Console.WriteLine($"    Max Clock: {device.MaxClockFrequency} MHz");
    }
}

// Select specific device
var gpuDevice = deviceManager.GetDevices(DeviceType.GPU).FirstOrDefault();
if (gpuDevice != null)
{
    var accelerator = new OpenCLAccelerator(
        gpuDevice,
        loggerFactory);
    await accelerator.InitializeAsync();
}
```

### Device Filtering

```csharp
// Get all GPU devices
var gpus = deviceManager.GetDevices(DeviceType.GPU);

// Get NVIDIA devices
var nvidiaDevices = deviceManager.GetDevicesByVendor("NVIDIA");

// Get devices with at least 4GB memory
var largeMemoryDevices = deviceManager.AllDevices
    .Where(d => d.GlobalMemorySize >= 4L * 1024 * 1024 * 1024);

// Get best device for compute
var bestDevice = deviceManager.GetBestDevice();
```

## Basic Usage

### Define and Execute Kernel

```csharp
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Memory;

// Define OpenCL kernel
var kernelSource = @"
__kernel void VectorAdd(
    __global const float* a,
    __global const float* b,
    __global float* result,
    const int length)
{
    int idx = get_global_id(0);
    if (idx < length)
    {
        result[idx] = a[idx] + b[idx];
    }
}";

var definition = new KernelDefinition
{
    Name = "VectorAdd",
    Source = kernelSource,
    EntryPoint = "VectorAdd"
};

// Compile kernel
var compiledKernel = await accelerator.CompileKernelAsync(definition);

// Allocate memory
var length = 1000;
var inputA = await accelerator.AllocateAsync<float>((nuint)length);
var inputB = await accelerator.AllocateAsync<float>((nuint)length);
var output = await accelerator.AllocateAsync<float>((nuint)length);

// Fill input data
var dataA = Enumerable.Range(0, length).Select(i => (float)i).ToArray();
var dataB = Enumerable.Range(0, length).Select(i => (float)i * 2).ToArray();

await inputA.CopyFromAsync(dataA);
await inputB.CopyFromAsync(dataB);

// Execute kernel
var execution = accelerator.CreateExecution(compiledKernel)
    .WithArgument(inputA)
    .WithArgument(inputB)
    .WithArgument(output)
    .WithArgument(length)
    .WithGlobalWorkSize(length);

await execution.ExecuteAsync();

// Get results
var results = new float[length];
await output.CopyToAsync(results);

// Verify results
for (int i = 0; i < 10; i++)
{
    Console.WriteLine($"results[{i}] = {results[i]} (expected {dataA[i] + dataB[i]})");
}
```

### Using [Kernel] Attribute (Modern Approach)

```csharp
using DotCompute.Generators;

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

// Execute via orchestrator
var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();

var a = new float[1000];
var b = new float[1000];
var result = new float[1000];

// Initialize data...

await orchestrator.ExecuteKernelAsync(
    "Kernels.VectorAdd",
    a, b, result);
```

## Configuration

### Basic Configuration

```csharp
using DotCompute.Backends.OpenCL.Configuration;

var config = new OpenCLConfiguration
{
    // Stream (command queue) configuration
    Stream = new StreamConfiguration
    {
        MinimumQueuePoolSize = 2,
        MaximumQueuePoolSize = 8,
        EnableOutOfOrderExecution = true
    },

    // Event pooling configuration
    Event = new EventConfiguration
    {
        MinimumEventPoolSize = 10,
        MaximumEventPoolSize = 100,
        EnableProfiling = true
    },

    // Memory configuration
    Memory = new MemoryConfiguration
    {
        EnablePooling = true,
        InitialPoolSize = 16 * 1024 * 1024, // 16 MB
        MaxPoolSize = 512 * 1024 * 1024 // 512 MB
    }
};

var accelerator = new OpenCLAccelerator(loggerFactory, config);
```

### Vendor-Specific Optimizations

```csharp
using DotCompute.Backends.OpenCL.Vendor;

// Vendor adapter is automatically selected based on device
// Access via accelerator property
var vendorAdapter = accelerator.VendorAdapter;

Console.WriteLine($"Vendor: {vendorAdapter.VendorName}");

// Get vendor-specific compiler options
var compilerOptions = vendorAdapter.GetCompilerOptions(enableOptimizations: true);

// Apply vendor-specific optimizations to device info
var optimizedWorkGroupSize = vendorAdapter.GetOptimalWorkGroupSize(deviceInfo, kernelInfo);
```

### Compilation Options

```csharp
using DotCompute.Backends.OpenCL.Compilation;

var compilationOptions = new CompilationOptions
{
    OptimizationLevel = 3, // 0-3, higher is more aggressive
    EnableFastMath = true,
    EnableMadEnable = true, // Multiply-add optimization
    EnableDebugInfo = false, // Disable for production
    AdditionalFlags = new[] { "-cl-finite-math-only" }
};

var kernel = await compiler.CompileAsync(
    kernelSource,
    "VectorAdd",
    compilationOptions);
```

## Platform Detection

### Check OpenCL Availability

```csharp
using DotCompute.Backends.OpenCL.DeviceManagement;

var deviceManager = new OpenCLDeviceManager(logger);

if (!deviceManager.IsOpenCLAvailable)
{
    Console.WriteLine("OpenCL not available on this system");
    return;
}

Console.WriteLine($"Found {deviceManager.Platforms.Count} OpenCL platforms");
Console.WriteLine($"Total devices: {deviceManager.AllDevices.Count()}");

// Check for specific vendor
var hasNvidia = deviceManager.GetDevicesByVendor("NVIDIA").Any();
var hasAMD = deviceManager.GetDevicesByVendor("AMD").Any();
var hasIntel = deviceManager.GetDevicesByVendor("Intel").Any();

Console.WriteLine($"NVIDIA: {hasNvidia}");
Console.WriteLine($"AMD: {hasAMD}");
Console.WriteLine($"Intel: {hasIntel}");
```

### Platform Capabilities

```csharp
foreach (var platform in deviceManager.Platforms)
{
    Console.WriteLine($"\nPlatform: {platform.Name}");
    Console.WriteLine($"  Vendor: {platform.Vendor}");
    Console.WriteLine($"  Version: {platform.Version}");
    Console.WriteLine($"  Profile: {platform.Profile}");

    // Check for extensions
    var extensions = platform.Extensions.Split(' ');
    Console.WriteLine($"  Extensions: {extensions.Length}");

    if (extensions.Contains("cl_khr_fp64"))
        Console.WriteLine("    ✓ Double precision floating-point");
    if (extensions.Contains("cl_khr_int64_base_atomics"))
        Console.WriteLine("    ✓ 64-bit atomic operations");
    if (extensions.Contains("cl_khr_local_int32_base_atomics"))
        Console.WriteLine("    ✓ Local memory atomic operations");
}
```

## Troubleshooting

### Common Issues

#### 1. "No OpenCL platforms found"

**Cause**: OpenCL runtime not installed

**Solution**:
```bash
# Install vendor-specific OpenCL runtime
# NVIDIA: CUDA Toolkit
# AMD: ROCm or Radeon Software
# Intel: Intel OpenCL Runtime
```

#### 2. "Device kernel image is invalid"

**Cause**: Incompatible binary format or OpenCL version mismatch

**Solution**:
```csharp
// Clear compilation cache
var cache = OpenCLCompilationCache.Instance;
cache.Clear();

// Recompile with explicit options
var options = new CompilationOptions
{
    OptimizationLevel = 0, // Start with no optimization
    EnableDebugInfo = true
};
```

#### 3. "Out of resources" during kernel execution

**Cause**: Kernel using too much local memory or register pressure

**Solution**:
```csharp
// Check resource limits
var device = accelerator.DeviceInfo;
Console.WriteLine($"Local memory: {device.LocalMemorySize / 1024} KB");
Console.WriteLine($"Max work group size: {device.MaxWorkGroupSize}");

// Reduce work group size
execution.WithLocalWorkSize(128); // Instead of 256 or 512
```

#### 4. Slow compilation times

**Cause**: Cache not persisting or large kernels

**Solution**:
```csharp
// Enable disk caching
var cache = OpenCLCompilationCache.Instance;
// Cache is automatically persisted to disk

// Pre-compile kernels at startup
await PrecompileKernelsAsync(accelerator);
```

### Debugging

#### Enable Detailed Logging

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Debug)
        .AddConsole()
        .AddFilter("DotCompute.Backends.OpenCL", LogLevel.Trace);
});
```

#### Inspect Compilation Logs

```csharp
try
{
    var kernel = await compiler.CompileAsync(source, "MyKernel", options);
}
catch (CompilationException ex)
{
    Console.WriteLine("Compilation failed:");
    Console.WriteLine(ex.BuildLog);
    Console.WriteLine("\nSuggestion:");
    Console.WriteLine(ex.Message);
}
```

#### Profile Kernel Execution

```csharp
using DotCompute.Backends.OpenCL.Profiling;

var profiler = new OpenCLProfiler(context, logger);

// Execute with profiling
var session = profiler.BeginSession("VectorAdd");
await execution.ExecuteAsync();
var result = profiler.EndSession(session);

Console.WriteLine($"Execution time: {result.ExecutionTime.TotalMilliseconds} ms");
Console.WriteLine($"Kernel time: {result.KernelTime.TotalMilliseconds} ms");
Console.WriteLine($"Memory transfer time: {result.MemoryTransferTime.TotalMilliseconds} ms");
```

## Next Steps

- [OpenCL Architecture Guide](OpenCL-Architecture.md) - Understand the internal design
- [OpenCL Performance Guide](OpenCL-Performance.md) - Optimize your kernels
- [OpenCL API Reference](OpenCL-API-Reference.md) - Complete API documentation
- [Backend Comparison](Backend-Comparison.md) - Compare OpenCL vs CUDA vs Metal
- [Sample Projects](../../samples/OpenCL/README.md) - Working examples

## Additional Resources

- [OpenCL Specification](https://www.khronos.org/opencl/)
- [OpenCL Programming Guide](https://www.khronos.org/opencl/resources)
- [Vendor Documentation](https://github.com/KhronosGroup/OpenCL-Docs)
