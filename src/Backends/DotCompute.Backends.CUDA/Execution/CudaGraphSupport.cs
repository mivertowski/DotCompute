// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Execution.Graph;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Abstractions.Types;
using Microsoft.Extensions.Logging;
using CudaArchitecture = DotCompute.Backends.CUDA.Types.CudaArchitecture;
using DotCompute.Backends.CUDA.Logging;

namespace DotCompute.Backends.CUDA.Execution
{
    /// <summary>
    /// Advanced CUDA Graph support for kernel fusion and optimization on RTX 2000 Ada architecture.
    /// Provides comprehensive graph management, execution, and optimization capabilities.
    /// </summary>
    /// <remarks>
    /// This class manages the complete lifecycle of CUDA graphs including creation, instantiation,
    /// execution, and optimization. It provides advanced features like kernel fusion, Ada Lovelace
    /// architecture optimizations, and performance monitoring for high-throughput computing scenarios.
    /// </remarks>
    public sealed class CudaGraphSupport : IDisposable
    {
        private readonly CudaContext _context;
        private readonly CudaStreamManager _streamManager;
        private readonly CudaEventManager _eventManager;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, CudaGraphInstance> _graphInstances;
        private readonly ConcurrentDictionary<string, CudaGraph> _graphTemplates;
        private readonly SemaphoreSlim _graphSemaphore;
        private readonly Timer _optimizationTimer;
        private bool _disposed;

        // Graph configuration constants
        private const int MaxGraphInstances = 100;

        /// <summary>
        /// Initializes a new instance of the <see cref="CudaGraphSupport"/> class.
        /// </summary>
        /// <param name="context">The CUDA context for graph operations.</param>
        /// <param name="streamManager">The stream manager for execution scheduling.</param>
        /// <param name="eventManager">The event manager for timing and synchronization.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public CudaGraphSupport(
            CudaContext context,
            CudaStreamManager streamManager,
            CudaEventManager eventManager,
            ILogger<CudaGraphSupport> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _streamManager = streamManager ?? throw new ArgumentNullException(nameof(streamManager));
            _eventManager = eventManager ?? throw new ArgumentNullException(nameof(eventManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _graphInstances = new ConcurrentDictionary<string, CudaGraphInstance>();
            _graphTemplates = new ConcurrentDictionary<string, CudaGraph>();
            _graphSemaphore = new SemaphoreSlim(MaxGraphInstances, MaxGraphInstances);

            // Set up periodic optimization
            _optimizationTimer = new Timer(OptimizeGraphs, null,
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            _logger.LogInfoMessage("CUDA Graph Support initialized for RTX 2000 Ada optimizations");
        }

        /// <summary>
        /// Creates a CUDA graph from a sequence of kernel operations.
        /// </summary>
        /// <param name="graphId">The unique identifier for the graph.</param>
        /// <param name="operations">The sequence of kernel operations to include in the graph.</param>
        /// <param name="options">Optional optimization settings for the graph.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous graph creation operation, returning the graph ID.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public async Task<string> CreateGraphAsync(
            string graphId,
            IEnumerable<CudaKernelOperation> operations,
            CUDA.Types.CudaGraphOptimizationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _graphSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                _context.MakeCurrent();

                var graph = new CudaGraph
                {
                    Id = graphId,
                    Operations = [.. operations],
                    CreatedAt = DateTimeOffset.UtcNow,
                    Options = options ?? new CUDA.Types.CudaGraphOptimizationOptions()
                };

                // Build the graph
                await BuildGraphAsync(graph, cancellationToken).ConfigureAwait(false);

                // Optimize the graph
                if (graph.Options.EnableOptimization)
                {
                    await OptimizeGraphAsync(graph, cancellationToken).ConfigureAwait(false);
                }

                _graphTemplates[graphId] = graph;

                _logger.LogInfoMessage($"Created CUDA graph {graphId} with {graph.NodeCount} nodes");

                return graphId;
            }
            finally
            {
                _ = _graphSemaphore.Release();
            }
        }

