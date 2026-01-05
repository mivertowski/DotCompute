# Migration Guide: v0.5.x to v0.9.x

**Document Version**: 1.0.0
**Last Updated**: January 5, 2026

---

## Overview

This guide helps you migrate from DotCompute v0.5.x (first stable release) to v0.9.x. This migration involves significant improvements including Ring Kernels, LINQ extensions, and the new analyzer system.

---

## Breaking Changes Summary

| Change | Impact | Migration Effort |
|--------|--------|------------------|
| Kernel attribute system | High | Update all kernels |
| Memory management | Medium | API updates |
| Backend initialization | Medium | Factory pattern |
| Threading model | Low | Review async code |

---

## Step-by-Step Migration

### Step 1: Update to New Kernel System

v0.9 introduces the `[Kernel]` attribute with source generators:

```csharp
// Before (v0.5.x) - Manual kernel definition
public class MyKernel : IKernel
{
    public void Configure(KernelBuilder builder) { ... }
    public void Execute(IKernelContext ctx, float[] a, float[] b, float[] c) { ... }
}

// After (v0.9.x) - Attribute-based with auto-generation
[Kernel]
static void VectorAdd(float[] a, float[] b, float[] c, int n)
{
    int idx = Kernel.ThreadId.X + Kernel.BlockId.X * Kernel.BlockDim.X;
    if (idx < n)
    {
        c[idx] = a[idx] + b[idx];
    }
}
```

**Key benefits**:
- Compile-time validation
- Automatic optimization
- Real-time IDE feedback

### Step 2: Update Memory Management

Unified buffer API changes:

```csharp
// Before (v0.5.x)
using var buffer = new GpuBuffer<float>(accelerator, 1024);
buffer.Upload(hostData);
buffer.Download(hostData);

// After (v0.9.x)
await using var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
await buffer.WriteAsync(hostData);
var result = await buffer.ReadAsync();
```

**Key changes**:
- `GpuBuffer<T>` → `UnifiedBuffer<T>` via `Memory.AllocateAsync`
- Upload/Download → WriteAsync/ReadAsync
- IDisposable → IAsyncDisposable

### Step 3: Update Backend Initialization

Use the new factory pattern:

```csharp
// Before (v0.5.x)
var cuda = new CudaAccelerator();
cuda.Initialize();

// After (v0.9.x)
await using var accelerator = await AcceleratorFactory.CreateAsync();
// Auto-selects best available backend
```

### Step 4: Add Roslyn Analyzers

Enable the new analyzer package for compile-time validation:

```xml
<ItemGroup>
  <PackageReference Include="DotCompute.Analyzers" Version="0.9.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

The analyzers provide:
- DC001-DC012 diagnostic rules
- Real-time feedback in IDE
- Automatic code fixes

### Step 5: Update SIMD Operations (CPU Backend)

New intrinsics API:

```csharp
// Before (v0.5.x)
var result = SimdHelper.Add(vec1, vec2);

// After (v0.9.x)
var result = CpuIntrinsics.Add(vec1, vec2);
// Or use higher-level API:
await accelerator.ExecuteAsync<VectorAddKernel>(a, b, c, n);
```

### Step 6: Adopt Ring Kernels (Optional)

v0.9 introduces the Ring Kernel system for persistent GPU computation:

```csharp
// New in v0.9.x - Persistent GPU actors
await using var runtime = await RingKernelRuntime.CreateAsync(accelerator);

var actor = await runtime.SpawnAsync<MyActor>("worker");
var response = await actor.AskAsync<Request, Response>(request);
```

### Step 7: Use LINQ Extensions (Optional)

GPU-accelerated LINQ operations:

```csharp
// New in v0.9.x - GPU LINQ
var result = await data
    .AsGpuQueryable(accelerator)
    .Where(x => x > threshold)
    .Select(x => x * 2)
    .Sum();
```

---

## Configuration Updates

### Update appsettings.json

```json
// Before (v0.5.x)
{
  "Gpu": {
    "DeviceId": 0
  }
}

// After (v0.9.x)
{
  "DotCompute": {
    "DefaultBackend": "Cuda",
    "DeviceId": 0,
    "EnablePooling": true,
    "PoolSize": 1073741824
  }
}
```

### Update Dependency Injection

```csharp
// Before (v0.5.x)
services.AddSingleton<IGpuContext, CudaContext>();

// After (v0.9.x)
services.AddDotCompute();
// Or with options:
services.AddDotCompute(options =>
{
    options.PreferredBackend = BackendType.Cuda;
});
```

---

## New Features Available

After migration, you can use these new v0.9 features:

### 1. Ring Kernels
Persistent GPU computation with actor model:
```csharp
await using var runtime = await RingKernelRuntime.CreateAsync(accelerator);
var actor = await runtime.SpawnAsync<ProcessingActor>("processor");
```

### 2. GPU LINQ
Familiar LINQ syntax for GPU operations:
```csharp
var sum = await data.AsGpuQueryable(accel).Where(x => x > 0).Sum();
```

### 3. Multi-GPU Support
P2P transfers and NCCL integration:
```csharp
await accelerator.P2P.TransferAsync(srcBuffer, dstBuffer, targetGpu);
```

### 4. Observability
OpenTelemetry integration:
```csharp
services.AddDotCompute(opt => opt.EnableTelemetry = true);
```

---

## Compatibility Notes

### Removed Features

| v0.5 Feature | v0.9 Status | Alternative |
|--------------|-------------|-------------|
| Manual PTX loading | Removed | Use `[Kernel]` attribute |
| Direct CUDA driver calls | Removed | Use abstraction layer |
| Synchronous execution | Deprecated | Use async APIs |

### Experimental Features

| Feature | Status |
|---------|--------|
| OpenCL Backend | Experimental |
| Metal Backend | Experimental |
| Blazor WebAssembly | Experimental |

---

## Troubleshooting

### Issue: Kernel not found

**Cause**: Source generator not running
**Solution**:
1. Clean solution
2. Ensure `<EnableSourceGenerators>true</EnableSourceGenerators>` in project
3. Rebuild

### Issue: Memory access violations

**Cause**: Bounds checking changes
**Solution**: Add explicit bounds checks in kernels:
```csharp
if (idx < n) { /* safe access */ }
```

### Issue: Async deadlocks

**Cause**: Mixing sync/async patterns
**Solution**: Use `await` consistently, avoid `.Result` or `.Wait()`

---

## Migration Checklist

- [ ] Update all package references to v0.9.x
- [ ] Convert kernel classes to `[Kernel]` methods
- [ ] Update buffer creation to use `AllocateAsync`
- [ ] Replace sync operations with async equivalents
- [ ] Update configuration files
- [ ] Enable Roslyn analyzers
- [ ] Fix all analyzer warnings
- [ ] Run test suite
- [ ] Performance benchmark comparison

---

## Support

- **Documentation**: https://mivertowski.github.io/DotCompute/
- **GitHub**: https://github.com/mivertowski/DotCompute
- **Issues**: Tag with `migration`
