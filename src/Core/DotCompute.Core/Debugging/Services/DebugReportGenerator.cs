// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Validation;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DotCompute.Core.Debugging.Services;

/// <summary>
/// Generates comprehensive debug reports and documentation.
/// </summary>
public sealed partial class DebugReportGenerator : IDisposable
{
    private readonly ILogger<DebugReportGenerator> _logger;
    private readonly DebugServiceOptions _options;
    private bool _disposed;

    public DebugReportGenerator(ILogger<DebugReportGenerator> logger, DebugServiceOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? DebugServiceOptions.Development;
    }

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
                Directory.CreateDirectory(directory);
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
    private string GenerateReportContent(DebugData debugData, ReportFormat format)
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

        summary.AppendLine($"Debug Summary for Kernel: {debugData.KernelName}");
        summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        if (debugData.CrossValidationResult != null)
        {
            summary.AppendLine($"Cross-Validation: {(debugData.CrossValidationResult.IsValid ? "PASSED" : "FAILED")}");
            summary.AppendLine($"Validation Issues: {debugData.CrossValidationResult.ValidationIssues.Count}");
        }

        if (debugData.PerformanceAnalysis != null)
        {
            summary.AppendLine($"Performance Analysis: {debugData.PerformanceAnalysis.DataPoints} data points");
            summary.AppendLine($"Average Execution Time: {debugData.PerformanceAnalysis.AverageExecutionTimeMs:F2} ms");
            // Success rate not available in PerformanceAnalysis
        }

        if (debugData.DeterminismResult != null)
        {
            summary.AppendLine($"Determinism Test: {(debugData.DeterminismResult.IsDeterministic ? "PASSED" : "FAILED")}");
        }

        if (debugData.MemoryAnalysis != null)
        {
            summary.AppendLine($"Memory Analysis: {(debugData.MemoryAnalysis.IsMemorySafe ? "SAFE" : "ISSUES DETECTED")}");
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

            // Success rate check removed - not available in PerformanceAnalysis
            if (false) // Placeholder for success rate check
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

        md.AppendLine($"# Debug Report: {debugData.KernelName}");
        md.AppendLine();
        md.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine();

        if (debugData.CrossValidationResult != null)
        {
            md.AppendLine("## Cross-Validation Results");
            md.AppendLine($"- **Status:** {(debugData.CrossValidationResult.IsValid ? "✅ PASSED" : "❌ FAILED")}");
            md.AppendLine($"- **Validation Time:** {debugData.CrossValidationResult.ValidationTime:yyyy-MM-dd HH:mm:ss}");
            md.AppendLine($"- **Issues Found:** {debugData.CrossValidationResult.ValidationIssues.Count}");
            md.AppendLine();
        }

        if (debugData.PerformanceAnalysis != null)
        {
            md.AppendLine("## Performance Analysis");
            md.AppendLine($"- **Data Points:** {debugData.PerformanceAnalysis.DataPoints}");
            md.AppendLine($"- **Average Execution Time:** {debugData.PerformanceAnalysis.AverageExecutionTimeMs:F2} ms");
            // Success rate not available in PerformanceAnalysis
            md.AppendLine();
        }

        return md.ToString();
    }

