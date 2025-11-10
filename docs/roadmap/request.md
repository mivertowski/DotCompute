# DotCompute Integration Proposal: Temporal Correctness APIs

## For Orleans.GpuBridge.Core Temporal Features

**Document Version**: 1.0

**Target DotCompute Version**: 0.5.0 or 1.0.0

**Priority**: Medium (needed for Phase 5, week 9-10)

**Maintainer**: [Your Name]

---

## Executive Summary

This proposal outlines API extensions to DotCompute to support temporal correctness features in Orleans.GpuBridge.Core. These extensions enable GPU-native actors to achieve:

1. **Nanosecond-precision timing** for physics simulations

2. **Device-wide synchronization** for lockstep execution

3. **Causal memory ordering** for distributed correctness

**Timeline**: Not needed until Week 9 of Orleans.GpuBridge implementation (Phases 1-4 complete without DotCompute changes).

**Impact**: Purely additive APIs - no breaking changes to existing DotCompute functionality.

---

## Background

Orleans.GpuBridge.Core is implementing temporal correctness for two use cases:

### Use Case 1: Financial Transaction Graph Analytics

- Detect temporal patterns (rapid transaction splits, circular flows)

- **Requirements**: Message ordering, causal dependencies

- **Timeline**: Production-ready Week 8 (no DotCompute changes needed)

### Use Case 2: Physics Wave Propagation Simulations

- Spatial quantized wave propagation with strict temporal ordering

- **Requirements**: GPU timing, device barriers, memory ordering

- **Timeline**: Production-ready Week 10 (requires DotCompute APIs)

**Current Status**: Phases 1-4 (Weeks 1-8) use CPU-based timing and synchronization. Phase 5 (Weeks 9-10) requires GPU-native features.

---

## Proposed API Extensions

### 1. Timing API (Priority: High)

**Goal**: Enable GPU-native timestamp generation with nanosecond precision.

#### New Interfaces

```csharp

namespace DotCompute.Timing;

 

/// <summary>

/// Provides GPU-native timing capabilities.

/// </summary>

public interface ITimingProvider

{

    /// <summary>

    /// Gets current GPU timestamp in nanoseconds.

    /// CUDA: Uses %%globaltimer register (1ns resolution)

    /// OpenCL: Uses clock() built-in (microsecond resolution)

    /// CPU: Uses Stopwatch (100ns resolution)

    /// </summary>

    Task<long> GetGpuTimestampAsync(CancellationToken ct = default);

 

    /// <summary>

    /// Gets multiple timestamps in batch (more efficient).

    /// </summary>

    Task<long[]> GetGpuTimestampsBatchAsync(int count, CancellationToken ct = default);

 

    /// <summary>

    /// Calibrates GPU clock against CPU clock.

    /// Returns offset (GPU - CPU), drift (PPM), and error bound.

    /// </summary>

    Task<ClockCalibration> CalibrateAsync(int sampleCount = 100, CancellationToken ct = default);

 

    /// <summary>

    /// Enables automatic timestamp injection at kernel entry.

    /// When enabled, kernels automatically record timestamp in parameter slot 0.

    /// </summary>

    void EnableTimestampInjection(bool enable = true);

 

    /// <summary>

    /// Gets GPU clock frequency in Hz.

    /// </summary>

    long GetGpuClockFrequency();

 

    /// <summary>

    /// Gets timer resolution in nanoseconds.

    /// </summary>

    long GetTimerResolutionNanos();

}

 

public readonly struct ClockCalibration

{

    public long OffsetNanos { get; init; }           // GPU_time - CPU_time

    public double DriftPPM { get; init; }            // Parts per million

    public long ErrorBoundNanos { get; init; }       // ± uncertainty

    public int SampleCount { get; init; }

    public long CalibrationTimestampNanos { get; init; }

 

    public long GpuToCpuTime(long gpuTimeNanos) { /* ... */ }

    public (long min, long max) GetUncertaintyRange(long gpuTimeNanos) { /* ... */ }

}

```

#### CUDA Implementation Reference

