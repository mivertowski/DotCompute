# DotCompute.Backends.OpenCL

Cross-platform OpenCL compute backend for .NET 9+ with GPU and accelerator support.

## Status: 🚧 Foundation Complete, Integration In Progress

The OpenCL backend provides cross-platform GPU acceleration:
- **OpenCL Runtime Integration**: P/Invoke bindings to OpenCL C API
- **Device Management**: Platform and device enumeration
- **Context Management**: OpenCL context creation and lifecycle
- **Memory Management**: Device memory allocation and transfers
- **Kernel Compilation**: Runtime kernel compilation from OpenCL C
- **Plugin Architecture**: Integrated with DotCompute plugin system

## Key Components

### OpenCL Accelerator

#### OpenCLAccelerator
Main accelerator implementation providing:
- Device initialization and management
- Kernel compilation and execution
- Memory allocation and synchronization
- OpenCL context lifecycle management
- Error handling and diagnostics

### Device Management

#### OpenCLDeviceManager
Manages OpenCL devices:
- Platform enumeration
- Device discovery and selection
- Capability detection
- Device information queries
- Multi-device support

#### OpenCLDeviceInfo
Device information structure:
- Device name and vendor
- OpenCL version and driver version
- Memory sizes (global, local, constant)
- Compute capabilities (work group size, compute units)
- Image support and dimensions
- Device type (GPU, CPU, Accelerator)

#### OpenCLPlatformInfo
Platform information:
- Platform name and vendor
- OpenCL version support
- Available extensions
- Device count

### Context and Execution

#### OpenCLContext
OpenCL context wrapper:
- Context creation from devices
- Command queue management
- Resource lifecycle
- Error handling
- Synchronization primitives

### Memory Management

#### OpenCLMemoryManager
Unified memory manager for OpenCL:
- Device memory allocation
- Host-device memory transfers
- Buffer management
- Memory pooling support
- Synchronous and asynchronous operations

#### OpenCLMemoryBuffer
Buffer implementation:
- Device buffer allocation
- Read/write operations
- Zero-copy mapping when supported
- Rectangular buffer support
- Sub-buffer creation

### Kernel Management

#### OpenCLCompiledKernel
Compiled kernel representation:
- Kernel compilation from OpenCL C source
- Argument binding
- Execution with work dimensions
- Local memory specification
- Synchronous and asynchronous execution

### Factory

#### OpenCLAcceleratorFactory
Factory for creating OpenCL accelerators:
- Automatic device selection
- Configuration-based creation
- Workload profile matching
- Performance profile tuning

### Native Interop

#### OpenCLRuntime
P/Invoke bindings to OpenCL C API:
- Platform and device functions
- Context and queue functions
- Memory object functions
- Kernel functions
- Event and synchronization functions

#### OpenCLTypes
Native type definitions:
- Platform and device IDs
- Context and queue handles
- Memory object handles
- Kernel handles
- Error codes and status types

#### OpenCLException
Exception type for OpenCL errors:
- Error code mapping
- Human-readable error messages
- Stack trace preservation

## Installation

```bash
dotnet add package DotCompute.Backends.OpenCL --version 0.2.0-alpha
```

## Usage

### Basic Setup

```csharp
using DotCompute.Backends.OpenCL;
using Microsoft.Extensions.Logging;

var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<OpenCLAccelerator>();

// Create accelerator
var accelerator = new OpenCLAccelerator(logger, loggerFactory);

// Initialize with default device (first GPU or CPU)
await accelerator.InitializeAsync();

Console.WriteLine($"Using: {accelerator.Name}");
Console.WriteLine($"Global Memory: {accelerator.Info.TotalMemory / (1024*1024)} MB");
```

### Service Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register OpenCL backend
services.AddSingleton<IAccelerator, OpenCLAccelerator>();

// OR use plugin registration
services.AddDotComputeBackend("DotCompute.Backends.OpenCL");
```

### Device Selection

```csharp
using DotCompute.Backends.OpenCL.DeviceManagement;

