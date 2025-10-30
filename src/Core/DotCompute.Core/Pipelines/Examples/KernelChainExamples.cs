// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using DotCompute.Core.Pipelines.Examples.Models;
using DotCompute.Core.Pipelines.Examples.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
// Type aliases to resolve ambiguous references
using ErrorHandlingStrategy = DotCompute.Abstractions.Pipelines.Enums.ErrorHandlingStrategy;
using KernelChainExecutionResult = DotCompute.Abstractions.Pipelines.Results.KernelChainExecutionResult;
using KernelChainMemoryMetrics = DotCompute.Abstractions.Pipelines.Results.KernelChainMemoryMetrics;
using KernelStepMetrics = DotCompute.Abstractions.Pipelines.Results.KernelStepMetrics;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Pipelines.Examples
{
    /// <summary>
    /// Comprehensive examples demonstrating the fluent kernel chaining API.
    /// These examples show how to leverage the existing sophisticated pipeline infrastructure
    /// through an intuitive fluent interface.
    /// </summary>
    public static partial class KernelChainExamples
    {
        #region LoggerMessage Delegates

        [LoggerMessage(EventId = 15011, Level = MsLogLevel.Information, Message = "Kernel chain diagnostics: {Diagnostics}")]
        private static partial void LogKernelChainDiagnostics(ILogger logger, string diagnostics);

        [LoggerMessage(EventId = 15012, Level = MsLogLevel.Information, Message = "Kernel chaining system initialized successfully")]
        private static partial void LogKernelChainInitialized(ILogger logger);

        [LoggerMessage(EventId = 15013, Level = MsLogLevel.Error, Message = "Kernel chaining system initialization failed")]
        private static partial void LogKernelChainInitFailed(ILogger logger);

        #endregion

        // Sample data for examples
        private static readonly int[] _sampleSortArray = [3, 1, 4, 1, 5, 9];

        /// <summary>
        /// Example: Simple sequential kernel chain for image processing.
        /// Demonstrates basic kernel chaining with error handling.
        /// </summary>
        public static async Task<byte[]> ImageProcessingChainExampleAsync(byte[] imageData)
        {
            var result = await KernelChain.Create()
                .Kernel("LoadImage", imageData)
                .ThenExecute("ApplyGaussianBlur", 2.0f)
                .ThenExecute("EdgeDetection", 0.5f)
                .ThenExecute("ConvertToGrayscale")
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
        public static async Task<float[]> ParallelDataProcessingExampleAsync(float[] rawData)
        {
            var result = await KernelChain.Create()
                .Kernel("PreprocessData", rawData)
                .Parallel(
                    ("ApplyFilterA", new object[] { "low-pass", 0.1f }),
                    ("ApplyFilterB", new object[] { "high-pass", 0.8f }),
                    ("ApplyFilterC", new object[] { "band-pass", 0.3f, 0.7f })
                )
                .ThenExecute("CombineResults")
                .ThenExecute("NormalizeOutput")
                .OnBackend("CUDA") // Prefer GPU execution
                .Cache("processed_data_v1", TimeSpan.FromHours(1))
                .ExecuteAsync<float[]>();

            return result;
        }

        /// <summary>
        /// Example: Conditional branching based on analysis results.
        /// Demonstrates dynamic execution paths based on intermediate results.
        /// </summary>
        public static async Task<ProcessedData> ConditionalProcessingExampleAsync(RawInputData inputData)
        {
            var result = await KernelChain.Create()
                .Kernel("AnalyzeInput", inputData)
                .Branch<AnalysisResult>(
                    condition: analysis => analysis.RequiresGpuProcessing,
                    truePath: chain => chain
                        .ThenExecute("GpuAcceleratedKernel")
                        .ThenExecute("GpuOptimizedPostProcess")
                        .OnBackend("CUDA"),
                    falsePath: chain => chain
                        .ThenExecute("CpuOptimizedKernel")
                        .ThenExecute("CpuVectorizedPostProcess")
                        .OnBackend("CPU")
                )
                .ThenExecute("FinalizeResults")
                .WithValidation()
                .ExecuteAsync<ProcessedData>();

            return result;
        }

        /// <summary>
        /// Example: Complex machine learning inference pipeline.
        /// Shows integration with existing DotCompute infrastructure for ML workloads.
        /// </summary>
        public static async Task<MLPrediction> MachineLearningInferenceExampleAsync(MLInputData inputData)
        {
            var result = await KernelChain.Create()
                .Kernel("LoadMLModel", "model_v2.bin")
                .ThenExecute("PreprocessInput", inputData)
                .ThenExecute("TokenizeText", 512)
                .Parallel(
                    ("ComputeEmbeddings", Array.Empty<object>()),
                    ("ExtractFeatures", Array.Empty<object>()),
                    ("ApplyAttention", Array.Empty<object>())
                )
                .ThenExecute("ConcatenateFeatures")
                .ThenExecute("RunInference")
                .ThenExecute("ApplySoftmax")
                .ThenExecute("ConvertToPrediction")
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
        public static async Task<SimulationResult> ScientificComputingExampleAsync(SimulationParameters parameters)
        {
            var result = await KernelChain.Create()
                .Kernel("InitializeSimulation", parameters)
                .ThenExecute("SetupGrid", 1024)
                .ThenExecute("ApplyBoundaryConditions")
                .Branch<GridState>(
                    condition: state => state.RequiresHighPrecision,
                    truePath: chain => chain
                        .ThenExecute("DoubleePrecisionSolver")
                        .OnError(ex => ex is OutOfMemoryException ? ErrorHandlingStrategy.Fallback : ErrorHandlingStrategy.Abort),
                    falsePath: chain => chain
                        .ThenExecute("SinglePrecisionSolver")
                )
                .ThenExecute("ComputeStatistics")
                .ThenExecute("ValidateResults")
                .ThenExecute("GenerateVisualization")
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
        public static async Task<ProcessingMetrics> RealTimeStreamProcessingExampleAsync(IAsyncEnumerable<DataChunk> dataStream)
        {
            var totalProcessed = 0;
            var errors = new List<Exception>();
            var startTime = DateTime.UtcNow;

            await foreach (var chunk in dataStream)
            {
                try
                {
                    _ = await KernelChain.Create()
                        .Kernel("ValidateChunk", chunk)
                        .ThenExecute("DecompressData")
                        .ThenExecute("ApplyRealTimeFilter")
                        .ThenExecute("UpdateRunningStatistics")
                        .ThenExecute("TriggerAlerts")
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
        public static async Task<DetailedExecutionReport> AdvancedMetricsExampleAsync(ComplexWorkload workload)
        {
            var executionResult = await KernelChain.Create()
                .Kernel("AnalyzeWorkload", workload)
                .ThenExecute("OptimizeParameters")
                .Parallel(
                    ("ProcessBatchA", Array.Empty<object>()),
                    ("ProcessBatchB", Array.Empty<object>()),
                    ("ProcessBatchC", Array.Empty<object>())
                )
                .ThenExecute("MergeResults")
                .ThenExecute("ApplyPostProcessing")
                .WithProfiling("AdvancedWorkload")
                .WithValidation()
                .OnError(ex => ErrorHandlingStrategy.Retry)
                .ExecuteWithMetricsAsync();

            // Analyze results and create detailed report
            var report = new DetailedExecutionReport
            {
                Success = executionResult.Success,
                TotalExecutionTime = executionResult.ExecutionTime,
                StepMetrics = [.. executionResult.StepMetrics.Select(ConvertToResultsKernelStepMetrics)],
                MemoryMetrics = ConvertToResultsKernelChainMemoryMetrics(executionResult.MemoryMetrics),
                BackendUsed = executionResult.Backend,
                Errors = executionResult.Errors?.ToList() ?? [],
                Recommendations = await GenerateOptimizationRecommendationsAsync(ConvertToResultsKernelChainExecutionResult(executionResult))
            };

            return report;
        }

        /// <summary>
        /// Example: Quick utility methods for common operations.
        /// Shows the convenience methods for simple kernel executions.
        /// </summary>
        public static async Task QuickOperationsExampleAsync()
        {
            // Simple one-shot kernel execution
            _ = await KernelChain.QuickAsync<float[]>("GenerateRandomNumbers", 1000);

            // Quick execution with backend preference
            _ = await KernelChain.OnBackend("CUDA")
                .Kernel("MatrixMultiply", new float[][] { [1, 2], [3, 4] }, new float[][] { [5, 6], [7, 8] })
                .ExecuteAsync<float[][]>();

            // Quick execution with profiling
            _ = await KernelChain.WithProfiling("QuickTest")
                .Kernel("SortArray", _sampleSortArray)
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
            _ = services.AddScoped<ImageProcessingService>();
            _ = services.AddScoped<DataAnalysisService>();
            _ = services.AddSingleton<MLInferenceService>();
        }

        /// <summary>
        /// Example: Application startup configuration.
        /// Shows the proper initialization sequence for production applications.
        /// </summary>
        public static Task ConfigureApplicationAsync(IHost app)
        {
            // Initialize DotCompute runtime with kernel chaining support
            // await app.Services.InitializeDotComputeWithKernelChainingAsync();
            // This would be called in the application's startup where DotCompute.Runtime.Extensions is available

            // Verify kernel chain system is working
            var diagnostics = KernelChain.GetDiagnostics();
            var logger = app.Services.GetRequiredService<ILogger<Program>>();

            LogKernelChainDiagnostics(logger, diagnostics?.ToString() ?? "No diagnostics available");

            if (diagnostics != null && diagnostics.ContainsKey("IsConfigured") && (bool)diagnostics["IsConfigured"])
            {
                LogKernelChainInitialized(logger);
            }
            else
            {
                LogKernelChainInitFailed(logger);
            }

            return Task.CompletedTask;
        }

        // Helper method to generate optimization recommendations
        private static async Task<List<string>> GenerateOptimizationRecommendationsAsync(KernelChainExecutionResult executionResult)
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

        /// <summary>
        /// Converts Interface KernelStepMetrics to Results KernelStepMetrics
        /// </summary>
        [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "Exception is constructed to store error information, not thrown")]
        private static KernelStepMetrics ConvertToResultsKernelStepMetrics(AbstractionsMemory.Interfaces.Pipelines.KernelStepMetrics interfaceMetrics)
        {
            return new KernelStepMetrics
            {
                KernelName = interfaceMetrics.KernelName,
                StepIndex = interfaceMetrics.StepIndex,
                ExecutionTime = interfaceMetrics.ExecutionTime,
                Success = interfaceMetrics.Success,
                Backend = interfaceMetrics.Backend,
                WasCached = interfaceMetrics.WasCached,
                MemoryUsed = interfaceMetrics.MemoryUsed,
                StartTime = DateTime.UtcNow.Subtract(interfaceMetrics.ExecutionTime),
                EndTime = DateTime.UtcNow,
                Error = string.IsNullOrEmpty(interfaceMetrics.ErrorMessage) ? null : new Exception(interfaceMetrics.ErrorMessage),
                Throughput = interfaceMetrics.ThroughputOpsPerSecond
            };
        }

        /// <summary>
        /// Converts Interface KernelChainMemoryMetrics to Results KernelChainMemoryMetrics
        /// </summary>
        private static KernelChainMemoryMetrics? ConvertToResultsKernelChainMemoryMetrics(AbstractionsMemory.Interfaces.Pipelines.KernelChainMemoryMetrics? interfaceMetrics)
        {
            if (interfaceMetrics == null)
            {
                return null;
            }


            return new KernelChainMemoryMetrics
            {
                TotalMemoryAllocated = interfaceMetrics.TotalMemoryAllocated,
                PeakMemoryUsage = interfaceMetrics.PeakMemoryUsage,
                TotalMemoryFreed = 0, // Default value as not available in interface
                GarbageCollections = interfaceMetrics.GarbageCollections,
                MemoryPoolingUsed = interfaceMetrics.MemoryPoolingUsed,
                AllocationCount = 1, // Estimated default
                DeallocationCount = 1  // Estimated default
            };
        }

        /// <summary>
        /// Converts Interface KernelChainExecutionResult to Results KernelChainExecutionResult
        /// </summary>
        private static KernelChainExecutionResult ConvertToResultsKernelChainExecutionResult(AbstractionsMemory.Interfaces.Pipelines.KernelChainExecutionResult interfaceResult)
        {
            return new KernelChainExecutionResult
            {
                Success = interfaceResult.Success,
                Result = null, // Set to null for this conversion - could be set to actual result if available
                ExecutionTime = interfaceResult.ExecutionTime,
                Backend = interfaceResult.Backend,
                StepMetrics = interfaceResult.StepMetrics.Select(ConvertToResultsKernelStepMetrics).ToList(),
                MemoryMetrics = ConvertToResultsKernelChainMemoryMetrics(interfaceResult.MemoryMetrics),
                Errors = interfaceResult.Errors?.ToList()
            };
        }
    }
}

