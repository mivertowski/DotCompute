# Kernel Development Guide

This guide covers best practices for writing efficient, correct, and maintainable compute kernels with DotCompute.

## Kernel Basics

### Anatomy of a Kernel

```csharp
[Kernel]                                    // 1. Kernel attribute
public static void MyKernel(                // 2. Must be static, return void
    ReadOnlySpan<float> input,              // 3. Input parameters (ReadOnlySpan)
    Span<float> output,                     // 4. Output parameters (Span)
    int length)                             // 5. Scalar parameters
{
    int idx = Kernel.ThreadId.X;            // 6. Thread indexing

    if (idx < length)                       // 7. Bounds checking
    {
        output[idx] = input[idx] * 2.0f;    // 8. Kernel logic
    }
}
```

### Kernel Requirements

**Must Have**:
- `[Kernel]` attribute
- `static` modifier
- `void` return type
- At least one parameter
- Bounds checking
- `Kernel.ThreadId` for indexing

**Cannot Have**:
- `async` modifier
- Instance methods
- `ref`/`out` parameters (except `Span<T>`)
- LINQ expressions
- Reflection
- Non-inlineable method calls

## Thread Indexing

### 1D Indexing (Most Common)

```csharp
[Kernel]
public static void Process1D(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;

    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2;
    }
}
```

**Use Cases**: Vector operations, element-wise operations, 1D arrays

### 2D Indexing

```csharp
[Kernel]
public static void Process2D(
    ReadOnlySpan<float> input,
    Span<float> output,
    int width,
    int height)
{
    int col = Kernel.ThreadId.X;
    int row = Kernel.ThreadId.Y;

    if (col < width && row < height)
    {
        int idx = row * width + col;
        output[idx] = input[idx] * 2;
    }
}
```

**Use Cases**: Image processing, matrix operations, 2D grids

### 3D Indexing

```csharp
[Kernel]
public static void Process3D(
    ReadOnlySpan<float> input,
    Span<float> output,
    int width,
    int height,
    int depth)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;
    int z = Kernel.ThreadId.Z;

    if (x < width && y < height && z < depth)
    {
        int idx = z * (width * height) + y * width + x;
        output[idx] = input[idx] * 2;
    }
}
```

**Use Cases**: Volumetric data, 3D simulations, video processing

## Parameter Types

### Input Parameters: ReadOnlySpan<T>

```csharp
[Kernel]
public static void Example(
    ReadOnlySpan<float> input,    // ✅ Recommended
    ReadOnlySpan<int> indices,    // ✅ Multiple inputs OK
    Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[indices[idx]];
    }
}
```

**Benefits**:
- Compiler enforces read-only semantics
- Zero-copy on CPU
- Clear intent in API

### Output Parameters: Span<T>

```csharp
[Kernel]
public static void Example(
    ReadOnlySpan<float> input,
    Span<float> output1,          // ✅ Multiple outputs OK
    Span<float> output2)
{
    int idx = Kernel.ThreadId.X;
    if (idx < input.Length)
    {
        output1[idx] = input[idx] * 2;
        output2[idx] = input[idx] + 1;
    }
}
```

**Benefits**:
- Clear output semantics
- Allows multiple outputs
- Efficient memory access

### Scalar Parameters

```csharp
[Kernel]
public static void Scale(
    ReadOnlySpan<float> input,
    Span<float> output,
    float scaleFactor,           // ✅ Scalar parameter
    int offset)                  // ✅ Multiple scalars OK
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * scaleFactor + offset;
    }
}
```

**Supported Scalar Types**:
- `int`, `long`, `short`, `byte`
- `float`, `double`
- `bool`
- `uint`, `ulong`, `ushort`, `sbyte`

## Bounds Checking

### Why Bounds Checking Matters

```csharp
// ❌ BAD: No bounds check (crashes on out-of-bounds)
[Kernel]
public static void Unsafe(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    output[idx] = input[idx] * 2; // May access beyond array bounds!
}

// ✅ GOOD: Always check bounds
[Kernel]
public static void Safe(ReadOnlySpan<float> input, Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)  // Prevents out-of-bounds access
    {
        output[idx] = input[idx] * 2;
    }
}
```

**Analyzer Warning**: DC009 will warn about missing bounds checks

### Bounds Checking Patterns

**Pattern 1: Single Array**
```csharp
if (idx < array.Length)
{
    array[idx] = value;
}
```

**Pattern 2: Multiple Arrays (Same Size)**
```csharp
if (idx < Math.Min(input.Length, output.Length))
{
    output[idx] = input[idx] * 2;
}
```

**Pattern 3: 2D Bounds**
```csharp
if (col < width && row < height)
{
    int idx = row * width + col;
    array[idx] = value;
}
```

**Pattern 4: Early Return**
```csharp
int idx = Kernel.ThreadId.X;
if (idx >= array.Length)
    return;  // Thread does nothing if out of bounds

array[idx] = value;
```

## Common Kernel Patterns

### 1. Vector Addition

```csharp
[Kernel]
public static void VectorAdd(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result)
{
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] + b[idx];
    }
}
```

