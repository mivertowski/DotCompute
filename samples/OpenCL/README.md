# OpenCL Backend Sample Projects

This directory contains comprehensive sample projects demonstrating DotCompute's OpenCL backend capabilities.

## Sample Projects

### 1. Basic Vector Operations
**Directory**: `01-VectorOperations/`

Simple vector addition, subtraction, and multiplication demonstrating:
- Basic kernel definition
- Memory allocation and transfer
- Kernel execution
- Result retrieval

**Key Concepts**:
- OpenCL context initialization
- Buffer management
- Simple kernel execution

### 2. Matrix Multiplication
**Directory**: `02-MatrixMultiply/`

Optimized matrix multiplication with tiling:
- Local memory usage
- Work group optimization
- Performance benchmarking
- Multiple optimization levels

**Key Concepts**:
- Tiled algorithms
- Local memory (shared memory)
- Synchronization barriers
- Work group sizing

### 3. Image Processing
**Directory**: `03-ImageProcessing/`

Image filtering and transformations:
- Convolution kernels (blur, sharpen, edge detection)
- Color space conversions
- 2D work groups
- Image memory objects

**Key Concepts**:
- 2D work groups
- Image memory vs buffer memory
- Sampler objects
- Texture filtering

### 4. Multi-Device Execution
**Directory**: `04-MultiDevice/`

Distributing work across multiple OpenCL devices:
- Device enumeration
- Workload partitioning
- Device-to-device transfers
- Performance comparison

**Key Concepts**:
- Multiple accelerator instances
- Load balancing
- Device capabilities querying

### 5. Performance Profiling
**Directory**: `05-Profiling/`

Comprehensive profiling and optimization:
- Event-based profiling
- Hardware counter integration
- Bottleneck identification
- Performance visualization

**Key Concepts**:
- OpenCL profiling
- Performance metrics
- Optimization strategies

## Running the Samples

### Prerequisites

```bash
# Install .NET 9.0 SDK
dotnet --version  # Should be 9.0 or later

# Install OpenCL runtime (see platform-specific instructions)
# NVIDIA: CUDA Toolkit
# AMD: ROCm or Radeon Software
# Intel: Intel OpenCL Runtime
```

### Build and Run

```bash
# Navigate to sample directory
cd 01-VectorOperations

# Build
dotnet build

# Run
dotnet run
```

### Expected Output

Each sample will:
1. Detect available OpenCL devices
2. Display device information
3. Execute the sample kernel
4. Verify results
5. Show performance metrics

Example output:
```
OpenCL Device Discovery:
  Platform: NVIDIA CUDA (NVIDIA Corporation)
    Device: NVIDIA GeForce RTX 4090
      Type: GPU
      Compute Units: 128
      Memory: 24576 MB
      Max Clock: 2520 MHz

Selected device: NVIDIA GeForce RTX 4090

Vector Addition (1,000,000 elements):
  Compilation time: 45.2 ms
  Execution time: 0.18 ms
  Throughput: 22.2 GB/s
  ✓ Results verified

```

## Sample Code Overview

### 01-VectorOperations

#### Program.cs
```csharp
using DotCompute.Backends.OpenCL;
using DotCompute.Abstractions.Kernels;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

// Initialize OpenCL
var accelerator = new OpenCLAccelerator(loggerFactory);
await accelerator.InitializeAsync();

Console.WriteLine($"Using device: {accelerator.Name}");
Console.WriteLine($"Memory: {accelerator.Info.TotalMemory / (1024 * 1024)} MB");

// Define kernel
var kernelSource = @"
__kernel void VectorAdd(
    __global const float* a,
    __global const float* b,
    __global float* result,
    const int length)
{
    int gid = get_global_id(0);
    if (gid < length)
    {
        result[gid] = a[gid] + b[gid];
    }
}";

var definition = new KernelDefinition
{
    Name = "VectorAdd",
    Source = kernelSource,
    EntryPoint = "VectorAdd"
};

// Compile kernel
var kernel = await accelerator.CompileKernelAsync(definition);

// Allocate memory
const int length = 1_000_000;
var inputA = await accelerator.AllocateAsync<float>((nuint)length);
var inputB = await accelerator.AllocateAsync<float>((nuint)length);
var output = await accelerator.AllocateAsync<float>((nuint)length);

// Fill input data
var dataA = Enumerable.Range(0, length).Select(i => (float)i).ToArray();
var dataB = Enumerable.Range(0, length).Select(i => (float)i * 2).ToArray();

await inputA.CopyFromAsync(dataA);
await inputB.CopyFromAsync(dataB);

// Execute kernel
var stopwatch = Stopwatch.StartNew();

await kernel.ExecuteAsync(
    globalWorkSize: length,
    localWorkSize: null, // Automatic
    inputA, inputB, output, length);

await accelerator.SynchronizeAsync();
stopwatch.Stop();

// Get results
var results = new float[length];
await output.CopyToAsync(results);

// Verify
bool correct = true;
for (int i = 0; i < Math.Min(10, length); i++)
{
    float expected = dataA[i] + dataB[i];
    if (Math.Abs(results[i] - expected) > 0.001f)
    {
        correct = false;
        Console.WriteLine($"Mismatch at {i}: {results[i]} != {expected}");
    }
}

Console.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds} ms");
Console.WriteLine($"Results: {(correct ? "✓ Verified" : "✗ Failed")}");

// Cleanup
await inputA.DisposeAsync();
await inputB.DisposeAsync();
await output.DisposeAsync();
accelerator.Dispose();
```

### 02-MatrixMultiply

