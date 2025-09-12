// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Linq;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Extensions;
using DotCompute.Linq.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotCompute.Linq.Examples;

/// <summary>
/// Comprehensive examples demonstrating basic usage of the DotCompute.Linq pipeline system.
/// These examples show common patterns and best practices for GPU-accelerated data processing.
/// </summary>
/// <remarks>
/// <para>
/// This class provides practical examples that developers can use as templates for their own
/// applications. All examples include error handling, performance monitoring, and follow
/// established best practices for GPU computing.
/// </para>
/// <para>
/// Prerequisites: Before running these examples, ensure that the DotCompute runtime is
/// properly configured with the necessary services and that compatible hardware (GPU or
/// CPU with SIMD support) is available.
/// </para>
/// </remarks>
public class BasicUsageExamples
{
    #region Data Types for Examples

    /// <summary>
    /// Sample data structure optimized for GPU processing.
    /// Uses value types and simple fields for best performance.
    /// </summary>
    public readonly struct SampleDataItem
    {
        public readonly int Id;
        public readonly float Value;
        public readonly bool IsActive;
        public readonly int Category;

        public SampleDataItem(int id, float value, bool isActive, int category)
        {
            Id = id;
            Value = value;
            IsActive = isActive;
            Category = category;
        }
    }

    /// <summary>
    /// Result structure for processed data.
    /// </summary>
    public readonly struct ProcessedResult
    {
        public readonly int Id;
        public readonly double ProcessedValue;
        public readonly int Category;

        public ProcessedResult(int id, double processedValue, int category)
        {
            Id = id;
            ProcessedValue = processedValue;
            Category = category;
        }
    }

    /// <summary>
    /// Aggregation result structure.
    /// </summary>
    public readonly struct CategorySummary
    {
        public readonly int Category;
        public readonly int Count;
        public readonly double Average;
        public readonly double Sum;
        public readonly double Max;
        public readonly double Min;

        public CategorySummary(int category, int count, double average, double sum, double max, double min)
        {
            Category = category;
            Count = count;
            Average = average;
            Sum = sum;
            Max = max;
            Min = min;
        }
    }

    #endregion

    #region Basic LINQ Operations

    /// <summary>
    /// Demonstrates basic filtering and transformation operations with GPU acceleration.
    /// Shows how to convert collections to compute queryables and execute simple operations.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    public static async Task BasicFilterAndTransformAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting basic filter and transform example");

