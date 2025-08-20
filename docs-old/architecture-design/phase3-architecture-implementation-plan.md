# DotCompute Phase 3 Implementation Plan

## Implementation Order

1. **Plugin System Core** (Foundation)
2. **Source Generators** (Build-time infrastructure)
3. **CUDA Backend** (Primary GPU target)
4. **Metal Backend** (macOS/iOS GPU target)

## 1. Plugin System Implementation

### Core Interfaces

```csharp
// src/DotCompute.Plugins/Interfaces/IPlugin.cs
namespace DotCompute.Plugins
{
    public interface IPlugin
    {
        string Id { get; }
        string Name { get; }
        Version Version { get; }
        IPluginMetadata Metadata { get; }
        
        Task InitializeAsync(IPluginContext context);
        Task ShutdownAsync();
    }

    public interface IPluginContext
    {
        IServiceProvider Services { get; }
        IConfiguration Configuration { get; }
        ILogger Logger { get; }
        string PluginDirectory { get; }
    }

    public interface IPluginMetadata
    {
        string Author { get; }
        string Description { get; }
        string[] Tags { get; }
        Uri ProjectUrl { get; }
        PluginDependency[] Dependencies { get; }
    }

    public interface IBackendPlugin : IPlugin
    {
        BackendInfo BackendInfo { get; }
        IAccelerator CreateAccelerator(AcceleratorConfiguration config);
        Task<bool> IsAvailableAsync();
        Task<BackendCapabilities> GetCapabilitiesAsync();
    }
}
```

### Plugin Discovery Implementation

```csharp
// src/DotCompute.Plugins/Discovery/PluginDiscoverer.cs
namespace DotCompute.Plugins.Discovery
{
    public class PluginDiscoverer
    {
        private readonly string[] _searchPaths;
        private readonly ILogger<PluginDiscoverer> _logger;

        public async Task<IEnumerable<PluginInfo>> DiscoverPluginsAsync()
        {
            // 1. Scan directories for *.plugin manifests
            // 2. Load plugin metadata
            // 3. Verify dependencies
            // 4. Return discovered plugins
        }
    }

    public class PluginManifest
    {
        public string Id { get; set; }
        public string AssemblyName { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
    }
}
```

### Plugin Loading

```csharp
// src/DotCompute.Plugins/Loading/PluginLoader.cs
namespace DotCompute.Plugins.Loading
{
    public class PluginLoader
    {
        private readonly Dictionary<string, PluginLoadContext> _loadContexts;

        public async Task<IPlugin> LoadPluginAsync(PluginInfo info)
        {
            // 1. Create isolated AssemblyLoadContext
            // 2. Load plugin assembly
            // 3. Create plugin instance
            // 4. Initialize plugin
        }
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            // Custom assembly resolution logic
        }
    }
}
```

## 2. Source Generators Implementation

### Kernel Source Generator

```csharp
// src/DotCompute.SourceGenerators/Generators/KernelSourceGenerator.cs
[Generator]
public class KernelSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Register syntax receiver for [Kernel] attributes
        // 2. Generate backend-specific implementations
        // 3. Create dispatch tables
    }

    private string GenerateCudaKernel(KernelMetadata metadata)
    {
        // Generate CUDA C++ kernel code
    }

    private string GenerateMetalKernel(KernelMetadata metadata)
    {
        // Generate Metal Shading Language kernel
    }

    private string GenerateSimdKernel(KernelMetadata metadata)
    {
        // Generate SIMD-optimized CPU kernel
    }
}
```

### Kernel Attribute Design

```csharp
// src/DotCompute.Abstractions/Attributes/KernelAttributes.cs
namespace DotCompute.Abstractions
{
    [AttributeUsage(AttributeTargets.Method)]
    public class KernelAttribute : Attribute
    {
        public string Name { get; set; }
        public BackendTarget[] Targets { get; set; }
    }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class GlobalAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Parameter)]
    public class LocalAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class VectorizeAttribute : Attribute
    {
        public int Width { get; set; }
    }
}
```

### Example Kernel Definition

```csharp
// Example usage in user code
public static class MyKernels
{
    [Kernel(Targets = new[] { BackendTarget.Cuda, BackendTarget.Metal, BackendTarget.Cpu })]
    [Vectorize(Width = 8)]
    public static void VectorAdd(
        [Global] ReadOnlySpan<float> a,
        [Global] ReadOnlySpan<float> b,
        [Global] Span<float> result,
        int length)
    {
        int idx = KernelIndex.X;
        if (idx < length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

## 3. CUDA Backend Implementation

### CUDA Accelerator

```csharp
// plugins/backends/DotCompute.Backends.CUDA/src/Accelerators/CudaAccelerator.cs
namespace DotCompute.Backends.CUDA
{
    public class CudaAccelerator : IAccelerator
    {
        private readonly CudaContext _context;
        private readonly CudaDevice _device;
        private readonly CudaKernelCache _kernelCache;

        public async Task<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions options)
        {
            // 1. Generate PTX from kernel definition
            // 2. Compile using NVRTC
            // 3. Cache compiled kernel
            // 4. Return CudaCompiledKernel
        }

