// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Execution;

/// <summary>
/// Represents the result of executing a model layer in model parallel execution.
/// </summary>
public class LayerExecutionResult
{
    public int LayerId { get; set; }
    public required string DeviceId { get; set; }
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public long ComputeFLOPS { get; set; }
    public long MemoryUsageBytes { get; set; }
    public string? ErrorMessage { get; set; }
}
