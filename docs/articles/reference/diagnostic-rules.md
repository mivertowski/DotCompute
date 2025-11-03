# Diagnostic Rules Reference

Complete reference for DotCompute analyzer diagnostics (DC001-DC012) with automated code fixes.

## Overview

DotCompute includes 12 Roslyn analyzers that provide real-time feedback in Visual Studio and VS Code. All diagnostics have automated code fixes available via **Ctrl+.** (Quick Actions).

**Analyzer Categories**:
- **Kernel Definition** (DC001-DC003): Correct kernel signatures
- **Parameter Types** (DC004-DC005): Proper parameter usage
- **Runtime Safety** (DC006-DC008): Prevent runtime errors
- **Performance** (DC009-DC011): Optimization opportunities
- **Documentation** (DC012): API documentation

## Kernel Definition Rules

### DC001: Method Should Be Kernel

**Severity**: Warning
**Category**: Design

**Description**: Method performs compute operations and should be marked with `[Kernel]` attribute.

**Detected Patterns**:
- Method named with "Kernel" suffix
- Thread indexing (`threadIdx`, `blockIdx` patterns)
- SIMD-friendly operations
- Array processing in loops

**Example**:
```csharp
// ❌ Missing [Kernel] attribute
public static void ProcessDataKernel(Span<float> data)
{
    int idx = threadIdx.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;
    }
}
```

**Fix**: Add `[Kernel]` attribute
```csharp
// ✅ Correct
[Kernel]
public static void ProcessDataKernel(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;
    }
}
```

**Rationale**: Ensures compute-intensive methods are optimized for GPU execution.

---

### DC002: Kernel Must Return Void

**Severity**: Error
**Category**: Design

**Description**: Kernel methods must return `void`. GPU kernels cannot return values directly.

**Example**:
```csharp
// ❌ Returns float
[Kernel]
public static float SumArray(ReadOnlySpan<float> data)
{
    return data[0];  // Error!
}
```

**Fix**: Use output parameter
```csharp
// ✅ Correct
[Kernel]
public static void SumArray(ReadOnlySpan<float> data, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        Kernel.AtomicAdd(ref result[0], data[idx]);
    }
}
```

**Rationale**: GPU execution model doesn't support return values. Use output parameters instead.

---

### DC003: Kernel Must Be Static

**Severity**: Error
**Category**: Design

**Description**: Kernel methods must be `static`. Instance methods cannot be compiled to GPU code.

**Example**:
```csharp
// ❌ Instance method
[Kernel]
public void ProcessData(Span<float> data)
{
    // Error: Cannot access 'this' in kernel
}
```

**Fix**: Make method static
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;
    }
}
```

**Rationale**: Kernels execute on GPU without access to host object instances.

---

## Parameter Type Rules

### DC004: Unsupported Parameter Type

**Severity**: Error
**Category**: Usage

**Description**: Kernel parameter type not supported on GPU.

**Supported Types**:
- `Span<T>`, `ReadOnlySpan<T>` (T = primitive type)
- Primitive types: `int`, `float`, `double`, `long`, `bool`, `byte`, etc.
- Structs (value types only)

**Unsupported Types**:
- `string`, `object`, `dynamic`
- Reference types (classes)
- Delegates, `Action`, `Func`
- Nullable reference types

**Example**:
```csharp
// ❌ Unsupported types
[Kernel]
public static void ProcessData(
    string text,        // Error: string not supported
    List<float> data,   // Error: List<T> not supported
    Action callback)    // Error: delegate not supported
{
}
```

**Fix**: Use supported types
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(
    ReadOnlySpan<byte> text,   // byte array for string data
    ReadOnlySpan<float> data,  // Span instead of List
    int callbackMode)          // int instead of delegate
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        // Process...
    }
}
```

**Rationale**: GPU has limited type support. Use primitive types and spans.

---

### DC005: Write to ReadOnlySpan

**Severity**: Error
**Category**: Usage

