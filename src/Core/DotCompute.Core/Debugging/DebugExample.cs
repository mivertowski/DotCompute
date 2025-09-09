// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Interfaces;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Comprehensive example demonstrating the enhanced debugging capabilities.
/// Shows integration between runtime services and debugging infrastructure.
/// </summary>
public static class DebugExample
{
    /// <summary>
    /// Demonstrates the complete debugging workflow with cross-backend validation.
    /// </summary>
    public static async Task RunComprehensiveDebuggingExampleAsync()
    {
        Console.WriteLine("=== DotCompute Enhanced Debugging System Demo ===\n");

        // Create host with comprehensive debugging services
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Add core DotCompute services
                services.AddDotComputeRuntime();

                // Add comprehensive debugging (development mode)

                services.AddDevelopmentDebugging();

                // Add logging
                services.AddLogging(builder => builder
                    .SetMinimumLevel(LogLevel.Debug)
                    .AddConsole());
            })
            .Build();

        await host.StartAsync();

        var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();
        var debugService = host.Services.GetRequiredService<IKernelDebugService>();
        var logger = host.Services.GetRequiredService<ILogger<DebugExample>>();

        try
        {
            // Example 1: Basic kernel execution with debug hooks
            logger.LogInformation("\n--- Example 1: Debug-Enhanced Kernel Execution ---");
            await DemonstrateDebugEnhancedExecution(orchestrator, logger);

            // Example 2: Cross-backend validation
            logger.LogInformation("\n--- Example 2: Cross-Backend Validation ---");
            await DemonstrateCrossBackendValidation(debugService, logger);

            // Example 3: Performance analysis
            logger.LogInformation("\n--- Example 3: Performance Analysis ---");
            await DemonstratePerformanceAnalysis(debugService, logger);

            // Example 4: Determinism testing
            logger.LogInformation("\n--- Example 4: Determinism Testing ---");
            await DemonstrateDeterminismTesting(debugService, logger);

            // Example 5: Memory pattern analysis
            logger.LogInformation("\n--- Example 5: Memory Pattern Analysis ---");
            await DemonstrateMemoryAnalysis(debugService, logger);

            // Example 6: Backend information
            logger.LogInformation("\n--- Example 6: Backend Information ---");
            await DemonstrateBackendInformation(debugService, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during debugging demonstration");
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static async Task DemonstrateDebugEnhancedExecution(
        IComputeOrchestrator orchestrator,
        ILogger logger)
    {
        logger.LogInformation("Executing kernel with debug hooks enabled...");

        // Create sample data
        var inputData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
        var outputData = new float[inputData.Length];

        try
        {
            // This execution will automatically include:
            // - Pre-execution validation
            // - Performance monitoring  
            // - Error analysis (if needed)
            // - Optional cross-backend validation
            await orchestrator.ExecuteAsync("VectorAdd", inputData, outputData, 2.0f);

            logger.LogInformation("Kernel executed successfully with debug monitoring");
            logger.LogInformation("Input:  [{Input}]", string.Join(", ", inputData));
            logger.LogInformation("Output: [{Output}]", string.Join(", ", outputData));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Kernel execution failed (expected for demo): {Message}", ex.Message);
            logger.LogInformation("Debug service automatically analyzed the failure");
        }
    }

    private static async Task DemonstrateCrossBackendValidation(
        IKernelDebugService debugService,
        ILogger logger)
    {
        logger.LogInformation("Performing cross-backend validation...");

        var inputs = new object[]
        {

            new float[] { 1.0f, 2.0f, 3.0f, 4.0f },
            new float[4],
            1.5f
        };

        try
        {
            var validationResult = await debugService.ValidateKernelAsync("VectorMultiply", inputs);

            logger.LogInformation("Validation completed in {Time}ms",

                validationResult.TotalValidationTime.TotalMilliseconds);
            logger.LogInformation("Backends tested: {Backends}",

                string.Join(", ", validationResult.BackendsTested));
            logger.LogInformation("Validation result: {IsValid}", validationResult.IsValid);
            logger.LogInformation("Recommended backend: {Backend}", validationResult.RecommendedBackend);

            if (validationResult.Issues.Count > 0)
            {
                logger.LogInformation("Issues found:");
                foreach (var issue in validationResult.Issues)
                {
                    logger.LogInformation("  - {Severity}: {Message}", issue.Severity, issue.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Cross-backend validation demonstration: {Message}", ex.Message);
        }
    }

    private static async Task DemonstratePerformanceAnalysis(
        IKernelDebugService debugService,
        ILogger logger)
    {
        logger.LogInformation("Analyzing performance across backends...");

        var inputs = new object[] { new float[1000], new float[1000] };

        try
        {
            // Execute on specific backends for comparison
            var cpuResult = await debugService.ExecuteOnBackendAsync("MatrixMultiply", "CPU", inputs);
            var cudaResult = await debugService.ExecuteOnBackendAsync("MatrixMultiply", "CUDA", inputs);

            var results = new[] { cpuResult, cudaResult }.Where(r => r.Success);


            if (results.Any())
            {
                var comparison = await debugService.CompareResultsAsync(results);


                logger.LogInformation("Performance comparison:");
                foreach (var perf in comparison.PerformanceComparison)
                {
                    logger.LogInformation("  {Backend}: {Time}ms, {Memory}KB, {Throughput} ops/sec",
                        perf.Key,
                        perf.Value.ExecutionTime.TotalMilliseconds,
                        perf.Value.MemoryUsage / 1024,
                        perf.Value.ThroughputOpsPerSecond);
                }

                logger.LogInformation("Results match: {Match} (Strategy: {Strategy})",
                    comparison.ResultsMatch, comparison.Strategy);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Performance analysis demonstration: {Message}", ex.Message);
        }
    }

    private static async Task DemonstrateDeterminismTesting(
        IKernelDebugService debugService,
        ILogger logger)
    {
        logger.LogInformation("Testing kernel determinism...");

        var inputs = new object[] { new float[] { 1.0f, 2.0f, 3.0f }, new float[3] };

        try
        {
            var determinismReport = await debugService.ValidateDeterminismAsync(
                "SimpleKernel", inputs, 5);

            logger.LogInformation("Determinism test results:");
            logger.LogInformation("  Is deterministic: {IsDeterministic}", determinismReport.IsDeterministic);
            logger.LogInformation("  Executions tested: {Count}", determinismReport.ExecutionCount);
            logger.LogInformation("  Max variation: {Variation:F6}", determinismReport.MaxVariation);

            if (!string.IsNullOrEmpty(determinismReport.NonDeterminismSource))
            {
                logger.LogInformation("  Non-determinism source: {Source}",

                    determinismReport.NonDeterminismSource);
            }

            if (determinismReport.Recommendations.Count > 0)
            {
                logger.LogInformation("  Recommendations:");
                foreach (var recommendation in determinismReport.Recommendations)
                {
                    logger.LogInformation("    - {Recommendation}", recommendation);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Determinism testing demonstration: {Message}", ex.Message);
        }
    }

    private static async Task DemonstrateMemoryAnalysis(
        IKernelDebugService debugService,
        ILogger logger)
    {
        logger.LogInformation("Analyzing memory access patterns...");

        var inputs = new object[] { new float[1024], new float[1024] };

        try
        {
            var memoryReport = await debugService.AnalyzeMemoryPatternsAsync("MemoryIntensiveKernel", inputs);

            logger.LogInformation("Memory analysis results:");
            logger.LogInformation("  Backend: {Backend}", memoryReport.BackendType);
            logger.LogInformation("  Total memory accessed: {Memory}MB",

                memoryReport.TotalMemoryAccessed / (1024 * 1024));
            logger.LogInformation("  Memory efficiency: {Efficiency:P1}", memoryReport.MemoryEfficiency);

            if (memoryReport.AccessPatterns.Count > 0)
            {
                logger.LogInformation("  Access patterns:");
                foreach (var pattern in memoryReport.AccessPatterns)
                {
                    logger.LogInformation("    - {Type}: {Count} accesses, {Efficiency:P1} coalescing",
                        pattern.PatternType, pattern.AccessCount, pattern.CoalescingEfficiency);
                }
            }

            if (memoryReport.Optimizations.Count > 0)
            {
                logger.LogInformation("  Optimization suggestions:");
                foreach (var opt in memoryReport.Optimizations)
                {
                    logger.LogInformation("    - {Type}: {Description} (potential {Speedup:F1}x speedup)",
                        opt.Type, opt.Description, opt.PotentialSpeedup);
                }
            }

            if (memoryReport.Warnings.Count > 0)
            {
                logger.LogInformation("  Warnings:");
                foreach (var warning in memoryReport.Warnings)
                {
                    logger.LogInformation("    - {Warning}", warning);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Memory analysis demonstration: {Message}", ex.Message);
        }
    }

    private static async Task DemonstrateBackendInformation(
        IKernelDebugService debugService,
        ILogger logger)
    {
        logger.LogInformation("Gathering backend information...");

        try
        {
            var backends = await debugService.GetAvailableBackendsAsync();

            logger.LogInformation("Available backends:");
            foreach (var backend in backends)
            {
                logger.LogInformation("  {Name} v{Version}:",
                    backend.Name, backend.Version);
                logger.LogInformation("    Available: {Available}", backend.IsAvailable);
                logger.LogInformation("    Priority: {Priority}", backend.Priority);


                if (!backend.IsAvailable && !string.IsNullOrEmpty(backend.UnavailabilityReason))
                {
                    logger.LogInformation("    Reason: {Reason}", backend.UnavailabilityReason);
                }

                if (backend.Capabilities.Length > 0)
                {
                    logger.LogInformation("    Capabilities: {Capabilities}",

                        string.Join(", ", backend.Capabilities));
                }

                if (backend.Properties.Count > 0)
                {
                    logger.LogInformation("    Properties:");
                    foreach (var prop in backend.Properties)
                    {
                        logger.LogInformation("      {Key}: {Value}", prop.Key, prop.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Backend information demonstration: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Example of configuring different debugging levels for different scenarios.
    /// </summary>
    public static IServiceCollection ConfigureCustomDebugging(this IServiceCollection services)
    {
        // Example: Custom debugging configuration for ML workloads
        return services.AddComprehensiveDebugging(
            debugService =>
            {
                debugService.VerbosityLevel = LogLevel.Information;
                debugService.EnableProfiling = true;
                debugService.EnableMemoryAnalysis = true;
                debugService.SaveExecutionLogs = true;
                debugService.LogOutputPath = "./ml-debug-logs";
                debugService.ExecutionTimeout = TimeSpan.FromMinutes(10); // ML kernels can be slow
            },
            execution =>
            {
                execution.EnableDebugHooks = true;
                execution.ValidateBeforeExecution = true;
                execution.EnableCrossBackendValidation = true;
                execution.CrossValidationProbability = 0.05; // 5% for production ML
                execution.ValidationTolerance = 1e-4f; // ML tolerance
                execution.EnablePerformanceMonitoring = true;
                execution.TestDeterminism = false; // ML often uses randomness
                execution.AnalyzeErrorsOnFailure = true;
                execution.StorePerformanceHistory = true;
            });
    }
}

/// <summary>
/// Example kernel definitions for debugging demonstrations.
/// These would typically be generated by the source generator.
/// </summary>
public static class ExampleKernels
{
    /// <summary>
    /// Simple vector addition kernel for demonstration.
    /// </summary>
    public static void VectorAdd(ReadOnlySpan<float> input, Span<float> output, float scalar)
    {
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] + scalar;
        }
    }

    /// <summary>
    /// Vector multiplication kernel for cross-backend testing.
    /// </summary>
    public static void VectorMultiply(ReadOnlySpan<float> input, Span<float> output, float scalar)
    {
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * scalar;
        }
    }

    /// <summary>
    /// Matrix multiplication for performance analysis.
    /// </summary>
    public static void MatrixMultiply(ReadOnlySpan<float> matrixA, ReadOnlySpan<float> matrixB)
    {
        // Simplified matrix multiply for demonstration
        // In practice, this would be much more complex
    }

    /// <summary>
    /// Simple kernel for determinism testing.
    /// </summary>
    public static void SimpleKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = input[i] * 2.0f;
        }
    }

    /// <summary>
    /// Memory-intensive kernel for memory pattern analysis.
    /// </summary>
    public static void MemoryIntensiveKernel(ReadOnlySpan<float> input, Span<float> output)
    {
        // Complex memory access patterns for analysis
        for (var i = 0; i < input.Length; i++)
        {
            var idx = (i * 7) % input.Length; // Non-sequential access
            output[i] = input[idx] * 1.5f;
        }
    }
}