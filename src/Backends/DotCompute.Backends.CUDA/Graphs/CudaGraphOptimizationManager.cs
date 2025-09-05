using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using global::System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using DotCompute.Core.Memory;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Graphs
{
    /// <summary>
    /// Production-grade CUDA graph optimization manager with automatic graph capture,
    /// instantiation, and performance analysis.
    /// </summary>
    public sealed class CudaGraphOptimizationManager : IDisposable
    {
        private readonly ILogger<CudaGraphOptimizationManager> _logger;
        private readonly ConcurrentDictionary<string, GraphInstance> _graphs;
        private readonly ConcurrentDictionary<string, GraphStatistics> _statistics;
        private readonly SemaphoreSlim _graphCreationLock;
        private readonly Timer _optimizationTimer;
        private bool _disposed;

        // CUDA API imports
        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphCreate(out IntPtr graph, uint flags);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphInstantiate(
            out IntPtr graphExec, IntPtr graph, IntPtr phErrorNode, IntPtr logBuffer, ulong bufferSize);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphLaunch(IntPtr graphExec, IntPtr stream);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphDestroy(IntPtr graph);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphExecDestroy(IntPtr graphExec);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaStreamBeginCapture(IntPtr stream, CudaGraphCaptureMode mode);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaStreamEndCapture(IntPtr stream, out IntPtr graph);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphExecUpdate(
            IntPtr hGraphExec, IntPtr hGraph, IntPtr hErrorNode, out CudaGraphExecUpdateResult updateResult);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphGetNodes(
            IntPtr graph, IntPtr nodes, out ulong numNodes);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphGetEdges(
            IntPtr graph, IntPtr from, IntPtr to, out ulong numEdges);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphDebugDotPrint(
            IntPtr graph, string path, uint flags);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphClone(out IntPtr clonedGraph, IntPtr originalGraph);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphNodeGetType(
            IntPtr node, out CudaGraphNodeType type);

        [DllImport("cudart64_12", CallingConvention = CallingConvention.Cdecl)]
        private static extern CudaError cudaGraphExecGetFlags(
            IntPtr graphExec, out ulong flags);

        public CudaGraphOptimizationManager(ILogger<CudaGraphOptimizationManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphs = new ConcurrentDictionary<string, GraphInstance>();
            _statistics = new ConcurrentDictionary<string, GraphStatistics>();
            _graphCreationLock = new SemaphoreSlim(1, 1);
            
            // Start optimization timer to periodically analyze and optimize graphs
            _optimizationTimer = new Timer(
                OptimizeGraphs,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
            
            _logger.LogInformation("CUDA Graph Optimization Manager initialized");
        }

        /// <summary>
        /// Captures a sequence of CUDA operations into a graph for optimized execution.
        /// </summary>
        public async Task<string> CaptureGraphAsync(
            IntPtr stream,
            Func<Task> operations,
            string? graphName = null,
            GraphCaptureOptions? options = null)
        {
            options ??= GraphCaptureOptions.Default;
            graphName ??= $"graph_{Guid.NewGuid():N}";

            await _graphCreationLock.WaitAsync();
            try
            {
                _logger.LogDebug("Beginning graph capture for {GraphName}", graphName);
                
                // Start capture
                var captureMode = options.AllowInvalidation
                    ? CudaGraphCaptureMode.Global
                    : CudaGraphCaptureMode.ThreadLocal;
                
                var error = cudaStreamBeginCapture(stream, captureMode);
                if (error != CudaError.Success)
                {
                    throw new CudaException($"Failed to begin graph capture: {error}");
                }

                var captureStartTime = Stopwatch.GetTimestamp();
                
                try
                {
                    // Execute the operations to be captured
                    await operations();
                }
                catch (Exception ex)
                {
                    // Abort capture on error
                    _logger.LogError(ex, "Error during graph capture for {GraphName}", graphName);
                    throw;
                }

                // End capture and get graph
                error = cudaStreamEndCapture(stream, out var graph);
                if (error != CudaError.Success)
                {
                    throw new CudaException($"Failed to end graph capture: {error}");
                }

                var captureElapsed = Stopwatch.GetElapsedTime(captureStartTime);
                
                // Analyze graph structure
                var analysis = await AnalyzeGraphAsync(graph);
                
                // Instantiate graph for execution
                error = cudaGraphInstantiate(
                    out var graphExec,
                    graph,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0);
                
                if (error != CudaError.Success)
                {
                    _ = cudaGraphDestroy(graph);
                    throw new CudaException($"Failed to instantiate graph: {error}");
                }

                // Store graph instance
                var instance = new GraphInstance
                {
                    Name = graphName,
                    Graph = graph,
                    GraphExec = graphExec,
                    CreatedAt = DateTimeOffset.UtcNow,
                    NodeCount = analysis.NodeCount,
                    EdgeCount = analysis.EdgeCount,
                    Options = options,
                    CaptureTime = captureElapsed
                };

                _graphs[graphName] = instance;
                
                // Initialize statistics
                _statistics[graphName] = new GraphStatistics
                {
                    GraphName = graphName,
                    CreatedAt = instance.CreatedAt,
                    NodeCount = analysis.NodeCount,
                    EdgeCount = analysis.EdgeCount
                };

                _logger.LogInformation(
                    "Successfully captured graph {GraphName} with {NodeCount} nodes and {EdgeCount} edges in {CaptureTime:F2}ms",
                    graphName, analysis.NodeCount, analysis.EdgeCount, captureElapsed.TotalMilliseconds);

                // Export debug visualization if requested
                if (options.ExportDebugVisualization)
                {
                    await ExportGraphVisualizationAsync(graph, graphName);
                }

                return graphName;
            }
            finally
            {
                _ = _graphCreationLock.Release();
            }
        }

        /// <summary>
        /// Launches a previously captured graph for execution.
        /// </summary>
        public async Task LaunchGraphAsync(string graphName, IntPtr stream)
        {
            if (!_graphs.TryGetValue(graphName, out var instance))
            {
                throw new ArgumentException($"Graph '{graphName}' not found");
            }

            var launchStart = Stopwatch.GetTimestamp();
            
            var error = cudaGraphLaunch(instance.GraphExec, stream);
            if (error != CudaError.Success)
            {
                _statistics[graphName].FailureCount++;
                throw new CudaException($"Failed to launch graph '{graphName}': {error}");
            }

            var launchElapsed = Stopwatch.GetElapsedTime(launchStart);
            
            // Update statistics
            var stats = _statistics[graphName];
            stats.LaunchCount++;
            stats.TotalLaunchTime += launchElapsed;
            stats.LastLaunchTime = launchElapsed;
            stats.LastUsedAt = DateTimeOffset.UtcNow;
            
            if (launchElapsed < stats.MinLaunchTime || stats.MinLaunchTime == TimeSpan.Zero)
            {
                stats.MinLaunchTime = launchElapsed;
            }


            if (launchElapsed > stats.MaxLaunchTime)
            {
                stats.MaxLaunchTime = launchElapsed;
            }


            _logger.LogDebug(
                "Launched graph {GraphName} in {LaunchTime:F3}ms (avg: {AvgTime:F3}ms)",
                graphName,
                launchElapsed.TotalMilliseconds,
                stats.AverageLaunchTime.TotalMilliseconds);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Updates an existing graph with new operations, preserving optimizations where possible.
        /// </summary>
        public async Task<bool> UpdateGraphAsync(
            string graphName,
            IntPtr stream,
            Func<Task> newOperations)
        {
            if (!_graphs.TryGetValue(graphName, out var instance))
            {
                throw new ArgumentException($"Graph '{graphName}' not found");
            }

            await _graphCreationLock.WaitAsync();
            try
            {
                _logger.LogDebug("Updating graph {GraphName}", graphName);
                
                // Capture new graph
                var error = cudaStreamBeginCapture(stream, CudaGraphCaptureMode.Global);
                if (error != CudaError.Success)
                {
                    throw new CudaException($"Failed to begin graph capture for update: {error}");
                }

                await newOperations();

                error = cudaStreamEndCapture(stream, out var newGraph);
                if (error != CudaError.Success)
                {
                    throw new CudaException($"Failed to end graph capture for update: {error}");
                }

                // Try to update existing executable graph
                error = cudaGraphExecUpdate(
                    instance.GraphExec,
                    newGraph,
                    IntPtr.Zero,
                    out var updateResult);

                if (error == CudaError.Success && updateResult == CudaGraphExecUpdateResult.Success)
                {
                    // Update succeeded - replace graph but keep executable
                    _ = cudaGraphDestroy(instance.Graph);
                    instance.Graph = newGraph;
                    instance.LastUpdatedAt = DateTimeOffset.UtcNow;
                    
                    _statistics[graphName].UpdateCount++;
                    _logger.LogInformation("Successfully updated graph {GraphName} in-place", graphName);
                    return true;
                }
                else
                {
                    // Update failed - need to recreate executable
                    _logger.LogWarning(
                        "In-place update failed for graph {GraphName} (result: {UpdateResult}), recreating",
                        graphName, updateResult);

                    // Destroy old resources
                    _ = cudaGraphExecDestroy(instance.GraphExec);
                    _ = cudaGraphDestroy(instance.Graph);
                    
                    // Create new executable
                    error = cudaGraphInstantiate(
                        out var newGraphExec,
                        newGraph,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        0);
                    
                    if (error != CudaError.Success)
                    {
                        _ = cudaGraphDestroy(newGraph);
                        throw new CudaException($"Failed to instantiate updated graph: {error}");
                    }

                    // Update instance
                    instance.Graph = newGraph;
                    instance.GraphExec = newGraphExec;
                    instance.LastUpdatedAt = DateTimeOffset.UtcNow;
                    
                    _statistics[graphName].RecreateCount++;
                    return false;
                }
            }
            finally
            {
                _ = _graphCreationLock.Release();
            }
        }

        /// <summary>
        /// Clones an existing graph for variant execution paths.
        /// </summary>
        public async Task<string> CloneGraphAsync(string sourceGraphName, string? targetName = null)
        {
            if (!_graphs.TryGetValue(sourceGraphName, out var sourceInstance))
            {
                throw new ArgumentException($"Source graph '{sourceGraphName}' not found");
            }

            targetName ??= $"{sourceGraphName}_clone_{Guid.NewGuid():N}";

            await _graphCreationLock.WaitAsync();
            try
            {
                var error = cudaGraphClone(out var clonedGraph, sourceInstance.Graph);
                if (error != CudaError.Success)
                {
                    throw new CudaException($"Failed to clone graph: {error}");
                }

                error = cudaGraphInstantiate(
                    out var clonedGraphExec,
                    clonedGraph,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0);
                
                if (error != CudaError.Success)
                {
                    _ = cudaGraphDestroy(clonedGraph);
                    throw new CudaException($"Failed to instantiate cloned graph: {error}");
                }

                var clonedInstance = new GraphInstance
                {
                    Name = targetName,
                    Graph = clonedGraph,
                    GraphExec = clonedGraphExec,
                    CreatedAt = DateTimeOffset.UtcNow,
                    NodeCount = sourceInstance.NodeCount,
                    EdgeCount = sourceInstance.EdgeCount,
                    Options = sourceInstance.Options,
                    ClonedFrom = sourceGraphName
                };

                _graphs[targetName] = clonedInstance;
                _statistics[targetName] = new GraphStatistics
                {
                    GraphName = targetName,
                    CreatedAt = clonedInstance.CreatedAt,
                    NodeCount = clonedInstance.NodeCount,
                    EdgeCount = clonedInstance.EdgeCount
                };

                _logger.LogInformation(
                    "Successfully cloned graph {SourceGraph} to {TargetGraph}",
                    sourceGraphName, targetName);

                return targetName;
            }
            finally
            {
                _ = _graphCreationLock.Release();
            }
        }

        /// <summary>
        /// Analyzes graph structure for optimization opportunities.
        /// </summary>
        private async Task<GraphAnalysis> AnalyzeGraphAsync(IntPtr graph)
        {
            var analysis = new GraphAnalysis();

            // Get node count
            var error = cudaGraphGetNodes(graph, IntPtr.Zero, out var nodeCount);
            if (error != CudaError.Success && error != CudaError.InvalidValue)
            {
                _logger.LogWarning("Failed to get graph node count: {Error}", error);
                return analysis;
            }
            analysis.NodeCount = (int)nodeCount;

            // Get edge count
            error = cudaGraphGetEdges(graph, IntPtr.Zero, IntPtr.Zero, out var edgeCount);
            if (error != CudaError.Success && error != CudaError.InvalidValue)
            {
                _logger.LogWarning("Failed to get graph edge count: {Error}", error);
                return analysis;
            }
            analysis.EdgeCount = (int)edgeCount;

            // Analyze node types if we have nodes
            if (nodeCount > 0)
            {
                var nodes = Marshal.AllocHGlobal((int)nodeCount * IntPtr.Size);
                try
                {
                    error = cudaGraphGetNodes(graph, nodes, out nodeCount);
                    if (error == CudaError.Success)
                    {
                        for (var i = 0; i < (int)nodeCount; i++)
                        {
                            var node = Marshal.ReadIntPtr(nodes, i * IntPtr.Size);
                            if (cudaGraphNodeGetType(node, out var nodeType) == CudaError.Success)
                            {
                                analysis.NodeTypes[nodeType] = analysis.NodeTypes.GetValueOrDefault(nodeType) + 1;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(nodes);
                }
            }

            await Task.CompletedTask;
            return analysis;
        }

        /// <summary>
        /// Exports graph visualization for debugging.
        /// </summary>
        private async Task ExportGraphVisualizationAsync(IntPtr graph, string graphName)
        {
            try
            {
                var dotFile = $"graph_{graphName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.dot";
                var error = cudaGraphDebugDotPrint(graph, dotFile, 0);
                
                if (error == CudaError.Success)
                {
                    _logger.LogInformation("Exported graph visualization to {DotFile}", dotFile);
                }
                else
                {
                    _logger.LogWarning("Failed to export graph visualization: {Error}", error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting graph visualization");
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Periodically optimizes graphs based on usage patterns.
        /// </summary>
        private void OptimizeGraphs(object? state)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                var graphsToRemove = new List<string>();

                foreach (var (name, stats) in _statistics)
                {
                    // Remove graphs not used in last hour
                    if (stats.LastUsedAt.HasValue && 
                        now - stats.LastUsedAt.Value > TimeSpan.FromHours(1))
                    {
                        graphsToRemove.Add(name);
                        continue;
                    }

                    // Log performance degradation
                    if (stats.LaunchCount > 100 && 
                        stats.LastLaunchTime > stats.AverageLaunchTime * 1.5)
                    {
                        _logger.LogWarning(
                            "Graph {GraphName} showing performance degradation. Last: {LastTime:F3}ms, Avg: {AvgTime:F3}ms",
                            name,
                            stats.LastLaunchTime.TotalMilliseconds,
                            stats.AverageLaunchTime.TotalMilliseconds);
                    }

                    // Log high failure rate
                    if (stats.LaunchCount > 0)
                    {
                        var failureRate = (double)stats.FailureCount / (stats.LaunchCount + stats.FailureCount);
                        if (failureRate > 0.1)
                        {
                            _logger.LogWarning(
                                "Graph {GraphName} has high failure rate: {FailureRate:P}",
                                name, failureRate);
                        }
                    }
                }

                // Clean up unused graphs
                foreach (var name in graphsToRemove)
                {
                    if (_graphs.TryRemove(name, out var instance))
                    {
                        _ = cudaGraphExecDestroy(instance.GraphExec);
                        _ = cudaGraphDestroy(instance.Graph);
                        _ = _statistics.TryRemove(name, out _);
                        
                        _logger.LogInformation(
                            "Removed unused graph {GraphName} (last used: {LastUsed})",
                            name, instance.LastUsedAt?.ToString() ?? "never");
                    }
                }

                // Log summary
                _logger.LogInformation(
                    "Graph optimization complete. Active graphs: {ActiveCount}, Removed: {RemovedCount}",
                    _graphs.Count, graphsToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graph optimization");
            }
        }

        /// <summary>
        /// Gets performance statistics for a graph.
        /// </summary>
        public GraphStatistics? GetStatistics(string graphName) => _statistics.TryGetValue(graphName, out var stats) ? stats : null;

        /// <summary>
        /// Gets all graph statistics.
        /// </summary>
        public IReadOnlyDictionary<string, GraphStatistics> GetAllStatistics() => _statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        /// <summary>
        /// Removes a graph from the manager.
        /// </summary>
        public bool RemoveGraph(string graphName)
        {
            if (_graphs.TryRemove(graphName, out var instance))
            {
                _ = cudaGraphExecDestroy(instance.GraphExec);
                _ = cudaGraphDestroy(instance.Graph);
                _ = _statistics.TryRemove(graphName, out _);
                
                _logger.LogInformation("Removed graph {GraphName}", graphName);
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }


            _optimizationTimer?.Dispose();
            _graphCreationLock?.Dispose();

            // Clean up all graphs
            foreach (var instance in _graphs.Values)
            {
                try
                {
                    _ = cudaGraphExecDestroy(instance.GraphExec);
                    _ = cudaGraphDestroy(instance.Graph);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing graph {GraphName}", instance.Name);
                }
            }

            _graphs.Clear();
            _statistics.Clear();
            _disposed = true;
        }

        // Supporting classes
        private class GraphInstance
        {
            public required string Name { get; init; }
            public required IntPtr Graph { get; set; }
            public required IntPtr GraphExec { get; set; }
            public required DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? LastUpdatedAt { get; set; }
            public DateTimeOffset? LastUsedAt { get; set; }
            public required int NodeCount { get; init; }
            public required int EdgeCount { get; init; }
            public required GraphCaptureOptions Options { get; init; }
            public TimeSpan CaptureTime { get; init; }
            public string? ClonedFrom { get; init; }
        }

        public class GraphStatistics
        {
            public required string GraphName { get; init; }
            public required DateTimeOffset CreatedAt { get; init; }
            public DateTimeOffset? LastUsedAt { get; set; }
            public int LaunchCount { get; set; }
            public int FailureCount { get; set; }
            public int UpdateCount { get; set; }
            public int RecreateCount { get; set; }
            public TimeSpan TotalLaunchTime { get; set; }
            public TimeSpan LastLaunchTime { get; set; }
            public TimeSpan MinLaunchTime { get; set; }
            public TimeSpan MaxLaunchTime { get; set; }
            public required int NodeCount { get; init; }
            public required int EdgeCount { get; init; }
            
            public TimeSpan AverageLaunchTime
                => LaunchCount > 0 ? TotalLaunchTime / LaunchCount : TimeSpan.Zero;
        }

        private class GraphAnalysis
        {
            public int NodeCount { get; set; }
            public int EdgeCount { get; set; }
            public Dictionary<CudaGraphNodeType, int> NodeTypes { get; } = [];
        }

        public class GraphCaptureOptions
        {
            public bool AllowInvalidation { get; set; } = true;
            public bool ExportDebugVisualization { get; set; }

            public int MaxNodeCount { get; set; } = 10000;
            
            public static GraphCaptureOptions Default => new();
        }

        // CUDA enums
        private enum CudaError
        {
            Success = 0,
            InvalidValue = 1,
            // Add other error codes as needed
        }

        private enum CudaGraphCaptureMode
        {
            Global = 0,
            ThreadLocal = 1,
            Relaxed = 2
        }

        private enum CudaGraphNodeType
        {
            Kernel = 0,
            Memcpy = 1,
            Memset = 2,
            Host = 3,
            Graph = 4,
            Empty = 5,
            // Add other node types as needed
        }

        private enum CudaGraphExecUpdateResult
        {
            Success = 0,
            ErrorTopologyChanged = 1,
            ErrorNodeTypeChanged = 2,
            // Add other results as needed
        }

        private class CudaException : Exception
        {
            public CudaException(string message) : base(message) { }
            public CudaException()
            {
            }
            public CudaException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}