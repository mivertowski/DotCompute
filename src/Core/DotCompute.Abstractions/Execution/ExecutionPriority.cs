// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Execution;

/// <summary>
/// Execution priority levels for compute operations and pipeline stages.
/// This is the canonical ExecutionPriority enum used across all projects.
/// </summary>
public enum ExecutionPriority
{
    /// <summary>
    /// Lowest priority for background operations that can be delayed.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Low priority for non-critical operations.
    /// </summary>
    Low = 1,

    /// <summary>
    /// Below normal priority for deferred operations.
    /// </summary>
    BelowNormal = 2,

    /// <summary>
    /// Normal priority for standard operations.
    /// This is the default priority level.
    /// </summary>
    Normal = 3,

    /// <summary>
    /// Above normal priority for important operations.
    /// </summary>
    AboveNormal = 4,

    /// <summary>
    /// High priority for important operations that should be processed quickly.
    /// </summary>
    High = 5,

    /// <summary>
    /// Highest priority for critical operations that must be processed immediately.
    /// </summary>
    Critical = 6,

    /// <summary>
    /// Real-time priority for time-sensitive operations with strict deadlines.
    /// Use with caution as this can starve other operations.
    /// </summary>
    RealTime = 7
}

/// <summary>
/// Extension methods for ExecutionPriority.
/// </summary>
public static class ExecutionPriorityExtensions
{
    /// <summary>
    /// Gets a human-readable description of the execution priority.
    /// </summary>
    /// <param name="priority">The execution priority.</param>
    /// <returns>A descriptive string.</returns>
    public static string GetDescription(this ExecutionPriority priority)
    {
        return priority switch
        {
            ExecutionPriority.Idle => "Idle - Background operations",
            ExecutionPriority.Low => "Low - Non-critical operations",
            ExecutionPriority.BelowNormal => "Below Normal - Deferred operations",
            ExecutionPriority.Normal => "Normal - Standard operations",
            ExecutionPriority.AboveNormal => "Above Normal - Important operations",
            ExecutionPriority.High => "High - Quick processing required",
            ExecutionPriority.Critical => "Critical - Immediate processing",
            ExecutionPriority.RealTime => "Real-time - Strict deadlines",
            _ => "Unknown priority level"
        };
    }

    /// <summary>
    /// Gets the numeric weight for priority scheduling.
    /// Higher values indicate higher priority.
    /// </summary>
    /// <param name="priority">The execution priority.</param>
    /// <returns>The numeric weight (0-100).</returns>
    public static int GetWeight(this ExecutionPriority priority)
    {
        return priority switch
        {
            ExecutionPriority.Idle => 0,
            ExecutionPriority.Low => 10,
            ExecutionPriority.BelowNormal => 25,
            ExecutionPriority.Normal => 50,
            ExecutionPriority.AboveNormal => 75,
            ExecutionPriority.High => 90,
            ExecutionPriority.Critical => 95,
            ExecutionPriority.RealTime => 100,
            _ => 50 // Default to normal
        };
    }

    /// <summary>
    /// Determines if this priority is higher than another priority.
    /// </summary>
    /// <param name="priority">The current priority.</param>
    /// <param name="other">The other priority to compare against.</param>
    /// <returns>True if this priority is higher than the other.</returns>
    public static bool IsHigherThan(this ExecutionPriority priority, ExecutionPriority other)
    {
        return priority > other;
    }

    /// <summary>
    /// Determines if this priority is real-time or critical.
    /// </summary>
    /// <param name="priority">The execution priority.</param>
    /// <returns>True if the priority is real-time or critical.</returns>
    public static bool IsUrgent(this ExecutionPriority priority)
    {
        return priority >= ExecutionPriority.Critical;
    }
}