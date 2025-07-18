# DotCompute CUDA Backend

A high-performance CUDA backend for DotCompute, providing GPU acceleration for compute operations on NVIDIA GPUs.

## Features

- **Full CUDA Support**: Complete integration with CUDA runtime and driver APIs
- **Memory Management**: Efficient GPU memory allocation, transfer, and management
- **Kernel Compilation**: Dynamic PTX compilation with caching support
- **Stream Support**: Asynchronous execution with CUDA streams
- **Multi-GPU**: Support for multiple CUDA devices
- **Error Handling**: Comprehensive CUDA error checking and reporting
- **Performance Monitoring**: Device capability queries and memory statistics

## Requirements

- NVIDIA GPU with CUDA Compute Capability 5.0 or higher
- CUDA Toolkit 11.0 or later
- NVIDIA GPU drivers
- .NET 8.0 or later

## Architecture

### Core Components

1. **CudaAccelerator**: Main accelerator implementation
   - Device management and synchronization
   - Capability detection and reporting
   - Context lifecycle management

2. **CudaMemoryManager**: GPU memory operations
   - Device memory allocation/deallocation
   - Host-to-device and device-to-host transfers
   - Memory filling and copying operations
   - Memory statistics tracking

3. **CudaKernelCompiler**: CUDA kernel compilation
   - PTX generation from CUDA C source
   - Compilation caching for performance
   - Support for various optimization levels
   - OpenCL to CUDA translation (basic)

4. **CudaContext**: CUDA context management
   - Primary context handling
   - Stream creation and management
   - Thread-safe context switching

5. **Native Interop**: P/Invoke wrappers
   - Complete CUDA runtime API coverage
   - CUDA driver API for advanced features
   - Error handling and string conversion

## Usage Example

```csharp
// Create CUDA backend
var factory = new CudaBackendFactory();
using var accelerator = factory.CreateDefaultAccelerator();

// Allocate GPU memory
var memoryManager = accelerator.MemoryManager;
using var buffer = memoryManager.Allocate(1024 * sizeof(float));

// Compile a kernel
var kernel = new KernelSource
{
    Name = "vector_add",
    Language = KernelLanguage.Cuda,
    EntryPoint = "vector_add",
    Code = @"
        extern ""C"" __global__ void vector_add(float* a, float* b, float* c, int n) {
            int idx = blockIdx.x * blockDim.x + threadIdx.x;
            if (idx < n) {
                c[idx] = a[idx] + b[idx];
            }
        }"
};

var compiledKernel = await accelerator.KernelCompiler.CompileAsync(kernel);

// Execute kernel
var execution = compiledKernel.CreateExecution(new ExecutionConfiguration
{
    GlobalWorkSize = new[] { 1024 },
    LocalWorkSize = new[] { 256 }
});

execution.SetArgument(0, bufferA)
         .SetArgument(1, bufferB)
         .SetArgument(2, bufferC)
         .SetArgument(3, 1024)
         .Execute();
```

## Performance Considerations

1. **Memory Transfers**: Minimize host-device transfers for optimal performance
2. **Kernel Occupancy**: Use appropriate block sizes for your GPU
3. **Compilation Cache**: Reuse compiled kernels when possible
4. **Async Execution**: Use streams for overlapping computation and transfers
5. **Multi-GPU**: Distribute work across available GPUs

## Error Handling

The CUDA backend provides comprehensive error handling:

```csharp
try 
{
    // CUDA operations
}
catch (CudaException ex)
{
    Console.WriteLine($"CUDA Error {ex.ErrorCode}: {ex.Message}");
}
catch (AcceleratorException ex)
{
    Console.WriteLine($"Accelerator Error: {ex.Message}");
}
```

## Testing

Run the included test suite:

```bash
dotnet run --project CudaBackendTests.cs
```

This will verify:
- CUDA availability
- Memory operations
- Kernel compilation
- Kernel execution
- Multi-device support

## Limitations

1. Requires NVIDIA GPU hardware
2. PTX compilation requires nvcc (CUDA Toolkit)
3. No direct host memory access (must use copy operations)
4. OpenCL to CUDA translation is basic

## Future Enhancements

- [ ] NVRTC integration for runtime compilation
- [ ] Unified memory support
- [ ] CUDA graphs for complex workflows
- [ ] Tensor Core support
- [ ] Advanced profiling integration
- [ ] cuDNN and cuBLAS integration