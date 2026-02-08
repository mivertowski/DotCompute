# Migration Guide: v0.9.x to v1.0.0

> **DRAFT** — This document describes planned features for a future release. Current version: v0.6.2.

**Document Version**: 1.0.0
**Last Updated**: January 5, 2026

---

## Overview

This guide helps you migrate your DotCompute applications from v0.9.x to v1.0.0. The v1.0.0 release includes API stabilization, performance improvements, and a few breaking changes.

---

## Breaking Changes Summary

| Change | Impact | Migration Effort |
|--------|--------|------------------|
| Namespace consolidation | High | Search & Replace |
| Buffer API refinements | Medium | Code updates |
| Async pattern updates | Low | Minor adjustments |
| Removed deprecated APIs | Low | Use replacements |

---

## Step-by-Step Migration

### Step 1: Update Package References

Update your `.csproj` files:

```xml
<!-- Before (v0.9.x) -->
<PackageReference Include="DotCompute" Version="0.9.0" />

<!-- After (v1.0.0) -->
<PackageReference Include="DotCompute" Version="1.0.0" />
```

### Step 2: Update Namespaces

Some namespaces have been consolidated:

```csharp
// Before (v0.9.x)
using DotCompute.Core;
using DotCompute.Core.Memory;
using DotCompute.Core.Kernels;

// After (v1.0.0)
using DotCompute;
using DotCompute.Memory;
using DotCompute.Kernels;
```

**Automated fix**: Use regex find/replace:
- Find: `using DotCompute\.Core\.`
- Replace: `using DotCompute.`

### Step 3: Update Buffer Creation

The buffer creation API has been simplified:

```csharp
// Before (v0.9.x)
var buffer = accelerator.Memory.CreateBuffer<float>(1024);
await buffer.InitializeAsync();

// After (v1.0.0)
var buffer = await accelerator.Memory.AllocateAsync<float>(1024);
```

**Key changes**:
- `CreateBuffer` → `AllocateAsync` (combines creation + initialization)
- Buffer creation is now always async
- `InitializeAsync` method removed (automatic)

### Step 4: Update Kernel Execution

Kernel execution has been streamlined:

```csharp
// Before (v0.9.x)
await accelerator.LaunchKernelAsync<MyKernel>(
    gridDim: (1024, 1, 1),
    blockDim: (256, 1, 1),
    args: new object[] { buffer1, buffer2, n });

// After (v1.0.0)
await accelerator.ExecuteAsync<MyKernel>(buffer1, buffer2, n);
// Grid/block dims are auto-calculated, or specify explicitly:
await accelerator.ExecuteAsync<MyKernel>(
    new LaunchConfig(grid: 1024, block: 256),
    buffer1, buffer2, n);
```

**Key changes**:
- `LaunchKernelAsync` → `ExecuteAsync`
- Auto grid/block calculation by default
- Type-safe parameters (no object array)

### Step 5: Update Memory Transfers

Data transfer API is now more explicit:

```csharp
// Before (v0.9.x)
buffer.CopyFrom(hostArray);
buffer.CopyTo(hostArray);

// After (v1.0.0)
await buffer.WriteAsync(hostArray);
var result = await buffer.ReadAsync();
```

**Key changes**:
- Synchronous transfers removed
- `CopyFrom` → `WriteAsync`
- `CopyTo` → `ReadAsync` (returns new array)

### Step 6: Update Exception Handling

Exception types have been consolidated:

```csharp
// Before (v0.9.x)
try { ... }
catch (CudaException ex) { ... }
catch (OpenCLException ex) { ... }
catch (ComputeException ex) { ... }

// After (v1.0.0)
try { ... }
catch (DotComputeException ex)
{
    // Check specific type if needed
    if (ex is BackendException backendEx)
    {
        Console.WriteLine($"Backend error: {backendEx.BackendType}");
    }
}
```

### Step 7: Update Accelerator Discovery

Device enumeration has been improved:

```csharp
// Before (v0.9.x)
var devices = AcceleratorDevice.EnumerateAll();
var accelerator = new CudaAccelerator(devices.First(d => d.Type == DeviceType.Cuda));

// After (v1.0.0)
var accelerator = await AcceleratorFactory.CreateAsync();  // Auto-select best
// Or specific selection:
var accelerator = await AcceleratorFactory.CreateAsync(new AcceleratorOptions
{
    PreferredBackend = BackendType.Cuda,
    MinComputeCapability = new Version(7, 0)
});
```

### Step 8: Remove Deprecated API Usage

The following deprecated APIs have been removed:

| Removed | Replacement |
|---------|-------------|
| `Accelerator.Sync()` | `await accelerator.SynchronizeAsync()` |
| `Buffer.Length` | `Buffer.Count` |
| `Kernel.Launch()` | `await accelerator.ExecuteAsync<T>()` |
| `MemoryPool.Alloc()` | `await pool.AllocateAsync()` |
| `ComputeContext` | `IAccelerator` |

---

## Configuration Changes

### appsettings.json Updates

```json
// Before (v0.9.x)
{
  "DotCompute": {
    "DefaultBackend": "Cuda",
    "PoolSize": 1073741824
  }
}

// After (v1.0.0)
{
  "DotCompute": {
    "Accelerator": {
      "PreferredBackend": "Cuda",
      "Fallback": "Cpu"
    },
    "Memory": {
      "PoolSizeBytes": 1073741824,
      "EnablePooling": true
    }
  }
}
```

### Dependency Injection Updates

```csharp
// Before (v0.9.x)
services.AddDotCompute();

// After (v1.0.0)
services.AddDotCompute(options =>
{
    options.PreferredBackend = BackendType.Cuda;
    options.EnableMemoryPooling = true;
});
```

---

## Performance Considerations

### Memory Pool Behavior

v1.0.0 enables memory pooling by default. If you see memory issues:

```csharp
// Disable pooling if needed
services.AddDotCompute(options =>
{
    options.EnableMemoryPooling = false;
});
```

### Kernel Caching

Kernel compilation is now cached by default:

```csharp
// Clear cache if experiencing issues
accelerator.KernelCache.Clear();

// Disable caching
services.AddDotCompute(options =>
{
    options.EnableKernelCaching = false;
});
```

---

## Compatibility Matrix

| v0.9.x Feature | v1.0.0 Status |
|----------------|---------------|
| CUDA Backend | ✅ Supported |
| OpenCL Backend | ⚠️ Experimental |
| Metal Backend | ⚠️ Experimental |
| CPU Backend | ✅ Supported |
| Ring Kernels | ✅ Supported |
| LINQ Extensions | ✅ Supported |
| Auto-Diff | ✅ New in v1.0 |
| Sparse Matrices | ✅ New in v1.0 |

---

## Troubleshooting

### Issue: "Method not found" at runtime

**Cause**: Mixed package versions
**Solution**: Ensure all DotCompute packages are v1.0.0:

```bash
dotnet list package | grep DotCompute
```

### Issue: Compilation errors after migration

**Cause**: API signature changes
**Solution**: Check the specific error and refer to the changes above

### Issue: Performance regression

**Cause**: Different default settings
**Solution**: Compare configurations and adjust memory/caching settings

---

## Need Help?

- **Documentation**: https://mivertowski.github.io/DotCompute/
- **GitHub Issues**: https://github.com/mivertowski/DotCompute/issues
- **Migration Support**: Tag issues with `migration`

---

## Changelog Reference

See [CHANGELOG.md](../../CHANGELOG.md) for complete v1.0.0 changes.
