# Backend Selection

This module covers DotCompute's backend system and how to target specific hardware for optimal performance.

## Available Backends

DotCompute supports multiple compute backends:

| Backend | Vendor | Performance | Use Case |
|---------|--------|-------------|----------|
| **CUDA** | NVIDIA | Highest | Production GPU computing |
| **Metal** | Apple | High | macOS/iOS applications |
| **OpenCL** | Cross-vendor | Good | AMD, Intel, cross-platform |
| **CPU** | All | Baseline | Development, fallback |

## Automatic Backend Selection

By default, DotCompute selects the best available backend:

```csharp
var services = new ServiceCollection();
services.AddDotCompute(); // Automatic selection

var provider = services.BuildServiceProvider();
var computeService = provider.GetRequiredService<IComputeService>();

// Check which backend was selected
Console.WriteLine($"Active backend: {computeService.ActiveBackend.Name}");
```

**Selection priority:**
1. CUDA (if NVIDIA GPU available)
2. Metal (if Apple Silicon available)
3. OpenCL (if supported GPU available)
4. CPU (always available)

## Manual Backend Selection

### Specify Preferred Backend

```csharp
services.AddDotCompute(options =>
{
    options.PreferredBackend = BackendType.CUDA;
});
```

### Require Specific Backend

```csharp
services.AddDotCompute(options =>
{
    options.RequiredBackend = BackendType.CUDA;
    options.FailIfUnavailable = true; // Throw if CUDA not available
});
```

### Multiple Backends

Enable fallback chain:

```csharp
services.AddDotCompute(options =>
{
    options.BackendPriority = new[]
    {
        BackendType.CUDA,
        BackendType.OpenCL,
        BackendType.CPU
    };
});
```

## Backend-Specific Configuration

### CUDA Configuration

```csharp
services.AddDotCompute(options =>
{
    options.Cuda = new CudaOptions
    {
        DeviceId = 0,                    // GPU device index
        EnableP2P = true,                // Peer-to-peer for multi-GPU
        EnableUnifiedMemory = true,      // Managed memory
        ComputeCapability = "8.9",       // Target capability (auto-detect if null)
        MaxSharedMemoryPerBlock = 49152  // 48 KB
    };
});
```

### Metal Configuration

```csharp
services.AddDotCompute(options =>
{
    options.Metal = new MetalOptions
    {
        EnableBinaryCache = true,        // Cache compiled shaders
        PreferLowPower = false,          // Use discrete GPU if available
        EnableMPS = true                 // Metal Performance Shaders
    };
});
```

### OpenCL Configuration

```csharp
services.AddDotCompute(options =>
{
    options.OpenCL = new OpenCLOptions
    {
        PlatformId = 0,                  // OpenCL platform
        DeviceType = DeviceType.GPU,     // GPU, CPU, or Accelerator
        EnableProfiling = false          // Command queue profiling
    };
});
```

### CPU Configuration

```csharp
services.AddDotCompute(options =>
{
    options.Cpu = new CpuOptions
    {
        EnableSimd = true,               // AVX2/AVX512/NEON
        ThreadCount = Environment.ProcessorCount,
        EnableNuma = true                // NUMA-aware allocation
    };
});
```

## Querying Available Backends

```csharp
var computeService = provider.GetRequiredService<IComputeService>();

foreach (var backend in computeService.GetAvailableBackends())
{
    Console.WriteLine($"Backend: {backend.Name}");
    Console.WriteLine($"  Device: {backend.DeviceName}");
    Console.WriteLine($"  Memory: {backend.TotalMemory / (1024*1024*1024.0):F1} GB");
    Console.WriteLine($"  Compute Units: {backend.ComputeUnits}");
    Console.WriteLine($"  Max Work Group: {backend.MaxWorkGroupSize}");
    Console.WriteLine();
}
```

## Runtime Backend Switching

For advanced scenarios, switch backends at runtime:

```csharp
// Get specific backend
var cudaBackend = computeService.GetBackend(BackendType.CUDA);
var cpuBackend = computeService.GetBackend(BackendType.CPU);

// Execute on specific backend
await cudaBackend.ExecuteKernelAsync(kernel, config, buffers);

// Or use backend context
using (computeService.UseBackend(BackendType.CPU))
{
    // All operations in this scope use CPU backend
    await computeService.ExecuteKernelAsync(kernel, config, buffers);
}
```

## Backend Selection Strategies

### Strategy 1: Performance First

Use the fastest available GPU:

```csharp
services.AddDotCompute(options =>
{
    options.SelectionStrategy = BackendSelectionStrategy.Performance;
});
```

### Strategy 2: Power Efficient

Prefer integrated GPU or low-power devices:

```csharp
services.AddDotCompute(options =>
{
    options.SelectionStrategy = BackendSelectionStrategy.PowerEfficient;
    options.Metal.PreferLowPower = true;
});
```

### Strategy 3: Memory Capacity

Choose device with most available memory:

```csharp
services.AddDotCompute(options =>
{
    options.SelectionStrategy = BackendSelectionStrategy.MaxMemory;
});
```

## Cross-Platform Development

### Detect Platform Capabilities

```csharp
public class ComputeCapabilities
{
    public static void PrintCapabilities(IComputeService service)
    {
        var backend = service.ActiveBackend;

        Console.WriteLine("Platform Capabilities:");
        Console.WriteLine($"  Float64 Support: {backend.SupportsFloat64}");
        Console.WriteLine($"  Unified Memory: {backend.SupportsUnifiedMemory}");
        Console.WriteLine($"  Cooperative Launch: {backend.SupportsCooperativeLaunch}");
        Console.WriteLine($"  Dynamic Parallelism: {backend.SupportsDynamicParallelism}");
    }
}
```

### Conditional Feature Usage

```csharp
if (computeService.ActiveBackend.SupportsFloat64)
{
    // Use double precision
    await ExecuteDoublePrecisionKernel();
}
else
{
    // Fall back to single precision
    await ExecuteSinglePrecisionKernel();
}
```

## Troubleshooting

### CUDA Not Detected

```bash
# Check NVIDIA driver
nvidia-smi

# Check CUDA toolkit
nvcc --version

# WSL2: Set library path
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

### Exercise 2: Performance Comparison

Run the same kernel on each available backend and compare execution times.

### Exercise 3: Fallback Chain

Implement a solution that uses CUDA if available, falls back to CPU otherwise.

## Key Takeaways

1. **DotCompute auto-selects** the best available backend
2. **CUDA provides highest performance** on NVIDIA hardware
3. **CPU backend is always available** for development and fallback
4. **Query capabilities** to write portable code
5. **Configure backend-specific options** for optimal performance

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
