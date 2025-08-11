// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.ICompiledKernel;

namespace DotCompute.Core.Pipelines;

/// <summary>
/// Example demonstrating kernel pipeline usage.
/// This file shows how to use the pipeline infrastructure for chaining compute operations.
/// </summary>
public static class PipelineUsageExample
{
    /// <summary>
    /// Example: Image processing pipeline with blur, edge detection, and enhancement.
    /// </summary>
    public static Task<IKernelPipeline> CreateImageProcessingPipelineAsync(
        IComputeDevice device,
        ICompiledKernel blurKernel,
        ICompiledKernel edgeKernel,
        ICompiledKernel enhanceKernel)
    {
        var pipeline = KernelPipelineBuilder.Create()
            .WithName("ImageProcessingPipeline")

            // Stage 1: Gaussian blur
            .AddKernel("Blur", blurKernel, stage => stage
                .WithWorkSize(1920, 1080)  // Full HD image
                .MapInput("input_image", "source_image")
                .MapOutput("output_image", "blurred_image")
                .SetParameter("blur_radius", 3.0f)
                .WithMemoryHint(MemoryHint.Sequential)
                .WithPriority(1))

            // Stage 2: Edge detection (depends on blur output)
            .AddKernel("EdgeDetection", edgeKernel, stage => stage
                .WithWorkSize(1920, 1080)
                .MapInput("input_image", "blurred_image")
                .MapOutput("edge_map", "edges")
                .SetParameter("threshold", 0.1f)
                .DependsOn("Blur")
                .WithMemoryHint(MemoryHint.Random))

            // Stage 3: Parallel enhancement operations
            .AddParallel(parallel => parallel
                .AddKernel("Sharpen", enhanceKernel, stage => stage
                    .WithWorkSize(1920, 1080)
                    .MapInput("input_image", "blurred_image")
                    .MapOutput("sharpened", "sharp_image")
                    .SetParameter("sharpen_amount", 1.5f))

                .AddKernel("Contrast", enhanceKernel, stage => stage
                    .WithWorkSize(1920, 1080)
                    .MapInput("input_image", "blurred_image")
                    .MapOutput("contrasted", "contrast_image")
                    .SetParameter("contrast_level", 1.2f))

                .WithMaxDegreeOfParallelism(2)
                .WithSynchronization(SynchronizationMode.WaitAll)
                .WithBarrier())

            // Stage 4: Conditional final processing
            .AddBranch(
                context => context.Inputs.ContainsKey("enable_final_filter"),
                trueBranch => trueBranch
                    .AddKernel("FinalFilter", enhanceKernel, stage => stage
                        .WithWorkSize(1920, 1080)
                        .MapInput("input_image", "contrast_image")
                        .MapOutput("final_image", "result")),
                falseBranch => falseBranch
                    .AddKernel("DirectCopy", enhanceKernel, stage => stage
                        .WithWorkSize(1920, 1080)
                        .MapInput("input_image", "contrast_image")
                        .MapOutput("final_image", "result")))

            // Configure optimization
            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = true;
                opt.EnableMemoryOptimization = true;
                opt.EnableParallelMerging = true;
                opt.Level = PipelineOptimizationLevel.Aggressive;
            })

            // Add metadata
            .WithMetadata("ImageFormat", "RGBA32")
            .WithMetadata("Resolution", "1920x1080")
            .WithMetadata("Creator", "DotCompute.ImageProcessing")

            // Add event monitoring
            .WithEventHandler(evt =>
            {
                Console.WriteLine($"[{evt.Type}] {evt.Message}");
                if (evt.StageId != null)
                {
                    Console.WriteLine($"  Stage: {evt.StageId}");
                }
            })

            // Add error handling
            .WithErrorHandler((exception, context) =>
            {
                Console.WriteLine($"Error in pipeline: {exception.Message}");

                // Continue on non-critical errors
                if (exception is not OutOfMemoryException)
                {
                    return ErrorHandlingResult.Continue;
                }

                return ErrorHandlingResult.Abort;
            })

            .Build();

        return Task.FromResult(pipeline);
    }
    private static readonly float[] _value = [0.485f, 0.456f, 0.406f];
    private static readonly float[] _valueArray = [0.229f, 0.224f, 0.225f];

    /// <summary>
    /// Example: Machine learning inference pipeline with preprocessing and postprocessing.
    /// </summary>
    public static Task<IKernelPipeline> CreateMLInferencePipelineAsync(
        IComputeDevice device,
        ICompiledKernel preprocessKernel,
        ICompiledKernel inferenceKernel,
        ICompiledKernel postprocessKernel)
    {
        var pipeline = KernelPipelineBuilder.Create()
            .WithName("MLInferencePipeline")

            // Preprocessing stage
            .AddKernel("Preprocessing", preprocessKernel, stage => stage
                .WithWorkSize(224, 224, 3)  // Standard ML input size
                .MapInput("raw_data", "input_tensor")
                .MapOutput("normalized_data", "preprocessed_tensor")
                .SetParameter("mean", _value)
                .SetParameter("std", _valueArray)
                .WithMemoryHint(MemoryHint.Sequential))

            // Inference stage
            .AddKernel("Inference", inferenceKernel, stage => stage
                .WithWorkSize(1)  // Single inference
                .MapInput("input_tensor", "preprocessed_tensor")
                .MapOutput("logits", "raw_predictions")
                .WithMemoryHint(MemoryHint.Persistent)
                .DependsOn("Preprocessing"))

            // Postprocessing stage
            .AddKernel("Postprocessing", postprocessKernel, stage => stage
                .WithWorkSize(1000)  // 1000 classes
                .MapInput("logits", "raw_predictions")
                .MapOutput("probabilities", "final_predictions")
                .SetParameter("temperature", 1.0f)
                .WithMemoryHint(MemoryHint.Temporary)
                .DependsOn("Inference"))

            .WithOptimization(opt =>
            {
                opt.EnableKernelFusion = true;
                opt.EnableMemoryOptimization = true;
                opt.Level = PipelineOptimizationLevel.Balanced;
            })

            .Build();

        return Task.FromResult(pipeline);
    }

    /// <summary>
    /// Example: Iterative scientific computation with convergence checking.
    /// </summary>
    public static Task<IKernelPipeline> CreateIterativeComputationPipelineAsync(
        IComputeDevice device,
        ICompiledKernel computeKernel,
        ICompiledKernel convergenceKernel)
    {
        var pipeline = KernelPipelineBuilder.Create()
            .WithName("IterativeComputationPipeline")

            // Initial setup
            .AddKernel("Initialize", computeKernel, stage => stage
                .WithWorkSize(1000, 1000)
                .MapInput("initial_values", "x0")
                .MapOutput("current_values", "x")
                .WithMemoryHint(MemoryHint.Persistent))

            // Iterative loop
            .AddLoop(
                (context, iteration) =>
                {
                    // Continue until convergence or max iterations
                    if (iteration >= 1000)
                    {
                        return false;
                    }

                    if (context.State.TryGetValue("converged", out var converged))
                    {
                        return !(bool)converged;
                    }

                    return true;
                },
                loopBody => loopBody
                    .AddKernel("Compute", computeKernel, stage => stage
                        .WithWorkSize(1000, 1000)
                        .MapInput("x_prev", "current_values")
                        .MapOutput("x_new", "next_values")
                        .SetParameter("alpha", 0.01f))

                    .AddKernel("CheckConvergence", convergenceKernel, stage => stage
                        .WithWorkSize(1)
                        .MapInput("x_old", "current_values")
                        .MapInput("x_new", "next_values")
                        .MapOutput("converged", "convergence_flag")
                        .SetParameter("tolerance", 1e-6f))

                    .AddKernel("UpdateValues", computeKernel, stage => stage
                        .WithWorkSize(1000, 1000)
                        .MapInput("x_new", "next_values")
                        .MapOutput("current_values", "x")))

            .WithOptimization(opt =>
            {
                opt.EnableMemoryOptimization = true;
                opt.Level = PipelineOptimizationLevel.Conservative;
            })

            .Build();

        return Task.FromResult(pipeline);
    }

    /// <summary>
    /// Example: Executing a pipeline with comprehensive monitoring.
    /// </summary>
    public static async Task<PipelineExecutionResult> ExecutePipelineWithMonitoringAsync(
        IKernelPipeline pipeline,
        IComputeDevice device,
        IPipelineMemoryManager memoryManager,
        Dictionary<string, object> inputs)
    {
        // Create profiler for detailed monitoring
        var profiler = new BasicPipelineProfiler();

        // Create execution context
        var context = new PipelineExecutionContext
        {
            Inputs = inputs,
            Device = device,
            MemoryManager = memoryManager,
            Profiler = profiler,
            Options = new PipelineExecutionOptions
            {
                EnableProfiling = true,
                EnableDetailedLogging = true,
                MaxParallelStages = Environment.ProcessorCount,
                ContinueOnError = false
            }
        };

        try
        {
            Console.WriteLine($"Starting pipeline execution: {pipeline.Name}");
            Console.WriteLine($"Input count: {inputs.Count}");

            // Validate pipeline before execution
            var validation = pipeline.Validate();
            if (!validation.IsValid)
            {
                Console.WriteLine("Pipeline validation failed:");
                if (validation.Errors != null)
                {
                    foreach (var error in validation.Errors)
                    {
                        Console.WriteLine($"  Error: {error.Message}");
                    }
                }
                throw new PipelineValidationException("Pipeline validation failed", validation.Errors ?? new List<ValidationError>());
            }

            // Execute pipeline
            var result = await pipeline.ExecuteAsync(context);

            // Display results
            Console.WriteLine($"Pipeline execution completed: {(result.Success ? "SUCCESS" : "FAILED")}");
            Console.WriteLine($"Execution time: {result.Metrics.Duration.TotalMilliseconds:F2} ms");
            Console.WriteLine($"Memory usage: {result.Metrics.MemoryUsage.AllocatedBytes / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"Compute utilization: {result.Metrics.ComputeUtilization:P}");
            Console.WriteLine($"Output count: {result.Outputs.Count}");

            // Display stage performance
            Console.WriteLine("\nStage Performance:");
            foreach (var stageResult in result.StageResults)
            {
                Console.WriteLine($"  {stageResult.StageId}: {stageResult.Duration.TotalMilliseconds:F2} ms ({(stageResult.Success ? "OK" : "FAILED")})");
            }

            if (result.Errors != null)
            {
                Console.WriteLine("\nErrors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  {error.Severity}: {error.Message}");
                    if (error.StageId != null)
                    {
                        Console.WriteLine($"    Stage: {error.StageId}");
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pipeline execution failed with exception: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Example: Pipeline optimization workflow.
    /// </summary>
    public static async Task<IKernelPipeline> OptimizePipelineAsync(IKernelPipeline originalPipeline)
    {
        Console.WriteLine($"Optimizing pipeline: {originalPipeline.Name}");

        var optimizer = new PipelineOptimizer();
        var optimizationSettings = new PipelineOptimizationSettings
        {
            EnableKernelFusion = true,
            EnableStageReordering = true,
            EnableMemoryOptimization = true,
            EnableParallelMerging = true,
            Level = PipelineOptimizationLevel.Aggressive
        };

        var optimizedResult = await optimizer.OptimizeAsync(originalPipeline, optimizationSettings);

        Console.WriteLine($"Optimization completed:");
        Console.WriteLine($"  Applied optimizations: {optimizedResult.AppliedOptimizations.Count}");
        Console.WriteLine($"  Estimated speedup: {optimizedResult.EstimatedSpeedup:F2}x");
        Console.WriteLine($"  Estimated memory savings: {optimizedResult.EstimatedMemorySavings / 1024.0 / 1024.0:F2} MB");

        Console.WriteLine("\nOptimizations applied:");
        foreach (var optimization in optimizedResult.AppliedOptimizations)
        {
            Console.WriteLine($"  {optimization.Type}: {optimization.Description}");
            Console.WriteLine($"    Impact: {optimization.EstimatedImpact:P}");
            Console.WriteLine($"    Affected stages: {string.Join(", ", optimization.AffectedStages)}");
        }

        return optimizedResult.Pipeline;
    }
}

/// <summary>
/// Basic pipeline profiler implementation for examples.
/// </summary>
internal sealed class BasicPipelineProfiler : IPipelineProfiler
{
    private readonly Dictionary<string, DateTime> _executionStarts = [];
    private readonly Dictionary<string, DateTime> _stageStarts = [];

    public void StartPipelineExecution(string pipelineId, string executionId)
    {
        _executionStarts[executionId] = DateTime.UtcNow;
        Console.WriteLine($"[PROFILER] Pipeline {pipelineId} started (ID: {executionId})");
    }

    public void EndPipelineExecution(string executionId)
    {
        if (_executionStarts.TryGetValue(executionId, out var startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            Console.WriteLine($"[PROFILER] Pipeline completed in {duration.TotalMilliseconds:F2} ms");
        }
    }

    public void StartStageExecution(string executionId, string stageId)
    {
        _stageStarts[$"{executionId}_{stageId}"] = DateTime.UtcNow;
        Console.WriteLine($"[PROFILER] Stage {stageId} started");
    }

    public void EndStageExecution(string executionId, string stageId)
    {
        var key = $"{executionId}_{stageId}";
        if (_stageStarts.TryGetValue(key, out var startTime))
        {
            var duration = DateTime.UtcNow - startTime;
            Console.WriteLine($"[PROFILER] Stage {stageId} completed in {duration.TotalMilliseconds:F2} ms");
        }
    }

    public void RecordMemoryAllocation(string executionId, long bytes, string purpose) => Console.WriteLine($"[PROFILER] Memory allocated: {bytes / 1024.0 / 1024.0:F2} MB for {purpose}");

    public void RecordMemoryDeallocation(string executionId, long bytes) => Console.WriteLine($"[PROFILER] Memory released: {bytes / 1024.0 / 1024.0:F2} MB");

    public void RecordDataTransfer(string executionId, long bytes, TimeSpan duration, DataTransferType type)
    {
        var rate = bytes / duration.TotalSeconds / 1024.0 / 1024.0;
        Console.WriteLine($"[PROFILER] Data transfer ({type}): {bytes / 1024.0 / 1024.0:F2} MB in {duration.TotalMilliseconds:F2} ms ({rate:F2} MB/s)");
    }

    public void RecordKernelExecution(string executionId, KernelExecutionStats stats) => Console.WriteLine($"[PROFILER] Kernel {stats.KernelName}: {stats.ExecutionTime.TotalMilliseconds:F2} ms, {stats.WorkItemsProcessed} work items, {stats.ComputeUtilization:P} utilization");

    public void RecordCustomMetric(string executionId, string name, double value) => Console.WriteLine($"[PROFILER] Metric {name}: {value}");

    public ProfilingResults GetResults(string executionId)
    {
        // Return simplified results for this example
        return new ProfilingResults
        {
            ExecutionId = executionId,
            PipelineId = "example",
            Metrics = new PipelineExecutionMetrics
            {
                ExecutionId = executionId,
                StartTime = DateTime.UtcNow.AddMinutes(-1),
                EndTime = DateTime.UtcNow,
                Duration = TimeSpan.FromMinutes(1),
                MemoryUsage = new MemoryUsageStats
                {
                    AllocatedBytes = 1024 * 1024,
                    PeakBytes = 2 * 1024 * 1024,
                    AllocationCount = 10,
                    DeallocationCount = 8
                },
                ComputeUtilization = PerformanceMonitor.GetCpuUtilization(),
                MemoryBandwidthUtilization = PerformanceMonitor.GetMemoryBandwidthUtilization(),
                StageExecutionTimes = new Dictionary<string, TimeSpan>(),
                DataTransferTimes = new Dictionary<string, TimeSpan>()
            },
            Timeline = new List<TimelineEvent>()
        };
    }

    public AggregatedProfilingResults GetAggregatedResults(string pipelineId)
    {
        // Return simplified aggregated results for this example
        return new AggregatedProfilingResults
        {
            PipelineId = pipelineId,
            ExecutionCount = 1,
            Statistics = new StatisticalMetrics
            {
                Average = GetResults("example").Metrics,
                Median = GetResults("example").Metrics,
                StandardDeviation = GetResults("example").Metrics,
                Percentiles = new Dictionary<int, PipelineExecutionMetrics>()
            },
            Trends = new List<PerformanceTrend>(),
            CommonBottlenecks = new List<BottleneckInfo>()
        };
    }
}
