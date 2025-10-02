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
    PipelineStarted,
    PipelineCompleted,
    PipelineStageStarted,
    PipelineStageCompleted,
    KernelExecuted,
    MemoryAllocated,
    CacheAccess,
    Error,
    Performance
}