// <copyright file="MemoryRecoveryStrategyType.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Core.Recovery.Memory.Types;

/// <summary>
/// Defines memory recovery strategy types.
/// Different approaches to reclaiming memory when under pressure.
/// </summary>
public enum MemoryRecoveryStrategyType
{
    /// <summary>
    /// Simple garbage collection.
    /// Basic GC invocation to free unreferenced objects.
    /// </summary>
    SimpleGarbageCollection,

    /// <summary>
    /// Defragmentation with garbage collection.
    /// Combines memory defragmentation with GC for better results.
    /// </summary>
    DefragmentationWithGC,

    /// <summary>
    /// Aggressive cleanup.
    /// Forceful memory reclamation including LOH compaction.
    /// </summary>
    AggressiveCleanup,

    /// <summary>
    /// Emergency recovery.
    /// Last-resort recovery when system is critically low on memory.
    /// </summary>
    EmergencyRecovery
}