# DotCompute Articles

Comprehensive guides, tutorials, and conceptual documentation for DotCompute.

## Getting Started

- **[Installation Guide](getting-started.md)** - Install DotCompute and configure your first project
- **[Quick Start Tutorial](quick-start.md)** - Write your first GPU-accelerated kernel in 5 minutes
- **[Hello World Example](examples/hello-world.md)** - Complete walkthrough of a simple compute kernel

## Architecture

Understanding DotCompute's design and component relationships:

- **[Architecture Overview](architecture/overview.md)** - High-level system architecture
- **[Core Orchestration](architecture/core-orchestration.md)** - Compute orchestration and service integration
- **[Backend Integration](architecture/backend-integration.md)** - How backends plug into the framework
- **[Memory Management](architecture/memory-management.md)** - Unified memory system design
- **[Debugging System](architecture/debugging-system.md)** - Cross-backend validation architecture
- **[Optimization Engine](architecture/optimization-engine.md)** - Adaptive backend selection design
- **[Source Generators](architecture/source-generators.md)** - Compile-time code generation system

## Developer Guides

Practical guides for common development scenarios:

- **[Kernel Development](guides/kernel-development.md)** - Writing efficient compute kernels
- **[Backend Selection](guides/backend-selection.md)** - Choosing and configuring backends
- **[Performance Tuning](guides/performance-tuning.md)** - Optimizing kernel performance
- **[Debugging Guide](guides/debugging-guide.md)** - Debugging kernels and troubleshooting issues
- **[Native AOT Compilation](guides/native-aot.md)** - Building with Native AOT for maximum performance
- **[Multi-GPU Programming](guides/multi-gpu.md)** - Scaling across multiple GPUs
- **[Memory Management Best Practices](guides/memory-management.md)** - Efficient memory usage patterns
- **[Dependency Injection Integration](guides/dependency-injection.md)** - Using DotCompute with DI containers
- **[Telemetry and Monitoring](guides/telemetry.md)** - Observability and performance monitoring
- **[Timing API Guide](guides/timing-api.md)** - High-precision GPU timestamps with CPU-GPU clock synchronization
- **[Barrier API Guide](guides/barrier-api.md)** - Hardware-accelerated GPU thread synchronization primitives
- **[Troubleshooting Common Issues](guides/troubleshooting.md)** - Solutions to common problems

## Performance

Understanding and optimizing performance:

- **[Performance Characteristics](performance/characteristics.md)** - Expected performance for different operations
- **[Benchmarking Guide](performance/benchmarking.md)** - How to benchmark your kernels
- **[Optimization Strategies](performance/optimization-strategies.md)** - Advanced optimization techniques
- **[Hardware Requirements](performance/hardware-requirements.md)** - Hardware considerations for different backends

## Advanced Topics

Deep dives into specialized areas:

- **[SIMD Vectorization](advanced/simd-vectorization.md)** - CPU SIMD optimization techniques
- **[CUDA Programming](advanced/cuda-programming.md)** - CUDA-specific features and optimization
- **[Metal Shading Language](advanced/metal-shading.md)** - Writing Metal compute shaders
- **[OpenCL Development](advanced/opencl-development.md)** - Cross-platform OpenCL programming
- **[Plugin Development](advanced/plugin-development.md)** - Creating custom backend plugins
- **[Analyzer Development](advanced/analyzer-development.md)** - Writing custom Roslyn analyzers

## Examples

Complete, runnable examples:

- **[Basic Examples](examples/basic/index.md)** - Simple kernels and common patterns
- **[Intermediate Examples](examples/intermediate/index.md)** - Real-world compute scenarios
- **[Advanced Examples](examples/advanced/index.md)** - Complex multi-kernel pipelines

## Migration Guides

Migrating from other compute frameworks:

- **[From ILGPU](migration/from-ilgpu.md)** - Migrating ILGPU code to DotCompute
- **[From TorchSharp](migration/from-torchsharp.md)** - Migrating ML tensor operations
- **[From Alea GPU](migration/from-alea.md)** - Migrating F# GPU code

## Reference

Additional reference material:

- **[Diagnostic Rules (DC001-DC012)](reference/diagnostic-rules.md)** - Complete analyzer diagnostic reference
- **[Kernel Attribute Reference](reference/kernel-attribute.md)** - `[Kernel]` attribute options
- **[Configuration Reference](reference/configuration.md)** - All configuration options
- **[Glossary](reference/glossary.md)** - Terminology and definitions

## Contributing

Help improve DotCompute:

- **[Contributing Guide](../CONTRIBUTING.md)** - How to contribute to the project
- **[Code of Conduct](../CODE_OF_CONDUCT.md)** - Community guidelines
- **[Development Setup](contributing/development-setup.md)** - Setting up a development environment