**Performance**: Memory-bound, ~12 GB/s on PCIe 4.0

### 2. Scalar Multiply

```csharp
[Kernel]
public static void ScalarMultiply(
    ReadOnlySpan<float> input,
    Span<float> output,
    float scalar)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * scalar;
    }
}
```

**Performance**: Memory-bound, same as vector add

### 3. Dot Product (Reduction)

```csharp
[Kernel]
public static void DotProduct(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> partialSums,  // One per thread block
    int n)
{
    int idx = Kernel.ThreadId.X;

    // Each thread computes partial sum
    float sum = 0;
    for (int i = idx; i < n; i += Kernel.GridDim.X * Kernel.BlockDim.X)
    {
        sum += a[i] * b[i];
    }

    // Store partial sum (final reduction happens on CPU)
    int blockIdx = Kernel.BlockId.X;
    partialSums[blockIdx] = sum;
}
```

**Note**: Full parallel reduction requires additional synchronization not shown here

### 4. Matrix Multiplication

```csharp
[Kernel]
public static void MatrixMultiply(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> c,
    int M, int N, int K)  // A: M×K, B: K×N, C: M×N
{
    int row = Kernel.ThreadId.Y;
    int col = Kernel.ThreadId.X;

    if (row < M && col < N)
    {
        float sum = 0;
        for (int k = 0; k < K; k++)
        {
            sum += a[row * K + k] * b[k * N + col];
        }
        c[row * N + col] = sum;
    }
}
```

**Performance**: Compute-bound, benefits greatly from GPU

### 5. Image Blur (Convolution)

```csharp
[Kernel]
public static void GaussianBlur(
    ReadOnlySpan<float> input,
    Span<float> output,
    int width,
    int height)
{
    int x = Kernel.ThreadId.X;
    int y = Kernel.ThreadId.Y;

    if (x >= 1 && x < width - 1 && y >= 1 && y < height - 1)
    {
        // 3x3 Gaussian kernel
        float sum = 0;
        sum += input[(y-1) * width + (x-1)] * 0.0625f;
        sum += input[(y-1) * width +  x   ] * 0.125f;
        sum += input[(y-1) * width + (x+1)] * 0.0625f;
        sum += input[ y    * width + (x-1)] * 0.125f;
        sum += input[ y    * width +  x   ] * 0.25f;
        sum += input[ y    * width + (x+1)] * 0.125f;
        sum += input[(y+1) * width + (x-1)] * 0.0625f;
        sum += input[(y+1) * width +  x   ] * 0.125f;
        sum += input[(y+1) * width + (x+1)] * 0.0625f;

        output[y * width + x] = sum;
    }
}
```

**Performance**: Memory-bound with spatial locality

## Performance Optimization

### 1. Memory Access Patterns

**Sequential Access (Fast)**:
```csharp
// ✅ Good: Sequential, cache-friendly
for (int i = idx; i < n; i += stride)
{
    output[i] = input[i] * 2;
}
```

**Random Access (Slow)**:
```csharp
// ❌ Bad: Random access, cache-unfriendly
for (int i = idx; i < n; i += stride)
{
    output[i] = input[indices[i]] * 2;  // Random lookup
}
```

**Strided Access (Medium)**:
```csharp
// ⚠️ OK: Strided but predictable
for (int i = idx; i < n; i += stride)
{
    output[i] = input[i * 4] * 2;  // Every 4th element
}
```

### 2. Data Reuse

**Without Reuse** (reads `input[i]` three times):
```csharp
output[i] = input[i] + input[i] * input[i];
```

**With Reuse** (reads `input[i]` once):
```csharp
float value = input[i];
output[i] = value + value * value;
```

### 3. Avoid Branching

**Branchy Code** (bad for GPU):
```csharp
if (input[idx] > threshold)
{
    output[idx] = input[idx] * 2;
}
else
{
    output[idx] = input[idx] / 2;
}
```

**Branch-Free** (better for GPU):
```csharp
float value = input[idx];
float isGreater = value > threshold ? 1.0f : 0.0f;
output[idx] = value * (2.0f * isGreater + 0.5f * (1.0f - isGreater));
```

**Or Use Math**:
```csharp
float value = input[idx];
float multiplier = (value > threshold) ? 2.0f : 0.5f;
output[idx] = value * multiplier;
```

### 4. Loop Unrolling

**Regular Loop**:
```csharp
for (int i = 0; i < 4; i++)
{
    sum += input[idx * 4 + i];
}
```

**Unrolled Loop** (compiler may do this automatically):
```csharp
sum += input[idx * 4 + 0];
sum += input[idx * 4 + 1];
sum += input[idx * 4 + 2];
sum += input[idx * 4 + 3];
```

### 5. Use Appropriate Precision

**Single Precision** (faster on most GPUs):
```csharp
float result = input[idx] * 2.0f;  // ✅ Fast
```

**Double Precision** (slower, use only when needed):
```csharp
double result = input[idx] * 2.0;  // ⚠️ 2-8x slower on GPU
```

