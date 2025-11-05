# Known API Issues in v0.4.0-rc2

This document describes known API design issues in DotCompute v0.4.0-rc2 and provides workarounds.

## Critical Issue: AddDotComputeRuntime() Namespace Conflict

### Problem Description

There are **TWO different** `AddDotComputeRuntime()` extension methods in DotCompute v0.4.0-rc2, located in different namespaces:

1. **`DotCompute.Runtime.AddDotComputeRuntime()`**
   - Location: `src/Runtime/DotCompute.Runtime/ServiceCollectionExtensions.cs` (line 32)
   - Registers: `IUnifiedAcceleratorFactory` ✅
   - Does NOT register: `IComputeOrchestrator` ❌

2. **`DotCompute.Runtime.Extensions.AddDotComputeRuntime()`**
   - Location: `src/Runtime/DotCompute.Runtime/Extensions/ServiceCollectionExtensions.cs` (line 32)
   - Registers: `IComputeOrchestrator` ✅
   - Does NOT register: `IUnifiedAcceleratorFactory` ❌

### Impact

**Neither method registers BOTH services!** This creates confusion for users who expect a single call to set up everything.

**User Complaints**:
- "AddDotComputeRuntime() doesn't work - IComputeOrchestrator not found"
- "Device discovery shows zero devices" (actually: device properties were incorrectly 0 due to struct offset bug, but exacerbated by unclear API)

### Root Cause

The two methods have:
- Identical names
- Similar signatures
- Overlapping but conflicting namespaces
- Different registration behavior

Users importing `using DotCompute.Runtime;` get method #1, which doesn't provide orchestrator functionality shown in documentation examples.

### Verification

Test code confirming the issue:

```csharp
// Test 1: using DotCompute.Runtime;
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>(); // ✅ Works
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>(); // ❌ FAILS!
```

```csharp
// Test 2: using DotCompute.Runtime.Extensions;
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>(); // ❌ FAILS!
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>(); // ✅ Works
```

## Recommended Workaround (v0.4.0-rc2)

**Use manual service registration** for reliable, predictable behavior:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Factories;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Factories;

var host = Host.CreateApplicationBuilder(args);

// Add logging
host.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Configure runtime options
host.Services.Configure<DotComputeRuntimeOptions>(options =>
{
    options.ValidateCapabilities = false;
    options.AcceleratorLifetime = ServiceLifetime.Transient;
});

// Manually register accelerator factory
host.Services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();

var app = host.Build();

// This pattern works reliably
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>(); // ✅ Always works
```

### For IComputeOrchestrator Usage

If you specifically need `IComputeOrchestrator`, use the Extensions namespace AND manually add the factory:

```csharp
using DotCompute.Runtime.Extensions;
using DotCompute.Runtime.Factories;

// Register both
host.Services.AddSingleton<IUnifiedAcceleratorFactory, DefaultAcceleratorFactory>();
host.Services.AddDotComputeRuntime(); // From Extensions namespace

// Now both work
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>(); // ✅
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>(); // ✅
```

## Planned Fix (Future Release)

This will be addressed in a future release by:
1. Consolidating both methods into a single, comprehensive registration
2. Deprecating the conflicting namespace versions
3. Providing clear migration guidance

## Additional Issues Fixed in v0.4.0-rc2

### CUDA Device Properties Struct Offset Bug (FIXED)

**Problem**: CUDA devices reported 0 MB memory and 0 SMs instead of actual values (e.g., 8 GB, 24 SMs).

**Cause**: CUDA 12.0+ added two new fields (`luid` and `luidDeviceNodeMask`) that shifted all subsequent fields by 16 bytes. C# P/Invoke struct had incorrect offsets.

**Fix**: Updated `CudaDeviceProperties.cs` with correct field offsets for CUDA 12.0+ compatibility.

**Status**: ✅ FIXED - Devices now correctly report memory and SM count.

## Documentation Updates

All documentation has been updated to reflect the manual registration pattern:

- ✅ `docs/articles/examples/WORKING_REFERENCE.md` - Updated with namespace conflict warning
- ✅ `docs/articles/quick-start.md` - Updated to manual registration
- ✅ `docs/articles/getting-started.md` - Updated to manual registration

## References

- GitHub Issue: [To be created]
- Test Verification: `/tmp/addruntime-test/RuntimeTest/`
- CUDA Fix Commit: [Previous commit with struct offset fixes]

---

**Last Updated**: 2025-11-05
**Applies To**: DotCompute v0.4.0-rc2
**Status**: Known issue with documented workaround