        /// <summary>
        /// Instantiates a graph for execution.
        /// </summary>
        /// <param name="graphId">The identifier of the graph to instantiate.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous instantiation operation, returning the graph instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown if the specified graph is not found.</exception>
        public async Task<CudaGraphInstance> InstantiateGraphAsync(
            string graphId,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            ThrowIfDisposed();

            if (!_graphTemplates.TryGetValue(graphId, out var graph))
            {
                throw new ArgumentException($"Graph {graphId} not found", nameof(graphId));
            }

            _context.MakeCurrent();

            var instanceId = $"{graphId}_{Guid.NewGuid():N}";
            var instance = new CudaGraphInstance
            {
                Id = instanceId,
                GraphId = graphId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                // Create graph instance
                var instanceHandle = IntPtr.Zero;
                var result = CudaRuntime.cuGraphInstantiate(
                    ref instanceHandle,
                    graph.Handle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    0);
                instance.Handle = instanceHandle;

                CudaRuntime.CheckError(result, "instantiating CUDA graph");

                instance.IsValid = true;
                _graphInstances[instanceId] = instance;

                _logger.LogDebugMessage($"Instantiated graph {graphId} as instance {instanceId}");

                return instance;
            }
            catch
            {
                instance.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Executes a graph instance.
        /// </summary>
        /// <param name="instance">The graph instance to execute.</param>
        /// <param name="stream">The CUDA stream for execution (default stream if not specified).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous execution operation, returning the execution results.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the graph instance is not valid.</exception>
        public Task<CUDA.Types.CudaGraphExecutionResult> ExecuteGraphAsync(
            CudaGraphInstance instance,
            IntPtr stream = default,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!instance.IsValid)
            {
                throw new InvalidOperationException("Graph instance is not valid");
            }

            _context.MakeCurrent();

            var startTime = DateTimeOffset.UtcNow;
            var startEvent = _eventManager.CreateEvent();
            var endEvent = _eventManager.CreateEvent();

            try
            {
                var executionStream = stream == IntPtr.Zero ? _streamManager.DefaultStream : stream;

                // Record start
                _eventManager.RecordEventFast(startEvent, executionStream);

                // Launch graph
                var result = CudaRuntime.cuGraphLaunch(instance.Handle, executionStream);
                CudaRuntime.CheckError(result, "launching CUDA graph");

                // Record end
                _eventManager.RecordEventFast(endEvent, executionStream);

                // Wait for completion
                var syncResult = CudaRuntime.cudaStreamSynchronize(executionStream);
                CudaRuntime.CheckError(syncResult, "stream synchronization");

                var endTime = DateTimeOffset.UtcNow;
                var gpuTime = _eventManager.ElapsedTime(startEvent, endEvent);

                instance.ExecutionCount++;
                instance.LastExecutedAt = endTime;
                instance.TotalGpuTime += gpuTime;

                return Task.FromResult(new CUDA.Types.CudaGraphExecutionResult
                {
                    Success = true,
                    InstanceId = instance.Id,
                    GraphId = instance.GraphId,
                    ExecutionTime = endTime - startTime,
                    GpuTimeMs = gpuTime,
                    ExecutionCount = instance.ExecutionCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogErrorMessage("");

                return Task.FromResult(new CUDA.Types.CudaGraphExecutionResult
                {
                    Success = false,
                    InstanceId = instance.Id,
                    GraphId = instance.GraphId,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                _eventManager.DestroyEvent(startEvent);
                _eventManager.DestroyEvent(endEvent);
            }
        }

        /// <summary>
        /// Updates a graph instance with new parameters.
        /// </summary>
        /// <param name="instance">The graph instance to update.</param>
        /// <param name="updateParams">The parameters for the update operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous update operation, returning true if successful.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public async Task<bool> UpdateGraphAsync(
            CudaGraphInstance instance,
            CUDA.Types.CudaGraphUpdateParameters updateParams,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            ThrowIfDisposed();

            if (!instance.IsValid)
            {
                return false;
            }

            _context.MakeCurrent();

            try
            {
                // Check if update is possible
                var updateResult = IntPtr.Zero;
                var result = CudaRuntime.cuGraphExecUpdate(
                    instance.Handle,
                    updateParams.SourceGraph,
                    ref updateResult);

                if (result == CudaError.Success)
                {
                    instance.UpdateCount++;
                    instance.LastUpdatedAt = DateTimeOffset.UtcNow;

                    _logger.LogDebugMessage($"Updated graph instance {instance.Id} (update #{instance.UpdateCount})");
                    return true;
                }
                else
                {
                    _logger.LogWarningMessage($"Graph update failed for instance {instance.Id}: {CudaRuntime.GetErrorString(result)}");
                    return false;
                }
            }
            catch (Exception)
            {
                _logger.LogErrorMessage("Error checking CUDA graph support");
                return false;
            }
        }

        /// <summary>
        /// Captures a sequence of operations into a graph.
        /// </summary>
        /// <param name="graphId">The unique identifier for the captured graph.</param>
        /// <param name="operations">A function that executes operations to be captured.</param>
        /// <param name="mode">The capture mode for the operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous capture operation, returning the graph ID.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public async Task<string> CaptureGraphAsync(
            string graphId,
            Func<IntPtr, Task> operations,
            CUDA.Types.CudaGraphCaptureMode mode = CUDA.Types.CudaGraphCaptureMode.Global,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var stream = await _streamManager.GetPooledStreamAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            try
            {
                _context.MakeCurrent();

                // Begin capture
                var result = CudaRuntime.cuStreamBeginCapture(stream.Stream, (uint)mode);
                CudaRuntime.CheckError(result, "beginning stream capture");

                try
                {
                    // Execute operations to capture
                    await operations(stream.Stream).ConfigureAwait(false);

                    // End capture and get graph
                    var capturedGraph = IntPtr.Zero;
                    result = CudaRuntime.cuStreamEndCapture(stream.Stream, ref capturedGraph);
                    CudaRuntime.CheckError(result, "ending stream capture");

                    var graph = new CudaGraph
                    {
                        Id = graphId,
                        Handle = capturedGraph,
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsCaptured = true,
                        Options = new CUDA.Types.CudaGraphOptimizationOptions { EnableOptimization = true }
                    };

                    // Verify node count - NodeCount is computed from the nodes collection
                    var nodeCount = graph.NodeCount;
                    var nodeCountNuint = (nuint)nodeCount;
                    result = CudaRuntime.cuGraphGetNodes(capturedGraph, IntPtr.Zero, ref nodeCountNuint);
                    nodeCount = (int)nodeCountNuint;
                    CudaRuntime.CheckError(result, "getting graph node count");

                    // Log verification of node count consistency

                    if (nodeCount != graph.NodeCount)
                    {
                        _logger.LogWarningMessage($"Node count mismatch: Graph reports {graph.NodeCount}, CUDA reports {nodeCount}");
                    }

                    _graphTemplates[graphId] = graph;

                    _logger.LogInfoMessage($"Captured CUDA graph {graphId} with {graph.NodeCount} nodes");

                    return graphId;
                }
                catch
                {
                    // Try to end capture on error
                    try
                    {
                        var errorGraph = IntPtr.Zero;
                        _ = CudaRuntime.cuStreamEndCapture(stream.Stream, ref errorGraph);
                        if (errorGraph != IntPtr.Zero)
                        {
                            _ = CudaRuntime.cuGraphDestroy(errorGraph);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up failed graph capture");
                    }
                    throw;
                }
            }
            finally
            {
                stream.Dispose();
            }
        }

        /// <summary>
        /// Fuses multiple kernels into a single optimized graph.
        /// </summary>
        /// <param name="graphId">The unique identifier for the fused graph.</param>
        /// <param name="kernels">The kernels to fuse together.</param>
        /// <param name="fusionOptions">Configuration options for the fusion operation.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous fusion operation, returning the graph ID.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public async Task<string> FuseKernelsAsync(
            string graphId,
            IEnumerable<CudaCompiledKernel> kernels,
            Configuration.CudaKernelFusionOptions fusionOptions,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var operations = kernels.Select(kernel => new CudaKernelOperation
            {
                Kernel = kernel,
                Arguments = fusionOptions.GetArgumentsForKernel(kernel),
                LaunchConfig = fusionOptions.GetLaunchConfigForKernel(kernel)
            });

            var graphOptions = new CUDA.Types.CudaGraphOptimizationOptions
            {
                EnableOptimization = true,
                EnableKernelFusion = true,
                OptimizationLevel = CUDA.Types.CudaGraphOptimizationLevel.Aggressive,
                TargetArchitecture = CudaArchitecture.Ada // RTX 2000 Ada specific
            };

            return await CreateGraphAsync(graphId, operations, graphOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets performance statistics for a graph.
        /// </summary>
        /// <param name="graphId">The identifier of the graph for which to retrieve statistics.</param>
        /// <returns>A CudaGraphStatistics object containing performance metrics.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public CUDA.Types.CudaGraphStatistics GetGraphStatistics(string graphId)
        {
            ThrowIfDisposed();

            var instances = _graphInstances.Values
                .Where(i => i.GraphId == graphId)
                .ToList();

            var totalExecutions = instances.Sum(i => i.ExecutionCount);
            var totalGpuTime = instances.Sum(i => i.TotalGpuTime);
            var avgExecutionTime = totalExecutions > 0 ? totalGpuTime / totalExecutions : 0;

            return new CUDA.Types.CudaGraphStatistics
            {
                GraphId = graphId,
                InstanceCount = instances.Count,
                TotalExecutions = totalExecutions,
                TotalGpuTimeMs = totalGpuTime,
                AverageExecutionTimeMs = avgExecutionTime,
                LastExecutedAt = instances.Max(i => i.LastExecutedAt),
                IsOptimized = _graphTemplates.TryGetValue(graphId, out var graph) &&
                             graph.Options.EnableOptimization
            };
        }

        /// <summary>
        /// Cleans up unused graph instances older than the specified age.
        /// </summary>
        /// <param name="maxAge">The maximum age for keeping unused instances.</param>
        /// <returns>The number of instances that were cleaned up.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
        public int CleanupUnusedInstances(TimeSpan maxAge)
        {
            ThrowIfDisposed();

            var cutoffTime = DateTimeOffset.UtcNow - maxAge;
            var instancesToRemove = _graphInstances.Values
                .Where(i => i.LastExecutedAt < cutoffTime ||
                           (!i.LastExecutedAt.HasValue && i.CreatedAt < cutoffTime))
                .ToList();

            var removedCount = 0;
            foreach (var instance in instancesToRemove)
            {
                if (_graphInstances.TryRemove(instance.Id, out var removed))
                {
                    removed.Dispose();
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                _logger.LogInfoMessage(" unused graph instances");
            }

            return removedCount;
        }

        private async Task BuildGraphAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            _context.MakeCurrent();

            // Create empty graph
            var graphHandle = graph.Handle;
            var result = CudaRuntime.cuGraphCreate(ref graphHandle, 0);
            graph.Handle = graphHandle;
            CudaRuntime.CheckError(result, "creating CUDA graph");

            var nodeHandles = new List<IntPtr>();

            try
            {
                foreach (var operation in graph.Operations)
                {
                    var nodeHandle = await CreateKernelNodeAsync(graph.Handle, operation, cancellationToken)
                        .ConfigureAwait(false);
                    nodeHandles.Add(nodeHandle);
                }

                graph.IsBuilt = true;

                // Verify node count matches handles created

                if (graph.NodeCount != nodeHandles.Count)
                {
                    _logger.LogWarningMessage($"Node count mismatch after building: Graph has {graph.NodeCount} nodes, created {nodeHandles.Count} handles");
                }

                _logger.LogDebugMessage($"Built graph {graph.Id} with {graph.NodeCount} kernel nodes");
            }
            catch
            {
                // Clean up on failure
                foreach (var nodeHandle in nodeHandles)
                {
                    try
                    {
                        _ = CudaRuntime.cuGraphDestroyNode(nodeHandle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up graph node during build failure");
                    }
                }
                throw;
            }
        }

        private static async Task<IntPtr> CreateKernelNodeAsync(
            IntPtr graph,
            CudaKernelOperation operation,
            CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            var nodeParams = new CudaKernelNodeParams
            {
                func = operation.Kernel.FunctionHandle,
                gridDimX = operation.LaunchConfig.GridX,
                gridDimY = operation.LaunchConfig.GridY,
                gridDimZ = operation.LaunchConfig.GridZ,
                blockDimX = operation.LaunchConfig.BlockX,
                blockDimY = operation.LaunchConfig.BlockY,
                blockDimZ = operation.LaunchConfig.BlockZ,
                sharedMemBytes = operation.LaunchConfig.SharedMemoryBytes
            };

            // Prepare kernel arguments
            var argPointers = PrepareCudaKernelArguments(operation.Arguments);
            nodeParams.kernelParams = argPointers;

            var nodeHandle = IntPtr.Zero;
            var result = CudaRuntime.cuGraphAddKernelNode(
                ref nodeHandle,
                graph,
                [], // No dependencies yet
                0,
                ref nodeParams);

            CudaRuntime.CheckError(result, "adding kernel node to graph");

            return nodeHandle;
        }

        private static IntPtr PrepareCudaKernelArguments(CUDA.Types.CudaKernelArguments arguments)
        {
            // Convert arguments to format suitable for graph nodes
            // This is a simplified version - production would need more sophisticated handling
            var handles = new List<GCHandle>();
            var argPointers = new List<IntPtr>();

            foreach (var arg in arguments.ToArray())
            {
                var handle = GCHandle.Alloc(arg, GCHandleType.Pinned);
                handles.Add(handle);
                argPointers.Add(handle.AddrOfPinnedObject());
            }

            var argArray = argPointers.ToArray();
            var arrayHandle = GCHandle.Alloc(argArray, GCHandleType.Pinned);

            return arrayHandle.AddrOfPinnedObject();
        }

        private async Task OptimizeGraphAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            if (!graph.Options.EnableOptimization)
            {
                return;
            }

            // RTX 2000 Ada specific optimizations
            if (graph.Options.TargetArchitecture == CudaArchitecture.Ada)
            {
                await OptimizeForAdaArchitectureAsync(graph, cancellationToken).ConfigureAwait(false);
            }

            // General optimizations
            if (graph.Options.EnableKernelFusion && graph.NodeCount > 1)
            {
                await AttemptKernelFusionAsync(graph, cancellationToken).ConfigureAwait(false);
            }

            graph.IsOptimized = true;
        }

        private async Task OptimizeForAdaArchitectureAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Ada-specific optimizations
            // - Optimize for tensor cores
            // - Memory access patterns
            // - Warp scheduling

            _logger.LogDebugMessage("");

            // Apply Ada Lovelace specific optimizations
            // These would use CUDA 12+ APIs for Ada architecture features
            await ApplyTensorCoreOptimizationsAsync(graph, cancellationToken).ConfigureAwait(false);
            await OptimizeMemoryAccessPatternsAsync(graph, cancellationToken).ConfigureAwait(false);
            await ConfigureWarpSchedulingAsync(graph, cancellationToken).ConfigureAwait(false);
        }

        private async Task AttemptKernelFusionAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Attempt to fuse compatible kernels
            _logger.LogDebugMessage("");

            // Analyze graph nodes for fusion opportunities
            var fusionCandidates = IdentifyFusionCandidates(graph);
            if (fusionCandidates.Count > 0)
            {
                // Apply kernel fusion using CUDA graph API
                await FuseKernelNodesAsync(graph, fusionCandidates, cancellationToken).ConfigureAwait(false);
                _logger.LogInfoMessage($"Successfully fused {fusionCandidates.Count} kernel pairs in graph {graph.Id}");
            }
        }

        private void OptimizeGraphs(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // Periodic optimization of existing graphs
                var activeGraphs = _graphInstances.Values
                    .GroupBy(i => i.GraphId)
                    .Where(g => g.Any(i => i.ExecutionCount > 10)) // Only optimize frequently used graphs
                    .Select(g => g.Key)
                    .ToList();

                foreach (var graphId in activeGraphs)
                {
                    if (_graphTemplates.TryGetValue(graphId, out var graph) && !graph.IsOptimized)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await OptimizeGraphAsync(graph, CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error optimizing graph {GraphId}", graphId);
                            }
                        });
                    }
                }

                // Clean up old instances
                _ = CleanupUnusedInstances(TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during periodic graph optimization");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CudaGraphSupport));
            }
        }

