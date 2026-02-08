using System;

namespace DotCompute.Linq.Reactive;

/// <summary>
/// Interface for managing backpressure in streaming compute scenarios.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 7: Reactive Extensions Integration.
/// Backpressure management prevents memory exhaustion in fast producer scenarios.
/// </remarks>
public interface IBackpressureManager
{
    /// <summary>
    /// Applies a backpressure strategy to control flow rate.
    /// </summary>
    /// <param name="strategy">The backpressure strategy to apply.</param>
    public void ApplyBackpressure(BackpressureStrategy strategy);

    /// <summary>
    /// Gets the current backpressure state.
    /// </summary>
    /// <returns>The current backpressure state.</returns>
    public BackpressureState GetState();
}

/// <summary>
/// Defines strategies for handling backpressure in streaming scenarios.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 7: Reactive Extensions Integration.
/// </remarks>
public enum BackpressureStrategy
{
    /// <summary>
    /// Buffer elements until consumer can process them.
    /// </summary>
    Buffer = 0,

    /// <summary>
    /// Drop elements when buffer is full.
    /// </summary>
    Drop = 1,

    /// <summary>
    /// Drop the latest element when buffer is full.
    /// </summary>
    DropLatest = 2,

    /// <summary>
    /// Drop the oldest element when buffer is full.
    /// </summary>
    DropOldest = 3,

    /// <summary>
    /// Block producer until consumer catches up.
    /// </summary>
    Block = 4,

    /// <summary>
    /// Sample elements at regular intervals.
    /// </summary>
    Sample = 5
}

/// <summary>
/// Represents the current state of backpressure management.
/// </summary>
/// <remarks>
/// ⚠️ STUB - Phase 2: Test Infrastructure Foundation.
/// Full implementation in Phase 7: Reactive Extensions Integration.
/// </remarks>
public class BackpressureState
{
    /// <summary>
    /// Gets the number of items currently queued.
    /// </summary>
    public int QueuedItems { get; init; }

    /// <summary>
    /// Gets a value indicating whether the producer is currently blocked.
    /// </summary>
    public bool IsBlocked { get; init; }

    /// <summary>
    /// Gets the total number of dropped items.
    /// </summary>
    public int DroppedCount { get; init; }

    /// <summary>
    /// Gets the current buffer utilization percentage (0-100).
    /// </summary>
    public double BufferUtilization { get; init; }
}
