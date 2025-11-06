---
title: "DotCompute RC2 — Cross-Backend GPU Compute for .NET"
datePublished: Thu Nov 06 2025 10:30:20 GMT+0000 (Coordinated Universal Time)
cuid: cmhnacp8y000d02ju3kvge7gg
slug: dotcompute-rc2-cross-backend-gpu-compute-for-net
tags: dotnet, kernel, gpu, dotcompute

---

Another framework? Yes.  
Another abstraction layer that hates your cache and your startup time? No.

DotCompute’s first release candidate is ready. It gives you GPU and CPU acceleration from C# with:

- **One kernel definition** → multiple backends (CPU SIMD, CUDA, Metal, OpenCL)
- **Native AOT-first** design for .NET 9+
- **Modern C# API** using `[Kernel]` attributes and source generators
- **Automatic backend selection** based on data size and available hardware 

If you’re a .NET dev who wants to use the GPU without writing a second language (or giving up AOT), this is for you.

---

## What DotCompute is (in one paragraph)

DotCompute is a **universal compute framework for .NET 9+**: you write kernels in C#, mark them with `[Kernel]`, and let the runtime decide whether to run them on **CPU (AVX2/AVX512)**, **CUDA**, **Metal** or fallback CPU. The compiler pipeline is built around **source generators** and **Roslyn analyzers**, with cross-backend validation and telemetry baked in. :contentReference[oaicite:4]{index=4}  

No DSL, no separate shader projects, no “almost C#” language.

---

## Installing the RC

Minimal setup:

```bash
dotnet new console -n MyFirstKernel
cd MyFirstKernel

dotnet add package DotCompute.Core
dotnet add package DotCompute.Abstractions
dotnet add package DotCompute.Memory
dotnet add package DotCompute.Backends.CPU
dotnet add package DotCompute.Backends.CUDA   # optional, NVIDIA
dotnet add package DotCompute.Backends.Metal  # optional, Apple Silicon
dotnet add package DotCompute.Generators
dotnet add package DotCompute.Runtime

Target **.NET 9 + C# 13** in your `.csproj`:

```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <LangVersion>13.0</LangVersion>
</PropertyGroup>
```

Straight from the getting-started guide; no magic project templates required. ([mivertowski.github.io](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html))

---

## Your first kernel (real one, not a toy)

You can get from zero to a working GPU-capable kernel in roughly one coffee:

```csharp
using DotCompute;

public static class Kernels
{
    /// <summary>
    /// result[i] = a[i] + b[i]
    /// </summary>
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result)
    {
        int idx = Kernel.ThreadId.X;

        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

Key rules:

* `static`, `void`, `[Kernel]`
    
* `ReadOnlySpan<T>` for inputs, `Span<T>` for outputs
    
* Use `Kernel.ThreadId` for indexing
    
* Always bounds-check (the GPU will not feel bad for you) ([mivertowski.github.io](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html))
    

Wire it up with the runtime:

```csharp
using DotCompute.Abstractions;
using DotCompute.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDotComputeRuntime();
    })
    .Build();

var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();

const int size = 1_000_000;
var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
var b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
var result = new float[size];

await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { a, b, result });
```

On build, the source generator emits:

* A CPU SIMD implementation
    
* A CUDA kernel (if the backend is present)
    
* A Metal shader (if you’re on Apple Silicon)
    
* Registration glue so the runtime can find and run the kernel ([mivertowski.github.io](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html))
    

The orchestrator then picks **CPU or GPU based on data size and hardware**. Small arrays stay on CPU, big arrays go to the fastest device that exists. ([mivertowski.github.io](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html))

---

## Why you might care

A few use cases where DotCompute actually solves problems instead of being yet another abstraction:

* **High-throughput numerics**  
    Vector ops, matrix multiplies, simulations—offload to CUDA/Metal when the payload makes it worth it, stay on CPU for “toy sized” data without changing code. ([GitHub](https://github.com/mivertowski/DotCompute?utm_source=chatgpt.com))
    
* **Hybrid services**  
    .NET microservices that sometimes need a GPU burst: keep the service in C#, plug DotCompute in as a library instead of building a separate Python sidecar.
    
* **Cross-platform compute**  
    Same codebase running on a Linux server with NVIDIA, a MacBook (Metal), or just CPU. The framework is designed as **AOT-friendly** for lean deployment. ([GitHub](https://github.com/mivertowski/DotCompute?utm_source=chatgpt.com))
    
* **Future: persistent kernels and GPU “actors”**  
    The same infrastructure underpins RingKernels and persistent kernels—launch once, push messages forever. That gets its own post, but RC already lays the groundwork.
    

---

## Production-leaning bits (even in an RC)

The RC isn’t just a “demo build”. Today you already get: ([GitHub](https://github.com/mivertowski/DotCompute?utm_source=chatgpt.com))

* **Unified memory management** with pooling to cut allocations
    
* **Cross-backend validation** to compare CPU vs GPU results
    
* **Telemetry & profiling hooks** for execution timing
    
* **Native AOT support** with trimming-friendly design
    

It’s still a release candidate, not a court-admissible standard library, but it’s aimed at real workloads, not just blog benchmarks.

---

## Where to start

* **Docs / Quickstart:**  
    Getting Started guide (10-minute kernel):  
    👉 [https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html) ([mivertowski.github.io](https://mivertowski.github.io/DotCompute/docs/articles/getting-started.html))
    
* **Source / issues / stars (the usual ritual):**  
    👉 [https://github.com/mivertowski/DotCompute](https://github.com/mivertowski/DotCompute) ([GitHub](https://github.com/mivertowski/DotCompute?utm_source=chatgpt.com))
    
* **Coming next on the blog:**
    
    * The beauty of persistent kernels (RingKernels deep dive)
        
    * Real-world scenarios (graphs, audits, simulations)
        
    * Under-the-hood: how the generator pipeline works
        

If you try DotCompute and something breaks, that’s… good. For now.  
File an issue, tell it what you attempted, and we can make the RC a bit less “R” and a bit more “just ship it”.