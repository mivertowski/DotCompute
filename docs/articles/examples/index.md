# Examples

Practical examples demonstrating DotCompute features and patterns.

## Getting Started

New to DotCompute? Start with these examples:

1. **[Vector Addition](basic-vector-operations.md#vector-addition)** - Classic "Hello World" for GPU computing
2. **[Scalar Multiplication](basic-vector-operations.md#scalar-multiplication)** - Element-wise operations
3. **[Dot Product](basic-vector-operations.md#dot-product)** - Reduction operations

## Basic Examples

Fundamental operations and patterns:

- **[Vector Operations](basic-vector-operations.md)**
  - Vector addition (element-wise)
  - Scalar multiplication
  - Dot product (reduction)
  - Vector normalization

- **[Array Transformations](array-transformations.md)**
  - Map operations
  - Filter operations
  - Prefix sum (scan)
  - Sorting

- **[Mathematical Functions](mathematical-functions.md)**
  - Trigonometric functions
  - Exponential and logarithm
  - Statistical operations
  - Random number generation

## Intermediate Examples

Real-world applications:

- **[Image Processing](image-processing.md)**
  - Gaussian blur
  - Edge detection (Sobel, Canny)
  - Color space conversion
  - Image resizing

- **[Matrix Operations](matrix-operations.md)**
  - Matrix multiplication
  - Matrix transpose
  - Matrix inversion
  - Eigenvalues and eigenvectors

- **[Signal Processing](signal-processing.md)**
  - FFT (Fast Fourier Transform)
  - Convolution
  - Filtering (low-pass, high-pass)
  - Correlation

- **[Machine Learning](machine-learning.md)**
  - Neural network layers
  - Activation functions
  - Backpropagation
  - Gradient descent

## Advanced Examples

Complex patterns and optimizations:

- **[Multi-Kernel Pipelines](multi-kernel-pipelines.md)**
  - Chaining multiple kernels
  - Data dependencies
  - Pipeline optimization
  - Asynchronous execution

- **[Multi-GPU Computing](multi-gpu-computing.md)**
  - Data parallelism
  - Model parallelism
  - Pipeline parallelism
  - Load balancing

- **[Custom Backends](custom-backends.md)**
  - Implementing IAccelerator
  - Custom memory management
  - Platform-specific optimizations
  - Plugin system

- **[Performance Optimization](performance-optimization.md)**
  - Memory access patterns
  - Kernel fusion
  - Tiling strategies
  - SIMD vectorization

## By Use Case

### Scientific Computing
- [Numerical Integration](mathematical-functions.md#numerical-integration)
- [Differential Equations](mathematical-functions.md#differential-equations)
- [Monte Carlo Simulation](mathematical-functions.md#monte-carlo)

### Computer Vision
- [Object Detection](image-processing.md#object-detection)
- [Feature Extraction](image-processing.md#feature-extraction)
- [Image Segmentation](image-processing.md#segmentation)

### Data Science
- [Large Dataset Processing](array-transformations.md#large-datasets)
- [Time Series Analysis](signal-processing.md#time-series)
- [Statistical Analysis](mathematical-functions.md#statistics)

### Deep Learning
- [Training Loop](machine-learning.md#training-loop)
- [Inference Pipeline](machine-learning.md#inference)
- [Distributed Training](multi-gpu-computing.md#distributed-training)

## By Performance Pattern

### Memory Optimization
- [Zero-Copy Operations](../guides/memory-management.md#zero-copy-operations)
- [Memory Pooling](../guides/memory-management.md#memory-pooling)
- [Pinned Memory](../guides/memory-management.md#pinned-memory)

### Compute Optimization
- [SIMD Vectorization](../guides/performance-tuning.md#simd-vectorization)
- [Coalesced Memory Access](../guides/performance-tuning.md#memory-coalescing)
- [Shared Memory Usage](../guides/performance-tuning.md#shared-memory)

### Multi-GPU Patterns
- [Scatter-Gather](../guides/multi-gpu.md#scatter-gather)
- [All-Reduce](../guides/multi-gpu.md#all-reduce)
- [Ring Reduce](../guides/multi-gpu.md#ring-reduce)

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
using Microsoft.Extensions.DependencyInjection;

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
    new { input, output });
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

| Operation | CPU (SIMD) | CUDA RTX 3090 | Metal M1 Pro |
|-----------|-----------|---------------|--------------|
| Addition | 0.8ms | 0.15ms | 0.12ms |
| Multiplication | 0.8ms | 0.15ms | 0.12ms |
| Dot Product | 1.2ms | 0.08ms | 0.10ms |

### Matrix Operations

| Operation | Size | CPU | CUDA | Metal |
|-----------|------|-----|------|-------|
| MatMul | 1024×1024 | 45ms | 2.1ms | 3.8ms |
| Transpose | 4096×4096 | 18ms | 0.9ms | 1.2ms |

### Image Processing

| Operation | Resolution | CPU | CUDA | Metal |
|-----------|-----------|-----|------|-------|
| Gaussian Blur (5×5) | 1920×1080 | 28ms | 1.8ms | 2.1ms |
| Sobel Edge | 1920×1080 | 35ms | 2.2ms | 2.7ms |

**Note**: Performance measured on:
- CPU: AMD Ryzen 9 5950X (AVX2 SIMD)
- CUDA: NVIDIA RTX 3090
- Metal: Apple M1 Pro

## Related Documentation

- [Getting Started](../getting-started.md) - Installation and setup
- [Kernel Development Guide](../guides/kernel-development.md) - Writing efficient kernels
- [Performance Tuning](../guides/performance-tuning.md) - Optimization techniques
- [Debugging Guide](../guides/debugging-guide.md) - Troubleshooting

---

**Examples • Patterns • Best Practices • Production Ready**