**Description**: Attempt to write to `ReadOnlySpan<T>` parameter.

**Example**:
```csharp
// ❌ Writing to readonly parameter
[Kernel]
public static void ProcessData(ReadOnlySpan<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] = 0;  // Error: data is readonly
    }
}
```

**Fix**: Change to `Span<T>`
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] = 0;  // OK: data is writable
    }
}
```

**Rationale**: `ReadOnlySpan<T>` prevents accidental data corruption. Use `Span<T>` for outputs.

---

## Runtime Safety Rules

### DC006: Missing Bounds Check

**Severity**: Warning
**Category**: Reliability

**Description**: Array/span access without bounds checking. May cause out-of-range errors.

**Example**:
```csharp
// ❌ No bounds check
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    data[idx] = 0;  // Warning: may be out of range
}
```

**Fix**: Add bounds check
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)  // Bounds check
    {
        data[idx] = 0;
    }
}
```

**Rationale**: Thread count may exceed array size. Always validate indices.

---

### DC007: Non-Linear Array Access

**Severity**: Warning
**Category**: Performance

**Description**: Complex array indexing pattern may cause poor performance on GPU.

**Problematic Patterns**:
- Indirect indexing: `data[indices[idx]]`
- Complex expressions: `data[idx * stride + offset * 2]`
- Pointer arithmetic

**Example**:
```csharp
// ⚠️ Non-linear access
[Kernel]
public static void ScatterData(
    ReadOnlySpan<float> input,
    ReadOnlySpan<int> indices,
    Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < input.Length)
    {
        output[indices[idx]] = input[idx];  // Warning: indirect indexing
    }
}
```

**Note**: This is a warning, not an error. Pattern is valid but may be slow.

**Optimization**: Use tiling or reorganize data layout if possible.

**Rationale**: Linear access patterns enable memory coalescing on GPU for better performance.

---

### DC008: Async in Kernel

**Severity**: Error
**Category**: Usage

**Description**: `async`/`await` not supported in kernel code.

**Example**:
```csharp
// ❌ Async kernel
[Kernel]
public static async Task ProcessData(Span<float> data)
{
    await Task.Delay(100);  // Error: async not supported
}
```

**Fix**: Remove async
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;
    }
}
```

**Rationale**: GPU kernels execute synchronously. Use host-side async for orchestration.

---

## Performance Rules

### DC009: Thread ID Not Used

**Severity**: Warning
**Category**: Performance

**Description**: Kernel doesn't use thread indexing. Will only execute on single thread.

**Example**:
```csharp
// ⚠️ No thread indexing
[Kernel]
public static void ProcessData(Span<float> data)
{
    for (int i = 0; i < data.Length; i++)
    {
        data[i] *= 2.0f;  // Sequential loop
    }
}
```

**Fix**: Use thread indexing
```csharp
// ✅ Correct
[Kernel]
public static void ProcessData(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;  // Parallel access
    }
}
```

**Rationale**: Kernels should exploit parallelism. Use `Kernel.ThreadId` for parallel execution.

---

### DC010: Incorrect Threading Pattern

**Severity**: Warning
**Category**: Performance

**Description**: Thread indexing pattern may not utilize GPU effectively.

**Common Issues**:
- Using only `ThreadId.X` for 2D/3D data
- Not accounting for block dimensions
- Grid-stride loops without proper stride

**Example**:
```csharp
// ⚠️ Suboptimal pattern for 2D data
[Kernel]
public static void ProcessImage(Span<byte> pixels, int width, int height)
{
    int idx = Kernel.ThreadId.X;  // 1D indexing for 2D data
    if (idx < pixels.Length)
    {
        pixels[idx] = 0;
    }
}
```

**Fix**: Use appropriate dimensions
```csharp
// ✅ Correct for 2D data
[Kernel]
public static void ProcessImage(Span<byte> pixels, int width, int height)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;

    if (x < width && y < height)
    {
        int idx = y * width + x;
        pixels[idx] = 0;
    }
}
```

**Rationale**: Matching thread dimensions to data dimensions improves memory access patterns.

---

### DC011: Performance Opportunity

**Severity**: Info
**Category**: Performance

**Description**: Code pattern has optimization opportunity.

**Detected Opportunities**:
- Loop fusion: Multiple loops over same data
- Operation fusion: Multiple passes that could be single pass
- Memory reuse: Intermediate allocations

**Example**:
```csharp
// ℹ️ Can be optimized
[Kernel]
public static void ProcessStep1(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] *= 2.0f;
    }
}

