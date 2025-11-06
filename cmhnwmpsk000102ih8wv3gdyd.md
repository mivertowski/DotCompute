---
title: "Less hardware, more throughput: DotCompute as carbon-reduced tech"
datePublished: Thu Nov 06 2025 20:53:59 GMT+0000 (Coordinated Universal Time)
cuid: cmhnwmpsk000102ih8wv3gdyd
slug: less-hardware-more-throughput-dotcompute-as-carbon-reduced-tech
tags: dotnet, gpu, green-technology, carbon-reduced-technology

---

DotCompute started as “make GPU compute sane for .NET”.  
A close second goal: **use the hardware we already paid for** instead of buying new toys every quarter.

This post is about that second part.

---

# Why a GPU framework cares about carbon at all

Green software work usually boils down to a few boring but solid principles:

- **Carbon efficiency** – emit the least CO₂ per unit of work.  
- **Energy efficiency** – do the same job with less kWh.  
- **Hardware efficiency / embodied carbon** – squeeze more useful work out of the devices you already have before you buy new ones.

“Embodied carbon” is the CO₂ from *manufacturing and disposing* hardware (servers, GPUs, racks), as opposed to the energy used while running it. Data-center hardware has a significant embodied footprint; some analyses estimate ~20% of server life-cycle emissions come from manufacturing, the rest from use.

So if we can **delay buying new servers/GPUs** and **push more work through the ones we already own**, we’re doing two things at once:

1. Avoiding new embodied carbon.  
2. Using existing energy budget more efficiently.

That’s where DotCompute fits.

---

# GPUs: not just power hungry, also work-per-watt friendly

GPUs draw a lot of power on paper, yes. But for parallel workloads they often deliver **more useful work per watt** than CPU-only systems:

- NVIDIA reports customers getting big performance-per-watt wins – e.g. PayPal cut server energy consumption by almost **8×** for real-time fraud detection by moving to accelerated compute. 
- GPU performance-per-watt is a serious research topic in HPC; tuning GPUs around efficiency instead of max clocks can improve throughput per watt by 20–30% in real workloads. 
- Modern data-center GPU guidance explicitly frames “performance per watt” as the metric that matters as power constraints tighten.

The gap is: **most .NET enterprise workloads never touch those GPUs**. They sit in ML clusters or developer workstations, while your batch jobs and analytics run on yet another fleet of CPU VMs.

DotCompute exists to bridge exactly that gap.

---

# DotCompute’s angle: unlock the silicon you already own

Quick recap of what DotCompute is:

- A **native AOT-first universal compute framework for .NET 9+**.  
- Unified C# API across CPU and GPU backends.  
- Production-ready backends today: **SIMD CPU** and **CUDA**; others are evolving. 

You define kernels once in C#, and the framework does:

- Build-time generation of CPU and GPU implementations.  
- Runtime **automatic backend selection** based on hardware and data size.  
- Unified memory buffers and pooling to reduce allocations. :contentReference[oaicite:6]{index=6}  

Very small jobs stay on CPU (cheaper, no PCIe tax). Bigger chunks go to whatever GPU is present. Same code, no extra service written in another language.

That means you can:

- Point existing on-prem NVIDIA boxes at workloads that used to require more CPU nodes.  
- Reuse ML cluster capacity for “boring” analytics and simulations between training runs.  
- Turn idle developer / data-science workstations into night-time batch engines, if governance allows.

All without building a parallel Python / CUDA stack next to your .NET estate.

---

# A minimal example, with a carbon lens

You’ve seen “hello, vector add” already, so let’s keep it short:

```csharp
[Kernel("VectorAdd")]
public static void VectorAdd(
    KernelContext ctx,
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result)
{
    var i = ctx.GlobalId.X;
    if (i < result.Length)
        result[i] = a[i] + b[i];
}
````

Wiring the runtime:

```csharp
var services = new ServiceCollection()
    .AddDotCompute()
    .AddCpuBackend()
    .AddCudaBackend()   // If there’s a GPU, it will be used
    .BuildServiceProvider();

var compute = services.GetRequiredService<IComputeService>();

