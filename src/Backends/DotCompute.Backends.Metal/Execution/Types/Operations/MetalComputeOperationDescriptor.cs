// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Execution.Types.Operations;

/// <summary>
/// Descriptor for compute operations
/// </summary>
public sealed class MetalComputeOperationDescriptor : MetalOperationDescriptor
{
    /// <summary>
    /// Compute pipeline state to use
    /// </summary>
    public IntPtr PipelineState { get; set; }

    /// <summary>
    /// Thread group size
    /// </summary>
    public (int x, int y, int z) ThreadgroupSize { get; set; }

    /// <summary>
    /// Grid size for dispatch
    /// </summary>
    public (int x, int y, int z) GridSize { get; set; }

    /// <summary>
    /// Input buffers for the compute operation
    /// </summary>
    public IList<IntPtr> InputBuffers { get; } = [];

    /// <summary>
    /// Output buffers for the compute operation
    /// </summary>
    public IList<IntPtr> OutputBuffers { get; } = [];

    /// <summary>
    /// Constant parameters
    /// </summary>
    public Dictionary<int, object> Constants { get; } = [];
}