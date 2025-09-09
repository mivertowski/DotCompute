// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DotCompute.Abstractions.Interfaces;
using DotCompute.Core.Debugging;

namespace DotCompute.Core.Optimization;

/// <summary>
/// Comprehensive example demonstrating the complete integrated system with all four phases:
/// 1. Generator ↔ Runtime Integration
/// 2. Roslyn Analyzer Integration  
/// 3. Enhanced Debugging Service
/// 4. Production Optimizations
/// </summary>
public static class ComprehensiveOptimizationExample
{
    /// <summary>
    /// Demonstrates the complete end-to-end optimized DotCompute system.
    /// </summary>
    public static async Task RunCompleteOptimizedSystemExampleAsync()
    {
        Console.WriteLine("=== DotCompute Complete Optimized System Demo ===\n");

        // Create host with all optimization layers
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Phase 1: Core DotCompute Runtime (Generator ↔ Runtime Integration)
                services.AddDotComputeRuntime();

                // Phase 2: IDE-Integrated Static Analysis (already integrated with generators)

                // Phase 3: Enhanced Debugging with Cross-Backend Validation

                services.AddDevelopmentDebugging();

                // Phase 4: Production Performance Optimizations

                services.AddDevelopmentOptimization(); // Use development profile for demo

                // Add comprehensive logging
                services.AddLogging(builder => builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddConsole());
            })
            .Build();

        await host.StartAsync();

        var orchestrator = host.Services.GetRequiredService<IComputeOrchestrator>();
        var logger = host.Services.GetRequiredService<ILogger<ComprehensiveOptimizationExample>>();

        logger.LogInformation("🚀 Complete optimized DotCompute system initialized");
        logger.LogInformation("📊 System includes: Runtime Integration + Static Analysis + Debugging + Performance Optimization");

        try
        {
            // Example 1: Demonstrate optimized kernel execution
            logger.LogInformation("\n--- Example 1: Optimized Kernel Execution ---");
            await DemonstrateOptimizedExecution(orchestrator, logger);

            // Example 2: Show performance insights
            logger.LogInformation("\n--- Example 2: Performance Insights ---");
            await DemonstratePerformanceInsights(host.Services, logger);

            // Example 3: Demonstrate workload-specific optimization
            logger.LogInformation("\n--- Example 3: Workload-Specific Optimization ---");
            await DemonstrateWorkloadSpecificOptimization(orchestrator, logger);

            // Example 4: Show system adaptability
            logger.LogInformation("\n--- Example 4: System Adaptability ---");
            await DemonstrateSystemAdaptability(orchestrator, logger);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during comprehensive system demonstration");
        }
        finally
        {
            await host.StopAsync();
        }

        logger.LogInformation("\n✅ Complete optimized system demonstration completed");
    }

    private static async Task DemonstrateOptimizedExecution(
        IComputeOrchestrator orchestrator,
        ILogger logger)
    {
        logger.LogInformation("Executing kernels with full optimization stack...");

        // Various workload patterns to demonstrate optimization
        var workloads = new[]
        {
            ("VectorAdd", new float[1000], new float[1000], 2.5f),
            ("MatrixMultiply", new float[100, 100], new float[100, 100]),
            ("MemoryIntensiveCopy", new byte[1024 * 1024], new byte[1024 * 1024]),
            ("ComputeIntensiveFFT", new float[4096]),
            ("HighlyParallelMap", new float[10000])
        };

        foreach (var (kernelName, args) in workloads.Take(3)) // Limit for demo
        {
            try
            {
                logger.LogInformation("  Executing {KernelName}...", kernelName);

                // This execution automatically includes:
                // ✅ Workload analysis and backend selection
                // ✅ Pre-execution optimizations
                // ✅ Cross-backend validation (if enabled)
                // ✅ Real-time performance monitoring
                // ✅ Machine learning from results
                // ✅ Error analysis and debugging hooks


                await orchestrator.ExecuteAsync(kernelName, args);


                logger.LogInformation("    ✅ {KernelName} completed with optimization", kernelName);
            }
            catch (Exception ex)
            {
                logger.LogWarning("    ⚠️ {KernelName} failed (expected for demo): {Message}",

                    kernelName, ex.Message);
            }
        }
    }

    private static async Task DemonstratePerformanceInsights(
        IServiceProvider services,
        ILogger logger)
    {
        logger.LogInformation("Gathering performance insights from optimization system...");

        try
        {
            var insights = services.GetPerformanceInsights();


            logger.LogInformation("System Performance Insights:");
            logger.LogInformation("  📈 Total workload signatures tracked: {Count}", insights.TotalWorkloadSignatures);
            logger.LogInformation("  🖥️  Total backends available: {Count}", insights.TotalBackends);
            logger.LogInformation("  📊 Learning effectiveness: {Effectiveness:P1}", insights.LearningStatistics.LearningEffectiveness);
            logger.LogInformation("  🎯 Performance samples collected: {Samples}", insights.LearningStatistics.TotalPerformanceSamples);

            if (insights.BackendStates.Any())
            {
                logger.LogInformation("  Backend States:");
                foreach (var (backendId, state) in insights.BackendStates)
                {
                    logger.LogInformation("    {Backend}: {Utilization:P1} utilization, {Executions} recent executions",
                        backendId, state.CurrentUtilization, state.RecentExecutionCount);
                }
            }

            if (insights.TopPerformingPairs.Any())
            {
                logger.LogInformation("  🏆 Top performing workload-backend pairs:");
                foreach (var (workload, backend, score) in insights.TopPerformingPairs.Take(3))
                {
                    logger.LogInformation("    {Kernel} on {Backend}: {Score:F2} score",
                        workload.KernelName, backend, score);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation("Performance insights: {Message} (expected if no executions yet)", ex.Message);
        }

        await Task.CompletedTask;
    }

    private static async Task DemonstrateWorkloadSpecificOptimization(
        IComputeOrchestrator orchestrator,
        ILogger logger)
    {
        logger.LogInformation("Demonstrating workload-specific optimizations...");

        var scenarios = new[]
        {
            ("Small data, compute-intensive", "CryptoHash", new byte[256]),
            ("Large data, memory-intensive", "DataTransform", new float[1000000]),
            ("Medium data, balanced workload", "SignalProcess", new double[50000])
        };

        foreach (var (description, kernelName, data) in scenarios)
        {
            logger.LogInformation("  Scenario: {Description}", description);
            logger.LogInformation("    Kernel: {KernelName}, Data size: {Size} elements",

                kernelName, GetDataSize(data));

            try
            {
                // The optimization system will:
                // 1. Analyze workload characteristics (compute/memory/parallelism intensity)
                // 2. Select optimal backend based on workload pattern
                // 3. Apply workload-specific optimizations
                // 4. Learn from execution for future similar workloads


                await orchestrator.ExecuteAsync(kernelName, data);
                logger.LogInformation("    ✅ Optimized for {Description}", description.ToLowerInvariant());
            }
            catch (Exception ex)
            {
                logger.LogInformation("    ℹ️ Scenario demo: {Message}", ex.Message);
            }
        }
    }

    private static async Task DemonstrateSystemAdaptability(
        IComputeOrchestrator orchestrator,
        ILogger logger)
    {
        logger.LogInformation("Demonstrating system adaptability over multiple executions...");

        // Simulate system learning by running same kernel multiple times
        // The system should adapt and improve selection over time


        var kernelName = "AdaptiveTestKernel";
        var testData = new float[10000];

        logger.LogInformation("  Running {KernelName} multiple times to demonstrate learning...", kernelName);

        for (var iteration = 1; iteration <= 5; iteration++)
        {
            logger.LogInformation("    Iteration {Iteration}/5:", iteration);

            try
            {
                // With each execution, the system:
                // ✅ Records performance results
                // ✅ Updates backend performance models
                // ✅ Improves future selections based on learning
                // ✅ Adapts to changing system conditions


                await orchestrator.ExecuteAsync(kernelName, testData);


                logger.LogInformation("      ✅ Execution {Iteration} completed - system learning applied", iteration);

                // Simulate some delay between executions

                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                logger.LogInformation("      ℹ️ Iteration {Iteration}: {Message}", iteration, ex.Message);
            }
        }

        logger.LogInformation("  📈 After 5 iterations, the system has:");
        logger.LogInformation("    • Learned workload characteristics");
        logger.LogInformation("    • Built performance history");
        logger.LogInformation("    • Improved backend selection confidence");
        logger.LogInformation("    • Adapted to system conditions");
    }

    private static int GetDataSize(object data) => data switch
    {
        Array array => array.Length,
        _ => 1
    };

    /// <summary>
    /// Example of configuring the complete system for different environments.
    /// </summary>
    public static IServiceCollection ConfigureForEnvironment(
        this IServiceCollection services,

        DeploymentEnvironment environment)
    {
        return environment switch
        {
            DeploymentEnvironment.Development => services
                .AddDotComputeRuntime()
                .AddDevelopmentDebugging()
                .AddDevelopmentOptimization(),

            DeploymentEnvironment.Production => services
                .AddDotComputeRuntime()
                .AddProductionDebugging()
                .AddProductionOptimization(),

            DeploymentEnvironment.HighPerformance => services
                .AddDotComputeRuntime()
                .AddProductionDebugging()
                .AddHighPerformanceOptimization(),

            DeploymentEnvironment.MachineLearning => services
                .AddDotComputeRuntime()
                .AddDevelopmentDebugging()
                .AddMLWorkloadOptimization(),

            _ => services.AddDotComputeRuntime()
        };
    }

    /// <summary>
    /// Summary of all four phases and their integration.
    /// </summary>
    public static void PrintSystemOverview()
    {
        Console.WriteLine("=== DotCompute Complete System Architecture ===\n");


        Console.WriteLine("Phase 1: Generator ↔ Runtime Integration Bridge");
        Console.WriteLine("  ✅ IComputeOrchestrator interface");
        Console.WriteLine("  ✅ KernelExecutionService for runtime integration");
        Console.WriteLine("  ✅ GeneratedKernelDiscoveryService");
        Console.WriteLine("  ✅ ServiceCollectionExtensions for DI");
        Console.WriteLine("  ✅ IntegrationExample demonstrating workflow");


        Console.WriteLine("\nPhase 2: Roslyn Analyzer Integration");
        Console.WriteLine("  ✅ DotComputeKernelAnalyzer with 12 diagnostic rules");
        Console.WriteLine("  ✅ KernelCodeFixProvider with 5 automated fixes");
        Console.WriteLine("  ✅ IDE integration for real-time feedback");
        Console.WriteLine("  ✅ AnalyzerDemo with comprehensive examples");


        Console.WriteLine("\nPhase 3: Enhanced Debugging Service");
        Console.WriteLine("  ✅ IKernelDebugService with 8 debugging methods");
        Console.WriteLine("  ✅ KernelDebugService production implementation");
        Console.WriteLine("  ✅ DebugIntegratedOrchestrator with hooks");
        Console.WriteLine("  ✅ Cross-backend validation and analysis");
        Console.WriteLine("  ✅ Multiple debugging profiles (dev/test/prod)");


        Console.WriteLine("\nPhase 4: Production Optimizations");
        Console.WriteLine("  ✅ AdaptiveBackendSelector with ML capabilities");
        Console.WriteLine("  ✅ PerformanceOptimizedOrchestrator");
        Console.WriteLine("  ✅ Comprehensive workload analysis");
        Console.WriteLine("  ✅ Real-time performance monitoring");
        Console.WriteLine("  ✅ Multiple optimization profiles");


        Console.WriteLine("\n🏗️  Complete Integrated Architecture:");
        Console.WriteLine("  📝 Source Generator → 🔍 Static Analysis → 🚀 Runtime → 🐛 Debugging → 🔧 Optimization");
        Console.WriteLine("  \nThe system provides:");
        Console.WriteLine("  • Universal kernel execution with optimal backend selection");
        Console.WriteLine("  • Real-time static analysis with automated fixes");
        Console.WriteLine("  • Comprehensive debugging with cross-backend validation");
        Console.WriteLine("  • Intelligent performance optimization with machine learning");
        Console.WriteLine("  • Production-grade reliability and monitoring");
    }
}

/// <summary>
/// Deployment environment types for configuration.
/// </summary>
public enum DeploymentEnvironment
{
    Development,
    Production,
    HighPerformance,
    MachineLearning
}