        public IMemoryBuffer AllocateBuffer(long sizeInBytes, MemoryFlags flags)
        {
            return new CudaBuffer(_context, sizeInBytes, flags);
        }
    }
}
```

### CUDA Native Interop

```cpp
// plugins/backends/DotCompute.Backends.CUDA/native/cuda_wrapper.cu
#include <cuda_runtime.h>
#include <nvrtc.h>

extern "C" {
    struct CudaKernelHandle {
        CUmodule module;
        CUfunction function;
    };

    EXPORT int cuda_compile_kernel(
        const char* source,
        const char* kernel_name,
        const char** options,
        int num_options,
        CudaKernelHandle** handle);

    EXPORT int cuda_launch_kernel(
        CudaKernelHandle* handle,
        dim3 grid_dim,
        dim3 block_dim,
        void** args,
        size_t shared_mem);
}
```

### CUDA Memory Management

```csharp
// plugins/backends/DotCompute.Backends.CUDA/src/Memory/CudaBuffer.cs
namespace DotCompute.Backends.CUDA.Memory
{
    public class CudaBuffer : IMemoryBuffer
    {
        private readonly IntPtr _devicePtr;
        private readonly long _size;
        private readonly CudaContext _context;

        public async Task CopyFromHostAsync<T>(ReadOnlyMemory<T> source) where T : unmanaged
        {
            // Async copy from host to device
        }

        public async Task CopyToHostAsync<T>(Memory<T> destination) where T : unmanaged
        {
            // Async copy from device to host
        }
    }
}
```

## 4. Metal Backend Implementation

### Metal Accelerator

```csharp
// plugins/backends/DotCompute.Backends.Metal/src/Accelerators/MetalAccelerator.cs
namespace DotCompute.Backends.Metal
{
    public class MetalAccelerator : IAccelerator
    {
        private readonly MetalDevice _device;
        private readonly MetalCommandQueue _commandQueue;
        private readonly MetalPipelineCache _pipelineCache;

        public async Task<ICompiledKernel> CompileKernelAsync(
            KernelDefinition definition,
            CompilationOptions options)
        {
            // 1. Generate Metal Shading Language
            // 2. Compile to Metal library
            // 3. Create compute pipeline state
            // 4. Return MetalCompiledKernel
        }
    }
}
```

### Metal Native Interop

```objc
// plugins/backends/DotCompute.Backends.Metal/native/metal_wrapper.mm
#import <Metal/Metal.h>
#import <MetalPerformanceShaders/MetalPerformanceShaders.h>

extern "C" {
    struct MetalKernelHandle {
        id<MTLComputePipelineState> pipeline;
        id<MTLFunction> function;
    };

    EXPORT int metal_compile_kernel(
        const char* source,
        const char* kernel_name,
        MetalKernelHandle** handle);

    EXPORT int metal_dispatch_kernel(
        MetalKernelHandle* handle,
        id<MTLCommandBuffer> command_buffer,
        MTLSize grid_size,
        MTLSize thread_group_size);
}
```

## Testing Infrastructure

### Source Generator Tests

```csharp
// src/DotCompute.SourceGenerators.Tests/KernelGeneratorTests.cs
public class KernelGeneratorTests
{
    [Fact]
    public async Task GeneratesCudaKernel()
    {
        var source = @"
            [Kernel(Targets = new[] { BackendTarget.Cuda })]
            public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c)
            {
                c[KernelIndex.X] = a[KernelIndex.X] + b[KernelIndex.X];
            }";

        var generated = await GenerateAndVerify(source);
        // Verify generated CUDA code
    }
}
```

### Backend Integration Tests

```csharp
// Common test base for all backends
public abstract class BackendIntegrationTestBase
{
    protected abstract IAccelerator CreateAccelerator();

    [Fact]
    public async Task ExecuteVectorAddKernel()
    {
        var accelerator = CreateAccelerator();
        var kernel = await accelerator.CompileKernelAsync(VectorAddKernel);
        
        // Allocate buffers
        // Execute kernel
        // Verify results
    }
}
```

## Build System Updates

### Directory.Build.props for Plugins

```xml
<!-- plugins/Directory.Build.props -->
<Project>
  <PropertyGroup>
    <PluginOutputPath>$(MSBuildThisFileDirectory)..\artifacts\plugins\$(Configuration)\</PluginOutputPath>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="*.plugin" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

### CMake for Native Components

```cmake
# plugins/backends/DotCompute.Backends.CUDA/native/CMakeLists.txt
cmake_minimum_required(VERSION 3.20)
project(DotComputeCudaNative LANGUAGES CXX CUDA)

find_package(CUDAToolkit REQUIRED)

add_library(dotcompute_cuda SHARED
    cuda_wrapper.cu
    kernel_cache.cpp
)

target_link_libraries(dotcompute_cuda
    CUDA::cudart
    CUDA::nvrtc
)
```

## Performance Considerations

### Kernel Compilation Caching
- Cache compiled kernels to disk
- Use content hash for cache keys
- Background compilation for faster startup

### Memory Transfer Optimization
- Pinned memory for faster transfers
- Async copy operations
- Memory pooling to reduce allocations

### Multi-GPU Support
- Device enumeration and selection
- Peer-to-peer memory access
- Work distribution strategies

## Security and Validation

### Plugin Validation
- Verify plugin signatures
- Sandbox plugin execution
- Resource usage limits

### Kernel Safety
- Bounds checking in generated code
- Memory access validation
- Thread synchronization verification