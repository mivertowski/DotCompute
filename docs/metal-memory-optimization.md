# Metal Backend Memory Access Pattern Analysis

## Overview

The Metal backend now includes automatic memory access pattern analysis and optimization detection. The `CSharpToMSLTranslator` analyzes array indexing patterns during C#-to-MSL translation and provides actionable diagnostics for performance optimization.

## Detected Patterns

### 1. Coalesced Access (Optimal)
**Pattern:** `buffer[thread_position_in_grid.x]` or `buffer[Kernel.ThreadId.X]`

**Performance:** ✅ Optimal GPU memory bandwidth utilization

**Example:**
```csharp
[Kernel]
public static void VectorAdd(Span<float> a, Span<float> b, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];  // Coalesced - no warning
    }
}
```

**Diagnostic:** None (optimal pattern)

---

### 2. Strided Access (Suboptimal)
**Pattern:** `buffer[idx * stride]` or `buffer[idx / divisor]`

**Performance:** ⚠️ 15-30% bandwidth reduction for large strides

**Example:**
```csharp
[Kernel]
public static void StridedCopy(Span<float> input, Span<float> output, int stride)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = input[idx * stride];  // Strided access
}
```

**Diagnostic:**
```
Info: Strided memory access detected in 'input[idx * stride]' with stride 'stride'.
For large strides, consider threadgroup staging to improve coalescing.
Expected performance impact: 15-30% bandwidth reduction.
Suggestion: For stride > 16, use threadgroup memory staging
```

**Optimization Strategy:**
```metal
// Stage through threadgroup memory for coalescing
threadgroup float temp[THREADGROUP_SIZE];
temp[tid_in_group] = input[gid * stride];
threadgroup_barrier(mem_flags::mem_threadgroup);
// Now access temp[] with coalesced pattern
```

---

### 3. Scattered Access (Poor)
**Pattern:** `buffer[indices[idx]]` - indirect indexing

**Performance:** ❌ 50-80% bandwidth reduction

**Example:**
```csharp
[Kernel]
public static void GatherData(Span<float> data, Span<int> indices, Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    result[idx] = data[indices[idx]];  // Scattered access
}
```

**Diagnostic:**
```
Warning: Scattered memory access detected in 'data[indices[idx]]'.
Consider using threadgroup staging for better performance.
Scattered access can reduce memory bandwidth by 50-80%.
Suggestion: Use threadgroup memory to coalesce scattered reads
```

**Optimization Strategy:**
```metal
// Batch and sort indices in threadgroup memory
threadgroup int sorted_indices[THREADGROUP_SIZE];
threadgroup float cached_data[THREADGROUP_SIZE];

// Sort indices within threadgroup
sorted_indices[tid_in_group] = indices[gid];
threadgroup_barrier(mem_flags::mem_threadgroup);

// Coalesced batch load
if (tid_in_group < batch_size)
{
    cached_data[tid_in_group] = data[sorted_indices[tid_in_group]];
}
threadgroup_barrier(mem_flags::mem_threadgroup);
```

---

### 4. Sequential Access
**Pattern:** `buffer[i + offset]` - simple linear iteration

**Performance:** ✅ Generally good, compiler-optimizable

**Diagnostic:** None

---

### 5. Unknown/Complex
**Pattern:** Complex expressions not matching other patterns

**Diagnostic:** None (requires manual analysis)

---

## Using the Diagnostics API

### Accessing Diagnostics

```csharp
using DotCompute.Backends.Metal.Translation;
using Microsoft.Extensions.Logging;

var translator = new CSharpToMSLTranslator(logger);
var mslCode = translator.Translate(csharpCode, "MyKernel", "my_kernel");

// Get all diagnostics
var diagnostics = translator.GetDiagnostics();

foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"[{diagnostic.Severity}] {diagnostic.Message}");

    // Access detailed context
    if (diagnostic.Context.TryGetValue("BufferName", out var bufferName))
    {
        Console.WriteLine($"  Buffer: {bufferName}");
    }

    if (diagnostic.Context.TryGetValue("Pattern", out var pattern))
    {
        Console.WriteLine($"  Pattern: {pattern}");
    }

    if (diagnostic.Context.TryGetValue("LineNumber", out var lineNumber))
    {
        Console.WriteLine($"  Line: {lineNumber}");
    }
}
```

