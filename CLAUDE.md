# DotCompute - Claude Code Context

## 🎯 Core Principles

**ALWAYS**:
- Quality first - no shortcuts, no compromises
- Production-grade code only
- Test-driven development
- Native AOT compatibility
- Organize files in proper directories (never root!)

## 📦 Project Overview

**DotCompute** v1.0.0-preview2 - Production Release Candidate for .NET 10

- **Repository**: https://github.com/mivertowski/DotCompute
- **Documentation**: https://mivertowski.github.io/DotCompute/
- **NuGet**: Published under `ivertowski` account as `DotCompute.*.V2` (legacy `DotCompute.*` IDs on `mivertowski` account are frozen at 0.6.2).
- **Performance**: CPU 3.7x (SIMD), GPU 21-92x (CUDA on RTX 2000 Ada, CC 8.9). Hopper (sm_90) first-class.
- **Backends (v1.0)**: CPU, CUDA, Metal only. OpenCL removed; WSL2 not a production target.
- **Code Quality**: Pristine build with 1 documented NoWarn (CA1873); inline SYSLIB1104 on DotCompute.Plugins for generic TOptions binding.

## 🚀 Quick Build Commands

```bash
# Build solution
dotnet build DotCompute.sln --configuration Release

# Run all tests (recommended - auto-configures WSL2)
./scripts/run-tests.sh DotCompute.sln --configuration Release

# Run specific categories
./scripts/run-tests.sh DotCompute.sln --filter "Category=Unit" --configuration Release
./scripts/run-tests.sh DotCompute.sln --filter "Category=Hardware" --configuration Release  # GPU required

# Run CUDA tests (with WSL2 auto-configuration)
./scripts/run-tests.sh tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj

# Manual test run (requires LD_LIBRARY_PATH set for WSL2)
dotnet test DotCompute.sln --configuration Release

# Clean
dotnet clean DotCompute.sln
```

## 🏗️ Architecture (4 Layers)

```
DotCompute/
├── src/
│   ├── Core/                          # Abstractions, Memory, Runtime
│   ├── Backends/                      # CPU, CUDA, OpenCL, Metal
│   ├── Extensions/                    # Algorithms, LINQ
│   └── Runtime/                       # Generators, Analyzers, Plugins
└── tests/
    ├── Unit/                          # Component tests
    ├── Integration/                   # Cross-component tests
    ├── Hardware/                      # GPU-specific tests
    └── Shared/                        # Test utilities
```

## ✅ Production-Ready Features

**Backend Infrastructure** (v1.0 scope: CPU / CUDA / Metal):
- ✅ CPU Backend (AVX2/AVX512/NEON SIMD - 3.7x speedup)
- ✅ CUDA Backend (CC 5.0-9.0 incl. Hopper, P2P idempotent, NCCL, Ring Kernels, classified errors, OTel - 21-92x speedup)
  - `DotCompute.Backends.CUDA.Hopper/`: ClusterLaunchConfig, TmaConfig, DsmemConfig, AsyncMemoryPool
  - Cache-line padded atomic counters in lock-free queues
- ✅ Metal Backend (FEATURE-COMPLETE - MPS, MSL translation, Ring Kernels, memory pooling)
- ✅ Memory Management (90% allocation reduction, P2P transfers)

**Developer Tooling**:
- ✅ Source Generators ([Kernel] attribute → auto-optimization)
- ✅ Roslyn Analyzers (12 diagnostic rules DC001-DC012)
- ✅ IDE Integration (5 automated code fixes, real-time feedback)
- ✅ Cross-Backend Debugging (CPU vs GPU validation)
- ✅ Adaptive Optimization (ML-powered backend selection)

**Advanced APIs**:
- ✅ Ring Kernel System (persistent GPU computation, actor model)
- ✅ GPU Timing API (1ns resolution CC 6.0+, 4 calibration strategies)
- ✅ Barrier API (ThreadBlock, Grid, Warp, Named barriers - <20ns)
- ✅ Memory Ordering API (3 consistency models, acquire-release semantics)
- ✅ LINQ Extensions (Phase 6: GPU compilation CUDA/OpenCL/Metal, 80% complete)

**Runtime & Integration**:
- ✅ DI Integration (Microsoft.Extensions.DependencyInjection)
- ✅ Plugin System (hot-reload capability)
- ✅ Native AOT (sub-10ms startup)

## 🔧 Critical System Info

**CUDA Configuration**:
- CUDA 13.0 at `/usr/local/cuda`
- GPU: NVIDIA RTX 2000 Ada (Compute Capability 8.9)
- Driver: 581.15
- **Always use**: `CudaCapabilityManager.GetTargetComputeCapability()`

