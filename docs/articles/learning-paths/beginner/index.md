# Beginner Learning Path

Welcome to GPU computing with DotCompute. This path introduces fundamental concepts and guides you through writing your first GPU-accelerated code.

## Prerequisites

- C# programming fundamentals (variables, classes, methods)
- Basic understanding of arrays and loops
- .NET 9.0 SDK installed
- Any supported GPU (NVIDIA, AMD, Intel, Apple Silicon) or CPU fallback

## Learning Objectives

By completing this path, you will:

1. Understand the difference between CPU and GPU computing
2. Write and execute your first GPU kernel
3. Manage memory transfers between CPU and GPU
4. Choose the appropriate backend for your hardware

## Modules

### Module 1: Introduction to GPU Computing
**Duration**: 30-45 minutes

Learn why GPUs excel at parallel workloads and when GPU acceleration provides meaningful speedups.

[Start Module 1 →](gpu-computing-intro.md)

### Module 2: Your First Kernel
**Duration**: 45-60 minutes

Write a vector addition kernel using the `[Kernel]` attribute and execute it on your GPU.

[Start Module 2 →](first-kernel.md)

### Module 3: Memory Fundamentals
**Duration**: 45-60 minutes

Understand GPU memory hierarchy, buffer types, and how to efficiently transfer data.

[Start Module 3 →](memory-fundamentals.md)

### Module 4: Backend Selection
**Duration**: 30-45 minutes

Learn about DotCompute's backend system and how to target specific hardware.

[Start Module 4 →](backend-selection.md)

## Completion Checklist

- [ ] Explain when GPU acceleration is beneficial
- [ ] Write a kernel using the `[Kernel]` attribute
- [ ] Create and manage GPU buffers
- [ ] Execute kernels with proper thread configuration
- [ ] Select appropriate backend for your hardware

## Next Steps

After completing this path, continue to the [Intermediate Path](../intermediate/index.md) to learn about memory optimization and kernel performance tuning.

---

*Estimated total duration: 2-4 hours*
