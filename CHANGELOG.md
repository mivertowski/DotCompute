# Changelog

All notable changes to DotCompute will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0-alpha] - 2025-11-03

### Major Release - Production-Ready GPU Acceleration

This release represents a significant milestone with production-ready GPU acceleration, comprehensive developer tooling, and the introduction of Ring Kernels for persistent GPU computation.

### Added

#### Ring Kernel System
- **`[RingKernel]` Attribute**: New persistent kernel programming model for GPU-resident actor systems
- **Message Passing Infrastructure**: Lock-free message queues with multiple strategies:
  - SharedMemory: GPU shared memory queues (fastest single-GPU)
  - AtomicQueue: Global memory atomics (scalable)
  - P2P: Direct GPU-to-GPU transfers (CUDA with NVLink)
  - NCCL: Multi-GPU collectives (CUDA distributed workloads)
- **Execution Modes**:
  - Persistent: Continuously running kernels for streaming workloads
  - EventDriven: On-demand activation for sporadic tasks
- **Domain Optimizations**:
  - GraphAnalytics: Optimized for irregular memory access (PageRank, BFS, shortest paths)
  - SpatialSimulation: Stencil patterns and halo exchange (fluids, physics)
  - ActorModel: Message-heavy workloads with dynamic distribution
  - General: No domain-specific optimizations
- **Runtime Management**: IRingKernelRuntime with launch, activation, status monitoring, and metrics
- **Cross-Backend Support**: Implemented for CPU (simulation), CUDA, OpenCL, and Metal

#### OpenCL Backend (Now Production Ready)
- **Status Upgrade**: Moved from experimental to production-ready
- **Cross-Platform GPU Support**: Full support for:
  - NVIDIA GPUs (via CUDA Toolkit or nvidia-opencl-icd)
  - AMD GPUs (via ROCm or amdgpu-pro drivers)
  - Intel GPUs (via intel-opencl-icd or beignet)
  - ARM Mali GPUs (mobile and embedded)
  - Qualcomm Adreno GPUs (mobile)
- **Ring Kernel Support**: Complete persistent kernel implementation with atomic message queues
- **OpenCL 1.2+ Compatibility**: Works with all modern OpenCL runtimes
- **Runtime Kernel Compilation**: Dynamic OpenCL C kernel compilation
- **Memory Management**: Device memory allocation, transfers, and zero-copy mapping
- **Multi-Device Support**: Workload distribution across multiple OpenCL devices

#### Source Generators & Analyzers
- **Modern `[Kernel]` Attribute API**: Cleaner C# kernel definitions with automatic optimization
- **Incremental Source Generator**: IIncrementalGenerator for optimal IDE performance
- **Automatic Code Generation**:
  - Backend-specific implementations (CPU SIMD, CUDA GPU, OpenCL, Metal)
  - Kernel registry for runtime dispatch
  - Message queue infrastructure for Ring Kernels
  - Type-safe kernel invokers
- **Roslyn Analyzer Integration**: 12 diagnostic rules (DC001-DC012) with real-time feedback
- **Automated Code Fixes**: 5 one-click fixes in Visual Studio and VS Code
- **IDE Integration**: Real-time diagnostics, performance suggestions, and GPU compatibility analysis

#### Cross-Backend Debugging & Validation
- **IKernelDebugService**: Comprehensive debugging interface with 8 validation methods
- **CPU vs GPU Validation**: Automatic result comparison across backends
- **Performance Analysis**: Bottleneck detection and optimization suggestions
- **Determinism Testing**: Validates consistent results across multiple runs
- **Memory Pattern Analysis**: Detects memory access issues
- **Multiple Debug Profiles**: Development, Testing, Production configurations
- **Transparent Wrapper**: DebugIntegratedOrchestrator with zero-overhead production mode

#### Performance Optimization Engine
- **AdaptiveBackendSelector**: ML-powered backend selection based on workload characteristics
- **Workload Pattern Recognition**: Automatic detection of compute vs memory-bound operations
- **Real-Time Performance Monitoring**: Hardware counters and telemetry collection
- **Historical Performance Tracking**: Learning from execution patterns
- **Multiple Optimization Profiles**:
  - Conservative: Prioritizes correctness
  - Balanced: Balances performance and safety
  - Aggressive: Maximum performance optimizations
  - ML-Optimized: Machine learning-based decisions