**WSL2 CUDA Setup** (CRITICAL for Windows developers):
- NVIDIA libraries in `/usr/lib/wsl/lib/` (NOT `/usr/lib/x86_64-linux-gnu/`)
- Must set: `export LD_LIBRARY_PATH="/usr/lib/wsl/lib:$LD_LIBRARY_PATH"`
- Use: `./scripts/run-tests.sh` (handles WSL2 environment automatically)
- Or use: `./scripts/ci/setup-environment.sh` for full environment setup
- See: `docs/guides/wsl2-setup.md` for detailed configuration
- **Common Error**: CUDA_ERROR_NO_DEVICE (100) means library path not set

**Key Components**:
1. **Kernel System**: `IKernelCompiler`, `KernelDefinition`, `ICompiledKernel`
2. **Memory**: `UnifiedBuffer<T>`, `MemoryPool`, `P2PManager`
3. **Backend**: `IAccelerator`, `IComputeService`, `CompilationOptions`
4. **Runtime**: `IComputeOrchestrator`, `KernelExecutionService`
5. **Debugging**: `IKernelDebugService`, `DebugIntegratedOrchestrator`
6. **Optimization**: `AdaptiveBackendSelector`, `PerformanceOptimizedOrchestrator`

## 📝 Development Guidelines

**Native AOT**:
- No runtime code generation
- Use source generators for compile-time codegen
- Avoid reflection (use attributes where needed)

**Memory Safety**:
- Use `UnifiedBuffer<T>` for cross-device memory
- Implement `IDisposable` properly
- Use memory pooling for frequent allocations
- Validate buffer bounds in kernels

**Testing**:
- Unit tests for all public APIs
- Hardware tests check device availability
- Use `[SkippableFact]` for hardware-specific tests
- Target: ~80% code coverage

