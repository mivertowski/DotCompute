# Backend Architecture Overview

DotCompute supports multiple compute backends, each optimized for different hardware platforms.

## Available Backends

- **CPU Backend**: SIMD-accelerated execution (AVX2, AVX512, NEON)
- **CUDA Backend**: NVIDIA GPU support (Compute Capability 5.0+)
- **Metal Backend**: Apple Silicon and macOS GPU support
- **OpenCL Backend**: Cross-platform GPU support

## Backend Integration

See [Backend Integration](backend-integration.md) for details on how backends integrate with the core runtime.

## Related Documentation

- [CPU Backend](cpu.md)
- [CUDA Configuration](cuda-configuration.md)
- [Metal Backend](metal.md)
- [Backend Integration](backend-integration.md)
