# Metal Backend Uncovered Code Paths

**Analysis Date:** October 27, 2025
**Backend Version:** 0.2.0

This document details specific code paths and functionalities in the Metal backend that currently lack test coverage.

## Critical Uncovered Areas (0% Coverage)

### 1. Kernel Compilation System (2,489 lines) ⚠️ **HIGHEST PRIORITY**

#### MetalKernelCompiler.cs (987 lines)
**Uncovered Functionality:**
- MSL shader source compilation
- Compilation option handling (optimization levels, debug info, fast math)
- Multi-target compilation (iOS arm64, macOS x64/arm64, Catalyst)
- Compilation error parsing and diagnostics
- Shader preprocessing and macro expansion
- Include file resolution
- Metal Standard Library version selection
- Compilation timeout handling
- Binary format selection (metallib vs AIR)

**Critical Code Paths:**
```csharp
// Uncovered: Compilation pipeline
public Task<CompiledKernel> CompileAsync(string source, CompilationOptions options)
public Task<CompiledKernel> CompileFromFileAsync(string path, CompilationOptions options)
public Task<byte[]> CompileToBinaryAsync(string source, CompilationOptions options)

// Uncovered: Error handling
private void ParseCompilationErrors(string diagnostics)
private bool IsRecoverableError(CompilationError error)

// Uncovered: Multi-target
private byte[] CompileForTarget(string source, string targetTriple, string osVersion)
```

**Missing Test Scenarios:**
- Valid shader compilation succeeds
- Syntax error → CompilationException with line/column
- Undefined function → Helpful error message
- Incompatible Metal version → Graceful fallback
- Optimization level comparison (O0 vs O2 vs O3)
- Debug info preservation for debugging
- Fast math flag behavior
- Compilation timeout triggers correctly
- Binary caching and reuse
- Include path resolution
- Macro definition handling
- Target-specific compilation (iOS vs macOS)

#### MetalKernelCache.cs (445 lines)
**Uncovered Functionality:**
- Kernel binary caching by source hash
- Cache invalidation strategies
- Cache size management (LRU eviction)
- Disk cache persistence
- Cache hit/miss statistics
- Cache warming on startup
- Multi-threaded cache access
- Cache corruption recovery

**Critical Code Paths:**
```csharp
// Uncovered: Cache operations
public Task<CompiledKernel?> GetAsync(string sourceHash)
public Task StoreAsync(string sourceHash, CompiledKernel kernel)
public Task InvalidateAsync(string sourceHash)
public Task ClearAsync()

// Uncovered: Cache management
private void EvictLeastRecentlyUsed(int count)
private void PersistToDisk()
private void LoadFromDisk()

// Uncovered: Statistics
public CacheStatistics GetStatistics()
```

**Missing Test Scenarios:**
- Cache hit returns correct kernel
- Cache miss returns null
- Cache stores new kernel
- LRU eviction works correctly
- Cache size limit enforced
- Disk persistence survives restart
- Concurrent access is thread-safe
- Corrupted cache entry handled gracefully
- Cache invalidation removes entry
- Statistics track hits/misses accurately

#### MetalKernelOptimizer.cs (621 lines)
**Uncovered Functionality:**
- Dead code elimination
- Constant propagation and folding
- Loop unrolling optimization
- Memory access pattern optimization
- Register allocation improvement
- Instruction scheduling
- Branch prediction hints
- Vectorization opportunities
- Metal-specific optimizations (SIMD-group, quad operations)

**Critical Code Paths:**
```csharp
// Uncovered: Optimization pipeline
public OptimizedShader Optimize(string source, OptimizationLevel level)
private void EliminateDeadCode(AST ast)
private void PropagateConstants(AST ast)
private void UnrollLoops(AST ast, int maxUnrollFactor)
private void OptimizeMemoryAccess(AST ast)
private void ScheduleInstructions(AST ast)
```

**Missing Test Scenarios:**
- Dead code is removed correctly
- Constants are folded
- Small loops unroll properly
- Large loops don't explode code size
- Memory coalescing improves bandwidth
- Register pressure stays within limits
- SIMD-group operations inserted where beneficial
- Optimization preserves correctness
- Different optimization levels produce expected results
- Optimization time stays reasonable

#### MetalCompiledKernel.cs (289 lines)
**Uncovered Functionality:**
- Kernel argument binding and validation
- Thread grid configuration
- Resource state tracking
- Kernel launch parameter setup
- Error state management

