# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DotCompute is a high-performance, Native AOT-compatible universal compute framework for .NET 9+ with production-ready CPU and CUDA acceleration. The system is designed for sub-10ms startup times and provides 8-23x speedup through SIMD vectorization.

## Essential Build Commands

```bash
# Build the solution
dotnet build DotCompute.sln --configuration Release

# Run all tests
dotnet test DotCompute.sln --configuration Release

# Run specific test category
dotnet test --filter "Category=Unit" --configuration Release
dotnet test --filter "Category=Hardware" --configuration Release  # Requires NVIDIA GPU

# Run CUDA-specific tests
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj

# Clean build artifacts
dotnet clean DotCompute.sln

# Generate code coverage
./scripts/run-coverage.sh

# Run hardware tests with detailed output
./scripts/run-hardware-tests.sh
```

## Critical System Information

### CUDA Configuration
- **System has CUDA 13.0 installed** at `/usr/local/cuda` (symlink to `/usr/local/cuda-13.0`)
- **GPU**: NVIDIA RTX 2000 Ada Generation (Compute Capability 8.9)
- **Driver Version**: 581.15
- **All CUDA capability detection must use**: `DotCompute.Backends.CUDA.Configuration.CudaCapabilityManager`

### Architecture Overview

The codebase follows a **comprehensive, production-grade architecture** with four integrated layers:

```
DotCompute/
├── src/
│   ├── Core/                    # Core abstractions and runtime
│   │   ├── DotCompute.Core/     # Main runtime, orchestration, optimization
│   │   │   ├── Debugging/       # ✅ Cross-backend validation system
│   │   │   ├── Optimization/    # ✅ Adaptive backend selection with ML
│   │   │   ├── Telemetry/       # ✅ Performance profiling infrastructure
│   │   │   └── Pipelines/       # ✅ Execution pipeline management
│   │   ├── DotCompute.Abstractions/  # Interface definitions
│   │   │   ├── Debugging/       # ✅ IKernelDebugService interface
│   │   │   └── Interfaces/      # ✅ IComputeOrchestrator, IKernel
│   │   └── DotCompute.Memory/   # Unified memory with pooling
│   ├── Backends/                # Compute backend implementations
│   │   ├── DotCompute.Backends.CPU/   # ✅ Production: AVX2/AVX512 SIMD
│   │   ├── DotCompute.Backends.CUDA/  # ✅ Production: NVIDIA GPU (CC 5.0+)
│   │   ├── DotCompute.Backends.Metal/ # ❌ Stubs only
│   │   └── DotCompute.Backends.ROCm/  # ❌ Placeholder
│   ├── Extensions/              # Extension libraries
│   │   ├── DotCompute.Algorithms/     # Algorithm implementations
│   │   └── DotCompute.Linq/           # LINQ provider with kernels
│   └── Runtime/                 # Runtime services and code generation
│       ├── DotCompute.Runtime/  # ✅ Service orchestration, discovery
│       │   └── Services/        # ✅ KernelExecutionService, discovery
│       ├── DotCompute.Generators/     # ✅ Source generators & analyzers
│       │   ├── Analyzers/       # ✅ 12 diagnostic rules (DC001-DC012)
│       │   ├── CodeFixes/       # ✅ 5 automated IDE fixes
│       │   ├── Kernel/          # ✅ Kernel source generation
│       │   └── Examples/        # ✅ Analyzer demonstrations
│       └── DotCompute.Plugins/  # Plugin system with hot-reload
└── tests/
    ├── Unit/                    # Unit tests for components
    ├── Integration/             # Integration tests
    ├── Hardware/                # Hardware-specific tests (GPU)
    └── Shared/                  # Shared test utilities
```

### Key Architectural Components

1. **Kernel System** (`src/Core/DotCompute.Core/Kernels/`)
   - `KernelDefinition`: Metadata for compute kernels
   - `IKernelCompiler`: Interface for backend-specific compilation
   - `ICompiledKernel`: Represents compiled kernel ready for execution

2. **Memory Management** (`src/Core/DotCompute.Memory/`)
   - `UnifiedBuffer<T>`: Zero-copy memory buffer across devices
   - `MemoryPool`: Pooled memory allocation (90% reduction)
   - `P2PManager`: Peer-to-peer GPU memory transfers

3. **Backend Abstraction** (`src/Core/DotCompute.Abstractions/`)
   - `IAccelerator`: Common interface for all compute backends
   - `IComputeService`: High-level compute orchestration
   - `CompilationOptions`: Kernel compilation settings

