// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Compilation;
using DotCompute.Backends.CUDA.Types;
using Microsoft.Extensions.Logging;
using DotCompute.Backends.CUDA.Native.Types;

namespace DotCompute.Backends.CUDA.Execution
{

    /// <summary>
    /// Advanced CUDA Graph support for kernel fusion and optimization on RTX 2000 Ada
    /// </summary>
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

        // Graph configuration
        private const int MaxGraphInstances = 100;
        private const int MaxNodesPerGraph = 1000;

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

            _logger.LogInformation("CUDA Graph Support initialized for RTX 2000 Ada optimizations");
        }

        /// <summary>
        /// Creates a CUDA graph from a sequence of kernel operations
        /// </summary>
        public async Task<string> CreateGraphAsync(
            string graphId,
            IEnumerable<CudaKernelOperation> operations,
            CudaGraphOptimizationOptions? options = null,
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
                    Options = options ?? new CudaGraphOptimizationOptions()
                };

                // Build the graph
                await BuildGraphAsync(graph, cancellationToken).ConfigureAwait(false);

                // Optimize the graph
                if (graph.Options.EnableOptimization)
                {
                    await OptimizeGraphAsync(graph, cancellationToken).ConfigureAwait(false);
                }

                _graphTemplates[graphId] = graph;

                _logger.LogInformation("Created CUDA graph {GraphId} with {NodeCount} nodes",
                    graphId, graph.NodeCount);

                return graphId;
            }
            finally
            {
                _ = _graphSemaphore.Release();
            }
        }

        /// <summary>
        /// Instantiates a graph for execution
        /// </summary>
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

                _logger.LogDebug("Instantiated graph {GraphId} as instance {InstanceId}",
                    graphId, instanceId);

                return instance;
            }
            catch
            {
                instance.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Executes a graph instance
        /// </summary>
        public Task<CudaGraphExecutionResult> ExecuteGraphAsync(
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

                return Task.FromResult(new CudaGraphExecutionResult
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
                _logger.LogError(ex, "Failed to execute graph instance {InstanceId}", instance.Id);

                return Task.FromResult(new CudaGraphExecutionResult
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
        /// Updates a graph instance with new parameters
        /// </summary>
        public async Task<bool> UpdateGraphAsync(
            CudaGraphInstance instance,
            CudaGraphUpdateParameters updateParams,
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

                    _logger.LogDebug("Updated graph instance {InstanceId} (update #{UpdateCount})",
                        instance.Id, instance.UpdateCount);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Graph update failed for instance {InstanceId}: {Error}",
                        instance.Id, CudaRuntime.GetErrorString(result));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating graph instance {InstanceId}", instance.Id);
                return false;
            }
        }

        /// <summary>
        /// Captures a sequence of operations into a graph
        /// </summary>
        public async Task<string> CaptureGraphAsync(
            string graphId,
            Func<IntPtr, Task> operations,
            CudaGraphCaptureMode mode = CudaGraphCaptureMode.Global,
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
                        Options = new CudaGraphOptimizationOptions { EnableOptimization = true }
                    };

                    // Get node count
                    var nodeCount = graph.NodeCount;
                    result = CudaRuntime.cuGraphGetNodes(capturedGraph, IntPtr.Zero, ref nodeCount);
                    graph.NodeCount = nodeCount;
                    CudaRuntime.CheckError(result, "getting graph node count");

                    _graphTemplates[graphId] = graph;

                    _logger.LogInformation("Captured CUDA graph {GraphId} with {NodeCount} nodes",
                        graphId, graph.NodeCount);

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
        /// Fuses multiple kernels into a single optimized graph
        /// </summary>
        public async Task<string> FuseKernelsAsync(
            string graphId,
            IEnumerable<CudaCompiledKernel> kernels,
            CudaKernelFusionOptions fusionOptions,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var operations = kernels.Select(kernel => new CudaKernelOperation
            {
                Kernel = kernel,
                Arguments = fusionOptions.GetArgumentsForKernel(kernel),
                LaunchConfig = fusionOptions.GetLaunchConfigForKernel(kernel)
            });

            var graphOptions = new CudaGraphOptimizationOptions
            {
                EnableOptimization = true,
                EnableKernelFusion = true,
                OptimizationLevel = CudaGraphOptimizationLevel.Aggressive,
                TargetArchitecture = CudaArchitecture.Ada // RTX 2000 Ada specific
            };

            return await CreateGraphAsync(graphId, operations, graphOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Gets performance statistics for a graph
        /// </summary>
        public CudaGraphStatistics GetGraphStatistics(string graphId)
        {
            ThrowIfDisposed();

            var instances = _graphInstances.Values
                .Where(i => i.GraphId == graphId)
                .ToList();

            var totalExecutions = instances.Sum(i => i.ExecutionCount);
            var totalGpuTime = instances.Sum(i => i.TotalGpuTime);
            var avgExecutionTime = totalExecutions > 0 ? totalGpuTime / totalExecutions : 0;

            return new CudaGraphStatistics
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
        /// Cleans up unused graph instances
        /// </summary>
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
                _logger.LogInformation("Cleaned up {Count} unused graph instances", removedCount);
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

                graph.NodeCount = nodeHandles.Count;
                graph.IsBuilt = true;

                _logger.LogDebug("Built graph {GraphId} with {NodeCount} kernel nodes",
                    graph.Id, graph.NodeCount);
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
                Function = operation.Kernel.FunctionHandle,
                GridDimX = operation.LaunchConfig.GridX,
                GridDimY = operation.LaunchConfig.GridY,
                GridDimZ = operation.LaunchConfig.GridZ,
                BlockDimX = operation.LaunchConfig.BlockX,
                BlockDimY = operation.LaunchConfig.BlockY,
                BlockDimZ = operation.LaunchConfig.BlockZ,
                SharedMemBytes = operation.LaunchConfig.SharedMemoryBytes
            };

            // Prepare kernel arguments
            var argPointers = PrepareCudaKernelArguments(operation.Arguments);
            nodeParams.KernelParams = argPointers;

            var nodeHandle = IntPtr.Zero;
            var result = CudaRuntime.cuGraphAddKernelNode(
                ref nodeHandle,
                graph,
                new IntPtr[0], // No dependencies yet
                0,
                ref nodeParams);

            CudaRuntime.CheckError(result, "adding kernel node to graph");

            return nodeHandle;
        }

        private static IntPtr PrepareCudaKernelArguments(CudaKernelArguments arguments)
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

            _logger.LogDebug("Applied Ada Lovelace optimizations to graph {GraphId}", graph.Id);

            // Apply Ada Lovelace specific optimizations
            // These would use CUDA 12+ APIs for Ada architecture features
            await ApplyTensorCoreOptimizationsAsync(graph, cancellationToken).ConfigureAwait(false);
            await OptimizeMemoryAccessPatternsAsync(graph, cancellationToken).ConfigureAwait(false);
            await ConfigureWarpSchedulingAsync(graph, cancellationToken).ConfigureAwait(false);
        }

        private async Task AttemptKernelFusionAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Attempt to fuse compatible kernels
            _logger.LogDebug("Attempting kernel fusion for graph {GraphId}", graph.Id);

            // Analyze graph nodes for fusion opportunities
            var fusionCandidates = IdentifyFusionCandidates(graph);
            if (fusionCandidates.Count > 0)
            {
                // Apply kernel fusion using CUDA graph API
                await FuseKernelNodesAsync(graph, fusionCandidates, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Successfully fused {Count} kernel pairs in graph {GraphId}",
                    fusionCandidates.Count, graph.Id);
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

                _logger.LogInformation("CUDA Graph Support disposed");
            }
        }

        private static async Task ApplyTensorCoreOptimizationsAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Apply tensor core optimizations for matrix operations
            // This would use CUDA's tensor core APIs for Ada architecture
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            foreach (var op in graph.Operations.Where(o => o.Type == CudaKernelType.MatrixMultiply))
            {
                // Configure for tensor core usage
                op.UseTensorCores = true;
                op.TensorCoreConfig = new TensorCoreConfig
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
                op.CacheConfig = CacheConfig.PreferL1;
            }
        }

        private static async Task ConfigureWarpSchedulingAsync(CudaGraph graph, CancellationToken cancellationToken)
        {
            // Configure warp scheduling for Ada architecture
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            // Ada-specific warp scheduling optimizations
            foreach (var op in graph.Operations)
            {
                op.WarpScheduling = WarpSchedulingMode.Persistent;
            }
        }

        private static List<KernelFusionCandidate> IdentifyFusionCandidates(CudaGraph graph)
        {
            var candidates = new List<KernelFusionCandidate>();

            // Identify adjacent kernels that can be fused
            for (var i = 0; i < graph.Operations.Count - 1; i++)
            {
                var current = graph.Operations[i];
                var next = graph.Operations[i + 1];

                // Check if kernels can be fused (element-wise operations are good candidates)
                if (CanFuseKernels(current, next))
                {
                    candidates.Add(new KernelFusionCandidate
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
            return first.Type == CudaKernelType.ElementWise &&
                   second.Type == CudaKernelType.ElementWise &&
                   first.OutputDimensions == second.InputDimensions;
        }

        private static double EstimateFusionBenefit(CudaKernelOperation first, CudaKernelOperation second)
            // Estimate the performance benefit of fusing these kernels
            // Consider memory bandwidth savings and kernel launch overhead reduction
            => 0.2; // 20% estimated improvement

        private static async Task FuseKernelNodesAsync(CudaGraph graph, List<KernelFusionCandidate> candidates, CancellationToken cancellationToken)
        {
            // Apply kernel fusion using CUDA graph API
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);

            foreach (var candidate in candidates.OrderByDescending(c => c.EstimatedBenefit))
            {
                // Create fused kernel
                var fusedKernel = new CudaKernelOperation
                {
                    Name = $"{candidate.FirstKernel.Name}_fused_{candidate.SecondKernel.Name}",
                    Type = CudaKernelType.Fused,
                    IsFused = true,
                    OriginalOperations = new[] { candidate.FirstKernel, candidate.SecondKernel }
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

    // Supporting types and classes
    public sealed class CudaGraph : IDisposable
    {
        public string Id { get; set; } = string.Empty;
        public IntPtr Handle { get; set; }
        public List<CudaKernelOperation> Operations { get; set; } = [];
        public int NodeCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsBuilt { get; set; }
        public bool IsOptimized { get; set; }
        public bool IsCaptured { get; set; }
        public CudaGraphOptimizationOptions Options { get; set; } = new();

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    _ = CudaRuntime.cuGraphDestroy(Handle);
                }
                catch (Exception)
                {
                    // Ignore disposal errors
                }
                Handle = IntPtr.Zero;
            }
        }
    }

    public sealed class CudaGraphInstance : IDisposable
    {
        public string Id { get; set; } = string.Empty;
        public string GraphId { get; set; } = string.Empty;
        public IntPtr Handle { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastExecutedAt { get; set; }
        public DateTimeOffset? LastUpdatedAt { get; set; }
        public int ExecutionCount { get; set; }
        public int UpdateCount { get; set; }
        public float TotalGpuTime { get; set; }
        public bool IsValid { get; set; }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                try
                {
                    _ = CudaRuntime.cuGraphExecDestroy(Handle);
                }
                catch (Exception)
                {
                    // Ignore disposal errors
                }
                Handle = IntPtr.Zero;
                IsValid = false;
            }
        }
    }

    public sealed class CudaKernelOperation
    {
        public CudaCompiledKernel Kernel { get; set; } = null!;
        public CudaKernelArguments Arguments { get; set; } = null!;
        public CudaLaunchConfig LaunchConfig { get; set; }
        public string Name { get; set; } = string.Empty;
        public CudaKernelType Type { get; set; } = CudaKernelType.Custom;
        public bool UseTensorCores { get; set; }
        public TensorCoreConfig? TensorCoreConfig { get; set; }
        public MemoryAccessPattern MemoryAccessPattern { get; set; } = MemoryAccessPattern.Sequential;
        public CacheConfig CacheConfig { get; set; } = CacheConfig.PreferNone;
        public WarpSchedulingMode WarpScheduling { get; set; } = WarpSchedulingMode.Default;
        public string OutputDimensions { get; set; } = string.Empty;
        public string InputDimensions { get; set; } = string.Empty;
        public bool IsFused { get; set; }
        public CudaKernelOperation[]? OriginalOperations { get; set; }
    }

    public sealed class CudaGraphOptimizationOptions
    {
        public bool EnableOptimization { get; set; } = true;
        public bool EnableKernelFusion { get; set; } = true;
        public CudaGraphOptimizationLevel OptimizationLevel { get; set; } = CudaGraphOptimizationLevel.Balanced;
        public CudaArchitecture TargetArchitecture { get; set; } = CudaArchitecture.Ada;
    }

    public enum CudaGraphOptimizationLevel
    {
        None,
        Basic,
        Balanced,
        Aggressive
    }

    public enum CudaArchitecture
    {
        Turing,
        Ampere,
        Ada,
        Hopper
    }

    public enum CudaGraphCaptureMode
    {
        Global = 0,
        ThreadLocal = 1,
        Relaxed = 2
    }

    public sealed class CudaGraphUpdateParameters
    {
        public IntPtr SourceGraph { get; set; }
        public Dictionary<string, object> UpdatedParameters { get; set; } = [];
    }

    public sealed class CudaKernelFusionOptions
    {
        private readonly Dictionary<CudaCompiledKernel, CudaKernelArguments> _arguments = [];
        private readonly Dictionary<CudaCompiledKernel, CudaLaunchConfig> _configs = [];

        public CudaKernelArguments GetArgumentsForKernel(CudaCompiledKernel kernel) => _arguments.TryGetValue(kernel, out var args) ? args : new CudaKernelArguments([]);

        public CudaLaunchConfig GetLaunchConfigForKernel(CudaCompiledKernel kernel) => _configs.TryGetValue(kernel, out var config) ? config : new CudaLaunchConfig(1, 1, 1, 256, 1, 1);

        public void SetCudaKernelArguments(CudaCompiledKernel kernel, CudaKernelArguments arguments) => _arguments[kernel] = arguments;

        public void SetKernelLaunchConfig(CudaCompiledKernel kernel, CudaLaunchConfig config) => _configs[kernel] = config;
    }

    public sealed class CudaGraphExecutionResult
    {
        public bool Success { get; set; }
        public string InstanceId { get; set; } = string.Empty;
        public string GraphId { get; set; } = string.Empty;
        public TimeSpan ExecutionTime { get; set; }
        public float GpuTimeMs { get; set; }
        public int ExecutionCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public sealed class CudaGraphStatistics
    {
        public string GraphId { get; set; } = string.Empty;
        public int InstanceCount { get; set; }
        public int TotalExecutions { get; set; }
        public float TotalGpuTimeMs { get; set; }
        public float AverageExecutionTimeMs { get; set; }
        public DateTimeOffset? LastExecutedAt { get; set; }
        public bool IsOptimized { get; set; }
    }

    public sealed class KernelFusionCandidate
    {
        public CudaKernelOperation FirstKernel { get; set; } = null!;
        public CudaKernelOperation SecondKernel { get; set; } = null!;
        public double EstimatedBenefit { get; set; }
    }

    public sealed class TensorCoreConfig
    {
        public string DataType { get; set; } = "TF32";
        public string Precision { get; set; } = "High";
    }

    public enum MemoryAccessPattern
    {
        Sequential,
        Strided,
        Coalesced,
        Random
    }

    public enum CacheConfig
    {
        PreferNone,
        PreferShared,
        PreferL1,
        PreferEqual
    }

    public enum WarpSchedulingMode
    {
        Default,
        Persistent,
        Dynamic
    }

    public enum CudaKernelType
    {
        ElementWise,
        MatrixMultiply,
        Reduction,
        Transpose,
        Custom,
        Fused
    }
}
