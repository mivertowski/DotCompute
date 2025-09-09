# DotCompute Roslyn Analyzers & Code Fixes

This directory contains production-ready Roslyn analyzers and automated code fixes for DotCompute kernel development. The analyzers provide real-time validation, optimization suggestions, and automated fixes in Visual Studio, VS Code, and other IDEs.

## 📊 Diagnostic Rules

### Critical Error Rules (DC001-DC003)

#### DC001: Kernel methods must be static
**Severity:** Error  
**Fix Available:** ✅ Automatic  
```csharp
// ❌ Wrong
[Kernel]
public void ProcessData(Span<float> data) { }

// ✅ Fixed automatically
[Kernel]
public static void ProcessData(Span<float> data) { }
```

#### DC002: Invalid kernel parameters
**Severity:** Error  
**Fix Available:** ✅ Automatic  
```csharp
// ❌ Wrong
[Kernel]
public static void ProcessData(float[] data, object settings) { }

// ✅ Fixed automatically
[Kernel]
public static void ProcessData(Span<float> data, int settings) { }
```

#### DC003: Unsupported language constructs
**Severity:** Error  
**Fix Available:** ❌ Manual  
```csharp
// ❌ Not supported in kernels
[Kernel]
public static void ProcessData(Span<float> data)
{
    try
    {
        data[0] = 1.0f;
    }
    catch (Exception ex) // ← Exception handling not supported
    {
        // Handle error
    }
}
```

### Performance Optimization Rules (DC004-DC006)

#### DC004: Vectorization opportunities
**Severity:** Info  
**Description:** Suggests SIMD vectorization opportunities  
```csharp
// ℹ️ Can be vectorized
[Kernel]
public static void VectorAdd(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
{
    for (int i = 0; i < result.Length; i++) // ← SIMD opportunity detected
    {
        result[i] = a[i] + b[i];
    }
}
```

#### DC005: Suboptimal memory access patterns
**Severity:** Warning  
**Description:** Detects memory access patterns that may cause cache misses  
```csharp
// ⚠️ Non-coalesced memory access
[Kernel]
public static void ScatteredAccess(Span<float> data)
{
    int index = Kernel.ThreadId.X;
    int scatteredIndex = (index * 7) % data.Length; // ← Non-sequential pattern
    data[scatteredIndex] = data[index] * 2.0f;
}
```

#### DC006: Register spilling risk
**Severity:** Warning  
**Description:** Too many local variables may cause GPU register spilling  
```csharp
// ⚠️ Too many variables (>16 may cause register spilling)
[Kernel]
public static void ManyVariables(Span<float> data)
{
    float v1=1, v2=2, v3=3, v4=4, v5=5, v6=6, v7=7, v8=8;
    float v9=9, v10=10, v11=11, v12=12, v13=13, v14=14, v15=15, v16=16;
    float v17=17, v18=18; // ← Register spilling risk
    // ...
}
```

### Code Quality Rules (DC007-DC009)

#### DC007: Missing [Kernel] attribute
**Severity:** Info  
**Fix Available:** ✅ Automatic  
```csharp
// ℹ️ Looks like a kernel but missing attribute
public static void ProcessData(Span<float> input, Span<float> output)
{
    for (int i = 0; i < input.Length; i++)
    {
        output[i] = input[i] * 2.0f;
    }
}

// ✅ Fixed automatically
[Kernel]
public static void ProcessData(Span<float> input, Span<float> output) { }
```

#### DC008: Unnecessary complexity
**Severity:** Info  
**Description:** Suggests simplification opportunities  
```csharp
// ℹ️ Nested loops detected - consider vectorization
[Kernel]
public static void ComplexKernel(Span<float> data)
{
    for (int i = 0; i < 10; i++)
    {
        for (int j = 0; j < 10; j++) // ← Complexity warning
        {
            data[i * 10 + j] = i + j;
        }
    }
}
```

#### DC009: Thread safety warnings
**Severity:** Warning  
**Description:** Potential race conditions in parallel execution  
```csharp
// ⚠️ Multiple assignments may cause race conditions
[Kernel]
public static void UnsafeKernel(Span<float> data)
{
    float temp = 0.0f;
    temp = 1.0f;
    temp = 2.0f; // ← Multiple assignments to shared variable
}
```

