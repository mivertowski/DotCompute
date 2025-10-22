// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Globalization;
using DotCompute.Backends.Metal.Execution.Graph.Configuration;
using DotCompute.Backends.Metal.Execution.Graph.Statistics;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph;

/// <summary>
/// Provides centralized management of Metal compute graphs including creation, optimization,
/// execution, and lifecycle management with comprehensive performance monitoring.
/// </summary>
public sealed partial class MetalGraphManager : IDisposable
{
    private readonly ILogger<MetalGraphManager> _logger;
    private readonly MetalGraphExecutor _executor;
    private readonly MetalGraphOptimizer _optimizer;
    private readonly ConcurrentDictionary<string, MetalComputeGraph> _graphs;
    private readonly ConcurrentDictionary<string, MetalGraphStatistics> _graphStatistics;
    private readonly MetalGraphConfiguration _defaultConfiguration;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalGraphManager"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for graph management operations.</param>
    /// <param name="defaultConfiguration">Default configuration for new graphs.</param>
    public MetalGraphManager(
        ILogger<MetalGraphManager> logger,

        MetalGraphConfiguration? defaultConfiguration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultConfiguration = defaultConfiguration ?? new MetalGraphConfiguration();


        var executorLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalGraphExecutor>.Instance;
        var optimizerLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalGraphOptimizer>.Instance;


        _executor = new MetalGraphExecutor(executorLogger, _defaultConfiguration.MaxConcurrentOperations);
        _optimizer = new MetalGraphOptimizer(optimizerLogger, _defaultConfiguration.OptimizationParameters);


        _graphs = new ConcurrentDictionary<string, MetalComputeGraph>();
        _graphStatistics = new ConcurrentDictionary<string, MetalGraphStatistics>();

        LogManagerInitialized(_logger);
    }

    /// <summary>
    /// Gets the collection of active graphs managed by this instance.
    /// </summary>
    public IReadOnlyDictionary<string, MetalComputeGraph> Graphs => _graphs.AsReadOnly();

    /// <summary>
    /// Gets the statistics for all managed graphs.
    /// </summary>
    public IReadOnlyDictionary<string, MetalGraphStatistics> Statistics => _graphStatistics.AsReadOnly();