```cuda

// GPU timestamp using globaltimer register

__device__ __forceinline__ long gpu_nanotime()

{

    long time;

    asm volatile("mov.u64 %0, %%globaltimer;" : "=l"(time));

    return time;

}

 

// Automatic injection in kernel prologue

__global__ void user_kernel_with_timestamp(

    long* timestamps,  // Auto-injected parameter 0

    float* input,      // User parameter 1

    float* output)     // User parameter 2

{

    if (threadIdx.x == 0 && threadIdx.y == 0 && threadIdx.z == 0)

    {

        timestamps[blockIdx.x] = gpu_nanotime();

    }

    __syncthreads();

 

    // User kernel code...

}

```

#### Integration Point

```csharp

// Add to IDeviceManager

public interface IDeviceManager

{

    // Existing methods...

    IEnumerable<IDevice> GetDevices();

 

    // NEW

    ITimingProvider GetTimingProvider(IDevice device);

}

 

// Add to IDevice

public interface IDevice

{

    // Existing properties...

    string Name { get; }

 

    // NEW

    bool SupportsNanosecondTimers { get; }

    long TimerResolutionNanos { get; }

    long ClockFrequencyHz { get; }

}

```

**Performance Targets**:

- Single timestamp: <10ns (CUDA), <100ns (OpenCL/CPU)

- Batch timestamps (1000): <1μs amortized

- Clock calibration: <10ms for 100 samples

- Injection overhead: <20ns per kernel launch

---

### 2. Barrier API (Priority: High)

**Goal**: Enable device-wide synchronization for lockstep execution.

#### New Interfaces

```csharp

namespace DotCompute.Synchronization;

 

/// <summary>

/// Provides device-wide synchronization barriers.

/// </summary>

public interface IBarrierProvider

{

    /// <summary>

    /// Creates a device-wide barrier.

    /// CUDA: Uses Cooperative Groups (cudaLaunchCooperativeKernel)

    /// OpenCL: Uses work-group barriers (requires extension)

    /// CPU: Uses System.Threading.Barrier

    /// </summary>

    IBarrierHandle CreateBarrier(int participantCount, BarrierOptions? options = null);

 

    /// <summary>

    /// Launches kernel with device-wide barrier support.

    /// Requires cooperative launch for CUDA.

    /// </summary>

    Task ExecuteWithBarrierAsync(

        ICompiledKernel kernel,

        IBarrierHandle barrier,

        LaunchConfiguration config,

        object[] arguments,

        CancellationToken ct = default);

 

    /// <summary>

    /// Checks if device supports hardware barriers.

    /// </summary>

    bool IsHardwareBarrierSupported { get; }

 

    /// <summary>

    /// Gets maximum barrier participant count.

    /// </summary>

    int MaxBarrierParticipants { get; }

}

 

public sealed class BarrierOptions

{

    public TimeSpan Timeout { get; init; } = TimeSpan.MaxValue;

    public bool EnableTimeoutDetection { get; init; } = false;

    public BarrierScope Scope { get; init; } = BarrierScope.Device;

    public bool EnableArrivalCounting { get; init; } = false;

}

 

public enum BarrierScope

{

    ThreadBlock,  // __syncthreads

    Device,       // cooperative groups grid.sync()

    System        // across devices

}

 

public interface IBarrierHandle : IDisposable

{

    Task WaitAsync(CancellationToken ct = default);

    void Reset();

    int ArrivalCount { get; }

    int ParticipantCount { get; }

    bool IsReady { get; }

    Guid BarrierId { get; }

}

```

#### CUDA Implementation Reference

```cuda

#include <cooperative_groups.h>

 

__global__ void wave_step_with_barrier(

    float* state_current,

    float* state_next,

    int grid_size)

{

    namespace cg = cooperative_groups;

    cg::grid_group grid = cg::this_grid();

 

    int tid = threadIdx.x + blockIdx.x * blockDim.x;

    if (tid >= grid_size) return;

 

    // Compute next state

    state_next[tid] = compute_wave_propagation(state_current, tid);

 

    // BARRIER: Wait for ALL threads across ALL blocks

    grid.sync();

 

    // Now safe to read from state_next

    float gradient = state_next[tid] - state_next[(tid + 1) % grid_size];

}

```

**Launch Requirements**:

```csharp

// Must use cudaLaunchCooperativeKernel for device barriers

cudaLaunchCooperativeKernel(kernel, gridDim, blockDim, args, 0, stream);

```

