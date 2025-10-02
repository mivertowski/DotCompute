// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a model layer in model parallel execution.
/// </summary>
public class LayerExecutionResult
{
    /// <summary>
    /// Gets or sets the layer identifier.
    /// </summary>
    /// <value>The layer id.</value>
    public int LayerId { get; set; }
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
    /// Gets or sets the compute f l o p s.
    /// </summary>
    /// <value>The compute f l o p s.</value>
    public long ComputeFLOPS { get; set; }
    /// <summary>
    /// Gets or sets the memory usage bytes.
    /// </summary>
    /// <value>The memory usage bytes.</value>
    public long MemoryUsageBytes { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
