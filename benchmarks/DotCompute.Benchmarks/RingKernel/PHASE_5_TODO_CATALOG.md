# Phase 5: TODO Catalog and Implementation Plan

## Overview

This document catalogs all outstanding TODOs in the Metal backend and provides a systematic implementation plan for Phase 5, which will enable actual Metal Ring Kernel execution for PageRank.

**Goal**: Replace stub implementations with real Metal Ring Kernel infrastructure to enable:
- Persistent GPU kernel actors
- K2K (kernel-to-kernel) message routing
- Multi-kernel barrier synchronization
- MemoryPack serialization/deserialization
- PageRank convergence detection

---

## TODO Catalog

### Critical TODOs (PageRank Orchestrator - 6 items)

**File**: `samples/RingKernels/PageRank/Metal/MetalPageRankOrchestrator.cs`

| Line | Priority | TODO | Impact |
|------|----------|------|--------|
| 190 | **CRITICAL** | Configure routing table with kernel IDs | K2K messaging won't work |
| 198 | **CRITICAL** | Initialize barrier with participant count = 3 | Synchronization won't work |
| 293 | **CRITICAL** | Implement actual kernel launch via MetalRingKernelRuntime | Kernels won't execute |
| 336 | **CRITICAL** | Send messages via MetalRingKernelRuntime | Graph distribution won't work |
| 367 | **CRITICAL** | Poll for ConvergenceCheckResult messages | Results won't be collected |
| 394 | **CRITICAL** | Collect final ranks from RankAggregator | Final output missing |

**Status**: All 6 block PageRank functionality completely

---

### High Priority TODOs (Ring Kernel Infrastructure - 6 items)

**File**: `src/Backends/DotCompute.Backends.Metal/RingKernels/Native/TopicPubSub.metal`

| Line | Priority | TODO | Impact |
|------|----------|------|--------|
| 178 | **HIGH** | Route message to subscriber using route_message_to_kernel() | Topic routing incomplete |
| 190 | **HIGH** | Route message to subscriber using route_message_to_kernel() | Topic routing incomplete |
| 419 | **HIGH** | Route message to subscriber | Broadcast routing incomplete |
| 433 | **HIGH** | Route message to subscriber | Broadcast routing incomplete |

**File**: `src/Backends/DotCompute.Backends.Metal/RingKernels/MetalRingKernelCompiler.cs`

| Line | Priority | TODO | Impact |
|------|----------|------|--------|
| 211 | **HIGH** | Process message based on kernel logic | Message processing placeholder |
| 238 | **HIGH** | Process message based on kernel logic | Message processing placeholder |

**Status**: These affect message routing efficiency but have fallback mechanisms

---

### Medium Priority TODOs (Infrastructure - 5 items)

| File | Line | TODO | Impact |
|------|------|------|--------|
| MetalBarrierProvider.cs | 284 | Implement barrier-aware kernel execution | Optimization missing |
| MetalMemoryOrderingProvider.cs | 164 | Integration with kernel compiler | Memory consistency incomplete |
| MetalKernelParameterBinder.cs | 239 | Support for unified/pooled buffers | Memory optimization missing |
| MetalMemoryManager.cs | 251 | Implement pool hit rate tracking | Metric missing |
| MetalKernelCache.cs | 81 | Get Metal version from device | Hardcoded version |

**Status**: Nice-to-have optimizations, not blocking

---

### Low Priority TODOs (Cleanup - 2 items)

| File | Line | TODO | Impact |
|------|------|------|--------|
| MetalUnifiedMemoryOptimizer.cs | 195 | Cleanup resources | Resource leak potential |
| MetalOperationDescriptor.cs | 49, 61 | Extract types from other files | Code organization |

**Status**: Technical debt, not blocking functionality

---

## Phase 5 Implementation Plan

### Stage 1: Critical Infrastructure (Week 1)

**Goal**: Enable basic Ring Kernel execution

1. **TODO #1**: Implement MetalRingKernelRuntime.LaunchAsync
   - Create persistent GPU kernel instances
   - Set up kernel execution loops
   - Target: <500μs launch latency

2. **TODO #2**: Implement K2K routing table initialization
   - Configure 3-kernel routing (ContributionSender → RankAggregator → ConvergenceChecker)
   - Set up Metal buffer-based message queues
   - Target: <100ns routing overhead

