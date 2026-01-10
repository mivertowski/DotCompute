# DotCompute Roadmap

**Current Version**: v0.5.3 (January 2026)
**Status**: Production-Ready, Code Quality Release
**Next Release**: v0.6.0 (Q1 2026)

---

## Vision

DotCompute aims to be the premier universal compute framework for .NET, providing:
- **Zero-configuration GPU acceleration** across NVIDIA, AMD, Intel, Apple, and ARM devices
- **Production-grade reliability** with comprehensive testing and error handling
- **Developer-friendly APIs** that feel natural to .NET developers
- **Native AOT compatibility** for minimal startup times and deployment simplicity

---

## Release Strategy

### Semantic Versioning
- **Major (x.0.0)**: Breaking API changes, architectural shifts
- **Minor (0.x.0)**: New features, deferred functionality, backward-compatible
- **Patch (0.0.x)**: Bug fixes, performance improvements, no new features

### Release Cadence
- **Alpha**: Feature development (monthly updates)
- **Beta**: Stabilization and testing (bi-monthly)
- **Stable**: Production-ready (quarterly)

---

## v0.5.x Series (November 2025 - January 2026) - CURRENT

### v0.5.3 (January 2026) - Code Quality & Documentation

**Code Quality Improvements**:
- GeneratedRegex migration for all regex patterns (SYSLIB1045)
- Fixed reserved exception types (CA2201) across memory management
- Removed 17+ obsolete NoWarn suppressions
- Pristine build: Only 1 documented suppression (CA1873 for LoggerMessage)

**Documentation Overhaul**:
- Comprehensive version alignment to v0.5.3
- Metal backend status: Feature-Complete
- Roadmap reconciliation (marked completed items)
- Cleaned up stray files from repository

### v0.5.2 (December 2025) - GPU Atomics & Quality Build

**GPU Atomic Operations (New)**:
- AtomicAdd, AtomicSub, AtomicExchange, AtomicCompareExchange
- AtomicMin, AtomicMax, AtomicAnd, AtomicOr, AtomicXor
- Memory ordering (Relaxed, Acquire, Release, AcquireRelease, SequentiallyConsistent)
- ThreadFence for Workgroup, Device, and System scopes
- Cross-backend compilation (CUDA, OpenCL, Metal, CPU)

**Quality Build**:
- Zero warnings across entire codebase (49 resolved)
- Dependency updates (NuGet 7.0.1, MemoryPack 1.21.4, CodeAnalysis 5.0.0)
- CUDA 13 support for CC 8.9 (RTX 2000 Ada)

### v0.5.1 (November 2025) - Build System & Developer Experience

- CLI tool isolation for CUDA code generation
- #nullable enable in generated code
- Compile-time backend detection
- Transitive generator activation prevention
- Telemetry analyzer suppression attribute

### v0.5.0 (November 2025) - First Stable Release

**Core Infrastructure (Production Ready)**:
- CPU Backend with SIMD (AVX2/AVX512/NEON) - 3.7x measured speedup
- CUDA Backend (CC 5.0-8.9, tested on RTX 2000 Ada) - 21-92x measured speedup
- Memory Management with pooling (90% allocation reduction)
- Native AOT support (sub-10ms startup)

**Ring Kernel System (Complete)**:
- Phase 1: MemoryPack Integration (43/43 tests)
- Phase 2: CPU Ring Kernel Implementation (43/43 tests)
- Phase 3: CUDA Ring Kernel Implementation (115/122 tests - 94.3%)
- Phase 4: Multi-GPU Coordination Infrastructure
- Phase 5: Performance & Observability (94/94 tests - 100%)
  - 5.1: Performance Profiling & Optimization
  - 5.2: Advanced Messaging Patterns
  - 5.3: Fault Tolerance & Resilience
  - 5.4: Observability & Distributed Tracing (OpenTelemetry, Prometheus, Health Checks)

**Developer Tooling (Production Ready)**:
- Source Generators with [Kernel] attribute
- Roslyn Analyzers (12 rules, 5 automated fixes)
- Cross-Backend Debugging (CPU vs GPU validation)
- Adaptive Backend Selection (ML-powered optimization)

**LINQ Extensions (80% Complete)**:
- GPU Kernel Generation (CUDA, OpenCL, Metal)
- Query Provider Integration (transparent GPU execution)
- Automatic Backend Selection
- Expression Compilation Pipeline
- Kernel Fusion Optimization (50-80% bandwidth reduction)
- Filter Compaction (atomic stream compaction)
- Integration Testing (43/54 passing, 80%)
- *Deferred*: Join, GroupBy, OrderBy operations

