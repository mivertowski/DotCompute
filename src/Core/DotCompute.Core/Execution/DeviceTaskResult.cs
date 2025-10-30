// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a device task in parallel execution.
/// </summary>
public class DeviceTaskResult
{
    /// <summary>
    /// Gets or sets the task index.
    /// </summary>
    /// <value>The task index.</value>
    public int TaskIndex { get; set; }
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    /// <value>The device id.</value>
    public required string DeviceId { get; set; }
    /// <summary>
    /// Gets or sets the success.
    /// </summary>
    /// <value>The success.</value>
    public bool Success { get; set; }
    /// <summary>
    /// Gets or sets the execution time ms.
    /// </summary>
    /// <value>The execution time ms.</value>
    public double ExecutionTimeMs { get; set; }
    /// <summary>
    /// Gets or sets the elements processed.
    /// </summary>
    /// <value>The elements processed.</value>
    public int ElementsProcessed { get; set; }
    /// <summary>
    /// Gets or sets the throughput g f l o p s.
    /// </summary>
    /// <value>The throughput g f l o p s.</value>
    public double ThroughputGFLOPS { get; set; }
    /// <summary>
    /// Gets or sets the memory bandwidth g bps.
    /// </summary>
    /// <value>The memory bandwidth g bps.</value>
    public double MemoryBandwidthGBps { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Gets or sets the completion event.
    /// </summary>
    /// <value>The completion event.</value>
    public required ExecutionEvent CompletionEvent { get; set; }
}