        try
        {
            // Generate sample data (1 million items)
            var data = GenerateSampleData(1_000_000);
            logger.LogInformation("Generated {Count} sample data items", data.Length);

            // Convert to GPU-accelerated queryable
            var queryable = data.AsComputeQueryable(services);

            // Apply filtering and transformation
            var stopwatch = Stopwatch.StartNew();
            var results = await queryable
                .Where(item => item.IsActive && item.Value > 100.0f)
                .Select(item => new ProcessedResult(
                    item.Id,
                    item.Value * 2.5, // Transform the value
                    item.Category))
                .ExecuteAsync();

            stopwatch.Stop();

            var resultArray = results.ToArray();
            logger.LogInformation("Processed {InputCount} items to {OutputCount} results in {ElapsedMs}ms",
                data.Length, resultArray.Length, stopwatch.ElapsedMilliseconds);

            // Demonstrate performance comparison with standard LINQ
            await CompareWithStandardLinq(data, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in basic filter and transform example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates GPU-accelerated aggregation operations including sum, average, and custom aggregations.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task AggregationOperationsAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting aggregation operations example");

        try
        {
            // Generate numeric data for aggregation
            var numbers = Enumerable.Range(1, 10_000_000)
                .Select(i => i * 1.5f)
                .ToArray();

            var queryable = numbers.AsComputeQueryable(services);

            var stopwatch = Stopwatch.StartNew();

            // GPU-accelerated sum
            var sum = queryable.ComputeSum();
            logger.LogInformation("GPU Sum: {Sum}", sum);

            // GPU-accelerated average
            var average = queryable.ComputeAverage();
            logger.LogInformation("GPU Average: {Average}", average);

            // Complex aggregation with grouping
            var data = GenerateSampleData(1_000_000);
            var groupedResults = await data.AsComputeQueryable(services)
                .Where(item => item.IsActive)
                .GroupBy(item => item.Category)
                .Select(g => new CategorySummary(
                    g.Key,
                    g.Count(),
                    g.Average(x => x.Value),
                    g.Sum(x => x.Value),
                    g.Max(x => x.Value),
                    g.Min(x => x.Value)))
                .ExecuteAsync();

            stopwatch.Stop();

            var summaries = groupedResults.ToArray();
            logger.LogInformation("Generated {Count} category summaries in {ElapsedMs}ms",
                summaries.Length, stopwatch.ElapsedMilliseconds);

            // Display results
            foreach (var summary in summaries.Take(5))
            {
                logger.LogInformation("Category {Category}: Count={Count}, Avg={Average:F2}, Sum={Sum:F2}",
                    summary.Category, summary.Count, summary.Average, summary.Sum);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in aggregation operations example");
            throw;
        }
    }

    #endregion

    #region Pipeline Operations

    /// <summary>
    /// Demonstrates advanced pipeline operations with optimization and caching.
    /// Shows how to create, optimize, and execute complex data processing pipelines.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task AdvancedPipelineOperationsAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting advanced pipeline operations example");

        try
        {
            var data = GenerateSampleData(2_000_000);

            // Create optimized pipeline
            var pipeline = data.AsComputePipeline(services)
                .ThenWhere<SampleDataItem>(item => item.IsActive)
                .ThenSelect<SampleDataItem, ProcessedResult>(item => new ProcessedResult(
                    item.Id,
                    Math.Sqrt(item.Value * item.Value + 100), // Complex mathematical operation
                    item.Category))
                .ThenWhere<ProcessedResult>(result => result.ProcessedValue > 50.0)
                .WithIntelligentCaching<ProcessedResult>();

            // Optimize the pipeline
            var optimizedPipeline = await pipeline.OptimizeQueryPlanAsync(services);

            // Execute with aggressive optimization
            var stopwatch = Stopwatch.StartNew();
            // Execute the optimized pipeline
            var resultsQuery = await ((dynamic)optimizedPipeline).ExecuteAsync();
            var results = ((IQueryable<ProcessedResult>)resultsQuery).ToArray();
            stopwatch.Stop();

            logger.LogInformation("Pipeline executed {Count} results in {ElapsedMs}ms with optimization",
                results.Length, stopwatch.ElapsedMilliseconds);

            // Demonstrate pipeline reuse (should be faster due to caching)
            stopwatch.Restart();
            // Execute cached pipeline (reuse optimized pipeline)
            var cachedQuery = await ((dynamic)optimizedPipeline).ExecuteAsync();
            var cachedResults = ((IQueryable<ProcessedResult>)cachedQuery).ToArray();
            stopwatch.Stop();

            logger.LogInformation("Cached pipeline executed {Count} results in {ElapsedMs}ms",
                cachedResults.Length, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in advanced pipeline operations example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates streaming pipeline operations for real-time data processing.
    /// Shows how to handle continuous data streams with batching and windowing.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task StreamingPipelineAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting streaming pipeline example");

        try
        {
            // Create a streaming data source
            var streamingData = GenerateStreamingDataAsync(10_000);

            // Configure streaming pipeline with batching
            var streamingPipeline = streamingData.AsStreamingPipeline(
                services,
                batchSize: 1000, // Process in batches of 1000 items
                windowSize: TimeSpan.FromSeconds(2)); // 2-second sliding windows

            var processedCount = 0;
            var totalValue = 0.0;
            var stopwatch = Stopwatch.StartNew();

            // Process streaming data
            await foreach (var batch in streamingPipeline.Take(5000)) // Process 5000 items
            {
                processedCount++;
                totalValue += batch.Value;

                // Log progress every 1000 items
                if (processedCount % 1000 == 0)
                {
                    logger.LogInformation("Processed {Count} streaming items, average value: {Average:F2}",
                        processedCount, totalValue / processedCount);
                }
            }

            stopwatch.Stop();
            logger.LogInformation("Streaming pipeline processed {Count} items in {ElapsedMs}ms",
                processedCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming pipeline example");
            throw;
        }
    }

    #endregion

    #region Performance Analysis

    /// <summary>
    /// Demonstrates performance analysis and optimization suggestion features.
    /// Shows how to analyze queries for optimization opportunities and backend selection.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task PerformanceAnalysisAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting performance analysis example");

        try
        {
            var data = GenerateSampleData(500_000);

            // Create a complex query for analysis
            var queryable = data.AsComputeQueryable(services)
                .Where(item => item.IsActive)
                .Select(item => new { item.Id, ProcessedValue = item.Value * Math.Sin(item.Id) })
                .Where(item => item.ProcessedValue > 0)
                .GroupBy(item => item.Id % 10)
                .Select(g => new { Category = g.Key, Average = g.Average(x => x.ProcessedValue) });

            // Check GPU compatibility
            var isGpuCompatible = queryable.IsGpuCompatible(services);
            logger.LogInformation("Query GPU compatible: {IsCompatible}", isGpuCompatible);

            // Get optimization suggestions
            var suggestions = queryable.GetOptimizationSuggestions(services);
            logger.LogInformation("Optimization suggestions:");
            foreach (var suggestion in suggestions.Take(5))
            {
                logger.LogInformation("  {Severity}: {Message} (Impact: {Impact:P})",
                    suggestion.Severity, suggestion.Message, suggestion.EstimatedImpact);
            }

            // Analyze pipeline performance
            var performanceReport = await queryable.AnalyzePipelinePerformanceAsync(services);
            logger.LogInformation("Performance Analysis Results:");
            logger.LogInformation("  Estimated execution time: {Time}ms",

                performanceReport.EstimatedExecutionTime.TotalMilliseconds);
            logger.LogInformation("  Estimated memory usage: {Memory}MB",

                performanceReport.EstimatedMemoryUsage / (1024.0 * 1024.0));

            // Get backend recommendation
            var backendRecommendation = await queryable.RecommendOptimalBackendAsync(services);
            logger.LogInformation("  Recommended backend: {Backend} (Confidence: {Confidence:P})",
                backendRecommendation.RecommendedBackend, backendRecommendation.Confidence);

            // Get memory estimate
            var memoryEstimate = await queryable.EstimateMemoryUsageAsync(services);
            logger.LogInformation("  Peak memory estimate: {Memory}MB",
                memoryEstimate.PeakMemoryUsage / (1024.0 * 1024.0));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in performance analysis example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates pre-compilation for improved performance in production scenarios.
    /// Shows how to pre-compile frequently used queries to reduce runtime overhead.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task PrecompilationExampleAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting pre-compilation example");

        try
        {
            var data = GenerateSampleData(100_000);

            // Define frequently used queries
            var frequentQueries = new IQueryable[]
            {
                data.AsComputeQueryable(services).Where(x => x.IsActive),
                data.AsComputeQueryable(services).Select(x => new { ProcessedValue = x.Value * 2.0f }),
                data.AsComputeQueryable(services).Where(x => x.Category < 5)
            };

            // Pre-compile queries
            var precompileStopwatch = Stopwatch.StartNew();
            await Task.WhenAll(frequentQueries.Select(q => q.PrecompileAsync(services)));
            precompileStopwatch.Stop();

            logger.LogInformation("Pre-compilation completed in {ElapsedMs}ms",

                precompileStopwatch.ElapsedMilliseconds);

            // Execute pre-compiled queries (should be faster)
            var executionTimes = new List<long>();

            foreach (var query in frequentQueries)
            {
                var stopwatch = Stopwatch.StartNew();
                var results = await query.ExecuteAsync();
                stopwatch.Stop();


                executionTimes.Add(stopwatch.ElapsedMilliseconds);
                logger.LogInformation("Pre-compiled query executed in {ElapsedMs}ms, {Count} results",
                    stopwatch.ElapsedMilliseconds, results.Count());
            }

            logger.LogInformation("Average execution time for pre-compiled queries: {AverageMs}ms",
                executionTimes.Average());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in pre-compilation example");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates sample data for demonstrations.
    /// </summary>
    /// <param name="count">Number of items to generate.</param>
    /// <returns>Array of sample data items.</returns>
    private static SampleDataItem[] GenerateSampleData(int count)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        return Enumerable.Range(0, count)
            .Select(i => new SampleDataItem(
                id: i,
                value: (float)(random.NextDouble() * 1000),
                isActive: random.NextDouble() > 0.3, // 70% active
                category: random.Next(0, 10)))
            .ToArray();
    }

    /// <summary>
    /// Generates streaming data for real-time processing demonstrations.
    /// </summary>
    /// <param name="count">Number of items to generate.</param>
    /// <returns>Async enumerable of sample data items.</returns>
    private static async IAsyncEnumerable<SampleDataItem> GenerateStreamingDataAsync(int count)
    {
        var random = new Random(42);


        for (int i = 0; i < count; i++)
        {
            // Simulate streaming delay
            await Task.Delay(1);


            yield return new SampleDataItem(
                id: i,
                value: (float)(random.NextDouble() * 1000 + Math.Sin(i * 0.1) * 100),
                isActive: random.NextDouble() > 0.2,
                category: random.Next(0, 5));
        }
    }

    /// <summary>
    /// Compares GPU-accelerated operations with standard LINQ for performance analysis.
    /// </summary>
    /// <param name="data">Sample data to process.</param>
    /// <param name="logger">Logger for output.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static Task CompareWithStandardLinq(SampleDataItem[] data, ILogger logger)
    {
        // Standard LINQ performance
        var standardStopwatch = Stopwatch.StartNew();
        var standardResults = data
            .Where(item => item.IsActive && item.Value > 100.0f)
            .Select(item => new ProcessedResult(
                item.Id,
                item.Value * 2.5,
                item.Category))
            .ToArray();
        standardStopwatch.Stop();

        logger.LogInformation("Standard LINQ: {Count} results in {ElapsedMs}ms",
            standardResults.Length, standardStopwatch.ElapsedMilliseconds);

        // Note: GPU-accelerated timing was already logged in the calling method
        // This comparison helps demonstrate the performance difference
        return Task.CompletedTask;
    }

    #endregion

    #region Configuration and Setup

    /// <summary>
    /// Example of how to configure services for DotCompute.Linq.
    /// This method shows the typical service registration needed for the examples.
    /// </summary>
    /// <returns>Configured service provider.</returns>
    public static IServiceProvider CreateExampleServiceProvider()
    {
        var services = new ServiceCollection();

        // Add DotCompute services
        services.AddDotComputeLinq(options =>
        {
            options.EnableCaching = true;
            options.EnableProfiling = true;
            options.OptimizationLevel = DotCompute.Abstractions.Types.OptimizationLevel.Balanced;
            options.EnableAutoFallback = true;
        });

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs all basic usage examples in sequence.
    /// This method can be used as a comprehensive demonstration of the pipeline system.
    /// </summary>
    /// <param name="services">Configured service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunAllExamplesAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting comprehensive DotCompute.Linq examples");

        try
        {
            await BasicFilterAndTransformAsync(services);
            await AggregationOperationsAsync(services);
            await AdvancedPipelineOperationsAsync(services);
            await StreamingPipelineAsync(services);
            await PerformanceAnalysisAsync(services);
            await PrecompilationExampleAsync(services);

            logger.LogInformation("All examples completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error running examples");
            throw;
        }
    }

    #endregion
}

/// <summary>
/// Extension methods to support the examples with additional functionality.
/// </summary>
public static class ExampleExtensions
{
    /// <summary>
    /// Creates a simple benchmark for comparing different execution strategies.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="data">Data to process.</param>
    /// <param name="operation">Operation to benchmark.</param>
    /// <param name="iterations">Number of iterations to run.</param>
    /// <returns>Average execution time in milliseconds.</returns>
    public static async Task<double> BenchmarkAsync<T>(
        this T[] data,
        Func<T[], Task<object>> operation,
        int iterations = 5)
    {
        var times = new List<long>();

        // Warm up
        await operation(data);

        // Measure iterations
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await operation(data);
            stopwatch.Stop();
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        return times.Average();
    }
}