// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Abstractions.Types;
using DotCompute.Linq.Extensions;
using DotCompute.Linq.Pipelines;
using DotCompute.Linq.Pipelines.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IKernelPipeline = DotCompute.Core.Pipelines.IKernelPipeline;

namespace DotCompute.Linq.Examples;

/// <summary>
/// Comprehensive examples demonstrating error handling patterns and recovery strategies
/// in the DotCompute.Linq pipeline system. Shows how to handle common error scenarios
/// and implement robust, production-ready error handling.
/// </summary>
/// <remarks>
/// <para>
/// This class demonstrates best practices for error handling in GPU-accelerated computing scenarios,
/// including automatic fallback strategies, validation patterns, and monitoring approaches.
/// All examples include comprehensive logging and recovery mechanisms.
/// </para>
/// <para>
/// Error Categories Covered:
/// - GPU memory exhaustion and resource limitations
/// - Unsupported operations and backend compatibility issues  
/// - Compilation errors and kernel generation failures
/// - Runtime execution errors and timeout scenarios
/// - Data validation and type compatibility issues
/// </para>
/// </remarks>
public class ErrorHandlingExamples
{
    #region Data Types for Error Scenarios

    /// <summary>
    /// Test data structure that may cause GPU compatibility issues.
    /// </summary>
    public struct ProblematicDataItem
    {
        public int Id;
        public string Name; // Reference type - problematic for GPU
        public float[] Values; // Array field - may cause issues
        public DateTime Timestamp; // Complex type
        public decimal Money; // Not GPU-friendly type

        public ProblematicDataItem(int id, string name, float[] values, DateTime timestamp, decimal money)
        {
            Id = id;
            Name = name;
            Values = values;
            Timestamp = timestamp;
            Money = money;
        }
    }

    /// <summary>
    /// GPU-optimized version of the data structure.
    /// </summary>
    public readonly struct OptimizedDataItem
    {
        public readonly int Id;
        public readonly float Value;
        public readonly long Timestamp; // Unix timestamp
        public readonly double Money; // GPU-compatible type

        public OptimizedDataItem(int id, float value, long timestamp, double money)
        {
            Id = id;
            Value = value;
            Timestamp = timestamp;
            Money = money;
        }
    }

    #endregion

    #region Memory and Resource Error Handling

    /// <summary>
    /// Demonstrates handling of GPU memory exhaustion scenarios with automatic fallback
    /// and data size optimization strategies.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleMemoryExhaustionAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting memory exhaustion handling example");

