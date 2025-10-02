// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Execution;

/// <summary>
/// Contains profiling data collected during execution.
/// </summary>
public class ExecutionProfilingData
{
    /// <summary>
    /// Gets or sets the execution identifier.
    /// </summary>
    /// <value>The execution id.</value>
    public Guid ExecutionId { get; set; }
    /// <summary>
    /// Gets or sets the strategy.
    /// </summary>
    /// <value>The strategy.</value>
    public ExecutionStrategyType Strategy { get; set; }
    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    /// <value>The start time.</value>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    /// <value>The end time.</value>
    public DateTimeOffset EndTime { get; set; }
    /// <summary>
    /// Gets or sets the total duration.
    /// </summary>
    /// <value>The total duration.</value>
    public TimeSpan TotalDuration { get; set; }
    /// <summary>
    /// Gets or sets the events.
    /// </summary>
    /// <value>The events.</value>
    public IList<ProfilingEvent> Events { get; } = [];
}
