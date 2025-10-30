// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the supported trace export formats for distributed tracing.
/// Each format represents a different tracing system or protocol.
/// </summary>
public enum TraceExportFormat
{
    /// <summary>
    /// OpenTelemetry format - industry standard for observability data.
    /// </summary>
    OpenTelemetry,

    /// <summary>
    /// Jaeger format - distributed tracing system by Uber.
    /// </summary>
    Jaeger,

    /// <summary>
    /// Zipkin format - distributed tracing system by Twitter.
    /// </summary>
    Zipkin,

    /// <summary>
    /// Custom format - application-specific trace format.
    /// </summary>
    Custom
}