[Kernel]
public static void ProcessStep2(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] += 1.0f;
    }
}
```

**Suggested Optimization**: Fuse kernels
```csharp
// ✅ Fused kernel (better performance)
[Kernel]
public static void ProcessFused(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx < data.Length)
    {
        data[idx] = data[idx] * 2.0f + 1.0f;  // Single pass
    }
}
```

**Rationale**: Fewer kernel launches and memory transfers improve performance.

---

## Documentation Rule

### DC012: Missing XML Documentation

**Severity**: Warning (configurable)
**Category**: Documentation

**Description**: Public kernel missing XML documentation.

**Example**:
```csharp
// ⚠️ Missing documentation
[Kernel]
public static void ProcessData(ReadOnlySpan<float> input, Span<float> output)
{
    // ...
}
```

**Fix**: Add XML documentation
```csharp
// ✅ Documented
/// <summary>
/// Processes input data and stores results in output.
/// </summary>
/// <param name="input">Input data to process.</param>
/// <param name="output">Output buffer for results.</param>
[Kernel]
public static void ProcessData(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2.0f;
    }
}
```

**Rationale**: Public APIs should be documented for maintainability.

---

## Configuration

### Severity Levels

Configure in `.editorconfig`:

```ini
# Kernel Definition
dotnet_diagnostic.DC001.severity = warning
dotnet_diagnostic.DC002.severity = error
dotnet_diagnostic.DC003.severity = error

# Parameter Types
dotnet_diagnostic.DC004.severity = error
dotnet_diagnostic.DC005.severity = error

# Runtime Safety
dotnet_diagnostic.DC006.severity = warning
dotnet_diagnostic.DC007.severity = suggestion
dotnet_diagnostic.DC008.severity = error

# Performance
dotnet_diagnostic.DC009.severity = warning
dotnet_diagnostic.DC010.severity = suggestion
dotnet_diagnostic.DC011.severity = suggestion

# Documentation
dotnet_diagnostic.DC012.severity = warning
```

### Suppress Diagnostics

**Suppress specific diagnostic**:
```csharp
#pragma warning disable DC007  // Non-linear access is intentional
[Kernel]
public static void ScatterKernel(...)
{
    // ...
}
#pragma warning restore DC007
```

**Suppress in project**:
```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);DC007;DC011</NoWarn>
</PropertyGroup>
```

## IDE Integration

### Visual Studio

1. Diagnostics appear in **Error List** (View → Error List)
2. Squiggles in code editor
3. Quick Actions (Ctrl+.) for automated fixes
4. Light bulb icon 💡 indicates fix available

### VS Code

1. Install C# Dev Kit extension
2. Diagnostics in **Problems** panel
3. Quick Fix (Ctrl+.) for automated fixes
4. Diagnostic codes appear in hover tooltips

### Batch Fixes

Apply all fixes in file/project:
1. Right-click in editor
2. **Quick Actions and Refactorings**
3. **Apply all code fixes in [File/Project]**

## Related Documentation

- [Kernel Development Guide](../guides/kernel-development.md) - Writing efficient kernels
- [Source Generators Architecture](../architecture/source-generators.md) - Analyzer design
- [Troubleshooting Guide](../guides/troubleshooting.md) - Resolving analyzer warnings

---

**Diagnostics • Code Quality • Real-Time Feedback • Production Ready**
