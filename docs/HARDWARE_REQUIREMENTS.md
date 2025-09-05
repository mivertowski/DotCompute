# DotCompute Hardware Requirements

## Minimum Requirements (CUDA 13.0+)

### GPU Requirements
- **Architecture**: NVIDIA Turing architecture or newer
- **Compute Capability**: 7.5 or higher
- **Minimum GPU Models**: RTX 2060, RTX 2070, RTX 2080, T4, or newer

### Software Requirements
- **CUDA Toolkit**: 13.0 or newer
- **CUDA Driver**: 535.00 or newer
- **Operating System**: 
  - Windows 10/11 (64-bit)
  - Linux (Ubuntu 20.04/22.04, RHEL 8/9, or compatible)
- **.NET Runtime**: .NET 8.0 or newer

### Memory Requirements
- **GPU Memory**: Minimum 4GB VRAM recommended
- **System Memory**: 8GB RAM minimum, 16GB+ recommended

## Supported GPU Architectures

### Turing (Compute Capability 7.5)
- **Consumer GPUs**: 
  - RTX 2060, RTX 2060 Super
  - RTX 2070, RTX 2070 Super
  - RTX 2080, RTX 2080 Super, RTX 2080 Ti
  - GTX 1660, GTX 1660 Super, GTX 1660 Ti
- **Professional GPUs**: 
  - Quadro RTX 4000, RTX 5000, RTX 6000, RTX 8000
  - T4, T1000, T2000
- **Key Features**:
  - RT Cores for ray tracing
  - Tensor Cores (1st generation)
  - Shared memory register spilling
  - Cooperative groups support

### Ampere (Compute Capability 8.0, 8.6, 8.7)
- **Consumer GPUs (CC 8.6)**:
  - RTX 3050, RTX 3050 Ti
  - RTX 3060, RTX 3060 Ti
  - RTX 3070, RTX 3070 Ti
  - RTX 3080, RTX 3080 Ti
  - RTX 3090, RTX 3090 Ti
- **Professional GPUs**:
  - A100 (CC 8.0) - Data center
  - A10 (CC 8.6), A16 (CC 8.6), A30 (CC 8.0), A40 (CC 8.6)
  - RTX A2000, A4000, A5000, A6000
- **Key Features**:
  - 2nd generation RT Cores
  - 3rd generation Tensor Cores
  - Improved async compute
  - Enhanced memory bandwidth

### Ada Lovelace (Compute Capability 8.9)
- **Consumer GPUs**:
  - RTX 4060, RTX 4060 Ti
  - RTX 4070, RTX 4070 Ti, RTX 4070 Ti Super
  - RTX 4080, RTX 4080 Super
  - RTX 4090
- **Professional GPUs**:
  - L4, L40, L40S
  - RTX 4000 Ada, RTX 5000 Ada, RTX 6000 Ada
- **Key Features**:
  - 3rd generation RT Cores
  - 4th generation Tensor Cores with FP8 support
  - AV1 encoding support
  - Shader Execution Reordering (SER)

### Hopper (Compute Capability 9.0)
- **Data Center GPUs**:
  - H100 PCIe, H100 SXM5
  - H200
- **Key Features**:
  - 4th generation Tensor Cores
  - Transformer Engine with FP8
  - Thread Block Cluster
  - Distributed Shared Memory
  - TMA (Tensor Memory Accelerator)

## Deprecated Architectures (Not Supported)

### ❌ Maxwell (Compute Capability 5.x)
- **GPUs**: GTX 970, GTX 980, GTX 980 Ti, GTX Titan X
- **Status**: Not supported in CUDA 13.0
- **Last Supported**: CUDA 11.8

### ❌ Pascal (Compute Capability 6.x)
- **GPUs**: GTX 1050, 1060, 1070, 1080, P100, P40
- **Status**: Not supported in CUDA 13.0
- **Last Supported**: CUDA 11.8

### ❌ Volta (Compute Capability 7.0)
- **GPUs**: V100, Titan V, Quadro GV100
- **Status**: Not supported in CUDA 13.0
- **Last Supported**: CUDA 11.8

## Feature Availability by Architecture

| Feature | Turing (7.5) | Ampere (8.x) | Ada (8.9) | Hopper (9.0) |
|---------|-------------|--------------|-----------|--------------|
| Tensor Cores | ✓ (Gen 1) | ✓ (Gen 3) | ✓ (Gen 4) | ✓ (Gen 4) |
| RT Cores | ✓ (Gen 1) | ✓ (Gen 2) | ✓ (Gen 3) | ✗ |
| FP32 Performance | 1x | 2x | 2.5x | 3x |
| FP16/BF16 | ✓ | ✓ | ✓ | ✓ |
| FP8 | ✗ | ✗ | ✓ | ✓ |
| Cooperative Groups | ✓ | ✓ | ✓ | ✓ |
| Register Spilling | ✓ | ✓ | ✓ | ✓ |
| CUDA Graphs | ✓ | ✓ | ✓ | ✓ |
| Dynamic Parallelism | ✓ | ✓ | ✓ | ✓ |
| Unified Memory | ✓ | ✓ | ✓ | ✓ |
| Multi-Instance GPU | ✗ | ✓ (A100) | ✗ | ✓ |
| Thread Block Cluster | ✗ | ✗ | ✗ | ✓ |

## Performance Optimization Guidelines

### Turing Architecture
- **Optimal Block Size**: 256-512 threads
- **Register Usage**: Up to 255 registers per thread
- **Shared Memory**: 64KB per SM
- **L1 Cache**: Combined with shared memory (96KB total)
- **Tips**:
  - Use cooperative groups for warp-level primitives
  - Enable register spilling for high register pressure kernels
  - Leverage tensor cores for AI workloads

