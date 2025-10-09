// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using DotCompute.Abstractions.Debugging;
using DotCompute.Abstractions.Debugging.Types;
using DotCompute.Abstractions.Validation;
using DotCompute.Abstractions.Interfaces.Kernels;
using KernelValidationResult = DotCompute.Abstractions.Debugging.KernelValidationResult;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Debugging;

/// <summary>
/// Handles report generation and metrics for kernel debugging operations.
/// Provides comprehensive reporting capabilities and result comparison.
/// </summary>
internal sealed partial class KernelDebugReporter(ILogger<KernelDebugReporter> logger) : IDisposable
{
    #region LoggerMessage Delegates

    [LoggerMessage(EventId = 16001, Level = MsLogLevel.Information, Message = "Comparing results between {Backend1} and {Backend2}")]
    private static partial void LogResultComparison(ILogger logger, string backend1, string backend2);

    #endregion

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly ILogger<KernelDebugReporter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private DebugServiceOptions _options = new();
    private bool _disposed;
    /// <summary>
    /// Performs configure.
    /// </summary>
    /// <param name="options">The options.</param>

    public void Configure(DebugServiceOptions options) => _options = options ?? throw new ArgumentNullException(nameof(options));
    /// <summary>
    /// Gets compare results asynchronously.
    /// </summary>
    /// <param name="result1">The result1.</param>
    /// <param name="result2">The result2.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The result of the operation.</returns>

    public Task<ResultComparisonReport> CompareResultsAsync(
        KernelExecutionResult result1,
        KernelExecutionResult result2,
        float tolerance = 1e-6f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(result1);
        ArgumentNullException.ThrowIfNull(result2);

        LogResultComparison(_logger, result1.BackendType ?? "Unknown", result2.BackendType ?? "Unknown");

        try
        {
            var report = new ResultComparisonReport
            {
                Result1 = result1,
                Result2 = result2,
                Tolerance = tolerance,
                ComparisonTime = DateTimeOffset.UtcNow
            };

            // Compare execution success
            if (result1.Success != result2.Success)
            {
                report.Issues.Add(new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Error,
                    Category = "Execution Status",
                    Description = "One execution succeeded while the other failed",
                    Details = $"{result1.BackendType}: {(result1.Success ? "Success" : "Failed")} | " +
                             $"{result2.BackendType}: {(result2.Success ? "Success" : "Failed")}"
                });
            }

            // Compare execution times
            var time1 = result1.Timings?.TotalTimeMs ?? 0;
            var time2 = result2.Timings?.TotalTimeMs ?? 0;
            var timeDifference = Math.Abs(time1 - time2);
            var averageTime = (time1 + time2) / 2;

            if (averageTime > 0 && timeDifference / averageTime > 0.5) // >50% difference
            {
                report.Issues.Add(new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Warning,
                    Category = "Performance",
                    Description = "Significant execution time difference detected",
                    Details = $"{result1.BackendType}: {time1:F2}ms | " +
                             $"{result2.BackendType}: {time2:F2}ms"
                });
            }

            // Compare memory usage
            var mem1 = GetMemoryUsage(result1);
            var mem2 = GetMemoryUsage(result2);
            if (mem1 > 0 && mem2 > 0)
            {
                var memoryDifference = Math.Abs(mem1 - mem2);
                var averageMemory = (mem1 + mem2) / 2.0;

                if (memoryDifference / averageMemory > 0.3) // >30% difference
                {
                    report.Issues.Add(new ComparisonIssue
                    {
                        Severity = ComparisonSeverity.Warning,
                        Category = "Memory Usage",
                        Description = "Significant memory usage difference detected",
                        Details = $"{result1.BackendType}: {mem1:N0} bytes | " +
                                 $"{result2.BackendType}: {mem2:N0} bytes"
                    });
                }
            }

            // Compare actual results
            if (result1.Success && result2.Success)
            {
                var resultComparison = CompareResultValues(result1.Output, result2.Output, tolerance);
                if (!resultComparison.IsMatch)
                {
                    report.Issues.Add(new ComparisonIssue
                    {
                        Severity = ComparisonSeverity.Error,
                        Category = "Result Values",
                        Description = "Result values differ between backends",
                        Details = $"Difference: {resultComparison.Difference:F6}, Tolerance: {tolerance:F6}"
                    });

                    // Add to Differences collection (ResultDifference is computed from this)
                    report.Differences.Add(new ResultDifference
                    {
                        Location = "Output",
                        ExpectedValue = result1.Output ?? "null",
                        ActualValue = result2.Output ?? "null",
                        Difference = resultComparison.Difference
                    });
                }

                report.ResultsMatch = resultComparison.IsMatch;
            }
            else
            {
                report.ResultsMatch = false;
                if (!result1.Success || !result2.Success)
                {
                    report.Differences.Add(new ResultDifference
                    {
                        Location = "Success",
                        ExpectedValue = result1.Success,
                        ActualValue = result2.Success,
                        Difference = float.MaxValue
                    });
                }
            }

