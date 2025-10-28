// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Debugging.Types;

/// <summary>
/// Comprehensive debug report combining all analysis types for kernel execution.
/// Provides a complete diagnostic overview including validation, performance, determinism, and memory analysis.
/// </summary>
public sealed class ComprehensiveDebugReport
{
    /// <summary>
    /// Gets or sets the kernel name analyzed.
    /// </summary>
    public string KernelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the kernel validation results.
    /// </summary>
    public KernelValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Gets or sets the performance analysis results.
    /// </summary>
    public PerformanceReport? PerformanceAnalysis { get; set; }

    /// <summary>
    /// Gets or sets the determinism test results.
    /// </summary>
    public DeterminismReport? DeterminismAnalysis { get; set; }

    /// <summary>
    /// Gets or sets the memory pattern analysis.
    /// </summary>
    public MemoryAnalysisReport? MemoryAnalysis { get; set; }

    /// <summary>
    /// Gets or sets the resource utilization analysis.
    /// </summary>
    public ResourceUtilizationReport? ResourceUtilization { get; set; }

    /// <summary>
    /// Gets or sets the executive summary of findings.
    /// </summary>
    public string ExecutiveSummary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the overall health score (0-100).
    /// Composite score based on all analysis dimensions.
    /// </summary>
    public double OverallHealthScore { get; set; }

    /// <summary>
    /// Gets or sets the overall validation severity.
    /// </summary>
    public ValidationSeverity OverallSeverity { get; set; }

    /// <summary>
    /// Gets or sets whether the kernel is production-ready.
    /// </summary>
    public bool IsProductionReady { get; set; }

    /// <summary>
    /// Gets or sets critical issues identified.
    /// </summary>
    public IList<string> CriticalIssues { get; init; } = [];

    /// <summary>
    /// Gets or sets warnings identified.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Gets or sets recommendations for improvement.
    /// </summary>
    public IList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets or sets the backend comparison results.
    /// </summary>
    public Dictionary<string, BackendAnalysisSummary> BackendComparisons { get; init; } = [];

    /// <summary>
    /// Gets or sets detected anomalies across all analyses.
    /// </summary>
    public IList<PerformanceAnomaly> Anomalies { get; init; } = [];

    /// <summary>
    /// Gets or sets the report format.
    /// </summary>
    public ReportFormat Format { get; set; } = ReportFormat.Json;

    /// <summary>
    /// Gets or sets additional metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Summary of backend-specific analysis results.
/// </summary>
public sealed class BackendAnalysisSummary
{
    /// <summary>
    /// Gets or sets the backend type.
    /// </summary>
    public string BackendType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the backend is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the execution success rate (0-1).
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average execution time.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the average memory usage in bytes.
    /// </summary>
    public long AverageMemoryUsage { get; set; }

    /// <summary>
    /// Gets or sets the performance score relative to other backends (0-100).
    /// </summary>
    public double PerformanceScore { get; set; }

    /// <summary>
    /// Gets or sets whether this is the recommended backend.
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// Gets or sets backend-specific issues.
    /// </summary>
    public IList<string> Issues { get; init; } = [];
}
