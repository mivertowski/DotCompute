# OpenCL Backend Architecture

Comprehensive architectural documentation for DotCompute's OpenCL backend implementation.

## Table of Contents

- [Overview](#overview)
- [Component Architecture](#component-architecture)
- [Compilation Pipeline](#compilation-pipeline)
- [Memory Management](#memory-management)
- [Execution Engine](#execution-engine)
- [Resource Management](#resource-management)
- [Performance Optimization](#performance-optimization)
- [Thread Safety](#thread-safety)

## Overview

The OpenCL backend provides cross-vendor GPU and CPU acceleration through a production-ready implementation with the following characteristics:

- **Vendor-agnostic**: Works with NVIDIA, AMD, Intel, and other OpenCL 1.2+ devices
- **High performance**: 90%+ allocation reduction through pooling, multi-tier compilation caching
- **Production-ready**: Thread-safe, async-first, comprehensive error handling
- **Native AOT compatible**: No runtime code generation

### Key Design Principles

1. **Separation of Concerns**: Each component has a single, well-defined responsibility
2. **Async-First**: All I/O and potentially blocking operations are asynchronous
3. **Resource Pooling**: Command queues, events, and memory buffers are pooled
4. **Vendor Optimization**: Automatic detection and application of vendor-specific optimizations
5. **Graceful Degradation**: Fallback mechanisms for missing features

## Component Architecture

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    OpenCLAccelerator                        │
│  (Main entry point, implements IAccelerator)               │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ Initialize   │  │ Compile      │  │ Execute      │    │
│  │ Select Device│  │ Kernel       │  │ Synchronize  │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
└─────────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Device     │  │  Compilation │  │   Execution  │
│  Management  │  │   Pipeline   │  │    Engine    │
└──────────────┘  └──────────────┘  └──────────────┘
        │                  │                  │
        ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│Platform/     │  │Kernel        │  │Stream/Event  │
│Device Info   │  │Compiler      │  │Management    │
└──────────────┘  └──────────────┘  └──────────────┘
                                             │
                                             ▼
                                    ┌──────────────┐
                                    │   Memory     │
                                    │  Management  │
                                    └──────────────┘
```

### Core Components

#### 1. OpenCLAccelerator

**File**: `OpenCLAccelerator.cs`

Main accelerator implementation that coordinates all subsystems.

**Responsibilities**:
- Device initialization and selection
- Lifecycle management of subsystems
- Kernel compilation coordination
- Memory allocation delegation
- Synchronization operations

**Key Dependencies**:
- `OpenCLContext`: OpenCL context wrapper
- `OpenCLDeviceManager`: Device discovery and selection
- `OpenCLMemoryManager`: Memory operations
- `OpenCLKernelCompiler`: Kernel compilation (Phase 2 Week 1)
- `OpenCLKernelExecutionEngine`: Kernel execution (Phase 2 Week 1)

#### 2. OpenCLContext

**File**: `OpenCLContext.cs`

Encapsulates OpenCL context, command queue, and device information.

**Responsibilities**:
- OpenCL context creation and management
- Command queue creation
- Program and kernel creation
- Resource cleanup

**Native Handles Managed**:
- `cl_context`: OpenCL context handle
- `cl_command_queue`: Default command queue
- `cl_program`: Program handles
- `cl_kernel`: Kernel handles

#### 3. Device Management Layer

**File**: `OpenCLDeviceManager.cs`

Discovers and manages OpenCL platforms and devices.

**Responsibilities**:
- Platform enumeration via `clGetPlatformIDs`
- Device discovery via `clGetDeviceIDs`
- Capability detection (memory size, compute units, extensions)
- Best device selection algorithm

**Selection Algorithm**:
```
Priority Order:
1. Discrete GPU (highest priority)
2. Integrated GPU
3. Accelerator
4. CPU (lowest priority)

Within same type, rank by:
1. Estimated GFlops (compute units × clock frequency)
2. Global memory size
```

#### 4. Compilation Pipeline

**File**: `Compilation/OpenCLKernelCompiler.cs` (Phase 2 Week 1)

Comprehensive kernel compilation with caching and error handling.

**Architecture**:
```
Source Code
    │
    ▼
┌─────────────────┐
│ Cache Lookup    │──── Cache Hit ────┐
│ (Memory + Disk) │                   │
└─────────────────┘                   │
    │ Cache Miss                       │
    ▼                                  ▼
┌─────────────────┐             ┌──────────────┐
│ Apply Vendor    │             │ Create from  │
│ Optimizations   │             │ Binary       │
└─────────────────┘             └──────────────┘
    │                                  │
    ▼                                  │
┌─────────────────┐                   │
│ clCreateProgram │                   │
│ WithSource      │                   │
└─────────────────┘                   │
    │                                  │
    ▼                                  │
┌─────────────────┐                   │
│ clBuildProgram  │                   │
└─────────────────┘                   │
    │                                  │
    ▼                                  │
┌─────────────────┐                   │
│ Extract Binary  │                   │
│ (Cache Storage) │                   │
└─────────────────┘                   │
    │                                  │
    ▼                                  │
┌─────────────────┐                   │
│ clCreateKernel  │◄──────────────────┘
└─────────────────┘
    │
    ▼
┌─────────────────┐
│ Enrich Metadata │
│ (Arguments,     │
│  Work Sizes,    │
│  Memory Usage)  │
└─────────────────┘
    │
    ▼
CompiledKernel
```

**Key Features**:
- **Two-tier caching**: In-memory cache + disk-based persistent cache
- **Vendor optimization**: Automatic application of vendor-specific compiler flags
- **Error analysis**: Build log parsing with helpful suggestions
- **Metadata extraction**: Argument info, work group sizes, memory requirements
- **Async compilation**: Non-blocking compilation operations

#### 5. OpenCLCompilationCache

**File**: `Compilation/OpenCLCompilationCache.cs`

Multi-tier caching system for compiled binaries.

**Cache Hierarchy**:
```
┌──────────────────────────────────────┐
│         Memory Cache (L1)            │
│  ConcurrentDictionary<string, byte[]>│
│  Fast access, process lifetime       │
└──────────────────────────────────────┘
                ▼
┌──────────────────────────────────────┐
│          Disk Cache (L2)             │
│  ~/.dotcompute/opencl-cache/         │
│  Persistent across runs              │
└──────────────────────────────────────┘
```

**Cache Key Generation**:
```csharp
SHA256(source + options + deviceName + OpenCLVersion)
```

**Features**:
- Thread-safe access via `SemaphoreSlim`
- Automatic invalidation on source change
- Size limits with LRU eviction
- Atomic write operations

#### 6. Kernel Execution Engine

**File**: `Execution/OpenCLKernelExecutionEngine.cs` (Phase 2 Week 1)

Manages kernel argument binding and NDRange execution.

**Execution Flow**:
```
Kernel Arguments
    │
    ▼
┌─────────────────┐
│ Argument        │
│ Type Analysis   │
└─────────────────┘
    │
    ▼
┌─────────────────┐
│ clSetKernelArg  │ ◄─── For each argument
└─────────────────┘
    │
    ▼
┌─────────────────┐
│ Work Size       │
│ Optimization    │
└─────────────────┘
    │
    ▼
┌─────────────────┐
│ clEnqueueND     │
│ RangeKernel     │
└─────────────────┘
    │
    ▼
┌─────────────────┐
│ Event Tracking  │
│ (Optional)      │
└─────────────────┘
```

**Argument Handling**:
- **Buffer arguments**: Pass cl_mem handle
- **Scalar arguments**: Direct value passing
- **Local memory**: Size specification
- **Image arguments**: cl_mem image handle

**Work Size Optimization**:
```csharp
// Automatic work group size calculation
if (localWorkSize == null)
{
    // Round up to multiple of preferred work group size
    var preferredSize = device.PreferredWorkGroupSizeMultiple;
    localWorkSize = RoundUpToMultiple(globalWorkSize, preferredSize);

    // Ensure within device limits
    localWorkSize = Math.Min(localWorkSize, device.MaxWorkGroupSize);
}
```

## Memory Management

### OpenCLMemoryManager

**File**: `Memory/OpenCLMemoryManager.cs`

Unified memory management with buffer tracking and pooling.

**Architecture**:
```
IUnifiedMemoryManager Interface
    │
    ▼
OpenCLMemoryManager
    │
    ├─► OpenCLMemoryBuffer<T> (per-type buffers)
    │       │
    │       └─► OpenCLMemObject (native cl_mem handle)
    │
    └─► OpenCLMemoryPoolManager (Phase 1)
            │
            └─► Buffer Pool (reusable allocations)
```

**Buffer Lifecycle**:
```
1. Allocate:
   AllocateAsync<T>(count)
   → clCreateBuffer(CL_MEM_READ_WRITE, size)
   → Track in _allocatedBuffers
   → Update _currentAllocatedMemory

2. Use:
   CopyFromAsync(host → device) → clEnqueueWriteBuffer
   CopyToAsync(device → host) → clEnqueueReadBuffer
   Kernel execution → clSetKernelArg(buffer.Handle)

3. Free:
   Free(buffer)
   → Remove from tracking
   → clReleaseMemObject
   → Update _currentAllocatedMemory
```

**Memory Flags Mapping**:
```csharp
MemoryOptions.None           → CL_MEM_READ_WRITE
MemoryOptions.ReadOnly       → CL_MEM_READ_ONLY
MemoryOptions.WriteOnly      → CL_MEM_WRITE_ONLY
MemoryOptions.Mapped         → CL_MEM_ALLOC_HOST_PTR
MemoryOptions.UseHostPointer → CL_MEM_USE_HOST_PTR
```

### OpenCLMemoryPoolManager

**File**: `Memory/OpenCLMemoryPoolManager.cs` (Phase 1)

Advanced buffer pooling for allocation reuse.

**Pool Architecture**:
```
Size-based buckets:
[0-4KB]    → Pool of small buffers
[4-64KB]   → Pool of medium buffers
[64KB-1MB] → Pool of large buffers
[1MB+]     → Pool of huge buffers (or direct allocation)

Each bucket:
- Free list of available buffers
- In-use tracking
- LRU eviction policy
```

**Allocation Strategy**:
```csharp
1. Check if size matches existing bucket
2. Try to get buffer from free list
3. If free list empty:
   a. Check if pool below max size
   b. If yes: Create new buffer
   c. If no: Evict LRU buffer
4. Track buffer as in-use
5. Return buffer to caller
```

## Execution Engine

### Stream and Event Management

**Architecture**:
```
OpenCLStreamManager (Phase 1)
    │
    ├─► OpenCLStreamPool
    │       │
    │       └─► Pool of cl_command_queue handles
    │
    └─► Stream allocation and reuse

OpenCLEventManager (Phase 1)
    │
    ├─► OpenCLEventPool
    │       │
    │       └─► Pool of cl_event handles
    │
    └─► Event allocation and profiling
```

**Stream (Command Queue) Pooling**:
```
Pool Configuration:
- MinimumQueuePoolSize: 2 (default)
- MaximumQueuePoolSize: 8 (default)
- EnableOutOfOrderExecution: false (default)

Acquire Flow:
1. TryGetFromPool()
2. If pool empty and < max: CreateNewQueue()
3. If at max: Wait or create temporary
4. Return queue handle

Release Flow:
1. Finish queue operations (clFinish)
2. Reset queue state
3. Return to pool
```

**Event Pooling**:
```
Pool Configuration:
- MinimumEventPoolSize: 10 (default)
- MaximumEventPoolSize: 100 (default)
- EnableProfiling: false (default)

Event Lifecycle:
1. Acquire from pool
2. Attach to operation (clEnqueueXXX)
3. Wait for completion (clWaitForEvents)
4. Query profiling data (if enabled)
5. Release back to pool
```

### Command Graph & Pipeline Execution

**File**: `Execution/OpenCLCommandGraph.cs` (Phase 2 Week 2)

DAG-based execution planning for complex workflows.

**Graph Structure**:
```
CommandNode
    ├─► NodeType: Kernel | MemoryCopy | Synchronization
    ├─► Dependencies: List<CommandNode>
    ├─► cl_event: Completion event
    └─► Status: Pending | Running | Completed

CommandGraph
    ├─► Nodes: List<CommandNode>
    ├─► Edges: Dependencies
    └─► ExecutionPlan: Topologically sorted nodes
```

**Execution Strategy**:
```
1. Build Graph:
   AddKernelNode(kernel, args)
   AddMemoryCopyNode(src, dst)
   AddDependency(nodeA, nodeB)

2. Optimize:
   - Topological sort
   - Identify independent paths
   - Batch memory operations

3. Execute:
   foreach (node in topological_order)
   {
       var waitEvents = node.Dependencies.Select(d => d.Event);
       await ExecuteNodeAsync(node, waitEvents);
   }
```

## Resource Management

### Resource Lifecycle

All OpenCL resources follow a consistent lifecycle pattern:

```
Create → Track → Use → Release → Untrack
```

**Tracked Resources**:
1. `cl_context` - OpenCL context
2. `cl_command_queue` - Command queues (pooled)
3. `cl_program` - Compiled programs
4. `cl_kernel` - Kernel instances
5. `cl_mem` - Memory buffers (pooled)
6. `cl_event` - Events (pooled)

### OpenCLResourceManager

**File**: `Infrastructure/OpenCLResourceManager.cs` (Phase 1)

Centralized resource tracking and cleanup.

**Features**:
- Reference counting for shared resources
- Automatic cleanup on dispose
- Leak detection in debug builds
- Resource usage statistics

## Performance Optimization

### Vendor-Specific Optimizations

**File**: `Vendor/VendorAdapterFactory.cs`

Automatic vendor detection and optimization application.

**Supported Vendors**:

#### NVIDIA
```csharp
NvidiaOpenCLAdapter:
- Compiler flags: -cl-nv-verbose, -cl-nv-maxrregcount
- Optimal work group size: 256 (multiple of warp size 32)
- Memory coalescing guidance
- Shared memory optimization
```

#### AMD
```csharp
AmdOpenCLAdapter:
- Compiler flags: -cl-amd-media-ops
- Optimal work group size: 64 (wavefront size)
- LDS (Local Data Share) optimization
- Vector width selection
```

#### Intel
```csharp
IntelOpenCLAdapter:
- Compiler flags: -cl-intel-greater-than-4GB-buffer-required
- Optimal work group size: based on EU count
- Sub-group operations
- Work group size recommendations
```

### Profiling Integration

**File**: `Profiling/OpenCLProfiler.cs` (Phase 1)

Hardware-based performance profiling.

**Metrics Collected**:
```
ProfilingStatistics:
├─► ExecutionTime (wall clock)
├─► KernelTime (device execution)
├─► MemoryTransferTime (host ↔ device)
├─► QueuedTime (waiting in queue)
└─► SubmitTime (submission overhead)
```

**Usage Example**:
```csharp
var session = profiler.BeginSession("MatrixMultiply");

// Execute operations
await CopyToDeviceAsync(...);
await kernel.ExecuteAsync(...);
await CopyFromDeviceAsync(...);

var stats = profiler.EndSession(session);
Console.WriteLine($"Total: {stats.ExecutionTime}");
Console.WriteLine($"Kernel: {stats.KernelTime}");
Console.WriteLine($"Memory: {stats.MemoryTransferTime}");
```

## Thread Safety

### Concurrency Model

**Thread-Safe Components**:
- `OpenCLAccelerator`: Thread-safe via internal locking
- `OpenCLMemoryManager`: Thread-safe via concurrent collections
- `OpenCLCompilationCache`: Thread-safe via semaphore
- `OpenCLStreamManager`: Thread-safe pool management
- `OpenCLEventManager`: Thread-safe pool management

**Lock Granularity**:
```
Coarse-grained locks:
- Context creation (initialization)
- Resource disposal

Fine-grained locks:
- Pool access (stream/event/memory)
- Cache access (compilation)
- Statistics updates (atomic operations)
```

**Async Patterns**:
```csharp
// Correct async pattern
public async ValueTask<T> OperationAsync(CancellationToken ct)
{
    await EnsureInitializedAsync(ct);

    return await Task.Run(() =>
    {
        // Potentially blocking OpenCL call
        var result = OpenCLRuntime.clEnqueueXXX(...);
        return ProcessResult(result);
    }, ct);
}
```

## Error Handling

### Exception Hierarchy

```
OpenCLException (base)
    ├─► CompilationException
    │       ├─► BuildLog property
    │       ├─► SourceCode property
    │       └─► Helpful suggestions
    │
    ├─► DeviceException
    │       └─► Device-specific errors
    │
    └─► MemoryException
            └─► Out of memory, invalid allocation
```

### Error Codes

OpenCL error codes are mapped to exceptions with helpful messages:

```csharp
CL_SUCCESS                    → No exception
CL_DEVICE_NOT_FOUND           → DeviceException
CL_INVALID_VALUE              → ArgumentException
CL_OUT_OF_RESOURCES           → MemoryException
CL_OUT_OF_HOST_MEMORY         → OutOfMemoryException
CL_BUILD_PROGRAM_FAILURE      → CompilationException
```

## Integration Points

### With Core Framework

```
DotCompute.Core
    └─► IAccelerator
            ▲
            │ implements
            │
    OpenCLAccelerator
```

### With Runtime Services

```
IComputeOrchestrator
    └─► Discovers OpenCLAccelerator via plugin
        └─► Backend selection
            └─► Kernel execution routing
```

### With Memory System

```
IUnifiedMemoryManager
    ▲
    │ implements
    │
OpenCLMemoryManager
    └─► Creates OpenCLMemoryBuffer<T>
            └─► Wraps cl_mem handles
```

## Future Enhancements

Phase 2 Week 3 and beyond:

1. **Advanced Features**:
   - Pipes and SPIR-V support
   - Sub-groups and work group collectives
   - Device-side enqueue

2. **Performance**:
   - Asynchronous compilation
   - Kernel specialization
   - Auto-tuning system

3. **Interoperability**:
   - OpenGL sharing
   - DirectX sharing
   - Vulkan integration

## References

- [OpenCL Specification 3.0](https://www.khronos.org/registry/OpenCL/)
- [OpenCL Programming Guide](https://www.khronos.org/files/opencl-quick-reference-card.pdf)
- [Vendor Optimization Guides](https://github.com/KhronosGroup/OpenCL-Docs)
