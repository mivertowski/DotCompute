# Your First Kernel

This module guides you through writing and executing your first GPU kernel using DotCompute's `[Kernel]` attribute.

## What is a Kernel?

A **kernel** is a function that executes in parallel across many GPU threads. Each thread:

- Runs the same code
- Has a unique thread ID
- Processes different data elements

## Writing a Vector Addition Kernel

### Step 1: Define the Kernel

Create a static method with the `[Kernel]` attribute:

```csharp
using DotCompute.Generators.Kernel.Attributes;

public static partial class MyKernels
{
    [Kernel]
    public static void VectorAdd(
        ReadOnlySpan<float> a,
        ReadOnlySpan<float> b,
        Span<float> result)
    {
        // Get this thread's index
        int idx = Kernel.ThreadId.X;

        // Bounds check (critical for correctness)
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }
}
```

**Key elements:**

| Element | Purpose |
|---------|---------|
| `[Kernel]` | Marks method for GPU compilation |
| `partial class` | Enables source generation |
| `ReadOnlySpan<T>` | Input buffer (read-only on GPU) |
| `Span<T>` | Output buffer (read-write on GPU) |
| `Kernel.ThreadId.X` | Current thread's X-dimension index |
| Bounds check | Prevents out-of-bounds access |

### Step 2: Create the Compute Service

```csharp
using DotCompute;
using Microsoft.Extensions.DependencyInjection;

// Build the compute service
var services = new ServiceCollection();
services.AddDotCompute();
var provider = services.BuildServiceProvider();

var computeService = provider.GetRequiredService<IComputeService>();
```

### Step 3: Allocate Buffers

```csharp
const int size = 10_000;

// Create input data
float[] hostA = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
float[] hostB = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
float[] hostResult = new float[size];

// Allocate GPU buffers
using var bufferA = computeService.CreateBuffer<float>(size);
using var bufferB = computeService.CreateBuffer<float>(size);
using var bufferResult = computeService.CreateBuffer<float>(size);

// Copy input data to GPU
await bufferA.CopyFromAsync(hostA);
await bufferB.CopyFromAsync(hostB);
```

### Step 4: Execute the Kernel

```csharp
// Configure thread dimensions
var config = new KernelConfig
{
    GridSize = (size + 255) / 256,  // Number of thread blocks
    BlockSize = 256                   // Threads per block
};

// Execute kernel
await computeService.ExecuteKernelAsync(
    MyKernels.VectorAdd,
    config,
    bufferA, bufferB, bufferResult);

// Copy results back to host
await bufferResult.CopyToAsync(hostResult);
```

### Step 5: Verify Results

```csharp
// Verify correctness
bool correct = true;
for (int i = 0; i < size; i++)
{
    float expected = hostA[i] + hostB[i];
    if (Math.Abs(hostResult[i] - expected) > 0.001f)
    {
        Console.WriteLine($"Mismatch at {i}: expected {expected}, got {hostResult[i]}");
        correct = false;
        break;
    }
}

Console.WriteLine(correct ? "Results verified!" : "Verification failed!");
```

## Complete Example

```csharp
using DotCompute;
using DotCompute.Generators.Kernel.Attributes;
using Microsoft.Extensions.DependencyInjection;

// Define kernel
public static partial class MyKernels
{
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
}

// Main program
var services = new ServiceCollection();
services.AddDotCompute();
var provider = services.BuildServiceProvider();
var computeService = provider.GetRequiredService<IComputeService>();

const int size = 10_000;

// Prepare data
float[] hostA = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
float[] hostB = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
float[] hostResult = new float[size];

// Allocate and transfer
using var bufferA = computeService.CreateBuffer<float>(size);
using var bufferB = computeService.CreateBuffer<float>(size);
using var bufferResult = computeService.CreateBuffer<float>(size);

await bufferA.CopyFromAsync(hostA);
await bufferB.CopyFromAsync(hostB);

// Execute
var config = new KernelConfig
{
    GridSize = (size + 255) / 256,
    BlockSize = 256
};

await computeService.ExecuteKernelAsync(
    MyKernels.VectorAdd,
    config,
    bufferA, bufferB, bufferResult);

// Retrieve and verify
await bufferResult.CopyToAsync(hostResult);

Console.WriteLine($"Result[0] = {hostResult[0]}");      // 0 + 0 = 0
Console.WriteLine($"Result[100] = {hostResult[100]}");  // 100 + 200 = 300
Console.WriteLine($"Result[999] = {hostResult[999]}");  // 999 + 1998 = 2997
```

## Understanding Thread Configuration

### Grid and Block Dimensions

```
Total threads = GridSize × BlockSize
```

For 10,000 elements with BlockSize=256:
- GridSize = ceil(10000/256) = 40 blocks
- Total threads = 40 × 256 = 10,240 threads

The extra 240 threads are handled by the bounds check.

### Choosing Block Size

| Block Size | Use Case |
|------------|----------|
| 32 | Minimum (one warp) |
| 128 | Memory-bound kernels |
| 256 | General purpose (recommended) |
| 512 | Compute-bound kernels |
| 1024 | Maximum (device dependent) |

## Common Mistakes

### 1. Missing Bounds Check

```csharp
// WRONG: Will crash or corrupt memory
result[idx] = a[idx] + b[idx];

// CORRECT: Always check bounds
if (idx < result.Length)
{
    result[idx] = a[idx] + b[idx];
}
```

### 2. Forgetting to Copy Data

```csharp
// WRONG: Buffer contains uninitialized data
using var buffer = computeService.CreateBuffer<float>(size);
// Missing: await buffer.CopyFromAsync(hostData);
```

### 3. Not Disposing Buffers

```csharp
// WRONG: Memory leak
var buffer = computeService.CreateBuffer<float>(size);
// Missing: using statement or Dispose()

// CORRECT: Use 'using' statement
using var buffer = computeService.CreateBuffer<float>(size);
```

## Exercises

### Exercise 1: Element-wise Multiplication

Modify the kernel to multiply instead of add:

```csharp
[Kernel]
public static void VectorMultiply(
    ReadOnlySpan<float> a,
    ReadOnlySpan<float> b,
    Span<float> result)
{
    // Your code here
}
```

### Exercise 2: Scalar Addition

Add a scalar value to each element:

```csharp
[Kernel]
public static void ScalarAdd(
    ReadOnlySpan<float> input,
    float scalar,
    Span<float> result)
{
    // Your code here
}
```

### Exercise 3: Benchmark

Compare GPU vs CPU performance for different data sizes:

- 1,000 elements
- 100,000 elements
- 10,000,000 elements

At what size does GPU become faster?

## Key Takeaways

1. **Kernels execute in parallel** across thousands of threads
2. **Always include bounds checks** to prevent memory errors
3. **Buffer lifecycle matters** - use `using` statements
4. **Thread configuration affects performance** - experiment with block sizes

## Next Module

[Memory Fundamentals →](memory-fundamentals.md)

Learn about GPU memory hierarchy and efficient data transfers.