## Testing Kernels

### Unit Testing

```csharp
[Fact]
public async Task VectorAdd_ProducesCorrectResults()
{
    // Arrange
    var orchestrator = CreateOrchestrator();
    var a = new float[] { 1, 2, 3, 4, 5 };
    var b = new float[] { 10, 20, 30, 40, 50 };
    var result = new float[5];

    // Act
    await orchestrator.ExecuteKernelAsync(
        "VectorAdd",
        new { a, b, result }
    );

    // Assert
    Assert.Equal(new float[] { 11, 22, 33, 44, 55 }, result);
}
```

### Cross-Backend Validation

```csharp
[Fact]
public async Task VectorAdd_CPUandGPU_ProduceSameResults()
{
    var debugService = GetService<IKernelDebugService>();
    var input = GenerateRandomData(10_000);

    var validation = await debugService.ValidateCrossBackendAsync(
        "VectorAdd",
        new { input },
        primaryBackend: AcceleratorType.CUDA,
        referenceBackend: AcceleratorType.CPU
    );

    Assert.True(validation.IsValid);
}
```

### Performance Testing

```csharp
[Fact]
public async Task VectorAdd_PerformanceIsAcceptable()
{
    var orchestrator = CreateOrchestrator();
    var input = new float[1_000_000];

    var stopwatch = Stopwatch.StartNew();

    for (int i = 0; i < 100; i++)
    {
        await orchestrator.ExecuteKernelAsync("VectorAdd", new { input });
    }

    stopwatch.Stop();

    var avgTime = stopwatch.Elapsed.TotalMilliseconds / 100;
    Assert.True(avgTime < 5.0, $"Average execution time {avgTime}ms exceeds 5ms");
}
```

## Debugging Kernels

### Enable Debug Validation

```csharp
#if DEBUG
services.AddProductionDebugging(options =>
{
    options.Profile = DebugProfile.Development;
    options.ValidateAllExecutions = true;
    options.ThrowOnValidationFailure = true;
});
#endif
```

### Common Issues

**Issue 1: Wrong Results on GPU**

**Symptom**: GPU produces different results than CPU

**Debug**:
```csharp
var validation = await debugService.ValidateCrossBackendAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    AcceleratorType.CPU
);

if (!validation.IsValid)
{
    foreach (var diff in validation.Differences)
    {
        Console.WriteLine($"Index {diff.Index}: GPU={diff.PrimaryValue}, CPU={diff.ReferenceValue}");
    }
}
```

**Common Causes**:
- Missing bounds check
- Race condition (multiple threads writing same location)
- Uninitialized memory
- Floating-point precision differences

**Issue 2: Non-Deterministic Results**

**Symptom**: Same input produces different outputs on different runs

**Debug**:
```csharp
var determinism = await debugService.TestDeterminismAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    runs: 100
);

if (!determinism.IsDeterministic)
{
    Console.WriteLine($"Cause: {determinism.Cause}");
}
```

**Common Causes**:
- Race conditions
- Unordered reduction operations
- Floating-point rounding (accumulation order matters)

**Issue 3: Slow Performance**

**Debug**:
```csharp
var profile = await debugService.ProfileKernelAsync(
    "MyKernel",
    parameters,
    AcceleratorType.CUDA,
    iterations: 1000
);

Console.WriteLine($"Average: {profile.AverageTime.TotalMicroseconds}μs");
Console.WriteLine($"Std dev: {profile.StandardDeviation.TotalMicroseconds}μs");
Console.WriteLine($"GFLOPS: {profile.GFLOPS:F2}");
```

**Common Causes**:
- Memory-bound (low compute intensity)
- Poor memory access pattern
- Too many branches
- Insufficient parallelism

## Best Practices Summary

### ✅ Do

1. **Always check bounds**: Prevents crashes and undefined behavior
2. **Use `ReadOnlySpan<T>` for inputs**: Clear semantics, zero-copy on CPU
3. **Reuse variables**: Reduces memory traffic
4. **Keep kernels simple**: Complex logic is hard to optimize
5. **Test cross-backend**: Ensures correctness on all platforms
6. **Profile before optimizing**: Know where the bottleneck is

### ❌ Don't

1. **Don't use LINQ**: Not supported in kernels
2. **Don't use reflection**: Not supported, not AOT-compatible
3. **Don't call complex methods**: May not inline properly
4. **Don't ignore analyzer warnings**: DC001-DC012 catch real issues
5. **Don't optimize prematurely**: Profile first
6. **Don't forget async/await**: Kernel execution is asynchronous

## Further Reading

- [Performance Tuning Guide](performance-tuning.md) - Advanced optimization
- [Debugging Guide](debugging-guide.md) - Troubleshooting techniques
- [Backend Selection](backend-selection.md) - Choosing optimal backend
- [Architecture: Source Generators](../architecture/source-generators.md) - How code generation works
- [Diagnostic Rules Reference](../reference/diagnostic-rules.md) - Complete DC001-DC012 reference

---

**Write Efficient Kernels • Test Thoroughly • Profile Before Optimizing**
