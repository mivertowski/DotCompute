// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Interfaces.Kernels;
using ICompiledKernel = DotCompute.Abstractions.Interfaces.Kernels.ICompiledKernel;
using DotCompute.Backends.Metal.Execution.Interfaces;
using DotCompute.Backends.Metal.Execution.Graph.Types;

namespace DotCompute.Backends.Metal.Execution.Graph.Nodes;

/// <summary>
/// Represents an individual computation node in a Metal compute graph.
/// Each node encapsulates a single operation (kernel, memory copy, barrier) with its dependencies and resource requirements.
/// </summary>
public sealed class MetalGraphNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetalGraphNode"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for this node.</param>
    /// <param name="type">The type of operation this node represents.</param>
    /// <exception cref="ArgumentException">Thrown when id is null or whitespace.</exception>
    public MetalGraphNode(string id, MetalNodeType type)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));
        }

        Id = id;
        Type = type;
        CreatedAt = DateTimeOffset.UtcNow;
        Dependencies = [];
        Arguments = [];
    }

    #region Properties

    /// <summary>
    /// Gets the unique identifier for this node.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the type of operation this node represents.
    /// </summary>
    public MetalNodeType Type { get; }

    /// <summary>
    /// Gets the timestamp when this node was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets the list of nodes that this node depends on.
    /// </summary>
    public List<MetalGraphNode> Dependencies { get; set; }

    /// <summary>
    /// Gets or sets the estimated memory usage of this node in bytes.
    /// </summary>
    public long EstimatedMemoryUsage { get; set; }

    #endregion

    #region Kernel-specific Properties

    /// <summary>
    /// Gets or sets the compiled Metal kernel for kernel nodes.
    /// </summary>
    public ICompiledKernel? Kernel { get; set; }

    /// <summary>
    /// Gets or sets the number of threadgroups to dispatch for kernel nodes.
    /// </summary>
    public MTLSize ThreadgroupsPerGrid { get; set; }

    /// <summary>
    /// Gets or sets the number of threads per threadgroup for kernel nodes.
    /// </summary>
    public MTLSize ThreadsPerThreadgroup { get; set; }

    /// <summary>
    /// Gets or sets the kernel arguments array.
    /// </summary>
    public object[] Arguments { get; set; }

    #endregion

    #region Memory Operation Properties

    /// <summary>
    /// Gets or sets the source buffer for memory operations.
    /// </summary>
    public IntPtr SourceBuffer { get; set; }

    /// <summary>
    /// Gets or sets the destination buffer for memory operations.
    /// </summary>
    public IntPtr DestinationBuffer { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes for memory operations.
    /// </summary>
    public long CopySize { get; set; }

    /// <summary>
    /// Gets or sets the fill value for memory set operations.
    /// </summary>
    public byte FillValue { get; set; }

    #endregion

    #region Execution State

    /// <summary>
    /// Gets or sets the current execution state of this node.
    /// </summary>
    public MetalNodeExecutionState ExecutionState { get; set; } = MetalNodeExecutionState.NotStarted;

    /// <summary>
    /// Gets or sets the timestamp when this node started execution.
    /// </summary>
    public DateTimeOffset? ExecutionStartTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this node completed execution.
    /// </summary>
    public DateTimeOffset? ExecutionEndTime { get; set; }

    /// <summary>
    /// Gets the execution duration, if the node has completed.
    /// </summary>
    public TimeSpan? ExecutionDuration
        => ExecutionStartTime.HasValue && ExecutionEndTime.HasValue
            ? ExecutionEndTime.Value - ExecutionStartTime.Value
            : null;

    /// <summary>
    /// Gets or sets any error that occurred during execution.
    /// </summary>
    public Exception? ExecutionError { get; set; }

    #endregion

    #region Resource Management

    /// <summary>
    /// Gets or sets custom user data associated with this node.
    /// </summary>
    public object? UserData { get; set; }

    /// <summary>
    /// Gets or sets Metal-specific resources associated with this node.
    /// </summary>
    public Dictionary<string, object> MetalResources { get; set; } = [];

    /// <summary>
    /// Gets or sets the Metal command encoder type required for this node.
    /// </summary>
    public MetalCommandEncoderType RequiredEncoderType { get; set; } = MetalCommandEncoderType.Compute;

    #endregion

    #region Optimization Metadata

    /// <summary>
    /// Gets or sets a value indicating whether this node can be fused with adjacent nodes.
    /// </summary>
    public bool CanBeFused { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this node's memory access patterns are optimized.
    /// </summary>
    public bool IsMemoryOptimized { get; set; }

    /// <summary>
    /// Gets or sets the priority level for this node's execution.
    /// </summary>
    public MetalNodePriority Priority { get; set; } = MetalNodePriority.Normal;

    /// <summary>
    /// Gets or sets optimization hints for this node.
    /// </summary>
    public MetalOptimizationHints OptimizationHints { get; set; } = MetalOptimizationHints.None;

    #endregion

    #region Methods

    /// <summary>
    /// Adds a dependency to this node.
    /// </summary>
    /// <param name="dependency">The node that this node depends on.</param>
    /// <exception cref="ArgumentNullException">Thrown when dependency is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when adding the dependency would create a cycle.</exception>
    public void AddDependency(MetalGraphNode dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        if (dependency.Id == Id)
        {
            throw new InvalidOperationException("A node cannot depend on itself.");
        }

        if (WouldCreateCycle(dependency))
        {
            throw new InvalidOperationException($"Adding dependency '{dependency.Id}' to node '{Id}' would create a circular dependency.");
        }

        if (!Dependencies.Any(d => d.Id == dependency.Id))
        {
            Dependencies.Add(dependency);
        }
    }

    /// <summary>
    /// Removes a dependency from this node.
    /// </summary>
    /// <param name="dependencyId">The ID of the dependency to remove.</param>
    /// <returns><c>true</c> if the dependency was found and removed; otherwise, <c>false</c>.</returns>
    public bool RemoveDependency(string dependencyId)
    {
        if (string.IsNullOrWhiteSpace(dependencyId))
        {
            return false;
        }


        var dependency = Dependencies.FirstOrDefault(d => d.Id == dependencyId);
        if (dependency != null)
        {
            return Dependencies.Remove(dependency);
        }

        return false;
    }

    /// <summary>
    /// Determines if this node has any dependencies.
    /// </summary>
    /// <returns><c>true</c> if the node has dependencies; otherwise, <c>false</c>.</returns>
    public bool HasDependencies() => Dependencies.Count > 0;

    /// <summary>
    /// Determines if this node depends directly on the specified node.
    /// </summary>
    /// <param name="nodeId">The ID of the node to check for dependency.</param>
    /// <returns><c>true</c> if this node depends on the specified node; otherwise, <c>false</c>.</returns>
    public bool DependsOn(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {

            return false;
        }


        return Dependencies.Any(d => d.Id == nodeId);
    }

    /// <summary>
    /// Gets all transitive dependencies of this node.
    /// </summary>
    /// <returns>A set of all nodes that this node transitively depends on.</returns>
    public HashSet<MetalGraphNode> GetTransitiveDependencies()
    {
        var result = new HashSet<MetalGraphNode>();
        var visited = new HashSet<string>();

        void CollectDependencies(MetalGraphNode node)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!visited.Contains(dep.Id))
                {
                    visited.Add(dep.Id);
                    result.Add(dep);
                    CollectDependencies(dep);
                }
            }
        }

        CollectDependencies(this);
        return result;
    }

    /// <summary>
    /// Validates this node's configuration and resource requirements.
    /// </summary>
    /// <returns>A list of validation errors, or an empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        switch (Type)
        {
            case MetalNodeType.Kernel:
                if (Kernel == null)
                {
                    errors.Add("Kernel node must have a valid kernel.");
                }

                if (ThreadgroupsPerGrid.width == 0 || ThreadgroupsPerGrid.height == 0 || ThreadgroupsPerGrid.depth == 0)
                {
                    errors.Add("Kernel node must have valid threadgroup dimensions.");
                }

                if (ThreadsPerThreadgroup.width == 0 || ThreadsPerThreadgroup.height == 0 || ThreadsPerThreadgroup.depth == 0)
                {
                    errors.Add("Kernel node must have valid threads per threadgroup dimensions.");
                }

                var totalThreadsPerThreadgroup = ThreadsPerThreadgroup.width * ThreadsPerThreadgroup.height * ThreadsPerThreadgroup.depth;
                if (totalThreadsPerThreadgroup > 1024)
                {
                    errors.Add($"Total threads per threadgroup ({totalThreadsPerThreadgroup}) exceeds Metal limit of 1024.");
                }

                break;

            case MetalNodeType.MemoryCopy:
                if (SourceBuffer == IntPtr.Zero)
                {
                    errors.Add("Memory copy node must have a valid source buffer.");
                }

                if (DestinationBuffer == IntPtr.Zero)
                {
                    errors.Add("Memory copy node must have a valid destination buffer.");
                }

                if (CopySize <= 0)
                {
                    errors.Add("Memory copy node must have a positive copy size.");
                }

                break;

            case MetalNodeType.MemorySet:
                if (DestinationBuffer == IntPtr.Zero)
                {
                    errors.Add("Memory set node must have a valid destination buffer.");
                }

                if (CopySize <= 0)
                {
                    errors.Add("Memory set node must have a positive size.");
                }

                break;
        }

        return errors;
    }

    /// <summary>
    /// Creates a shallow copy of this node with a new ID.
    /// </summary>
    /// <param name="newId">The ID for the cloned node.</param>
    /// <returns>A new node instance that is a copy of this node.</returns>
    public MetalGraphNode Clone(string newId)
    {
        if (string.IsNullOrWhiteSpace(newId))
        {
            throw new ArgumentException("New node ID cannot be null or empty.", nameof(newId));
        }

        var clone = new MetalGraphNode(newId, Type)
        {
            Kernel = Kernel,
            ThreadgroupsPerGrid = ThreadgroupsPerGrid,
            ThreadsPerThreadgroup = ThreadsPerThreadgroup,
            Arguments = Arguments.ToArray(),
            SourceBuffer = SourceBuffer,
            DestinationBuffer = DestinationBuffer,
            CopySize = CopySize,
            FillValue = FillValue,
            EstimatedMemoryUsage = EstimatedMemoryUsage,
            RequiredEncoderType = RequiredEncoderType,
            CanBeFused = CanBeFused,
            IsMemoryOptimized = IsMemoryOptimized,
            Priority = Priority,
            OptimizationHints = OptimizationHints,
            UserData = UserData
        };

        // Copy Metal resources
        foreach (var kvp in MetalResources)
        {
            clone.MetalResources[kvp.Key] = kvp.Value;
        }

        // Note: Dependencies are not copied to avoid circular references in cloning
        
        return clone;
    }

    /// <summary>
    /// Returns a string representation of this node.
    /// </summary>
    /// <returns>A string containing the node's type and ID.</returns>
    public override string ToString()
    {
        var state = ExecutionState == MetalNodeExecutionState.NotStarted ? "" : $" ({ExecutionState})";
        return $"{Type}[{Id}]{state}";
    }

    #endregion

    #region Private Helper Methods

    private bool WouldCreateCycle(MetalGraphNode newDependency)
    {
        // Check if adding newDependency would create a cycle
        // This happens if newDependency transitively depends on this node
        var transitiveDeps = newDependency.GetTransitiveDependencies();
        return transitiveDeps.Any(dep => dep.Id == Id);
    }

    #endregion
}

