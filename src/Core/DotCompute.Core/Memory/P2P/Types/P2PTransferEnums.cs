// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Memory.P2P.Types;

/// <summary>
/// Status of a peer-to-peer transfer operation.
/// </summary>
/// <remarks>
/// <para>
/// Tracks the lifecycle state of P2P transfers from creation through completion or failure.
/// Used for monitoring, retry logic, and resource management.
/// </para>
/// </remarks>
public enum P2PTransferStatus
{
    /// <summary>Transfer is pending and not yet started.</summary>
    /// <remarks>Initial state after creation.</remarks>
    Pending,

    /// <summary>Transfer is being initialized.</summary>
    /// <remarks>Setting up transfer resources and validating parameters.</remarks>
    Initializing,

    /// <summary>Transfer is being planned.</summary>
    /// <remarks>Transfer plan is being created and optimized.</remarks>
    Planned,

    /// <summary>Transfer is currently in progress.</summary>
    /// <remarks>Active data movement between devices.</remarks>
    InProgress,

    /// <summary>Transfer is actively transferring data.</summary>
    /// <remarks>Data is being moved between devices.</remarks>
    Transferring,

    /// <summary>Transfer is being validated.</summary>
    /// <remarks>Data integrity checks in progress.</remarks>
    Validating,

    /// <summary>Transfer validation failed.</summary>
    /// <remarks>Data integrity check detected errors.</remarks>
    ValidationFailed,

    /// <summary>Transfer completed successfully.</summary>
    /// <remarks>All data transferred and validated if enabled.</remarks>
    Completed,

    /// <summary>Transfer failed with errors.</summary>
    /// <remarks>Check ErrorMessage for failure details.</remarks>
    Failed,

    /// <summary>Transfer was cancelled by user request.</summary>
    /// <remarks>Partial data may have been transferred.</remarks>
    Cancelled,

    /// <summary>Transfer is being optimized or scheduled.</summary>
    /// <remarks>Planning phase before actual data movement.</remarks>
    Optimizing
}

/// <summary>
/// Strategy for executing peer-to-peer transfers.
/// </summary>
/// <remarks>
/// <para>
/// Different strategies optimize for different objectives: bandwidth utilization,
/// latency, reliability, or specific hardware characteristics.
/// </para>
/// <para>
/// The manager automatically selects optimal strategy based on transfer characteristics
/// unless explicitly overridden.
/// </para>
/// </remarks>
public enum P2PTransferStrategy
{
    /// <summary>Direct peer-to-peer transfer between two devices.</summary>
    /// <remarks>Fastest for single source-destination pairs. Requires P2P support.</remarks>
    Direct,

    /// <summary>Direct peer-to-peer transfer (alias for Direct).</summary>
    /// <remarks>Fastest for single source-destination pairs. Requires P2P support.</remarks>
    DirectP2P = Direct,

    /// <summary>Staged transfer through intermediate buffers.</summary>
    /// <remarks>Fallback when direct P2P is unavailable. Uses host or intermediate GPU.</remarks>
    Staged,

    /// <summary>Pipelined transfer for large data.</summary>
    /// <remarks>Overlaps transfer and computation. Best for streaming workloads.</remarks>
    Pipelined,

    /// <summary>Pipelined P2P transfer (alias for Pipelined).</summary>
    /// <remarks>Overlaps transfer and computation. Best for streaming workloads.</remarks>
    PipelinedP2P = Pipelined,

    /// <summary>Batched transfer combining multiple operations.</summary>
    /// <remarks>Reduces overhead for many small transfers.</remarks>
    Batched,

    /// <summary>Chunked P2P transfer for large data.</summary>
    /// <remarks>Splits large transfers into manageable chunks.</remarks>
    ChunkedP2P,

    /// <summary>Host-mediated transfer via CPU memory.</summary>
    /// <remarks>Fallback when direct P2P is unavailable.</remarks>
    HostMediated,

    /// <summary>Optimized transfer using automatic strategy selection.</summary>
    /// <remarks>Default. Analyzer chooses best strategy based on hardware and data size.</remarks>
    Optimized
}

/// <summary>
/// Priority level for transfer scheduling and resource allocation.
/// </summary>
/// <remarks>
/// <para>
/// Higher priority transfers are scheduled earlier and may preempt lower priority transfers
/// when resource contention occurs.
/// </para>
/// <para>
/// Use judiciously to avoid priority inversion and starvation of normal priority transfers.
/// </para>
/// </remarks>
public enum P2PTransferPriority
{
    /// <summary>Low priority background transfers.</summary>
    /// <remarks>Scheduled when no higher priority work is pending.</remarks>
    Low,

    /// <summary>Normal priority for routine transfers.</summary>
    /// <remarks>Default priority. Provides fair scheduling.</remarks>
    Normal,

    /// <summary>High priority for time-sensitive transfers.</summary>
    /// <remarks>Scheduled before normal priority. Use for latency-critical operations.</remarks>
    High,

    /// <summary>Critical priority for urgent transfers.</summary>
    /// <remarks>
    /// Highest priority. May preempt ongoing transfers. Reserve for system-critical operations.
    /// </remarks>
    Critical
}
