// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Types;

/// <summary>
/// Defines the available memory recovery strategy types that can be employed
/// to handle memory pressure and allocation failures.
/// </summary>
/// <remarks>
/// These strategies represent different approaches to memory recovery, ranging from
/// lightweight garbage collection to aggressive emergency measures. The choice of
/// strategy should be based on the severity of memory pressure and the acceptable
/// impact on application performance.
/// </remarks>
public enum MemoryRecoveryStrategyType
{
    /// <summary>
    /// Performs a simple garbage collection without additional recovery measures.
    /// </summary>
    /// <remarks>
    /// This is the least invasive strategy, suitable for minor memory pressure situations.
    /// It triggers a standard garbage collection cycle to reclaim unused managed memory
    /// without performing defragmentation or other resource-intensive operations.
    /// Expected impact: Minimal performance impact, moderate memory recovery.
    /// </remarks>
    SimpleGarbageCollection,

    /// <summary>
    /// Combines garbage collection with memory defragmentation operations.
    /// </summary>
    /// <remarks>
    /// This strategy first performs garbage collection and then attempts to
    /// defragment memory pools to reduce fragmentation and improve allocation
    /// efficiency. It provides better memory recovery than simple GC but with
    /// higher performance cost.
    /// Expected impact: Moderate performance impact, good memory recovery.
    /// </remarks>
    DefragmentationWithGC,

    /// <summary>
    /// Performs comprehensive cleanup including cache clearing and resource reclamation.
    /// </summary>
    /// <remarks>
    /// This strategy goes beyond garbage collection to aggressively reclaim resources
    /// such as cached data, temporary buffers, and other reclaimable memory.
    /// It may temporarily impact performance but provides significant memory recovery.
    /// Expected impact: High performance impact, excellent memory recovery.
    /// </remarks>
    AggressiveCleanup,

    /// <summary>
    /// Implements emergency recovery measures as a last resort to prevent out-of-memory conditions.
    /// </summary>
    /// <remarks>
    /// This is the most aggressive strategy, used when the system is under critical
    /// memory pressure. It may include forceful resource deallocation, emergency
    /// reserve usage, and other drastic measures that can significantly impact
    /// application performance and functionality.
    /// Expected impact: Severe performance impact, maximum memory recovery.
    /// Use only when absolutely necessary to prevent application failure.
    /// </remarks>
    EmergencyRecovery
}