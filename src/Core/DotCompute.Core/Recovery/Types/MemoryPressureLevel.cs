// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Recovery.Types;

/// <summary>
/// Defines the classification levels for system memory pressure, indicating
/// the severity of memory usage and the urgency of recovery actions needed.
/// </summary>
/// <remarks>
/// These levels provide a standardized way to categorize memory pressure and
/// trigger appropriate recovery strategies. The levels are typically determined
/// by comparing current memory usage against predefined thresholds, with each
/// level suggesting different response strategies and urgency levels.
/// </remarks>
public enum MemoryPressureLevel
{
    /// <summary>
    /// Indicates low memory pressure with abundant available memory.
    /// </summary>
    /// <remarks>
    /// Under low pressure conditions, the system has plenty of available memory
    /// and no immediate recovery actions are necessary. This is the normal
    /// operating state where memory allocations should succeed without issues.
    /// Typical threshold: Memory usage below 70% of total capacity.
    /// Recommended actions: Continue normal operations, optional background cleanup.
    /// </remarks>
    Low,

    /// <summary>
    /// Indicates moderate memory pressure with some memory constraints beginning to appear.
    /// </summary>
    /// <remarks>
    /// Medium pressure suggests that memory usage is elevated but not yet critical.
    /// The system should begin proactive memory management to prevent escalation
    /// to higher pressure levels. This is a good time to perform routine cleanup
    /// and optimization operations.
    /// Typical threshold: Memory usage between 70% and 85% of total capacity.
    /// Recommended actions: Initiate background cleanup, defer non-essential allocations.
    /// </remarks>
    Medium,

    /// <summary>
    /// Indicates high memory pressure with limited available memory.
    /// </summary>
    /// <remarks>
    /// High pressure conditions require immediate attention to prevent out-of-memory
    /// situations. The system should actively perform memory recovery operations
    /// and may need to reduce functionality or defer operations to conserve memory.
    /// Performance may be impacted by necessary recovery actions.
    /// Typical threshold: Memory usage between 85% and 95% of total capacity.
    /// Recommended actions: Aggressive cleanup, defragmentation, cache clearing.
    /// </remarks>
    High,

    /// <summary>
    /// Indicates critical memory pressure where immediate emergency action is required.
    /// </summary>
    /// <remarks>
    /// Critical pressure represents an emergency situation where the system is
    /// at risk of running out of memory completely. All available recovery
    /// measures should be employed immediately, including emergency reserves
    /// and drastic cleanup operations. Application functionality may be
    /// severely impacted to preserve system stability.
    /// Typical threshold: Memory usage above 95% of total capacity.
    /// Recommended actions: Emergency recovery, use emergency reserves, consider
    /// graceful degradation or shutdown of non-essential components.
    /// </remarks>
    Critical
}
