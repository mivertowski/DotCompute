# Known Limitations and Workarounds

**Document Version**: 1.0
**Last Updated**: November 5, 2025
**DotCompute Version**: 0.2.0-alpha

This document provides a comprehensive list of known limitations, planned features, and recommended workarounds for DotCompute v0.2.0-alpha.

---

## 📊 Feature Status Legend

- ✅ **Production Ready**: Fully implemented, tested, and supported for production use
- ⚠️ **Preview/Experimental**: Implemented but may have limitations or API changes in future versions
- 🚧 **In Development**: Partially implemented, not recommended for production
- 📋 **Planned**: Documented but not yet implemented

---

## 🎯 Component Status Overview

| Component | Status | Notes |
|-----------|--------|-------|
| **CPU Backend** | ✅ Production | SIMD-optimized (AVX2/AVX512/NEON), full feature set |
| **CUDA Backend** | ✅ Production | Compute Capability 5.0-8.9, P2P, NCCL support |
| **OpenCL Backend** | ⚠️ Preview | Cross-platform GPU support, limited advanced features |
| **Metal Backend** | 🚧 In Development | Native API complete, MSL compilation 60% complete |
| **Ring Kernel System** | ✅ Production | All backends implemented (CUDA, OpenCL, Metal, CPU) |
| **Source Generators** | ✅ Production | [Kernel] attribute, automatic optimization |
| **Roslyn Analyzers** | ✅ Production | 12 diagnostic rules, 5 automated fixes |
| **LINQ GPU (Basic)** | ✅ Production | Select, Where, Aggregate working across all GPU backends |
| **LINQ GPU (Advanced)** | 🚧 In Development | GroupBy, Join, OrderBy planned for v0.3.0 |
| **Security/Crypto** | ✅ Production | AES-256-GCM, ChaCha20-Poly1305, RSA, ECDSA implemented |
| **Plugin System** | ⚠️ Preview | Hot-reload working, security validation basic |
| **Telemetry** | ⚠️ Preview | Core metrics implemented, Prometheus integration partial |
| **ROCm Backend** | 📋 Planned | AMD GPU support planned for v0.4.0 |

---

## 🔴 Critical Limitations

### 1. Interface Design Issues (RESOLVED in v0.2.0-alpha)

**Issue**: IGpuKernelGenerator interface violated Interface Segregation Principle  
**Status**: ✅ RESOLVED - Deprecated in favor of backend-specific interfaces  
**Workaround**: Use new interfaces:
- `ICudaKernelGenerator` for CUDA backend
- `IOpenCLKernelGenerator` for OpenCL backend
- `IMetalKernelGenerator` for Metal backend

**Migration Example**:
\`\`\`csharp
// Old (deprecated)
IGpuKernelGenerator generator = new CudaKernelGenerator();

// New (recommended)
ICudaKernelGenerator cudaGenerator = new CudaKernelGenerator();
IOpenCLKernelGenerator openclGenerator = new OpenCLKernelGenerator();
IMetalKernelGenerator metalGenerator = new MetalKernelGenerator();
\`\`\`

### 2. Metal Backend - MSL Compilation

**Issue**: Metal Shading Language (MSL) code generation pipeline incomplete  
**Status**: 🚧 In Development (60% complete)  
**Impact**: Metal kernels currently use precompiled \`.metallib\` files  
**Workaround**: Use CPU or CUDA backend for dynamic kernel compilation  
**Target**: v0.3.0 completion

### 3. Device Enumeration Native Library Path

**Issue**: Native libraries (e.g., \`libDotComputeMetal.dylib\`) not found when using \`dotnet run\`  
**Status**: ✅ RESOLVED - Use run script or execute from output directory  
**Workaround**:
\`\`\`bash
# Option 1: Run from output directory
cd bin/Release/net9.0 && ./YourApp

# Option 2: Use provided script
./run.sh

# Option 3: Set DYLD_LIBRARY_PATH (macOS)
export DYLD_LIBRARY_PATH="./bin/Release/net9.0"
dotnet run
\`\`\`

---

For complete documentation, see [https://mivertowski.github.io/DotCompute/](https://mivertowski.github.io/DotCompute/)

