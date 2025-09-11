# DotCompute Examples

Practical code examples demonstrating common DotCompute usage patterns and scenarios for real-world applications.

## Table of Contents

- [Basic Vector Operations](#basic-vector-operations)
- [Matrix Multiplication](#matrix-multiplication)
- [Image Processing](#image-processing)
- [Parallel Algorithms](#parallel-algorithms)
- [Custom Kernels](#custom-kernels)
- [Memory Management Patterns](#memory-management-patterns)
- [Performance Optimization](#performance-optimization)
- [Error Handling](#error-handling)
- [Integration Examples](#integration-examples)

## Basic Vector Operations

### Vector Addition with Automatic Backend Selection

```csharp
using DotCompute.Generators.Kernel.Attributes;
using DotCompute.Core.Kernels;
using DotCompute.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

public static class VectorKernels
{
    [Kernel]
    public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] + b[idx];
        }
    }

    [Kernel]
    public static void Multiply(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length)
        {
            result[idx] = a[idx] * b[idx];
        }
    }

    [Kernel]
    public static void ScalarMultiply(ReadOnlySpan<float> input, Span<float> output, float scalar)
    {
        int idx = Kernel.ThreadId.X;
        if (idx < output.Length)
        {
            output[idx] = input[idx] * scalar;
        }
    }
}

// Usage
public class VectorOperationsExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public VectorOperationsExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task RunVectorOperations()
    {
        const int size = 1_000_000;
        var a = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var b = Enumerable.Range(0, size).Select(i => (float)(i * 2)).ToArray();
        var result = new float[size];

        // Vector addition - automatically selects best backend
        await _orchestrator.ExecuteAsync<object>("VectorKernels.Add", a, b, result);
        Console.WriteLine($"First elements: {a[0]} + {b[0]} = {result[0]}");

        // Scalar multiplication
        await _orchestrator.ExecuteAsync<object>("VectorKernels.ScalarMultiply", result, result, 0.5f);
        Console.WriteLine($"After scalar multiply: {result[0]}");

        // Chain operations efficiently
        await _orchestrator.ExecuteAsync<object>("VectorKernels.Multiply", a, b, result);
        await _orchestrator.ExecuteAsync<object>("VectorKernels.ScalarMultiply", result, result, 2.0f);
        Console.WriteLine($"Final result: {result[0]}");
    }
}
```

### Vector Reduction (Sum, Max, Min)

```csharp
public static class ReductionKernels
{
    [Kernel]
    public static void PartialSum(ReadOnlySpan<float> input, Span<float> partialSums, int elementsPerThread)
    {
        int tid = Kernel.ThreadId.X;
        int startIdx = tid * elementsPerThread;
        int endIdx = Math.Min(startIdx + elementsPerThread, input.Length);

        float sum = 0.0f;
        for (int i = startIdx; i < endIdx; i++)
        {
            sum += input[i];
        }

        if (tid < partialSums.Length)
        {
            partialSums[tid] = sum;
        }
    }

    [Kernel]
    public static void FindMax(ReadOnlySpan<float> input, Span<float> partialMaxes, int elementsPerThread)
    {
        int tid = Kernel.ThreadId.X;
        int startIdx = tid * elementsPerThread;
        int endIdx = Math.Min(startIdx + elementsPerThread, input.Length);

        float maxVal = float.NegativeInfinity;
        for (int i = startIdx; i < endIdx; i++)
        {
            if (input[i] > maxVal)
            {
                maxVal = input[i];
            }
        }

        if (tid < partialMaxes.Length)
        {
            partialMaxes[tid] = maxVal;
        }
    }
}

public class ReductionExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public ReductionExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<float> ParallelSum(float[] data)
    {
        const int threadsPerBlock = 256;
        int elementsPerThread = (data.Length + threadsPerBlock - 1) / threadsPerBlock;
        var partialSums = new float[threadsPerBlock];

        // First reduction phase on GPU/CPU
        await _orchestrator.ExecuteAsync<object>("ReductionKernels.PartialSum", 
            data, partialSums, elementsPerThread);

        // Final reduction on CPU (small array)
        return partialSums.Sum();
    }

    public async Task<float> ParallelMax(float[] data)
    {
        const int threadsPerBlock = 256;
        int elementsPerThread = (data.Length + threadsPerBlock - 1) / threadsPerBlock;
        var partialMaxes = new float[threadsPerBlock];

        await _orchestrator.ExecuteAsync<object>("ReductionKernels.FindMax", 
            data, partialMaxes, elementsPerThread);

        return partialMaxes.Max();
    }
}
```

## Matrix Multiplication

### Optimized Matrix Multiplication with Tiling

```csharp
public static class MatrixKernels
{
    [Kernel(WorkGroupSizeX = 16, WorkGroupSizeY = 16)]
    public static void MatrixMultiplyTiled(
        ReadOnlySpan<float> a, 
        ReadOnlySpan<float> b, 
        Span<float> c,
        int rowsA, 
        int colsA, 
        int colsB)
    {
        int row = Kernel.BlockId.Y * Kernel.BlockDim.Y + Kernel.ThreadId.Y;
        int col = Kernel.BlockId.X * Kernel.BlockDim.X + Kernel.ThreadId.X;

        if (row < rowsA && col < colsB)
        {
            float sum = 0.0f;
            
            // Perform dot product
            for (int k = 0; k < colsA; k++)
            {
                sum += a[row * colsA + k] * b[k * colsB + col];
            }
            
            c[row * colsB + col] = sum;
        }
    }

    [Kernel]
    public static void MatrixMultiplySimple(
        ReadOnlySpan<float> a, 
        ReadOnlySpan<float> b, 
        Span<float> c,
        int size)
    {
        int row = Kernel.ThreadId.Y;
        int col = Kernel.ThreadId.X;

        if (row < size && col < size)
        {
            float sum = 0.0f;
            for (int k = 0; k < size; k++)
            {
                sum += a[row * size + k] * b[k * size + col];
            }
            c[row * size + col] = sum;
        }
    }
}

public class MatrixMultiplicationExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public MatrixMultiplicationExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<float[,]> MultiplyMatrices(float[,] matrixA, float[,] matrixB)
    {
        int rowsA = matrixA.GetLength(0);
        int colsA = matrixA.GetLength(1);
        int rowsB = matrixB.GetLength(0);
        int colsB = matrixB.GetLength(1);

        if (colsA != rowsB)
        {
            throw new ArgumentException("Matrix dimensions don't match for multiplication");
        }

        // Flatten matrices for kernel processing
        var flatA = new float[rowsA * colsA];
        var flatB = new float[rowsB * colsB];
        var flatC = new float[rowsA * colsB];

        // Copy data
        Buffer.BlockCopy(matrixA, 0, flatA, 0, flatA.Length * sizeof(float));
        Buffer.BlockCopy(matrixB, 0, flatB, 0, flatB.Length * sizeof(float));

        // Execute matrix multiplication
        await _orchestrator.ExecuteAsync<object>("MatrixKernels.MatrixMultiplyTiled",
            flatA, flatB, flatC, rowsA, colsA, colsB);

        // Convert back to 2D array
        var result = new float[rowsA, colsB];
        for (int i = 0; i < rowsA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                result[i, j] = flatC[i * colsB + j];
            }
        }

        return result;
    }

    public async Task BenchmarkMatrixSizes()
    {
        var sizes = new[] { 128, 256, 512, 1024, 2048 };
        
        foreach (var size in sizes)
        {
            var a = CreateRandomMatrix(size, size);
            var b = CreateRandomMatrix(size, size);
            
            var stopwatch = Stopwatch.StartNew();
            var result = await MultiplyMatrices(a, b);
            stopwatch.Stop();
            
            var operations = 2L * size * size * size; // FLOPs
            var gflops = operations / (stopwatch.Elapsed.TotalSeconds * 1e9);
            
            Console.WriteLine($"Size {size}x{size}: {stopwatch.ElapsedMilliseconds}ms, {gflops:F2} GFLOPS");
        }
    }

    private static float[,] CreateRandomMatrix(int rows, int cols)
    {
        var matrix = new float[rows, cols];
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                matrix[i, j] = (float)(random.NextDouble() * 2.0 - 1.0);
            }
        }
        
        return matrix;
    }
}
```

## Image Processing

### Image Convolution and Filtering

```csharp
public static class ImageKernels
{
    [Kernel]
    public static void GaussianBlur(
        ReadOnlySpan<float> input,
        Span<float> output,
        ReadOnlySpan<float> kernel,
        int width,
        int height,
        int kernelSize)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x >= width || y >= height) return;

        int radius = kernelSize / 2;
        float sum = 0.0f;
        float weightSum = 0.0f;

        for (int ky = -radius; ky <= radius; ky++)
        {
            for (int kx = -radius; kx <= radius; kx++)
            {
                int px = Math.Clamp(x + kx, 0, width - 1);
                int py = Math.Clamp(y + ky, 0, height - 1);
                
                int kernelIdx = (ky + radius) * kernelSize + (kx + radius);
                float weight = kernel[kernelIdx];
                
                sum += input[py * width + px] * weight;
                weightSum += weight;
            }
        }

        output[y * width + x] = sum / weightSum;
    }

    [Kernel]
    public static void SobelEdgeDetection(
        ReadOnlySpan<float> input,
        Span<float> output,
        int width,
        int height)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x >= width || y >= height || x == 0 || y == 0 || x == width - 1 || y == height - 1)
        {
            if (x < width && y < height)
            {
                output[y * width + x] = 0.0f;
            }
            return;
        }

        // Sobel X kernel
        float gx = (-1 * input[(y - 1) * width + (x - 1)]) +
                   (-2 * input[y * width + (x - 1)]) +
                   (-1 * input[(y + 1) * width + (x - 1)]) +
                   (1 * input[(y - 1) * width + (x + 1)]) +
                   (2 * input[y * width + (x + 1)]) +
                   (1 * input[(y + 1) * width + (x + 1)]);

        // Sobel Y kernel
        float gy = (-1 * input[(y - 1) * width + (x - 1)]) +
                   (-2 * input[(y - 1) * width + x]) +
                   (-1 * input[(y - 1) * width + (x + 1)]) +
                   (1 * input[(y + 1) * width + (x - 1)]) +
                   (2 * input[(y + 1) * width + x]) +
                   (1 * input[(y + 1) * width + (x + 1)]);

        float magnitude = MathF.Sqrt(gx * gx + gy * gy);
        output[y * width + x] = Math.Clamp(magnitude, 0.0f, 1.0f);
    }

    [Kernel]
    public static void RGBToGrayscale(
        ReadOnlySpan<float> rgb,
        Span<float> grayscale,
        int width,
        int height)
    {
        int idx = Kernel.ThreadId.X;
        int totalPixels = width * height;

        if (idx >= totalPixels) return;

        int rgbIdx = idx * 3; // RGB has 3 components per pixel
        
        // Standard luminance formula
        float gray = 0.299f * rgb[rgbIdx] +     // Red
                     0.587f * rgb[rgbIdx + 1] + // Green
                     0.114f * rgb[rgbIdx + 2];  // Blue

        grayscale[idx] = gray;
    }
}

public class ImageProcessingExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public ImageProcessingExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<float[]> ProcessImage(float[] imageData, int width, int height, ImageFilter filter)
    {
        var output = new float[imageData.Length];

        switch (filter)
        {
            case ImageFilter.GaussianBlur:
                var gaussianKernel = CreateGaussianKernel(5, 1.0f);
                await _orchestrator.ExecuteAsync<object>("ImageKernels.GaussianBlur",
                    imageData, output, gaussianKernel, width, height, 5);
                break;

            case ImageFilter.EdgeDetection:
                await _orchestrator.ExecuteAsync<object>("ImageKernels.SobelEdgeDetection",
                    imageData, output, width, height);
                break;

            case ImageFilter.Grayscale:
                var grayscaleOutput = new float[width * height]; // Single channel
                await _orchestrator.ExecuteAsync<object>("ImageKernels.RGBToGrayscale",
                    imageData, grayscaleOutput, width, height);
                return grayscaleOutput;

            default:
                throw new ArgumentException("Unknown filter type");
        }

        return output;
    }

    public async Task ProcessImageBatch(string[] imagePaths, ImageFilter filter)
    {
        var tasks = imagePaths.Select(async path =>
        {
            var (data, width, height) = LoadImage(path);
            var processed = await ProcessImage(data, width, height, filter);
            var outputPath = Path.ChangeExtension(path, $".{filter.ToString().ToLower()}.png");
            SaveImage(processed, width, height, outputPath);
            return outputPath;
        });

        var results = await Task.WhenAll(tasks);
        Console.WriteLine($"Processed {results.Length} images with {filter} filter");
    }

    private static float[] CreateGaussianKernel(int size, float sigma)
    {
        var kernel = new float[size * size];
        int radius = size / 2;
        float sum = 0.0f;

        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                float value = MathF.Exp(-(x * x + y * y) / (2 * sigma * sigma));
                kernel[(y + radius) * size + (x + radius)] = value;
                sum += value;
            }
        }

        // Normalize
        for (int i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= sum;
        }

        return kernel;
    }

    // Placeholder methods for image I/O
    private (float[] data, int width, int height) LoadImage(string path)
    {
        // Implementation would load actual image file
        return (new float[0], 0, 0);
    }

    private void SaveImage(float[] data, int width, int height, string path)
    {
        // Implementation would save processed image
    }
}

public enum ImageFilter
{
    GaussianBlur,
    EdgeDetection,
    Grayscale
}
```

## Parallel Algorithms

### Parallel Sort (Bitonic Sort)

```csharp
public static class SortingKernels
{
    [Kernel]
    public static void BitonicSort(Span<float> data, int stage, int step)
    {
        int idx = Kernel.ThreadId.X;
        int dataSize = data.Length;

        if (idx >= dataSize / 2) return;

        int distance = 1 << (step - 1);
        int blockSize = 1 << stage;
        
        int leftIdx = ((idx / distance) * distance * 2) + (idx % distance);
        int rightIdx = leftIdx + distance;

        if (rightIdx < dataSize)
        {
            bool ascending = ((leftIdx / blockSize) % 2) == 0;
            
            if ((data[leftIdx] > data[rightIdx]) == ascending)
            {
                // Swap elements
                (data[leftIdx], data[rightIdx]) = (data[rightIdx], data[leftIdx]);
            }
        }
    }

    [Kernel]
    public static void ParallelQuicksortPartition(
        Span<float> data,
        Span<int> indices,
        int left,
        int right,
        float pivot)
    {
        int idx = Kernel.ThreadId.X + left;
        
        if (idx > right) return;

        // Mark elements less than pivot
        indices[idx] = (data[idx] < pivot) ? 1 : 0;
    }
}

public class ParallelSortExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public ParallelSortExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<float[]> BitonicSort(float[] input)
    {
        var data = input.ToArray(); // Create copy
        int n = data.Length;

        // Ensure length is power of 2
        if ((n & (n - 1)) != 0)
        {
            throw new ArgumentException("Array length must be power of 2 for bitonic sort");
        }

        int stages = (int)Math.Log2(n);

        for (int stage = 1; stage <= stages; stage++)
        {
            for (int step = stage; step >= 1; step--)
            {
                await _orchestrator.ExecuteAsync<object>("SortingKernels.BitonicSort",
                    data, stage, step);
            }
        }

        return data;
    }

    public async Task BenchmarkSorting()
    {
        var sizes = new[] { 1024, 4096, 16384, 65536 };
        
        foreach (var size in sizes)
        {
            var data = GenerateRandomData(size);
            
            var stopwatch = Stopwatch.StartNew();
            var sorted = await BitonicSort(data);
            stopwatch.Stop();

            bool isSorted = IsSorted(sorted);
            Console.WriteLine($"Size {size}: {stopwatch.ElapsedMilliseconds}ms, Sorted: {isSorted}");
        }
    }

    private static float[] GenerateRandomData(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(0, size)
            .Select(_ => (float)random.NextDouble())
            .ToArray();
    }

    private static bool IsSorted(float[] array)
    {
        for (int i = 1; i < array.Length; i++)
        {
            if (array[i] < array[i - 1])
                return false;
        }
        return true;
    }
}
```

### Monte Carlo Pi Estimation

```csharp
public static class MonteCarloKernels
{
    [Kernel]
    public static void EstimatePi(
        ReadOnlySpan<float> randomX,
        ReadOnlySpan<float> randomY,
        Span<int> results)
    {
        int idx = Kernel.ThreadId.X;
        
        if (idx >= randomX.Length) return;

        float x = randomX[idx];
        float y = randomY[idx];
        
        // Check if point is inside unit circle
        float distanceSquared = x * x + y * y;
        results[idx] = (distanceSquared <= 1.0f) ? 1 : 0;
    }

    [Kernel]
    public static void GenerateRandomNumbers(
        Span<float> output,
        int seed,
        int offset)
    {
        int idx = Kernel.ThreadId.X;
        
        if (idx >= output.Length) return;

        // Simple linear congruential generator
        int localSeed = seed + idx + offset;
        localSeed = (localSeed * 1103515245 + 12345) & 0x7fffffff;
        
        output[idx] = (float)localSeed / (float)0x7fffffff;
    }
}

public class MonteCarloExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public MonteCarloExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<double> EstimatePi(int totalSamples)
    {
        const int batchSize = 1_000_000;
        int batches = (totalSamples + batchSize - 1) / batchSize;
        long totalHits = 0;
        int processedSamples = 0;

        var random = new Random();

        for (int batch = 0; batch < batches; batch++)
        {
            int currentBatchSize = Math.Min(batchSize, totalSamples - processedSamples);
            
            var randomX = new float[currentBatchSize];
            var randomY = new float[currentBatchSize];
            var results = new int[currentBatchSize];

            // Generate random numbers on GPU
            await _orchestrator.ExecuteAsync<object>("MonteCarloKernels.GenerateRandomNumbers",
                randomX, random.Next(), batch * batchSize);
            await _orchestrator.ExecuteAsync<object>("MonteCarloKernels.GenerateRandomNumbers",
                randomY, random.Next(), batch * batchSize + currentBatchSize);

            // Estimate Pi for this batch
            await _orchestrator.ExecuteAsync<object>("MonteCarloKernels.EstimatePi",
                randomX, randomY, results);

            // Sum results
            int batchHits = results.Sum();
            totalHits += batchHits;
            processedSamples += currentBatchSize;

            if (batch % 10 == 0)
            {
                double currentEstimate = 4.0 * totalHits / processedSamples;
                Console.WriteLine($"Batch {batch}: {currentEstimate:F6} (processed {processedSamples:N0} samples)");
            }
        }

        return 4.0 * totalHits / totalSamples;
    }

    public async Task RunConvergenceAnalysis()
    {
        var sampleCounts = new[] { 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000 };
        
        Console.WriteLine("Monte Carlo Pi Estimation Convergence Analysis");
        Console.WriteLine("Samples\t\tEstimate\tError\t\tTime");
        
        foreach (var samples in sampleCounts)
        {
            var stopwatch = Stopwatch.StartNew();
            var estimate = await EstimatePi(samples);
            stopwatch.Stop();
            
            var error = Math.Abs(estimate - Math.PI);
            
            Console.WriteLine($"{samples:N0}\t\t{estimate:F6}\t{error:F6}\t{stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
```

## Custom Kernels

### Creating Domain-Specific Kernels

```csharp
// Financial calculations example
public static class FinancialKernels
{
    [Kernel]
    public static void BlackScholesOptionPricing(
        ReadOnlySpan<float> spotPrices,
        ReadOnlySpan<float> strikePrices,
        ReadOnlySpan<float> timeToExpiry,
        Span<float> callPrices,
        Span<float> putPrices,
        float riskFreeRate,
        float volatility)
    {
        int idx = Kernel.ThreadId.X;
        
        if (idx >= spotPrices.Length) return;

        float S = spotPrices[idx];
        float K = strikePrices[idx];
        float T = timeToExpiry[idx];
        float r = riskFreeRate;
        float sigma = volatility;

        float d1 = (MathF.Log(S / K) + (r + 0.5f * sigma * sigma) * T) / (sigma * MathF.Sqrt(T));
        float d2 = d1 - sigma * MathF.Sqrt(T);

        float Nd1 = NormalCDF(d1);
        float Nd2 = NormalCDF(d2);
        float NegD1 = NormalCDF(-d1);
        float NegD2 = NormalCDF(-d2);

        callPrices[idx] = S * Nd1 - K * MathF.Exp(-r * T) * Nd2;
        putPrices[idx] = K * MathF.Exp(-r * T) * NegD2 - S * NegD1;
    }

    private static float NormalCDF(float x)
    {
        // Abramowitz and Stegun approximation
        float a1 = 0.254829592f;
        float a2 = -0.284496736f;
        float a3 = 1.421413741f;
        float a4 = -1.453152027f;
        float a5 = 1.061405429f;
        float p = 0.3275911f;

        int sign = x < 0 ? -1 : 1;
        x = MathF.Abs(x) / MathF.Sqrt(2.0f);

        float t = 1.0f / (1.0f + p * x);
        float y = 1.0f - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * MathF.Exp(-x * x);

        return 0.5f * (1.0f + sign * y);
    }
}

// Scientific computing example
public static class ScienceKernels
{
    [Kernel]
    public static void HeatEquation2D(
        ReadOnlySpan<float> currentTemp,
        Span<float> nextTemp,
        int width,
        int height,
        float alpha,
        float deltaTime,
        float deltaX)
    {
        int x = Kernel.ThreadId.X;
        int y = Kernel.ThreadId.Y;

        if (x == 0 || x >= width - 1 || y == 0 || y >= height - 1) return;

        int idx = y * width + x;
        float current = currentTemp[idx];
        
        // Calculate Laplacian using finite differences
        float laplacian = (currentTemp[idx - 1] + currentTemp[idx + 1] +
                          currentTemp[idx - width] + currentTemp[idx + width] - 4 * current) 
                          / (deltaX * deltaX);

        // Update temperature using explicit Euler method
        nextTemp[idx] = current + alpha * deltaTime * laplacian;
    }

    [Kernel]
    public static void FFT1D_Radix2(
        Span<float> realPart,
        Span<float> imagPart,
        int n,
        int stage,
        bool inverse)
    {
        int idx = Kernel.ThreadId.X;
        int groupSize = 1 << stage;
        int groupCount = n / (groupSize * 2);
        
        if (idx >= groupCount * groupSize) return;

        int groupIdx = idx / groupSize;
        int elemIdx = idx % groupSize;
        
        int i = groupIdx * groupSize * 2 + elemIdx;
        int j = i + groupSize;

        // Twiddle factor
        float angle = (inverse ? 2.0f : -2.0f) * MathF.PI * elemIdx / (groupSize * 2);
        float twiddleReal = MathF.Cos(angle);
        float twiddleImag = MathF.Sin(angle);

        // Butterfly operation
        float tempReal = realPart[j] * twiddleReal - imagPart[j] * twiddleImag;
        float tempImag = realPart[j] * twiddleImag + imagPart[j] * twiddleReal;

        realPart[j] = realPart[i] - tempReal;
        imagPart[j] = imagPart[i] - tempImag;
        realPart[i] = realPart[i] + tempReal;
        imagPart[i] = imagPart[i] + tempImag;
    }
}

public class CustomKernelExample
{
    private readonly IComputeOrchestrator _orchestrator;

    public CustomKernelExample(IComputeOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task<(float[] calls, float[] puts)> PriceOptions(
        float[] spots, float[] strikes, float[] times, 
        float rate, float vol)
    {
        var calls = new float[spots.Length];
        var puts = new float[spots.Length];

        await _orchestrator.ExecuteAsync<object>("FinancialKernels.BlackScholesOptionPricing",
            spots, strikes, times, calls, puts, rate, vol);

        return (calls, puts);
    }

    public async Task<float[,]> SimulateHeatDiffusion(
        float[,] initialTemp, float alpha, float deltaTime, float deltaX, int timeSteps)
    {
        int height = initialTemp.GetLength(0);
        int width = initialTemp.GetLength(1);
        
        var current = new float[height * width];
        var next = new float[height * width];

        // Flatten initial conditions
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                current[y * width + x] = initialTemp[y, x];
            }
        }

        // Time stepping
        for (int step = 0; step < timeSteps; step++)
        {
            await _orchestrator.ExecuteAsync<object>("ScienceKernels.HeatEquation2D",
                current, next, width, height, alpha, deltaTime, deltaX);

            // Swap arrays
            (current, next) = (next, current);
        }

        // Convert back to 2D
        var result = new float[height, width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[y, x] = current[y * width + x];
            }
        }

        return result;
    }
}
```

## Memory Management Patterns

### Efficient Buffer Reuse

```csharp
public class MemoryOptimizedProcessing
{
    private readonly IUnifiedMemoryManager _memoryManager;
    private readonly IComputeOrchestrator _orchestrator;
    private readonly Dictionary<int, IUnifiedMemoryBuffer<float>> _bufferPool = new();

    public MemoryOptimizedProcessing(IUnifiedMemoryManager memoryManager, IComputeOrchestrator orchestrator)
    {
        _memoryManager = memoryManager;
        _orchestrator = orchestrator;
    }

    public async Task ProcessLargeDataset(IEnumerable<float[]> dataChunks)
    {
        const int maxBufferSize = 10_000_000; // 10M elements
        IUnifiedMemoryBuffer<float>? inputBuffer = null;
        IUnifiedMemoryBuffer<float>? outputBuffer = null;

        try
        {
            foreach (var chunk in dataChunks)
            {
                // Reuse or allocate buffers
                inputBuffer ??= await _memoryManager.AllocateAsync<float>(maxBufferSize);
                outputBuffer ??= await _memoryManager.AllocateAsync<float>(maxBufferSize);

                // Process chunk
                await ProcessChunk(chunk, inputBuffer, outputBuffer);
            }
        }
        finally
        {
            inputBuffer?.Dispose();
            outputBuffer?.Dispose();
        }
    }

    private async Task ProcessChunk(float[] data, IUnifiedMemoryBuffer<float> inputBuffer, IUnifiedMemoryBuffer<float> outputBuffer)
    {
        // Copy data to buffer
        await inputBuffer.WriteAsync(data);

        // Execute processing kernel
        await _orchestrator.ExecuteWithBuffersAsync<object>("ProcessingKernel",
            new[] { inputBuffer, outputBuffer });

        // Read results if needed
        var results = await outputBuffer.ReadAsync(0, data.Length);
        
        // Process results...
    }

    // Buffer pool for frequent allocations
    public async Task<IUnifiedMemoryBuffer<T>> GetPooledBuffer<T>(int size) where T : unmanaged
    {
        if (_bufferPool.TryGetValue(size, out var buffer) && buffer is IUnifiedMemoryBuffer<T> typedBuffer)
        {
            _bufferPool.Remove(size);
            return typedBuffer;
        }

        return await _memoryManager.AllocateAsync<T>(size);
    }

    public void ReturnToPool<T>(IUnifiedMemoryBuffer<T> buffer) where T : unmanaged
    {
        if (!buffer.IsDisposed)
        {
            _bufferPool[buffer.Length] = (IUnifiedMemoryBuffer<float>)(object)buffer;
        }
    }
}
```

## Performance Optimization

### Benchmarking and Profiling

```csharp
public class PerformanceBenchmark
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly IKernelProfiler _profiler;

    public PerformanceBenchmark(IComputeOrchestrator orchestrator, IKernelProfiler profiler)
    {
        _orchestrator = orchestrator;
        _profiler = profiler;
    }

    public async Task BenchmarkKernelPerformance(string kernelName, object[] args, int iterations = 100)
    {
        // Warmup
        for (int i = 0; i < 5; i++)
        {
            await _orchestrator.ExecuteAsync<object>(kernelName, args);
        }

        // Benchmark
        var times = new List<TimeSpan>();
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await _orchestrator.ExecuteAsync<object>(kernelName, args);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        // Statistics
        var avgTime = TimeSpan.FromTicks((long)times.Average(t => t.Ticks));
        var minTime = times.Min();
        var maxTime = times.Max();
        var stdDev = CalculateStandardDeviation(times);

        Console.WriteLine($"Kernel: {kernelName}");
        Console.WriteLine($"Iterations: {iterations}");
        Console.WriteLine($"Average: {avgTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Min: {minTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Max: {maxTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"StdDev: {stdDev.TotalMilliseconds:F2}ms");

        // Get profiler metrics
        var metrics = await _profiler.GetHistoricalMetricsAsync(kernelName, TimeSpan.FromMinutes(5));
        if (metrics.Any())
        {
            var latest = metrics.Last();
            Console.WriteLine($"Throughput: {latest.Throughput:F0} elements/sec");
            Console.WriteLine($"Memory Used: {latest.MemoryUsed / 1024 / 1024:F1} MB");
        }
    }

    public async Task CompareBackends(string kernelName, object[] args)
    {
        var accelerators = await _orchestrator.GetSupportedAcceleratorsAsync(kernelName);

        Console.WriteLine($"Backend Comparison for {kernelName}:");
        Console.WriteLine("Backend\t\tTime\t\tThroughput");

        foreach (var accelerator in accelerators)
        {
            var stopwatch = Stopwatch.StartNew();
            await _orchestrator.ExecuteAsync<object>(kernelName, accelerator, args);
            stopwatch.Stop();

            var elementCount = GetElementCount(args);
            var throughput = elementCount / stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine($"{accelerator.Type}\t\t{stopwatch.ElapsedMilliseconds}ms\t\t{throughput:F0} elem/sec");
        }
    }

    private static TimeSpan CalculateStandardDeviation(IEnumerable<TimeSpan> times)
    {
        var timesArray = times.ToArray();
        var avgTicks = timesArray.Average(t => t.Ticks);
        var variance = timesArray.Average(t => Math.Pow(t.Ticks - avgTicks, 2));
        return TimeSpan.FromTicks((long)Math.Sqrt(variance));
    }

    private static int GetElementCount(object[] args)
    {
        return args.OfType<Array>().FirstOrDefault()?.Length ?? 1;
    }
}
```

## Error Handling

### Robust Error Handling and Recovery

```csharp
public class RobustKernelExecutor
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<RobustKernelExecutor> _logger;

    public RobustKernelExecutor(IComputeOrchestrator orchestrator, ILogger<RobustKernelExecutor> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<TResult> ExecuteWithFallback<TResult>(
        string kernelName, 
        object[] args, 
        params string[] preferredBackends)
    {
        var exceptions = new List<Exception>();

        // Try preferred backends in order
        foreach (var backend in preferredBackends)
        {
            try
            {
                _logger.LogDebug("Attempting execution on {Backend}", backend);
                return await _orchestrator.ExecuteAsync<TResult>(kernelName, backend, args);
            }
            catch (AcceleratorNotAvailableException ex)
            {
                _logger.LogWarning("Backend {Backend} not available: {Message}", backend, ex.Message);
                exceptions.Add(ex);
            }
            catch (KernelCompilationException ex)
            {
                _logger.LogError("Compilation failed on {Backend}: {Message}", backend, ex.Message);
                exceptions.Add(ex);
            }
            catch (MemoryAllocationException ex)
            {
                _logger.LogWarning("Memory allocation failed on {Backend}, trying next: {Message}", backend, ex.Message);
                exceptions.Add(ex);
            }
        }

        // Try automatic backend selection as final fallback
        try
        {
            _logger.LogInformation("Falling back to automatic backend selection");
            return await _orchestrator.ExecuteAsync<TResult>(kernelName, args);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
            _logger.LogError("All execution attempts failed");
            throw new AggregateException("Kernel execution failed on all backends", exceptions);
        }
    }

    public async Task<TResult> ExecuteWithRetry<TResult>(
        string kernelName,
        object[] args,
        int maxRetries = 3,
        TimeSpan? delay = null)
    {
        delay ??= TimeSpan.FromMilliseconds(100);
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await _orchestrator.ExecuteAsync<TResult>(kernelName, args);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                _logger.LogWarning("Attempt {Attempt} failed, retrying in {Delay}ms: {Message}", 
                    attempt + 1, delay.Value.TotalMilliseconds, ex.Message);
                
                await Task.Delay(delay.Value);
                delay = TimeSpan.FromMilliseconds(delay.Value.TotalMilliseconds * 2); // Exponential backoff
            }
        }

        throw new InvalidOperationException($"Kernel execution failed after {maxRetries + 1} attempts");
    }

    private static bool IsTransientError(Exception ex)
    {
        return ex is MemoryAllocationException ||
               ex is TimeoutException ||
               (ex is KernelExecutionException kex && kex.Message.Contains("temporary"));
    }
}
```

## Integration Examples

### ASP.NET Core Integration

```csharp
// Startup.cs or Program.cs
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add DotCompute services
        services.AddDotComputeRuntime();
        services.AddProductionOptimization();
        services.AddCudaBackend();

        // Register custom services
        services.AddScoped<ImageProcessingService>();
        services.AddScoped<FinancialCalculationService>();
    }
}

// API Controller
[ApiController]
[Route("api/[controller]")]
public class ComputeController : ControllerBase
{
    private readonly ImageProcessingService _imageService;
    private readonly FinancialCalculationService _financialService;

    public ComputeController(ImageProcessingService imageService, FinancialCalculationService financialService)
    {
        _imageService = imageService;
        _financialService = financialService;
    }

    [HttpPost("process-image")]
    public async Task<IActionResult> ProcessImage(IFormFile image, [FromQuery] string filter)
    {
        if (image == null || image.Length == 0)
            return BadRequest("No image provided");

        using var stream = image.OpenReadStream();
        var processedImage = await _imageService.ProcessImageAsync(stream, filter);
        
        return File(processedImage, "image/png");
    }

    [HttpPost("option-pricing")]
    public async Task<IActionResult> PriceOptions([FromBody] OptionPricingRequest request)
    {
        var result = await _financialService.PriceOptionsAsync(request);
        return Ok(result);
    }
}

// Background Service
public class ComputeBackgroundService : BackgroundService
{
    private readonly IComputeOrchestrator _orchestrator;
    private readonly ILogger<ComputeBackgroundService> _logger;

    public ComputeBackgroundService(IComputeOrchestrator orchestrator, ILogger<ComputeBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Precompile frequently used kernels
        await _orchestrator.PrecompileKernelAsync("VectorKernels.Add");
        await _orchestrator.PrecompileKernelAsync("MatrixKernels.MatrixMultiplyTiled");
        
        _logger.LogInformation("Kernels precompiled successfully");

        // Periodic cleanup
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            // Perform cleanup tasks
        }
    }
}
```

### Console Application

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDotComputeRuntime();
        services.AddProductionOptimization();

        var serviceProvider = services.BuildServiceProvider();

        // Get services
        var orchestrator = serviceProvider.GetRequiredService<IComputeOrchestrator>();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting DotCompute example application");

            // Run examples
            await RunVectorOperations(orchestrator);
            await RunMatrixMultiplication(orchestrator);
            await RunImageProcessing(orchestrator);

            logger.LogInformation("All examples completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application failed");
        }
        finally
        {
            await serviceProvider.DisposeAsync();
        }
    }

    static async Task RunVectorOperations(IComputeOrchestrator orchestrator)
    {
        Console.WriteLine("Running vector operations...");
        
        var example = new VectorOperationsExample(orchestrator);
        await example.RunVectorOperations();
        
        Console.WriteLine("Vector operations completed.");
    }

    static async Task RunMatrixMultiplication(IComputeOrchestrator orchestrator)
    {
        Console.WriteLine("Running matrix multiplication...");
        
        var example = new MatrixMultiplicationExample(orchestrator);
        await example.BenchmarkMatrixSizes();
        
        Console.WriteLine("Matrix multiplication completed.");
    }

    static async Task RunImageProcessing(IComputeOrchestrator orchestrator)
    {
        Console.WriteLine("Running image processing...");
        
        var example = new ImageProcessingExample(orchestrator);
        // Process example images if available
        
        Console.WriteLine("Image processing completed.");
    }
}
```

These examples demonstrate the full range of DotCompute capabilities, from basic vector operations to complex scientific computing and real-world application integration. The framework's automatic backend selection, unified memory management, and source generation features make it easy to write high-performance compute code that works efficiently across different hardware platforms.