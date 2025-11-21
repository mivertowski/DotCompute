# Metal Backend Thread-Safety Analysis

**Date**: 2025-11-21
**Issue**: Segmentation fault when executing kernels in parallel
**Severity**: CRITICAL - Blocks parallel workloads

---

## 🐛 Problem Summary

When executing multiple Metal kernels in parallel (e.g., using `Task.WhenAll`), the application crashes with:
- **Exit Code**: 139 (SIGSEGV)
- **Fault Address**: `0x000002f93b6ef800` (invalid memory access)
- **Impact**: Cannot use parallel kernel execution, Graph Execution Parallelism validation fails

---

## 🔍 Root Cause Analysis

### Finding #1: Shared Command Queue Without Synchronization

**Location**: `MetalCompiledKernel.ExecuteAsync()` (line 104)

```csharp
// ALL kernels share the SAME command queue
commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
```

**Issue**:
- Multiple threads execute kernels simultaneously
- All create command buffers from the **same** `_commandQueue` instance
- No synchronization around command queue access
- Metal command queue IS thread-safe for buffer creation, BUT:
  - Encoder operations may not be atomic
  - Completion handlers may race
  - Native interop layer may have issues

**Evidence**:
```
Thread 1: CreateCommandBuffer(_commandQueue) -> encoder1 -> commit1
Thread 2: CreateCommandBuffer(_commandQueue) -> encoder2 -> commit2  // RACE!
Thread 3: CreateCommandBuffer(_commandQueue) -> encoder3 -> commit3  // RACE!
```

### Finding #2: Compilation Semaphore Doesn't Protect Execution

**Location**: `MetalKernelCompiler.CompileAsync()` (line 169)

```csharp
await _compilationSemaphore.WaitAsync(cancellationToken);  // Only protects compilation!
try { /* compile kernel */ }
finally { _compilationSemaphore.Release(); }
```

**Issue**:
- Semaphore only protects **compilation**, not **execution**
- Once compiled, multiple threads can execute simultaneously
- No synchronization for command queue access during execution

### Finding #3: Cache Access Is Thread-Safe (✅ Not the Issue)

**Location**: `MetalKernelCache.TryGetKernel()` (line 268)

```csharp
_cacheLock.EnterReadLock();  // ✅ Properly synchronized
try {
    if (_cache.TryGetValue(cacheKey, out var entry)) {  // ✅ ConcurrentDictionary
        MetalNative.RetainLibrary(library);  // ✅ Atomic retain
        MetalNative.RetainPipelineState(pipelineState);  // ✅ Atomic retain
    }
}
finally { _cacheLock.ExitReadLock(); }
```

**Analysis**: Cache is properly thread-safe with `ReaderWriterLockSlim` + `ConcurrentDictionary`

### Finding #4: Command Buffer Pool Is Thread-Safe (✅ Not the Issue)

**Location**: `MetalCommandBufferPool.GetCommandBuffer()` (line 42)

```csharp
var buffer = MetalNative.CreateCommandBuffer(_commandQueue);  // ✅ Thread-safe API
_activeBuffers.TryAdd(buffer, info);  // ✅ ConcurrentDictionary
```

**Analysis**: Pool properly uses `ConcurrentDictionary` for tracking

---

## 🎯 The Real Issue: Native Metal API Thread-Safety

### Hypothesis

The crash occurs because:

1. **Multiple threads create encoders simultaneously** from different command buffers on the same queue
2. **Completion handlers race** when multiple command buffers finish at the same time
3. **Native Metal objects** (encoders, pipeline states) are being accessed without proper synchronization
4. **P/Invoke marshalling** may have threading issues with callbacks

### Evidence from Crash Report

```
Exception Type: EXC_BAD_ACCESS (SIGSEGV)
Exception Subtype: KERN_INVALID_ADDRESS at 0x000002f93b6ef800
Faulting Thread: 84

Likely in: MetalNative calls or completion handler callback
```

---

## 🔧 Proposed Solutions

### Solution 1: Serialize Command Buffer Creation (Quick Fix)

Add a lock around command buffer creation and encoder setup:

```csharp
public sealed class MetalCompiledKernel
{
    private static readonly SemaphoreSlim _executionSemaphore = new(1, 1);  // Global lock

    public async ValueTask ExecuteAsync(...)
    {
        await _executionSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Get command buffer
            commandBuffer = _commandBufferPool?.GetCommandBuffer()
                ?? MetalNative.CreateCommandBuffer(_commandQueue);

            // Create encoder and dispatch
            var encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
            // ... rest of execution ...
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }
}
```

**Pros**:
- Simple, minimal code changes
- Guaranteed to prevent race conditions
- Easy to test

**Cons**:
- Serializes ALL kernel execution (no parallelism!)
- Defeats the purpose of parallel execution
- Performance regression

### Solution 2: Per-CommandQueue Lock (Better)

Add synchronization per command queue:

