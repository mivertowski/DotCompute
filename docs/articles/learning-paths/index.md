# Learning Paths

Choose your learning path based on your experience level and goals. Each path provides a structured progression through DotCompute's features with hands-on examples.

## Path Overview

| Path | Target Audience | Duration | Prerequisites |
|------|-----------------|----------|---------------|
| [Beginner](beginner/index.md) | New to GPU computing | 2-4 hours | C# fundamentals |
| [Intermediate](intermediate/index.md) | Familiar with parallel programming | 4-6 hours | Beginner path or equivalent |
| [Advanced](advanced/index.md) | Building production systems | 6-8 hours | Intermediate path |
| [Contributor](contributor/index.md) | Contributing to DotCompute | 4-6 hours | Advanced path |

## Beginner Path

**Goal**: Understand GPU computing fundamentals and write your first kernel.

1. [Introduction to GPU Computing](beginner/gpu-computing-intro.md) - CPU vs GPU architecture, when to use GPU acceleration
2. [Your First Kernel](beginner/first-kernel.md) - Create and execute a simple vector addition kernel
3. [Memory Fundamentals](beginner/memory-fundamentals.md) - Understanding buffers, transfers, and memory spaces
4. [Backend Selection](beginner/backend-selection.md) - Choosing between CPU, CUDA, OpenCL, and Metal

**Outcome**: Ability to write basic GPU-accelerated computations.

## Intermediate Path

**Goal**: Build efficient GPU applications with proper resource management.

1. [Memory Optimization](intermediate/memory-optimization.md) - Pooling, unified memory, and allocation strategies
2. [Kernel Performance](intermediate/kernel-performance.md) - Thread organization, occupancy, and profiling
3. [Multi-Kernel Pipelines](intermediate/multi-kernel-pipelines.md) - Chaining kernels and managing data flow
4. [Error Handling](intermediate/error-handling.md) - Debugging GPU code and handling failures gracefully

**Outcome**: Ability to build production-quality GPU applications.

## Advanced Path

**Goal**: Master advanced GPU programming patterns and Ring Kernels.

1. [Ring Kernel Fundamentals](advanced/ring-kernel-fundamentals.md) - Persistent GPU computation with actor-style messaging
2. [Synchronization Patterns](advanced/synchronization-patterns.md) - Barriers, memory ordering, and coordination
3. [Multi-GPU Programming](advanced/multi-gpu-programming.md) - P2P transfers, load balancing, and scaling
4. [Performance Profiling](advanced/performance-profiling.md) - GPU timing API, bottleneck analysis, and optimization

**Outcome**: Ability to build high-performance distributed GPU systems.

## Contributor Path

**Goal**: Contribute to the DotCompute framework effectively.

1. [Architecture Deep Dive](contributor/architecture-deep-dive.md) - Core abstractions, backend integration, and extensibility
2. [Source Generator Development](contributor/source-generators.md) - Building compile-time code generation
3. [Analyzer Development](contributor/analyzers.md) - Creating Roslyn analyzers for GPU code
4. [Testing and Benchmarking](contributor/testing-benchmarking.md) - Test patterns, hardware tests, and performance validation

**Outcome**: Ability to extend and improve the DotCompute framework.

## Quick Assessment

Not sure which path to start with? Answer these questions:

**Do you understand what a GPU thread block is?**
- No → Start with [Beginner Path](beginner/index.md)
- Yes → Continue below

**Have you written GPU kernels before (CUDA, OpenCL, Metal)?**
- No → Start with [Beginner Path](beginner/index.md)
- Yes → Continue below

**Are you comfortable with memory coalescing and occupancy optimization?**
- No → Start with [Intermediate Path](intermediate/index.md)
- Yes → Continue below

**Have you implemented persistent GPU kernels or actor models?**
- No → Start with [Advanced Path](advanced/index.md)
- Yes → [Advanced Path](advanced/index.md) or [Contributor Path](contributor/index.md)

## Learning Resources

### Prerequisites

- **C# Fundamentals**: Variables, classes, methods, async/await
- **Basic Parallel Programming**: Understanding of threads and synchronization
- **.NET Development**: Experience with .NET CLI and project structure

### Recommended Tools

- Visual Studio 2022 or VS Code with C# extension
- .NET 9.0 SDK
- CUDA Toolkit (for NVIDIA GPU testing)
- BenchmarkDotNet (for performance measurement)

### Sample Code

All learning paths include working sample code in the `/samples` directory:

```
samples/
├── Learning/
│   ├── Beginner/
│   ├── Intermediate/
│   └── Advanced/
└── RingKernels/
    ├── VectorAdd/
    └── PageRank/
```

---

*Choose your path and begin your GPU computing journey with DotCompute.*
