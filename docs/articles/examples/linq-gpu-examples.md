---
title: LINQ GPU Computing Examples
uid: examples_linq_gpu
---

# LINQ GPU Computing Examples

Practical examples using DotCompute's LINQ provider for GPU-accelerated computing.

🚧 **Documentation In Progress** - LINQ examples are being developed. Note: LINQ GPU acceleration is currently in foundation phase (5% complete).

## Overview

DotCompute's LINQ extensions provide:

- GPU-accelerated filtering and mapping
- Query expression syntax for GPU compute
- Automatic backend optimization
- Seamless fallback to CPU LINQ

## Status

**Current Implementation**: Foundation only
- Basic queryable wrapper (`AsComputeQueryable`)
- Delegates to standard LINQ (no GPU acceleration yet)
- Full implementation timeline: 24 weeks

**Planned Features** (NOT YET AVAILABLE):
- CUDA kernel generation from LINQ expressions
- GPU-accelerated select, where, aggregate operations
- Expression tree compilation
- Multi-backend code generation

See [LINQ Implementation Plan](../../docs/LINQ_IMPLEMENTATION_PLAN.md) for details.

## Basic Queries

### Select Operations

TODO: Provide select examples (currently delegates to LINQ)

### Where Operations

TODO: Document where clause examples (currently delegates to LINQ)

### Aggregate Operations

TODO: Explain aggregate patterns (currently delegates to LINQ)

## Advanced Queries

### Complex Expressions

TODO: Cover complex query expressions (future GPU acceleration)

### Join Operations

TODO: Document join patterns (future GPU acceleration)

### Group Operations

TODO: Explain grouping (future GPU acceleration)

## Reactive Extensions Integration

TODO: Document Rx.NET integration (planned for Phase 7)

## Performance Comparison

TODO: Provide performance benchmarks (once GPU acceleration is implemented)

## Migration Guide

### From Standard LINQ

TODO: Document migration patterns from standard LINQ

## Limitations

Current limitations (Phase 5 foundation):
- No GPU kernel generation yet
- No expression compilation
- Standard LINQ semantics with CPU execution
- Full async/await support in roadmap

## See Also

- [LINQ Implementation Plan](../../docs/LINQ_IMPLEMENTATION_PLAN.md)
- [GPU Kernel Generation Guide](../../phase5/GPU_KERNEL_GENERATION_GUIDE.md)
- [Performance Optimization](./performance-optimization.md)