var deviceManager = new OpenCLDeviceManager(logger);

// Enumerate all devices
var devices = await deviceManager.EnumerateDevicesAsync();

foreach (var device in devices)
{
    Console.WriteLine($"Device: {device.Name}");
    Console.WriteLine($"  Type: {device.DeviceType}");
    Console.WriteLine($"  Compute Units: {device.MaxComputeUnits}");
    Console.WriteLine($"  Global Memory: {device.GlobalMemorySize / (1024*1024)} MB");
    Console.WriteLine($"  Local Memory: {device.LocalMemorySize / 1024} KB");
}

// Select specific device
var selectedDevice = devices.FirstOrDefault(d => d.DeviceType == DeviceType.GPU);
if (selectedDevice != null)
{
    await accelerator.InitializeAsync(selectedDevice);
}
```

### Kernel Compilation and Execution

```csharp
using DotCompute.Abstractions.Kernels;

// Define OpenCL kernel
var kernelDef = new KernelDefinition
{
    Name = "VectorAdd",
    Source = @"
        __kernel void vector_add(
            __global const float* a,
            __global const float* b,
            __global float* result,
            const int length)
        {
            int gid = get_global_id(0);
            if (gid < length) {
                result[gid] = a[gid] + b[gid];
            }
        }
    ",
    EntryPoint = "vector_add"
};

// Compile kernel
var compiledKernel = await accelerator.CompileKernelAsync(kernelDef);

// Allocate device memory
var length = 1_000_000;
var bufferA = await accelerator.Memory.AllocateAsync<float>(length);
var bufferB = await accelerator.Memory.AllocateAsync<float>(length);
var bufferResult = await accelerator.Memory.AllocateAsync<float>(length);

// Copy data to device
var dataA = Enumerable.Range(0, length).Select(i => (float)i).ToArray();
var dataB = Enumerable.Range(0, length).Select(i => (float)(i * 2)).ToArray();

await bufferA.CopyFromAsync(dataA);
await bufferB.CopyFromAsync(dataB);

// Set kernel arguments and execute
var launchParams = new KernelLaunchParameters
{
    GlobalWorkSize = new[] { (uint)length },
    LocalWorkSize = new[] { 256u }
};

await compiledKernel.ExecuteAsync(new object[]
{
    bufferA,
    bufferB,
    bufferResult,
    length
}, launchParams);

// Read results back
var results = new float[length];
await bufferResult.CopyToAsync(results);

// Cleanup
await bufferA.DisposeAsync();
await bufferB.DisposeAsync();
await bufferResult.DisposeAsync();
```

### Memory Operations

```csharp
// Allocate buffer
var buffer = await accelerator.Memory.AllocateAsync<float>(10_000);

// Write to device
var hostData = new float[10_000];
await buffer.CopyFromAsync(hostData);

// Read from device
var resultData = new float[10_000];
await buffer.CopyToAsync(resultData);

// Map memory for zero-copy access (if supported)
if (accelerator.DeviceInfo?.SupportsHostMemoryMapping == true)
{
    var mappedPtr = await buffer.MapAsync(MapMode.ReadWrite);
    // Access memory directly...
    await buffer.UnmapAsync(mappedPtr);
}
```

### Using Factory

```csharp
using DotCompute.Backends.OpenCL.Factory;

var factory = new OpenCLAcceleratorFactory(configuration, logger);

// Create accelerator with performance profile
var accelerator = await factory.CreateAsync(new WorkloadProfile
{
    WorkloadType = WorkloadType.Compute,
    DataSize = DataSize.Large,
    MemoryIntensive = true
});
```

## Architecture

### Component Hierarchy

```
OpenCLAccelerator (IAccelerator)
    ├── OpenCLContext (Context management)
    ├── OpenCLDeviceManager (Device discovery)
    ├── OpenCLMemoryManager (Memory operations)
    └── OpenCLCompiledKernel (Kernel execution)

