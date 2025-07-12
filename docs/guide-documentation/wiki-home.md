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
| CPU Backend | ✅ **Stable** | [Complete](API-CPU-Backend) |
| Memory System | 🔄 **In Progress** | [Draft](Memory-Management) |
| CUDA Backend | 🚧 **Planned** | [Coming Soon](Backend-CUDA) |
| Metal Backend | 🚧 **Planned** | [Coming Soon](Backend-Metal) |
| LINQ Provider | 🚧 **Planned** | [Coming Soon](LINQ-Provider) |

## 🤝 **Community & Support**

- **[GitHub Discussions](https://github.com/mivertowski/DotCompute/discussions)** - Community Q&A and feature discussions
- **[Issues](https://github.com/mivertowski/DotCompute/issues)** - Bug reports and feature requests
- **[Contributing Guide](Contributing)** - Join the development effort
- **[Code of Conduct](Code-of-Conduct)** - Community guidelines

## 📈 **Latest Updates**

### Phase 2: Memory System & CPU Backend (In Progress)
- ✅ UnifiedBuffer<T> implementation with lazy transfer optimization
- ✅ CPU backend foundation with SIMD detection (AVX512, AVX2, NEON)
- 🔄 Memory pooling system for 90% allocation reduction
- 🔄 Performance benchmarking suite with BenchmarkDotNet

### Recent Additions
- **[Memory Architecture](Memory-Management)** - Deep dive into unified buffer system
- **[CPU Vectorization](SIMD-Vectorization)** - SIMD optimization techniques
- **[Performance Benchmarks](Benchmarking)** - Latest performance measurements

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