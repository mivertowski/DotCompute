# DotCompute Build Troubleshooting Guide

This guide helps you resolve common build and runtime issues when working with DotCompute.

## 🚨 Current Build Status

**Important**: DotCompute is under active development. The CPU backend is production-ready, while GPU backends have compilation errors that are being resolved.

### ✅ What Should Build Successfully
- **Core packages**: DotCompute.Core, DotCompute.Abstractions, DotCompute.Memory
- **CPU backend**: DotCompute.Backends.CPU (production-ready)
- **Plugin system**: DotCompute.Plugins, DotCompute.Generators
- **Unit tests**: Most test projects in tests/Unit/
- **Sample applications**: Basic CPU-focused samples

### 🚧 What Currently Has Build Issues
- **GPU backends**: CUDA and Metal backends have interface compatibility issues
- **LINQ provider**: Expression compilation has ambiguous references
- **Hardware tests**: Some GPU test projects need API updates
- **Integration tests**: Some end-to-end scenarios under development

## 🛠️ Quick Fixes

### For Production Use (CPU Backend Only)

```bash
# Clean and restore
dotnet clean
dotnet restore

# Build only stable projects
dotnet build src/DotCompute.Core/
dotnet build src/DotCompute.Abstractions/
dotnet build src/DotCompute.Memory/
dotnet build plugins/backends/DotCompute.Backends.CPU/
dotnet build src/DotCompute.Plugins/

# Run CPU-focused tests
dotnet test tests/Unit/**/*.csproj
```

### For Development Work

```bash
# Ignore GPU backend errors temporarily
dotnet build --configuration Release --verbosity minimal || echo "Some GPU projects failed - this is expected during development"

# Run tests that work
dotnet test tests/Unit/DotCompute.Core.Tests/
dotnet test tests/Unit/DotCompute.Memory.Tests/
dotnet test tests/Integration/DotCompute.Integration.Tests/
```

## 🔧 Common Issues & Solutions

### Issue 1: CS0535 - Missing Interface Members

**Error**: `'ClassName' does not implement interface member 'IInterface.Member'`

**Cause**: Interface definitions have been updated but implementations lag behind.

**Solution**: 
- For CPU backend: This should be resolved - please report if you see this
- For GPU backends: This is a known issue being worked on
- Temporarily exclude problematic projects from build

### Issue 2: CS0104 - Ambiguous References

**Error**: `'IKernel' is an ambiguous reference between 'DotCompute.Linq.Operators.IKernel' and 'DotCompute.Abstractions.IKernel'`

**Cause**: Duplicate interface definitions in different namespaces.

**Solution**:
```csharp
// Use fully qualified names
using DotCompute.Abstractions.IKernel explicitKernel = ...;

// Or add using alias
using LinqKernel = DotCompute.Linq.Operators.IKernel;
```

### Issue 3: CS0101 - Duplicate Type Definitions

**Error**: `The namespace 'X' already contains a definition for 'Y'`

**Cause**: Class definitions duplicated in same namespace.

**Solution**: Check for multiple partial class declarations missing `partial` keyword.

### Issue 4: Missing NuGet Packages

**Error**: Various package reference errors

**Solution**:
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore with force
dotnet restore --force

# Check package references in Directory.Build.props
cat Directory.Build.props
```

## 🎯 Recommended Build Strategy

### For New Users (CPU Only)
1. **Start with minimal setup**:
   ```bash
   dotnet new console -n TestApp
   cd TestApp
   dotnet add package DotCompute.Core
   dotnet add package DotCompute.Backends.CPU
   ```

2. **Use stable APIs only**:
   - Focus on CPU backend features
   - Avoid GPU-specific functionality until backends are stable
   - Use plugin system for extensibility

### For Contributors (Full Development)
1. **Accept partial build failures**:
   - GPU backends are under active development
   - Focus on components you're working on
   - Use conditional compilation if needed

2. **Use selective building**:
   ```bash
   # Build specific projects
   dotnet build src/DotCompute.Core/
   dotnet build plugins/backends/DotCompute.Backends.CPU/
   
   # Test specific areas
   dotnet test tests/Unit/DotCompute.Core.Tests/
   ```

## 🧪 Testing Strategy

### Stable Tests (Always Run)
```bash
# Core functionality tests
dotnet test tests/Unit/DotCompute.Core.Tests/
dotnet test tests/Unit/DotCompute.Memory.Tests/
dotnet test tests/Unit/DotCompute.Abstractions.Tests/

