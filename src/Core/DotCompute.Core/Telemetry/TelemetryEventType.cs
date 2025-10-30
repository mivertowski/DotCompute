// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry;
/// <summary>
/// An telemetry event type enumeration.
/// </summary>

/// <summary>
/// Defines the types of telemetry events that can be recorded.
/// </summary>
public enum TelemetryEventType
{
    /// <summary>Pipeline execution started event.</summary>
    PipelineStarted,
    /// <summary>Pipeline execution completed event.</summary>
    PipelineCompleted,
    /// <summary>Pipeline stage started event.</summary>
    PipelineStageStarted,
    /// <summary>Pipeline stage completed event.</summary>
    PipelineStageCompleted,
    /// <summary>Kernel execution event.</summary>
    KernelExecuted,
    /// <summary>Memory allocation event.</summary>
    MemoryAllocated,
    /// <summary>Cache access event.</summary>
    CacheAccess,
    /// <summary>Error event.</summary>
    Error,
    /// <summary>Performance metric event.</summary>
    Performance
}
