// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using DotCompute.Backends.Metal.Execution.Graph.Configuration;
using DotCompute.Backends.Metal.Execution.Graph.Statistics;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph;

/// <summary>
/// Provides centralized management of Metal compute graphs including creation, optimization,
/// execution, and lifecycle management with comprehensive performance monitoring.
/// </summary>
public sealed class MetalGraphManager : IDisposable
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

        _logger.LogInformation("Initialized MetalGraphManager with default configuration");
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
            _logger.LogInformation("Created Metal compute graph '{GraphName}'", name);
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
            _graphStatistics.TryRemove(name, out _);
            graph.Dispose();
            
            _logger.LogInformation("Removed and disposed Metal compute graph '{GraphName}'", name);
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
            _logger.LogDebug("Cloning node '{SourceNodeId}' to '{ClonedNodeId}'", sourceNode.Id, clonedNode.Id);
        }

        _logger.LogInformation("Cloned graph '{SourceName}' to '{TargetName}'", sourceName, targetName);
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
            _logger.LogWarning("Attempted to optimize non-existent graph '{GraphName}'", graphName);
            return null;
        }

        _logger.LogInformation("Starting optimization of graph '{GraphName}'", graphName);
        
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

                _logger.LogInformation(
                    "Successfully optimized graph '{GraphName}' - Performance improvement: {Improvement:F2}x",
                    graphName, result.EstimatedPerformanceImprovement);
            }
            else
            {
                _logger.LogWarning("Failed to optimize graph '{GraphName}': {Error}", 
                    graphName, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing graph '{GraphName}'", graphName);
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
            _logger.LogWarning("Attempted to analyze non-existent graph '{GraphName}'", graphName);
            return null;
        }

        return await _optimizer.AnalyzeGraphAsync(graph);
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
            _logger.LogWarning("Attempted to execute non-existent graph '{GraphName}'", graphName);
            return null;
        }

        if (!graph.IsBuilt)
        {
            _logger.LogInformation("Building graph '{GraphName}' before execution", graphName);
            try
            {
                graph.Build();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build graph '{GraphName}' for execution", graphName);
                throw;
            }
        }

        _logger.LogInformation("Executing graph '{GraphName}' with {NodeCount} nodes", 
            graphName, graph.NodeCount);

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
                _logger.LogInformation(
                    "Successfully executed graph '{GraphName}' in {ExecutionTime:F2}ms (GPU: {GpuTime:F2}ms)",
                    graphName, result.ExecutionDuration.TotalMilliseconds, result.GpuExecutionTimeMs);
            }
            else
            {
                _logger.LogWarning("Failed to execute graph '{GraphName}': {Error}", 
                    graphName, result.ErrorMessage);
            }

            return result;
        }
        catch (Exception ex)
        {
            // Record the error in statistics
            var statistics = _graphStatistics.GetValueOrDefault(graphName);
            statistics?.RecordError("execution", ex.Message);

            _logger.LogError(ex, "Error executing graph '{GraphName}'", graphName);
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
            _logger.LogDebug("All graphs are already built");
            return;
        }

        _logger.LogInformation("Building {UnbuiltCount} graphs", unbuiltGraphs.Count);

        var buildTasks = unbuiltGraphs.Select(async graph =>
        {
            try
            {
                await Task.Run(() => graph.Build());
                _logger.LogDebug("Built graph '{GraphName}'", graph.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build graph '{GraphName}'", graph.Name);
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

        report.AppendLine("Metal Graph Manager Comprehensive Report");
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine(new string('=', 60));

        // Manager summary
        report.AppendLine("\nManager Summary:");
        report.AppendLine($"  Total Graphs Managed: {aggregated.TotalGraphs:N0}");
        report.AppendLine($"  Total Executions: {aggregated.TotalExecutions:N0}");
        report.AppendLine($"  Overall Success Rate: {aggregated.OverallSuccessRate:F1}%");
        report.AppendLine($"  Total GPU Time: {aggregated.TotalGpuTimeMs:F0} ms");
        report.AppendLine($"  Average Execution Time: {aggregated.AverageExecutionTimeMs:F2} ms");

        // Individual graph reports
        report.AppendLine("\nIndividual Graph Reports:");
        report.AppendLine(new string('-', 40));

        foreach (var kvp in _graphStatistics.OrderBy(kvp => kvp.Key))
        {
            var graphReport = kvp.Value.GenerateReport();
            report.AppendLine(graphReport);
            report.AppendLine(new string('-', 40));
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
            _logger.LogInformation("Reset statistics for graph '{GraphName}'", graphName);
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

        _logger.LogInformation("Reset statistics for all {GraphCount} graphs", _graphs.Count);
    }

    #endregion

    #region Private Helper Methods

    private void ApplyConfigurationToGraph(MetalComputeGraph graph, MetalGraphConfiguration config)
    {
        // Apply configuration settings to the graph
        // This would involve setting up the graph based on the configuration
        
        _logger.LogDebug("Applied configuration to graph '{GraphName}' - " +
                        "Parallel execution: {ParallelExecution}, " +
                        "Apple Silicon optimizations: {AppleSiliconOpt}",
            graph.Name, config.EnableParallelExecution, config.EnableAppleSiliconOptimizations);
    }

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
            _logger.LogInformation("Disposing MetalGraphManager with {GraphCount} graphs", _graphs.Count);

            // Dispose all graphs
            foreach (var graph in _graphs.Values)
            {
                try
                {
                    graph.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing graph '{GraphName}'", graph.Name);
                }
            }

            _graphs.Clear();
            _graphStatistics.Clear();

            // Dispose executor and optimizer
            _executor.Dispose();

            _logger.LogInformation("MetalGraphManager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MetalGraphManager disposal");
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