#### Optimized Kernel
```opencl
__kernel void MatrixMultiplyTiled(
    __global const float* A,
    __global const float* B,
    __global float* C,
    const int N,
    __local float* tileA,
    __local float* tileB)
{
    const int TILE_SIZE = 16;

    int row = get_global_id(1);
    int col = get_global_id(0);
    int localRow = get_local_id(1);
    int localCol = get_local_id(0);

    float sum = 0.0f;

    int numTiles = N / TILE_SIZE;

    for (int t = 0; t < numTiles; t++)
    {
        // Load tile into local memory
        tileA[localRow * TILE_SIZE + localCol] =
            A[row * N + (t * TILE_SIZE + localCol)];
        tileB[localRow * TILE_SIZE + localCol] =
            B[(t * TILE_SIZE + localRow) * N + col];

        barrier(CLK_LOCAL_MEM_FENCE);

        // Compute partial sum using local memory
        for (int k = 0; k < TILE_SIZE; k++)
        {
            sum += tileA[localRow * TILE_SIZE + k] *
                   tileB[k * TILE_SIZE + localCol];
        }

        barrier(CLK_LOCAL_MEM_FENCE);
    }

    C[row * N + col] = sum;
}
```

#### Execution with Local Memory
```csharp
const int tileSize = 16;
const int N = 1024;

var execution = accelerator.CreateExecution(kernel)
    .WithArgument(matrixA)
    .WithArgument(matrixB)
    .WithArgument(matrixC)
    .WithArgument(N)
    .WithLocalMemory(tileSize * tileSize * sizeof(float))
    .WithLocalMemory(tileSize * tileSize * sizeof(float))
    .WithGlobalWorkSize(N, N)
    .WithLocalWorkSize(tileSize, tileSize);

await execution.ExecuteAsync();
```

### 03-ImageProcessing

#### Gaussian Blur Kernel
```opencl
__constant sampler_t sampler = CLK_NORMALIZED_COORDS_FALSE |
                                CLK_ADDRESS_CLAMP_TO_EDGE |
                                CLK_FILTER_LINEAR;

__kernel void GaussianBlur(
    __read_only image2d_t input,
    __write_only image2d_t output,
    __constant float* kernel,
    const int kernelSize)
{
    int2 coord = (int2)(get_global_id(0), get_global_id(1));

    float4 sum = (float4)(0.0f);
    int halfSize = kernelSize / 2;

    for (int y = -halfSize; y <= halfSize; y++)
    {
        for (int x = -halfSize; x <= halfSize; x++)
        {
            int2 offset = (int2)(x, y);
            float weight = kernel[(y + halfSize) * kernelSize + (x + halfSize)];

            float4 pixel = read_imagef(input, sampler, coord + offset);
            sum += pixel * weight;
        }
    }

    write_imagef(output, coord, sum);
}
```

### 05-Profiling

#### Performance Analysis
```csharp
using DotCompute.Backends.OpenCL.Profiling;

var profiler = new OpenCLProfiler(context, logger);

// Begin session
var session = profiler.BeginSession("MatrixMultiply");

// Execute with profiling
await inputBuffer.CopyFromAsync(inputData);
await kernel.ExecuteAsync();
await outputBuffer.CopyToAsync(outputData);

// End session and analyze
var stats = profiler.EndSession(session);

Console.WriteLine($"\nPerformance Analysis:");
Console.WriteLine($"  Total time: {stats.ExecutionTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"  Kernel time: {stats.KernelTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"  Memory H→D: {stats.HostToDeviceTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"  Memory D→H: {stats.DeviceToHostTime.TotalMilliseconds:F3} ms");
Console.WriteLine($"  Queue overhead: {stats.QueuedTime.TotalMilliseconds:F3} ms");

// Calculate efficiency
var computeRatio = stats.KernelTime / stats.ExecutionTime;
Console.WriteLine($"  Compute efficiency: {computeRatio:P1}");

// Throughput calculation
var dataSize = inputData.Length * sizeof(float) * 3; // Read A, B, Write C
var throughputGBps = dataSize / stats.ExecutionTime.TotalSeconds / (1024 * 1024 * 1024);
Console.WriteLine($"  Throughput: {throughputGBps:F2} GB/s");
```

## Learning Path

1. **Start with 01-VectorOperations**
   - Understand basic OpenCL workflow
   - Learn memory management
   - Execute simple kernels

2. **Progress to 02-MatrixMultiply**
   - Learn work group optimization
   - Understand local memory
   - Implement tiled algorithms

3. **Explore 03-ImageProcessing**
   - Work with 2D data
   - Use image memory objects
   - Apply domain-specific optimizations

4. **Master 04-MultiDevice**
   - Scale across devices
   - Handle heterogeneous systems
   - Implement load balancing

5. **Optimize with 05-Profiling**
   - Measure performance
   - Identify bottlenecks
   - Apply optimizations

## Additional Resources

- [OpenCL Getting Started](../../docs/backends/OpenCL-GettingStarted.md)
- [OpenCL Performance Guide](../../docs/backends/OpenCL-Performance.md)
- [OpenCL Architecture](../../docs/backends/OpenCL-Architecture.md)
- [Backend Comparison](../../docs/backends/Backend-Comparison.md)

## Contributing

We welcome contributions! To add a new sample:

1. Create a new directory with a descriptive name
2. Include a README.md explaining the sample
3. Add inline comments explaining key concepts
4. Ensure the sample builds and runs
5. Update this README with the new sample

## License

All samples are licensed under the MIT License. See LICENSE file for details.
