// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces.Kernels;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;
using DotCompute.Backends.Metal.Execution.Interfaces;
using DotCompute.Backends.Metal.Execution.Graph.Configuration;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph.Examples;

/// <summary>
/// Demonstrates how to use the Metal compute graph system for complex computation pipelines.
/// This example shows graph construction, optimization, and execution with performance monitoring.
/// </summary>
public static class MetalGraphExample
{
    /// <summary>
    /// Creates and executes a complete example showing matrix multiplication with preprocessing and postprocessing.
    /// This example demonstrates the full Metal graph execution pipeline including optimization.
    /// </summary>
    /// <param name="logger">Logger for operation tracking.</param>
    /// <param name="commandQueue">Metal command queue handle.</param>
    /// <param name="inputBufferA">Input matrix A buffer.</param>
    /// <param name="inputBufferB">Input matrix B buffer.</param>
    /// <param name="outputBuffer">Output result buffer.</param>
    /// <param name="matrixSize">Size of the square matrices.</param>
    /// <returns>The execution result with performance metrics.</returns>
    public static async Task<MetalGraphExecutionResult?> RunCompleteMatrixMultiplicationExampleAsync(
        ILogger logger,
        IntPtr commandQueue,
        IntPtr inputBufferA,
        IntPtr inputBufferB,
        IntPtr outputBuffer,
        uint matrixSize)
    {
        // Create Metal graph manager with Apple Silicon optimizations
        var config = MetalGraphConfiguration.CreateAppleSiliconOptimized();
        var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalGraphManager>.Instance;
        using var graphManager = new MetalGraphManager(typedLogger, config);

        // Create a complex computation graph for matrix multiplication pipeline
        var graph = graphManager.CreateGraph("MatrixMultiplicationPipeline");

        try
        {
            // Step 1: Create mock kernels (in real implementation, these would be actual compiled Metal kernels)
            var normalizeKernel = CreateMockKernel("normalize_matrix");
            var matMulKernel = CreateMockKernel("matrix_multiply");
            var postProcessKernel = CreateMockKernel("post_process_result");

            // Step 2: Add preprocessing nodes - normalize input matrices
            var normalizeA = graph.AddKernelNode(
                normalizeKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16), // Threadgroups
                MTLSize.Make(16, 16), // Threads per threadgroup
                new object[] { inputBufferA, matrixSize }
            );

            var normalizeB = graph.AddKernelNode(
                normalizeKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                new object[] { inputBufferB, matrixSize }
            );

            // Step 3: Add main computation node - matrix multiplication
            // This depends on both normalization operations completing
            var matrixMultiply = graph.AddKernelNode(
                matMulKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                new object[] { inputBufferA, inputBufferB, outputBuffer, matrixSize },
                dependencies: new[] { normalizeA, normalizeB }
            );

            // Step 4: Add memory synchronization barrier
            var syncBarrier = graph.AddBarrierNode(new[] { matrixMultiply });

            // Step 5: Add postprocessing node - apply final transformations
            var postProcess = graph.AddKernelNode(
                postProcessKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                new object[] { outputBuffer, matrixSize },
                dependencies: new[] { syncBarrier }
            );

            // Step 6: Add memory operations for result copying
            var tempBuffer = CreateMockBuffer(matrixSize * matrixSize * sizeof(float));
            var copyResult = graph.AddMemoryCopyNode(
                outputBuffer,
                tempBuffer,
                matrixSize * matrixSize * sizeof(float),
                dependencies: new[] { postProcess }
            );

            logger.LogInformation("Created complex matrix multiplication graph with {NodeCount} nodes", 
                graph.NodeCount);

            // Step 7: Build the graph (validates dependencies and creates execution plan)
            graph.Build();
            logger.LogInformation("Graph built successfully with critical path length: {CriticalPath}", 
                graph.Statistics.CriticalPathLength);

            // Step 8: Analyze optimization opportunities
            var analysis = await graphManager.AnalyzeGraphAsync("MatrixMultiplicationPipeline");
            if (analysis != null)
            {
                logger.LogInformation("Graph analysis - Fusion opportunities: {Fusion}, " +
                                    "Memory optimizations: {Memory}, Parallelism: {Parallel}",
                    analysis.FusionOpportunities, analysis.MemoryCoalescingOpportunities, 
                    analysis.ParallelismOpportunities);
            }

            // Step 9: Apply optimizations
            var optimizationResult = await graphManager.OptimizeGraphAsync("MatrixMultiplicationPipeline");
            if (optimizationResult?.Success == true)
            {
                logger.LogInformation("Graph optimization completed - Performance improvement: {Improvement:F2}x, " +
                                    "Memory reduction: {MemoryReduction:P1}",
                    optimizationResult.EstimatedPerformanceImprovement,
                    optimizationResult.MemoryReduction);
            }

            // Step 10: Execute the optimized graph
            var executionResult = await graphManager.ExecuteGraphAsync(
                "MatrixMultiplicationPipeline", 
                commandQueue);

            if (executionResult?.Success == true)
            {
                logger.LogInformation("Graph execution completed successfully in {ExecutionTime:F2}ms " +
                                    "(GPU: {GpuTime:F2}ms) - {NodesExecuted} nodes, {CommandBuffers} command buffers",
                    executionResult.ExecutionDuration.TotalMilliseconds,
                    executionResult.GpuExecutionTimeMs,
                    executionResult.NodesExecuted,
                    executionResult.CommandBuffersUsed);

                // Log performance metrics
                foreach (var metric in executionResult.PerformanceMetrics)
                {
                    logger.LogDebug("Performance metric - {MetricName}: {MetricValue}", 
                        metric.Key, metric.Value);
                }
            }

            // Step 11: Generate comprehensive report
            var report = graphManager.GenerateComprehensiveReport();
            logger.LogInformation("Graph execution report:\n{Report}", report);

            return executionResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute matrix multiplication graph");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates advanced graph features including dynamic graph modification and real-time optimization.
    /// </summary>
    /// <param name="logger">Logger for operation tracking.</param>
    /// <param name="commandQueue">Metal command queue handle.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static async Task RunAdvancedGraphFeaturesExampleAsync(ILogger logger, IntPtr commandQueue)
    {
        var config = new MetalGraphConfiguration
        {
            EnableKernelFusion = true,
            EnableMemoryOptimization = true,
            EnableAppleSiliconOptimizations = true,
            EnablePerformanceMetrics = true,
            MaxKernelFusionDepth = 5,
            MemoryStrategy = MetalMemoryStrategy.UnifiedMemory
        };

        var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalGraphManager>.Instance;
        using var graphManager = new MetalGraphManager(typedLogger, config);

        // Create multiple graphs for comparison
        var graphs = new[]
        {
            ("SimpleCompute", CreateSimpleComputeGraph(graphManager, logger)),
            ("ComplexPipeline", CreateComplexPipelineGraph(graphManager, logger)),
            ("MemoryIntensive", CreateMemoryIntensiveGraph(graphManager, logger))
        };

        // Build all graphs in parallel
        await graphManager.BuildAllGraphsAsync();

        // Execute and compare performance
        var results = new List<(string Name, MetalGraphExecutionResult? Result)>();

        foreach (var (name, graph) in graphs)
        {
            logger.LogInformation("Executing graph '{GraphName}'", name);

            // Optimize each graph individually
            var optimizationResult = await graphManager.OptimizeGraphAsync(name);
            if (optimizationResult?.Success == true)
            {
                logger.LogInformation("Optimized '{GraphName}' - {OptimizationSteps} steps applied",
                    name, optimizationResult.OptimizationSteps.Count);
            }

            // Execute with performance monitoring
            var executionResult = await graphManager.ExecuteGraphAsync(name, commandQueue);
            results.Add((name, executionResult));

            if (executionResult?.Success == true)
            {
                logger.LogInformation("'{GraphName}' executed in {ExecutionTime:F2}ms", 
                    name, executionResult.ExecutionDuration.TotalMilliseconds);
            }
        }

        // Generate comparative analysis
        var aggregatedStats = graphManager.GetAggregatedStatistics();
        logger.LogInformation("Aggregated statistics - {TotalGraphs} graphs, {TotalExecutions} executions, " +
                            "{SuccessRate:F1}% success rate",
            aggregatedStats.TotalGraphs, aggregatedStats.TotalExecutions, aggregatedStats.OverallSuccessRate);

        // Demonstrate graph cloning and modification
        var clonedGraph = graphManager.CloneGraph("SimpleCompute", "SimpleComputeClone");
        if (clonedGraph != null)
        {
            logger.LogInformation("Successfully cloned graph 'SimpleCompute' to 'SimpleComputeClone'");
            
            // Execute the cloned graph
            var cloneResult = await graphManager.ExecuteGraphAsync("SimpleComputeClone", commandQueue);
            if (cloneResult?.Success == true)
            {
                logger.LogInformation("Cloned graph executed in {ExecutionTime:F2}ms", 
                    cloneResult.ExecutionDuration.TotalMilliseconds);
            }
        }

        // Clean up
        foreach (var (name, _) in graphs)
        {
            graphManager.RemoveGraph(name);
        }
        graphManager.RemoveGraph("SimpleComputeClone");
    }

    /// <summary>
    /// Demonstrates error handling and recovery in graph execution.
    /// </summary>
    /// <param name="logger">Logger for operation tracking.</param>
    /// <param name="commandQueue">Metal command queue handle.</param>
    /// <returns>Task representing the asynchronous operation.</returns>
    public static async Task RunErrorHandlingExampleAsync(ILogger logger, IntPtr commandQueue)
    {
        var config = MetalGraphConfiguration.CreateDebugOptimized();
        var typedLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalGraphManager>.Instance;
        using var graphManager = new MetalGraphManager(typedLogger, config);

        try
        {
            // Create a graph with intentional issues for error handling demonstration
            var graph = graphManager.CreateGraph("ErrorHandlingDemo");

            // Add a kernel with invalid configuration to trigger validation errors
            var invalidKernel = CreateMockKernel("invalid_kernel");
            var invalidNode = graph.AddKernelNode(
                invalidKernel,
                MTLSize.Make(0, 0), // Invalid threadgroup size
                MTLSize.Make(2048, 1), // Exceeds Metal limits
                new object[] { IntPtr.Zero } // Invalid buffer
            );

            logger.LogInformation("Created graph with intentional errors for demonstration");

            // Attempt to build the graph - this should catch validation errors
            try
            {
                graph.Build();
                logger.LogWarning("Graph build unexpectedly succeeded despite invalid configuration");
            }
            catch (InvalidOperationException ex)
            {
                logger.LogInformation("Graph validation correctly caught error: {Error}", ex.Message);

                // Fix the issues
                invalidNode.ThreadgroupsPerGrid = MTLSize.Make(64, 64);
                invalidNode.ThreadsPerThreadgroup = MTLSize.Make(16, 16);
                invalidNode.Arguments = new object[] { CreateMockBuffer(1024) };

                // Retry building
                graph.Build();
                logger.LogInformation("Graph built successfully after fixing validation errors");
            }

            // Attempt execution with timeout to demonstrate timeout handling
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            
            try
            {
                var result = await graphManager.ExecuteGraphAsync("ErrorHandlingDemo", commandQueue, cts.Token);
                if (result?.Success == false)
                {
                    logger.LogInformation("Graph execution failed as expected: {Error}", result.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Graph execution correctly timed out as expected");
            }

            // Check error statistics
            var statistics = graphManager.GetGraphStatistics("ErrorHandlingDemo");
            if (statistics != null)
            {
                logger.LogInformation("Error statistics - Failed executions: {FailedCount}, " +
                                    "Node failures: {NodeFailures}",
                    statistics.FailedExecutions, statistics.NodeExecutionFailures);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in error handling demonstration");
        }
    }

    #region Helper Methods for Creating Example Graphs

    private static MetalComputeGraph CreateSimpleComputeGraph(MetalGraphManager manager, ILogger logger)
    {
        var graph = manager.CreateGraph("SimpleCompute");
        var kernel = CreateMockKernel("simple_add");
        
        graph.AddKernelNode(
            kernel,
            MTLSize.Make(64, 64),
            MTLSize.Make(16, 16),
            new object[] { CreateMockBuffer(1024), CreateMockBuffer(1024), CreateMockBuffer(1024) }
        );

        logger.LogDebug("Created simple compute graph with 1 kernel node");
        return graph;
    }

    private static MetalComputeGraph CreateComplexPipelineGraph(MetalGraphManager manager, ILogger logger)
    {
        var graph = manager.CreateGraph("ComplexPipeline");
        
        // Create a multi-stage pipeline
        var stage1 = graph.AddKernelNode(
            CreateMockKernel("preprocess"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            new object[] { CreateMockBuffer(2048) }
        );

        var stage2A = graph.AddKernelNode(
            CreateMockKernel("process_branch_a"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            new object[] { CreateMockBuffer(2048) },
            dependencies: new[] { stage1 }
        );

        var stage2B = graph.AddKernelNode(
            CreateMockKernel("process_branch_b"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            new object[] { CreateMockBuffer(2048) },
            dependencies: new[] { stage1 }
        );
        _ = graph.AddKernelNode(
            CreateMockKernel("merge_results"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            new object[] { CreateMockBuffer(2048), CreateMockBuffer(2048), CreateMockBuffer(2048) },
            dependencies: new[] { stage2A, stage2B }
        );

        logger.LogDebug("Created complex pipeline graph with 4 kernel nodes");
        return graph;
    }

    private static MetalComputeGraph CreateMemoryIntensiveGraph(MetalGraphManager manager, ILogger logger)
    {
        var graph = manager.CreateGraph("MemoryIntensive");

        // Create multiple memory operations
        var largeBuffer1 = CreateMockBuffer(1024 * 1024);
        var largeBuffer2 = CreateMockBuffer(1024 * 1024);
        var largeBuffer3 = CreateMockBuffer(1024 * 1024);

        var copy1 = graph.AddMemoryCopyNode(largeBuffer1, largeBuffer2, 1024 * 1024);
        var copy2 = graph.AddMemoryCopyNode(largeBuffer2, largeBuffer3, 1024 * 1024, new[] { copy1 });
        _ = graph.AddKernelNode(
            CreateMockKernel("memory_intensive_kernel"),
            MTLSize.Make(128, 128),
            MTLSize.Make(8, 8),
            new object[] { largeBuffer3 },
            dependencies: new[] { copy2 }
        );

        logger.LogDebug("Created memory intensive graph with 2 copy nodes and 1 kernel node");
        return graph;
    }

    private static ICompiledKernel CreateMockKernel(string name)
    {
        return new MockCompiledKernel { Name = name };
    }

    private static IntPtr CreateMockBuffer(long size)
    {
        // In a real implementation, this would create an actual Metal buffer
        return new IntPtr(Random.Shared.Next(0x1000, 0x10000));
    }

    #endregion
}

/// <summary>
/// Mock implementation of ICompiledKernel for demonstration purposes.
/// In a real implementation, this would be a Metal-compiled kernel.
/// </summary>
internal class MockCompiledKernel : ICompiledKernel
{
    public string Name { get; set; } = string.Empty;
    public IntPtr Handle { get; set; }
    public bool IsReady { get; set; } = true;
    public string BackendType => "Metal";
    public bool IsDisposed { get; private set; }

    public Task ExecuteAsync(object[] parameters, CancellationToken cancellationToken = default)
    {
        // Mock implementation - just return completed task
        return Task.CompletedTask;
    }

    public object GetMetadata()
    {
        return new
        {
            Name = Name,
            BackendType = BackendType,
            IsReady = IsReady,
            Handle = Handle,
            IsDisposed = IsDisposed
        };
    }

    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}