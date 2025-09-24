// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Core.Debugging.Types;

/// <summary>
/// Execution statistics for a kernel.
/// </summary>
public class ExecutionStatistics
{
    public string KernelName { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}