#### Integration Point

```csharp

// Add to IDeviceManager

public interface IDeviceManager

{

    // NEW

    IBarrierProvider GetBarrierProvider(IDevice device);

}

 

// Add to IDevice

public interface IDevice

{

    // NEW

    bool SupportsCooperativeGroups { get; }

    int MaxBarrierParticipants { get; }

}

```

**Performance Targets**:

- Block-level barrier: <1μs (1K threads)

- Device-level barrier: <100μs (1M threads)

- System-level barrier: <1ms (multi-GPU)

- Launch overhead: <50μs (cooperative launch)

---

### 3. Memory Ordering API (Priority: Medium)

**Goal**: Enforce causal memory ordering for distributed actor correctness.

#### New Interfaces

```csharp

namespace DotCompute.Memory;

 

/// <summary>

/// Provides causal memory ordering primitives.

/// </summary>

public interface IMemoryOrderingProvider

{

    /// <summary>

    /// Enables causal memory ordering (acquire-release semantics).

    /// When enabled:

    /// - Writes use release semantics

    /// - Reads use acquire semantics

    /// - Memory fences enforce ordering

    /// </summary>

    void EnableCausalOrdering(bool enable = true);

 

    /// <summary>

    /// Inserts memory fence in kernel.

    /// CUDA: __threadfence_block(), __threadfence(), __threadfence_system()

    /// OpenCL: mem_fence(), atomic_work_item_fence()

    /// </summary>

    void InsertFence(FenceType type, FenceLocation? location = null);

 

    /// <summary>

    /// Configures memory consistency model.

    /// </summary>

    void SetConsistencyModel(MemoryConsistencyModel model);

 

    MemoryConsistencyModel ConsistencyModel { get; }

    bool IsAcquireReleaseSupported { get; }

}

 

public enum FenceType

{

    ThreadBlock,  // __threadfence_block()

    Device,       // __threadfence()

    System        // __threadfence_system()

}

 

public enum MemoryConsistencyModel

{

    Relaxed,          // Default GPU model

    ReleaseAcquire,   // Causal ordering

    Sequential        // Total order

}

 

public sealed class FenceLocation

{

    public int? InstructionIndex { get; init; }

    public bool AtEntry { get; init; }

    public bool AtExit { get; init; }

    public bool AfterWrites { get; init; }

    public bool BeforeReads { get; init; }

}

```

#### CUDA Implementation Reference

```cuda

// Causal write with release semantics

__device__ void causal_write_i64(volatile long* addr, long value)

{

    __threadfence_system();  // Release fence

    atomicExch((unsigned long long*)addr, (unsigned long long)value);

}

 

// Causal read with acquire semantics

__device__ long causal_read_i64(volatile long* addr)

{

    long value = atomicAdd((unsigned long long*)addr, 0ULL);

    __threadfence_system();  // Acquire fence

    return value;

}

```

#### Integration Point

```csharp

// Add to IDeviceManager

public interface IDeviceManager

{

    // NEW

    IMemoryOrderingProvider GetMemoryOrderingProvider(IDevice device);

}

 

// Add to IDevice

public interface IDevice

{

    // NEW

    bool SupportsAcquireRelease { get; }

    MemoryConsistencyModel DefaultConsistencyModel { get; }

}

```

**Performance Impact**:

- Relaxed: 1.0× (baseline)

- Release-Acquire: 0.85× (15% overhead)

- Sequential: 0.60× (40% overhead)

- Fence overhead: 10ns (block), 100ns (device), 200ns (system)

---

## Platform Support Matrix

| Feature | CUDA (Compute 6.0+) | OpenCL 2.0+ | CPU Fallback |

|---------|---------------------|-------------|--------------|

| **Nanosecond timers** | ✅ (%%globaltimer) | ⚠️ (μs resolution) | ✅ (Stopwatch) |

| **Timestamp injection** | ✅ | ✅ | ✅ |

| **Clock calibration** | ✅ | ✅ | ✅ |

| **Device barriers** | ✅ (Coop. Groups) | ⚠️ (Extension req.) | ✅ (Barrier class) |

| **System barriers** | ✅ | ⚠️ | ✅ |

