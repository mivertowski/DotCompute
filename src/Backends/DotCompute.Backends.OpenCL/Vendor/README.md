# OpenCL Vendor Abstraction Layer

This directory contains the multi-vendor abstraction layer for DotCompute's OpenCL backend, enabling production-ready support for NVIDIA, AMD, and Intel GPUs from the start.

## Architecture Overview

The vendor abstraction layer consists of:

1. **IOpenCLVendorAdapter** - Common interface for all vendors
2. **Vendor-Specific Adapters** - NVIDIA, AMD, Intel implementations
3. **GenericOpenCLAdapter** - Safe fallback for unknown vendors
4. **VendorAdapterFactory** - Automatic vendor detection and adapter selection

## Supported Vendors

### NVIDIA GPUs
- **File**: `NvidiaOpenCLAdapter.cs`
- **Architecture**: CUDA-based, warp execution (32 threads)
- **Optimizations**:
  - Work groups aligned to warp boundaries (multiples of 32)
  - 128-byte buffer alignment for coalesced access
  - Aggressive math optimizations enabled
  - Out-of-order queue execution
- **Compute Capability**: 5.0+ supported

### AMD GPUs
- **File**: `AmdOpenCLAdapter.cs`
- **Architecture**: GCN/RDNA, wavefront execution (32/64 threads)
- **Optimizations**:
  - Architecture detection (RDNA vs GCN)
  - Wavefront-aligned work groups
  - 256-byte buffer alignment
  - ROCm-optimized compiler flags
- **Supported Generations**: GCN, RDNA, RDNA2, RDNA3, CDNA

### Intel GPUs
- **File**: `IntelOpenCLAdapter.cs`
- **Architecture**: Xe/Gen, SIMD-based execution
- **Optimizations**:
  - Discrete vs integrated GPU detection
  - SIMD-width aligned work groups
  - 64-byte alignment for cache efficiency
  - Conservative compiler flags for compatibility
- **Supported Generations**: Gen9, Gen11, Gen12/Xe-LP, Xe-HPG (Arc), Xe-HPC

### Generic Fallback
- **File**: `GenericOpenCLAdapter.cs`
- **Purpose**: Safe defaults for unknown vendors
- **Strategy**:
  - Conservative resource usage (50% of hardware max)
  - Only trust KHR standard extensions
  - No aggressive optimizations
  - Prioritize correctness over performance

## Key Features

### 1. Automatic Vendor Detection
```csharp
var adapter = VendorAdapterFactory.GetAdapter(platform);
var vendor = VendorAdapterFactory.DetectVendor(platform);
```

### 2. Hardware-Specific Optimizations
Each adapter provides:
- Optimal work group sizes based on hardware execution model
- Local memory usage recommendations
- Queue configuration (in-order vs out-of-order)
- Compiler optimization flags
- Buffer alignment requirements

### 3. Extension Reliability Checking
```csharp
bool reliable = adapter.IsExtensionReliable("cl_khr_fp64", device);
```

Some vendors report extensions but have implementation quirks. The adapters blacklist problematic extensions per vendor.

### 4. Persistent Kernel Support
```csharp
bool supported = adapter.SupportsPersistentKernels(device);
```

Determines if hardware supports efficient persistent kernel patterns for streaming workloads.

## Implementation Details

### Work Group Sizing Strategy

**NVIDIA**:
- Prefer 256 threads (8 warps) for compute-heavy kernels
- Fall back to 128 threads (4 warps) for smaller devices
- Always align to warp boundaries (32 threads)

**AMD**:
- Detect RDNA (32-wide) vs GCN (64-wide) wavefronts
- Prefer 256 threads (8 RDNA or 4 GCN wavefronts)
- Always align to wavefront boundaries

**Intel**:
- Prefer 256 threads for discrete GPUs (Arc series)
- Use 128 threads for integrated GPUs
- Align to SIMD width (16 threads minimum)

**Generic**:
- Use hardware maximum or conservative default
- Minimum 32 threads for reasonable parallelism

### Memory Alignment

| Vendor  | Alignment | Reason                          |
|---------|-----------|--------------------------------|
| NVIDIA  | 128 bytes | Coalesced global memory access |
| AMD     | 256 bytes | Memory controller granularity  |
| Intel   | 64 bytes  | Cache line size                |
| Generic | 128 bytes | Safe compromise                |

### Compiler Optimizations

**NVIDIA**:
```
-cl-mad-enable -cl-fast-relaxed-math -cl-denorms-are-zero
```

**AMD**:
```
-cl-mad-enable -cl-fast-relaxed-math -cl-unsafe-math-optimizations
```

**Intel**:
```
-cl-mad-enable -cl-fast-relaxed-math
```

**Generic**:
```
-cl-mad-enable
```

## Usage Example

```csharp
// Get adapter for platform
var adapter = VendorAdapterFactory.GetAdapter(platform);

// Get optimal work group size
int workGroupSize = adapter.GetOptimalWorkGroupSize(device, defaultSize: 256);

// Apply vendor optimizations to queue
var optimizedProps = adapter.ApplyVendorOptimizations(properties, device);

// Get compiler options
string compilerOpts = adapter.GetCompilerOptions(enableOptimizations: true);

// Check extension reliability
if (adapter.IsExtensionReliable("cl_khr_fp64", device))
{
    // Use double precision
}

// Get buffer alignment
int alignment = adapter.GetRecommendedBufferAlignment(device);
```

## Design Principles

1. **Conservative Fallbacks**: Always provide safe defaults for unknown hardware
2. **Thread Safety**: All adapters are stateless singletons
3. **Production Quality**: Comprehensive error handling and validation
4. **Documentation**: Extensive XML documentation explaining vendor quirks
5. **Extensibility**: Easy to add new vendor adapters

## Testing Considerations

The vendor abstraction layer should be tested with:
- Unit tests for each adapter's logic
- Integration tests on real hardware (NVIDIA, AMD, Intel)
- Fallback testing with mock/unknown vendors
- Performance validation of vendor-specific optimizations

## Future Enhancements

Potential additions:
- Apple Silicon (Metal-based OpenCL, deprecated)
- ARM Mali GPUs (mobile compute)
- Qualcomm Adreno GPUs (mobile compute)
- Additional vendor-specific optimizations as hardware evolves

## Statistics

- **Total Lines**: 963 lines
- **Files**: 6 files
- **Supported Vendors**: 3 major vendors + generic fallback
- **Interface Methods**: 8 optimization hooks
- **Code Coverage Target**: 90%+

## License

Copyright (c) Michael Ivertowski. Licensed under the MIT License.