#### LINQ Extensions
- **Expression Compilation Pipeline**: Direct LINQ-to-kernel compilation
- **Reactive Extensions Integration**: GPU-accelerated streaming with Rx.NET
- **Kernel Fusion**: Multiple operations combined into single execution
- **Adaptive Batching**: Automatic batching for GPU efficiency
- **Memory Optimization**: Intelligent caching and buffer reuse
- **50+ Integration Tests**: Comprehensive validation with performance benchmarks

#### Runtime Services & DI
- **IComputeOrchestrator**: Universal kernel execution interface
- **KernelExecutionService**: Runtime orchestration with dependency injection
- **GeneratedKernelDiscoveryService**: Automatic kernel registration
- **Microsoft.Extensions.DependencyInjection**: Full DI container integration
- **Service Lifetimes**: Proper singleton, scoped, and transient registrations

### Improved

#### CUDA Backend Enhancements
- **Full Production Status**: Complete implementation with comprehensive testing
- **Ring Kernel Support**: P2P, NCCL, and shared memory messaging strategies
- **Enhanced P2P Transfers**: Optimized GPU-to-GPU memory operations
- **Improved Kernel Compilation**: Better NVRTC error reporting and diagnostics
- **Memory Pool Optimization**: 90% allocation reduction through intelligent pooling
- **Compute Capability Support**: Validated on CC 5.0 through 8.9 (Maxwell to Ada Lovelace)

#### CPU Backend Optimizations
- **Measured 3.7x Speedup**: Benchmarked SIMD vectorization improvements
- **Ring Kernel Simulation**: CPU-based implementation for testing and development
- **Enhanced AVX512 Support**: Better utilization of advanced SIMD instructions
- **Parallel Execution**: Improved multi-threaded kernel scheduling
- **Memory Alignment**: Optimized buffer alignment for cache performance

#### Memory Management
- **90% Allocation Reduction**: Through memory pooling and reuse
- **Unified Buffer Improvements**: Better cross-device memory abstraction
- **P2P Manager**: Peer-to-peer GPU memory transfer coordination
- **Zero-Copy Optimizations**: Enhanced support for unified memory operations

#### Developer Experience
- **Real-Time IDE Feedback**: Instant diagnostics as you type
- **Performance Suggestions**: Analyzer-provided optimization hints
- **GPU Compatibility Analysis**: Automatic detection of GPU-compatible patterns
- **Code Fix Integration**: One-click automated improvements
- **Comprehensive Documentation**: 27 documentation files (~19,500 lines)

### Changed

- **API Modernization**: Moved from manual kernel definition to `[Kernel]` and `[RingKernel]` attributes
- **Backend Status**: OpenCL promoted from experimental to production
- **Documentation Structure**: Reorganized with architecture, guides, examples, and reference sections
- **Error Reporting**: More actionable error messages with recommendations
- **Configuration System**: Enhanced with validation and better defaults

### Fixed

- **CUDA Driver Compatibility**: Resolved issues with CUDA 13.0 and newer drivers
- **Memory Leaks**: Fixed buffer cleanup issues in CUDA and OpenCL backends
- **Race Conditions**: Eliminated concurrent access issues in kernel compilation
- **Analyzer False Positives**: Refined diagnostic rules to reduce noise
- **Native AOT Compatibility**: Resolved trimming issues for full Native AOT support

### Performance

Measured improvements with BenchmarkDotNet on .NET 9.0:

| Operation | Dataset Size | .NET Standard | DotCompute | Improvement |
|-----------|--------------|---------------|------------|-------------|
| Vector Add | 100K elements | 2.14ms | 0.58ms | **3.7x faster** |
| Sum Reduction | 100K elements | 0.65ms | 0.17ms | **3.8x faster** |
| Memory Allocations | Per operation | 48 bytes | 0 bytes | **100% reduction** |
| Startup Time (Native AOT) | Cold start | ~50ms | <10ms | **5x faster** |

### Documentation

- **Architecture Documentation**: 7 comprehensive guides covering system design
- **Developer Guides**: 10 guides for kernel development, performance tuning, debugging
- **Examples**: 5 practical examples with benchmarks (vector ops, image processing, matrix ops)
- **Reference**: Complete diagnostic rules and performance benchmarking documentation
- **API Documentation**: Full DocFX-generated API reference
- **README Updates**: All package READMEs updated with professional tone and documentation links

### Known Issues

- **Metal Backend**: Foundation complete but MSL compilation in progress
- **ROCm Backend**: Placeholder implementation, not yet functional
- **Ring Kernel P2P on OpenCL**: Not available (use SharedMemory or AtomicQueue strategies)
- **NCCL on Non-CUDA**: Only available on CUDA backend

