# DotCompute Metal Backend

High-performance GPU compute backend for macOS and iOS using Apple's Metal framework.

## Features

- **Native Metal Integration**: Direct access to Apple GPUs through Metal API
- **Unified Memory Architecture**: Optimal performance on Apple Silicon with zero-copy operations
- **Metal Performance Shaders**: Integration with MPS for optimized operations
- **GPU Family Optimization**: Specialized code paths for different Apple GPU families
- **Command Buffer Pooling**: Efficient command buffer management
- **Threadgroup Memory**: Optimized tiling for matrix operations
- **Asynchronous Execution**: Non-blocking kernel execution with completion handlers

## Supported Platforms

- macOS 10.14+ (Mojave and later)
- iOS 12.0+
- Mac Catalyst
- Apple Silicon (M1, M2, M3) with unified memory
- Intel Macs with discrete/integrated GPUs

## Installation

```xml
<PackageReference Include="DotCompute.Backends.Metal" Version="1.0.0" />
```

## Usage

### Basic Setup

```csharp
using DotCompute.Backends.Metal.Registration;

// Add Metal backend to services
services.AddMetalBackend();

// Or with configuration
services.AddMetalBackend(options =>
{
    options.MaxMemoryAllocation = 8L * 1024 * 1024 * 1024; // 8GB
    options.EnableMetalPerformanceShaders = true;
    options.EnableGpuFamilySpecialization = true;
});
```

### Device Selection

```csharp
// Prefer integrated GPU (better battery life)
services.AddMetalBackend(MetalDeviceSelector.PreferIntegrated);

// Prefer discrete GPU (maximum performance)
services.AddMetalBackend(MetalDeviceSelector.PreferDiscrete);
```

### Executing Kernels

```csharp
// Get the Metal accelerator
var accelerator = serviceProvider.GetRequiredService<IAccelerator>();

// Compile a kernel
var kernel = await accelerator.CompileKernelAsync(
    MetalOptimizedKernels.CreateMatrixMultiplyKernel(),
    new CompilationOptions
    {
        OptimizationLevel = OptimizationLevel.Maximum,
        EnableFastMath = true
    });

// Allocate GPU memory
var matrixA = accelerator.Memory.Allocate(sizeof(float) * 1024 * 1024);
var matrixB = accelerator.Memory.Allocate(sizeof(float) * 1024 * 1024);
var matrixC = accelerator.Memory.Allocate(sizeof(float) * 1024 * 1024);

// Execute kernel
await kernel.ExecuteAsync(new KernelExecutionContext
{
    GlobalWorkSize = new WorkSize(1024, 1024, 1),
    LocalWorkSize = new WorkSize(16, 16, 1),
    Arguments = new[]
    {
        new KernelArgument("matrixA", matrixA),
        new KernelArgument("matrixB", matrixB),
        new KernelArgument("matrixC", matrixC),
        new KernelArgument("M", 1024u),
        new KernelArgument("N", 1024u),
        new KernelArgument("K", 1024u)
    }
});
```

## Performance Optimization

### Memory Management

```csharp
// Use shared memory for Apple Silicon (zero-copy)
var sharedMemory = accelerator.Memory.Allocate(
    size, 
    MemoryHints.HostVisible | MemoryHints.HostCoherent);

// Use private memory for discrete GPUs
var privateMemory = accelerator.Memory.Allocate(
    size, 
    MemoryHints.DeviceLocal);
```

### Threadgroup Optimization

The backend automatically optimizes threadgroup sizes based on:
- GPU family (Apple7, Apple8, Mac2, etc.)
- Kernel characteristics
- Memory access patterns

### Metal Performance Shaders

Enable MPS for optimized operations:

```csharp
var kernel = MetalOptimizedKernels.CreateFFTKernel(1024);
// Automatically uses MPS for FFT operations
```

## Debugging

### Enable Metal Validation

```bash
export METAL_DEVICE_WRAPPER_TYPE=1
export METAL_DEBUG_ERROR_MODE=2
```

### GPU Frame Capture

Use Xcode's GPU Frame Debugger for detailed performance analysis.

## Native Interop

The Metal backend includes a native library (`libDotComputeMetal.dylib`) that provides:
- Direct Metal API access
- Objective-C++ bridge for Metal objects
- Optimized memory management
- Command buffer pooling

## Building from Source

### Prerequisites

- Xcode 14.0+
- .NET 9.0 SDK
- CMake 3.20+ (for native library)

### Build Steps

```bash
# Build native library
cd native
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make

# Build .NET library
cd ../..
dotnet build -c Release
```

## Limitations

- Only available on Apple platforms
- Double precision support varies by GPU
- Maximum buffer size limited by GPU memory
- Some operations require specific GPU families

## Troubleshooting

### Metal Not Available

```csharp
if (!MetalUtilities.IsMetalAvailable())
{
    // Fall back to CPU backend
    services.AddCpuBackend();
}
```

### Device Information

```csharp
var devices = MetalUtilities.GetAllDevices();
foreach (var device in devices)
{
    Console.WriteLine(device);
}
```

## License

Same as DotCompute project.