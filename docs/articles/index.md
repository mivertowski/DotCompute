# DotCompute Articles

Comprehensive guides, tutorials, and conceptual documentation for DotCompute.

## Getting Started

- **[Installation Guide](getting-started.md)** - Install DotCompute and configure your first project
- **[Quick Start Tutorial](quick-start.md)** - Write your first GPU-accelerated kernel in 5 minutes

## Architecture

Understanding DotCompute's design and component relationships:

- **[Architecture Overview](architecture/overview.md)** - High-level system architecture
- **[Core Orchestration](architecture/core-orchestration.md)** - Compute orchestration and service integration
- **[Backend Integration](architecture/backend-integration.md)** - How backends plug into the framework
- **[Backends](architecture/backends.md)** - Backend comparison and details
- **[Memory Management](architecture/memory-management.md)** - Unified memory system design
- **[Debugging System](architecture/debugging-system.md)** - Cross-backend validation architecture
- **[Optimization Engine](architecture/optimization-engine.md)** - Adaptive backend selection design
- **[Source Generators](architecture/source-generators.md)** - Compile-time code generation system
- **[Analyzers](architecture/analyzers.md)** - Roslyn analyzer architecture
- **[Runtime](architecture/runtime.md)** - Runtime services architecture
- **[Metal Backend Architecture](architecture/metal-backend-architecture.md)** - Apple Metal backend design

## Developer Guides

Practical guides for common development scenarios:

- **[Kernel Development](guides/kernel-development.md)** - Writing efficient compute kernels
- **[Performance Tuning](guides/performance-tuning.md)** - Optimizing kernel performance
- **[Debugging Guide](guides/debugging-guide.md)** - Debugging kernels and troubleshooting issues
- **[Native AOT Compilation](guides/native-aot.md)** - Building with Native AOT for maximum performance
- **[Multi-GPU Programming](guides/multi-gpu.md)** - Scaling across multiple GPUs
- **[Memory Management Best Practices](guides/memory-management.md)** - Efficient memory usage patterns
- **[Dependency Injection Integration](guides/dependency-injection.md)** - Using DotCompute with DI containers
- **[Timing API Guide](guides/timing-api.md)** - High-precision GPU timestamps with CPU-GPU clock synchronization
- **[Device Reset Guide](guides/device-reset.md)** - Device reset and recovery procedures
- **[Performance Profiling](guides/performance-profiling.md)** - Performance profiling and bottleneck analysis
- **[Troubleshooting Common Issues](guides/troubleshooting.md)** - Solutions to common problems
- **[WSL2 Limitations](guides/wsl2-limitations.md)** - Known WSL2 GPU memory limitations
- **[LINQ GPU Acceleration](guides/linq-gpu-acceleration.md)** - GPU-accelerated LINQ queries
- **[Metal Backend Guide](guides/metal-backend.md)** - Apple Metal backend usage
- **[Orleans Integration](guides/orleans-integration.md)** - Distributed system integration patterns

## Ring Kernels

Persistent GPU computation with actor-style messaging:

- **[Ring Kernels Overview](guides/ring-kernels/index.md)** - Introduction to Ring Kernels
- **[Architecture](guides/ring-kernels/overview.md)** - Ring Kernel system design
- **[Messaging & Telemetry](guides/ring-kernels/messaging-telemetry.md)** - Message passing and observability
- **[Advanced Topics](guides/ring-kernels/advanced.md)** - Advanced Ring Kernel patterns
- **[Memory Ordering](guides/ring-kernels/memory-ordering.md)** - Memory ordering in Ring Kernels
- **[Migration Guide](guides/ring-kernels/migration.md)** - Migrating Ring Kernel code

## Advanced Topics

Deep dives into specialized areas:

- **[Barriers and Memory Ordering](advanced/barriers-and-memory-ordering.md)** - GPU synchronization primitives
- **[GPU Kernel Generation](advanced/gpu-kernel-generation.md)** - Backend-specific code generation

## Performance

Understanding and optimizing performance:

- **[Benchmarking Guide](performance/benchmarking.md)** - How to benchmark your kernels

## Examples

Practical examples demonstrating DotCompute features:

- **[Examples Overview](examples/index.md)** - Index of all examples
- **[Working Reference](examples/WORKING_REFERENCE.md)** - Tested, working code patterns
- **[Basic Vector Operations](examples/basic-vector-operations.md)** - Vector addition and element-wise operations
- **[Matrix Operations](examples/matrix-operations.md)** - Matrix multiplication and transformations
- **[Image Processing](examples/image-processing.md)** - GPU-accelerated image filters
- **[Multi-Kernel Pipelines](examples/multi-kernel-pipelines.md)** - Chaining multiple kernels

## Learning Paths

Structured learning progressions:

- **[Learning Paths Overview](learning-paths/index.md)** - Choose your path
- **[Beginner Path](learning-paths/beginner/index.md)** - GPU computing fundamentals
- **[Intermediate Path](learning-paths/intermediate/index.md)** - Memory optimization and performance
- **[Advanced Path](learning-paths/advanced/index.md)** - Ring Kernels and multi-GPU
- **[Contributor Path](learning-paths/contributor/index.md)** - Extend and improve DotCompute

## Reference

Additional reference material:

- **[Diagnostic Rules (DC001-DC012)](reference/diagnostic-rules.md)** - Complete analyzer diagnostic reference
- **[Kernel Attribute Reference](reference/kernel-attribute.md)** - `[Kernel]` attribute options
- **[Performance Benchmarking](reference/performance-benchmarking.md)** - Performance measurement reference

## Migration

- **[v0.4.0 to v0.4.1](migration/from-0.4.0-to-0.4.1.md)** - Migration guide for v0.4.x users
