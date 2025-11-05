# DotCompute v0.2.0-alpha - Known Limitations

**Document Version**: 1.0
**Release**: v0.2.0-alpha
**Last Updated**: November 2025

## Overview

This document catalogs known limitations, deferred features, and areas requiring future work in DotCompute v0.2.0-alpha. All items are tracked with target versions for resolution.

## Summary by Category

| Category | Total Issues | Critical | High | Medium | Low |
|----------|-------------|----------|------|--------|-----|
| **Backend - Metal** | 2 | 0 | 1 | 1 | 0 |
| **Backend - ROCm** | 1 | 0 | 0 | 0 | 1 |
| **Cryptography** | 2 | 0 | 1 | 1 | 0 |
| **LINQ Extensions** | 3 | 0 | 2 | 1 | 0 |
| **Algorithm Libraries** | 2 | 0 | 1 | 1 | 0 |
| **Plugin System** | 1 | 0 | 1 | 0 | 0 |
| **Profiling** | 1 | 0 | 0 | 1 | 0 |
| **Total** | 12 | 0 | 6 | 5 | 1 |

---

## 1. Backend Limitations

### 1.1 Metal Backend - MSL Compilation (HIGH)

**Status**: In Development (60% complete)
**Target**: v0.3.0 (Q1 2026)

**Description**:
Metal backend has native API integration complete but MSL (Metal Shading Language) kernel compilation is incomplete.

**Impact**:
- Metal backend cannot execute compute kernels
- macOS/iOS GPU acceleration unavailable
- Apple Silicon systems fall back to CPU

**Workaround**:
- Use CPU SIMD backend (3.7x speedup still available)
- OpenCL backend may work on some macOS systems

**Technical Details**:
- Native Objective-C++ integration: ✅ Complete
- Device management and memory: ✅ Complete
- MSL kernel compilation: 🚧 60% complete
- Command buffer execution: ✅ Complete

**Files Affected**:
- `src/Backends/DotCompute.Backends.Metal/Compilation/MetalKernelCompiler.cs`
- `src/Backends/DotCompute.Backends.Metal/Compilation/MSLCodeGenerator.cs`

**Reference**: See `docs/METAL_BACKEND_STATUS.md` for detailed progress

---

### 1.2 ROCm Backend - AMD GPU Support (LOW)

**Status**: Placeholder
**Target**: v0.4.0 (Q3 2026)

**Description**:
ROCm backend for AMD GPU support is currently a placeholder with no implementation.

**Impact**:
- AMD Radeon GPUs cannot be used for compute
- Systems with AMD GPUs fall back to CPU or OpenCL (if available)

**Workaround**:
- OpenCL backend supports many AMD GPUs
- CPU SIMD backend provides acceleration on all systems

**Technical Details**:
- Project structure: ✅ Created
- HIP runtime integration: ❌ Not started
- Kernel compilation: ❌ Not started
- Memory management: ❌ Not started

**Priority Rationale**:
- OpenCL provides adequate AMD GPU support for v0.2.0-v0.3.0
- ROCm offers marginal performance benefit vs. development cost
- Focus on Metal completion (larger user base) first

---

## 2. Cryptography Limitations

### 2.1 PKCS#12 Key Export Format (HIGH)

**Status**: Deferred
**Target**: v0.3.0 (Q1 2026)

**Description**:
PKCS#12 (.p12/.pfx) key export format is not supported. Attempting to export throws `NotSupportedException`.

**Impact**:
- Cannot export keys in PKCS#12 format for Windows certificate stores
- Cross-platform key exchange with legacy systems may be limited

**Workaround**:
- Use PKCS#8 format (fully supported, provides equivalent functionality)
- Use PEM format for broader compatibility
- Convert PKCS#8 to PKCS#12 using OpenSSL if needed:
  ```bash
  openssl pkcs8 -in key.p8 -out key.key
  openssl pkcs12 -export -inkey key.key -out key.p12
  ```

**Technical Details**:
- PKCS#8 export: ✅ Complete (equivalent functionality)
- PEM export: ✅ Complete
- DER export: ✅ Complete
- PKCS#12 export: ❌ Deferred to v0.3.0

**Error Message**:
```
NotSupportedException: PKCS#12 key export format is not supported in this version.
Use PKCS#8 format instead. PKCS#12 support is planned for v0.3.0.
```