**Critical Code Paths:**
```csharp
// Uncovered: Execution setup
public void SetArgument(int index, IBuffer buffer)
public void SetArgument(int index, object value)
public void SetThreads(int x, int y, int z)
public void SetThreadgroupSize(int x, int y, int z)

// Uncovered: Validation
private void ValidateArguments()
private void ValidateThreadConfiguration()
```

**Missing Test Scenarios:**
- Argument binding succeeds for valid types
- Invalid argument type → Exception
- Argument out of bounds → Exception
- Thread grid validation
- Threadgroup size limits enforced
- Resource state transitions
- Multiple kernel launches reuse compilation

#### MetalOptimizedKernels.cs (147 lines)
**Uncovered Functionality:**
- Pre-compiled kernel template library
- Common operation kernels (MatMul, Conv2D, etc.)
- Kernel template instantiation
- Template parameter substitution

**Missing Test Scenarios:**
- Template instantiation works
- Parameter substitution correct
- Optimized kernels faster than naive
- Templates work across device families

---

### 2. Configuration System (339 lines)

#### MetalCapabilityManager.cs (339 lines)
**Uncovered Functionality:**
- Device capability detection (GPU family, features, limits)
- Metal version detection
- Feature support queries (raytracing, mesh shaders, etc.)
- Device tier classification
- Multi-GPU enumeration
- Capability caching

**Critical Code Paths:**
```csharp
// Uncovered: Device detection
public static MetalCapabilities GetDeviceCapabilities(IntPtr device)
public static bool SupportsFeature(MetalFeature feature)
public static GPUFamily GetGPUFamily(IntPtr device)

// Uncovered: Feature queries
public static int GetMaxThreadgroupMemory()
public static int GetMaxThreadsPerThreadgroup()
public static bool SupportsRaytracing()
```

**Missing Test Scenarios:**
- M1/M2/M3 Mac GPU family detected correctly
- Intel Mac GPU detected correctly
- iOS device capabilities detected
- Feature support queries return correct values
- Resource limits match device specs
- Multi-GPU systems enumerate all devices
- Capability caching works correctly
- Fallback to safe defaults if detection fails

---

### 3. Factory and Registration (443 lines)

#### MetalBackendFactory.cs (218 lines)
**Uncovered Functionality:**
- Backend instantiation
- Device selection logic
- Configuration validation
- Fallback to CPU if Metal unavailable
- Resource initialization

**Critical Code Paths:**
```csharp
// Uncovered: Factory methods
public static IBackend Create(BackendOptions options)
public static IBackend CreateForDevice(IntPtr device)
private void InitializeResources()
private void ValidateConfiguration(BackendOptions options)
```

**Missing Test Scenarios:**
- Factory creates backend successfully
- Invalid options → Exception
- Metal unavailable → Fallback or exception
- Device selection chooses correct GPU
- Resource initialization completes
- Configuration validation works

#### MetalBackendPlugin.cs (225 lines)
**Uncovered Functionality:**
- Plugin system integration
- Backend discovery and registration
- Lifecycle management
- Version compatibility checks
- Hot-reload support

**Missing Test Scenarios:**
- Plugin registers backend successfully
- Discovery finds Metal backend
- Version check validates compatibility
- Lifecycle hooks called correctly
- Hot-reload preserves state

---

### 4. Native Bindings (211 lines)

#### MetalNative.cs (167 lines)
**Uncovered Functionality:**
- P/Invoke declarations for Metal API
- Function pointer marshalling
- Error code translation
- Memory management for native objects
- Resource lifetime tracking

**Missing Test Scenarios:**
- P/Invoke calls succeed
- Error codes translated correctly
- Memory leaks prevented
- Native object disposal works
- Function pointers valid

#### MetalNative.Attributes.cs (44 lines)
**Uncovered Functionality:**
- Native AOT compatibility attributes
- DllImport configurations
- Calling convention specifications

**Missing Test Scenarios:**
- Native AOT compilation succeeds
- Attributes applied correctly

---

## Partially Covered Areas (< 60% Coverage)

### 5. Execution Engine Gaps (9,895 lines, 48% coverage)

#### MetalCommandEncoder.cs (715 lines) - 0% Coverage
**Uncovered Functionality:**
- Command encoding for compute pipelines
- Resource binding and state management
- Barrier and fence insertion
- Indirect command buffer encoding
- Debug markers and labels

