# DotCompute + ML.NET Integration Sample

This sample demonstrates how to integrate DotCompute GPU acceleration with ML.NET for high-performance machine learning workloads.

## Overview

The integration enables GPU-accelerated operations within ML.NET pipelines:

- **Matrix Operations**: Neural network forward/backward passes
- **Feature Transformation**: Batch normalization, z-score, min-max scaling
- **Distance Calculations**: K-means clustering, KNN classification
- **Custom Transforms**: GPU-accelerated ML.NET IEstimator implementations

## Getting Started

```bash
# Build the sample
dotnet build samples/MLNetIntegration/DotCompute.Samples.MLNet.csproj

# Run the sample
dotnet run --project samples/MLNetIntegration/DotCompute.Samples.MLNet.csproj
```

## Sample Components

### MLNetIntegrationSamples.cs

Demonstrates core integration patterns:

1. **GPU-Accelerated Matrix Operations** - Neural network layer computations
2. **ML.NET Data Pipeline Integration** - Loading and transforming data
3. **Batch Feature Transformation** - Z-score normalization at scale
4. **Distance Calculations** - K-means cluster assignment

### GpuAcceleratedTransformer.cs

Shows how to create a custom ML.NET transformer that uses GPU acceleration:

```csharp
// Create a GPU-accelerated normalization pipeline
var pipeline = mlContext.Transforms.Concatenate("Features", featureColumns)
    .Append(mlContext.Transforms.GpuNormalize("Features", mode: NormalizationMode.ZScore));

// Fit and transform
var model = pipeline.Fit(trainingData);
var transformedData = model.Transform(testData);
```

## Performance Benefits

| Operation | CPU Time | GPU Time (est.) | Speedup |
|-----------|----------|-----------------|---------|
| Matrix Multiply (1024x784x256) | ~50ms | ~5ms | ~10x |
| Batch Normalization (50K samples) | ~30ms | ~2ms | ~15x |
| K-means Assignment (10K x 100) | ~100ms | ~2ms | ~50x |

## Integration Patterns

### Pattern 1: Preprocessing on GPU

```csharp
// Load data
var rawData = LoadData();

// GPU-accelerated preprocessing
using var gpuBuffer = await accelerator.AllocateAsync(rawData);
await normalizeKernel.ExecuteAsync(gpuBuffer);
var normalizedData = await gpuBuffer.CopyToHostAsync();

// Continue with ML.NET
var dataView = mlContext.Data.LoadFromEnumerable(normalizedData);
```

### Pattern 2: Custom GPU Transformer

```csharp
public class GpuPcaEstimator : IEstimator<GpuPcaTransformer>
{
    public GpuPcaTransformer Fit(IDataView input)
    {
        // Extract data for GPU processing
        var matrix = ExtractMatrix(input);

        // Compute covariance on GPU
        var covariance = await gpuCovarianceKernel.ExecuteAsync(matrix);

        // Compute eigenvectors (GPU-accelerated)
        var components = await gpuEigenKernel.ExecuteAsync(covariance);

        return new GpuPcaTransformer(components);
    }
}
```

### Pattern 3: Hybrid CPU/GPU Pipeline

```csharp
var pipeline = mlContext.Transforms.Concatenate("Features", columns)
    // CPU-based text processing
    .Append(mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "Text"))
    // GPU-accelerated embedding lookup
    .Append(new GpuEmbeddingTransformer(embeddingMatrix))
    // GPU-accelerated normalization
    .Append(mlContext.Transforms.GpuNormalize("Features"))
    // ML.NET classifier
    .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression());
```

## Requirements

- .NET 9.0 or later
- DotCompute NuGet packages
- ML.NET 3.0.1 or later
- CUDA-capable GPU (optional, falls back to CPU)

## License

MIT License - See LICENSE file in the repository root.
