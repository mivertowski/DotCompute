# Performance Baseline Documentation

**Version**: 0.6.2
**Baseline Date**: January 10, 2026
**Hardware Reference**: NVIDIA RTX 2000 Ada / AMD Ryzen 9

---

## Executive Summary

DotCompute achieves the following performance characteristics:

| Workload | CPU Speedup | GPU Speedup (CUDA) |
|----------|-------------|-------------------|
| Vector Operations | 3.7x (SIMD) | 21x |
| Matrix Multiply | 3.5x (SIMD) | 92x |
| Reduction | 4.1x | 35x |
| FFT | 3.2x | 45x |
| Convolution | 5.3x | 78x |

---

## Benchmark Environment

### Reference Hardware

| Component | Specification |
|-----------|---------------|
| CPU | AMD Ryzen 9 7950X (16C/32T, 5.7GHz) |
| GPU | NVIDIA RTX 2000 Ada (CC 8.9, 8GB VRAM) |
| Memory | 64GB DDR5-5600 |
| Storage | NVMe SSD |
| OS | Ubuntu 22.04 LTS |
| .NET | 9.0.0 |
| CUDA | 13.0 |

### Benchmark Configuration

```csharp
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[Config(typeof(AntivirusFriendlyConfig))]
public class DotComputeBenchmarks { }
```

---

## Vector Operations

### Vector Addition

| Size | Sequential | CPU SIMD | CUDA | OpenCL | Metal |
|------|------------|----------|------|--------|-------|
| 1K | 0.5 μs | 0.3 μs | 12 μs | 15 μs | 8 μs |
| 10K | 4.2 μs | 1.1 μs | 14 μs | 18 μs | 10 μs |
| 100K | 42 μs | 11 μs | 18 μs | 24 μs | 15 μs |
| 1M | 420 μs | 110 μs | 45 μs | 62 μs | 48 μs |
| 10M | 4.2 ms | 1.1 ms | 198 μs | 312 μs | 245 μs |
| 100M | 42 ms | 11 ms | 1.8 ms | 3.1 ms | 2.4 ms |

**Crossover Point**: GPU faster than CPU SIMD at ~50K elements

### Vector Multiply

| Size | Sequential | CPU SIMD | CUDA |
|------|------------|----------|------|
| 10M | 4.5 ms | 1.2 ms | 205 μs |
| 100M | 45 ms | 12 ms | 1.9 ms |

---

## Matrix Operations

### Matrix Multiplication (float32)

| Dimensions | Sequential | CPU SIMD | CUDA | OpenCL |
|------------|------------|----------|------|--------|
| 128×128 | 2.1 ms | 0.6 ms | 0.08 ms | 0.12 ms |
| 256×256 | 16 ms | 4.6 ms | 0.15 ms | 0.24 ms |
| 512×512 | 128 ms | 37 ms | 0.42 ms | 0.78 ms |
| 1024×1024 | 1.02 s | 294 ms | 2.8 ms | 5.2 ms |
| 2048×2048 | 8.2 s | 2.4 s | 18 ms | 38 ms |
| 4096×4096 | 65 s | 19 s | 138 ms | 285 ms |

**CUDA Performance**: 92x speedup at 4096×4096

### Matrix Transpose

| Dimensions | Sequential | CUDA (Coalesced) |
|------------|------------|------------------|
| 1024×1024 | 4.2 ms | 0.12 ms |
| 4096×4096 | 68 ms | 1.8 ms |

---

## Reduction Operations

### Sum Reduction

| Elements | Sequential | CPU Parallel | CUDA |
|----------|------------|--------------|------|
| 1M | 0.8 ms | 0.2 ms | 0.05 ms |
| 10M | 8 ms | 1.9 ms | 0.23 ms |
| 100M | 80 ms | 19 ms | 2.1 ms |

### Max/Min Reduction

| Elements | Sequential | CUDA |
|----------|------------|------|
| 10M | 12 ms | 0.28 ms |
| 100M | 120 ms | 2.6 ms |

---

## Signal Processing

### FFT (Complex, Float32)

| Samples | FFTW (CPU) | CUDA cuFFT |
|---------|------------|------------|
| 64K | 0.8 ms | 0.04 ms |
| 256K | 4.2 ms | 0.12 ms |
| 1M | 18 ms | 0.4 ms |
| 4M | 82 ms | 1.8 ms |
| 16M | 380 ms | 8.2 ms |

