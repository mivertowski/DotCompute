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

### Step 2: Set Up DotCompute Runtime

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;
using DotCompute.Abstractions.Interfaces;

// Build the host with DotCompute services
var host = Host.CreateApplicationBuilder(args);
host.Services.AddDotComputeRuntime();  // Registers all necessary services
var app = host.Build();

// Get the orchestrator from DI
var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();
```

### Step 3: Prepare Data

```csharp
const int size = 10_000;

// Create input arrays
float[] a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
float[] b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
float[] result = new float[size];
```

### Step 4: Execute the Kernel

```csharp
// Execute kernel with automatic backend selection
await orchestrator.ExecuteKernelAsync(
    kernelName: "VectorAdd",
    args: new object[] { a, b, result }
);
```

The orchestrator automatically:
- Selects the best available backend (GPU or CPU)
- Handles data transfers to/from the device
- Manages thread configuration

### Step 5: Verify Results

```csharp
// Verify correctness
bool correct = true;
for (int i = 0; i < 5; i++)  // Check first 5 elements
{
    float expected = a[i] + b[i];
    Console.WriteLine($"result[{i}] = {result[i]} (expected: {expected})");
    if (Math.Abs(result[i] - expected) > 0.001f)
    {
        correct = false;
    }
}

Console.WriteLine(correct ? "Results verified!" : "Verification failed!");
```

## Complete Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotCompute.Runtime;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Generators.Kernel.Attributes;

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
class Program
{
    static async Task Main(string[] args)
    {
        // Setup DotCompute
        var host = Host.CreateApplicationBuilder(args);
        host.Services.AddDotComputeRuntime();
        var app = host.Build();

        var orchestrator = app.Services.GetRequiredService<IComputeOrchestrator>();

        // Prepare data
        const int size = 10_000;
        float[] a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        float[] b = Enumerable.Range(0, size).Select(i => (float)i * 2).ToArray();
        float[] result = new float[size];

        // Execute kernel
        await orchestrator.ExecuteKernelAsync(
            kernelName: "VectorAdd",
            args: new object[] { a, b, result }
        );

        // Display results
        Console.WriteLine($"Result[0] = {result[0]}");      // 0 + 0 = 0
        Console.WriteLine($"Result[100] = {result[100]}");  // 100 + 200 = 300
        Console.WriteLine($"Result[999] = {result[999]}");  // 999 + 1998 = 2997
    }
}
```

## Understanding Thread Configuration

### Automatic Thread Management

DotCompute automatically configures thread dimensions based on your data size. For advanced control, you can use the `[Kernel]` attribute options:

```csharp
[Kernel(
    GridDimensions = new[] { 16 },      // Number of thread blocks
    BlockDimensions = new[] { 256 }     // Threads per block
)]
public static void CustomConfigKernel(...)
{
    // ...
}
```

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

## Specifying a Backend

You can explicitly request a specific backend:

```csharp
// Force CUDA execution
await orchestrator.ExecuteAsync<object>(
    "VectorAdd",
    "CUDA",  // Preferred backend
    a, b, result
);

// Force CPU execution (useful for debugging)
await orchestrator.ExecuteAsync<object>(
    "VectorAdd",
    "CPU",
    a, b, result
);
```

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

### 2. Forgetting `AddDotComputeRuntime()`

```csharp
// WRONG: Missing service registration
var services = new ServiceCollection();
var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>(); // Will throw!

// CORRECT: Register DotCompute services
services.AddDotComputeRuntime();
```

### 3. Using Wrong Interface

```csharp
// WRONG: IComputeService doesn't exist
var computeService = provider.GetRequiredService<IComputeService>();

// CORRECT: Use IComputeOrchestrator
var orchestrator = provider.GetRequiredService<IComputeOrchestrator>();
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
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = a[idx] * b[idx];
    }
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
    int idx = Kernel.ThreadId.X;
    if (idx < result.Length)
    {
        result[idx] = input[idx] + scalar;
    }
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
3. **Use `AddDotComputeRuntime()`** to register all necessary services
4. **Use `IComputeOrchestrator`** for kernel execution
5. **Thread configuration is automatic** but can be customized

## Next Module

[Memory Fundamentals →](memory-fundamentals.md)

Learn about GPU memory hierarchy and efficient data transfers.