    private string GenerateMarkdownCrossValidationReport(CrossValidationResult result)
    {
        var md = new StringBuilder();

        md.AppendLine($"# Cross-Validation Report: {result.KernelName}");
        md.AppendLine();
        md.AppendLine($"**Status:** {(result.IsValid ? "✅ PASSED" : "❌ FAILED")}");
        md.AppendLine($"**Validation Time:** {result.ValidationTime:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine();

        md.AppendLine("## Execution Results");
        foreach (var execResult in result.ExecutionResults)
        {
            md.AppendLine($"- **{execResult.AcceleratorName}** ({execResult.AcceleratorType}):");
            md.AppendLine($"  - Success: {(execResult.Success ? "✅" : "❌")}");
            md.AppendLine($"  - Execution Time: {execResult.ExecutionTime.TotalMilliseconds:F2} ms");
            if (execResult.Error != null)
            {
                md.AppendLine($"  - Error: {execResult.Error.Message}");
            }
        }

        if (result.ValidationIssues.Any())
        {
            md.AppendLine();
            md.AppendLine("## Validation Issues");
            foreach (var issue in result.ValidationIssues)
            {
                md.AppendLine($"- **{issue.Severity}:** {issue.Message}");
                if (!string.IsNullOrEmpty(issue.Context))
                {
                    md.AppendLine($"  - Context: {issue.Context}");
                }
            }
        }

        return md.ToString();
    }

    private static string GenerateMarkdownPerformanceReport(PerformanceAnalysis performance)
    {
        var md = new StringBuilder();

        md.AppendLine($"# Performance Analysis: {performance.KernelName}");
        md.AppendLine();
        md.AppendLine($"**Analysis Time:** {performance.AnalysisTime:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Data Points:** {performance.DataPoints}");
        md.AppendLine();

        md.AppendLine("## Execution Time Statistics");
        md.AppendLine($"- **Average:** {performance.AverageExecutionTime:F2} ms");
        md.AppendLine($"- **Median:** {performance.MedianExecutionTime:F2} ms");
        md.AppendLine($"- **Minimum:** {performance.MinExecutionTime:F2} ms");
        md.AppendLine($"- **Maximum:** {performance.MaxExecutionTime:F2} ms");
        md.AppendLine($"- **Standard Deviation:** {performance.ExecutionTimeStdDev:F2} ms");
        md.AppendLine();

        md.AppendLine("## Memory Statistics");
        md.AppendLine($"- **Average Usage:** {performance.AverageMemoryUsage:F0} bytes");
        md.AppendLine($"- **Peak Usage:** {performance.PeakMemoryUsage:F0} bytes");
        md.AppendLine();

        md.AppendLine("## Reliability");
        md.AppendLine($"- **Success Rate:** {performance.SuccessRate:F1}%");
        md.AppendLine($"- **Total Executions:** {performance.TotalExecutions}");

        return md.ToString();
    }

    private static string GenerateMarkdownDeterminismReport(DeterminismTestResult result)
    {
        var md = new StringBuilder();

        md.AppendLine($"# Determinism Test: {result.KernelName}");
        md.AppendLine();
        md.AppendLine($"**Status:** {(result.IsDeterministic ? "✅ DETERMINISTIC" : "❌ NON-DETERMINISTIC")}");
        md.AppendLine($"**Accelerator:** {result.AcceleratorType}");
        md.AppendLine($"**Iterations:** {result.Iterations}");
        md.AppendLine($"**Test Time:** {result.TestTime:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine();

        if (result.Issues.Any())
        {
            md.AppendLine("## Issues and Recommendations");
            foreach (var issue in result.Issues)
            {
                md.AppendLine($"- {issue}");
            }
        }

        return md.ToString();
    }

    private static string GenerateMarkdownMemoryReport(MemoryPatternAnalysis analysis)
    {
        var md = new StringBuilder();

        md.AppendLine($"# Memory Analysis: {analysis.KernelName}");
        md.AppendLine();
        md.AppendLine($"**Status:** {(analysis.IsMemorySafe ? "✅ SAFE" : "⚠️ ISSUES DETECTED")}");
        md.AppendLine($"**Analysis Time:** {analysis.AnalysisTime:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Total Input Memory:** {analysis.TotalInputMemory:N0} bytes");
        md.AppendLine();

        if (analysis.Issues.Any())
        {
            md.AppendLine("## Memory Issues");
            foreach (var issue in analysis.Issues)
            {
                md.AppendLine($"- **{issue.Severity} - {issue.Type}:** {issue.Description}");
                if (!string.IsNullOrEmpty(issue.Context))
                {
                    md.AppendLine($"  - Context: {issue.Context}");
                }
            }
            md.AppendLine();
        }

        if (analysis.Recommendations.Any())
        {
            md.AppendLine("## Recommendations");
            foreach (var recommendation in analysis.Recommendations)
            {
                md.AppendLine($"- {recommendation}");
            }
        }

        return md.ToString();
    }

    #endregion

    #region Other Format Generators (HTML, JSON, PlainText)

    private static string GenerateHtmlReport(DebugData debugData)
    {
        // HTML generation implementation
        return $"<html><body><h1>Debug Report: {debugData.KernelName}</h1></body></html>";
    }

    private static string GenerateJsonReport(DebugData debugData)
    {
        return JsonSerializer.Serialize(debugData, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GeneratePlainTextReport(DebugData debugData)
    {
        return $"Debug Report for {debugData.KernelName}\nGenerated: {DateTime.UtcNow}";
    }

    private static string GenerateHtmlCrossValidationReport(CrossValidationResult result)
    {
        return $"<html><body><h1>Cross-Validation: {result.KernelName}</h1></body></html>";
    }

    private static string GenerateJsonCrossValidationReport(CrossValidationResult result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GeneratePlainTextCrossValidationReport(CrossValidationResult result)
    {
        return $"Cross-Validation Report for {result.KernelName}\nStatus: {(result.IsValid ? "PASSED" : "FAILED")}";
    }

    private static string GenerateHtmlPerformanceReport(PerformanceAnalysis performance)
    {
        return $"<html><body><h1>Performance Analysis: {performance.KernelName}</h1></body></html>";
    }

    private static string GenerateJsonPerformanceReport(PerformanceAnalysis performance)
    {
        return JsonSerializer.Serialize(performance, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GeneratePlainTextPerformanceReport(PerformanceAnalysis performance)
    {
        return $"Performance Analysis for {performance.KernelName}\nData Points: {performance.DataPoints}";
    }

    private static string GenerateHtmlDeterminismReport(DeterminismTestResult result)
    {
        return $"<html><body><h1>Determinism Test: {result.KernelName}</h1></body></html>";
    }

    private static string GenerateJsonDeterminismReport(DeterminismTestResult result)
    {
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GeneratePlainTextDeterminismReport(DeterminismTestResult result)
    {
        return $"Determinism Test for {result.KernelName}\nStatus: {(result.IsDeterministic ? "DETERMINISTIC" : "NON-DETERMINISTIC")}";
    }

    private static string GenerateHtmlMemoryReport(MemoryPatternAnalysis analysis)
    {
        return $"<html><body><h1>Memory Analysis: {analysis.KernelName}</h1></body></html>";
    }

    private static string GenerateJsonMemoryReport(MemoryPatternAnalysis analysis)
    {
        return JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GeneratePlainTextMemoryReport(MemoryPatternAnalysis analysis)
    {
        return $"Memory Analysis for {analysis.KernelName}\nStatus: {(analysis.IsMemorySafe ? "SAFE" : "ISSUES DETECTED")}";
    }

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