| **Acquire-release** | ✅ (CUDA 9.0+) | ✅ (2.0+) | ✅ (volatile/Interlocked) |

| **Memory fences** | ✅ | ✅ | ✅ |

**Legend**: ✅ Full support | ⚠️ Partial support | ❌ Not supported

---

## Implementation Phases

### Phase 1: Timing API (Week 9)

**Deliverables**:

- [ ] Define interfaces (ITimingProvider, ClockCalibration)

- [ ] Implement CUDA backend (%%globaltimer)

- [ ] Implement OpenCL backend (clock())

- [ ] Implement CPU fallback (Stopwatch)

- [ ] Add clock calibration algorithm

- [ ] Add timestamp injection support

- [ ] Write unit tests

- [ ] Document API

**Estimated Effort**: 3-4 days

### Phase 2: Barrier API (Week 9-10)

**Deliverables**:

- [ ] Define interfaces (IBarrierProvider, IBarrierHandle)

- [ ] Implement CUDA backend (Cooperative Groups)

- [ ] Implement OpenCL backend (work-group barriers)

- [ ] Implement CPU fallback (Barrier class)

- [ ] Add multi-device support

- [ ] Write unit tests

- [ ] Document API

**Estimated Effort**: 4-5 days

### Phase 3: Memory Ordering API (Week 10)

**Deliverables**:

- [ ] Define interfaces (IMemoryOrderingProvider)

- [ ] Implement CUDA backend (fences, acquire-release)

- [ ] Implement OpenCL backend (mem_fence)

- [ ] Implement CPU fallback (volatile/Interlocked)

- [ ] Write correctness tests

- [ ] Document API

**Estimated Effort**: 2-3 days

**Total Effort**: ~10 days for all three APIs

---

## Backwards Compatibility

### Design Principles

1. **Opt-in features**: All temporal features disabled by default

2. **Graceful degradation**: Falls back to CPU synchronization if GPU features unavailable

3. **Capability detection**: Runtime checks for hardware support

4. **No breaking changes**: All APIs are additions, not modifications

### Feature Detection Example

```csharp

var device = deviceManager.GetDevice(0);

 

// Check capabilities before using

if (device.SupportsNanosecondTimers)

{

    var timingProvider = deviceManager.GetTimingProvider(device);

    var timestamp = await timingProvider.GetGpuTimestampAsync();

}

else

{

    // Fall back to CPU timing

    var timestamp = Stopwatch.GetTimestamp();

}

```

---

## Testing Requirements

### Unit Tests

```csharp

[Fact]

public async Task TimingProvider_ReturnsMonotonicTimestamps()

{

    var provider = GetTimingProvider();

    var t1 = await provider.GetGpuTimestampAsync();

    await Task.Delay(10);

    var t2 = await provider.GetGpuTimestampAsync();

 

    Assert.True(t2 > t1, "Timestamps must be monotonically increasing");

}

 

[Fact]

public async Task Barrier_SynchronizesAllThreads()

{

    var barrierProvider = GetBarrierProvider();

    var barrier = barrierProvider.CreateBarrier(participantCount: 1024);

 

    await barrierProvider.ExecuteWithBarrierAsync(testKernel, barrier, config, args);

 

    Assert.Equal(1024, barrier.ArrivalCount);

    Assert.True(barrier.IsReady);

}

```

### Integration Tests

Orleans.GpuBridge.Core will provide integration tests that validate:

- Clock calibration accuracy

- Barrier correctness for wave propagation

- Memory ordering for causal message passing

---

## Performance Benchmarks

| Benchmark | Target | Notes |

|-----------|--------|-------|

| Single timestamp query | <10ns | CUDA %%globaltimer |

| Batch timestamp query (1000) | <1μs | Amortized |

| Clock calibration (100 samples) | <10ms | One-time cost |

| Timestamp injection overhead | <20ns | Per kernel launch |

| Device barrier (1M threads) | <100μs | Cooperative groups |

| System fence overhead | <200ns | __threadfence_system |

---

## Alternative Approaches Considered

### 1. CPU-Only Timing

**Rejected**: 10-100μs latency for CPU-GPU round-trip is too high for physics simulations requiring nanosecond precision.

### 2. User-Space Kernel Injection

