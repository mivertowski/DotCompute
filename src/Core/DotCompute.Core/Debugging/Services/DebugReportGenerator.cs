// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Debugging.Services;

/// <summary>
/// Generates comprehensive debug reports and documentation.
/// </summary>
public sealed partial class DebugReportGenerator(ILogger<DebugReportGenerator> logger, DebugServiceOptions? _options = null) : IDisposable
{
    private readonly ILogger<DebugReportGenerator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool _disposed;

    /// <summary>
    /// Generates a comprehensive debug report.
    /// </summary>
    /// <param name="debugData">Debug data to include in the report.</param>
    /// <param name="format">Report format.</param>
    /// <returns>Generated debug report.</returns>
    public DebugReport GenerateReport(DebugData debugData, ReportFormat format = ReportFormat.Markdown)
    {
        ArgumentNullException.ThrowIfNull(debugData);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogReportGenerationStarted(debugData.KernelName, format.ToString());

        var report = new DebugReport
        {
            KernelName = debugData.KernelName,
            GeneratedAt = DateTime.UtcNow,
            Format = format,
            Content = GenerateReportContent(debugData, format),
            Summary = GenerateExecutiveSummary(debugData),
            Recommendations = GenerateRecommendations(debugData)
        };

        LogReportGenerated(debugData.KernelName, report.Content.Length);

        return report;
    }

    /// <summary>
    /// Generates a cross-validation report.
    /// </summary>
    /// <param name="validationResult">Cross-validation results.</param>
    /// <param name="format">Report format.</param>
    /// <returns>Cross-validation report.</returns>
    public string GenerateCrossValidationReport(CrossValidationResult validationResult, ReportFormat format = ReportFormat.Markdown)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogCrossValidationReportStarted(validationResult.KernelName);

        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownCrossValidationReport(validationResult),
            ReportFormat.Html => GenerateHtmlCrossValidationReport(validationResult),
            ReportFormat.Json => GenerateJsonCrossValidationReport(validationResult),
            ReportFormat.PlainText => GeneratePlainTextCrossValidationReport(validationResult),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Generates a performance analysis report.
    /// </summary>
    /// <param name="performanceData">Performance analysis data.</param>
    /// <param name="format">Report format.</param>
    /// <returns>Performance analysis report.</returns>
    public string GeneratePerformanceReport(PerformanceAnalysis performanceData, ReportFormat format = ReportFormat.Markdown)
    {
        ArgumentNullException.ThrowIfNull(performanceData);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogPerformanceReportStarted(performanceData.KernelName);

        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownPerformanceReport(performanceData),
            ReportFormat.Html => GenerateHtmlPerformanceReport(performanceData),
            ReportFormat.Json => GenerateJsonPerformanceReport(performanceData),
            ReportFormat.PlainText => GeneratePlainTextPerformanceReport(performanceData),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Generates a determinism test report.
    /// </summary>
    /// <param name="determinismResult">Determinism test results.</param>
    /// <param name="format">Report format.</param>
    /// <returns>Determinism test report.</returns>
    public string GenerateDeterminismReport(DeterminismTestResult determinismResult, ReportFormat format = ReportFormat.Markdown)
    {
        ArgumentNullException.ThrowIfNull(determinismResult);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogDeterminismReportStarted(determinismResult.KernelName);

        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownDeterminismReport(determinismResult),
            ReportFormat.Html => GenerateHtmlDeterminismReport(determinismResult),
            ReportFormat.Json => GenerateJsonDeterminismReport(determinismResult),
            ReportFormat.PlainText => GeneratePlainTextDeterminismReport(determinismResult),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Generates a memory analysis report.
    /// </summary>
    /// <param name="memoryAnalysis">Memory analysis results.</param>
    /// <param name="format">Report format.</param>
    /// <returns>Memory analysis report.</returns>
    public string GenerateMemoryAnalysisReport(MemoryPatternAnalysis memoryAnalysis, ReportFormat format = ReportFormat.Markdown)
    {
        ArgumentNullException.ThrowIfNull(memoryAnalysis);
        ObjectDisposedException.ThrowIf(_disposed, this);

        LogMemoryReportStarted(memoryAnalysis.KernelName);

        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownMemoryReport(memoryAnalysis),
            ReportFormat.Html => GenerateHtmlMemoryReport(memoryAnalysis),
            ReportFormat.Json => GenerateJsonMemoryReport(memoryAnalysis),
            ReportFormat.PlainText => GeneratePlainTextMemoryReport(memoryAnalysis),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Saves a report to file.
    /// </summary>
    /// <param name="report">The report to save.</param>
    /// <param name="filePath">File path to save to.</param>
    /// <returns>Task representing the save operation.</returns>
    public async Task SaveReportAsync(DebugReport report, string filePath)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, report.Content).ConfigureAwait(false);
            LogReportSaved(report.KernelName, filePath);
        }
        catch (Exception ex)
        {
            LogReportSaveFailed(report.KernelName, filePath, ex.Message);
            throw;
        }
    }

