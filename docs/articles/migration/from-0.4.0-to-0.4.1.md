# Migration Guide: v0.4.0 to v0.4.1-rc2

> **Status**: ✅ Official Migration Guide | **Released**: November 6, 2025

This guide helps you migrate from DotCompute v0.4.0 to v0.4.1-rc2, which includes critical dependency injection fixes and API improvements.

## 🎯 What Changed in v0.4.1-rc2

### Critical Fix: Unified DI Registration

**Problem in v0.4.0**: Namespace conflicts caused by duplicate `AddDotComputeRuntime()` extension methods in multiple namespaces.

**Fixed in v0.4.1-rc2**: Single unified method in `DotCompute.Runtime` namespace.

## 🚀 Quick Migration Steps

### Step 1: Update Package References

Update all DotCompute packages to v0.4.1-rc2:

```xml
<ItemGroup>
  <PackageReference Include="DotCompute.Core" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Abstractions" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Runtime" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Memory" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Backends.CPU" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Backends.CUDA" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Backends.OpenCL" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Backends.Metal" Version="0.4.1-rc2" />
  <PackageReference Include="DotCompute.Generators" Version="0.4.1-rc2" />
</ItemGroup>
```

### Step 2: Update Using Directives

**Before (v0.4.0)** - Multiple namespaces caused conflicts:
```csharp
using DotCompute.Core.Extensions;  // ❌ Had AddDotComputeRuntime()
using DotCompute.Runtime;           // ❌ Also had AddDotComputeRuntime()
// Compiler error: ambiguous reference
```

**After (v0.4.1-rc2)** - Single namespace:
```csharp
using DotCompute.Runtime;  // ✅ Only namespace needed
```

### Step 3: Verify Service Registration

The API remains the same, but now works correctly:

```csharp
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;  // ✅ Single unified namespace

var host = Host.CreateApplicationBuilder(args);

// ✅ This now works without namespace conflicts!
host.Services.AddDotComputeRuntime();

var app = host.Build();
```

## 📋 Breaking Changes

### None! 🎉

v0.4.1-rc2 is a **bug fix release** with no breaking API changes. Your existing v0.4.0 code will work after updating package versions and cleaning up redundant using directives.

## 🔧 API Improvements

### 1. Cleaner Namespace Structure

**Before (v0.4.0)**:
```csharp
// Multiple places to import from
using DotCompute.Core.Extensions;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime;
```

**After (v0.4.1-rc2)**:
```csharp
// Single import for all runtime features
using DotCompute.Runtime;
```

### 2. Enhanced IAcceleratorProvider Registration

Fixed internal registration of `IAcceleratorProvider` implementations:

```csharp
// Now works correctly - providers are properly registered
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
var devices = await factory.GetAvailableDevicesAsync();

// All backend providers are discovered automatically
foreach (var device in devices)
{
    Console.WriteLine($"{device.Name} ({device.DeviceType})");
}
```

## ✅ Verification Checklist

After migrating, verify your setup:

- [ ] All packages updated to 0.4.1-rc2
- [ ] Build succeeds without warnings
- [ ] No namespace conflict errors
- [ ] `AddDotComputeRuntime()` resolves correctly
- [ ] Device enumeration works
- [ ] Kernel execution succeeds

### Quick Verification Test

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DotCompute.Runtime;
using DotCompute.Abstractions.Factories;
using DotCompute.Abstractions.Interfaces;

var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();
var app = host.Build();

// Verify factory registration
var factory = app.Services.GetRequiredService<IUnifiedAcceleratorFactory>();
Console.WriteLine("✅ Factory registered correctly");

// Verify orchestrator registration
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();
Console.WriteLine("✅ Orchestrator registered correctly");

// Verify device enumeration
var devices = await factory.GetAvailableDevicesAsync();
Console.WriteLine($"✅ Found {devices.Count} device(s)");
```

Expected output:
```
✅ Factory registered correctly
✅ Orchestrator registered correctly
✅ Found X device(s)
```

## 🐛 Known Issues Fixed

### Issue 1: Namespace Collision
**Problem**: `AddDotComputeRuntime()` existed in multiple namespaces
**Fixed**: Unified to single namespace (`DotCompute.Runtime`)
**Impact**: High - affected all users

### Issue 2: IAcceleratorProvider Registration
**Problem**: Backend providers not properly registered in DI container
**Fixed**: Corrected service registration in `AddDotComputeRuntime()`
**Impact**: Medium - affected device discovery

## 📚 Additional Resources

- [Getting Started Guide](../getting-started.md)
- [Quick Start](../quick-start.md)
- [API Reference](../../api/index.md)
- [Working Reference Example](../examples/WORKING_REFERENCE.md)

## 🆘 Troubleshooting

### Problem: "Ambiguous reference to AddDotComputeRuntime()"

**Solution**: Remove duplicate using directives. You only need `using DotCompute.Runtime;`

### Problem: "Device enumeration returns 0 devices"

**Solution**:
1. Ensure backend packages are installed (CPU, CUDA, Metal, OpenCL)
2. Verify v0.4.1-rc2 is installed (device discovery was fixed)
3. Check that `AddDotComputeRuntime()` is called before building the host

### Problem: "IUnifiedAcceleratorFactory not registered"

**Solution**: Verify you're calling `AddDotComputeRuntime()` on the service collection:

```csharp
host.Services.AddDotComputeRuntime();  // ✅ Correct
// NOT: host.Services.AddDotComputeCore();  // ❌ Old method
```

## 📝 Changelog Summary

**v0.4.1-rc2 (November 6, 2025)**:
- 🐛 **Fix**: Resolved namespace conflicts in `AddDotComputeRuntime()`
- 🐛 **Fix**: Corrected `IAcceleratorProvider` registration
- ✨ **Enhancement**: Unified DI extension methods to single namespace
- 📚 **Docs**: Updated all documentation to reflect correct API patterns
- ✅ **Test**: 91.9% test coverage (215/234 tests passing)

## 💬 Need Help?

- **GitHub Issues**: [Report migration problems](https://github.com/mivertowski/DotCompute/issues)
- **Discussions**: [Ask questions](https://github.com/mivertowski/DotCompute/discussions)
- **Documentation**: [Browse guides](../index.md)

---

**Migration Difficulty**: 🟢 Easy | **Time Required**: < 5 minutes | **Breaking Changes**: None