4. **CUDA Backend** (`src/Backends/DotCompute.Backends.CUDA/`)
   - `CudaCapabilityManager`: Centralized compute capability detection
   - `CudaKernelCompiler`: NVRTC-based kernel compilation
   - `CudaAccelerator`: CUDA-specific accelerator implementation
   - `CudaRuntime`: P/Invoke wrappers for CUDA API

5. **CPU Backend** (`src/Backends/DotCompute.Backends.CPU/`)
   - `SimdProcessor`: SIMD vectorization (AVX512/AVX2/NEON)
   - `CpuAccelerator`: Multi-threaded CPU execution
   - `VectorizedOperations`: Hardware-accelerated operations

6. **Runtime Integration** (`src/Core/DotCompute.Core/Interfaces/`)
   - `IComputeOrchestrator`: Universal kernel execution interface
   - `KernelExecutionService`: Runtime orchestration service
   - `GeneratedKernelDiscoveryService`: Automatic kernel registration

7. **Debugging System** (`src/Core/DotCompute.Core/Debugging/`)
   - `IKernelDebugService`: Cross-backend validation interface
   - `KernelDebugService`: Production debugging implementation
   - `DebugIntegratedOrchestrator`: Transparent debug wrapper
   - Multiple profiles: Development, Testing, Production

8. **Optimization Engine** (`src/Core/DotCompute.Core/Optimization/`)
   - `AdaptiveBackendSelector`: ML-powered backend selection
   - `PerformanceOptimizedOrchestrator`: Performance-driven execution
   - `WorkloadCharacteristics`: Workload pattern analysis
   - Multiple optimization strategies: Conservative, Balanced, Aggressive

9. **Source Generators & Analyzers** (`src/Runtime/DotCompute.Generators/`)
   - `KernelSourceGenerator`: Generates kernel wrappers from [Kernel] attributes
   - `DotComputeKernelAnalyzer`: 12 diagnostic rules for kernel quality
   - `KernelCodeFixProvider`: 5 automated code fixes
   - Real-time IDE integration with Visual Studio and VS Code

## Development Guidelines

### Native AOT Compatibility
- All code must be Native AOT compatible
- No runtime code generation
- Use source generators for compile-time code generation
- Avoid reflection except where marked with proper attributes

### Memory Safety
- Always use `UnifiedBuffer<T>` for cross-device memory
- Implement proper `IDisposable` patterns
- Use memory pooling for frequent allocations
- Validate buffer bounds in kernel code

### Testing Requirements
- Unit tests required for all public APIs
- Hardware tests must check for device availability
- Use `[SkippableFact]` for tests requiring specific hardware
- Maintain ~75% code coverage target

