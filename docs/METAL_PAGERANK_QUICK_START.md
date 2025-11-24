# Metal PageRank - Quick Start Guide

**Status**: Foundation in place, ready for implementation
**Main Guide**: See `metal-pagerank-implementation-guide.md` for comprehensive 80-page guide
**Estimated Time**: 6-9 weeks full implementation

## What's Been Completed

### ✅ Analysis & Planning
- [x] Comprehensive architecture analysis
- [x] Current state assessment (230KB of infrastructure code)
- [x] Gap analysis (what's missing vs what exists)
- [x] 6-phase implementation plan with dependencies
- [x] Testing strategy and performance targets
- [x] 80-page implementation guide with code examples

### ✅ Outstanding Issues Analysis
- [x] 13 TODOs documented and prioritized
- [x] Disposal issue identified (already fixed in codebase)
- [x] PageRank CUDA reference analyzed (3-kernel architecture)
- [x] Performance benchmarks targets defined

## What Needs Implementation

### 🔄 Critical Path (Must be done in order)

1. **Phase 1.1-1.2**: MSL Code Generation Enhancement (PRIORITY 1)
   - Add MemoryPack serialization integration
   - Fix message processing TODOs in `MetalRingKernelCompiler.cs:211-212, 238`
   - See `metal-pagerank-implementation-guide.md` lines 240-500 for detailed code

2. **Phase 1.3-1.4**: Barrier & K2K Routing Kernels (PRIORITY 2)
   - Create `MultiKernelBarrier.metal`
   - Create `KernelRouter.metal`
   - Wire into existing C# wrappers

3. **Phase 2**: PageRank Kernels (PRIORITY 3)
   - ContributionSender
   - RankAggregator
   - ConvergenceChecker

4. **Phase 3-6**: Integration, Testing, Benchmarks, Docs

## How to Get Started

### Option 1: Start with Phase 1.1 (Recommended)

**Goal**: Fix the core MSL generation to integrate MemoryPack

**File to Modify**: `src/Backends/DotCompute.Backends.Metal/RingKernels/MetalRingKernelCompiler.cs`

**Key Changes**:
1. Read the discovered kernel's `InputMessageTypeName` and `OutputMessageTypeName`
2. Use `_serializerGenerator` to generate MSL serialization code
3. Replace TODOs with actual deserialization/serialization calls
4. Add inline handler invocation

**Code Location**: See implementation guide lines 240-450 for complete code

**Test**: Create simple echo kernel to verify MSL compiles and runs

### Option 2: Start with Phase 2.1 (Message Definitions)

**Goal**: Define PageRank messages for Metal

**New File**: `samples/RingKernels/PageRank/Metal/PageRankMetalMessages.cs`

**Code**: See implementation guide lines 520-580

**Benefit**: Can test MemoryPack serialization independently

### Option 3: Study CUDA Reference

**File**: `samples/RingKernels/PageRank/PageRankKernels.cs` (CUDA version)

**Learn**:
- How 3-kernel architecture works
- Message flow patterns
- K2K messaging setup
- Barrier usage

Then adapt to Metal MSL syntax.

## Key Files Reference

### Source Files to Modify

| File | Purpose | Changes Needed |
|------|---------|----------------|
| `MetalRingKernelCompiler.cs` | MSL generation | Add MemoryPack integration (Phase 1) |
| `MetalMultiKernelBarrier.cs` | Barrier wrapper | Replace 3 TODOs with kernel launches |
| `MetalKernelRoutingTable.cs` | K2K routing | Add routing kernel execution |

### New Files to Create

| File | Purpose | Phase |
|------|---------|-------|
| `MultiKernelBarrier.metal` | Barrier kernels MSL | Phase 1 |
| `KernelRouter.metal` | Routing kernels MSL | Phase 1 |
| `PageRankMetalMessages.cs` | Message definitions | Phase 2 |
| `PageRankContributionSenderKernel.cs` | ContributionSender C# | Phase 2 |
| `PageRankContributionSender.metal` | ContributionSender MSL | Phase 2 |
| `PageRankRankAggregatorKernel.cs` | RankAggregator C# | Phase 2 |
| `PageRankRankAggregator.metal` | RankAggregator MSL | Phase 2 |
| `PageRankConvergenceCheckerKernel.cs` | ConvergenceChecker C# | Phase 2 |
| `PageRankConvergenceChecker.metal` | ConvergenceChecker MSL | Phase 2 |
| `MetalPageRankOrchestrator.cs` | Coordinator | Phase 3 |

### Test Files to Create

| File | Purpose | Phase |
|------|---------|-------|
| `MetalMslGenerationTests.cs` | Test MSL generation | Phase 1 |
| `PageRankContributionSenderTests.cs` | Test ContributionSender | Phase 2 |
| `PageRankRankAggregatorTests.cs` | Test RankAggregator | Phase 2 |
| `PageRankConvergenceCheckerTests.cs` | Test ConvergenceChecker | Phase 2 |
| `PageRankE2ETests.cs` | End-to-end tests | Phase 3 |
| `CpuPageRankBenchmark.cs` | CPU baseline | Phase 4 |
| `MetalBatchPageRankBenchmark.cs` | GPU batch | Phase 4 |
| `MetalNativeActorPageRankBenchmark.cs` | GPU native actor | Phase 4 |

## Critical Code Snippets

### 1. Enhanced MSL Generation (Phase 1.1)

**Location**: `MetalRingKernelCompiler.cs` line 210

**BEFORE**:
```csharp
sb.AppendLine("        if (input_queue->try_dequeue(msg_buffer)) {");
sb.AppendLine("            // TODO: Process message based on kernel logic");
```

**AFTER** (see full code in implementation guide):
```csharp
sb.AppendLine("        char msg_buffer[512];");
sb.AppendLine("        if (input_queue->try_dequeue(msg_buffer)) {");
sb.AppendLine($"            {inputMessageStruct} input_msg;");
sb.AppendLine($"            if (deserialize_{inputMessageStruct}((device const uchar*)msg_buffer, 512, &input_msg)) {{");
// ... handler invocation
sb.AppendLine($"                {outputMessageStruct} output_msg;");
// ... serialize output
```

### 2. PageRank Message (Phase 2.1)

```csharp
[MemoryPackable]
public partial struct PageRankContribution
{
    public int SourceNodeId { get; set; }
    public int TargetNodeId { get; set; }
    public float Contribution { get; set; }
    public int Iteration { get; set; }
}
```

### 3. Barrier Kernel MSL (Phase 1.3)

```metal
kernel void wait_at_multi_kernel_barrier_kernel(
    device MultiKernelBarrierState* barrier [[buffer(0)]],
    uint thread_id [[thread_position_in_grid]])
{
    if (thread_id == 0) {
        atomic_fetch_add_explicit(&barrier->arrived_count, 1, memory_order_acq_rel);

        while (atomic_load_explicit(&barrier->arrived_count, memory_order_acquire) < barrier->participant_count) {
            // Spin wait
        }

        threadgroup_barrier(mem_flags::mem_device);
    }
}
```

## Common Metal vs CUDA Differences

| Concept | CUDA | Metal | Notes |
|---------|------|-------|-------|
| Thread ID | `threadIdx.x` | `thread_in_threadgroup` | Direct mapping |
| Block ID | `blockIdx.x` | `threadgroup_id` | Direct mapping |
| Shared Memory | `__shared__` | `threadgroup` | Address space qualifier |
| Barrier | `__syncthreads()` | `threadgroup_barrier(mem_flags)` | Requires memory flags |
| Atomic | `atomicAdd(&x, 1)` | `atomic_fetch_add_explicit(&x, 1, memory_order_relaxed)` | Explicit memory order |
| Grid Barrier | `cooperative_groups::grid_group` | ❌ NOT SUPPORTED | Use multiple dispatches |

## Debugging Tips

### 1. Save Generated MSL

```csharp
// In MetalRingKernelCompiler.cs
var mslSource = GenerateMslSource(kernel);
File.WriteAllText($"/tmp/{kernelId}.metal", mslSource);
```

### 2. Compile MSL Manually

```bash
# Test MSL compilation
xcrun -sdk macosx metal -c /tmp/kernel.metal -o /tmp/kernel.air

# Check for errors
xcrun -sdk macosx metal -c /tmp/kernel.metal 2>&1 | less
```

### 3. Enable Verbose Logging

```csharp
// In test or application
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
```

### 4. Use Xcode Metal Debugger

```bash
# Capture Metal frame
# Product > Scheme > Edit Scheme > Run > Options
# Enable "GPU Frame Capture"
```

## Performance Expectations

### Ring Kernel Infrastructure

- Kernel launch: <5ms (target)
- K2K message throughput: >1M msgs/sec (target)
- Coordination overhead: <100μs (target)

### PageRank

| Graph Size | CPU Baseline | GPU Batch | GPU Actor |
|------------|--------------|-----------|-----------|
| 100 nodes | 1x | 0.5-1x | 0.5-1x |
| 1K nodes | 1x | 2-5x | 3-7x |
| 10K nodes | 1x | 5-15x | 10-30x |
| 100K nodes | 1x | 10-30x | 30-100x |

## Next Steps

1. **Read the full implementation guide** (`metal-pagerank-implementation-guide.md`)
2. **Choose a starting point** (recommend Phase 1.1)
3. **Set up development environment**:
   - macOS with Xcode
   - .NET 9 SDK
   - Metal-capable Mac (Apple Silicon preferred)
4. **Create a branch**: `git checkout -b feature/metal-pagerank`
5. **Start implementing Phase 1.1**
6. **Test incrementally** - don't wait until everything is done
7. **Commit frequently** - this is a 6-9 week project

## Resources

- **Main Guide**: `metal-pagerank-implementation-guide.md` (80 pages, comprehensive)
- **Metal Docs**: https://developer.apple.com/metal/
- **MSL Spec**: https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf
- **MemoryPack**: https://github.com/Cysharp/MemoryPack
- **CUDA Reference**: `samples/RingKernels/PageRank/PageRankKernels.cs`

## Support

If you encounter issues:

1. Check implementation guide for detailed solutions
2. Review CUDA reference implementation
3. Enable verbose logging
4. Save and manually compile MSL
5. Check Metal validation layers

## Summary

**You now have**:
- ✅ Complete implementation guide (80 pages)
- ✅ This quick-start guide
- ✅ Clear 6-phase plan
- ✅ Code examples for all phases
- ✅ Testing strategy
- ✅ Performance targets

**You need to**:
- 🔄 Implement Phase 1-6 (6-9 weeks)
- 🔄 Test incrementally
- 🔄 Benchmark and optimize
- 🔄 Document as you go

**Start here**: Phase 1.1 - Enhance MSL generation in `MetalRingKernelCompiler.cs`

Good luck! 🚀
