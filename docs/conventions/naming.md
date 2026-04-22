# Type Suffix Naming Convention

**Scope:** Applies to **new code** from v1.0.0-preview3 onward. Existing public types
are grandfathered — renaming the public API surface costs more than the clarity gain.

## Core taxonomy

| Suffix | Role | Owns | Example |
|---|---|---|---|
| `Manager` | Lifecycle owner of a resource | Disposable state — handles, pools, CTS, timers | `CudaMemoryManager` owns device allocations |
| `Service` | Stateless facade / orchestration | No long-lived resources; coordinates others | `KernelExecutionService` dispatches to managers |
| `Provider` | DI factory for one implementation family | Creates instances at composition time | `CudaAcceleratorProvider` creates `CudaAccelerator` |
| `Runtime` | Persistent execution environment (often GPU-resident) | State bound to a running kernel or actor loop | `CudaRingKernelRuntime` owns the long-running kernel |
| `Analyzer` | Roslyn diagnostic | Reads code, reports diagnostics | `KernelTypeSafetyAnalyzer` |
| `Helpers` / `Utilities` | Stateless free functions | Static-only, no instance state | `BufferHelpers`, `MemoryUtilities` |

Adjacent pattern names used in the codebase — keep where they fit better than M/S/P/R:
`Bridge`, `Cache`, `Compiler`, `Factory`, `Pool`, `Registry`, `Repository`, `Stage`,
`Translator`, `Watchdog`.

## How to pick

1. Holds a disposable resource? → **Manager**.
2. Stateless coordinator that forwards to others? → **Service**.
3. Constructs instances of one family at DI composition? → **Provider**.
4. Represents a running execution environment with long-lived kernel/actor state?
   → **Runtime**.
5. Roslyn diagnostic? → **Analyzer**.
6. Purely static helpers? → **Helpers** / **Utilities**.
7. Unsure? Prefer **Service** — least committal; `Manager` implies a disposal contract
   you then have to honour.

## Examples from the codebase that fit cleanly

- `CudaMemoryManager` — owns `cuMemAlloc` handles, pools, must dispose → **Manager**.
- `KernelExecutionService` — no owned resources beyond a `SemaphoreSlim`, delegates to
  runtime + cache + compiler + profiler → **Service**.
- `CudaAcceleratorProvider` — implements `IAcceleratorProvider`, creates accelerators
  at DI resolution time → **Provider**.
- `CudaRingKernelRuntime` — launches and owns a persistent CUDA kernel; lifecycle is
  tied to the running kernel, not the DI container → **Runtime**.
- `KernelTypeSafetyAnalyzer` — Roslyn `DiagnosticAnalyzer` → **Analyzer**.
- `BufferHelpers` — static SIMD helpers, no instance state → **Helpers**.

## New code checklist

- [ ] Pick a suffix using the flowchart.
- [ ] If it's a `Manager`, it **must** implement `IDisposable` (or `IAsyncDisposable`).
- [ ] If it's a `Service`, prefer constructor-injected dependencies and no instance
      state beyond small caches / locks.
- [ ] If it's a `Provider`, it **must** implement a `*Provider` interface.
- [ ] If it's a `Runtime`, document the lifecycle explicitly in the XML docs.
- [ ] Don't stack suffixes (`KernelExecutionServiceManager` is always wrong).

## Future-rename candidates (informational — do NOT rename without an API-break plan)

These are existing names that would fit a different suffix better under this convention.
They are listed here so reviewers can flag the pattern for the next major version, but
they are **not** being renamed now because they are public API.

1. **`SecurityAlertManager`** (public, `DotCompute.Core`) — coordinates alert handling
   with injected `SecurityLoggingConfiguration`; owns only session metadata dictionaries.
   Better fit: `SecurityAlertService`. Rename cost: public API break.
2. **`SystemInfoManager`** (public, `DotCompute.Core`) — primarily a query surface that
   caches a `SystemInfo` with an optional `Timer`. The cache + timer scrape by for
   Manager, but Service is closer to the intent. Rename cost: public API break.
3. **`CudaCapabilityManager`** (public, `DotCompute.Backends.CUDA`) — static-style
   capability lookup with no disposable state; the class is a facade over detection
   code. Better fit: `CudaCapabilityService` (or just `CudaCapabilities`). Rename cost:
   public API break, widely used across the codebase.
4. **`MalwareScanningService`** (public, `DotCompute.Algorithms`) — owns `HttpClient`,
   `SemaphoreSlim`, and hash state; fit is borderline. It leans towards **Manager** by
   the disposal rule but was named `Service`. Either keep the name or accept the border
   case; no action.
5. **`PluginMonitoring.cs`** (file, `DotCompute.Plugins`) — file name has no suffix, but
   it only hosts internal types (`PluginHealthMonitor`, `PluginMetricsCollector`) which
   are correctly named. A cosmetic rename of the file to `PluginHealthMonitor.cs` +
   splitting `PluginMetricsCollector.cs` would improve navigation, but no public API
   impact.
6. **`CudaRuntime`** (public, `DotCompute.Backends.CUDA.Native`) — this is a P/Invoke
   wrapper for the CUDA runtime library, not a `Runtime` in our taxonomy sense. The
   name matches the native library's name (`cudart`), so the collision is acceptable
   and documented here. Do not rename.
7. **Three `PluginLifecycleManager` classes** in different namespaces
   (`DotCompute.Runtime.DependencyInjection`, `DotCompute.Algorithms.Management.Core`,
   `DotCompute.Plugins.Aot.Lifecycle`) — all legitimately `Manager` (each owns
   registered-plugin state), but the duplicate simple name across namespaces is a
   navigation hazard. Consider scoped prefixes (`AotPluginLifecycleManager` etc.) at
   the next major break.
8. **`PoolReturnManager` / `IStreamReturnManager`** (internal, `CudaStreamPool.cs`) —
   the class is a stateless one-method callback wrapper, not a Manager. It is also
   currently **never constructed** anywhere in the codebase, so the right move is
   removal rather than rename. Tracked as a separate dead-code cleanup.

## Where this convention is not applied

- **Orleans.GpuBridge.Core consumer surface.** Types exposed across the repo boundary
  are effectively public API even when marked `internal`. Do not rename them under this
  convention.
- **Hopper code (`Backends/DotCompute.Backends.CUDA/Hopper/`).** Out of scope for
  refactors — behaviour parity with the CUDA driver reference is prioritised over
  naming.
- **Native P/Invoke wrappers.** Names mirror the underlying native library and are
  kept for 1:1 traceability.