```csharp
public sealed class MetalCompiledKernel
{
    private readonly SemaphoreSlim _queueLock;  // One per queue

    public async ValueTask ExecuteAsync(...)
    {
        await _queueLock.WaitAsync(cancellationToken);
        try
        {
            // Critical section: buffer creation + encoder setup
            commandBuffer = MetalNative.CreateCommandBuffer(_commandQueue);
            encoder = MetalNative.CreateComputeCommandEncoder(commandBuffer);
            MetalNative.SetComputePipelineState(encoder, _pipelineState);
            SetKernelArguments(encoder, arguments);
            MetalNative.DispatchThreadgroups(encoder, gridSize, threadgroupSize);
            MetalNative.EndEncoding(encoder);
            MetalNative.CommitCommandBuffer(commandBuffer);
        }
        finally
        {
            _queueLock.Release();
        }

        // Wait for completion OUTSIDE the lock (allows parallelism)
        await tcs.Task;
    }
}
```

**Pros**:
- Protects critical Metal API calls
- Allows parallelism for different queues
- Minimal performance impact

**Cons**:
- Still serializes execution on same queue
- Slightly more complex

### Solution 3: Multiple Command Queues (Best for Performance)

Create a pool of command queues for parallel execution:

```csharp
public sealed class MetalKernelCompiler
{
    private readonly CommandQueuePool _queuePool;

    public MetalKernelCompiler(...)
    {
        // Create multiple queues for parallel execution
        _queuePool = new CommandQueuePool(_device, maxConcurrency: 4);
    }

    public async ValueTask<ICompiledKernel> CompileAsync(...)
    {
        // Each kernel gets its own queue from the pool
        var queue = _queuePool.GetQueue();
        return new MetalCompiledKernel(..., queue, ...);
    }
}
```

**Pros**:
- True parallelism - multiple queues can execute simultaneously
- Best performance for parallel workloads
- Follows Metal best practices (multiple queues for concurrent work)

**Cons**:
- More complex implementation
- Need to manage queue lifecycle
- Need to test queue pool thoroughly

### Solution 4: Thread-Safe Wrapper Around Native Calls (Most Robust)

Wrap all Metal native calls with synchronization:

```csharp
public static class ThreadSafeMetalNative
{
    private static readonly SemaphoreSlim _nativeLock = new(1, 1);

    public static async Task<IntPtr> CreateCommandBufferAsync(IntPtr queue)
    {
        await _nativeLock.WaitAsync();
        try
        {
            return MetalNative.CreateCommandBuffer(queue);
        }
        finally
        {
            _nativeLock.Release();
        }
    }

    // Similar wrappers for other native calls...
}
```

**Pros**:
- Most robust - guarantees thread-safety at the lowest level
- Easy to debug and reason about
- Catches ALL potential race conditions

**Cons**:
- Most invasive changes
- Performance overhead for every native call
- May serialize more than necessary

---

## 📋 Recommended Solution

**Hybrid Approach: Solution 2 + Solution 3**

1. **Short-term (Hot Fix)**: Implement Solution 2 (per-queue lock)
   - Fixes the crash immediately
   - Minimal code changes
   - Acceptable performance for most workloads

2. **Long-term (Optimization)**: Implement Solution 3 (queue pool)
   - Enables true parallelism
   - Better performance for multi-threaded workloads
   - Aligns with Metal best practices

---

## 🧪 Testing Strategy

1. **Unit Test**: Create test that executes 10 kernels in parallel
2. **Stress Test**: Execute 100+ kernels concurrently to find edge cases
3. **Validation**: Re-enable Graph Execution Parallelism test
4. **Benchmark**: Measure performance before/after fix

---

## 📝 Implementation Checklist

- [ ] Add per-queue semaphore to `MetalCompiledKernel`
- [ ] Wrap critical section (buffer creation through commit)
- [ ] Test with parallel kernel execution
- [ ] Verify no crashes with 10+ concurrent kernels
- [ ] Measure performance impact
- [ ] Re-enable Graph Execution test
- [ ] Document thread-safety guarantees
- [ ] Consider queue pool for v0.5.0

---

## 🚨 Additional Considerations

### MPS Disposal Crash (Separate Issue)

The MPS performance test also crashes during cleanup. This may be related to:
- Improper device lifecycle management in `MetalMPSOrchestrator`
- Device being released while MPS operations are still in flight
- Need separate investigation

### Performance Impact

Adding synchronization will impact performance:
- **Best case**: ~5-10% overhead (quick lock/unlock)
- **Worst case**: Serializes execution (no parallelism)
- **Mitigation**: Use queue pool for true parallelism

---

## 📚 References

- [Metal Best Practices Guide](https://developer.apple.com/library/archive/documentation/3DDrawing/Conceptual/MTLBestPracticesGuide/)
- [Metal Threading and Concurrency](https://developer.apple.com/documentation/metal/resource_fundamentals/synchronizing_cpu_and_gpu_work)
- DotCompute Issue: Graph Execution Parallelism validation crash

---

**Status**: Analysis complete, ready for implementation
**Next Step**: Implement Solution 2 (per-queue lock) as hotfix