Native Layer:
    ├── OpenCLRuntime (P/Invoke bindings)
    ├── OpenCLTypes (Native type definitions)
    └── OpenCLException (Error handling)
```

### Initialization Flow

1. **Platform Enumeration**: Detect all OpenCL platforms
2. **Device Discovery**: Find devices on each platform
3. **Device Selection**: Choose appropriate device
4. **Context Creation**: Create OpenCL context for device
5. **Queue Creation**: Create command queue for execution
6. **Memory Manager**: Initialize memory management
7. **Ready**: Accelerator ready for kernel execution

### Kernel Execution Flow

1. **Kernel Compilation**: Compile OpenCL C to device binary
2. **Argument Binding**: Bind buffers and scalar arguments
3. **Work Sizing**: Calculate global and local work sizes
4. **Enqueue**: Enqueue kernel for execution
5. **Synchronize**: Wait for completion (if synchronous)
6. **Result Retrieval**: Copy results back to host

## Supported Platforms

### Operating Systems
- **Windows**: 10, 11, Server 2019+
- **Linux**: Most distributions with OpenCL runtime
- **macOS**: 10.13+ (deprecated by Apple, prefer Metal backend)

### OpenCL Versions
- **OpenCL 1.2**: Minimum supported version
- **OpenCL 2.0**: Full feature support
- **OpenCL 2.1/2.2/3.0**: Enhanced features when available

### Device Types

#### GPU Devices
- **NVIDIA**: GeForce, Quadro, Tesla (via NVIDIA OpenCL runtime)
- **AMD**: Radeon, FirePro, Instinct (via AMD OpenCL or ROCm)
- **Intel**: Iris, Arc Graphics (via Intel OpenCL runtime)
- **ARM Mali**: Mobile and embedded GPUs
- **Qualcomm Adreno**: Mobile GPUs

#### CPU Devices
- **Intel**: via Intel OpenCL CPU runtime
- **AMD**: via AMD OpenCL CPU runtime
- **ARM**: via ARM Compute Library

#### Accelerator Devices
- **FPGA**: Intel/Xilinx FPGA with OpenCL support
- **DSP**: Specialized signal processing accelerators

## System Requirements

### Minimum
- .NET 9.0 or later
- OpenCL 1.2 compatible device
- OpenCL runtime installed

### Recommended
- OpenCL 2.0+ compatible device
- 4GB+ device memory
- Latest vendor drivers

### Installing OpenCL Runtime

#### Windows
- **NVIDIA**: Install CUDA Toolkit or NVIDIA drivers
- **AMD**: Install AMD Radeon Software
- **Intel**: Install Intel Graphics drivers

#### Linux
- **NVIDIA**: Install CUDA Toolkit or nvidia-opencl-icd
- **AMD**: Install ROCm or amdgpu-pro drivers
- **Intel**: Install intel-opencl-icd or beignet

```bash
# Ubuntu/Debian
sudo apt-get install ocl-icd-opencl-dev nvidia-opencl-icd

# Fedora/RHEL
sudo dnf install ocl-icd-devel pocl

# Verify installation
clinfo
```

#### macOS
OpenCL is deprecated on macOS. Use Metal backend for macOS devices.

## Configuration

### Environment Variables

```bash
# Enable OpenCL debugging
export DOTCOMPUTE_OPENCL_DEBUG=1

# Select specific platform
export DOTCOMPUTE_OPENCL_PLATFORM=0

# Select specific device
export DOTCOMPUTE_OPENCL_DEVICE=0

