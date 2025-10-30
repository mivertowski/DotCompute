// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a pipeline stage.
/// </summary>
public class StageExecutionResult
{
    /// <summary>
    /// Gets or sets the stage identifier.
    /// </summary>
    /// <value>The stage id.</value>
    public int StageId { get; set; }
    /// <summary>
    /// Gets or sets the microbatch index.
    /// </summary>
    /// <value>The microbatch index.</value>
    public int MicrobatchIndex { get; set; }
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
    /// Gets or sets the stage name.
    /// </summary>
    /// <value>The stage name.</value>
    public required string StageName { get; set; }
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    /// <value>The error message.</value>
    public string? ErrorMessage { get; set; }
}
