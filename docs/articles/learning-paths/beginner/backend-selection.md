# Backend Selection

This module covers DotCompute's backend system and how to target specific hardware for optimal performance.

## Available Backends

DotCompute supports multiple compute backends:

| Backend | Vendor | Status | Use Case |
|---------|--------|--------|----------|
| **CUDA** | NVIDIA | ✅ Production | Production GPU computing |
| **CPU** | All | ✅ Production | Development, fallback, SIMD optimization |
| **Metal** | Apple | ⚠️ Experimental | macOS/iOS applications |
| **OpenCL** | Cross-vendor | ⚠️ Experimental | AMD, Intel, cross-platform |

## Automatic Backend Selection

By default, DotCompute selects the best available backend automatically:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Factories;

// Build host with DotCompute services
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();  // Registers all services
var app = host.Build();

// Get orchestrator - handles automatic backend selection
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Execute kernel (orchestrator selects best backend)
await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { a, b, result }
);
```

**Selection priority:**
1. CUDA (if NVIDIA GPU available)
2. Metal (if Apple Silicon available)
3. OpenCL (if supported GPU available)
4. CPU (always available)

## Device Discovery

Use `IUnifiedAcceleratorFactory` to enumerate available devices:

```csharp
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();

// Get all available devices
var devices = await factory.GetAvailableDevicesAsync();

Console.WriteLine($"Found {devices.Count} device(s):");
foreach (var device in devices)
{
    Console.WriteLine($"  - {device.Name} ({device.DeviceType})");
    Console.WriteLine($"    Memory: {device.TotalMemory / (1024.0 * 1024 * 1024):F2} GB");
    Console.WriteLine($"    Compute Units: {device.MaxComputeUnits}");
    Console.WriteLine();
}
```

## Manual Backend Selection

### Specify Preferred Backend

You can request a specific backend when executing kernels:

```csharp
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

// Request CUDA backend explicitly
await orchestrator.ExecuteAsync<object>(
    "VectorAdd",
    "CUDA",  // Preferred backend
    a, b, result
);

// Request CPU backend (useful for debugging)
await orchestrator.ExecuteAsync<object>(
    "VectorAdd",
    "CPU",
    a, b, result
);
```

### Create Accelerator for Specific Device

For more control, create an accelerator for a specific device:

```csharp
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

// Find CUDA device
var cudaDevice = devices.FirstOrDefault(d => d.DeviceType == "CUDA");

if (cudaDevice != null)
{
    // Create accelerator for this specific device
    using var accelerator = await factory.CreateAsync(cudaDevice);
    Console.WriteLine($"Using: {cudaDevice.Name}");

    // Execute with specific accelerator
    await orchestrator.ExecuteAsync<object>(
        "VectorAdd",
        accelerator,
        a, b, result
    );
}
```

### Backend Fallback Chain

Implement your own fallback logic:

```csharp
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

// Priority order: CUDA > OpenCL > CPU
var device = devices.FirstOrDefault(d => d.DeviceType == "CUDA")
          ?? devices.FirstOrDefault(d => d.DeviceType == "OpenCL")
          ?? devices.First();  // CPU is always available

Console.WriteLine($"Selected device: {device.Name} ({device.DeviceType})");

using var accelerator = await factory.CreateAsync(device);
```

## Querying Device Capabilities

```csharp
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

foreach (var device in devices)
{
    Console.WriteLine($"Device: {device.Name}");
    Console.WriteLine($"  Type: {device.DeviceType}");
    Console.WriteLine($"  Memory: {device.TotalMemory / (1024*1024*1024.0):F1} GB");
    Console.WriteLine($"  Compute Units: {device.MaxComputeUnits}");
    Console.WriteLine();
}
```

## Troubleshooting

### CUDA Not Detected

```bash
# Check NVIDIA driver
nvidia-smi

# Check CUDA toolkit
nvcc --version

# WSL2: Set library path (CRITICAL for Windows developers)
export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"
```

### OpenCL Not Detected

```bash
# List OpenCL platforms (Linux)
clinfo

# Install OpenCL runtime (Intel)
sudo apt install intel-opencl-icd
```

### Metal Not Available

Metal requires macOS 10.14+ or iOS 12+. Verify in code:

```csharp
if (OperatingSystem.IsMacOS())
{
    // Metal should be available
}
```

## Performance Comparison

Typical performance relative to CPU baseline:

| Operation | CPU (SIMD) | CUDA | Metal | OpenCL |
|-----------|------------|------|-------|--------|
| VectorAdd 1M | 3.7x | 92x | 75x | 60x |
| MatMul 1K×1K | 4x | 85x | 70x | 55x |
| Reduction 10M | 3x | 45x | 40x | 35x |

*Note: Actual performance depends on hardware and workload.*

## Exercises

### Exercise 1: Backend Enumeration

List all available backends and their properties on your system.

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Abstractions.Factories;

var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

foreach (var device in devices)
{
    Console.WriteLine($"{device.DeviceType}: {device.Name}");
    Console.WriteLine($"  Memory: {device.TotalMemory / 1e9:F2} GB");
}
```

### Exercise 2: Performance Comparison

Run the same kernel on each available backend and compare execution times.

### Exercise 3: Fallback Chain

Implement a solution that uses CUDA if available, falls back to CPU otherwise.

## Key Takeaways

1. **DotCompute auto-selects** the best available backend
2. **Use `IComputeOrchestrator`** for kernel execution with automatic selection
3. **Use `IUnifiedAcceleratorFactory`** for device discovery
4. **CUDA provides highest performance** on NVIDIA hardware (Production)
5. **CPU backend is always available** for development and fallback (Production)
6. **Metal and OpenCL are experimental** - use for testing only

## Path Complete

Congratulations! You've completed the Beginner Learning Path.

**What you learned:**
- GPU computing fundamentals
- Writing kernels with `[Kernel]` attribute
- Memory management and transfers
- Backend selection and configuration

**Next steps:**
- [Intermediate Path](../intermediate/index.md) - Memory optimization and performance
- [Ring Kernels Guide](../../guides/ring-kernels/index.md) - Persistent GPU computation
- [Examples](../../examples/index.md) - Real-world code samples