            // Note: ResultComparisonReport does not have a ComparisonSummary property
            // Summary is generated by GenerateComparisonSummary but not stored in the report
            return Task.FromResult(report);
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Error during result comparison");

            return Task.FromResult(new ResultComparisonReport
            {
                Result1 = result1,
                Result2 = result2,
                Tolerance = tolerance,
                ComparisonTime = DateTimeOffset.UtcNow,
                ResultsMatch = false,
                Issues = [new ComparisonIssue
                {
                    Severity = ComparisonSeverity.Error,
                    Category = "Comparison Error",
                    Description = "Failed to compare results",
                    Details = ex.Message
                }]
            });
        }
    }
    /// <summary>
    /// Gets generate detailed report asynchronously.
    /// </summary>
    /// <param name="validationResult">The validation result.</param>
    /// <returns>The result of the operation.</returns>

    public Task<string> GenerateDetailedReportAsync(KernelValidationResult validationResult)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(validationResult);

        var report = new StringBuilder();

        // Header
        _ = report.AppendLine("=== Kernel Debug Report ===");
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Kernel: {0}", validationResult.KernelName));
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Generated: {0:yyyy-MM-dd HH:mm:ss UTC}", DateTimeOffset.UtcNow));
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Validation Status: {0}", validationResult.IsValid ? "PASSED" : "FAILED"));
        _ = report.AppendLine();

        // Executive Summary
        _ = report.AppendLine("--- Executive Summary ---");
        if (validationResult.IsValid)
        {
            _ = report.AppendLine("✓ All cross-backend validations passed successfully");
            _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "✓ Tested on {0} backend(s): {1}", validationResult.BackendsTested.Count, string.Join(", ", validationResult.BackendsTested)));
        }
        else
        {
            var errorCount = validationResult.Issues.Count(i => i.Severity == ValidationSeverity.Error);
            var warningCount = validationResult.Issues.Count(i => i.Severity == ValidationSeverity.Warning);
            _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "✗ Validation failed with {0} error(s) and {1} warning(s)", errorCount, warningCount));
        }
        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Execution Time: {0:F2}ms", validationResult.ExecutionTime.TotalMilliseconds));
        _ = report.AppendLine();

        // Backend Results
        if (validationResult.Results?.Count > 0)
        {
            _ = report.AppendLine("--- Backend Execution Results ---");
            foreach (var result in validationResult.Results)
            {
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Backend: {0}", result.BackendType));
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Status: {0}", result.Success ? "Success" : "Failed"));
                var execTime = result.Timings?.TotalTimeMs ?? 0;
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Execution Time: {0:F2}ms", execTime));
                var memUsage = GetMemoryUsage(result);
                if (memUsage > 0)
                {
                    _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Memory Usage: {0:N0} bytes", memUsage));
                }
                if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Error: {0}", result.ErrorMessage));
                }
                _ = report.AppendLine();
            }
        }

        // Cross-Backend Comparisons
        if (validationResult.Comparisons?.Count > 0)
        {
            _ = report.AppendLine("--- Cross-Backend Comparisons ---");
            foreach (var comparison in validationResult.Comparisons)
            {
                var status = comparison.IsMatch ? "✓ MATCH" : "✗ DIFFER";
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} vs {1}: {2}", comparison.Backend1, comparison.Backend2, status));
                if (!comparison.IsMatch)
                {
                    _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  Difference: {0:F6}", comparison.Difference));
                }
            }
            _ = report.AppendLine();
        }

        // Issues and Recommendations
        if (validationResult.Issues.Count > 0)
        {
            _ = report.AppendLine("--- Issues Found ---");
            var groupedIssues = validationResult.Issues.GroupBy(i => i.Severity);
            foreach (var group in groupedIssues.OrderBy(g => g.Key))
            {
                _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}s:", group.Key));
                foreach (var issue in group)
                {
                    _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  • {0}", issue.Message));
                    if (issue.Details?.Count > 0)
                    {
                        var detailsText = string.Join(", ", issue.Details.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                        _ = report.AppendLine(string.Format(CultureInfo.InvariantCulture, "    Details: {0}", detailsText));
                    }
                }
                _ = report.AppendLine();
            }
        }

        return Task.FromResult(report.ToString());
    }
    /// <summary>
    /// Gets export report asynchronously.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="format">The format.</param>
    /// <returns>The result of the operation.</returns>

    public async Task<string> ExportReportAsync(object report, ReportFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            return format switch
            {
                ReportFormat.Json => JsonSerializer.Serialize(report, _jsonOptions),
                ReportFormat.Xml => await ExportToXmlAsync(report),
                ReportFormat.Csv => await ExportToCsvAsync(report),
                ReportFormat.PlainText => await ExportToTextAsync(report),
                _ => throw new ArgumentException($"Unsupported report format: {format}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error exporting report in format {format}");
            throw;
        }
    }
    /// <summary>
    /// Gets the available backends async.
    /// </summary>
    /// <returns>The available backends async.</returns>

    public static async Task<IEnumerable<BackendInfo>> GetAvailableBackendsAsync()
    {
        await Task.CompletedTask;

        // This would typically query the actual system for available backends
        // For now, return a static list of common backends
        return new List<BackendInfo>
        {
            new() { Name = "CPU", IsAvailable = true, Version = "1.0", Capabilities = ["SIMD", "MultiThreading"] },
            new() { Name = "CUDA", IsAvailable = false, Version = "Unknown", Capabilities = [] } // Would check actual CUDA availability
        };
    }

    private static (bool IsMatch, float Difference) CompareResultValues(object? result1, object? result2, float tolerance)
    {
        if (result1 == null && result2 == null)
        {
            return (true, 0f);
        }


        if (result1 == null || result2 == null)
        {
            return (false, 1f);
        }


        if (result1.GetType() != result2.GetType())
        {

            return (false, 1.0f);
        }

        // Numeric comparison with tolerance

        if (result1 is float f1 && result2 is float f2)
        {
            var diff = Math.Abs(f1 - f2);
            return (diff <= tolerance, diff);
        }

        if (result1 is double d1 && result2 is double d2)
        {
            var diff = Math.Abs(d1 - d2);
            return (diff <= tolerance, (float)diff);
        }

        // Array comparison
        if (result1 is Array arr1 && result2 is Array arr2)
        {
            if (arr1.Length != arr2.Length)
            {

                return (false, 1.0f);
            }


            var totalDifference = 0f;
            var elementCount = 0;

            for (var i = 0; i < arr1.Length; i++)
            {
                var (isMatch, diff) = CompareResultValues(arr1.GetValue(i), arr2.GetValue(i), tolerance);
                totalDifference += diff;
                elementCount++;

                if (!isMatch && diff > tolerance)
                {

                    return (false, diff);
                }
            }

            var avgDifference = elementCount > 0 ? totalDifference / elementCount : 0f;
            return (avgDifference <= tolerance, avgDifference);
        }

        // Default comparison
        return (result1.Equals(result2), result1.Equals(result2) ? 0f : 0.5f);
    }

    private static string GenerateComparisonSummary(ResultComparisonReport report)
    {
        var summary = new StringBuilder();

        if (report.ResultsMatch)
        {
            _ = summary.Append("Results match");
        }
        else
        {
            _ = summary.Append("Results differ");
            // ResultDifference is a collection, calculate max difference
            if (report.Differences.Count > 0)
            {
                var maxDiff = report.Differences.Max(d => d.Difference);
                if (maxDiff < float.MaxValue)
                {
                    _ = summary.Append(string.Format(CultureInfo.InvariantCulture, " (max difference: {0:F6})", maxDiff));
                }
            }
        }

        var errorCount = report.Issues.Count(i => i.Severity == ComparisonSeverity.Error);
        var warningCount = report.Issues.Count(i => i.Severity == ComparisonSeverity.Warning);

        if (errorCount > 0 || warningCount > 0)
        {
            _ = summary.Append(string.Format(CultureInfo.InvariantCulture, " - {0} error(s), {1} warning(s)", errorCount, warningCount));
        }

        return summary.ToString();
    }

    private static async Task<string> ExportToXmlAsync(object report)
    {
        await Task.CompletedTask;

        // Simplified XML export - in practice, you'd use a proper XML serializer
        return $"<Report>{JsonSerializer.Serialize(report)}</Report>";
    }

    private static async Task<string> ExportToCsvAsync(object report)
    {
        await Task.CompletedTask;

        // Simplified CSV export - in practice, you'd extract specific properties
        if (report is KernelValidationResult validation)
        {
            var csv = new StringBuilder();
            _ = csv.AppendLine("Kernel,Backend,Success,ExecutionTimeMs,MemoryUsage");

            if (validation.Results != null)
            {
                foreach (var result in validation.Results)
                {
                    var execTime = result.Timings?.TotalTimeMs ?? 0;
                    var memUsage = GetMemoryUsage(result);
                    _ = csv.AppendLine(CultureInfo.InvariantCulture, $"{result.KernelName},{result.BackendType},{result.Success}," +
                                 $"{execTime},{memUsage}");
                }
            }

            return csv.ToString();
        }

        return "CSV export not supported for this report type";
    }

    private async Task<string> ExportToTextAsync(object report)
    {
        if (report is KernelValidationResult validation)
        {
            return await GenerateDetailedReportAsync(validation);
        }

        return report.ToString() ?? "No data available";
    }

    /// <summary>
    /// Gets the memory usage from a kernel execution result.
    /// </summary>
    /// <param name="result">The kernel execution result.</param>
    /// <returns>Memory usage in bytes, or 0 if not available.</returns>
    private static long GetMemoryUsage(KernelExecutionResult result)
    {
        if (result.PerformanceCounters?.TryGetValue("MemoryUsage", out var memory) == true)
        {
            return memory is long l ? l : 0;
        }
        return 0;
    }

    /// <summary>
    /// Performs dispose.
    /// </summary>

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
