// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Pipelines.Examples
{
    /// <summary>
    /// Comprehensive examples demonstrating the fluent kernel chaining API.
    /// These examples show how to leverage the existing sophisticated pipeline infrastructure
    /// through an intuitive fluent interface.
    /// </summary>
    public static class KernelChainExamples
    {
        /// <summary>
        /// Example: Simple sequential kernel chain for image processing.
        /// Demonstrates basic kernel chaining with error handling.
        /// </summary>
        public static async Task<byte[]> ImageProcessingChainExample(byte[] imageData)
        {
            var result = await KernelChain.Create()
                .Kernel("LoadImage", imageData)
                .Then("ApplyGaussianBlur", 2.0f)
                .Then("EdgeDetection", 0.5f)
                .Then("ConvertToGrayscale")
                .WithProfiling("ImageProcessing")
                .WithTimeout(TimeSpan.FromMinutes(2))
                .OnError(ex => ex is ArgumentException ? ErrorHandlingStrategy.Skip : ErrorHandlingStrategy.Abort)
                .ExecuteAsync<byte[]>();

            return result ?? throw new InvalidOperationException("Image processing chain returned null result");
        }

        /// <summary>
        /// Example: Parallel execution for data processing pipeline.
        /// Shows how multiple kernels can run concurrently for performance.
        /// </summary>
        public static async Task<float[]> ParallelDataProcessingExample(float[] rawData)
        {
            var result = await KernelChain.Create()
                .Kernel("PreprocessData", rawData)
                .Parallel(
                    ("ApplyFilterA", new object[] { "low-pass", 0.1f }),
                    ("ApplyFilterB", new object[] { "high-pass", 0.8f }),
                    ("ApplyFilterC", new object[] { "band-pass", 0.3f, 0.7f })
                )
                .Then("CombineResults")
                .Then("NormalizeOutput")
                .OnBackend("CUDA") // Prefer GPU execution
                .Cache("processed_data_v1", TimeSpan.FromHours(1))
                .ExecuteAsync<float[]>();

            return result;
        }

        /// <summary>
        /// Example: Conditional branching based on analysis results.
        /// Demonstrates dynamic execution paths based on intermediate results.
        /// </summary>
        public static async Task<ProcessedData> ConditionalProcessingExample(RawInputData inputData)
        {
            var result = await KernelChain.Create()
                .Kernel("AnalyzeInput", inputData)
                .Branch<AnalysisResult>(
                    condition: analysis => analysis.RequiresGpuProcessing,
                    truePath: chain => chain
                        .Then("GpuAcceleratedKernel")
                        .Then("GpuOptimizedPostProcess")
                        .OnBackend("CUDA"),
                    falsePath: chain => chain
                        .Then("CpuOptimizedKernel")
                        .Then("CpuVectorizedPostProcess")
                        .OnBackend("CPU")
                )
                .Then("FinalizeResults")
                .WithValidation()
                .ExecuteAsync<ProcessedData>();

            return result;
        }

        /// <summary>
        /// Example: Complex machine learning inference pipeline.
        /// Shows integration with existing DotCompute infrastructure for ML workloads.
        /// </summary>
        public static async Task<MLPrediction> MachineLearningInferenceExample(MLInputData inputData)
        {
            var result = await KernelChain.Create()
                .Kernel("LoadMLModel", "model_v2.bin")
                .Then("PreprocessInput", inputData)
                .Then("TokenizeText", 512)
                .Parallel(
                    ("ComputeEmbeddings", Array.Empty<object>()),
                    ("ExtractFeatures", Array.Empty<object>()),
                    ("ApplyAttention", Array.Empty<object>())
                )
                .Then("ConcatenateFeatures")
                .Then("RunInference")
                .Then("ApplySoftmax")
                .Then("ConvertToPrediction")
                .OnBackend("CUDA") // ML workloads benefit from GPU acceleration
                .Cache($"ml_prediction_{inputData.GetHashCode()}", TimeSpan.FromMinutes(30))
                .WithProfiling($"MLInference_{DateTime.Now:yyyyMMdd_HHmmss}")
                .OnError(ex => ErrorHandlingStrategy.Retry)
                .ExecuteAsync<MLPrediction>();

            return result ?? throw new InvalidOperationException("ML inference chain returned null result");
        }

        /// <summary>
        /// Example: Scientific computing with error handling and fallbacks.
        /// Demonstrates robust execution with multiple error handling strategies.
        /// </summary>
        public static async Task<SimulationResult> ScientificComputingExample(SimulationParameters parameters)
        {
            var result = await KernelChain.Create()
                .Kernel("InitializeSimulation", parameters)
                .Then("SetupGrid", 1024)
                .Then("ApplyBoundaryConditions")
                .Branch<GridState>(
                    condition: state => state.RequiresHighPrecision,
                    truePath: chain => chain
                        .Then("DoubleePrecisionSolver")
                        .OnError(ex => ex is OutOfMemoryException ? ErrorHandlingStrategy.Fallback : ErrorHandlingStrategy.Abort),
                    falsePath: chain => chain
                        .Then("SinglePrecisionSolver")
                )
                .Then("ComputeStatistics")
                .Then("ValidateResults")
                .Then("GenerateVisualization")
                .OnBackend("CUDA")
                .WithTimeout(TimeSpan.FromHours(2))
                .WithProfiling("ScientificSimulation")
                .OnError(ex =>
                {
                    return ex switch
                    {
                        TimeoutException => ErrorHandlingStrategy.Abort,
                        OutOfMemoryException => ErrorHandlingStrategy.Fallback,
                        ArgumentException => ErrorHandlingStrategy.Skip,
                        _ => ErrorHandlingStrategy.Retry
                    };
                })
                .ExecuteAsync<SimulationResult>();

            return result ?? throw new InvalidOperationException("Scientific computing chain returned null result");
        }

        /// <summary>
        /// Example: Real-time processing pipeline with streaming data.
        /// Shows how to handle continuous data streams with kernel chains.
        /// </summary>
        public static async Task<ProcessingMetrics> RealTimeStreamProcessingExample(IAsyncEnumerable<DataChunk> dataStream)
        {
            var totalProcessed = 0;
            var errors = new List<Exception>();
            var startTime = DateTime.UtcNow;

            await foreach (var chunk in dataStream)
            {
                try
                {
                    await KernelChain.Create()
                        .Kernel("ValidateChunk", chunk)
                        .Then("DecompressData")
                        .Then("ApplyRealTimeFilter")
                        .Then("UpdateRunningStatistics")
                        .Then("TriggerAlerts")
                        .Cache($"chunk_{chunk.Id}", TimeSpan.FromSeconds(30))
                        .OnBackend("CPU") // Real-time processing often better on CPU for latency
                        .WithTimeout(TimeSpan.FromMilliseconds(100)) // Strict real-time constraints
                        .OnError(ex =>
                        {
                            errors.Add(ex);
                            return ErrorHandlingStrategy.Continue; // Keep processing other chunks
                        })
                        .ExecuteAsync<object>();

                    totalProcessed++;
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    // Continue with next chunk
                }
            }

            var endTime = DateTime.UtcNow;

            return new ProcessingMetrics
            {
                TotalChunksProcessed = totalProcessed,
                TotalErrors = errors.Count,
                ProcessingTime = endTime - startTime,
                ThroughputPerSecond = totalProcessed / (endTime - startTime).TotalSeconds
            };
        }

        /// <summary>
        /// Example: Advanced kernel chain with metrics and optimization recommendations.
        /// Shows how to get detailed execution information and performance insights.
        /// </summary>
        public static async Task<DetailedExecutionReport> AdvancedMetricsExample(ComplexWorkload workload)
        {
            var executionResult = await KernelChain.Create()
                .Kernel("AnalyzeWorkload", workload)
                .Then("OptimizeParameters")
                .Parallel(
                    ("ProcessBatchA", Array.Empty<object>()),
                    ("ProcessBatchB", Array.Empty<object>()),
                    ("ProcessBatchC", Array.Empty<object>())
                )
                .Then("MergeResults")
                .Then("ApplyPostProcessing")
                .WithProfiling("AdvancedWorkload")
                .WithValidation()
                .OnError(ex => ErrorHandlingStrategy.Retry)
                .ExecuteWithMetricsAsync();

            // Analyze results and create detailed report
            var report = new DetailedExecutionReport
            {
                Success = executionResult.Success,
                TotalExecutionTime = executionResult.ExecutionTime,
                StepMetrics = executionResult.StepMetrics.ToList(),
                MemoryMetrics = executionResult.MemoryMetrics,
                BackendUsed = executionResult.Backend,
                Errors = executionResult.Errors?.ToList() ?? new List<Exception>(),
                Recommendations = await GenerateOptimizationRecommendations(executionResult)
            };

            return report;
        }

        /// <summary>
        /// Example: Quick utility methods for common operations.
        /// Shows the convenience methods for simple kernel executions.
        /// </summary>
        public static async Task QuickOperationsExample()
        {
            // Simple one-shot kernel execution
            var result1 = await KernelChain.Quick<float[]>("GenerateRandomNumbers", 1000);

            // Quick execution with backend preference
            var result2 = await KernelChain.OnBackend("CUDA")
                .Kernel("MatrixMultiply", new float[,] { { 1, 2 }, { 3, 4 } }, new float[,] { { 5, 6 }, { 7, 8 } })
                .ExecuteAsync<float[,]>();

            // Quick execution with profiling
            var result3 = await KernelChain.WithProfiling("QuickTest")
                .Kernel("SortArray", new int[] { 3, 1, 4, 1, 5, 9 })
                .ExecuteAsync<int[]>();
        }

        /// <summary>
        /// Example: Integration with dependency injection and hosted services.
        /// Shows how to use kernel chains in a production ASP.NET Core or similar application.
        /// </summary>
        public static void ConfigureServices(IServiceCollection services)
        {
            // Add DotCompute runtime with kernel chaining
            // services.AddDotComputeWithKernelChaining();
            // This would be called in the application's Startup/Program.cs file
            // where DotCompute.Runtime.Extensions is available

            // Add application-specific services that use kernel chains
            services.AddScoped<ImageProcessingService>();
            services.AddScoped<DataAnalysisService>();
            services.AddSingleton<MLInferenceService>();
        }

        /// <summary>
        /// Example: Application startup configuration.
        /// Shows the proper initialization sequence for production applications.
        /// </summary>
        public static Task ConfigureApplication(IHost app)
        {
            // Initialize DotCompute runtime with kernel chaining support
            // await app.Services.InitializeDotComputeWithKernelChainingAsync();
            // This would be called in the application's startup where DotCompute.Runtime.Extensions is available

            // Verify kernel chain system is working
            var diagnostics = KernelChain.GetDiagnostics();
            var logger = app.Services.GetRequiredService<ILogger<Program>>();


            logger.LogInformation("Kernel chain diagnostics: {@Diagnostics}", diagnostics);

            if (diagnostics.ContainsKey("IsConfigured") && (bool)diagnostics["IsConfigured"])
            {
                logger.LogInformation("Kernel chaining system initialized successfully");
            }
            else
            {
                logger.LogError("Kernel chaining system initialization failed");
            }

            return Task.CompletedTask;
        }

        // Helper method to generate optimization recommendations
        private static async Task<List<string>> GenerateOptimizationRecommendations(KernelChainExecutionResult executionResult)
        {
            var recommendations = new List<string>();

            // Analyze execution metrics and provide recommendations
            if (executionResult.ExecutionTime > TimeSpan.FromSeconds(10))
            {
                recommendations.Add("Consider using parallel execution for better performance");
            }

            if (executionResult.MemoryMetrics?.PeakMemoryUsage > 1024 * 1024 * 1024) // 1GB
            {
                recommendations.Add("Consider enabling memory pooling to reduce allocations");
            }

            if (executionResult.Backend == "CPU" && executionResult.StepMetrics.Count > 5)
            {
                recommendations.Add("Consider using GPU backend for better performance with complex workloads");
            }

            await Task.CompletedTask; // Simulate async analysis
            return recommendations;
        }
    }

    // Supporting classes for examples
    public class RawInputData { }
    public class AnalysisResult { public bool RequiresGpuProcessing { get; set; } }
    public class ProcessedData { }
    public class MLInputData { public override int GetHashCode() => base.GetHashCode(); }
    public class MLPrediction { }
    public class SimulationParameters { }
    public class GridState { public bool RequiresHighPrecision { get; set; } }
    public class SimulationResult { }
    public class DataChunk { public string Id { get; set; } = string.Empty; }
    public class ProcessingMetrics
    {

        public int TotalChunksProcessed { get; set; }
        public int TotalErrors { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public double ThroughputPerSecond { get; set; }
    }
    public class ComplexWorkload { }
    public class DetailedExecutionReport

    {
        public bool Success { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public List<KernelStepMetrics> StepMetrics { get; set; } = new();
        public KernelChainMemoryMetrics? MemoryMetrics { get; set; }
        public string BackendUsed { get; set; } = string.Empty;
        public List<Exception> Errors { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    // Example service classes that would use kernel chains
    public class ImageProcessingService
    {
        public async Task<byte[]> ProcessImageAsync(byte[] imageData)
        {
            return await KernelChainExamples.ImageProcessingChainExample(imageData);
        }
    }

    public class DataAnalysisService
    {
        public async Task<float[]> AnalyzeDataAsync(float[] data)
        {
            return await KernelChainExamples.ParallelDataProcessingExample(data);
        }
    }

    public class MLInferenceService
    {
        public async Task<MLPrediction> PredictAsync(MLInputData input)
        {
            return await KernelChainExamples.MachineLearningInferenceExample(input);
        }
    }

    // Placeholder for Program class in examples
    public class Program { }
}
