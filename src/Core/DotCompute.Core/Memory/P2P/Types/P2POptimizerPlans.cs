// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// Comprehensive P2P transfer plan with optimization parameters.
/// </summary>
/// <remarks>
/// <para>
/// Encapsulates all parameters for optimized P2P transfer execution including
/// strategy selection, chunking, pipelining, and performance estimates.
/// </para>
/// <para>
/// Plans are created by <see cref="P2POptimizer"/> based on device capabilities,
/// transfer size, and historical performance data.
/// </para>
/// </remarks>
public sealed class P2PTransferPlan
{
    /// <summary>
    /// Gets or sets the unique plan identifier.
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// Gets or sets the source device accelerator.
    /// </summary>
    public required IAccelerator SourceDevice { get; init; }

    /// <summary>
    /// Gets or sets the target device accelerator.
    /// </summary>
    public required IAccelerator TargetDevice { get; init; }

    /// <summary>
    /// Gets or sets the total transfer size in bytes.
    /// </summary>
    public required long TransferSize { get; init; }

    /// <summary>
    /// Gets or sets the P2P connection capability.
    /// </summary>
    public required P2PConnectionCapability Capability { get; init; }

    /// <summary>
    /// Gets or sets the selected transfer strategy.
    /// </summary>
    /// <remarks>
    /// Mutable to allow history-based adaptation before execution.
    /// </remarks>
    public P2PTransferStrategy Strategy { get; set; }

    /// <summary>
    /// Gets or sets the chunk size in bytes.
    /// </summary>
    public int ChunkSize { get; set; }

    /// <summary>
    /// Gets or sets the pipeline depth (number of concurrent operations).
    /// </summary>
    public int PipelineDepth { get; set; }

    /// <summary>
    /// Gets or sets the estimated transfer time in milliseconds.
    /// </summary>
    public required double EstimatedTransferTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the optimization score (0.0-1.0).
    /// </summary>
    /// <remarks>
    /// Higher scores indicate better expected performance.
    /// </remarks>
    public required double OptimizationScore { get; init; }

    /// <summary>
    /// Gets or sets when this plan was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// P2P scatter plan for one-to-many data distribution operations.
/// </summary>
/// <remarks>
/// <para>
/// Distributes data from a single source buffer to multiple destination buffers
/// across different GPUs. Optimizes chunk distribution and transfer ordering.
/// </para>
/// <para>
/// Common use case: Broadcasting model parameters in distributed training.
/// </para>
/// </remarks>
public sealed class P2PScatterPlan
{
    /// <summary>
    /// Gets or sets the unique plan identifier.
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// Gets or sets the source buffer (type-erased for genericity).
    /// </summary>
    public required object SourceBuffer { get; init; }

    /// <summary>
    /// Gets or sets the destination buffers (type-erased for genericity).
    /// </summary>
    public required IReadOnlyList<object> DestinationBuffers { get; init; }

    /// <summary>
    /// Gets or sets the transfer chunks defining the scatter operation.
    /// </summary>
    public required IList<P2PTransferChunk> Chunks { get; init; }

    /// <summary>
    /// Gets or sets the estimated total time in milliseconds.
    /// </summary>
    /// <remarks>
    /// For parallel scatters, this is the maximum chunk time (not the sum).
    /// </remarks>
    public double EstimatedTotalTimeMs { get; set; }

    /// <summary>
    /// Gets or sets when this plan was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// P2P gather plan for many-to-one data collection operations.
/// </summary>
/// <remarks>
/// <para>
/// Collects data from multiple source buffers across different GPUs into
/// a single destination buffer. Optimizes transfer ordering and aggregation.
/// </para>
/// <para>
/// Common use case: Collecting gradient updates in distributed training.
/// </para>
/// </remarks>
public sealed class P2PGatherPlan
{
    /// <summary>
    /// Gets or sets the unique plan identifier.
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// Gets or sets the source buffers (type-erased for genericity).
    /// </summary>
    public required IReadOnlyList<object> SourceBuffers { get; init; }

    /// <summary>
    /// Gets or sets the destination buffer (type-erased for genericity).
    /// </summary>
    public required object DestinationBuffer { get; init; }

    /// <summary>
    /// Gets or sets the transfer chunks defining the gather operation.
    /// </summary>
    public required IList<P2PTransferChunk> Chunks { get; init; }

    /// <summary>
    /// Gets or sets the estimated total time in milliseconds.
    /// </summary>
    /// <remarks>
    /// For parallel gathers, this is the maximum chunk time (not the sum).
    /// </remarks>
    public double EstimatedTotalTimeMs { get; set; }

    /// <summary>
    /// Gets or sets when this plan was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// P2P transfer chunk information for scatter/gather operations.
/// </summary>
/// <remarks>
/// Defines a single contiguous data transfer segment within a larger
/// scatter or gather operation.
/// </remarks>
public sealed class P2PTransferChunk
{
    /// <summary>
    /// Gets or sets the chunk identifier (sequential index).
    /// </summary>
    public required int ChunkId { get; init; }

    /// <summary>
    /// Gets or sets the source buffer offset in elements.
    /// </summary>
    public required int SourceOffset { get; init; }

    /// <summary>
    /// Gets or sets the destination buffer offset in elements.
    /// </summary>
    public required int DestinationOffset { get; init; }

    /// <summary>
    /// Gets or sets the number of elements to transfer.
    /// </summary>
    public required int ElementCount { get; init; }

    /// <summary>
    /// Gets or sets the estimated transfer time in milliseconds.
    /// </summary>
    public required double EstimatedTimeMs { get; set; }
}
