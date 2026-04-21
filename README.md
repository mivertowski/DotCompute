# DotCompute

[![NuGet](https://img.shields.io/nuget/vpre/DotCompute.Core.V2.svg?label=DotCompute.Core.V2)](https://www.nuget.org/packages/DotCompute.Core.V2/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Status](https://img.shields.io/badge/status-preview-orange.svg)](CHANGELOG.md)
[![Build](https://github.com/mivertowski/DotCompute/actions/workflows/ci.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions)

High-performance compute framework for .NET 10. **v1.0.0-preview3 (prerelease)** — API is stable from preview2 forward; H100 hardware validation and Metal NuGet publishing are tracked for v1.0.0 GA.

---

## What it does

DotCompute lets you write a compute kernel as a static C# method annotated with `[Kernel]`, and the framework produces a wrapper that runs it on the CPU (SIMD: AVX2/AVX-512/NEON), an NVIDIA GPU (CUDA, compute capability 5.0 through 9.0 with first-class Hopper support), or an Apple Silicon GPU (Metal). The `IComputeOrchestrator` chooses a backend at runtime based on what hardware is available and can be steered explicitly (`"CPU"`, `"CUDA"`, `"Metal"`).

A source generator reads `[Kernel]`-marked methods at compile time and emits the adapter code for each enabled backend, so there is no runtime code generation and the library is Native AOT compatible. A companion Roslyn analyzer set (DC001–DC019) flags structural problems (non-static kernels, missing bounds checks, unmanaged constraint violations, unsafe thread-model usage) directly in the IDE.

Beyond one-shot kernel launches, DotCompute ships a **Ring Kernel** system for persistent GPU computation in an actor style — a kernel launches once, polls a message queue, and keeps running until told to stop. It ships a GPU-accelerated LINQ provider (`AsComputeQueryable`), cross-backend validation for debugging correctness on CPU versus GPU, and OpenTelemetry instrumentation (`ActivitySource` + `Meter`) for production observability.

---

## Quick start

Create a .NET 10 console project, install the preview packages, and run the following program. It does not require a GPU — the CPU backend runs everywhere.

```bash
dotnet new console -n DotComputeHello
cd DotComputeHello
dotnet add package DotCompute.Runtime.V2         --prerelease
dotnet add package DotCompute.Backends.CPU.V2    --prerelease
dotnet add package DotCompute.Generators.V2      --prerelease
```

```csharp
using DotCompute.Abstractions.Interfaces;
using DotCompute.Generators;
using DotCompute.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateApplicationBuilder(args);
host.Services.AddLogging();
host.Services.AddDotComputeRuntime();
var app = host.Build();

var a      = new float[] { 1, 2, 3, 4, 5 };
var b      = new float[] { 10, 20, 30, 40, 50 };
var result = new float[5];

var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();
await orchestrator.ExecuteAsync<float[]>("VectorAdd", a, b, result);

Console.WriteLine(string.Join(", ", result));
// Expected: 11, 22, 33, 44, 55

public static class Kernels
{
    [Kernel]
    public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = KernelContext.ThreadId.X;
        if (idx < result.Length) result[idx] = a[idx] + b[idx];
    }
}
```

To run the same kernel on CUDA, install `DotCompute.Backends.CUDA.V2 --prerelease` and pass `"CUDA"` as the second argument to `ExecuteAsync`.

---

## Supported backends

| Backend | Ship state in preview3 | Hardware range | Reported speedup (conditions below) |
|---------|------------------------|----------------|-------------------------------------|
| **CPU (SIMD)** | Published on nuget.org as `DotCompute.Backends.CPU.V2` | AVX2 / AVX-512 / ARM NEON | ~3.7× vs. scalar C# for vector add at 100K elements (1) |
| **CUDA** | Published on nuget.org as `DotCompute.Backends.CUDA.V2`; Hopper (sm_90) API surface complete, H100 hardware validation pending | NVIDIA GPUs, compute capability 5.0–9.0 | Kernel-dependent; see (2) |
| **Metal** | Source-complete in repo; **NuGet package not yet published** (native `.dylib` bundling not wired into the Linux CI that pushes packages) | Apple Silicon (M1–M4) and Intel Macs with Metal-capable GPUs | Not yet measured under this release |

**(1)** Measured with BenchmarkDotNet on a workstation-class x86 CPU, .NET 10, `ReadOnlySpan<float>` inputs of length 100,000. Your results will vary.

**(2)** On an NVIDIA RTX 2000 Ada (CC 8.9) in WSL2 with .NET 10 and CUDA 13.0, internal benchmarks observed the following speedups relative to single-threaded scalar C#: matrix multiply (1024²) ≈ 92×, sum reduction (1M) ≈ 33×, fused filter-then-map (1M) ≈ 23×. These figures are **directionally representative only** — Hopper / H100 re-measurement is on the v1.0.0 GA checklist, and the usual caveats (dataset size, block-shape tuning, warm-up, memory residency) apply. Reproduce them yourself with `benchmarks/` before citing them.

OpenCL was a supported backend in v0.6.x and earlier and was removed in v1.0.0-preview1. Consumers that depend on OpenCL should pin to `DotCompute.Backends.OpenCL 0.6.2` or earlier.

---

## Features in v1.0.0-preview3

- **`[Kernel]` attribute + source generator.** Compile-time codegen for CPU SIMD and GPU backends. No runtime IL emission; Native AOT compatible.
- **Roslyn analyzers DC001–DC019.** Kernel correctness (non-static, bad parameters, unsupported constructs, unmanaged-constraint violations), performance hints (vectorization, memory access patterns, register spilling), and RingKernel-specific rules (unpublished backends, telemetry misuse). Five automated code fixes.
- **Classified error model.** `CudaErrorClass` enum (Success / Transient / Resource / Programmer / Fatal); `CudaException` surfaces `.Classification`, `.IsRecoverable`, `.IsResourceError`, `.IsFatal`, `.IsRetryable`. `CudaErrorHandler` retry and circuit-breaker policies dispatch on the classification.
- **NVIDIA Hopper (sm_90) first-class support.** `DotCompute.Backends.CUDA.Hopper` namespace ships thread-block-cluster launches (`cuLaunchKernelEx`), Tensor Memory Accelerator configuration (`cp.async.bulk`), distributed shared memory (DSMEM), and async memory pools (`cuMemAllocAsync` / `cuMemFreeAsync`). 89 unit tests cover config validation and layout math. **Hardware validation on real H100 is tracked for v1.0.0 GA** — the code is wired and unit-tested but not yet soak-tested on the target silicon.
- **Cache-line-padded atomic counters.** All eight high-frequency producer/consumer counters in `CudaMessageQueue<T>` are `PaddedLong`/`PaddedInt` (128-byte lines). Ported from a RustCompute pattern; throughput delta on a comparable Rust queue was 22–28 %. Re-measurement on DotCompute is on the GA checklist.
- **Ring Kernels — persistent GPU computation.** `[RingKernel]` attribute, message-queue bridge, and three backend implementations:
  - **CPU**: thread-based persistent kernel, 43/43 tests passing.
  - **CUDA**: GPU-resident kernel with mapped-memory message queue, 115/122 tests passing (94 %). WSL2 adds ~5 s per launch due to memory-coherence limits; use native Linux for latency-sensitive workloads.
  - **Metal**: feature-complete source, less exercised than CPU/CUDA.
- **LINQ over GPU.** `AsComputeQueryable()` compiles a query tree to a backend kernel. Preview3 ships **Join (inner / semi / anti), GroupBy (Count / Sum / Min / Max / Avg), and OrderBy (bitonic sort on GPU, `Array.Sort` on CPU)** — the full set. Earlier previews were marked "80 % complete"; that caveat is now resolved.
- **Cross-backend debugging.** `IKernelDebugService` validates a kernel's GPU output against a CPU reference run, surfacing mismatches with element-level diffs.
- **OpenTelemetry instrumentation.** `CudaTelemetry.Source` `ActivitySource` (`DotCompute.Backends.CUDA`) plus a `Meter`. Wired into context-make-current, P2P enable, and kernel launch sites. Standard tag surface aligns with the sibling RustCompute project so dashboards port across.
- **Native AOT.** All shipped packages set `<IsAotCompatible>true</IsAotCompatible>` and build under the IL-trimming analyzers.

---

## Installation

All packages are on nuget.org under the `ivertowski` publisher with the `.V2` suffix (see [CHANGELOG.md](CHANGELOG.md) for the rename rationale). `AssemblyName` and `RootNamespace` are **unchanged** from v0.6.x, so existing `using DotCompute.Core;` statements still work — only the `<PackageReference Include="..." />` ID changes.

`DotCompute.Runtime.V2` is the DI entry point and transitively depends on `DotCompute.Core.V2`, `DotCompute.Abstractions.V2`, and `DotCompute.Backends.CUDA.V2`. You still add the CPU backend and the source generator explicitly, because the generator is an analyzer (not a framework reference) and the CPU backend is optional at the project level.

**Minimal (CPU only, runs anywhere — CUDA drivers not required even though the CUDA assembly is present):**

```bash
dotnet add package DotCompute.Runtime.V2         --prerelease
dotnet add package DotCompute.Backends.CPU.V2    --prerelease
dotnet add package DotCompute.Generators.V2      --prerelease
```

**With an explicit CUDA runtime (no behavioural change from minimal; use this when you want the package reference to be visible):**

```bash
dotnet add package DotCompute.Runtime.V2         --prerelease
dotnet add package DotCompute.Backends.CPU.V2    --prerelease
dotnet add package DotCompute.Backends.CUDA.V2   --prerelease
dotnet add package DotCompute.Generators.V2      --prerelease
```

**With LINQ and algorithm extensions:**

```bash
dotnet add package DotCompute.Linq.V2        --prerelease
dotnet add package DotCompute.Algorithms.V2  --prerelease
```

Metal is not yet on nuget.org; to use it, clone this repo and add a project reference to `src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj` from a macOS build.

---

## Documentation

- [CHANGELOG.md](CHANGELOG.md) — authoritative release notes. The `[1.0.0-preview3]` section is the source of truth for what shipped.
- [Documentation site](https://mivertowski.github.io/DotCompute/) — guides, API reference, learning paths.
- [`docs/articles/quick-start.md`](docs/articles/quick-start.md) — extended getting-started guide.
- [`docs/articles/guides/`](docs/articles/guides/) — kernel development, backend selection, memory management, Native AOT, debugging.
- [`docs/migration/MIGRATION-v0.9-to-v1.0.md`](docs/migration/MIGRATION-v0.9-to-v1.0.md) — draft migration notes for the v1.0 breaking changes.
- [`samples/RingKernels/PageRank/Metal/SimpleExample/`](samples/RingKernels/PageRank/Metal/SimpleExample/) — an architectural walkthrough of the Ring Kernel pattern for the Metal backend.
- [`benchmarks/`](benchmarks/) — BenchmarkDotNet projects to reproduce the numbers cited above.

---

## System requirements

- **.NET 10.0 SDK or later.** C# 14 language features are used in the codebase and in examples that use `using static` + lambdas; consumer code has no specific C# version requirement beyond what .NET 10 provides.
- **For CUDA:** NVIDIA GPU with compute capability 5.0 or higher, [CUDA Toolkit 12.0 or later](https://developer.nvidia.com/cuda-downloads). CUDA 13.0 or later is required to target Hopper (`sm_90`).
- **For Metal:** macOS 10.13 or later, Metal-capable GPU. Use a local build; no NuGet package yet.
- **Operating systems:** Linux, Windows, macOS (64-bit).
- **WSL2:** supported **for development and testing only**. WSL2's GPU virtualization layer (GPU-PV) does not provide reliable CPU↔GPU memory coherence, which forces Ring Kernels into a kernel-relaunch path with ~5 second latency per round-trip instead of the sub-millisecond behaviour on native Linux. Do not use WSL2 for latency-sensitive production workloads.

---

## Known limitations in v1.0.0-preview3

- **H100 hardware validation pending.** Hopper API surface (cluster launch, TMA, DSMEM, async mem pools) is coded and unit-tested; real-silicon soak tests and throughput re-measurement are on the v1.0.0 GA checklist.
- **Metal NuGet package not yet published.** The backend is source-complete; the CI pipeline that pushes packages runs on Linux and does not currently produce the macOS native `.dylib` bundling needed for a runnable Metal NuGet. Use a project reference from a macOS clone for now.
- **Ring Kernel CUDA tests: 115 / 122 passing (94 %).** The seven failures are resource-cleanup edge cases on the WSL2 test environment; see the CHANGELOG for detail.
- **System.CommandLine pinned at `2.0.0-beta4.22272.1`.** The 2.0.6 redesign is a breaking API change that is tracked as a follow-up.
- **Tensor core production path has remaining `TODO` markers.** The core Hopper path is complete; opinionated tensor-core helpers are a post-GA item.
- **ROCm backend:** not shipped. Not on the v1.0 roadmap.
- **Preview status.** v1.0.0-preview3 is a release candidate under the new nuget.org publisher account; API surface is stable from preview2 forward, but the `-preview3` suffix will remain until the items above are cleared.

---

## Building from source

```bash
git clone https://github.com/mivertowski/DotCompute.git
cd DotCompute
dotnet build DotCompute.sln --configuration Release

# CPU-only tests (run anywhere):
dotnet test --filter "Category!=Hardware"

# Full test run (requires an NVIDIA GPU; handles WSL2 LD_LIBRARY_PATH automatically):
./scripts/run-tests.sh DotCompute.sln --configuration Release
```

---

## Contributing

Contributions are welcome. Good entry points: performance optimizations (benchmarked against `benchmarks/`), additional analyzer rules, documentation improvements, bug fixes with accompanying tests. See [CONTRIBUTING.md](CONTRIBUTING.md) for details.

---

## Related projects

- **[RustCompute](https://github.com/mivertowski/RustCompute)** — a sibling Rust project that is hardened on H100. Several DotCompute v1.0 patterns (cache-line-padded atomics, classified error model, OTel surface, the Hopper namespace layout) are ports from RustCompute. Cross-referencing the two projects is useful when you want to see a feature validated on H100 silicon before it has been validated here.
- **[Orleans.GpuBridge.Core](https://github.com/mivertowski/Orleans.GpuBridge.Core)** — Orleans grain abstractions for GPU-resident persistent actors. Consumes DotCompute via `IGpuBackendProvider`. The `v1.0-preview` bump and Ring Kernel wire-up are tracked in that repo's migration notes.

---

## License

MIT — see [LICENSE](LICENSE).

Copyright (c) 2023–2026 Michael Ivertowski.
