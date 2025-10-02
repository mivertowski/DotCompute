// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Overall performance statistics across all backends.
/// </summary>
public class OverallPerformanceStats
{
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double AverageExecutionTime { get; set; }
    public long TotalMemoryUsed { get; set; }
}