**CUDA**:
- Dynamic compute capability detection (don't hardcode)
- Use CUBIN for CC >= 7.0, PTX for older
- Support both compilation paths

## 🎯 Common Tasks

**Add New Kernel (Modern - v0.2.0+)**:
1. Add `[Kernel]` attribute to static method
2. Use `Kernel.ThreadId.X/Y/Z` for threading
3. Add bounds checking: `if (idx < length)`
4. Source generator creates wrapper automatically
5. IDE analyzers provide real-time feedback

**Debug CUDA Issues**:
1. Check capability: `CudaCapabilityManager.GetTargetComputeCapability()`
2. Verify CUDA: `/usr/local/cuda/bin/nvcc --version`
3. Check compilation logs in `CudaKernelCompiler`
4. Use `cuda-memcheck` for memory errors
5. Enable debug: `GenerateDebugInfo = true`

**Performance Optimization**:
1. Profile with BenchmarkDotNet (`benchmarks/`)
2. Use SIMD intrinsics for CPU
3. Optimize memory access patterns for GPU
4. Leverage memory pooling
5. Use P2P for multi-GPU

## 📊 Current Status (v0.5.3)

**Ring Kernel System**:
- ✅ **Phase 1 COMPLETE**: MemoryPack Integration (43/43 tests passing)
  - Auto-discovery of `[MemoryPackable]` types via Roslyn
  - Batch CUDA code generation for 10+ message types
  - MSBuild integration for pre-build codegen
  - Performance: <100ns serialization/deserialization
  - Zero manual CUDA coding required
- ✅ **Phase 2 COMPLETE**: CPU Ring Kernel Implementation (43/43 tests passing)
  - Thread-based persistent kernel execution
  - Message queue bridge infrastructure for IRingKernelMessage types
  - Echo mode with intelligent message transformation (VectorAdd, etc.)
  - Full lifecycle management (Launch, Activate, Deactivate, Terminate)
  - Telemetry and metrics tracking
  - Adaptive backoff for CPU efficiency
- ✅ **Phase 3 COMPLETE**: CUDA Ring Kernel Implementation (115/122 tests - 94.3%)
  - End-to-end message processing with MemoryPack serialization
  - 6-stage compilation pipeline (Discovery → CodeGen → PTX → Module → Functions → Result)
  - Performance: ~1.24μs serialization, ~1.01μs deserialization
  - Resource validation and cleanup (7 tests)
  - GPU-resident persistent kernels with CUDA
- ✅ **Phase 4 COMPLETE**: Multi-GPU coordination infrastructure
  - Single GPU system (RTX 2000 Ada) - multi-GPU tests N/A
  - P2P memory transfer infrastructure ready
  - Cross-GPU barrier foundations in place
- ✅ **Phase 5 COMPLETE**: Performance & Observability
  - 5.1: Performance Profiling & Optimization ✅
  - 5.2: Advanced Messaging Patterns ✅
  - 5.3: Fault Tolerance & Resilience ✅
  - 5.4: Observability & Distributed Tracing ✅ (94 tests passing)
    - OpenTelemetry integration (ActivitySource, Meter)
    - Prometheus-compatible metrics exporter
    - Health check infrastructure (94 tests)

**LINQ Extensions**:
- ✅ Phase 6: GPU Integration
  - GPU kernel generation (CPU SIMD, CUDA, Metal)
  - Automatic backend selection
  - Kernel fusion optimization
  - Filter compaction
- ✅ Phase 8 (v1.0.0): Advanced operators complete
  - **Join**: Hash join with linear probing (Inner/LeftOuter/Semi/Anti) on CPU/CUDA/Metal
  - **GroupBy**: Count/Sum/Min/Max/Average aggregations (CPU/CUDA/Metal)
  - **OrderBy / OrderByDescending**: CPU uses framework introsort, GPU uses bitonic sort
  - 50 new unit tests + 17 new integration tests (all passing)
  - Shipping scope: primary-key sort only; chained `ThenBy` deferred to v1.1

## 🐛 Known Limitations

1. **LINQ**: Feature-complete for v1.0.0. ThenBy/ThenByDescending chained sorts deferred to v1.1
2. **OpenCL Backend**: Removed from v1.0.0 scope (was experimental)
3. **Metal Backend**: Feature-complete, MSL translation works
4. **ROCm Backend**: Placeholder only
5. **Hardware Tests**: Require CC 5.0+ NVIDIA GPU

## ⚠️ WSL2 GPU Memory Limitations (CRITICAL)

### The Problem

WSL2 has **fundamental limitations** with GPU memory coherence that affect persistent kernel modes and real-time CPU-GPU communication:

1. **System-Scope Atomics Don't Work Reliably**
   - `cuda::memory_order_system` and `__threadfence_system()` don't provide reliable CPU-GPU visibility
   - GPU kernels cannot reliably see host memory updates in real-time
   - This is due to WSL2's GPU virtualization layer (GPU-PV)

2. **Unified Memory is Limited**
   - CUDA managed memory (`cudaMallocManaged`) has restricted functionality
   - Memory cannot spill from VRAM to system RAM like on native Linux
   - Large datasets that exceed VRAM fail instead of using system memory

3. **Persistent Kernel Mode Doesn't Work**
   - Ring kernels that poll for messages via tail pointer updates fail
   - Kernel never sees host-side updates to shared memory
   - Must use EventDriven mode with kernel relaunch instead

### Impact on Ring Kernels

| Feature | Native Linux | WSL2 |
|---------|-------------|------|
| Persistent kernel mode | ✅ Works (<1ms latency) | ❌ Fails (memory visibility) |
| EventDriven kernel mode | ✅ Works | ✅ Works (~5s latency) |
| System-scope atomics | ✅ Works | ❌ Unreliable |
| Unified memory spill | ✅ Works | ❌ Limited |
| `is_active` flag polling | ✅ Works | ❌ Kernel never sees updates |

### Workarounds Implemented

1. **Start-Active Pattern**: Kernels start with `is_active=1` already set, avoiding mid-execution activation
2. **EventDriven Mode**: Kernel processes available messages then terminates, host relaunches for new messages
3. **Bridge Transfer**: Messages queued on host, transferred to GPU buffer, kernel relaunched to process

### Performance Implications

- **Native Linux**: Sub-millisecond message latency with persistent kernels
- **WSL2**: ~5 second latency due to kernel relaunch overhead
- **Optimization**: Bridge uses SpinWait+Yield polling (sub-ms response when kernel is fast)

### Feature Request Channels

To request improved GPU memory coherence in WSL2:

1. **Microsoft WSL Repository**: https://github.com/microsoft/WSL/issues
   - Related: [Issue #7198](https://github.com/microsoft/wslg/issues/357) - Shared memory space issues
   - Related: [Issue #8447](https://github.com/microsoft/WSL/issues/8447) - CUDA out of memory
   - Related: [Issue #3789](https://github.com/Microsoft/WSL/issues/3789) - OpenCL/CUDA support

2. **Microsoft WSLg Repository** (GPU-specific): https://github.com/microsoft/wslg/issues

3. **NVIDIA CUDA on WSL**: https://docs.nvidia.com/cuda/wsl-user-guide/
   - Report issues via NVIDIA Developer Forums

### Recommendation

For production GPU-native actor systems requiring <10ms latency:
- **Use native Linux** (bare metal or VM with GPU passthrough)
- WSL2 is suitable for development and testing only

## 📂 Important Files

- `DotCompute.sln` - Solution file
- `Directory.Build.props` / `Directory.Build.targets` - Build config
- `Directory.Packages.props` - Package versions
- `scripts/` - Test and build scripts
- `docs/` - Documentation (29 guides)
- `benchmarks/` - Performance benchmarks

## 🛠️ Environment

- .NET 10 SDK (v10.0.106+)
- C# 14 language features
- Visual Studio 2022 17.13+ or VS Code
- CUDA Toolkit 12.0+ (for GPU support; CUDA 13.0+ recommended for sm_90)
- cmake (for native components)
- NVIDIA GPU with CC 5.0+ (for CUDA tests); CC 9.0 H100 for Hopper-specific tests

## 📋 File Organization

**NEVER save to root!** Use proper directories:
- `/src` - Source code
- `/tests` - Test files
- `/docs` - Documentation
- `/benchmarks` - Performance tests
- `/scripts` - Utility scripts
- `/samples` - Example code

---

**Remember**: Quality first, no shortcuts, production-grade only!