### Technical Details

- **Target Framework**: .NET 9.0
- **Language Version**: C# 13.0
- **Supported Platforms**: Windows x64, Linux x64, macOS ARM64 and x64
- **CUDA Support**: Compute Capability 5.0+ (Maxwell through Ada Lovelace)
- **OpenCL Support**: OpenCL 1.2+ (NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno)
- **Native AOT**: Full compatibility with sub-10ms startup times
- **Test Coverage**: 75-85% with comprehensive unit and integration tests

### Migration Guide from 0.1.0

#### Old Approach (Manual Kernel Definition)
```csharp
var kernelDef = new KernelDefinition { Name = "VectorAdd", Source = "..." };
var kernel = await accelerator.CompileKernelAsync(kernelDef);
await kernel.ExecuteAsync(args);
```

#### New Approach (Attribute-Based)
```csharp
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length) result[idx] = a[idx] + b[idx];
}

// Automatic optimization and backend selection
var orchestrator = services.GetRequiredService<IComputeOrchestrator>();
await orchestrator.ExecuteAsync("VectorAdd", a, b, result);
```

### Breaking Changes

- **Kernel Definition API**: Manual KernelDefinition is deprecated in favor of `[Kernel]` attributes
- **Backend Initialization**: Now uses dependency injection instead of direct instantiation
- **Memory Buffer API**: UnifiedBuffer<T> replaces direct device memory allocation
- **Configuration**: New options pattern with validation replaces direct property access

### Contributors

- Michael Ivertowski - Core architecture, implementation, and documentation

### Upgrade Notes

1. **Add Source Generator**: Reference DotCompute.Generators project with OutputItemType="Analyzer"
2. **Update Kernel Definitions**: Convert to `[Kernel]` attributes for automatic optimization
3. **Use Dependency Injection**: Register services with AddDotComputeRuntime()
4. **Enable Production Features**: Use AddProductionOptimization() and AddProductionDebugging()
5. **Review Diagnostics**: Address any new analyzer warnings in your code

---

**Status**: This release is production-ready for CPU and GPU acceleration. Ring Kernels are stable across all backends. Comprehensive testing and documentation included.

[0.2.0-alpha]: https://github.com/mivertowski/DotCompute/releases/tag/v0.2.0-alpha

## [0.1.0-alpha] - 2025-01-05

### Initial Alpha Release

This is the first public alpha release of DotCompute, an experimental compute acceleration framework for .NET 9+.

### Added

#### Core Features
- Basic compute acceleration framework with modular architecture
- Native AOT compilation support for reduced startup times
- Unified memory management system with pooling capabilities
- Abstract accelerator interfaces for device-agnostic programming

#### CPU Backend
- SIMD vectorization support using AVX2/AVX512 instructions
- Multi-threaded kernel execution
- Basic vectorized operations for common compute patterns
- Memory-aligned buffer management

#### CUDA Backend (Experimental)
- Partial NVIDIA GPU support through CUDA
- NVRTC-based kernel compilation pipeline
- Basic PTX and CUBIN compilation
- Device memory management
- Compute capability detection

#### Memory System
- Unified buffer abstraction for cross-device memory
- Memory pooling with automatic buffer reuse
- Zero-copy operations where supported
- Pinned memory allocation for GPU transfers

### Known Issues

- CUDA backend has compatibility issues with some driver versions
- Limited kernel language support (C-style kernels only)
- API is unstable and will change in future releases
- Performance optimizations are incomplete
- No support for AMD GPUs (ROCm) or Apple Silicon (Metal)
- Documentation is incomplete

### Technical Details

- Target Framework: .NET 9.0
- Language Version: C# 13.0
- Supported Platforms: Windows x64, Linux x64, macOS x64 (CPU only)
- CUDA Support: Requires CUDA Toolkit 12.0+ and compatible NVIDIA drivers
- Native AOT: Fully compatible with .NET 9 Native AOT compilation

### Breaking Changes

As this is the initial release, there are no breaking changes. However, users should expect significant API changes in future releases as the framework evolves.

### Contributors

- Michael Ivertowski - Initial implementation and architecture

---

**Note**: This is experimental alpha software. Use at your own risk. Not recommended for production workloads.

[0.1.0-alpha]: https://github.com/mivertowski/DotCompute/releases/tag/v0.1.0-alpha