// <copyright file="PerformanceExportFormat.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace DotCompute.Runtime.Services.Performance.Types;

/// <summary>
/// Defines the available formats for exporting performance data.
/// Used to specify the output format when exporting profiling results.
/// </summary>
public enum PerformanceExportFormat
{
    /// <summary>
    /// JSON format.
    /// Human-readable format suitable for web APIs and data interchange.
    /// </summary>
    Json,

    /// <summary>
    /// CSV format.
    /// Tabular format suitable for spreadsheet applications and data analysis.
    /// </summary>
    Csv,

    /// <summary>
    /// XML format.
    /// Structured format suitable for enterprise systems and configuration.
    /// </summary>
    Xml,

    /// <summary>
    /// Binary format.
    /// Compact binary format for efficient storage and transmission.
    /// </summary>
    Binary
}
