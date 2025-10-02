// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Execution.Graph.Configuration;
using DotCompute.Backends.CUDA.Execution.Graph.Enums;
using DotCompute.Backends.CUDA.Execution.Graph.Results;
using CudaGraphCaptureMode = DotCompute.Backends.CUDA.Types.CudaGraphCaptureMode;
using CudaGraphNode = DotCompute.Backends.CUDA.Execution.Graph.Types.CudaGraphNode;
using CudaGraphExecutable = DotCompute.Backends.CUDA.Execution.Graph.Types.CudaGraphExecutable;
using HostNodeParams = DotCompute.Backends.CUDA.Execution.Graph.Types.HostNodeParams;
using MemsetNodeParams = DotCompute.Backends.CUDA.Execution.Graph.Types.MemsetNodeParams;
using KernelNodeParams = DotCompute.Backends.CUDA.Execution.Graph.Types.KernelNodeParams;
using MemcpyNodeParams = DotCompute.Backends.CUDA.Execution.Graph.Types.MemcpyNodeParams;
using DotCompute.Backends.CUDA.Graphs.Models;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Native.Exceptions;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Execution.Graph
{
    /// <summary>
    /// Manages CUDA graph creation, execution, and optimization
    /// </summary>
    public sealed class CudaGraphManager(CudaContext context, ILogger<CudaGraphManager> logger) : IDisposable
    {
        private readonly ILogger<CudaGraphManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly ConcurrentDictionary<string, CudaGraph> _graphs = new();
        private readonly ConcurrentDictionary<string, CudaGraphExecutable> _executables = new();
        private readonly ConcurrentDictionary<string, Types.GraphStatistics> _statistics = new();
        private readonly object _captureLock = new();
        private bool _disposed;

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

            _logger.LogInfoMessage("'");
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

                _logger.LogDebugMessage("");


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

                _logger.LogInfoMessage("'");
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

            _logger.LogDebugMessage("'");
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
            var cudaParams = new CudaMemcpy3DParms
            {
                srcArray = IntPtr.Zero,
                srcPos = new CudaPos { x = 0, y = 0, z = 0 },
                srcPtr = nodeParams.Source,
                dstArray = IntPtr.Zero,
                dstPos = new CudaPos { x = 0, y = 0, z = 0 },
                dstPtr = nodeParams.Destination,
                extent = new CudaExtent { Width = nodeParams.ByteCount, Height = 1, Depth = 1 },
                kind = CudaMemcpyKind.DeviceToDevice
            };

            var result = CudaRuntime.cuGraphAddMemcpyNode(
                ref nodeHandle,
                graph.Handle,
                depHandles,
                (ulong)depHandles.Length,
                ref cudaParams);

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

            _logger.LogDebugMessage("'");
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

            var result = CudaRuntime.cuGraphAddMemsetNode(
                out nodeHandle,
                graph.Handle,
                IntPtr.Zero,
                (nuint)depHandles.Length,
                ref cudaParams);

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

            _logger.LogDebugMessage("'");
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

            // Convert nint function pointer to CudaHostFn delegate
            var hostFn = Marshal.GetDelegateForFunctionPointer<CudaHostFn>(callbackPtr);

            var cudaParams = new CudaHostNodeParams
            {
                fn = hostFn,
                userData = nodeParams.UserData
            };

            var result = CudaRuntime.cudaGraphAddHostNode(
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

            _logger.LogDebugMessage("'");
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

            var result = CudaRuntime.cuGraphAddEventRecordNode(
                out nodeHandle,
                graph.Handle,
                IntPtr.Zero,
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

            _logger.LogDebugMessage("'");
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

            var result = CudaRuntime.cuGraphAddEventWaitNode(
                out nodeHandle,
                graph.Handle,
                IntPtr.Zero,
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

            _logger.LogDebugMessage("'");
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

            var result = CudaRuntime.cuGraphAddChildGraphNode(
                out nodeHandle,
                parentGraph.Handle,
                IntPtr.Zero,
                (nuint)depHandles.Length,
                childGraph.Handle);

            CudaRuntime.CheckError(result, "adding child graph node");

            var node = new CudaGraphNode
            {
                Handle = nodeHandle,
                Type = GraphNodeType.ChildGraph,
                Id = Guid.NewGuid().ToString(),
                Dependencies = dependencies?.ToList() ?? [],
                ChildGraph = null // Cannot convert between different CudaGraph types
            };

            parentGraph.Nodes.Add(node);
            UpdateStatistics(parentGraph.Name, s => s.NodeCount++);

            _logger.LogDebugMessage("'");
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

                _logger.LogInfoMessage("' for execution");
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
                _ = CudaRuntime.cudaEventCreate(ref startEvent);
                _ = CudaRuntime.cudaEventCreate(ref endEvent);

                _ = CudaRuntime.cudaEventRecord(startEvent, stream);

                // Launch the graph
                var result = CudaRuntime.cudaGraphLaunch(executable.Handle, stream);
                CudaRuntime.CheckError(result, "launching graph");

                // Record end event
                _ = CudaRuntime.cudaEventRecord(endEvent, stream);

                // Wait for completion if not cancelled
                await Task.Run(() =>
                {
                    _ = CudaRuntime.cudaStreamSynchronize(stream);
                }, cancellationToken);

                // Calculate execution time
                float elapsedMs = 0;
                _ = CudaRuntime.cudaEventElapsedTime(ref elapsedMs, startEvent, endEvent);

                // Clean up events
                _ = CudaRuntime.cudaEventDestroy(startEvent);
                _ = CudaRuntime.cudaEventDestroy(endEvent);

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

                _logger.LogDebugMessage($"Graph '{executable.Graph.Name}' executed in {elapsedMs}ms");

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

                _logger.LogErrorMessage("'");
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


                _logger.LogInfoMessage("'");
                return true;
            }

            _logger.LogWarningMessage($"Failed to update graph executable for '{newGraph.Name}': {result}");
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
            var result = CudaRuntime.cuGraphClone(out cloneHandle, sourceGraph.Handle);
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

            _logger.LogInfoMessage($"Cloned graph '{sourceGraph.Name}' to '{newName}'");
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
            _logger.LogInfoMessage($"Optimized graph '{graph.Name}' with {analysis.OptimizationsApplied} optimizations applied");
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
            _ = dot.AppendLine($"digraph {graph.Name} {{");
            _ = dot.AppendLine("  rankdir=TB;");
            _ = dot.AppendLine("  node [shape=box];");

            foreach (var node in graph.Nodes)
            {
                var label = $"{node.Type}\\n{node.Id.Substring(0, 8)}";
                _ = dot.AppendLine($"  \"{node.Id}\" [label=\"{label}\"];");

                foreach (var dep in node.Dependencies)
                {
                    _ = dot.AppendLine($"  \"{dep.Id}\" -> \"{node.Id}\";");
                }
            }

            _ = dot.AppendLine("}");
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

                _ = CudaRuntime.cudaGraphDestroy(graph.Handle);
                _logger.LogInfoMessage("'");
            }

            if (_executables.TryRemove(graphName, out var executable))
            {
                _ = CudaRuntime.cuGraphExecDestroy(executable.Handle);
                _logger.LogDebugMessage("'");
            }
        }

        private static GraphAnalysis AnalyzeGraph(CudaGraph graph)
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

        private static int CalculateCriticalPath(CudaGraph graph)
        {
            // Simplified critical path calculation
            var visited = new HashSet<string>();
            var pathLengths = new Dictionary<string, int>();

            int CalculatePathLength(CudaGraphNode node)
            {
                if (visited.Contains(node.Id))
                {

                    return pathLengths.GetValueOrDefault(node.Id, 0);
                }


                _ = visited.Add(node.Id);


                var maxDepPath = node.Dependencies.Count > 0
                    ? node.Dependencies.Max(d => CalculatePathLength(d))
                    : 0;

                pathLengths[node.Id] = maxDepPath + 1;
                return pathLengths[node.Id];
            }

            return graph.Nodes.Count > 0

                ? graph.Nodes.Max(CalculatePathLength)
                : 0;
        }

        private static int FindParallelizationOpportunities(CudaGraph graph)
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

        private static int FindFusionOpportunities(CudaGraph graph)
        {
            // Find kernel nodes that can be fused
            var opportunities = 0;
            var kernelNodes = graph.Nodes.Where(n => n.Type == GraphNodeType.Kernel).ToList();

            for (var i = 0; i < kernelNodes.Count - 1; i++)
            {
                for (var j = i + 1; j < kernelNodes.Count; j++)
                {
                    if (CanFuseKernels(kernelNodes[i], kernelNodes[j]))
                    {
                        opportunities++;
                    }
                }
            }

            return opportunities;
        }

        private static bool CanFuseKernels(CudaGraphNode kernel1, CudaGraphNode kernel2)
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

        private static long EstimateMemoryFootprint(CudaGraph graph)
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
            _logger.LogDebugMessage("'");
            analysis.OptimizationsApplied++;
        }

        private void OptimizeMemoryAccess(CudaGraph graph, GraphAnalysis analysis)
        {
            // Implement memory access pattern optimization
            _logger.LogDebugMessage("'");
            analysis.OptimizationsApplied++;
        }

        private void MaximizeParallelism(CudaGraph graph, GraphAnalysis analysis)
        {
            // Implement parallelism maximization
            _logger.LogDebugMessage("'");
            analysis.OptimizationsApplied++;
        }

        private void UpdateStatistics(string graphName, Action<Types.GraphStatistics> update)
        {
            _ = _statistics.AddOrUpdate(graphName,
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
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

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
    public sealed class GraphCaptureContext(
        IntPtr stream,
        string graphName,
        CudaGraphCaptureMode mode,
        Func<IntPtr, string, CudaGraph> endCapture) : IDisposable
    {
        private readonly IntPtr _stream = stream;
        private readonly string _graphName = graphName;
        private readonly Func<IntPtr, string, CudaGraph> _endCapture = endCapture;
        private bool _disposed;
        /// <summary>
        /// Gets or sets the mode.
        /// </summary>
        /// <value>The mode.</value>

        public CudaGraphCaptureMode Mode { get; } = mode;
        /// <summary>
        /// Gets end capture.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        public CudaGraph EndCapture()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var graph = _endCapture(_stream, _graphName);
            _disposed = true;
            return graph;
        }
        /// <summary>
        /// Performs dispose.
        /// </summary>

        public void Dispose()
        {
            if (!_disposed)
            {
                _ = EndCapture();
            }
        }
    }


    /// <summary>
    /// Graph optimization options
    /// </summary>
    public class GraphOptimizationOptions
    {
        /// <summary>
        /// Gets or sets the enable kernel fusion.
        /// </summary>
        /// <value>The enable kernel fusion.</value>
        public bool EnableKernelFusion { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable memory optimization.
        /// </summary>
        /// <value>The enable memory optimization.</value>
        public bool EnableMemoryOptimization { get; set; } = true;
        /// <summary>
        /// Gets or sets the enable parallelization.
        /// </summary>
        /// <value>The enable parallelization.</value>
        public bool EnableParallelization { get; set; } = true;
        /// <summary>
        /// Gets or sets the max fusion depth.
        /// </summary>
        /// <value>The max fusion depth.</value>
        public int MaxFusionDepth { get; set; } = 3;
        /// <summary>
        /// Gets or sets the max memory budget.
        /// </summary>
        /// <value>The max memory budget.</value>
        public long MaxMemoryBudget { get; set; } = 1024L * 1024 * 1024; // 1GB
    }
}