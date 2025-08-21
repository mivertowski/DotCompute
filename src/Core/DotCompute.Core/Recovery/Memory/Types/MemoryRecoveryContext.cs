// <copyright file="MemoryRecoveryContext.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Context information for memory recovery operations.
/// Provides details about the memory allocation request and its context.
/// </summary>
public class MemoryRecoveryContext
{
    /// <summary>
    /// Gets or sets the operation name.
    /// Identifies the operation requesting memory allocation.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the requested bytes.
    /// Amount of memory requested in bytes.
    /// </summary>
    public long RequestedBytes { get; set; }

    /// <summary>
    /// Gets or sets the pool identifier.
    /// Optional ID of the memory pool being used.
    /// </summary>
    public string? PoolId { get; set; }

    /// <summary>
    /// Gets or sets additional metadata.
    /// Extra context information for the recovery operation.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];
}