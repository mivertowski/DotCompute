// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Core.Execution.Types;

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a device task in parallel execution.
/// </summary>
public class DeviceTaskResult
{
    public int TaskIndex { get; set; }
    public required string DeviceId { get; set; }
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public int ElementsProcessed { get; set; }
    public double ThroughputGFLOPS { get; set; }
    public double MemoryBandwidthGBps { get; set; }
    public string? ErrorMessage { get; set; }
    public required ExecutionEvent CompletionEvent { get; set; }
}
