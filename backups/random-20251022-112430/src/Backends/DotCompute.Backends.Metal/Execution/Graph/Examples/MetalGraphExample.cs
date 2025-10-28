// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;
using DotCompute.Backends.Metal.Execution.Graph.Configuration;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph.Examples;

/// <summary>
/// Demonstrates how to use the Metal compute graph system for complex computation pipelines.
/// This example shows graph construction, optimization, and execution with performance monitoring.
/// </summary>
public static partial class MetalGraphExample
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6200,
        Level = LogLevel.Information,
        Message = "Created complex matrix multiplication graph with {NodeCount} nodes")]
    private static partial void LogGraphCreated(ILogger logger, int nodeCount);

    [LoggerMessage(
        EventId = 6201,
        Level = LogLevel.Information,
        Message = "Graph built successfully with critical path length: {CriticalPath}")]
    private static partial void LogGraphBuilt(ILogger logger, int criticalPath);

    [LoggerMessage(
        EventId = 6202,
        Level = LogLevel.Information,
        Message = "Graph analysis - Fusion opportunities: {Fusion}, Memory optimizations: {Memory}, Parallelism: {Parallel}")]
    private static partial void LogGraphAnalysis(ILogger logger, int fusion, int memory, int parallel);

    [LoggerMessage(
        EventId = 6203,
        Level = LogLevel.Information,
        Message = "Graph optimization completed - Performance improvement: {Improvement:F2}x, Memory reduction: {MemoryReduction:P1}")]
    private static partial void LogOptimizationCompleted(ILogger logger, double improvement, double memoryReduction);

    [LoggerMessage(
        EventId = 6204,
        Level = LogLevel.Information,
        Message = "Graph execution completed successfully in {ExecutionTime:F2}ms (GPU: {GpuTime:F2}ms) - {NodesExecuted} nodes, {CommandBuffers} command buffers")]
    private static partial void LogExecutionCompleted(ILogger logger, double executionTime, double gpuTime, int nodesExecuted, int commandBuffers);

    [LoggerMessage(
        EventId = 6205,
        Level = LogLevel.Debug,
        Message = "Performance metric - {MetricName}: {MetricValue}")]
    private static partial void LogPerformanceMetric(ILogger logger, string metricName, object metricValue);

    [LoggerMessage(
        EventId = 6206,
        Level = LogLevel.Information,
        Message = "Graph execution report:\n{Report}")]
    private static partial void LogExecutionReport(ILogger logger, string report);

    [LoggerMessage(
        EventId = 6207,
        Level = LogLevel.Error,
        Message = "Failed to execute matrix multiplication graph")]
    private static partial void LogExecutionFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6208,
        Level = LogLevel.Information,
        Message = "Executing graph '{GraphName}'")]
    private static partial void LogExecutingGraph(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6209,
        Level = LogLevel.Information,
        Message = "Optimized '{GraphName}' - {OptimizationSteps} steps applied")]
    private static partial void LogGraphOptimized(ILogger logger, string graphName, int optimizationSteps);

    [LoggerMessage(
        EventId = 6210,
        Level = LogLevel.Information,
        Message = "'{GraphName}' executed in {ExecutionTime:F2}ms")]
    private static partial void LogGraphExecuted(ILogger logger, string graphName, double executionTime);

    [LoggerMessage(
        EventId = 6211,
        Level = LogLevel.Information,
        Message = "Aggregated statistics - {TotalGraphs} graphs, {TotalExecutions} executions, {SuccessRate:F1}% success rate")]
    private static partial void LogAggregatedStatistics(ILogger logger, int totalGraphs, long totalExecutions, double successRate);

    [LoggerMessage(
        EventId = 6212,
        Level = LogLevel.Information,
        Message = "Successfully cloned graph 'SimpleCompute' to 'SimpleComputeClone'")]
    private static partial void LogGraphCloned(ILogger logger);

    [LoggerMessage(
        EventId = 6213,
        Level = LogLevel.Information,
        Message = "Cloned graph executed in {ExecutionTime:F2}ms")]
    private static partial void LogClonedGraphExecuted(ILogger logger, double executionTime);

    [LoggerMessage(
        EventId = 6214,
        Level = LogLevel.Information,
        Message = "Created graph with intentional errors for demonstration")]
    private static partial void LogErrorDemoGraphCreated(ILogger logger);

    [LoggerMessage(
        EventId = 6215,
        Level = LogLevel.Warning,
        Message = "Graph build unexpectedly succeeded despite invalid configuration")]
    private static partial void LogBuildUnexpectedlySucceeded(ILogger logger);

    [LoggerMessage(
        EventId = 6216,
        Level = LogLevel.Information,
        Message = "Graph validation correctly caught error: {Error}")]
    private static partial void LogValidationCaughtError(ILogger logger, string error);

    [LoggerMessage(
        EventId = 6217,
        Level = LogLevel.Information,
        Message = "Graph built successfully after fixing validation errors")]
    private static partial void LogGraphBuiltAfterFix(ILogger logger);

    [LoggerMessage(
        EventId = 6218,
        Level = LogLevel.Information,
        Message = "Graph execution failed as expected: {Error}")]
    private static partial void LogExecutionFailedAsExpected(ILogger logger, string? error);

    [LoggerMessage(
        EventId = 6219,
        Level = LogLevel.Information,
        Message = "Graph execution correctly timed out as expected")]
    private static partial void LogExecutionTimedOut(ILogger logger);

    [LoggerMessage(
        EventId = 6220,
        Level = LogLevel.Information,
        Message = "Error statistics - Failed executions: {FailedCount}, Node failures: {NodeFailures}")]
    private static partial void LogErrorStatistics(ILogger logger, long failedCount, int nodeFailures);

    [LoggerMessage(
        EventId = 6221,
        Level = LogLevel.Error,
        Message = "Unexpected error in error handling demonstration")]
    private static partial void LogUnexpectedError(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 6222,
        Level = LogLevel.Debug,
        Message = "Created simple compute graph with 1 kernel node")]
    private static partial void LogSimpleGraphCreated(ILogger logger);

    [LoggerMessage(
        EventId = 6223,
        Level = LogLevel.Debug,
        Message = "Created complex pipeline graph with 4 kernel nodes")]
    private static partial void LogComplexPipelineCreated(ILogger logger);

    [LoggerMessage(
        EventId = 6224,
        Level = LogLevel.Debug,
        Message = "Created memory intensive graph with 2 copy nodes and 1 kernel node")]
    private static partial void LogMemoryIntensiveGraphCreated(ILogger logger);

    #endregion

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
                [inputBufferA, matrixSize]
            );

            var normalizeB = graph.AddKernelNode(
                normalizeKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                [inputBufferB, matrixSize]
            );

            // Step 3: Add main computation node - matrix multiplication
            // This depends on both normalization operations completing
            var matrixMultiply = graph.AddKernelNode(
                matMulKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                [inputBufferA, inputBufferB, outputBuffer, matrixSize],
                dependencies: [normalizeA, normalizeB]
            );

            // Step 4: Add memory synchronization barrier
            var syncBarrier = graph.AddBarrierNode([matrixMultiply]);

            // Step 5: Add postprocessing node - apply final transformations
            var postProcess = graph.AddKernelNode(
                postProcessKernel,
                MTLSize.Make(matrixSize / 16, matrixSize / 16),
                MTLSize.Make(16, 16),
                [outputBuffer, matrixSize],
                dependencies: [syncBarrier]
            );

            // Step 6: Add memory operations for result copying
            var tempBuffer = CreateMockBuffer(matrixSize * matrixSize * sizeof(float));
            var copyResult = graph.AddMemoryCopyNode(
                outputBuffer,
                tempBuffer,
                matrixSize * matrixSize * sizeof(float),
                dependencies: [postProcess]
            );

            LogGraphCreated(logger, graph.NodeCount);

            // Step 7: Build the graph (validates dependencies and creates execution plan)
            graph.Build();
            LogGraphBuilt(logger, graph.Statistics.CriticalPathLength);

            // Step 8: Analyze optimization opportunities
            var analysis = await graphManager.AnalyzeGraphAsync("MatrixMultiplicationPipeline");
            if (analysis != null)
            {
                LogGraphAnalysis(logger, analysis.FusionOpportunities,
                    analysis.MemoryCoalescingOpportunities, analysis.ParallelismOpportunities);
            }

            // Step 9: Apply optimizations
            var optimizationResult = await graphManager.OptimizeGraphAsync("MatrixMultiplicationPipeline");
            if (optimizationResult?.Success == true)
            {
                LogOptimizationCompleted(logger, optimizationResult.EstimatedPerformanceImprovement,
                    optimizationResult.MemoryReduction);
            }

            // Step 10: Execute the optimized graph
            var executionResult = await graphManager.ExecuteGraphAsync(
                "MatrixMultiplicationPipeline",

                commandQueue);

            if (executionResult?.Success == true)
            {
                LogExecutionCompleted(logger, executionResult.ExecutionDuration.TotalMilliseconds,
                    executionResult.GpuExecutionTimeMs, executionResult.NodesExecuted,
                    executionResult.CommandBuffersUsed);

                // Log performance metrics
                foreach (var metric in executionResult.PerformanceMetrics)
                {
                    LogPerformanceMetric(logger, metric.Key, metric.Value);
                }
            }

            // Step 11: Generate comprehensive report
            var report = graphManager.GenerateComprehensiveReport();
            LogExecutionReport(logger, report);

            return executionResult;
        }
        catch (Exception ex)
        {
            LogExecutionFailed(logger, ex);
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
            LogExecutingGraph(logger, name);

            // Optimize each graph individually
            var optimizationResult = await graphManager.OptimizeGraphAsync(name);
            if (optimizationResult?.Success == true)
            {
                LogGraphOptimized(logger, name, optimizationResult.OptimizationSteps.Count);
            }

            // Execute with performance monitoring
            var executionResult = await graphManager.ExecuteGraphAsync(name, commandQueue);
            results.Add((name, executionResult));

            if (executionResult?.Success == true)
            {
                LogGraphExecuted(logger, name, executionResult.ExecutionDuration.TotalMilliseconds);
            }
        }

        // Generate comparative analysis
        var aggregatedStats = graphManager.GetAggregatedStatistics();
        LogAggregatedStatistics(logger, aggregatedStats.TotalGraphs, aggregatedStats.TotalExecutions,
            aggregatedStats.OverallSuccessRate);

        // Demonstrate graph cloning and modification
        var clonedGraph = graphManager.CloneGraph("SimpleCompute", "SimpleComputeClone");
        if (clonedGraph != null)
        {
            LogGraphCloned(logger);

            // Execute the cloned graph

            var cloneResult = await graphManager.ExecuteGraphAsync("SimpleComputeClone", commandQueue);
            if (cloneResult?.Success == true)
            {
                LogClonedGraphExecuted(logger, cloneResult.ExecutionDuration.TotalMilliseconds);
            }
        }

        // Clean up
        foreach (var (name, _) in graphs)
        {
            _ = graphManager.RemoveGraph(name);
        }
        _ = graphManager.RemoveGraph("SimpleComputeClone");
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
                [IntPtr.Zero] // Invalid buffer
            );

            LogErrorDemoGraphCreated(logger);

            // Attempt to build the graph - this should catch validation errors
            try
            {
                graph.Build();
                LogBuildUnexpectedlySucceeded(logger);
            }
            catch (InvalidOperationException ex)
            {
                LogValidationCaughtError(logger, ex.Message);

                // Fix the issues
                invalidNode.ThreadgroupsPerGrid = MTLSize.Make(64, 64);
                invalidNode.ThreadsPerThreadgroup = MTLSize.Make(16, 16);
                invalidNode.Arguments = [CreateMockBuffer(1024)];

                // Retry building
                graph.Build();
                LogGraphBuiltAfterFix(logger);
            }

            // Attempt execution with timeout to demonstrate timeout handling
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));


            try
            {
                var result = await graphManager.ExecuteGraphAsync("ErrorHandlingDemo", commandQueue, cts.Token);
                if (result?.Success == false)
                {
                    LogExecutionFailedAsExpected(logger, result.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                LogExecutionTimedOut(logger);
            }

            // Check error statistics
            var statistics = graphManager.GetGraphStatistics("ErrorHandlingDemo");
            if (statistics != null)
            {
                LogErrorStatistics(logger, (long)statistics.FailedExecutions, (int)statistics.NodeExecutionFailures);
            }
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex);
        }
    }

    #region Helper Methods for Creating Example Graphs

    private static MetalComputeGraph CreateSimpleComputeGraph(MetalGraphManager manager, ILogger logger)
    {
        var graph = manager.CreateGraph("SimpleCompute");
        var kernel = CreateMockKernel("simple_add");


        _ = graph.AddKernelNode(
            kernel,
            MTLSize.Make(64, 64),
            MTLSize.Make(16, 16),
            [CreateMockBuffer(1024), CreateMockBuffer(1024), CreateMockBuffer(1024)]
        );

        LogSimpleGraphCreated(logger);
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
            [CreateMockBuffer(2048)]
        );

        var stage2A = graph.AddKernelNode(
            CreateMockKernel("process_branch_a"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            [CreateMockBuffer(2048)],
            dependencies: [stage1]
        );

        var stage2B = graph.AddKernelNode(
            CreateMockKernel("process_branch_b"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            [CreateMockBuffer(2048)],
            dependencies: [stage1]
        );
        _ = graph.AddKernelNode(
            CreateMockKernel("merge_results"),
            MTLSize.Make(32, 32),
            MTLSize.Make(16, 16),
            [CreateMockBuffer(2048), CreateMockBuffer(2048), CreateMockBuffer(2048)],
            dependencies: [stage2A, stage2B]
        );

        LogComplexPipelineCreated(logger);
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
        var copy2 = graph.AddMemoryCopyNode(largeBuffer2, largeBuffer3, 1024 * 1024, [copy1]);
        _ = graph.AddKernelNode(
            CreateMockKernel("memory_intensive_kernel"),
            MTLSize.Make(128, 128),
            MTLSize.Make(8, 8),
            [largeBuffer3],
            dependencies: [copy2]
        );

        LogMemoryIntensiveGraphCreated(logger);
        return graph;
    }

    private static ICompiledKernel CreateMockKernel(string name) => new MockCompiledKernel { Name = name };

    private static IntPtr CreateMockBuffer(long size)
    {
        // In a real implementation, this would create an actual Metal buffer
        // Use int.MaxValue to avoid overflow when size is very large
        var maxValue = (int)Math.Min(size, int.MaxValue);
        var clampedMax = Math.Min(maxValue, 0x10000);
        return new IntPtr(Random.Shared.Next(0x1000, clampedMax));
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
        // Mock implementation - just return completed task

        => Task.CompletedTask;

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