# CPU backend tests
dotnet test plugins/backends/DotCompute.Backends.CPU/tests/
```

### Hardware Tests (Conditional)
```bash
# Only run if you have specific hardware
dotnet test tests/Hardware/DotCompute.Hardware.Cuda.Tests/  # Requires NVIDIA GPU
dotnet test tests/Hardware/DotCompute.Hardware.RTX2000.Tests/  # Requires RTX 2000 Ada
```

### Mock Tests (CI/CD)
```bash
# For environments without hardware
dotnet test tests/Hardware/DotCompute.Hardware.Mock.Tests/
```

## 🔍 Debugging Build Issues

### Check Prerequisites
```bash
# Verify .NET version
dotnet --version  # Should be 9.0.100 or later

# Check SDK installation
dotnet --list-sdks

# Verify project references
find . -name "*.csproj" -exec grep -l "ProjectReference" {} \;
```

### Examine Build Output
```bash
# Verbose build output
dotnet build --verbosity diagnostic > build.log 2>&1

# Look for specific error patterns
grep -E "(error|Error|ERROR)" build.log
```

### Check Dependencies
```bash
# List all package references
find . -name "*.csproj" -exec grep -H "PackageReference" {} \;

# Check for version conflicts
dotnet list package --outdated
dotnet list package --deprecated
```

## 🚀 Performance Troubleshooting

### CPU Performance Issues

**Issue**: CPU kernels not showing expected speedup

**Solutions**:
1. **Enable vectorization**:
   ```csharp
   services.AddCpuBackend(options => {
       options.EnableVectorization = true;
       options.ThreadCount = Environment.ProcessorCount;
   });
   ```

2. **Check SIMD support**:
   ```csharp
   Console.WriteLine($"Vector support: {Vector.IsHardwareAccelerated}");
   Console.WriteLine($"Vector<float> count: {Vector<float>.Count}");
   ```

3. **Verify data alignment**:
   - Use properly sized arrays (multiples of vector width)
   - Ensure data is properly aligned for SIMD operations

### Memory Performance Issues

**Issue**: High memory allocation or leaks

**Solutions**:
1. **Enable memory pooling**:
   ```csharp
   services.AddUnifiedMemory(options => {
       options.EnablePooling = true;
       options.MaxPoolSize = 1_000_000_000; // 1GB
   });
   ```

2. **Check buffer disposal**:
   ```csharp
   using var buffer = memoryManager.Allocate<float>(size);
   // Automatic disposal
   ```

3. **Monitor memory usage**:
   ```csharp
   var stats = memoryManager.GetStatistics();
   Console.WriteLine($"Pool efficiency: {stats.PoolHitRate:P}");
   ```

## 📞 Getting Help

### Before Reporting Issues
1. **Check this troubleshooting guide**
2. **Review the [Implementation Status](./IMPLEMENTATION_STATUS.md)**
3. **Try building only stable components first**
4. **Check if it's a known development issue**

### When Reporting Issues
Include:
- Operating system and architecture
- .NET version (`dotnet --version`)
- Complete error message and stack trace
- Minimal reproduction case
- Whether you're using CPU-only or GPU features

### Community Resources
- **GitHub Issues**: [Report bugs and feature requests](../../issues)
- **GitHub Discussions**: [Community support and questions](../../discussions)
- **Documentation**: [Complete guides](./guide-documentation/)
- **Contributing**: [Development guidelines](./project-management/project-contributing-guidelines.md)

---

**Note**: DotCompute is under active development. The CPU backend is production-ready, while GPU backends are being stabilized. This troubleshooting guide will be updated as development progresses.