### Ampere Architecture
- **Optimal Block Size**: 256-512 threads
- **Register Usage**: Up to 255 registers per thread
- **Shared Memory**: 64KB per SM (configurable up to 164KB)
- **L1 Cache**: 128KB per SM
- **Tips**:
  - Utilize async copy operations
  - Take advantage of improved FP32 throughput
  - Use sparse tensor cores for pruned models

### Ada Lovelace Architecture
- **Optimal Block Size**: 256-512 threads
- **Register Usage**: Up to 255 registers per thread
- **Shared Memory**: 64KB per SM (configurable up to 100KB)
- **L1 Cache**: 128KB per SM
- **L2 Cache**: Up to 96MB (4090)
- **Tips**:
  - Leverage FP8 for transformer models
  - Use Shader Execution Reordering for ray tracing
  - Optimize for larger L2 cache

### Hopper Architecture
- **Optimal Block Size**: 256-1024 threads
- **Register Usage**: Up to 255 registers per thread
- **Shared Memory**: 64KB per SM (configurable up to 228KB)
- **L1 Cache**: 256KB per SM
- **L2 Cache**: 60MB (H100)
- **Tips**:
  - Use Thread Block Clusters for multi-SM cooperation
  - Leverage TMA for efficient tensor operations
  - Utilize Transformer Engine for LLMs

## Migration Guide from Older GPUs

### From Pascal (GTX 1000 series)
1. **Upgrade Path**: Minimum RTX 2060 or T4
2. **Benefits**:
   - 2-3x performance improvement
   - Tensor Core acceleration
   - RT Core ray tracing
   - Better memory bandwidth
3. **Code Changes**:
   - Update to CUDA 13.0 APIs
   - Leverage cooperative groups
   - Use tensor cores for AI workloads

### From Volta (V100)
1. **Upgrade Path**: A100, H100, or RTX 3090/4090
2. **Benefits**:
   - Improved tensor core performance
   - Better FP32 throughput
   - Larger memory capacities
3. **Code Changes**:
   - Minimal - Volta code mostly compatible
   - Optimize for newer tensor core formats

## Checking System Compatibility

### Linux
```bash
# Check CUDA version
nvidia-smi
nvcc --version

# Check driver version
cat /proc/driver/nvidia/version

# List available GPUs
nvidia-smi -L

# Check compute capability
nvidia-smi --query-gpu=name,compute_cap --format=csv
```

### Windows
```powershell
# Check CUDA version
nvidia-smi.exe
nvcc --version

# Check GPU information
wmic path win32_VideoController get name

# Use CUDA deviceQuery sample
deviceQuery.exe
```

### Programmatic Check (C#)
```csharp
using DotCompute.Backends.CUDA.Factory;

var factory = new CudaAcceleratorFactory();
if (factory.IsAvailable())
{
    var accelerator = factory.CreateProductionAccelerator(0);
    var info = accelerator.Info;
    
    Console.WriteLine($"GPU: {info.Name}");
    Console.WriteLine($"Compute Capability: {info.ComputeCapability.Major}.{info.ComputeCapability.Minor}");
    Console.WriteLine($"Architecture: {info.ArchitectureGeneration()}");
    Console.WriteLine($"Memory: {info.GlobalMemoryBytes() / (1024.0 * 1024 * 1024):F2} GB");
}
```

## Troubleshooting

### Error: "Device not compatible with CUDA 13.0"
- **Cause**: GPU is pre-Turing (CC < 7.5)
- **Solution**: Upgrade to RTX 2060 or newer

### Error: "CUDA driver version is insufficient"
- **Cause**: Driver version < 535.00
- **Solution**: Update NVIDIA drivers from nvidia.com

### Error: "CUDA runtime version mismatch"
- **Cause**: CUDA Toolkit version < 13.0
- **Solution**: Install CUDA Toolkit 13.0+ from developer.nvidia.com/cuda

### Performance Issues
- **Check occupancy**: Use profiler to analyze kernel occupancy
- **Memory bandwidth**: Ensure coalesced memory access patterns
- **Register pressure**: Enable register spilling for complex kernels
- **Block size**: Experiment with different block sizes (128-512)

## Recommended Development GPUs

### Budget Development
- **RTX 3060 (12GB)**: Good balance of price/performance
- **RTX 4060 Ti (16GB)**: Newer architecture, more VRAM

### Professional Development
- **RTX 4070 Ti**: Excellent performance, 12GB VRAM
- **RTX 4080**: High performance, 16GB VRAM

### AI/ML Development
- **RTX 4090**: Consumer flagship, 24GB VRAM
- **RTX 6000 Ada**: Professional, 48GB VRAM

### Data Center/Cloud
- **A100**: Industry standard for AI training
- **H100**: Latest generation, best performance
- **L4**: Cost-effective inference

## Cloud GPU Options

### AWS
- **P4**: A100 instances
- **P5**: H100 instances
- **G5**: A10G instances
- **G4**: T4 instances

### Azure
- **NC A100 v4**: A100 instances
- **ND H100 v5**: H100 instances
- **NCasT4_v3**: T4 instances

### Google Cloud
- **A2**: A100 instances
- **G2**: L4 instances

### Pricing Considerations
- T4: ~$0.5-1.0/hour
- A10G: ~$1.0-2.0/hour
- A100: ~$3.0-5.0/hour
- H100: ~$5.0-10.0/hour

## Future Compatibility

DotCompute is designed to support future NVIDIA architectures:
- **Blackwell (B100)**: Expected 2025
- **Future architectures**: Automatic support through CUDA forward compatibility

The framework will continue to support new CUDA versions and GPU architectures as they become available, while maintaining the minimum requirement of Compute Capability 7.5 for CUDA 13.0+.