**Speedup**: 45x at 16M samples

### 2D Convolution

| Image Size | Kernel | CPU | CUDA |
|------------|--------|-----|------|
| 1920×1080 | 3×3 | 12 ms | 0.15 ms |
| 1920×1080 | 5×5 | 28 ms | 0.18 ms |
| 3840×2160 | 3×3 | 48 ms | 0.52 ms |
| 3840×2160 | 5×5 | 112 ms | 0.68 ms |

---

## Memory Operations

### Host-to-Device Transfer

| Size | Bandwidth | Latency |
|------|-----------|---------|
| 4 KB | 0.8 GB/s | 5 μs |
| 64 KB | 8.2 GB/s | 8 μs |
| 1 MB | 12.4 GB/s | 85 μs |
| 16 MB | 13.8 GB/s | 1.2 ms |
| 256 MB | 14.2 GB/s | 18 ms |

### Device-to-Device Transfer (P2P)

| Size | Bandwidth (Same PCIe) | Bandwidth (NVLink) |
|------|----------------------|-------------------|
| 256 MB | 12 GB/s | 48 GB/s |

### Memory Pool Efficiency

| Metric | Without Pool | With Pool | Improvement |
|--------|--------------|-----------|-------------|
| Allocation time | 45 μs | 2 μs | 22x |
| Fragmentation | 23% | 3% | 87% reduction |
| Peak memory | 1.8 GB | 1.2 GB | 33% reduction |

---

## Ring Kernel Performance

### Message Throughput

| Message Size | Throughput | Latency (p99) |
|--------------|------------|---------------|
| 64 bytes | 1.2M msg/s | 1.8 μs |
| 256 bytes | 980K msg/s | 2.2 μs |
| 1 KB | 720K msg/s | 3.1 μs |
| 4 KB | 420K msg/s | 5.8 μs |

### Serialization Performance (MemoryPack)

| Operation | Time |
|-----------|------|
| Serialize (256 bytes) | 98 ns |
| Deserialize (256 bytes) | 82 ns |

---

## Compilation Performance

### Kernel Compilation Time

| Backend | Cold Start | Cached |
|---------|------------|--------|
| CUDA (PTX) | 180 ms | 2 ms |
| CUDA (CUBIN) | 450 ms | 2 ms |
| OpenCL | 120 ms | 3 ms |
| Metal | 85 ms | 2 ms |
| CPU (IL) | 12 ms | <1 ms |

### Source Generator Performance

| Kernels | Generation Time | Memory |
|---------|-----------------|--------|
| 10 | 45 ms | 12 MB |
| 100 | 320 ms | 48 MB |
| 500 | 1.8 s | 185 MB |

---

## Benchmark Reproducibility

### Running Benchmarks

```bash
# Full benchmark suite
dotnet run -c Release --project benchmarks/DotCompute.Benchmarks

# Specific category
dotnet run -c Release --project benchmarks/DotCompute.Benchmarks -- --filter "*Matrix*"

# With hardware counters
dotnet run -c Release --project benchmarks/DotCompute.Benchmarks -- --hardware
```

### Expected Variance

| Metric | Acceptable Variance |
|--------|---------------------|
| Throughput | ±5% |
| Latency (median) | ±3% |
| Latency (p99) | ±10% |
| Memory | ±2% |

---

## Performance Regression Detection

### CI Integration

```yaml
- name: Run Performance Tests
  run: |
    dotnet run -c Release --project benchmarks/DotCompute.Benchmarks -- \
      --exporters json \
      --artifacts ./benchmark-results

    # Compare against baseline
    ./scripts/compare-benchmarks.sh baseline.json ./benchmark-results/*.json
```

### Regression Thresholds

| Metric | Warning | Failure |
|--------|---------|---------|
| Throughput | -5% | -10% |
| Latency | +10% | +20% |
| Memory | +10% | +25% |

---

## Optimization Guidelines

### When to Use GPU

- Vector operations > 50K elements
- Matrix operations > 256×256
- FFT > 64K samples
- Image processing > 720p

### When to Use CPU

- Small data sets (< 10K elements)
- Sequential dependencies
- High kernel launch overhead scenarios
- Memory-bound workloads with CPU affinity

---

**Baseline Valid Until**: v0.6.0 release
**Next Benchmark Run**: Before each minor release
