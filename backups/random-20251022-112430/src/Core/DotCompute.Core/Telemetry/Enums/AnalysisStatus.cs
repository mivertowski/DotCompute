// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Telemetry.Enums;

/// <summary>
/// Defines the possible status values for an analysis operation.
/// Indicates the current state or final outcome of the analysis process.
/// </summary>
public enum AnalysisStatus
{
    /// <summary>
    /// The analysis completed successfully with valid results.
    /// </summary>
    Success,

    /// <summary>
    /// No data was available for analysis.
    /// </summary>
    NoData,

    /// <summary>
    /// The analysis encountered an error and could not complete.
    /// </summary>
    Error,

    /// <summary>
    /// The analysis is currently in progress.
    /// </summary>
    InProgress
}