# [Kernel] Attribute Reference

Complete reference for the `[Kernel]` attribute and kernel programming model in DotCompute.

## Overview

The `[Kernel]` attribute marks C# methods for GPU compilation. DotCompute's source generator automatically translates these methods to backend-specific code (CUDA C, Metal Shading Language, OpenCL C, or SIMD-optimized C#).

## Basic Syntax

```csharp
[Kernel]
public static void KernelName(
    ReadOnlySpan<T> input,
    Span<T> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = Process(input[idx]);
    }
}
```

## Kernel Requirements

### Method Signature

✅ **Valid**:
```csharp
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, Span<float> b)
{
    // GPU-compatible code
}
```

❌ **Invalid**:
```csharp
[Kernel]
public void InstanceMethod() { } // Must be static

[Kernel]
public static int ReturnValue() { } // Must return void

[Kernel]
public static async Task AsyncMethod() { } // Cannot be async
```

### Parameter Types

**Supported**:
- `Span<T>` - Writable buffer
- `ReadOnlySpan<T>` - Read-only buffer
- Primitive types: `int`, `float`, `double`, `long`, `bool`
- Value types (structs with primitive fields)

**Not Supported**:
- Reference types (classes, strings)
- Delegates, lambda expressions
- `out` or `ref` parameters
- Pointers (except in CUDA-specific code)

## Thread Model

### Thread IDs

DotCompute provides a unified threading model across all backends:

```csharp
[Kernel]
public static void Process2DData(Span<float> data, int width, int height)
{
    int x = Kernel.ThreadId.X; // Column index
    int y = Kernel.ThreadId.Y; // Row index
    int z = Kernel.ThreadId.Z; // Depth index (for 3D)

    if (x < width && y < height)
    {
        int index = y * width + x;
        data[index] = x + y;
    }
}
```

### Grid and Block Dimensions

```csharp
// Thread counts
int gridSizeX = Kernel.GridDim.X;    // Number of thread blocks in X
int gridSizeY = Kernel.GridDim.Y;    // Number of thread blocks in Y
int blockSizeX = Kernel.BlockDim.X;  // Threads per block in X
int blockSizeY = Kernel.BlockDim.Y;  // Threads per block in Y

// Block indices
int blockX = Kernel.BlockId.X;
int blockY = Kernel.BlockId.Y;

// Global thread index
int globalX = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadIdx.X;
int globalY = Kernel.BlockId.Y * Kernel.BlockDim.Y + Kernel.ThreadIdx.Y;
```

## Memory Access Patterns

### Coalesced Memory Access (Optimal)

```csharp
[Kernel]
public static void CoalescedAccess(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    // Sequential access - optimal for GPU
    output[idx] = input[idx] * 2.0f;
}
```

### Strided Access (Suboptimal)

```csharp
[Kernel]
public static void StridedAccess(ReadOnlySpan<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    // Strided access - may cause bank conflicts
    output[idx * stride] = input[idx * stride];
}
```

### Shared Memory (CUDA/Metal)

```csharp
[Kernel]
public static void SharedMemoryExample(
    ReadOnlySpan<float> input,
    Span<float> output)
{
    // Declare shared memory
    var shared = Kernel.AllocateShared<float>(256);

    int tid = Kernel.ThreadIdx.X;
    int bid = Kernel.BlockId.X;
    int idx = bid * Kernel.BlockDim.X + tid;

    // Load to shared memory
    shared[tid] = input[idx];

    // Synchronize threads in block
    Kernel.Barrier();

    // Use shared data
    output[idx] = shared[tid] + shared[(tid + 1) % 256];
}
```

## Synchronization and Memory Ordering

### Barrier Configuration

Control thread synchronization behavior via attribute properties:

```csharp
[Kernel(
    UseBarriers = true,                      // Enable barriers
    BarrierScope = BarrierScope.ThreadBlock, // Synchronization scope
    BarrierCapacity = 256)]                  // Expected thread count
public static void WithBarriers(Span<float> data)
{
    var shared = Kernel.AllocateShared<float>(256);
    int tid = Kernel.ThreadIdx.X;

    shared[tid] = data[tid];
    Kernel.Barrier();  // Synchronize all threads

    data[tid] = shared[tid] + shared[(tid + 1) % 256];
}
```

