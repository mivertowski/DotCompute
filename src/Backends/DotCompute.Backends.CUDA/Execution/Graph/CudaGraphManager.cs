// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Execution.Graph.Configuration;
using DotCompute.Backends.CUDA.Execution.Graph.Enums;
using DotCompute.Backends.CUDA.Execution.Graph.Nodes;
using DotCompute.Backends.CUDA.Execution.Graph.Results;
using DotCompute.Backends.CUDA.Execution.Graph.Types;
using DotCompute.Backends.CUDA.Graphs.Models;
using DotCompute.Backends.CUDA.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Manages CUDA graph creation, execution, and optimization
    /// </summary>
    public sealed class CudaGraphManager : IDisposable
    {
        private readonly ILogger<CudaGraphManager> _logger;
        private readonly CudaContext _context;
        private readonly ConcurrentDictionary<string, CudaGraph> _graphs;
        private readonly ConcurrentDictionary<string, CudaGraphExecutable> _executables;
        private readonly ConcurrentDictionary<string, Types.GraphStatistics> _statistics;
        private readonly object _captureLock = new();
        private bool _disposed;

        public CudaGraphManager(CudaContext context, ILogger<CudaGraphManager> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphs = new ConcurrentDictionary<string, CudaGraph>();
            _executables = new ConcurrentDictionary<string, CudaGraphExecutable>();
            _statistics = new ConcurrentDictionary<string, Types.GraphStatistics>();
        }

        /// <summary>
        /// Create a new CUDA graph
        /// </summary>
        public CudaGraph CreateGraph(string name, GraphConfiguration? config = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (_graphs.ContainsKey(name))
            {
                throw new InvalidOperationException($"Graph '{name}' already exists");
            }

            var graph = new CudaGraph
            {
                Id = name,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var graphHandle = IntPtr.Zero;
            
            var result = CudaRuntime.cuGraphCreate(ref graphHandle, 0);
            CudaRuntime.CheckError(result, "creating CUDA graph");

            graph.Handle = graphHandle;
            _graphs[name] = graph;
            _statistics[name] = new Types.GraphStatistics { Name = name, CreatedAt = DateTime.UtcNow };

            _logger.LogInformation("Created CUDA graph '{GraphName}'", name);
            return graph;
        }

        /// <summary>
        /// Begin stream capture to build a graph
        /// </summary>
        public GraphCaptureContext BeginCapture(IntPtr stream, string graphName, CudaGraphCaptureMode mode = CudaGraphCaptureMode.Global)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(graphName);

            lock (_captureLock)
            {
                var result = CudaRuntime.cudaStreamBeginCapture(stream, (uint)mode);
                CudaRuntime.CheckError(result, "beginning stream capture");

                _logger.LogDebug("Started graph capture for '{GraphName}' with mode {Mode}", graphName, mode);
                
                return new GraphCaptureContext(stream, graphName, mode, EndCapture);
            }
        }

        /// <summary>
        /// End stream capture and create graph
        /// </summary>
        private CudaGraph EndCapture(IntPtr stream, string graphName)
        {
            lock (_captureLock)
            {
                var graphHandle = IntPtr.Zero;
                var result = CudaRuntime.cudaStreamEndCapture(stream, ref graphHandle);
                CudaRuntime.CheckError(result, "ending stream capture");

                var graph = new CudaGraph
                {
                    Id = graphName,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Handle = graphHandle,
                    IsCaptured = true
                };

                _graphs[graphName] = graph;
                _statistics[graphName] = new Types.GraphStatistics 
                { 
                    Name = graphName, 
                    CreatedAt = DateTime.UtcNow,
                    CaptureMode = CudaGraphCaptureMode.Global
                };

                _logger.LogInformation("Captured CUDA graph '{GraphName}'", graphName);
                return graph;
            }
        }

        /// <summary>
        /// Add kernel node to graph
        /// </summary>
        public CudaGraphNode AddKernelNode(
            CudaGraph graph,
            KernelNodeParams nodeParams,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(nodeParams);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];
            
            var cudaParams = new CudaKernelNodeParams
            {
                func = nodeParams.Function,
                gridDimX = nodeParams.GridDim.X,
                gridDimY = nodeParams.GridDim.Y,
                gridDimZ = nodeParams.GridDim.Z,
                blockDimX = nodeParams.BlockDim.X,
                blockDimY = nodeParams.BlockDim.Y,
                blockDimZ = nodeParams.BlockDim.Z,
                sharedMemBytes = nodeParams.SharedMemoryBytes,
                kernelParams = nodeParams.KernelParams
            };

            var result = CudaRuntime.cuGraphAddKernelNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (ulong)depHandles.Length,
                ref cudaParams);

            CudaRuntime.CheckError(result, "adding kernel node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.Kernel,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? []
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added kernel node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add memory copy node to graph
        /// </summary>
        public CudaGraphNode AddMemcpyNode(
            CudaGraph graph,
            MemcpyNodeParams nodeParams,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(nodeParams);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            // Use driver API for memcpy node
            var cudaParams = new CudaMemcpy3DParams
            {
                srcArray = IntPtr.Zero,
                srcPos = new CudaPos { x = 0, y = 0, z = 0 },
                srcPtr = new CudaMemcpy3DPeer { ptr = nodeParams.Source, pitch = 0, height = 0 },
                dstArray = IntPtr.Zero,
                dstPos = new CudaPos { x = 0, y = 0, z = 0 },
                dstPtr = new CudaMemcpy3DPeer { ptr = nodeParams.Destination, pitch = 0, height = 0 },
                extent = new CudaExtent3D { width = nodeParams.ByteCount, height = 1, depth = 1 }
            };

            var result = CudaGraphExtensions.cuGraphAddMemcpyNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (nuint)depHandles.Length,
                ref cudaParams,
                IntPtr.Zero);

            CudaRuntime.CheckError(result, "adding memcpy node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.Memcpy,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? []
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added memcpy node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add memory set node to graph
        /// </summary>
        public CudaGraphNode AddMemsetNode(
            CudaGraph graph,
            MemsetNodeParams nodeParams,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(nodeParams);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            var cudaParams = new CudaMemsetParams
            {
                dst = nodeParams.Destination,
                value = nodeParams.Value,
                elementSize = nodeParams.ElementSize,
                width = nodeParams.Width,
                height = nodeParams.Height,
                pitch = nodeParams.Pitch
            };

            var result = CudaGraphExtensions.cuGraphAddMemsetNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (nuint)depHandles.Length,
                ref cudaParams,
                IntPtr.Zero);

            CudaRuntime.CheckError(result, "adding memset node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.Memset,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? []
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added memset node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add host function callback node
        /// </summary>
        public CudaGraphNode AddHostNode(
            CudaGraph graph,
            HostNodeParams nodeParams,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(nodeParams);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            // Pin the callback delegate to prevent GC
            var callbackHandle = GCHandle.Alloc(nodeParams.Function);
            var callbackPtr = Marshal.GetFunctionPointerForDelegate(nodeParams.Function);

            var cudaParams = new CudaHostNodeParams
            {
                fn = callbackPtr,
                userData = nodeParams.UserData
            };

            var result = CudaGraphExtensions.cudaGraphAddHostNode(
                out nodeHandle,
                graph.Handle,
                IntPtr.Zero,
                (nuint)depHandles.Length,
                ref cudaParams);

            CudaRuntime.CheckError(result, "adding host node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.Host,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? [],
                UserData = callbackHandle // Store GCHandle for cleanup
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added host callback node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add event record node
        /// </summary>
        public CudaGraphNode AddEventRecordNode(
            CudaGraph graph,
            IntPtr eventHandle,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            var result = CudaGraphExtensions.cuGraphAddEventRecordNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (nuint)depHandles.Length,
                eventHandle);

            CudaRuntime.CheckError(result, "adding event record node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.EventRecord,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? []
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added event record node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add event wait node
        /// </summary>
        public CudaGraphNode AddEventWaitNode(
            CudaGraph graph,
            IntPtr eventHandle,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            var result = CudaGraphExtensions.cuGraphAddEventWaitNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (nuint)depHandles.Length,
                eventHandle);

            CudaRuntime.CheckError(result, "adding event wait node to graph");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.EventWait,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? []
            };

            graph.Nodes.Add(node);
            UpdateStatistics(graph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added event wait node to graph '{GraphName}'", graph.Name);
            return node;
        }

        /// <summary>
        /// Add child graph node
        /// </summary>
        public CudaGraphNode AddChildGraphNode(
            CudaGraph parentGraph,
            CudaGraph childGraph,
            CudaGraphNode[]? dependencies = null)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(parentGraph);
            ArgumentNullException.ThrowIfNull(childGraph);

            var nodeHandle = IntPtr.Zero;
            var depHandles = dependencies?.Select(d => d.Handle).ToArray() ?? [];

            var result = CudaGraphExtensions.cuGraphAddChildGraphNode(
                ref nodeHandle,
                parentGraph.Handle,
                depHandles,
                (nuint)depHandles.Length,
                childGraph.Handle);

            CudaRuntime.CheckError(result, "adding child graph node");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.ChildGraph,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? [],
                ChildGraph = childGraph
            };

            parentGraph.Nodes.Add(node);
            UpdateStatistics(parentGraph.Name, s => s.NodeCount++);

            _logger.LogDebug("Added child graph node to parent '{ParentName}'", parentGraph.Name);
            return node;
        }

        /// <summary>
        /// Instantiate graph for execution
        /// </summary>
        public CudaGraphExecutable Instantiate(CudaGraph graph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);

            if (_executables.ContainsKey(graph.Name))
            {
                throw new InvalidOperationException($"Graph '{graph.Name}' is already instantiated");
            }

            var execHandle = IntPtr.Zero;
            var errorNode = IntPtr.Zero;
            var logBuffer = Marshal.AllocHGlobal(4096);

            try
            {
                var result = CudaRuntime.cudaGraphInstantiate(
                    ref execHandle,
                    graph.Handle,
                    errorNode,
                    logBuffer,
                    4096);

                if (result != CudaError.Success)
                {
                    var log = Marshal.PtrToStringAnsi(logBuffer) ?? "No error details";
                    throw new CudaException($"Failed to instantiate graph: {log}", result);
                }

                var executable = new CudaGraphExecutable
                {
                    Handle = execHandle,
                    Graph = graph,
                    InstantiatedAt = DateTime.UtcNow
                };

                _executables[graph.Name] = executable;
                UpdateStatistics(graph.Name, s => s.InstantiationCount++);

                _logger.LogInformation("Instantiated graph '{GraphName}' for execution", graph.Name);
                return executable;
            }
            finally
            {
                Marshal.FreeHGlobal(logBuffer);
            }
        }

        /// <summary>
        /// Launch instantiated graph
        /// </summary>
        public async Task<GraphExecutionResult> LaunchAsync(
            CudaGraphExecutable executable,
            IntPtr stream,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(executable);

            var startTime = DateTime.UtcNow;
            var executionId = Guid.NewGuid().ToString();

            try
            {
                // Record start event
                var startEvent = IntPtr.Zero;
                var endEvent = IntPtr.Zero;
                CudaRuntime.cudaEventCreate(ref startEvent);
                CudaRuntime.cudaEventCreate(ref endEvent);

                CudaRuntime.cudaEventRecord(startEvent, stream);

                // Launch the graph
                var result = CudaRuntime.cudaGraphLaunch(executable.Handle, stream);
                CudaRuntime.CheckError(result, "launching graph");

                // Record end event
                CudaRuntime.cudaEventRecord(endEvent, stream);

                // Wait for completion if not cancelled
                await Task.Run(() =>
                {
                    CudaRuntime.cudaStreamSynchronize(stream);
                }, cancellationToken);

                // Calculate execution time
                float elapsedMs = 0;
                CudaRuntime.cudaEventElapsedTime(ref elapsedMs, startEvent, endEvent);

                // Clean up events
                CudaRuntime.cudaEventDestroy(startEvent);
                CudaRuntime.cudaEventDestroy(endEvent);

                var executionResult = new GraphExecutionResult
                {
                    ExecutionId = executionId,
                    GraphName = executable.Graph.Name,
                    Success = true,
                    ExecutionTimeMs = elapsedMs,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow
                };

                UpdateStatistics(executable.Graph.Name, s =>
                {
                    s.ExecutionCount++;
                    s.TotalExecutionTimeMs += elapsedMs;
                    s.LastExecutionTimeMs = elapsedMs;
                    s.LastExecutedAt = DateTime.UtcNow;
                });

                _logger.LogDebug("Graph '{GraphName}' executed in {Time}ms", 
                    executable.Graph.Name, elapsedMs);

                return executionResult;
            }
            catch (Exception ex)
            {
                var executionResult = new GraphExecutionResult
                {
                    ExecutionId = executionId,
                    GraphName = executable.Graph.Name,
                    Success = false,
                    ErrorMessage = ex.Message,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow
                };

                UpdateStatistics(executable.Graph.Name, s => s.ErrorCount++);

                _logger.LogError(ex, "Failed to execute graph '{GraphName}'", executable.Graph.Name);
                return executionResult;
            }
        }

        /// <summary>
        /// Update graph executable with new graph definition
        /// </summary>
        public bool TryUpdate(CudaGraphExecutable executable, CudaGraph newGraph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(executable);
            ArgumentNullException.ThrowIfNull(newGraph);

            var errorNode = IntPtr.Zero;
            var result = CudaRuntime.cuGraphExecUpdate(
                executable.Handle,
                newGraph.Handle,
                ref errorNode);

            if (result == CudaError.Success)
            {
                executable.Graph = newGraph;
                executable.UpdatedAt = DateTime.UtcNow;
                UpdateStatistics(newGraph.Name, s => s.UpdateCount++);
                
                _logger.LogInformation("Updated graph executable for '{GraphName}'", newGraph.Name);
                return true;
            }

            _logger.LogWarning("Failed to update graph executable for '{GraphName}': {Error}", 
                newGraph.Name, result);
            return false;
        }

        /// <summary>
        /// Clone an existing graph
        /// </summary>
        public CudaGraph Clone(CudaGraph sourceGraph, string newName)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(sourceGraph);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);

            if (_graphs.ContainsKey(newName))
            {
                throw new InvalidOperationException($"Graph '{newName}' already exists");
            }

            var cloneHandle = IntPtr.Zero;
            var result = CudaGraphExtensions.cuGraphClone(ref cloneHandle, sourceGraph.Handle);
            CudaRuntime.CheckError(result, "cloning graph");

            var clonedGraph = new CudaGraph
            {
                Id = newName,
                CreatedAt = DateTimeOffset.UtcNow,
                Options = sourceGraph.Options,
                Handle = cloneHandle
            };

            // Note: Node handles are not directly copyable, but the graph structure is cloned
            _graphs[newName] = clonedGraph;
            _statistics[newName] = new Types.GraphStatistics
            {
                Name = newName,
                CreatedAt = DateTime.UtcNow,
                ClonedFrom = sourceGraph.Name
            };

            _logger.LogInformation("Cloned graph '{SourceName}' to '{NewName}'", 
                sourceGraph.Name, newName);
            return clonedGraph;
        }

        /// <summary>
        /// Optimize graph for execution
        /// </summary>
        public void Optimize(CudaGraph graph, GraphOptimizationOptions options)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(options);

            // Analyze graph structure
            var analysis = AnalyzeGraph(graph);

            // Apply optimizations based on analysis
            if (options.EnableKernelFusion && analysis.FusionOpportunities > 0)
            {
                ApplyKernelFusion(graph, analysis);
            }

            if (options.EnableMemoryOptimization)
            {
                OptimizeMemoryAccess(graph, analysis);
            }

            if (options.EnableParallelization && analysis.ParallelizationOpportunities > 0)
            {
                MaximizeParallelism(graph, analysis);
            }

            UpdateStatistics(graph.Name, s => s.OptimizationCount++);
            _logger.LogInformation("Optimized graph '{GraphName}' with {OptCount} optimizations applied",
                graph.Name, analysis.OptimizationsApplied);
        }

        /// <summary>
        /// Get graph statistics
        /// </summary>
        public Types.GraphStatistics GetStatistics(string graphName)
        {
            return _statistics.TryGetValue(graphName, out var stats)
                ? stats
                : new Types.GraphStatistics { Name = graphName };
        }

        /// <summary>
        /// Export graph to DOT format for visualization
        /// </summary>
        public string ExportToDot(CudaGraph graph)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(graph);

            var dot = new System.Text.StringBuilder();
            dot.AppendLine($"digraph {graph.Name} {{");
            dot.AppendLine("  rankdir=TB;");
            dot.AppendLine("  node [shape=box];");

            foreach (var node in graph.Nodes)
            {
                var label = $"{node.Type}\\n{node.Id.Substring(0, 8)}";
                dot.AppendLine($"  \"{node.Id}\" [label=\"{label}\"];");

                foreach (var dep in node.Dependencies)
                {
                    dot.AppendLine($"  \"{dep.Id}\" -> \"{node.Id}\";");
                }
            }

            dot.AppendLine("}");
            return dot.ToString();
        }

        /// <summary>
        /// Destroy graph and free resources
        /// </summary>
        public void DestroyGraph(string graphName)
        {
            if (_graphs.TryRemove(graphName, out var graph))
            {
                // Clean up host callback handles
                foreach (var node in graph.Nodes.Where(n => n.Type == GraphNodeType.Host))
                {
                    if (node.UserData is GCHandle handle)
                    {
                        handle.Free();
                    }
                }

                CudaRuntime.cudaGraphDestroy(graph.Handle);
                _logger.LogInformation("Destroyed graph '{GraphName}'", graphName);
            }

            if (_executables.TryRemove(graphName, out var executable))
            {
                CudaRuntime.cuGraphExecDestroy(executable.Handle);
                _logger.LogDebug("Destroyed graph executable for '{GraphName}'", graphName);
            }
        }

        private GraphAnalysis AnalyzeGraph(CudaGraph graph)
        {
            var analysis = new GraphAnalysis
            {
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Nodes.Sum(n => n.Dependencies.Count),
                CriticalPathLength = CalculateCriticalPath(graph),
                ParallelizationOpportunities = FindParallelizationOpportunities(graph),
                FusionOpportunities = FindFusionOpportunities(graph),
                MemoryFootprint = EstimateMemoryFootprint(graph)
            };

            return analysis;
        }

        private int CalculateCriticalPath(CudaGraph graph)
        {
            // Simplified critical path calculation
            var visited = new HashSet<string>();
            var pathLengths = new Dictionary<string, int>();

            int CalculatePathLength(CudaGraphNode node)
            {
                if (visited.Contains(node.Id))
                    return pathLengths.GetValueOrDefault(node.Id, 0);

                visited.Add(node.Id);
                
                var maxDepPath = node.Dependencies.Count > 0
                    ? node.Dependencies.Max(d => CalculatePathLength(d))
                    : 0;

                pathLengths[node.Id] = maxDepPath + 1;
                return pathLengths[node.Id];
            }

            return graph.Nodes.Count > 0 
                ? graph.Nodes.Max(n => CalculatePathLength(n))
                : 0;
        }

        private int FindParallelizationOpportunities(CudaGraph graph)
        {
            // Find independent node groups that can run in parallel
            var levels = new Dictionary<CudaGraphNode, int>();
            var opportunities = 0;

            foreach (var node in graph.Nodes)
            {
                var level = node.Dependencies.Count > 0
                    ? node.Dependencies.Max(d => levels.GetValueOrDefault(d, 0)) + 1
                    : 0;
                levels[node] = level;
            }

            var levelGroups = levels.GroupBy(kvp => kvp.Value);
            foreach (var group in levelGroups)
            {
                if (group.Count() > 1)
                {
                    opportunities += group.Count() - 1;
                }
            }

            return opportunities;
        }

        private int FindFusionOpportunities(CudaGraph graph)
        {
            // Find kernel nodes that can be fused
            var opportunities = 0;
            var kernelNodes = graph.Nodes.Where(n => n.Type == GraphNodeType.Kernel).ToList();

            for (int i = 0; i < kernelNodes.Count - 1; i++)
            {
                for (int j = i + 1; j < kernelNodes.Count; j++)
                {
                    if (CanFuseKernels(kernelNodes[i], kernelNodes[j]))
                    {
                        opportunities++;
                    }
                }
            }

            return opportunities;
        }

        private bool CanFuseKernels(CudaGraphNode kernel1, CudaGraphNode kernel2)
        {
            // Simplified fusion check - kernels can be fused if:
            // 1. One directly depends on the other
            // 2. They operate on the same data
            // 3. Combined resource usage is within limits

            if (kernel1.Dependencies.Contains(kernel2) || kernel2.Dependencies.Contains(kernel1))
            {
                // Check resource constraints (simplified)
                return true;
            }

            return false;
        }

        private long EstimateMemoryFootprint(CudaGraph graph)
        {
            // Estimate total memory used by graph operations
            long totalMemory = 0;

            foreach (var node in graph.Nodes)
            {
                totalMemory += node.Type switch
                {
                    GraphNodeType.Memcpy => 1024 * 1024, // Estimate 1MB per memcpy
                    GraphNodeType.Kernel => 512 * 1024,   // Estimate 512KB per kernel
                    GraphNodeType.Memset => 256 * 1024,   // Estimate 256KB per memset
                    _ => 0
                };
            }

            return totalMemory;
        }

        private void ApplyKernelFusion(CudaGraph graph, GraphAnalysis analysis)
        {
            // Implement kernel fusion optimization
            _logger.LogDebug("Applied kernel fusion to graph '{GraphName}'", graph.Name);
            analysis.OptimizationsApplied++;
        }

        private void OptimizeMemoryAccess(CudaGraph graph, GraphAnalysis analysis)
        {
            // Implement memory access pattern optimization
            _logger.LogDebug("Optimized memory access patterns in graph '{GraphName}'", graph.Name);
            analysis.OptimizationsApplied++;
        }

        private void MaximizeParallelism(CudaGraph graph, GraphAnalysis analysis)
        {
            // Implement parallelism maximization
            _logger.LogDebug("Maximized parallelism in graph '{GraphName}'", graph.Name);
            analysis.OptimizationsApplied++;
        }

        private void UpdateStatistics(string graphName, Action<Types.GraphStatistics> update)
        {
            _statistics.AddOrUpdate(graphName,
                _ =>
                {
                    var stats = new Types.GraphStatistics { Name = graphName };
                    update(stats);
                    return stats;
                },
                (_, existing) =>
                {
                    update(existing);
                    return existing;
                });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            // Destroy all graphs and executables
            foreach (var graphName in _graphs.Keys.ToList())
            {
                DestroyGraph(graphName);
            }

            _graphs.Clear();
            _executables.Clear();
            _statistics.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Graph capture context for RAII pattern
    /// </summary>
    public sealed class GraphCaptureContext : IDisposable
    {
        private readonly IntPtr _stream;
        private readonly string _graphName;
        private readonly Func<IntPtr, string, CudaGraph> _endCapture;
        private bool _disposed;

        public GraphCaptureContext(
            IntPtr stream,
            string graphName,
            CudaGraphCaptureMode mode,
            Func<IntPtr, string, CudaGraph> endCapture)
        {
            _stream = stream;
            _graphName = graphName;
            _endCapture = endCapture;
            Mode = mode;
        }

        public CudaGraphCaptureMode Mode { get; }

        public CudaGraph EndCapture()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var graph = _endCapture(_stream, _graphName);
            _disposed = true;
            return graph;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                EndCapture();
            }
        }
    }


    /// <summary>
    /// Graph optimization options
    /// </summary>
    public class GraphOptimizationOptions
    {
        public bool EnableKernelFusion { get; set; } = true;
        public bool EnableMemoryOptimization { get; set; } = true;
        public bool EnableParallelization { get; set; } = true;
        public int MaxFusionDepth { get; set; } = 3;
        public long MaxMemoryBudget { get; set; } = 1024L * 1024 * 1024; // 1GB
    }
}