### Usage Pattern Rules (DC010-DC012)

#### DC010: Incorrect threading model
**Severity:** Warning  
**Fix Available:** ✅ Automatic  
```csharp
// ⚠️ Should use Kernel.ThreadId instead of loops
[Kernel]
public static void IncorrectThreading(Span<float> data)
{
    for (int i = 0; i < data.Length; i++) // ← Should use ThreadId
    {
        data[i] *= 2.0f;
    }
}

// ✅ Fixed automatically
[Kernel]
public static void CorrectThreading(Span<float> data)
{
    int index = Kernel.ThreadId.X; // ← Added automatically
    if (index < data.Length)       // ← Bounds check added
    {
        data[index] *= 2.0f;
    }
}
```

#### DC011: Missing bounds check
**Severity:** Warning  
**Fix Available:** ✅ Automatic  
```csharp
// ⚠️ Missing bounds validation
[Kernel]
public static void UnsafeAccess(Span<float> data)
{
    int index = GetIndex();
    data[index] = data[index] * 2.0f; // ← No bounds check
}

// ✅ Fixed automatically
[Kernel]
public static void SafeAccess(Span<float> data)
{
    int index = GetIndex();
    if (index >= data.Length) return; // ← Added automatically
    data[index] = data[index] * 2.0f;
}
```

#### DC012: Suboptimal backend selection
**Severity:** Info  
**Description:** Backend selection doesn't match kernel complexity  
```csharp
// ℹ️ Simple operation doesn't need GPU
[Kernel(Backends = KernelBackends.CUDA)] // ← Overkill for simple operation
public static void SimpleAdd(Span<float> data)
{
    int index = Kernel.ThreadId.X;
    if (index < data.Length)
        data[index] += 1.0f; // ← CPU might be faster
}
```

## 🔧 Available Code Fixes

1. **Make Method Static** (DC001) - Adds `static` modifier
2. **Convert Arrays to Span** (DC002) - Converts `float[]` → `Span<float>`
3. **Add [Kernel] Attribute** (DC007) - Adds missing attribute
4. **Add Kernel Threading** (DC010) - Replaces loops with `Kernel.ThreadId`
5. **Add Bounds Check** (DC011) - Adds safety validation

## 🚀 IDE Integration

### Visual Studio
- Real-time diagnostics in Error List
- Quick fixes via Ctrl+. (lightbulb menu)
- IntelliSense integration

### VS Code
- Diagnostics in Problems panel  
- Quick fixes via Ctrl+. or right-click
- Hover information for diagnostics

## ⚡ Performance Impact

- **Zero runtime overhead** - All analysis at compile time
- **Incremental analysis** - Only analyzes changed code
- **Concurrent execution** - Parallel analysis for faster builds
- **Minimal memory usage** - Efficient syntax tree traversal

## 🎯 Best Practices Enforced

1. **Memory Safety** - Bounds checking and safe indexing
2. **Performance** - SIMD hints and memory access optimization
3. **GPU Compatibility** - Static methods and supported constructs only
4. **Thread Safety** - Race condition detection
5. **Code Quality** - Complexity reduction suggestions

## 📈 Usage Statistics

From DotCompute.Generators.Examples.AnalyzerDemo.cs:
- **12 diagnostic rules** covering all kernel development aspects
- **5 automated code fixes** for instant problem resolution  
- **Production tested** with comprehensive test suite
- **IDE integrated** for seamless development experience

## 🔍 Examples

See `Examples/AnalyzerDemo.cs` for comprehensive examples of:
- ✅ **Good kernel patterns** that pass all checks
- ❌ **Problematic code** that triggers diagnostics  
- 🔧 **Before/after** code fix demonstrations
- 📊 **Performance analysis** examples

## 🧪 Testing

Run analyzer tests:
```bash
dotnet test tests/Unit/DotCompute.Generators.Tests/
```

The test suite includes:
- **Diagnostic accuracy tests** - Ensures rules fire correctly
- **Code fix validation** - Verifies fixes produce correct code  
- **IDE integration tests** - Tests Visual Studio/VS Code scenarios
- **Performance benchmarks** - Ensures minimal analysis overhead

---

*The DotCompute analyzers are production-ready and actively maintained. They provide immediate value by catching issues early and automatically fixing common problems, leading to better kernel code quality and developer productivity.*