# Changelog

All notable changes to DotCompute will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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