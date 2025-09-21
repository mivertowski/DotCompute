// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using DotCompute.Core.Pipelines;
using DotCompute.Linq.Extensions;
using DotCompute.Linq.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace DotCompute.Linq.Examples;
/// <summary>
/// Comprehensive example demonstrating the pipeline telemetry system with performance validation.
/// Shows how to achieve less than 1% overhead while collecting detailed metrics.
/// </summary>
public class PipelineTelemetryExample
{
    /// <summary>
    /// Demonstrates comprehensive pipeline telemetry with performance validation.
    /// </summary>
    public static async Task<ExampleResult> RunComprehensiveExampleAsync()
    {
        // Setup dependency injection with telemetry
        var services = new ServiceCollection();
        ConfigureTelemetryServices(services);
        var serviceProvider = services.BuildServiceProvider();
        var metricsService = serviceProvider.GetRequiredService<PipelineMetricsService>();
        var logger = serviceProvider.GetRequiredService<ILogger<PipelineTelemetryExample>>();
        logger.LogInformation("Starting comprehensive pipeline telemetry example");
        var results = new List<PipelineExecutionResult>();
        var overallStopwatch = Stopwatch.StartNew();
        try
        {
            // Run multiple pipeline executions to validate performance
            for (var i = 0; i < 1000; i++)
            {
                var result = await ExecuteSamplePipelineWithTelemetryAsync(
                    metricsService,
                    $"sample-pipeline-{i % 10}", // 10 different pipeline types
                    logger);
                results.Add(result);
                // Simulate some processing delay
                if (i % 100 == 0)
                {
                    await Task.Delay(1); // Small delay every 100 iterations
                }
            }
            overallStopwatch.Stop();
            // Analyze performance overhead
            var overheadStats = metricsService.GetOverheadStats();
            logger.LogInformation("Performance overhead analysis: {@OverheadStats}", overheadStats);
            // Validate <1% overhead requirement
            var meetsRequirement = overheadStats.MeetsPerformanceRequirement;
            logger.LogInformation(
                "Performance requirement (<1% overhead) {Status}: {Percentage:F4}%",
                meetsRequirement ? "MET" : "NOT MET",
                overheadStats.OverheadPercentage);
            // Export metrics in different formats
            var jsonMetrics = await metricsService.ExportMetricsAsync(MetricsExportFormat.Json);
            var prometheusMetrics = await metricsService.ExportMetricsAsync(MetricsExportFormat.Prometheus);
            var openTelemetryMetrics = await metricsService.ExportMetricsAsync(MetricsExportFormat.OpenTelemetry);
            // Analyze collected metrics
            var pipelineMetrics = metricsService.GetAllPipelineMetrics();
            return new ExampleResult
                TotalExecutions = results.Count,
                TotalTime = overallStopwatch.Elapsed,
                OverheadStats = overheadStats,
                MeetsPerformanceRequirement = meetsRequirement,
                PipelineMetrics = pipelineMetrics,
                ExportedMetrics = new Dictionary<string, string>
                    ["json"] = jsonMetrics,
                    ["prometheus"] = prometheusMetrics,
                    ["opentelemetry"] = openTelemetryMetrics
                },
                AverageExecutionTime = results.Average(r => r.ExecutionTime.TotalMilliseconds),
                CacheHitRateOverall = pipelineMetrics.Values.Average(m => m.CacheHitRatio),
                ThroughputOverall = results.Sum(r => r.ItemsProcessed) / overallStopwatch.Elapsed.TotalSeconds
            };
        }
        finally
            await serviceProvider.DisposeAsync();
    }
    /// Executes a sample pipeline with comprehensive telemetry collection.
    private static async Task<PipelineExecutionResult> ExecuteSamplePipelineWithTelemetryAsync(
        PipelineMetricsService metricsService,
        string pipelineId,
        ILogger logger)
        var executionStopwatch = Stopwatch.StartNew();
        var context = metricsService.CreateMetricsContext(
            pipelineId,
            metadata: new Dictionary<string, object>
                ["example"] = true,
                ["timestamp"] = DateTime.UtcNow,
                ["version"] = "1.0.0"
            });
            // Stage 1: Data Loading with cache simulation
            var stage1Result = await context.MeasureStageAsync(
                metricsService,
                "data-loading",
                "Data Loading Stage",
                async () =>
                    // Simulate cache access
                    var cacheKey = $"data-{pipelineId}";
                    var cacheHit = Random.Shared.NextDouble() > 0.3; // 70% hit rate
                    context.RecordCacheAccess(metricsService, cacheKey, cacheHit);
                    if (cacheHit)
                    {
                        await Task.Delay(1); // Fast cache retrieval
                        return GenerateSampleData(100);
                    }
                    else
                        await Task.Delay(5); // Slower data loading
                });
            // Record throughput for stage 1
            metricsService.RecordThroughput(context, stage1Result.Length);
            // Stage 2: Data Processing
            var stage2Result = await context.MeasureStageAsync(
                "data-processing",
                "Data Processing Stage",
                    await Task.Delay(Random.Shared.Next(1, 10)); // Simulate variable processing time
                    // Simulate some computation
                    return stage1Result.Select(x => x * 2 + Random.Shared.NextDouble()).ToArray();
            // Record throughput for stage 2
            metricsService.RecordThroughput(context, stage2Result.Length);
            // Stage 3: Result Aggregation with more cache access
            var stage3Result = await context.MeasureStageAsync(
                "aggregation",
                "Result Aggregation Stage",
                    // Multiple cache accesses for aggregation
                    for (var i = 0; i < 5; i++)
                        var cacheKey = $"agg-{pipelineId}-{i}";
                        var cacheHit = Random.Shared.NextDouble() > 0.2; // 80% hit rate for aggregation
                        context.RecordCacheAccess(metricsService, cacheKey, cacheHit);
                    await Task.Delay(2); // Aggregation processing
                    return new
                        Sum = stage2Result.Sum(),
                        Average = stage2Result.Average(),
                        Count = stage2Result.Length,
                        Min = stage2Result.Min(),
                        Max = stage2Result.Max()
                    };
            // Complete pipeline execution
            metricsService.CompleteExecution(context, success: true);
            executionStopwatch.Stop();
            return new PipelineExecutionResult
                PipelineId = pipelineId,
                Success = true,
                ExecutionTime = executionStopwatch.Elapsed,
                ItemsProcessed = stage1Result.Length,
                StagesCompleted = 3,
                Result = stage3Result
        catch (Exception ex)
            logger.LogError(ex, "Pipeline execution failed for {PipelineId}", pipelineId);
            metricsService.CompleteExecution(context, success: false, exception: ex);
                Success = false,
                ItemsProcessed = 0,
                StagesCompleted = 0,
                Exception = ex
            context.Dispose();
    /// Configures telemetry services for the example.
    private static void ConfigureTelemetryServices(IServiceCollection services)
        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        // Add comprehensive pipeline telemetry
        services.AddComprehensivePipelineTelemetry(options =>
            options.EnableDistributedTracing = true;
            options.EnableDetailedTelemetry = true;
            options.EnablePeriodicExport = false; // Disable for example
            options.DefaultExportFormat = MetricsExportFormat.Json;
            options.IntegrateWithGlobalTelemetry = false; // No global telemetry in example
            options.EnableOverheadMonitoring = true; // Critical for performance validation
        });
        // Add pipeline telemetry options
        services.Configure<PipelineMetricsOptions>(options =>
            options.EnableOverheadMonitoring = true;
    /// Generates sample data for pipeline processing.
    private static double[] GenerateSampleData(int count)
        return Enumerable.Range(0, count)
            .Select(i => Random.Shared.NextDouble() * 100)
            .ToArray();
    /// Validates the performance overhead meets requirements.
    public static ValidationResult ValidatePerformanceRequirements(PerformanceOverheadStats stats)
        var requirements = new[]
            new Requirement("Overhead < 1%", stats.OverheadPercentage < 1.0, stats.OverheadPercentage),
            new Requirement("Average operation < 10μs", stats.AverageOverheadPerOperation.TotalMicroseconds < 10, stats.AverageOverheadPerOperation.TotalMicroseconds),
            new Requirement("Total operations > 1000", stats.TotalOperations > 1000, stats.TotalOperations)
        };
        return new ValidationResult
            AllRequirementsMet = requirements.All(r => r.Met),
            Requirements = requirements.ToList(),
            OverallScore = requirements.Count(r => r.Met) / (double)requirements.Length,
            Summary = $"Performance validation: {requirements.Count(r => r.Met)}/{requirements.Length} requirements met"
}
/// Result of the comprehensive telemetry example.
public sealed class ExampleResult
    public int TotalExecutions { get; set; }
    public TimeSpan TotalTime { get; set; }
    public PerformanceOverheadStats OverheadStats { get; set; } = null!;
    public bool MeetsPerformanceRequirement { get; set; }
    public IReadOnlyDictionary<string, DotCompute.Core.Pipelines.Interfaces.IPipelineMetrics> PipelineMetrics { get; set; } = null!;
    public Dictionary<string, string> ExportedMetrics { get; set; } = [];
    public double AverageExecutionTime { get; set; }
    public double CacheHitRateOverall { get; set; }
    public double ThroughputOverall { get; set; }
    public void PrintSummary()
        Console.WriteLine($"=== Pipeline Telemetry Example Results ===");
        Console.WriteLine($"Total Executions: {TotalExecutions}");
        Console.WriteLine($"Total Time: {TotalTime.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Average Execution Time: {AverageExecutionTime:F2}ms");
        Console.WriteLine($"Overall Throughput: {ThroughputOverall:F2} items/sec");
        Console.WriteLine($"Overall Cache Hit Rate: {CacheHitRateOverall:P2}");
        Console.WriteLine($"Performance Overhead: {OverheadStats.OverheadPercentage:F4}%");
        Console.WriteLine($"Meets <1% Requirement: {MeetsPerformanceRequirement}");
        Console.WriteLine($"Total Metrics Operations: {OverheadStats.TotalOperations:N0}");
        Console.WriteLine($"Avg Operation Overhead: {OverheadStats.AverageOverheadPerOperation.TotalMicroseconds:F2}μs");
        Console.WriteLine($"Pipeline Types Monitored: {PipelineMetrics.Count}");
