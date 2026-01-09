# DotCompute Roadmap: Backend Feature Parity

**Document Version**: 1.0
**Last Updated**: January 2026
**Target Version**: v0.6.0 - v1.0.0
**Status**: Strategic Planning

---

## Executive Summary

This document outlines the strategy to achieve feature parity across all DotCompute backends (CUDA, Metal, OpenCL, CPU). Currently, CUDA is the most feature-complete backend, with Metal and OpenCL being experimental and CPU production-ready but limited in GPU-specific features.

**Key Objectives**:
- Bring Metal from experimental to production-ready (C# translation 60% → 100%)
- Complete OpenCL experimental features
- Standardize feature implementations across all backends
- Define clear feature tiers with backend support matrix

---

## 1. Current State Analysis

### Backend Maturity Matrix

| Backend | Status | Lines of Code | Test Coverage | Production Ready |
|---------|--------|---------------|---------------|------------------|
| **CUDA** | Production | 98,363 | 94.3% | Yes |
| **CPU** | Production | ~37,000 | 85% | Yes |
| **Metal** | Experimental | 53,442 | 80% | Partial |
| **OpenCL** | Experimental | 25,713 | 70% | No |

### Feature Comparison Matrix

#### Core Features

| Feature | CUDA | Metal | OpenCL | CPU |
|---------|------|-------|--------|-----|
| Kernel Compilation | ✅ NVRTC | ✅ MSL Direct | ✅ OpenCL C | ✅ IL Gen |
| C# Kernel Translation | ✅ 100% | ⚠️ 60% | ⚠️ 80% | ✅ 100% |
| Memory Pooling | ✅ 90% reduction | ✅ 90% reduction | ✅ Basic | ✅ Basic |
| Unified Memory | ✅ Full | ✅ Apple Silicon | ⚠️ SVM 2.0+ | ❌ N/A |
| Health Monitoring | ✅ NVML | ✅ Native | ✅ Basic | ❌ None |
| Native AOT | ✅ Full | ✅ Full | ⚠️ Partial | ✅ Full |

#### Advanced Features

| Feature | CUDA | Metal | OpenCL | CPU |
|---------|------|-------|--------|-----|
| **Barriers** |||||
| - ThreadBlock/Workgroup | ✅ | ✅ Threadgroup | ❌ | ❌ |
| - Grid/Device | ✅ | ❌ (N/A) | ❌ | ❌ |
| - Named Barriers | ✅ | ❌ | ❌ | ❌ |
| - Warp/Simdgroup | ✅ | ✅ Simdgroup | ❌ | ❌ |
| **Memory Ordering** |||||
| - Relaxed | ✅ | ⚠️ Planned | ❌ | ❌ |
| - Release-Acquire | ✅ | ⚠️ Planned | ❌ | ❌ |
| - Sequential | ✅ | ⚠️ Planned | ❌ | ❌ |
| - System Scope | ✅ | ❌ | ❌ | ❌ |
| **Timing** |||||
| - GPU Timestamps | ✅ 1ns (CC 6.0+) | ❌ | ❌ | ❌ |
| - Clock Calibration | ✅ | ❌ | ❌ | ❌ |
| **P2P Transfers** |||||
| - Device-to-Device | ✅ NVLink | ⚠️ Basic | ❌ | ❌ |
| - Topology Awareness | ✅ | ❌ | ❌ | ❌ |
| **Advanced Execution** |||||
| - Dynamic Parallelism | ✅ (CC 3.5+) | ✅ | ❌ | ❌ |
| - Cooperative Groups | ✅ Full | ✅ Simdgroup | ❌ | ❌ |
| - CUDA Graphs | ✅ (CC 10+) | ❌ | ❌ | ❌ |
| - Tensor Cores | ✅ WMMA | ✅ MPS | ❌ | ❌ |

#### Ring Kernel Features

| Feature | CUDA | Metal | OpenCL | CPU |
|---------|------|-------|--------|-----|
| Persistent Kernels | ✅ | ✅ | ✅ | ✅ |
| Message Queues | ✅ Full | ✅ Full | ⚠️ Basic | ✅ Full |
| Named Queues | ✅ | ✅ | ❌ TODO | ✅ |
| Telemetry | ✅ | ✅ | ⚠️ Basic | ✅ |
| MemoryPack Integration | ✅ | ✅ | ⚠️ Partial | ✅ |

---

## 2. Feature Tiers

### Tier 1: Essential (All Backends)
Features required for production use:

- ✅ Kernel compilation from backend-native language
- ✅ Memory allocation and transfer
- ✅ Synchronous and asynchronous execution
- ✅ Basic error handling
- ✅ Logging and diagnostics
- ✅ Native AOT compatibility
- ✅ Ring kernel support (basic)

### Tier 2: Standard (GPU Backends)
Features expected for GPU compute:

- ✅ Workgroup/threadblock barriers
- ✅ Shared memory access
- ✅ Memory pooling (90%+ allocation reduction)
- ✅ C# kernel translation
- ✅ Health monitoring
- ✅ Basic profiling
- ✅ Ring kernel message queues

### Tier 3: Advanced (Hardware-Dependent)
Features that depend on hardware capabilities:

- ⚡ GPU timing (requires hardware support)
- ⚡ Memory ordering (requires atomic support)
- ⚡ P2P transfers (requires multi-GPU)
- ⚡ Tensor operations (requires specialized hardware)
- ⚡ Dynamic parallelism (requires CC 3.5+/Metal 2.0+)

---

## 3. Metal Backend Roadmap

### Current Status
- **Maturity**: Experimental
- **C# to MSL Translation**: 60% complete
- **Key Gap**: Kernel translation incomplete

### Phase 1: Complete C# Translation (v0.6.0)

#### 1.1 Expression Support Expansion

Currently supported:
- ✅ Arithmetic operations
- ✅ Array access
- ✅ Basic control flow (if/else, for, while)
- ✅ Thread indexing

To implement:
```csharp
// 1. Math intrinsics mapping
[Kernel]
public static void Kernel(Span<float> output)
{
    var x = Kernel.ThreadId.X;
    output[x] = MathF.Sin(x) * MathF.Cos(x); // Math.* → metal::*
}

// Target MSL:
kernel void Kernel(device float* output [[buffer(0)]],
                   uint x [[thread_position_in_grid]]) {
    output[x] = metal::sin((float)x) * metal::cos((float)x);
}
```

```csharp
// 2. Struct support
public struct Vector3
{
    public float X, Y, Z;
}

[Kernel]
public static void Kernel(Span<Vector3> vectors)
{
    var idx = Kernel.ThreadId.X;
    vectors[idx].X = vectors[idx].Y + vectors[idx].Z;
}

// Target MSL:
struct Vector3 {
    float X, Y, Z;
};

kernel void Kernel(device Vector3* vectors [[buffer(0)]],
                   uint idx [[thread_position_in_grid]]) {
    vectors[idx].X = vectors[idx].Y + vectors[idx].Z;
}
```

```csharp
// 3. Threadgroup memory
[Kernel]
public static void ReduceKernel(Span<float> input, Span<float> output)
{
    var shared = Kernel.AllocateShared<float>(256);
    var tid = Kernel.ThreadId.X;
    var lid = Kernel.LocalThreadId.X;

    shared[lid] = input[tid];
    Kernel.Barrier();  // threadgroup_barrier(mem_flags::mem_threadgroup)

    // Reduction...
}
```

#### 1.2 Translation Pipeline Improvements

```
Current Pipeline:
C# Roslyn AST → Basic MSL → Compile

Target Pipeline:
C# Roslyn AST → Semantic Analysis → Type Mapping → Intrinsics Mapping →
MSL AST → Optimization → MSL Code → Metal Compiler → Binary Archive
```

#### 1.3 Implementation Tasks

| Task | Complexity | Priority |
|------|------------|----------|
| Math intrinsics (sin, cos, sqrt, etc.) | Medium | P0 |
| Struct translation | High | P0 |
| Threadgroup memory | High | P0 |
| Texture sampling | Medium | P1 |
| Atomic operations | Medium | P1 |
| SIMD operations | Medium | P1 |
| Control flow optimization | Low | P2 |

### Phase 2: Feature Parity with CUDA (v0.7.0)

#### 2.1 Memory Ordering Implementation

```csharp
// Implement for Metal backend
public sealed class MetalMemoryOrderingProvider : IMemoryOrderingProvider
{
    public void ThreadfenceBlock()
    {
        // Metal: threadgroup_barrier(mem_flags::mem_threadgroup)
    }

    public void ThreadfenceDevice()
    {
        // Metal: device_memory_fence()
    }

    // Note: System scope not available on Metal
    public void ThreadfenceSystem()
        => throw new NotSupportedOnBackendException(BackendType.Metal);
}
```

#### 2.2 Named Barrier Implementation

```csharp
// Note: Metal doesn't support named barriers (grid-wide sync not available)
// Document limitation and provide alternative patterns

public sealed class MetalBarrierProvider : IBarrierProvider
{
    public ValueTask BarrierAsync(BarrierScope scope)
    {
        return scope switch
        {
            BarrierScope.Threadgroup => ThreadgroupBarrierAsync(),
            BarrierScope.Simdgroup => SimdgroupBarrierAsync(),
            BarrierScope.Grid => throw new NotSupportedOnBackendException(
                "Grid-wide barriers not supported on Metal. Use kernel chaining instead."),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
```

### Phase 3: Production Hardening (v0.8.0)

- [ ] Performance benchmarking vs CUDA
- [ ] Memory leak detection
- [ ] Comprehensive error messages
- [ ] M1/M2/M3 optimization profiles
- [ ] Binary cache optimization
- [ ] Integration with Apple ecosystem

---

## 4. OpenCL Backend Roadmap

### Current Status
- **Maturity**: Experimental
- **Key Gap**: Missing timing, barriers, memory ordering

### Phase 1: Core Stability (v0.6.0)

#### 1.1 Timing Provider Implementation

```csharp
public sealed class OpenCLTimingProvider : ITimingProvider
{
    public bool IsSupported => _profile.ProfilingTimerResolution > 0;

    public ValueTask<TimingResult> TimeKernelAsync(
        ICompiledKernel kernel,
        ExecutionConfiguration config,
        CancellationToken ct = default)
    {
        // Use OpenCL event profiling
        // CL_PROFILING_COMMAND_START, CL_PROFILING_COMMAND_END
        var evt = await _queue.EnqueueNDRangeKernelAsync(kernel, config);
        await evt.WaitAsync();

        var start = await evt.GetProfilingInfoAsync(ProfilingInfo.Start);
        var end = await evt.GetProfilingInfoAsync(ProfilingInfo.End);

        return new TimingResult(start, end, end - start);
    }
}
```

#### 1.2 Named Message Queue Implementation

```csharp
// Currently TODO in OpenCLRingKernelRuntime.cs:527-570
public sealed class OpenCLRingKernelRuntime : IRingKernelRuntime
{
    private readonly Dictionary<string, OpenCLMessageQueue> _namedQueues = new();

    public IMessageQueue GetQueue(string name)
    {
        if (!_namedQueues.TryGetValue(name, out var queue))
        {
            queue = new OpenCLMessageQueue(_context, _queueOptions);
            _namedQueues[name] = queue;
        }
        return queue;
    }
}
```

### Phase 2: Cross-Platform Validation (v0.7.0)

#### 2.1 Vendor-Specific Testing

| Vendor | Device | Status | Priority |
|--------|--------|--------|----------|
| NVIDIA | RTX 3000/4000 | ⚠️ Need testing | P1 |
| AMD | RX 6000/7000 | ⚠️ Need testing | P1 |
| Intel | Arc/Iris | ⚠️ Need testing | P1 |
| ARM | Mali | ⚠️ Need testing | P2 |
| Qualcomm | Adreno | ⚠️ Need testing | P2 |

#### 2.2 Vendor Adapter Refinement

```csharp
public interface IOpenCLVendorAdapter
{
    string VendorName { get; }
    bool SupportsFeature(OpenCLFeature feature);
    string? GetOptimizedKernelVariant(string baseKernel);
    OpenCLCompilationOptions GetOptimalCompilationOptions();
}

public sealed class NvidiaOpenCLAdapter : IOpenCLVendorAdapter
{
    public string VendorName => "NVIDIA";

    public bool SupportsFeature(OpenCLFeature feature) => feature switch
    {
        OpenCLFeature.AtomicFloat => true,
        OpenCLFeature.SubGroups => true,
        OpenCLFeature.FP16 => true,
        _ => false
    };
}

public sealed class AMDOpenCLAdapter : IOpenCLVendorAdapter
{
    // AMD-specific optimizations
}
```

### Phase 3: Feature Completion (v0.8.0)

- [ ] Barrier implementation (where possible)
- [ ] Memory ordering (vendor-dependent)
- [ ] Comprehensive profiling
- [ ] Multi-device support
- [ ] Integration with AMD ROCm (future)

---

## 5. CPU Backend Roadmap

### Current Status
- **Maturity**: Production
- **Key Strength**: SIMD vectorization (AVX2/AVX512/NEON)

### Phase 1: Enhanced SIMD Coverage (v0.6.0)

#### 1.1 Complete Reduction Operations

```csharp
// Currently TODO in SimdVectorOperations.cs:541
public static T Reduce<T>(
    ReadOnlySpan<T> input,
    ReductionOperation operation) where T : unmanaged
{
    if (Avx2.IsSupported)
        return ReduceAvx2(input, operation);
    if (AdvSimd.IsSupported)
        return ReduceNeon(input, operation);

    return ReduceScalar(input, operation);
}

private static T ReduceAvx2<T>(ReadOnlySpan<T> input, ReductionOperation op)
{
    // Vectorized reduction using AVX2
}
```

#### 1.2 Product Reduction

```csharp
// Currently TODO in SimdInstructionDispatcher.cs:221
public static T Product<T>(ReadOnlySpan<T> input) where T : unmanaged
{
    // Implement vectorized product reduction
}
```

### Phase 2: NUMA Optimization (v0.7.0)

```csharp
// Currently TODO in CpuMemoryManager.cs:376
public sealed class NumaAwareMemoryAllocator
{
    public Memory<T> AllocateOnNode<T>(int numaNode, int length) where T : unmanaged
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return AllocateWindowsNuma<T>(numaNode, length);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return AllocateLinuxNuma<T>(numaNode, length);
        }

        // Fallback: standard allocation
        return new T[length];
    }

    private Memory<T> AllocateWindowsNuma<T>(int numaNode, int length)
    {
        // Use VirtualAllocExNuma
        var ptr = NativeMethods.VirtualAllocExNuma(
            Process.GetCurrentProcess().Handle,
            IntPtr.Zero,
            (UIntPtr)(length * Unsafe.SizeOf<T>()),
            AllocationType.Commit | AllocationType.Reserve,
            MemoryProtection.ReadWrite,
            (uint)numaNode);

        return new NativeMemory<T>(ptr, length);
    }
}
```

### Phase 3: Health Monitoring (v0.8.0)

```csharp
// Add CPU health monitoring (currently none)
public sealed class CpuHealthMonitor : IHealthMonitor
{
    public ValueTask<HealthSnapshot> GetSnapshotAsync()
    {
        var cpuUsage = GetCpuUsage();
        var memoryUsage = GetMemoryUsage();
        var threadPoolStats = GetThreadPoolStats();

        return ValueTask.FromResult(new HealthSnapshot
        {
            CpuUtilization = cpuUsage,
            MemoryAvailable = memoryUsage.Available,
            MemoryTotal = memoryUsage.Total,
            ThreadPoolActive = threadPoolStats.Active,
            ThreadPoolQueued = threadPoolStats.Queued
        });
    }
}
```

---

## 6. New Backend Considerations

### 6.1 ROCm/HIP Backend (Future)

| Aspect | Notes |
|--------|-------|
| Target | AMD GPUs (RDNA2/3, CDNA2/3) |
| Priority | P2 (after OpenCL stabilization) |
| Approach | HIPify from CUDA where possible |
| Key Challenge | Different memory model |

### 6.2 Vulkan Compute Backend (Future)

| Aspect | Notes |
|--------|-------|
| Target | Cross-platform (alternative to OpenCL) |
| Priority | P3 |
| Approach | SPIR-V compilation from GLSL/HLSL |
| Key Challenge | Complex pipeline setup |

### 6.3 WebGPU Backend (Future)

| Aspect | Notes |
|--------|-------|
| Target | Browser/WASM compute |
| Priority | P3 |
| Approach | WGSL generation |
| Key Challenge | Limited features, async-only |

### 6.4 DirectML Backend (Future)

| Aspect | Notes |
|--------|-------|
| Target | Windows ML acceleration |
| Priority | P4 |
| Approach | Operator graph model |
| Key Challenge | Different programming model |

---

## 7. Shared Infrastructure

### 7.1 Backend-Agnostic Testing

```csharp
// Test suite that runs on all backends
[Theory]
[InlineData(BackendType.CPU)]
[InlineData(BackendType.CUDA)]
[InlineData(BackendType.Metal)]
[InlineData(BackendType.OpenCL)]
public async Task VectorAdd_ProducesCorrectResult(BackendType backend)
{
    Skip.IfNot(await IsBackendAvailable(backend));

    using var accelerator = await CreateAccelerator(backend);
    using var a = await accelerator.AllocateAsync<float>(1024);
    using var b = await accelerator.AllocateAsync<float>(1024);
    using var c = await accelerator.AllocateAsync<float>(1024);

    await accelerator.ExecuteAsync("VectorAdd", a, b, c);

    var result = c.AsSpan().ToArray();
    Assert.All(result, (v, i) => Assert.Equal(a[i] + b[i], v));
}
```

### 7.2 Feature Detection API

```csharp
public interface IFeatureDetector
{
    bool SupportsFeature(ComputeFeature feature);
    FeatureSupport GetFeatureSupport(ComputeFeature feature);
}

public enum ComputeFeature
{
    // Tier 1
    KernelExecution,
    MemoryAllocation,
    AsyncExecution,

    // Tier 2
    WorkgroupBarriers,
    SharedMemory,
    MemoryPooling,
    HealthMonitoring,

    // Tier 3
    GridBarriers,
    NamedBarriers,
    MemoryOrdering,
    GpuTiming,
    P2PTransfers,
    TensorOperations,
    DynamicParallelism
}

public readonly record struct FeatureSupport(
    bool IsSupported,
    string? RequiredVersion,
    string? Notes);
```

### 7.3 Capability-Based API

```csharp
// Execute only if feature is supported
if (accelerator.SupportsFeature(ComputeFeature.GpuTiming))
{
    var timing = await accelerator.TimeKernelAsync(kernel);
    logger.LogInformation("Kernel executed in {Time}ns", timing.ElapsedNanoseconds);
}
else
{
    // Fallback to wall-clock timing
    var sw = Stopwatch.StartNew();
    await accelerator.ExecuteAsync(kernel);
    sw.Stop();
    logger.LogInformation("Kernel executed in ~{Time}ms", sw.ElapsedMilliseconds);
}
```

---

## 8. Implementation Timeline

### v0.6.0 (3 months)
- [ ] Metal C# translation: 60% → 85%
- [ ] OpenCL timing provider
- [ ] OpenCL named message queues
- [ ] CPU SIMD reduction completion
- [ ] Backend-agnostic test suite

### v0.7.0 (3 months)
- [ ] Metal C# translation: 85% → 100%
- [ ] Metal memory ordering (where possible)
- [ ] OpenCL vendor testing (NVIDIA, AMD, Intel)
- [ ] CPU NUMA optimization
- [ ] Feature detection API

### v0.8.0 (3 months)
- [ ] Metal production hardening
- [ ] OpenCL barrier implementation
- [ ] CPU health monitoring
- [ ] Cross-platform CI validation
- [ ] Performance benchmarking

### v1.0.0 (3 months)
- [ ] All Tier 1 features on all backends
- [ ] All Tier 2 features on GPU backends
- [ ] Tier 3 features where hardware supports
- [ ] Comprehensive documentation
- [ ] Production certification

---

## 9. Success Metrics

### Backend Readiness

| Backend | Current | v0.7.0 | v1.0.0 |
|---------|---------|--------|--------|
| CUDA | Production | Production | Production |
| CPU | Production | Production | Production |
| Metal | Experimental | Beta | Production |
| OpenCL | Experimental | Beta | Production |

### Feature Coverage

| Feature Tier | CUDA | Metal | OpenCL | CPU |
|--------------|------|-------|--------|-----|
| Tier 1 | 100% | 100% | 100% | 100% |
| Tier 2 | 100% | 80% | 60% | 80% |
| Tier 3 | 95% | 40% | 10% | N/A |

### Test Coverage by Backend

| Backend | Unit Tests | Integration Tests | Hardware Tests |
|---------|------------|-------------------|----------------|
| CUDA | 95% | 90% | 85% |
| CPU | 90% | 85% | N/A |
| Metal | 85% | 75% | 70% |
| OpenCL | 75% | 60% | 50% |

---

## 10. Appendix: Feature Implementation Guides

### A. Adding a New Feature to All Backends

```
1. Define interface in DotCompute.Abstractions
   └── INewFeatureProvider.cs

2. Add feature detection
   └── ComputeFeature.NewFeature

3. Implement in each backend
   ├── CudaNewFeatureProvider.cs
   ├── MetalNewFeatureProvider.cs
   ├── OpenCLNewFeatureProvider.cs
   └── CpuNewFeatureProvider.cs (or throw NotSupportedException)

4. Add to accelerator facade
   └── IAccelerator.NewFeature property

5. Write backend-agnostic tests
   └── NewFeatureTests.cs with [Theory] for all backends

6. Document in feature matrix
   └── Update this document
```

### B. Documenting Unsupported Features

```csharp
/// <summary>
/// Provides grid-wide barrier synchronization.
/// </summary>
/// <remarks>
/// <b>Backend Support:</b>
/// <list type="bullet">
/// <item>CUDA: Supported on CC 6.0+ with cooperative launch</item>
/// <item>Metal: Not supported (architectural limitation)</item>
/// <item>OpenCL: Not supported</item>
/// <item>CPU: Not applicable</item>
/// </list>
/// <para>
/// For backends that don't support grid barriers, consider using
/// kernel chaining or multi-pass algorithms.
/// </para>
/// </remarks>
ValueTask GridBarrierAsync(CancellationToken ct = default);
```

---

**Document Owner**: Backend Platform Team
**Review Cycle**: Quarterly
**Next Review**: April 2026
