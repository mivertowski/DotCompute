# DotCompute Phase 3 Architecture Design

## Overview

Phase 3 extends DotCompute with source generation capabilities and GPU backend support (CUDA and Metal). This document outlines the complete architecture, project structure, and design decisions.

## Project Structure

### 1. Source Generator Project

**Project:** `DotCompute.SourceGenerators`
**Location:** `src/DotCompute.SourceGenerators`

#### Directory Structure:
```
src/DotCompute.SourceGenerators/
├── DotCompute.SourceGenerators.csproj
├── Analyzers/
│   ├── KernelAnalyzer.cs
│   └── BackendCompatibilityAnalyzer.cs
├── Generators/
│   ├── KernelSourceGenerator.cs
│   ├── BackendAdapterGenerator.cs
│   └── VectorizationHintGenerator.cs
├── Models/
│   ├── KernelMetadata.cs
│   ├── BackendCapabilities.cs
│   └── GenerationOptions.cs
├── Templates/
│   ├── CudaKernelTemplate.cs
│   ├── MetalKernelTemplate.cs
│   └── SimdKernelTemplate.cs
└── Utils/
    ├── SourceGeneratorHelpers.cs
    └── DiagnosticDescriptors.cs
```

#### Key Components:
- **KernelSourceGenerator**: Main generator that produces backend-specific kernel implementations
- **BackendAdapterGenerator**: Generates adapter code for different backends
- **VectorizationHintGenerator**: Adds hints for auto-vectorization

### 2. Plugin System Core

**Project:** `DotCompute.Plugins`
**Location:** `src/DotCompute.Plugins`

#### Directory Structure:
```
src/DotCompute.Plugins/
├── DotCompute.Plugins.csproj
├── Discovery/
│   ├── PluginDiscoverer.cs
│   ├── AssemblyScanner.cs
│   └── PluginManifest.cs
├── Loading/
│   ├── PluginLoader.cs
│   ├── PluginContext.cs
│   └── DependencyResolver.cs
├── Management/
│   ├── PluginManager.cs
│   ├── PluginLifecycle.cs
│   └── PluginRegistry.cs
├── Interfaces/
│   ├── IPlugin.cs
│   ├── IBackendPlugin.cs
│   └── IPluginMetadata.cs
└── Configuration/
    ├── PluginConfiguration.cs
    └── PluginSettings.cs
```

### 3. CUDA Backend

**Project:** `DotCompute.Backends.CUDA`
**Location:** `plugins/backends/DotCompute.Backends.CUDA`

#### Directory Structure:
```
plugins/backends/DotCompute.Backends.CUDA/
├── DotCompute.Backends.CUDA.csproj
├── README.md
├── src/
│   ├── Accelerators/
│   │   ├── CudaAccelerator.cs
│   │   ├── CudaCompiledKernel.cs
│   │   └── CudaMemoryManager.cs
│   ├── Compilation/
│   │   ├── CudaKernelCompiler.cs
│   │   ├── PtxGenerator.cs
│   │   └── NvrtcWrapper.cs
│   ├── Interop/
│   │   ├── CudaBindings.cs
│   │   ├── CudaDeviceInfo.cs
│   │   └── CudaException.cs
│   ├── Kernels/
│   │   ├── CudaKernelLauncher.cs
│   │   ├── KernelCache.cs
│   │   └── WarpIntrinsics.cs
│   ├── Memory/
│   │   ├── CudaBuffer.cs
│   │   ├── UnifiedMemory.cs
│   │   └── PinnedMemory.cs
│   ├── Registration/
│   │   └── CudaBackendPlugin.cs
│   └── Scheduling/
│       ├── CudaStream.cs
│       └── CudaEventPool.cs
├── native/
│   ├── CMakeLists.txt
│   ├── cuda_wrapper.cu
│   ├── cuda_wrapper.h
│   └── kernel_templates/
│       ├── reduction.cuh
│       ├── scan.cuh
│       └── gemm.cuh
└── tests/
    ├── DotCompute.Backends.CUDA.Tests.csproj
    └── (test files)
```

### 4. Metal Backend

**Project:** `DotCompute.Backends.Metal`
**Location:** `plugins/backends/DotCompute.Backends.Metal`

#### Directory Structure:
```
plugins/backends/DotCompute.Backends.Metal/
├── DotCompute.Backends.Metal.csproj
├── README.md
├── src/
│   ├── Accelerators/
│   │   ├── MetalAccelerator.cs
│   │   ├── MetalCompiledKernel.cs
│   │   └── MetalMemoryManager.cs
│   ├── Compilation/
│   │   ├── MetalKernelCompiler.cs
│   │   ├── MetalShaderGenerator.cs
│   │   └── MslCompiler.cs
│   ├── Interop/
│   │   ├── MetalBindings.cs
│   │   ├── MetalDeviceInfo.cs
│   │   └── MetalException.cs
│   ├── Kernels/
│   │   ├── MetalKernelDispatcher.cs
│   │   ├── MetalPipelineCache.cs
│   │   └── SimdGroupIntrinsics.cs
│   ├── Memory/
│   │   ├── MetalBuffer.cs
│   │   ├── SharedMemory.cs
│   │   └── ManagedBuffer.cs
│   ├── Registration/
│   │   └── MetalBackendPlugin.cs
│   └── Scheduling/
│       ├── MetalCommandQueue.cs
│       └── MetalCommandBuffer.cs
├── native/
│   ├── CMakeLists.txt
│   ├── metal_wrapper.mm
│   ├── metal_wrapper.h
│   └── shaders/
│       ├── compute_kernels.metal
│       ├── reduction.metal
│       └── matrix_ops.metal
└── tests/
    ├── DotCompute.Backends.Metal.Tests.csproj
    └── (test files)
```