**Backend Status**:
- ✅ CPU Backend (Production) - AVX2/AVX512/NEON SIMD
- ✅ CUDA Backend (Production) - CC 5.0-8.9, P2P, Ring Kernels
- ✅ Metal Backend (Feature-Complete) - MPS, MSL translation, memory pooling, Ring Kernels
- ⚠️ OpenCL Backend (Experimental) - NVIDIA, AMD, Intel, ARM Mali, Qualcomm Adreno

**Documentation**:
- GitHub Pages site (3,237+ pages)
- 27+ comprehensive guides
- Complete API reference
- Learning paths for beginners and advanced users

### What's Deferred to Future Releases

- LINQ advanced operations (Join, GroupBy, OrderBy)
- ROCm Backend (AMD GPU support)
- PKCS#12 and JWK key export formats
- Linear algebra QR/SVD GPU acceleration
- CUPTI profiling support

---

## v0.6.0 (Q1 2026) - PLANNED

**Theme**: Feature Completion & Hardening

### Major Features (Planned)

#### 1. Reactive Extensions Integration (Phase 7)
*Status: Design complete, implementation planned*

- `AsComputeObservable()` for GPU-accelerated streaming
- Adaptive batching for GPU efficiency
- Windowing operations (tumbling, sliding, time-based)
- Backpressure handling strategies
- Integration with Rx.NET ecosystem

#### 2. LINQ Advanced Operations (Phases 8-10)

**Phase 8 - Join Operations**:
- Inner join, Left/right/full outer join, Cross join
- GPU hash join algorithm
- Optimized for large datasets

**Phase 9 - GroupBy Aggregation**:
- Group by single/multiple keys
- Aggregation functions (Sum, Count, Min, Max, Average)
- GPU parallel grouping

**Phase 10 - OrderBy Sorting**:
- Single-key and multi-key sorting (ThenBy)
- GPU parallel sorting algorithms

#### 3. Linear Algebra GPU Acceleration

- Householder QR decomposition on GPU
- Jacobi SVD iteration on GPU
- GPU-accelerated matrix transpose
- Consider cuBLAS/cuSOLVER integration

#### 4. Plugin Security Validation

- Strong name / code signing verification
- Assembly scanning for dangerous patterns
- Sandbox execution environment
- Capability-based security model

### Minor Features (Planned)

- Enhanced error messages with suggestions
- Performance tuning guide documentation
- Extended hardware test coverage
- Memory leak stress testing
- Persistent kernel cache (cross-session caching)

---

## v0.7.0 (Q2-Q3 2026) - FUTURE

**Theme**: Backend Expansion

### Potential Features

#### 1. Metal Backend Enhancement

- ✅ MSL (Metal Shading Language) C# translation (COMPLETED in v0.5.3)
- Performance optimization for Apple Silicon
- iOS/iPadOS support
- Enhanced Metal Performance Shaders integration

#### 2. ROCm Backend (AMD GPU Support)

- HIP runtime integration
- ROCm kernel compilation
- Memory management optimized for AMD architecture
- Performance parity with CUDA backend

#### 3. CUPTI Profiling Support

- CUPTI initialization and shutdown
- Hardware counter collection
- Profiler session management
- Integration with Nsight tools

---

## v1.0.0 (2027) - FUTURE VISION

**Theme**: Enterprise and Cloud Ready

### Goals for v1.0

**Stability Requirements**:
- Core backends (CPU, CUDA) fully stable
- OpenCL and Metal backends production-ready
- 90%+ test coverage
- API stabilized (no more breaking changes)
- Production use by 1000+ applications

**Potential Features** (Subject to Community Feedback):

1. **Cloud Integration**
   - Azure GPU compute integration
   - AWS EC2 GPU instances
   - Google Cloud TPU support
   - Kubernetes orchestration

2. **Language Interoperability**
   - C/C++ interop improvements
   - Python bindings (via PythonNET)
   - F# optimizations

3. **Domain-Specific Libraries**
   - Machine Learning primitives
   - Computer Vision kernels
   - Scientific computing algorithms

4. **Advanced Memory Management**
   - Unified memory automation
   - Multi-tier memory hierarchy
   - NVLINK/P2P optimization

---

## Long-Term Goals (2027+)

### Research & Innovation Areas

1. **Automatic Kernel Optimization**
   - AI-driven kernel tuning
   - Architecture-specific optimizations
   - Learned performance models

2. **Distributed Computing**
   - Multi-GPU coordination
   - Node-to-node communication
   - Distributed memory management
   - Fault tolerance and recovery

3. **Energy Efficiency**
   - Power-aware scheduling
   - Carbon footprint tracking
   - Green computing optimizations

---

## Community Priorities

The roadmap is influenced by community feedback. Current top requests:

