// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Validation;

namespace DotCompute.Abstractions.Debugging;

/// <summary>
/// Defines the format for debug reports.
/// </summary>
public enum ReportFormat
{
    /// <summary>
    /// Markdown format for human-readable reports.
    /// </summary>
    Markdown,

    /// <summary>
    /// HTML format for web display.
    /// </summary>
    Html,

    /// <summary>
    /// JSON format for programmatic consumption.
    /// </summary>
    Json,

    /// <summary>
    /// Plain text format for simple output.
    /// </summary>
    PlainText,

    /// <summary>
    /// XML format for structured data.
    /// </summary>
    Xml,

    /// <summary>
    /// CSV format for tabular data.
    /// </summary>
    Csv
}

/// <summary>
/// Represents debug data collected during kernel analysis.
/// </summary>
public sealed class DebugData
{
    /// <summary>
    /// Gets the name of the kernel being debugged.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets when this debug data was collected.
    /// </summary>
    public DateTime CollectedAt { get; init; }

    /// <summary>
    /// Gets the cross-validation results.
    /// </summary>
    public CrossValidationResult? CrossValidationResult { get; init; }

    /// <summary>
    /// Gets the performance analysis results.
    /// </summary>
    public PerformanceAnalysis? PerformanceAnalysis { get; init; }

    /// <summary>
    /// Gets the determinism test results.
    /// </summary>
    public DeterminismTestResult? DeterminismResult { get; init; }

    /// <summary>
    /// Gets the memory analysis results.
    /// </summary>
    public MemoryPatternAnalysis? MemoryAnalysis { get; init; }

    /// <summary>
    /// Gets the execution history for this kernel.
    /// </summary>
    public IReadOnlyList<KernelExecutionResult> ExecutionHistory { get; init; } = [];

    /// <summary>
    /// Gets the metrics data collected.
    /// </summary>
    public MetricsReport? MetricsData { get; init; }

    /// <summary>
    /// Gets additional debug information.
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; init; } = [];
}

/// <summary>
/// Represents a comprehensive debug report.
/// </summary>
public sealed class DebugReport
{
    /// <summary>
    /// Gets the name of the kernel this report covers.
    /// </summary>
    public string KernelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Gets the format of this report.
    /// </summary>
    public ReportFormat Format { get; init; }

    /// <summary>
    /// Gets the main content of the report.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Gets the executive summary of findings.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Gets recommendations based on the analysis.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = [];

    /// <summary>
    /// Gets the severity level of issues found.
    /// </summary>
    public ValidationSeverity OverallSeverity { get; init; }

    /// <summary>
    /// Gets performance ratings and scores.
    /// </summary>
    public Dictionary<string, double> Scores { get; init; } = [];

    /// <summary>
    /// Gets attachments or additional files.
    /// </summary>
    public IReadOnlyList<ReportAttachment> Attachments { get; init; } = [];
}

/// <summary>
/// Represents an attachment to a debug report.
/// </summary>
public sealed class ReportAttachment
{
    /// <summary>
    /// Gets the attachment filename.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the MIME type of the attachment.
    /// </summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the attachment content.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; init; } = ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Gets the description of the attachment.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Represents configuration for debug operations.
/// </summary>
public sealed class DebugConfiguration
{
    /// <summary>
    /// Gets whether verbose logging is enabled.
    /// </summary>
    public bool VerboseLogging { get; init; }

    /// <summary>
    /// Gets the timeout for debug operations.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the maximum number of test iterations.
    /// </summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// Gets the memory usage threshold for warnings.
    /// </summary>
    public long MemoryThresholdBytes { get; init; } = 1024 * 1024 * 100; // 100MB

    /// <summary>
    /// Gets the performance threshold for warnings.
    /// </summary>
    public TimeSpan PerformanceThreshold { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets whether to include detailed stack traces.
    /// </summary>
    public bool IncludeStackTraces { get; init; } = true;

    /// <summary>
    /// Gets whether to perform cross-validation by default.
    /// </summary>
    public bool EnableCrossValidation { get; init; } = true;

    /// <summary>
    /// Gets whether to collect performance metrics.
    /// </summary>
    public bool CollectMetrics { get; init; } = true;

    /// <summary>
    /// Gets custom debug settings.
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; init; } = [];
}