**Barrier Properties**:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseBarriers` | bool | `false` | Enable barrier synchronization |
| `BarrierScope` | enum | `ThreadBlock` | Synchronization scope (ThreadBlock, Warp, Grid, etc.) |
| `BarrierCapacity` | int | `0` (auto) | Expected number of threads (0 = automatic) |

### Barrier Scopes

```csharp
public enum BarrierScope
{
    ThreadBlock = 0,  // All threads in block (~10-20ns)
    Warp = 2,         // 32-thread warp (~1-5ns)
    Grid = 1,         // All blocks (CUDA only, ~1-10μs)
    Tile = 3,         // Arbitrary subset (~20ns)
    System = 4        // Multi-GPU + CPU (~1-10ms)
}
```

**Examples**:

```csharp
// ThreadBlock barrier (most common)
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.ThreadBlock)]
public static void BlockSync(Span<float> data)
{
    Kernel.Barrier();  // Sync all threads in block
}

// Warp barrier (fine-grained)
[Kernel(UseBarriers = true, BarrierScope = BarrierScope.Warp)]
public static void WarpSync(Span<float> data)
{
    Kernel.Barrier();  // Sync 32-thread warp only
}

// Grid barrier (CUDA only, requires cooperative launch)
[Kernel(
    Backends = KernelBackends.CUDA,  // CUDA only!
    UseBarriers = true,
    BarrierScope = BarrierScope.Grid)]
public static void GridSync(Span<float> data)
{
    Kernel.Barrier();  // Sync all blocks (NOT supported on Metal)
}
```

**Backend Support**:
- **CUDA**: Full support (ThreadBlock, Warp, Grid with cooperative launch)
- **Metal**: ThreadBlock and Warp only (no Grid barriers)
- **OpenCL**: ThreadBlock barriers
- **CPU**: Emulated via threading primitives

### Memory Consistency Models

Control memory operation ordering and visibility:

```csharp
[Kernel(
    MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire,
    EnableCausalOrdering = true)]
public static void SafeMessaging(Span<int> data, Span<int> flags)
{
    int tid = Kernel.ThreadId.X;

    // Write data, then set flag (release)
    data[tid] = ComputeValue(tid);
    flags[tid] = READY;

    // Wait for neighbor's flag, then read data (acquire)
    int neighbor = (tid + 1) % Kernel.BlockDim.X;
    while (flags[neighbor] != READY) { }
    int value = data[neighbor];  // Guaranteed to see write
}
```

**Memory Consistency Properties**:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MemoryConsistency` | enum | `Relaxed` | Memory consistency model |
| `EnableCausalOrdering` | bool | `false` | Enable release-acquire semantics |

**Consistency Models**:

```csharp
public enum MemoryConsistencyModel
{
    Relaxed = 0,         // No ordering (1.0× performance, GPU default)
    ReleaseAcquire = 1,  // Causal ordering (0.85× performance)
    Sequential = 2       // Total order (0.60× performance)
}
```

**When to Use Each Model**:

| Model | Performance | Use Case |
|-------|------------|----------|
| **Relaxed** | 1.0× (fastest) | Data-parallel, no inter-thread communication |
| **ReleaseAcquire** | 0.85× (15% overhead) | Message passing, producer-consumer (recommended) |
| **Sequential** | 0.60× (40% overhead) | Debugging race conditions only |

**Examples**:

```csharp
// Relaxed: Data-parallel (default)
[Kernel(MemoryConsistency = MemoryConsistencyModel.Relaxed)]
public static void DataParallel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    data[idx] = MathF.Sqrt(data[idx]);  // No inter-thread communication
}

// Release-Acquire: Producer-consumer
[Kernel(MemoryConsistency = MemoryConsistencyModel.ReleaseAcquire)]
public static void ProducerConsumer(Span<int> data, Span<int> flags)
{
    // Proper causality for message passing
}

// Sequential: Debugging only (40% slower!)
[Kernel(MemoryConsistency = MemoryConsistencyModel.Sequential)]
public static void DebugRaces(Span<int> data)
{
    // Use to detect race conditions, then fix and switch back
}

// Convenience: EnableCausalOrdering = true → ReleaseAcquire
[Kernel(EnableCausalOrdering = true)]  // Shorthand
public static void SafeMessaging(Span<int> data)
{
    // Automatically uses ReleaseAcquire
}
```