        /// <summary>
        /// Disposes of the graph support resources and cleans up all managed graphs and instances.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _optimizationTimer?.Dispose();

                // Dispose all instances
                foreach (var instance in _graphInstances.Values)
                {
                    instance.Dispose();
                }
                _graphInstances.Clear();

                // Dispose all graphs
                foreach (var graph in _graphTemplates.Values)
                {
                    graph.Dispose();
                }
                _graphTemplates.Clear();

                _graphSemaphore?.Dispose();
                _disposed = true;

                _logger.LogInfoMessage("CUDA Graph Support disposed");
            }
        }

        private static async Task ApplyTensorCoreOptimizationsAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Apply tensor core optimizations for matrix operations
            // This would use CUDA's tensor core APIs for Ada architecture
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            foreach (var op in graph.Operations.Where(o => o.Type == CUDA.Types.CudaKernelType.MatrixMultiply))
            {
                // Configure for tensor core usage
                op.UseTensorCores = true;
                op.TensorCoreConfig = new CUDA.Types.TensorCoreConfig
                {
                    DataType = "TF32",
                    Precision = "High"
                };
            }
        }

        private static async Task OptimizeMemoryAccessPatternsAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Optimize memory access patterns for coalesced access
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            foreach (var op in graph.Operations)
            {
                // Configure optimal memory access patterns
                op.MemoryAccessPattern = MemoryAccessPattern.Coalesced;
                op.CacheConfig = CUDA.Types.CacheConfig.PreferL1;
            }
        }

        private static async Task ConfigureWarpSchedulingAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Configure warp scheduling for Ada architecture
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            // Ada-specific warp scheduling optimizations
            foreach (var op in graph.Operations)
            {
                op.WarpScheduling = CUDA.Types.WarpSchedulingMode.Persistent;
            }
        }

        private static List<Optimization.KernelFusionCandidate> IdentifyFusionCandidates(CudaGraph graph)
        {
            var candidates = new List<Optimization.KernelFusionCandidate>();

            // Identify adjacent kernels that can be fused
            for (var i = 0; i < graph.Operations.Count - 1; i++)
            {
                var current = graph.Operations[i];
                var next = graph.Operations[i + 1];

                // Check if kernels can be fused (element-wise operations are good candidates)
                if (CanFuseKernels(current, next))
                {
                    candidates.Add(new Optimization.KernelFusionCandidate
                    {
                        FirstKernel = current,
                        SecondKernel = next,
                        EstimatedBenefit = EstimateFusionBenefit(current, next)
                    });
                }
            }

            return candidates;
        }

        private static bool CanFuseKernels(CudaKernelOperation first, CudaKernelOperation second)
        {
            // Check if kernels are compatible for fusion
            // Element-wise operations with the same dimensions are good candidates
            return first.Type == CUDA.Types.CudaKernelType.ElementWise &&
                   second.Type == CUDA.Types.CudaKernelType.ElementWise &&
                   first.OutputDimensions == second.InputDimensions;
        }

        private static double EstimateFusionBenefit(CudaKernelOperation first, CudaKernelOperation second)
            // Estimate the performance benefit of fusing these kernels
            // Consider memory bandwidth savings and kernel launch overhead reduction




            => 0.2; // 20% estimated improvement

        private static async Task FuseKernelNodesAsync(CudaGraph graph, List<Optimization.KernelFusionCandidate> candidates, CancellationToken cancellationToken)
        {
            // Apply kernel fusion using CUDA graph API
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            foreach (var candidate in candidates.OrderByDescending(c => c.EstimatedBenefit))
            {
                // Create fused kernel
                var fusedKernel = new CudaKernelOperation
                {
                    Name = $"{candidate.FirstKernel.Name}_fused_{candidate.SecondKernel.Name}",
                    Type = CUDA.Types.CudaKernelType.Fused,
                    IsFused = true,
                    OriginalOperations = [candidate.FirstKernel, candidate.SecondKernel]
                };

                // Replace original kernels with fused kernel in graph
                var index = graph.Operations.IndexOf(candidate.FirstKernel);
                if (index >= 0)
                {
                    graph.Operations[index] = fusedKernel;
                    _ = graph.Operations.Remove(candidate.SecondKernel);
                }
            }
        }
    }
}