/// Result of a single pipeline execution.
public sealed class PipelineExecutionResult
    public string PipelineId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public long ItemsProcessed { get; set; }
    public int StagesCompleted { get; set; }
    public object? Result { get; set; }
    public Exception? Exception { get; set; }
/// Performance requirement for validation.
public sealed class Requirement
    public string Description { get; set; }
    public bool Met { get; set; }
    public double ActualValue { get; set; }
    public Requirement(string description, bool met, double actualValue)
        Description = description;
        Met = met;
        ActualValue = actualValue;
/// Result of performance validation.
public sealed class ValidationResult
    public bool AllRequirementsMet { get; set; }
    public List<Requirement> Requirements { get; set; } = [];
    public double OverallScore { get; set; }
    public string Summary { get; set; } = string.Empty;
    public void PrintValidationResults()
        Console.WriteLine($"\n=== Performance Validation Results ===");
        Console.WriteLine($"Overall Score: {OverallScore:P0} ({Requirements.Count(r => r.Met)}/{Requirements.Count} requirements met)");
        Console.WriteLine($"All Requirements Met: {AllRequirementsMet}");
        Console.WriteLine("\nDetailed Requirements:");
        foreach (var req in Requirements)
            var status = req.Met ? "✅ PASS" : "❌ FAIL";
            Console.WriteLine($"  {status} - {req.Description} (Actual: {req.ActualValue:F4})");
        Console.WriteLine($"\n{Summary}");
