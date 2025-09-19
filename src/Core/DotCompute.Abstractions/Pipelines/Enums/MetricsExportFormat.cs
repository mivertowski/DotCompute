// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Pipelines.Enums;

/// <summary>
/// Specifies the format for exporting pipeline metrics and telemetry data.
/// </summary>
public enum MetricsExportFormat
{
    /// <summary>
    /// JavaScript Object Notation format for web applications and APIs.
    /// </summary>
    Json,

    /// <summary>
    /// Extensible Markup Language format for structured data exchange.
    /// </summary>
    Xml,

    /// <summary>
    /// Comma-Separated Values format for spreadsheet and data analysis tools.
    /// </summary>
    Csv,

    /// <summary>
    /// Human-readable plain text format for logs and debugging.
    /// </summary>
    Text,

    /// <summary>
    /// Binary format for efficient storage and transmission.
    /// </summary>
    Binary,

    /// <summary>
    /// Prometheus metrics format for monitoring and alerting systems.
    /// </summary>
    Prometheus,

    /// <summary>
    /// InfluxDB line protocol format for time-series databases.
    /// </summary>
    InfluxDB,

    /// <summary>
    /// StatsD format for real-time metrics aggregation.
    /// </summary>
    StatsD,

    /// <summary>
    /// OpenTelemetry format for distributed tracing and metrics.
    /// </summary>
    OpenTelemetry,

    /// <summary>
    /// Grafana-compatible format for visualization dashboards.
    /// </summary>
    Grafana,

    /// <summary>
    /// YAML format for configuration and human-readable data.
    /// </summary>
    Yaml,

    /// <summary>
    /// MessagePack format for efficient binary serialization.
    /// </summary>
    MessagePack,

    /// <summary>
    /// Protocol Buffers format for high-performance serialization.
    /// </summary>
    Protobuf,

    /// <summary>
    /// Apache Parquet format for analytical data processing.
    /// </summary>
    Parquet,

    /// <summary>
    /// HDF5 format for scientific computing and large datasets.
    /// </summary>
    HDF5
}