        try
        {
            // Create a dataset that may exceed GPU memory
            var largeDataset = GenerateLargeDataset(10_000_000); // 10M items
            logger.LogInformation("Generated dataset with {Count} items (~{Size}MB)",

                largeDataset.Length,

                largeDataset.Length * sizeof(float) / (1024 * 1024));

            // Check memory requirements before execution
            var queryable = largeDataset.AsComputeQueryable(services);
            var memoryEstimate = await queryable.EstimateMemoryUsageAsync(services);

            logger.LogInformation("Estimated memory usage: Peak={Peak}MB, Average={Average}MB",
                memoryEstimate.PeakMemoryUsage / (1024 * 1024),
                memoryEstimate.AverageMemoryUsage / (1024 * 1024));

            // Strategy 1: Use memory limits to prevent exhaustion
            var limitedQueryable = queryable.WithMemoryLimit(512 * 1024 * 1024); // 512MB limit

            try
            {
                var results = await limitedQueryable
                    .Where(x => x > 0.5f)
                    .Select(x => x * 2.0f)
                    .ExecuteAsync();

                logger.LogInformation("Successfully processed {Count} items with memory limits",

                    results.Count());
            }
            catch (OutOfMemoryException ex)
            {
                logger.LogWarning("Memory limit exceeded, switching to streaming approach: {Message}",

                    ex.Message);

                // Strategy 2: Use streaming processing for large datasets
                await ProcessLargeDatasetWithStreamingAsync(largeDataset, services, logger);
            }

            // Strategy 3: Batch processing
            await ProcessInBatchesAsync(largeDataset, services, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in memory exhaustion handling example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates handling of GPU device errors and automatic backend fallback.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleDeviceErrorsAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting device error handling example");

        try
        {
            var data = Enumerable.Range(1, 1_000_000).Select(x => (float)x).ToArray();

            // Try GPU execution with fallback handling
            var queryable = data.AsComputeQueryable(services);

            try
            {
                // Prefer GPU execution
                var results = await queryable.ExecuteAsync("CUDA");
                logger.LogInformation("GPU execution successful: {Count} results", results.Count());
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("CUDA"))
            {
                logger.LogWarning("GPU execution failed, falling back to CPU: {Message}", ex.Message);

                // Automatic fallback to CPU
                var results = await queryable.ExecuteAsync("CPU");
                logger.LogInformation("CPU fallback execution successful: {Count} results", results.Count());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Both GPU and CPU execution failed");

                // Final fallback to standard LINQ
                logger.LogInformation("Falling back to standard LINQ");
                var results = data.Where(x => x > 0).Select(x => x * 2.0f).ToArray();
                logger.LogInformation("Standard LINQ fallback successful: {Count} results", results.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in device error handling example");
            throw;
        }
    }

    #endregion

    #region Compatibility and Validation Errors

    /// <summary>
    /// Demonstrates handling of GPU compatibility issues and data type validation.
    /// Shows how to detect and resolve common compatibility problems.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleCompatibilityIssuesAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting compatibility issues handling example");

        try
        {
            // Create problematic data that may not be GPU-compatible
            var problematicData = GenerateProblematicData(10000);

            var queryable = problematicData.AsComputeQueryable(services);

            // Check GPU compatibility before execution
            var isCompatible = queryable.IsGpuCompatible(services);
            logger.LogInformation("Data GPU compatible: {IsCompatible}", isCompatible);

            if (!isCompatible)
            {
                // Get specific compatibility issues
                var suggestions = queryable.GetOptimizationSuggestions(services);
                var compatibilityIssues = suggestions.Where(s => s.Category == "GPU Compatibility");

                logger.LogWarning("GPU compatibility issues found:");
                foreach (var issue in compatibilityIssues)
                {
                    logger.LogWarning("  {Severity}: {Message}", issue.Severity, issue.Message);
                }

                // Strategy 1: Convert to GPU-compatible format
                var optimizedData = ConvertToGpuCompatibleFormat(problematicData);
                var optimizedQueryable = optimizedData.AsComputeQueryable(services);

                var optimizedCompatible = optimizedQueryable.IsGpuCompatible(services);
                logger.LogInformation("Optimized data GPU compatible: {IsCompatible}", optimizedCompatible);

                if (optimizedCompatible)
                {
                    var results = await optimizedQueryable
                        .Where(x => x.Value > 100)
                        .ExecuteAsync();


                    logger.LogInformation("Successfully processed optimized data: {Count} results",

                        results.Count());
                }
            }

            // Strategy 2: Validate and handle unsupported operations
            await HandleUnsupportedOperationsAsync(services, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in compatibility issues handling example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates comprehensive pipeline validation and error analysis.
    /// Shows how to validate entire pipelines before execution.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task PipelineValidationAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting pipeline validation example");

        try
        {
            var errorHandler = services.GetService<IPipelineErrorHandler>();
            if (errorHandler == null)
            {
                logger.LogWarning("Pipeline error handler not available, using basic validation");
                return;
            }

            // Create a complex pipeline that may have issues
            var data = Enumerable.Range(1, 100_000).Select(x => (float)x).ToArray();
            var pipeline = data.AsComputePipeline(services)
                .ThenWhere<float>(x => x > 0)
                .ThenSelect<float, double>(x => Math.Sqrt(x)) // Potentially expensive operation
                .ThenWhere<double>(x => !double.IsNaN(x))
                .ThenSelect<double, float>(x => (float)(x * Math.PI));

            // Validate the entire pipeline (simplified for demo)
            try
            {
                var kernelPipeline = pipeline as IKernelPipeline;
                if (kernelPipeline != null)
                {
                    var validationResult = await errorHandler.ValidatePipelineAsync(kernelPipeline);


                    if (validationResult.IsValid)
                    {
                        logger.LogInformation("Pipeline validation passed");
                        var results = await kernelPipeline.ExecutePipelineAsync<float[]>();
                        logger.LogInformation("Pipeline executed successfully: {Count} results", results.Length);
                    }
                    else
                    {
                        logger.LogWarning("Pipeline validation failed with {ErrorCount} errors", validationResult.Errors.Count);
                    }
                }
                else
                {
                    logger.LogInformation("Pipeline validation skipped - IKernelPipeline interface not available");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Pipeline validation failed: {Message}", ex.Message);
            }

            // Demonstrate expression-level error analysis
            await AnalyzeExpressionErrorsAsync(errorHandler, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in pipeline validation example");
            throw;
        }
    }

    #endregion

    #region Runtime and Execution Errors

    /// <summary>
    /// Demonstrates handling of runtime execution errors including timeouts,
    /// kernel compilation failures, and execution exceptions.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleRuntimeErrorsAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting runtime error handling example");

        try
        {
            var data = Enumerable.Range(1, 1_000_000).Select(x => (float)x).ToArray();
            var queryable = data.AsComputeQueryable(services);

            // Test timeout handling
            await HandleTimeoutScenariosAsync(queryable, logger);

            // Test kernel compilation errors
            await HandleCompilationErrorsAsync(queryable, services, logger);

            // Test execution exceptions
            await HandleExecutionExceptionsAsync(queryable, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in runtime error handling example");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates comprehensive error monitoring and recovery strategies.
    /// Shows how to implement production-ready error handling with monitoring.
    /// </summary>
    /// <param name="services">Configured service provider with DotCompute services.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ComprehensiveErrorMonitoringAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting comprehensive error monitoring example");

        var errorCount = 0;
        var successCount = 0;
        var fallbackCount = 0;

        try
        {
            var data = Enumerable.Range(1, 100_000).Select(x => (float)x).ToArray();
            var scenarios = new (string Name, Func<float[], IServiceProvider, Task> Operation)[]
            {
                ("Normal Operation", ExecuteNormalOperationAsync),
                ("Memory Intensive Operation", ExecuteMemoryIntensiveOperationAsync),
                ("Complex Mathematical Operation", ExecuteComplexMathOperationAsync),
                ("Large Dataset Operation", ExecuteLargeDatasetOperationAsync),
                ("Potentially Problematic Operation", ExecuteProblematicOperationAsync)
            };

            foreach (var (name, operation) in scenarios)
            {
                logger.LogInformation("Testing scenario: {Scenario}", name);

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    await operation(data, services);
                    stopwatch.Stop();

                    successCount++;
                    logger.LogInformation("  ✓ Success: {Scenario} completed in {ElapsedMs}ms",

                        name, stopwatch.ElapsedMilliseconds);
                }
                catch (OutOfMemoryException ex)
                {
                    logger.LogWarning("  ⚠ Memory issue in {Scenario}: {Message}", name, ex.Message);

                    // Try fallback strategy

                    try
                    {
                        await ExecuteFallbackStrategy(data, services, logger);
                        fallbackCount++;
                        logger.LogInformation("  ✓ Fallback successful for {Scenario}", name);
                    }
                    catch
                    {
                        errorCount++;
                        logger.LogError("  ✗ Fallback failed for {Scenario}", name);
                    }
                }
                catch (NotSupportedException ex)
                {
                    logger.LogWarning("  ⚠ Compatibility issue in {Scenario}: {Message}", name, ex.Message);
                    fallbackCount++;
                    // Use CPU fallback for unsupported operations
                }
                catch (Exception ex)
                {
                    errorCount++;
                    logger.LogError(ex, "  ✗ Error in {Scenario}", name);
                }

                // Brief pause between scenarios
                await Task.Delay(100);
            }

            // Summary
            logger.LogInformation("Error Monitoring Summary:");
            logger.LogInformation("  Successful operations: {Success}", successCount);
            logger.LogInformation("  Fallback operations: {Fallbacks}", fallbackCount);
            logger.LogInformation("  Failed operations: {Errors}", errorCount);
            logger.LogInformation("  Success rate: {Rate:P}",

                (double)(successCount + fallbackCount) / scenarios.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in comprehensive error monitoring example");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private static float[] GenerateLargeDataset(int size)
    {
        var random = new Random(42);
        return Enumerable.Range(0, size)
            .Select(_ => (float)random.NextDouble())
            .ToArray();
    }

    private static ProblematicDataItem[] GenerateProblematicData(int count)
    {
        var random = new Random(42);
        return Enumerable.Range(0, count)
            .Select(i => new ProblematicDataItem(
                i,
                $"Item_{i}",
                [(float)random.NextDouble(), (float)random.NextDouble()],
                DateTime.Now.AddDays(random.Next(-365, 365)),
                (decimal)(random.NextDouble() * 1000)))
            .ToArray();
    }

    private static OptimizedDataItem[] ConvertToGpuCompatibleFormat(ProblematicDataItem[] data)
    {
        return data.Select(item => new OptimizedDataItem(
            item.Id,
            item.Values?.FirstOrDefault() ?? 0f,
            item.Timestamp.Ticks,
            (double)item.Money))
            .ToArray();
    }

    private static async Task ProcessLargeDatasetWithStreamingAsync(
        float[] data,

        IServiceProvider services,

        ILogger logger)
    {
        logger.LogInformation("Processing large dataset with streaming approach");

        // Convert to async enumerable for streaming using chunks
        var streamingData = data.AsEnumerable();


        var processedCount = 0;
        var batchSize = 10000;

        await foreach (var batch in streamingData.Chunk(batchSize).ToAsyncEnumerable())
        {
            try
            {
                var batchResults = await batch.AsComputeQueryable(services)
                    .Where(x => x > 0.5f)
                    .Select(x => x * 2.0f)
                    .ExecuteAsync();

                processedCount += batchResults.Count();

                if (processedCount % (batchSize * 10) == 0)
                {
                    logger.LogInformation("Streamed processing: {Count} items completed", processedCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Batch processing error: {Message}", ex.Message);
            }
        }

        logger.LogInformation("Streaming processing completed: {Total} items processed", processedCount);
    }

    private static async Task ProcessInBatchesAsync(
        float[] data,

        IServiceProvider services,

        ILogger logger)
    {
        logger.LogInformation("Processing large dataset in batches");

        const int batchSize = 100_000;
        var batches = data.Chunk(batchSize).ToArray();

        logger.LogInformation("Split into {BatchCount} batches of {BatchSize} items",

            batches.Length, batchSize);

        var processedCount = 0;
        var batchIndex = 0;

        foreach (var batch in batches)
        {
            try
            {
                var results = await batch.AsComputeQueryable(services)
                    .Where(x => x > 0.5f)
                    .Select(x => x * 2.0f)
                    .ExecuteAsync();

                processedCount += results.Count();
                batchIndex++;

                logger.LogInformation("Batch {Index}/{Total} completed: {Count} results",

                    batchIndex, batches.Length, results.Count());
            }
            catch (Exception ex)
            {
                logger.LogWarning("Batch {Index} failed: {Message}", batchIndex + 1, ex.Message);
            }
        }

        logger.LogInformation("Batch processing completed: {Total} total results", processedCount);
    }

    private static Task HandleUnsupportedOperationsAsync(IServiceProvider services, ILogger logger)
    {
        try
        {
            var stringData = new[] { "hello", "world", "gpu", "computing" };

            // This will likely fail on GPU

            var queryable = stringData.AsComputeQueryable(services);


            var isCompatible = queryable.IsGpuCompatible(services);
            if (!isCompatible)
            {
                logger.LogInformation("String operations detected as GPU-incompatible, using CPU");

                // Process with CPU instead

                var results = stringData
                    .Where(s => s.Length > 3)
                    .Select(s => s.ToUpper())
                    .ToArray();


                logger.LogInformation("CPU processing successful: {Count} results", results.Length);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Handled unsupported operation gracefully: {Message}", ex.Message);
        }


        return Task.CompletedTask;
    }

    private static async Task AnalyzeExpressionErrorsAsync(IPipelineErrorHandler errorHandler, ILogger logger)
    {
        try
        {
            // Create a problematic expression
            var expression = System.Linq.Expressions.Expression.Call(
                typeof(string),
                "Concat",

                Type.EmptyTypes,

                System.Linq.Expressions.Expression.Constant("test"));

            var analysisError = new NotSupportedException("String concatenation not supported on GPU");
            var analysis = await errorHandler.AnalyzeExpressionErrorAsync(expression, analysisError);

            logger.LogInformation("Expression error analysis completed:");
            foreach (var area in analysis.ProblemAreas)
            {
                logger.LogInformation("  Problem area: {Area}", area);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Expression analysis not available: {Message}", ex.Message);
        }
    }

    private static async Task HandleTimeoutScenariosAsync(IQueryable<float> queryable, ILogger logger)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // This might timeout for very large operations

            var results = await queryable
                .Select(x => (float)Math.Pow(x, 10)) // Expensive operation
                .ExecuteAsync(cts.Token);

            logger.LogInformation("Operation completed within timeout: {Count} results", results.Count());
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operation timed out, consider reducing data size or complexity");
        }
    }

    private static async Task HandleCompilationErrorsAsync(
        IQueryable<float> queryable,

        IServiceProvider services,
        ILogger logger)
    {
        try
        {
            // Pre-compile to catch compilation errors early
            await queryable
                .Select(x => x * 2.0f) // Simple operation
                .PrecompileAsync(services);


            logger.LogInformation("Pre-compilation successful");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("compilation"))
        {
            logger.LogWarning("Kernel compilation failed: {Message}", ex.Message);
        }
    }

    private static async Task HandleExecutionExceptionsAsync(IQueryable<float> queryable, ILogger logger)
    {
        try
        {
            // Operation that might cause division by zero or other runtime errors
            var results = await queryable
                .Select(x => 1.0f / (x - 500000)) // Potential division by zero
                .Where(x => !float.IsInfinity(x) && !float.IsNaN(x))
                .ExecuteAsync();

            logger.LogInformation("Handled potential runtime exceptions: {Count} valid results",

                results.Count());
        }
        catch (Exception ex)
        {
            logger.LogWarning("Runtime execution error handled: {Message}", ex.Message);
        }
    }

    // Mock operations for error monitoring scenarios
    private static async Task ExecuteNormalOperationAsync(float[] data, IServiceProvider services)
    {
        await data.AsComputeQueryable(services)
            .Where(x => x > 0.5f)
            .Select(x => x * 2.0f)
            .ExecuteAsync();
    }

    private static async Task ExecuteMemoryIntensiveOperationAsync(float[] data, IServiceProvider services)
    {
        // Create multiple large intermediate results
        var query1 = data.AsComputeQueryable(services).Select(x => x * 2.0f);
        var query2 = data.AsComputeQueryable(services).Select(x => x * 3.0f);


        await Task.WhenAll(
            query1.ExecuteAsync(),
            query2.ExecuteAsync()
        );
    }

    private static async Task ExecuteComplexMathOperationAsync(float[] data, IServiceProvider services)
    {
        await data.AsComputeQueryable(services)
            .Select(x => (float)(Math.Sin(x) * Math.Cos(x) + Math.Sqrt(Math.Abs(x))))
            .Where(x => !float.IsNaN(x) && !float.IsInfinity(x))
            .ExecuteAsync();
    }

    private static async Task ExecuteLargeDatasetOperationAsync(float[] data, IServiceProvider services)
    {
        // Expand the dataset temporarily
        var expandedData = data.SelectMany(x => new[] { x, x * 2, x * 3 }).ToArray();


        await expandedData.AsComputeQueryable(services)
            .Where(x => x > 100)
            .ExecuteAsync();
    }

    private static async Task ExecuteProblematicOperationAsync(float[] data, IServiceProvider services)
    {
        // Operation that might not be supported on all backends
        await data.AsComputeQueryable(services)
            .Select(x => float.Parse(x.ToString())) // String conversion - problematic for GPU
            .ExecuteAsync();
    }

    private static async Task ExecuteFallbackStrategy(float[] data, IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Executing fallback strategy with reduced complexity");

        // Simplified operation that should work on most backends

        await data.Take(data.Length / 2) // Process half the data
            .AsComputeQueryable(services)
            .Where(x => x > 0)
            .ExecuteAsync();
    }

    #endregion

    #region Configuration for Error Handling Examples

    /// <summary>
    /// Configures services with comprehensive error handling support.
    /// </summary>
    /// <returns>Configured service provider with error handling capabilities.</returns>
    public static IServiceProvider CreateErrorHandlingServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddDotComputeLinq(options =>
        {
            options.EnableAutoFallback = true;
            options.FallbackTimeout = TimeSpan.FromSeconds(30);
            options.EnableDetailedProfiling = true;
            options.MaxMemoryUsage = 512 * 1024 * 1024; // 512MB limit
        });

        // Add error handling services
        services.AddScoped<IPipelineErrorHandler, PipelineErrorHandler>();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs all error handling examples to demonstrate comprehensive error management.
    /// </summary>
    /// <param name="services">Configured service provider.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunAllErrorHandlingExamplesAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var logger = services.GetRequiredService<ILogger>();
        logger.LogInformation("Starting comprehensive error handling examples");

        var exampleMethods = new (string Name, Func<IServiceProvider, Task> Method)[]
        {
            ("Memory Exhaustion Handling", HandleMemoryExhaustionAsync),
            ("Device Error Handling", HandleDeviceErrorsAsync),
            ("Compatibility Issues Handling", HandleCompatibilityIssuesAsync),
            ("Pipeline Validation", PipelineValidationAsync),
            ("Runtime Error Handling", HandleRuntimeErrorsAsync),
            ("Comprehensive Error Monitoring", ComprehensiveErrorMonitoringAsync)
        };

        var successCount = 0;
        var errorCount = 0;

        foreach (var (name, method) in exampleMethods)
        {
            try
            {
                logger.LogInformation("Running example: {ExampleName}", name);
                await method(services);
                successCount++;
                logger.LogInformation("✓ {ExampleName} completed successfully", name);
            }
            catch (Exception ex)
            {
                errorCount++;
                logger.LogError(ex, "✗ {ExampleName} failed", name);
            }

            logger.LogInformation(""); // Empty line for readability
        }

        logger.LogInformation("Error handling examples summary:");
        logger.LogInformation("  Successful examples: {Success}/{Total}", successCount, exampleMethods.Length);
        logger.LogInformation("  Failed examples: {Errors}/{Total}", errorCount, exampleMethods.Length);

        if (errorCount == 0)
        {
            logger.LogInformation("🎉 All error handling examples completed successfully!");
        }
    }

    #endregion
}