# Force CPU device (for debugging)
export DOTCOMPUTE_OPENCL_FORCE_CPU=1
```

### Configuration Options

```csharp
var options = new OpenCLOptions
{
    PreferredDeviceType = DeviceType.GPU,
    EnableProfiling = true,
    EnableOutOfOrderExecution = false,
    BuildOptions = "-cl-fast-relaxed-math -cl-mad-enable",
    CacheKernels = true
};
```

## Current Limitations

1. **Image Processing**: Limited support for image objects
2. **Shared Virtual Memory**: SVM support not implemented
3. **Device Partitioning**: Sub-device creation not supported
4. **Pipes**: OpenCL 2.0 pipes not implemented
5. **Testing**: Limited hardware testing on diverse OpenCL implementations

## Troubleshooting

### Device Not Found

1. **Verify Runtime**: Run `clinfo` to list OpenCL platforms and devices
2. **Check Drivers**: Ensure latest GPU drivers installed
3. **Permissions**: On Linux, ensure user in `video` group
4. **Platform Selection**: Try different OpenCL platforms

### Compilation Failures

1. **Kernel Syntax**: Validate OpenCL C syntax
2. **Build Options**: Check build options compatibility
3. **Extensions**: Verify required extensions available
4. **Device Capabilities**: Check device limits (work group size, etc.)

### Performance Issues

1. **Work Group Size**: Optimize local work size for device
2. **Memory Access**: Ensure coalesced memory access patterns
3. **Transfer Overhead**: Minimize host-device transfers
4. **Kernel Complexity**: Profile kernel execution time

### Debug Tools

```csharp
// Enable detailed logging
var logger = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Get device capabilities
var info = accelerator.DeviceInfo;
Console.WriteLine($"Max Work Group Size: {info.MaxWorkGroupSize}");
Console.WriteLine($"Max Compute Units: {info.MaxComputeUnits}");
Console.WriteLine($"Extensions: {string.Join(", ", info.Extensions)}");

// Profile kernel execution
var sw = Stopwatch.StartNew();
await kernel.ExecuteAsync(args, launchParams);
await accelerator.SynchronizeAsync();
sw.Stop();
Console.WriteLine($"Kernel time: {sw.ElapsedMilliseconds}ms");
```

## Advanced Features

### Multi-Device Execution

```csharp
// Create accelerator for each device
var accelerators = new List<OpenCLAccelerator>();
foreach (var device in devices)
{
    var acc = new OpenCLAccelerator(logger, loggerFactory);
    await acc.InitializeAsync(device);
    accelerators.Add(acc);
}

// Distribute work across devices
var tasks = accelerators.Select(acc =>
    acc.CompileKernelAsync(kernelDef)
       .ContinueWith(t => t.Result.ExecuteAsync(args))
);

await Task.WhenAll(tasks);
```

### Custom Build Options

```csharp
var options = new CompilationOptions
{
    OptimizationLevel = OptimizationLevel.O3,
    CustomOptions = new[]
    {
        "-cl-mad-enable",           // Mad operations
        "-cl-fast-relaxed-math",    // Fast math
        "-cl-finite-math-only",     // No INF/NaN
        "-cl-unsafe-math-optimizations"
    }
};

var kernel = await accelerator.CompileKernelAsync(definition, options);
```

## Dependencies

- **DotCompute.Core**: Core runtime components
- **DotCompute.Abstractions**: Interface definitions
- **DotCompute.Plugins**: Plugin system integration
- **System.Runtime.InteropServices**: P/Invoke support
- **Polly**: Resilience and retry policies
- **Microsoft.Extensions.Logging**: Logging infrastructure

## Future Enhancements

1. **OpenCL 2.0+ Features**: SVM, pipes, device-side enqueue
2. **Image Support**: Comprehensive image object operations
3. **SPIR-V**: Support for SPIR-V kernels
4. **Sub-Devices**: Device partitioning support
5. **Interoperability**: OpenGL/DirectX interop
6. **Performance**: Further optimization and tuning

## Contributing

Contributions are welcome, particularly in:
- Testing on diverse OpenCL implementations
- Platform-specific optimizations
- Additional OpenCL feature support
- Performance benchmarking
- Documentation improvements

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for guidelines.

## References

- [OpenCL Specification](https://www.khronos.org/opencl/)
- [OpenCL Programming Guide](https://www.khronos.org/files/opencl30-reference-guide.pdf)
- [clinfo Tool](https://github.com/Oblomov/clinfo)

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