### Diagnostic Severity Levels

- **Info:** Performance suggestions (strided access)
- **Warning:** Significant performance issues (scattered access)
- **Error:** Not currently used for memory patterns
- **Critical:** Not currently used for memory patterns

---

## Expected Performance Impact

| Pattern | Bandwidth Efficiency | Performance Impact | Recommendation |
|---------|---------------------|-------------------|----------------|
| Coalesced | 90-100% | None | ✅ Keep as-is |
| Sequential | 80-95% | Minimal | ✅ Generally good |
| Strided (stride ≤ 16) | 70-85% | 15-30% loss | ⚠️ Acceptable |
| Strided (stride > 16) | 40-70% | 30-60% loss | ❌ Use staging |
| Scattered | 20-50% | 50-80% loss | ❌ Requires optimization |

---

## Best Practices

### 1. Design for Coalesced Access
```csharp
// ✅ GOOD: Direct thread ID indexing
int idx = Kernel.ThreadId.X;
output[idx] = input[idx];
```

### 2. Minimize Stride
```csharp
// ❌ BAD: Large stride
output[idx] = input[idx * 1000];

// ✅ BETTER: Restructure data layout
// Use transposed or blocked layout to reduce stride
```

### 3. Batch Scattered Reads
```csharp
// ❌ BAD: Individual scattered reads
for (int i = 0; i < count; i++)
    result[i] = data[indices[i]];

// ✅ BETTER: Batch and sort in threadgroup memory
// Then perform coalesced loads
```

### 4. Use Threadgroup Memory for Reuse
```csharp
// If data is reused across threads, stage in threadgroup memory
// Amortize scattered access cost across threadgroup
```

---

## Integration with Build Pipeline

The analyzer runs automatically during kernel compilation:

1. **Development:** Full diagnostics logged
2. **CI/CD:** Can fail on warning threshold
3. **Production:** Diagnostics collected for monitoring

### Build Configuration

```xml
<PropertyGroup>
  <MetalAnalysisLevel>Warning</MetalAnalysisLevel>
  <TreatMetalWarningsAsErrors>true</TreatMetalWarningsAsErrors>
</PropertyGroup>
```

---

## Testing

Unit tests verify pattern detection:

```csharp
[Fact]
public void Translate_ScatteredAccess_Warning()
{
    var translator = new CSharpToMSLTranslator(logger);
    var msl = translator.Translate(kernelWithScatteredAccess, "Test", "test");
    var diagnostics = translator.GetDiagnostics();

    Assert.Contains(diagnostics, d =>
        d.Severity == SeverityLevel.Warning &&
        d.Message.Contains("Scattered memory access"));
}
```

---

## Future Enhancements

### Planned Features
- [ ] Automatic threadgroup staging generation
- [ ] Kernel fusion for memory access optimization
- [ ] Runtime performance profiling integration
- [ ] ML-based pattern prediction
- [ ] Cross-kernel memory access analysis

### Experimental Features
- Pattern-based kernel transformation
- Automatic data layout optimization
- Dynamic stride detection and adaptation

---

## References

- [Metal Best Practices Guide](https://developer.apple.com/metal/Metal-Best-Practices-Guide.pdf)
- [Memory Performance Guidelines](https://developer.apple.com/documentation/metal/resource_objects/memory_performance_guidelines)
- DotCompute Memory Management Documentation
- Metal Performance Shaders (MPS) Integration

---

**Last Updated:** 2025-10-28
**Version:** 0.2.0
**Component:** `DotCompute.Backends.Metal.Translation.CSharpToMSLTranslator`