**See Also**: [Barriers and Memory Ordering Guide](../advanced/barriers-and-memory-ordering.md) for comprehensive coverage.

### Thread Barrier (Legacy API)

The `Kernel.Barrier()` method provides runtime barrier synchronization:

```csharp
Kernel.Barrier(); // Synchronize all threads in configured scope
```

**Use Cases**:
- After writing to shared memory
- Before reading data written by other threads
- Coordinating multi-phase algorithms

**Important**: Barrier scope is determined by the `BarrierScope` attribute property.

## Atomic Operations

For thread-safe updates to shared data:

```csharp
[Kernel]
public static void HistogramAtomic(ReadOnlySpan<int> data, Span<int> histogram)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        int bin = data[idx];
        Kernel.AtomicAdd(ref histogram[bin], 1);
    }
}
```

**Supported Atomic Operations**:
- `Kernel.AtomicAdd(ref target, value)`
- `Kernel.AtomicSub(ref target, value)`
- `Kernel.AtomicMin(ref target, value)`
- `Kernel.AtomicMax(ref target, value)`
- `Kernel.AtomicExchange(ref target, value)`
- `Kernel.AtomicCompareExchange(ref target, compare, value)`

## Math Functions

### Built-in Math

DotCompute translates C# math to GPU-optimized intrinsics:

```csharp
[Kernel]
public static void MathOperations(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    float x = input[idx];

    // Trigonometric
    output[idx] = MathF.Sin(x);       // → sinf() on CUDA
    output[idx] = MathF.Cos(x);       // → cosf() on CUDA
    output[idx] = MathF.Tan(x);       // → tanf() on CUDA

    // Power and exponential
    output[idx] = MathF.Sqrt(x);      // → sqrtf() on CUDA
    output[idx] = MathF.Pow(x, 2);    // → powf() on CUDA
    output[idx] = MathF.Exp(x);       // → expf() on CUDA
    output[idx] = MathF.Log(x);       // → logf() on CUDA

    // Min/Max
    output[idx] = MathF.Min(x, 1.0f); // → fminf() on CUDA
    output[idx] = MathF.Max(x, 0.0f); // → fmaxf() on CUDA

    // Absolute value
    output[idx] = MathF.Abs(x);       // → fabsf() on CUDA
}
```

## Control Flow

### Supported Constructs

```csharp
[Kernel]
public static void ControlFlow(Span<int> data)
{
    int idx = Kernel.ThreadId.X;

    // If statements
    if (idx % 2 == 0)
    {
        data[idx] = 0;
    }
    else
    {
        data[idx] = 1;
    }

    // For loops
    for (int i = 0; i < 10; i++)
    {
        data[idx] += i;
    }

    // While loops
    int count = 0;
    while (count < 5)
    {
        data[idx]++;
        count++;
    }

    // Switch statements
    switch (data[idx] % 3)
    {
        case 0: data[idx] = 10; break;
        case 1: data[idx] = 20; break;
        case 2: data[idx] = 30; break;
    }
}
```

### Unsupported Constructs

❌ **Not Allowed**:
- Recursion
- Dynamic memory allocation (`new`, `stackalloc`)
- Exception handling (`try/catch/finally`)
- LINQ queries
- Async/await
- Virtual method calls
- Interface calls

## Optimization Attributes

### Memory Coalescing Hint

```csharp
[Kernel(CoalescedAccess = true)]
public static void OptimizedKernel(ReadOnlySpan<float> data)
{
    // Compiler applies memory coalescing optimizations
}
```

### Register Pressure Control

```csharp
[Kernel(MaxRegisters = 32)]
public static void RegisterOptimized(Span<float> data)
{
    // Limits register usage to increase occupancy
}
```

### Shared Memory Size

```csharp
[Kernel(SharedMemoryBytes = 4096)]
public static void SharedMemoryKernel(Span<float> data)
{
    var shared = Kernel.AllocateShared<float>(1024); // 4KB
    // ...
}
```

## Type Conversions

