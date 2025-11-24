# Multi-Kernel Pipelines

This module covers building efficient multi-stage GPU processing pipelines with proper data flow management.

## Why Multiple Kernels?

Complex computations often decompose into stages:

| Stage | Purpose | Example |
|-------|---------|---------|
| Preprocessing | Data transformation | Normalization |
| Processing | Main computation | Convolution |
| Reduction | Aggregate results | Sum/Average |
| Postprocessing | Final transformation | Thresholding |

Breaking into multiple kernels provides:
- Better resource utilization
- Simpler debugging
- Reusable components

## Basic Pipeline Pattern

### Sequential Execution

```csharp
public class ImagePipeline
{
    private readonly IComputeService _service;

    public async Task<byte[]> ProcessAsync(byte[] input, int width, int height)
    {
        int pixelCount = width * height;

        // Allocate pipeline buffers
        using var inputBuffer = _service.CreateBuffer<byte>(pixelCount * 4);
        using var normalizedBuffer = _service.CreateBuffer<float>(pixelCount * 4);
        using var processedBuffer = _service.CreateBuffer<float>(pixelCount * 4);
        using var outputBuffer = _service.CreateBuffer<byte>(pixelCount * 4);

        // Transfer input
        await inputBuffer.CopyFromAsync(input);

        var config = CalculateConfig(pixelCount);

        // Stage 1: Normalize (byte -> float, 0-255 -> 0-1)
        await _service.ExecuteKernelAsync(
            ImageKernels.Normalize,
            config,
            inputBuffer, normalizedBuffer);

        // Stage 2: Process (e.g., sharpen)
        await _service.ExecuteKernelAsync(
            ImageKernels.Sharpen,
            config,
            normalizedBuffer, processedBuffer, width, height);

        // Stage 3: Denormalize (float -> byte)
        await _service.ExecuteKernelAsync(
            ImageKernels.Denormalize,
            config,
            processedBuffer, outputBuffer);

        // Transfer output
        var output = new byte[pixelCount * 4];
        await outputBuffer.CopyToAsync(output);

        return output;
    }
}
```

### Pipeline Kernels

```csharp
public static partial class ImageKernels
{
    [Kernel]
    public static void Normalize(
        ReadOnlySpan<byte> input,
        Span<float> output)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
        {
            output[idx] = input[idx] / 255.0f;
        }
    }

    [Kernel]
    public static void Sharpen(
        ReadOnlySpan<float> input,
        Span<float> output,
        int width, int height)
    {
        int idx = Kernel.ThreadId.X;
        if (idx >= output.Length) return;

        int x = idx % width;
        int y = idx / width;

        // Skip border pixels
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
        {
            output[idx] = input[idx];
            return;
        }

        // Sharpen kernel: center * 5 - neighbors
        float center = input[idx] * 5.0f;
        float neighbors = input[idx - 1] + input[idx + 1] +
                         input[idx - width] + input[idx + width];

        output[idx] = MathF.Max(0, MathF.Min(1, center - neighbors));
    }

    [Kernel]
    public static void Denormalize(
        ReadOnlySpan<float> input,
        Span<byte> output)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
        {
            output[idx] = (byte)(input[idx] * 255.0f);
        }
    }
}
```

## Concurrent Execution

### Using CUDA Streams

Execute independent operations concurrently:

```csharp
public async Task ProcessMultipleAsync(byte[][] inputs)
{
    // Create streams for concurrent execution
    var streams = Enumerable.Range(0, 4)
        .Select(_ => _service.CreateStream())
        .ToArray();

    var tasks = new List<Task>();

    for (int i = 0; i < inputs.Length; i++)
    {
        var stream = streams[i % streams.Length];
        var input = inputs[i];

        tasks.Add(ProcessOnStreamAsync(input, stream));
    }

    await Task.WhenAll(tasks);

    // Cleanup streams
    foreach (var stream in streams)
        stream.Dispose();
}

private async Task ProcessOnStreamAsync(byte[] input, IComputeStream stream)
{
    using var buffer = _service.CreateBuffer<byte>(input.Length);

    // All operations on same stream execute in order
    // Operations on different streams can execute concurrently
    await buffer.CopyFromAsync(input, stream);
    await _service.ExecuteKernelAsync(kernel, config, stream, buffer);
    await buffer.CopyToAsync(input, stream);
}
```

