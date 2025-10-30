// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Pipelines.Enums;

/// <summary>
/// Defines the supported export formats for pipeline metrics data.
/// Each format enables integration with different monitoring and analysis tools.
/// </summary>
public enum MetricsExportFormat
{
    /// <summary>
    /// JavaScript Object Notation (JSON) format.
    /// Widely supported structured data format suitable for web applications and APIs.
    /// </summary>
    Json,

    /// <summary>
    /// Comma-Separated Values (CSV) format.
    /// Simple tabular format suitable for spreadsheet applications and data analysis tools.
    /// </summary>
    Csv,

    /// <summary>
    /// Prometheus exposition format.
    /// Industry-standard format for metrics collection and monitoring systems.
    /// </summary>
    Prometheus,

    /// <summary>
    /// OpenTelemetry format.
    /// Modern observability standard for distributed tracing and metrics.
    /// </summary>
    OpenTelemetry
}
