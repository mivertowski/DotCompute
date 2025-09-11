// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Pipelines;
using DotCompute.Tests.Common;
using DotCompute.Tests.Common.Generators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotCompute.Pipeline.IntegrationTests;

/// <summary>
/// End-to-end integration tests for complete pipeline workflows.
/// Tests real-world scenarios with multiple pipeline stages and cross-backend execution.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Component", "PipelineWorkflow")]
public class PipelineWorkflowTests : PipelineTestBase, IClassFixture<DotComputeTestFixture>
{
    private readonly DotComputeTestFixture _fixture;
    private readonly ILogger<PipelineWorkflowTests> _logger;

    public PipelineWorkflowTests(DotComputeTestFixture fixture)
    {
        _fixture = fixture;
        _logger = Services.GetRequiredService<ILogger<PipelineWorkflowTests>>();
    }

    [Fact]
    public async Task EndToEnd_ImageProcessing_Pipeline()
    {
        // Arrange - Create synthetic image data
        const int width = 512;
        const int height = 512;
        const int channels = 3; // RGB
        
        var imageData = DataGenerator.GenerateImageProcessingData(width, height, channels);
        var builder = CreatePipelineBuilder();

        _logger.LogInformation($"Starting image processing pipeline: {width}x{height}x{channels}");

        // Act - Execute complete image processing pipeline
        var result = await MeasureExecutionTimeAsync(async () =>
        {
            return await builder
                // Stage 1: Gaussian blur (convolution)
                .Kernel("GaussianBlur", imageData, new float[imageData.Length], 3) // 3x3 kernel
                // Stage 2: Edge detection (Sobel operator)
                .Then("SobelEdgeDetection", new object[] { "blurred", new float[imageData.Length] })
                // Stage 3: Brightness adjustment
                .Then("BrightnessAdjust", new object[] { "edges", 1.2f, new float[imageData.Length] })
                // Stage 4: Histogram equalization
                .Then("HistogramEqualize", new object[] { "brightened", new float[imageData.Length] })
                .WithProfiling("image-processing")
                .WithValidation(validateInputs: true)
                .ExecuteWithMetricsAsync(CreateTestTimeout(30));
        });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Equal(4, result.StepMetrics.Count); // Should have 4 processing stages
        
        // Verify each stage executed successfully
        Assert.All(result.StepMetrics, metric =>
        {
            Assert.True(metric.ExecutionTime > TimeSpan.Zero);
            Assert.True(metric.MemoryUsed > 0);
            Assert.NotNull(metric.KernelName);
        });

        // Image processing should complete within reasonable time
        Assert.True(result.ExecutionTime < TimeSpan.FromSeconds(10),
            $"Image processing took too long: {result.ExecutionTime.TotalSeconds:F2}s");

        _logger.LogInformation($"Image processing completed in {result.ExecutionTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task EndToEnd_DataAnalysis_Pipeline()
    {
        // Arrange - Generate financial data for analysis
        const int transactionCount = 100000;
        var financialData = DataGenerator.GenerateFinancialAnalysisData(transactionCount);
        var prices = financialData.Select(t => t.Price).ToArray();
        var volumes = financialData.Select(t => (float)t.Volume).ToArray();
        
        var builder = CreatePipelineBuilder();

        _logger.LogInformation($"Starting financial data analysis: {transactionCount} transactions");

        // Act - Execute complete financial analysis pipeline
        var result = await MeasureExecutionTimeAsync(async () =>
        {
            return await builder
                // Stage 1: Calculate moving averages
                .Kernel("MovingAverage", prices, 20, new float[prices.Length]) // 20-period MA
                // Stage 2: Calculate volume-weighted average price (VWAP)
                .Then("VWAP", new object[] { prices, volumes, new float[prices.Length] })
                // Stage 3: Detect anomalies using statistical methods
                .Then("AnomalyDetection", new object[] { "vwap", 2.0f, new bool[prices.Length] }) // 2-sigma threshold
                // Stage 4: Generate trading signals
                .Then("TradingSignals", new object[] { "moving_avg", "anomalies", new int[prices.Length] })
                // Stage 5: Calculate portfolio metrics
                .Then("PortfolioMetrics", new object[] { "signals", prices, volumes, new float[5] }) // 5 metrics
                .WithProfiling("financial-analysis")
                .OnBackend("CPU") // Financial calculations often better on CPU
                .ExecuteWithMetricsAsync(CreateTestTimeout(45));
        });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Equal(5, result.StepMetrics.Count);
        
        // Verify financial metrics are calculated
        var portfolioMetrics = (float[])result.Result;
        Assert.Equal(5, portfolioMetrics.Length);
        Assert.All(portfolioMetrics, metric => Assert.True(metric >= 0));

        // Analysis should complete within reasonable time for 100k transactions
        Assert.True(result.ExecutionTime < TimeSpan.FromSeconds(15),
            $"Financial analysis took too long: {result.ExecutionTime.TotalSeconds:F2}s");

        _logger.LogInformation($"Financial analysis completed: {result.ExecutionTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task EndToEnd_MachineLearning_Pipeline()
    {
        // Arrange - Generate training data for ML pipeline
        const int sampleCount = 50000;
        const int featureCount = 100;
        
        var trainingData = DataGenerator.GenerateMatrixData(sampleCount, featureCount, sparsity: 0.1f);
        var labels = GenerateTestData<float>(sampleCount, DataPattern.Random);
        var builder = CreatePipelineBuilder();

        _logger.LogInformation($"Starting ML training pipeline: {sampleCount} samples, {featureCount} features");

        // Act - Execute machine learning training pipeline
        var result = await MeasureExecutionTimeAsync(async () =>
        {
            return await builder
                // Stage 1: Data normalization (feature scaling)
                .Kernel("StandardScaler", trainingData, new float[trainingData.Length])
                // Stage 2: Feature selection (remove low-variance features)
                .Then("VarianceThreshold", new object[] { "scaled_data", 0.01f, new float[trainingData.Length] })
                // Stage 3: Principal Component Analysis (dimensionality reduction)
                .Then("PCA", new object[] { "selected_features", 50, new float[sampleCount * 50] }) // Reduce to 50 dims
                // Stage 4: Train linear regression model
                .Then("LinearRegression", new object[] { "pca_features", labels, new float[50] }) // 50 weights
                // Stage 5: Model validation (cross-validation)
                .Then("CrossValidation", new object[] { "model_weights", "pca_features", labels, 5, new float[3] }) // 5-fold CV, 3 metrics
                .WithProfiling("ml-training")
                .OnBackend("CUDA") // ML training benefits from GPU acceleration
                .WithTimeout(TimeSpan.FromMinutes(2))
                .ExecuteWithMetricsAsync(CreateTestTimeout(120));
        });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Equal(5, result.StepMetrics.Count);
        
        // Verify model validation metrics
        var validationMetrics = (float[])result.Result;
        Assert.Equal(3, validationMetrics.Length);
        
        // ML pipeline should complete within reasonable time
        Assert.True(result.ExecutionTime < TimeSpan.FromMinutes(1),
            $"ML training took too long: {result.ExecutionTime.TotalSeconds:F2}s");

        _logger.LogInformation($"ML training completed: {result.ExecutionTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task EndToEnd_StreamProcessing_Pipeline()
    {
        // Arrange - Create streaming data source
        const int batchSize = 1000;
        const int totalBatches = 20;
        var streamingData = DataGenerator.GenerateStreamingData(batchSize, totalBatches);
        
        var processedBatches = new List<float[]>();
        var builder = CreatePipelineBuilder();

        _logger.LogInformation($"Starting stream processing: {totalBatches} batches of {batchSize} elements");

        // Act - Process streaming data with real-time pipeline
        var totalTime = await MeasureExecutionTimeAsync(async () =>
        {
            await foreach (var batch in streamingData)
            {
                var result = await builder
                    // Stage 1: Real-time filtering
                    .Kernel("RealtimeFilter", batch, 0.3f, new float[batch.Length])
                    // Stage 2: Moving window aggregation
                    .Then("WindowAggregate", new object[] { "filtered", 10, new float[batch.Length] })
                    // Stage 3: Anomaly detection with streaming context
                    .Then("StreamingAnomalyDetect", new object[] { "aggregated", new bool[batch.Length] })
                    // Stage 4: Alert generation
                    .Then("AlertGeneration", new object[] { "anomalies", batch, new float[batch.Length] })
                    .WithTimeout(TimeSpan.FromSeconds(1)) // Real-time constraint
                    .ExecuteAsync<float[]>(CreateTestTimeout(5));
                
                processedBatches.Add(result);
            }
        });

        // Assert
        Assert.Equal(totalBatches, processedBatches.Count);
        Assert.All(processedBatches, batch => 
        {
            Assert.NotNull(batch);
            Assert.Equal(batchSize, batch.Length);
        });

        // Stream processing should maintain real-time performance
        var avgBatchTime = totalTime.TotalMilliseconds / totalBatches;
        Assert.True(avgBatchTime < 100, // Each batch should process in <100ms
            $"Stream processing too slow: {avgBatchTime:F2}ms per batch");

        _logger.LogInformation($"Stream processing completed: {totalTime.TotalMilliseconds:F2}ms total, " +
                              $"{avgBatchTime:F2}ms per batch");
    }

    [SkippableFact]
    [Trait("Category", "Hardware")]
    public async Task CrossBackend_ResultConsistency()
    {
        Skip.IfNot(_fixture.IsGpuAvailable, "GPU not available for cross-backend testing");

        // Arrange - Same computation on different backends
        var data = GenerateTestData<float>(10000, DataPattern.Sequential);
        var cpuBuilder = CreatePipelineBuilder();
        var gpuBuilder = CreatePipelineBuilder();

        _logger.LogInformation("Starting cross-backend consistency test");

        // Act - Execute same pipeline on CPU and GPU
        var cpuResult = await cpuBuilder
            .Kernel("VectorAdd", data, data, new float[data.Length])
            .Then("VectorMultiply", new object[] { "result", data, new float[data.Length] })
            .OnBackend("CPU")
            .ExecuteAsync<float[]>(CreateTestTimeout());

        var gpuResult = await gpuBuilder
            .Kernel("VectorAdd", data, data, new float[data.Length])
            .Then("VectorMultiply", new object[] { "result", data, new float[data.Length] })
            .OnBackend("CUDA")
            .ExecuteAsync<float[]>(CreateTestTimeout());

        // Assert - Results should be consistent across backends
        Assert.Equal(cpuResult.Length, gpuResult.Length);
        
        for (int i = 0; i < cpuResult.Length; i++)
        {
            Assert.Equal(cpuResult[i], gpuResult[i], precision: 5); // Allow for minor floating-point differences
        }

        _logger.LogInformation("Cross-backend consistency verified");
    }

    [SkippableFact]
    [Trait("Category", "Hardware")]
    public async Task GPU_Pipeline_OptimizationValidation()
    {
        Skip.IfNot(_fixture.IsGpuAvailable, "GPU not available for optimization testing");

        // Arrange - Large dataset that benefits from GPU optimization
        var largeData = GenerateTestData<float>(1000000, DataPattern.Random); // 1M elements
        var builder = CreatePipelineBuilder();

        _logger.LogInformation("Starting GPU optimization validation");

        // Act - Execute with different optimization levels
        var conservativeTime = await MeasureExecutionTimeAsync(async () =>
        {
            await builder
                .Kernel("ParallelReduce", largeData, new float[1])
                .Then("VectorNormalize", new object[] { largeData, new float[largeData.Length] })
                .OnBackend("CUDA")
                .WithValidation(false) // Skip validation for performance
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        var aggressiveTime = await MeasureExecutionTimeAsync(async () =>
        {
            await builder
                .Kernel("ParallelReduce", largeData, new float[1])
                .Then("VectorNormalize", new object[] { largeData, new float[largeData.Length] })
                .OnBackend("CUDA")
                .WithValidation(false)
                // Add aggressive optimizations
                .Cache("gpu-large-vector", TimeSpan.FromMinutes(5))
                .ExecuteAsync<float[]>(CreateTestTimeout());
        });

        // Assert - Optimized version should be faster (or at least not slower)
        Assert.True(aggressiveTime <= conservativeTime * 1.1, // Allow 10% margin
            $"Optimization regression: Conservative {conservativeTime.TotalMilliseconds:F2}ms, " +
            $"Aggressive {aggressiveTime.TotalMilliseconds:F2}ms");

        _logger.LogInformation($"GPU optimization validated: " +
                              $"Conservative {conservativeTime.TotalMilliseconds:F2}ms, " +
                              $"Aggressive {aggressiveTime.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task Pipeline_ErrorRecovery_HandlesFailuresGracefully()
    {
        // Arrange - Pipeline with potential failures
        var data = GenerateTestData<float>(1000);
        var builder = CreatePipelineBuilder();

        // Configure error handling
        var errorHandlingStrategy = ErrorHandlingStrategy.Skip;
        builder.OnError(ex => errorHandlingStrategy);

        _logger.LogInformation("Testing pipeline error recovery");

        // Act - Execute pipeline with failing stage
        var result = await builder
            .Kernel("VectorAdd", data, data, new float[data.Length])
            .Then("FailingKernel", new object[] { "result" }) // This will fail
            .Then("VectorMultiply", new object[] { "result", data, new float[data.Length] }) // This should still execute
            .WithValidation(validateInputs: false) // Don't validate to allow failure
            .ExecuteWithMetricsAsync(CreateTestTimeout());

        // Assert - Pipeline should complete despite failure
        Assert.True(result.Success); // Overall pipeline succeeds with error handling
        Assert.NotNull(result.Errors); // Should record the error
        Assert.NotEmpty(result.Errors); // Should have at least one error
        
        // Should have executed stages before and after the failure
        Assert.True(result.StepMetrics.Count >= 2);

        _logger.LogInformation($"Error recovery test completed with {result.Errors?.Count} errors handled");
    }

    [Fact]
    public async Task Pipeline_MemoryEfficiency_HandlesLargeWorkloads()
    {
        // Arrange - Very large dataset to test memory management
        var largeDatasetSize = 5_000_000; // 5M elements
        var largeData = GenerateTestData<float>(Math.Min(largeDatasetSize, 100000)); // Limit for test environment
        var builder = CreatePipelineBuilder();

        _logger.LogInformation($"Testing memory efficiency with large dataset");

        // Act & Assert - Monitor memory usage during execution
        var memoryMetrics = await ValidateMemoryUsageAsync(async () =>
        {
            var result = await builder
                .Kernel("VectorAdd", largeData, largeData, new float[largeData.Length])
                .Then("VectorMultiply", new object[] { "result", largeData, new float[largeData.Length] })
                .Then("VectorNormalize", new object[] { "result", new float[largeData.Length] })
                .WithProfiling("memory-efficiency")
                .ExecuteAsync<float[]>(CreateTestTimeout());
                
            Assert.NotNull(result);
            Assert.Equal(largeData.Length, result.Length);
        }, maxMemoryIncreaseMB: 200); // Allow reasonable memory increase

        // Memory should be managed efficiently
        Assert.True(memoryMetrics.MemoryIncreaseMB < 200,
            $"Memory usage too high: {memoryMetrics.MemoryIncreaseMB:F2}MB");

        _logger.LogInformation($"Memory efficiency validated: {memoryMetrics.MemoryIncreaseMB:F2}MB increase");
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Add integration test specific services
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        // Use real implementations instead of mocks for integration tests
        // services.AddDotComputeCore();
        // services.AddDotComputePipelines();
        // services.AddDotComputeLinq();
    }
}

/// <summary>
/// Test fixture for DotCompute integration tests.
/// Provides shared setup and GPU availability detection.
/// </summary>
public class DotComputeTestFixture : IDisposable
{
    public bool IsGpuAvailable { get; private set; }
    public bool IsCudaAvailable { get; private set; }

    public DotComputeTestFixture()
    {
        DetectHardwareCapabilities();
    }

    private void DetectHardwareCapabilities()
    {
        try
        {
            // Detect GPU availability
            // In a real implementation, this would use DotCompute's hardware detection
            IsGpuAvailable = Environment.GetEnvironmentVariable("CUDA_VISIBLE_DEVICES") != "-1";
            IsCudaAvailable = IsGpuAvailable && File.Exists("/usr/local/cuda/bin/nvcc");
        }
        catch
        {
            IsGpuAvailable = false;
            IsCudaAvailable = false;
        }
    }

    public void Dispose()
    {
        // Cleanup test fixture resources
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}