await compute.ExecuteAsync("VectorAdd", new { a, b, result });
```

You deploy this into an environment where:

* Some nodes are CPU only.
* Some nodes have a GPU that is mostly idle (or bursts for ML jobs).

DotCompute will:

* Run on CPU where no GPU is present.
* Shift heavier calls to the GPU where it is present, using SIMD CPU for smaller ones. ([GitHub][1])

Operationally: same binary, same code base, more work done on hardware that previously sat bored and powered-on.

From a green-software perspective, that’s **hardware efficiency** in practice: higher utilization of existing devices, less pressure to scale out new ones. ([Medium][2])

---

# Enterprise use cases where this actually matters

A few places where DotCompute’s “use what you have” story lines up with both carbon and cost.

## 1. Graph analytics & audits

Things like:

* Process graphs
* Fraud / transaction graphs
* Relationship networks in large ledgers

These are embarrassingly parallel across edges/vertices. With DotCompute you can:

* Implement graph passes (e.g. PageRank, propagation, scoring) as kernels.
* Run them on CPUs in test/dev, then on GPU-equipped nodes in prod without code changes. ([GitHub][1])

Result:

* Heavy analysis jobs now share the **same GPU hardware** already bought for ML or risk models instead of needing a separate analytics cluster.
* You keep GPU utilization high and server count lower.

## 2. Simulation and “what-if” workloads

CFD-light, risk simulations, Monte Carlo, large scenario grids:

* Traditionally: run on a farm of CPU servers; when demand spikes, buy more.
* With DotCompute: run on a mix of CPU and GPU, letting the orchestration layer decide where each job lands.

Because GPUs can execute many of these workloads with better performance per watt, you get:

* Fewer total servers for the same throughput.
* Shorter runtimes → lower operational energy per batch. ([NVIDIA Blog][3])

## 3. Streaming analytics and low-latency services

If you have a handful of high-traffic services (ticks, sensors, logs) on already-provisioned GPU nodes:

* DotCompute lets you offload the hot loops – filters, windowed stats, anomaly flags.
* Persistent kernels / RingKernels can keep state resident on GPU, moving less data back and forth.

Compared to standing up **another** specialized streaming cluster, reusing those nodes reduces both hardware line items and their embodied carbon. ([Learn Green Software][4])

---

## Carbon-reduced ≈ fewer boxes, longer lifetime

Most green-software guidance on hardware boils down to two simple tactics:

1. **Extend hardware lifetime** – amortize the embodied carbon over more years of useful work. ([Learn Green Software][4])
2. **Increase utilization** – avoid having multiple underutilized machines where one will do. ([Medium][2])

DotCompute plays into both:

* It makes it feasible for .NET teams to **adopt GPUs already in the fleet** without new languages / stacks.
* It keeps small jobs on CPU and larger jobs on GPU, so hardware is used closer to its efficient point instead of idling. ([GitHub][1])

From an IT ops view, this is also just boring TCO:

* Fewer additional servers for new analytics/compute workloads.
* Less power and cooling for a given amount of compute.
* A single, AOT-friendly runtime that doesn’t drag JITs and huge runtime dependencies into every microservice. ([GitHub][1])

Carbon-reduced and cost-reduced happen to be aligned for once.

---

# Carbon awareness: the “later” story

DotCompute doesn’t (yet) ship a built-in carbon scheduler, but the shape of it is straightforward:

* The **compute orchestrator** already decides which backend to use.
* Your scheduling layer can choose *when* to run large jobs based on grid carbon intensity (night vs day, region A vs region B). ([Learn Green Software][5])

Combine that with:

* CPU/GPU backend metrics
* Cloud carbon APIs
* A bit of policy (“run big backfills in the lowest-carbon window of the day”)

…and you get an end-to-end path from **C# kernel** to **carbon-aware batch execution**, without switching ecosystems.

That’s future work, but the building blocks are already heading in that direction.

---

# So is DotCompute “green tech”?

No. It’s a compute framework.

But it **does** make three very practical things easier:

1. **Reusing existing GPU capacity** from within normal .NET services.
2. **Finishing work faster on more efficient hardware**, so each batch burns less energy.
3. **Delaying or avoiding new hardware purchases** for certain workloads by driving higher utilization on what’s in the rack.

Green software principles talk about hardware as a carbon proxy and ask us to build apps that don’t force constant scaling-out of infrastructure. ([Medium][2])

DotCompute was designed mainly for performance and ergonomics.
The fact that it helps you ship **carbon-reduced compute** with the GPUs you already have is a very welcome side effect.

If your organization has idle GPUs and too many CPU servers, DotCompute is essentially a polite way of telling them to do more with what’s already humming in the data center.


[1]: https://github.com/mivertowski/DotCompute "mivertowski/DotCompute: A native AOT-first universal ..."
[2]: https://medium.com/%40bruno-bernardes-tech/engineering-a-sustainable-future-green-software-for-software-developers-0829bd144da8 "Green Software For Software Developers"
[3]: https://blogs.nvidia.com/blog/datacenter-efficiency-metrics-isc/ "Dial It In: Data Centers Need New Metric for Energy Efficiency"
[4]: https://learn.greensoftware.foundation/hardware-efficiency/ "Hardware Efficiency | Learn Green Software"
[5]: https://learn.greensoftware.foundation/introduction/ "Introduction | Learn Green Software"
