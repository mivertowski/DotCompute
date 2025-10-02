// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Types;

namespace DotCompute.Core.Execution;

/// <summary>
/// Contains profiling data collected during execution.
/// </summary>
public class ExecutionProfilingData
{
    public Guid ExecutionId { get; set; }
    public ExecutionStrategyType Strategy { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<ProfilingEvent> Events { get; set; } = [];
}