**Files Affected**:
- `src/Core/DotCompute.Core/Security/CryptographicKeyManager.cs:589-593`

---

### 2.2 JWK (JSON Web Key) Export Format (MEDIUM)

**Status**: Deferred
**Target**: v0.3.0 (Q1 2026)

**Description**:
JWK (JSON Web Key) export format is not supported. Attempting to export throws `NotSupportedException`.

**Impact**:
- Cannot export keys for modern web applications expecting JWK format
- OAuth 2.0 / OpenID Connect integrations require manual conversion

**Workaround**:
- Use PEM or PKCS#8 format (fully supported)
- Convert to JWK using external tools or libraries:
  ```bash
  # Using jose CLI tool
  jose jwk fmt -i key.pem

  # Using Python jwcrypto
  python -c "from jwcrypto import jwk; k = jwk.JWK.from_pem(open('key.pem','rb').read()); print(k.export())"
  ```

**Technical Details**:
- PEM export: ✅ Complete
- PKCS#8 export: ✅ Complete
- JWK export: ❌ Deferred to v0.3.0
- JWK import: ❌ Also deferred

**Error Message**:
```
NotSupportedException: JWK (JSON Web Key) export format is not supported in this version.
Use PEM or PKCS#8 format instead. JWK support is planned for v0.3.0.
```

**Files Affected**:
- `src/Core/DotCompute.Core/Security/CryptographicKeyManager.cs:595-599`

---

## 3. LINQ Extensions Limitations

### 3.1 Advanced LINQ Operations (HIGH)

**Status**: Deferred
**Target**: v0.3.0 (Q1 2026) for Join/GroupBy/OrderBy

**Description**:
Advanced LINQ operations (Join, GroupBy, OrderBy, Scan) are not yet implemented with GPU acceleration.

**Currently Supported**:
- ✅ Map (Select) - Full GPU acceleration
- ✅ Filter (Where) - Full GPU acceleration with stream compaction
- ✅ Reduce (Aggregate, Sum, Count, etc.) - Full GPU acceleration
- ⚠️ Scan (Prefix Sum) - Experimental (60% test pass rate)

**Not Supported**:
- ❌ Join (Inner, Left, Right, Full)
- ❌ GroupBy (with aggregation)
- ❌ OrderBy / ThenBy (sorting)
- ❌ Distinct / Union / Intersect / Except (set operations)
- ❌ Skip / Take / Chunk (partitioning)

**Impact**:
- Complex queries requiring these operations fall back to standard LINQ (CPU-only)
- No performance benefit for join-heavy workloads

**Workaround**:
- Use supported operations (Map/Filter/Reduce) where possible
- Standard LINQ automatically used as fallback
- Consider pre-sorting data or restructuring queries

**Performance**:
- Map/Filter/Reduce: 15-92x GPU speedup (measured)
- Advanced operations: CPU performance (no GPU acceleration)

**Technical Details**:
See `docs/LINQ_IMPLEMENTATION_PLAN.md` for detailed 24-week roadmap:
- Phase 8 (4 weeks): Join operations
- Phase 9 (4 weeks): GroupBy with aggregation
- Phase 10 (4 weeks): OrderBy sorting algorithms

---

### 3.2 Complex Lambda Expressions (HIGH)

**Status**: Partial Support
**Target**: v0.3.0 (Q1 2026)

**Description**:
Multi-statement lambdas and complex closures have limited GPU compilation support.

**Currently Supported**:
- ✅ Single-statement lambdas
- ✅ Arithmetic operations (+, -, *, /, %, etc.)
- ✅ Comparison operations (<, <=, >, >=, ==, !=)
- ✅ Logical operations (&&, ||, !)
- ✅ Math functions (Sin, Cos, Sqrt, Pow, Abs, etc.)
- ✅ Simple captured variables (constants, parameters)

**Limited Support**:
- ⚠️ Multi-statement lambdas (may fall back to CPU)
- ⚠️ Complex closure captures (reference types)
- ⚠️ Nested function calls (deep call stacks)

**Not Supported**:
- ❌ Dynamic dispatch (virtual method calls)
- ❌ Reflection-based operations
- ❌ Async/await patterns
- ❌ Exception handling (try/catch)
- ❌ LINQ to Objects complex queries

