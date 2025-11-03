using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Graphs
{
    /// <summary>
    /// Production-grade CUDA graph optimization manager with automatic graph capture,
    /// instantiation, and performance analysis.
    /// </summary>
    public sealed partial class CudaGraphOptimizationManager : IDisposable
    {
        private readonly ILogger<CudaGraphOptimizationManager> _logger;
        private readonly ConcurrentDictionary<string, GraphInstance> _graphs;
        private readonly ConcurrentDictionary<string, GraphStatistics> _statistics;
        private readonly SemaphoreSlim _graphCreationLock;
        private readonly Timer _optimizationTimer;
        private bool _disposed;

        // CUDA API imports
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphCreate(out IntPtr graph, uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphInstantiate(
            out IntPtr graphExec, IntPtr graph, IntPtr phErrorNode, IntPtr logBuffer, ulong bufferSize);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphLaunch(IntPtr graphExec, IntPtr stream);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphDestroy(IntPtr graph);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphExecDestroy(IntPtr graphExec);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaStreamBeginCapture(IntPtr stream, CudaGraphCaptureMode mode);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaStreamEndCapture(IntPtr stream, out IntPtr graph);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphExecUpdate(
            IntPtr hGraphExec, IntPtr hGraph, IntPtr hErrorNode, out CudaGraphExecUpdateResult updateResult);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphGetNodes(
            IntPtr graph, IntPtr nodes, out ulong numNodes);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphGetEdges(
            IntPtr graph, IntPtr from, IntPtr to, out ulong numEdges);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments - UTF-8 marshaling is explicitly specified
        private static partial CudaError cudaGraphDebugDotPrint(
            IntPtr graph,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
            uint flags);
#pragma warning restore CA2101

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphClone(out IntPtr clonedGraph, IntPtr originalGraph);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphNodeGetType(
            IntPtr node, out CudaGraphNodeType type);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [LibraryImport("cudart64_12")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        private static partial CudaError cudaGraphExecGetFlags(
            IntPtr graphExec, out ulong flags);
        /// <summary>
        /// Initializes a new instance of the CudaGraphOptimizationManager class.
        /// </summary>
        /// <param name="logger">The logger.</param>

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

            LogManagerInitialized();
        }

        /// <summary>
        /// Captures a sequence of CUDA operations into a graph for optimized execution.
        /// </summary>
        internal async Task<string> CaptureGraphAsync(
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
                LogBeginningGraphCapture(graphName);

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
                    LogGraphCaptureError(ex);
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

                LogGraphCaptureSuccess(graphName, analysis.NodeCount, analysis.EdgeCount, captureElapsed.TotalMilliseconds);

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


            LogGraphLaunched(graphName, launchElapsed.TotalMilliseconds, stats.AverageLaunchTime.TotalMilliseconds);

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
                LogUpdatingGraph(graphName);

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
                    LogGraphUpdateSuccess(graphName);
                    return true;
                }
                else
                {
                    // Update failed - need to recreate executable
                    LogUpdateFailed(graphName, updateResult.ToString());

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

                LogGraphCloned(sourceGraphName, targetName);

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
            if (error is not CudaError.Success and not CudaError.InvalidValue)
            {
                LogGetNodeCountFailed(error.ToString());
                return analysis;
            }
            analysis.NodeCount = (int)nodeCount;

            // Get edge count
            error = cudaGraphGetEdges(graph, IntPtr.Zero, IntPtr.Zero, out var edgeCount);
            if (error is not CudaError.Success and not CudaError.InvalidValue)
            {
                LogGetEdgeCountFailed(error.ToString());
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
                    LogGraphVisualizationExported(dotFile);
                }
                else
                {
                    LogGraphVisualizationFailed(error.ToString());
                }
            }
            catch (Exception ex)
            {
                LogGraphVisualizationError(ex);
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
                        LogPerformanceDegradation(name, stats.LastLaunchTime.TotalMilliseconds, stats.AverageLaunchTime.TotalMilliseconds);
                    }

                    // Log high failure rate
                    if (stats.LaunchCount > 0)
                    {
                        var failureRate = (double)stats.FailureCount / (stats.LaunchCount + stats.FailureCount);
                        if (failureRate > 0.1)
                        {
                            LogHighFailureRate(name, failureRate);
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


                        LogUnusedGraphRemoved(name, instance.LastUsedAt?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "never");
                    }
                }

                // Log summary
                LogOptimizationComplete(_graphs.Count, graphsToRemove.Count);
            }
            catch (Exception ex)
            {
                LogGraphOptimizationError(ex);
            }
        }

        /// <summary>
        /// Gets performance statistics for a graph.
        /// </summary>
        internal GraphStatistics? GetStatistics(string graphName) => _statistics.TryGetValue(graphName, out var stats) ? stats : null;

        /// <summary>
        /// Gets all graph statistics.
        /// </summary>
        internal IReadOnlyDictionary<string, GraphStatistics> GetAllStatistics() => _statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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

                LogGraphRemoved(graphName);
                return true;
            }

            return false;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

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
                    LogGraphDisposeError(ex);
                }
            }

            _graphs.Clear();
            _statistics.Clear();
            _disposed = true;
        }

        // Supporting classes
        private class GraphInstance
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>The name.</value>
            public required string Name { get; init; }
            /// <summary>
            /// Gets or sets the graph.
            /// </summary>
            /// <value>The graph.</value>
            public required IntPtr Graph { get; set; }
            /// <summary>
            /// Gets or sets the graph exec.
            /// </summary>
            /// <value>The graph exec.</value>
            public required IntPtr GraphExec { get; set; }
            /// <summary>
            /// Gets or sets the created at.
            /// </summary>
            /// <value>The created at.</value>
            public required DateTimeOffset CreatedAt { get; init; }
            /// <summary>
            /// Gets or sets the last updated at.
            /// </summary>
            /// <value>The last updated at.</value>
            public DateTimeOffset? LastUpdatedAt { get; set; }
            /// <summary>
            /// Gets or sets the last used at.
            /// </summary>
            /// <value>The last used at.</value>
            public DateTimeOffset? LastUsedAt { get; set; }
            /// <summary>
            /// Gets or sets the node count.
            /// </summary>
            /// <value>The node count.</value>
            public required int NodeCount { get; init; }
            /// <summary>
            /// Gets or sets the edge count.
            /// </summary>
            /// <value>The edge count.</value>
            public required int EdgeCount { get; init; }
            /// <summary>
            /// Gets or sets the options.
            /// </summary>
            /// <value>The options.</value>
            public required GraphCaptureOptions Options { get; init; }
            /// <summary>
            /// Gets or sets the capture time.
            /// </summary>
            /// <value>The capture time.</value>
            public TimeSpan CaptureTime { get; init; }
            /// <summary>
            /// Gets or sets the cloned from.
            /// </summary>
            /// <value>The cloned from.</value>
            public string? ClonedFrom { get; init; }
        }
        /// <summary>
        /// A class that represents graph statistics.
        /// </summary>

        internal class GraphStatistics
        {
            /// <summary>
            /// Gets or sets the graph name.
            /// </summary>
            /// <value>The graph name.</value>
            public required string GraphName { get; init; }
            /// <summary>
            /// Gets or sets the created at.
            /// </summary>
            /// <value>The created at.</value>
            public required DateTimeOffset CreatedAt { get; init; }
            /// <summary>
            /// Gets or sets the last used at.
            /// </summary>
            /// <value>The last used at.</value>
            public DateTimeOffset? LastUsedAt { get; set; }
            /// <summary>
            /// Gets or sets the launch count.
            /// </summary>
            /// <value>The launch count.</value>
            public int LaunchCount { get; set; }
            /// <summary>
            /// Gets or sets the failure count.
            /// </summary>
            /// <value>The failure count.</value>
            public int FailureCount { get; set; }
            /// <summary>
            /// Gets or sets the update count.
            /// </summary>
            /// <value>The update count.</value>
            public int UpdateCount { get; set; }
            /// <summary>
            /// Gets or sets the recreate count.
            /// </summary>
            /// <value>The recreate count.</value>
            public int RecreateCount { get; set; }
            /// <summary>
            /// Gets or sets the total launch time.
            /// </summary>
            /// <value>The total launch time.</value>
            public TimeSpan TotalLaunchTime { get; set; }
            /// <summary>
            /// Gets or sets the last launch time.
            /// </summary>
            /// <value>The last launch time.</value>
            public TimeSpan LastLaunchTime { get; set; }
            /// <summary>
            /// Gets or sets the min launch time.
            /// </summary>
            /// <value>The min launch time.</value>
            public TimeSpan MinLaunchTime { get; set; }
            /// <summary>
            /// Gets or sets the max launch time.
            /// </summary>
            /// <value>The max launch time.</value>
            public TimeSpan MaxLaunchTime { get; set; }
            /// <summary>
            /// Gets or sets the node count.
            /// </summary>
            /// <value>The node count.</value>
            public required int NodeCount { get; init; }
            /// <summary>
            /// Gets or sets the edge count.
            /// </summary>
            /// <value>The edge count.</value>
            public required int EdgeCount { get; init; }
            /// <summary>
            /// Gets or sets the average launch time.
            /// </summary>
            /// <value>The average launch time.</value>


            public TimeSpan AverageLaunchTime
                => LaunchCount > 0 ? TotalLaunchTime / LaunchCount : TimeSpan.Zero;
        }

        private class GraphAnalysis
        {
            /// <summary>
            /// Gets or sets the node count.
            /// </summary>
            /// <value>The node count.</value>
            public int NodeCount { get; set; }
            /// <summary>
            /// Gets or sets the edge count.
            /// </summary>
            /// <value>The edge count.</value>
            public int EdgeCount { get; set; }
            /// <summary>
            /// Gets or sets the node types.
            /// </summary>
            /// <value>The node types.</value>
            public Dictionary<CudaGraphNodeType, int> NodeTypes { get; } = [];
        }
        /// <summary>
        /// A class that represents graph capture options.
        /// </summary>

        internal class GraphCaptureOptions
        {
            /// <summary>
            /// Gets or sets the allow invalidation.
            /// </summary>
            /// <value>The allow invalidation.</value>
            public bool AllowInvalidation { get; set; } = true;
            /// <summary>
            /// Gets or sets the export debug visualization.
            /// </summary>
            /// <value>The export debug visualization.</value>
            public bool ExportDebugVisualization { get; set; }
            /// <summary>
            /// Gets or sets the max node count.
            /// </summary>
            /// <value>The max node count.</value>

            public int MaxNodeCount { get; set; } = 10000;
            /// <summary>
            /// Gets or sets the default.
            /// </summary>
            /// <value>The default.</value>


            public static GraphCaptureOptions Default => new();
        }
        /// <summary>
        /// An cuda error enumeration.
        /// </summary>

        // CUDA enums
        private enum CudaError
        {
            Success = 0,
            InvalidValue = 1,
            // Add other error codes as needed
        }
        /// <summary>
        /// An cuda graph capture mode enumeration.
        /// </summary>

        private enum CudaGraphCaptureMode
        {
            Global = 0,
            ThreadLocal = 1,
            Relaxed = 2
        }
        /// <summary>
        /// An cuda graph node type enumeration.
        /// </summary>

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
        /// <summary>
        /// An cuda graph exec update result enumeration.
        /// </summary>

        private enum CudaGraphExecUpdateResult
        {
            Success = 0,
            ErrorTopologyChanged = 1,
            ErrorNodeTypeChanged = 2,
            // Add other results as needed
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064:Exceptions should be public",
            Justification = "Internal exception type used only within CudaGraphOptimizationManager for graph optimization errors")]
        private class CudaException : Exception
        {
            /// <summary>
            /// Initializes a new instance of the CudaException class.
            /// </summary>
            /// <param name="message">The message.</param>
            public CudaException(string message) : base(message) { }
            /// <summary>
            /// Initializes a new instance of the CudaException class.
            /// </summary>
            public CudaException()
            {
            }
            /// <summary>
            /// Initializes a new instance of the CudaException class.
            /// </summary>
            /// <param name="message">The message.</param>
            /// <param name="innerException">The inner exception.</param>
            public CudaException(string message, Exception innerException) : base(message, innerException)
            {
            }
        }
    }
}
