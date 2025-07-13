# DotCompute Wiki

Welcome to the comprehensive DotCompute documentation! This wiki provides in-depth guides, tutorials, and reference materials for building high-performance compute applications with .NET 9's Native AOT.

## 🚀 Quick Navigation

### 📚 **Getting Started**
- **[Installation Guide](Installation-Guide)** - Set up DotCompute in your project
- **[Your First Kernel](Your-First-Kernel)** - Write and execute your first compute kernel
- **[Hello World Tutorial](Hello-World-Tutorial)** - Complete step-by-step example
- **[Migration Guide](Migration-Guide)** - Migrate from other compute frameworks

### 🏗️ **Architecture & Design**
- **[System Architecture](Architecture)** - High-level system design and components
- **[Memory Management](Memory-Management)** - Unified buffer system and optimization
- **[Kernel System](Kernel-System)** - Kernel compilation and execution pipeline
- **[Backend Plugins](Backend-Plugins)** - CPU, CUDA, Metal, and Vulkan backends

### ⚡ **Performance & Optimization**
- **[Performance Guide](Performance-Guide)** - Optimization strategies and best practices
- **[Benchmarking](Benchmarking)** - Performance measurement and analysis
- **[SIMD Vectorization](SIMD-Vectorization)** - CPU vectorization with AVX/NEON
- **[Memory Optimization](Memory-Optimization)** - Zero-copy operations and pooling

### 🔧 **API Reference**
- **[Core API](API-Core)** - DotCompute.Core interfaces and classes
- **[Memory API](API-Memory)** - Memory management and buffer operations
- **[CPU Backend API](API-CPU-Backend)** - CPU-specific acceleration features
- **[Kernel Attributes](API-Kernel-Attributes)** - Kernel definition and metadata

### 💡 **Examples & Tutorials**
- **[Real-World Examples](Examples)** - Production-ready compute scenarios
- **[Linear Algebra](Examples-Linear-Algebra)** - Matrix operations and algorithms
- **[Image Processing](Examples-Image-Processing)** - Computer vision and graphics
- **[Machine Learning](Examples-Machine-Learning)** - ML primitives and operations

### 🛠️ **Development**
- **[Contributing](Contributing)** - How to contribute to DotCompute
- **[Building from Source](Building-from-Source)** - Development environment setup
- **[Testing Guide](Testing-Guide)** - Writing and running tests
- **[Debugging](Debugging)** - Troubleshooting and performance analysis

### 📋 **Reference**
- **[Supported Platforms](Supported-Platforms)** - Compatibility matrix
- **[Backend Comparison](Backend-Comparison)** - Choose the right backend
- **[FAQ](FAQ)** - Frequently asked questions
- **[Troubleshooting](Troubleshooting)** - Common issues and solutions

## 🎯 **Featured Guides**

### 🚀 **For Beginners**
Start with our **[Getting Started](Getting-Started)** guide to set up DotCompute and run your first kernel in under 5 minutes.

### ⚡ **For Performance Enthusiasts**
Dive into **[SIMD Vectorization](SIMD-Vectorization)** to achieve 4-16x speedups with CPU backends.

### 🏗️ **For Architects**
Explore **[System Architecture](Architecture)** to understand DotCompute's design principles and extensibility.

### 🔬 **For Researchers**
Check out **[Performance Guide](Performance-Guide)** for detailed benchmarking and optimization strategies.

## 📊 **Current Status**

| Component | Status | Documentation |
|-----------|--------|---------------|
| Core Framework | ✅ **Stable** | [Complete](API-Core) |
| CPU Backend | ✅ **Stable (Phase 3)** | [Complete](API-CPU-Backend) |
| CUDA Backend | ✅ **Stable (Phase 3)** | [Complete](Backend-CUDA) |
| Metal Backend | ✅ **Stable (Phase 3)** | [Complete](Backend-Metal) |
| Source Generators | ✅ **Stable (Phase 3)** | [Complete](Source-Generators) |
| Plugin System | ✅ **Stable (Phase 3)** | [Complete](Plugin-System) |
| Memory System | ✅ **Stable** | [Complete](Memory-Management) |
| LINQ Provider | 🚧 **Planned (Phase 4)** | [Coming Soon](LINQ-Provider) |

## 🤝 **Community & Support**

- **[GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)** - Community Q&A and feature discussions
- **[Issues](https://github.com/mivertowski/DotCompute/issues)** - Bug reports and feature requests
- **[Contributing Guide](Contributing)** - Join the development effort
- **[Code of Conduct](Code-of-Conduct)** - Community guidelines

## 📈 **Latest Updates**

### 🎉 Phase 3: GPU Backends & Source Generators (COMPLETE)
- ✅ **CUDA Backend** - Production-ready GPU acceleration with 8-100x speedups
- ✅ **Metal Backend** - Apple Silicon optimization for M1/M2/M3 processors
- ✅ **Source Generators** - Incremental compilation with multi-backend code generation
- ✅ **Plugin System** - Hot-reload capable architecture with assembly isolation
- ✅ **Kernel Pipelines** - Multi-stage workflow orchestration with optimization
- ✅ **95%+ Test Coverage** - Comprehensive validation across all components

### Performance Highlights
- **🚀 GPU Acceleration**: 8-100x speedups validated across CUDA and Metal
- **⚡ SIMD Optimization**: 4-16x CPU performance with AVX512/NEON
- **💾 Memory Efficiency**: 95% bandwidth utilization with unified buffer system
- **🔧 Build Performance**: 80% faster compilation with incremental source generation

### Recent Major Additions
- **[GPU Backends](Backend-CUDA)** - Full CUDA and Metal acceleration support
- **[Source Generation](Source-Generators)** - Compile-time kernel optimization
- **[Plugin Architecture](Plugin-System)** - Extensible backend ecosystem
- **[Pipeline Framework](Pipeline-Framework)** - Advanced workflow orchestration

## 🔍 **Search Tips**

Use the search bar above to quickly find:
- **API methods**: Search for class or method names
- **Concepts**: Search for "memory pooling", "kernel fusion", etc.
- **Examples**: Search for "matrix multiplication", "vector addition", etc.
- **Troubleshooting**: Search for error messages or symptoms

## 📚 **External Resources**

- **[.NET 9 Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)** - Microsoft's AOT documentation
- **[SIMD in .NET](https://devblogs.microsoft.com/dotnet/using-net-hardware-intrinsics-api-to-accelerate-machine-learning-scenarios/)** - Hardware intrinsics guide
- **[High-Performance .NET](https://github.com/ben-watson/high-performance-dotnet)** - Performance optimization techniques

---

💡 **Can't find what you're looking for?** 
- Check our **[FAQ](FAQ)** for common questions
- Search **[GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)** for community answers
- Open a **[new discussion](https://github.com/mivertowski/DotCompute/discussions/new)** for help

**Happy computing with DotCompute!** 🚀