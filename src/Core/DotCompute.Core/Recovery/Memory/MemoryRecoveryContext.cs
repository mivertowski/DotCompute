// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Memory;

/// <summary>
/// Provides context information for memory recovery operations, including operation details,
/// resource requirements, and metadata for tracking and debugging purposes.
/// </summary>
/// <remarks>
/// This class encapsulates the context needed during memory recovery processes,
/// allowing for better tracking, debugging, and optimization of memory operations.
/// The metadata dictionary can be used to store custom information specific to
/// the recovery operation being performed.
/// </remarks>
public class MemoryRecoveryContext
{
    /// <summary>
    /// Gets or sets the name or description of the operation being performed.
    /// </summary>
    /// <value>
    /// A string describing the current operation. Defaults to an empty string.
    /// </value>
    /// <example>
    /// context.Operation = "Large object allocation retry";
    /// </example>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of bytes requested for allocation.
    /// </summary>
    /// <value>
    /// The size in bytes of the memory allocation request.
    /// </value>
    /// <remarks>
    /// This value is used to determine the appropriate recovery strategy
    /// and to track memory usage patterns.
    /// </remarks>
    public long RequestedBytes { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the memory pool involved in the operation.
    /// </summary>
    /// <value>
    /// The pool identifier, or null if no specific pool is involved.
    /// </value>
    /// <remarks>
    /// When specified, this allows the recovery system to target specific
    /// memory pools for cleanup or defragmentation operations.
    /// </remarks>
    public string? PoolId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata associated with the recovery operation.
    /// </summary>
    /// <value>
    /// A dictionary containing key-value pairs of custom metadata.
    /// </value>
    /// <remarks>
    /// This collection can be used to store operation-specific information
    /// such as timing data, caller information, or debugging details.
    /// The dictionary is initialized to an empty collection.
    /// </remarks>
    /// <example>
    /// context.Metadata["caller"] = "DataProcessor.ProcessLargeDataset";
    /// context.Metadata["attempt"] = 2;
    /// </example>
    public Dictionary<string, object> Metadata { get; set; } = [];
}