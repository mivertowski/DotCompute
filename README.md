# DotCompute

[![CI/CD](https://github.com/mivertowski/DotCompute/actions/workflows/main.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions/workflows/main.yml)
[![CodeQL](https://github.com/mivertowski/DotCompute/actions/workflows/codeql.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/mivertowski/DotCompute/branch/main/graph/badge.svg)](https://codecov.io/gh/mivertowski/DotCompute)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![Native AOT](https://img.shields.io/badge/Native%20AOT-Ready-brightgreen)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)
[![NuGet](https://img.shields.io/nuget/v/DotCompute.Core.svg)](https://www.nuget.org/packages/DotCompute.Core/)

**A native AOT-first universal compute framework for .NET 9+**

DotCompute is a high-performance, cross-platform compute framework designed from the ground up for .NET 9's Native AOT compilation. It provides a unified API for compute acceleration across multiple backends with production-ready CPU and CUDA support.

## 🚀 Quick Start

```bash
# Install DotCompute
dotnet add package DotCompute.Core --version 0.1.0-alpha.1
dotnet add package DotCompute.Backends.CPU --version 0.1.0-alpha.1   # Production Ready
dotnet add package DotCompute.Backends.CUDA --version 0.1.0-alpha.1  # Production Ready
```

```csharp
using DotCompute;

// Define a kernel
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

// Execute with automatic backend selection
var services = new ServiceCollection()
    .AddDotCompute()
    .AddCpuBackend()
    .AddCudaBackend()  // Automatic GPU detection
    .BuildServiceProvider();

var compute = services.GetRequiredService<IComputeService>();
var result = await compute.ExecuteAsync("VectorAdd", new { a, b, length = 1000 });
```

## ✨ Key Features

### 🎯 **Native AOT First**
- **Zero Runtime Codegen**: All kernels compiled at build time
- **Single File Deployment**: Self-contained executables under 10MB
- **Sub-10ms Startup**: Instant application launch (3ms achieved)
- **Memory Efficient**: < 1MB framework overhead (0.8MB achieved)

### ⚡ **Production Performance**
- **CPU Backend**: SIMD vectorization with AVX512/AVX2/NEON support (8-23x speedup)
- **CUDA Backend**: Complete GPU acceleration with RTX 2000 Ada support
- **Memory Pooling**: 90%+ allocation reduction through intelligent reuse
- **Zero-Copy Operations**: Direct memory access with unified buffers

### 🌐 **Backend Support**
- **CPU**: ✅ **Production Ready** - Multi-threaded with SIMD vectorization
- **CUDA**: ✅ **Production Ready** - Complete implementation with P2P transfers
- **Metal**: ❌ **Not Implemented** - Placeholder only (planned for future)
- **ROCm**: ❌ **Not Implemented** - Placeholder only (AMD GPU support planned)

### 🔒 **Enterprise Security**
- **Code Validation**: Comprehensive security scanning for kernels
- **Buffer Protection**: Runtime bounds checking and overflow prevention
- **Injection Prevention**: SQL/Command injection detection
- **Plugin Security**: Authenticode signing and malware scanning

## 📊 Performance Benchmarks

| Operation | DotCompute | Scalar C# | Speedup | Platform |
|-----------|------------|-----------|---------|----------|
| Vector Addition (1M) | 187K ticks | 4.33M ticks | **23x faster** | Intel Core Ultra 7 165H |
| Matrix Multiply (512×512) | 89ms | 2,340ms | **26x faster** | AVX512 + Multi-threading |
| Memory Allocation | Pooled | Standard | **93% reduction** | Memory reuse |
| Startup Time | 3ms | N/A | Sub-10ms | Native AOT |

## 🏗️ Architecture

DotCompute follows a modular architecture with clear separation of concerns:

- **Core Layer**: Abstract interfaces and unified API
- **Backend Layer**: CPU, CUDA, and future GPU implementations
- **Memory Layer**: Unified memory management with pooling
- **Plugin Layer**: Hot-reload capable extension system
- **Runtime Layer**: Service registration and dependency injection

## 📦 Package Structure

| Package | Description | Status |
|---------|-------------|---------|
| `DotCompute.Core` | Core abstractions and runtime | ✅ Production Ready |
| `DotCompute.Backends.CPU` | CPU vectorization backend | ✅ Production Ready |
| `DotCompute.Backends.CUDA` | NVIDIA CUDA backend | ✅ Production Ready |
| `DotCompute.Memory` | Unified memory system | ✅ Production Ready |
| `DotCompute.Plugins` | Plugin system | ✅ Production Ready |
| `DotCompute.Generators` | Source generators | ✅ Production Ready |
| `DotCompute.Algorithms` | Algorithm library | 🚧 Basic Implementation |
| `DotCompute.Linq` | LINQ query provider | 🚧 CPU Fallback Working |
| `DotCompute.Runtime` | Runtime orchestration | 🚧 Service Stubs |

## 🚀 Getting Started

### Prerequisites
- **.NET 9.0 SDK** or later
- **Visual Studio 2022 17.8+** or VS Code with C# extension
- **For CUDA**: CUDA Toolkit 12.0+ and NVIDIA GPU with Compute Capability 5.0+

### Installation & Usage

See our comprehensive guides:
- **[Getting Started](docs/GETTING_STARTED.md)** - Step-by-step tutorial
- **[API Reference](docs/API.md)** - Complete API documentation
- **[Architecture](docs/ARCHITECTURE.md)** - System design overview
- **[Performance](docs/PERFORMANCE.md)** - Optimization and benchmarks
- **[Development](docs/DEVELOPMENT.md)** - Contributing guidelines

## 🧪 Testing & Quality

- **19,000+ lines** of comprehensive test code
- **~75% code coverage** with proper measurement
- **Professional test organization**: Unit/Integration/Hardware/Shared
- **Hardware test suites**: Real GPU validation
- **920+ security tests**: Comprehensive validation
- **CI/CD pipeline**: Multi-platform automation

## 📈 Current Status

### ✅ Production Ready
- CPU compute with 8-23x SIMD acceleration
- CUDA compute with complete GPU support
- Memory management with pooling and P2P transfers
- Plugin system with hot-reload capability
- Native AOT compatibility with sub-10ms startup

### 🚧 In Development
- Metal backend for Apple Silicon
- ROCm backend for AMD GPUs
- LINQ provider GPU compilation
- Advanced algorithm libraries

### ⚠️ Known Limitations
- Metal backend contains stubs only
- ROCm backend is placeholder
- Hardware testing requires NVIDIA GPU
- Cross-platform GPU limited to NVIDIA currently

## 🤝 Contributing

We welcome contributions! Please see our [Development Guide](docs/DEVELOPMENT.md) for details on:
- Development setup
- Coding standards
- Testing requirements
- Pull request process

## 🔒 Security

DotCompute implements comprehensive security measures. Report vulnerabilities to: security@dotcompute.dev

See our [Security Policy](docs/SECURITY.md) for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Links

- **[Documentation](docs/)** - Complete documentation
- **[NuGet Packages](https://www.nuget.org/packages?q=DotCompute)** - Official packages
- **[GitHub Issues](../../issues)** - Bug reports and features
- **[Releases](../../releases)** - Version history

---

**Built with ❤️ for the .NET community**

*DotCompute - Production-ready GPU acceleration for .NET*