```csharp
[Kernel]
public static void TypeConversions(Span<float> floats, Span<int> ints)
{
    int idx = Kernel.ThreadId.X;

    // Explicit conversions
    floats[idx] = (float)ints[idx];
    ints[idx] = (int)floats[idx];

    // Reinterpret casting (bitwise)
    int bitPattern = Kernel.ReinterpretCast<float, int>(floats[idx]);
}
```

## Debugging Tips

### Bounds Checking

Always include bounds checks:

```csharp
[Kernel]
public static void SafeKernel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length) // Essential bounds check
    {
        data[idx] = ProcessValue(data[idx]);
    }
}
```

### Cross-Backend Validation

```csharp
// Enable debugging to validate GPU results
var debugService = new KernelDebugService();
var results = await debugService.CompareBackendsAsync(
    kernel,
    arguments,
    cpuAccelerator,
    cudaAccelerator
);

if (!results.OutputsMatch)
{
    Console.WriteLine($"GPU bug detected! Max error: {results.MaxAbsoluteDifference}");
}
```

## Performance Best Practices

### 1. Minimize Divergence

✅ **Good**:
```csharp
if (Kernel.ThreadId.X < threshold)  // All threads in warp take same path
```

❌ **Bad**:
```csharp
if (data[Kernel.ThreadId.X] > threshold)  // Threads diverge
```

### 2. Coalesce Memory Access

✅ **Good**:
```csharp
int idx = Kernel.ThreadId.X;
output[idx] = input[idx]; // Sequential, coalesced
```

❌ **Bad**:
```csharp
int idx = Kernel.ThreadId.X;
output[idx] = input[idx * 7]; // Strided, uncoalesced
```

### 3. Use Shared Memory for Repeated Access

✅ **Good**:
```csharp
var shared = Kernel.AllocateShared<float>(256);
shared[Kernel.ThreadIdx.X] = input[globalIdx];
Kernel.Barrier();
// Access shared multiple times
```

❌ **Bad**:
```csharp
// Access global memory repeatedly
for (int i = 0; i < 10; i++)
{
    sum += input[globalIdx];
}
```

### 4. Minimize Atomic Operations

✅ **Better**:
```csharp
// Use local accumulation, then single atomic update
float local = 0;
for (int i = 0; i < 100; i++)
{
    local += data[i];
}
Kernel.AtomicAdd(ref total, local); // One atomic op
```

❌ **Worse**:
```csharp
for (int i = 0; i < 100; i++)
{
    Kernel.AtomicAdd(ref total, data[i]); // 100 atomic ops
}
```

## Backend-Specific Attributes

### CUDA-Specific

```csharp
[Kernel]
[CudaMaxThreadsPerBlock(512)]
[CudaMinBlocksPerMultiprocessor(2)]
public static void CudaOptimized(Span<float> data)
{
    // CUDA-specific optimizations
}
```

### Metal-Specific

```csharp
[Kernel]
[MetalThreadExecutionWidth(32)]
[MetalMaxTotalThreadsPerThreadgroup(1024)]
public static void MetalOptimized(Span<float> data)
{
    // Metal-specific optimizations
}
```

## Advanced: Inline MSL/CUDA

For maximum control, embed native code:

```csharp
[Kernel]
[NativeCode(Backend.CUDA, @"
    __global__ void custom_kernel(float* data) {
        int idx = blockIdx.x * blockDim.x + threadIdx.x;
        data[idx] = __sinf(data[idx]);
    }
")]
public static void CustomKernel(Span<float> data)
{
    // Fallback C# implementation for CPU
    int idx = Kernel.ThreadId.X;
    data[idx] = MathF.Sin(data[idx]);
}
```

## See Also

- **[Barriers and Memory Ordering](../advanced/barriers-and-memory-ordering.md)** - Complete guide to synchronization
- [Performance Guide](../performance/characteristics.md) - Optimization strategies
- [CUDA Programming](../advanced/cuda-programming.md) - CUDA-specific features
- [Metal Shading](../advanced/metal-shading.md) - Metal-specific features
- [Ring Kernels](../architecture/ring-kernels.md) - Message-passing patterns
- [API Reference](../../api/DotCompute.Abstractions.html) - Complete API docs

---

**Next**: Learn about [barriers and memory ordering](../advanced/barriers-and-memory-ordering.md) for advanced synchronization patterns.