**Impact**:
- Complex expressions may trigger CPU fallback
- Performance varies based on expression complexity

**Workaround**:
- Break complex lambdas into multiple LINQ operations
- Use explicit parameters instead of closures
- Pre-compute complex values before GPU execution

**Example**:
```csharp
// ❌ Not supported (multi-statement)
data.Select(x => {
    var temp = x * 2;
    return temp + 5;
})

// ✅ Supported (single statement)
data.Select(x => x * 2 + 5)

// ✅ Supported (chained operations)
data.Select(x => x * 2)
    .Select(x => x + 5)
```

---

### 3.3 Reactive Extensions Integration (MEDIUM)

**Status**: Planned
**Target**: v0.2.1 (Q4 2025)

**Description**:
GPU-accelerated streaming compute with Rx.NET is not yet implemented.

**Impact**:
- Real-time data streams cannot be GPU-accelerated
- Windowing operations run on CPU only
- Backpressure handling not optimized for GPU

**Workaround**:
- Batch streaming data and use regular LINQ GPU acceleration
- Use standard Rx.NET (CPU-only)
- Consider buffering strategies

**Planned Features**:
- `AsComputeObservable()` extension for GPU streaming
- Adaptive batching for GPU efficiency
- Windowing operations (tumbling, sliding, time-based)
- GPU-aware backpressure handling

**Technical Details**:
See `docs/LINQ_IMPLEMENTATION_PLAN.md` Phase 7 (8 weeks)

---

## 4. Algorithm Libraries

### 4.1 Linear Algebra QR/SVD GPU Acceleration (HIGH)

**Status**: CPU Fallback
**Target**: v0.2.1 (Q4 2025)

**Description**:
QR decomposition and SVD (Singular Value Decomposition) currently use CPU fallback instead of GPU acceleration.

**Current State**:
- ✅ Matrix multiplication: Full GPU acceleration (21-92x speedup)
- ⚠️ QR decomposition: CPU fallback (Gram-Schmidt)
- ⚠️ SVD: CPU fallback (simplified power iteration)
- ✅ Transpose: CPU (async, planned GPU in v0.2.1)

**Impact**:
- Large matrix decompositions slower than optimal
- Linear solvers (QR-based) use CPU
- No performance benefit for eigenvalue problems

**Workaround**:
- Use matrix multiplication (GPU-accelerated)
- Consider external libraries (MKL, cuBLAS) for production workloads
- CPU implementation is correct, just not GPU-accelerated

**Planned Implementation** (v0.2.1):
- Householder QR decomposition on GPU
- Jacobi SVD iteration on GPU
- Consider cuBLAS/cuSOLVER integration for production performance

**Files Affected**:
- `src/Extensions/DotCompute.Algorithms/LinearAlgebra/Components/GpuMatrixOperations.cs:136-206` (QR)
- `src/Extensions/DotCompute.Algorithms/LinearAlgebra/Components/GpuMatrixOperations.cs:208-318` (SVD)

---

### 4.2 Advanced FFT Implementations (MEDIUM)

**Status**: Basic Implementation
**Target**: v0.3.0 (Q1 2026)

