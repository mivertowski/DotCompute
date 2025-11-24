# Ring Kernels Documentation

Ring Kernels are a revolutionary programming model enabling persistent GPU-resident computation with actor-style message passing. This section provides comprehensive documentation for developing, deploying, and optimizing Ring Kernel applications.

## Getting Started

| Document | Description |
|----------|-------------|
| [Overview](overview.md) | Introduction to Ring Kernels and their benefits |
| [Architecture](architecture.md) | System architecture and design principles |
| [Migration Guide](migration.md) | Migrating to the unified Ring Kernel system |

## Core Concepts

| Document | Description |
|----------|-------------|
| [Telemetry](telemetry.md) | Real-time GPU health monitoring with <1us latency |
| [Messaging & Telemetry](messaging-telemetry.md) | Message queue integration and telemetry patterns |
| [MemoryPack Format](memorypack-format.md) | Binary serialization format for GPU messages |
| [Compilation Pipeline](compilation-pipeline.md) | How Ring Kernels are compiled for GPU execution |

## Synchronization & Coordination

| Document | Description |
|----------|-------------|
| [Barriers](barriers.md) | Thread-block, grid, and warp barrier synchronization |
| [Memory Ordering](memory-ordering.md) | Causal consistency and memory fence operations |
| [Phase 3: Coordination](phase3-coordination.md) | Multi-kernel coordination primitives |
| [Phase 4: Temporal Causality](phase4-temporal.md) | Hybrid Logical Clocks and advanced coordination |
| [Health Monitoring](health-monitoring.md) | GPU health and failure detection |

## Advanced Topics

| Document | Description |
|----------|-------------|
| [Advanced Programming](advanced.md) | Complex patterns and production deployment |

## Examples

| Document | Description |
|----------|-------------|
| [VectorAdd Example](vectoradd-example.md) | Complete reference implementation |
| [PageRank Example](pagerank-example.md) | Distributed actor implementation of PageRank |

## Quick Reference

**Key Features:**
- Zero kernel launch overhead after initial launch
- Actor-style message passing on GPU
- Sub-microsecond telemetry polling
- Cross-kernel coordination
- Hybrid Logical Clock support

**Supported Backends:**
- CUDA (CC 5.0+)
- Metal (Apple Silicon)
- OpenCL 1.2+
- CPU (fallback)

---

*Ring Kernels v0.4.2-rc2 - Production Ready*
