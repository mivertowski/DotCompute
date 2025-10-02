// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using DotCompute.Backends.Metal.Execution.Graph.Nodes;
using DotCompute.Backends.Metal.Execution.Graph.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Execution.Graph;

/// <summary>
/// Provides optimized execution of Metal compute graphs with parallel processing,
/// resource scheduling, and performance monitoring capabilities.
/// </summary>
public sealed class MetalGraphExecutor : IDisposable
{
    private readonly ILogger<MetalGraphExecutor> _logger;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _nodeCompletions;
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetalGraphExecutor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for execution monitoring.</param>
    /// <param name="maxConcurrentOperations">The maximum number of concurrent operations allowed.</param>
    public MetalGraphExecutor(ILogger<MetalGraphExecutor> logger, int maxConcurrentOperations = 8)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _nodeCompletions = new ConcurrentDictionary<string, TaskCompletionSource<bool>>();
        _executionSemaphore = new SemaphoreSlim(Math.Max(1, maxConcurrentOperations), Math.Max(1, maxConcurrentOperations));
        _cancellationTokenSource = new CancellationTokenSource();

        _logger.LogDebug("Created MetalGraphExecutor with max concurrent operations: {MaxOperations}", maxConcurrentOperations);
    }

    /// <summary>
    /// Executes a Metal compute graph with optimal performance and resource utilization.
    /// </summary>
    /// <param name="graph">The graph to execute.</param>
    /// <param name="commandQueue">The Metal command queue for execution.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The execution result containing performance metrics and status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when graph or commandQueue is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the graph is not built or contains validation errors.</exception>
    public async Task<MetalGraphExecutionResult> ExecuteAsync(
        MetalComputeGraph graph,
        IntPtr commandQueue,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(graph);

        if (commandQueue == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(commandQueue));
        }

        if (!graph.IsBuilt)
        {
            throw new InvalidOperationException("Graph must be built before execution.");
        }

        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _cancellationTokenSource.Token);

        var executionId = Guid.NewGuid().ToString();
        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting execution of graph '{GraphName}' with ID {ExecutionId}", graph.Name, executionId);

        try
        {
            // Validate the graph before execution
            var validationErrors = await ValidateGraphAsync(graph, combinedCts.Token);
            if (validationErrors.Count > 0)
            {
                var errorMessage = string.Join("; ", validationErrors);
                throw new InvalidOperationException($"Graph validation failed: {errorMessage}");
            }

            // Create execution context
            var context = new GraphExecutionContext(executionId, graph, commandQueue, combinedCts.Token);

            // Execute the graph
            await ExecuteGraphInternalAsync(context);

            // Gather performance metrics
            var endTime = DateTimeOffset.UtcNow;
            stopwatch.Stop();

            var result = new MetalGraphExecutionResult
            {
                ExecutionId = executionId,
                GraphName = graph.Name,
                Success = true,
                StartTime = startTime,
                EndTime = endTime,
                GpuExecutionTimeMs = context.TotalGpuTimeMs,
                NodesExecuted = context.NodesExecuted,
                CommandBuffersUsed = context.CommandBuffersUsed,
                TotalMemoryTransferred = context.TotalMemoryTransferred
            };

            // Add performance metrics
            result.PerformanceMetrics["TotalExecutionTimeMs"] = stopwatch.Elapsed.TotalMilliseconds;
            result.PerformanceMetrics["AverageNodeExecutionTimeMs"] = context.NodesExecuted > 0 ? context.TotalGpuTimeMs / context.NodesExecuted : 0;
            result.PerformanceMetrics["ParallelEfficiency"] = CalculateParallelEfficiency(context);
            result.PerformanceMetrics["MemoryBandwidthMBps"] = CalculateMemoryBandwidth(context);

            _logger.LogInformation(
                "Completed execution of graph '{GraphName}' in {ExecutionTimeMs:F2}ms - Nodes: {NodesExecuted}, GPU Time: {GpuTimeMs:F2}ms",
                graph.Name, stopwatch.Elapsed.TotalMilliseconds, context.NodesExecuted, context.TotalGpuTimeMs);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Execution of graph '{GraphName}' was cancelled", graph.Name);


            return new MetalGraphExecutionResult
            {
                ExecutionId = executionId,
                GraphName = graph.Name,
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                ErrorMessage = "Execution was cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute graph '{GraphName}'", graph.Name);


            return new MetalGraphExecutionResult
            {
                ExecutionId = executionId,
                GraphName = graph.Name,
                Success = false,
                StartTime = startTime,
                EndTime = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
        finally
        {
            // Clean up node completion sources
            foreach (var completion in _nodeCompletions.Values)
            {
                _ = completion.TrySetResult(false);
            }
            _nodeCompletions.Clear();
        }
    }

    /// <summary>
    /// Executes a single node from the graph.
    /// </summary>
    /// <param name="node">The node to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal async Task ExecuteNodeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        await _executionSemaphore.WaitAsync(context.CancellationToken);

        try
        {
            node.ExecutionState = MetalNodeExecutionState.Executing;
            node.ExecutionStartTime = DateTimeOffset.UtcNow;

            _logger.LogDebug("Executing node '{NodeId}' of type {NodeType}", node.Id, node.Type);

            var nodeStopwatch = Stopwatch.StartNew();

            try
            {
                await ExecuteNodeByTypeAsync(node, context);


                nodeStopwatch.Stop();
                node.ExecutionEndTime = DateTimeOffset.UtcNow;
                node.ExecutionState = MetalNodeExecutionState.Completed;

                context.NodesExecuted++;
                context.TotalGpuTimeMs += nodeStopwatch.Elapsed.TotalMilliseconds;

                _logger.LogTrace("Completed node '{NodeId}' in {ExecutionTimeMs:F2}ms",

                    node.Id, nodeStopwatch.Elapsed.TotalMilliseconds);

                // Mark node as completed
                if (_nodeCompletions.TryRemove(node.Id, out var completion))
                {
                    completion.SetResult(true);
                }
            }
            catch (Exception ex)
            {
                nodeStopwatch.Stop();
                node.ExecutionEndTime = DateTimeOffset.UtcNow;
                node.ExecutionState = MetalNodeExecutionState.Failed;
                node.ExecutionError = ex;

                _logger.LogError(ex, "Failed to execute node '{NodeId}' of type {NodeType}", node.Id, node.Type);

                // Mark node as failed
                if (_nodeCompletions.TryRemove(node.Id, out var completion))
                {
                    completion.SetException(ex);
                }

                throw;
            }
        }
        finally
        {
            _ = _executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits for all dependencies of a node to complete.
    /// </summary>
    /// <param name="node">The node whose dependencies to wait for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when all dependencies are finished.</returns>
    public async Task WaitForDependenciesAsync(MetalGraphNode node, CancellationToken cancellationToken = default)
    {
        if (node.Dependencies.Count == 0)
        {
            return;
        }


        var dependencyTasks = node.Dependencies
            .Select(dep => _nodeCompletions.TryGetValue(dep.Id, out var tcs) ? tcs.Task : Task.CompletedTask)
            .ToArray();

        if (dependencyTasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(dependencyTasks).WaitAsync(cancellationToken);
                _logger.LogTrace("All dependencies completed for node '{NodeId}'", node.Id);
            }
            catch (TaskCanceledException)
            {
                _logger.LogDebug("Dependency wait cancelled for node '{NodeId}'", node.Id);
                throw;
            }
        }
    }

    #region Private Implementation

    private static async Task<List<string>> ValidateGraphAsync(MetalComputeGraph graph, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        await Task.Run(() =>
        {
            // Validate each node
            foreach (var node in graph.Nodes)
            {
                var nodeErrors = node.Validate();
                errors.AddRange(nodeErrors.Select(error => $"Node {node.Id}: {error}"));
            }

            // Validate resource requirements
            var totalMemoryRequired = graph.EstimatedMemoryFootprint;
            if (totalMemoryRequired > GetAvailableMetalMemory())
            {
                errors.Add($"Graph requires {totalMemoryRequired:N0} bytes but only {GetAvailableMetalMemory():N0} bytes available");
            }

            // Validate execution order is possible
            try
            {
                var executionOrder = graph.GetExecutionOrder();
                if (executionOrder.Count != graph.NodeCount)
                {
                    errors.Add("Graph execution order does not include all nodes");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to determine execution order: {ex.Message}");
            }

        }, cancellationToken);

        return errors;
    }

    private async Task ExecuteGraphInternalAsync(GraphExecutionContext context)
    {
        var nodes = context.Graph.GetExecutionOrder();

        // Initialize completion sources for all nodes

        foreach (var node in nodes)
        {
            _nodeCompletions[node.Id] = new TaskCompletionSource<bool>();
        }

        // Group nodes by execution level for parallel execution
        var executionLevels = GroupNodesByExecutionLevel(nodes);

        _logger.LogDebug("Executing graph with {LevelCount} execution levels", executionLevels.Count);

        // Execute each level
        for (var levelIndex = 0; levelIndex < executionLevels.Count; levelIndex++)
        {
            var level = executionLevels[levelIndex];
            _logger.LogTrace("Starting execution level {LevelIndex} with {NodeCount} nodes", levelIndex, level.Count);

            // Execute all nodes in this level in parallel
            var levelTasks = level.Select(node => ExecuteNodeWithDependenciesAsync(node, context)).ToArray();


            try
            {
                await Task.WhenAll(levelTasks);
                _logger.LogTrace("Completed execution level {LevelIndex}", levelIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute level {LevelIndex}", levelIndex);
                throw;
            }
        }
    }

    private async Task ExecuteNodeWithDependenciesAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        // Wait for dependencies first
        await WaitForDependenciesAsync(node, context.CancellationToken);

        // Then execute the node

        await ExecuteNodeAsync(node, context);
    }

    private async Task ExecuteNodeByTypeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        switch (node.Type)
        {
            case MetalNodeType.Kernel:
                await ExecuteKernelNodeAsync(node, context);
                break;

            case MetalNodeType.MemoryCopy:
                await ExecuteMemoryCopyNodeAsync(node, context);
                break;

            case MetalNodeType.MemorySet:
                await ExecuteMemorySetNodeAsync(node, context);
                break;

            case MetalNodeType.Barrier:
                await ExecuteBarrierNodeAsync(node, context);
                break;

            default:
                throw new NotSupportedException($"Node type {node.Type} is not supported");
        }
    }

    private async Task ExecuteKernelNodeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        if (node.Kernel == null)
        {
            throw new InvalidOperationException($"Kernel node {node.Id} has no kernel assigned");
        }

        // Create command buffer
        var commandBuffer = CreateMetalCommandBuffer(context.CommandQueue);
        context.CommandBuffersUsed++;

        try
        {
            // Create compute command encoder
            var computeEncoder = CreateMetalComputeCommandEncoder(commandBuffer);

            // Set compute pipeline state
            SetMetalComputePipelineState(computeEncoder, node.Kernel);

            // Set kernel arguments
            for (var i = 0; i < node.Arguments.Length; i++)
            {
                SetMetalKernelArgument(computeEncoder, i, node.Arguments[i]);
            }

            // Dispatch threadgroups
            DispatchMetalThreadgroups(computeEncoder, node.ThreadgroupsPerGrid, node.ThreadsPerThreadgroup);

            // End encoding
            EndMetalCommandEncoding(computeEncoder);

            // Commit and wait
            await CommitAndWaitMetalCommandBufferAsync(commandBuffer);

            _logger.LogTrace("Executed kernel node '{NodeId}' with {ThreadgroupCount} threadgroups",

                node.Id, node.ThreadgroupsPerGrid.TotalElements);
        }
        finally
        {
            // Clean up native resources
            ReleaseMetalCommandBuffer(commandBuffer);
        }
    }

    private async Task ExecuteMemoryCopyNodeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        if (node.SourceBuffer == IntPtr.Zero || node.DestinationBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Memory copy node {node.Id} has invalid buffer pointers");
        }

        // Create command buffer
        var commandBuffer = CreateMetalCommandBuffer(context.CommandQueue);
        context.CommandBuffersUsed++;

        try
        {
            // Create blit command encoder
            var blitEncoder = CreateMetalBlitCommandEncoder(commandBuffer);

            // Copy buffer
            CopyMetalBuffer(blitEncoder, node.SourceBuffer, 0, node.DestinationBuffer, 0, node.CopySize);

            // End encoding
            EndMetalCommandEncoding(blitEncoder);

            // Commit and wait
            await CommitAndWaitMetalCommandBufferAsync(commandBuffer);

            context.TotalMemoryTransferred += node.CopySize;

            _logger.LogTrace("Executed memory copy node '{NodeId}' - {ByteCount:N0} bytes",

                node.Id, node.CopySize);
        }
        finally
        {
            ReleaseMetalCommandBuffer(commandBuffer);
        }
    }

    private async Task ExecuteMemorySetNodeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        if (node.DestinationBuffer == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Memory set node {node.Id} has invalid buffer pointer");
        }

        // Create command buffer
        var commandBuffer = CreateMetalCommandBuffer(context.CommandQueue);
        context.CommandBuffersUsed++;

        try
        {
            // Create blit command encoder
            var blitEncoder = CreateMetalBlitCommandEncoder(commandBuffer);

            // Fill buffer
            FillMetalBuffer(blitEncoder, node.DestinationBuffer, node.FillValue, node.CopySize);

            // End encoding
            EndMetalCommandEncoding(blitEncoder);

            // Commit and wait
            await CommitAndWaitMetalCommandBufferAsync(commandBuffer);

            context.TotalMemoryTransferred += node.CopySize;

            _logger.LogTrace("Executed memory set node '{NodeId}' - {ByteCount:N0} bytes with value {FillValue}",

                node.Id, node.CopySize, node.FillValue);
        }
        finally
        {
            ReleaseMetalCommandBuffer(commandBuffer);
        }
    }

    private async Task ExecuteBarrierNodeAsync(MetalGraphNode node, GraphExecutionContext context)
    {
        // Barrier nodes don't perform actual work, they just ensure dependencies are met
        // The dependency waiting is already handled in ExecuteNodeWithDependenciesAsync
        await Task.Delay(1, context.CancellationToken); // Minimal delay to yield execution

        _logger.LogTrace("Executed barrier node '{NodeId}'", node.Id);
    }

    private static List<List<MetalGraphNode>> GroupNodesByExecutionLevel(IReadOnlyList<MetalGraphNode> nodes)
    {
        var levels = new List<List<MetalGraphNode>>();
        var nodeToLevel = new Dictionary<string, int>();
        var processed = new HashSet<string>();

        // Calculate execution level for each node
        foreach (var node in nodes)
        {
            _ = CalculateExecutionLevel(node, nodeToLevel, processed);
        }

        // Group nodes by level
        var levelGroups = nodeToLevel.GroupBy(kvp => kvp.Value)
                                   .OrderBy(group => group.Key)
                                   .ToList();

        foreach (var levelGroup in levelGroups)
        {
            var levelNodes = levelGroup.Select(kvp => nodes.First(n => n.Id == kvp.Key)).ToList();
            levels.Add(levelNodes);
        }

        return levels;
    }

    private static int CalculateExecutionLevel(MetalGraphNode node, Dictionary<string, int> nodeToLevel, HashSet<string> processed)
    {
        if (nodeToLevel.TryGetValue(node.Id, out var existingLevel))
        {
            return existingLevel;
        }

        if (processed.Contains(node.Id))
        {
            throw new InvalidOperationException($"Circular dependency detected involving node {node.Id}");
        }

        _ = processed.Add(node.Id);

        var maxDependencyLevel = -1;
        foreach (var dependency in node.Dependencies)
        {
            var depLevel = CalculateExecutionLevel(dependency, nodeToLevel, processed);
            maxDependencyLevel = Math.Max(maxDependencyLevel, depLevel);
        }

        var level = maxDependencyLevel + 1;
        nodeToLevel[node.Id] = level;
        _ = processed.Remove(node.Id);

        return level;
    }

    private static double CalculateParallelEfficiency(GraphExecutionContext context)
    {
        if (context.NodesExecuted == 0)
        {
            return 0.0;
        }

        // Simplified efficiency calculation

        var idealParallelTime = context.TotalGpuTimeMs / Environment.ProcessorCount;
        var actualTime = context.TotalGpuTimeMs;


        return Math.Min(1.0, idealParallelTime / actualTime);
    }

    private static double CalculateMemoryBandwidth(GraphExecutionContext context)
    {
        if (context.TotalGpuTimeMs == 0)
        {
            return 0.0;
        }


        var totalMemoryGB = context.TotalMemoryTransferred / (1024.0 * 1024.0 * 1024.0);
        var timeInSeconds = context.TotalGpuTimeMs / 1000.0;


        return totalMemoryGB / timeInSeconds; // GB/s
    }

    private static long GetAvailableMetalMemory()
        // This would query the actual Metal device memory
        // For now, return a reasonable default (8GB for Apple Silicon)
        => 8L * 1024 * 1024 * 1024;

    #endregion

    #region Metal Native Method Stubs

    // These methods would be implemented using Metal native bindings
    // For now, they are stubs that simulate the actual Metal operations

    private static IntPtr CreateMetalCommandBuffer(IntPtr commandQueue)
        // Create Metal command buffer from command queue
        => new(0x1000); // Stub

    private static IntPtr CreateMetalComputeCommandEncoder(IntPtr commandBuffer)
        // Create compute command encoder
        => new(0x2000); // Stub

    private static IntPtr CreateMetalBlitCommandEncoder(IntPtr commandBuffer)
        // Create blit command encoder
        => new(0x3000); // Stub

    private static void SetMetalComputePipelineState(IntPtr encoder, object kernel)
    {
        // Set the compute pipeline state
    }

    private static void SetMetalKernelArgument(IntPtr encoder, int index, object argument)
    {
        // Set kernel argument at index
    }

    private static void DispatchMetalThreadgroups(IntPtr encoder, MTLSize threadgroupsPerGrid, MTLSize threadsPerThreadgroup)
    {
        // Dispatch compute threadgroups
    }

    private static void CopyMetalBuffer(IntPtr encoder, IntPtr sourceBuffer, long sourceOffset, IntPtr destBuffer, long destOffset, long size)
    {
        // Copy between Metal buffers
    }

    private static void FillMetalBuffer(IntPtr encoder, IntPtr buffer, byte value, long size)
    {
        // Fill Metal buffer with value
    }

    private static void EndMetalCommandEncoding(IntPtr encoder)
    {
        // End command encoding
    }

    private static async Task CommitAndWaitMetalCommandBufferAsync(IntPtr commandBuffer)
        // Commit command buffer and wait for completion
        => await Task.Delay(1); // Simulate GPU execution time

    private static void ReleaseMetalCommandBuffer(IntPtr commandBuffer)
    {
        // Release command buffer resources
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Releases all resources used by the Metal graph executor.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        try
        {
            _cancellationTokenSource.Cancel();

            // Complete any pending node tasks

            foreach (var completion in _nodeCompletions.Values)
            {
                _ = completion.TrySetCanceled();
            }
            _nodeCompletions.Clear();

            _executionSemaphore.Dispose();
            _cancellationTokenSource.Dispose();

            _logger.LogDebug("Disposed MetalGraphExecutor");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during MetalGraphExecutor disposal");
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
/// Represents the execution context for a Metal compute graph.
/// </summary>
public class GraphExecutionContext(string executionId, MetalComputeGraph graph, IntPtr commandQueue, CancellationToken cancellationToken)
{
    public string ExecutionId { get; } = executionId;
    public MetalComputeGraph Graph { get; } = graph;
    public IntPtr CommandQueue { get; } = commandQueue;
    public CancellationToken CancellationToken { get; } = cancellationToken;


    public int NodesExecuted { get; set; }
    public int CommandBuffersUsed { get; set; }
    public double TotalGpuTimeMs { get; set; }
    public long TotalMemoryTransferred { get; set; }
}