## Key Interfaces and Abstractions

### Plugin System Interfaces

```csharp
namespace DotCompute.Plugins
{
    public interface IPlugin
    {
        string Name { get; }
        Version Version { get; }
        void Initialize(IPluginContext context);
        void Shutdown();
    }

    public interface IBackendPlugin : IPlugin
    {
        IAccelerator CreateAccelerator(AcceleratorConfiguration config);
        BackendCapabilities GetCapabilities();
        bool IsSupported();
    }

    public interface IPluginMetadata
    {
        string Id { get; }
        string DisplayName { get; }
        string Description { get; }
        string[] Dependencies { get; }
    }
}
```

### Source Generator Models

```csharp
namespace DotCompute.SourceGenerators.Models
{
    public class KernelMetadata
    {
        public string Name { get; set; }
        public KernelType Type { get; set; }
        public BackendTarget[] Targets { get; set; }
        public OptimizationHints Hints { get; set; }
    }

    public class BackendCapabilities
    {
        public string BackendName { get; set; }
        public ComputeCapability ComputeCapability { get; set; }
        public MemoryHierarchy Memory { get; set; }
        public IntrinsicSupport Intrinsics { get; set; }
    }
}
```

## Architecture Decisions

### 1. Plugin Discovery and Loading
- **Assembly Scanning**: Scan designated plugin directories for assemblies
- **Manifest-Based**: Each plugin includes a manifest file with metadata
- **Isolated Loading**: Use AssemblyLoadContext for plugin isolation
- **Hot Reload**: Support for plugin hot-reloading during development

### 2. Source Generation Strategy
- **Incremental Generation**: Only regenerate changed kernels
- **Multi-Target**: Generate code for multiple backends from single source
- **Compile-Time Optimization**: Apply optimizations during generation
- **Diagnostic Integration**: Provide rich diagnostics in IDE

### 3. CUDA Integration
- **NVRTC**: Use NVIDIA Runtime Compilation for JIT compilation
- **PTX Caching**: Cache compiled PTX for performance
- **Unified Memory**: Support for CUDA Unified Memory
- **Multi-GPU**: Built-in multi-GPU support

### 4. Metal Integration
- **Metal Shading Language**: Direct MSL generation
- **Pipeline State Caching**: Cache compiled pipelines
- **Shared Memory**: Efficient CPU-GPU memory sharing
- **Argument Buffers**: Use for efficient parameter passing

### 5. Backend Abstraction
- **Common Interface**: All backends implement IAccelerator
- **Capability Queries**: Runtime capability detection
- **Fallback Mechanism**: Automatic fallback to CPU
- **Performance Profiling**: Built-in profiling support

## Dependencies

### Source Generators
- Microsoft.CodeAnalysis.CSharp (4.8.0)
- Microsoft.CodeAnalysis.Analyzers (3.3.4)

### CUDA Backend
- CUDA Toolkit (12.0+)
- Native dependencies handled via CMake

### Metal Backend
- Metal framework (macOS/iOS)
- Objective-C++ runtime

### Plugin System
- System.Reflection.MetadataLoadContext
- System.Runtime.Loader

## Build Configuration

### Solution Structure Update
The solution file needs to be updated with:
1. New source generator project
2. Plugin system project
3. CUDA backend project and tests
4. Metal backend project and tests

### Directory.Build.props Updates
- Add source generator output paths
- Configure plugin discovery paths
- Set native library paths

### CMake Integration
- CUDA backend native compilation
- Metal backend native compilation
- Cross-platform build support

## Testing Strategy

### Unit Tests
- Source generator tests using snapshot testing
- Plugin system tests with mock plugins
- Backend-specific kernel tests

### Integration Tests
- End-to-end kernel compilation and execution
- Plugin loading and lifecycle tests
- Multi-backend execution tests

### Performance Tests
- Benchmark native performance vs managed
- Memory transfer overhead measurements
- Kernel launch latency tests

## Security Considerations

### Plugin Security
- Code signing for plugins
- Sandboxed execution environment
- Permission system for resource access

### Memory Safety
- Bounds checking in kernels
- Safe interop with native code
- Memory leak detection

## Future Considerations

### Phase 4 Preparations
- Distributed computing interfaces
- Network protocol abstractions
- Cluster management APIs

### Extensibility Points
- Custom backend development SDK
- Kernel optimization plugins
- Performance analysis tools