### Graph-Based Execution

Define dependencies explicitly:

```csharp
public class ComputeGraph
{
    public async Task ExecutePipelineAsync()
    {
        // Define nodes
        var normalize = new KernelNode(ImageKernels.Normalize, inputBuffer, normalizedBuffer);
        var blur = new KernelNode(ImageKernels.Blur, normalizedBuffer, blurredBuffer);
        var sharpen = new KernelNode(ImageKernels.Sharpen, normalizedBuffer, sharpenedBuffer);
        var combine = new KernelNode(ImageKernels.Combine, blurredBuffer, sharpenedBuffer, outputBuffer);

        // Define dependencies
        //        normalize
        //        /       \
        //      blur    sharpen
        //        \       /
        //         combine

        blur.DependsOn(normalize);
        sharpen.DependsOn(normalize);
        combine.DependsOn(blur, sharpen);

        // Execute (blur and sharpen run concurrently)
        await _service.ExecuteGraphAsync(new[] { normalize, blur, sharpen, combine });
    }
}
```

## Buffer Management Strategies

### Strategy 1: Static Allocation

Pre-allocate all buffers:

```csharp
public class StaticPipeline : IDisposable
{
    private readonly IBuffer<float>[] _stageBuffers;

    public StaticPipeline(IComputeService service, int maxSize, int stages)
    {
        _stageBuffers = new IBuffer<float>[stages];
        for (int i = 0; i < stages; i++)
        {
            _stageBuffers[i] = service.CreateBuffer<float>(maxSize);
        }
    }

    public async Task ProcessAsync(int actualSize)
    {
        // Use pre-allocated buffers
        for (int stage = 0; stage < _stageBuffers.Length - 1; stage++)
        {
            await _service.ExecuteKernelAsync(
                _kernels[stage],
                GetConfig(actualSize),
                _stageBuffers[stage],
                _stageBuffers[stage + 1]);
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _stageBuffers)
            buffer.Dispose();
    }
}
```

### Strategy 2: Ping-Pong Buffers

Alternate between two buffers:

```csharp
public async Task PingPongProcessAsync(float[] data, int iterations)
{
    using var bufferA = _service.CreateBuffer<float>(data.Length);
    using var bufferB = _service.CreateBuffer<float>(data.Length);

    await bufferA.CopyFromAsync(data);

    for (int i = 0; i < iterations; i++)
    {
        var source = (i % 2 == 0) ? bufferA : bufferB;
        var dest = (i % 2 == 0) ? bufferB : bufferA;

        await _service.ExecuteKernelAsync(kernel, config, source, dest);
    }

    // Result is in appropriate buffer based on iteration count
    var result = (iterations % 2 == 0) ? bufferA : bufferB;
    await result.CopyToAsync(data);
}
```

### Strategy 3: In-Place with Synchronization

```csharp
[Kernel(RequiresBarrier = true)]
public static void InPlaceProcess(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    if (idx >= data.Length) return;

    // Phase 1: Read neighbors
    float left = (idx > 0) ? data[idx - 1] : 0;
    float right = (idx < data.Length - 1) ? data[idx + 1] : 0;

    // Synchronize - ensure all reads complete
    Kernel.SyncThreads();

    // Phase 2: Write result
    data[idx] = (left + data[idx] + right) / 3.0f;
}
```

## Reduction Patterns

### Parallel Reduction

