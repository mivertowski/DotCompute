# DotCompute.Generators.Attributes

Consumer-facing attribute types for the DotCompute source generator framework.

## Overview

This package provides the attributes and supporting types that consumers use to mark their methods as compute kernels. These types are automatically included when you reference `DotCompute.Generators`.

## Attributes

### KernelAttribute

Marks a static method as a compute kernel for GPU/CPU acceleration:

```csharp
using DotCompute.Generators;

[Kernel(Backends = KernelBackends.Cuda | KernelBackends.Cpu)]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
        result[idx] = a[idx] + b[idx];
}
```

### RingKernelAttribute

Marks a method as a persistent ring kernel for continuous GPU-resident computation:

```csharp
using DotCompute.Generators;

[RingKernel(
    KernelId = "data-processor",
    Domain = RingKernelDomain.ActorModel,
    Mode = RingKernelMode.Persistent)]
public static void ProcessMessages(
    IMessageQueue<DataMessage> incoming,
    IMessageQueue<ResultMessage> outgoing)
{
    while (incoming.TryDequeue(out var msg))
    {
        // Process message...
        outgoing.Enqueue(new ResultMessage { ... });
    }
}
```

### RingKernelMessageAttribute

Marks a type as a message for ring kernel communication:

```csharp
[RingKernelMessage(Direction = MessageDirection.Input)]
public struct DataMessage
{
    public int Id;
    public float Value;
}
```

## Enums

| Enum | Description |
|------|-------------|
| `KernelBackends` | Target backends (Cpu, Cuda, OpenCL, Metal, All) |
| `BarrierScope` | Synchronization scope (ThreadBlock, Grid, Warp, Named, Tile) |
| `MemoryAccessPattern` | Memory access hints (Sequential, Strided, Random, Coalesced, Tiled) |
| `MemoryConsistencyModel` | Memory ordering (Relaxed, ReleaseAcquire, Sequential) |
| `OptimizationHints` | Optimization flags (AggressiveInlining, LoopUnrolling, Vectorize, etc.) |
| `RingKernelMode` | Execution mode (Persistent, EventDriven) |
| `RingKernelDomain` | Application domain (General, GraphAnalytics, SpatialSimulation, ActorModel) |
| `MessageDirection` | Message flow direction (Input, Output, Bidirectional) |
| `MessagePassingStrategy` | Communication strategy (SharedMemory, AtomicQueue, P2P, NCCL) |
| `RingProcessingMode` | Processing mode (Sequential, Parallel, Hybrid) |

## Installation

This package is automatically included when you add the DotCompute.Generators package:

```bash
dotnet add package DotCompute.Generators --version 0.6.0
```

Or reference directly for attribute-only usage:

```bash
dotnet add package DotCompute.Generators.Attributes --version 0.6.0
```

## Requirements

- .NET Standard 2.0+ (for maximum compatibility)
- Works with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+

## Documentation

- [DotCompute Documentation](https://mivertowski.github.io/DotCompute/)
- [Kernel Development Guide](https://mivertowski.github.io/DotCompute/articles/guides/kernel-development.html)
- [Ring Kernels Guide](https://mivertowski.github.io/DotCompute/articles/guides/ring-kernels.html)

## License

MIT License - Copyright (c) 2025 Michael Ivertowski
