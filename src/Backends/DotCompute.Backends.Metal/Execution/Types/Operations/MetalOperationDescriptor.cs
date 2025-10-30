// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Operations;

/// <summary>
/// Base class for Metal operation descriptors
/// </summary>
public abstract class MetalOperationDescriptor
{
    /// <summary>
    /// Unique identifier for the operation
    /// </summary>
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Human-readable name for the operation
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Operation priority
    /// </summary>
    public MetalOperationPriority Priority { get; set; } = MetalOperationPriority.Normal;

    /// <summary>
    /// Dependencies on other operations
    /// </summary>
    public IList<string> Dependencies { get; } = [];

    /// <summary>
    /// Expected resource usage
    /// </summary>
    public Dictionary<MetalResourceType, long> ResourceUsage { get; } = [];

    /// <summary>
    /// Estimated execution time
    /// </summary>
    public TimeSpan? EstimatedDuration { get; set; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = [];
}

/// <summary>
/// Placeholder for MetalOperationPriority enum
/// TODO: Extract from MetalCommandStream.cs or other file
/// </summary>
public enum MetalOperationPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>
/// Placeholder for MetalResourceType enum
/// TODO: Extract from MetalExecutionContext.cs or other file
/// </summary>
public enum MetalResourceType
{
    Buffer,
    Texture,
    CommandBuffer,
    ComputeState,
    Event
}