**Rejected**: Requires users to manually add timing code to kernels. Error-prone and inconsistent.

### 3. Event-Based Synchronization

**Rejected**: CUDA events have ~5-10μs overhead, unsuitable for fine-grained synchronization.

### 4. Atomic-Based Barriers

**Rejected**: Software barriers on GPU have 10-100× overhead vs. hardware cooperative groups.

---

## Integration Example (Orleans.GpuBridge.Core)

```csharp

// Orleans.GpuBridge initialization with temporal features

var siloHost = new SiloHostBuilder()

    .AddGpuBridge(options =>

    {

        options.EnableTemporalFeatures = true;

        options.TimingMode = TimingMode.GpuNative;  // Uses DotCompute timing API

        options.SynchronizationMode = SynchronizationMode.Lockstep;  // Uses barriers

    })

    .Build();

 

await siloHost.StartAsync();

 

// DotCompute backend automatically provides timing/barrier support

var gpuActor = client.GetGrain<IGpuResidentGrainEnhanced>("wave-sim");

 

// GPU-native timing and barriers used transparently

await gpuActor.SimulateWaveStepAsync();

```

---

## Open Questions for DotCompute Maintainer

1. **API Surface Location**:

   - Should these APIs be in `DotCompute.Core` or separate package `DotCompute.Temporal`?

2. **Versioning**:

   - Target version: 0.5.0 or 1.0.0?

   - Breaking change policy?

3. **Platform Priorities**:

   - Priority order: CUDA → OpenCL → CPU?

   - Support older CUDA compute capabilities (< 6.0)?

4. **Testing Infrastructure**:

   - Preferred testing framework?

   - CI/CD integration requirements?

5. **Timeline**:

   - Can this be integrated by Week 9 of Orleans.GpuBridge development?

   - Would you like to co-develop these APIs?

---

## Benefits to DotCompute Ecosystem

While designed for Orleans.GpuBridge.Core, these APIs are **general-purpose** and enable:

1. **Real-time systems**: GPU applications requiring nanosecond timing

2. **Scientific computing**: Lockstep simulations (weather, physics, molecular dynamics)

3. **Distributed GPU computing**: Multi-GPU applications with causal ordering

4. **Performance monitoring**: Fine-grained GPU kernel profiling

5. **Debugging**: Temporal analysis of GPU execution

These APIs add **significant value** to DotCompute as a platform for time-sensitive GPU computing.

---

## Deliverables for DotCompute Maintainer

Upon approval, I will provide:

1. ✅ Complete API interface definitions (C# code)

2. ✅ CUDA reference implementations

3. ✅ OpenCL reference implementations (if needed)

4. ✅ CPU fallback implementations

5. ✅ Unit tests and integration tests

6. ✅ API documentation (XML docs)

7. ✅ Usage examples and tutorials

8. ✅ Performance benchmarks

---

## References

- **Orleans.GpuBridge.Core Design**: `docs/temporal/TEMPORAL-CORRECTNESS-DESIGN.md`

- **Detailed API Spec**: `docs/temporal/DOTCOMPUTE-API-SPEC.md`

- **Implementation Roadmap**: `docs/temporal/IMPLEMENTATION-ROADMAP.md`

- **CUDA Cooperative Groups**: https://docs.nvidia.com/cuda/cuda-c-programming-guide/index.html#cooperative-groups

- **CUDA Timing**: https://docs.nvidia.com/cuda/parallel-thread-execution/index.html#special-registers-globaltimer

---

## Contact

**Orleans.GpuBridge.Core Maintainer**: [Your Name]

**Email**: [Your Email]

**GitHub**: https://github.com/mivertowski/Orleans.GpuBridge.Core

**Availability**: Ready to collaborate on implementation, provide reference code, and answer questions.

---

## Conclusion

These API extensions are **critical** for Orleans.GpuBridge.Core to achieve GPU-native temporal correctness for physics simulations. However:

- ✅ **Not urgent**: Phases 1-4 (Weeks 1-8) don't require these APIs

- ✅ **Low risk**: Purely additive, no breaking changes

- ✅ **High value**: Enables entire class of time-sensitive GPU applications

- ✅ **Well-defined**: Complete specification with reference implementations