### CUDA Development
- Always use `CudaCapabilityManager.GetTargetComputeCapability()` for capability detection
- Support dynamic CUDA version detection (don't hardcode paths)
- Use CUBIN compilation for modern GPUs (compute capability >= 7.0)
- Handle both PTX and CUBIN compilation paths

## Common Development Tasks

### Adding a New Kernel (Modern Approach v0.2.0+)
1. Add `[Kernel]` attribute to static method in C#
2. Use `Kernel.ThreadId.X/Y/Z` for threading model
3. Ensure bounds checking with `if (idx < length)`
4. Source generator creates wrapper automatically
5. IDE analyzers provide real-time feedback
6. System automatically optimizes for CPU/GPU

### Adding a New Kernel (Legacy Approach)
1. Define kernel in `KernelDefinition` format
2. Implement CPU version using SIMD intrinsics
3. Implement CUDA version in CUDA C
4. Add unit tests in appropriate test project
5. Add hardware tests if GPU-specific

### Debugging CUDA Issues
1. Check compute capability: `CudaCapabilityManager.GetTargetComputeCapability()`
2. Verify CUDA installation: `/usr/local/cuda/bin/nvcc --version`
3. Check kernel compilation logs in `CudaKernelCompiler`
4. Use `cuda-memcheck` for memory errors
5. Enable debug compilation with `GenerateDebugInfo = true`

### Performance Optimization
1. Profile with BenchmarkDotNet (`benchmarks/` directory)
2. Use SIMD intrinsics for CPU backend
3. Optimize memory access patterns for GPU
4. Leverage memory pooling for allocations
5. Use P2P transfers for multi-GPU scenarios

## Important Files and Locations

- **Solution File**: `DotCompute.sln`
- **Build Configuration**: `Directory.Build.props`, `Directory.Build.targets`
- **Package Versions**: `Directory.Packages.props`
- **Test Scripts**: `scripts/` directory (various shell scripts)
- **Documentation**: `docs/` directory (architecture, API, guides)
- **Benchmarks**: `benchmarks/` directory
- **Examples**: `samples/` directory

## Known Issues and Limitations

1. **Metal Backend**: Contains stubs only, not implemented
2. **ROCm Backend**: Placeholder, not implemented
3. **CUDA Tests**: Some tests may fail with "device kernel image is invalid" on CUDA 13
4. **Hardware Tests**: Require NVIDIA GPU with Compute Capability 5.0+
5. **Cross-Platform GPU**: Currently limited to NVIDIA GPUs only

## Production-Ready Components

✅ **Fully Production Ready (v0.2.0):**
- CPU Backend with SIMD vectorization (8-23x speedup)
- CUDA Backend with complete GPU support
- Memory Management with pooling and P2P
- Plugin System with hot-reload
- Native AOT compilation support
- Source Generator with [Kernel] attribute support
- Roslyn Analyzers with 12 diagnostic rules
- Cross-Backend Debugging with validation
- Adaptive Backend Selection with ML
- Performance Profiling with hardware counters
- Runtime Orchestration with DI integration

🚧 **Basic Implementation:**
- Algorithm libraries (basic operations only)
- LINQ provider (CPU fallback working)
- Metal and ROCm backends (stubs only)

## Critical Implementation Details

### CUDA Kernel Compilation Flow
1. Source code → NVRTC compilation → PTX/CUBIN
2. Architecture selection via `CudaCapabilityManager`
3. Caching in `CudaKernelCache` for performance
4. Module loading with `cuModuleLoadDataEx`

### Memory Management Strategy
1. Unified buffers abstract device-specific memory
2. Memory pool reduces allocations by 90%+
3. P2P manager handles GPU-to-GPU transfers
4. Pinned memory for CPU-GPU transfers

### Backend Selection Priority
1. CUDA (if NVIDIA GPU available)
2. CPU with SIMD (always available)
3. Future: Metal (macOS), ROCm (AMD GPU)

## Environment Requirements

- **.NET 9.0 SDK** or later
- **C# 13** language features
- **Visual Studio 2022 17.8+** or VS Code
- **CUDA Toolkit 12.0+** for GPU support
- **cmake** for native components
- **NVIDIA GPU** with Compute Capability 5.0+ for CUDA tests

## New Features in v0.2.0-alpha

### Phase 1: Generator ↔ Runtime Integration
- `IComputeOrchestrator` interface for universal kernel execution
- `KernelExecutionService` for runtime orchestration
- `GeneratedKernelDiscoveryService` for automatic kernel registration
- Dependency injection integration with Microsoft.Extensions.DependencyInjection

### Phase 2: Roslyn Analyzer Integration
- 12 diagnostic rules (DC001-DC012) for kernel code quality
- 5 automated code fixes in IDE
- Real-time feedback in Visual Studio and VS Code
- Comprehensive examples in `AnalyzerDemo.cs`

### Phase 3: Enhanced Debugging Service
- `IKernelDebugService` with 8 debugging methods
- Cross-backend validation (CPU vs GPU)
- Performance analysis and profiling
- Determinism testing and memory pattern analysis
- Multiple debugging profiles (Development, Testing, Production)

### Phase 4: Production Optimizations
- `AdaptiveBackendSelector` with machine learning capabilities
- `PerformanceOptimizedOrchestrator` for intelligent execution
- Workload pattern recognition and analysis
- Real-time performance monitoring
- Multiple optimization profiles (Conservative, Balanced, Aggressive, ML-optimized)

### Usage Examples

#### Modern Kernel Definition
```csharp
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

#### Service Configuration
```csharp
services.AddDotComputeRuntime();
services.AddProductionOptimization();  // Intelligent backend selection
services.AddProductionDebugging();     // Cross-backend validation
```

### Key Benefits
- **Universal Execution**: Write once in C#, run optimally on CPU or GPU
- **IDE Integration**: Real-time feedback and automated improvements
- **Production Ready**: Comprehensive debugging and optimization
- **Intelligent**: ML-powered backend selection learns from execution patterns
- **Observable**: Detailed performance metrics and profiling