/// <summary>
/// Represents the execution state of a Metal graph node.
/// </summary>
public enum MetalNodeExecutionState
{
    /// <summary>Node has not started execution.</summary>
    NotStarted,
    
    /// <summary>Node is currently executing.</summary>
    Executing,
    
    /// <summary>Node has completed successfully.</summary>
    Completed,
    
    /// <summary>Node execution failed with an error.</summary>
    Failed,
    
    /// <summary>Node execution was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Represents the priority level for node execution.
/// </summary>
public enum MetalNodePriority
{
    /// <summary>Low priority - execute when resources are available.</summary>
    Low = 0,
    
    /// <summary>Normal priority - default execution priority.</summary>
    Normal = 1,
    
    /// <summary>High priority - execute with elevated priority.</summary>
    High = 2,
    
    /// <summary>Critical priority - execute as soon as possible.</summary>
    Critical = 3
}

/// <summary>
/// Represents optimization hints for Metal graph nodes.
/// </summary>
[Flags]
public enum MetalOptimizationHints
{
    /// <summary>No specific optimization hints.</summary>
    None = 0,
    
    /// <summary>Node can be optimized for memory bandwidth.</summary>
    MemoryBandwidthOptimized = 1 << 0,
    
    /// <summary>Node can be optimized for compute throughput.</summary>
    ComputeThroughputOptimized = 1 << 1,
    
    /// <summary>Node can benefit from kernel fusion.</summary>
    FusionCandidate = 1 << 2,
    
    /// <summary>Node should be executed with minimal latency.</summary>
    LowLatency = 1 << 3,
    
    /// <summary>Node has predictable memory access patterns.</summary>
    PredictableMemoryAccess = 1 << 4
}