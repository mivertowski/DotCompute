---
title: "CUDA Kernel Execution Debugging Journey"
datePublished: Thu Sep 04 2025 12:38:09 GMT+0000 (Coordinated Universal Time)
cuid: cmf5e6ega000002kybsl3a7p9
slug: cuda-kernel-execution-debugging-journey
tags: dotnet, cuda

---

*Short version:* we went from **8/70** passing CUDA tests to a stable, auditable path by fixing NVRTC name resolution, argument marshaling, and unified-memory sync in [**DotCompute**](https://github.com/mivertowski/DotCompute). No mysticism—just careful pointers and fewer foot-guns.

---

## TL;DR

* NVRTC will happily mangle your kernel names. Resolve them explicitly.
    
* CUDA expects **pointers to values** for kernel params (yes, even device pointers).
    
* Unified memory needs synchronization before CPU access.
    
* Track every unmanaged allocation; free it.
    
* Tests climbed from **8/70 → 41/70 → aiming 95%+**.
    

---

## The Starting Point

The hardware test suite was a parade of classics:

* “**named symbol not found**” at launch
    
* **GCHandle pinning** failures (non-blittable types)
    
* **CUDA 700** (illegal address) on kernel calls
    
* **Unified memory** access violations
    
* A demoralizing **8/70** green checks
    

---

## What Actually Fixed Things

### 1) NVRTC name mangling (resolved)

**Problem:** `extern "C"` didn’t save us—NVRTC produced **mangled** names while our loader looked for unmangled ones.

**Fix:** Register name expressions *before* compile, then retrieve lowered names *after*.

```csharp
// Register before compilation
foreach (var funcName in functionNames)
{
    var nameExpr = $"&{funcName}";
    NvrtcInterop.nvrtcAddNameExpression(program, nameExpr);
}

// After compilation, retrieve the lowered (mangled) name
var lowered = NvrtcInterop.GetLoweredName(program, nameExpr);
mangledNames[funcName] = lowered;
```

**Impact:** Jumps us to **41+ / 70** passing tests.

---

### 2) Marshaling unified memory arguments

**Problem:** `CudaUnifiedMemoryBuffer<T>` isn’t blittable; direct pinning fails.

**Fix:** Reflect out the device pointer and pass that (see §3 for the **pointer-to-value** nuance).

```csharp
if (argValue.GetType().Name.StartsWith("CudaUnifiedMemoryBuffer"))
{
    var prop = argType.GetProperty("DevicePointer",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    if (prop?.GetValue(argValue) is IntPtr devicePtr)
    {
        unsafe
        {
            var storage = Marshal.AllocHGlobal(sizeof(IntPtr));
            *(IntPtr*)storage = devicePtr;    // store value
            unmanagedAllocations.Add(storage); // remember to free
            return storage;                    // return pointer to value
        }
    }
}
```

---

### 3) The critical param passing bug

**Problem:** We passed device pointer **values** directly to `cuLaunchKernel`. CUDA wants an array of **pointers to values**.

**Fix:** Allocate space, write the value, pass a pointer to that space.

```csharp
unsafe
{
    var storage = Marshal.AllocHGlobal(sizeof(IntPtr));
    *(IntPtr*)storage = devicePtr;   // write the value
    unmanagedAllocations.Add(storage);
    return storage;                  // CUDA reads the value from here
}
```

**Symptom this kills:** persistent **CUDA 700** on matrix-mult tests.

---

### 4) Unmanaged memory hygiene

**Problem:** Leaks from tiny per-arg allocations.

**Fix:** Track and free religiously.

```csharp
var unmanagedAllocations = new List<IntPtr>();

try
{
    var argPtr = PrepareKernelArgument(arg, handles, unmanagedAllocations);
    // ... launch ...
}
finally
{
    foreach (var p in unmanagedAllocations)
        if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
}
```

---

### 5) Unified memory: sync before you touch

**Problem:** CPU reading stale/remote pages.

**Fix:** Gate host spans behind an explicit sync.

```csharp
public override Span<T> GetSpan()
{
    EnsureOnHost(); // synchronize first
    return new Span<T>((void*)_hostPtr, Length);
}
```

---

## Deep Dives (the “why”)

### How CUDA reads kernel arguments

* **Wrong:** `argPointers[i] = devicePtr;`
    
* **Right:** `argPointers[i] = &devicePtr;`
    

`cuLaunchKernel` dereferences your `argPointers` to fetch the actual values. Device pointers are values too—treat them like any other scalar.

### C# overload resolution trap

Without the generic arg, a `params` overload may win by accident:

```csharp
LaunchAsync(config, buf1, buf2, buf3, size);      // may hit wrong overload
LaunchAsync<float>(config, buf1, buf2, buf3, size); // forces intended path
```

---

## Lessons (written on a sticky note)

1. **700 ≠ haunted memory** — often just wrong argument plumbing.
    
2. **P/Invoke is sharp** — mind blittability, lifetimes, and double-indirection.
    
3. **GPU is async by default** — sync before the CPU peeks.
    
4. **Reflection is a tool, not a lifestyle** — but it saved us here.
    
5. **Iterate mercilessly** — fix → run → commit → repeat.
    

---

## Status & Performance

* Start: **8 / 70** (11.4%)
    
* After NVRTC fix: **41 / 70** (58.6%)
    
* Targeting **95%+** with the remaining stragglers (edge cases & perf polish).
    

---

## Production Checklist

* Every CUDA/NVRTC call checks return codes
    
* All unmanaged allocations tracked & freed
    
* Timers/metrics around compile, HtoD/DtoH, and kernel time
    
* Launch config validated against device caps
    
* Tests for edge cases, stress, and multi-GPU
    

---

## What’s Next

* Dynamic parallelism (flag plumbing + tests)
    
* Faster transfers for bidi workloads
    
* CUDA Graphs to amortize launch overhead
    
* Texture/constant memory where patterns fit
    
* Nsight-based profiling in CI
    

---

## Appendix: File-level changes

* `CudaKernelCompiler.cs` — NVRTC name resolution
    
* `CudaKernelLauncher.cs` — argument marshaling, lifetime fixes
    
* `CudaUnifiedMemoryBuffer.cs` — host-access synchronization
    
* `CudaKernelExecutionTests.cs` — overload resolution fixed
    

### Key APIs we leaned on

* `nvrtcAddNameExpression`, `nvrtcGetLoweredName`
    
* `Marshal.AllocHGlobal`, `Marshal.FreeHGlobal`, `GCHandle.Alloc`
    
* `cudaDeviceSynchronize` (and friends)
    

### Handy error codes

* **NVRTC\_ERROR\_COMPILATION** — syntax/flags/headers
    
* **700** — illegal address (often arg passing)
    
* **716** — misaligned address
    
* **719** — launch failure
    

---

*Credit where due:* ClaudeCode helped pattern-match the error soup, surface the right docs, and keep the loop tight. The wins are still boring engineering ones—our favorite kind.