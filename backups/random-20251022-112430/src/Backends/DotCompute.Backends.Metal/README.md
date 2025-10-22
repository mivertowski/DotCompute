# DotCompute.Backends.Metal

Metal compute backend for .NET 9+ targeting Apple Silicon and macOS GPU acceleration.

## Current Status: 🚧 Foundation Implementation

The Metal backend provides foundational infrastructure with native Metal API integration. While core components are implemented, the backend requires additional development for production use.

### Implementation Overview

#### ✅ Implemented Components
- **Native Metal Integration**: P/Invoke bindings to Metal framework via `libDotComputeMetal.dylib`
- **Device Management**: Metal device enumeration and capability detection
- **Memory Management**: Buffer allocation with unified memory support
- **Kernel Infrastructure**: Compilation pipeline and execution framework
- **Service Architecture**: Dependency injection and plugin registration
- **Telemetry System**: Performance monitoring and metrics collection
- **Error Handling**: Comprehensive error recovery and retry mechanisms

#### 🚧 Partial Implementation
- **Kernel Compilation**: Infrastructure present, MSL compilation requires completion
- **Command Execution**: Command buffer and queue management implemented, execution pipeline needs integration
- **Memory Transfers**: Basic buffer operations implemented, optimization needed
- **Performance Profiling**: Telemetry framework ready, Metal-specific profiling pending

#### ❌ Not Yet Implemented
- **Metal Shading Language**: OpenCL C to MSL translation pipeline
- **Compute Pipeline States**: Metal compute pipeline creation and caching
- **Advanced Features**: Metal Performance Shaders integration
- **Production Optimization**: Performance tuning for Apple Silicon

## Architecture

The Metal backend follows DotCompute's standard architecture with Metal-specific implementations:

```
DotCompute.Backends.Metal/
├── Execution/           # Command buffer and execution management
├── Factory/            # Component factory patterns
├── Kernels/            # Kernel compilation and caching
├── Memory/             # Metal buffer management
├── native/             # Native Metal API integration (C++/Objective-C++)
├── Registration/       # Service registration
├── Telemetry/         # Performance monitoring
└── Utilities/         # Support utilities
```

### Native Integration

The backend includes a native library (`libDotComputeMetal.dylib`) built with:
- Objective-C++ for Metal framework access
- CMake build system integration
- Direct Metal API calls for device and buffer management

### Key Classes

- `MetalAccelerator`: Main accelerator implementation with device management
- `MetalBackend`: Backend initialization and capability detection
- `MetalKernelCompiler`: Kernel compilation infrastructure
- `MetalMemoryManager`: GPU memory allocation and management
- `MetalNative`: P/Invoke declarations for native Metal calls
- `MetalExecutionEngine`: Command buffer execution management
- `MetalTelemetryProvider`: Performance metrics collection

## Current Capabilities

### Device Support
- Detects Apple Silicon (M1/M2/M3) unified memory architecture
- Enumerates Metal-capable devices
- Reports device capabilities and limits
- Manages multiple command queues

### Memory Management
- Allocates Metal buffers via native API
- Supports unified memory on Apple Silicon
- Implements memory pooling infrastructure
- Provides host-device transfer mechanisms

### Execution Framework
- Command buffer creation and management
- Multi-queue support for concurrent execution
- Error handling with retry policies
- Performance telemetry integration

## Testing Infrastructure

Comprehensive test suite with 30+ tests covering:
- Hardware detection and capability verification
- Memory allocation and stress testing
- Concurrent execution scenarios
- Performance benchmarking
- Error handling and recovery
- Integration with DotCompute core

See `tests/Hardware/DotCompute.Hardware.Metal.Tests/` for test implementation.

## Usage Example

```csharp
// Current usage - foundational components work
var options = Options.Create(new MetalAcceleratorOptions 
{
    PreferredDeviceIndex = 0,
    EnableProfiling = true
});

var accelerator = new MetalAccelerator(options, logger);
await accelerator.InitializeAsync();

// Device information available
var info = accelerator.DeviceInfo;
Console.WriteLine($"Device: {info.Name}");
Console.WriteLine($"Memory: {info.GlobalMemorySize / (1024*1024*1024)}GB");

// Memory allocation works
var buffer = await accelerator.AllocateAsync<float>(1024 * 1024);

// Kernel compilation infrastructure ready (MSL compilation pending)
var kernel = await accelerator.CompileKernelAsync(kernelDefinition);
```

## Development Roadmap

### Phase 1: Core Completion (Current Focus)
- [ ] Complete MSL compilation from OpenCL C
- [ ] Integrate compute pipeline state creation
- [ ] Implement kernel argument binding
- [ ] Complete execution pipeline

### Phase 2: Optimization
- [ ] Memory transfer optimization for Apple Silicon
- [ ] Kernel caching and binary storage
- [ ] Thread group size optimization
- [ ] Performance profiling integration

### Phase 3: Advanced Features
- [ ] Metal Performance Shaders integration
- [ ] Multi-GPU support
- [ ] Indirect command buffers
- [ ] Resource heaps and tier support

### Phase 4: Production Readiness
- [ ] Comprehensive error recovery
- [ ] Production performance tuning
- [ ] Documentation completion
- [ ] Certification testing

## Building

### Prerequisites
- macOS 12.0+ (Monterey or later)
- Xcode 14+ with Metal development tools
- .NET 9.0 SDK
- CMake 3.20+

### Build Instructions
```bash
# Build native library
cd src/Backends/DotCompute.Backends.Metal/native
mkdir build && cd build
cmake ..
make

# Build .NET project
dotnet build src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj
```

### Running Tests
```bash
# Run Metal-specific tests
dotnet test tests/Hardware/DotCompute.Hardware.Metal.Tests/DotCompute.Hardware.Metal.Tests.csproj

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"
```

## Limitations

### Current Limitations
- MSL compilation pipeline not complete
- Kernel execution uses CPU fallback for complex operations
- Performance optimization pending for production workloads
- Limited to basic Metal features

### Platform Requirements
- macOS only (Metal is Apple-specific)
- Requires Metal-capable hardware
- Best performance on Apple Silicon (M1/M2/M3)

## Contributing

The Metal backend welcomes contributions in the following areas:
1. MSL compilation pipeline completion
2. Performance optimization for Apple Silicon
3. Test coverage expansion
4. Documentation improvements

See [CONTRIBUTING.md](../../../CONTRIBUTING.md) for contribution guidelines.

## Technical Notes

### Memory Architecture
The implementation leverages Apple Silicon's unified memory architecture where available, providing efficient CPU-GPU memory sharing without explicit transfers.

### Threading Model
Follows Metal's threading model with:
- Thread groups for work distribution
- SIMD groups for wavefront execution
- Threadgroup memory for local data sharing

### Error Handling
Comprehensive error handling includes:
- Automatic retry for transient failures
- Graceful degradation for unsupported features
- Detailed error reporting via telemetry

## Support

For issues specific to the Metal backend:
1. Check test suite for examples and validation
2. Review native library logs in `build/` directory
3. Enable debug logging via `MetalAcceleratorOptions.EnableDebugLogging`
4. File issues with Metal-specific tags in the main repository

## License

The Metal backend is part of the DotCompute project and follows the same MIT license. See [LICENSE](../../../LICENSE) for details.