1. **LINQ Advanced Operations** (v0.6.0) - Required for complex data processing
2. **Metal Backend Completion** (v0.7.0) - High demand from macOS developers
3. **ROCm Backend** (v0.7.0) - AMD GPU users requesting support
4. **Distributed Computing** (v1.0+) - Enterprise multi-GPU scenarios
5. **Python Bindings** (v1.0+) - Data science community interest

**Vote on Features**: https://github.com/mivertowski/DotCompute/discussions

---

## Contribution Opportunities

Want to accelerate a feature? Here's how to contribute:

### High-Impact Areas

1. **LINQ Advanced Operations** (v0.6.0)
   - Join algorithm implementation
   - GroupBy GPU kernels
   - OrderBy sorting algorithms

2. **Metal Backend** (v0.7.0)
   - MSL code generation
   - Performance benchmarking
   - iOS testing

3. **ROCm Backend** (v0.7.0)
   - HIP runtime bindings
   - Kernel compiler
   - AMD GPU testing

4. **Testing & Documentation**
   - Hardware test reports
   - Performance benchmarks
   - Tutorial videos
   - Sample applications

### Getting Started

1. Review [CONTRIBUTING.md](./CONTRIBUTING.md)
2. Check [Good First Issues](https://github.com/mivertowski/DotCompute/labels/good%20first%20issue)
3. Review [Architecture Guide](./docs/guides/architecture.md)

---

## Breaking Changes Policy

### Major Version (x.0.0)
- API redesigns allowed
- Namespace changes permitted
- Behavioral changes documented

### Minor Version (0.x.0)
- Backward-compatible only
- Deprecations announced 2 versions ahead
- Migration guides provided

### Patch Version (0.0.x)
- Bug fixes only
- Performance improvements
- No API changes

---

## Success Metrics

### v0.5.0 Goals - ACHIEVED
- 75%+ test coverage - **ACHIEVED** (80%+ across components)
- 3x+ CPU SIMD speedup - **ACHIEVED** (3.7x measured)
- 20x+ GPU speedup (CUDA) - **ACHIEVED** (21-92x measured)
- Sub-10ms Native AOT startup - **ACHIEVED**
- Phase 5 Observability complete - **ACHIEVED** (94 tests)

### v0.6.0 Goals
- 85%+ test coverage
- LINQ Phase 8-10 complete (Join, GroupBy, OrderBy)
- Rx.NET integration
- 100+ production users

### v1.0.0 Goals
- 90%+ test coverage
- All backends production-ready
- 1000+ production users
- API stable (no breaking changes)

---

## Versioning History

| Version | Release Date | Highlights |
|---------|-------------|------------|
| **v0.5.3** | January 2026 | **Code Quality**: GeneratedRegex, CA2201 fixes, 17+ NoWarn removals, documentation overhaul |
| v0.5.2 | December 2025 | **GPU Atomics**: AtomicAdd/CAS/Min/Max/And/Or/Xor, ThreadFence, zero warnings |
| v0.5.1 | November 2025 | **Build System**: CLI isolation, nullable enable, backend detection |
| v0.5.0 | November 2025 | **First Stable Release**: Phase 5 Observability complete, Ring Kernels production-ready |
| v0.4.2-rc2 | November 2025 | Timing, Barrier, Memory Ordering APIs complete |
| v0.4.1-rc2 | November 2025 | DI fixes, documentation overhaul |
| v0.4.0-rc2 | January 2025 | Metal backend, device observability, LINQ Phase 6 |
| v0.3.0-rc1 | January 2025 | AcceleratorInfo properties, standalone usage |
| v0.2.0-alpha | January 2025 | Initial release, CPU/CUDA/OpenCL backends |
| v0.1.0-alpha | December 2024 | Proof of concept |

---

## FAQ

### When is v1.0?

**v1.0** will be released when:
- Core backends (CPU, CUDA) are fully stable
- OpenCL and Metal backends are production-ready
- LINQ extensions support all major operations
- Comprehensive testing (90%+ coverage)
- API stabilized (no more breaking changes)

**Estimated**: 2027

### Will there be LTS releases?

**Yes**, starting with v1.0:
- LTS releases supported for 2 years
- Security patches and critical bug fixes
- No new features in LTS branch

### How do I request a feature?

1. Check existing [GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)
2. Search [Issues](https://github.com/mivertowski/DotCompute/issues)
3. Create new discussion with use case, expected behavior, and priority

---

## Contact & Feedback

- **GitHub Issues**: https://github.com/mivertowski/DotCompute/issues
- **Discussions**: https://github.com/mivertowski/DotCompute/discussions
- **Documentation**: https://mivertowski.github.io/DotCompute/

---

**Last Updated**: January 2026
**Next Review**: Q1 2026 (v0.6.0 release planning)