**Missing Test Scenarios:**
- Encode compute commands correctly
- Resource bindings validated
- Memory barriers inserted appropriately
- Debug markers visible in profiler
- Indirect dispatch works

#### MetalCommandStream.cs (556 lines) - 0% Coverage
**Uncovered Functionality:**
- Command stream management
- Asynchronous command submission
- Command dependency tracking
- Stream synchronization
- Error recovery in command stream

**Missing Test Scenarios:**
- Commands execute in order
- Async submission doesn't block
- Dependencies respected
- Synchronization points work
- Errors recovered gracefully

#### MetalComputeGraph.cs (1,252 lines) - 0% Coverage
**Uncovered Functionality:**
- Compute graph construction
- Node dependency analysis
- Graph validation
- Resource lifetime management
- Graph serialization/deserialization

**Critical Missing Tests:**
- Graph construction API
- Dependency detection
- Cyclic dependency detection
- Resource allocation
- Graph optimization
- Serialization roundtrip

#### MetalGraphExecutor.cs (972 lines) - 0% Coverage
**Uncovered Functionality:**
- Graph execution scheduling
- Node parallelization
- Resource prefetching
- Execution state management
- Progress tracking

**Critical Missing Tests:**
- Graph executes correctly
- Parallel nodes execute concurrently
- Resources prefetched appropriately
- Execution can be paused/resumed
- Progress reported accurately

#### MetalGraphOptimizer.cs (738 lines) - 0% Coverage
**Uncovered Functionality:**
- Graph optimization passes
- Node fusion opportunities
- Memory reuse analysis
- Redundant operation elimination
- Execution plan generation

**Critical Missing Tests:**
- Fusion reduces kernel launches
- Memory reuse saves allocations
- Redundant ops eliminated
- Optimization preserves correctness
- Execution plan optimal

#### MetalGraphManager.cs (602 lines) - 0% Coverage
**Uncovered Functionality:**
- Graph lifecycle management
- Graph caching and reuse
- Version tracking
- Graph debugging support

**Missing Test Scenarios:**
- Graph cached after first use
- Cache invalidated appropriately
- Versions tracked correctly
- Debug info available

#### MetalEvent.cs (298 lines) - 0% Coverage
**Uncovered Functionality:**
- Event creation and signaling
- Event waiting and synchronization
- Event pooling for reuse
- Cross-queue synchronization

**Missing Test Scenarios:**
- Event signals correctly
- Wait blocks until signal
- Cross-queue sync works
- Pooling reduces allocations

#### MetalEventPool.cs (347 lines) - 0% Coverage
**Uncovered Functionality:**
- Event pool management
- Event recycling
- Pool size management
- Thread-safe access

**Missing Test Scenarios:**
- Pool provides events
- Events recycled after use
- Pool size bounded
- Thread-safe

#### MetalExecutionContext.cs (434 lines) - 0% Coverage
**Uncovered Functionality:**
- Execution context lifecycle
- Resource tracking per context
- Context switching overhead
- Nested context support

**Missing Test Scenarios:**
- Context creation/disposal
- Resources tracked correctly
- Context switch works
- Nested contexts supported

#### MetalExecutionLogger.cs (253 lines) - 20% Coverage
**Partially Covered, Needs:**
- Performance impact measurement
- Log filtering effectiveness
- Structured logging format
- Log level behavior

### 6. Telemetry Gaps (4,688 lines, 46% coverage)

#### MetalPerformanceCounters.cs (892 lines) - 0% Coverage
**Uncovered Functionality:**
- Hardware performance counter collection
- GPU utilization tracking
- Memory bandwidth measurement
- Shader execution statistics
- Counter aggregation and reporting

**Missing Test Scenarios:**
- Counters increment correctly
- Aggregation accurate
- Reporting format correct
- Counter overhead minimal

#### MetalHealthMonitor.cs (756 lines) - 0% Coverage
**Uncovered Functionality:**
- System health monitoring
- Resource exhaustion detection
- Performance degradation alerts
- Automatic recovery triggers
- Health score calculation

**Missing Test Scenarios:**
- Resource exhaustion detected
- Alerts triggered appropriately
- Recovery actions work
- Health score accurate

#### MetalMetricsExporter.cs (654 lines) - 0% Coverage
**Uncovered Functionality:**
- Metrics export to monitoring systems
- Format conversion (Prometheus, JSON, etc.)
- Periodic export scheduling
- Export failure handling

**Missing Test Scenarios:**
- Metrics export successfully
- Formats correct
- Scheduling works
- Failures handled