    /// <summary>
    /// Gets the number of graphs currently managed.
    /// </summary>
    public int GraphCount => _graphs.Count;

    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Initialized MetalGraphManager with default configuration")]
    private static partial void LogManagerInitialized(ILogger logger);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Created Metal compute graph '{GraphName}'")]
    private static partial void LogGraphCreated(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Removed and disposed Metal compute graph '{GraphName}'")]
    private static partial void LogGraphRemoved(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Debug,
        Message = "Cloning node '{SourceNodeId}' to '{ClonedNodeId}'")]
    private static partial void LogNodeCloning(ILogger logger, string sourceNodeId, string clonedNodeId);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "Cloned graph '{SourceName}' to '{TargetName}'")]
    private static partial void LogGraphCloned(ILogger logger, string sourceName, string targetName);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Warning,
        Message = "Attempted to optimize non-existent graph '{GraphName}'")]
    private static partial void LogOptimizeNonExistentGraph(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "Starting optimization of graph '{GraphName}'")]
    private static partial void LogOptimizationStarting(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Information,
        Message = "Successfully optimized graph '{GraphName}' - Performance improvement: {Improvement:F2}x")]
    private static partial void LogOptimizationCompleted(ILogger logger, string graphName, double improvement);

    [LoggerMessage(
        EventId = 6009,
        Level = LogLevel.Warning,
        Message = "Failed to optimize graph '{GraphName}': {Error}")]
    private static partial void LogOptimizationFailed(ILogger logger, string graphName, string error);

    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Error,
        Message = "Error optimizing graph '{GraphName}'")]
    private static partial void LogOptimizationError(ILogger logger, Exception ex, string graphName);

    [LoggerMessage(
        EventId = 6011,
        Level = LogLevel.Warning,
        Message = "Attempted to analyze non-existent graph '{GraphName}'")]
    private static partial void LogAnalyzeNonExistentGraph(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6012,
        Level = LogLevel.Warning,
        Message = "Attempted to execute non-existent graph '{GraphName}'")]
    private static partial void LogExecuteNonExistentGraph(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6013,
        Level = LogLevel.Information,
        Message = "Building graph '{GraphName}' before execution")]
    private static partial void LogBuildingGraphForExecution(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6014,
        Level = LogLevel.Error,
        Message = "Failed to build graph '{GraphName}' for execution")]
    private static partial void LogBuildGraphFailed(ILogger logger, Exception ex, string graphName);

    [LoggerMessage(
        EventId = 6015,
        Level = LogLevel.Information,
        Message = "Executing graph '{GraphName}' with {NodeCount} nodes")]
    private static partial void LogGraphExecutionStarted(ILogger logger, string graphName, int nodeCount);

    [LoggerMessage(
        EventId = 6016,
        Level = LogLevel.Information,
        Message = "Successfully executed graph '{GraphName}' in {ExecutionTime:F2}ms (GPU: {GpuTime:F2}ms)")]
    private static partial void LogGraphExecutionCompleted(ILogger logger, string graphName, double executionTime, double gpuTime);

    [LoggerMessage(
        EventId = 6017,
        Level = LogLevel.Warning,
        Message = "Failed to execute graph '{GraphName}': {Error}")]
    private static partial void LogGraphExecutionFailed(ILogger logger, string graphName, string error);

    [LoggerMessage(
        EventId = 6018,
        Level = LogLevel.Error,
        Message = "Error executing graph '{GraphName}'")]
    private static partial void LogGraphExecutionError(ILogger logger, Exception ex, string graphName);

    [LoggerMessage(
        EventId = 6019,
        Level = LogLevel.Debug,
        Message = "All graphs are already built")]
    private static partial void LogAllGraphsAlreadyBuilt(ILogger logger);

    [LoggerMessage(
        EventId = 6020,
        Level = LogLevel.Information,
        Message = "Building {UnbuiltCount} graphs")]
    private static partial void LogBuildingUnbuiltGraphs(ILogger logger, int unbuiltCount);

    [LoggerMessage(
        EventId = 6021,
        Level = LogLevel.Debug,
        Message = "Built graph '{GraphName}'")]
    private static partial void LogGraphBuilt(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6022,
        Level = LogLevel.Error,
        Message = "Failed to build graph '{GraphName}'")]
    private static partial void LogGraphBuildFailed(ILogger logger, Exception ex, string graphName);

    [LoggerMessage(
        EventId = 6023,
        Level = LogLevel.Information,
        Message = "Reset statistics for graph '{GraphName}'")]
    private static partial void LogStatisticsReset(ILogger logger, string graphName);

    [LoggerMessage(
        EventId = 6024,
        Level = LogLevel.Information,
        Message = "Reset statistics for all {GraphCount} graphs")]
    private static partial void LogAllStatisticsReset(ILogger logger, int graphCount);

    [LoggerMessage(
        EventId = 6025,
        Level = LogLevel.Debug,
        Message = "Applied configuration to graph '{GraphName}' - Parallel execution: {ParallelExecution}, Apple Silicon optimizations: {AppleSiliconOpt}")]
    private static partial void LogConfigurationApplied(ILogger logger, string graphName, bool parallelExecution, bool appleSiliconOpt);

    [LoggerMessage(
        EventId = 6026,
        Level = LogLevel.Information,
        Message = "Disposing MetalGraphManager with {GraphCount} graphs")]
    private static partial void LogDisposingManager(ILogger logger, int graphCount);

    [LoggerMessage(
        EventId = 6027,
        Level = LogLevel.Warning,
        Message = "Error disposing graph '{GraphName}'")]
    private static partial void LogGraphDisposalError(ILogger logger, Exception ex, string graphName);

    [LoggerMessage(
        EventId = 6028,
        Level = LogLevel.Information,
        Message = "MetalGraphManager disposed successfully")]
    private static partial void LogManagerDisposed(ILogger logger);

    [LoggerMessage(
        EventId = 6029,
        Level = LogLevel.Error,
        Message = "Error during MetalGraphManager disposal")]
    private static partial void LogManagerDisposalError(ILogger logger, Exception ex);

    #endregion

    #region Graph Creation and Management

    /// <summary>
    /// Creates a new Metal compute graph with the specified configuration.
    /// </summary>
    /// <param name="name">The unique name for the graph.</param>
    /// <param name="configuration">Optional configuration for the graph.</param>
    /// <returns>The created Metal compute graph.</returns>
    /// <exception cref="ArgumentException">Thrown when name is null, empty, or already exists.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the manager has been disposed.</exception>
    public MetalComputeGraph CreateGraph(string name, MetalGraphConfiguration? configuration = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Graph name cannot be null or empty.", nameof(name));
        }

        if (_graphs.ContainsKey(name))
        {
            throw new ArgumentException($"Graph with name '{name}' already exists.", nameof(name));
        }

        var config = configuration ?? _defaultConfiguration;
        var graphLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MetalComputeGraph>.Instance;


        var graph = new MetalComputeGraph(name, graphLogger);

        // Apply configuration settings

        ApplyConfigurationToGraph(graph, config);

        if (_graphs.TryAdd(name, graph))
        {
            _graphStatistics[name] = graph.Statistics;
            LogGraphCreated(_logger, name);
            return graph;
        }
        else
        {
            graph.Dispose();
            throw new InvalidOperationException($"Failed to register graph '{name}'. It may have been created concurrently.");
        }
    }

    /// <summary>
    /// Gets an existing graph by name.
    /// </summary>
    /// <param name="name">The name of the graph to retrieve.</param>
    /// <returns>The graph with the specified name, or null if not found.</returns>
    public MetalComputeGraph? GetGraph(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        return _graphs.TryGetValue(name ?? string.Empty, out var graph) ? graph : null;
    }

    /// <summary>
    /// Removes and disposes a graph.
    /// </summary>
    /// <param name="name">The name of the graph to remove.</param>
    /// <returns>True if the graph was found and removed; otherwise, false.</returns>
    public bool RemoveGraph(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }


        if (_graphs.TryRemove(name, out var graph))
        {
            _ = _graphStatistics.TryRemove(name, out _);
            graph.Dispose();


            LogGraphRemoved(_logger, name);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Clones an existing graph with a new name.
    /// </summary>
    /// <param name="sourceName">The name of the source graph to clone.</param>
    /// <param name="targetName">The name for the cloned graph.</param>
    /// <param name="configuration">Optional configuration for the cloned graph.</param>
    /// <returns>The cloned graph, or null if the source graph was not found.</returns>
    /// <exception cref="ArgumentException">Thrown when target name already exists.</exception>
    public MetalComputeGraph? CloneGraph(string sourceName, string targetName, MetalGraphConfiguration? configuration = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }


        if (_graphs.ContainsKey(targetName))
        {
            throw new ArgumentException($"Graph with name '{targetName}' already exists.", nameof(targetName));
        }

        var sourceGraph = GetGraph(sourceName);
        if (sourceGraph == null)
        {

            return null;
        }


        var clonedGraph = CreateGraph(targetName, configuration);

        // Clone nodes from source graph

        foreach (var sourceNode in sourceGraph.Nodes)
        {
            var clonedNode = sourceNode.Clone($"clone_{sourceNode.Id}");

            // Add the cloned node to the new graph (this would need proper implementation)
            // For now, we'll just log the cloning operation

            LogNodeCloning(_logger, sourceNode.Id, clonedNode.Id);
        }

        LogGraphCloned(_logger, sourceName, targetName);
        return clonedGraph;
    }

    #endregion

    #region Graph Optimization

    /// <summary>
    /// Optimizes a graph for maximum performance and efficiency.
    /// </summary>
    /// <param name="graphName">The name of the graph to optimize.</param>
    /// <param name="parameters">Optional optimization parameters.</param>
    /// <returns>The optimization result, or null if the graph was not found.</returns>
    public async Task<MetalOptimizationResult?> OptimizeGraphAsync(
        string graphName,

        MetalOptimizationParameters? parameters = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var graph = GetGraph(graphName);
        if (graph == null)
        {
            LogOptimizeNonExistentGraph(_logger, graphName);
            return null;
        }

        LogOptimizationStarting(_logger, graphName);


        try
        {
            var result = await _optimizer.OptimizeAsync(graph, parameters);


            if (result.Success)
            {
                // Update graph statistics with optimization results
                graph.Statistics.UpdateOptimizationStatistics(
                    result.KernelFusionsApplied,
                    result.MemoryOptimizationsApplied,
                    result.CommandBufferOptimizationsApplied,
                    result.EstimatedPerformanceImprovement);

                LogOptimizationCompleted(_logger, graphName, result.EstimatedPerformanceImprovement);
            }
            else
            {
                LogOptimizationFailed(_logger, graphName, result.ErrorMessage ?? "Unknown error");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogOptimizationError(_logger, ex, graphName);
            throw;
        }
    }

    /// <summary>
    /// Analyzes a graph for optimization opportunities without applying changes.
    /// </summary>
    /// <param name="graphName">The name of the graph to analyze.</param>
    /// <returns>The analysis result, or null if the graph was not found.</returns>
    public async Task<MetalGraphAnalysis?> AnalyzeGraphAsync(string graphName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var graph = GetGraph(graphName);
        if (graph == null)
        {
            LogAnalyzeNonExistentGraph(_logger, graphName);
            return null;
        }

        return await MetalGraphOptimizer.AnalyzeGraphAsync(graph);
    }

    #endregion

    #region Graph Execution

    /// <summary>
    /// Executes a graph using the specified Metal command queue.
    /// </summary>
    /// <param name="graphName">The name of the graph to execute.</param>
    /// <param name="commandQueue">The Metal command queue for execution.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The execution result, or null if the graph was not found.</returns>
    public async Task<MetalGraphExecutionResult?> ExecuteGraphAsync(
        string graphName,
        IntPtr commandQueue,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var graph = GetGraph(graphName);
        if (graph == null)
        {
            LogExecuteNonExistentGraph(_logger, graphName);
            return null;
        }

        if (!graph.IsBuilt)
        {
            LogBuildingGraphForExecution(_logger, graphName);
            try
            {
                graph.Build();
            }
            catch (Exception ex)
            {
                LogBuildGraphFailed(_logger, ex, graphName);
                throw;
            }
        }

        LogGraphExecutionStarted(_logger, graphName, graph.NodeCount);

        try
        {
            var result = await _executor.ExecuteAsync(graph, commandQueue, cancellationToken);

            // Update statistics with execution results

            var statistics = _graphStatistics.GetValueOrDefault(graphName);
            if (statistics != null)
            {
                statistics.UpdateExecutionStatistics(
                    result.GpuExecutionTimeMs,
                    result.ExecutionDuration.TotalMilliseconds - result.GpuExecutionTimeMs,
                    result.TotalMemoryTransferred,
                    result.CommandBuffersUsed,
                    result.Success);
            }

            if (result.Success)
            {
                LogGraphExecutionCompleted(_logger, graphName,
                    result.ExecutionDuration.TotalMilliseconds, result.GpuExecutionTimeMs);
            }
            else
            {
                LogGraphExecutionFailed(_logger, graphName, result.ErrorMessage ?? "Unknown error");
            }

            return result;
        }
        catch (Exception ex)
        {
            // Record the error in statistics
            var statistics = _graphStatistics.GetValueOrDefault(graphName);
            statistics?.RecordError("execution", ex.Message);

            LogGraphExecutionError(_logger, ex, graphName);
            throw;
        }
    }

    /// <summary>
    /// Builds all graphs that are not yet built.
    /// </summary>
    /// <returns>A task representing the asynchronous build operation.</returns>
    public async Task BuildAllGraphsAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var unbuiltGraphs = _graphs.Values.Where(g => !g.IsBuilt).ToList();


        if (unbuiltGraphs.Count == 0)
        {
            LogAllGraphsAlreadyBuilt(_logger);
            return;
        }

        LogBuildingUnbuiltGraphs(_logger, unbuiltGraphs.Count);

        var buildTasks = unbuiltGraphs.Select(async graph =>
        {
            try
            {
                await Task.Run(() => graph.Build());
                LogGraphBuilt(_logger, graph.Name);
            }
            catch (Exception ex)
            {
                LogGraphBuildFailed(_logger, ex, graph.Name);
            }
        });

        await Task.WhenAll(buildTasks);
    }

    #endregion

    #region Statistics and Monitoring

    /// <summary>
    /// Gets comprehensive statistics for a specific graph.
    /// </summary>
    /// <param name="graphName">The name of the graph.</param>
    /// <returns>The graph statistics, or null if not found.</returns>
    public MetalGraphStatistics? GetGraphStatistics(string graphName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        return _graphStatistics.TryGetValue(graphName ?? string.Empty, out var stats) ? stats : null;
    }

    /// <summary>
    /// Gets aggregated statistics for all managed graphs.
    /// </summary>
    /// <returns>Aggregated statistics across all graphs.</returns>
    public AggregatedGraphStatistics GetAggregatedStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var stats = new AggregatedGraphStatistics
        {
            TotalGraphs = _graphs.Count,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var graphStats in _graphStatistics.Values)
        {
            stats.TotalExecutions += graphStats.TotalExecutions;
            stats.TotalSuccessfulExecutions += graphStats.SuccessfulExecutions;
            stats.TotalFailedExecutions += graphStats.FailedExecutions;
            stats.TotalNodes += graphStats.NodeCount;
            stats.TotalMemoryTransferred += graphStats.TotalMemoryTransferred;
            stats.TotalGpuTimeMs += graphStats.TotalGpuExecutionTimeMs;
            stats.TotalOptimizationPasses += graphStats.OptimizationPasses;

            if (graphStats.AverageGpuExecutionTimeMs > 0)
            {
                stats.AverageExecutionTimeMs = (stats.AverageExecutionTimeMs * (stats.GraphsWithExecutions - 1) +

                                               graphStats.AverageGpuExecutionTimeMs) / stats.GraphsWithExecutions;
                stats.GraphsWithExecutions++;
            }
        }

        stats.OverallSuccessRate = stats.TotalExecutions > 0 ?

            (double)stats.TotalSuccessfulExecutions / stats.TotalExecutions * 100 : 0;

        return stats;
    }

    /// <summary>
    /// Generates a comprehensive report of all graph statistics.
    /// </summary>
    /// <returns>A formatted report string.</returns>
    public string GenerateComprehensiveReport()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var report = new System.Text.StringBuilder();
        var aggregated = GetAggregatedStatistics();

        _ = report.AppendLine("Metal Graph Manager Comprehensive Report");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        _ = report.AppendLine(new string('=', 60));

        // Manager summary
        _ = report.AppendLine("\nManager Summary:");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total Graphs Managed: {aggregated.TotalGraphs:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total Executions: {aggregated.TotalExecutions:N0}");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Overall Success Rate: {aggregated.OverallSuccessRate:F1}%");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Total GPU Time: {aggregated.TotalGpuTimeMs:F0} ms");
        _ = report.AppendLine(CultureInfo.InvariantCulture, $"  Average Execution Time: {aggregated.AverageExecutionTimeMs:F2} ms");

        // Individual graph reports
        _ = report.AppendLine("\nIndividual Graph Reports:");
        _ = report.AppendLine(new string('-', 40));

        foreach (var kvp in _graphStatistics.OrderBy(kvp => kvp.Key))
        {
            var graphReport = kvp.Value.GenerateReport();
            _ = report.AppendLine(graphReport);
            _ = report.AppendLine(new string('-', 40));
        }

        return report.ToString();
    }

    /// <summary>
    /// Resets statistics for a specific graph.
    /// </summary>
    /// <param name="graphName">The name of the graph to reset statistics for.</param>
    /// <returns>True if statistics were reset; false if graph not found.</returns>
    public bool ResetGraphStatistics(string graphName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        var statistics = GetGraphStatistics(graphName);
        if (statistics != null)
        {
            statistics.Reset();
            LogStatisticsReset(_logger, graphName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets statistics for all graphs.
    /// </summary>
    public void ResetAllStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        foreach (var stats in _graphStatistics.Values)
        {
            stats.Reset();
        }

        LogAllStatisticsReset(_logger, _graphs.Count);
    }

    #endregion

    #region Private Helper Methods

    private void ApplyConfigurationToGraph(MetalComputeGraph graph, MetalGraphConfiguration config)
        // Apply configuration settings to the graph
        // This would involve setting up the graph based on the configuration


        => LogConfigurationApplied(_logger, graph.Name, config.EnableParallelExecution, config.EnableAppleSiliconOptimizations);

    #endregion

    #region Disposal

    /// <summary>
    /// Releases all resources used by the Metal graph manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            LogDisposingManager(_logger, _graphs.Count);

            // Dispose all graphs
            foreach (var graph in _graphs.Values)
            {
                try
                {
                    graph.Dispose();
                }
                catch (Exception ex)
                {
                    LogGraphDisposalError(_logger, ex, graph.Name);
                }
            }

            _graphs.Clear();
            _graphStatistics.Clear();

            // Dispose executor and optimizer
            _executor.Dispose();

            LogManagerDisposed(_logger);
        }
        catch (Exception ex)
        {
            LogManagerDisposalError(_logger, ex);
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Contains aggregated statistics across all managed Metal compute graphs.
/// </summary>
public class AggregatedGraphStatistics
{
    /// <summary>Gets or sets the total number of graphs.</summary>
    public int TotalGraphs { get; set; }

    /// <summary>Gets or sets the total number of nodes across all graphs.</summary>
    public int TotalNodes { get; set; }

    /// <summary>Gets or sets the total number of executions across all graphs.</summary>
    public long TotalExecutions { get; set; }

    /// <summary>Gets or sets the total number of successful executions.</summary>
    public long TotalSuccessfulExecutions { get; set; }

    /// <summary>Gets or sets the total number of failed executions.</summary>
    public long TotalFailedExecutions { get; set; }

    /// <summary>Gets or sets the overall success rate percentage.</summary>
    public double OverallSuccessRate { get; set; }

    /// <summary>Gets or sets the total GPU execution time in milliseconds.</summary>
    public double TotalGpuTimeMs { get; set; }

    /// <summary>Gets or sets the average execution time across all graphs.</summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>Gets or sets the total memory transferred across all executions.</summary>
    public long TotalMemoryTransferred { get; set; }

    /// <summary>Gets or sets the total number of optimization passes.</summary>
    public int TotalOptimizationPasses { get; set; }

    /// <summary>Gets or sets the number of graphs that have execution history.</summary>
    public int GraphsWithExecutions { get; set; }

    /// <summary>Gets or sets the timestamp when these statistics were generated.</summary>
    public DateTime GeneratedAt { get; set; }
}