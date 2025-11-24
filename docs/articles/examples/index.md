# Examples

Practical examples demonstrating DotCompute features and patterns.

## Getting Started

New to DotCompute? Start with these examples:

1. **[Working Reference](WORKING_REFERENCE.md)** - Tested, working code patterns for v0.4.2-rc2
2. **[Vector Addition](basic-vector-operations.md#vector-addition)** - Classic "Hello World" for GPU computing
3. **[Matrix Multiplication](matrix-operations.md#matrix-multiplication)** - 2D grid operations

## Available Examples

### Basic Operations

- **[Vector Operations](basic-vector-operations.md)**
  - Vector addition (element-wise)
  - Scalar multiplication
  - Dot product (reduction)
  - Vector normalization

### Matrix Operations

- **[Matrix Operations](matrix-operations.md)**
  - Matrix multiplication
  - Matrix transpose
  - Matrix inversion
  - Eigenvalues and eigenvectors

### Image Processing

- **[Image Processing](image-processing.md)**
  - Gaussian blur
  - Edge detection (Sobel, Canny)
  - Color space conversion
  - Image resizing

### Pipeline Patterns

- **[Multi-Kernel Pipelines](multi-kernel-pipelines.md)**
  - Chaining multiple kernels
  - Data dependencies
  - Pipeline optimization
  - Asynchronous execution

## Quick Reference

### Basic Kernel Template

```csharp
using DotCompute;

[Kernel]
public static void MyKernel(
    ReadOnlySpan<float> input,
    Span<float> output)
{
    int idx = Kernel.ThreadId.X;
    if (idx < output.Length)
    {
        output[idx] = input[idx] * 2.0f;
    }
}
```

### Execution Pattern

```csharp
using DotCompute;
using DotCompute.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddDotComputeRuntime();
    })
    .Build();

var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();

var input = new float[] { 1, 2, 3, 4, 5 };
var output = new float[5];

await orchestrator.ExecuteKernelAsync(
    "MyKernel",
    new object[] { input, output });
```

### Common Patterns

**Element-wise operation**:
```csharp
int idx = Kernel.ThreadId.X;
if (idx < length)
{
    output[idx] = operation(input[idx]);
}
```

**Reduction**:
```csharp
int idx = Kernel.ThreadId.X;
float sum = 0.0f;
for (int i = idx; i < length; i += Kernel.BlockDim.X)
{
    sum += input[i];
}
atomicAdd(ref output[0], sum);
```

**2D Grid**:
```csharp
int x = Kernel.ThreadId.X;
int y = Kernel.ThreadId.Y;
if (x < width && y < height)
{
    int idx = y * width + x;
    output[idx] = input[idx];
}
```

## Performance Expectations

### Vector Operations (1M elements)

| Operation | CPU (SIMD) | CUDA RTX 2000 Ada |
|-----------|-----------|-------------------|
| Addition | 0.8ms | 0.04ms |
| Multiplication | 0.8ms | 0.04ms |
| Dot Product | 1.2ms | 0.05ms |

### Matrix Operations

| Operation | Size | CPU | CUDA |
|-----------|------|-----|------|
| MatMul | 1024x1024 | 45ms | 2.1ms |
| Transpose | 4096x4096 | 18ms | 0.9ms |

### Image Processing

| Operation | Resolution | CPU | CUDA |
|-----------|-----------|-----|------|
| Gaussian Blur (5x5) | 1920x1080 | 28ms | 1.8ms |
| Sobel Edge | 1920x1080 | 35ms | 2.2ms |

**Note**: Performance measured on:
- CPU: AMD Ryzen with AVX2 SIMD
- CUDA: NVIDIA RTX 2000 Ada (CC 8.9)

## Related Documentation

- [Getting Started](../getting-started.md) - Installation and setup
- [Kernel Development Guide](../guides/kernel-development.md) - Writing efficient kernels
- [Performance Tuning](../guides/performance-tuning.md) - Optimization techniques
- [Debugging Guide](../guides/debugging-guide.md) - Troubleshooting
- [Learning Paths](../learning-paths/index.md) - Structured learning by experience level

---

**Examples - Patterns - Best Practices - Production Ready**