#### MetalAlertsManager.cs (523 lines) - 0% Coverage
**Uncovered Functionality:**
- Alert rule definition
- Alert triggering logic
- Alert notification delivery
- Alert suppression and aggregation

**Missing Test Scenarios:**
- Rules trigger correctly
- Notifications delivered
- Suppression prevents spam
- Aggregation reduces noise

### 7. Utilities Gaps (1,239 lines, 50% coverage)

#### MetalPerformanceProfiler.cs (412 lines) - 0% Coverage
**Uncovered Functionality:**
- GPU profiling API
- Timeline capture
- Hotspot identification
- Profile report generation

**Missing Test Scenarios:**
- Profiling captures timeline
- Hotspots identified
- Reports generated correctly
- Minimal overhead

#### MetalValidation.cs (267 lines) - 0% Coverage
**Uncovered Functionality:**
- Input validation helpers
- Resource state validation
- Configuration validation
- Assertion helpers

**Missing Test Scenarios:**
- Validation catches errors
- Assertions fire correctly
- Performance acceptable

#### MetalCommandBufferPool.cs (298 lines) - 0% Coverage
**Uncovered Functionality:**
- Command buffer pooling
- Buffer recycling
- Pool size management

**Missing Test Scenarios:**
- Pool provides buffers
- Buffers recycled
- Pool size bounded

#### SimpleRetryPolicy.cs (84 lines) - 0% Coverage
**Uncovered Functionality:**
- Retry logic with exponential backoff
- Max retry limit
- Retry condition evaluation

**Missing Test Scenarios:**
- Retry on transient errors
- Stop after max retries
- Backoff timing correct

---

## Test Priority Matrix

| Component | Lines | Current Coverage | Priority | Effort (days) | Risk |
|-----------|-------|------------------|----------|---------------|------|
| **MetalKernelCompiler** | 987 | 0% | 🔴 Critical | 5-7 | High |
| **MetalKernelCache** | 445 | 0% | 🔴 Critical | 3-4 | High |
| **MetalKernelOptimizer** | 621 | 0% | 🔴 Critical | 4-5 | High |
| **MetalComputeGraph** | 1,252 | 0% | 🟠 High | 6-8 | Medium |
| **MetalGraphExecutor** | 972 | 0% | 🟠 High | 5-6 | Medium |
| **MetalGraphOptimizer** | 738 | 0% | 🟠 High | 4-5 | Medium |
| **MetalCapabilityManager** | 339 | 0% | 🟠 High | 2-3 | Medium |
| **MetalCommandEncoder** | 715 | 0% | 🟡 Medium | 3-4 | Low |
| **MetalCommandStream** | 556 | 0% | 🟡 Medium | 3-4 | Low |
| **MetalPerformanceCounters** | 892 | 0% | 🟡 Medium | 3-4 | Low |
| **MetalHealthMonitor** | 756 | 0% | 🟢 Low | 2-3 | Low |

**Total Estimated Effort:** 40-53 developer days (~8-11 weeks)

---

## Actionable Next Steps

### Phase 1: Critical (Weeks 1-3)
1. **MetalKernelCompiler** tests (7 days)
2. **MetalKernelCache** tests (4 days)
3. **MetalKernelOptimizer** tests (5 days)

### Phase 2: High Priority (Weeks 4-7)
4. **MetalComputeGraph** tests (8 days)
5. **MetalGraphExecutor** tests (6 days)
6. **MetalGraphOptimizer** tests (5 days)
7. **MetalCapabilityManager** tests (3 days)

### Phase 3: Medium Priority (Weeks 8-10)
8. **MetalCommandEncoder** tests (4 days)
9. **MetalCommandStream** tests (4 days)
10. **MetalPerformanceCounters** tests (4 days)

### Phase 4: Polish (Weeks 11-12)
11. Complete remaining utilities and telemetry
12. Integration test suite
13. Performance regression tests

---

## Conclusion

The Metal backend has **significant gaps** in test coverage, particularly in:
- ⚠️ **Kernel compilation system** (entire pipeline untested)
- ⚠️ **Compute graph functionality** (major feature untested)
- ⚠️ **Configuration and device detection** (critical for robustness)

**Estimated effort to reach 80% coverage:** 8-11 weeks of focused development and testing.

---

**Document Version:** 1.0
**Last Updated:** October 27, 2025
**Next Review:** After Phase 1 completion