3. **TODO #3**: Implement multi-kernel barrier initialization
   - Create Metal atomic barriers for 3 participants
   - Configure iteration synchronization points
   - Target: <20ns per sync

**Deliverables**:
- `MetalRingKernelRuntime.cs`: LaunchAsync implementation
- `MetalKernelRoutingTableManager.cs`: InitializeRoutingTableAsync implementation
- `MetalMultiKernelBarrierManager.cs`: CreateBarrierAsync implementation

---

### Stage 2: Message Distribution (Week 1-2)

**Goal**: Enable graph node distribution and message routing

4. **TODO #4**: Implement message sending via MetalRingKernelRuntime
   - MemoryPack serialization integration
   - Metal buffer allocation for messages
   - Message queue enqueue operations
   - Target: >2M messages/sec

5. **TODO #5**: Implement topic/broadcast routing in TopicPubSub.metal
   - Complete route_message_to_kernel() calls
   - Wire up K2K routing table lookups
   - Enable multi-subscriber broadcasts

**Deliverables**:
- `MetalRingKernelRuntime.cs`: SendMessageAsync implementation
- `TopicPubSub.metal`: Complete routing logic (4 TODOs)

---

### Stage 3: Result Collection (Week 2)

**Goal**: Enable convergence detection and result gathering

6. **TODO #6**: Implement message polling/receiving
   - Poll for ConvergenceCheckResult messages
   - Metal buffer read operations
   - MemoryPack deserialization

7. **TODO #7**: Implement rank collection from RankAggregator
   - Read final rank buffers
   - Deserialize to Dictionary<int, float>
   - Validate convergence criteria

**Deliverables**:
- `MetalRingKernelRuntime.cs`: ReceiveMessagesAsync implementation
- `MetalPageRankOrchestrator.cs`: Result collection logic

---

### Stage 4: Testing & Validation (Week 2)

**Goal**: Validate all benchmarks pass with real implementation

8. **Run all 53 benchmarks**
   - PageRank CPU baseline
   - PageRank Metal batch
   - PageRank Metal native actor
   - MemoryPack serialization

9. **Run Metal validation suite**
   - Original 7 claims (--simple)
   - New 5 PageRank claims (--pagerank)
   - Target: 12/12 passing

10. **Run E2E integration tests**
    - 3-kernel pipeline execution
    - Graph validation (10/100/1000 nodes)
    - Convergence accuracy (<0.0001 error)

**Deliverables**:
- All 156 Metal tests passing
- All 53 benchmarks running
- 12/12 validation claims passing

---

## Implementation Details

### TODO #1: MetalRingKernelRuntime.LaunchAsync

```csharp
public async Task LaunchAsync(
    string kernelId,
    int gridSize,
    int blockSize,
    Dictionary<string, object>? parameters,
    CancellationToken cancellationToken)
{
    // 1. Get compiled kernel from compiler
    var kernelDef = _discoveredKernels[kernelId];
    var compiledKernel = await _compiler.CompileAsync(kernelDef, cancellationToken);

    // 2. Set up persistent execution loop
    var executionLoop = CreatePersistentLoop(compiledKernel, gridSize, blockSize);

    // 3. Launch on Metal command queue
    var commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
    var computeEncoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);

    // 4. Configure threadgroup sizes
    MetalNative.SetComputePipelineState(computeEncoder, compiledKernel.PipelineState);
    MetalNative.DispatchThreadgroups(computeEncoder, gridSize, blockSize);

    // 5. Commit and track
    MetalNative.CommitCommandBuffer(commandBuffer);
    _runningKernels[kernelId] = executionLoop;
}
```

### TODO #2: K2K Routing Table Initialization