**Description**:
FFT (Fast Fourier Transform) has basic CPU implementation. GPU acceleration and advanced features (2D FFT, Bluestein's algorithm, etc.) are not implemented.

**Current State**:
- ✅ 1D FFT: Basic CPU implementation
- ❌ GPU-accelerated FFT
- ❌ 2D/3D FFT
- ❌ Real-to-complex FFT
- ❌ Bluestein's algorithm (arbitrary sizes)

**Impact**:
- Signal processing workloads not optimally accelerated
- Large FFT operations slower than libraries like FFTW or cuFFT

**Workaround**:
- Use external libraries (FFTW, Intel MKL, cuFFT)
- CPU implementation sufficient for small-to-medium sizes

**Planned Features**:
- GPU-accelerated Cooley-Tukey algorithm
- cuFFT integration for NVIDIA GPUs
- 2D/3D FFT support
- Optimized real-to-complex transforms

---

## 5. Plugin System

### 5.1 Plugin Security Validation (HIGH)

**Status**: Incomplete
**Target**: v0.2.1 (Q4 2025)

**Description**:
Plugin security validation is superficial. Full security scanning for malicious plugins is not implemented.

**Current State**:
- ✅ Assembly loading: Functional
- ✅ Basic validation: Name, version checks
- ⚠️ Security validation: Warning logged, not enforced
- ❌ Code signing verification: Not implemented
- ❌ Sandbox execution: Not implemented

**Impact**:
- **Security Risk**: Malicious plugins could compromise system
- **Production Risk**: Not safe for third-party plugins without review

**Workaround**:
- **Only load trusted plugins** (same development team)
- Review plugin source code before loading
- Run in isolated environment if loading untrusted code

**Warning Message**:
```
LogWarning: Plugin security validation is not fully implemented.
Exercise caution with untrusted plugins.
```

**Planned Implementation**:
- Strong name / code signing verification
- Assembly scanning for dangerous patterns
- Sandbox execution environment
- Capability-based security model

**Files Affected**:
- `src/Extensions/DotCompute.Algorithms/Management/AlgorithmPluginValidator.cs:26` (commented out)
- `src/Extensions/DotCompute.Algorithms/Management/AlgorithmPluginValidator.cs:59-63` (placeholder)

**Priority Rationale**:
- **Critical** for production use with third-party plugins
- Current workaround (trusted plugins only) acceptable for v0.2.0
- Full implementation required before public plugin ecosystem

---

## 6. Profiling and Telemetry

### 6.1 CUPTI Profiling Support (MEDIUM)

**Status**: Incomplete
**Target**: v0.3.0 (Q1 2026)

**Description**:
CUDA CUPTI (CUDA Profiling Tools Interface) initialization and profiler session management is incomplete.

**Current State**:
- ✅ Basic CUDA profiling: Kernel timings available
- ❌ CUPTI initialization: Throws NotImplementedException
- ❌ Advanced metrics: Hardware counters not exposed
- ❌ Profiler sessions: Not implemented

**Impact**:
- Cannot collect detailed GPU performance metrics
- Hardware counter data (memory bandwidth, SM occupancy, etc.) unavailable
- Profiling tools (Nsight Systems, Nsight Compute) integration limited

**Workaround**:
- Use CUDA events for basic kernel timing (available)
- Use external profiling tools (nvprof, Nsight Systems)
- Manual instrumentation where needed

**Planned Features**:
- CUPTI initialization and shutdown
- Hardware counter collection
- Profiler session management
- Integration with Nsight tools

**Files Affected**:
- `src/Backends/DotCompute.Backends.CUDA/Profiling/CuptiWrapper.cs:101` (initialization)
- `src/Backends/DotCompute.Backends.CUDA/Profiling/CuptiWrapper.cs:251` (sessions)

---

## 7. Deprecation Timeline

### 7.1 CudaMemoryBufferView (Deprecated in v0.2.0)

**Status**: `[Obsolete]` and `internal`
**Removal**: v0.3.0 (Q1 2026)

**Description**:
`CudaMemoryBufferView` and `CudaMemoryBufferView<T>` are internal implementation details marked obsolete in v0.2.0.

**Impact**:
- Internal classes, no public API impact
- Users should use `IUnifiedMemoryBuffer` through `IMemoryManager`

**Migration**:
See `docs/BUFFER_VIEW_MIGRATION.md` for complete guide.

---

## 8. Performance Considerations

### 8.1 Small Data Transfer Overhead

**Issue**: GPU overhead dominates for small datasets (< 10,000 elements)

**Impact**:
- CPU SIMD may be faster than GPU for small workloads
- Transfer overhead: ~50-200µs per operation

**Mitigation**:
- ✅ Automatic backend selection (checks size threshold)
- ✅ Batch multiple operations to amortize overhead
- ✅ Use kernel fusion to reduce transfers

**Threshold**: 10,000 elements (configurable)

---

### 8.2 Kernel Compilation Cold Start

**Issue**: First kernel compilation takes 50-500ms

**Impact**:
- Initial query execution slower (one-time cost)
- JIT compilation overhead

**Mitigation**:
- ✅ Kernel caching (subsequent calls fast)
- ✅ Persistent cache across sessions (planned v0.2.1)
- ⏳ AOT compilation option (v0.3.0)

---

## 9. Platform-Specific Issues

### 9.1 macOS Metal Backend

**Platform**: macOS 10.15+, iOS 13+
**Status**: 60% complete (MSL compilation incomplete)
**Target**: v0.3.0

See Section 1.1 for details.

---

### 9.2 Linux OpenCL on NVIDIA

**Platform**: Linux with NVIDIA GPUs
**Issue**: Some distributions require manual OpenCL ICD setup

**Workaround**:
```bash
# Install NVIDIA OpenCL ICD
sudo apt-get install nvidia-opencl-icd-xxx  # xxx = driver version

# Or use CUDA backend (recommended on NVIDIA)
```

**Impact**: Minimal (CUDA backend recommended for NVIDIA GPUs)

---

### 9.3 Windows ARM64

**Platform**: Windows ARM64 (Surface Pro X, etc.)
**Status**: Not tested
**Expected**: CPU SIMD should work, GPU backends may not

**Recommendation**: Test on target hardware, provide feedback

---

## 10. Documentation Gaps

### 10.1 Performance Tuning Guide

**Status**: Planned
**Target**: v0.2.1

Missing comprehensive guide for:
- Backend selection tuning
- Memory pool configuration
- Kernel fusion strategies
- Profiling and optimization

**Workaround**: See individual backend guides and API documentation

---

### 10.2 Migration Guide (v0.1.x → v0.2.0)

**Status**: Partial
**Target**: v0.2.1

Specific migration guides exist:
- ✅ BUFFER_VIEW_MIGRATION.md (buffer views)
- ⏳ Comprehensive migration guide (planned)

---

## 11. Testing Coverage

### 11.1 Hardware Test Coverage

**Current Coverage**:
- NVIDIA RTX 2000 Ada: ✅ Full testing
- Intel/AMD CPUs (AVX2/AVX512): ✅ Full testing
- Other GPUs: ⚠️ Community testing needed

**Missing Coverage**:
- AMD Radeon GPUs (OpenCL)
- Intel Arc GPUs (OpenCL)
- Apple Silicon (Metal - backend incomplete)
- ARM Mali / Qualcomm Adreno (OpenCL mobile)

**Impact**: Potential hardware-specific issues undiscovered

**Contribution Welcome**: Test reports from various hardware configurations

---

### 11.2 Stress Testing

**Status**: Basic stress tests
**Target**: v0.3.0

**Current**:
- ✅ Functional correctness tests
- ✅ Basic performance benchmarks
- ⏳ Extended stress testing (memory leaks, long-running operations)
- ⏳ Chaos engineering (error injection, fault tolerance)

---

## 12. Contributing

Found a limitation not listed here? Want to help resolve one?

1. **Report Issues**: https://github.com/mivertowski/DotCompute/issues
2. **Contribute Fixes**: See [CONTRIBUTING.md](../CONTRIBUTING.md)
3. **Request Features**: Use GitHub Discussions

---

## 13. Version Roadmap

### v0.2.1 (Q4 2025)
- ✅ Plugin security validation
- ✅ Linear algebra QR/SVD GPU acceleration
- ✅ Persistent kernel cache
- ✅ Reactive Extensions integration (Phase 7)

### v0.3.0 (Q1 2026)
- ✅ Metal backend completion (MSL compilation)
- ✅ PKCS#12 and JWK key export formats
- ✅ Complex lambda expressions support
- ✅ LINQ Join/GroupBy/OrderBy operations
- ✅ CUPTI profiling support
- ✅ Remove CudaMemoryBufferView (deprecated)

### v0.4.0+ (Q3 2026+)
- ✅ ROCm backend (AMD GPU support)
- ✅ ML-based backend selection
- ✅ Advanced FFT implementations
- ✅ Extended testing and hardening

---

## Appendix: Quick Reference

### Feature Status Legend
- ✅ Complete and production-ready
- 🚧 In development (percentage shown)
- ⚠️ Partial support / workaround available
- ❌ Not implemented / deferred
- ⏳ Planned for future version

### Severity Levels
- **CRITICAL**: Blocks production use for intended scenarios
- **HIGH**: Significant limitation, workaround available
- **MEDIUM**: Minor limitation, alternative approaches exist
- **LOW**: Nice-to-have, minimal impact

---

**Last Updated**: November 2025
**Document Version**: 1.0
**For**: DotCompute v0.2.0-alpha
