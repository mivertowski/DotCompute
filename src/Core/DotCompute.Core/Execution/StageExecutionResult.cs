// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a pipeline stage.
/// </summary>
public class StageExecutionResult
{
    public int StageId { get; set; }
    public int MicrobatchIndex { get; set; }
    public required string DeviceId { get; set; }
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public required string StageName { get; set; }
    public string? ErrorMessage { get; set; }
}
