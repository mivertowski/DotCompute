# C# to Metal Shading Language Translation Reference

## Overview

This document describes the C# to Metal Shading Language (MSL) translation pipeline implemented in `MetalKernelCompiler.TranslateFromCSharpAsync()`. The translation enables [Kernel] attribute code to run on Apple Silicon GPUs.

## Translation Mappings

### Threading Model

| C# Construct | Metal Equivalent | Description |
|-------------|------------------|-------------|
| `Kernel.ThreadId.X` | `thread_position_in_grid.x` | Global thread X position |
| `Kernel.ThreadId.Y` | `thread_position_in_grid.y` | Global thread Y position |
| `Kernel.ThreadId.Z` | `thread_position_in_grid.z` | Global thread Z position |
| `ThreadId.X` | `thread_position_in_grid.x` | Shorthand (without Kernel prefix) |

### Parameter Types

| C# Type | Metal Type | Access Mode |
|---------|-----------|-------------|
| `ReadOnlySpan<float>` | `const device float*` | Read-only buffer |
| `Span<float>` | `device float*` | Read-write buffer |
| `float[]` | `device float*` | Read-write buffer |
| `int`, `uint` | `int`, `uint` | Scalar parameters |

### Primitive Type Mappings

| C# Type | Metal Type |
|---------|-----------|
| `float` | `float` |
| `double` | `double` |
| `int` | `int` |
| `uint` | `uint` |
| `short` | `short` |
| `ushort` | `ushort` |
| `byte` | `uchar` |
| `sbyte` | `char` |
| `long` | `long` |
| `ulong` | `ulong` |
| `bool` | `bool` |
| `half` | `half` (Metal-specific) |

### Math Functions

| C# Function | Metal Equivalent |
|------------|-----------------|
| `Math.Sqrt(x)` | `metal::sqrt(x)` |
| `Math.Abs(x)` | `metal::abs(x)` |
| `Math.Sin(x)` | `metal::sin(x)` |
| `Math.Cos(x)` | `metal::cos(x)` |
| `Math.Tan(x)` | `metal::tan(x)` |
| `Math.Exp(x)` | `metal::exp(x)` |
| `Math.Log(x)` | `metal::log(x)` |
| `Math.Pow(x, y)` | `metal::pow(x, y)` |
| `Math.Floor(x)` | `metal::floor(x)` |
| `Math.Ceil(x)` | `metal::ceil(x)` |
| `Math.Round(x)` | `metal::round(x)` |
| `Math.Min(x, y)` | `metal::min(x, y)` |
| `Math.Max(x, y)` | `metal::max(x, y)` |
| `MathF.*` | Same as above (Metal auto-handles precision) |

### Atomic Operations

| C# Interlocked | Metal Atomic | Memory Order |
|---------------|-------------|--------------|
| `Interlocked.Add(ref x, val)` | `atomic_fetch_add_explicit(&x, val, memory_order_relaxed)` | Relaxed |
| `Interlocked.Increment(ref x)` | `atomic_fetch_add_explicit(&x, 1, memory_order_relaxed)` | Relaxed |
| `Interlocked.Decrement(ref x)` | `atomic_fetch_sub_explicit(&x, 1, memory_order_relaxed)` | Relaxed |
| `Interlocked.Exchange(ref x, val)` | `atomic_exchange_explicit(&x, val, memory_order_relaxed)` | Relaxed |
| `Interlocked.CompareExchange(ref x, val, cmp)` | `atomic_compare_exchange_weak_explicit(&x, &cmp, val, memory_order_relaxed, memory_order_relaxed)` | Relaxed |

### Synchronization

| C# Construct | Metal Equivalent |
|-------------|-----------------|
| `Barrier()` | `threadgroup_barrier(mem_flags::mem_device)` |
| `Barrier.Sync()` | `threadgroup_barrier(mem_flags::mem_device)` |

## Translation Algorithm

### 1. Method Signature Parsing

```csharp
// Input C# (from [Kernel] attribute)
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] * 2.0f;
    }
}
```

### 2. Metal Kernel Generation

```metal
#include <metal_stdlib>
#include <metal_compute>
using namespace metal;

kernel void VectorAdd(
    const device float* a [[buffer(0)]],
    device float* result [[buffer(1)]],
    uint3 thread_position_in_grid [[thread_position_in_grid]],
    uint3 threads_per_threadgroup [[threads_per_threadgroup]],
    uint3 threadgroup_position_in_grid [[threadgroup_position_in_grid]])
{
    int idx = thread_position_in_grid.x;
    if (idx < result.Length)
    {
        result[idx] = a[idx] * 2.0f;
    }
}
```

## Implementation Details

### Parameter Translation

1. **Extract parameter list** from C# method signature
2. **Parse each parameter**: Split by commas, extract type and name
3. **Translate type**: Map C# types to Metal types
   - `ReadOnlySpan<T>` → `const device T*`
   - `Span<T>` → `device T*`
   - Arrays → `device T*`