```csharp
public async Task InitializeRoutingTableAsync(
    List<DiscoveredRingKernel> kernels,
    CancellationToken cancellationToken)
{
    // 1. Create routing buffer (kernel_id → queue_buffer_ptr mapping)
    var routingTableSize = kernels.Count * sizeof(ulong) * 2; // (id, ptr) pairs
    var routingBuffer = MetalNative.CreateBuffer(
        _device,
        routingTableSize,
        MTLResourceOptions.StorageModeShared);

    // 2. Populate routing table
    unsafe
    {
        var ptr = (ulong*)MetalNative.GetBufferContents(routingBuffer);
        for (int i = 0; i < kernels.Count; i++)
        {
            var kernel = kernels[i];
            var queueBuffer = await CreateMessageQueueAsync(kernel.KernelId, cancellationToken);

            ptr[i * 2] = (ulong)kernel.KernelId.GetHashCode(); // Simple hash for demo
            ptr[i * 2 + 1] = (ulong)queueBuffer.ToInt64();
        }
    }

    // 3. Bind to all kernels
    foreach (var kernel in kernels)
    {
        await BindRoutingTableToKernelAsync(kernel.KernelId, routingBuffer, cancellationToken);
    }

    _routingTable = routingBuffer;
}
```

### TODO #3: Multi-Kernel Barrier Initialization

```csharp
public async Task CreateBarrierAsync(
    int participantCount,
    CancellationToken cancellationToken)
{
    // 1. Create atomic counter buffer
    var barrierBuffer = MetalNative.CreateBuffer(
        _device,
        sizeof(int) * 3, // counter, expected, generation
        MTLResourceOptions.StorageModeShared);

    // 2. Initialize barrier state
    unsafe
    {
        var ptr = (int*)MetalNative.GetBufferContents(barrierBuffer);
        ptr[0] = 0; // counter = 0
        ptr[1] = participantCount; // expected = 3
        ptr[2] = 0; // generation = 0
    }

    // 3. Bind to all participant kernels
    foreach (var kernelId in _participantKernels)
    {
        await BindBarrierToKernelAsync(kernelId, barrierBuffer, cancellationToken);
    }

    _barrier = barrierBuffer;
}
```

---

## Success Criteria

### Stage 1 Complete:
- ✅ 3 Ring Kernel actors launch successfully
- ✅ Launch latency <500μs
- ✅ K2K routing table initialized (3 entries)
- ✅ Multi-kernel barrier initialized (3 participants)

### Stage 2 Complete:
- ✅ MetalGraphNode messages sent to ContributionSender
- ✅ K2K routing functional (ContributionSender → RankAggregator)
- ✅ Message throughput >2M msg/sec
- ✅ Routing overhead <100ns/msg

### Stage 3 Complete:
- ✅ ConvergenceCheckResult messages received
- ✅ Final ranks collected from RankAggregator
- ✅ Results match CPU baseline (<0.0001 error)

### Stage 4 Complete:
- ✅ All 156 Metal tests passing
- ✅ All 53 benchmarks executable
- ✅ 12/12 validation claims passing
- ✅ Phase 5 complete, ready for production

---

## Risk Assessment

### High Risk:
- **Metal buffer lifetime management**: Potential crashes if buffers released while kernels running
- **MemoryPack integration**: Serialization overhead may exceed 100ns target
- **Barrier synchronization**: Deadlocks possible if one kernel stalls

### Mitigation:
- Use Metal autorelease pools for buffer management
- Profile MemoryPack with BenchmarkDotNet
- Implement timeout/watchdog for barrier waits

### Medium Risk:
- **Message queue overflow**: Fixed-size queues may fill up
- **K2K routing performance**: Hash table lookups may add latency

### Mitigation:
- Implement dynamic queue resizing or backpressure
- Use perfect hashing for 3-kernel routing (O(1) guaranteed)

---

## Timeline

| Week | Stage | Deliverables | Success Metric |
|------|-------|--------------|----------------|
| 1 | Stage 1 | Runtime, routing, barriers | Kernels launch successfully |
| 1-2 | Stage 2 | Message distribution | >2M msg/sec throughput |
| 2 | Stage 3 | Result collection | Ranks match CPU baseline |
| 2 | Stage 4 | Testing & validation | 12/12 claims passing |

**Total**: 2 weeks for full Phase 5 completion

---

## Next Steps

1. **Create MetalRingKernelRuntime.cs** with LaunchAsync implementation
2. **Implement K2K routing table manager** initialization
3. **Implement multi-kernel barrier manager** initialization
4. **Wire up message sending/receiving** in MetalPageRankOrchestrator
5. **Test with simple 10-node graph** before scaling to 1000 nodes
6. **Run full validation suite** and benchmark all 53 tests

---

**Document Version**: 1.0
**Phase**: 5 (Implementation)
**Status**: Ready to begin
**Est. Completion**: 2 weeks from start