    #region Private Report Generation Methods

    /// <summary>
    /// Generates report content based on format.
    /// </summary>
    private static string GenerateReportContent(DebugData debugData, ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Markdown => GenerateMarkdownReport(debugData),
            ReportFormat.Html => GenerateHtmlReport(debugData),
            ReportFormat.Json => GenerateJsonReport(debugData),
            ReportFormat.PlainText => GeneratePlainTextReport(debugData),
            _ => throw new ArgumentException($"Unsupported report format: {format}")
        };
    }

    /// <summary>
    /// Generates executive summary.
    /// </summary>
    private static string GenerateExecutiveSummary(DebugData debugData)
    {
        var summary = new StringBuilder();

        _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Debug Summary for Kernel: {0}", debugData.KernelName));
        _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Generated: {0:yyyy-MM-dd HH:mm:ss} UTC", DateTime.UtcNow));

        if (debugData.CrossValidationResult != null)
        {
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Cross-Validation: {0}", debugData.CrossValidationResult.IsValid ? "PASSED" : "FAILED"));
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Validation Issues: {0}", debugData.CrossValidationResult.ValidationIssues.Count));
        }

        if (debugData.PerformanceAnalysis != null)
        {
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Performance Analysis: {0} data points", debugData.PerformanceAnalysis.DataPoints));
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Average Execution Time: {0:F2} ms", debugData.PerformanceAnalysis.AverageExecutionTimeMs));
            // Note: SuccessRate not available on PerformanceAnalysis, skipping
        }

        if (debugData.DeterminismResult != null)
        {
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Determinism Test: {0}", debugData.DeterminismResult.IsDeterministic ? "PASSED" : "FAILED"));
        }

        if (debugData.MemoryAnalysis != null)
        {
            _ = summary.AppendLine(string.Format(CultureInfo.InvariantCulture, "Memory Analysis: {0}", debugData.MemoryAnalysis.IsMemorySafe ? "SAFE" : "ISSUES DETECTED"));
        }

        return summary.ToString();
    }

    /// <summary>
    /// Generates recommendations based on debug data.
    /// </summary>
    private static List<string> GenerateRecommendations(DebugData debugData)
    {
        var recommendations = new List<string>();

        // Add cross-validation recommendations
        if (debugData.CrossValidationResult != null && !debugData.CrossValidationResult.IsValid)
        {
            recommendations.Add("Cross-validation failed. Review implementation for consistency across accelerators.");
            recommendations.AddRange(debugData.CrossValidationResult.ValidationIssues
                .Where(i => i.Severity == ValidationSeverity.Error)
                .Select(i => $"Fix: {i.Message}"));
        }

        // Add performance recommendations
        if (debugData.PerformanceAnalysis != null)
        {
            if (debugData.PerformanceAnalysis.AverageExecutionTimeMs > 1000) // > 1 second
            {
                recommendations.Add("Consider optimizing for better performance (current average > 1s).");
            }

            // Note: SuccessRate not available on PerformanceAnalysis type
            // Skipping this check
            if (false) // Disabled: SuccessRate not available
            {
                recommendations.Add("Low success rate detected. Investigate and fix reliability issues.");
            }
        }

        // Add determinism recommendations
        if (debugData.DeterminismResult != null && !debugData.DeterminismResult.IsDeterministic)
        {
            recommendations.Add("Kernel is non-deterministic. Consider:");
            recommendations.AddRange(debugData.DeterminismResult.Issues);
        }

        // Add memory recommendations
        if (debugData.MemoryAnalysis != null && !debugData.MemoryAnalysis.IsMemorySafe)
        {
            recommendations.Add("Memory safety issues detected:");
            recommendations.AddRange(debugData.MemoryAnalysis.Recommendations);
        }

        return recommendations;
    }

    #endregion

    #region Markdown Report Generators

    private static string GenerateMarkdownReport(DebugData debugData)
    {
        var md = new StringBuilder();

        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Debug Report: {0}", debugData.KernelName));
        _ = md.AppendLine();
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Generated:** {0:yyyy-MM-dd HH:mm:ss} UTC", DateTime.UtcNow));
        _ = md.AppendLine();

        if (debugData.CrossValidationResult != null)
        {
            _ = md.AppendLine("## Cross-Validation Results");
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Status:** {0}", debugData.CrossValidationResult.IsValid ? "✅ PASSED" : "❌ FAILED"));
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Validation Time:** {0:yyyy-MM-dd HH:mm:ss}", debugData.CrossValidationResult.ValidationTime));
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Issues Found:** {0}", debugData.CrossValidationResult.ValidationIssues.Count));
            _ = md.AppendLine();
        }

        if (debugData.PerformanceAnalysis != null)
        {
            _ = md.AppendLine("## Performance Analysis");
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Data Points:** {0}", debugData.PerformanceAnalysis.DataPoints));
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Average Execution Time:** {0:F2} ms", debugData.PerformanceAnalysis.AverageExecutionTimeMs));
            // Note: SuccessRate not available on PerformanceAnalysis type
            _ = md.AppendLine();
        }

        return md.ToString();
    }

    private static string GenerateMarkdownCrossValidationReport(CrossValidationResult result)
    {
        var md = new StringBuilder();

        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Cross-Validation Report: {0}", result.KernelName));
        _ = md.AppendLine();
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Status:** {0}", result.IsValid ? "✅ PASSED" : "❌ FAILED"));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Validation Time:** {0:yyyy-MM-dd HH:mm:ss} UTC", result.ValidationTime));
        _ = md.AppendLine();

        _ = md.AppendLine("## Execution Results");
        foreach (var execResult in result.ExecutionResults)
        {
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **{0}** ({1}):", execResult.AcceleratorName, execResult.AcceleratorType));
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "  - Success: {0}", execResult.Success ? "✅" : "❌"));
            _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "  - Execution Time: {0:F2} ms", execResult.ExecutionTime.TotalMilliseconds));
            if (execResult.Error != null)
            {
                _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "  - Error: {0}", execResult.Error.Message));
            }
        }

        if (result.ValidationIssues.Any())
        {
            _ = md.AppendLine();
            _ = md.AppendLine("## Validation Issues");
            foreach (var issue in result.ValidationIssues)
            {
                _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **{0}:** {1}", issue.Severity, issue.Message));
                if (!string.IsNullOrEmpty(issue.Context))
                {
                    _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "  - Context: {0}", issue.Context));
                }
            }
        }

        return md.ToString();
    }

    private static string GenerateMarkdownPerformanceReport(PerformanceAnalysis performance)
    {
        var md = new StringBuilder();

        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Performance Analysis: {0}", performance.KernelName));
        _ = md.AppendLine();
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Analysis Time:** {0:yyyy-MM-dd HH:mm:ss} UTC", performance.AnalysisTime));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Data Points:** {0}", performance.DataPoints));
        _ = md.AppendLine();

        _ = md.AppendLine("## Execution Time Statistics");
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Average:** {0:F2} ms", performance.AverageExecutionTimeMs));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Minimum:** {0:F2} ms", performance.MinExecutionTimeMs));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Maximum:** {0:F2} ms", performance.MaxExecutionTimeMs));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Standard Deviation:** {0:F2} ms", performance.ExecutionTimeStdDev));
        _ = md.AppendLine();

        _ = md.AppendLine("## Memory Statistics");
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Average Usage:** {0:F0} bytes", performance.AverageMemoryUsage));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Peak Usage:** {0:F0} bytes", performance.PeakMemoryUsage));
        _ = md.AppendLine();

        _ = md.AppendLine("## Reliability");
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Data Points:** {0}", performance.DataPointCount));
        // Note: SuccessRate and TotalExecutions not available on PerformanceAnalysis type

        return md.ToString();
    }

    private static string GenerateMarkdownDeterminismReport(DeterminismTestResult result)
    {
        var md = new StringBuilder();

        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Determinism Test: {0}", result.KernelName));
        _ = md.AppendLine();
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Status:** {0}", result.IsDeterministic ? "✅ DETERMINISTIC" : "❌ NON-DETERMINISTIC"));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Accelerator:** {0}", result.AcceleratorType));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Iterations:** {0}", result.Iterations));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Test Time:** {0:yyyy-MM-dd HH:mm:ss} UTC", result.TestTime));
        _ = md.AppendLine();

        if (result.Issues.Any())
        {
            _ = md.AppendLine("## Issues and Recommendations");
            foreach (var issue in result.Issues)
            {
                _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- {0}", issue));
            }
        }

        return md.ToString();
    }

    private static string GenerateMarkdownMemoryReport(MemoryPatternAnalysis analysis)
    {
        var md = new StringBuilder();

        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "# Memory Analysis: {0}", analysis.KernelName));
        _ = md.AppendLine();
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Status:** {0}", analysis.IsMemorySafe ? "✅ SAFE" : "⚠️ ISSUES DETECTED"));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Analysis Time:** {0:yyyy-MM-dd HH:mm:ss} UTC", analysis.AnalysisTime));
        _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "**Total Input Memory:** {0:N0} bytes", analysis.TotalInputMemory));
        _ = md.AppendLine();

        if (analysis.Issues.Any())
        {
            _ = md.AppendLine("## Memory Issues");
            foreach (var issue in analysis.Issues)
            {
                _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **{0} - {1}:** {2}", issue.Severity, issue.Type, issue.Description));
                if (!string.IsNullOrEmpty(issue.Context))
                {
                    _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "  - Context: {0}", issue.Context));
                }
            }
            _ = md.AppendLine();
        }

        if (analysis.Recommendations.Any())
        {
            _ = md.AppendLine("## Recommendations");
            foreach (var recommendation in analysis.Recommendations)
            {
                _ = md.AppendLine(string.Format(CultureInfo.InvariantCulture, "- {0}", recommendation));
            }
        }

        return md.ToString();
    }

    #endregion

    #region Other Format Generators (HTML, JSON, PlainText)

    private static string GenerateHtmlReport(DebugData debugData)
        // HTML generation implementation
        => string.Format(CultureInfo.InvariantCulture, "<html><body><h1>Debug Report: {0}</h1></body></html>", debugData.KernelName);

    private static string GenerateJsonReport(DebugData debugData) => JsonSerializer.Serialize(debugData, new JsonSerializerOptions { WriteIndented = true });

    private static string GeneratePlainTextReport(DebugData debugData) => string.Format(CultureInfo.InvariantCulture, "Debug Report for {0}\nGenerated: {1}", debugData.KernelName, DateTime.UtcNow);

    private static string GenerateHtmlCrossValidationReport(CrossValidationResult result) => string.Format(CultureInfo.InvariantCulture, "<html><body><h1>Cross-Validation: {0}</h1></body></html>", result.KernelName);

    private static string GenerateJsonCrossValidationReport(CrossValidationResult result) => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

    private static string GeneratePlainTextCrossValidationReport(CrossValidationResult result) => string.Format(CultureInfo.InvariantCulture, "Cross-Validation Report for {0}\nStatus: {1}", result.KernelName, result.IsValid ? "PASSED" : "FAILED");

    private static string GenerateHtmlPerformanceReport(PerformanceAnalysis performance) => string.Format(CultureInfo.InvariantCulture, "<html><body><h1>Performance Analysis: {0}</h1></body></html>", performance.KernelName);

    private static string GenerateJsonPerformanceReport(PerformanceAnalysis performance) => JsonSerializer.Serialize(performance, new JsonSerializerOptions { WriteIndented = true });

    private static string GeneratePlainTextPerformanceReport(PerformanceAnalysis performance) => string.Format(CultureInfo.InvariantCulture, "Performance Analysis for {0}\nData Points: {1}", performance.KernelName, performance.DataPoints);

    private static string GenerateHtmlDeterminismReport(DeterminismTestResult result) => string.Format(CultureInfo.InvariantCulture, "<html><body><h1>Determinism Test: {0}</h1></body></html>", result.KernelName);

    private static string GenerateJsonDeterminismReport(DeterminismTestResult result) => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });

    private static string GeneratePlainTextDeterminismReport(DeterminismTestResult result) => string.Format(CultureInfo.InvariantCulture, "Determinism Test for {0}\nStatus: {1}", result.KernelName, result.IsDeterministic ? "DETERMINISTIC" : "NON-DETERMINISTIC");

    private static string GenerateHtmlMemoryReport(MemoryPatternAnalysis analysis) => string.Format(CultureInfo.InvariantCulture, "<html><body><h1>Memory Analysis: {0}</h1></body></html>", analysis.KernelName);

    private static string GenerateJsonMemoryReport(MemoryPatternAnalysis analysis) => JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });

    private static string GeneratePlainTextMemoryReport(MemoryPatternAnalysis analysis) => string.Format(CultureInfo.InvariantCulture, "Memory Analysis for {0}\nStatus: {1}", analysis.KernelName, analysis.IsMemorySafe ? "SAFE" : "ISSUES DETECTED");
    /// <summary>
    /// Performs dispose.
    /// </summary>

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    #region Logger Messages

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Starting report generation for {KernelName} in {Format} format")]
    private partial void LogReportGenerationStarted(string kernelName, string format);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Report generated for {KernelName}: {ContentLength} characters")]
    private partial void LogReportGenerated(string kernelName, int contentLength);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Starting cross-validation report for {KernelName}")]
    private partial void LogCrossValidationReportStarted(string kernelName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Starting performance report for {KernelName}")]
    private partial void LogPerformanceReportStarted(string kernelName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Starting determinism report for {KernelName}")]
    private partial void LogDeterminismReportStarted(string kernelName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Starting memory analysis report for {KernelName}")]
    private partial void LogMemoryReportStarted(string kernelName);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Report saved for {KernelName} to {FilePath}")]
    private partial void LogReportSaved(string kernelName, string filePath);

    [LoggerMessage(Level = Microsoft.Extensions.Logging.LogLevel.Error, Message = "Failed to save report for {KernelName} to {FilePath}: {Error}")]
    private partial void LogReportSaveFailed(string kernelName, string filePath, string error);

    #endregion
}