4. **Add buffer attributes**: `[[buffer(N)]]` for each parameter
5. **Inject Metal built-ins**:
   - `thread_position_in_grid` - Global thread ID
   - `threads_per_threadgroup` - Workgroup size
   - `threadgroup_position_in_grid` - Workgroup ID

### Body Translation

1. **Line-by-line processing**: Preserve indentation and structure
2. **Replace thread IDs**: `Kernel.ThreadId.X` → `thread_position_in_grid.x`
3. **Replace math functions**: `Math.Sqrt(` → `metal::sqrt(`
4. **Replace atomics**: `Interlocked.Add(` → `atomic_fetch_add_explicit(`
5. **Replace barriers**: `Barrier()` → `threadgroup_barrier(mem_flags::mem_device)`

### Error Handling

Translation failures provide detailed error messages:
- **Missing method declaration**: No `public static void` found
- **Invalid parameters**: Unsupported parameter types
- **Malformed code**: Unbalanced braces/parentheses
- **Unsupported constructs**: Features not supported in MSL

## Validation & Testing

### Valid C# Input Requirements

1. **Static method** marked with `[Kernel]` attribute
2. **Supported parameter types**: Span, ReadOnlySpan, primitives
3. **Thread ID usage**: Must use `Kernel.ThreadId.X/Y/Z` or `ThreadId.X/Y/Z`
4. **Bounds checking**: Must validate array accesses
5. **No dynamic allocations**: Metal doesn't support malloc/new

### Translation Validation

The compiler performs these checks:
1. **Syntax validation**: Valid C# method structure
2. **Type validation**: All types can be mapped to Metal
3. **Thread model validation**: Proper use of thread IDs
4. **Bounds check detection**: Array accesses are guarded

### Example Test Cases

#### Simple Vector Operation
```csharp
[Kernel]
public static void ScaleVector(ReadOnlySpan<float> input, Span<float> output, float scale)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * scale;
    }
}
```

#### 2D Matrix Operation
```csharp
[Kernel]
public static void MatrixAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b,
                             Span<float> result, int width)
{
    int row = Kernel.ThreadId.Y;
    int col = Kernel.ThreadId.X;
    int idx = row * width + col;

    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

#### Atomic Reduction
```csharp
[Kernel]
public static void AtomicSum(ReadOnlySpan<float> input, Span<int> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < input.Length)
    {
        int value = (int)input[idx];
        Interlocked.Add(ref result[0], value);
    }
}
```

## Limitations & Edge Cases

### Known Limitations

1. **No closures**: Lambdas and anonymous functions not supported
2. **No generics**: Metal doesn't support C# generics directly
3. **No reflection**: Runtime type information unavailable
4. **No exceptions**: No try/catch in Metal kernels
5. **No recursion**: Metal kernel functions cannot recurse
6. **Limited library support**: Only math functions mapped

### Edge Cases

1. **Multi-dimensional arrays**: Require manual linearization
2. **String operations**: Not supported in Metal
3. **LINQ expressions**: Not supported in kernels
4. **Virtual calls**: No polymorphism in Metal
5. **Boxing/unboxing**: Not applicable in Metal
6. **Nullable types**: Metal doesn't have null concept

### Workarounds

- **Complex types**: Pass as multiple primitive parameters or linearized arrays
- **Conditionals on threadgroup**: Use Metal's `simd_any/simd_all` for warp-level operations
- **Shared memory**: Declare `threadgroup` memory explicitly in Metal version

## Performance Considerations

### Optimization Hints

1. **Memory access patterns**: Coalesce reads/writes for best performance
2. **Thread divergence**: Minimize branching within warps
3. **Shared memory**: Use threadgroup memory for data reuse
4. **Atomic contention**: Minimize atomic operations on same location
5. **Register pressure**: Keep local variables minimal

### Compilation Options

The translator generates optimized Metal code based on:
- **Fast math**: Enabled for aggressive optimizations
- **Metal version**: Uses latest Metal 3.1 features when available
- **Precision**: Prefers half precision where appropriate

## Future Enhancements

1. **Roslyn-based parsing**: Use full C# syntax tree for accurate translation
2. **SIMD intrinsics**: Map C# Vector&lt;T&gt; to Metal SIMD types
3. **Threadgroup memory**: Auto-detect shared memory patterns
4. **Advanced atomics**: Support Metal 3.0+ atomic operations
5. **Texture support**: Map C# image types to Metal textures
6. **Compute capability detection**: Adapt to device capabilities

## References

- **Metal Shading Language Specification**: https://developer.apple.com/metal/Metal-Shading-Language-Specification.pdf
- **CUDA to Metal Translation**: Similar patterns to CUDA backend in `CudaKernelCompiler.cs`
- **DotCompute Architecture**: See `/docs/ARCHITECTURE.md` for system overview