```csharp
public static partial class ReductionKernels
{
    [Kernel(UseSharedMemory = true)]
    public static void SumReduce(
        ReadOnlySpan<float> input,
        Span<float> partialSums,
        int n)
    {
        // Shared memory for block reduction
        var shared = Kernel.AllocateShared<float>(256);

        int tid = Kernel.ThreadId.X;
        int idx = Kernel.BlockId.X * Kernel.BlockDim.X + tid;

        // Load to shared memory
        shared[tid] = (idx < n) ? input[idx] : 0;
        Kernel.SyncThreads();

        // Reduce within block
        for (int stride = Kernel.BlockDim.X / 2; stride > 0; stride >>= 1)
        {
            if (tid < stride)
            {
                shared[tid] += shared[tid + stride];
            }
            Kernel.SyncThreads();
        }

        // Write block result
        if (tid == 0)
        {
            partialSums[Kernel.BlockId.X] = shared[0];
        }
    }
}

public async Task<float> SumAsync(float[] data)
{
    int n = data.Length;
    int blockSize = 256;
    int gridSize = (n + blockSize - 1) / blockSize;

    using var inputBuffer = _service.CreateBuffer<float>(n);
    using var partialBuffer = _service.CreateBuffer<float>(gridSize);

    await inputBuffer.CopyFromAsync(data);

    // First reduction pass
    await _service.ExecuteKernelAsync(
        ReductionKernels.SumReduce,
        new KernelConfig { BlockSize = blockSize, GridSize = gridSize },
        inputBuffer, partialBuffer, n);

    // Continue until single value
    while (gridSize > 1)
    {
        int newGridSize = (gridSize + blockSize - 1) / blockSize;
        using var newPartialBuffer = _service.CreateBuffer<float>(newGridSize);

        await _service.ExecuteKernelAsync(
            ReductionKernels.SumReduce,
            new KernelConfig { BlockSize = blockSize, GridSize = newGridSize },
            partialBuffer, newPartialBuffer, gridSize);

        gridSize = newGridSize;
        // Swap buffers
    }

    var result = new float[1];
    await partialBuffer.CopyToAsync(result);
    return result[0];
}
```

## Pipeline Performance Tips

### 1. Minimize Kernel Launches

```csharp
// BAD: Many small kernels
await ExecuteAsync(Normalize, data);
await ExecuteAsync(Square, data);
await ExecuteAsync(Sum, data);

// BETTER: Fused kernel
[Kernel]
public static void NormalizeSquareSum(Span<float> data)
{
    int idx = Kernel.ThreadId.X;
    float val = data[idx] / 255.0f;  // Normalize
    data[idx] = val * val;            // Square
    // Sum handled separately (requires reduction)
}
```

### 2. Overlap Transfers and Compute

```csharp
// Process in batches with overlap
for (int batch = 0; batch < batchCount; batch++)
{
    // Start transfer of next batch while processing current
    var transferTask = (batch < batchCount - 1)
        ? buffers[(batch + 1) % 2].CopyFromAsync(GetBatch(batch + 1))
        : Task.CompletedTask;

    // Process current batch
    await _service.ExecuteKernelAsync(kernel, config, buffers[batch % 2]);

    // Ensure transfer complete before next iteration
    await transferTask;
}
```

### 3. Use Appropriate Buffer Sizes

Balance between parallelism and overhead:

| Data Size | Recommendation |
|-----------|----------------|
| < 10K | Single kernel, consider CPU |
| 10K - 1M | Standard pipeline |
| > 1M | Stream with overlap |

## Exercises

### Exercise 1: Image Filter Pipeline

Build a pipeline: Grayscale → Blur → Edge Detection → Threshold

### Exercise 2: Parallel Reduction

Implement parallel min/max reduction.

### Exercise 3: Stream Overlap

Measure speedup from overlapping transfer and compute.

## Key Takeaways

1. **Decompose complex operations** into pipeline stages
2. **Reuse buffers** to minimize allocation
3. **Use streams** for concurrent execution
4. **Fuse kernels** when possible to reduce launch overhead
5. **Overlap transfers and compute** for maximum throughput

## Next Module

[Error Handling →](error-handling.md)

Learn to debug GPU code and handle failures gracefully.
